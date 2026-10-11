#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader.IO;

namespace SoulmatesRegressionProbe;

public sealed partial class EngineChecks
{
	private static void CheckPlaceBeta(Action<bool, string> check)
	{
		Tilemap oldMap = Main.tile;
		NPC[] oldNpcs = Main.npc;
		WorldFileData oldWorld = Main.ActiveWorldFileData;
		int oldWidth = Main.maxTilesX, oldHeight = Main.maxTilesY;
		int oldSpawnX = Main.spawnTileX, oldSpawnY = Main.spawnTileY;
		Guid world = new("bd90bcee-8f4e-4839-b096-d2d5464fb7f1");
		Guid otherWorld = new("a1513b91-2fa9-4898-a2ea-9de730ad9cc1");
		try {
			Main.maxTilesX = 120; Main.maxTilesY = 100;
			Main.tile = (Tilemap)Activator.CreateInstance(typeof(Tilemap),
				BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null,
				new object[] { (ushort)120, (ushort)100 }, null)!;
			Main.ActiveWorldFileData = new WorldFileData("", false) { UniqueId = world };
			Main.npc = Enumerable.Range(0, oldNpcs.Length).Select(i => new NPC { whoAmI = i }).ToArray();
			var owner = new Player { active = true, SpawnX = -1, SpawnY = -1 };
			var mate = new NPC { active = true, homeless = true, Center = new Point(15, 35).ToWorldCoordinates() };
			Main.spawnTileX = 15; Main.spawnTileY = 35;
			for (int x = 2; x < 25; x++) PlaceTile(x, 40, TileID.Grass);
			CompanionPlaceObservation outside = CompanionPlaces.Sample(owner, mate);
			check(outside.Valid && !outside.IsCandidate && !outside.OwnerBedSpawn && !outside.AssignedHousing,
				"Outdoor world spawn was treated as a home candidate");
			PlaceTile(15, 35, TileID.Containers);
			check(!CompanionPlaces.Sample(owner, mate).IsCandidate, "A lone chest implied home or shelter");
			owner.SpawnX = 15; owner.SpawnY = 36;
			check(!CompanionPlaces.Sample(owner, mate).OwnerBedSpawn, "Spawn fields accepted a chest as a selected bed");

			void Room(int left, int right) {
				for (int x = left; x <= right; x++) for (int y = 30; y <= 40; y++) {
					if (x == left || x == right || y == 30 || y == 40) PlaceTile(x, y, TileID.WoodBlock);
					else { Tile tile = Main.tile[x, y]; tile.WallType = WallID.Wood; }
				}
			}
			Room(30, 42); Room(65, 77);
			Point first = new(36, 35), second = new(71, 35);
			mate.Center = first.ToWorldCoordinates();
			check(!CompanionPlaces.Sample(owner, mate).IsCandidate, "An empty enclosure implied a furnished shelter");
			PlaceTile(33, 38, TileID.Chairs);
			PlaceTile(68, 38, TileID.Tables);
			byte[] tilesBefore = PlaceTileSnapshot();
			int roomCount = WorldGen.numRoomTiles;
			int[] roomX = (int[])WorldGen.roomX.Clone(), roomY = (int[])WorldGen.roomY.Clone();
			int[] roomBounds = [WorldGen.roomX1, WorldGen.roomX2, WorldGen.roomY1, WorldGen.roomY2];
			FieldInfo[] roomFields = new[] { "roomDoor", "roomChair", "roomTable" }.Select(name =>
				typeof(WorldGen).GetField(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!).ToArray();
			object?[] roomNeeds = roomFields.Select(field => field.GetValue(null)).ToArray();
			CompanionPlaceObservation shelter = CompanionPlaces.Sample(owner, mate);
			check(shelter.IsCandidate && shelter.Enclosed && shelter.HasManufacturedWalls
				&& shelter.HasFloor && shelter.HasFurniture && !shelter.OwnerBedSpawn && !shelter.AssignedHousing,
				"Protected walls, floor and furniture did not yield only a shelter candidate");
			check(CompanionPlaces.SampleAt(owner, mate, new Point(32, 32)).Anchor == shelter.Anchor
				&& CompanionPlaces.SampleAt(owner, mate, new Point(40, 38)).Anchor == shelter.Anchor,
				"Movement within an enclosed room changed its canonical anchor");
			check(tilesBefore.SequenceEqual(PlaceTileSnapshot()) && WorldGen.numRoomTiles == roomCount
				&& roomX.SequenceEqual(WorldGen.roomX) && roomY.SequenceEqual(WorldGen.roomY)
				&& roomBounds.SequenceEqual(new[] { WorldGen.roomX1, WorldGen.roomX2, WorldGen.roomY1, WorldGen.roomY2 })
				&& roomNeeds.SequenceEqual(roomFields.Select(field => field.GetValue(null))),
				"Environmental queries mutated tiles or native global room-check state");
			Tile roof = Main.tile[36, 30]; roof.HasTile = false;
			check(!CompanionPlaces.Sample(owner, mate).IsCandidate, "An open roof still counted as an enclosed shelter");
			PlaceTile(36, 30, TileID.WoodBlock);
			for (int x = 31; x < 42; x++) for (int y = 31; y < 40; y++) Main.tile[x, y].WallType = WallID.DirtUnsafe;
			check(!CompanionPlaces.Sample(owner, mate).IsCandidate, "Unsafe natural walls implied a manufactured shelter");
			for (int x = 31; x < 42; x++) for (int y = 31; y < 40; y++) Main.tile[x, y].WallType = WallID.Wood;

			for (int x = 34; x <= 37; x++) for (int y = 38; y <= 39; y++) {
				PlaceTile(x, y, TileID.Beds);
				Main.tile[x, y].TileFrameX = (short)((x - 34) * 18);
				Main.tile[x, y].TileFrameY = (short)((y - 38) * 18);
			}
			owner.SpawnX = 36; owner.SpawnY = 40;
			check(CompanionPlaces.Sample(owner, mate).OwnerBedSpawn
				&& !CompanionPlaces.SampleAt(owner, mate, second).OwnerBedSpawn,
				"Selected native bed was missing or leaked into a different room");
			Tile selectedBed = Main.tile[36, 39]; selectedBed.HasTile = false;
			check(!CompanionPlaces.Sample(owner, mate).OwnerBedSpawn, "Removed bed left stale selected-spawn evidence");
			PlaceTile(36, 39, TileID.Beds); selectedBed.IsActuated = true;
			check(!CompanionPlaces.Sample(owner, mate).OwnerBedSpawn, "An actuated bed counted as a selected spawn");
			selectedBed.IsActuated = false;
			PlaceTile(35, 37, TileID.Stone);
			check(!CompanionPlaces.Sample(owner, mate).OwnerBedSpawn, "Obstructed native bed counted as a usable spawn");
			Tile obstruction = Main.tile[35, 37]; obstruction.HasTile = false;

			NPC resident = Main.npc[10];
			resident.active = resident.townNPC = true; resident.type = NPCID.Guide;
			resident.homeless = false; resident.homeTileX = 71; resident.homeTileY = 39;
			resident.Center = first.ToWorldCoordinates();
			mate.homeless = false; mate.homeTileX = 36; mate.homeTileY = 39;
			CompanionPlaceObservation playerRoom = CompanionPlaces.Sample(owner, mate);
			CompanionPlaceObservation residentRoom = CompanionPlaces.SampleAt(owner, mate, second);
			check(playerRoom.OwnerBedSpawn && !playerRoom.AssignedHousing
				&& residentRoom.AssignedHousing && !residentRoom.OwnerBedSpawn,
				"Player bed, companion fields or resident position were confused with native assigned housing");
			Tile residentRoof = Main.tile[71, 30]; residentRoof.HasTile = false;
			check(!CompanionPlaces.SampleAt(owner, mate, second).AssignedHousing,
				"Destroyed enclosure retained stale NPC housing authority");
			PlaceTile(71, 30, TileID.WoodBlock);
			resident.homeless = true;
			check(!CompanionPlaces.SampleAt(owner, mate, second).AssignedHousing, "Homeless NPC retained housing authority");
			resident.homeless = false; resident.active = false;
			check(!CompanionPlaces.SampleAt(owner, mate, second).AssignedHousing, "Inactive resident implied assigned housing");
			resident.active = true; resident.townNPC = false;
			check(!CompanionPlaces.SampleAt(owner, mate, second).AssignedHousing, "Non-town NPC implied assigned housing");
			resident.townNPC = true; resident.homeTileX = 120;
			check(!CompanionPlaces.SampleAt(owner, mate, second).AssignedHousing, "Out-of-world housing anchor was accepted");
			check(!CompanionPlaces.SampleAt(owner, mate, Point.Zero).Valid, "Invalid sample location was accepted");
			Main.maxTilesX = 200;
			check(!CompanionPlaces.SampleAt(owner, mate, new Point(150, 35)).Valid, "Sample ignored allocated native tile bounds");
			Main.maxTilesX = 120;
			mate.Center = new Vector2(float.NaN, 10);
			check(!CompanionPlaces.Sample(owner, mate).Valid, "Non-finite NPC position was sampled");
			CheckPlaceMemory(check, world, otherWorld, shelter.Anchor);
		}
		finally {
			Main.tile = oldMap; Main.npc = oldNpcs; Main.ActiveWorldFileData = oldWorld;
			Main.maxTilesX = oldWidth; Main.maxTilesY = oldHeight;
			Main.spawnTileX = oldSpawnX; Main.spawnTileY = oldSpawnY;
		}
	}

	private static void CheckPlaceMemory(Action<bool, string> check, Guid world, Guid otherWorld, Point anchor)
	{
		var places = new CompanionPlaces();
		places.Observe(world, anchor); places.Observe(world, anchor + new Point(1, 0));
		CompanionPlace familiar = places.Observe(world, anchor)!;
		check(places.Records.Count == 1 && familiar.Visits == 3 && familiar.IsFamiliar
			&& familiar.Anchor == anchor && familiar.Role == CompanionPlaceRole.Shelter && !familiar.IsHome,
			"Repeated room returns duplicated anchors or inferred ownership");
		places.RememberRole(world, anchor, CompanionPlaceRole.SharedHome);
		Point adjacent = anchor + new Point(6, 0);
		places.Observe(world, adjacent);
		check(places.Records.Count == 2 && !places.FindNearby(world, adjacent)!.IsHome,
			"Confirmed ownership transferred to an adjacent room");
		check(places.FindNearby(otherWorld, anchor) is null, "Place memory leaked across worlds");
		places.Observe(otherWorld, anchor);
		check(!places.FindNearby(otherWorld, anchor)!.IsHome, "Same coordinates inherited ownership in another world");
		for (int i = 0; i < 10000; i++) places.Observe(world, anchor);
		check(places.FindNearby(world, anchor)!.Visits == CompanionPlaces.MaximumVisits
			&& places.FindNearby(world, anchor)!.Confidence == 80, "Place familiarity grew without bound");
		var clone = places.Clone();
		clone.RememberRole(world, anchor, CompanionPlaceRole.NotHome);
		check(places.FindNearby(world, anchor)!.IsHome && !clone.FindNearby(world, anchor)!.IsHome,
			"A cloned Sigil shared mutable place memory");
		var saved = CompanionPlaces.Load(places.Save());
		check(saved.Records.SequenceEqual(places.Records), "Place save/load lost visits, roles or world scope");
		using (var stream = new MemoryStream()) {
			using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) places.Write(writer);
			stream.Position = 0;
			using var reader = new BinaryReader(stream);
			check(CompanionPlaces.Read(reader).Records.SequenceEqual(places.Records) && stream.Position == stream.Length,
				"Place wire round-trip lost data or alignment");
		}
		check(places.Observe(Guid.Empty, anchor) is null && places.Observe(world, Point.Zero) is null
			&& places.Observe(world, new Point(120, 30)) is null
			&& places.Observe(otherWorld, new Point(int.MaxValue, 30)) is null
			&& places.RememberRole(world, anchor, (CompanionPlaceRole)255) is null,
			"Place mutation accepted invalid world, coordinates or role");
		var bounded = new CompanionPlaces();
		for (int i = 0; i < 20; i++) bounded.Observe(otherWorld, new Point(10 + i * 4, 50));
		check(bounded.Records.Count == 8 && bounded.FindNearby(otherWorld, new Point(10, 50)) is null,
			"Place memory exceeded eight records or failed to evict oldest returns");
		var duplicate = new TagCompound { ["places"] = new List<TagCompound> {
			PlaceEntry(world, anchor, CompanionPlaceRole.CompanionHome, 3),
			PlaceEntry(world, anchor + new Point(1, 0), CompanionPlaceRole.Shelter, 2) } };
		var coalesced = CompanionPlaces.Load(duplicate);
		check(coalesced.Records.Count == 1 && coalesced.Records[0].Anchor == anchor
			&& coalesced.Records[0].Visits == 3 && coalesced.Records[0].Role == CompanionPlaceRole.CompanionHome,
			"Save duplicates inflated visits, moved anchors or erased confirmed ownership");
		var bad = PlaceEntry(world, anchor, CompanionPlaceRole.Shelter, 1); bad["x"] = "bad";
		check(CompanionPlaces.Load(new TagCompound { ["places"] = new List<TagCompound> { bad } }).Records.Count == 0
			&& CompanionPlaces.Load(new TagCompound()).Records.Count == 0
			&& CompanionPlaces.Load(new TagCompound { ["places"] = "bad" }).Records.Count == 0,
			"Malformed or old saves invented a remembered place");
		foreach ((Guid id, Point point, CompanionPlaceRole role, int visits) in new[] {
			(Guid.Empty, anchor, CompanionPlaceRole.Shelter, 1), (world, Point.Zero, CompanionPlaceRole.Shelter, 1),
			(world, new Point(120, 30), CompanionPlaceRole.Shelter, 1), (world, anchor, (CompanionPlaceRole)4, 1),
			(world, anchor, CompanionPlaceRole.Shelter, 0), (world, anchor, CompanionPlaceRole.Shelter, 33) }) {
			check(CompanionPlaces.Load(new TagCompound { ["places"] = new List<TagCompound> { PlaceEntry(id, point, role, visits) } })
				.Records.Count == 0, "Invalid saved place was accepted");
			RejectPlaceWire(check, PlaceWire(id, point, role, visits), "Invalid network place was accepted");
		}
		RejectPlaceWire(check, new byte[] { 1, 9 }, "Oversized network place count was accepted");
		RejectPlaceWire(check, new byte[] { 2, 0 }, "Unknown network place version was accepted");
		RejectPlaceWire(check, new byte[] { 1, 1, 0 }, "Truncated network world was accepted");
		byte[] wire = PlaceWire(world, anchor, CompanionPlaceRole.Shelter, 1);
		RejectPlaceWire(check, wire[..^1], "Truncated network place was accepted");
		byte[] repeated = new byte[2 + (wire.Length - 2) * 2]; repeated[0] = 1; repeated[1] = 2;
		Array.Copy(wire, 2, repeated, 2, wire.Length - 2); Array.Copy(wire, 2, repeated, wire.Length, wire.Length - 2);
		RejectPlaceWire(check, repeated, "Duplicate network places were accepted");
	}

