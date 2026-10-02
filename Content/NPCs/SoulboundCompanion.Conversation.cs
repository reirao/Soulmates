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

	public bool HasPendingQuestion => pendingQuestion != CompanionQuestion.None && questionTicks > 0;
	public CompanionQuestion PendingQuestion => pendingQuestion;
	public Guid QuestionId => questionId;
	public int QuestionTicks => Math.Max(0, questionTicks);

	private bool CanHoldQuestion() => Profile.AutonomyEnabled && Command != StayCommand
		&& Owner.active && !Owner.dead && FindBoundSigil() is not null && Profile.Energy >= 20 && Profile.Mood >= 15
		&& activeJob == CompanionJob.None && Profile.Routine == CompanionJob.None
		&& autonomyActivity == AutonomyActivity.None && !HasPendingInitiative
		&& guardianTarget < 0 && socialNpcTarget < 0 && !IsAttendingCritter
		&& Vector2.DistanceSquared(NPC.Center, Owner.Center) <= 400f * 400f;

	private void UpdateChoiceConversation()
	{
		if (Main.netMode == NetmodeID.MultiplayerClient) {
			if (questionTicks > 0) questionTicks--;
			return;
		}
		if (companyQuestionCooldown > 0) companyQuestionCooldown--;
		if (walletQuestionCooldown > 0) walletQuestionCooldown--;
		if (lastOfferedWallet > Profile.WalletCopper) lastOfferedWallet = Profile.WalletCopper;
		if (HasPendingQuestion) {
			if (!CanHoldQuestion() || --questionTicks <= 0) ClearChoiceQuestion();
			return;
		}
		if (!CanHoldQuestion() || speechTimer > 0
			|| Main.netMode == NetmodeID.SinglePlayer && !SoulmatesUIInput.CanPresentInitiative)
			return;
		if (walletQuestionCooldown <= 0 && Profile.WalletCopper - lastOfferedWallet >= 100)
			BeginChoiceQuestion(CompanionQuestion.Wallet);
		else if (companyQuestionCooldown <= 0)
			BeginChoiceQuestion(CompanionQuestion.Company);
	}

	private bool BeginChoiceQuestion(CompanionQuestion question)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || HasPendingQuestion || !CanHoldQuestion()
			|| question is not (CompanionQuestion.Company or CompanionQuestion.Wallet)
			|| question == CompanionQuestion.Wallet && Profile.WalletCopper.IsZero)
			return false;
		pendingQuestion = question;
		questionId = Guid.NewGuid();
		questionTicks = InitiativeResponseTicks;
		// Offers are spaced even if unanswered; the wallet has no artificial "full" state.
		if (question == CompanionQuestion.Wallet) {
			lastOfferedWallet = Profile.WalletCopper;
			walletQuestionCooldown = 10800;
			SpeakLocalized($"Conversation.Wallet.{Profile.Personality}.Line{walletLine++ % 2}", Profile.DescribeWallet());
			ShowNativeEmote(EmoteID.ItemGoldpile, 180);
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

	private void ClearChoiceQuestion()
	{
		pendingQuestion = CompanionQuestion.None;
		questionId = Guid.Empty;
		questionTicks = 0;
		NPC.netUpdate = true;
	}

	public bool RespondToQuestion(Guid token, CompanionAnswer answer)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || !Enum.IsDefined(answer)
			|| !HasPendingQuestion || token == Guid.Empty || token != questionId)
			return false;
		if (!CanHoldQuestion()) { ClearChoiceQuestion(); return false; }
		CompanionQuestion question = pendingQuestion;
		ClearChoiceQuestion();
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
