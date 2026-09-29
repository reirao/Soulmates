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
	private void UpdateSocialState()
	{
		if (speechTimer > 0)
			speechTimer--;
		else
			speechText = "";
		if (interactionRewardCooldown > 0)
			interactionRewardCooldown--;
		if (nativeEmoteReactionCooldown > 0)
			nativeEmoteReactionCooldown--;
		if (combatReactionCooldown > 0)
			combatReactionCooldown--;
		if (townNpcInteractionCooldown > 0)
			townNpcInteractionCooldown--;
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
		if (activeJob != CompanionJob.None || autonomyActivity != AutonomyActivity.None
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
		if (Main.rand.NextBool(4))
			SpeakLocalized(key);
	}

	private void UpdateLearningFromOwner()
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || --learningObservationTimer > 0
			|| Vector2.DistanceSquared(NPC.Center, Owner.Center) > 520f * 520f)
			return;

		learningObservationTimer = 60;
		if (Owner.chest >= 0) {
			learningObservationTimer = 180;
			ObserveOwnerActivity(LearnedBehavior.Exploration);
			return;
		}
		if (!Owner.controlUseItem && Owner.itemAnimation <= 0)
			return;

		Item held = Owner.HeldItem;
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
		int before = Profile.GetInsight(behavior);
		bool unlockedForester = Profile.Observe(behavior, amount);
		int after = Profile.GetInsight(behavior);
		if (after == before)
			return;

		SyncProfileToBoundSigil();
		bool syncMilestone = unlockedForester || after % 5 == 0;
		string message = "";
		if (unlockedForester) {
			Profile.GainExperience(5, out _);
			StartEmote(CompanionEmote.Cheer, 120);
			ShowNativeEmote(CompanionEmote.Cheer, 140);
			SpeakLocalized("Learning.ForesterUnlocked");
			message = SoulmatesText.Get("Messages.PerkUnlocked", Profile.Name, SoulmatesText.Get("Learning.Forester"));
		}

		if (!syncMilestone)
			return;
		NPC.netUpdate = true;
		if (Main.netMode == NetmodeID.Server)
			global::Soulmates.Soulmates.SendProfileUpdate(Owner, this, message);
		else if (!string.IsNullOrEmpty(message))
			Main.NewText(message, Profile.EssenceColor);
	}

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
		if (Command == StayCommand || socialNpcTarget >= 0 || townNpcInteractionCooldown > 0
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
		socialNpcTimer = 300;
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
		if (!IsSocialTownNpc(townNpc)
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
			ShowNativeEmote(gesture, 150);
			EmoteBubble.NewBubbleNPC(new WorldUIAnchor(townNpc), 150, new WorldUIAnchor(NPC));
			socialNpcGreeted = true;
			socialNpcTimer = Math.Min(socialNpcTimer, 150);
			NPC.netUpdate = true;
		}
		return true;
	}

	private CompanionEmote PersonalityGesture() => Profile.Personality switch {
		CompanionPersonality.Gentle => CompanionEmote.Heart,
		CompanionPersonality.Brave => CompanionEmote.Cheer,
		CompanionPersonality.Mischievous => CompanionEmote.Laugh,
		CompanionPersonality.Curious => CompanionEmote.Wave,
		_ => CompanionEmote.Comfort
	};

	private static bool IsSocialTownNpc(NPC candidate) => candidate.active && candidate.townNPC
		&& candidate.life > 0 && !candidate.dontTakeDamageFromHostiles;

	private void ClearTownNpcInteraction()
	{
		socialNpcTarget = -1;
		socialNpcTimer = 0;
		socialNpcGreeted = false;
		if (Main.netMode != NetmodeID.MultiplayerClient)
			NPC.netUpdate = true;
	}

}
