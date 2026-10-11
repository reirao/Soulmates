#nullable enable
using System;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Common.Dialogue;
using Soulmates.Common.Feedback;
using Terraria;
using Terraria.GameContent.UI;
using Terraria.ID;

namespace Soulmates.Content.NPCs;

public sealed partial class SoulboundCompanion
{
	private CompanionInitiativeKind? evilCelebration;
	private int evilCelebrationTicks;
	private int evilCelebrationCooldown;

	private CompanionConversationResult ToggleEvilMode()
	{
		if (!NPC.active || !TryGetOwner(out Player owner) || owner.dead || FindBoundSigil() is null
			|| CompanionInventorySync.IsPending(owner)) return new(SoulmatesText.Get("TargetOrders.Invalid"), false);
		Profile.EvilMode = !Profile.EvilMode;
		ClearEvilCelebration();
		if (!HasPendingQuestion && !HasPendingInitiative) {
			ClearNativeExpression();
			RetireNativeBubble();
			ShowNativeExpression(Profile.EvilMode ? EmoteID.EmoteLaugh : EmoteID.EmoteWink, EmoteID.EmotionLove);
		}
		SyncProfileToBoundSigil();
		NPC.netUpdate = true;
		TraceDiagnostic($"persona: EVIL={Profile.EvilMode}; permissions and assignment unchanged");
		SoulmatesFeedbackSystem.Record("persona", ("evil", Profile.EvilMode));
		return new(HasPendingQuestion || HasPendingInitiative ? ""
			: SoulmatesText.Get(Profile.EvilMode ? "Evil.Enabled" : "Evil.Disabled"), true);
	}

	// The persona transforms expression, never task selection, permissions or rewards.
	private string PersonaSpeechKey(string key)
	{
		if (!Profile.EvilMode) return key;
		return key switch {
			"Social.Autonomous.Context.LowEnergy" => "Evil.Ambient.Tired",
			"Social.Autonomous.Context.Hurt" => "Evil.Ambient.Hurt",
			"Social.Autonomous.Context.Rain" => "Evil.Ambient.Rain",
			"Social.Autonomous.Context.Underground" => "Evil.Ambient.Underground",
			"Social.Autonomous.Context.Night" => "Evil.Ambient.Night",
			"Conversation.Reflection.Reply.First" => "Evil.Reflection.First",
			"Conversation.Reflection.Reply.Second" => "Evil.Reflection.Second",
			"Conversation.Reflection.Reply.Third" => "Evil.Reflection.Third",
			_ when key.StartsWith("Social.Autonomous.", StringComparison.Ordinal)
				&& !key.StartsWith("Social.Autonomous.Context.", StringComparison.Ordinal)
				|| key.StartsWith("Conversation.Chatter.", StringComparison.Ordinal)
				=> CurrentIntentContext() == CompanionIntentContext.Building ? "Evil.Ambient.Building"
					: (chatterSequence % 3) switch { 0 => "Evil.Ambient.Scheme0", 1 => "Evil.Ambient.Scheme1", _ => "Evil.Ambient.Scheme2" },
			_ => key
		};
	}

	private void QueueEvilCelebration(CompanionInitiativeKind kind)
	{
		if (!Profile.EvilMode || evilCelebrationCooldown > 0 || evilCelebration is not null) return;
		evilCelebration = kind;
		evilCelebrationTicks = 5400;
	}
	private void ClearEvilCelebration()
	{
		evilCelebration = null;
		evilCelebrationTicks = 0;
	}

	private void TickEvilPersona()
	{
		if (evilCelebrationCooldown > 0) evilCelebrationCooldown--;
		if (!Profile.EvilMode || Profile.WorkPaused || !Profile.AutonomyEnabled || Command == StayCommand) {
			ClearEvilCelebration();
			return;
		}
		if (evilCelebration is not { } kind) return;
		if (--evilCelebrationTicks <= 0) { evilCelebration = null; return; }
		if (speechTimer > 0 || HasPendingQuestion || HasPendingInitiative || nativeExpressionTicks > 0
			|| guardianTarget >= 0 || activeJob != CompanionJob.None || autonomyActivity != AutonomyActivity.None
			|| IsAttendingCritter || socialNpcTarget >= 0 || pendingCritterLossKey.Length > 0 || pendingCritterNotice is not null
			|| !TryGetOwner(out Player owner) || owner.dead || FindBoundSigil() is null
			|| Vector2.DistanceSquared(NPC.Center, owner.Center) > 400f * 400f) return;
		evilCelebration = null;
		evilCelebrationCooldown = 7200;
		ShowNativeExpression(CompanionAbilityRegistry.Find(kind)!.Emote, EmoteID.EmoteLaugh, EmoteID.EmotionLove);
		SpeakLocalized($"Evil.Outcomes.{kind}");
	}
}
