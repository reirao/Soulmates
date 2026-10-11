#nullable enable
using System;
using System.IO;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Content.Projectiles;
using Terraria;
using Terraria.ID;

namespace Soulmates.Content.NPCs;

public sealed partial class SoulboundCompanion
{
	private const float PersonalPlanRadius = 240f;
	private const int PersonalPlanDwellTicks = 120;
	private const byte PersonalPlanWireVersion = 1;
	private sealed record PersonalPlanDisplay(CompanionPersonalPlanKind Kind, bool Returning, int Remaining, string Subject);
	private sealed class PersonalPlanAttempt
	{
		public CompanionPersonalPlanKind Kind;
		public Guid ProfileId;
		public Guid World;
		public Point Home;
		public Projectile? Pet;
		public int ItemType;
		public string SupplyKey = "";
		public string Subject = "";
		public Vector2 Destination;
		public Vector2 MovementTarget;
		public int Elapsed;
		public int Dwell;
		public bool Returning;
		public int HomeValidationTicks;
		public bool HomeStillValid;
	}
	private PersonalPlanAttempt? personalPlan;
	private int personalPlanDecisionTicks = 600;
	private CompanionPersonalPlanMemory? personalPlanReflection;
	private PersonalPlanDisplay? synchronizedPersonalPlan;
	private CompanionPersonalPlanMemory? synchronizedPersonalPlanReflection;
	private string personalPlanStopReason = "";

	public CompanionPersonalPlanKind CurrentPersonalPlan => Main.netMode == NetmodeID.MultiplayerClient
		? synchronizedPersonalPlan?.Kind ?? CompanionPersonalPlanKind.None : personalPlan?.Kind ?? CompanionPersonalPlanKind.None;
	public int PersonalPlanTicksRemaining => Main.netMode == NetmodeID.MultiplayerClient
		? synchronizedPersonalPlan?.Remaining ?? 0 : personalPlan is { } plan
			? Math.Max(0, CompanionPersonalPlans.MaximumDurationTicks - plan.Elapsed) : 0;
	public int PersonalPlanObservedTicks => Main.netMode == NetmodeID.MultiplayerClient ? 0 : personalPlan?.Dwell ?? 0;
	public bool IsReturningFromPersonalPlan => Main.netMode == NetmodeID.MultiplayerClient
		? synchronizedPersonalPlan?.Returning == true : personalPlan?.Returning == true;
	public string PersonalPlanSubject => Main.netMode == NetmodeID.MultiplayerClient
		? synchronizedPersonalPlan?.Subject ?? "" : personalPlan?.Subject ?? "";
	public string LastPersonalPlanStopReason => personalPlanStopReason;
	public string PersonalPlanStatusText => CurrentPersonalPlan != CompanionPersonalPlanKind.None
		? SoulmatesText.Get(IsReturningFromPersonalPlan ? "PersonalPlans.Returning" : $"PersonalPlans.Status.{CurrentPersonalPlan}", PersonalPlanSubject) : "";
	public string ReflectionSubject => (Main.netMode == NetmodeID.MultiplayerClient
		? synchronizedPersonalPlanReflection : personalPlanReflection) is { } memory
		? SoulmatesText.Get($"PersonalPlans.Subject.{memory.Kind}", memory.Subject)
		: SoulmatesText.EnumName(ReflectionKind) + " (" + SoulmatesText.Get($"Conversation.Reflection.Contexts.{ReflectionContext}") + ")";

	private string? PersonalPlanBlockReason()
	{
		if (!NPC.active || !TryGetOwner(out Player owner) || owner.dead) return "owner_unavailable";
		if (FindBoundSigil() is null) return "binding_missing";
		if (CompanionInventorySync.IsPending(owner)) return "inventory_pending";
		if (!Profile.AutonomyEnabled) return "autonomy_off";
		if (Profile.WorkPaused || Command == StayCommand) return "paused_or_stay";
		if (activeJob != CompanionJob.None || Profile.Routine != CompanionJob.None || learningIntent is not null) return "explicit_work";
		if (HasPendingQuestion || HasPendingInitiative) return "awaiting_reply";
		if (autonomyActivity != AutonomyActivity.None || IsAttendingCritter || socialNpcTarget >= 0) return "other_activity";
		if (autonomyRecoveryPaused || Profile.Energy < 20 || Profile.Mood < 15) return "recovering";
		if (guardianTarget >= 0 || FindDefenseTarget(owner.Center) is not null) return "defense";
		if (ActivityLane is not (CompanionActivityLane.None or CompanionActivityLane.Follow)) return "other_activity";
		if (!float.IsFinite(owner.Center.X) || !float.IsFinite(owner.Center.Y)
			|| !float.IsFinite(NPC.Center.X) || !float.IsFinite(NPC.Center.Y)
			|| owner.velocity.LengthSquared() > 9f || owner.controlUseItem || owner.itemAnimation > 0
			|| Vector2.DistanceSquared(owner.Center, NPC.Center) > PersonalPlanRadius * PersonalPlanRadius) return "owner_moving";
		return null;
	}

