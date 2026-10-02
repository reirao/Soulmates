using Terraria;
using Terraria.ID;

namespace Soulmates.Common.Dialogue;

public enum CompanionItemTopic : byte { All, Food, Ores, Tools, Weapons, Recovery, Materials, Critters, Other }

public static class CompanionItemTopics
{
	public static CompanionItemTopic Classify(Item item)
	{
		if (item.IsAir) return CompanionItemTopic.Other;
		if (item.buffType > 0 && item.buffType < BuffID.Sets.IsWellFed.Length && BuffID.Sets.IsWellFed[item.buffType])
			return CompanionItemTopic.Food;
		if (item.pick > 0 || item.axe > 0 || item.hammer > 0 || item.fishingPole > 0
			|| item.type < ItemID.Sets.CatchingTool.Length && ItemID.Sets.CatchingTool[item.type]) return CompanionItemTopic.Tools;
		if (item.makeNPC > 0) return CompanionItemTopic.Critters;
		if (item.healLife > 0 || item.healMana > 0 || item.consumable && item.buffType > 0) return CompanionItemTopic.Recovery;
		if (item.createTile >= TileID.Dirt && item.createTile < TileID.Sets.Ore.Length && TileID.Sets.Ore[item.createTile])
			return CompanionItemTopic.Ores;
		if (item.damage > 0 && item.ammo == AmmoID.None) return CompanionItemTopic.Weapons;
		return CompanionProfile.IsResource(item) ? CompanionItemTopic.Materials : CompanionItemTopic.Other;
	}

	public static int Icon(CompanionItemTopic topic) => topic switch {
		CompanionItemTopic.Food => ItemID.CookedFish,
		CompanionItemTopic.Ores => ItemID.CopperOre,
		CompanionItemTopic.Tools => ItemID.CopperPickaxe,
		CompanionItemTopic.Weapons => ItemID.CopperShortsword,
		CompanionItemTopic.Recovery => ItemID.LesserHealingPotion,
		CompanionItemTopic.Materials => ItemID.Wood,
		CompanionItemTopic.Critters => ItemID.BugNet,
		CompanionItemTopic.Other => ItemID.Chest,
		_ => ItemID.Book
	};
}
