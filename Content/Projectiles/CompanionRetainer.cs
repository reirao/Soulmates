#nullable enable
using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Soulmates.Common;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulmates.Content.Projectiles;

public sealed class CompanionRetainer : ModProjectile
{
	private Guid companionId;
	private int bindingWait;
	private int attackCooldown;
	private bool ready;
	private NPC? attackTarget;
	public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.BabySlime}";
	internal void Bind(Guid id) => companionId = id;
	internal bool BelongsTo(SoulboundCompanion companion) => companionId != Guid.Empty
		&& companionId == companion.Profile.Id && Projectile.ai[0] == companion.NPC.whoAmI;
	internal static SoulboundCompanion? Resolve(Projectile projectile, Guid id)
	{
		int index = (int)projectile.ai[0];
		return id != Guid.Empty && index >= 0 && index < Main.maxNPCs && Main.npc[index].active
			&& Main.npc[index].ModNPC is SoulboundCompanion companion && companion.Profile.Id == id
			&& companion.CanMaintainRetinue && CompanionRetinue.IsActive(companion.Profile, (int)projectile.ai[1])
			? companion : null;
	}
	public override void SetDefaults()
	{
		Projectile.width = Projectile.height = 24;
		Projectile.aiStyle = -1;
		Projectile.friendly = true;
		Projectile.npcProj = true;
		Projectile.DamageType = DamageClass.Generic;
		Projectile.tileCollide = false;
		Projectile.ignoreWater = true;
		Projectile.penetrate = -1;
		Projectile.timeLeft = 60;
		Projectile.netImportant = true;
		Projectile.usesLocalNPCImmunity = true;
		Projectile.localNPCHitCooldown = 45;
	}
	public override void OnSpawn(IEntitySource source)
	{
		if (source is EntitySource_Parent { Entity: NPC { ModNPC: SoulboundCompanion companion } }) Bind(companion.Profile.Id);
	}
	public override void AI()
	{
		ready = false;
		attackTarget = null;
		SoulboundCompanion? companion = Resolve(Projectile, companionId);
		if (companion is null) {
			if (Main.netMode == NetmodeID.MultiplayerClient && ++bindingWait < 90) { Projectile.timeLeft = 2; return; }
			Projectile.Kill(); return;
		}
		bindingWait = 0;
		ready = true;
		Projectile.timeLeft = 2;
		Item[] active = CompanionRetinue.ActiveItems(companion.Profile).ToArray();
		int slot = Array.FindIndex(active, item => item.type == (int)Projectile.ai[1]);
		if (slot < 0) { Projectile.Kill(); return; }
		Item item = active[slot];
		Projectile.damage = CompanionRetinue.Damage(companion.Profile, item);
		CompanionRetinueStyle style = CompanionRetinue.Style(item);
		float age = ++Projectile.localAI[0];
		float angle = MathHelper.TwoPi * slot / Math.Max(1, active.Length) + age / 240f;
		float radius = 48f + Math.Min(96f, active.Length * 4f);
		Vector2 goal = companion.NPC.Center + angle.ToRotationVector2() * radius + new Vector2(0, -24f);
		NPC? target = companion.RetinueTarget;
		bool visibleTarget = target is not null && Collision.CanHitLine(Projectile.position, Projectile.width,
			Projectile.height, target.position, target.width, target.height);
		if (CompanionInventorySync.IsPending(Main.player[(int)companion.NPC.ai[0]])) target = null;
		if (style == CompanionRetinueStyle.Sentry)
			goal = companion.RetinueAnchor + new Vector2((slot % 5 - 2) * 36f, -12f - slot / 5 * 28f);
		if (target is not null && visibleTarget) {
			if (style == CompanionRetinueStyle.Melee) { goal = target.Center; attackTarget = target; }
			else if (Main.netMode != NetmodeID.MultiplayerClient && --attackCooldown <= 0) {
				attackCooldown = 70 + slot % 5 * 7;
				int index = Projectile.NewProjectile(companion.NPC.GetSource_FromAI(), Projectile.Center,
					(target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 9f,
					ModContent.ProjectileType<CompanionRetinueBolt>(), Projectile.damage, item.knockBack,
					Main.myPlayer,
					companion.NPC.whoAmI, item.type, target.whoAmI);
				if (index >= 0 && index < Main.maxProjectiles && Main.projectile[index].ModProjectile is CompanionRetinueBolt bolt) {
					bolt.Bind(companionId, target);
					Main.projectile[index].netUpdate = true;
				}
			}
		}
		Vector2 offset = goal - Projectile.Center;
		if (offset.LengthSquared() > 900f * 900f) {
			Projectile.Center = companion.NPC.Center;
			Projectile.velocity = Vector2.Zero;
			if (Main.netMode != NetmodeID.MultiplayerClient) Projectile.netUpdate = true;
		}
		else Projectile.velocity = Vector2.Lerp(Projectile.velocity, offset.SafeNormalize(Vector2.Zero)
			* Math.Min(attackTarget is null ? 8f : 11f, offset.Length() / 8f), 0.14f);
		if (target is not null && MathF.Abs(target.Center.X - Projectile.Center.X) > 2f)
			Projectile.spriteDirection = target.Center.X > Projectile.Center.X ? -1 : 1;
		else if (MathF.Abs(Projectile.velocity.X) > 0.35f) Projectile.spriteDirection = Projectile.velocity.X > 0 ? -1 : 1;
		int visual = CompanionRetinue.VisualFor(item.type);
		if (++Projectile.frameCounter >= 12) {
			Projectile.frameCounter = 0;
			Projectile.frame = (Projectile.frame + 1) % Math.Max(1, Main.projFrames[visual]);
		}
	}
	public override bool? CanDamage() => ready && attackTarget is not null && Main.netMode != NetmodeID.MultiplayerClient;
	public override bool? CanHitNPC(NPC target) => !target.friendly && !CompanionCritters.IsCritter(target)
		&& target == attackTarget && Resolve(Projectile, companionId)?.RetinueTarget == target;
	public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => CreditDefeat(Resolve(Projectile, companionId), target);
	internal static void CreditDefeat(SoulboundCompanion? companion, NPC target)
	{
		if (companion is null || Main.netMode == NetmodeID.MultiplayerClient) return;
		int owner = (int)companion.NPC.ai[0];
		if (owner >= 0 && owner < Main.maxPlayers) target.playerInteraction[owner] = true;
		if (target.life <= 0) companion.RecordDefeat(target);
	}
	public override bool PreDraw(ref Color lightColor)
	{
		if (!ready) return false;
		int visual = CompanionRetinue.VisualFor((int)Projectile.ai[1]);
		if (visual <= 0) return false;
		Main.instance.LoadProjectile(visual);
		Texture2D texture = TextureAssets.Projectile[visual].Value;
		Rectangle frame = texture.Frame(1, Math.Max(1, Main.projFrames[visual]), 0, Projectile.frame % Math.Max(1, Main.projFrames[visual]));
		Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, frame, lightColor, 0f,
			frame.Size() * 0.5f, Math.Min(1f, 64f / Math.Max(frame.Width, frame.Height)),
			Projectile.spriteDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
		return false;
	}
	public override void SendExtraAI(BinaryWriter writer) => writer.Write(companionId.ToByteArray());
	public override void ReceiveExtraAI(BinaryReader reader) => companionId = ReadBinding(reader);
	internal static Guid ReadBinding(BinaryReader reader)
	{
		byte[] bytes = reader.ReadBytes(16);
		if (bytes.Length != 16 || new Guid(bytes) == Guid.Empty) throw new InvalidDataException("Invalid retinue binding.");
		return new Guid(bytes);
	}
}

