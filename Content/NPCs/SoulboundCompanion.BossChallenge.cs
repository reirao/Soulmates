#nullable enable
using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Common.Dialogue;
using Soulmates.Common.Feedback;
using Soulmates.Content.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulmates.Content.NPCs;

public sealed partial class SoulboundCompanion
{
	private int bossChallengeSession;
	private int bossChallengeTicks;
	private int bossChallengeFarTicks;
	public bool IsBossChallenge => bossChallengeSession != 0;
	public int BossChallengeSession => bossChallengeSession;
	public int BossChallengePhase => !IsBossChallenge ? 0 : NPC.life > NPC.lifeMax * 2 / 3 ? 1
		: NPC.life > NPC.lifeMax / 3 ? 2 : 3;
	private CompanionConversationResult ToggleBossChallenge()
	{
		if (!NPC.active || !TryGetOwner(out Player owner) || owner.dead || FindBoundSigil() is null
			|| CompanionInventorySync.IsPending(owner)) return new(SoulmatesText.Get("TargetOrders.Invalid"), false);
		if (IsBossChallenge) {
			EndBossChallenge("Cancelled");
			return new(SoulmatesText.Get("BossChallenge.Cancelled"), true);
		}
		if (!Profile.EvilMode || Main.npc.Any(npc => npc.active && npc.boss))
			return new(SoulmatesText.Get("BossChallenge.Unavailable"), false);
		// Retire all normal work and owned effects before this same NPC becomes hostile.
		StopOwnedEffects();
		Profile.WorkPaused = true;
		bossChallengeSession = Main.rand.Next(1, 8000001);
		bossChallengeTicks = bossChallengeFarTicks = 0;
		NPC.lifeMax = Math.Clamp(owner.statLifeMax2 * 8 + Profile.BalancedLevel * 80, 1500, 20000);
		NPC.life = NPC.lifeMax;
		NPC.defense = 12;
		NPC.velocity = Vector2.Zero;
		NPC.target = owner.whoAmI;
		ApplyBossChallengeFlags();
		NPC.netUpdate = true;
		SyncProfileToBoundSigil();
		SoulmatesFeedbackSystem.Record("boss_challenge", ("state", "started"));
		return new(SoulmatesText.Get("BossChallenge.Started"), true);
	}
	private void ApplyBossChallengeFlags()
	{
		Vector2 center = NPC.Center;
		NPC.width = IsBossChallenge ? 72 : 42;
		NPC.height = IsBossChallenge ? 76 : 46;
		NPC.Center = center;
		NPC.boss = IsBossChallenge;
		NPC.friendly = !IsBossChallenge;
		NPC.dontTakeDamage = !IsBossChallenge;
		NPC.damage = IsBossChallenge && bossChallengeTicks >= 120 ? 24 + BossChallengePhase * 4 : 0;
		NPC.knockBackResist = 0f;
		NPC.scale = IsBossChallenge ? 1.65f : 1f;
	}
	private void UpdateBossChallenge(Player owner)
	{
		ApplyBossChallengeFlags();
		UpdateSocialState();
		if (Main.netMode == NetmodeID.MultiplayerClient) {
			bossChallengeTicks = Math.Min(18000, bossChallengeTicks + 1);
			return;
		}
		bossChallengeTicks++;
		bossChallengeFarTicks = Vector2.DistanceSquared(owner.Center, NPC.Center) > 1600f * 1600f
			? bossChallengeFarTicks + 1 : 0;
		if (bossChallengeTicks >= 18000 || bossChallengeFarTicks >= 180
			|| Main.npc.Any(npc => npc.active && npc.boss && npc.whoAmI != NPC.whoAmI)) {
			EndBossChallenge("Cancelled");
			return;
		}
		int cycle = bossChallengeTicks % 240;
		int phase = BossChallengePhase;
		Vector2 orbit = owner.Center + new Vector2(MathF.Cos(bossChallengeTicks * 0.012f) * 240f, -160f);
		if (cycle is >= 90 and < 120) NPC.velocity *= 0.88f;
		else if (cycle == 120) NPC.velocity = (owner.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * (10f + phase * 2f);
		else if (cycle is < 120 or > 145)
			NPC.velocity = Vector2.Lerp(NPC.velocity, (orbit - NPC.Center).SafeNormalize(Vector2.UnitY)
				* Math.Min(8f, Vector2.Distance(orbit, NPC.Center) * 0.05f), 0.08f);
		facing = owner.Center.X >= NPC.Center.X ? 1 : -1;
		NPC.direction = NPC.spriteDirection = facing;
		if (cycle == 180 && bossChallengeTicks > 120) {
			Vector2 aim = (owner.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
			int count = 1 + phase * 2;
			for (int i = 0; i < count; i++)
				Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, aim.RotatedBy((i - (count - 1) * 0.5f) * 0.18f) * 5.5f,
					ModContent.ProjectileType<CompanionBossSpark>(), 16 + phase * 2, 0f, Main.myPlayer,
					NPC.whoAmI, owner.whoAmI, bossChallengeSession);
		}
		if (bossChallengeTicks % 30 == 0 || cycle == 120) NPC.netUpdate = true;
	}
	private void EndBossChallenge(string outcome, bool announce = true)
	{
		if (!IsBossChallenge) return;
		bossChallengeSession = bossChallengeTicks = bossChallengeFarTicks = 0;
		for (int i = 0; i < Main.maxProjectiles; i++) {
			Projectile shot = Main.projectile[i];
			if (shot.active && shot.ModProjectile is CompanionBossSpark && (int)shot.ai[0] == NPC.whoAmI) shot.Kill();
		}
		NPC.lifeMax = NPC.life = 250;
		Array.Clear(NPC.buffType); Array.Clear(NPC.buffTime);
		NPC.lifeRegen = NPC.lifeRegenCount = 0;
		NPC.defense = 10;
		NPC.velocity = Vector2.Zero;
		NPC.rotation = 0f;
		NPC.active = true;
		ApplyBossChallengeFlags();
		NPC.netUpdate = true;
		if (Main.netMode != NetmodeID.MultiplayerClient) {
			SyncProfileToBoundSigil();
			if (announce) SpeakLocalized("BossChallenge." + outcome);
			SoulmatesFeedbackSystem.Record("boss_challenge", ("state", outcome));
		}
	}
	public override bool CheckDead()
	{
		if (!IsBossChallenge) return true;
		if (Main.netMode != NetmodeID.MultiplayerClient) EndBossChallenge("Defeated");
		else NPC.life = 1;
		return false;
	}
	public override bool CanHitPlayer(Player target, ref int cooldownSlot)
		=> IsBossChallenge && bossChallengeTicks >= 120 && target.whoAmI == (int)NPC.ai[0];
	public override bool CanHitNPC(NPC target) => false;
	public override bool? CanBeHitByItem(Player player, Item item) => IsBossChallenge ? player.whoAmI == (int)NPC.ai[0] : null;
	public override bool? CanBeHitByProjectile(Projectile projectile) => IsBossChallenge
		? projectile.friendly && projectile.owner == (int)NPC.ai[0] : null;
}
