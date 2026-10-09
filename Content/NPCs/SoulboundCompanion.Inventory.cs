#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BigInteger = System.Numerics.BigInteger;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Soulmates.Common;
using Soulmates.Common.Feedback;
using Soulmates.Common.Dialogue;
using Soulmates.Common.UI;
using Soulmates.Content.Items;
using Soulmates.Content.Projectiles;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulmates.Content.NPCs;

public sealed partial class SoulboundCompanion
{
	public string StoreSelectedItem()
	{
		if (CompanionInventorySync.IsPending(Owner)) return SoulmatesText.Get("TargetOrders.TargetLost");
		using var inventorySync = new CompanionInventorySync(Owner);
		Item selected = Owner.inventory[Owner.selectedItem];
		return StoreInventoryItem(selected);
	}

	internal bool StorePetItem(int slot, byte[] token, out string reply)
	{
		reply = SoulmatesText.Get("TargetOrders.TargetLost");
		if (Main.netMode == NetmodeID.MultiplayerClient || !NPC.active || !Owner.active || Owner.dead
			|| FindBoundSigil() is null || CompanionInventorySync.IsPending(Owner)
			|| slot < 0 || slot >= 50 || token.Length != 32) return false;
		Item item = Owner.inventory[slot];
		if (item.favorited || !CompanionPets.IsPetItem(item)
			|| !CompanionInventorySync.Token(item).AsSpan().SequenceEqual(token)) return false;
		using var inventorySync = new CompanionInventorySync(Owner);
		int before = item.stack;
		reply = StoreInventoryItem(item);
		return item.stack < before || item.IsAir;
	}

	private string StoreInventoryItem(Item selected)
	{
		if (selected.IsAir)
			return SoulmatesText.Get("Pack.SelectedEmpty");
		if (!CanCarry(selected))
			return SoulmatesText.Get("Pack.CannotCarry");
		CompanionStorage storage = CompanionProfile.StorageFor(selected);
		int carryLimit = Profile.CarryLimitFor(selected);
		if (storage != CompanionStorage.Wallet && Profile.ItemCount(selected.type) >= carryLimit)
			return SoulmatesText.Get("Pack.ItemLimit", selected.Name, carryLimit);
		int itemType = selected.type;
		bool firstPet = storage == CompanionStorage.Pets && Profile.PetItems.Count == 0 && Profile.PetItemType == 0;
		int moved = Profile.Store(selected);
		if (moved <= 0)
			return SoulmatesText.Get(storage == CompanionStorage.Resources ? "Storage.ResourcesFull"
				: storage == CompanionStorage.Pets ? "Pets.Full" : "Pack.Full",
				Profile.PackLoad, Profile.PackCapacity);
		if (firstPet) Profile.PetItemType = itemType;
		UpdateEquippedPet();
		SyncProfileToBoundSigil();
		NPC.netUpdate = true;
		SoulmatesFeedbackSystem.Record("pack_stored", ("item_type", itemType), ("amount", moved),
			("pack_load", Profile.PackLoad), ("pet_selected", firstPet));
		if (firstPet) return SoulmatesText.Get("Pets.Selected", Lang.GetItemNameValue(itemType));
		return SoulmatesText.Get("Storage.Stored", moved, SoulmatesText.EnumName(storage));
	}

	public string UnloadPack()
	{
		if (CompanionInventorySync.IsPending(Owner)) return SoulmatesText.Get("TargetOrders.TargetLost");
		using var inventorySync = new CompanionInventorySync(Owner);
		int moved = 0;
		foreach (List<Item> storage in new[] { Profile.Pack, Profile.Resources, Profile.PetItems }) {
			for (int i = storage.Count - 1; i >= 0; i--) {
				Item stored = storage[i];
				while (!stored.IsAir) {
					int transferred = TransferStoredStack(stored, singleItem: false);
					if (transferred <= 0) break;
					moved += transferred;
				}
				if (stored.IsAir)
					storage.RemoveAt(i);
			}
		}
		for (int slot = 0; slot < 4; slot++)
			moved += TransferWalletCoins(CompanionProfile.WalletCoinType(slot), singleItem: false);
		UpdateEquippedPet();
		SyncProfileToBoundSigil();
		NPC.netUpdate = true;
		SoulmatesFeedbackSystem.Record("pack_unloaded", ("amount", moved), ("pack_load", Profile.PackLoad));
		return moved > 0
			? SoulmatesText.Get("Storage.Returned", moved)
			: Profile.PackLoad == 0 && Profile.ResourceLoad == 0 && Profile.PetItems.Count == 0 && Profile.WalletCopper.IsZero
				? SoulmatesText.Get("Pack.AlreadyEmpty") : SoulmatesText.Get("Pack.InventoryFull");
	}

	public string WithdrawPackSlot(int index, bool singleItem)
		=> WithdrawStorageSlot(CompanionStorage.Pack, index, singleItem);

