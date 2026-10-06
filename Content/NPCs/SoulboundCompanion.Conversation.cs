#nullable enable
using System;
using BigInteger = System.Numerics.BigInteger;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Common.Feedback;
using Soulmates.Common.UI;
using Terraria;
using Terraria.GameContent.UI;
using Terraria.ID;

namespace Soulmates.Content.NPCs;

public sealed partial class SoulboundCompanion
{
	private CompanionQuestion pendingQuestion;
	private Guid questionId;
	private int questionTicks;
	private int companyQuestionCooldown = 1800;
	private int walletQuestionCooldown = 600;
	private BigInteger lastOfferedWallet;
	private int nearbyPickupTimer;
	private int walletLine;
	private int critterCareCooldown;
	private int personalQuestionCooldown = 7200;
	private int PersonalQuestionInterval => Profile.QuestionCadence == CompanionQuestionCadence.Chatty ? 7200 : 18000;
	private int pendingCritterCareTicks;
	private string pendingCritterCareName = "";

	public bool HasPendingQuestion => pendingQuestion != CompanionQuestion.None && questionTicks > 0;
	public CompanionQuestion PendingQuestion => pendingQuestion;
	public Guid QuestionId => questionId;
	public int QuestionTicks => Math.Max(0, questionTicks);

	private bool CanKeepQuestion() => Profile.AutonomyEnabled && !Profile.WorkPaused && Command != StayCommand
		&& Owner.active && !Owner.dead && FindBoundSigil() is not null
		&& activeJob == CompanionJob.None && Profile.Routine == CompanionJob.None
		&& !HasPendingInitiative
		&& socialNpcTarget < 0
		&& Vector2.DistanceSquared(NPC.Center, Owner.Center) <= 560f * 560f;

	private bool CanHoldQuestion() => CanKeepQuestion() && autonomyActivity == AutonomyActivity.None
		&& !IsAttendingCritter && guardianTarget < 0 && Profile.Energy >= 20 && Profile.Mood >= 15;

	private void UpdateChoiceConversation()
	{
		if (Main.netMode == NetmodeID.MultiplayerClient) {
			if (questionTicks > 0) questionTicks--;
			return;
		}
		if (companyQuestionCooldown > 0) companyQuestionCooldown--;
		if (walletQuestionCooldown > 0) walletQuestionCooldown--;
		if (critterCareCooldown > 0) critterCareCooldown--;
		if (personalQuestionCooldown > 0) personalQuestionCooldown--;
		if (pendingCritterCareTicks > 0 && personalQuestionCooldown <= 0 && CanHoldQuestion()
			&& speechTimer <= 0 && pendingCritterLossKey.Length == 0) pendingCritterCareTicks--;
		if (lastOfferedWallet > Profile.WalletCopper) lastOfferedWallet = Profile.WalletCopper;
		if (HasPendingQuestion) {
			if (!CanKeepQuestion() || --questionTicks <= 0) ClearChoiceQuestion();
			return;
		}
		if (Profile.QuestionCadence == CompanionQuestionCadence.Quiet || personalQuestionCooldown > 0
			|| !CanHoldQuestion() || speechTimer > 0 || pendingCritterLossKey.Length > 0 || pendingCritterNotice is not null
			|| Main.netMode == NetmodeID.SinglePlayer && !SoulmatesUIInput.CanPresentInitiative)
			return;
		if (pendingCritterCareTicks > 0 && critterCareCooldown <= 0)
			BeginChoiceQuestion(CompanionQuestion.CritterCare);
		else if (walletQuestionCooldown <= 0 && Profile.WalletCopper - lastOfferedWallet >= 100)
			BeginChoiceQuestion(CompanionQuestion.Wallet);
		else if (companyQuestionCooldown <= 0)
			BeginChoiceQuestion(CompanionQuestion.Company);
	}

