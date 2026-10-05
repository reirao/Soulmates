using System;
using System.Reflection;
using Terraria;

namespace Soulmates.Common;

internal static class CompanionNativeRules
{
	private delegate int PickaxeDamage(Player player, int x, int y, int pickPower, int hitBufferIndex, Tile tile);
	// These native helpers are private. Bind once, not by reflection in the tile-search hot path.
	private static readonly PickaxeDamage GetPickaxeDamage = Bind<PickaxeDamage>("GetPickaxeDamage",
		[typeof(int), typeof(int), typeof(int), typeof(int), typeof(Tile)]);
	private static readonly Action<Player, Item> ApplyPotionDelay = Bind<Action<Player, Item>>("ApplyPotionDelay", [typeof(Item)]);

	internal static bool CanPick(Player player, int x, int y, int pickPower)
	{
		if (!WorldGen.InWorld(x, y, 10) || pickPower <= 0) return false;
		Tile tile = Main.tile[x, y];
		// Frame-important targets can redirect native hit buffers; companion mining excludes them.
		return tile.HasTile && CompanionMiningRules.IsSafeMaterial(tile.TileType)
			&& GetPickaxeDamage(player, x, y, pickPower, 0, tile) > 0;
	}

	internal static void ApplyHealingDelay(Player player, Item item)
	{
		if (item.potion) ApplyPotionDelay(player, item);
	}

	private static T Bind<T>(string name, Type[] parameters) where T : Delegate
		=> (typeof(Player).GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
			binder: null, parameters, modifiers: null) ?? throw new MissingMethodException(typeof(Player).FullName, name))
			.CreateDelegate<T>();
}