	private void TickPersonalPlans()
	{
		if (Main.netMode == NetmodeID.MultiplayerClient) return;
		if (personalPlanDecisionTicks > 0) personalPlanDecisionTicks--;
		// Pending inventory transactions must never persist a competing profile snapshot.
		if (!TryGetOwner(out Player owner) || CompanionInventorySync.IsPending(owner)) {
			CancelPersonalPlan("owner_or_inventory_unavailable"); return;
		}
		Profile.PersonalPlans.Tick();
		if (PersonalPlanBlockReason() is { } reason) { CancelPersonalPlan(reason); return; }
		if (personalPlan is null) {
			if (personalPlanDecisionTicks > 0 || reflectionIntent is not null || speechTimer > 0
				|| owner.velocity.LengthSquared() > 1f) return;
			personalPlanDecisionTicks = 300;
			personalPlan = FindPersonalPlan(owner);
			if (personalPlan is null) return;
			personalPlan.ProfileId = Profile.Id;
			personalPlan.Subject = CompanionPersonalPlans.NormalizeSubject(personalPlan.Subject);
			Profile.PersonalPlans.Defer(personalPlan.Kind, CompanionPersonalPlans.InterruptedCooldownTicks);
			personalPlanStopReason = "";
			SyncProfileToBoundSigil();
			TraceDiagnostic($"personal plan: {personalPlan.Kind}; bounded idle visit");
		}
		PersonalPlanAttempt plan = personalPlan;
		if (plan.ProfileId != Profile.Id) { CancelPersonalPlan("binding_changed"); return; }
		if (++plan.Elapsed >= CompanionPersonalPlans.MaximumDurationTicks) { CancelPersonalPlan("time_limit"); return; }
		if (!PersonalPlanTargetValid(plan, owner)) { CancelPersonalPlan("target_changed"); return; }
		if (!plan.Returning) {
			bool arrived = Vector2.DistanceSquared(NPC.Center, plan.Destination) <= 48f * 48f;
			if (arrived && plan.Kind == CompanionPersonalPlanKind.HomeVisit)
				arrived = NPC.Center.ToTileCoordinates() == plan.Home;
			if (arrived && ActivityLane == CompanionActivityLane.Follow)
				plan.Dwell = Math.Min(PersonalPlanDwellTicks, plan.Dwell + 1);
			else plan.Dwell = 0;
			if (plan.Dwell >= PersonalPlanDwellTicks && plan.Elapsed >= CompanionPersonalPlans.MinimumDurationTicks)
				plan.Returning = true;
			else if (plan.Elapsed >= CompanionPersonalPlans.MaximumDurationTicks - 180) {
				CancelPersonalPlan("visit_incomplete"); return;
			}
		}
		Vector2 target = plan.Returning ? PersonalPlanReturnTarget(owner) : plan.Destination;
		if (plan.Returning && Vector2.DistanceSquared(NPC.Center, target) <= 48f * 48f) {
			plan.HomeValidationTicks = 0;
			if (!PersonalPlanTargetValid(plan, owner)) { CancelPersonalPlan("target_changed"); return; }
			CompletePersonalPlan(plan); return;
		}
		// The existing Follow lane moves Wander targets. Higher priority lanes retain all movement ownership.
		brainState = BrainState.Wander;
		idleTarget = plan.MovementTarget = target;
		stateTimer = 2;
		if (plan.Elapsed == 1 || plan.Elapsed % 60 == 0) NPC.netUpdate = true;
	}

