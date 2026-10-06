#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Common.UI;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.Net;

namespace SoulmatesRegressionProbe;

public sealed partial class EngineChecks
{
	private static void CheckAbilityRegistry(Action<bool, string> check)
	{
		const BindingFlags flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
		Type activityType = typeof(SoulboundCompanion).GetNestedType("AutonomyActivity", flags)!;
		string[] names = { "FetchItem", "AssistMining", "TendForest", "InspectTreasure", "InviteCritter", "CatchCritter" };
		string[] keys = { "gatheringInitiative", "miningInitiative", "forestryInitiative", "treasureInitiative", "critterCompanyInitiative", "critterCollectInitiative" };
		byte[] codes = { 1, 2, 4, 3, 5, 6 };
		var rules = (CompanionQuickAction[])typeof(CompanionWheelSystem).GetField("InitiativeRules", flags)!.GetValue(null)!;
		check(rules.SequenceEqual(CompanionAbilityRegistry.All.Select(a => a.PolicyAction).Append(CompanionQuickAction.ResetInitiativeRules)),
			"Ability registration differs from the visible rules menu");
		for (int i = 0; i < keys.Length; i++) {
			CompanionAbilityDefinition ability = CompanionAbilityRegistry.All[i];
			check(ability.SaveKey == keys[i] && ability.ActivityCode == codes[i] && ability.ActivityName == names[i]
				&& Convert.ToByte(Enum.Parse(activityType, names[i])) == codes[i], "Legacy ability code/name/key changed: " + i);
			check(ReferenceEquals(ability, CompanionAbilityRegistry.ForActivity(codes[i]))
				&& ReferenceEquals(ability, CompanionAbilityRegistry.Find(ability.PolicyAction)), "Ability lookup is ambiguous");
			check(ability.Emote >= 0 && ability.Emote < EmoteID.Count && ability.RuleEmote >= 0 && ability.RuleEmote < EmoteID.Count,
				"Ability uses an invalid native symbol");
			var profile = new CompanionProfile { Energy = ability.MinimumEnergy, Mood = ability.MinimumMood };
			check(ability.HasStamina(profile), "Ability rejected its exact stamina boundary");
			profile.Energy--; check(!ability.HasStamina(profile), "Ability ignored its energy boundary");
			profile.Energy++; profile.Mood--; check(!ability.HasStamina(profile), "Ability ignored its mood boundary");
			ability.WritePolicy(profile, (CompanionInitiativePolicy)255);
			check(profile.GetInitiativePolicy(ability.Kind) == CompanionInitiativePolicy.Ask,
				"Invalid live permission leaked an undefined enum into UI or consent");
			check(CompanionProfile.Load(new TagCompound { [keys[i]] = (byte)255 }).GetInitiativePolicy(ability.Kind) == CompanionInitiativePolicy.Ask,
				"Corrupt saved permission did not become Ask");
		}

		// Hand-written 0.19.9 empty-cargo wire fixture, independent of the new registry serializer.
		var original = new CompanionProfile {
			Id = Guid.Parse("ef7f793b-e541-4f6d-987d-78d536710ad1"), Name = "Beta fixture", LastMemory = "compatibility fixture",
			Bond = 11, Mood = 83, Energy = 72, JobsCompleted = 3, Experience = 24, DefeatedEnemies = 5, Interactions = 6,
			GatheringInsight = 7, MiningInsight = 8, ForestryInsight = 9, CombatInsight = 10, ExplorationInsight = 12,
			ObservedPickPower = 55, QuestionCadence = CompanionQuestionCadence.Chatty, CritterMode = CompanionCritterMode.Company,
			GatheringInitiative = CompanionInitiativePolicy.Always, MiningInitiative = CompanionInitiativePolicy.Never,
			ForestryInitiative = CompanionInitiativePolicy.Ask, TreasureInitiative = CompanionInitiativePolicy.Always,
			CritterCompanyInitiative = CompanionInitiativePolicy.Never, CritterCollectInitiative = CompanionInitiativePolicy.Always
		};
		using var legacy = new MemoryStream();
		using (var writer = new BinaryWriter(legacy, Encoding.UTF8, true)) {
			writer.Write(original.Id.ToString()); writer.Write(original.Name);
			writer.Write((byte)original.Personality); writer.Write((byte)original.Talent); writer.Write((byte)original.Essence);
			writer.Write((byte)original.Form); writer.Write((byte)original.Aura); writer.Write((byte)original.Muse);
			writer.Write((byte)original.Voice); writer.Write((byte)original.Trinket); writer.Write((byte)original.Routine);
			writer.Write((byte)original.MiningApproach); writer.Write(original.AutonomyEnabled);
			writer.Write((byte)1); writer.Write((byte)2); writer.Write((byte)0); writer.Write((byte)1);
			foreach (int value in new[] { 11, 83, 72, 3, 24, 5, 6 }) writer.Write(value);
			foreach (byte value in new byte[] { 7, 8, 9, 10, 12 }) writer.Write(value);
			writer.Write("compatibility fixture"); writer.Write((short)55);
			writer.Write((byte)0); // Mining knowledge.
			writer.Write((byte)0); writer.Write((byte)0); writer.Write("0"); // Pack, resources, wallet.
			writer.Write((byte)0); writer.Write((byte)0); // Memories, relationships.
			writer.Write((byte)CompanionCritterMode.Company); writer.Write(false);
			writer.Write((ushort)0); writer.Write((ushort)0); // Automatic mining filters.
			writer.Write((byte)CompanionQuestionCadence.Chatty); writer.Write((byte)2); writer.Write((byte)1);
		}
		using var current = new MemoryStream();
		using (var writer = new BinaryWriter(current, Encoding.UTF8, true)) original.Write(writer);
		check(current.ToArray().Take((int)legacy.Length).SequenceEqual(legacy.ToArray()), "Profile write changed the 0.19.9 wire prefix");
		// 0.21 extends the profile; mixed mod versions are not a supported network session.
		using (var extension = new BinaryWriter(legacy, Encoding.UTF8, true)) {
			extension.Write((byte)CompanionMiningDirection.Auto); extension.Write((byte)CompanionTunnelEnd.Passage); extension.Write(false);
			extension.Write(0); // No selected carried pet in an old profile.
		}
		check(current.ToArray().SequenceEqual(legacy.ToArray()), "Profile write has an unexpected work extension");
		legacy.Position = 0;
		using var reader = new BinaryReader(legacy, Encoding.UTF8, true);
		CompanionProfile loaded = CompanionProfile.Read(reader);
		check(legacy.Position == legacy.Length && loaded.Id == original.Id && loaded.Name == original.Name
			&& loaded.Energy == 72 && loaded.ObservedPickPower == 55 && loaded.QuestionCadence == original.QuestionCadence,
			"Legacy profile fixture misaligned non-policy fields");
		foreach (CompanionAbilityDefinition ability in CompanionAbilityRegistry.All)
			check(loaded.GetInitiativePolicy(ability.Kind) == original.GetInitiativePolicy(ability.Kind), "Legacy permission byte changed");
		CompanionProfile clone = original.Clone(); clone.ResetInitiativePolicies();
		check(original.CritterCollectInitiative == CompanionInitiativePolicy.Always
			&& clone.GetInitiativePolicy(CompanionInitiativeKind.CritterCollect) == CompanionInitiativePolicy.Ask,
			"Cloned policy state is shared with its original");
		for (int combination = 0; combination < 729; combination++) {
			var profile = new CompanionProfile(); int remaining = combination;
			foreach (CompanionAbilityDefinition ability in CompanionAbilityRegistry.All) {
				profile.SetInitiativePolicy(ability.Kind, (CompanionInitiativePolicy)(remaining % 3)); remaining /= 3;
			}
			CompanionProfile copy = profile.Clone(), saved = CompanionProfile.Load(profile.Save());
			using var stream = new MemoryStream();
			using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) profile.Write(writer);
			stream.Position = 0;
			using var input = new BinaryReader(stream, Encoding.UTF8, true);
			CompanionProfile network = CompanionProfile.Read(input);
			check(stream.Position == stream.Length && CompanionAbilityRegistry.All.All(ability =>
				copy.GetInitiativePolicy(ability.Kind) == profile.GetInitiativePolicy(ability.Kind)
				&& saved.GetInitiativePolicy(ability.Kind) == profile.GetInitiativePolicy(ability.Kind)
				&& network.GetInitiativePolicy(ability.Kind) == profile.GetInitiativePolicy(ability.Kind)),
				"Simultaneous ability policies aliased during persistence: " + combination);
		}
	}

	private static void CheckActivityDispatch(Action<bool, string> check)
	{
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
		Player[] oldPlayers = (Player[])Main.player.Clone(); NPC[] oldNpcs = Main.npc; Item[] oldItems = Main.item;
		Projectile[] oldProjectiles = Main.projectile; Tilemap oldMap = Main.tile;
		int oldWidth = Main.maxTilesX, oldHeight = Main.maxTilesY, oldMode = Main.netMode, oldLocal = Main.myPlayer;
		RemoteClient[] oldClients = (RemoteClient[])Netplay.Clients.Clone();
		bool oldDedicated = Main.dedServ;
		try {
			Main.dedServ = true; Main.myPlayer = 255;
			for (int i = 0; i < Netplay.Clients.Length; i++) Netplay.Clients[i] = new RemoteClient { Socket = new ProbeSocket() };
			foreach (int mode in new[] { NetmodeID.SinglePlayer, NetmodeID.Server }) {
				Main.netMode = mode; Main.maxTilesX = Main.maxTilesY = 100;
				Main.tile = (Tilemap)Activator.CreateInstance(typeof(Tilemap), flags, null, new object[] { (ushort)100, (ushort)100 }, null)!;
				Main.npc = Enumerable.Range(0, oldNpcs.Length).Select(i => new NPC { whoAmI = i }).ToArray();
				Main.item = Enumerable.Range(0, oldItems.Length).Select(i => new Item { whoAmI = i }).ToArray();
				Main.projectile = Enumerable.Range(0, oldProjectiles.Length).Select(i => new Projectile { whoAmI = i }).ToArray();
				SoulboundCompanion Create(int ownerIndex, int npcIndex) {
					var owner = new Player { whoAmI = ownerIndex, active = true, Center = new Vector2(700, 500), statLife = 100, statLifeMax2 = 100 };
					Main.player[ownerIndex] = owner;
					NPC npc = Main.npc[npcIndex]; npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
					npc.active = true; npc.ai[0] = ownerIndex; npc.Center = owner.Center;
					var mate = (SoulboundCompanion)npc.ModNPC; mate.Profile.Name = "AETHER";
					mate.Profile.AutonomyEnabled = false; mate.Profile.QuestionCadence = CompanionQuestionCadence.Quiet;
					owner.inventory[0] = new Item(ModContent.ItemType<SoulboundSigil>());
					((SoulboundSigil)owner.inventory[0].ModItem).Profile = mate.Profile.Clone();
					return mate;
				}
				SoulboundCompanion first = Create(0, 20), second = Create(1, 21);
				CompanionActivityCoordinator Coordinator(SoulboundCompanion mate) => (CompanionActivityCoordinator)typeof(SoulboundCompanion)
					.GetProperty("Activities", flags)!.GetValue(mate)!;
				void Set(string field, object value) => typeof(SoulboundCompanion).GetField(field, flags)!.SetValue(first, value);
				object? Get(string field) => typeof(SoulboundCompanion).GetField(field, flags)!.GetValue(first);
				CompanionActivityCoordinator a = Coordinator(first), b = Coordinator(second);
				check(!ReferenceEquals(a, b), "Native NPC instances share one activity controller");
				first.AI(); second.AI();
				check(a.Current == CompanionActivityLane.Follow && b.Current == CompanionActivityLane.Follow,
					"Native AI did not use each instance's own controller");
				first.PerformQuickAction(CompanionQuickAction.Gather);
				first.AI(); int timer = (int)Get("jobTimer")!;
				check(a.Current == CompanionActivityLane.Assignment && first.CurrentJob == CompanionJob.Gather, "Assignment lane is not wired");
				first.PerformQuickAction(CompanionQuickAction.Pause);
				for (int tick = 0; tick < 120; tick++) first.AI();
				check(a.Current == CompanionActivityLane.Paused && (int)Get("jobTimer")! == timer
					&& first.CurrentJob == CompanionJob.Gather && b.Current == CompanionActivityLane.Follow, "Pause advanced work or affected another companion");
				NPC zombie = Main.npc[18]; zombie.SetDefaults(NPCID.Zombie); zombie.active = true; zombie.Center = first.NPC.Center + new Vector2(100, 0);
				first.AI();
				check(a.Current == CompanionActivityLane.Defense && (int)Get("jobTimer")! == timer
					&& first.CurrentJobName == SoulmatesText.Get("Status.Guarding"), "Defense did not preempt Pause or was hidden by the old assignment");
				zombie.active = false; first.AI();
				check(a.Current == CompanionActivityLane.Paused && (int)Get("jobTimer")! == timer, "Defense completion resumed paused work");
				first.PerformQuickAction(CompanionQuickAction.Resume); first.AI();
				check(a.Current == CompanionActivityLane.Assignment && (int)Get("jobTimer")! > timer, "Resume lost the native assignment");
				first.PerformQuickAction(CompanionQuickAction.Abort); first.AI();
				check(a.Current == CompanionActivityLane.Paused && first.CurrentJob == CompanionJob.None, "Abort left active work behind");
				first.PerformQuickAction(CompanionQuickAction.Resume); first.PerformQuickAction(CompanionQuickAction.Stay); first.AI();
				check(a.Current == CompanionActivityLane.Stay, "Stay lane is not wired");
				first.PerformQuickAction(CompanionQuickAction.Follow);
				first.Profile.AutonomyEnabled = true; first.Profile.CritterMode = CompanionCritterMode.Collect;
				first.Profile.CritterCollectInitiative = CompanionInitiativePolicy.Always;
				NPC bunny = Main.npc[19]; bunny.SetDefaults(NPCID.Bunny); bunny.active = true; bunny.Center = first.NPC.Center + new Vector2(150, 0);
				Set("autonomyDecisionTimer", 0); Set("townNpcInteractionCooldown", 9999);
				a.Tick();
				check(a.Current == CompanionActivityLane.AutomaticWork, "Shared scheduler did not own the critter selection frame");
				a.Tick();
				check(a.Current == CompanionActivityLane.Critters && first.CurrentJobName == SoulmatesText.Get("Status.Assignment",
					SoulmatesText.EnumName(CompanionInitiativeKind.CritterCollect)), "Critter visit was invisible or reported as watching");
				bunny.Center = first.NPC.Center;
				check(a.Tick() == CompanionActivityLane.Critters && !bunny.active && first.Profile.ItemCount(ItemID.Bunny) == 1,
					"Completed native capture released its frame to a second activity");
				check(a.Tick() == CompanionActivityLane.Follow, "Completed critter visit failed to release the next frame");
				first.Profile.AutonomyEnabled = false;
				Type activity = typeof(SoulboundCompanion).GetNestedType("AutonomyActivity", BindingFlags.NonPublic)!;
				Set("autonomyActivity", Enum.Parse(activity, "FetchItem"));
				check(a.Tick() == CompanionActivityLane.AutomaticWork && Get("autonomyActivity")!.ToString() == "None",
					"Terminating an automatic task let another movement writer run in its frame");
				check(a.Tick() == CompanionActivityLane.Follow, "Cancelled automatic task did not release the next frame");
				first.Recall(); check(a.Current == CompanionActivityLane.None && !first.NPC.active, "Recall retained a stale activity lane");
				check(b.Current == CompanionActivityLane.Follow && second.NPC.active, "Recall erased another companion's activity");
			}
		}
		finally {
			Main.npc = oldNpcs; Main.item = oldItems; Main.projectile = oldProjectiles; Main.tile = oldMap;
			for (int i = 0; i < oldPlayers.Length; i++) Main.player[i] = oldPlayers[i];
			Main.maxTilesX = oldWidth; Main.maxTilesY = oldHeight; Main.netMode = oldMode; Main.myPlayer = oldLocal; Main.dedServ = oldDedicated;
			for (int i = 0; i < Netplay.Clients.Length; i++) Netplay.Clients[i] = oldClients[i];
		}
	}
}
