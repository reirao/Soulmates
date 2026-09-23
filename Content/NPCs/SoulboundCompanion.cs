using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Soulmates.Common;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulmates.Content.NPCs;

public sealed class SoulboundCompanion : ModNPC
{
	private enum BrainState
	{
		Follow,
		Idle,
		Wander,
		Inspect,
		CatchUp,
		Stay
	}

	private const float FollowCommand = 0f;
	private const float StayCommand = 1f;
	private BrainState brainState;
	private int stateTimer;
	private int facing = 1;
	private int facingCooldown;
	private Vector2 idleTarget;
	private float bobSeed;

	public CompanionProfile Profile { get; set; } = new();
	private Player Owner => Main.player[(int)NPC.ai[0]];
	private ref float Command => ref NPC.ai[1];
	public string CommandName => Command == StayCommand ? "Stay" : "Follow";

	public override void SetStaticDefaults()
	{
		Main.npcFrameCount[Type] = 1;
	}

	public override void SetDefaults()
	{
		NPC.width = 42;
		NPC.height = 46;
		NPC.lifeMax = 250;
		NPC.damage = 0;
		NPC.defense = 10;
		NPC.friendly = true;
		NPC.noGravity = true;
		NPC.noTileCollide = true;
		NPC.dontTakeDamage = true;
		NPC.netAlways = true;
		bobSeed = Main.rand.NextFloat(MathHelper.TwoPi);
	}

	public override bool CheckActive() => !Owner.active || Owner.dead;

	public override void AI()
	{
		if (!Owner.active || Owner.dead) {
			NPC.active = false;
			return;
		}

		NPC.GivenName = Profile.Name;
		Lighting.AddLight(NPC.Center, Profile.EssenceColor.ToVector3() * 0.42f);
		UpdateAuraDust();

		if (Command == StayCommand) {
			if (brainState != BrainState.Stay) {
				brainState = BrainState.Stay;
				idleTarget = NPC.Center;
			}
			MoveTo(idleTarget + new Vector2(0f, IdleBob()), 2.2f, 0.04f);
			UpdateFacing();
			return;
		}

		Vector2 followTarget = Owner.Center + new Vector2(-Owner.direction * 66f, -58f);
		float distance = Vector2.Distance(NPC.Center, followTarget);
		if (distance > 1200f) {
			NPC.Center = followTarget;
			NPC.velocity = Vector2.Zero;
			brainState = BrainState.Follow;
			stateTimer = 60;
			NPC.netUpdate = true;
			return;
		}

		if (distance > 340f) {
			brainState = BrainState.CatchUp;
			stateTimer = 45;
		}

		if (stateTimer-- <= 0)
			ChooseNextState(distance, followTarget);

		switch (brainState) {
			case BrainState.Idle:
				MoveTo(followTarget + new Vector2(0f, IdleBob()), 2.4f, 0.035f);
				break;
			case BrainState.Wander:
				MoveTo(idleTarget + new Vector2(0f, IdleBob() * 0.5f), 4.2f, 0.055f);
				break;
			case BrainState.Inspect:
				Vector2 inspectOffset = new Vector2(MathF.Sin((stateTimer + bobSeed) * 0.035f) * 26f, -82f + IdleBob());
				MoveTo(Owner.Center + inspectOffset, 3.4f, 0.045f);
				break;
			case BrainState.CatchUp:
				MoveTo(followTarget, 14f, 0.13f);
				break;
			default:
				MoveTo(followTarget, 8f, 0.075f);
				break;
		}

		NPC.rotation = MathHelper.Lerp(NPC.rotation, NPC.velocity.X * 0.025f, 0.08f);
		UpdateFacing();
	}

	private void ChooseNextState(float distance, Vector2 followTarget)
	{
		if (distance > 170f || Owner.velocity.LengthSquared() > 9f) {
			brainState = BrainState.Follow;
			stateTimer = 70;
			return;
		}

		int curiosity = Profile.Personality switch {
			CompanionPersonality.Curious => 55,
			CompanionPersonality.Mischievous => 45,
			CompanionPersonality.Brave => 30,
			_ => 20
		};
		int roll = Main.rand.Next(100);
		if (roll < curiosity) {
			brainState = Main.rand.NextBool() ? BrainState.Wander : BrainState.Inspect;
			idleTarget = followTarget + Main.rand.NextVector2Circular(90f, 44f);
			stateTimer = Main.rand.Next(100, 220);
		}
		else {
			brainState = BrainState.Idle;
			stateTimer = Profile.Personality == CompanionPersonality.Gentle ? Main.rand.Next(220, 380) : Main.rand.Next(140, 280);
		}
	}

