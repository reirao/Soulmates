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
	public void ToggleCommand()
	{
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
		CancelAssignment();
		Command = stay ? StayCommand : FollowCommand;
		brainState = stay ? BrainState.Stay : BrainState.Follow;
		stateTimer = 1;
		if (stay)
			idleTarget = NPC.Center;
		NPC.netUpdate = true;
	}

	public void AskToExplore()
	{
		CancelAssignment();
		Command = FollowCommand;
		brainState = BrainState.Inspect;
		stateTimer = Main.rand.Next(180, 320);
		idleTarget = Owner.Center + Main.rand.NextVector2Circular(120f, 60f);
		NPC.netUpdate = true;
	}

	public void StartJob(CompanionJob job)
	{
		Profile.Routine = job;
		BeginJob(job);
	}

	public CompanionConversationResult PerformQuickAction(CompanionQuickAction action)
	{
		if (action == CompanionQuickAction.ToggleAutonomy) {
			Profile.AutonomyEnabled = !Profile.AutonomyEnabled;
			if (!Profile.AutonomyEnabled)
				CancelAutonomousActivity();
			string reply = SoulmatesText.Get(Profile.AutonomyEnabled
				? "Autonomy.Enabled"
				: "Autonomy.Disabled");
			SyncProfileToBoundSigil();
			NPC.netUpdate = true;
			return new CompanionConversationResult(reply, true);
		}
		if (action == CompanionQuickAction.ResetInitiativeRules) {
			Profile.ResetInitiativePolicies();
			ClearPendingInitiative(120);
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

	public CompanionConversationResult Converse(TalkCategory category, int option, int memoryCursor)
	{
		DialogueResult result = CompanionDialogueEngine.Speak(Profile, category, option);
		Profile.ChangeBond(result.BondDelta);
		Profile.Mood = Math.Clamp(Profile.Mood + result.MoodDelta, 0, 100);
		Profile.Energy = Math.Clamp(Profile.Energy + result.EnergyDelta, 0, 100);
		ApplyConversationAction(result.Action);

		string reply = result.Action switch {
			SpeechAction.ShowPack => Profile.DescribePack(),
			SpeechAction.StoreHeldItem => StoreSelectedItem(),
			SpeechAction.UnloadPack => UnloadPack(),
			SpeechAction.RecallMemory => Profile.RecallMemory(memoryCursor),
			_ => result.Reply
		};
		if (result.Action is SpeechAction.StoreHeldItem or SpeechAction.UnloadPack)
			SyncOwnerInventory();
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
		if (!TryGetOwner(out Player owner)) {
			CancelAssignment();
			return;
		}
		CancelAutonomousActivity();
		ClearTownNpcInteraction();
		activeJob = job;
		jobTimer = 0;
		jobCount = 0;
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
		ClearTownNpcInteraction();
		CancelAutonomousActivity();
		activeJob = CompanionJob.None;
		Profile.Routine = CompanionJob.None;
		jobTimer = 0;
		jobCount = 0;
		gatherPause = 0;
		jobTargetItem = -1;
		gatherForestAction = ForestAction.None;
		areaEmptyTimer = 0;
		hasJobTarget = false;
		jobOrigin = Vector2.Zero;
		failedMiningTargets.Clear();
	}

	public void EquipTrinket(CompanionTrinket trinket)
	{
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
		speechText = string.IsNullOrWhiteSpace(text) ? "..." : text.Trim();
		if (speechText.Length > 180)
			speechText = speechText[..180];
		speechTimer = Math.Clamp(180 + speechText.Length * 2, 210, 360);
	}

	public void PerformEmote(CompanionEmote emote) => PerformEmote(emote, -1, applyRestCommand: true);

	public void ReactToNativeEmote(int emoteId)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || emoteId < 0
			|| emoteId >= EmoteBubbleLoader.EmoteBubbleCount
			|| nativeEmoteReactionCooldown > 0)
			return;
		nativeEmoteReactionCooldown = 45;
		PerformEmote(RelationshipEmoteFor(emoteId), emoteId, applyRestCommand: false);
	}

	private void PerformEmote(CompanionEmote emote, int nativeEmoteId, bool applyRestCommand)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || !Enum.IsDefined(emote))
			return;

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
		if (Main.rand.NextBool(4))
			SpeakLocalized($"Social.Emotes.{emote}.{Profile.Personality}");
		SyncProfileToBoundSigil();
		NPC.netUpdate = true;
		string message = leveledUp ? SoulmatesText.Get("Messages.LevelUp", Profile.Name, newLevel) : "";
		if (Main.netMode == NetmodeID.Server)
			global::Soulmates.Soulmates.SendProfileUpdate(Owner, this, message);
		else if (!string.IsNullOrEmpty(message))
			Main.NewText(message, Profile.EssenceColor);
	}

	private static CompanionEmote RelationshipEmoteFor(int emoteId) => emoteId switch {
		EmoteID.EmotionLove or EmoteID.EmoteKiss or EmoteID.EmoteWink => CompanionEmote.Heart,
		EmoteID.EmoteHappiness => CompanionEmote.Cheer,
		EmoteID.EmoteLaugh or EmoteID.EmoteSilly => CompanionEmote.Laugh,
		EmoteID.EmoteSleep => CompanionEmote.Rest,
		EmoteID.EmotionCry or EmoteID.EmoteSadness or EmoteID.EmoteFear or EmoteID.EmoteConfused
			=> CompanionEmote.Comfort,
		EmoteID.EmotionAnger or EmoteID.EmoteAnger or EmoteID.EmoteFight or EmoteID.EmoteScowl
			=> CompanionEmote.Cheer,
		_ => CompanionEmote.Wave
	};

	public void RecordDefeat(NPC defeated)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || defeated.type == NPCID.TargetDummy)
			return;
		bool creature = defeated.catchItem > 0 || defeated.type != NPCID.None && defeated.type < NPCID.Sets.CountsAsCritter.Length
			&& NPCID.Sets.CountsAsCritter[defeated.type];
		int experience = defeated.boss ? 12 : creature ? 1 : Math.Clamp(1 + defeated.lifeMax / 120, 1, 7);
		Profile.DefeatedEnemies++;
		bool leveledUp = Profile.GainExperience(experience, out int newLevel);

		if (creature) {
			Profile.Remember(CompanionMemoryKind.CreatureEncounter, detail: defeated.TypeName);
			int moodDelta = Profile.Personality switch {
				CompanionPersonality.Gentle => -5,
				CompanionPersonality.Curious => -1,
				CompanionPersonality.Mischievous => -1,
				_ => 0
			};
			Profile.Mood = Math.Clamp(Profile.Mood + moodDelta, 0, 100);
		}
		else {
			if (Profile.HasTalent(CompanionTalent.Guardian))
				Profile.Remember(CompanionMemoryKind.GuardianVictory, detail: defeated.TypeName);
			Profile.Mood = Math.Min(100, Profile.Mood + 1);
		}

		if (combatReactionCooldown <= 0 || defeated.boss || creature) {
			string kind = defeated.boss ? "Boss" : creature ? "Creature" : "Victory";
			CompanionEmote reaction = creature && Profile.Personality == CompanionPersonality.Gentle
				? CompanionEmote.Comfort
				: CompanionEmote.Cheer;
			StartEmote(reaction, 130);
			ShowNativeEmote(reaction, 140);
			if (defeated.boss || Main.rand.NextBool(5))
				SpeakLocalized($"Social.Combat.{kind}.{Profile.Personality}");
			combatReactionCooldown = creature ? 360 : 240;
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
		activeJob = CompanionJob.None;
		jobTimer = 0;
		jobCount = 0;
		gatherPause = 0;
		jobTargetItem = -1;
		gatherForestAction = ForestAction.None;
		areaEmptyTimer = 0;
		hasJobTarget = false;
		jobOrigin = Vector2.Zero;
		failedMiningTargets.Clear();
		Command = StayCommand;
		brainState = BrainState.Stay;
		idleTarget = NPC.Center;
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

	private void ShowNativeEmote(int emoteId, int duration)
	{
		if (emoteId >= 0 && emoteId < EmoteBubbleLoader.EmoteBubbleCount)
			EmoteBubble.NewBubble(emoteId, new WorldUIAnchor(NPC), duration);
	}

	private void SpeakLocalized(string key, string argument = "")
	{
		if (Main.netMode == NetmodeID.Server)
			global::Soulmates.Soulmates.SendCompanionSpeech(Owner, this, key, argument);
		else
			ShowSpeech(string.IsNullOrEmpty(argument) ? SoulmatesText.Get(key) : SoulmatesText.Get(key, argument));
	}

}