	private PersonalPlanAttempt? FindPersonalPlan(Player owner)
	{
		PersonalPlanAttempt? best = null;
		int bestScore = int.MinValue;
		void Offer(PersonalPlanAttempt candidate, CompanionIntent? context = null) {
			if (!Profile.PersonalPlans.IsReady(candidate.Kind)) return;
			if (context is { } intent && !attention.IsReady(intent.Kind)) return;
			int score = Profile.PersonalPlans.Preference(candidate.Kind) + (context is { } learned ? Profile.IntentLearning.Score(learned) : 6);
			if (Profile.PersonalPlans.LastCompleted?.Kind == candidate.Kind) score -= 4;
			if (score > bestScore) { best = candidate; bestScore = score; }
		}
		Guid world = PlaceWorld;
		foreach (CompanionPlace place in Profile.Places.Records) {
			Vector2 destination = place.Anchor.ToWorldCoordinates();
			if (place.WorldId != world || !place.IsHome
				|| Vector2.DistanceSquared(owner.Center, destination) > 208f * 208f
				|| Vector2.DistanceSquared(NPC.Center, destination) < 64f * 64f
				|| Vector2.DistanceSquared(NPC.Center, destination) > 208f * 208f) continue;
			var candidate = new PersonalPlanAttempt { Kind = CompanionPersonalPlanKind.HomeVisit, World = world,
				Home = place.Anchor, Destination = destination };
			if (PersonalPlanTargetValid(candidate, owner)) Offer(candidate);
		}
		if (Profile.CritterMode != CompanionCritterMode.Off
			&& Profile.GetInitiativePolicy(CompanionInitiativeKind.CritterCompany) != CompanionInitiativePolicy.Never
			&& CompanionPets.IsCarried(Profile, Profile.PetItemType)) {
			foreach (Projectile projectile in Main.ActiveProjectiles) {
				if (projectile.ModProjectile is not CompanionFamiliar familiar || !familiar.BelongsTo(this)
					|| projectile.ai[1] != Profile.PetItemType) continue;
				var candidate = new PersonalPlanAttempt { Kind = CompanionPersonalPlanKind.PetCompany,
					Pet = projectile, ItemType = Profile.PetItemType, Subject = Lang.GetItemNameValue(Profile.PetItemType),
					Destination = NPC.Center };
				if (PersonalPlanTargetValid(candidate, owner)) Offer(candidate,
					new CompanionIntent(CompanionInitiativeKind.CritterCompany, CompanionIntentContext.Social));
				break;
			}
		}
		if (CurrentIntentContext() == CompanionIntentContext.Building) {
			foreach (Item item in Profile.CarriedItems) {
				if (item is null || item.IsAir || item.stack <= 0 || !Profile.IntentLearning.RecognizesSupply(BuildingSupplyKey(item))) continue;
				Offer(new PersonalPlanAttempt { Kind = CompanionPersonalPlanKind.SupplyReflection,
					ItemType = item.type, SupplyKey = BuildingSupplyKey(item), Subject = item.Name, Destination = NPC.Center },
					new CompanionIntent(CompanionInitiativeKind.Gathering, CompanionIntentContext.Building));
				break;
			}
		}
		return best;
	}

	private bool PersonalPlanTargetValid(PersonalPlanAttempt plan, Player owner)
	{
		if (!float.IsFinite(plan.Destination.X) || !float.IsFinite(plan.Destination.Y)
			|| Vector2.DistanceSquared(owner.Center, plan.Destination) > PersonalPlanRadius * PersonalPlanRadius) return false;
		if (plan.Kind == CompanionPersonalPlanKind.HomeVisit) {
			if (plan.World == Guid.Empty || plan.World != PlaceWorld
				|| Profile.Places.FindNearby(plan.World, plan.Home)?.IsHome != true) return false;
			if (plan.HomeValidationTicks-- <= 0) {
				CompanionPlaceObservation actual = CompanionPlaces.SampleAt(owner, NPC, plan.Home);
				plan.HomeStillValid = actual.IsCandidate && actual.Anchor == plan.Home;
				plan.HomeValidationTicks = 30;
			}
			return plan.HomeStillValid;
		}
		if (plan.Kind == CompanionPersonalPlanKind.PetCompany) {
			Projectile? pet = plan.Pet;
			return Profile.CritterMode != CompanionCritterMode.Off
				&& Profile.GetInitiativePolicy(CompanionInitiativeKind.CritterCompany) != CompanionInitiativePolicy.Never
				&& pet is { active: true } && pet.whoAmI >= 0 && pet.whoAmI < Main.maxProjectiles
				&& ReferenceEquals(Main.projectile[pet.whoAmI], pet) && pet.ModProjectile is CompanionFamiliar familiar
				&& familiar.BelongsTo(this) && pet.ai[1] == plan.ItemType && Profile.PetItemType == plan.ItemType
				&& CompanionPets.IsCarried(Profile, plan.ItemType)
				&& Vector2.DistanceSquared(pet.Center, NPC.Center) <= 112f * 112f;
		}
		if (plan.Kind != CompanionPersonalPlanKind.SupplyReflection
			|| !Profile.IntentLearning.RecognizesSupply(plan.SupplyKey)) return false;
		foreach (Item item in Profile.CarriedItems)
			if (item is not null && !item.IsAir && item.stack > 0 && item.type == plan.ItemType
				&& BuildingSupplyKey(item) == plan.SupplyKey) return true;
		return false;
	}

