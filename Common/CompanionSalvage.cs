#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace Soulmates.Common;

internal enum SalvageKind : byte { Gem, Pot, Web, Decoration }

internal sealed record SalvageCell(Point Position, ushort Type, short X, short Y, ushort Wall, byte Paint);

// A permit is a frozen footprint, not permission to search and demolish a region later.
internal sealed record SalvageTarget(Guid World, Rectangle Bounds, ushort Type, SalvageKind Kind,
	int Style, IReadOnlyList<SalvageCell> Cells, ulong Fingerprint)
{
	public Point Anchor => Bounds.Location;
	public Vector2 Center => new((Bounds.X + Bounds.Width * 0.5f) * 16f, (Bounds.Y + Bounds.Height * 0.5f) * 16f);
	public string Name {
		get {
			// Legacy multi-tiles use a frame identity here, not an item placement style.
			if (Bounds.Width * Bounds.Height > 1 && TileObjectData.GetTileData(Main.tile[Anchor]) is null)
				return SoulmatesText.Get($"Salvage.Kinds.{Kind}");
			int drop = TileLoader.GetItemDropFromTypeAndStyle(Type, Style);
			return drop > ItemID.None ? Lang.GetItemNameValue(drop) : SoulmatesText.Get($"Salvage.Kinds.{Kind}");
		}
	}
	public bool IsCurrent => World == Main.ActiveWorldFileData?.UniqueId && Cells.All(cell => {
		if (!WorldGen.InWorld(cell.Position.X, cell.Position.Y, 10)) return false;
		Tile tile = Main.tile[cell.Position];
		return tile.HasTile && tile.TileType == cell.Type && tile.TileFrameX == cell.X && tile.TileFrameY == cell.Y
			&& tile.WallType == cell.Wall && tile.TileColor == cell.Paint;
	});
}

internal static class CompanionSalvage
{
	internal const int Radius = 12;
	internal const int MaximumTargets = 16;
	internal static bool IsSupported(ushort type) => Kind(type) is not null;
	private static SalvageKind? Kind(ushort type) => type switch {
		TileID.Amethyst or TileID.Topaz or TileID.Sapphire or TileID.Emerald or TileID.Ruby or TileID.Diamond
			or TileID.AmberStoneBlock or TileID.ExposedGems => SalvageKind.Gem,
		TileID.Pots => SalvageKind.Pot,
		TileID.Cobweb => SalvageKind.Web,
		TileID.Coral or TileID.Books or TileID.Bottles or TileID.WaterCandle
			or TileID.Statues or TileID.Banners or TileID.Painting2X3 or TileID.Painting3X2
			or TileID.Painting3X3 or TileID.Painting4X3 or TileID.Painting6X4 => SalvageKind.Decoration,
		_ => null
	};

	internal static SalvageTarget? Capture(Point cursor)
	{
		if (Main.ActiveWorldFileData?.UniqueId is not Guid world || world == Guid.Empty
			|| !WorldGen.InWorld(cursor.X, cursor.Y, 10)) return null;
		Tile tile = Main.tile[cursor];
		if (!tile.HasTile || Kind(tile.TileType) is not { } kind || tile.TileFrameX < 0 || tile.TileFrameY < 0) return null;
		TileObjectData? data = TileObjectData.GetTileData(tile);
		// Some legacy vanilla multi-tiles do not have TileObjectData.
		Point legacySize = tile.TileType switch {
			TileID.Pots => new(2, 2), TileID.Statues => new(2, 3), TileID.Banners => new(1, 3),
			TileID.Painting2X3 => new(2, 3), TileID.Painting3X2 => new(3, 2), TileID.Painting3X3 => new(3, 3),
			TileID.Painting4X3 => new(4, 3), TileID.Painting6X4 => new(6, 4),
			_ => new(1, 1)
		};
		int width = data?.Width ?? legacySize.X;
		int height = data?.Height ?? legacySize.Y;
		if (width is < 1 or > 6 || height is < 1 or > 6) return null;
		int fullWidth = data?.CoordinateFullWidth ?? width * 18;
		int fullHeight = data?.CoordinateFullHeight ?? height * 18;
		if (fullWidth <= 0 || fullHeight <= 0) return null;
		int column = tile.TileFrameX % fullWidth / ((data?.CoordinateWidth ?? 16) + (data?.CoordinatePadding ?? 2));
		int localY = tile.TileFrameY % fullHeight, row = 0, rowOffset = 0;
		while (row < height - 1 && localY >= rowOffset + (data?.CoordinateHeights[row] ?? 16) + (data?.CoordinatePadding ?? 2)) {
			rowOffset += (data?.CoordinateHeights[row] ?? 16) + (data?.CoordinatePadding ?? 2);
			row++;
		}
		var bounds = new Rectangle(cursor.X - column, cursor.Y - row, width, height);
		var cells = new List<SalvageCell>();
		ulong fingerprint = 14695981039346656037UL;
		void Hash(ulong value) { fingerprint = unchecked((fingerprint ^ value) * 1099511628211UL); }
		int expectedY = tile.TileFrameY - rowOffset;
		for (int y = 0; y < height; y++) {
			for (int x = 0; x < width; x++) {
				Point point = new(bounds.X + x, bounds.Y + y);
				if (!WorldGen.InWorld(point.X, point.Y, 10)) return null;
				Tile part = Main.tile[point];
				if (!part.HasTile || part.TileType != tile.TileType
					|| width * height > 1 && (part.TileFrameX != tile.TileFrameX - column * ((data?.CoordinateWidth ?? 16) + (data?.CoordinatePadding ?? 2))
						+ x * ((data?.CoordinateWidth ?? 16) + (data?.CoordinatePadding ?? 2)) || part.TileFrameY != expectedY)) return null;
				cells.Add(new(point, part.TileType, part.TileFrameX, part.TileFrameY, part.WallType, part.TileColor));
				Hash((ulong)point.X); Hash((ulong)point.Y); Hash(part.TileType); Hash((ushort)part.TileFrameX);
				Hash((ushort)part.TileFrameY); Hash(part.WallType); Hash(part.TileColor);
			}
			expectedY += (data?.CoordinateHeights[y] ?? 16) + (data?.CoordinatePadding ?? 2);
		}
		int style = !Main.tileFrameImportant[tile.TileType] ? 0
			: data is null ? tile.TileFrameX / fullWidth | tile.TileFrameY / fullHeight << 16 : TileObjectData.GetTileStyle(tile);
		return new(world, bounds, tile.TileType, kind, style, cells, fingerprint);
	}

