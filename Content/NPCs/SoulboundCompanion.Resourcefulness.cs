#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Common.Feedback;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulmates.Content.NPCs;

public sealed partial class SoulboundCompanion
{
	private enum PackUseKind
	{
		None,
		Healing,
		Mana,
		Food
	}

	private int packItemUseCooldown;
	private int craftingSuggestionCooldown;
	private int lastPackWeaponType;
	private readonly Queue<int> recentCraftSuggestions = new();

	private void UpdateResourcefulness()
	{
		AddPackLight();
		if (Main.netMode != NetmodeID.MultiplayerClient) {
			if (packItemUseCooldown > 0)
				packItemUseCooldown--;
			else
				TryUsePackConsumable();
		}
		UpdateCraftingSuggestions();
	}

	internal bool ObserveMiningTarget(Point target, int toolType)
	{
		if (TryGetOwner(out Player pendingOwner) && CompanionInventorySync.IsPending(pendingOwner)) return false;
		if (Main.netMode == NetmodeID.MultiplayerClient || !TryGetOwner(out Player owner) || owner.dead
			|| FindBoundSigil() is null || Vector2.DistanceSquared(NPC.Center, owner.Center) > 520f * 520f
			|| !WorldGen.InWorld(target.X, target.Y, 10)
			|| !owner.IsInTileInteractionRange(target.X, target.Y, TileReachCheckSettings.Simple)) return false;
		Item pickaxe = owner.HeldItem;
		if (pickaxe.type != toolType || pickaxe.pick <= 0 || !(owner.controlUseItem || owner.itemAnimation > 0)) return false;
		Tile tile = Main.tile[target.X, target.Y];
		if (!tile.HasTile || !IsLearnableMiningMaterial(tile.TileType)
			|| !WorldGen.CanKillTile(target.X, target.Y)
			|| !CompanionNativeRules.CanPick(owner, target.X, target.Y, pickaxe.pick)) return false;

		int previousPower = Profile.ObservedPickPower;
		bool learned = Profile.LearnMiningMaterial(tile.TileType, pickaxe.pick);
		if (!learned && Profile.ObservedPickPower == previousPower)
			return true;

		SoulmatesFeedbackSystem.Record("mining_material_observed", ("tile_type", tile.TileType),
			("pick_power", pickaxe.pick), ("new_material", learned));
		SyncProfileToBoundSigil();
		NPC.netUpdate = true;
		if (!learned)
			return true;

		int dropType = TileLoader.GetItemDropFromTypeAndStyle(tile.TileType, 0);
		string materialName = dropType > ItemID.None
			? Lang.GetItemNameValue(dropType)
			: SoulmatesText.Get("Resourcefulness.UnknownMaterial");
		ShowNativeEmote(EmoteID.ItemPickaxe, 110);
		SpeakLocalized("Resourcefulness.LearnedMaterial", materialName);
		return true;
	}

	private bool CanMineTile(int x, int y, bool includeLearnedMaterials)
	{
		if (!WorldGen.InWorld(x, y, 10))
			return false;
		Tile tile = Main.tile[x, y];
		if (!tile.HasTile || !IsSafeMiningMaterial(tile.TileType))
			return false;
		bool knownMaterial = IsOreTile(tile.TileType)
			|| includeLearnedMaterials && Profile.KnowsMiningMaterial(tile.TileType);
		if (!knownMaterial || HasMiningDecorationNeighbour(x, y) || !WorldGen.CanKillTile(x, y))
			return false;

		return CompanionNativeRules.CanPick(Owner, x, y, EffectivePickPower(out _));
	}

	private int EffectivePickPower(out int itemType)
	{
		int power = Math.Max(35, Profile.ObservedPickPower);
		itemType = ItemID.None;
		foreach (Item item in Profile.Pack) {
			if (item.IsAir || item.pick <= power)
				continue;
			power = item.pick;
			itemType = item.type;
		}
		return power;
	}

	private static bool IsOreTile(ushort type) => CompanionMiningRules.IsOre(type);

	private static bool IsLearnableMiningMaterial(ushort type)
		=> IsOreTile(type) || IsSafeMiningMaterial(type);

	private static bool IsSafeMiningMaterial(ushort type) => CompanionMiningRules.IsSafeMaterial(type);

	private Item? BestPackWeapon()
	{
		Item? best = null;
		foreach (Item item in Profile.Pack) {
			if (item.IsAir || item.damage <= 0 || item.consumable || item.ammo > AmmoID.None
				|| item.useAmmo > AmmoID.None || item.pick > 0 || item.axe > 0 || item.hammer > 0)
				continue;
			if (best is null || item.damage > best.damage)
				best = item;
		}
		return best;
	}

	private int PackWeaponDamageBonus(out int itemType)
	{
		Item? weapon = BestPackWeapon();
		itemType = weapon?.type ?? ItemID.None;
		return weapon is null ? 0 : Math.Clamp(weapon.damage / 10, 1, 8);
	}