	private bool BeginChoiceQuestion(CompanionQuestion question)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || HasPendingQuestion || !CanHoldQuestion()
			|| Profile.QuestionCadence == CompanionQuestionCadence.Quiet || personalQuestionCooldown > 0
			|| question is not (CompanionQuestion.Company or CompanionQuestion.Wallet or CompanionQuestion.CritterCare)
			|| question == CompanionQuestion.CritterCare && (pendingCritterCareTicks <= 0 || pendingCritterCareName.Length == 0)
			|| question == CompanionQuestion.Wallet && Profile.WalletCopper.IsZero)
			return false;
		pendingQuestion = question;
		questionId = Guid.NewGuid();
		questionTicks = InitiativeResponseTicks;
		personalQuestionCooldown = PersonalQuestionInterval;
		// Offers are spaced even if unanswered; the wallet has no artificial "full" state.
		if (question == CompanionQuestion.Wallet) {
			lastOfferedWallet = Profile.WalletCopper;
			walletQuestionCooldown = 10800;
			SpeakLocalized($"Conversation.Wallet.{Profile.Personality}.Line{walletLine++ % 2}", Profile.DescribeWallet());
			ShowNativeExpression(EmoteID.ItemGoldpile, EmoteID.EmotionLove, EmoteID.EmoteConfused);
		}
		else if (question == CompanionQuestion.CritterCare) {
			critterCareCooldown = 10800;
			pendingCritterCareTicks = 0;
			SpeakLocalized("Conversation.CritterCare.Question", pendingCritterCareName);
			pendingCritterCareName = "";
			ShowNativeExpression(EmoteID.CritterBunny, EmoteID.EmoteConfused);
		}
		else {
			companyQuestionCooldown = 14400;
			SpeakLocalized($"Conversation.Company.Question.{Profile.Voice}");
			ShowNativeEmote(EmoteID.EmoteConfused, 180);
		}
		SoulmatesFeedbackSystem.Record("conversation_question", ("question", question.ToString()));
		NPC.netUpdate = true;
		return true;
	}

	internal static CompanionQuestionCadence? QuestionCadenceFor(CompanionQuickAction action) => action switch {
		CompanionQuickAction.QuestionsQuiet => CompanionQuestionCadence.Quiet,
		CompanionQuickAction.QuestionsCalm => CompanionQuestionCadence.Calm,
		CompanionQuickAction.QuestionsChatty => CompanionQuestionCadence.Chatty,
		_ => null
	};

	private void ClearChoiceQuestion()
	{
		if (HasPendingQuestion) ClearNativeExpression();
		pendingQuestion = CompanionQuestion.None;
		questionId = Guid.Empty;
		questionTicks = 0;
		NPC.netUpdate = true;
	}

	public bool RespondToQuestion(Guid token, CompanionAnswer answer)
	{
		if (CompanionInventorySync.IsPending(Owner)) return false;
		if (Main.netMode == NetmodeID.MultiplayerClient || !Enum.IsDefined(answer)
			|| !HasPendingQuestion || token == Guid.Empty || token != questionId)
			return false;
		if (!CanKeepQuestion()) { ClearChoiceQuestion(); return false; }
		if (guardianTarget >= 0) return false;
		CompanionQuestion question = pendingQuestion;
		ClearChoiceQuestion();
		SoulmateEmoteObserver.ShowReply(Owner, QuestionAnswerEmote(question, (int)answer));
		string reply;
		bool reward = answer != CompanionAnswer.Later;
		if (answer == CompanionAnswer.Later)
			reply = "Conversation.Later";
		else if (question == CompanionQuestion.Wallet) {
			if (answer == CompanionAnswer.First) {
				using var inventorySync = new CompanionInventorySync(Owner);
				BigInteger before = Profile.WalletCopper;
				for (int slot = 0; slot < 4; slot++)
					TransferWalletCoins(CompanionProfile.WalletCoinType(slot), singleItem: false);
				bool moved = before > Profile.WalletCopper;
				reward = moved;
				reply = !moved ? "Conversation.Wallet.Full"
					: Profile.WalletCopper.IsZero ? "Conversation.Wallet.Delivered" : "Conversation.Wallet.Partial";
				SoulmatesFeedbackSystem.Record("wallet_gift", ("amount_copper", (before - Profile.WalletCopper).ToString()),
					("balance_copper", Profile.WalletCopper.ToString()));
			}
			else {
				Profile.Voice = answer == CompanionAnswer.Second ? CompanionVoice.Direct : CompanionVoice.Playful;
				reply = answer == CompanionAnswer.Second ? "Conversation.Wallet.Saving" : "Conversation.Wallet.TreasureHunters";
			}
			lastOfferedWallet = Profile.WalletCopper;
		}
		else if (question == CompanionQuestion.CritterCare) {
			Profile.Voice = answer switch {
				CompanionAnswer.First => CompanionVoice.Soft,
				CompanionAnswer.Second => CompanionVoice.Playful,
				_ => CompanionVoice.Direct
			};
			Profile.CritterMode = answer == CompanionAnswer.Second ? CompanionCritterMode.Company : CompanionCritterMode.Watch;
			if (Profile.CritterMode != CompanionCritterMode.Company) ReleaseCritterCompany();
			critterDecisionTimer = 0;
			reward = interactionRewardCooldown <= 0;
			if (reward) interactionRewardCooldown = 300;
			reply = $"Conversation.CritterCare.Reply.{answer}";
		}
		else {
			Profile.Voice = answer switch {
				CompanionAnswer.First => CompanionVoice.Direct,
				CompanionAnswer.Second => CompanionVoice.Playful,
				_ => CompanionVoice.Soft
			};
			if (answer == CompanionAnswer.First) {
				Profile.GatheringInitiative = CompanionInitiativePolicy.Always;
				attention.Reset();
				autonomyDecisionTimer = 0;
			}
			reply = $"Conversation.Company.Reply.{Profile.Voice}";
		}
		if (reward) {
			Profile.Mood = Math.Min(100, Profile.Mood + (Profile.Voice == CompanionVoice.Playful ? 4 : 2));
			Profile.ChangeBond(1);
		}
		ShowNativeEmote(answer == CompanionAnswer.Later ? EmoteID.EmoteWink : EmoteID.EmoteHappiness, 150);
		SpeakLocalized(reply);
		SyncPackState();
		SoulmatesFeedbackSystem.Record("conversation_answer", ("question", question.ToString()),
			("answer", answer.ToString()), ("voice", Profile.Voice.ToString()), ("mood", Profile.Mood), ("bond", Profile.Bond));
		return true;
	}

	internal static int QuestionAnswerEmote(CompanionQuestion question, int index) => question switch {
		CompanionQuestion.CritterCare => index switch {
			0 => EmoteID.EmotionLove, 1 => EmoteID.CritterBunny, 2 => EmoteID.EmotionAlert, _ => EmoteID.EmoteSleep
		},
		_ => index switch {
			0 => EmoteID.ItemGoldpile,
			1 => question == CompanionQuestion.Wallet ? EmoteID.EmoteWink : EmoteID.EmoteLaugh,
			2 => question == CompanionQuestion.Wallet ? EmoteID.EmoteLaugh : EmoteID.EmotionLove,
			_ => EmoteID.EmoteSleep
		}
	};

	private void UpdateNearbyPickup()
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || ++nearbyPickupTimer < 30) return;
		nearbyPickupTimer = 0;
		if (!Profile.AutonomyEnabled || Profile.GatheringInitiative != CompanionInitiativePolicy.Always
			|| Command == StayCommand || activeJob != CompanionJob.None || guardianTarget >= 0
			|| autonomyActivity != AutonomyActivity.None || HasPendingInitiative
			|| Vector2.DistanceSquared(NPC.Center, Owner.Center) > 440f * 440f) return;
		// Only already-approved, nearby drops; use the same conservation transaction as every other pickup.
		CollectNearbyLooseItems(NPC.Center, 64f, 2);
	}
}
