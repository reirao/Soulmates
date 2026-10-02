using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulmates.Common;

public static class CompanionMiningRules
{
	public const int MaximumRules = 512;
	public static bool IsOre(ushort type) => type < TileID.Sets.Ore.Length && TileID.Sets.Ore[type]
		|| type < Main.tileOreFinderPriority.Length && Main.tileOreFinderPriority[type] > 0;

	public static bool IsSafeMaterial(ushort type)
	{
		if (type >= Main.tileSolid.Length || !Main.tileSolid[type] || Main.tileFrameImportant[type]
			|| Main.tileAxe[type] || Main.tileContainer[type]) return false;
		if (type < TileID.Sets.IsAContainer.Length && TileID.Sets.IsAContainer[type]) return false;
		return type >= TileID.Sets.IsATreeTrunk.Length || !TileID.Sets.IsATreeTrunk[type];
	}

	// Mod tile IDs can change with load order; persist their stable content names instead.
	public static string Key(int tileType) => TileLoader.GetTile(tileType)?.FullName ?? $"Terraria/{tileType}";
	public static bool ValidKey(string key) => key.Length is > 0 and <= 200
		&& key.IndexOf('/') > 0 && !key.Any(char.IsControl);
	public static List<int> Types(CompanionProfile profile, bool ores)
		=> (ores ? Enumerable.Range(0, TileLoader.TileCount) : profile.LearnedMiningTiles)
			.Where(type => type >= 0 && type < TileLoader.TileCount && IsSafeMaterial((ushort)type)
				&& IsOre((ushort)type) == ores).Distinct().OrderBy(type => type).ToList();
	public static int ItemType(int tileType) => TileLoader.GetItemDropFromTypeAndStyle(tileType, 0);
	public static string Name(int tileType) => ItemType(tileType) is > ItemID.None and var itemType
		? Lang.GetItemNameValue(itemType) : SoulmatesText.Get("Resourcefulness.UnknownMaterial");
}
