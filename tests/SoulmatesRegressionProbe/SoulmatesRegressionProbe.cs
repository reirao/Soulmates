using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BigInteger = System.Numerics.BigInteger;
using Hjson;
using Soulmates.Common;
using Soulmates.Common.Dialogue;
using Soulmates.Common.Feedback;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Soulmates.Content.Projectiles;
using Soulmates.Common.UI;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.GameContent.UI;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.Net;
using Terraria.Net.Sockets;
using Terraria.ModLoader.IO;

namespace SoulmatesRegressionProbe;

public sealed class SoulmatesRegressionProbe : Mod { }

public sealed class ProbeSocket : ISocket
{
	public static bool Recording;
	public static readonly List<byte[]> Sent = [];
	public void AsyncSend(byte[] data, int offset, int size, SocketSendCallback callback, object? state = null)
	{
		if (Recording) Sent.Add(data.AsSpan(offset, size).ToArray());
	}
	public void AsyncReceive(byte[] data, int offset, int size, SocketReceiveCallback callback, object? state = null) { }
	public void Close() { }
	public void Connect(RemoteAddress address) { }
	public RemoteAddress GetRemoteAddress() => null!;
	public bool IsConnected() => Recording;
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

public sealed class RecoveryPacketSpy : ModSystem
{
	public static bool Watching;
	public static readonly List<(int Type, int Recipient, int Player, float Amount, float Ticks)> Packets = [];
	public override bool HijackSendData(int whoAmI, int msgType, int remoteClient, int ignoreClient,
		NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)
	{
		if (!Watching) return false;
		Packets.Add((msgType, remoteClient, number, number2, number3));
		return true;
	}
}

public sealed partial class EngineChecks : ModSystem
{
	private static readonly string[] SupportedCultures = { "en-US", "de-DE", "it-IT", "fr-FR", "es-ES", "ru-RU", "pt-BR", "pl-PL", "zh-Hans" };

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
			Version expectedVersion = Version.Parse(Environment.GetEnvironmentVariable("SOULMATES_EXPECTED_TEST_VERSION") ?? "0.22.1");
			Check(soulmates.Version == expectedVersion, "Wrong packaged version: " + soulmates.Version);
			Check(!soulmates.FileExists("icon_small.rawimg") && !soulmates.FileExists("icon_small.png"),
				"Optional mini-icon reintroduced the installed packer's exhausted-stream conversion");
			byte[] icon = soulmates.GetFileBytes("icon.png");
			Check(icon.Length > 24 && icon[0] == 137 && icon[1] == 80 && icon[2] == 78 && icon[3] == 71,
				"Full mod icon was removed or is not a PNG");
			var core = new Item(ModContent.ItemType<Soulcore>());
			Check(!core.consumable, "Soulcore marked consumable");
			Check(!ItemLoader.ConsumeItem(core, new Player()), "Inventory right-click consumes the reusable Soulcore");
			// Recipes load before a world exists; native projectile collision needs allocated tile storage.
			Main.maxTilesX = Main.maxTilesY = 100;
			Main.tile = (Tilemap)Activator.CreateInstance(typeof(Tilemap), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
				null, new object[] { (ushort)100, (ushort)100 }, null)!;
			Lighting.Initialize();
			CheckMenuInput(Check, core);
			CheckContextMouseModes(Check);
			CheckTalkUiRouting(Check);
			CheckRockPaperScissors(Check, soulmates);
			CheckWorldPickup(Check);
			CheckRevisionBoundaries(Check, soulmates);
			CheckMultiplayerCreation(Check, soulmates);
			CheckRecoveryAndLifecycle(Check, soulmates);
			CheckEnergyAndMiningRules(Check, soulmates);
			CheckCombatAndSwitching(Check, soulmates);
			CheckCargoGrowthAndWallet(Check);
			CheckCargoLayout(Check);
			CheckAttention(Check);
			CheckPointingAndInitiative(Check);
			CheckRelationships(Check);
			CheckNature(Check);
			CheckSaplingPlacement(Check);
			CheckChoiceConversation(Check, soulmates);
			CheckAnswerableMoments(Check);
			CheckGentleEncounters(Check);
			CheckCritterModes(Check);
			CheckGamesAndCritters(Check);
			CheckWorkRecipes(Check, ModLoader.GetMod("Soulmates"));
			CheckCompanionPets(Check, ModLoader.GetMod("Soulmates"));
			CheckCritterScheduling(Check);
			CheckAbilityRegistry(Check);
			CheckActivityDispatch(Check);
			CheckTreeContext(Check);
			CheckCritterPermissions(Check);
			CheckResidentConversation(Check);
			CheckLivingBehavior(Check);
			foreach (string culture in SupportedCultures) {
				LanguageManager.Instance.SetLanguage(culture);
				Check(Language.ActiveCulture.Name == culture, "Engine did not select language: " + culture);
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
				new object?[] { Activity(1), 10, Point.Zero, Forest(0), null })!;
			Item Drop(int count = 7) => new Item(ItemID.Gel, count) {
				active = true, playerIndexTheItemIsReservedFor = 255, position = new Vector2(450f, 400f)
			};
			Main.item[10] = Drop();
			check(!Ask() && companion.HasPendingInitiative, "Ask did not wait for an answer");
			check((int)Get("autonomyDecisionTimer") < 3600, "Prompt incurred a full minute of decision debt");
			Main.item[10] = Drop();
			check(!companion.RespondToInitiative(companion.InitiativeId, CompanionInitiativeResponse.Always), "Reused world-item slot accepted stale Yes");
			check(companion.Profile.GatheringInitiative == CompanionInitiativePolicy.Ask,
				"Stale answer changed saved permission");
			((CompanionAttention)Get("attention")).Reset();
			Ask();
			check(companion.RespondToInitiative(companion.InitiativeId, CompanionInitiativeResponse.Yes), "Valid answer did not start work");
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
			check(!companion.RespondToInitiative(companion.InitiativeId, CompanionInitiativeResponse.Yes), "Low energy accepted stale permission");
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
			check(!companion.RespondToInitiative(companion.InitiativeId, CompanionInitiativeResponse.Always), "Disabled autonomy accepted an automatic order");

			int energy = companion.Profile.Energy, experience = companion.Profile.Experience;
			Set("activeJob", CompanionJob.Mine);
			CompanionConversationResult observation = companion.PerformDirectOrder(CompanionTargetOrder.Look, Point.Zero, 10);
			check(observation.Accepted && (CompanionJob)Get("activeJob") == CompanionJob.Mine,
				"Look interrupted an existing assignment");
			check(companion.Profile.ItemCount(ItemID.Gel) == 0 && Main.item[10].stack == 7
				&& companion.Profile.Energy == energy && companion.Profile.Experience == experience,
				"Look consumed cargo, energy, or farmed XP");
			CompanionPersonality oldPersonality = companion.Profile.Personality;
			CompanionVoice oldVoice = companion.Profile.Voice;
			var personalReplies = new HashSet<string>();
			foreach (CompanionPersonality personality in Enum.GetValues<CompanionPersonality>()) {
				companion.Profile.Personality = personality;
				foreach (CompanionVoice voice in Enum.GetValues<CompanionVoice>()) {
					companion.Profile.Voice = voice;
					var reply = companion.PerformDirectOrder(CompanionTargetOrder.Look, Point.Zero, 10);
					string fact = SoulmatesText.Get("TargetOrders.LookItem", Main.item[10].Name, Main.item[10].stack);
					check(reply.Accepted && reply.Reply.StartsWith(fact) && !reply.Reply.Contains("Mods.Soulmates."),
						"Personal observation lost its facts or displayed a localization key");
					if (voice == CompanionVoice.Direct) check(reply.Reply == fact, "Direct voice ignored the player's concise preference");
					else check(reply.Reply.EndsWith(SoulmatesText.Get($"TargetOrders.Noticing.{voice}.{personality}")),
						"Shared observation ignored personality or chosen voice");
					if (voice == CompanionVoice.Soft) personalReplies.Add(reply.Reply);
					check((CompanionJob)Get("activeJob") == CompanionJob.Mine && companion.Profile.Energy == energy
						&& companion.Profile.Experience == experience && Main.item[10].stack == 7
						&& companion.Profile.ItemCount(ItemID.Gel) == 0 && !companion.Profile.AutonomyEnabled,
						"Shared observation changed work, cargo, autonomy, energy or progression");
				}
			}
			check(personalReplies.Count == 5, "Different personalities shared the same observation voice");
			companion.Profile.Voice = CompanionVoice.Soft;
			companion.Profile.Energy = 0;
			var tired = companion.PerformDirectOrder(CompanionTargetOrder.Look, Point.Zero, 10);
			check(tired.Accepted && tired.Reply.EndsWith(SoulmatesText.Get("TargetOrders.Noticing.Rest")),
				"Shared observation refused low-energy company or hid the rest context");
			companion.Profile.Energy = energy;
			companion.Profile.Mood = 0;
			check(companion.PerformDirectOrder(CompanionTargetOrder.Look, Point.Zero, 10).Reply
				.EndsWith(SoulmatesText.Get("TargetOrders.Noticing.Quiet")), "Low mood did not get a quieter observation");
			companion.Profile.Mood = 100;
			Item observedDrop = Main.item[10];
			Main.item[10] = new Item(ItemID.SilverCoin, 3) { active = true, position = observedDrop.position };
			check(companion.PerformDirectOrder(CompanionTargetOrder.Look, Point.Zero, 10).Reply.StartsWith(
				SoulmatesText.Get("TargetOrders.LookCoins", Main.item[10].Name)), "Coin inspection used finite pack capacity instead of the wallet");
			Main.item[10] = observedDrop;
			companion.Profile.Personality = oldPersonality; companion.Profile.Voice = oldVoice;
			companion.PerformDirectOrder(CompanionTargetOrder.Gather, Point.Zero, 10);
			check((bool)type.GetMethod("IsValidGatherTarget", flags)!.Invoke(companion, new object[] { 10 })!,
				"Directed drop was invalid on assignment");
			Main.item[10] = Drop();
			check(!(bool)type.GetMethod("IsValidGatherTarget", flags)!.Invoke(companion, new object[] { 10 })!,
				"Directed fetch followed a reused slot into a different drop");
			Tile ground = Main.tile[28, 30];
			ground.HasTile = true;
			ground.TileType = TileID.Grass;
			Tile otherGround = Main.tile[29, 30];
			otherGround.HasTile = true;
			otherGround.TileType = TileID.Grass;
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
				bool Catch() => (bool)typeof(SoulboundCompanion).GetMethod("TryCatchCritter", flags)!.Invoke(companion, new object?[] { true, null })!;
				void ResetCooldown() => typeof(SoulboundCompanion).GetField("insectCatchCooldown", flags)!.SetValue(companion, 0);
				var insect = new NPC { whoAmI = 19 };
				insect.SetDefaults(NPCID.Firefly);
				insect.active = true;
				insect.Center = npc.Center;
				Main.npc[19] = insect;
				int catchType = insect.catchItem;
				check(CompanionInsects.CanCatch(insect), "Natural firefly was excluded");
				var baselineNet = (Item)typeof(SoulboundCompanion).GetMethod("EffectiveCritterNet", flags)!.Invoke(companion, null)!;
				check(baselineNet.type == ItemID.BugNet && companion.Profile.ItemCount(ItemID.BugNet) == 0,
					"Built-in net was missing or appeared as withdrawable cargo");
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
					check(!Catch() && insect.active, "Changing a companion name bypassed full insect cargo");
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

	private static void CheckSaplingPlacement(Action<bool, string> check)
	{
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
		Tilemap oldMap = Main.tile;
		int oldWidth = Main.maxTilesX, oldHeight = Main.maxTilesY, oldMode = Main.netMode;
		Player oldPlayer = Main.player[0];
		try {
			Main.maxTilesX = Main.maxTilesY = 100;
			Main.tile = (Tilemap)Activator.CreateInstance(typeof(Tilemap), flags | BindingFlags.Public,
				null, new object[] { (ushort)100, (ushort)100 }, null)!;
			Main.netMode = NetmodeID.SinglePlayer;
			Main.player[0] = new Player { whoAmI = 0, active = true, Center = new Vector2(400, 400) };
			var npc = new NPC(); npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>()); npc.ai[0] = 0;
			var companion = (SoulboundCompanion)npc.ModNPC;
			companion.Profile.Store(new Item(ItemID.Acorn, 3));
			bool CanPlant() => (bool)typeof(SoulboundCompanion).GetMethod("CanPlantAcornAt", flags)!
				.Invoke(companion, new object[] { 28, 29 })!;
			bool Plant() => (bool)typeof(SoulboundCompanion).GetMethod("PlantAcorn", flags)!
				.Invoke(companion, new object[] { new Point(28, 29) })!;
			Tile left = Main.tile[28, 30], right = Main.tile[29, 30];
			left.HasTile = true; left.TileType = TileID.Grass;
			check(!CanPlant(), "Forestry chose a ledge without reserved growing room");
			right.HasTile = true; right.TileType = TileID.Grass;
			check(CanPlant(), "Forestry rejected native flat two-block sapling ground");
			right.IsHalfBlock = true;
			check(!CanPlant(), "Forestry chose half-block sapling ground");
			right.IsHalfBlock = false; right.Slope = SlopeType.SlopeDownRight;
			check(!CanPlant(), "Forestry chose sloped sapling ground");
			right.Slope = SlopeType.Solid; right.IsActuated = true;
			check(!CanPlant(), "Forestry chose actuated sapling ground");
			right.IsActuated = false;
			Tile occupied = Main.tile[29, 29]; occupied.HasTile = true; occupied.TileType = TileID.Stone;
			check(!CanPlant(), "Forestry ignored an obstruction in the sapling's second column");
			check(!Plant() && companion.Profile.ItemCount(ItemID.Acorn) == 3 && occupied.HasTile,
				"Failed planting consumed an acorn or destroyed an obstruction");
			occupied.ClearEverything();
			bool planted = Plant();
			check(planted && Main.tile[28, 29].HasTile && Main.tile[28, 29].TileType == TileID.Saplings
				&& companion.Profile.ItemCount(ItemID.Acorn) == 2,
				$"Valid native sapling did not consume exactly one carried acorn: result={planted}, acorns={companion.Profile.ItemCount(ItemID.Acorn)}, left={Main.tile[27, 29].HasTile}/{Main.tile[27, 29].TileType}, center={Main.tile[28, 29].HasTile}/{Main.tile[28, 29].TileType}, right={Main.tile[29, 29].HasTile}/{Main.tile[29, 29].TileType}");
			check(!Plant() && companion.Profile.ItemCount(ItemID.Acorn) == 2, "Repeated planting duplicated or consumed supplies");
		}
		finally {
			Main.tile = oldMap; Main.maxTilesX = oldWidth; Main.maxTilesY = oldHeight;
			Main.netMode = oldMode; Main.player[0] = oldPlayer;
		}
	}

