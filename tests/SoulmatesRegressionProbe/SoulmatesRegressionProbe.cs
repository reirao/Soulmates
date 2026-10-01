using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BigInteger = System.Numerics.BigInteger;
using Hjson;
using Soulmates.Common;
using Soulmates.Common.Dialogue;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Soulmates.Common.UI;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.GameContent.UI;
using Terraria.DataStructures;
using Terraria.Net;
using Terraria.Net.Sockets;

namespace SoulmatesRegressionProbe;

public sealed class SoulmatesRegressionProbe : Mod { }

public sealed class ProbeSocket : ISocket
{
	public void AsyncSend(byte[] data, int offset, int size, SocketSendCallback callback, object? state = null) { }
	public void AsyncReceive(byte[] data, int offset, int size, SocketReceiveCallback callback, object? state = null) { }
	public void Close() { }
	public void Connect(RemoteAddress address) { }
	public RemoteAddress GetRemoteAddress() => null!;
	public bool IsConnected() => false;
	public bool IsDataAvailable() => false;
	public void SendQueuedPackets() { }
	public bool StartListening(SocketConnectionAccepted callback) => false;
	public void StopListening() { }
}

public sealed class CatchSourceSpy : GlobalItem
{
	public static string LastSource = "none";
	public override void OnSpawn(Item item, IEntitySource source)
	{
		LastSource = source is EntitySource_Caught caught
			? $"caught {caught.Entity.GetType().Name}/{caught.Entity.whoAmI} by {caught.Catcher.GetType().Name}/{caught.Catcher.whoAmI}, item={item.type}"
			: source.GetType().Name;
	}
}

public sealed class EngineChecks : ModSystem
{
	public override void PostAddRecipes()
	{
		if (!Main.dedServ)
			return;
		int assertions = 0;
		var failures = new List<string>();
		void Check(bool condition, string label)
		{
			assertions++;
			if (!condition) failures.Add(label);
		}
		try {
			Mod soulmates = ModLoader.GetMod("Soulmates");
			Check(soulmates.Version == new Version(0, 15, 0), "Wrong packaged version: " + soulmates.Version);
			var core = new Item(ModContent.ItemType<Soulcore>());
			Check(!core.consumable, "Soulcore marked consumable");
			Check(!ItemLoader.ConsumeItem(core, new Player()), "Inventory right-click consumes the reusable Soulcore");
			CheckMenuInput(Check, core);
			CheckWorldPickup(Check);
			CheckCargoGrowthAndWallet(Check);
			CheckCargoLayout(Check);
			CheckAttention(Check);
			CheckPointingAndInitiative(Check);
			CheckRelationships(Check);
			CheckNature(Check);
			CheckResidentConversation(Check);
			foreach (string culture in new[] { "en-US", "de-DE" }) {
				LanguageManager.Instance.SetLanguage(culture);
				JsonValue catalog = HjsonValue.Parse(Encoding.UTF8.GetString(soulmates.GetFileBytes("Localization/" + culture + ".hjson")));
				void Walk(JsonValue value, string path)
				{
					if (value is JsonObject map) {
						foreach (var entry in map)
							Walk(entry.Value, path.Length == 0 ? entry.Key : path + "." + entry.Key);
					}
					else {
						Check(Language.Exists(path), culture + ": unregistered key " + path);
						Check(Language.GetTextValue(path) == (string)value, culture + ": wrong runtime text " + path);
					}
				}
				Walk(catalog, "");
				foreach (CompanionPersonality personality in Enum.GetValues<CompanionPersonality>()) {
					for (int emote = 0; emote < EmoteID.Count; emote++) {
						SocialReply reply = CompanionSocialDialogue.Respond(emote, personality);
						Check(Language.Exists("Mods.Soulmates." + CompanionSocialDialogue.ReplyKey(reply, personality)),
							culture + ": missing social response " + emote + "/" + personality);
						Check(reply.Emote >= 0 && reply.Emote < EmoteID.Count, "Invalid response emote");
						if (reply.Key == "Anger") Check(reply.Affinity < 0, "Anger created positive NPC affinity");
					}
				}
				var legacy = new CompanionProfile { LastMemory = "Mods.Soulmates.Memories.New" };
				Check(legacy.LastMemory == SoulmatesText.Get("Memories.New"), "Saved localization key not repaired");
				legacy.LastMemory = "Mods.Soulmates.Missing.OldKey";
				Check(legacy.LastMemory == SoulmatesText.Get("Memories.New"), "Broken saved key leaked into a reply");
				Check(new CompanionProfile().LastMemory == SoulmatesText.Get("Memories.New"), "Default memory cached too early");
			}

			var profile = new CompanionProfile { MiningApproach = CompanionMiningApproach.Surface };
			foreach (int type in new[] { ItemID.Gel, ItemID.Acorn, ItemID.StoneBlock, ItemID.Wood, ItemID.CopperOre, ItemID.DirtBlock }) {
				var item = new Item(type, 140);
				int limit = profile.CarryLimitFor(item);
				Check(profile.Store(item) == limit, "Incorrect insertion limit for item " + type);
				Check(profile.ItemCount(type) == limit && item.stack == 140 - limit, "Insertion lost or duplicated item " + type);
				var repeated = new Item(type, 40);
				Check(profile.Store(repeated) == 0 && repeated.stack == 40, "Repeated pickup exceeded reserve for " + type);
			}
			Check(profile.ResourceLoad == 6 && profile.PackLoad == 0, "Unlike item types merged or resources occupied equipment");
			var clone = profile.Clone();
			clone.Resources[0].stack--;
			Check(profile.Resources[0].stack == 50, "Cloned resources share mutable items");
			CompanionProfile loaded = CompanionProfile.Load(profile.Save());
			Check(loaded.ResourceLoad == 6 && loaded.ItemCount(ItemID.Acorn) == 50
				&& loaded.ItemCount(ItemID.Gel) == 50 && loaded.MiningApproach == CompanionMiningApproach.Surface,
				"Save/load lost cargo or mining approach");
			using (var stream = new MemoryStream()) {
				using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true)) profile.Write(writer);
				stream.Position = 0;
				using var reader = new BinaryReader(stream);
				CompanionProfile networked = CompanionProfile.Read(reader);
				Check(networked.ResourceLoad == 6 && networked.ItemCount(ItemID.Gel) == 50
					&& networked.MiningApproach == CompanionMiningApproach.Surface && stream.Position == stream.Length,
					"Network roundtrip lost cargo or changed packet layout");
			}
			var full = new CompanionProfile();
			FillEquipment(full);
			var overflow = new Item(ItemID.CopperShortsword, 1);
			Check(full.Store(overflow) == 0 && overflow.stack == 1, "Full equipment pack consumed an item");
			Check(full.Store(new Item(ItemID.Acorn, 12)) == 12, "Equipment fullness blocked separate resources");

