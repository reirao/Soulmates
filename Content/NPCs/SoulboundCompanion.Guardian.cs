#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Soulmates.Common;
using Soulmates.Common.Dialogue;
using Soulmates.Common.UI;
using Soulmates.Content.Items;
using Soulmates.Content.Projectiles;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulmates.Content.NPCs;

public sealed partial class SoulboundCompanion
{
	private bool UpdateTalentBehavior()
	{
		if (Profile.HasTalent(CompanionTalent.Healer))
			TryHealOwner();

		bool guardian = GuardianSpecialist;
		Vector2 anchor = Command == StayCommand ? idleTarget : Owner.Center;
		NPC? target = Main.netMode == NetmodeID.MultiplayerClient
			? GetSynchronizedDefenseTarget(anchor)
			: FindDefenseTarget(anchor);
		if (target is null) {
			if (Main.netMode != NetmodeID.MultiplayerClient)
				SetGuardianTarget(-1);
			return false;
		}

		if (Main.netMode != NetmodeID.MultiplayerClient)
			SetGuardianTarget(target.whoAmI);
		if (Main.netMode != NetmodeID.MultiplayerClient && socialNpcTarget >= 0)
			ClearTownNpcInteraction();
		brainState = BrainState.Guard;
		Vector2 awayFromTarget = (anchor - target.Center).SafeNormalize(new Vector2(-Owner.direction, 0f));
		Vector2 strafe = new Vector2(-awayFromTarget.Y, awayFromTarget.X)
			* MathF.Sin(Main.GlobalTimeWrappedHourly * 2.4f + bobSeed) * (guardian ? 34f : 24f);
		Vector2 guardPosition;
		if (Command == StayCommand) {
			Vector2 orbit = new Vector2(MathF.Sin(Main.GlobalTimeWrappedHourly * 1.8f + bobSeed) * 24f,
				-10f + IdleBob() * 0.45f);
			guardPosition = idleTarget + orbit;
			MoveTo(guardPosition, guardian ? 5.5f : 4.5f, guardian ? 0.12f : 0.1f);
		}
		else {
			guardPosition = target.Center + awayFromTarget * (guardian ? 145f : 125f)
				+ strafe + new Vector2(0f, -38f + IdleBob() * 0.3f);
			Vector2 fromAnchor = guardPosition - anchor;
			if (fromAnchor.LengthSquared() > PursuitRadius * PursuitRadius)
				guardPosition = anchor + fromAnchor.SafeNormalize(Vector2.UnitX) * PursuitRadius;
			MoveTo(guardPosition, guardian ? 12f : 9f, guardian ? 0.14f : 0.11f);
		}

		float attackRange = guardian ? 520f : 400f;
		if (Main.netMode != NetmodeID.MultiplayerClient && talentCooldown <= 0
			&& Vector2.DistanceSquared(NPC.Center, target.Center) < attackRange * attackRange) {
			Vector2 velocity = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX) * (guardian ? 9f : 7.5f);
			int damage = guardian
				? 8 + Profile.RankIndex * 2 + Math.Min(4, Profile.JobsCompleted / 8)
				: 4 + Profile.RankIndex + Math.Min(2, Profile.JobsCompleted / 12);
			int projectileOwner = Main.netMode == NetmodeID.Server ? 255 : Owner.whoAmI;
			int projectileIndex = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, velocity, ModContent.ProjectileType<SoulBolt>(),
				damage, 1.5f, projectileOwner, target.whoAmI, NPC.whoAmI);
			if (projectileIndex >= 0 && projectileIndex < Main.maxProjectiles) {
				Main.projectile[projectileIndex].npcProj = true;
				Main.projectile[projectileIndex].netUpdate = true;
			}
			NPC.netUpdate = true;
			talentCooldown = guardian ? Math.Max(55, 82 - Profile.RankIndex * 6) : Math.Max(90, 130 - Profile.RankIndex * 8);
		}
		return true;
	}

	private NPC? GetSynchronizedDefenseTarget(Vector2 anchor)
	{
		if (guardianTarget < 0 || guardianTarget >= Main.maxNPCs)
			return null;
		NPC target = Main.npc[guardianTarget];
		float leash = DefenseLeash;
		return IsThreat(target)
			&& Vector2.DistanceSquared(anchor, target.Center) <= leash * leash
			? target
			: null;
	}

	private NPC? FindDefenseTarget(Vector2 anchor)
	{
		if (guardianTarget >= 0 && guardianTarget < Main.maxNPCs) {
			NPC current = Main.npc[guardianTarget];
			float leash = DefenseLeash;
			if (IsThreat(current)
				&& Vector2.DistanceSquared(anchor, current.Center) <= leash * leash)
				return current;
		}
		return FindNearestThreat(anchor, DefenseRange);
	}

	private void SetGuardianTarget(int target)
	{
		if (guardianTarget == target)
			return;
		guardianTarget = target;
		if (target >= 0)
			talentCooldown = Math.Min(talentCooldown, 8);
		if (Main.netMode != NetmodeID.MultiplayerClient)
			NPC.netUpdate = true;
	}

	private void RecoverEnergy(int interval, int amount, bool recoverMood)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || ++recoveryTimer < interval)
			return;
		recoveryTimer = 0;
		int previousEnergy = Profile.Energy;
		int previousMood = Profile.Mood;
		Profile.Energy = Math.Clamp(Profile.Energy + amount, 0, 100);
		if (recoverMood)
			Profile.Mood = Math.Clamp(Profile.Mood + 1, 0, 100);
		if (Profile.Energy == previousEnergy && Profile.Mood == previousMood)
			return;
		SyncProfileToBoundSigil();
		NPC.netUpdate = true;
	}

	private void TryHealOwner()
	{
		if (Main.netMode == NetmodeID.MultiplayerClient)
			return;
		if (talentCooldown > 0 || Profile.Energy < 5 || Owner.statLife >= Owner.statLifeMax2)
			return;

		int missingLife = Owner.statLifeMax2 - Owner.statLife;
		int minimumNeed = Math.Max(6, Owner.statLifeMax2 / 20);
		if (missingLife < minimumNeed)
			return;

		int amount = Math.Min(missingLife, 5 + Profile.RankIndex * 2);
		Owner.statLife += amount;
		Owner.HealEffect(amount, broadcast: true);
		Profile.Energy = Math.Max(0, Profile.Energy - 4);
		Profile.Remember(CompanionMemoryKind.HealerAid, amount);
		bool leveledUp = Profile.GainExperience(2, out int newLevel);
		talentCooldown = Math.Max(360, 660 - Profile.RankIndex * 60);
		SyncProfileToBoundSigil();
		for (int i = 0; i < 12; i++) {
			Dust dust = Dust.NewDustPerfect(Owner.Center + Main.rand.NextVector2Circular(26f, 38f), DustID.HealingPlus,
				Main.rand.NextVector2Circular(0.7f, 0.7f), 80, Profile.EssenceColor, 0.9f);
			dust.noGravity = true;
		}
		string message = SoulmatesText.Get("Messages.Healed", Profile.Name, amount);
		if (leveledUp)
			message += " " + SoulmatesText.Get("Messages.LevelUp", Profile.Name, newLevel);
		if (Main.netMode == NetmodeID.Server)
			global::Soulmates.Soulmates.SendProfileUpdate(Owner, this, message);
		else
			Main.NewText(message, Profile.EssenceColor);
	}

	private NPC? FindNearestThreat(Vector2 anchor, float range)
	{
		NPC? result = null;
		float bestScore = float.MaxValue;
		for (int i = 0; i < Main.maxNPCs; i++) {
			NPC candidate = Main.npc[i];
			if (!IsThreat(candidate))
				continue;
			float anchorDistance = Vector2.DistanceSquared(anchor, candidate.Center);
			bool defendingOwner = candidate.target == Owner.whoAmI;
			float alertRange = defendingOwner || candidate.type == NPCID.TargetDummy ? range : Math.Min(range, PassiveAlertRange);
			if (anchorDistance > alertRange * alertRange)
				continue;
			float score = anchorDistance;
			if (defendingOwner)
				score *= 0.55f;
			if (score >= bestScore)
				continue;
			bestScore = score;
			result = candidate;
		}
		return result;
	}

	private static bool IsThreat(NPC candidate)
	{
		if (!candidate.active || candidate.friendly || candidate.lifeMax <= 5)
			return false;
		if (candidate.type == NPCID.TargetDummy)
			return true;
		return candidate.chaseable && !candidate.immortal;
	}

}
