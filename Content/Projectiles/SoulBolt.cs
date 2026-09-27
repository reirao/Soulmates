using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulmates.Content.Projectiles;

public sealed class SoulBolt : ModProjectile
{
	public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.RainbowRodBullet}";

	public override void SetStaticDefaults()
	{
		ProjectileID.Sets.TrailCacheLength[Type] = 10;
		ProjectileID.Sets.TrailingMode[Type] = 0;
	}

	public override void SetDefaults()
	{
		Projectile.width = 18;
		Projectile.height = 18;
		Projectile.friendly = true;
		Projectile.hostile = false;
		Projectile.DamageType = DamageClass.Summon;
		Projectile.penetrate = 1;
		Projectile.timeLeft = 180;
		Projectile.tileCollide = false;
		Projectile.ignoreWater = true;
		Projectile.netImportant = true;
		Projectile.scale = 1.2f;
		Projectile.usesLocalNPCImmunity = true;
		Projectile.localNPCHitCooldown = 20;
	}

	public override void AI()
	{
		int targetIndex = (int)Projectile.ai[0];
		if (targetIndex >= 0 && targetIndex < Main.maxNPCs) {
			NPC target = Main.npc[targetIndex];
			if (target.active && target.CanBeChasedBy(Projectile)) {
				Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Projectile.velocity) * 9f;
				Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.13f);
			}
		}

		Projectile.rotation += 0.22f * Math.Sign(Projectile.velocity.X == 0f ? 1f : Projectile.velocity.X);
		Color color = GetCompanionColor();
		Lighting.AddLight(Projectile.Center, color.ToVector3() * 0.8f);
		for (int i = 0; i < 2; i++) {
			Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(5f, 5f), DustID.Enchanted_Gold,
				-Projectile.velocity * Main.rand.NextFloat(0.05f, 0.13f), 90, color, Main.rand.NextFloat(0.8f, 1.15f));
			dust.noGravity = true;
		}
	}

	public override Color? GetAlpha(Color lightColor) => GetCompanionColor() * 0.92f;

	public override bool PreDraw(ref Color lightColor)
	{
		Texture2D texture = TextureAssets.Projectile[Type].Value;
		Rectangle frame = texture.Frame();
		Vector2 origin = frame.Size() * 0.5f;
		Color color = GetCompanionColor();
		for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
			if (Projectile.oldPos[i] == Vector2.Zero)
				continue;
			float strength = (Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length;
			Vector2 position = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
			Main.spriteBatch.Draw(texture, position, frame, color * strength * 0.38f, Projectile.oldRot[i], origin,
				Projectile.scale * (0.55f + strength * 0.35f), SpriteEffects.None, 0f);
		}

		Vector2 center = Projectile.Center - Main.screenPosition;
		for (int i = 0; i < 4; i++) {
			Vector2 glow = new Vector2(3f, 0f).RotatedBy(MathHelper.PiOver2 * i);
			Main.spriteBatch.Draw(texture, center + glow, frame, color * 0.42f, Projectile.rotation, origin,
				Projectile.scale * 1.2f, SpriteEffects.None, 0f);
		}
		Main.spriteBatch.Draw(texture, center, frame, Color.White, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);
		return false;
	}

	public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
	{
		if (!Main.dedServ) {
			SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.55f, Pitch = 0.25f }, Projectile.Center);
			Color color = GetCompanionColor();
			for (int i = 0; i < 14; i++) {
				Dust dust = Dust.NewDustPerfect(target.Center, DustID.Enchanted_Gold, Main.rand.NextVector2Circular(2.8f, 2.8f),
					70, color, Main.rand.NextFloat(0.9f, 1.35f));
				dust.noGravity = true;
			}
		}
		if (Main.netMode == NetmodeID.MultiplayerClient || target.life > 0)
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