	private void MoveTo(Vector2 target, float maxSpeed, float responsiveness)
	{
		Vector2 offset = target - NPC.Center;
		float speed = MathHelper.Clamp(offset.Length() / 16f, 0f, maxSpeed);
		Vector2 desired = offset.SafeNormalize(Vector2.Zero) * speed;
		NPC.velocity = Vector2.Lerp(NPC.velocity, desired, responsiveness);
		if (offset.LengthSquared() < 64f)
			NPC.velocity *= 0.94f;
	}

	private float IdleBob() => MathF.Sin(Main.GlobalTimeWrappedHourly * 2.2f + bobSeed) * 7f;

	private void UpdateFacing()
	{
		if (facingCooldown > 0)
			facingCooldown--;

		if (MathF.Abs(NPC.velocity.X) > 0.55f && facingCooldown == 0) {
			int desired = NPC.velocity.X < 0f ? -1 : 1;
			if (desired != facing) {
				facing = desired;
				facingCooldown = 24;
			}
		}
		NPC.spriteDirection = facing;
	}

	private void UpdateAuraDust()
	{
		int chance = Profile.Aura switch {
			CompanionAura.SoulSparks => 5,
			CompanionAura.OrbitingStars => 8,
			_ => 14
		};
		if (!Main.rand.NextBool(chance))
			return;

		Dust dust = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(24f, 24f), DustID.Enchanted_Gold,
			-NPC.velocity * 0.08f, 120, Profile.EssenceColor, Profile.Aura == CompanionAura.SoftGlow ? 0.55f : 0.8f);
		dust.noGravity = true;
	}

	public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
	{
		Texture2D texture = ModContent.Request<Texture2D>(Texture, AssetRequestMode.ImmediateLoad).Value;
		Vector2 center = NPC.Center - screenPos + new Vector2(0f, IdleBob() * 0.18f);
		Vector2 origin = texture.Size() * 0.5f;
		float breath = 1f + MathF.Sin(Main.GlobalTimeWrappedHourly * 2f + bobSeed) * 0.025f;
		Vector2 formScale = Profile.Form switch {
			CompanionForm.Round => new Vector2(1.12f, 0.92f),
			CompanionForm.Wisp => new Vector2(0.88f, 1.14f),
			_ => Vector2.One
		};
		Vector2 scale = formScale * (76f / texture.Width) * breath;
		SpriteEffects effects = facing < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
		Color tint = Color.Lerp(Color.White, Profile.EssenceColor, 0.34f);

		for (int i = 0; i < 4; i++) {
			Vector2 glowOffset = new Vector2(2f, 0f).RotatedBy(MathHelper.PiOver2 * i);
			spriteBatch.Draw(texture, center + glowOffset, null, Profile.EssenceColor * 0.18f, NPC.rotation, origin, scale, effects, 0f);
		}
		spriteBatch.Draw(texture, center, null, tint, NPC.rotation, origin, scale, effects, 0f);
		return false;
	}

	public override Color? GetAlpha(Color drawColor) => Color.Lerp(drawColor, Profile.EssenceColor, 0.42f);
	public override void SendExtraAI(BinaryWriter writer) => Profile.Write(writer);
	public override void ReceiveExtraAI(BinaryReader reader) => Profile = CompanionProfile.Read(reader);

	public void ToggleCommand()
	{
		Command = Command == StayCommand ? FollowCommand : StayCommand;
		brainState = Command == StayCommand ? BrainState.Stay : BrainState.Follow;
		stateTimer = 1;
		if (Command == StayCommand)
			idleTarget = NPC.Center;
		NPC.netUpdate = true;
	}

	public void Recall()
	{
		if (Owner.active)
			Owner.GetModPlayer<SoulmatesPlayer>().ActiveCompanionWhoAmI = -1;
		NPC.active = false;
		NPC.netUpdate = true;
	}

	public static SoulboundCompanion? FindFor(Player player)
	{
		for (int i = 0; i < Main.maxNPCs; i++) {
			NPC npc = Main.npc[i];
			if (npc.active && npc.type == ModContent.NPCType<SoulboundCompanion>() && (int)npc.ai[0] == player.whoAmI)
				return npc.ModNPC as SoulboundCompanion;
		}
		return null;
	}
}
