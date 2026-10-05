#nullable enable
using System;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Terraria;
using Terraria.ID;

namespace Soulmates.Content.NPCs;

public sealed partial class SoulboundCompanion
{
	private CompanionActivityCoordinator? activityCoordinator;
	internal CompanionActivityLane ActivityLane => activityCoordinator?.Current ?? CompanionActivityLane.None;

	private CompanionActivityCoordinator Activities => activityCoordinator ??= new([
		new CompanionActivityModule(CompanionActivityLane.Defense, TickDefenseActivity),
		new CompanionActivityModule(CompanionActivityLane.Paused, TickPausedActivity),
		new CompanionActivityModule(CompanionActivityLane.Assignment, TickAssignmentActivity),
		new CompanionActivityModule(CompanionActivityLane.Critters, TickCritterActivity),
		new CompanionActivityModule(CompanionActivityLane.AutomaticWork, TickAutomaticActivity),
		new CompanionActivityModule(CompanionActivityLane.Residents, TickResidentActivity),
		new CompanionActivityModule(CompanionActivityLane.Stay, TickStayActivity),
		new CompanionActivityModule(CompanionActivityLane.AwaitingReply, TickAwaitingActivity),
		new CompanionActivityModule(CompanionActivityLane.Follow, TickFollowActivity)
	]);

	private bool TickDefenseActivity()
	{
		if (!UpdateTalentBehavior()) return false;
		if (!jobRecoveryPaused) recoveryTimer = 0;
		FinishActivityMotion();
		return true;
	}
	private bool TickPausedActivity()
	{
		if (!Profile.WorkPaused) return false;
		UpdateStoppedWork();
		return true;
	}
	private bool TickAssignmentActivity()
	{
		if (activeJob == CompanionJob.None) return false;
		if (!jobRecoveryPaused) RecoverEnergy(180, 1, recoverMood: false);
		guardianTarget = -1;
		UpdateJob();
		FinishActivityMotion();
		return true;
	}
	private bool TickCritterActivity()
	{
		bool wasAttending = IsAttendingCritter;
		if (!UpdateCritterActivity() && !wasAttending) return false;
		RecoverEnergy(120, 2, recoverMood: false);
		UpdateFacing();
		return true;
	}
	private bool TickAutomaticActivity()
	{
		// Finishing or abandoning a task still owns this frame's movement.
		bool wasWorking = autonomyActivity != AutonomyActivity.None;
		if (!UpdateHelpfulAutonomy() && !wasWorking) return false;
		RecoverEnergy(180, 1, recoverMood: false);
		FinishActivityMotion();
		return true;
	}
	private bool TickResidentActivity()
	{
		if (!UpdateTownNpcInteraction()) return false;
		RecoverEnergy(120, Profile.Trinket == CompanionTrinket.HearthRibbon ? 3 : 2, recoverMood: false);
		FinishActivityMotion();
		return true;
	}
	private bool TickStayActivity()
	{
		if (Command != StayCommand) return false;
		brainState = BrainState.Stay;
		MoveTo(idleTarget + new Vector2(0f, IdleBob()), 1.8f, 0.032f);
		RecoverEnergy(120, Profile.Trinket == CompanionTrinket.HearthRibbon ? 5 : 3, recoverMood: true);
		UpdateFacing();
		return true;
	}
	private bool TickAwaitingActivity()
	{
		if (!HasPendingQuestion && !HasPendingInitiative) return false;
		MoveTo(Owner.Center + new Vector2(-Owner.direction * 66f, -58f + IdleBob() * 0.4f), 3f, 0.05f);
		RecoverEnergy(120, 2, recoverMood: false);
		UpdateFacing();
		return true;
	}
	private bool TickFollowActivity()
	{
		RecoverEnergy(120, Profile.Trinket == CompanionTrinket.HearthRibbon ? 3 : 2, recoverMood: false);
		Vector2 followTarget = Owner.Center + new Vector2(-Owner.direction * 66f, -58f);
		float distance = Vector2.Distance(NPC.Center, followTarget);
		if (distance > 1200f) {
			NPC.Center = followTarget;
			NPC.velocity = Vector2.Zero;
			brainState = BrainState.Follow;
			stateTimer = 60;
			NPC.netUpdate = true;
			return true;
		}
		if (Main.netMode != NetmodeID.MultiplayerClient && distance > 340f) {
			bool changed = brainState != BrainState.CatchUp;
			brainState = BrainState.CatchUp;
			stateTimer = 45;
			if (changed) NPC.netUpdate = true;
		}
		if (stateTimer-- <= 0 && Main.netMode != NetmodeID.MultiplayerClient) ChooseNextState(distance, followTarget);
		switch (brainState) {
			case BrainState.Idle:
				MoveTo(followTarget + new Vector2(0f, IdleBob()), 2f, 0.03f);
				break;
			case BrainState.Wander:
				MoveTo(idleTarget + new Vector2(0f, IdleBob() * 0.5f), 3f, 0.045f);
				break;
			case BrainState.Inspect:
				Vector2 inspectOffset = new(MathF.Sin((stateTimer + bobSeed) * 0.035f) * 26f, -82f + IdleBob());
				MoveTo(Owner.Center + inspectOffset, 2.8f, 0.04f);
				break;
			case BrainState.CatchUp:
				MoveTo(followTarget, 11f, 0.11f);
				break;
			default:
				MoveTo(followTarget, 6.5f, 0.065f);
				break;
		}
		FinishActivityMotion();
		return true;
	}
	private void FinishActivityMotion()
	{
		NPC.rotation = MathHelper.Lerp(NPC.rotation, NPC.velocity.X * 0.025f, 0.08f);
		UpdateFacing();
	}
}
