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
	private void UpdateSocialState()
	{
		if (Main.netMode != NetmodeID.MultiplayerClient && socialNpcTarget >= 0
			&& (!Profile.AutonomyEnabled || HasPendingInitiative || Command == StayCommand
				|| activeJob != CompanionJob.None || autonomyActivity != AutonomyActivity.None || guardianTarget >= 0))
			ClearTownNpcInteraction();
		if (speechTimer > 0) {
			speechTimer--;
			if (speechAnchorWorld == Vector2.Zero) {
				speechAnchorWorld = SpeechAnchorTarget();
				speechTrailWorld = NPC.Center;
			}
			Vector2 previousAnchor = speechAnchorWorld;
			Vector2 target = SpeechAnchorTarget();
			if (Vector2.DistanceSquared(previousAnchor, target) > 240f * 240f) {
				speechAnchorWorld = target;
				speechTrailWorld = target;
			}
			else {
				speechAnchorWorld = Vector2.Lerp(previousAnchor, target, 0.24f);
				speechTrailWorld = Vector2.Lerp(speechTrailWorld, previousAnchor, 0.1f);
			}
		}
		else {
			speechText = "";
			speechDuration = 0;
			speechAnchorWorld = Vector2.Zero;
			speechTrailWorld = Vector2.Zero;
		}
		if (interactionRewardCooldown > 0)
			interactionRewardCooldown--;
		if (nativeEmoteReactionCooldown > 0)
			nativeEmoteReactionCooldown--;
		if (combatReactionCooldown > 0)
			combatReactionCooldown--;
		if (townNpcInteractionCooldown > 0)
			townNpcInteractionCooldown--;
		if (socialReplyDelay > 0) socialReplyDelay--;
		if (tendedForestResetTimer > 0)
			tendedForestResetTimer--;
		else {
			tendedForestTargets.Clear();
			tendedForestResetTimer = 3600;
		}
		if (emoteTimer <= 0)
			return;
		emoteTimer--;
		UpdateEmoteEffects();
	}

	private void UpdateAutonomousSocialBehavior()
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || --socialTimer > 0)
			return;
		int minimum = Profile.Personality == CompanionPersonality.Mischievous ? 1200 : 1500;
		int maximum = Profile.Personality == CompanionPersonality.Gentle ? 3000 : 2600;
		socialTimer = Main.rand.Next(minimum, maximum);
		if (!Profile.AutonomyEnabled || HasPendingInitiative
			|| activeJob != CompanionJob.None || autonomyActivity != AutonomyActivity.None
			|| guardianTarget >= 0 || socialNpcTarget >= 0 || Owner.dead
			|| Vector2.DistanceSquared(NPC.Center, Owner.Center) > 520f * 520f)
			return;
		if (TryBeginTownNpcInteraction())
			return;

		string key;
		if (Profile.Energy < 20)
			key = "Social.Autonomous.Context.LowEnergy";
		else if (Profile.Mood < 25)
			key = "Social.Autonomous.Context.LowMood";
		else if (Owner.statLife < Owner.statLifeMax2 / 3)
			key = "Social.Autonomous.Context.Hurt";
		else if (Main.raining)
			key = "Social.Autonomous.Context.Rain";
		else if (Owner.ZoneRockLayerHeight || Owner.ZoneUnderworldHeight)
			key = "Social.Autonomous.Context.Underground";
		else if (!Main.dayTime)
			key = "Social.Autonomous.Context.Night";
		else if (Command == StayCommand)
			key = "Social.Autonomous.Context.Stay";
		else {
			int line = chatterSequence++ % 3;
			key = $"Social.Autonomous.{Profile.Personality}.Line{line}";
		}

		CompanionEmote gesture = Profile.Energy < 20
			? CompanionEmote.Rest
			: Profile.Mood < 25 || Owner.statLife < Owner.statLifeMax2 / 3
				? CompanionEmote.Comfort
				: PersonalityGesture();
		StartEmote(gesture, 110);
		ShowNativeEmote(gesture, 130);
		if (Main.rand.NextBool(2))
			SpeakLocalized(key);
	}

	private void UpdateLearningFromOwner()
	{
		if (Main.netMode == NetmodeID.MultiplayerClient
			|| Vector2.DistanceSquared(NPC.Center, Owner.Center) > 520f * 520f)
			return;
		Item held = Owner.HeldItem;
		bool ownerUsingItem = Owner.controlUseItem || Owner.itemAnimation > 0;
		if (ownerUsingItem && held.pick > 0)
			ObserveMiningEnvironment(held);
		if (--learningObservationTimer > 0)
			return;

		learningObservationTimer = 60;
		if (Owner.chest >= 0) {
			learningObservationTimer = 180;
			ObserveOwnerActivity(LearnedBehavior.Exploration);
			return;
		}
		if (!ownerUsingItem)
			return;

		if (held.axe > 0 || held.createTile == TileID.Saplings || held.type == ItemID.Acorn)
			ObserveOwnerActivity(LearnedBehavior.Forestry);
		else if (held.pick > 0)
			ObserveOwnerActivity(LearnedBehavior.Mining);
		else if (held.damage > 0)
			ObserveOwnerActivity(LearnedBehavior.Combat);
	}

	public void ObserveOwnerActivity(LearnedBehavior behavior, int amount = 1)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || !Enum.IsDefined(behavior))
			return;
		SoulmatesFeedbackSystem.Record("behavior_observed", ("behavior", behavior.ToString()),
			("amount", amount));
		int before = Profile.GetInsight(behavior);
		LearnedBehavior? unlockedPerk = Profile.Observe(behavior, amount);
		int after = Profile.GetInsight(behavior);
		QueueImitation(behavior);
		if (after == before)
			return;

		bool syncMilestone = unlockedPerk is not null || after % 5 == 0;
		string message = "";
		if (unlockedPerk is LearnedBehavior learnedBehavior) {
			SoulmatesFeedbackSystem.Record("learned_perk_unlocked", ("behavior", learnedBehavior.ToString()));
			Profile.GainExperience(5, out _);
			StartEmote(CompanionEmote.Cheer, 120);
			ShowNativeEmote(CompanionEmote.Cheer, 140);
			SpeakLocalized($"Learning.Unlocked.{learnedBehavior}");
			message = SoulmatesText.Get("Messages.PerkUnlocked", Profile.Name,
				CompanionProfile.LearnedPerkName(learnedBehavior));
		}
		SyncProfileToBoundSigil();

		if (!syncMilestone)
			return;
		NPC.netUpdate = true;
		if (Main.netMode == NetmodeID.Server)
			global::Soulmates.Soulmates.SendProfileUpdate(Owner, this, message);
		else if (!string.IsNullOrEmpty(message))
			Main.NewText(message, Profile.EssenceColor);
	}

	private Vector2 SpeechAnchorTarget() => NPC.Center;

	private void QueueImitation(LearnedBehavior behavior)
	{
		if (!HasAdaptiveInstinct(behavior))
			return;
		if (behavior == LearnedBehavior.Combat) {
			attackCooldown = Math.Min(attackCooldown, 8);
			return;
		}

		bool newSignal = !imitationSignals.TryGetValue(behavior, out int strength);
		strength = Math.Min(5, strength + 1);
		imitationSignals[behavior] = strength;
		imitationCueTimer = Math.Max(imitationCueTimer, 240 + strength * 30);
		autonomyDecisionTimer = Math.Min(autonomyDecisionTimer, Math.Max(8, 28 - strength * 4));
		if (newSignal)
			SoulmatesFeedbackSystem.Record("imitation_cued", ("behavior", behavior.ToString()));
		else if (strength is 2 or 4)
			SoulmatesFeedbackSystem.Record("imitation_reinforced", ("behavior", behavior.ToString()),
				("strength", strength));
	}

	private bool HasAdaptiveInstinct(LearnedBehavior behavior) => Profile.HasLearnedPerk(behavior)
		|| (behavior switch {
			LearnedBehavior.Gathering or LearnedBehavior.Forestry => Profile.HasTalent(CompanionTalent.Gatherer),
			LearnedBehavior.Mining => Profile.HasTalent(CompanionTalent.Miner),
			LearnedBehavior.Combat => Profile.HasTalent(CompanionTalent.Guardian),
			LearnedBehavior.Exploration => Profile.HasTalent(CompanionTalent.TreasureSeeker),
			_ => false
		});

	private void PerformAutonomousMoment()
	{
		CompanionEmote gesture = PersonalityGesture();
		StartEmote(gesture, 100);
		ShowNativeEmote(gesture, 120);
		brainState = BrainState.Inspect;
		stateTimer = Main.rand.Next(90, 160);
		idleTarget = Owner.Center + Main.rand.NextVector2Circular(92f, 46f);
		if (Main.rand.NextBool(3))
			SpeakLocalized($"Autonomy.Surprise.{Profile.Personality}");
		NPC.netUpdate = true;
	}

	private bool TryBeginTownNpcInteraction()
	{
		if (Command == StayCommand || Profile.Energy < 20 || Profile.Mood < 20
			|| socialNpcTarget >= 0 || townNpcInteractionCooldown > 0
			|| !Main.rand.NextBool(2))
			return false;

		NPC? nearest = null;
		float bestDistance = 260f * 260f;
		for (int i = 0; i < Main.maxNPCs; i++) {
			NPC candidate = Main.npc[i];
			if (!IsSocialTownNpc(candidate)
				|| Vector2.DistanceSquared(candidate.Center, Owner.Center) > 560f * 560f)
				continue;
			float distance = Vector2.DistanceSquared(candidate.Center, NPC.Center);
			if (distance >= bestDistance)
				continue;
			bestDistance = distance;
			nearest = candidate;
		}
		if (nearest is null)
			return false;

		socialNpcTarget = nearest.whoAmI;
		socialNpcIdentity = nearest;
		socialNpcType = nearest.type;
		socialResidentName = nearest.GivenName;
		socialNpcTimer = 900;
		socialNpcGreeted = false;
		townNpcInteractionCooldown = Main.rand.Next(1800, 3000);
		NPC.netUpdate = true;
		return true;
	}

	private bool UpdateTownNpcInteraction()
	{
		if (socialNpcTarget < 0)
			return false;
		if (Command == StayCommand || socialNpcTarget >= Main.maxNPCs || socialNpcTimer-- <= 0) {
			ClearTownNpcInteraction();
			return false;
		}

		NPC townNpc = Main.npc[socialNpcTarget];
		if (!Profile.AutonomyEnabled || HasPendingInitiative || !IsSocialTownNpc(townNpc)
			|| Main.netMode != NetmodeID.MultiplayerClient && (!ReferenceEquals(townNpc, socialNpcIdentity)
				|| townNpc.type != socialNpcType || townNpc.GivenName != socialResidentName)
			|| Vector2.DistanceSquared(townNpc.Center, Owner.Center) > 640f * 640f) {
			ClearTownNpcInteraction();
			return false;
		}

		float side = NPC.Center.X <= townNpc.Center.X ? -1f : 1f;
		Vector2 meetingPoint = townNpc.Center + new Vector2(side * 62f, -44f + IdleBob() * 0.35f);
		MoveTo(meetingPoint, 5.2f, 0.09f);
		if (Main.netMode != NetmodeID.MultiplayerClient && !socialNpcGreeted
			&& Vector2.DistanceSquared(NPC.Center, townNpc.Center) < 126f * 126f) {
			CompanionEmote gesture = PersonalityGesture();
			facing = townNpc.Center.X < NPC.Center.X ? -1 : 1;
			StartEmote(gesture, 130);
			ShowNativeEmote(CompanionSocialDialogue.ResidentGreetingEmote(townNpc.type), 140);
			socialNpcGreeted = true;
			socialNpcTimer = Math.Min(socialNpcTimer, 780);
			CompanionRelationship relation = Profile.MeetResident(Main.ActiveWorldFileData.UniqueId,
				townNpc.type, townNpc.GivenName);
			string greeting = relation.Meetings == 1 ? "First" : relation.Rank;
			SpeakLocalized($"Social.Residents.Greetings.{greeting}", townNpc.GivenName);
			SyncPackState();
			EmoteBubble.NewBubbleNPC(new WorldUIAnchor(townNpc), 180, new WorldUIAnchor(NPC));
			socialReplyDelay = 600;
			NPC.netUpdate = true;
		}
		if (Main.netMode != NetmodeID.MultiplayerClient && socialNpcGreeted && !socialNpcReplied
			&& socialReplyDelay <= 0) {
			socialNpcReplied = true;
			RespondToTownNpc(townNpc);
		}
		return true;
	}

	public void ObserveResidentEmote(NPC resident, EmoteBubble bubble)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || !socialNpcGreeted || socialNpcReplied
			|| !ReferenceEquals(resident, socialNpcIdentity)) return;
		socialResidentBubble = bubble;
	}

	private void RespondToTownNpc(NPC resident)
	{
		CompanionRelationship? relation = Profile.FindRelationship(Main.ActiveWorldFileData.UniqueId,
			resident.type, resident.GivenName);
		if (relation is null) return;
		int nativeEmote = socialResidentBubble?.emote ?? -1;
		SocialReply reply = CompanionSocialDialogue.Respond(nativeEmote, Profile.Personality);
		Profile.RespondToResident(relation, nativeEmote, reply.Affinity);
		StartEmote(reply.Gesture, 130);
		ShowNativeEmote(reply.Emote, 180);
		string key = reply.Key == "Listening"
			? $"Social.Residents.Roles.{CompanionSocialDialogue.ResidentRole(resident.type)}"
			: CompanionSocialDialogue.ReplyKey(reply, Profile.Personality);
		SpeakLocalized(key, resident.GivenName);
		SyncPackState();
	}

	private CompanionEmote PersonalityGesture() => Profile.Personality switch {
		CompanionPersonality.Gentle => CompanionEmote.Heart,
		CompanionPersonality.Brave => CompanionEmote.Cheer,
		CompanionPersonality.Mischievous => CompanionEmote.Laugh,
		CompanionPersonality.Curious => CompanionEmote.Wave,
		_ => CompanionEmote.Comfort
	};

	private static bool IsSocialTownNpc(NPC candidate) => candidate.active && candidate.townNPC
		&& candidate.life > 0 && !candidate.dontTakeDamageFromHostiles && !string.IsNullOrWhiteSpace(candidate.GivenName);

	private void ClearTownNpcInteraction()
	{
		socialNpcTarget = -1;
		socialNpcTimer = 0;
		socialNpcGreeted = false;
		socialNpcIdentity = null;
		socialResidentBubble = null;
		socialNpcReplied = false;
		socialReplyDelay = 0;
		if (Main.netMode != NetmodeID.MultiplayerClient)
			NPC.netUpdate = true;
	}

}
