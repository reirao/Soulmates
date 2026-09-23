using System.IO;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulmates.Content.NPCs;

public sealed class SoulboundCompanion : ModNPC
{
	private const float FollowCommand = 0f;
	private const float StayCommand = 1f;

	public CompanionProfile Profile { get; set; } = new();
	private Player Owner => Main.player[(int)NPC.ai[0]];
	private ref float Command => ref NPC.ai[1];
	public string CommandName => Command == StayCommand ? "Stay" : "Follow";

	public override string Texture => $"Terraria/Images/NPC_{NPCID.Bunny}";

	public override void SetStaticDefaults()
	{
		Main.npcFrameCount[Type] = Main.npcFrameCount[NPCID.Bunny];
	}

	public override void SetDefaults()
	{
		NPC.width = 28;
		NPC.height = 30;
		NPC.lifeMax = 250;
		NPC.damage = 0;
		NPC.defense = 10;
		NPC.friendly = true;
		NPC.noGravity = true;
		NPC.noTileCollide = true;
		NPC.dontTakeDamage = true;
		NPC.netAlways = true;
	}

	public override bool CheckActive() => !Owner.active || Owner.dead;

	public override void AI()
	{
		if (!Owner.active || Owner.dead) {
			NPC.active = false;
			return;
		}

		NPC.GivenName = Profile.Name;
		Lighting.AddLight(NPC.Center, Profile.EssenceColor.ToVector3() * 0.35f);
		Dust dust = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(14f, 14f), DustID.Enchanted_Gold,
			-NPC.velocity * 0.1f, 120, Profile.EssenceColor, 0.7f);
		dust.noGravity = true;

		if (Command == StayCommand) {
			NPC.velocity *= 0.9f;
			NPC.rotation = NPC.velocity.X * 0.03f;
			return;
		}

		Vector2 target = Owner.Center + new Vector2(-Owner.direction * 52f, -54f);
		Vector2 offset = target - NPC.Center;
		if (offset.LengthSquared() > 1200f * 1200f) {
			NPC.Center = target;
			NPC.velocity = Vector2.Zero;
			NPC.netUpdate = true;
			return;
		}

		float speed = MathHelper.Clamp(offset.Length() / 18f, 3f, 12f);
		Vector2 desired = offset.SafeNormalize(Vector2.Zero) * speed;
		NPC.velocity = Vector2.Lerp(NPC.velocity, desired, 0.08f);
		NPC.rotation = NPC.velocity.X * 0.035f;
		NPC.spriteDirection = NPC.velocity.X < 0f ? -1 : 1;
	}

	public override Color? GetAlpha(Color drawColor) => Color.Lerp(drawColor, Profile.EssenceColor, 0.42f);

	public override void SendExtraAI(BinaryWriter writer) => Profile.Write(writer);

	public override void ReceiveExtraAI(BinaryReader reader) => Profile = CompanionProfile.Read(reader);

	public void ToggleCommand()
	{
		Command = Command == StayCommand ? FollowCommand : StayCommand;
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
