#nullable enable
using System;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Common.Feedback;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulmates.Content.NPCs;

public sealed partial class SoulboundCompanion
{
	private CompanionIntentContext observedIntentContext;
	private int observedIntentTicks;
	private int intentRequestCooldown;
	private CompanionIntent? learningIntent;
	private CompanionIntentContext proposedIntentContext;
	private CompanionIntent? reflectionIntent;
	private int reflectionTicks;
	private int reflectionCooldown;
	public CompanionInitiativeKind ReflectionKind => reflectionIntent?.Kind ?? CompanionInitiativeKind.Gathering;
	public CompanionIntentContext ReflectionContext => reflectionIntent?.Context ?? CompanionIntentContext.General;

	private CompanionIntentContext CurrentIntentContext()
	{
		if (observedIntentTicks > 0) return observedIntentContext;
		return Owner.Center.Y > Main.worldSurface * 16f ? CompanionIntentContext.Underground : CompanionIntentContext.General;
	}
	private void ObserveIntentContext(CompanionIntentContext context)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || !Enum.IsDefined(context)) return;
		observedIntentContext = context;
		observedIntentTicks = 600;
	}
	private void ObserveBuildingUse(Item item)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || CompanionInventorySync.IsPending(Owner)) return;
		ObserveIntentContext(CompanionIntentContext.Building);
		// This records use of a construction item, not proof that a tile was successfully placed.
		if (item.createTile >= 0 || item.createWall > 0) Profile.IntentLearning.ObserveBuildingSupply(BuildingSupplyKey(item));
		SyncProfileToBoundSigil();
		NPC.netUpdate = true;
	}
	private static string BuildingSupplyKey(Item item) => item.ModItem?.FullName ?? $"Terraria/{item.type}";
	private void TickIntentLearning()
	{
		if (Main.netMode == NetmodeID.MultiplayerClient) return;
		if (observedIntentTicks > 0) observedIntentTicks--;
		if (intentRequestCooldown > 0) intentRequestCooldown--;
		if (reflectionCooldown > 0) reflectionCooldown--;
		if (reflectionTicks > 0 && !HasPendingQuestion && --reflectionTicks == 0) reflectionIntent = null;
	}
	private void BeginLearningIntent(CompanionInitiativeKind kind, bool requested,
		CompanionIntentContext? context = null)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient) return;
		var intent = new CompanionIntent(kind, context ?? CurrentIntentContext());
		if (!CompanionIntentLearning.Valid(intent)) return;
		learningIntent = intent;
		if (requested && intentRequestCooldown <= 0) {
			Profile.IntentLearning.Requested(intent);
			intentRequestCooldown = 180;
		}
		TraceDiagnostic($"intent: {kind} in {intent.Context}; requested={requested}; learned score={Profile.IntentLearning.Score(intent)}");
	}
	private void CompleteLearningIntent(bool successful)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || learningIntent is not { } intent) return;
		learningIntent = null;
		Profile.IntentLearning.Completed(intent, successful);
		// Keep one immutable result to discuss; later work cannot change what the question refers to.
		if (successful && reflectionIntent is null && reflectionCooldown <= 0
			&& Profile.QuestionCadence != CompanionQuestionCadence.Quiet) {
			reflectionIntent = intent;
			reflectionTicks = 18000;
		}
		TraceDiagnostic($"intent outcome: {intent.Kind}/{intent.Context}; success={successful}");
		SoulmatesFeedbackSystem.Record("intent_outcome", ("action", intent.Kind.ToString()),
			("context", intent.Context.ToString()), ("success", successful));
		SyncProfileToBoundSigil();
		NPC.netUpdate = true;
	}
	private void ClearReflection()
	{
		reflectionIntent = null;
		reflectionTicks = 0;
	}
	private void ClearCritterLearningIntent()
	{
		if (learningIntent?.Kind is CompanionInitiativeKind.CritterCompany or CompanionInitiativeKind.CritterCollect)
			learningIntent = null;
	}
	private void EvaluateReflection(CompanionAnswer answer)
	{
		if (reflectionIntent is not { } intent) return;
		Profile.IntentLearning.Evaluate(intent, answer);
		TraceDiagnostic($"player evaluation: {intent.Kind}/{intent.Context}; {answer}; preference={Profile.IntentLearning.Feedback(intent)}");
		SoulmatesFeedbackSystem.Record("intent_feedback", ("action", intent.Kind.ToString()),
			("context", intent.Context.ToString()), ("answer", answer.ToString()),
			("preference", Profile.IntentLearning.Feedback(intent)));
	}
	private static CompanionInitiativeKind WorkIntentKind(CompanionJob job) => job switch {
		CompanionJob.Mine => CompanionInitiativeKind.Mining,
		CompanionJob.FindTreasure => CompanionInitiativeKind.Treasure,
		_ => CompanionInitiativeKind.Gathering
	};
}