	private static TagCompound PlaceEntry(Guid world, Point anchor, CompanionPlaceRole role, int visits) => new() {
		["world"] = world.ToString(), ["x"] = anchor.X, ["y"] = anchor.Y, ["role"] = (int)role, ["visits"] = visits
	};
	private static byte[] PlaceWire(Guid world, Point anchor, CompanionPlaceRole role, int visits)
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter(stream);
		writer.Write((byte)1); writer.Write((byte)1); writer.Write(world.ToByteArray());
		writer.Write((ushort)anchor.X); writer.Write((ushort)anchor.Y); writer.Write((byte)role); writer.Write((byte)visits);
		return stream.ToArray();
	}
	private static void RejectPlaceWire(Action<bool, string> check, byte[] bytes, string label)
	{
		try { using var reader = new BinaryReader(new MemoryStream(bytes)); CompanionPlaces.Read(reader); check(false, label); }
		catch (IOException) { check(true, label); }
		catch (InvalidDataException) { check(true, label); }
	}
	private static void PlaceTile(int x, int y, ushort type)
	{
		Tile tile = Main.tile[x, y]; tile.HasTile = true; tile.TileType = type;
	}
	private static byte[] PlaceTileSnapshot()
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter(stream);
		for (int x = 0; x < Main.tile.Width; x++) for (int y = 0; y < Main.tile.Height; y++) {
			Tile tile = Main.tile[x, y];
			writer.Write(tile.HasTile); writer.Write(tile.TileType); writer.Write(tile.WallType);
			writer.Write(tile.TileFrameX); writer.Write(tile.TileFrameY); writer.Write(tile.LiquidAmount);
			writer.Write(tile.IsActuated); writer.Write(tile.IsHalfBlock); writer.Write((byte)tile.Slope);
		}
		return stream.ToArray();
	}
}