	private static void CheckChoiceConversation(Action<bool, string> check, Mod mod)
	{
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
		Type type = typeof(SoulboundCompanion);
		Player oldPlayer = Main.player[0];
		NPC[] oldNpcs = Main.npc;
		Item[] oldItems = Main.item;
		int oldMode = Main.netMode, oldMyPlayer = Main.myPlayer;
		RemoteClient[] oldClients = (RemoteClient[])Netplay.Clients.Clone();
		try {
			for (int i = 0; i < Netplay.Clients.Length; i++) Netplay.Clients[i] = new RemoteClient { Socket = new ProbeSocket() };
			foreach (int mode in new[] { NetmodeID.SinglePlayer, NetmodeID.Server }) {
				// This headless fixture has no local recipe cache for GetItem's graphical UI refresh.
				Main.netMode = mode; Main.myPlayer = 255;
				Main.npc = new NPC[oldNpcs.Length];
				for (int i = 0; i < Main.npc.Length; i++) Main.npc[i] = new NPC { whoAmI = i };
				Main.item = new Item[oldItems.Length];
				for (int i = 0; i < Main.item.Length; i++) Main.item[i] = new Item { whoAmI = i };
				var owner = new Player { whoAmI = 0, active = true, Center = new Vector2(400, 400) };
				Main.player[0] = owner;
				NPC npc = Main.npc[20]; npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
				npc.ai[0] = 0; npc.active = true; npc.Center = owner.Center;
				var companion = (SoulboundCompanion)npc.ModNPC;
				companion.Profile.Mood = 50; companion.Profile.Bond = 10;
				owner.inventory[0] = new Item(ModContent.ItemType<SoulboundSigil>());
				var sigil = (SoulboundSigil)owner.inventory[0].ModItem;
				sigil.Profile = companion.Profile.Clone();
				void Set(string field, object value) => type.GetField(field, flags)!.SetValue(companion, value);
				void Tick(string method) => type.GetMethod(method, flags)!.Invoke(companion, null);
				bool Begin(CompanionQuestion question) {
					SettleServerInventory(mod);
					Set("personalQuestionCooldown", 0);
					return (bool)type.GetMethod("BeginChoiceQuestion", flags)!.Invoke(companion, new object[] { question })!;
				}
				void Wallet(BigInteger amount) {
					TagCompound saved = companion.Profile.Save(); saved["walletCopper"] = amount.ToString();
					companion.Profile = CompanionProfile.Load(saved);
				}
				BigInteger Money() {
					BigInteger total = companion.Profile.WalletCopper;
					foreach (Item item in owner.inventory) total += (BigInteger)CompanionProfile.CoinValue(item.type) * item.stack;
					return total;
				}
				check(Begin(CompanionQuestion.Company), "Idle companion could not ask a social question");
				Guid first = companion.QuestionId;
				check(!companion.RespondToQuestion(Guid.NewGuid(), CompanionAnswer.First) && companion.HasPendingQuestion,
					"Unrelated/stale token changed a pending question");
				check(!companion.RespondToQuestion(first, (CompanionAnswer)255), "Invalid answer enum was accepted");
				Main.netMode = NetmodeID.MultiplayerClient;
				check(!companion.RespondToQuestion(first, CompanionAnswer.First), "Client independently mutated a conversation");
				Main.netMode = mode;
				check(companion.RespondToQuestion(first, CompanionAnswer.First)
					&& companion.Profile.Voice == CompanionVoice.Direct
					&& companion.Profile.GatheringInitiative == CompanionInitiativePolicy.Always
					&& companion.Profile.Mood == 52 && companion.Profile.Bond == 11,
					"Helpful answer did not change permission, mood, bond and voice");
				check(!companion.RespondToQuestion(first, CompanionAnswer.First) && companion.Profile.Bond == 11,
					"Repeated answer farmed a bond reward");
				check(sigil.Profile.Voice == CompanionVoice.Direct && sigil.Profile.GatheringInitiative == CompanionInitiativePolicy.Always,
					"Answer effects did not persist to the bound sigil");
				Begin(CompanionQuestion.Company);
				companion.RespondToQuestion(companion.QuestionId, CompanionAnswer.Second);
				check(companion.Profile.Voice == CompanionVoice.Playful && companion.Profile.Mood == 56,
					"Playful answer did not change tone and mood");
				Begin(CompanionQuestion.Company);
				companion.RespondToQuestion(companion.QuestionId, CompanionAnswer.Third);
				check(companion.Profile.Voice == CompanionVoice.Soft
					&& companion.Profile.MiningInitiative == CompanionInitiativePolicy.Ask,
					"Gentle answer changed unrelated world-edit permissions");
				Begin(CompanionQuestion.Company);
				int mood = companion.Profile.Mood, bond = companion.Profile.Bond;
				companion.RespondToQuestion(companion.QuestionId, CompanionAnswer.Later);
				check(companion.Profile.Mood == mood && companion.Profile.Bond == bond, "Later penalized or rewarded the player");
				var prompt = new InitiativePromptSystem();
				Type promptType = typeof(InitiativePromptSystem);
				promptType.GetField("center", flags)!.SetValue(prompt, new Vector2(400, 300));
				for (int index = 0; index < 4; index++) {
					Vector2 position = (Vector2)promptType.GetMethod("ResponsePosition", flags)!.Invoke(prompt, new object[] { index })!;
					check((int)promptType.GetMethod("FindHoveredResponse", flags)!.Invoke(prompt, new object[] { position })! == index,
						"Radial answer hit test selected the wrong node: " + index);
					if (mode == NetmodeID.Server) continue;
					Begin(CompanionQuestion.Company);
					promptType.GetField("companion", flags)!.SetValue(prompt, companion);
					promptType.GetField("openProfileId", flags)!.SetValue(prompt, companion.Profile.Id);
					promptType.GetField("conversation", flags)!.SetValue(prompt, true);
					promptType.GetField("openQuestionId", flags)!.SetValue(prompt, companion.QuestionId);
					Main.myPlayer = 0;
					promptType.GetMethod("Respond", flags)!.Invoke(prompt, new object[] { index });
					Main.myPlayer = 255;
					check(!companion.HasPendingQuestion, "Radial answer did not reach the companion: " + index);
					if (index < 3) check(companion.Profile.Voice == (index == 0 ? CompanionVoice.Direct
						: index == 1 ? CompanionVoice.Playful : CompanionVoice.Soft), "Radial answer changed the wrong preference: " + index);
				}
				Begin(CompanionQuestion.Company); Guid expired = companion.QuestionId;
				Set("questionTicks", 1); Tick("UpdateChoiceConversation");
				check(!companion.HasPendingQuestion && !companion.RespondToQuestion(expired, CompanionAnswer.First),
					"Expired question still accepted an answer");
				Begin(CompanionQuestion.Company); companion.SetCommand(stay: true);
				check(!companion.HasPendingQuestion && !Begin(CompanionQuestion.Company), "Stay retained a social popup");
				companion.SetCommand(stay: false);
				Begin(CompanionQuestion.Company); Set("guardianTarget", 21);
				Tick("UpdateChoiceConversation");
				check(companion.HasPendingQuestion && !companion.RespondToQuestion(companion.QuestionId, CompanionAnswer.First),
					"Combat discarded a question or allowed its answer to interrupt defense");
				Set("guardianTarget", -1);
				companion.RespondToQuestion(companion.QuestionId, CompanionAnswer.Later);

				Wallet(1234567);
				companion.Profile.Store(new Item(ItemID.Gel, 7));
				check(Begin(CompanionQuestion.Wallet), "Coin savings did not allow a gift offer");
				Guid gift = companion.QuestionId; BigInteger money = Money();
				check(companion.RespondToQuestion(gift, CompanionAnswer.First) && companion.Profile.WalletCopper.IsZero
					&& Money() == money && companion.Profile.ItemCount(ItemID.Gel) == 7,
					"Wallet gift lost/minted coins or unloaded resources");
				check(!companion.RespondToQuestion(gift, CompanionAnswer.First) && Money() == money,
					"Repeated gift answer minted money");
				Wallet(BigInteger.Pow(10, 25));
				Begin(CompanionQuestion.Wallet); money = Money();
				companion.RespondToQuestion(companion.QuestionId, CompanionAnswer.First);
				check(Money() == money && companion.Profile.WalletCopper > 0, "Huge wallet overflowed or dropped its remainder");
				for (int slot = 1; slot < owner.inventory.Length; slot++) {
					owner.inventory[slot] = new Item(ItemID.StoneBlock); owner.inventory[slot].stack = owner.inventory[slot].maxStack;
				}
				Begin(CompanionQuestion.Wallet); BigInteger before = companion.Profile.WalletCopper;
				bond = companion.Profile.Bond;
				companion.RespondToQuestion(companion.QuestionId, CompanionAnswer.First);
				check(companion.Profile.WalletCopper == before && companion.Profile.Bond == bond,
					"Full inventory lost coins or rewarded an unsuccessful transfer");
				Begin(CompanionQuestion.Wallet);
				companion.RespondToQuestion(companion.QuestionId, CompanionAnswer.Second);
				check(companion.Profile.WalletCopper == before && companion.Profile.Voice == CompanionVoice.Direct,
					"Saving answer changed wallet balance");
				Begin(CompanionQuestion.Wallet);
				companion.RespondToQuestion(companion.QuestionId, CompanionAnswer.Third);
				check(companion.Profile.WalletCopper == before && companion.Profile.Voice == CompanionVoice.Playful,
					"Treasure-hunter answer withdrew coins");

				companion.Profile.ClearCargo();
				Item Drop(int itemType, int count, int reserved = 255, int delay = 0) => new Item(itemType, count) {
					active = true, whoAmI = 10, playerIndexTheItemIsReservedFor = reserved, noGrabDelay = delay, Center = npc.Center
				};
				void Pickup() { Set("nearbyPickupTimer", 29); Tick("UpdateNearbyPickup"); }
				Main.item[10] = Drop(ItemID.SilverCoin, 5);
				companion.Profile.GatheringInitiative = CompanionInitiativePolicy.Ask;
				Pickup(); check(Main.item[10].stack == 5 && companion.Profile.WalletCopper.IsZero, "Ask picked up without permission");
				companion.Profile.GatheringInitiative = CompanionInitiativePolicy.Always;
				Pickup(); check(companion.Profile.WalletCopper == 500 && !Main.item[10].active,
					"Approved nearby coins were not stored in the wallet");
				Main.item[10] = Drop(ItemID.CopperCoin, 8, reserved: 1);
				Pickup(); check(Main.item[10].stack == 8 && companion.Profile.WalletCopper == 500, "Pickup stole another owner's coins");
				Main.item[10] = Drop(ItemID.CopperCoin, 8, delay: 120);
				Pickup(); check(Main.item[10].stack == 8 && companion.Profile.WalletCopper == 500, "Pickup ignored native no-grab delay");
				Main.item[10] = Drop(ItemID.CopperCoin, 8);
				companion.Profile.GatheringInitiative = CompanionInitiativePolicy.Never;
				Pickup(); check(Main.item[10].stack == 8, "Passive pickup bypassed Never");
				companion.Profile.GatheringInitiative = CompanionInitiativePolicy.Always;
				companion.Profile.AutonomyEnabled = false;
				Pickup(); check(Main.item[10].stack == 8, "Passive pickup bypassed disabled autonomy");
				companion.Profile.AutonomyEnabled = true;
				companion.SetCommand(stay: true);
				Pickup(); check(Main.item[10].stack == 8, "Passive pickup bypassed Stay");
				companion.SetCommand(stay: false); Set("guardianTarget", 21);
				Pickup(); check(Main.item[10].stack == 8, "Passive pickup interrupted combat"); Set("guardianTarget", -1);
				Main.item[10] = Drop(ItemID.Gel, 99);
				Pickup(); check(companion.Profile.ItemCount(ItemID.Gel) == 50 && Main.item[10].stack == 49,
					"Passive pickup bypassed shared resource capacity");
				Main.netMode = NetmodeID.MultiplayerClient;
				Main.item[10] = Drop(ItemID.GoldCoin, 2); Pickup();
				check(Main.item[10].stack == 2 && companion.Profile.WalletCopper == 500, "Client performed passive pickup");
				Main.netMode = mode;
				companion.Profile.MiningInitiative = companion.Profile.ForestryInitiative = companion.Profile.TreasureInitiative
					= CompanionInitiativePolicy.Never;
				Set("autonomyDecisionTimer", 0);
				Tick("UpdateHelpfulAutonomy");
				check(Convert.ToInt32(type.GetField("autonomyActivity", flags)!.GetValue(companion)) == 1
					&& !companion.HasPendingInitiative, "Always gathering waited for an available question UI");
				companion.PerformQuickAction(CompanionQuickAction.GatheringPolicy);
				companion.Profile.GatheringInitiative = CompanionInitiativePolicy.Always;
				if (mode == NetmodeID.Server) {
					Set("walletQuestionCooldown", 0); Set("companyQuestionCooldown", 1000); Set("personalQuestionCooldown", 0);
					Set("lastOfferedWallet", BigInteger.Zero); Set("speechTimer", 0);
					Tick("UpdateChoiceConversation");
					check(companion.PendingQuestion == CompanionQuestion.Wallet, "Accumulated coins did not trigger an automatic offer");
					Guid token = companion.QuestionId;
					using var body = new MemoryStream();
					using (var writer = new BinaryWriter(body, Encoding.UTF8, true)) companion.SendExtraAI(writer);
					var remoteNpc = new NPC(); remoteNpc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
					var remote = (SoulboundCompanion)remoteNpc.ModNPC;
					body.Position = 0;
					using (var reader = new BinaryReader(body, Encoding.UTF8, true)) remote.ReceiveExtraAI(reader);
					check(remote.HasPendingQuestion && remote.QuestionId == token && remote.PendingQuestion == CompanionQuestion.Wallet,
						"Question/token did not survive NPC synchronization");
					byte message = Convert.ToByte(Enum.Parse(mod.GetType().GetNestedType("MessageType", BindingFlags.NonPublic)!, "ChoiceResponseRequest"));
					void Packet(Guid profileId, Guid questionToken, int sender = 0) {
						using var packet = new MemoryStream();
						using (var writer = new BinaryWriter(packet, Encoding.UTF8, true)) {
							writer.Write(message); writer.Write(profileId.ToByteArray()); writer.Write(questionToken.ToByteArray());
							writer.Write((byte)CompanionAnswer.Second);
						}
						packet.Position = 0; using var reader = new BinaryReader(packet); mod.HandlePacket(reader, sender);
					}
					Packet(Guid.NewGuid(), token);
					Packet(companion.Profile.Id, Guid.NewGuid());
					check(companion.HasPendingQuestion, "Unbound multiplayer answer changed the question");
					Packet(companion.Profile.Id, token);
					check(!companion.HasPendingQuestion && companion.Profile.Voice == CompanionVoice.Direct,
						"Valid owned multiplayer answer was not applied");
					bond = companion.Profile.Bond; Packet(companion.Profile.Id, token);
					check(companion.Profile.Bond == bond, "Network replay farmed bond");
					Set("walletQuestionCooldown", 0); Set("speechTimer", 0); Tick("UpdateChoiceConversation");
					check(!companion.HasPendingQuestion, "Unchanged wallet offered the same savings again");
				}
			}
		}
		finally {
			Main.player[0] = oldPlayer; Main.npc = oldNpcs; Main.item = oldItems;
			Main.netMode = oldMode; Main.myPlayer = oldMyPlayer;
			for (int i = 0; i < oldClients.Length; i++) Netplay.Clients[i] = oldClients[i];
		}
	}

