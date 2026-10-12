#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Common.Dialogue;
using Soulmates.Common.Feedback;
using Soulmates.Common.UI;
using Terraria;
using Terraria.GameContent.UI;
using Terraria.ID;

namespace Soulmates.Content.NPCs;

public sealed partial class SoulboundCompanion
{
	private NPC? encounterCritter;
	private int encounterCritterType;
	private CompanionInitiativeKind? encounterInitiative;
	private CompanionCritterMode encounterMode;
	private int critterAcquaintanceTicks;
	private readonly Queue<(string Key, string Name)> pendingCatchNotices = new();

	private bool IsCurrentCritterEncounter() => encounterCritter is { } target && target.type == encounterCritterType
		&& CompanionCritters.IsCurrent(target) && CanTargetCritterCollect(target.whoAmI)
		&& (encounterInitiative is not { } kind || Profile.CritterMode == encounterMode && CanRunInitiative(kind));

	private CompanionConversationResult RequestCritterEncounter(NPC target, bool playerRequested,
		CompanionInitiativeKind? initiative = null)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || IsBossChallenge || !NPC.active
			|| !TryGetOwner(out Player owner) || owner.dead || FindBoundSigil() is null
			|| Vector2.DistanceSquared(owner.Center, NPC.Center) > 560f * 560f
			|| CompanionInventorySync.IsPending(owner) || !CanTargetCritterCollect(target.whoAmI)
			|| HasPendingQuestion || HasPendingInitiative || guardianTarget >= 0)
			return new(SoulmatesText.Get("TargetOrders.Invalid"), false);
		if (playerRequested) SetCommand(stay: false);
		else if (!CanHoldQuestion())
			return new(SoulmatesText.Get("Social.Critters.DirectBusy"), false);
		encounterCritter = target;
		encounterCritterType = target.type;
		encounterInitiative = initiative;
		encounterMode = Profile.CritterMode;
		IsFirstCritterGreeting(target);
		StartChoiceQuestion(CompanionQuestion.CritterEncounter, playerRequested: true);
		SoulmatesFeedbackSystem.Record("critter_encounter", ("npc_type", target.type), ("index", target.whoAmI));
		return new(SoulmatesText.Get("Conversation.CritterEncounter.Question", target.TypeName), true);
	}

	private bool RespondToCritterEncounter(CompanionAnswer answer)
	{
		NPC? target = encounterCritter;
		if (!IsCurrentCritterEncounter() || target is null) {
			ClearChoiceQuestion(); SpeakLocalized("TargetOrders.TargetLost"); return false;
		}
		ClearChoiceQuestion();
		SoulmateEmoteObserver.ShowReply(Owner, QuestionAnswerEmote(CompanionQuestion.CritterEncounter, (int)answer));
		if (answer is CompanionAnswer.First or CompanionAnswer.Second) {
			var result = PerformNpcContext(answer == CompanionAnswer.First ? CompanionNpcAction.Company : CompanionNpcAction.Collect,
				target.whoAmI, target.type);
			ShowSpeech(result.Reply);
			if (result.Accepted && answer == CompanionAnswer.First) critterAcquaintanceTicks = 60;
			return result.Accepted;
		}
		deferredCritter = target; deferredCritterType = target.type; deferredCritterTicks = 1800;
		critterDecisionTimer = 300;
		if (answer == CompanionAnswer.Third) {
			ShowNativeExpression(CompanionCritters.EmoteFor(target), EmoteID.EmoteWink);
			EmoteBubble.NewBubble(EmoteID.EmoteHappiness, new Terraria.GameContent.UI.WorldUIAnchor(target), 180);
		}
		SpeakLocalized(answer == CompanionAnswer.Third ? "Conversation.CritterEncounter.Watching" : "Conversation.CritterEncounter.Leaving",
			target.TypeName);
		return true;
	}

	private void ClearCritterEncounter()
	{
		if (encounterCritter is { } target) {
			deferredCritter = target; deferredCritterType = target.type; deferredCritterTicks = 1800;
		}
		encounterCritter = null; encounterCritterType = 0; encounterInitiative = null;
	}

	private void UpdateCatchNotice()
	{
		if (pendingCatchNotices.Count == 0 || speechTimer > 0
			|| HasPendingQuestion || HasPendingInitiative || pendingCritterLossKey.Length > 0) return;
		var notice = pendingCatchNotices.Dequeue();
		SpeakLocalized(notice.Key, notice.Name);
		ShowNativeEmote(EmoteID.ItemBugNet, 240);
	}
}
