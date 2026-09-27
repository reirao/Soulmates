using System;
using Microsoft.Xna.Framework;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulmates.Content.Projectiles;

public sealed class SoulBolt : ModProjectile
{
	public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.RainbowRodBullet}";

	public override void SetDefaults()
	{
		Projectile.width = 12;
		Projectile.height = 12;
		Projectile.friendly = true;
		Projectile.hostile = false;
		Projectile.DamageType = DamageClass.Summon;
		Projectile.penetrate = 1;
		Projectile.timeLeft = 120;
		Projectile.tileCollide = false;
		Projectile.ignoreWater = true;
		Projectile.usesLocalNPCImmunity = true;
		Projectile.localNPCHitCooldown = 20;
	}

	public override void AI()
	{
		int targetIndex = (int)Projectile.ai[0];
		if (targetIndex >= 0 && targetIndex < Main.maxNPCs) {
			NPC target = Main.npc[targetIndex];
			if (target.active && target.CanBeChasedBy(Projectile)) {
				Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Projectile.velocity) * 10f;
				Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.085f);
			}
		}

		Projectile.rotation += 0.22f * Math.Sign(Projectile.velocity.X == 0f ? 1f : Projectile.velocity.X);
		Color color = GetCompanionColor();
		Lighting.AddLight(Projectile.Center, color.ToVector3() * 0.45f);
		if (Main.rand.NextBool(2)) {
			Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Enchanted_Gold, -Projectile.velocity * 0.08f, 120, color, 0.72f);
			dust.noGravity = true;
		}
	}

	public override Color? GetAlpha(Color lightColor) => GetCompanionColor() * 0.92f;

	public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
	{
		if (target.life > 0)
			return;
		int companionIndex = (int)Projectile.ai[1];
		if (companionIndex < 0 || companionIndex >= Main.maxNPCs)
			return;
		NPC companionNpc = Main.npc[companionIndex];
		if (companionNpc.active && companionNpc.ModNPC is SoulboundCompanion companion)
			companion.RecordGuardianVictory(target);
	}

	private Color GetCompanionColor()
	{
		int companionIndex = (int)Projectile.ai[1];
		if (companionIndex >= 0 && companionIndex < Main.maxNPCs
			&& Main.npc[companionIndex].active
			&& Main.npc[companionIndex].ModNPC is SoulboundCompanion companion)
			return companion.Profile.EssenceColor;
		return new Color(255, 235, 145);
	}
}
