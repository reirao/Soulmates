#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader.IO;

namespace Soulmates.Common;

public enum CompanionPlaceRole : byte { Shelter, SharedHome, CompanionHome, NotHome }

public sealed record CompanionPlace(Guid WorldId, Point Anchor, CompanionPlaceRole Role, byte Visits)
{
	// Familiarity never implies ownership, even after many returns.
	public byte Confidence => (byte)Math.Min(80, Visits * 10);
	public bool IsFamiliar => Visits >= 3;
	public bool IsHome => Role is CompanionPlaceRole.SharedHome or CompanionPlaceRole.CompanionHome;
}

public readonly record struct CompanionPlaceObservation(Point Anchor, bool Valid, bool Enclosed,
	bool HasManufacturedWalls, bool HasFloor, bool HasFurniture, bool OwnerBedSpawn, bool AssignedHousing)
{
	public bool IsCandidate => Valid && (Enclosed && HasManufacturedWalls && HasFloor && HasFurniture
		|| OwnerBedSpawn || AssignedHousing);
}

// Owned by one Sigil. Observe is a return event, not a per-tick environmental sampler.
public sealed class CompanionPlaces
{
	public const int MaximumRecords = 8;
	public const int MaximumVisits = 32;
	public const int NearbyRadius = 2;
	private const byte WireVersion = 1;
	private const int MaximumCoordinate = ushort.MaxValue - 1;
	private const int MaximumRoomTiles = 640;
	private static readonly Point[] Directions = [new(-1, 0), new(1, 0), new(0, -1), new(0, 1)];
	private readonly List<CompanionPlace> records = [];
	public IReadOnlyList<CompanionPlace> Records => records.AsReadOnly();

	public static bool IsValidAnchor(Guid world, Point anchor) => world != Guid.Empty
		&& anchor.X is > 0 and <= MaximumCoordinate && anchor.Y is > 0 and <= MaximumCoordinate
		&& (Main.ActiveWorldFileData?.UniqueId != world || InNativeBounds(anchor));

	public CompanionPlace? FindNearby(Guid world, Point anchor, int radius = NearbyRadius)
	{
		if (!IsValidAnchor(world, anchor) || radius is < 0 or > 128) return null;
		CompanionPlace? nearest = null;
		long best = (long)radius * radius + 1;
		foreach (CompanionPlace place in records) {
			if (place.WorldId != world) continue;
			long dx = place.Anchor.X - anchor.X, dy = place.Anchor.Y - anchor.Y;
			long distance = dx * dx + dy * dy;
			if (distance >= best) continue;
			nearest = place; best = distance;
		}
		return nearest;
	}

	public CompanionPlace? Observe(Guid world, Point anchor)
	{
		if (!IsValidAnchor(world, anchor)) return null;
		CompanionPlace? previous = FindNearby(world, anchor);
		return Store(previous is null ? new(world, anchor, CompanionPlaceRole.Shelter, 1)
			: previous with { Visits = (byte)Math.Min(MaximumVisits, previous.Visits + 1) }, previous);
	}

	public CompanionPlace? RememberRole(Guid world, Point anchor, CompanionPlaceRole role)
	{
		if (!IsValidAnchor(world, anchor) || !ValidRole(role)) return null;
		CompanionPlace? previous = FindNearby(world, anchor);
		return Store(previous is null ? new(world, anchor, role, 1) : previous with { Role = role }, previous);
	}

	private CompanionPlace Store(CompanionPlace place, CompanionPlace? previous)
	{
		if (previous is not null) records.Remove(previous);
		records.Add(place);
		if (records.Count > MaximumRecords) records.RemoveAt(0);
		return place;
	}

	public CompanionPlaces Clone()
	{
		var clone = new CompanionPlaces();
		foreach (CompanionPlace place in records) clone.records.Add(place with { });
		return clone;
	}

	public TagCompound Save()
	{
		var entries = new List<TagCompound>();
		foreach (CompanionPlace place in records)
			entries.Add(new TagCompound { ["world"] = place.WorldId.ToString(), ["x"] = place.Anchor.X,
				["y"] = place.Anchor.Y, ["role"] = (int)place.Role, ["visits"] = (int)place.Visits });
		return new TagCompound { ["places"] = entries };
	}