public sealed class CompanionRetinueBolt : ModProjectile
{
	private Guid companionId;
	private NPC? capturedTarget;
	public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.RainbowRodBullet}";
	internal void Bind(Guid id, NPC target) { companionId = id; capturedTarget = target; }
	internal bool BelongsTo(SoulboundCompanion companion) => companionId == companion.Profile.Id && Projectile.ai[0] == companion.NPC.whoAmI;
	public override void OnSpawn(IEntitySource source)
	{
		int target = (int)Projectile.ai[2];
		if (source is EntitySource_Parent { Entity: NPC { ModNPC: SoulboundCompanion companion } }
			&& target >= 0 && target < Main.maxNPCs) Bind(companion.Profile.Id, Main.npc[target]);
	}
	public override void SetDefaults()
	{
		Projectile.width = Projectile.height = 12;
		Projectile.friendly = true;
		Projectile.npcProj = true;
		Projectile.DamageType = DamageClass.Generic;
		Projectile.tileCollide = true;
		Projectile.penetrate = 1;
		Projectile.timeLeft = 90;
		Projectile.netImportant = true;
	}
	public override void AI()
	{
		SoulboundCompanion? companion = CompanionRetainer.Resolve(Projectile, companionId);
		if (companion is null || companion.RetinueTarget is not { } target || target.whoAmI != (int)Projectile.ai[2]
			|| Main.netMode != NetmodeID.MultiplayerClient && target != capturedTarget) { Projectile.Kill(); return; }
		Projectile.rotation += 0.12f;
		Lighting.AddLight(Projectile.Center, companion.Profile.EssenceColor.ToVector3() * 0.4f);
	}
	public override bool? CanDamage() => Main.netMode != NetmodeID.MultiplayerClient;
	public override bool? CanHitNPC(NPC target) => !target.friendly && !CompanionCritters.IsCritter(target) && target == capturedTarget
		&& CompanionRetainer.Resolve(Projectile, companionId)?.RetinueTarget == target;
	public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		=> CompanionRetainer.CreditDefeat(CompanionRetainer.Resolve(Projectile, companionId), target);
	public override void SendExtraAI(BinaryWriter writer) => writer.Write(companionId.ToByteArray());
	public override void ReceiveExtraAI(BinaryReader reader) => companionId = CompanionRetainer.ReadBinding(reader);
}
