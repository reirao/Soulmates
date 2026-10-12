#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Common.Dialogue;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.Net;

namespace SoulmatesRegressionProbe;

public sealed partial class EngineChecks
{
	private static void CheckHomeIntegration(Action<bool, string> check)
	{
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
		Type type = typeof(SoulboundCompanion);
		Tilemap oldMap = Main.tile;
		Player oldPlayer = Main.player[0]; NPC[] oldNpcs = Main.npc; Item[] oldItems = Main.item;
		Chest[] oldChests = Main.chest; WorldFileData oldWorld = Main.ActiveWorldFileData;
		int oldWidth = Main.maxTilesX, oldHeight = Main.maxTilesY, oldMode = Main.netMode, oldLocal = Main.myPlayer;
		bool oldDedicated = Main.dedServ;
		RemoteClient[] oldClients = (RemoteClient[])Netplay.Clients.Clone();
		Guid world = new("b79fb9db-e948-49bf-b73c-430a3604297d");
		Guid otherWorld = new("385879da-d275-4ab5-8f68-f08ea380c5b6");
		try {
			Main.dedServ = true;
			for (int i = 0; i < Netplay.Clients.Length; i++) Netplay.Clients[i] = new RemoteClient { Socket = new ProbeSocket() };
			foreach (int mode in new[] { NetmodeID.SinglePlayer, NetmodeID.Server }) {
				void Verify(bool condition, string label) => check(condition, label + ": " + mode);
				Main.netMode = mode; Main.myPlayer = 255; Main.maxTilesX = Main.maxTilesY = 120;
				Main.tile = (Tilemap)Activator.CreateInstance(typeof(Tilemap), flags, null, new object[] { (ushort)120, (ushort)120 }, null)!;
				Main.ActiveWorldFileData = new WorldFileData("", false) { UniqueId = world };
				Main.npc = Enumerable.Range(0, oldNpcs.Length).Select(i => new NPC { whoAmI = i }).ToArray();
				Main.item = Enumerable.Range(0, oldItems.Length).Select(i => new Item { whoAmI = i }).ToArray();
				Main.chest = new Chest[oldChests.Length];
				Point indoors = new(40, 34), outdoors = new(90, 70);
				Point homeChest = new(34, 38), outdoorChest = new(58, 34), pillar = new(42, 36);
				var owner = new Player { whoAmI = 0, active = true, SpawnX = -1, SpawnY = -1,
					Center = indoors.ToWorldCoordinates(), statLife = 100, statLifeMax2 = 100 };
				Main.player[0] = owner;
				NPC npc = Main.npc[20]; npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
				npc.active = true; npc.ai[0] = 0; npc.Center = owner.Center; npc.homeless = true;
				var mate = (SoulboundCompanion)npc.ModNPC;
				mate.Profile.AutonomyEnabled = true; mate.Profile.WorkPaused = false; mate.Profile.Routine = CompanionJob.None;
				mate.Profile.QuestionCadence = CompanionQuestionCadence.Calm;
				mate.Profile.Mood = 55; mate.Profile.Energy = 70; mate.Profile.Bond = 11;
				mate.Profile.Store(new Item(ItemID.Wood, 13));
				mate.Profile.Store(new Item(ItemID.CopperShortsword));
				mate.Profile.Store(new Item(ItemID.GoldCoin, 7));
				owner.inventory[1] = new Item(ItemID.GoldCoin, 3);
				owner.inventory[2] = new Item(ItemID.Gel, 9);
				owner.inventory[0] = new Item(ModContent.ItemType<SoulboundSigil>());
				var sigil = (SoulboundSigil)owner.inventory[0].ModItem; sigil.Profile = mate.Profile.Clone();
				object? Get(string field) => type.GetField(field, flags)!.GetValue(mate);
				void Set(string field, object? value) => type.GetField(field, flags)!.SetValue(mate, value);
				object? Call(string method, params object?[] args) => type.GetMethod(method, flags)!.Invoke(mate, args);
				void Move(Point point) { owner.Center = point.ToWorldCoordinates(); npc.Center = owner.Center; }
				void AwarenessTicks(int ticks) { for (int i = 0; i < ticks; i++) Call("TickPlaceAwareness"); }
				bool BeginHome() {
					Set("personalQuestionCooldown", 0);
					return (bool)Call("BeginChoiceQuestion", CompanionQuestion.Home)!;
				}
				bool FindChest(string method, out Point target) {
					object?[] args = { Point.Zero };
					bool found = (bool)Call(method, args)!;
					target = (Point)args[0]!;
					return found;
				}
				bool CaptureCleared() => Get("questionPlaceAnchor") is null && (Guid)Get("questionPlaceWorld")! == Guid.Empty;
				(string Key, string Name, int Emote) Present() => ((string, string, int))Call("PresentObservation")!;

				for (int x = 30; x <= 54; x++) for (int y = 30; y <= 42; y++) {
					if (x == 30 || x == 54 || y == 30 || y == 42) PlaceTile(x, y, TileID.WoodBlock);
					else Main.tile[x, y].WallType = WallID.Wood;
				}
				PlaceTile(33, 40, TileID.Chairs);
				PlaceTile(pillar.X, pillar.Y, TileID.WoodBlock);
				foreach (Point chest in new[] { homeChest, outdoorChest }) {
					for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++) {
						PlaceTile(chest.X + x, chest.Y + y, TileID.Containers);
						Main.tile[chest.X + x, chest.Y + y].TileFrameX = (short)(x * 18);
						Main.tile[chest.X + x, chest.Y + y].TileFrameY = (short)(y * 18);
					}
				}
				Main.chest[0] = new Chest { x = homeChest.X, y = homeChest.Y };
				Main.chest[1] = new Chest { x = outdoorChest.X, y = outdoorChest.Y };
				CompanionPlaceObservation room = CompanionPlaces.Sample(owner, npc);
				Point anchor = room.Anchor;
				Verify(room.IsCandidate && !room.OwnerBedSpawn && !room.AssignedHousing && anchor == new Point(41, 36)
					&& Main.tile[pillar].HasTile && !Main.tile[indoors].HasTile,
					"Home fixture did not choose the deterministic open cell beside its center pillar");
				Verify(CompanionPlaces.SampleAt(owner, npc, new Point(50, 39)).Anchor == anchor
					&& CompanionPlaces.SampleAt(owner, npc, anchor) is { Valid: true, IsCandidate: true, Anchor: var sampledAnchor }
					&& sampledAnchor == anchor, "Pillar-room anchor changed with movement or could not sample itself");
				Verify(ReferenceEquals(mate.FindBoundSigil(), sigil), "Home fixture did not bind its owner and Sigil");
				byte[] tilesBefore = PlaceTileSnapshot();
				byte[] outcomeBefore = HomeOutcomeSnapshot(mate.Profile, owner);
				int roomCount = WorldGen.numRoomTiles;
				int[] roomX = (int[])WorldGen.roomX.Clone(), roomY = (int[])WorldGen.roomY.Clone();
				int[] roomBounds = { WorldGen.roomX1, WorldGen.roomX2, WorldGen.roomY1, WorldGen.roomY2 };
				FieldInfo[] roomFields = new[] { "roomDoor", "roomChair", "roomTable" }.Select(name =>
					typeof(WorldGen).GetField(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!).ToArray();
				object?[] roomNeeds = roomFields.Select(field => field.GetValue(null)).ToArray();

				AwarenessTicks(1800);
				Verify(mate.Profile.Places.Records.Count == 1 && mate.Profile.Places.FindNearby(world, anchor)?.Visits == 1
					&& sigil.Profile.Places.FindNearby(world, anchor)?.Visits == 1,
					"Stationary place awareness counted repeated scans as returns or failed to sync the Sigil");
				Verify(!(bool)Call("CanOfferHomeQuestion")! && !BeginHome(), "One room visit offered a home classification");
				for (byte visits = 2; visits <= 3; visits++) {
					Move(outdoors); AwarenessTicks(720);
					Move(indoors); AwarenessTicks(180);
					Verify(mate.Profile.Places.Records.Count == 1 && mate.Profile.Places.FindNearby(world, anchor)?.Visits == visits
						&& sigil.Profile.Places.FindNearby(world, anchor)?.Visits == visits,
						"Separated room return did not count exactly once or persist: " + visits);
					Verify((bool)Call("CanOfferHomeQuestion")! == (visits == 3), "Home offer ignored the three-return threshold: " + visits);
				}
				Verify(!(bool)Call("IsKnownHomeChest", homeChest)! && !(bool)Call("IsKnownHomeChest", outdoorChest)!,
					"Unclassified shelter or lone outdoor chest implied home ownership");

				Verify(BeginHome() && mate.PendingQuestion == CompanionQuestion.Home && mate.QuestionId != Guid.Empty,
					"Familiar pillar room could not begin its production Home question");
				Guid later = mate.QuestionId;
				Verify(!mate.RespondToQuestion(Guid.NewGuid(), CompanionAnswer.First) && mate.QuestionId == later,
					"Unrelated token consumed a captured Home question");
				Verify(mate.RespondToQuestion(later, CompanionAnswer.Later) && !mate.HasPendingQuestion && CaptureCleared()
					&& mate.Profile.Places.FindNearby(world, anchor)?.Role == CompanionPlaceRole.Shelter
					&& sigil.Profile.Places.FindNearby(world, anchor)?.Role == CompanionPlaceRole.Shelter,
					"Home Later classified the room or retained its captured question");
				Verify(!mate.RespondToQuestion(later, CompanionAnswer.First)
					&& outcomeBefore.SequenceEqual(HomeOutcomeSnapshot(mate.Profile, owner)),
					"Home Later or its replay changed rewards, money or cargo");

				Verify(BeginHome(), "Home demolition fixture could not capture a valid room");
				Guid demolished = mate.QuestionId;
				Tile roof = Main.tile[42, 30]; roof.HasTile = false;
				byte[] demolishedTiles = PlaceTileSnapshot();
				Verify(!mate.RespondToQuestion(demolished, CompanionAnswer.First) && !mate.HasPendingQuestion && CaptureCleared()
					&& mate.Profile.Places.FindNearby(world, anchor)?.Role == CompanionPlaceRole.Shelter
					&& sigil.Profile.Places.FindNearby(world, anchor)?.Role == CompanionPlaceRole.Shelter,
					"Captured Home question accepted a demolished room");
				Verify(demolishedTiles.SequenceEqual(PlaceTileSnapshot()) && !mate.RespondToQuestion(demolished, CompanionAnswer.First),
					"Rejected demolished Home answer edited terrain or accepted a replay");
				PlaceTile(42, 30, TileID.WoodBlock);
				Verify(BeginHome(), "Home world-change fixture could not capture a valid room");
				Guid movedWorld = mate.QuestionId;
				Main.ActiveWorldFileData = new WorldFileData("", false) { UniqueId = otherWorld };
				Verify(!mate.RespondToQuestion(movedWorld, CompanionAnswer.First) && !mate.HasPendingQuestion && CaptureCleared()
					&& mate.Profile.Places.FindNearby(world, anchor)?.Role == CompanionPlaceRole.Shelter
					&& mate.Profile.Places.FindNearby(otherWorld, anchor) is null,
					"Captured Home question assigned ownership after a world change");
				Verify(!mate.RespondToQuestion(movedWorld, CompanionAnswer.First)
					&& outcomeBefore.SequenceEqual(HomeOutcomeSnapshot(mate.Profile, owner)),
					"Stale Home answer or its replay changed rewards, money or cargo");
				Main.ActiveWorldFileData = new WorldFileData("", false) { UniqueId = world };

				Verify(BeginHome(), "Restored pillar room could not begin a fresh Home question");
				Guid confirmed = mate.QuestionId;
				Verify(mate.RespondToQuestion(confirmed, CompanionAnswer.First) && !mate.HasPendingQuestion && CaptureCleared()
					&& mate.Profile.Places.FindNearby(world, anchor) is { Role: CompanionPlaceRole.SharedHome, Visits: 3 }
					&& sigil.Profile.Places.FindNearby(world, anchor) is { Role: CompanionPlaceRole.SharedHome, Visits: 3 },
					"Production First answer could not confirm and persist the captured pillar room as SharedHome");
				Verify(!mate.RespondToQuestion(confirmed, CompanionAnswer.First)
					&& outcomeBefore.SequenceEqual(HomeOutcomeSnapshot(mate.Profile, owner))
					&& outcomeBefore.SequenceEqual(HomeOutcomeSnapshot(sigil.Profile, owner)),
					"Confirmed Home or token replay granted rewards, moved money or lost cargo");
				Verify(!(bool)Call("CanOfferHomeQuestion")! && !BeginHome() && (string?)Call("PlaceSpeechKey") == "Places.SharedHome",
					"Confirmed SharedHome was offered again or did not supply its home observation");

				var cache = (Dictionary<Point, bool>)Get("homeChestCache")!;
				cache.Clear();
				Chest savedHome = Main.chest[0], savedOutdoor = Main.chest[1];
				Main.chest[0] = Main.chest[1] = null!;
				for (int i = 0; i < 40; i++) Main.chest[i + 2] = new Chest { x = 80 + i % 20, y = 20 + i / 20 };
				Verify(!FindChest("FindAutonomousChest", out _) && cache.Count == 0,
					"Far world chests populated the home cache or became autonomous treasure targets");
				Main.chest[0] = savedHome; Main.chest[1] = savedOutdoor;
				Verify(FindChest("FindAutonomousChest", out Point automatic) && automatic == outdoorChest
					&& cache.TryGetValue(homeChest, out bool excluded) && excluded
					&& cache.TryGetValue(outdoorChest, out bool outsideHome) && !outsideHome && cache.Count == 2,
					"Autonomous treasure did not exclude remembered home while retaining a lone outdoor chest");
				Set("jobOrigin", owner.Center);
				Verify(FindChest("FindNearestChest", out Point explicitChest) && explicitChest == homeChest
					&& CompanionPlaces.SampleAt(owner, npc, homeChest).IsCandidate,
					"Explicit treasure search could not find a chest in a valid remembered home");
				cache.Clear();
				Point[] cachedPoints = Enumerable.Range(0, 32).Select(i => new Point(80 + i, 70)).ToArray();
				foreach (Point point in cachedPoints) Call("IsKnownHomeChest", point);
				Verify(cache.Count == 32 && (bool)Call("IsKnownHomeChest", homeChest)! && cache.Count == 32
					&& cachedPoints.Skip(1).All(cache.ContainsKey), "Full home chest cache cleared all entries instead of evicting one");

				NPC friendly = Main.npc[21], hostile = Main.npc[22];
				foreach (int friendlyType in new[] { NPCID.Bunny, NPCID.Guide }) {
					friendly.SetDefaults(friendlyType); friendly.active = true; friendly.homeless = true;
					friendly.Center = new Point(40, 35).ToWorldCoordinates();
					hostile.SetDefaults(NPCID.Zombie); hostile.active = true; hostile.friendly = false;
					hostile.Center = new Point(51, 34).ToWorldCoordinates();
					Verify(owner.statLife == owner.statLifeMax2 && friendly.life > 0 && (friendly.friendly || friendly.townNPC)
						&& hostile.life > 0 && hostile.damage > 0
						&& Vector2.DistanceSquared(npc.Center, friendly.Center) < Vector2.DistanceSquared(npc.Center, hostile.Center)
						&& Vector2.DistanceSquared(npc.Center, hostile.Center) < 260f * 260f
						&& Collision.CanHitLine(npc.Center, 1, 1, friendly.Center, 1, 1)
						&& Collision.CanHitLine(npc.Center, 1, 1, hostile.Center, 1, 1),
						"Home threat fixture did not have a nearer friendly and visible farther hostile: " + friendlyType);
					var present = Present();
					Verify(present.Key == "Stories.Present.Threat" && present.Name == hostile.FullName,
						"Nearer friendly or SharedHome hid a farther visible damaging hostile: " + friendlyType);
					hostile.active = false;
					Verify(Present().Key == "Places.SharedHome", "Home observation did not return after the hostile was removed: " + friendlyType);
				}
				friendly.active = false;

				Verify(cache.TryGetValue(homeChest, out bool cachedHome) && cachedHome,
					"World-change fixture did not prime its confirmed home chest cache");
				Main.ActiveWorldFileData = new WorldFileData("", false) { UniqueId = otherWorld };
				Verify(!(bool)Call("IsKnownHomeChest", homeChest)!
					&& mate.Profile.Places.FindNearby(otherWorld, anchor) is null
					&& (string?)Call("PlaceSpeechKey") == "Places.Shelter",
					"World change reused cached home ownership before the next awareness scan");
				AwarenessTicks(180);
				Verify(mate.Profile.Places.FindNearby(world, anchor)?.IsHome == true
					&& mate.Profile.Places.FindNearby(otherWorld, anchor) is { Role: CompanionPlaceRole.Shelter, Visits: 1 }
					&& sigil.Profile.Places.FindNearby(otherWorld, anchor) is { Role: CompanionPlaceRole.Shelter, Visits: 1 }
					&& !(bool)Call("IsKnownHomeChest", homeChest)! && (string?)Call("PlaceSpeechKey") == "Places.Shelter",
					"Same room coordinates in another world inherited confirmed ownership or a cached home chest");
				Verify(FindChest("FindAutonomousChest", out Point otherChest) && otherChest == homeChest,
					"Another world's unowned chest retained the original world's autonomous exclusion");
				Verify(tilesBefore.SequenceEqual(PlaceTileSnapshot()) && WorldGen.numRoomTiles == roomCount
					&& roomX.SequenceEqual(WorldGen.roomX) && roomY.SequenceEqual(WorldGen.roomY)
					&& roomBounds.SequenceEqual(new[] { WorldGen.roomX1, WorldGen.roomX2, WorldGen.roomY1, WorldGen.roomY2 })
					&& roomNeeds.SequenceEqual(roomFields.Select(field => field.GetValue(null))),
					"Home integration queries mutated tiles or native room-check state");
				Verify(outcomeBefore.SequenceEqual(HomeOutcomeSnapshot(mate.Profile, owner))
					&& outcomeBefore.SequenceEqual(HomeOutcomeSnapshot(sigil.Profile, owner)),
					"Home awareness, classification or observations changed rewards, money or cargo");
				for (int x = 60; x <= 91; x++) for (int y = 80; y <= 87; y++) {
					if (x == 60 || x == 91 || y == 80 || y == 87) PlaceTile(x, y, TileID.WoodBlock);
					else Main.tile[x, y].WallType = WallID.Wood;
				}
				PlaceTile(62, 86, TileID.Chairs);
				Point edgeChest = new(62, 84);
				PlaceTile(edgeChest.X, edgeChest.Y, TileID.Containers);
				Move(new Point(75, 83));
				Point wideAnchor = CompanionPlaces.Sample(owner, npc).Anchor;
				Verify(CompanionPlaces.Sample(owner, npc).IsCandidate
					&& CompanionPlaces.SampleAt(owner, npc, edgeChest) is { IsCandidate: true, Anchor: var edgeAnchor }
					&& edgeAnchor == wideAnchor, "Wide room changes classification when sampled from an edge chest");
				for (int i = 0; i < 3; i++) mate.Profile.Places.Observe(otherWorld, wideAnchor);
				foreach (CompanionAnswer answer in new[] { CompanionAnswer.First, CompanionAnswer.Second, CompanionAnswer.Third }) {
					mate.Profile.Places.RememberRole(otherWorld, wideAnchor, CompanionPlaceRole.Shelter);
					Verify(BeginHome(), "Wide shelter could not ask for classification: " + answer);
					Guid token = mate.QuestionId;
					CompanionPlaceRole role = answer == CompanionAnswer.First ? CompanionPlaceRole.SharedHome
						: answer == CompanionAnswer.Second ? CompanionPlaceRole.CompanionHome : CompanionPlaceRole.NotHome;
					Verify(mate.RespondToQuestion(token, answer) && !mate.HasPendingQuestion && CaptureCleared()
						&& mate.Profile.Places.FindNearby(otherWorld, wideAnchor)?.Role == role
						&& sigil.Profile.Places.FindNearby(otherWorld, wideAnchor)?.Role == role,
						"Home answer did not persist the intended role: " + answer);
					Verify((bool)Call("IsKnownHomeChest", edgeChest)! == (role != CompanionPlaceRole.NotHome),
						"Edge chest did not respect the captured room's home role: " + answer);
					Verify(!mate.RespondToQuestion(token, answer)
						&& outcomeBefore.SequenceEqual(HomeOutcomeSnapshot(mate.Profile, owner)),
						"Home classification or its replay changed rewards, money or cargo: " + answer);
				}

				// Explicit configuration must not depend on idle chatter or three previous visits.
				foreach (bool autonomy in new[] { false, true })
				foreach (bool paused in new[] { false, true })
				foreach (bool stay in new[] { false, true })
				foreach (CompanionQuestionCadence cadence in Enum.GetValues<CompanionQuestionCadence>()) {
					mate.SetCommand(stay);
					mate.Profile.Places = new CompanionPlaces();
					mate.Profile.AutonomyEnabled = autonomy; mate.Profile.WorkPaused = paused; mate.Profile.QuestionCadence = cadence;
					Set("personalQuestionCooldown", 18000);
					CompanionConversationResult requested = mate.PerformQuickAction(CompanionQuickAction.DiscussHome);
					Verify(requested.Accepted && requested.Reply.Length == 0 && mate.PendingQuestion == CompanionQuestion.Home
						&& mate.QuestionId != Guid.Empty && mate.Profile.Places.Records.Count == 0,
						"Explicit Home request depends on autonomy, pause, Stay, cadence or familiarity");
					Guid token = mate.QuestionId;
					Call("UpdateChoiceConversation");
					Verify(mate.HasPendingQuestion && mate.QuestionId == token && mate.QuestionTicks > 0,
						"Explicit Home question vanished on its first normal update");
					Verify(mate.RespondToQuestion(token, CompanionAnswer.Second)
						&& mate.Profile.Places.FindNearby(otherWorld, wideAnchor)?.Role == CompanionPlaceRole.CompanionHome
						&& sigil.Profile.Places.FindNearby(otherWorld, wideAnchor)?.Role == CompanionPlaceRole.CompanionHome,
						"Explicit Home answer did not persist for a quiet, resting or non-autonomous companion");
					Verify(mate.Profile.AutonomyEnabled == autonomy && mate.Profile.WorkPaused == paused
						&& mate.Profile.QuestionCadence == cadence && (mate.NPC.ai[1] == 1) == stay
						&& outcomeBefore.SequenceEqual(HomeOutcomeSnapshot(mate.Profile, owner)),
						"Explicit Home configuration changed work, personality settings, rewards or cargo");
					Verify(mate.PerformQuickAction(CompanionQuickAction.DiscussHome).Accepted
						&& mate.RespondToQuestion(mate.QuestionId, CompanionAnswer.Third)
						&& mate.Profile.Places.FindNearby(otherWorld, wideAnchor)?.Role == CompanionPlaceRole.NotHome,
						"Home wheel cannot reclassify a previously confirmed home");
				}
				mate.SetCommand(false); mate.Profile.AutonomyEnabled = true; mate.Profile.WorkPaused = false;
				mate.Profile.QuestionCadence = CompanionQuestionCadence.Calm;
				Set("activeJob", CompanionJob.Gather);
				Verify(!mate.PerformQuickAction(CompanionQuickAction.DiscussHome).Accepted && !mate.HasPendingQuestion
					&& mate.CurrentJob == CompanionJob.Gather, "Home request interrupted a running assignment");
				Set("activeJob", CompanionJob.None);
				Move(outdoors);
				Verify(!mate.PerformQuickAction(CompanionQuickAction.DiscussHome).Accepted && !mate.HasPendingQuestion,
					"Home request outside a shelter produced an ownership question");
				Move(new Point(75, 83));
				NPC resident = Main.npc[21]; resident.SetDefaults(NPCID.Guide); resident.active = true;
				resident.homeless = false; resident.homeTileX = 74; resident.homeTileY = 86;
				Verify(!mate.PerformQuickAction(CompanionQuickAction.DiscussHome).Accepted && !mate.HasPendingQuestion,
					"Manual Home request offered to claim assigned resident housing");
				resident.active = false;
				Verify(mate.PerformQuickAction(CompanionQuickAction.DiscussHome).Accepted, "Final Home request failed");
				Guid pinnedToken = mate.QuestionId;
				string pinnedSpeech = (string)Get("speechText")!;
				Verify(mate.PerformQuickAction(CompanionQuickAction.DiscussHome).Accepted && mate.QuestionId == pinnedToken
					&& (string)Get("speechText")! == pinnedSpeech, "Repeated Home click replaced an unanswered question");
				Verify(mate.RespondToQuestion(pinnedToken, CompanionAnswer.Later), "Final Home Later failed");
				if (mode == NetmodeID.Server) {
					using var stream = new MemoryStream();
					using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) {
						writer.Write(mate.Profile.Id.ToByteArray()); writer.Write((byte)CompanionQuickAction.DiscussHome);
					}
					stream.Position = 0;
					using var reader = new BinaryReader(stream);
					Mod mod = ModContent.GetInstance<global::Soulmates.Soulmates>();
					mod.GetType().GetMethod("HandleQuickActionRequest", flags)!.Invoke(mod, new object[] { reader, 0 });
					Verify(mate.PendingQuestion == CompanionQuestion.Home && mate.QuestionId != Guid.Empty,
						"Server quick-action routing lost the Home request");
					mate.RespondToQuestion(mate.QuestionId, CompanionAnswer.Later);
				}
				Main.netMode = NetmodeID.MultiplayerClient;
				Verify(!mate.PerformQuickAction(CompanionQuickAction.DiscussHome).Accepted && !mate.HasPendingQuestion,
					"Client Home action independently changed authoritative state");
				Main.netMode = mode;
			}
		}
		finally {
			Main.tile = oldMap; Main.player[0] = oldPlayer; Main.npc = oldNpcs; Main.item = oldItems; Main.chest = oldChests;
			Main.ActiveWorldFileData = oldWorld; Main.maxTilesX = oldWidth; Main.maxTilesY = oldHeight;
			Main.netMode = oldMode; Main.myPlayer = oldLocal; Main.dedServ = oldDedicated;
			Array.Copy(oldClients, Netplay.Clients, oldClients.Length);
		}
	}

	private static byte[] HomeOutcomeSnapshot(CompanionProfile profile, Player owner)
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter(stream);
		writer.Write(profile.Experience.ToString()); writer.Write(profile.Bond); writer.Write(profile.Mood); writer.Write(profile.Energy);
		writer.Write(profile.JobsCompleted); writer.Write(profile.Interactions);
		writer.Write(profile.WalletCopper.ToString(System.Globalization.CultureInfo.InvariantCulture));
		foreach (CompanionStorage storage in new[] { CompanionStorage.Pack, CompanionStorage.Resources, CompanionStorage.Pets }) {
			var items = profile.StorageItems(storage);
			writer.Write(items.Count);
			foreach (Item item in items) ItemIO.Send(item, writer, writeStack: true, writeFavorite: true);
		}
		for (int slot = 1; slot < owner.inventory.Length; slot++)
			ItemIO.Send(owner.inventory[slot], writer, writeStack: true, writeFavorite: true);
		return stream.ToArray();
	}
}
