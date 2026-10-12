#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Soulmates.Common;

// Evidence of construction, never evidence that an unrecorded object is unowned.
public sealed class SalvageProtectionSystem : ModSystem
{
	private const int CellSize = 8;
	private const int MaximumCells = 65536;
	private readonly HashSet<int> construction = [];
	private bool saturated;
	public override void Load()
	{
		On_WorldGen.PlaceTile += ObserveTile;
		On_TileObject.Place += ObserveObject;
		On_WorldGen.PlaceWall += ObserveWall;
		On_WorldGen.ReplaceTile += ObserveReplacement;
	}
	public override void Unload()
	{
		On_WorldGen.PlaceTile -= ObserveTile;
		On_TileObject.Place -= ObserveObject;
		On_WorldGen.PlaceWall -= ObserveWall;
		On_WorldGen.ReplaceTile -= ObserveReplacement;
	}
	public override void ClearWorld() { construction.Clear(); saturated = false; }
	public override void SaveWorldData(TagCompound tag)
	{
		tag["salvageConstruction"] = new List<int>(construction);
		tag["salvageProtectionFull"] = saturated;
	}
	public override void LoadWorldData(TagCompound tag)
	{
		ClearWorld();
		saturated = tag.GetBool("salvageProtectionFull");
		if (!tag.ContainsKey("salvageConstruction")) return;
		if (tag["salvageConstruction"] is not IList<int> saved) { saturated = true; return; }
		foreach (int cell in saved) {
			if (cell < 0 || construction.Count >= MaximumCells) { saturated = true; break; }
			construction.Add(cell);
		}
	}
	public bool IsProtected(Point tile) => saturated || construction.Contains(Key(tile.X, tile.Y));
	private static int Key(int x, int y) => (x / CellSize) << 16 | (y / CellSize & 65535);
	internal void Observe(int x, int y, int width = 1, int height = 1)
	{
		if (WorldGen.gen || Main.netMode == NetmodeID.MultiplayerClient || !WorldGen.InWorld(x, y, 1)) return;
		for (int i = Math.Max(1, x - 1); i <= Math.Min(Main.maxTilesX - 2, x + width); i++)
			for (int j = Math.Max(1, y - 1); j <= Math.Min(Main.maxTilesY - 2, y + height); j++) {
				if (construction.Count >= MaximumCells) { saturated = true; return; }
				construction.Add(Key(i, j));
			}
	}
	private static bool ObserveTile(On_WorldGen.orig_PlaceTile original, int x, int y, int type,
		bool mute, bool forced, int player, int style)
	{
		bool result = original(x, y, type, mute, forced, player, style);
		if (WorldGen.InWorld(x, y, 1) && Main.tile[x, y].HasTile && Main.tile[x, y].TileType == type)
			ModContent.GetInstance<SalvageProtectionSystem>().Observe(x, y);
		return result;
	}
	private static bool ObserveObject(On_TileObject.orig_Place original, TileObject data)
	{
		bool result = original(data);
		if (result) {
			var shape = Terraria.ObjectData.TileObjectData.GetTileData(data.type, data.style, data.alternate);
			ModContent.GetInstance<SalvageProtectionSystem>().Observe(data.xCoord, data.yCoord, shape?.Width ?? 1, shape?.Height ?? 1);
		}
		return result;
	}
	private static void ObserveWall(On_WorldGen.orig_PlaceWall original, int x, int y, int type, bool mute)
	{
		original(x, y, type, mute);
		if (WorldGen.InWorld(x, y, 1) && Main.tile[x, y].WallType == type)
			ModContent.GetInstance<SalvageProtectionSystem>().Observe(x, y);
	}
	private static bool ObserveReplacement(On_WorldGen.orig_ReplaceTile original, int x, int y, ushort type, int style)
	{
		bool result = original(x, y, type, style);
		if (result) ModContent.GetInstance<SalvageProtectionSystem>().Observe(x, y);
		return result;
	}
}