	private void AnnouncePackWeapon()
	{
		Item? weapon = BestPackWeapon();
		if (weapon is null || weapon.type == lastPackWeaponType)
			return;
		lastPackWeaponType = weapon.type;
		ShowNativeEmote(EmoteID.ItemSword, 90);
		SoulmatesFeedbackSystem.Record("pack_weapon_readied", ("item_type", weapon.type),
			("damage", weapon.damage));
	}

	private void AddPackLight()
	{
		if (!Profile.CarriedItems.Any(item => !item.IsAir && item.createTile == TileID.Torches))
			return;
		Lighting.AddLight(NPC.Center, new Vector3(0.8f, 0.58f, 0.28f));
	}

	private bool TryUsePackConsumable()
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || !TryGetOwner(out Player owner) || owner.dead
			|| owner.cursed || owner.CCed || CompanionInventorySync.IsPending(owner)) return false;
		int index = -1;
		PackUseKind kind = PackUseKind.None;
		int bestValue = 0;
		bool needsLife = Owner.statLife * 100 <= Owner.statLifeMax2 * 40 && Owner.potionDelay <= 0
			&& !Owner.HasBuff(BuffID.PotionSickness);
		bool needsMana = Owner.statManaMax2 > 0 && Owner.statMana * 100 <= Owner.statManaMax2 * 25
			&& !Owner.HasBuff(BuffID.ManaSickness);

		for (int i = 0; i < Profile.Pack.Count; i++) {
			Item item = Profile.Pack[i];
			if (item.IsAir || item.stack <= 0 || !item.consumable)
				continue;
			if (needsLife && item.healLife > bestValue && CombinedHooks.CanUseItem(Owner, item)) {
				index = i;
				kind = PackUseKind.Healing;
				bestValue = item.healLife;
			}
		}
		if (index < 0 && needsMana) {
			for (int i = 0; i < Profile.Pack.Count; i++) {
				Item item = Profile.Pack[i];
				if (item.IsAir || item.stack <= 0 || !item.consumable || item.healMana <= bestValue
					|| !CombinedHooks.CanUseItem(Owner, item))
					continue;
				index = i;
				kind = PackUseKind.Mana;
				bestValue = item.healMana;
			}
		}
		if (index < 0 && !HasWellFedBuff()) {
			for (int i = 0; i < Profile.Pack.Count; i++) {
				Item item = Profile.Pack[i];
				if (item.IsAir || item.stack <= 0 || !item.consumable || item.buffType <= 0
					|| item.rare > ItemRarityID.Blue || item.buffType >= BuffID.Sets.IsWellFed.Length
					|| !BuffID.Sets.IsWellFed[item.buffType] || !CombinedHooks.CanUseItem(Owner, item))
					continue;
				index = i;
				kind = PackUseKind.Food;
				break;
			}
		}
		if (index < 0 || kind == PackUseKind.None)
			return false;

		Item used = Profile.Pack[index];
		string itemName = used.Name;
		int itemType = used.type;
		if (kind == PackUseKind.Healing) {
			CompanionNativeRules.ApplyHealingDelay(Owner, used);
			int sicknessSlot = Owner.FindBuffIndex(BuffID.PotionSickness);
			if (Main.netMode == NetmodeID.Server && sicknessSlot >= 0)
				NetMessage.SendData(MessageID.AddPlayerBuff, Owner.whoAmI, -1, null,
					Owner.whoAmI, BuffID.PotionSickness, Owner.buffTime[sicknessSlot]);
		}
		ItemLoader.UseItem(used, Owner);
		switch (kind) {
			case PackUseKind.Healing:
				int healed = Math.Min(Owner.statLifeMax2 - Owner.statLife, Owner.GetHealLife(used, true));
				HealOwner(healed);
				ShowNativeEmote(EmoteID.ItemLifePotion, 100);
				break;
			case PackUseKind.Mana:
				int restored = Math.Clamp(Owner.GetHealMana(used, true), 0, Math.Max(0, Owner.statManaMax2 - Owner.statMana));
				Owner.statMana += restored;
				if (Main.netMode == NetmodeID.Server)
					global::Soulmates.Soulmates.SendManaRecovery(Owner, restored);
				else
					Owner.ManaEffect(restored);
				AddOwnerBuff(BuffID.ManaSickness, 300);
				ShowNativeEmote(EmoteID.ItemManaPotion, 100);
				break;
			case PackUseKind.Food:
				AddOwnerBuff(used.buffType, used.buffTime);
				ShowNativeEmote(EmoteID.ItemSoup, 100);
				break;
		}

		// ConsumeItem invokes OnConsumeItem itself; false permits use without consuming a unit.
		if (ItemLoader.ConsumeItem(used, Owner)) {
			used.stack--;
			if (used.stack <= 0) Profile.Pack.RemoveAt(index);
		}
		packItemUseCooldown = 900;
		SoulmatesFeedbackSystem.Record("pack_item_used", ("item_type", itemType),
			("use", kind.ToString()), ("pack_load", Profile.PackLoad));
		SpeakLocalized("Resourcefulness.UsedItem", itemName);
		SyncPackState();
		if (Main.netMode == NetmodeID.Server)
			NetMessage.SendData(MessageID.PlayerLifeMana, -1, -1, null, Owner.whoAmI);
		return true;
	}

	private void AddOwnerBuff(int type, int ticks)
	{
		Owner.AddBuff(type, ticks);
		if (Main.netMode == NetmodeID.Server)
			NetMessage.SendData(MessageID.AddPlayerBuff, Owner.whoAmI, -1, null, Owner.whoAmI, type, ticks);
	}

	private bool HasWellFedBuff()
	{
		for (int i = 0; i < Player.MaxBuffs; i++) {
			int buffType = Owner.buffType[i];
			if (buffType > 0 && buffType < BuffID.Sets.IsWellFed.Length && BuffID.Sets.IsWellFed[buffType])
				return true;
		}
		return false;
	}

	private void UpdateCraftingSuggestions()
	{
		if (Main.dedServ || Owner.whoAmI != Main.myPlayer || !Profile.AutonomyEnabled
			|| activeJob != CompanionJob.None || autonomyActivity != AutonomyActivity.None
			|| pendingAutonomyActivity != AutonomyActivity.None || HasPendingQuestion || guardianTarget >= 0 || speechTimer > 0
			|| pendingCritterNotice is not null || pendingCritterLossKey.Length > 0)
			return;
		if (craftingSuggestionCooldown > 0) {
			craftingSuggestionCooldown--;
			return;
		}
		craftingSuggestionCooldown = Main.rand.Next(1800, 3600);

		var availableRecipes = new HashSet<int>();
		for (int i = 0; i < Main.numAvailableRecipes; i++)
			availableRecipes.Add(Main.availableRecipe[i]);

		Recipe? suggestion = null;
		int bestScore = int.MinValue;
		for (int recipeIndex = 0; recipeIndex < Recipe.numRecipes; recipeIndex++) {
			Recipe recipe = Main.recipe[recipeIndex];
			Item result = recipe.createItem;
			if (recipe.Disabled || result.IsAir || recentCraftSuggestions.Contains(result.type)
				|| !PackContainsRecipeIngredient(recipe)
				|| !availableRecipes.Contains(recipeIndex) && !CanCraftFromInventoryAndPack(recipe))
				continue;
			int score = result.rare * 20 + result.value / 1000 + result.damage * 2 + result.pick
				+ (result.accessory ? 80 : 0) + (result.defense > 0 ? 60 : 0);
			if (score <= bestScore)
				continue;
			bestScore = score;
			suggestion = recipe;
		}
		if (suggestion is null) {
			craftingSuggestionCooldown = 600;
			return;
		}

		int resultType = suggestion.createItem.type;
		recentCraftSuggestions.Enqueue(resultType);
		while (recentCraftSuggestions.Count > 6)
			recentCraftSuggestions.Dequeue();
		ShowNativeEmote(EmoteID.ItemCog, 130);
		SpeakLocalized("Resourcefulness.CraftSuggestion", suggestion.createItem.Name);
		SoulmatesFeedbackSystem.Record("crafting_suggestion", ("item_type", resultType),
			("pack_load", Profile.PackLoad));
	}

	private bool PackContainsRecipeIngredient(Recipe recipe)
	{
		if (recipe.requiredItem.Any(required => !required.IsAir && Profile.ItemCount(required.type) > 0))
			return true;
		foreach (int groupId in recipe.acceptedGroups) {
			if (!RecipeGroup.recipeGroups.TryGetValue(groupId, out RecipeGroup? group))
				continue;
			if (Profile.CarriedItems.Any(item => !item.IsAir && group.ContainsItem(item.type)))
				return true;
		}
		return false;
	}

	private bool CanCraftFromInventoryAndPack(Recipe recipe)
	{
		if (recipe.acceptedGroups.Count > 0 || recipe.Conditions.Any(condition => !condition.IsMet()))
			return false;
		foreach (int requiredTile in recipe.requiredTile) {
			if (requiredTile >= 0 && (requiredTile >= Owner.adjTile.Length || !Owner.adjTile[requiredTile]))
				return false;
		}

		foreach (IGrouping<int, Item> requirement in recipe.requiredItem.Where(item => !item.IsAir)
			.GroupBy(item => item.type)) {
			int needed = requirement.Sum(item => item.stack);
			int available = Profile.ItemCount(requirement.Key);
			foreach (Item owned in Owner.inventory) {
				if (!owned.IsAir && owned.type == requirement.Key)
					available += owned.stack;
			}
			if (available < needed)
				return false;
		}
		return true;
	}
}