	public static CompanionPlaces Load(TagCompound tag)
	{
		var result = new CompanionPlaces();
		if (!tag.ContainsKey("places") || tag["places"] is not IList<TagCompound> entries) return result;
		for (int i = 0; i < Math.Min(entries.Count, MaximumRecords); i++) {
			TagCompound entry = entries[i];
			if (entry is null || !entry.ContainsKey("world") || entry["world"] is not string worldText
				|| !Guid.TryParse(worldText, out Guid world) || !TryInt(entry, "x", out int x)
				|| !TryInt(entry, "y", out int y) || !TryInt(entry, "role", out int role)
				|| role is < 0 or > (int)CompanionPlaceRole.NotHome || !TryInt(entry, "visits", out int visits)
				|| visits is < 1 or > MaximumVisits || !IsValidAnchor(world, new Point(x, y))) continue;
			CompanionPlace? previous = result.FindNearby(world, new Point(x, y));
			var place = new CompanionPlace(world, previous?.Anchor ?? new Point(x, y),
				role == (int)CompanionPlaceRole.Shelter && previous is not null ? previous.Role : (CompanionPlaceRole)role,
				(byte)Math.Max(visits, previous?.Visits ?? 0));
			result.Store(place, previous);
		}
		return result;
	}

	public void Write(BinaryWriter writer)
	{
		writer.Write(WireVersion); writer.Write((byte)records.Count);
		foreach (CompanionPlace place in records) {
			writer.Write(place.WorldId.ToByteArray());
			writer.Write((ushort)place.Anchor.X); writer.Write((ushort)place.Anchor.Y);
			writer.Write((byte)place.Role); writer.Write(place.Visits);
		}
	}

	public static CompanionPlaces Read(BinaryReader reader)
	{
		if (reader.ReadByte() != WireVersion) throw new InvalidDataException("Invalid companion place version.");
		int count = reader.ReadByte();
		if (count > MaximumRecords) throw new InvalidDataException("Too many companion places.");
		var result = new CompanionPlaces();
		for (int i = 0; i < count; i++) {
			byte[] worldBytes = reader.ReadBytes(16);
			if (worldBytes.Length != 16) throw new InvalidDataException("Truncated companion place world.");
			Guid world = new(worldBytes);
			Point anchor = new(reader.ReadUInt16(), reader.ReadUInt16());
			var role = (CompanionPlaceRole)reader.ReadByte();
			byte visits = reader.ReadByte();
			if (!IsValidAnchor(world, anchor) || !ValidRole(role) || visits is < 1 or > MaximumVisits
				|| result.FindNearby(world, anchor) is not null)
				throw new InvalidDataException("Invalid or duplicate companion place.");
			result.records.Add(new(world, anchor, role, visits));
		}
		return result;
	}

	private static bool TryInt(TagCompound tag, string key, out int value)
	{
		value = 0;
		if (!tag.ContainsKey(key) || tag[key] is not int number) return false;
		value = number; return true;
	}
	private static bool ValidRole(CompanionPlaceRole role) => role <= CompanionPlaceRole.NotHome;
	private static bool InNativeBounds(Point p) => p.X > 0 && p.Y > 0
		&& p.X < Main.maxTilesX - 1 && p.Y < Main.maxTilesY - 1
		&& p.X < Main.tile.Width - 1 && p.Y < Main.tile.Height - 1;
	private static bool Flag(bool[] flags, int type) => (uint)type < flags.Length && flags[type];
	private static bool CountsAs(int[] types, int type) => Array.IndexOf(types, type) >= 0;
	private static bool Solid(Tile tile) => tile.HasTile && !tile.IsActuated && Flag(Main.tileSolid, tile.TileType)
		&& !Flag(Main.tileSolidTop, tile.TileType);
	private static bool Boundary(Tile tile) => Solid(tile) || tile.HasTile && !tile.IsActuated
		&& CountsAs(TileID.Sets.RoomNeeds.CountsAsDoor, tile.TileType);

	public static CompanionPlaceObservation Sample(Player owner, NPC mate)
	{
		if (owner is null || mate is null || !float.IsFinite(mate.Center.X) || !float.IsFinite(mate.Center.Y)
			|| mate.Center.X < 0 || mate.Center.Y < 0 || mate.Center.X >= Main.maxTilesX * 16f
			|| mate.Center.Y >= Main.maxTilesY * 16f) return default;
		return SampleAt(owner, mate, new Point((int)(mate.Center.X / 16f), (int)(mate.Center.Y / 16f)));
	}