	public string WithdrawStorageSlot(CompanionStorage storageKind, int index, bool singleItem)
	{
		if (CompanionInventorySync.IsPending(Owner)) return SoulmatesText.Get("TargetOrders.TargetLost");
		using var inventorySync = new CompanionInventorySync(Owner);
		if (!Enum.IsDefined(storageKind))
			return SoulmatesText.Get("Pack.SlotEmpty");
		if (storageKind == CompanionStorage.Wallet) {
			int coinType = CompanionProfile.WalletCoinType(index);
			if (Profile.WalletCoins(coinType).IsZero)
				return SoulmatesText.Get("Pack.SlotEmpty");
			int coins = TransferWalletCoins(coinType, singleItem);
			if (coins <= 0)
				return SoulmatesText.Get("Pack.InventoryFull");
			SyncPackState();
			SoulmatesFeedbackSystem.Record("wallet_withdrawn", ("item_type", coinType), ("amount", coins),
				("balance_copper", Profile.WalletCopper.ToString()));
			return SoulmatesText.Get("Storage.Withdrawn", coins, SoulmatesText.EnumName(storageKind));
		}
		List<Item> storage = Profile.StorageItems(storageKind);
		if (index < 0 || index >= storage.Count || storage[index].IsAir)
			return SoulmatesText.Get("Pack.SlotEmpty");

		Item stored = storage[index];
		int itemType = stored.type;
		int moved = TransferStoredStack(stored, singleItem);
		if (moved <= 0)
			return SoulmatesText.Get("Pack.InventoryFull");

		if (stored.stack <= 0)
			storage.RemoveAt(index);
		UpdateEquippedPet();
		SyncProfileToBoundSigil();
		NPC.netUpdate = true;
		SoulmatesFeedbackSystem.Record("pack_withdrawn", ("item_type", itemType), ("amount", moved),
			("pack_load", Profile.PackLoad));
		return SoulmatesText.Get("Storage.Withdrawn", moved, SoulmatesText.EnumName(storageKind));
	}

	private int TransferStoredStack(Item stored, bool singleItem)
	{
		int requested = Math.Min(stored.stack, singleItem ? 1 : Math.Max(1, stored.maxStack));
		Item transfer = stored.Clone();
		transfer.stack = requested;
		Item leftover = Owner.GetItem(Owner.whoAmI, transfer, GetItemSettings.InventoryEntityToPlayerInventorySettings);
		int moved = requested - (leftover.IsAir ? 0 : leftover.stack);
		stored.stack -= moved;
		return moved;
	}

	private int TransferWalletCoins(int itemType, bool singleItem)
	{
		BigInteger available = Profile.WalletCoins(itemType);
		if (available.IsZero)
			return 0;
		var transfer = new Item(itemType);
		int requested = (int)BigInteger.Min(available, singleItem ? 1 : transfer.maxStack);
		transfer.stack = requested;
		Item leftover = Owner.GetItem(Owner.whoAmI, transfer, GetItemSettings.InventoryEntityToPlayerInventorySettings);
		int moved = requested - (leftover.IsAir ? 0 : leftover.stack);
		if (moved > 0)
			Profile.RemoveWalletCoins(itemType, moved);
		return moved;
	}

	private static bool CanCarry(Item item) => item.ModItem is not Soulcore and not SoulboundSigil and not CompanionTrinketItem;

	private bool CanCollectLooseItem(Item item) => AvailableCarryAmount(item) > 0;

	private bool IsEligibleLooseItem(Item item) => item.active && !item.IsAir && item.stack > 0
		&& item.noGrabDelay <= 0
		&& CanCarry(item) && !IsRecoveryPickup(item)
		&& (item.playerIndexTheItemIsReservedFor == 255 || item.playerIndexTheItemIsReservedFor == Owner.whoAmI);

	private int AvailableCarryAmount(Item item)
	{
		if (!IsEligibleLooseItem(item))
			return 0;

		return Profile.GetStorableAmount(item);
	}

	private void ReconcilePackOnce()
	{
		if (packReconciled)
			return;
		packReconciled = true;
		Profile.Normalize();
		var excessItems = new List<Item>();
		Item[] storedTypes = Profile.CarriedItems.Where(item => !item.IsAir)
			.GroupBy(item => item.type).Select(group => group.First()).ToArray();
		foreach (Item item in storedTypes)
			excessItems.AddRange(Profile.ExtractExcess(item.type, Profile.CarryLimitFor(item)));
		if (excessItems.Count == 0) {
			SyncPackState();
			return;
		}

		foreach (Item returned in excessItems) {
			Item leftover = Main.netMode == NetmodeID.Server ? returned.Clone()
				: Owner.GetItem(Owner.whoAmI, returned.Clone(), GetItemSettings.InventoryEntityToPlayerInventorySettings);
			if (leftover.IsAir)
				continue;
			int itemIndex = Item.NewItem(Owner.GetSource_Misc("SoulmatesPackRepair"), Owner.Hitbox, leftover);
			if (Main.netMode == NetmodeID.Server && itemIndex >= 0 && itemIndex < Main.maxItems)
				NetMessage.SendData(MessageID.SyncItem, -1, -1, null, itemIndex);
		}
		SyncPackState();
	}

	public void SyncProfileToBoundSigil()
	{
		if (FindBoundSigil() is { } sigil)
			sigil.Profile = Profile.Clone();
	}

	private void SyncPackState()
	{
		SyncProfileToBoundSigil();
		NPC.netUpdate = true;
		if (Main.netMode == NetmodeID.Server)
			global::Soulmates.Soulmates.SendProfileUpdate(Owner, this);
	}

	public SoulboundSigil? FindBoundSigil()
	{
		if (!TryGetOwner(out Player owner))
			return null;
		foreach (Item item in owner.inventory) {
			if (item.ModItem is SoulboundSigil sigil && sigil.Profile.Id == Profile.Id)
				return sigil;
		}
		if (Main.netMode == NetmodeID.SinglePlayer && owner.whoAmI == Main.myPlayer
			&& Main.mouseItem.ModItem is SoulboundSigil held && held.Profile.Id == Profile.Id)
			return held;
		return null;
	}

}