	private static Vector2 PersonalPlanReturnTarget(Player owner) => owner.Center + new Vector2(-owner.direction * 66f, -58f);
	private void CompletePersonalPlan(PersonalPlanAttempt plan)
	{
		Profile.PersonalPlans.Completed(plan.Kind, plan.Subject);
		if (reflectionIntent is null && reflectionCooldown <= 0 && Profile.QuestionCadence != CompanionQuestionCadence.Quiet) {
			personalPlanReflection = Profile.PersonalPlans.LastCompleted;
			// Reuse the existing answer token/cadence. Personal feedback is routed separately below.
			reflectionIntent = plan.Kind switch {
				CompanionPersonalPlanKind.PetCompany => new(CompanionInitiativeKind.CritterCompany, CompanionIntentContext.Social),
				CompanionPersonalPlanKind.SupplyReflection => new(CompanionInitiativeKind.Gathering, CompanionIntentContext.Building),
				_ => new(CompanionInitiativeKind.Treasure, CompanionIntentContext.Exploring)
			};
			reflectionTicks = 18000;
		}
		TraceDiagnostic($"personal plan completed: {plan.Kind}; observed={plan.Dwell}; returned=true");
		CancelPersonalPlan("completed");
		personalPlanDecisionTicks = CompanionPersonalPlans.InterruptedCooldownTicks;
		SyncProfileToBoundSigil();
		NPC.netUpdate = true;
	}
	private void CancelPersonalPlan(string reason)
	{
		if (personalPlan is not { } plan) return;
		personalPlan = null;
		personalPlanStopReason = reason;
		personalPlanDecisionTicks = Math.Max(personalPlanDecisionTicks, 300);
		if (Command != StayCommand && brainState == BrainState.Wander && idleTarget == plan.MovementTarget) {
			brainState = BrainState.Follow;
			stateTimer = 0;
		}
		TraceDiagnostic($"personal plan stopped: {plan.Kind}; {reason}");
		NPC.netUpdate = true;
	}
	public void ClearPersonalPlansRuntime()
	{
		CancelPersonalPlan("cleanup");
		if (personalPlanReflection is not null) { reflectionIntent = null; reflectionTicks = 0; }
		personalPlanReflection = null;
		synchronizedPersonalPlan = null;
		synchronizedPersonalPlanReflection = null;
	}

	// Append explicitly after the existing ExtraAI reflection fields on both ends.
	private void WritePersonalPlanState(BinaryWriter writer)
	{
		writer.Write(PersonalPlanWireVersion);
		writer.Write((byte)(personalPlan?.Kind ?? CompanionPersonalPlanKind.None));
		writer.Write(personalPlan?.Returning == true);
		writer.Write((ushort)(personalPlan is { } plan
			? Math.Clamp(CompanionPersonalPlans.MaximumDurationTicks - plan.Elapsed, 1, CompanionPersonalPlans.MaximumDurationTicks) : 0));
		writer.Write(personalPlan?.Subject ?? "");
		writer.Write((byte)(personalPlanReflection is { } memory && memory == Profile.PersonalPlans.LastCompleted
			? memory.Kind : CompanionPersonalPlanKind.None));
	}
	private void ReadPersonalPlanState(BinaryReader reader)
	{
		if (reader.ReadByte() != PersonalPlanWireVersion) throw new InvalidDataException("Unsupported personal plan display data.");
		var kind = (CompanionPersonalPlanKind)reader.ReadByte();
		byte returning = reader.ReadByte();
		int remaining = reader.ReadUInt16();
		string subject = reader.ReadString();
		var reflected = (CompanionPersonalPlanKind)reader.ReadByte();
		if (returning > 1 || remaining > CompanionPersonalPlans.MaximumDurationTicks
			|| subject != CompanionPersonalPlans.NormalizeSubject(subject)
			|| kind != CompanionPersonalPlanKind.None && (!CompanionPersonalPlans.Valid(kind) || remaining == 0)
			|| kind == CompanionPersonalPlanKind.None && (returning != 0 || remaining != 0 || subject.Length != 0))
			throw new InvalidDataException("Invalid personal plan display state.");
		CompanionPersonalPlanMemory? reflection = null;
		if (reflected != CompanionPersonalPlanKind.None) {
			if (!CompanionPersonalPlans.Valid(reflected) || Profile.PersonalPlans.LastCompleted is not { } completed
				|| completed.Kind != reflected) throw new InvalidDataException("Personal reflection has no matching completed undertaking.");
			reflection = completed;
		}
		// Do not apply a partial snapshot, or turn a received display state into executable work.
		synchronizedPersonalPlan = kind == CompanionPersonalPlanKind.None ? null : new(kind, returning == 1, remaining, subject);
		synchronizedPersonalPlanReflection = reflection;
	}
}