	// The explicit location supports chest queries without moving or constructing an NPC.
	public static CompanionPlaceObservation SampleAt(Player owner, NPC mate, Point location)
	{
		if (owner is null || mate is null || !InNativeBounds(location) || Boundary(Main.tile[location])) return default;
		var room = new HashSet<Point> { location };
		var pending = new Queue<Point>(); pending.Enqueue(location);
		bool enclosed = true, furniture = false;
		int walls = 0, floor = 0, left = location.X, right = left, top = location.Y, bottom = top;
		while (pending.Count > 0) {
			Point cell = pending.Dequeue();
			Tile tile = Main.tile[cell];
			left = Math.Min(left, cell.X); right = Math.Max(right, cell.X);
			top = Math.Min(top, cell.Y); bottom = Math.Max(bottom, cell.Y);
			if (tile.WallType != WallID.None && Flag(Main.wallHouse, tile.WallType)) walls++;
			if (tile.HasTile && !tile.IsActuated && (Flag(TileID.Sets.IsValidSpawnPoint, tile.TileType)
				|| CountsAs(TileID.Sets.RoomNeeds.CountsAsChair, tile.TileType)
				|| CountsAs(TileID.Sets.RoomNeeds.CountsAsTable, tile.TileType)
				|| CountsAs(TileID.Sets.RoomNeeds.CountsAsTorch, tile.TileType))) furniture = true;
			foreach (Point direction in Directions) {
				Point next = cell + direction;
				if (!InNativeBounds(next)) {
					enclosed = false; continue;
				}
				Tile neighbor = Main.tile[next];
				if (direction.Y == 1 && (Solid(neighbor) || neighbor.HasTile && !neighbor.IsActuated
					&& Flag(Main.tileSolidTop, neighbor.TileType))) floor++;
				if (Boundary(neighbor) || room.Contains(next)) continue;
				// Bound the room's extent, not the query's distance from its center.
				if (room.Count >= MaximumRoomTiles || Math.Max(right, next.X) - Math.Min(left, next.X) > 40
					|| Math.Max(bottom, next.Y) - Math.Min(top, next.Y) > 24) { enclosed = false; continue; }
				left = Math.Min(left, next.X); right = Math.Max(right, next.X);
				top = Math.Min(top, next.Y); bottom = Math.Max(bottom, next.Y);
				room.Add(next); pending.Enqueue(next);
			}
		}
		bool protectedWalls = room.Count >= 12 && walls * 5 >= room.Count * 4;
		bool hasFloor = floor >= 3;
		bool ownerBed = enclosed && protectedWalls && hasFloor && ValidSelectedBed(owner, room);
		bool assigned = false;
		foreach (NPC resident in Main.ActiveNPCs) {
			if (!enclosed || !protectedWalls || !hasFloor) break;
			if (!resident.townNPC || resident.homeless || ReferenceEquals(resident, mate)
				|| resident.ModNPC is SoulboundCompanion) continue;
			Point home = new(resident.homeTileX, resident.homeTileY);
			if (InNativeBounds(home) && (room.Contains(home) || room.Contains(new Point(home.X, home.Y - 1)))) {
				assigned = true; break;
			}
		}
		Point anchor = enclosed ? new((left + right) / 2, (top + bottom) / 2) : location;
		if (enclosed && !room.Contains(anchor)) {
			Point center = anchor;
			long best = long.MaxValue;
			foreach (Point cell in room) {
				long dx = cell.X - center.X, dy = cell.Y - center.Y, distance = dx * dx + dy * dy;
				if (distance < best || distance == best && (cell.X < anchor.X || cell.X == anchor.X && cell.Y < anchor.Y)) {
					anchor = cell; best = distance;
				}
			}
		}
		return new(anchor, true, enclosed, protectedWalls, hasFloor, furniture, ownerBed, assigned);
	}

	private static bool ValidSelectedBed(Player owner, HashSet<Point> room)
	{
		Point spawn = new(owner.SpawnX, owner.SpawnY), bed = new(owner.SpawnX, owner.SpawnY - 1);
		if (!InNativeBounds(spawn) || !InNativeBounds(bed) || !room.Contains(bed)) return false;
		Tile tile = Main.tile[bed];
		if (!tile.HasTile || tile.IsActuated || !Flag(TileID.Sets.IsValidSpawnPoint, tile.TileType)) return false;
		for (int x = spawn.X - 1; x <= spawn.X + 1; x++)
			for (int y = spawn.Y - 3; y < spawn.Y; y++)
				if (!InNativeBounds(new Point(x, y)) || Solid(Main.tile[x, y])) return false;
		return true;
	}
}
