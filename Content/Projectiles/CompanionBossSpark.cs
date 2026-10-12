#nullable enable
using Microsoft.Xna.Framework;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulmates.Content.Projectiles;

public sealed class CompanionBossSpark : ModProjectile
{
	public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.RainbowRodBullet}";
	public override void SetDefaults()
	{
		Projectile.width = Projectile.height = 14;
		Projectile.hostile = true;
		Projectile.friendly = false;
		Projectile.tileCollide = true;
		Projectile.ignoreWater = true;
		Projectile.penetrate = 1;
		Projectile.timeLeft = 180;
		Projectile.netImportant = true;
	}
	private bool HasChallenge => (int)Projectile.ai[0] >= 0 && (int)Projectile.ai[0] < Main.maxNPCs
		&& Main.npc[(int)Projectile.ai[0]].active
		&& Main.npc[(int)Projectile.ai[0]].ModNPC is SoulboundCompanion mate
		&& mate.IsBossChallenge && mate.BossChallengeSession == (int)Projectile.ai[2]
		&& (int)mate.NPC.ai[0] == (int)Projectile.ai[1];
	public override void AI()
	{
		if (!HasChallenge) { Projectile.Kill(); return; }
		Projectile.rotation += 0.06f;
		Lighting.AddLight(Projectile.Center, new Vector3(0.6f, 0.15f, 0.3f));
		if (!Main.dedServ) {
			Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.PinkTorch, -Projectile.velocity * 0.1f, 100, Color.HotPink, 0.7f);
			dust.noGravity = true;
		}
	}
	public override bool CanHitPlayer(Player target) => HasChallenge && target.whoAmI == (int)Projectile.ai[1];
	public override bool? CanHitNPC(NPC target) => false;
}