	private static void CheckCritterModes(Action<bool, string> check)
	{
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
		Player oldPlayer = Main.player[0];
		NPC[] oldNpcs = Main.npc;
		Item[] oldItems = Main.item;
		Tilemap oldMap = Main.tile;
		int oldWidth = Main.maxTilesX, oldHeight = Main.maxTilesY, oldMode = Main.netMode, oldMyPlayer = Main.myPlayer;
		RemoteClient[] oldClients = (RemoteClient[])Netplay.Clients.Clone();
		try {
			Main.maxTilesX = Main.maxTilesY = 100;
			Main.tile = (Tilemap)Activator.CreateInstance(typeof(Tilemap), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
				null, new object[] { (ushort)100, (ushort)100 }, null)!;
			for (int i = 0; i < Netplay.Clients.Length; i++) Netplay.Clients[i] = new RemoteClient { Socket = new ProbeSocket() };
			foreach (int mode in new[] { NetmodeID.SinglePlayer, NetmodeID.Server }) {
				Main.netMode = mode; Main.myPlayer = mode == NetmodeID.Server ? 255 : 0;
				Main.npc = new NPC[oldNpcs.Length];
				for (int i = 0; i < Main.npc.Length; i++) Main.npc[i] = new NPC { whoAmI = i };
				Main.item = new Item[oldItems.Length];
				for (int i = 0; i < Main.item.Length; i++) Main.item[i] = new Item { whoAmI = i };
				var owner = new Player { whoAmI = 0, active = true, Center = new Vector2(400, 400) };
				Main.player[0] = owner;
				NPC npc = Main.npc[20]; npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
				npc.active = true; npc.ai[0] = 0; npc.Center = owner.Center;
				var companion = (SoulboundCompanion)npc.ModNPC;
				companion.Profile.Name = "Luma";
				typeof(SoulboundCompanion).GetField("speechTimer", flags)!.SetValue(companion, 1000);
				owner.inventory[0] = new Item(ModContent.ItemType<SoulboundSigil>());
				((SoulboundSigil)owner.inventory[0].ModItem).Profile = companion.Profile.Clone();
				NPC Critter(int index, int type = NPCID.Bunny) {
					NPC result = Main.npc[index]; result.SetDefaults(type); result.active = true; result.Center = npc.Center;
					return result;
				}
				void ResetCatch() => typeof(SoulboundCompanion).GetField("insectCatchCooldown", flags)!.SetValue(companion, 0);
				bool Catch() => (bool)typeof(SoulboundCompanion).GetMethod("TryCatchCritter", flags)!.Invoke(companion, new object?[] { false, null })!;
				bool Join(NPC critter) => (bool)typeof(CompanionCritterCompany).GetMethod("TryJoin", flags)!
					.Invoke(critter.GetGlobalNPC<CompanionCritterCompany>(), new object[] { critter, companion })!;
				bool Belongs(CompanionCritterCompany company) => (bool)typeof(CompanionCritterCompany).GetMethod("BelongsTo", flags)!
					.Invoke(company, new object[] { companion })!;
				bool Activity() {
					typeof(SoulboundCompanion).GetMethod("UpdateHelpfulAutonomy", flags)!.Invoke(companion, null);
					return (bool)typeof(SoulboundCompanion).GetMethod("UpdateCritterActivity", flags)!.Invoke(companion, null)!;
				}
				void Set(string field, object value) => typeof(SoulboundCompanion).GetField(field, flags)!.SetValue(companion, value);
				object Get(string field) => typeof(SoulboundCompanion).GetField(field, flags)!.GetValue(companion)!;
				void Watch() => typeof(SoulboundCompanion).GetMethod("UpdateCritterWatch", flags)!.Invoke(companion, null);
				CompanionConversationResult NpcContext(CompanionNpcAction action, int index, int type) =>
					(CompanionConversationResult)typeof(SoulboundCompanion).GetMethod("PerformNpcContext", flags)!
						.Invoke(companion, new object[] { action, index, type })!;

				foreach (CompanionCritterMode setting in Enum.GetValues<CompanionCritterMode>()) {
					var profile = new CompanionProfile { CritterMode = setting };
					check(profile.Clone().CritterMode == setting && CompanionProfile.Load(profile.Save()).CritterMode == setting,
						"Critter mode lost in clone or native save/load: " + setting);
					using var stream = new MemoryStream();
					using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) profile.Write(writer);
					stream.Position = 0;
					using var reader = new BinaryReader(stream);
					check(CompanionProfile.Read(reader).CritterMode == setting, "Critter mode lost during profile transport");
				}
				TagCompound legacy = companion.Profile.Save(); legacy.Remove("critterMode");
				check(CompanionProfile.Load(legacy).CritterMode == CompanionCritterMode.Watch, "Legacy Sigil enabled catching by default");
				var invalid = new CompanionProfile { CritterMode = (CompanionCritterMode)255 }; invalid.Normalize();
				check(invalid.CritterMode == CompanionCritterMode.Watch, "Invalid critter mode survived normalization");
				check(companion.PerformQuickAction(CompanionQuickAction.CritterCollect).Accepted
					&& companion.Profile.CritterMode == CompanionCritterMode.Collect && companion.Profile.ItemCount(ItemID.BugNet) == 0,
					"Collect could not activate with the built-in net or fabricated equipment");
				check(companion.PerformQuickAction(CompanionQuickAction.CritterCollect).Accepted
					&& ((SoulboundSigil)owner.inventory[0].ModItem).Profile.CritterMode == CompanionCritterMode.Collect,
					"Collect choice was not saved to the bound Sigil");
				NPC bunny = Critter(19);
				int caughtType = bunny.catchItem;
				check(CompanionCritters.IsCommon(bunny) && Catch() && !bunny.active, "Native non-AETHER bunny catch failed");
				check(companion.Profile.ItemCount(caughtType) == 1, "Bunny catch did not enter real cargo exactly once");
				ResetCatch(); check(!Catch() && companion.Profile.ItemCount(caughtType) == 1, "Inactive bunny was collected twice");
				foreach (int type in new[] { NPCID.Bunny, NPCID.Bird, NPCID.Squirrel, NPCID.Firefly, NPCID.Worm }) {
					NPC common = Critter(19, type);
					check(CompanionCritters.IsCommon(common), "Natural common critter excluded: " + type);
					common.SpawnedFromStatue = true; check(!CompanionCritters.IsCommon(common), "Statue critter accepted");
					common.SpawnedFromStatue = false; common.releaseOwner = 0;
					check(!CompanionCritters.IsCommon(common), "Released critter accepted");
				}
				check(!CompanionCritters.IsCommon(Critter(19, NPCID.GoldBunny)), "Gold critter lost its automatic-catch protection");
				Critter(19, NPCID.GoldButterfly); companion.Profile.Name = "AETHER"; ResetCatch();
				check(!(bool)typeof(SoulboundCompanion).GetMethod("TryCatchCritter", flags)!.Invoke(companion, new object?[] { true, null })!
					&& Main.npc[19].active, "Forestry's insect shortcut bypassed gold-critter protection");
				companion.Profile.Name = "Luma";
				NPC lava = Critter(19, NPCID.Lavafly);
				check(!CompanionCritters.CanUseNet(lava, new Item(ItemID.BugNet))
					&& CompanionCritters.CanUseNet(lava, new Item(ItemID.GoldenBugNet)), "Native lava-net restriction bypassed");
				bunny = Critter(19);
				companion.Profile.Store(new Item(caughtType, 99)); ResetCatch();
				check(!Catch() && bunny.active, "Full companion cargo consumed a living bunny");
				companion.Profile.ClearCargo(); companion.Profile.Store(new Item(ItemID.BugNet));
				companion.PerformQuickAction(CompanionQuickAction.CritterWatch);
				Activity(); check(bunny.active && companion.Profile.ItemCount(caughtType) == 0, "Watch caught a critter");
				Set("activeJob", CompanionJob.Mine);
				Watch();
				check((int)Get("critterWatchCooldown") == 600 && (int)Get("critterNoticeCooldown") == 0,
					"Passive Watch failed during work or blocked an unheard speech line");
				check(ReferenceEquals(Get("pendingCritterNotice"), bunny), "A busy speech line discarded the critter greeting");
				Set("speechTimer", 0);
				typeof(SoulboundCompanion).GetMethod("UpdateCritterNoticeSpeech", flags)!.Invoke(companion, null);
				check(Get("pendingCritterNotice") is null && (string)Get("speechText") ==
					SoulmatesText.Get($"Social.Critters.Greeting.{companion.Profile.Personality}", bunny.TypeName),
					"Queued first greeting did not speak after the existing line ended");
				Set("critterNoticeCooldown", 0);
				Set("critterWatchCooldown", 0); Set("speechTimer", 0); Set("greetedCritter", null!);
				typeof(SoulboundCompanion).GetMethod("ClearNativeExpression", flags)!.Invoke(companion, null);
				Watch();
				check((string)Get("speechText") == SoulmatesText.Get($"Social.Critters.Greeting.{companion.Profile.Personality}", bunny.TypeName),
					"First critter observation did not greet it");
				Set("activeJob", CompanionJob.None);
				companion.PerformQuickAction(CompanionQuickAction.CritterCompany);
				companion.Profile.CritterCompanyInitiative = CompanionInitiativePolicy.Always;
				var company = bunny.GetGlobalNPC<CompanionCritterCompany>();
				Set("activeJob", CompanionJob.Mine); Set("speechTimer", 600);
				Watch();
				check(ReferenceEquals(Get("pendingCritterNotice"), bunny) && Get("critterTarget") is null,
					"Company failed to passively notice critters while explicit work had priority");
				Set("activeJob", CompanionJob.None);
				Main.item[12] = new Item(ItemID.CopperOre, 2) { active = true, playerIndexTheItemIsReservedFor = 255 };
				Main.item[12].Center = npc.Center + new Vector2(120, 0);
				companion.Profile.GatheringInitiative = CompanionInitiativePolicy.Always;
				Set("autonomyDecisionTimer", 0);
				for (int tick = 0; tick < 1800 && !Belongs(company); tick++) { npc.AI(); npc.position += npc.velocity; }
				check(Belongs(company), "New gathering work starved a nearby Company invitation for 30 seconds in full companion AI");
				check((int)Get("critterDecisionTimer") == 300, "Completed critter visit did not leave a bounded work interval");
				Main.item[12].active = false;
				Set("autonomyActivity", Enum.ToObject(typeof(SoulboundCompanion).GetField("autonomyActivity", flags)!.FieldType, 0));
				NPC second = Critter(18);
				check(!Join(second), "Ordinary companion exceeded one real critter friend");
				companion.Profile.Name = "AETHER";
				check(Join(second) && Join(Critter(17, NPCID.Firefly)) && !Join(Critter(16)), "AETHER company limit was not three");
				int health = bunny.life, style = bunny.aiStyle, drop = bunny.catchItem;
				Vector2 position = bunny.position;
				npc.Center += new Vector2(180, 0); bunny.velocity = Vector2.Zero;
				bunny.AI();
				check(bunny.velocity.X > 0 && bunny.position == position && bunny.life == health && bunny.aiStyle == style
					&& bunny.catchItem == drop && !bunny.noTileCollide, "Company replaced native physics, health or drop rules");
				bunny.velocity.X = -2f;
				company.PostAI(bunny);
				check(bunny.velocity.X > 0 && bunny.velocity.X <= 1.8f && bunny.direction == 1,
					"Native opposite walking direction overpowered company guidance");
				NPC flyingFriend = Main.npc[17]; flyingFriend.velocity = new Vector2(-3, 0);
				flyingFriend.GetGlobalNPC<CompanionCritterCompany>().PostAI(flyingFriend);
				check(flyingFriend.velocity.X > 0 && flyingFriend.velocity.Length() <= 2.81f,
					"Native flying direction overpowered company guidance");
				using (var body = new MemoryStream()) {
					var bits = new BitWriter();
					using (var writer = new BinaryWriter(body, Encoding.UTF8, true)) company.SendExtraAI(bunny, bits, writer);
					using var combined = new MemoryStream();
					using (var writer = new BinaryWriter(combined, Encoding.UTF8, true)) { bits.Flush(writer); writer.Write(body.ToArray()); }
					combined.Position = 0;
					using var reader = new BinaryReader(combined);
					var received = new CompanionCritterCompany(); received.ReceiveExtraAI(bunny, new BitReader(reader), reader);
					check(Belongs(received), "Native critter extra-AI lost its companion identity");
				}
				Guid originalId = companion.Profile.Id; companion.Profile.Id = Guid.NewGuid();
				company.PostAI(bunny); check(!Belongs(company) && bunny.active, "NPC slot reuse inherited a previous companion's friend");
				companion.Profile.Id = originalId;
				npc.Center = bunny.Center; check(Join(bunny), "Released friend could not rejoin");
				bunny.Center += new Vector2(700, 0); company.PostAI(bunny);
				check(!Belongs(company) && bunny.active, "Distant friend teleported instead of returning to native life");
				bunny.Center = npc.Center;
				companion.PerformQuickAction(CompanionQuickAction.CritterOff);
				check(!Belongs(second.GetGlobalNPC<CompanionCritterCompany>()) && second.active && !Activity(), "Off did not release real company");
				companion.PerformQuickAction(CompanionQuickAction.CritterCompany); companion.SetCommand(stay: true);
				check(!Join(bunny) && !Activity(), "Stay started new critter visits");
				companion.SetCommand(stay: false); companion.Profile.AutonomyEnabled = false;
				check(!Join(bunny) && !Activity(), "Disabled autonomy started critter visits");
				companion.Profile.AutonomyEnabled = true;
				typeof(SoulboundCompanion).GetField("activeJob", flags)!.SetValue(companion, CompanionJob.Mine);
				check(!Join(bunny) && !Activity(), "Assigned work lost priority to critter visits");
				typeof(SoulboundCompanion).GetField("activeJob", flags)!.SetValue(companion, CompanionJob.None);
				typeof(SoulboundCompanion).GetField("guardianTarget", flags)!.SetValue(companion, 16);
				check(!Join(bunny) && !Activity(), "Combat lost priority to critter visits");
				typeof(SoulboundCompanion).GetField("guardianTarget", flags)!.SetValue(companion, -1);
				companion.Profile.Energy = 10;
				check(!Join(bunny) && !Activity(), "Low energy started critter visits");
				companion.Profile.Energy = 100;
				check(Join(bunny), "Critter could not rejoin after autonomy resumed");
				Main.netMode = NetmodeID.MultiplayerClient; ResetCatch();
				check(!Join(Critter(16)) && !Catch() && bunny.active, "Client independently caught or befriended a critter");
				Main.netMode = mode;
				check(NPC.CheckCatchNPC(bunny, bunny.Hitbox, new Item(ItemID.BugNet), owner) && !bunny.active,
					"Native player net could no longer catch a real critter friend");
				int worldCount = 0;
				foreach (Item item in Main.item) if (item.active && item.type == caughtType) worldCount += item.stack;
				check(worldCount == 1 && companion.Profile.ItemCount(caughtType) == 0, "Native friend catch duplicated or generated companion cargo");
				check((int)Get("critterLossCooldown") == 0, "A native net catch was reported as a critter death");
				var bolt = new Projectile(); bolt.SetDefaults(ModContent.ProjectileType<SoulBolt>());
				bunny = Critter(19);
				check(((SoulBolt)bolt.ModProjectile).CanHitNPC(bunny) == false, "Soul Bolt could harm a harmless critter");
				check(((SoulBolt)bolt.ModProjectile).CanHitNPC(Critter(16, NPCID.Zombie)) is null,
					"Critter protection disabled ordinary hostile combat");
				Main.npc[16].active = false;
				int xp = companion.Profile.Experience, defeats = companion.Profile.DefeatedEnemies, mood = companion.Profile.Mood;
				foreach (CompanionPersonality personality in Enum.GetValues<CompanionPersonality>()) {
					foreach (bool attacked in new[] { false, true }) {
						bunny = Critter(19); companion.Profile.Personality = personality;
						Set("critterLossCooldown", 0); Set("speechTimer", 0);
						if (attacked) { bunny.lastInteraction = 0; bunny.playerInteraction[0] = true; }
						bunny.StrikeNPC(new NPC.HitInfo { Damage = 100, HitDirection = 1, HideCombatText = true }, noPlayerInteraction: true);
						check((string)Get("speechText") == SoulmatesText.Get($"Social.Critters.{(attacked ? "Harmed" : "Loss")}.{personality}", bunny.TypeName),
							"Native critter death did not select the correct reaction: " + personality + "/" + attacked);
					}
				}
				check(companion.Profile.Experience == xp && companion.Profile.DefeatedEnemies == defeats && companion.Profile.Mood == mood,
					"Critter loss generated progression or punished the player");
				bunny = Critter(19); Set("critterLossCooldown", 0); Set("speechTimer", 600);
				bunny.StrikeNPC(new NPC.HitInfo { Damage = 100, HitDirection = 1, HideCombatText = true }, noPlayerInteraction: true);
				check(((string)Get("pendingCritterLossKey")).Length > 0 && (int)Get("speechTimer") == 600,
					"Critter death interrupted an existing conversation instead of queueing its line");
				Set("speechTimer", 0);
				typeof(SoulboundCompanion).GetMethod("UpdateCritterLossSpeech", flags)!.Invoke(companion, null);
				check(((string)Get("pendingCritterLossKey")).Length == 0, "Queued apology did not resume after speech ended");
				Set("critterLossCooldown", 0);
				Main.netMode = NetmodeID.MultiplayerClient;
				new CompanionCritterLife().OnKill(bunny);
				check((int)Get("critterLossCooldown") == 0, "Client duplicated the authority's critter death reaction");
				Main.netMode = mode;
				companion.PerformQuickAction(CompanionQuickAction.CritterOff);
				bunny = Critter(19);
				check(NpcContext(CompanionNpcAction.Look, 19, NPCID.Bunny).Accepted
					&& companion.Profile.CritterMode == CompanionCritterMode.Off && Get("critterTarget") is null,
					"Looking at a critter changed its mode or started a visit");
				check(!NpcContext(CompanionNpcAction.Company, 19, NPCID.Squirrel).Accepted
					&& !NpcContext(CompanionNpcAction.Company, -1, NPCID.Bunny).Accepted,
					"Invalid NPC identity or slot accepted a company order");
				bunny.Center += new Vector2(700, 0);
				check(!NpcContext(CompanionNpcAction.Company, 19, NPCID.Bunny).Accepted, "Distant critter invitation accepted");
				bunny.Center = npc.Center;
				companion.Profile.ClearCargo();
				check(NpcContext(CompanionNpcAction.Company, 19, NPCID.Bunny).Accepted
					&& companion.Profile.CritterMode == CompanionCritterMode.Company
					&& ReferenceEquals(Get("critterTarget"), bunny), "Direct no-net invitation lost the clicked critter");
				companion.PerformQuickAction(CompanionQuickAction.CritterOff);
				void ContextPacket(Guid profileId, byte action, int index, int type, int sender = 0) {
					using var body = new MemoryStream();
					using (var writer = new BinaryWriter(body, Encoding.UTF8, true)) {
						writer.Write((byte)21); writer.Write(profileId.ToByteArray()); writer.Write(action);
						writer.Write((short)index); writer.Write(type);
					}
					body.Position = 0; using var reader = new BinaryReader(body);
					ModContent.GetInstance<global::Soulmates.Soulmates>().HandlePacket(reader, sender);
				}
				Main.netMode = NetmodeID.Server;
				ContextPacket(Guid.NewGuid(), 1, 19, NPCID.Bunny);
				ContextPacket(companion.Profile.Id, 255, 19, NPCID.Bunny);
				ContextPacket(companion.Profile.Id, 1, 19, NPCID.Squirrel);
				ContextPacket(companion.Profile.Id, 1, -1, NPCID.Bunny);
				ContextPacket(companion.Profile.Id, 1, 19, NPCID.Bunny, 1);
				check(companion.Profile.CritterMode == CompanionCritterMode.Off && Get("critterTarget") is null,
					"Forged, invalid, stale or non-owner NPC context packet changed companion state");
				Main.netMode = NetmodeID.MultiplayerClient;
				ContextPacket(companion.Profile.Id, 1, 19, NPCID.Bunny);
				check(companion.Profile.CritterMode == CompanionCritterMode.Off, "Wrong-direction context packet invited a critter");
				Main.netMode = NetmodeID.Server;
				ContextPacket(companion.Profile.Id, 1, 19, NPCID.Bunny);
				check(companion.Profile.CritterMode == CompanionCritterMode.Company && ReferenceEquals(Get("critterTarget"), bunny),
					"Authorized NPC context packet did not target the clicked critter");
				Main.netMode = mode;
				bunny = Critter(19); check(Join(bunny), "Friend join before recall failed");
				companion.Recall();
				check(bunny.active && !Belongs(bunny.GetGlobalNPC<CompanionCritterCompany>()), "Recall deleted its real critter or left a stale binding");
			}
		}
		finally {
			Main.player[0] = oldPlayer; Main.npc = oldNpcs; Main.item = oldItems;
			Main.tile = oldMap; Main.maxTilesX = oldWidth; Main.maxTilesY = oldHeight; Main.netMode = oldMode; Main.myPlayer = oldMyPlayer;
			for (int i = 0; i < oldClients.Length; i++) Netplay.Clients[i] = oldClients[i];
		}
	}

	private static void CheckLivingBehavior(Action<bool, string> check)
	{
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
		Player oldPlayer = Main.player[0]; NPC[] oldNpcs = Main.npc; Item[] oldItems = Main.item;
		Tilemap oldMap = Main.tile;
		int oldWidth = Main.maxTilesX, oldHeight = Main.maxTilesY, oldMode = Main.netMode, oldMyPlayer = Main.myPlayer;
		RemoteClient[] oldClients = (RemoteClient[])Netplay.Clients.Clone();
		try {
			Main.maxTilesX = Main.maxTilesY = 100;
			Main.tile = (Tilemap)Activator.CreateInstance(typeof(Tilemap), flags | BindingFlags.Public,
				null, new object[] { (ushort)100, (ushort)100 }, null)!;
			for (int i = 0; i < Netplay.Clients.Length; i++) Netplay.Clients[i] = new RemoteClient { Socket = new ProbeSocket() };
			foreach (int mode in new[] { NetmodeID.SinglePlayer, NetmodeID.Server }) {
				Main.netMode = mode; Main.myPlayer = mode == NetmodeID.Server ? 255 : 0;
				Main.npc = Enumerable.Range(0, oldNpcs.Length).Select(i => new NPC { whoAmI = i }).ToArray();
				Main.item = Enumerable.Range(0, oldItems.Length).Select(i => new Item { whoAmI = i }).ToArray();
				var owner = new Player { whoAmI = 0, active = true, Center = new Vector2(400, 400) };
				Main.player[0] = owner;
				NPC npc = Main.npc[20]; npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
				npc.active = true; npc.ai[0] = 0; npc.Center = owner.Center;
				var mate = (SoulboundCompanion)npc.ModNPC;
				mate.Profile.Name = "Luma";
				owner.inventory[0] = new Item(ModContent.ItemType<SoulboundSigil>());
				owner.inventory[1] = new Item(ItemID.CopperPickaxe);
				owner.inventory[2] = new Item(ItemID.CopperAxe);
				((SoulboundSigil)owner.inventory[0].ModItem).Profile = mate.Profile.Clone();
				void Set(string field, object? value) => typeof(SoulboundCompanion).GetField(field, flags)!.SetValue(mate, value);
				object? Get(string field) => typeof(SoulboundCompanion).GetField(field, flags)!.GetValue(mate);
				object? Call(string method, params object?[] args) => typeof(SoulboundCompanion).GetMethod(method,
					flags | BindingFlags.Static)!.Invoke(mate, args);
				void Tick(int count) { for (int i = 0; i < count; i++) { npc.AI(); npc.position += npc.velocity; } }
				NPC Critter(int index) { NPC n = Main.npc[index]; n.SetDefaults(NPCID.Bunny); n.active = true; n.Center = npc.Center; return n; }
				Set("speechTimer", 3000); Set("socialTimer", 10000); Set("ambientStoryCooldown", 10000);
				Set("companyQuestionCooldown", 10000); Set("walletQuestionCooldown", 10000);
				foreach (var example in new[] {
					(ItemID.CookedFish, CompanionItemTopic.Food), (ItemID.CopperOre, CompanionItemTopic.Ores),
					(ItemID.BugNet, CompanionItemTopic.Tools), (ItemID.CopperShortsword, CompanionItemTopic.Weapons),
					(ItemID.LesserHealingPotion, CompanionItemTopic.Recovery), (ItemID.Wood, CompanionItemTopic.Materials),
					(ItemID.Bunny, CompanionItemTopic.Critters), (ItemID.MagicMirror, CompanionItemTopic.Other)
				}) check(CompanionItemTopics.Classify(new Item(example.Item1)) == example.Item2, "Native item topic: " + example.Item1);
				mate.Profile.Store(new Item(ItemID.CookedFish, 2)); mate.Profile.Store(new Item(ItemID.CopperOre, 3));
				int xp = mate.Profile.Experience, bond = mate.Profile.Bond, mood = mate.Profile.Mood, energy = mate.Profile.Energy;
				string food = mate.Converse(TalkCategory.Items, 0, 0, CompanionItemTopic.Food).Reply;
				check(food.Contains(Lang.GetItemNameValue(ItemID.CookedFish)) && !food.Contains(Lang.GetItemNameValue(ItemID.CopperOre)),
					"Item conversation ignored its selected category");
				mate.Profile.Memories.Clear();
				string beginning = mate.Converse(TalkCategory.Bond, 1, int.MinValue).Reply;
				check(beginning.Contains(SoulmatesText.Get("Stories.Beginning")), "Empty journal invented an event");
				mate.Profile.Remember(CompanionMemoryKind.ItemFound, (int)CompanionItemTopic.Ores, "actual copper");
				foreach (CompanionVoice voice in Enum.GetValues<CompanionVoice>()) {
					mate.Profile.Voice = voice;
					string story = mate.Converse(TalkCategory.Items, 2, int.MinValue, CompanionItemTopic.Ores).Reply;
					check(story.Contains("actual copper") && !story.Contains("Mods.Soulmates"), "Story lost its factual memory or leaked a key");
					if (voice != CompanionVoice.Direct) check(story.Length > mate.Profile.Memories[0].Describe().Length,
						"Story did not reflect on the actual event");
				}
				check(mate.Profile.Experience == xp && mate.Profile.Bond == bond && mate.Profile.Mood == mood
					&& mate.Profile.Energy == energy && mate.Profile.ItemCount(ItemID.CookedFish) == 2,
					"Repeated item stories farmed progression or changed cargo");
				foreach (CompanionMemoryKind kind in new[] { CompanionMemoryKind.ItemFound, CompanionMemoryKind.CritterMet, CompanionMemoryKind.CritterLost }) {
					mate.Profile.Remember(kind, detail: "actual event");
					check(CompanionProfile.Load(mate.Profile.Save()).Memories.Last().Kind == kind, "New memory did not survive native save/load");
				}
				mate.Profile.ClearCargo(); mate.Profile.Talent = CompanionTalent.Miner; mate.Profile.Energy = 100;
				Point ore = new(28, 25); Tile oreTile = Main.tile[ore.X, ore.Y]; oreTile.HasTile = true; oreTile.TileType = TileID.Copper;
				check(mate.PerformDirectOrder(CompanionTargetOrder.Mine, ore, -1).Accepted, "Directed mining fixture was rejected");
				check(mate.PerformQuickAction(CompanionQuickAction.Pause).Accepted && mate.Profile.WorkPaused, "Pause did not activate");
				mate.Profile.Energy = 30;
				Tick(240);
				check(Main.tile[ore.X, ore.Y].HasTile && (CompanionJob)Get("activeJob")! == CompanionJob.Mine
					&& (int)Get("jobCount")! == 0 && mate.Profile.Energy > 30, "Paused AI worked, lost its assignment or failed to recover");
				check(CompanionProfile.Load(mate.Profile.Save()).WorkPaused && mate.Profile.Clone().WorkPaused, "Paused state was not persistent");
				using (var stream = new MemoryStream()) {
					using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) mate.Profile.Write(writer);
					stream.Position = 0; using var reader = new BinaryReader(stream);
					check(CompanionProfile.Read(reader).WorkPaused, "Paused state was lost in profile transport");
				}
				TagCompound legacy = mate.Profile.Save(); legacy.Remove("workPaused");
				check(!CompanionProfile.Load(legacy).WorkPaused, "Old Sigil was paused by migration");
				mate.PerformQuickAction(CompanionQuickAction.Resume); Tick(360);
				check(!mate.Profile.WorkPaused && !Main.tile[ore.X, ore.Y].HasTile, "Full AI failed to resume the retained mining target");
				mate.StartJob(CompanionJob.Gather); mate.PerformQuickAction(CompanionQuickAction.Abort); Tick(240);
				check(mate.Profile.WorkPaused && mate.Profile.Routine == CompanionJob.None && (CompanionJob)Get("activeJob")! == CompanionJob.None,
					"Abort left an assignment or restarted autonomous work");
				mate.SetCommand(stay: false); check(!mate.Profile.WorkPaused, "A fresh explicit command did not release the work hold");
				foreach (Item drop in Main.item) drop.TurnToAir();
				mate.StartJob(CompanionJob.Mine); mate.Profile.Energy = 3;
				NPC bunny = Critter(19); bunny.Center += new Vector2(120, 0);
				Type snapshotType = typeof(CompanionWheelSystem).Assembly.GetType("Soulmates.Common.UI.SoulwheelTarget")!;
				object snapshot = Activator.CreateInstance(snapshotType, bunny.Center, npc.whoAmI)!;
				check((bool)snapshotType.GetProperty("CanOfferFallback")!.GetValue(snapshot)!
					&& (bool)snapshotType.GetMethod("CanInviteCritter")!.Invoke(snapshot, new object[] { mate })!
					&& (bool)snapshotType.GetMethod("CanCollectCritter")!.Invoke(snapshot, new object[] { mate })!,
					"Clicked critter actions disappeared behind energy or a current job");
				var result = (CompanionConversationResult)Call("PerformNpcContext", CompanionNpcAction.Company, 19, NPCID.Bunny)!;
				check(result.Accepted && mate.Profile.Routine == CompanionJob.None && (CompanionJob)Get("activeJob")! == CompanionJob.None,
					"Direct Company did not replace existing work");
				mate.PerformQuickAction(CompanionQuickAction.Pause); int visitTicks = (int)Get("critterVisitTicks")!;
				Tick(30); check(ReferenceEquals(Get("critterTarget"), bunny) && (int)Get("critterVisitTicks")! == visitTicks,
					"Pause discarded the selected critter or consumed its visit timeout");
				mate.PerformQuickAction(CompanionQuickAction.Resume); Tick(100);
				check((bool)typeof(CompanionCritterCompany).GetMethod("BelongsTo", flags)!
					.Invoke(bunny.GetGlobalNPC<CompanionCritterCompany>(), new object[] { mate })!,
					"Full low-energy AI never reached the directly invited critter");
				mate.PerformQuickAction(CompanionQuickAction.CritterOff); bunny = Critter(19);
				check(((CompanionConversationResult)Call("PerformNpcContext", CompanionNpcAction.Collect, 19, NPCID.Bunny)!).Accepted && bunny.active
					&& mate.Profile.ItemCount(ItemID.BugNet) == 0, "Clicked Collect rejected the built-in net or fabricated equipment");
				NPC other = Critter(18);
				Set("insectCatchCooldown", 0);
				check(((CompanionConversationResult)Call("PerformNpcContext", CompanionNpcAction.Collect, 19, NPCID.Bunny)!).Accepted,
					"Clicked Collect did not start with the built-in net");
				npc.AI();
				check(!bunny.active && other.active && mate.Profile.ItemCount(ItemID.Bunny) == 1,
					"Targeted native catch took a different critter or failed its real cargo transaction");
				bunny = Critter(19); Set("insectCatchCooldown", 0);
				Call("PerformNpcContext", CompanionNpcAction.Collect, 19, NPCID.Bunny);
				bunny.SetDefaults(NPCID.Squirrel); bunny.active = true; bunny.Center = npc.Center;
				Call("UpdateHelpfulAutonomy"); Call("UpdateCritterActivity");
				check(bunny.active && other.active && Get("critterTarget") is null && !(bool)Get("directedCritterVisit")!,
					"Changed NPC slot silently redirected the selected catch");
				bunny.active = false;
				mate.PerformQuickAction(CompanionQuickAction.CritterOff); other.active = false;
				mate.SetCommand(stay: false); mate.Profile.Energy = 100;
				owner.selectedItem = 2; owner.controlUseItem = true; Set("learningObservationTimer", 0);
				int insight = mate.Profile.GetInsight(LearnedBehavior.Forestry); Tick(61); owner.controlUseItem = false;
				check(mate.Profile.GetInsight(LearnedBehavior.Forestry) > insight, "Full AI did not observe the owner's actual axe use");
				mate.PerformQuickAction(CompanionQuickAction.Abort);
				Set("speechTimer", 0); Set("ambientStoryCooldown", 0); Set("socialTimer", 10000);
				npc.AI(); string ambient = (string)Get("speechText")!;
				check(ambient.Length > 0 && !ambient.Contains("Mods.Soulmates"), "Full AI failed to verbalize its real surroundings");
				Set("ambientStoryCooldown", 0); npc.AI();
				check((string)Get("speechText")! == ambient, "Ambient speech interrupted a currently visible answer");
				// A native-framed tree fixture has rooted central wood and only one bare side twig.
				for (int x = 35; x <= 45; x++) for (int y = 45; y <= 50; y++) {
					Tile ground = Main.tile[x, y]; ground.HasTile = true; ground.TileType = (ushort)(y == 45 ? TileID.Grass : TileID.Dirt);
				}
				for (int y = 20; y < 45; y++) {
					Tile stem = Main.tile[40, y]; stem.HasTile = true; stem.TileType = TileID.Trees;
					stem.TileFrameX = 0; stem.TileFrameY = 0;
				}
				foreach (bool left in new[] { true, false }) {
					Point twig = new(left ? 39 : 41, 28);
					Tile branch = Main.tile[twig.X, twig.Y]; branch.HasTile = true; branch.TileType = TileID.Trees;
					branch.TileFrameX = (short)(left ? 66 : 88); branch.TileFrameY = (short)(left ? 22 : 88);
					check((bool)Call("IsBareTreeBranch", twig)!, "Native bare branch frame was not recognized");
					foreach (Item drop in Main.item) drop.TurnToAir();
					check((bool)Call("PruneBranch", twig)! && !branch.HasTile, "Native branch pruning failed");
					check(Enumerable.Range(20, 25).All(y => IsTreeTile(40, y)) && Main.tile[40, 45].TileType == TileID.Grass,
						"Branch pruning destroyed the central stem or ground");
					int nativeWood = Main.item.Where(item => item.active && item.type == ItemID.Wood).Sum(item => item.stack);
					// KillTile_GetItemDrops adds one native bonus wood when KillTile_GetTreeDrops requests it.
					check(!Main.item.Any(item => item.active && item.type is ItemID.DirtBlock or ItemID.Acorn)
						&& nativeWood is 1 or 2,
						"Pruning deviated from the native wood drop: mode=" + mode + ", left=" + left + ", drops="
						+ string.Join(",", Main.item.Where(item => item.active).Select(item => item.type + "x" + item.stack)));
					check(!(bool)Call("PruneBranch", twig)!, "Repeated pruning produced a second drop");
					check(Main.item.Where(item => item.active && item.type == ItemID.Wood).Sum(item => item.stack) == nativeWood,
						"Repeated pruning altered the native drop count");
					branch.HasTile = true; branch.TileType = TileID.Trees; branch.TileFrameX = 44; branch.TileFrameY = 198;
					check(!(bool)Call("PruneBranch", twig)! && branch.HasTile, "Leafy branch was cut");
					branch.HasTile = false;
				}
				check(!(bool)Call("PruneBranch", new Point(40, 28))!, "Central stem was accepted as a side branch");
				check(!(bool)Call("PruneBranch", new Point(40, 45))!, "Ground was accepted as a branch");
				Point directedTwig = new(39, 30); Tile directedBranch = Main.tile[39, 30];
				directedBranch.HasTile = true; directedBranch.TileType = TileID.Trees;
				directedBranch.TileFrameX = 66; directedBranch.TileFrameY = 44;
				check(mate.PerformDirectOrder(CompanionTargetOrder.Forest, directedTwig, -1).Accepted,
					"Clicked dry branch did not start a real forestry assignment");
				Tick(360);
				check(!directedBranch.HasTile && IsTreeTile(40, 30), "Full forestry AI never pruned the directed branch or cut the stem");
				mate.Profile.Name = "AETHER"; mate.Profile.Energy = 100; mate.Profile.Mood = 100;
				mate.SetCommand(stay: false);
				foreach (CompanionInitiativeKind kind in Enum.GetValues<CompanionInitiativeKind>())
					mate.Profile.SetInitiativePolicy(kind, kind == CompanionInitiativeKind.Forestry
						? CompanionInitiativePolicy.Always : CompanionInitiativePolicy.Never);
				Tile automaticBranch = Main.tile[41, 32]; automaticBranch.HasTile = true; automaticBranch.TileType = TileID.Trees;
				automaticBranch.TileFrameX = 88; automaticBranch.TileFrameY = 110;
				Set("treeShakeCooldown", 10000); Set("autonomyDecisionTimer", 0); Tick(600);
				check(!automaticBranch.HasTile && IsTreeTile(40, 32), "Actual autonomy scheduler failed to carry out permitted dry-branch work");
				bool IsTreeTile(int x, int y) => Main.tile[x, y].HasTile && Main.tile[x, y].TileType == TileID.Trees;
			}
		}
		finally {
			Main.player[0] = oldPlayer; Main.npc = oldNpcs; Main.item = oldItems; Main.tile = oldMap;
			Main.maxTilesX = oldWidth; Main.maxTilesY = oldHeight; Main.netMode = oldMode; Main.myPlayer = oldMyPlayer;
			for (int i = 0; i < oldClients.Length; i++) Netplay.Clients[i] = oldClients[i];
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
			check((int)Get("socialReplyDelay") == 45, "Resident emote did not shorten the visit reply delay");
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

			void ResetNotice() {
				type.GetMethod("ClearTownNpcInteraction", flags)!.Invoke(companion, null);
				Set("townNpcInteractionCooldown", 0); Set("activeJob", CompanionJob.None);
				Set("guardianTarget", -1); Set("speechTimer", 0);
				companion.Profile.AutonomyEnabled = true;
				companion.Profile.Energy = 100; companion.Profile.Mood = 100;
				npc.ai[1] = 0;
				resident.Center = npc.Center + new Vector2(60, 0);
				Main.player[0].dead = false;
			}
			void Notice() => EmoteBubble.NewBubble(EmoteID.EmotionLove, new WorldUIAnchor(resident), 120);
			ResetNotice();
			int meetings = companion.Profile.Relationships[0].Meetings;
			int experience = companion.Profile.Experience;
			Notice();
			check((int)Get("socialNpcTarget") == 21 && (bool)Get("socialNpcGreeted")
				&& (int)Get("socialReplyDelay") == 45, "Nearby resident emote did not initiate a short exchange");
			check(companion.Profile.Relationships[0].Meetings == meetings + 1, "Noticed resident meeting was not recorded once");
			Notice();
			check(companion.Profile.Relationships[0].Meetings == meetings + 1, "Repeated bubbles farmed meetings within one exchange");
			Set("socialReplyDelay", 0);
			check(Visit() && companion.Profile.Relationships[0].LastEmote == EmoteID.EmotionLove,
				"Spontaneous resident exchange did not answer the native emote");
			check(companion.Profile.Experience == experience, "Spontaneous NPC replies farmed XP");
			type.GetMethod("ClearTownNpcInteraction", flags)!.Invoke(companion, null);
			Notice();
			check((int)Get("socialNpcTarget") == -1, "Resident emote bypassed the social cooldown");
			ResetNotice(); companion.Profile.AutonomyEnabled = false; Notice();
			check((int)Get("socialNpcTarget") == -1, "Resident emote enabled disabled autonomy");
			ResetNotice(); npc.ai[1] = 1; Notice();
			check((int)Get("socialNpcTarget") == -1, "Resident emote displaced Stay");
			ResetNotice(); Set("activeJob", CompanionJob.Mine); Notice();
			check((int)Get("socialNpcTarget") == -1, "Resident emote displaced assigned mining");
			ResetNotice(); Set("guardianTarget", 22); Notice();
			check((int)Get("socialNpcTarget") == -1, "Resident emote displaced defense");
			ResetNotice(); resident.Center = npc.Center + new Vector2(400, 0); Notice();
			check((int)Get("socialNpcTarget") == -1, "Distant resident emote triggered a visit");
			ResetNotice(); Main.player[0].dead = true; Notice();
			check((int)Get("socialNpcTarget") == -1, "Resident emote triggered a visit for a dead owner");
			ResetNotice(); Main.netMode = NetmodeID.MultiplayerClient; Notice();
			check((int)Get("socialNpcTarget") == -1, "Client independently started a resident conversation");
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

	private static void CheckRevisionBoundaries(Action<bool, string> check, Mod mod)
	{
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
		Player oldPlayer = Main.player[0];
		Item[] oldItems = Main.item;
		int oldMode = Main.netMode;
		try {
			Main.player[0] = new Player { whoAmI = 0, active = true };
			Main.item = new Item[oldItems.Length];
			for (int i = 0; i < Main.item.Length; i++) Main.item[i] = new Item();
			var npc = new NPC();
			npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
			var companion = (SoulboundCompanion)npc.ModNPC;
			Main.item[10] = new Item(ItemID.Gel, 7) { active = true, playerIndexTheItemIsReservedFor = 255 };
			MethodInfo collect = typeof(SoulboundCompanion).GetMethod("StoreLooseItem", flags)!;
			Main.netMode = NetmodeID.MultiplayerClient;
			check((int)collect.Invoke(companion, new object[] { Main.item[10] })! == 0
				&& Main.item[10].stack == 7 && companion.Profile.ItemCount(ItemID.Gel) == 0,
				"Client mutated a world-to-cargo transaction");
			Main.netMode = NetmodeID.SinglePlayer;
			companion.Profile.ClearCargo();
			companion.Profile.Store(new Item(ItemID.Gel, 50));
			MethodInfo blocked = typeof(SoulboundCompanion).GetMethod("HasNearbyBlockedLoot", flags)!;
			check((bool)blocked.Invoke(companion, null)!, "Full resource type was mistaken for an empty gathering area");
			companion.Profile.ClearCargo();
			check(!(bool)blocked.Invoke(companion, null)!, "Available cargo was reported as blocked");
			companion.Profile.Store(new Item(ItemID.Gel, 50));
			Main.item[10].playerIndexTheItemIsReservedFor = 1;
			check(!(bool)blocked.Invoke(companion, null)!, "Another player's drop was reported as blocked cargo");
			Main.item[10] = new Item(ItemID.Heart) { active = true, playerIndexTheItemIsReservedFor = 255 };
			check(!(bool)blocked.Invoke(companion, null)!, "Recovery pickup was reported as blocked cargo");

			foreach (int mode in new[] { NetmodeID.Server, NetmodeID.MultiplayerClient, NetmodeID.SinglePlayer }) {
				Main.netMode = mode;
				for (int type = -1; type <= 21; type++) {
					using var stream = new MemoryStream(type < 0 ? Array.Empty<byte>() : new[] { (byte)type });
					using var reader = new BinaryReader(stream);
					bool rejectedSafely = true;
					try { mod.HandlePacket(reader, 0); }
					catch (Exception) { rejectedSafely = false; }
					check(rejectedSafely, $"Truncated/wrong-direction packet escaped boundary: {mode}/{type}");
				}
			}
			Main.netMode = NetmodeID.Server;
			foreach (int sender in new[] { -1, Main.maxPlayers }) {
				using var stream = new MemoryStream(new byte[] { 0 });
				using var reader = new BinaryReader(stream);
				mod.HandlePacket(reader, sender);
				check(stream.Position == 0, "Invalid sender was parsed before rejection: " + sender);
			}
			foreach ((int mode, byte message) in new[] { (NetmodeID.Server, (byte)12), (NetmodeID.MultiplayerClient, (byte)7) }) {
				Main.netMode = mode;
				bool escaped = false;
				using var stream = new MemoryStream(new byte[] { message, 255, 255, 255, 255, 15, 0, 0, 0, 0, 0, 0 });
				using var reader = new BinaryReader(stream);
				try { mod.HandlePacket(reader, 0); }
				catch (IOException) { escaped = true; }
				check(!escaped, "Negative encoded string length escaped packet validation: " + mode);
			}
		}
		finally {
			Main.player[0] = oldPlayer;
			Main.item = oldItems;
			Main.netMode = oldMode;
		}

		const BindingFlags statics = BindingFlags.Static | BindingFlags.NonPublic;
		Type feedback = typeof(SoulmatesFeedbackSystem);
		FieldInfo active = feedback.GetField("sessionActive", statics)!;
		FieldInfo sequence = feedback.GetField("sequence", statics)!;
		var lines = (List<string>)feedback.GetField("PendingLines", statics)!.GetValue(null)!;
		var events = (Queue<string>)feedback.GetField("RecentEvents", statics)!.GetValue(null)!;
		var metrics = (Dictionary<string, long>)feedback.GetField("Metrics", statics)!.GetValue(null)!;
		bool oldActive = (bool)active.GetValue(null)!;
		long oldSequence = (long)sequence.GetValue(null)!;
		string[] oldLines = lines.ToArray(), oldEvents = events.ToArray();
		var oldMetrics = new Dictionary<string, long>(metrics);
		try {
			lines.Clear(); events.Clear(); metrics.Clear();
			active.SetValue(null, true);
			SoulmatesFeedbackSystem.Record("revision_privacy", ("tile_x", 123), ("tile_y", 456), ("item_type", ItemID.Gel));
			check(!lines[0].Contains("tile_x") && !lines[0].Contains("tile_y") && lines[0].Contains("item_type"),
				"Field notes retained exact coordinates or dropped useful item metadata");
			string note = new('n', 360);
			check(SoulmatesFeedbackSystem.RecordNote(note)
				&& File.ReadAllLines(Path.Combine(SoulmatesFeedbackSystem.FeedbackFolder, "notes-inbox.jsonl")).Last().Contains(note),
				"Durable mailbox note was silently truncated below its 360-character input limit");
		}
		finally {
			active.SetValue(null, oldActive); sequence.SetValue(null, oldSequence);
			lines.Clear(); lines.AddRange(oldLines);
			events.Clear(); foreach (string value in oldEvents) events.Enqueue(value);
			metrics.Clear(); foreach (var pair in oldMetrics) metrics[pair.Key] = pair.Value;
		}
	}

	private static void CheckMultiplayerCreation(Action<bool, string> check, Mod mod)
	{
		Player oldPlayer = Main.player[0], oldClientPlayer = Main.clientPlayer;
		int oldMode = Main.netMode, oldMyPlayer = Main.myPlayer;
		bool oldServerCharacters = Main.ServerSideCharacter;
		ISocket oldSocket = Netplay.Clients[0].Socket;
		ISocket oldClientSocket = Netplay.Connection.Socket;
		int oldState = Netplay.Clients[0].State;
		NPC oldNpc = Main.npc[20];
		try {
			var server = new Player { whoAmI = 0, active = true };
			server.inventory[0] = new Item(ItemID.CopperShortsword);
			server.inventory[5] = new Item(ModContent.ItemType<BlankSigil>(), 3);
			server.inventory[6] = new Item(ModContent.ItemType<Soulcore>());
			var client = new Player { whoAmI = 0, active = true };
			for (int slot = 0; slot < server.inventory.Length; slot++) client.inventory[slot] = server.inventory[slot].Clone();
			Main.player[0] = client;
			Main.myPlayer = 0;
			Main.netMode = NetmodeID.MultiplayerClient;
			Main.ServerSideCharacter = false;
			Main.clientPlayer = new Player();
			var native = new MessageBuffer { whoAmI = 256 };
			native.ResetReader();
			int length;
			using (var stream = new MemoryStream(native.readBuffer)) {
				using var writer = new BinaryWriter(stream);
				writer.Write((byte)MessageID.SyncEquipment); writer.Write((byte)0); writer.Write((short)1);
				ItemIO.Send(new Item(ModContent.ItemType<SoulboundSigil>()), writer, writeStack: true);
				length = (int)stream.Position;
			}
			native.GetData(0, length, out _);
			check(client.inventory[1].IsAir, "Native owner inventory guard unexpectedly changed; revisit the custom delivery path");

			Main.player[0] = server;
			Main.myPlayer = 255;
			Main.netMode = NetmodeID.Server;
			Netplay.Clients[0].Socket = new ProbeSocket();
			Netplay.Connection.Socket = new ProbeSocket();
			Netplay.Clients[0].State = 10;
			ProbeSocket.Sent.Clear(); ProbeSocket.Recording = true;
			Type messageType = mod.GetType().GetNestedType("MessageType", BindingFlags.NonPublic)!;
			using (var request = new MemoryStream()) {
				using (var writer = new BinaryWriter(request, Encoding.UTF8, true)) {
					writer.Write(Convert.ToByte(Enum.Parse(messageType, "CreateCompanionRequest")));
					writer.Write("Network QA");
					foreach (byte value in new byte[6]) writer.Write(value);
				}
				request.Position = 0;
				using var reader = new BinaryReader(request);
				mod.HandlePacket(reader, 0);
			}
			check(server.inventory[1].ModItem is SoulboundSigil && server.inventory[5].stack == 2,
				"Server creation did not produce one orb and consume one blank");
			check(ProbeSocket.Sent.Count > 0, "Creation transport fixture captured no actual packets");
			Main.player[0] = client;
			Main.myPlayer = 0;
			Main.netMode = NetmodeID.MultiplayerClient;
			client.inventory[2] = new Item(ItemID.StoneBlock, 9);
			RecoveryPacketSpy.Watching = true;
			RecoveryPacketSpy.Packets.Clear();
			using var header = mod.GetPacket();
			int payloadOffset = (int)header.BaseStream.Position;
			FlushInventoryPackets(mod, server, client, payloadOffset);
			ProbeSocket.Recording = false;
			check(client.inventory[1].ModItem is SoulboundSigil delivered
				&& server.inventory[1].ModItem is SoulboundSigil created && delivered.Profile.Id == created.Profile.Id,
				"Successful multiplayer creation never delivered the bound orb to its owning client");
			check(client.inventory[5].stack == 2, "Owning client retained the consumed Blank Sigil");
			check(client.inventory[0].type == ItemID.CopperShortsword && client.inventory[6].ModItem is Soulcore,
				"Creation replaced unrelated inventory or consumed Soulcore");
			check(client.inventory[2].type == ItemID.StoneBlock && client.inventory[2].stack == 9,
				"Creation overwrote an unrelated client pickup with an old server snapshot");
			check(Main.clientPlayer.inventory[1].ModItem is SoulboundSigil cached
				&& cached.Profile.Id == ((SoulboundSigil)client.inventory[1].ModItem).Profile.Id
				&& Main.clientPlayer.inventory[5].stack == 2,
				"Delivered creation did not update the native client inventory cache");
			byte receiptType = Convert.ToByte(Enum.Parse(messageType, "InventoryReceipt"));
			check(ProbeSocket.Sent.Count(packet => packet[2] == MessageID.ModPacket && packet[payloadOffset] == receiptType) == 1,
				"Owner did not acknowledge the atomic creation transaction exactly once");
			Item savedOrb = ItemIO.Load(ItemIO.Save(client.inventory[1]));
			check(savedOrb.ModItem is SoulboundSigil saved && saved.Profile.Id == ((SoulboundSigil)client.inventory[1].ModItem).Profile.Id,
				"Delivered orb did not survive native item save/load");

			if (!Enum.IsDefined(messageType, "OwnerInventoryUpdate")) return;
			byte updateType = Convert.ToByte(Enum.Parse(messageType, "OwnerInventoryUpdate"));
			var npc = new NPC();
			npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
			npc.whoAmI = 20; npc.ai[0] = 0; npc.active = true;
			Main.npc[20] = npc;
			var companion = (SoulboundCompanion)npc.ModNPC;
			companion.Profile = ((SoulboundSigil)server.inventory[1].ModItem).Profile.Clone();
			companion.Profile.Store(new Item(ItemID.CopperCoin, 123));
			companion.Profile.Store(new Item(ItemID.Gel, 4));
			((SoulboundSigil)server.inventory[1].ModItem).Profile = companion.Profile.Clone();
			server.inventory[1].favorited = true;
			server.inventory[50] = new Item(ItemID.CopperCoin, 5);
			for (int slot = 0; slot < server.inventory.Length; slot++) client.inventory[slot] = server.inventory[slot].Clone();
			Main.player[0] = server; Main.myPlayer = 255; Main.netMode = NetmodeID.Server;
			RecoveryPacketSpy.Watching = false;
			ProbeSocket.Sent.Clear(); ProbeSocket.Recording = true;
			companion.WithdrawStorageSlot(CompanionStorage.Wallet, 3, true);
			FlushInventoryPackets(mod, server, client, payloadOffset);
			ProbeSocket.Sent.Clear();
			Main.player[0] = server; Main.myPlayer = 255; Main.netMode = NetmodeID.Server;
			companion.WithdrawStorageSlot(CompanionStorage.Resources, 0, true);
			RecoveryPacketSpy.Watching = true;
			FlushInventoryPackets(mod, server, client, payloadOffset);
			ProbeSocket.Recording = false;
			int gel = 0, copper = 0;
			foreach (Item item in client.inventory) {
				if (item.type == ItemID.Gel) gel += item.stack;
				if (item.type == ItemID.CopperCoin) copper += item.stack;
			}
			check(gel == 1 && copper == 6, "Multiplayer resource/coin withdrawal never reached the owner or duplicated items");
			CompanionProfile deliveredProfile = ((SoulboundSigil)client.inventory[1].ModItem).Profile;
			check(deliveredProfile.ItemCount(ItemID.Gel) == 3 && deliveredProfile.WalletCopper == 122,
				"Changed-slot delivery failed to preserve same-type Sigil metadata after withdrawals");
			check(client.inventory[1].favorited && Main.clientPlayer.inventory[1].favorited,
				"Inventory delivery cleared the Sigil's favorite marker");

			foreach (string invalid in new[] { "truncated", "duplicate", "cursor", "too_many" }) {
				client.inventory[27] = new Item(ItemID.Gel, 7);
				using var stream = new MemoryStream();
				using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) {
					writer.Write(updateType);
					writer.Write(Guid.NewGuid().ToByteArray());
					writer.Write((byte)(invalid == "too_many" ? 59 : invalid == "cursor" ? 1 : 2));
					if (invalid == "cursor") writer.Write((byte)58);
					else if (invalid != "too_many") {
						writer.Write((byte)27); ItemIO.Send(new Item(ItemID.Gel, 7), writer, writeStack: true, writeFavorite: true);
						ItemIO.Send(new Item(ItemID.StoneBlock, 99), writer, writeStack: true, writeFavorite: true);
						if (invalid == "duplicate") {
							writer.Write((byte)27); ItemIO.Send(new Item(ItemID.Gel, 7), writer, writeStack: true, writeFavorite: true);
							ItemIO.Send(new Item(ItemID.Wood, 4), writer, writeStack: true, writeFavorite: true);
						}
					}
					writer.Write(false);
				}
				stream.Position = 0;
				using var reader = new BinaryReader(stream);
				mod.HandlePacket(reader, 256);
				check(client.inventory[27].type == ItemID.Gel && client.inventory[27].stack == 7,
					"Malformed inventory transaction partially applied: " + invalid);
			}
			using (var stream = new MemoryStream(new byte[] { updateType, 0 })) {
				Main.netMode = NetmodeID.Server;
				using var reader = new BinaryReader(stream);
				mod.HandlePacket(reader, 0);
				check(stream.Position == 1, "Server parsed an owner-only inventory response from a client");
			}
		}
		finally {
			ProbeSocket.Recording = false; ProbeSocket.Sent.Clear(); RecoveryPacketSpy.Watching = false;
			ResetInventoryTransactions();
			Netplay.Connection.Socket = oldClientSocket;
			Main.player[0] = oldPlayer; Main.clientPlayer = oldClientPlayer;
			Main.netMode = oldMode; Main.myPlayer = oldMyPlayer; Main.ServerSideCharacter = oldServerCharacters;
			Netplay.Clients[0].Socket = oldSocket; Netplay.Clients[0].State = oldState;
			Main.npc[20] = oldNpc;
		}
	}

	private static void CheckRecoveryAndLifecycle(Action<bool, string> check, Mod mod)
	{
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
		Player oldPlayer = Main.player[0];
		NPC[] oldNpcs = Main.npc;
		CombatText[] oldCombatText = Main.combatText;
		Item oldMouse = Main.mouseItem;
		int oldMode = Main.netMode, oldMyPlayer = Main.myPlayer;
		try {
			Main.combatText = new CombatText[oldCombatText.Length];
			for (int i = 0; i < Main.combatText.Length; i++) Main.combatText[i] = new CombatText { active = true };
			Main.myPlayer = 0;
			Main.netMode = NetmodeID.SinglePlayer;
			var owner = new Player { whoAmI = 0, active = true, statLife = 40, statLifeMax2 = 100, statManaMax2 = 100 };
			Main.player[0] = owner;
			var npc = new NPC();
			npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
			npc.whoAmI = 20;
			var companion = (SoulboundCompanion)npc.ModNPC;
			MethodInfo heal = typeof(SoulboundCompanion).GetMethod("HealOwner", flags)!;
			check((int)heal.Invoke(companion, new object[] { 7 })! == 7 && owner.statLife == 47,
				"Single-player healing did not apply exactly once");
			check((int)heal.Invoke(companion, new object[] { 999 })! == 53 && owner.statLife == 100,
				"Healing exceeded maximum life");
			check((int)heal.Invoke(companion, new object[] { -7 })! == 0 && owner.statLife == 100,
				"Negative healing changed life");
			Main.netMode = NetmodeID.MultiplayerClient;
			owner.statLife = 40;
			check((int)heal.Invoke(companion, new object[] { 7 })! == 0 && owner.statLife == 40,
				"Client independently repeated server healing");
			Main.netMode = NetmodeID.Server;
			RecoveryPacketSpy.Watching = true;
			RecoveryPacketSpy.Packets.Clear();
			check((int)heal.Invoke(companion, new object[] { 7 })! == 7 && owner.statLife == 47,
				"Server healing did not update its authoritative copy");
			check(RecoveryPacketSpy.Packets.Exists(packet => packet.Type == MessageID.SpiritHeal
				&& packet.Player == 0 && packet.Amount == 7), "Server healing did not send a real life delta to the owner");
			typeof(SoulboundCompanion).GetMethod("AddOwnerBuff", flags)!.Invoke(companion, new object[] { BuffID.PotionSickness, 3600 });
			check(RecoveryPacketSpy.Packets.Exists(packet => packet.Type == MessageID.AddPlayerBuff
				&& packet.Recipient == 0 && packet.Amount == BuffID.PotionSickness && packet.Ticks == 3600),
				"Carried potion sickness was not sent to its owner");
			RecoveryPacketSpy.Watching = false;

			Main.netMode = NetmodeID.MultiplayerClient;
			Main.player[0] = new Player { whoAmI = 0, active = true, statLife = 40, statLifeMax2 = 100, statManaMax2 = 100 };
			owner = Main.player[0];
			var native = new MessageBuffer { whoAmI = 256 };
			native.ResetReader();
			using (var stream = new MemoryStream(native.readBuffer)) {
				using var writer = new BinaryWriter(stream);
				writer.Write((byte)MessageID.SpiritHeal); writer.Write((byte)0); writer.Write((short)7);
			}
			native.GetData(0, 4, out _);
			check(owner.statLife == 47, "Native SpiritHeal did not heal the owning client");
			using (var stream = new MemoryStream(native.readBuffer)) {
				using var writer = new BinaryWriter(stream);
				writer.Write((byte)MessageID.AddPlayerBuff); writer.Write((byte)0); writer.Write((ushort)BuffID.PotionSickness); writer.Write(3600);
			}
			native.GetData(0, 8, out _);
			check(owner.HasBuff(BuffID.PotionSickness), "Native carried-potion buff was not applied on the owning client");
			MethodInfo threat = typeof(SoulboundCompanion).GetMethod("IsThreat", BindingFlags.Static | BindingFlags.NonPublic)!;
			var enemy = new NPC { active = true, type = NPCID.BlueSlime, lifeMax = 25, chaseable = true };
			check((bool)threat.Invoke(null, new object[] { enemy })!, "Normal hostile target was ignored");
			enemy.dontTakeDamage = true;
			check(!(bool)threat.Invoke(null, new object[] { enemy })!, "Invulnerable enemy monopolized defense");
			Type messageType = mod.GetType().GetNestedType("MessageType", BindingFlags.NonPublic)!;
			byte manaType = Convert.ToByte(Enum.Parse(messageType, "ManaRecovery"));
			foreach (int amount in new[] { 30, -1, int.MaxValue }) {
				int before = owner.statMana;
				using var stream = new MemoryStream();
				using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) { writer.Write(manaType); writer.Write(amount); }
				stream.Position = 0;
				using var reader = new BinaryReader(stream);
				mod.HandlePacket(reader, 256);
				check(owner.statMana == (amount > 0 ? Math.Min(100, before + (long)amount) : before),
					"Owner mana recovery failed or exceeded its cap: " + amount);
			}

			Main.netMode = NetmodeID.SinglePlayer;
			Main.npc = new NPC[oldNpcs.Length];
			for (int i = 0; i < Main.npc.Length; i++) Main.npc[i] = new NPC { whoAmI = i };
			Main.npc[20] = npc;
			npc.active = true;
			var sigilItem = new Item(ModContent.ItemType<SoulboundSigil>());
			var sigil = (SoulboundSigil)sigilItem.ModItem;
			sigil.Profile = companion.Profile.Clone();
			Main.mouseItem = sigilItem;
			check(ReferenceEquals(companion.FindBoundSigil(), sigil), "Rearranging a sigil on the cursor broke its binding");
			companion.Profile.Store(new Item(ItemID.Wood, 7));
			companion.SyncProfileToBoundSigil();
			check(sigil.Profile.ItemCount(ItemID.Wood) == 7, "Cursor-held sigil did not retain cargo");
			var prompt = ModContent.GetInstance<InitiativePromptSystem>();
			typeof(SoulboundCompanion).GetField("pendingAutonomyActivity", flags)!.SetValue(companion,
				Enum.ToObject(typeof(SoulboundCompanion).GetField("pendingAutonomyActivity", flags)!.FieldType, 1));
			Guid initiativeToken = Guid.NewGuid();
			typeof(SoulboundCompanion).GetField("initiativeId", flags)!.SetValue(companion, initiativeToken);
			typeof(SoulboundCompanion).GetField("pendingInitiativeTimer", flags)!.SetValue(companion, 3600);
			typeof(InitiativePromptSystem).GetField("replyWaitTicks", flags)!.SetValue(prompt, 600);
			typeof(InitiativePromptSystem).GetField("replyProfileId", flags)!.SetValue(prompt, companion.Profile.Id);
			typeof(InitiativePromptSystem).GetField("replyKind", flags)!.SetValue(prompt, CompanionInitiativeKind.Gathering);
			typeof(InitiativePromptSystem).GetField("replyInitiativeId", flags)!.SetValue(prompt, initiativeToken);
			MethodInfo awaiting = typeof(InitiativePromptSystem).GetMethod("IsAwaitingReply", flags)!;
			check((bool)awaiting.Invoke(prompt, new object[] { companion })!, "Answered prompt was allowed to reopen before server acknowledgement");
			typeof(SoulboundCompanion).GetField("pendingAutonomyActivity", flags)!.SetValue(companion,
				Enum.ToObject(typeof(SoulboundCompanion).GetField("pendingAutonomyActivity", flags)!.FieldType, 0));
			check(!(bool)awaiting.Invoke(prompt, new object[] { companion })!, "Acknowledged prompt retained its waiting state");
			prompt.OnWorldUnload();
			owner.inventory[0] = sigilItem;
			Main.mouseItem = new Item();
			companion.Profile.MiningInsight = CompanionProfile.MiningUnlockInsight - 1;
			companion.Profile.Experience = 0;
			companion.ObserveOwnerActivity(LearnedBehavior.Mining);
			check(companion.Profile.Experience == 5 && sigil.Profile.Experience == 5,
				"Perk-unlock XP was not persisted in the same transaction");
			owner.inventory[0] = new Item();
			companion.AI();
			check(!npc.active && companion.CurrentJob == CompanionJob.None,
				"Companion kept modifying the world without a carried sigil");
			for (int i = 0; i < Main.maxNPCs; i++) Main.npc[i] = new NPC { whoAmI = i, active = true, type = NPCID.BlueSlime };
			bool summoned = (bool)typeof(SoulboundSigil).GetMethod("SummonCompanion", flags)!.Invoke(sigil, new object[] { owner })!;
			check(!summoned && owner.GetModPlayer<SoulmatesPlayer>().ActiveCompanionWhoAmI == -1,
				"Full NPC pool reported a successful summon or left a stale active companion");
		}
		finally {
			RecoveryPacketSpy.Watching = false;
			RecoveryPacketSpy.Packets.Clear();
			Main.player[0] = oldPlayer; Main.npc = oldNpcs; Main.mouseItem = oldMouse;
			Main.combatText = oldCombatText;
			Main.netMode = oldMode; Main.myPlayer = oldMyPlayer;
		}
	}

	private static void CheckCombatAndSwitching(Action<bool, string> check, Mod mod)
	{
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
		Type type = typeof(SoulboundCompanion);
		Player oldPlayer = Main.player[0];
		NPC[] oldNpcs = Main.npc;
		Projectile[] oldProjectiles = Main.projectile;
		int[,] oldProjectileIdentity = Main.projectileIdentity;
		int oldMode = Main.netMode, oldMyPlayer = Main.myPlayer;
		try {
			Main.netMode = NetmodeID.SinglePlayer;
			Main.myPlayer = 0;
			var owner = new Player { whoAmI = 0, active = true, statLife = 100, statLifeMax2 = 100 };
			Main.player[0] = owner;
			owner.Center = new Vector2(1400f, 900f);
			Main.npc = new NPC[oldNpcs.Length];
			for (int i = 0; i < Main.npc.Length; i++) Main.npc[i] = new NPC { whoAmI = i };
			Main.projectile = new Projectile[oldProjectiles.Length];
			Main.projectileIdentity = (int[,])oldProjectileIdentity.Clone();
			for (int i = 0; i < Main.projectile.Length; i++) Main.projectile[i] = new Projectile { whoAmI = i };
			var npc = new NPC { whoAmI = 20 };
			npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
			npc.active = true;
			npc.Center = new Vector2(400f, 300f);
			Main.npc[20] = npc;
			var companion = (SoulboundCompanion)npc.ModNPC;
			companion.Profile.Talent = CompanionTalent.Guardian;
			var item = new Item(ModContent.ItemType<SoulboundSigil>());
			((SoulboundSigil)item.ModItem).Profile = companion.Profile.Clone();
			owner.inventory[0] = item;
			var enemy = new NPC { whoAmI = 21 };
			enemy.SetDefaults(NPCID.BlueSlime);
			enemy.active = true;
			enemy.target = 0;
			enemy.Center = npc.Center + new Vector2(90f, 0f);
			Main.npc[21] = enemy;
			companion.SetCommand(stay: true);
			Vector2 anchor = (Vector2)type.GetField("idleTarget", flags)!.GetValue(companion)!;
			bool Defend() => (bool)type.GetMethod("UpdateTalentBehavior", flags)!.Invoke(companion, null)!;
			int CountBolts() {
				int count = 0;
				foreach (Projectile p in Main.projectile)
					if (p.active && p.type == ModContent.ProjectileType<SoulBolt>()) count++;
				return count;
			}
			check(Defend() && companion.IsDefending, "Stay did not defend its own anchor away from the owner");
			check(CountBolts() == 1, "Companion did not spawn an actual SoulBolt");
			Projectile? bolt = Array.Find(Main.projectile, p => p.active && p.type == ModContent.ProjectileType<SoulBolt>());
			check(bolt is not null && Vector2.DistanceSquared(bolt.Center, npc.Center) <= 1f
				&& bolt.owner == 0 && bolt.ai[0] == 21 && bolt.ai[1] == 20 && bolt.npcProj,
				$"Attack origin, target, or companion ownership was incorrect: center={bolt?.Center}/{npc.Center}, owner={bolt?.owner}, ai={bolt?.ai[0]}/{bolt?.ai[1]}, npcProj={bolt?.npcProj}");
			check(bolt is not null && bolt.damage == 8 && bolt.friendly && !bolt.hostile,
				"Base guardian attack damage or friendly state changed");
			check((Vector2)type.GetField("idleTarget", flags)!.GetValue(companion)! == anchor,
				"Combat moved the saved Stay anchor");
			Defend();
			check(CountBolts() == 1, "Attack cooldown allowed a repeated shot immediately");
			type.GetField("attackCooldown", flags)!.SetValue(companion, 0);
			Defend();
			check(CountBolts() == 2, "Companion stopped attacking after its first shot");
			npc.velocity = new Vector2(-5f, 0f);
			type.GetMethod("UpdateFacing", flags)!.Invoke(companion, null);
			check(npc.spriteDirection == 1, "Companion faced away from a right-hand enemy while retreating");
			enemy.Center = anchor - new Vector2(90f, 0f);
			npc.velocity = new Vector2(5f, 0f);
			type.GetMethod("UpdateFacing", flags)!.Invoke(companion, null);
			check(npc.spriteDirection == -1, "Companion faced away from a left-hand enemy while retreating");
			Main.netMode = NetmodeID.MultiplayerClient;
			type.GetField("attackCooldown", flags)!.SetValue(companion, 0);
			check(Defend() && CountBolts() == 2, "Client duplicated an authoritative companion attack");
			Main.netMode = NetmodeID.Server;
			Main.myPlayer = 255;
			RecoveryPacketSpy.Watching = true;
			Defend();
			check(CountBolts() == 3 && Array.Exists(Main.projectile,
				p => p.active && p.type == ModContent.ProjectileType<SoulBolt>() && p.owner == 255 && p.ai[1] == 20),
				"Server did not create its own companion attack");
			Main.netMode = NetmodeID.SinglePlayer;
			Main.myPlayer = 0;
			RecoveryPacketSpy.Watching = false;
			enemy.dontTakeDamage = true;
			check(!Defend() && !companion.IsDefending, "Invulnerable target was not released");
			enemy.dontTakeDamage = false;
			enemy.Center = owner.Center;
			check(!Defend(), "Stay chased an enemy at the distant player");
			companion.SetCommand(stay: false);
			check(Defend(), "Follow did not switch defense back to the owner");
			Main.netMode = NetmodeID.Server;
			Main.myPlayer = 255;
			MethodInfo requestCompanion = mod.GetType().GetMethod("FindRequestCompanion", BindingFlags.Static | BindingFlags.NonPublic,
				null, new[] { typeof(Player) }, null)!;
			check(ReferenceEquals(requestCompanion.Invoke(null, new object[] { owner }), companion),
				"Valid carried companion was rejected by the request boundary");
			owner.inventory[0] = new Item();
			bool autonomyBefore = companion.Profile.AutonomyEnabled;
			byte quickAction = Convert.ToByte(Enum.Parse(mod.GetType().GetNestedType("MessageType", BindingFlags.NonPublic)!, "QuickActionRequest"));
			using (var stream = new MemoryStream()) {
				using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) {
					writer.Write(quickAction); writer.Write(companion.Profile.Id.ToByteArray());
					writer.Write((byte)CompanionQuickAction.ToggleAutonomy);
				}
				stream.Position = 0;
				using var reader = new BinaryReader(stream);
				mod.HandlePacket(reader, 0);
			}
			check(companion.Profile.AutonomyEnabled == autonomyBefore,
				"Queued request changed a companion after its Sigil left the inventory");
			owner.inventory[0] = item;
			owner.dead = true;
			check(requestCompanion.Invoke(null, new object[] { owner }) is null,
				"Dead owner retained access to mutating companion requests");
			owner.dead = false;
			Main.netMode = NetmodeID.SinglePlayer;
			Main.myPlayer = 0;

			var other = new NPC { whoAmI = 22 };
			other.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
			other.active = true;
			other.Center = npc.Center;
			Main.npc[22] = other;
			var duplicate = (SoulboundCompanion)other.ModNPC;
			int unrelated = Projectile.NewProjectile(other.GetSource_FromAI(), other.Center, Vector2.UnitX,
				ModContent.ProjectileType<SoulBolt>(), 8, 1f, 0, 21, 22);
			owner.GetModPlayer<SoulmatesPlayer>().ActiveCompanionWhoAmI = 20;
			check(!(bool)type.GetMethod("ClaimActiveSlot", flags)!.Invoke(duplicate, new object[] { owner })!
				&& !other.active && npc.active && owner.GetModPlayer<SoulmatesPlayer>().ActiveCompanionWhoAmI == 20,
				"Duplicate retirement removed the keeper or corrupted its active slot");
			check(unrelated < Main.maxProjectiles && !Main.projectile[unrelated].active && CountBolts() == 3,
				"Duplicate retirement removed another companion's projectiles");
			unrelated = Projectile.NewProjectile(other.GetSource_FromAI(), other.Center, Vector2.UnitX,
				ModContent.ProjectileType<SoulBolt>(), 8, 1f, 0, 21, 22);
			companion.Recall();
			string survivors = string.Join(", ", Array.ConvertAll(Array.FindAll(Main.projectile,
				p => p.active && p.type == ModContent.ProjectileType<SoulBolt>()),
				p => $"{p.whoAmI}:owner{p.owner}/companion{p.ai[1]}"));
			check(!npc.active && !companion.IsDefending && companion.CurrentJob == CompanionJob.None
				&& owner.GetModPlayer<SoulmatesPlayer>().ActiveCompanionWhoAmI == -1,
				"Recall retained combat, work, or the active companion slot");
			check(CountBolts() == 1 && unrelated < Main.maxProjectiles && Main.projectile[unrelated].active,
				$"Recall did not remove exactly its own SoulBolts: count={CountBolts()}, unrelated={unrelated}, active={Main.projectile[unrelated].active}, survivors={survivors}");
		}
		finally {
			RecoveryPacketSpy.Watching = false;
			RecoveryPacketSpy.Packets.Clear();
			Main.player[0] = oldPlayer; Main.npc = oldNpcs; Main.projectile = oldProjectiles;
			Main.projectileIdentity = oldProjectileIdentity;
			Main.netMode = oldMode; Main.myPlayer = oldMyPlayer;
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
		MethodInfo initialStorage = type.GetMethod("InitialStorage", BindingFlags.Static | BindingFlags.NonPublic)!;
		CompanionStorage Initial(CompanionProfile cargo) => (CompanionStorage)initialStorage.Invoke(null, new object[] { cargo })!;
		check(Initial(new CompanionProfile()) == CompanionStorage.Pack, "Empty cargo should open equipment");
		check(Initial(profile) == CompanionStorage.Resources, "Collected resources opened an empty equipment tab");
		var walletOnly = new CompanionProfile();
		walletOnly.Store(new Item(ItemID.CopperCoin, 7));
		check(Initial(walletOnly) == CompanionStorage.Wallet, "Coin-only cargo opened an empty equipment tab");
		walletOnly.Resources.Add(new Item(ItemID.Gel, 3));
		check(Initial(walletOnly) == CompanionStorage.Resources, "Resources lost initial-tab priority to coins");
		walletOnly.Pack.Add(new Item(ItemID.CopperPickaxe));
		check(Initial(walletOnly) == CompanionStorage.Pack, "Equipment lost initial-tab priority");
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

	private static void CheckContextMouseModes(Action<bool, string> check)
	{
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
		Player oldPlayer = Main.player[0];
		NPC oldNpc = Main.npc[0], oldBunny = Main.npc[1];
		Item oldItem = Main.item[10];
		Tilemap oldMap = Main.tile;
		int oldWidth = Main.maxTilesX, oldHeight = Main.maxTilesY, oldMode = Main.netMode, oldLocal = Main.myPlayer;
		bool oldMenu = Main.gameMenu;
		var wheel = ModContent.GetInstance<CompanionWheelSystem>();
		var oldView = Main.GameViewMatrix;
		FieldInfo inputWidth = typeof(PlayerInput).GetField("_originalScreenWidth", BindingFlags.Static | BindingFlags.NonPublic)!;
		FieldInfo inputHeight = typeof(PlayerInput).GetField("_originalScreenHeight", BindingFlags.Static | BindingFlags.NonPublic)!;
		object oldInputWidth = inputWidth.GetValue(null)!, oldInputHeight = inputHeight.GetValue(null)!;
		try {
			Main.maxTilesX = Main.maxTilesY = 100;
			Main.tile = (Tilemap)Activator.CreateInstance(typeof(Tilemap), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
				null, new object[] { (ushort)100, (ushort)100 }, null)!;
			Main.netMode = NetmodeID.SinglePlayer;
			Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(null!);
			Main.GameViewMatrix.SetViewportOverride(new Microsoft.Xna.Framework.Graphics.Viewport(0, 0, 1280, 720));
			Main.GameViewMatrix.Zoom = new Vector2(1.4f);
			inputWidth.SetValue(null, 1280); inputHeight.SetValue(null, 720);
			Main.myPlayer = 0;
			Main.gameMenu = false;
			Main.playerInventory = false;
			Main.mouseItem = new Item();
			Main.player[0] = new Player { whoAmI = 0, active = true };
			Player player = Main.player[0];
			player.SetTalkNPC(-1);
			player.Center = new Vector2(400, 400);
			player.inventory[0] = new Item(ItemID.CopperPickaxe);
			Main.npc[0] = new NPC();
			Main.npc[0].SetDefaults(ModContent.NPCType<SoulboundCompanion>());
			Main.npc[0].whoAmI = 0;
			Main.npc[0].ai[0] = 0;
			Main.npc[0].active = true;
			Main.npc[0].Center = new Vector2(400, 350);
			var companion = (SoulboundCompanion)Main.npc[0].ModNPC;
			companion.Profile.Name = "AETHER";
			Main.item[10] = new Item(ItemID.Gel, 7) { active = true, playerIndexTheItemIsReservedFor = 255 };
			Main.item[10].Center = new Vector2(480, 400);
			Tile ore = Main.tile[30, 28];
			ore.HasTile = true;
			ore.TileType = TileID.Copper;
			Tile protectedTile = Main.tile[32, 28];
			protectedTile.HasTile = true;
			protectedTile.TileType = TileID.Containers;
			Main.npc[1] = new NPC();
			Main.npc[1].SetDefaults(NPCID.Bunny);
			Main.npc[1].whoAmI = 1;
			Main.npc[1].active = true;
			Main.npc[1].Center = new Vector2(640, 400);
			Main.mouseLeft = Main.mouseRight = false;
			Main.dedServ = false;
			wheel.ExitMouseMode();
			check(wheel.MouseMode == SoulwheelMouseMode.Terraria && !wheel.IsOpen, "World did not start in native Terraria mode");
			wheel.OpenContext(companion, Main.item[10].Center, new Vector2(400, 300));
			check(wheel.IsOpen && wheel.MouseMode == SoulwheelMouseMode.Terraria, "Direct drop context required a hidden mode switch");
			wheel.ExitMouseMode();
			FieldInfo bound = typeof(CompanionWheelSystem).GetField("companion", flags)!;
			bound.SetValue(wheel, companion);
			wheel.SelectMouseMode(SoulwheelMouseMode.Companion);
			check(!wheel.IsOpen && wheel.MouseMode == SoulwheelMouseMode.Companion, "Soulmate mode did not release the cursor for a context target");
			Type wheelType = typeof(CompanionWheelSystem);
			MethodInfo branchPosition = wheelType.GetMethod("BranchPosition", flags)!;
			Type branchType = branchPosition.GetParameters()[0].ParameterType;
			var commandActions = (CompanionQuickAction[])wheelType.GetField("CommandActions", flags | BindingFlags.Static)!.GetValue(null)!;
			check(new[] { CompanionQuickAction.Pause, CompanionQuickAction.Resume, CompanionQuickAction.Abort }
				.All(action => commandActions.Count(value => value == action) == 1), "Work controls missing or duplicated in Commands wheel");
			for (int i = 0; i < commandActions.Length; i++) for (int j = i + 1; j < commandActions.Length; j++) {
				Vector2 a = (Vector2)branchPosition.Invoke(wheel, new object[] { Enum.Parse(branchType, "Commands"), i, commandActions.Length })!;
				Vector2 b = (Vector2)branchPosition.Invoke(wheel, new object[] { Enum.Parse(branchType, "Commands"), j, commandActions.Length })!;
				check(Vector2.Distance(a, b) > 42f, "Commands wheel work controls overlap their click targets");
			}
			List<string> Actions() => ((System.Collections.IEnumerable)typeof(CompanionWheelSystem)
				.GetField("contextActions", flags)!.GetValue(wheel)!).Cast<object>().Select(value => value.ToString()!).ToList();
			void Context(Vector2 position) => wheel.OpenContext(companion, position, new Vector2(400, 300));
			void Activate(string action) => typeof(CompanionWheelSystem).GetMethod("ActivateContextAction", flags)!
				.Invoke(wheel, new object[] { Actions().IndexOf(action) });
			Context(Main.item[10].Center);
			check(Actions().Contains("Gather") && Actions().Contains("Look") && !Actions().Contains("Mine"),
				"Drop context did not restrict actions to the actual item");
			object snapshot = typeof(CompanionWheelSystem).GetField("contextTarget", flags)!.GetValue(wheel)!;
			Main.item[10] = new Item(ItemID.Gel, 11) { active = true, playerIndexTheItemIsReservedFor = 255,
				position = Main.item[10].position };
			check(!(bool)snapshot.GetType().GetProperty("IsCurrent")!.GetValue(snapshot)!, "Reused same-type item slot retained a valid menu target");
			Activate("Gather");
			check(!wheel.IsOpen && companion.Profile.PackLoad == 0, "Stale context order changed cargo");
			check(wheel.MouseMode == SoulwheelMouseMode.Companion, "Completed menu reset deliberate mouse mode");
			Context(Main.item[10].Center);
			PlayerInput.MouseX = 900; PlayerInput.MouseY = 700;
			Activate("Gather");
			FieldInfo directed = typeof(SoulboundCompanion).GetField("jobTargetItem", flags)!;
			check((int)directed.GetValue(companion)! == 10, "Menu click retargeted gathering to the cursor's new location");
			Context(new Vector2(30 * 16 + 8, 28 * 16 + 8));
			check(Actions().Contains("Mine") && !Actions().Contains("Gather"), "Ore context failed to offer mining");
			ore.TileType = TileID.Dirt;
			Activate("Mine");
			check(ore.HasTile && ore.TileType == TileID.Dirt && !wheel.IsOpen, "Changed tile received stale mining order");
			Context(new Vector2(32 * 16 + 8, 28 * 16 + 8));
			check(!Actions().Contains("Mine"), "Protected furniture was offered as a mining target");
			Context(Main.npc[1].Center);
			check(Actions().Contains("Look") && !Actions().Contains("Mine") && !Actions().Contains("Gather"),
				"NPC context was treated as a tile or drop");
			typeof(SoulboundCompanion).GetField("activeJob", flags)!.SetValue(companion, CompanionJob.None);
			Context(Main.npc[1].Center);
			check(Actions().Contains("Company"), "Available critter lacked a direct invitation action");
			PlayerInput.MouseX = 0; PlayerInput.MouseY = 0;
			Activate("Company");
			check(ReferenceEquals(typeof(SoulboundCompanion).GetField("critterTarget", flags)!.GetValue(companion), Main.npc[1]),
				"Company wheel action retargeted away from the clicked critter");
			companion.PerformQuickAction(CompanionQuickAction.CritterWatch);
			Context(Main.npc[1].Center); Activate("Look");
			check(companion.Profile.CritterMode == CompanionCritterMode.Watch
				&& typeof(SoulboundCompanion).GetField("critterTarget", flags)!.GetValue(companion) is null,
				"NPC Look action changed critter behavior");
			wheel.SelectMouseMode(SoulwheelMouseMode.Player);
			Context(Main.npc[1].Center);
			check(Actions().Contains("Point") && Actions().Contains("Emotes") && !Actions().Contains("Tools"),
				"Me mode reused companion work commands");
			typeof(SoulboundCompanion).GetField("nativeEmoteReactionCooldown", flags)!.SetValue(companion, 0);
			Activate("Point");
			check(!wheel.IsOpen && wheel.MouseMode == SoulwheelMouseMode.Player
				&& (int)typeof(SoulboundCompanion).GetField("nativeEmoteReactionCooldown", flags)!.GetValue(companion)! == 45,
				"Me pointing did not emit a native player emote and reach the companion observer");
			Context(Main.npc[1].Center);
			object npcSnapshot = typeof(CompanionWheelSystem).GetField("contextTarget", flags)!.GetValue(wheel)!;
			NPC replacement = new NPC(); replacement.SetDefaults(NPCID.Bunny);
			replacement.whoAmI = 1; replacement.active = true; replacement.Center = Main.npc[1].Center;
			Main.npc[1] = replacement;
			check(!(bool)npcSnapshot.GetType().GetProperty("IsCurrent")!.GetValue(npcSnapshot)!,
				"Reused same-type NPC slot retained a valid context target");
			Context(new Vector2(700, 600));
			check(Actions().SequenceEqual(new[] { "Emotes" }), "Empty air offered fictitious player target");
			wheel.Close();
			SoulmatesPlayer controls = player.GetModPlayer<SoulmatesPlayer>();
			Main.mouseRight = true;
			player.mouseInterface = false;
			player.controlUseItem = player.controlUseTile = true;
			controls.SetControls();
			check(!player.controlUseItem && !player.controlUseTile, "Explicit mode leaked right-click to native items or tiles");
			Main.mouseRight = false;
			controls.SetControls();
			Main.playerInventory = true;
			Main.mouseRight = true;
			player.controlUseTile = true;
			controls.SetControls();
			check(player.controlUseTile, "Mouse mode swallowed inventory right-click");
			Main.playerInventory = false;
			Main.mouseRight = false;
			controls.SetControls();
			Main.mouseItem = new Item(ItemID.Gel);
			Main.mouseRight = true;
			player.controlUseTile = true;
			controls.SetControls();
			check(player.controlUseTile, "Mouse mode swallowed cursor-held item placement");
			Main.mouseItem = new Item();
			Main.mouseRight = false;
			controls.SetControls();
			player.SetTalkNPC(1);
			Main.mouseRight = true;
			player.controlUseTile = true;
			controls.SetControls();
			check(player.controlUseTile, "Mouse mode intercepted an existing NPC conversation");
			player.SetTalkNPC(-1);
			wheel.ExitMouseMode();
			Main.mouseRight = false;
			controls.SetControls();
			Main.mouseRight = true;
			player.controlUseItem = player.controlUseTile = true;
			controls.SetControls();
			check(player.controlUseItem && player.controlUseTile, "Exit failed to restore native controls");
			check(!ModContent.GetInstance<DirectOrderSystem>().IsActive, "Exit left a hidden targeting mode active");
			Main.mouseLeft = Main.mouseRight = false;
			player.mouseInterface = false;
			controls.SetControls();
			MethodInfo queue = typeof(SoulmatesPlayer).GetMethod("QueueSelfSoulwheel", flags)!;
			MethodInfo flush = typeof(SoulmatesPlayer).GetMethod("OpenQueuedSelfSoulwheel", flags)!;
			FieldInfo queued = typeof(SoulmatesPlayer).GetField("queuedSelfSoulwheel", flags)!;
			void Queue() => queue.Invoke(controls, new object[] { companion, wheel,
				ModContent.GetInstance<InitiativePromptSystem>(), ModContent.GetInstance<TalkModeSystem>() });
			void Flush() => flush.Invoke(controls, new object[] { companion });
			void Aim(Vector2 world) {
				Vector2 screen = Vector2.Transform(world - Main.screenPosition, Main.GameViewMatrix.TransformationMatrix);
				PlayerInput.MouseX = (int)screen.X; PlayerInput.MouseY = (int)screen.Y;
			}
			Aim(new Vector2(600, 500)); Queue(); Flush();
			check(!wheel.IsOpen && !(bool)queued.GetValue(controls)!, "Native empty-air right-click opened a mod wheel");
			Aim(new Vector2(32 * 16 + 8, 28 * 16 + 8)); Queue(); Flush();
			check(!wheel.IsOpen && !(bool)queued.GetValue(controls)!, "Native furniture right-click opened a mod wheel");
			foreach (Vector2 overlap in new[] { player.Center, companion.NPC.Center, Main.item[10].Center }) {
				Point tilePosition = overlap.ToTileCoordinates();
				Tile torch = Main.tile[tilePosition.X, tilePosition.Y];
				torch.HasTile = true; torch.TileType = TileID.Torches;
				Aim(new Vector2(tilePosition.X * 16 + 8, tilePosition.Y * 16 + 8)); Queue(); Flush();
				check(!wheel.IsOpen && !(bool)queued.GetValue(controls)!, "Placed torch lost native priority to a nearby wheel hitbox");
				torch.HasTile = false;
			}
			Aim(Main.item[10].Center); Queue(); Flush();
			check(wheel.IsOpen && Actions().Contains("Gather") && wheel.MouseMode == SoulwheelMouseMode.Terraria,
				"Default drop right-click did not open the actual target context");
			wheel.Close(); player.mouseInterface = false; controls.SetControls();
			Aim(Main.npc[1].Center); Queue(); Flush();
			check(wheel.IsOpen && Actions().Contains("Look"), "Default NPC right-click lost its direct context");
			wheel.Close(); player.mouseInterface = false; controls.SetControls();
			Aim(player.Center); Queue(); Flush();
			check(wheel.IsOpen && Convert.ToInt32(typeof(CompanionWheelSystem).GetField("context", flags)!.GetValue(wheel)) == 1,
				"Default self-click failed to route to the player wheel");
			wheel.Close(); player.mouseInterface = false; controls.SetControls();
			Aim(companion.NPC.Center); Queue(); Flush();
			check(wheel.IsOpen && Convert.ToInt32(typeof(CompanionWheelSystem).GetField("context", flags)!.GetValue(wheel)) == 0,
				"Default companion click failed to route to the companion wheel");
			wheel.Close(); player.mouseInterface = false; controls.SetControls();
			Aim(player.Center); Queue();
			player.altFunctionUse = 2; Flush();
			check(!wheel.IsOpen, "Default mode intercepted a native alternate item action");
			player.altFunctionUse = 0;
			Aim(player.Center); Queue();
			player.SetTalkNPC(1); Flush();
			check(!wheel.IsOpen, "Deferred wheel overrode a newly opened native conversation");
			player.SetTalkNPC(-1);
			bound.SetValue(wheel, companion);
			wheel.SelectMouseMode(SoulwheelMouseMode.Companion);
			wheel.Close(); player.mouseInterface = false; controls.SetControls();
			Aim(Main.item[10].Center); Queue();
			PlayerInput.MouseX = 0; PlayerInput.MouseY = 0;
			Flush();
			check(wheel.IsOpen && Actions().Contains("Gather"), "Deferred context lost the originally clicked drop");
			wheel.Close(); player.mouseInterface = false; controls.SetControls();
			Aim(Main.item[10].Center); Queue();
			Guid id = companion.Profile.Id;
			companion.Profile.Id = Guid.NewGuid(); Flush();
			check(!wheel.IsOpen, "Queued menu leaked across a companion identity switch");
			companion.Profile.Id = id;
			wheel.OpenPlayer(companion);
			MethodInfo modePosition = typeof(CompanionWheelSystem).GetMethod("MouseModePosition", flags)!;
			MethodInfo findHover = typeof(CompanionWheelSystem).GetMethod("FindHoveredNode", flags)!;
			MethodInfo activateHover = typeof(CompanionWheelSystem).GetMethod("ActivateHovered", flags)!;
			float questionScale = Main.UIScale; string questionCulture = Language.ActiveCulture.Name;
			object questionWidth = inputWidth.GetValue(null)!, questionHeight = inputHeight.GetValue(null)!;
			try {
				foreach (string culture in SupportedCultures)
				foreach (float scale in new[] { 0.85f, 1f, 1.5f })
				foreach ((int width, int height) in new[] { (640, 360), (800, 600), (1280, 720), (1920, 1080) }) {
					Main.UIScale = scale; LanguageManager.Instance.SetLanguage(culture);
					inputWidth.SetValue(null, width); inputHeight.SetValue(null, height);
					wheel.Open(companion);
					wheelType.GetMethod("ActivateRoot", flags)!.Invoke(wheel, new object[] { Enum.Parse(branchType, "Commands") });
					wheelType.GetMethod("ActivateBranch", flags)!.Invoke(wheel, new object[] { Array.IndexOf(commandActions, CompanionQuickAction.QuestionSettings) });
					foreach (int index in new[] { 0, 2, 1 }) {
						Vector2 position = (Vector2)wheelType.GetMethod("InitiativeRulePosition", flags)!.Invoke(wheel, new object[] { index })!;
						PlayerInput.MouseX = (int)(position.X * Main.UIScale); PlayerInput.MouseY = (int)(position.Y * Main.UIScale);
						findHover.Invoke(wheel, null);
						check(wheelType.GetField("hoverLayer", flags)!.GetValue(wheel)!.ToString() == "InitiativeRule"
							&& (int)wheelType.GetField("hoverIndex", flags)!.GetValue(wheel)! == index,
							"Personal-question symbol did not own its native click target: " + width + "/" + scale);
						check(!((string)wheelType.GetMethod("HoverLabel", flags)!.Invoke(wheel, null)!).StartsWith("Mods."),
							"Question symbol displayed a localization key instead of its label: " + culture);
						activateHover.Invoke(wheel, null);
						check(companion.Profile.QuestionCadence == (CompanionQuestionCadence)index,
							"Personal-question symbol did not change its actual setting");
					}
					wheelType.GetMethod("StepBack", flags)!.Invoke(wheel, null);
					check(!(bool)wheelType.GetField("questionSettingsMenu", flags)!.GetValue(wheel)!
						&& wheelType.GetField("branch", flags)!.GetValue(wheel)!.ToString() == "Commands",
						"Question Back left Commands or retained its child menu");
					wheelType.GetMethod("ActivateBranch", flags)!.Invoke(wheel, new object[] { Array.IndexOf(commandActions, CompanionQuickAction.QuestionSettings) });
					wheelType.GetMethod("ActivateBranch", flags)!.Invoke(wheel, new object[] { Array.IndexOf(commandActions, CompanionQuickAction.ResetInitiativeRules) });
					check((bool)wheelType.GetField("initiativeRulesMenu", flags)!.GetValue(wheel)!
						&& !(bool)wheelType.GetField("questionSettingsMenu", flags)!.GetValue(wheel)!,
						"Question settings leaked into the work-permission submenu");
					var rules = (CompanionQuickAction[])wheelType.GetField("InitiativeRules", flags | BindingFlags.Static)!.GetValue(null)!;
					check(rules.Length == Enum.GetValues<CompanionInitiativeKind>().Length + 1,
						"A registered ability has no independent rule button");
					foreach (CompanionInitiativeKind kind in Enum.GetValues<CompanionInitiativeKind>()) {
						int index = (int)kind;
						check(rules[index].ToString() == kind + "Policy", "Rule button controls the wrong ability: " + kind);
						Vector2 position = (Vector2)wheelType.GetMethod("InitiativeRulePosition", flags)!.Invoke(wheel, new object[] { index })!;
						PlayerInput.MouseX = (int)(position.X * Main.UIScale); PlayerInput.MouseY = (int)(position.Y * Main.UIScale);
						findHover.Invoke(wheel, null);
						check(wheelType.GetField("hoverLayer", flags)!.GetValue(wheel)!.ToString() == "InitiativeRule"
							&& (int)wheelType.GetField("hoverIndex", flags)!.GetValue(wheel)! == index,
							"Permission symbol selected another button: " + kind + "/" + width + "/" + scale);
						check(!((string)wheelType.GetMethod("HoverLabel", flags)!.Invoke(wheel, null)!).StartsWith("Mods."),
							"Permission label displayed a localization key: " + kind + "/" + culture);
						CompanionInitiativePolicy before = companion.Profile.GetInitiativePolicy(kind);
						activateHover.Invoke(wheel, null);
						check(companion.Profile.GetInitiativePolicy(kind) == (CompanionInitiativePolicy)(((int)before + 1) % 3),
							"Permission button did not cycle the real saved ability: " + kind);
					}
				}
			}
			finally {
				Main.UIScale = questionScale; LanguageManager.Instance.SetLanguage(questionCulture);
				inputWidth.SetValue(null, questionWidth); inputHeight.SetValue(null, questionHeight);
			}
			wheel.Close();
			check(!(bool)wheelType.GetField("questionSettingsMenu", flags)!.GetValue(wheel)!, "Question submenu survived closing the wheel");
			foreach (int index in new[] { 1, 2, 0 }) {
				wheel.OpenPlayer(companion);
				Vector2 position = (Vector2)modePosition.Invoke(wheel, new object[] { index })!;
				PlayerInput.MouseX = (int)(position.X * Main.UIScale);
				PlayerInput.MouseY = (int)(position.Y * Main.UIScale);
				findHover.Invoke(wheel, null);
				check(typeof(CompanionWheelSystem).GetField("hoverLayer", flags)!.GetValue(wheel)!.ToString() == "MouseMode"
					&& (int)typeof(CompanionWheelSystem).GetField("hoverIndex", flags)!.GetValue(wheel)! == index,
					"Visible mode button failed its hit test: " + index);
				activateHover.Invoke(wheel, null);
				check((int)wheel.MouseMode == index, "Mode button did not execute its selected mode: " + index);
				check(!wheel.IsOpen, "Mode selection trapped the cursor in its previous wheel: " + index);
			}
			check(!wheel.IsOpen, "Terraria selector did not exit the wheel");
			Main.mouseRight = true;
			wheel.OpenPlayer(companion);
			wheel.UpdateUI(new GameTime());
			check(wheel.IsOpen && wheel.MouseMode == SoulwheelMouseMode.Terraria, "Opening held right-click immediately switched mode");
			Main.mouseRight = false; wheel.UpdateUI(new GameTime());
			Main.mouseRight = true; wheel.UpdateUI(new GameTime());
			check(wheel.IsOpen && wheel.MouseMode == SoulwheelMouseMode.Companion, "Right-click did not cycle Me to Soulmate");
			wheel.UpdateUI(new GameTime());
			check(wheel.IsOpen && wheel.MouseMode == SoulwheelMouseMode.Companion, "Held right-click cycled repeatedly");
			Main.mouseRight = false; wheel.UpdateUI(new GameTime());
			Main.mouseRight = true; wheel.UpdateUI(new GameTime());
			check(!wheel.IsOpen && wheel.MouseMode == SoulwheelMouseMode.Terraria, "Right-click did not cycle Soulmate back to native use");
			Main.mouseRight = false;
			wheel.OpenPlayer(companion);
			wheel.SelectMouseMode(SoulwheelMouseMode.Player);
			Main.keyState = new Microsoft.Xna.Framework.Input.KeyboardState(Microsoft.Xna.Framework.Input.Keys.Escape);
			Main.oldKeyState = new Microsoft.Xna.Framework.Input.KeyboardState();
			wheel.UpdateUI(new GameTime());
			check(!wheel.IsOpen && wheel.MouseMode == SoulwheelMouseMode.Terraria, "Escape did not exit deliberate mouse mode");
			Main.keyState = new Microsoft.Xna.Framework.Input.KeyboardState();
			wheel.OnWorldUnload();
			check(wheel.MouseMode == SoulwheelMouseMode.Terraria, "Mouse mode persisted across world unload");
		}
		finally {
			wheel.ExitMouseMode();
			Main.dedServ = true;
			Main.player[0] = oldPlayer;
			Main.npc[0] = oldNpc; Main.npc[1] = oldBunny;
			Main.item[10] = oldItem;
			Main.tile = oldMap;
			Main.maxTilesX = oldWidth; Main.maxTilesY = oldHeight;
			Main.netMode = oldMode; Main.myPlayer = oldLocal;
			Main.gameMenu = oldMenu;
			Main.GameViewMatrix = oldView;
			inputWidth.SetValue(null, oldInputWidth); inputHeight.SetValue(null, oldInputHeight);
			Main.playerInventory = Main.mouseLeft = Main.mouseRight = false;
			Main.mouseItem = new Item();
		}
	}

	private static void CheckRockPaperScissors(Action<bool, string> check, Mod mod)
	{
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
		RpsOutcome[,] expected = {
			{ RpsOutcome.Draw, RpsOutcome.CompanionWin, RpsOutcome.PlayerWin },
			{ RpsOutcome.PlayerWin, RpsOutcome.Draw, RpsOutcome.CompanionWin },
			{ RpsOutcome.CompanionWin, RpsOutcome.PlayerWin, RpsOutcome.Draw }
		};
		foreach (RpsMove playerMove in Enum.GetValues<RpsMove>()) {
			check(CompanionRps.TryGetMove(CompanionRps.Emote(playerMove), out RpsMove decoded) && decoded == playerMove,
				"Native RPS symbol did not round-trip: " + playerMove);
			foreach (RpsMove companionMove in Enum.GetValues<RpsMove>())
				check(CompanionRps.Resolve(playerMove, companionMove) == expected[(int)playerMove, (int)companionMove],
					$"Incorrect RPS rule: {playerMove}/{companionMove}");
		}
		foreach (int symbol in new[] { -1, EmoteID.RPSWinRock, EmoteID.RPSWinPaper, EmoteID.RPSWinScissors, EmoteID.EmoteHappiness })
			check(!CompanionRps.TryGetMove(symbol, out _), "Victory/unrelated emote became a game choice");
		bool invalidRejected = false;
		try { CompanionRps.Resolve((RpsMove)255, RpsMove.Rock); }
		catch (ArgumentOutOfRangeException) { invalidRejected = true; }
		check(invalidRejected, "Invalid RPS move was resolved");

		Player oldPlayer = Main.player[0];
		NPC[] oldNpcs = Main.npc;
		int oldMode = Main.netMode, oldLocal = Main.myPlayer;
		int oldMouseX = PlayerInput.MouseX, oldMouseY = PlayerInput.MouseY;
		FieldInfo inputWidth = typeof(PlayerInput).GetField("_originalScreenWidth", BindingFlags.Static | BindingFlags.NonPublic)!;
		FieldInfo inputHeight = typeof(PlayerInput).GetField("_originalScreenHeight", BindingFlags.Static | BindingFlags.NonPublic)!;
		object oldInputWidth = inputWidth.GetValue(null)!, oldInputHeight = inputHeight.GetValue(null)!;
		var oldRandom = Main.rand;
		string oldCulture = Language.ActiveCulture.Name;
		RemoteClient[] oldClients = (RemoteClient[])Netplay.Clients.Clone();
		try {
			for (int i = 0; i < Netplay.Clients.Length; i++) Netplay.Clients[i] = new RemoteClient { Socket = new ProbeSocket() };
			Main.myPlayer = 0;
			Main.player[0] = new Player { whoAmI = 0, active = true, Center = new Vector2(400, 400) };
			Player owner = Main.player[0];
			Main.npc = new NPC[oldNpcs.Length];
			for (int i = 0; i < Main.npc.Length; i++) Main.npc[i] = new NPC { whoAmI = i };
			NPC npc = Main.npc[20]; npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
			npc.ai[0] = 0; npc.active = true; npc.Center = owner.Center;
			var companion = (SoulboundCompanion)npc.ModNPC;
			companion.Profile.Voice = CompanionVoice.Direct;
			owner.inventory[0] = new Item(ModContent.ItemType<SoulboundSigil>());
			var sigil = (SoulboundSigil)owner.inventory[0].ModItem;
			sigil.Profile = companion.Profile.Clone();
			Type type = typeof(SoulboundCompanion);
			void Set(string field, object value) => type.GetField(field, flags)!.SetValue(companion, value);
			string Speech() => (string)type.GetField("speechText", flags)!.GetValue(companion)!;
			void Ready() { Set("rpsCooldown", 0); Set("nativeEmoteReactionCooldown", 0); }
			int xp = companion.Profile.Experience, bond = companion.Profile.Bond, mood = companion.Profile.Mood,
				energy = companion.Profile.Energy, interactions = companion.Profile.Interactions;
			Set("activeJob", CompanionJob.Gather); companion.Profile.Routine = CompanionJob.Gather;
			foreach (int mode in new[] { NetmodeID.SinglePlayer, NetmodeID.Server }) {
				Main.netMode = mode;
				foreach (RpsMove move in Enum.GetValues<RpsMove>()) {
					for (int seed = 0; seed < 12; seed++) {
						Ready(); Main.rand = new Terraria.Utilities.UnifiedRandom(seed);
						RpsMove answer = (RpsMove)new Terraria.Utilities.UnifiedRandom(seed).Next(3);
						check(companion.PlayRockPaperScissors(move), "Authority refused a valid nearby round");
						string result = SoulmatesText.Get("Games.Rps.Round", SoulmatesText.Get($"Games.Rps.Moves.{move}"),
							SoulmatesText.Get($"Games.Rps.Moves.{answer}"), SoulmatesText.Get($"Games.Rps.Outcomes.{CompanionRps.Resolve(move, answer)}"));
						check(Speech() == result, "Companion choice depended on player move or result was wrong");
						check(!companion.PlayRockPaperScissors(move) && Speech() == result, "Duplicate round bypassed cooldown");
						companion.ReactToNativeEmote(CompanionRps.Emote(move));
						check(Speech() == result, "Duplicate native observer rerolled a result");
					}
				}
				Ready(); companion.ReactToNativeEmote(EmoteID.RPSRock);
				check((int)type.GetField("rpsCooldown", flags)!.GetValue(companion)! == 180,
					"Native RPS emote did not start a real round");
				for (int tick = 0; tick < 180; tick++) type.GetMethod("UpdateSocialState", flags)!.Invoke(companion, null);
				check(companion.PlayRockPaperScissors(RpsMove.Paper), "Round cooldown never expired");
			}
			check(companion.Profile.Experience == xp && companion.Profile.Bond == bond && companion.Profile.Mood == mood
				&& companion.Profile.Energy == energy && companion.Profile.Interactions == interactions
				&& companion.CurrentJob == CompanionJob.Gather && companion.Profile.Routine == CompanionJob.Gather
				&& sigil.Profile.Experience == xp, "RPS changed progression or interrupted an assignment");
			Main.netMode = NetmodeID.MultiplayerClient; Ready();
			check(!companion.PlayRockPaperScissors(RpsMove.Rock), "Client resolved a round independently");
			Main.netMode = NetmodeID.SinglePlayer;
			check(!companion.PlayRockPaperScissors((RpsMove)255), "Invalid choice was accepted");
			owner.dead = true;
			check(!companion.PlayRockPaperScissors(RpsMove.Rock), "Dead owner could play"); owner.dead = false;
			npc.Center += new Vector2(600, 0);
			check(!companion.PlayRockPaperScissors(RpsMove.Rock), "Distant companion played remotely"); npc.Center = owner.Center;
			owner.inventory[0] = new Item();
			check(!companion.PlayRockPaperScissors(RpsMove.Rock), "Unbound companion played");
			owner.inventory[0] = new Item(ModContent.ItemType<SoulboundSigil>());
			((SoulboundSigil)owner.inventory[0].ModItem).Profile = companion.Profile.Clone();
			Set("activeJob", CompanionJob.None); companion.Profile.Routine = CompanionJob.None;
			type.GetField("personalQuestionCooldown", flags)!.SetValue(companion, 0);
			bool asked = (bool)type.GetMethod("BeginChoiceQuestion", flags)!.Invoke(companion, new object[] { CompanionQuestion.Company })!;
			Guid question = companion.QuestionId;
			check(asked && !companion.PlayRockPaperScissors(RpsMove.Rock) && companion.QuestionId == question,
				"Game replaced an unanswered personal question");
			type.GetMethod("ClearChoiceQuestion", flags)!.Invoke(companion, null);
			Set("activeJob", CompanionJob.Gather); companion.Profile.Routine = CompanionJob.Gather;

			foreach (string culture in SupportedCultures) {
				LanguageManager.Instance.SetLanguage(culture);
				foreach (CompanionPersonality personality in Enum.GetValues<CompanionPersonality>()) {
					companion.Profile.Personality = personality;
					foreach (CompanionVoice voice in Enum.GetValues<CompanionVoice>()) {
						companion.Profile.Voice = voice;
						foreach (RpsMove playerMove in Enum.GetValues<RpsMove>()) {
							foreach (RpsMove companionMove in Enum.GetValues<RpsMove>()) {
								companion.ShowRpsResult(playerMove, companionMove);
								check(!Speech().Contains("Mods.Soulmates") && Speech().Length <= 180,
									"RPS text was untranslated or truncated: " + culture + personality + voice);
								if (voice != CompanionVoice.Direct)
									check(Speech().Contains(SoulmatesText.Get($"Games.Rps.Replies.{CompanionRps.Resolve(playerMove, companionMove)}.{personality}")),
										"RPS omitted personality reaction");
							}
						}
					}
				}
				companion.Profile.Voice = CompanionVoice.Soft; companion.Profile.Energy = 0;
				companion.ShowRpsResult(RpsMove.Rock, RpsMove.Paper);
				check(Speech().Contains(SoulmatesText.Get("Games.Rps.Quiet")), "Tired companion did not use a quiet game reply");
				companion.Profile.Energy = energy;
			}

			byte response = Convert.ToByte(Enum.Parse(mod.GetType().GetNestedType("MessageType", BindingFlags.NonPublic)!, "RpsResult"));
			void Packet(Guid id, byte playerMove, byte companionMove) {
				using var body = new MemoryStream();
				using (var writer = new BinaryWriter(body, Encoding.UTF8, true)) {
					writer.Write(response); writer.Write(id.ToByteArray()); writer.Write(playerMove); writer.Write(companionMove);
				}
				body.Position = 0; using var reader = new BinaryReader(body); mod.HandlePacket(reader, 0);
			}
			Main.netMode = NetmodeID.MultiplayerClient;
			companion.ShowSpeech("unchanged"); Packet(Guid.NewGuid(), 0, 1);
			check(Speech() == "unchanged", "Other companion's result was displayed");
			Packet(companion.Profile.Id, 255, 0);
			check(Speech() == "unchanged", "Invalid result move was displayed");
			Packet(companion.Profile.Id, 0, 2);
			check(Speech().Contains(SoulmatesText.Get("Games.Rps.Outcomes.PlayerWin")), "Client did not localize authoritative result");
			Main.netMode = NetmodeID.Server; companion.ShowSpeech("unchanged"); Packet(companion.Profile.Id, 0, 2);
			check(Speech() == "unchanged", "Server accepted a client-forged result");
			Ready();
			byte request = Convert.ToByte(Enum.Parse(mod.GetType().GetNestedType("MessageType", BindingFlags.NonPublic)!, "NativeEmoteRequest"));
			void NativeRequest() {
				using var body = new MemoryStream();
				using (var writer = new BinaryWriter(body, Encoding.UTF8, true)) {
					writer.Write(request); writer.Write(companion.Profile.Id.ToByteArray()); writer.Write(EmoteID.RPSScissors);
				}
				body.Position = 0; using var reader = new BinaryReader(body); mod.HandlePacket(reader, 0);
			}
			companion.ShowSpeech("unchanged"); NativeRequest();
			check(Speech() == "unchanged", "Retired custom emote channel still executed a server round");
			companion.ReactToNativeEmote(EmoteID.RPSScissors); string authoritativeResult = Speech();
			companion.ReactToNativeEmote(EmoteID.RPSScissors);
			check(authoritativeResult != "unchanged" && Speech() == authoritativeResult
				&& (int)type.GetField("rpsCooldown", flags)!.GetValue(companion)! == 180,
				"Native emote observer did not resolve exactly one server round");

			var wheel = new CompanionWheelSystem(); Type wheelType = typeof(CompanionWheelSystem);
			wheelType.GetField("companion", flags)!.SetValue(wheel, companion);
			wheelType.GetField("center", flags)!.SetValue(wheel, new Vector2(400, 300));
			Type rootType = wheelType.GetNestedType("RootBranch", BindingFlags.NonPublic)!;
			object gamesBranch = Enum.Parse(rootType, "Games"), bondBranch = Enum.Parse(rootType, "Bond");
			var companionRoots = (Array)wheelType.GetField("CompanionRoots", flags | BindingFlags.Static)!.GetValue(null)!;
			var playerRoots = (Array)wheelType.GetField("PlayerRoots", flags | BindingFlags.Static)!.GetValue(null)!;
			check(companionRoots.Cast<object>().Contains(gamesBranch) && !playerRoots.Cast<object>().Contains(gamesBranch),
				"Games was missing from the companion wheel or leaked into the player wheel");
			wheelType.GetMethod("ActivateRoot", flags)!.Invoke(wheel, new[] { bondBranch });
			check((int)wheelType.GetMethod("BranchNodeCount", flags)!.Invoke(wheel, new[] { bondBranch })! == 3
				&& (int)wheelType.GetMethod("BranchNodeCount", flags)!.Invoke(wheel, new[] { gamesBranch })! == 1,
				"Bond retained a hidden game or Games lost its real game entry");
			wheelType.GetMethod("ActivateRoot", flags)!.Invoke(wheel, new[] { gamesBranch });
			wheelType.GetMethod("ActivateBranch", flags)!.Invoke(wheel, new object[] { 0 });
			check((bool)wheelType.GetField("rpsMenu", flags)!.GetValue(wheel)!, "Games button did not expand choices");
			foreach ((int width, int height) in new[] { (800, 600), (1280, 720), (1920, 1080) }) {
				inputWidth.SetValue(null, width); inputHeight.SetValue(null, height);
				for (int index = 0; index < 3; index++) {
					Vector2 position = (Vector2)wheelType.GetMethod("RpsPosition", flags)!.Invoke(wheel, new object[] { index })!;
					PlayerInput.MouseX = (int)(position.X * Main.UIScale); PlayerInput.MouseY = (int)(position.Y * Main.UIScale);
					wheelType.GetMethod("FindHoveredNode", flags)!.Invoke(wheel, null);
					check(wheelType.GetField("hoverLayer", flags)!.GetValue(wheel)!.ToString() == "Rps"
						&& (int)wheelType.GetField("hoverIndex", flags)!.GetValue(wheel)! == index,
						"Game hit-test selected the wrong symbol: " + width + "/" + index);
				}
			}
			wheelType.GetMethod("StepBack", flags)!.Invoke(wheel, null);
			check(!(bool)wheelType.GetField("rpsMenu", flags)!.GetValue(wheel)!
				&& wheelType.GetField("branch", flags)!.GetValue(wheel)!.ToString() == "Games", "Back left Games instead of only the game");
			wheelType.GetMethod("StepBack", flags)!.Invoke(wheel, null);
			check(wheelType.GetField("branch", flags)!.GetValue(wheel) is null, "Back did not return from Games to the root wheel");
			wheelType.GetMethod("ActivateRoot", flags)!.Invoke(wheel, new[] { gamesBranch });
			wheelType.GetMethod("ActivateBranch", flags)!.Invoke(wheel, new object[] { 0 }); wheel.Close();
			check(!(bool)wheelType.GetField("rpsMenu", flags)!.GetValue(wheel)!, "Closing retained the game submenu");
		}
		finally {
			Main.player[0] = oldPlayer; Main.npc = oldNpcs; Main.netMode = oldMode; Main.myPlayer = oldLocal; Main.rand = oldRandom;
			PlayerInput.MouseX = oldMouseX; PlayerInput.MouseY = oldMouseY;
			inputWidth.SetValue(null, oldInputWidth); inputHeight.SetValue(null, oldInputHeight);
			LanguageManager.Instance.SetLanguage(oldCulture);
			for (int i = 0; i < Netplay.Clients.Length; i++) Netplay.Clients[i] = oldClients[i];
		}
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
				foreach (string fieldName in new[] { "drawingPlayerChat", "editSign", "editChest" }) {
					FieldInfo typing = typeof(Main).GetField(fieldName)!;
					typing.SetValue(null, true);
					check(!CanPrompt(), "Prompt interrupts text entry: " + fieldName);
					typing.SetValue(null, false);
				}
				PlayerInput.WritingText = true;
				check(!CanPrompt(), "Prompt interrupts a text-entry control");
				PlayerInput.WritingText = false;
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
