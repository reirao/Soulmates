#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Soulmates.Common;
using Soulmates.Common.Feedback;
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
	public void ToggleCommand()
	{
		Profile.WorkPaused = false;
		CancelAssignment();
		Command = Command == StayCommand ? FollowCommand : StayCommand;
		brainState = Command == StayCommand ? BrainState.Stay : BrainState.Follow;
		stateTimer = 1;
		if (Command == StayCommand)
			idleTarget = NPC.Center;
		NPC.netUpdate = true;
	}

	public void SetCommand(bool stay)
	{
		Profile.WorkPaused = false;
		CancelAssignment();
		Command = stay ? StayCommand : FollowCommand;
		brainState = stay ? BrainState.Stay : BrainState.Follow;
		stateTimer = 1;
		if (stay)
			idleTarget = NPC.Center;
		NPC.netUpdate = true;
		SoulmatesFeedbackSystem.Record("command_state", ("action", stay ? "stay" : "follow"));
	}

	public void AskToExplore()
	{
		Profile.WorkPaused = false;
		CancelAssignment();
		Command = FollowCommand;
		brainState = BrainState.Inspect;
		stateTimer = Main.rand.Next(180, 320);
		idleTarget = Owner.Center + Main.rand.NextVector2Circular(120f, 60f);
		NPC.netUpdate = true;
	}

	public void StartJob(CompanionJob job)
	{
		SoulmatesFeedbackSystem.Record("job_started", ("job", job.ToString()),
			("energy", Profile.Energy), ("pack_load", Profile.PackLoad));
		Profile.Routine = job;
		BeginJob(job);
	}

	public CompanionConversationResult PerformQuickAction(CompanionQuickAction action)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || !Enum.IsDefined(action)
			|| action is CompanionQuickAction.Details or CompanionQuickAction.QuestionSettings)
			return new(SoulmatesText.Get("TargetOrders.Invalid"), false);
		if (QuestionCadenceFor(action) is CompanionQuestionCadence cadence) {
			Profile.QuestionCadence = cadence;
			ClearChoiceQuestion();
			personalQuestionCooldown = PersonalQuestionInterval;
			SyncProfileToBoundSigil();
			NPC.netUpdate = true;
			SoulmatesFeedbackSystem.Record("question_cadence", ("cadence", cadence.ToString()));
			return new(SoulmatesText.Get("Conversation.Settings.Selected", SoulmatesText.EnumName(cadence)), true);
		}
		if (action is CompanionQuickAction.Pause or CompanionQuickAction.Resume or CompanionQuickAction.Abort) {
			if (action == CompanionQuickAction.Abort) { CancelAssignment(); ReleaseCritterCompany(); }
			Profile.WorkPaused = action != CompanionQuickAction.Resume;
			if (Profile.WorkPaused) { ClearNativeExpression(); ClearChoiceQuestion(); ClearPendingInitiative(120); ClearTownNpcInteraction(); }
			SyncProfileToBoundSigil();
			NPC.netUpdate = true;
			SoulmatesFeedbackSystem.Record("work_control", ("action", action.ToString()));
			return new(SoulmatesText.Get($"WorkControl.{action}"), true);
		}
		if (TrySetCritterMode(action, out CompanionConversationResult critterResult))
			return critterResult;
		if (CompanionAbilityRegistry.Find(action) is { } ability) {
			CompanionInitiativeKind kind = ability.Kind;
			CompanionInitiativePolicy policy = (CompanionInitiativePolicy)(((int)Profile.GetInitiativePolicy(kind) + 1) % 3);
			Profile.SetInitiativePolicy(kind, policy);
			if (!directedCritterVisit && kind == (Profile.CritterMode == CompanionCritterMode.Collect
				? CompanionInitiativeKind.CritterCollect : CompanionInitiativeKind.CritterCompany)) critterTarget = null;
			if (autonomyActivity != AutonomyActivity.None && InitiativeKindFor(autonomyActivity) == kind)
				CancelAutonomousActivity(90);
			if (HasPendingInitiative && PendingInitiativeKind == kind) ClearPendingInitiative(90);
			attention.Reset();
			SoulmatesFeedbackSystem.Record("initiative_policy_changed", ("action", kind.ToString()), ("policy", policy.ToString()));
			SyncProfileToBoundSigil();
			NPC.netUpdate = true;
			return new CompanionConversationResult(SoulmatesText.Get("Autonomy.Initiative.PolicyChanged",
				SoulmatesText.EnumName(kind), SoulmatesText.EnumName(policy)), true);
		}
		if (TryGetMiningApproach(action, out CompanionMiningApproach approach)) {
			Profile.MiningApproach = approach;
			if (activeJob == CompanionJob.Mine && !directedJob) {
				miningPlanReady = false;
				plannedMiningTargets.Clear();
				plannedMiningCursor = 0;
				hasJobTarget = false;
				jobPlannedTotal = jobCount;
			}
			SoulmatesFeedbackSystem.Record("mining_approach_changed", ("approach", approach.ToString()),
				("job_active", activeJob == CompanionJob.Mine));
			SyncProfileToBoundSigil();
			NPC.netUpdate = true;
			return new CompanionConversationResult(
				SoulmatesText.Get($"UI.MiningApproaches.Selected.{approach}"), true);
		}
		if (action == CompanionQuickAction.ToggleAutonomy) {
			Profile.AutonomyEnabled = !Profile.AutonomyEnabled;
			if (!Profile.AutonomyEnabled) {
				ClearChoiceQuestion();
				CancelAutonomousActivity();
			}
			string reply = SoulmatesText.Get(Profile.AutonomyEnabled
				? "Autonomy.Enabled"
				: "Autonomy.Disabled");
			SyncProfileToBoundSigil();
			NPC.netUpdate = true;
			return new CompanionConversationResult(reply, true);
		}
		if (action == CompanionQuickAction.ResetInitiativeRules) {
			Profile.ResetInitiativePolicies();
			attention.Reset();
			CancelAutonomousActivity(120);
			if (!directedCritterVisit) critterTarget = null;
			SyncProfileToBoundSigil();
			NPC.netUpdate = true;
			return new CompanionConversationResult(SoulmatesText.Get("Autonomy.Initiative.RulesReset"), true);
		}

		(TalkCategory category, int option) request = action switch {
			CompanionQuickAction.Follow => (TalkCategory.Commands, 0),
			CompanionQuickAction.Stay => (TalkCategory.Commands, 1),
			CompanionQuickAction.Explore => (TalkCategory.Commands, 2),
			CompanionQuickAction.FindTreasure => (TalkCategory.Work, 0),
			CompanionQuickAction.Mine => (TalkCategory.Work, 1),
			CompanionQuickAction.Gather => (TalkCategory.Work, 2),
			_ => (TalkCategory.Care, 0)
		};
		return Converse(request.category, request.option, 0);
	}

	private static bool TryGetMiningApproach(CompanionQuickAction action, out CompanionMiningApproach approach)
	{
		approach = action switch {
			CompanionQuickAction.MiningTunnel => CompanionMiningApproach.Tunnel,
			CompanionQuickAction.MiningVein => CompanionMiningApproach.Vein,
			CompanionQuickAction.MiningSurface => CompanionMiningApproach.Surface,
			_ => CompanionMiningApproach.Adaptive
		};
		return action is CompanionQuickAction.MiningAdaptive or CompanionQuickAction.MiningTunnel
			or CompanionQuickAction.MiningVein or CompanionQuickAction.MiningSurface;
	}

	public CompanionConversationResult Converse(TalkCategory category, int option, int memoryCursor,
		CompanionItemTopic itemTopic = CompanionItemTopic.All)
	{
		if (category == TalkCategory.Items) return DiscussItems(itemTopic, option, memoryCursor);
		DialogueResult result = CompanionDialogueEngine.Speak(Profile, category, option);
		Profile.ChangeBond(result.BondDelta);
		Profile.Mood = Math.Clamp(Profile.Mood + result.MoodDelta, 0, 100);
		Profile.Energy = Math.Clamp(Profile.Energy + result.EnergyDelta, 0, 100);
		ApplyConversationAction(result.Action);

		string reply = result.Action switch {
			SpeechAction.ShowPack => Profile.DescribePack(),
			SpeechAction.StoreHeldItem => StoreSelectedItem(),
			SpeechAction.UnloadPack => UnloadPack(),
			SpeechAction.RecallMemory => TellRememberedStory(memoryCursor),
			SpeechAction.RecallResident => Profile.RecallResident(Main.ActiveWorldFileData.UniqueId, memoryCursor),
			_ => result.Reply
		};
		SyncProfileToBoundSigil();
		NPC.netUpdate = true;
		return new CompanionConversationResult(reply, result.Accepted);
	}

	private void ApplyConversationAction(SpeechAction action)
	{
		switch (action) {
			case SpeechAction.Follow:
				Profile.Routine = CompanionJob.None;
				SetCommand(stay: false);
				break;
			case SpeechAction.Stay:
			case SpeechAction.Rest:
				Profile.Routine = CompanionJob.None;
				SetCommand(stay: true);
				break;
			case SpeechAction.Explore:
				Profile.Routine = CompanionJob.None;
				AskToExplore();
				break;
			case SpeechAction.FindTreasure:
				StartJob(CompanionJob.FindTreasure);
				break;
			case SpeechAction.Mine:
				StartJob(CompanionJob.Mine);
				break;
			case SpeechAction.Gather:
				StartJob(CompanionJob.Gather);
				break;
			case SpeechAction.VoiceSoft:
				Profile.Voice = CompanionVoice.Soft;
				break;
			case SpeechAction.VoiceDirect:
				Profile.Voice = CompanionVoice.Direct;
				break;
			case SpeechAction.VoicePlayful:
				Profile.Voice = CompanionVoice.Playful;
				break;
		}
	}

	private void BeginJob(CompanionJob job)
	{
		ClearNativeExpression();
		ClearChoiceQuestion();
		if (!TryGetOwner(out Player owner)) {
			CancelAssignment();
			return;
		}
		CancelAutonomousActivity();
		Profile.WorkPaused = false;
		ClearTownNpcInteraction();
		ClearDirectedJob();
		activeJob = job;
		jobTimer = 0;
		jobCount = 0;
		jobPlannedTotal = 0;
		jobRecoveryPaused = false;
		miningPlanReady = false;
		plannedMiningTargets.Clear();
		plannedMiningCursor = 0;
		gatherPause = 0;
		jobTargetItem = -1;
		gatherForestAction = ForestAction.None;
		areaEmptyTimer = 0;
		hasJobTarget = false;
		jobOrigin = owner.Center;
		failedMiningTargets.Clear();
		if (job == CompanionJob.Gather)
			tendedForestTargets.Clear();
		Command = FollowCommand;
		NPC.netUpdate = true;
	}

	private void CancelAssignment()
	{
		ClearNativeExpression();
		critterTarget = null;
		directedCritterVisit = false;
		ClearChoiceQuestion();
		ClearTownNpcInteraction();
		CancelAutonomousActivity();
		activeJob = CompanionJob.None;
		Profile.Routine = CompanionJob.None;
		jobTimer = 0;
		jobCount = 0;
		jobPlannedTotal = 0;
		jobRecoveryPaused = false;
		miningPlanReady = false;
		plannedMiningTargets.Clear();
		plannedMiningCursor = 0;
		gatherPause = 0;
		jobTargetItem = -1;
		gatherForestAction = ForestAction.None;
		areaEmptyTimer = 0;
		hasJobTarget = false;
		jobOrigin = Vector2.Zero;
		failedMiningTargets.Clear();
		ClearDirectedJob();
	}

	public void EquipTrinket(CompanionTrinket trinket)
	{
		if (CompanionInventorySync.IsPending(Owner)) return;
		Profile.Trinket = trinket;
		if (trinket == CompanionTrinket.None)
			Profile.Remember(CompanionMemoryKind.TrinketRemoved);
		else
			Profile.Remember(CompanionMemoryKind.TrinketEquipped, detail: trinket.ToString());
		SyncProfileToBoundSigil();
		NPC.netUpdate = true;
	}

	public void ShowSpeech(string text)
	{
		string nextText = string.IsNullOrWhiteSpace(text) ? "..." : text.Trim();
		if (nextText.Length > 360)
			nextText = nextText[..360].TrimEnd() + "...";
		if (speechTimer > 0 && speechText == nextText)
			return;
		speechText = nextText;
		speechDuration = Math.Clamp(420 + speechText.Length * 3, 540, 1500);
		speechTimer = speechDuration;
		speechAnchorWorld = SpeechAnchorTarget();
		speechTrailWorld = NPC.Center;
		speechSide = TryGetOwner(out Player owner) && NPC.Center.X < owner.Center.X ? -1f : 1f;
	}

	public void PerformEmote(CompanionEmote emote) => PerformEmote(emote, -1, applyRestCommand: true);

	public void ReactToNativeEmote(int emoteId)
	{
		if (TryGetOwner(out Player pendingOwner) && CompanionInventorySync.IsPending(pendingOwner)) return;
		if (Main.netMode == NetmodeID.MultiplayerClient || emoteId < 0
			|| emoteId >= EmoteBubbleLoader.EmoteBubbleCount)
			return;
		if (HasPendingQuestion) {
			for (int i = 0; i < 4; i++)
				if (QuestionAnswerEmote(pendingQuestion, i) == emoteId) {
					RespondToQuestion(questionId, (CompanionAnswer)i);
					return;
				}
			return;
		}
		if (HasPendingInitiative) {
			foreach (CompanionInitiativeResponse response in Enum.GetValues<CompanionInitiativeResponse>())
				if (InitiativeResponseEmote(response) == emoteId) {
					RespondToInitiative(InitiativeId, response);
					return;
				}
			return;
		}
		if (nativeEmoteReactionCooldown > 0) return;
		if (CompanionRps.TryGetMove(emoteId, out RpsMove move)) {
			PlayRockPaperScissors(move);
			return;
		}
		nativeEmoteReactionCooldown = 45;
		SocialReply reply = CompanionSocialDialogue.Respond(emoteId, Profile.Personality);
		if (reply.Key == "Anger") {
			StartEmote(reply.Gesture, 130);
			ShowNativeEmote(reply.Emote, 180);
			if (interactionRewardCooldown <= 0) {
				Profile.Mood = Math.Max(0, Profile.Mood - 1);
				interactionRewardCooldown = 300;
				SyncPackState();
			}
		}
		else
			PerformEmote(reply.Gesture, reply.Emote, applyRestCommand: false);
		if (speechTimer <= 180)
			SpeakLocalized(CompanionSocialDialogue.ReplyKey(reply, Profile.Personality), Owner.name);
	}

	private void PerformEmote(CompanionEmote emote, int nativeEmoteId, bool applyRestCommand)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || !Enum.IsDefined(emote))
			return;
		ClearNativeExpression();

		bool rewarded = interactionRewardCooldown <= 0;
		bool leveledUp = false;
		int newLevel = Profile.Level;
		if (rewarded) {
			(int bond, int mood, int energy, int experience) = emote switch {
				CompanionEmote.Wave => (1, 1, 0, 1),
				CompanionEmote.Heart => (2, 4, 0, 2),
				CompanionEmote.Cheer => (1, 2, 2, 2),
				CompanionEmote.Comfort => (2, 5, 3, 2),
				CompanionEmote.Laugh => (1, 4, 0, 1),
				_ => (2, 2, 8, 2)
			};
			Profile.ChangeBond(bond);
			Profile.Mood = Math.Clamp(Profile.Mood + mood, 0, 100);
			Profile.Energy = Math.Clamp(Profile.Energy + energy, 0, 100);
			Profile.Interactions++;
			leveledUp = Profile.GainExperience(experience, out newLevel);
			if (Profile.Interactions == 1 || Profile.Interactions % 5 == 0)
				Profile.Remember(CompanionMemoryKind.SharedMoment, detail: emote.ToString());
			interactionRewardCooldown = 300;
		}

		if (applyRestCommand && emote == CompanionEmote.Rest)
			SetCommand(stay: true);
		StartEmote(emote, 150);
		if (nativeEmoteId >= 0)
			ShowNativeEmote(nativeEmoteId, 150);
		else
			ShowNativeEmote(emote, 150);
		if (nativeEmoteId < 0 && Main.rand.NextBool(2))
			SpeakLocalized($"Social.Emotes.{emote}.{Profile.Personality}");
		SyncProfileToBoundSigil();
		NPC.netUpdate = true;
		string message = leveledUp ? SoulmatesText.Get("Messages.LevelUp", Profile.Name, newLevel) : "";
		if (Main.netMode == NetmodeID.Server)
			global::Soulmates.Soulmates.SendProfileUpdate(Owner, this, message);
		else if (!string.IsNullOrEmpty(message))
			Main.NewText(message, Profile.EssenceColor);
	}

	public void RecordDefeat(NPC defeated)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || defeated.type == NPCID.TargetDummy)
			return;
		if (CompanionCritters.IsCritter(defeated)) {
			ObserveCritterLoss(defeated, playerAttack: false);
			return;
		}
		bool boss = defeated.boss;
		int life = defeated.lifeMax;
		string name = defeated.TypeName;
		CompanionInventorySync.RespondOrDefer(Owner.whoAmI, _ => RecordVictory(boss, life, name));
	}

	private void RecordVictory(bool boss, int life, string name)
	{
		int experience = boss ? 12 : Math.Clamp(1 + life / 120, 1, 7);
		Profile.DefeatedEnemies++;
		bool leveledUp = Profile.GainExperience(experience, out int newLevel);

		if (Profile.HasTalent(CompanionTalent.Guardian))
			Profile.Remember(CompanionMemoryKind.GuardianVictory, detail: name);
		Profile.Mood = Math.Min(100, Profile.Mood + 1);

		if (combatReactionCooldown <= 0 || boss) {
			string kind = boss ? "Boss" : "Victory";
			CompanionEmote reaction = CompanionEmote.Cheer;
			StartEmote(reaction, 130);
			ShowNativeEmote(reaction, 140);
			if (boss || Main.rand.NextBool(5))
				SpeakLocalized($"Social.Combat.{kind}.{Profile.Personality}");
			combatReactionCooldown = 240;
		}

		SyncProfileToBoundSigil();
		NPC.netUpdate = true;
		string message = leveledUp ? SoulmatesText.Get("Messages.LevelUp", Profile.Name, newLevel) : "";
		if (Main.netMode == NetmodeID.Server)
			global::Soulmates.Soulmates.SendProfileUpdate(Owner, this, message);
		else if (!string.IsNullOrEmpty(message))
			Main.NewText(message, Profile.EssenceColor);
	}

	private void StartEmote(CompanionEmote emote, int duration)
	{
		activeEmote = emote;
		emoteTimer = Math.Max(emoteTimer, duration);
		NPC.netUpdate = true;
	}

	private void PauseAssignmentForRecovery()
	{
		if (jobRecoveryPaused)
			return;
		jobRecoveryPaused = true;
		Command = StayCommand;
		brainState = BrainState.Stay;
		idleTarget = NPC.Center;
		ShowNativeEmote(EmoteID.EmoteSleep, 120);
		SoulmatesFeedbackSystem.Record("job_paused_for_recovery", ("job", activeJob.ToString()),
			("work_count", jobCount), ("planned_total", jobPlannedTotal), ("energy", Profile.Energy));
		SyncProfileToBoundSigil();
		NPC.netUpdate = true;
	}

	private void ShowNativeEmote(CompanionEmote emote, int duration)
	{
		int emoteId = emote switch {
			CompanionEmote.Heart => EmoteID.EmotionLove,
			CompanionEmote.Cheer => EmoteID.EmoteHappiness,
			CompanionEmote.Laugh => EmoteID.EmoteLaugh,
			CompanionEmote.Comfort => EmoteID.EmoteKiss,
			CompanionEmote.Rest => EmoteID.EmoteSleep,
			_ => EmoteID.EmoteWink
		};
		ShowNativeEmote(emoteId, duration);
	}

	private void SpeakLocalized(string key, string argument = "")
	{
		if (!ShowLocalizedSpeech(key, argument))
			return;
		if (Main.netMode == NetmodeID.Server)
			global::Soulmates.Soulmates.SendCompanionSpeech(Owner, this, key, argument);
	}

	public bool ShowLocalizedSpeech(string key, string argument = "")
	{
		bool ambient = key.StartsWith("Social.", StringComparison.Ordinal)
			|| key.StartsWith("Autonomy.Surprise.", StringComparison.Ordinal);
		if (ambient && (speechTimer > 180 || HasPendingQuestion || HasPendingInitiative))
			return false;
		ShowSpeech(SoulmatesText.Get(key, argument));
		return true;
	}

}