	internal static bool IsSafe(SalvageTarget target, CompanionProfile profile, Player owner, NPC mate)
	{
		if (!target.IsCurrent) return false;
		Rectangle region = target.Bounds; region.Inflate(1, 1);
		var protection = ModContent.GetInstance<SalvageProtectionSystem>();
		for (int x = region.Left; x < region.Right; x++)
			for (int y = region.Top; y < region.Bottom; y++) {
				Point point = new(x, y);
				if (!WorldGen.InWorld(x, y, 10) || protection.IsProtected(point)) return false;
				Tile tile = Main.tile[point];
				if (tile.RedWire || tile.GreenWire || tile.BlueWire || tile.YellowWire || tile.HasActuator || tile.IsActuated
					|| tile.TileColor != 0 || tile.WallColor != 0 || tile.IsTileInvisible || tile.IsWallInvisible
					|| tile.IsTileFullbright || tile.IsWallFullbright || tile.LiquidAmount > 0 && tile.LiquidType != LiquidID.Water
					|| tile.WallType < Main.wallHouse.Length && Main.wallHouse[tile.WallType]
					|| TileEntity.ByPosition.ContainsKey(new Point16(x, y))) return false;
				if (tile.HasTile && !target.Bounds.Contains(point) && Main.tileFrameImportant[tile.TileType]) return false;
				if (tile.HasTile && target.Bounds.Contains(point) && !WorldGen.CanKillTile(x, y)) return false;
				// Never remove a material which is supporting another tile or decoration.
				if (target.Kind == SalvageKind.Gem && Main.tileSolid[target.Type] && tile.HasTile
					&& !target.Bounds.Contains(point) && (Main.tileFrameImportant[tile.TileType] || Main.tileCut[tile.TileType]
						|| TileID.Sets.Falling[tile.TileType])) return false;
			}
		foreach (CompanionPlace home in profile.Places.Records)
			if (home.WorldId == target.World && home.IsHome && Vector2.DistanceSquared(home.Anchor.ToVector2(), target.Anchor.ToVector2()) <= 32 * 32) return false;
		foreach (Player player in Main.ActivePlayers)
			if (player.SpawnX > 0 && Vector2.DistanceSquared(new Vector2(player.SpawnX, player.SpawnY), target.Anchor.ToVector2()) <= 32 * 32) return false;
		foreach (NPC npc in Main.ActiveNPCs)
			if (npc.townNPC && !npc.homeless && Vector2.DistanceSquared(new Vector2(npc.homeTileX, npc.homeTileY), target.Anchor.ToVector2()) <= 32 * 32) return false;
		CompanionPlaceObservation room = CompanionPlaces.SampleAt(owner, mate, target.Anchor);
		return !room.IsCandidate && !room.OwnerBedSpawn && !room.AssignedHousing;
	}

	internal static List<SalvageTarget> Nearby(SalvageTarget selected, CompanionProfile profile, Player owner, NPC mate)
	{
		var results = new List<SalvageTarget> { selected };
		var anchors = new HashSet<Point> { selected.Anchor };
		for (int x = selected.Anchor.X - Radius; x <= selected.Anchor.X + Radius; x++)
			for (int y = selected.Anchor.Y - Radius; y <= selected.Anchor.Y + Radius; y++) {
				if (!WorldGen.InWorld(x, y, 10) || Main.tile[x, y].TileType != selected.Type || !Main.tile[x, y].HasTile
					|| Vector2.DistanceSquared(new Vector2(x, y), selected.Anchor.ToVector2()) > Radius * Radius) continue;
				SalvageTarget? candidate = Capture(new Point(x, y));
				if (candidate is null || anchors.Contains(candidate.Anchor) || candidate.Style != selected.Style
					|| !IsSafe(candidate, profile, owner, mate)) continue;
				anchors.Add(candidate.Anchor); results.Add(candidate);
			}
		return results.OrderBy(target => Vector2.DistanceSquared(target.Center, selected.Center)).Take(MaximumTargets).ToList();
	}
}
