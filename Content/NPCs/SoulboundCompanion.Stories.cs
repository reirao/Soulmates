#nullable enable
using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Common.Dialogue;
using Terraria;
using Terraria.GameContent.UI;
using Terraria.ID;

namespace Soulmates.Content.NPCs;

public sealed partial class SoulboundCompanion
{
	private int itemMemoryCooldown;
	private int ambientStoryCooldown = 600;
	private string lastAmbientKey = "";

	private void UpdateStoppedWork()
	{
		MoveTo(Owner.Center + new Vector2(-Owner.direction * 66f, -58f), 3f, 0.06f);
		RecoverEnergy(120, 3, recoverMood: true);
		UpdateFacing();
	}

	private CompanionConversationResult DiscussItems(CompanionItemTopic topic, int option, int cursor)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || !Enum.IsDefined(topic) || option is < 0 or > 2)
			return new(SoulmatesText.Get("TargetOrders.Invalid"), false);
		string name = SoulmatesText.Get($"Items.Topics.{topic}");
		Item[] cargo = Profile.CarriedItems.Where(item => !item.IsAir
			&& (topic == CompanionItemTopic.All || CompanionItemTopics.Classify(item) == topic)).ToArray();
		if (option == 2) return new(TellRememberedStory(cursor, topic), true);
		if (option == 0) {
			if (cargo.Length == 0) return new(SoulmatesText.Get("Items.Empty", name) + " " + DescribePresent(), true);
			string list = string.Join(", ", cargo.Take(4).Select(item => $"{item.Name} x{item.stack}"));
			if (cargo.Length > 4) list += SoulmatesText.Get("Pack.AndMoreStacks", cargo.Length - 4);
			return new(SoulmatesText.Get("Items.Carrying", name, list), true);
		}
		Item? example = !Owner.HeldItem.IsAir && (topic == CompanionItemTopic.All || CompanionItemTopics.Classify(Owner.HeldItem) == topic)
			? Owner.HeldItem : cargo.FirstOrDefault();
		if (example is null) return new(SoulmatesText.Get("Items.Empty", name) + " " + DescribePresent(), true);
		CompanionItemTopic actual = CompanionItemTopics.Classify(example);
		return new(SoulmatesText.Get("Items.Example", example.Name, SoulmatesText.Get($"Items.Topics.{actual}"))
			+ " " + SoulmatesText.Get($"Items.Uses.{actual}"), true);
	}

	private string TellRememberedStory(int cursor, CompanionItemTopic topic = CompanionItemTopic.All)
	{
		CompanionMemory[] memories = Profile.Memories.Where(memory => topic == CompanionItemTopic.All
			|| memory.Kind == CompanionMemoryKind.ItemFound && memory.Amount == (int)topic
			|| topic == CompanionItemTopic.Ores && memory.Kind == CompanionMemoryKind.MiningCompleted
			|| topic == CompanionItemTopic.Weapons && memory.Kind == CompanionMemoryKind.GuardianVictory
			|| topic == CompanionItemTopic.Recovery && memory.Kind == CompanionMemoryKind.HealerAid
			|| topic == CompanionItemTopic.Critters && memory.Kind is CompanionMemoryKind.CritterMet
				or CompanionMemoryKind.CritterLost or CompanionMemoryKind.InsectCompanion).Reverse().ToArray();
		if (memories.Length == 0) return SoulmatesText.Get("Stories.Beginning") + " " + DescribePresent();
		long position = Math.Abs((long)cursor);
		CompanionMemory memory = memories[(int)(position % memories.Length)];
		if (Profile.Voice == CompanionVoice.Direct) return memory.Describe();
		string theme = memory.Kind switch {
			CompanionMemoryKind.MiningCompleted or CompanionMemoryKind.GatheringCompleted or CompanionMemoryKind.ItemFound
				or CompanionMemoryKind.TreasureFound => "Work",
			CompanionMemoryKind.GuardianVictory or CompanionMemoryKind.HealerAid => "Protection",
			CompanionMemoryKind.ResidentMet or CompanionMemoryKind.ResidentFriend or CompanionMemoryKind.CritterMet
				or CompanionMemoryKind.CritterLost or CompanionMemoryKind.InsectCompanion => "Company",
			CompanionMemoryKind.Awakened => "Beginning",
			_ => "Growth"
		};
		string ending = Profile.Voice == CompanionVoice.Playful ? "Stories.Playful" : $"Stories.Personality.{Profile.Personality}";
		return SoulmatesText.Get($"Stories.Intro.Line{position % 3}") + " " + memory.Describe()
			+ " " + SoulmatesText.Get($"Stories.Reflection.{theme}") + " " + SoulmatesText.Get(ending);
	}

	private void RememberFoundItem(CompanionItemTopic topic, string name)
	{
		if (itemMemoryCooldown > 0 || Profile.Memories.Any(memory => memory.Kind == CompanionMemoryKind.ItemFound
			&& memory.Amount == (int)topic && memory.Detail == name)) return;
		itemMemoryCooldown = 3600;
		Profile.Remember(CompanionMemoryKind.ItemFound, (int)topic, name);
	}

	private string DescribePresent()
	{
		var observation = PresentObservation();
		return SoulmatesText.Get(observation.Key, observation.Name);
	}

	private (string Key, string Name, int Emote) PresentObservation()
	{
		if (Owner.statLife < Owner.statLifeMax2 / 3) return ("Social.Autonomous.Context.Hurt", "", EmoteID.EmoteFear);
		NPC? nearby = null;
		float distance = 260f * 260f;
		foreach (NPC npc in Main.ActiveNPCs) {
			float current = Vector2.DistanceSquared(npc.Center, NPC.Center);
			if (npc == NPC || npc.life <= 0 || current >= distance
				|| !Collision.CanHitLine(NPC.Center, 1, 1, npc.Center, 1, 1)) continue;
			distance = current;
			nearby = npc;
		}
		if (nearby is not null) return (!nearby.friendly && nearby.damage > 0 ? "Stories.Present.Threat"
			: nearby.townNPC ? "Stories.Present.Resident" : "Stories.Present.Creature", nearby.FullName,
			!nearby.friendly && nearby.damage > 0 ? EmoteID.EmoteFear : EmoteID.EmoteHappiness);
		Item? drop = null;
		distance = 220f * 220f;
		foreach (Item item in Main.ActiveItems) {
			float current = Vector2.DistanceSquared(item.Center, NPC.Center);
			if (item.IsAir || current >= distance || !Collision.CanHitLine(NPC.Center, 1, 1, item.Center, 1, 1)) continue;
			distance = current;
			drop = item;
		}
		if (drop is not null) return ("Stories.Present.Item", drop.Name, EmoteID.ItemGoldpile);
		if (Main.raining) return ("Social.Autonomous.Context.Rain", "", EmoteID.WeatherRain);
		if (Owner.ZoneRockLayerHeight || Owner.ZoneUnderworldHeight) return ("Social.Autonomous.Context.Underground", "", EmoteID.ItemPickaxe);
		if (!Main.dayTime) return ("Social.Autonomous.Context.Night", "", EmoteID.EmoteSleep);
		if (Profile.Energy < 20) return ("Social.Autonomous.Context.LowEnergy", "", EmoteID.EmoteSleep);
		return ("Stories.Present.Quiet", "", EmoteID.EmoteWink);
	}

	private void UpdateAmbientStories()
	{
		if (itemMemoryCooldown > 0) itemMemoryCooldown--;
		if (ambientStoryCooldown > 0) ambientStoryCooldown--;
		if (Main.netMode == NetmodeID.MultiplayerClient || !Profile.AutonomyEnabled || speechTimer > 0
			|| HasPendingQuestion || HasPendingInitiative || guardianTarget >= 0 || socialNpcTarget >= 0
			|| ambientStoryCooldown > 0 || !Owner.active || Owner.dead
			|| Vector2.DistanceSquared(NPC.Center, Owner.Center) > 520f * 520f) return;
		var observation = PresentObservation();
		string signature = observation.Key + observation.Name;
		bool changed = signature != lastAmbientKey;
		lastAmbientKey = signature;
		ambientStoryCooldown = changed ? 1800 : 3600;
		ShowNativeEmote(observation.Emote, 120);
		if (changed || chatterSequence++ % 3 != 0) SpeakLocalized(observation.Key, observation.Name);
		else if (Profile.Memories.Count > 0 && activeJob == CompanionJob.None && autonomyActivity == AutonomyActivity.None)
			SpeakLocalized("Stories.Told", TellRememberedStory(chatterSequence));
	}
}