			Main.maxTilesX = 100;
			Main.maxTilesY = 100;
			Main.tile = (Tilemap)Activator.CreateInstance(typeof(Tilemap), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
				null, new object[] { (ushort)100, (ushort)100 }, null)!;
			var companion = new SoulboundCompanion();
			var available = new HashSet<Point>();
			for (int x = 20; x < 44; x++) {
				Tile tile = Main.tile[x, 40];
				tile.HasTile = true;
				tile.TileType = TileID.Copper;
				available.Add(new Point(x, 40));
			}
			const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
			typeof(SoulboundCompanion).GetMethod("AppendConnectedOreVein", flags)!
				.Invoke(companion, new object[] { new Point(20, 40), available, 24 });
			var targets = (List<Point>)typeof(SoulboundCompanion).GetField("plannedMiningTargets", flags)!.GetValue(companion)!;
			Check(targets.Count == 24, "Vein limit counted empty neighbors instead of real ore blocks");
			companion.Profile.MiningApproach = CompanionMiningApproach.Surface;
			Point candidate = new(50, 50);
			foreach (Point point in new[] { candidate, new Point(49, 50), new Point(51, 50), new Point(50, 49), new Point(50, 51) }) {
				Tile tile = Main.tile[point.X, point.Y];
				tile.HasTile = true;
				tile.TileType = TileID.Stone;
			}
			MethodInfo allowed = typeof(SoulboundCompanion).GetMethod("IsAllowedByMiningApproach", flags)!;
			bool IsAllowed(ushort type) => (bool)allowed.Invoke(companion, new object[] { candidate, type })!;
			Check(!IsAllowed(TileID.Stone), "Surface allowed buried material");
			Tile neighbor = Main.tile[49, 50];
			neighbor.HasTile = false;
			Check(IsAllowed(TileID.Stone), "Surface rejected an exposed edge");
			neighbor.HasTile = true;
			Check(!IsAllowed(TileID.Stone), "Surface failed to recheck a newly blocked edge");
			companion.Profile.MiningApproach = CompanionMiningApproach.Adaptive;
			Check(!IsAllowed(TileID.Sand), "Adaptive allowed buried unstable material");
		}
		catch (Exception error) {
			failures.Add(error.ToString());
		}
		string report = $"ENGINE CHECKS: {assertions} assertions, {failures.Count} failures\n" + string.Join("\n", failures);
		File.WriteAllText(Path.Combine(Main.SavePath, "Soulmates-engine-checks.txt"), report);
		Console.WriteLine(report);
		// This isolated probe never joins or loads a player's world.
		Environment.Exit(failures.Count == 0 ? 0 : 1);
	}

	private static void CheckAttention(Action<bool, string> check)
	{
		var attention = new CompanionAttention();
		check(attention.Choose(Array.Empty<CompanionOpportunity>()) is null, "Empty attention invented a task");
		var offers = new[] {
			new CompanionOpportunity(CompanionInitiativeKind.Gathering, 100),
			new CompanionOpportunity(CompanionInitiativeKind.Mining, 0),
			new CompanionOpportunity(CompanionInitiativeKind.Forestry, 0),
			new CompanionOpportunity(CompanionInitiativeKind.Treasure, 0)
		};
		check(attention.Choose(offers) == CompanionInitiativeKind.Gathering, "Relevance did not win the first choice");
		int[] lastSeen = new int[4];
		for (int decision = 1; decision <= 500; decision++) {
			CompanionInitiativeKind? selected = attention.Choose(offers);
			check(selected.HasValue, "Actionable attention stopped choosing");
			lastSeen[(int)selected!.Value] = decision;
			if (decision > 20)
				for (int i = 0; i < 4; i++)
					check(decision - lastSeen[i] <= 20, "High-volume gathering starved task " + i);
		}
		attention.Defer(CompanionInitiativeKind.Gathering, 3);
		attention.Defer(CompanionInitiativeKind.Gathering, 1);
		for (int tick = 0; tick < 3; tick++) {
			check(!attention.IsReady(CompanionInitiativeKind.Gathering), "Deferral shortened or expired early");
			check(attention.Choose(offers) != CompanionInitiativeKind.Gathering, "Deferred task was selected");
			attention.Tick();
		}
		check(attention.IsReady(CompanionInitiativeKind.Gathering), "Deferral did not expire exactly");
		attention.Defer(CompanionInitiativeKind.Mining, int.MaxValue);
		attention.Reset();
		check(attention.IsReady(CompanionInitiativeKind.Mining), "Reset retained cooldown");
		check(attention.Choose(offers) == CompanionInitiativeKind.Gathering, "Reset retained waiting priority");
		check(!attention.IsReady((CompanionInitiativeKind)255), "Invalid attention kind was accepted");
		check(attention.Choose(new[] { new CompanionOpportunity((CompanionInitiativeKind)255, 100) }) is null,
			"Invalid opportunity was selected");
		check(attention.Choose(new[] { new CompanionOpportunity(CompanionInitiativeKind.Mining, -500) })
			== CompanionInitiativeKind.Mining, "Negative relevance was not bounded");
	}

	private static void CheckPointingAndInitiative(Action<bool, string> check)
	{
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
		Type type = typeof(SoulboundCompanion);
		Player oldPlayer = Main.player[0];
		Item oldItem = Main.item[10];
		int oldMode = Main.netMode;
		Tilemap oldMap = Main.tile;
		int oldWidth = Main.maxTilesX, oldHeight = Main.maxTilesY;
		try {
			Main.netMode = NetmodeID.SinglePlayer;
			Main.maxTilesX = Main.maxTilesY = 100;
			Main.tile = (Tilemap)Activator.CreateInstance(typeof(Tilemap), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
				null, new object[] { (ushort)100, (ushort)100 }, null)!;
			Main.player[0] = new Player { whoAmI = 0, active = true };
			Main.player[0].Center = new Vector2(400f, 400f);
			var npc = new NPC();
			npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
			npc.Center = new Vector2(400f, 360f);
			var companion = (SoulboundCompanion)npc.ModNPC;
			companion.Profile.Name = "AETHER";
			object Activity(int value) => Enum.ToObject(type.GetField("autonomyActivity", flags)!.FieldType, value);
			object Forest(int value) => Enum.ToObject(type.GetField("gatherForestAction", flags)!.FieldType, value);
			void Set(string field, object value) => type.GetField(field, flags)!.SetValue(companion, value);
			object Get(string field) => type.GetField(field, flags)!.GetValue(companion)!;
			void Tick() => type.GetMethod("UpdateAttentionClock", flags)!.Invoke(companion, null);
			bool Ask() => (bool)type.GetMethod("ConsiderInitiative", flags)!.Invoke(companion,
				new object[] { Activity(1), 10, Point.Zero, Forest(0) })!;
			Item Drop(int count = 7) => new Item(ItemID.Gel, count) {
				active = true, playerIndexTheItemIsReservedFor = 255, position = new Vector2(450f, 400f)
			};
			Main.item[10] = Drop();
			check(!Ask() && companion.HasPendingInitiative, "Ask did not wait for an answer");
			check((int)Get("autonomyDecisionTimer") < 3600, "Prompt incurred a full minute of decision debt");
			Main.item[10] = Drop();
			check(!companion.RespondToInitiative(CompanionInitiativeResponse.Always), "Reused world-item slot accepted stale Yes");
			check(companion.Profile.GatheringInitiative == CompanionInitiativePolicy.Ask,
				"Stale answer changed saved permission");
			((CompanionAttention)Get("attention")).Reset();
			Ask();
			check(companion.RespondToInitiative(CompanionInitiativeResponse.Yes), "Valid answer did not start work");
			check(!companion.HasPendingInitiative && Convert.ToInt32(Get("autonomyActivity")) == 1,
				"Accepted work retained pending prompt");
			companion.PerformQuickAction(CompanionQuickAction.GatheringPolicy);
			check(companion.Profile.GatheringInitiative == CompanionInitiativePolicy.Always,
				"Rule cycle did not select Always");
			companion.PerformQuickAction(CompanionQuickAction.GatheringPolicy);
			check(companion.Profile.GatheringInitiative == CompanionInitiativePolicy.Never
				&& Convert.ToInt32(Get("autonomyActivity")) == 0, "Never did not stop automatic work");
			check(!Ask() && !companion.HasPendingInitiative, "Never still asked for permission");
			companion.PerformQuickAction(CompanionQuickAction.GatheringPolicy);
			check(companion.Profile.GatheringInitiative == CompanionInitiativePolicy.Ask, "Rule cycle did not return to Ask");
			Ask();
			Set("pendingInitiativeTimer", 1);
			Set("guardianTarget", 1);
			Tick();
			check(!companion.HasPendingInitiative, "Combat froze the answer timeout");
			check(!((CompanionAttention)Get("attention")).IsReady(CompanionInitiativeKind.Gathering)
				&& ((CompanionAttention)Get("attention")).IsReady(CompanionInitiativeKind.Mining),
				"Unanswered question delayed unrelated tasks");
			((CompanionAttention)Get("attention")).Reset();
			Ask();
			Main.item[10].active = false;
			Tick();
			check(!companion.HasPendingInitiative, "Disappeared drop retained its question");
			Main.item[10] = Drop();
			((CompanionAttention)Get("attention")).Reset();
			Ask();
			companion.Profile.Energy = 15;
			check(!companion.RespondToInitiative(CompanionInitiativeResponse.Yes), "Low energy accepted stale permission");
			companion.Profile.Energy = 100;
			((CompanionAttention)Get("attention")).Reset();
			Ask();
			companion.Profile.Mood = 14;
			Tick();
			check(!companion.HasPendingInitiative, "Low mood retained an automatic question");
			companion.Profile.Mood = 100;
			((CompanionAttention)Get("attention")).Reset();
			Ask();
			companion.Profile.AutonomyEnabled = false;
			check(!companion.RespondToInitiative(CompanionInitiativeResponse.Always), "Disabled autonomy accepted an automatic order");

			int energy = companion.Profile.Energy, experience = companion.Profile.Experience;
			Set("activeJob", CompanionJob.Mine);
			CompanionConversationResult observation = companion.PerformDirectOrder(CompanionTargetOrder.Look, Point.Zero, 10);
			check(observation.Accepted && (CompanionJob)Get("activeJob") == CompanionJob.Mine,
				"Look interrupted an existing assignment");
			check(companion.Profile.ItemCount(ItemID.Gel) == 0 && Main.item[10].stack == 7
				&& companion.Profile.Energy == energy && companion.Profile.Experience == experience,
				"Look consumed cargo, energy, or farmed XP");
			companion.PerformDirectOrder(CompanionTargetOrder.Gather, Point.Zero, 10);
			check((bool)type.GetMethod("IsValidGatherTarget", flags)!.Invoke(companion, new object[] { 10 })!,
				"Directed drop was invalid on assignment");
			Main.item[10] = Drop();
			check(!(bool)type.GetMethod("IsValidGatherTarget", flags)!.Invoke(companion, new object[] { 10 })!,
				"Directed fetch followed a reused slot into a different drop");
			Tile ground = Main.tile[28, 30];
			ground.HasTile = true;
			ground.TileType = TileID.Grass;
			companion.Profile.Store(new Item(ItemID.Acorn, 3));
			CompanionConversationResult forest = companion.PerformDirectOrder(CompanionTargetOrder.Forest, new Point(28, 30), -1);
			check(forest.Accepted && (CompanionJob)Get("activeJob") == CompanionJob.Gather
				&& companion.Profile.Routine == CompanionJob.None, "Explicit forest task required autonomy or became a broad routine");
			check((Point)Get("jobTarget") == new Point(28, 29) && Convert.ToInt32(Get("gatherForestAction")) == 3,
				"Pointing at plantable ground did not resolve the air above it");
			check(ground.HasTile && ground.TileType == TileID.Grass && companion.Profile.ItemCount(ItemID.Acorn) == 3,
				"Forest selection changed terrain before arrival");
			check(!companion.PerformDirectOrder(CompanionTargetOrder.Forest, new Point(95, 95), -1).Accepted,
				"Out-of-range forest target was accepted");
			foreach (CompanionQuickAction action in new[] { CompanionQuickAction.MiningPolicy, CompanionQuickAction.ForestryPolicy, CompanionQuickAction.TreasurePolicy })
				companion.PerformQuickAction(action);
			CompanionProfile restored = CompanionProfile.Load(companion.Profile.Save());
			check(restored.MiningInitiative == CompanionInitiativePolicy.Always
				&& restored.ForestryInitiative == CompanionInitiativePolicy.Always
				&& restored.TreasureInitiative == CompanionInitiativePolicy.Always, "Individual rules did not survive save/load");
		}
		finally {
			Main.player[0] = oldPlayer;
			Main.item[10] = oldItem;
			Main.netMode = oldMode;
			Main.tile = oldMap;
			Main.maxTilesX = oldWidth;
			Main.maxTilesY = oldHeight;
		}
	}

	private static void CheckRelationships(Action<bool, string> check)
	{
		Guid world = Guid.NewGuid();
		var profile = new CompanionProfile { Personality = CompanionPersonality.Gentle };
		CompanionRelationship resident = profile.MeetResident(world, NPCID.Guide, "Andrew");
		check(resident.Meetings == 1 && resident.Affinity == 2 && profile.Relationships.Count == 1,
			"First meeting was not remembered");
		check(profile.FindRelationship(Guid.NewGuid(), NPCID.Guide, "Andrew") is null
			&& profile.FindRelationship(world, NPCID.Merchant, "Andrew") is null
			&& profile.FindRelationship(world, NPCID.Guide, "Brian") is null,
			"Friendship leaked across world, role or resident identity");
		profile.RespondToResident(resident, EmoteID.EmotionAnger, -3);
		check(resident.Rank == "Wary", "A cold conversation was counted as a friendship");
		for (int i = 0; i < 10; i++) {
			check(ReferenceEquals(resident, profile.MeetResident(world, NPCID.Guide, "Andrew")), "Reunion duplicated a resident");
			profile.RespondToResident(resident, EmoteID.EmotionLove, 3);
		}
		check(resident.Meetings == 11 && resident.Affinity == 49 && resident.Rank == "Friend", "Friendship did not grow");
		check(profile.Memories.Exists(memory => memory.Kind == CompanionMemoryKind.ResidentFriend), "Friendship milestone missing");
		check(profile.Experience == 0, "NPC emote conversations farmed XP");
		CompanionProfile clone = profile.Clone();
		clone.Relationships[0].Affinity = -50;
		check(resident.Affinity == 49, "Relationship clone shared mutable state");
		CompanionProfile saved = CompanionProfile.Load(profile.Save());
		check(saved.Relationships.Count == 1 && saved.Relationships[0].Meetings == 11
			&& saved.Relationships[0].Affinity == 49, "Saved relationships changed");
		using (var stream = new MemoryStream()) {
			using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) profile.Write(writer);
			stream.Position = 0;
			using var reader = new BinaryReader(stream);
			CompanionProfile networked = CompanionProfile.Read(reader);
			check(networked.Relationships.Count == 1 && networked.Relationships[0].LastEmote == EmoteID.EmotionLove
				&& stream.Position == stream.Length, "Relationship packet roundtrip failed");
		}
		var legacy = profile.Save();
		legacy.Remove("relationships");
		check(CompanionProfile.Load(legacy).Relationships.Count == 0, "Old sigils gained fabricated friends");
		for (int i = 0; i < 40; i++) profile.MeetResident(world, NPCID.Guide, "Resident " + i);
		check(profile.Relationships.Count == CompanionProfile.MaximumRelationships, "Relationship history grew without bound");
		profile.Relationships.Add(new CompanionRelationship { WorldId = world, NpcType = -1, Resident = "bad" });
		profile.Normalize();
		check(profile.Relationships.TrueForAll(relation => relation.NpcType > 0), "Invalid resident survived normalization");
		check(!profile.RecallResident(world, int.MinValue).Contains("Mods.Soulmates."), "Relationship recall leaked a localization key");
	}

	private static void CheckNature(Action<bool, string> check)
	{
		Player oldPlayer = Main.player[0];
		NPC oldInsect = Main.npc[19];
		Item[] oldItems = Main.item;
		RemoteClient[] oldClients = (RemoteClient[])Netplay.Clients.Clone();
		int oldMode = Main.netMode;
		Tilemap oldMap = Main.tile;
		int oldWidth = Main.maxTilesX, oldHeight = Main.maxTilesY;
		try {
			for (int i = 0; i < Netplay.Clients.Length; i++)
				Netplay.Clients[i] = new RemoteClient { Socket = new ProbeSocket() };
			Main.maxTilesX = Main.maxTilesY = 100;
			Main.tile = (Tilemap)Activator.CreateInstance(typeof(Tilemap), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
				null, new object[] { (ushort)100, (ushort)100 }, null)!;
			Main.player[0] = new Player { whoAmI = 0, active = true, Center = new Vector2(400, 400) };
			const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
			foreach (int mode in new[] { NetmodeID.SinglePlayer, NetmodeID.Server }) {
				Main.netMode = mode;
				Main.item = new Item[oldItems.Length];
				for (int i = 0; i < Main.item.Length; i++) Main.item[i] = new Item { whoAmI = i };
				var npc = new NPC { Center = new Vector2(400, 400) };
				npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
				npc.Center = Main.player[0].Center;
				var companion = (SoulboundCompanion)npc.ModNPC;
				companion.Profile.Name = "AETHER";
				typeof(SoulboundCompanion).GetField("speechTimer", flags)!.SetValue(companion, 1000);
				bool Catch() => (bool)typeof(SoulboundCompanion).GetMethod("TryCatchNearbyInsect", flags)!.Invoke(companion, null)!;
				void ResetCooldown() => typeof(SoulboundCompanion).GetField("insectCatchCooldown", flags)!.SetValue(companion, 0);
				var insect = new NPC { whoAmI = 19 };
				insect.SetDefaults(NPCID.Firefly);
				insect.active = true;
				insect.Center = npc.Center;
				Main.npc[19] = insect;
				int catchType = insect.catchItem;
				check(CompanionInsects.CanCatch(insect), "Natural firefly was excluded");
				check(!Catch() && insect.active, "AETHER caught an insect without a carried net");
				companion.Profile.Store(new Item(ItemID.BugNet));
				ResetCooldown();
				insect.SpawnedFromStatue = true;
				check(!Catch() && insect.active, "Caught a statue-spawned insect");
				insect.SpawnedFromStatue = false;
				insect.releaseOwner = 0;
				ResetCooldown();
				check(!Catch() && insect.active, "Recaptured a player-released insect");
				insect.releaseOwner = 255;
				ResetCooldown();
				check(Catch() && !insect.active, "Native insect catch failed in mode " + mode);
				int worldCount = 0;
				foreach (Item item in Main.item) if (item.active && item.type == catchType) worldCount += item.stack;
				check(companion.Profile.ItemCount(catchType) == 1 && worldCount == 0,
					"Caught insect cargo mismatch in mode " + mode + ": pack=" + companion.Profile.ItemCount(catchType)
					+ ", world=" + worldCount + ", source=" + CatchSourceSpy.LastSource);
				for (int i = 0; i < 5; i++) { ResetCooldown(); check(!Catch(), "Caught an inactive insect twice"); }
				check(companion.Profile.ItemCount(catchType) == 1, "Repeated catching changed real insect count");
				check(CompanionInsects.NpcForItem(catchType) == NPCID.Firefly
					&& CompanionInsects.NpcForItem(ItemID.Gel) == -1, "Flock included a non-insect item");
				insect.active = true;
				companion.Profile.Store(new Item(catchType, 1000));
				ResetCooldown();
				check(!Catch() && insect.active, "Full cargo consumed a living insect");
				companion.Profile.Name = "Luma";
				ResetCooldown();
				check(!Catch() && insect.active, "Non-AETHER companion gained insect collection");
				companion.Profile.Name = "AETHER";
				bool oldDedicated = Main.dedServ;
				Main.dedServ = false;
				try {
					companion.Profile.AutonomyEnabled = false;
					typeof(SoulboundCompanion).GetMethod("UpdateNatureCompanions", flags)!.Invoke(companion, null);
					check((int)typeof(SoulboundCompanion).GetField("insectFlockCount", flags)!.GetValue(companion)! == 6,
						"Flock exceeded six or ignored real carried insects");
					foreach (Item item in companion.Profile.CarriedItems) if (item.type == catchType) item.TurnToAir();
					typeof(SoulboundCompanion).GetField("insectFlockRefresh", flags)!.SetValue(companion, 0);
					typeof(SoulboundCompanion).GetMethod("UpdateNatureCompanions", flags)!.Invoke(companion, null);
					check((int)typeof(SoulboundCompanion).GetField("insectFlockCount", flags)!.GetValue(companion)! == 0,
						"Withdrawn insects left a phantom flock");
				}
				finally { Main.dedServ = oldDedicated; }
				Main.netMode = NetmodeID.MultiplayerClient;
				insect.active = true;
				ResetCooldown();
				check(!Catch() && insect.active, "Client independently caught an insect");
			}
		}
		finally {
			for (int i = 0; i < oldClients.Length; i++) Netplay.Clients[i] = oldClients[i];
			Main.player[0] = oldPlayer; Main.npc[19] = oldInsect; Main.item = oldItems; Main.netMode = oldMode;
			Main.tile = oldMap; Main.maxTilesX = oldWidth; Main.maxTilesY = oldHeight;
		}
	}

	private static void CheckResidentConversation(Action<bool, string> check)
	{
		Player oldPlayer = Main.player[0];
		NPC oldResident = Main.npc[21];
		NPC oldCompanion = Main.npc[20];
		var oldWorld = Main.ActiveWorldFileData;
		int oldMode = Main.netMode;
		try {
			Main.netMode = NetmodeID.SinglePlayer;
			Main.ActiveWorldFileData = new Terraria.IO.WorldFileData("", false) { UniqueId = Guid.NewGuid() };
			Main.player[0] = new Player { whoAmI = 0, active = true, Center = new Vector2(400, 400) };
			var npc = new NPC();
			npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
			npc.whoAmI = 20;
			npc.active = true;
			Main.npc[20] = npc;
			npc.Center = Main.player[0].Center;
			var companion = (SoulboundCompanion)npc.ModNPC;
			var resident = new NPC { whoAmI = 21 };
			resident.SetDefaults(NPCID.Guide);
			resident.GivenName = "Andrew";
			resident.Center = npc.Center + new Vector2(60, 0);
			resident.active = true;
			Main.npc[21] = resident;
			const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
			Type type = typeof(SoulboundCompanion);
			void Set(string field, object value) => type.GetField(field, flags)!.SetValue(companion, value);
			object Get(string field) => type.GetField(field, flags)!.GetValue(companion)!;
			void StartVisit() {
				Set("socialNpcTarget", 21); Set("socialNpcIdentity", resident); Set("socialNpcType", resident.type);
				Set("socialResidentName", resident.GivenName); Set("socialNpcTimer", 900);
				Set("socialNpcGreeted", false); Set("socialNpcReplied", false);
			}
			bool Visit() => (bool)type.GetMethod("UpdateTownNpcInteraction", flags)!.Invoke(companion, null)!;
			StartVisit();
			check(Visit() && companion.Profile.Relationships.Count == 1, "Town greeting did not create a resident relationship");
			EmoteBubble.NewBubble(EmoteID.EmotionAnger, new WorldUIAnchor(resident), 120);
			Set("socialReplyDelay", 0);
			Set("speechTimer", 0);
			check(Visit() && companion.Profile.Relationships[0].LastEmote == EmoteID.EmotionAnger
				&& companion.Profile.Relationships[0].Affinity < 0, "NPC response was not observed semantically");
			int affinity = companion.Profile.Relationships[0].Affinity;
			EmoteBubble.NewBubble(EmoteID.EmotionLove, new WorldUIAnchor(resident), 120);
			for (int i = 0; i < 20; i++) Visit();
			check(companion.Profile.Relationships[0].Affinity == affinity, "Emote observer created a response loop or farmed one visit");
			StartVisit();
			resident.GivenName = "Brian";
			check(!Visit() && (int)Get("socialNpcTarget") == -1, "Renamed or replaced resident retained a visit");
			resident.GivenName = "Andrew";
			StartVisit();
			companion.Profile.AutonomyEnabled = false;
			check(!Visit(), "Disabled autonomy retained a social visit");
			companion.Profile.AutonomyEnabled = true;
			StartVisit();
			Set("activeJob", CompanionJob.Mine);
			type.GetMethod("UpdateSocialState", flags)!.Invoke(companion, null);
			check((int)Get("socialNpcTarget") == -1, "Explicit assignment retained an interrupted NPC conversation");
		}
		finally {
			Main.player[0] = oldPlayer; Main.npc[21] = oldResident; Main.npc[20] = oldCompanion;
			Main.ActiveWorldFileData = oldWorld; Main.netMode = oldMode;
		}
	}

	private static void CheckWorldPickup(Action<bool, string> check)
	{
		Player original = Main.player[0];
		try {
			Main.player[0] = new Player { whoAmI = 0, active = true };
			var npc = new NPC();
			npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
			var companion = (SoulboundCompanion)npc.ModNPC;
			MethodInfo collect = typeof(SoulboundCompanion).GetMethod("StoreLooseItem",
				BindingFlags.Instance | BindingFlags.NonPublic)!;
			int Collect(Item item) => (int)collect.Invoke(companion, new object[] { item })!;
			foreach (int type in new[] { ItemID.Gel, ItemID.Acorn, ItemID.StoneBlock, ItemID.Wood, ItemID.CopperOre }) {
				var drop = new Item(type, 137) { active = true, playerIndexTheItemIsReservedFor = 255 };
				int limit = companion.Profile.CarryLimitFor(drop);
				check(Collect(drop) == limit, "World pickup limit failed: " + type);
				check(drop.active && drop.type == type && drop.stack == 137 - limit,
					"Uncollected remainder disappeared or changed type: " + type);
				for (int repeat = 0; repeat < 100; repeat++)
					check(Collect(drop) == 0 && companion.Profile.ItemCount(type) + drop.stack == 137,
						"Repeated world pickup duplicated or lost units: " + type + "/" + repeat);
			}
			check(companion.Profile.ResourceLoad == 5 && companion.Profile.PackLoad == 0, "World pickup merged different item types");
			foreach (int type in new[] { ItemID.Heart, ItemID.Star, ModContent.ItemType<Soulcore>(), ModContent.ItemType<SoulboundSigil>() }) {
				var protectedDrop = new Item(type, 1) { active = true, playerIndexTheItemIsReservedFor = 255 };
				check(Collect(protectedDrop) == 0 && protectedDrop.active && protectedDrop.stack == 1,
					"Pickup consumed a protected item: " + type);
			}
			var reserved = new Item(ItemID.IronOre, 7) { active = true, playerIndexTheItemIsReservedFor = 1 };
			check(Collect(reserved) == 0 && reserved.stack == 7, "Pickup stole another player's reservation");
			reserved.playerIndexTheItemIsReservedFor = 0;
			check(Collect(reserved) == 7 && !reserved.active && reserved.IsAir,
				"Owner-reserved pickup did not finish atomically");
			FillResources(companion.Profile, ItemID.DirtBlock);
			var overflow = new Item(ItemID.DirtBlock, 5) { active = true, playerIndexTheItemIsReservedFor = 255 };
			check(Collect(overflow) == 0 && overflow.stack == 5 && overflow.active,
				"Full pack destroyed a world drop");
			var bound = new Item(ModContent.ItemType<SoulboundSigil>());
			var sigil = (SoulboundSigil)bound.ModItem;
			sigil.Profile = companion.Profile.Clone();
			Main.player[0].inventory[0] = bound;
			companion.SyncProfileToBoundSigil();
			check(sigil.Profile.ItemCount(ItemID.Gel) == 50 && sigil.Profile.ItemCount(ItemID.Acorn) == 50,
				"World cargo did not persist to its bound sigil");
		}
		finally {
			Main.player[0] = original;
		}
	}

	private static void FillEquipment(CompanionProfile profile)
	{
		while (profile.PackLoad < profile.PackCapacity) {
			if (profile.Store(new Item(ItemID.CopperShortsword, 1)) == 0)
				throw new InvalidOperationException("Could not fill equipment slots.");
		}
	}

	private static void FillResources(CompanionProfile profile, int excludedType)
	{
		for (int type = 1; type < ItemID.Count && profile.ResourceLoad < CompanionProfile.MaximumResourceSlots; type++) {
			var item = new Item(type, 1);
			if (type != excludedType && CompanionProfile.IsResource(item))
				profile.Store(item);
		}
		if (profile.ResourceLoad != CompanionProfile.MaximumResourceSlots)
			throw new InvalidOperationException("Could not fill resource slots.");
	}

	private static void CheckCargoGrowthAndWallet(Action<bool, string> check)
	{
		foreach (int type in new[] { ItemID.Wood, ItemID.StoneBlock, ItemID.Gel, ItemID.Acorn, ItemID.CopperOre }) {
			for (int level = 1; level <= 20; level++) {
				var profile = new CompanionProfile { Experience = CompanionProfile.ExperienceForLevel(level) };
				var source = new Item(type, 1100);
				int expected = level * 50;
				check(profile.ResourceCarryLimit == expected, "Wrong resource capacity at level " + level);
				check(profile.Store(source) == expected && profile.ItemCount(type) == expected
					&& source.stack == 1100 - expected, "Level-scaled resource transfer failed: " + level + "/" + type);
				check(profile.Store(source) == 0, "Full resource type accepted repeated pickup");
			}
		}
		var growing = new CompanionProfile();
		growing.Store(new Item(ItemID.Wood, 50));
		growing.Experience = CompanionProfile.ExperienceForLevel(10);
		check(growing.Store(new Item(ItemID.Wood, 500)) == 450 && growing.ItemCount(ItemID.Wood) == 500,
			"Level 10 did not expand existing storage to 500");
		growing.Experience = CompanionProfile.ExperienceForLevel(20);
		check(growing.Store(new Item(ItemID.Wood, 600)) == 500 && growing.ItemCount(ItemID.Wood) == 1000,
			"Level 20 did not expand existing storage to 1000");
		check(!CompanionProfile.IsResource(new Item(ItemID.LesserHealingPotion))
			&& !CompanionProfile.IsResource(new Item(ItemID.CopperShortsword))
			&& CompanionProfile.IsResource(new Item(ItemID.Torch))
			&& !CompanionProfile.IsResource(new Item(ItemID.GoldCoin)), "Storage classification misplaced usable equipment or coins");

		var legacy = new CompanionProfile();
		var oldTag = legacy.Save();
		oldTag.Remove("resources");
		oldTag.Remove("walletCopper");
		oldTag["pack"] = new List<Terraria.ModLoader.IO.TagCompound> {
			Terraria.ModLoader.IO.ItemIO.Save(new Item(ItemID.Gel, 99)),
			Terraria.ModLoader.IO.ItemIO.Save(new Item(ItemID.CopperShortsword, 1)),
			Terraria.ModLoader.IO.ItemIO.Save(new Item(ItemID.GoldCoin, 99))
		};
		legacy = CompanionProfile.Load(oldTag);
		check(legacy.ResourceLoad == 1 && legacy.PackLoad == 1 && legacy.ItemCount(ItemID.Gel) == 99
			&& legacy.WalletCopper == 990_000, "Legacy migration lost items or coins");
		legacy.Normalize();
		check(legacy.WalletCopper == 990_000, "Repeated normalization duplicated legacy coins");
		List<Item> excess = legacy.ExtractExcess(ItemID.Gel, legacy.ResourceCarryLimit);
		check(excess.Count == 1 && excess[0].stack == 49 && legacy.ItemCount(ItemID.Gel) == 50,
			"Legacy overflow was not preserved for safe return");

		var wallet = new CompanionProfile();
		FillEquipment(wallet);
		FillResources(wallet, ItemID.DirtBlock);
		BigInteger expectedBalance = BigInteger.Zero;
		foreach (int type in new[] { ItemID.CopperCoin, ItemID.SilverCoin, ItemID.GoldCoin, ItemID.PlatinumCoin }) {
			var source = new Item(type, 137);
			expectedBalance += (BigInteger)137 * CompanionProfile.CoinValue(type);
			check(wallet.Store(source) == 137 && source.IsAir && wallet.WalletCopper == expectedBalance,
				"Full cargo blocked or duplicated coins of type " + type);
		}
		check(wallet.PackLoad == wallet.PackCapacity && wallet.ResourceLoad == 60,
			"Wallet used ordinary cargo slots");
		var hugeTag = wallet.Save();
		BigInteger huge = BigInteger.Pow(10, 60) + 321;
		hugeTag["walletCopper"] = huge.ToString();
		wallet = CompanionProfile.Load(hugeTag);
		check(wallet.WalletCopper == huge && wallet.Store(new Item(ItemID.PlatinumCoin, 9999)) == 9999
			&& wallet.WalletCopper == huge + 9_999_000_000L, "Unlimited wallet overflowed or truncated");
		CompanionProfile saved = CompanionProfile.Load(wallet.Save());
		check(saved.WalletCopper == wallet.WalletCopper && saved.ResourceLoad == 60,
			"Saving lost the unlimited wallet or resources");
		CompanionProfile clone = wallet.Clone();
		clone.RemoveWalletCoins(ItemID.CopperCoin, 1);
		clone.Resources[0].stack++;
		check(clone.WalletCopper == wallet.WalletCopper - 1 && clone.Resources[0].stack != wallet.Resources[0].stack,
			"Cloned cargo or wallet shared mutable state");
		using (var stream = new MemoryStream()) {
			using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) wallet.Write(writer);
			stream.Position = 0;
			using var reader = new BinaryReader(stream);
			CompanionProfile networked = CompanionProfile.Read(reader);
			check(networked.WalletCopper == wallet.WalletCopper && networked.ResourceLoad == 60
				&& networked.PackLoad == wallet.PackLoad && stream.Position == stream.Length,
				"Network serialization lost huge wallet or separated cargo");
		}
		BigInteger before = wallet.WalletCopper;
		check(!wallet.RemoveWalletCoins(ItemID.Wood, 1) && !wallet.RemoveWalletCoins(ItemID.GoldCoin, -1)
			&& wallet.WalletCopper == before, "Invalid withdrawal created or destroyed money");
		wallet.ClearCargo();
		check(wallet.PackLoad == 0 && wallet.ResourceLoad == 0 && wallet.WalletCopper.IsZero,
			"New-companion reset retained cargo or money");
		CheckCargoWithdrawals(check);
	}

	private static void CheckCargoWithdrawals(Action<bool, string> check)
	{
		Player original = Main.player[0];
		int originalMode = Main.netMode;
		try {
			Main.netMode = NetmodeID.SinglePlayer;
			var owner = new Player { whoAmI = 0, active = true };
			Main.player[0] = owner;
			var npc = new NPC();
			npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
			var companion = (SoulboundCompanion)npc.ModNPC;
			companion.Profile.Store(new Item(ItemID.GoldCoin, 137));
			companion.Profile.Store(new Item(ItemID.Wood, 50));
			BigInteger Coins() {
				BigInteger balance = BigInteger.Zero;
				foreach (Item item in owner.inventory)
					if (!item.IsAir) balance += (BigInteger)item.stack * CompanionProfile.CoinValue(item.type);
				return balance;
			}
			BigInteger total = companion.Profile.WalletCopper + Coins();
			companion.WithdrawStorageSlot(CompanionStorage.Wallet, 1, singleItem: true);
			check(companion.Profile.WalletCopper == 1_360_000 && Coins() == 10_000
				&& companion.Profile.WalletCopper + Coins() == total, "Gold withdrawal violated currency conservation");
			for (int i = 0; i < owner.inventory.Length; i++)
				owner.inventory[i] = new Item(ItemID.CopperShortsword, 1);
			BigInteger before = companion.Profile.WalletCopper;
			companion.WithdrawStorageSlot(CompanionStorage.Wallet, 0, singleItem: false);
			check(companion.Profile.WalletCopper == before, "Full player inventory destroyed wallet money");
			owner.inventory[0] = new Item(ItemID.Wood);
			owner.inventory[0].stack = owner.inventory[0].maxStack - 3;
			companion.WithdrawStorageSlot(CompanionStorage.Resources, 0, singleItem: false);
			check(companion.Profile.ItemCount(ItemID.Wood) == 47
				&& owner.inventory[0].stack == owner.inventory[0].maxStack,
				"Partial resource withdrawal destroyed or duplicated leftovers");
			companion.WithdrawStorageSlot((CompanionStorage)255, 0, true);
			check(companion.Profile.ItemCount(ItemID.Wood) == 47 && companion.Profile.WalletCopper == before,
				"Invalid storage request changed cargo");
			MethodInfo collect = typeof(SoulboundCompanion).GetMethod("StoreLooseItem",
				BindingFlags.Instance | BindingFlags.NonPublic)!;
			var drop = new Item(ItemID.SilverCoin, 137) { active = true, playerIndexTheItemIsReservedFor = 255 };
			check((int)collect.Invoke(companion, new object[] { drop })! == 137 && !drop.active && drop.IsAir
				&& companion.Profile.WalletCopper == before + 13_700, "World coin pickup was not atomic");
			check((int)collect.Invoke(companion, new object[] { drop })! == 0
				&& companion.Profile.WalletCopper == before + 13_700, "Repeated world coin pickup duplicated money");
			var hugeTag = companion.Profile.Save();
			BigInteger huge = BigInteger.Pow(10, 60);
			hugeTag["walletCopper"] = huge.ToString();
			companion.Profile = CompanionProfile.Load(hugeTag);
			var platinum = new Item(ItemID.PlatinumCoin, 9999) { active = true, playerIndexTheItemIsReservedFor = 255 };
			check((int)collect.Invoke(companion, new object[] { platinum })! == 9999 && platinum.IsAir
				&& companion.Profile.WalletCopper == huge + 9_999_000_000L,
				"World pickup overflowed an already enormous wallet");

			for (int i = 0; i < owner.inventory.Length; i++)
				owner.inventory[i] = new Item();
			companion.Profile = new CompanionProfile();
			companion.Profile.Pack.Add(new Item(ItemID.Gel, 99));
			companion.Profile.Pack.Add(new Item(ItemID.GoldCoin, 99));
			MethodInfo reconcile = typeof(SoulboundCompanion).GetMethod("ReconcilePackOnce",
				BindingFlags.Instance | BindingFlags.NonPublic)!;
			reconcile.Invoke(companion, null);
			int returnedGel = 0;
			foreach (Item item in owner.inventory)
				if (item.type == ItemID.Gel) returnedGel += item.stack;
			check(companion.Profile.ItemCount(ItemID.Gel) == 50 && returnedGel == 49
				&& companion.Profile.WalletCopper == 990_000, "Summon migration failed to return legacy resource excess");
			reconcile.Invoke(companion, null);
			check(companion.Profile.WalletCopper == 990_000 && companion.Profile.ItemCount(ItemID.Gel) == 50,
				"Repeated summon migration duplicated coins or resources");
		}
		finally {
			Main.player[0] = original;
			Main.netMode = originalMode;
		}
	}

	private static void CheckCargoLayout(Action<bool, string> check)
	{
		var profile = new CompanionProfile();
		FillResources(profile, ItemID.None);
		Type type = typeof(TalkModeState).Assembly.GetType("Soulmates.Common.UI.CompanionPackElement")!;
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
		var element = (Terraria.UI.UIElement)Activator.CreateInstance(type, flags | BindingFlags.Public, null,
			new object[] { new Func<CompanionProfile>(() => profile), new Action<CompanionStorage, int, bool>((_, _, _) => { }) }, null)!;
		MethodInfo slotBounds = type.GetMethod("CalculateSlotBounds", BindingFlags.Static | BindingFlags.NonPublic)!;
		FieldInfo storage = type.GetField("selectedStorage", flags)!;
		FieldInfo page = type.GetField("page", flags)!;
		MethodInfo changePage = type.GetMethod("ChangePage", flags)!;
		foreach (CompanionStorage kind in Enum.GetValues<CompanionStorage>()) {
			storage.SetValue(element, kind);
			int slots = kind == CompanionStorage.Wallet ? 4 : 12;
			foreach (int width in new[] { 40, 60, 90, 180, 300, 600 }) {
				Rectangle area = new(10, 20, width, 104);
				var bounds = new List<Rectangle>();
				for (int slot = 0; slot < slots; slot++) {
					Rectangle rectangle = (Rectangle)slotBounds.Invoke(null, new object[] { area, slot, kind == CompanionStorage.Wallet })!;
					check(area.Contains(rectangle) && rectangle.Width > 0 && rectangle.Top >= area.Top + 34,
						$"Cargo slot escaped bounds or overlapped tabs: {kind}/{width}/{slot}, area {area}, slot {rectangle}");
					foreach (Rectangle other in bounds)
						check(!rectangle.Intersects(other), $"Cargo slot hitboxes overlap: {kind}/{width}/{slot}");
					bounds.Add(rectangle);
				}
			}
		}
		storage.SetValue(element, CompanionStorage.Resources);
		page.SetValue(element, 0);
		changePage.Invoke(element, new object[] { -1 });
		check((int)page.GetValue(element)! == 4, "Resource page did not wrap backwards to the fifth page");
		changePage.Invoke(element, new object[] { 1 });
		check((int)page.GetValue(element)! == 0, "Resource page did not wrap forwards to the first page");
		storage.SetValue(element, CompanionStorage.Wallet);
		changePage.Invoke(element, new object[] { 1 });
		check((int)page.GetValue(element)! == 0, "Wallet inherited a resource page offset");
	}

	private static void CheckMenuInput(Action<bool, string> check, Item core)
	{
		bool oldMenu = Main.gameMenu;
		int oldPlayer = Main.myPlayer;
		Player original = Main.player[0];
		try {
			Main.dedServ = false;
			Main.gameMenu = false;
			Main.myPlayer = 0;
			Main.player[0] = new Player { whoAmI = 0, active = true };
			Player player = Main.player[0];
			player.SetTalkNPC(-1);
			SoulmatesPlayer controls = player.GetModPlayer<SoulmatesPlayer>();
			var hoverNpc = new NPC();
			hoverNpc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
			hoverNpc.ai[0] = 0;
			var hoverCompanion = (SoulboundCompanion)hoverNpc.ModNPC;
			Main.mouseLeft = Main.mouseRight = false;
			foreach ((ModSystem system, string field) in new (ModSystem, string)[] {
				(ModContent.GetInstance<CompanionWheelSystem>(), "open"),
				(ModContent.GetInstance<InitiativePromptSystem>(), "open"),
				(ModContent.GetInstance<DirectOrderSystem>(), "active")
			}) {
				FieldInfo state = system.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!;
				state.SetValue(system, true);
				FieldInfo context = typeof(CompanionWheelSystem).GetField("context", BindingFlags.Instance | BindingFlags.NonPublic)!;
				var wheel = ModContent.GetInstance<CompanionWheelSystem>();
				context.SetValue(wheel, Enum.ToObject(context.FieldType, 2));
				Main.mouseRight = Main.mouseRightRelease = true;
				hoverCompanion.PreHoverInteract(true);
				check((bool)state.GetValue(system)!, system.Name + " was replaced by a hovered companion");
				check(Convert.ToInt32(context.GetValue(wheel)) == 2, system.Name + " switched to the companion wheel");
				if (system == wheel) {
					PropertyInfo visibility = typeof(CompanionSpeechSystem).GetProperty("CanShowSpeech", BindingFlags.Static | BindingFlags.NonPublic)!;
					check(!(bool)visibility.GetValue(null)!, "Speech overlaps an open wheel");
				}
				Main.mouseRight = false;
				check(!controls.CanUseItem(core), system.Name + " allowed held item use");
				player.controlUseItem = player.controlUseTile = true;
				Main.mouseLeft = true;
				controls.SetControls();
				check(!player.controlUseItem && !player.controlUseTile, system.Name + " leaked controls");
				Type input = typeof(SoulmatesPlayer).Assembly.GetType("Soulmates.Common.UI.SoulmatesUIInput")!;
				bool CanPrompt() => (bool)input.GetProperty("CanPresentInitiative")!.GetValue(null)!;
				check(!CanPrompt(), system.Name + " can be replaced by a prompt");
				state.SetValue(system, false);
				controls.SetControls();
				check(!controls.CanUseItem(core), system.Name + " leaked closing click");
				Main.mouseLeft = Main.mouseRight = false;
				controls.SetControls();
				check(controls.CanUseItem(core), system.Name + " kept items blocked after release");
				check(CanPrompt(), system.Name + " failed to release deferred prompt");
				player.SetTalkNPC(1);
				check(!CanPrompt(), "Prompt interrupts a vanilla NPC conversation");
				PropertyInfo speech = typeof(CompanionSpeechSystem).GetProperty("CanShowSpeech",
					BindingFlags.Static | BindingFlags.NonPublic)!;
				check(!(bool)speech.GetValue(null)!, "Speech covers a vanilla NPC conversation");
				player.SetTalkNPC(-1);
				Main.playerInventory = true;
				check(!CanPrompt(), "Prompt interrupts inventory");
				check(!(bool)speech.GetValue(null)!, "Speech covers inventory");
				Main.playerInventory = false;
				check((bool)speech.GetValue(null)!, "Speech stays hidden after vanilla UI closes");
			}
		}
		finally {
			Main.dedServ = true;
			Main.gameMenu = oldMenu;
			Main.myPlayer = oldPlayer;
			Main.player[0] = original;
			Main.mouseLeft = Main.mouseRight = false;
			Main.playerInventory = false;
		}
	}
}
