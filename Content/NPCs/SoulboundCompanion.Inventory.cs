#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Soulmates.Common;
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
		Item selected = Owner.inventory[Owner.selectedItem];
		if (selected.IsAir)
			return SoulmatesText.Get("Pack.SelectedEmpty");
		if (!CanCarry(selected))
			return SoulmatesText.Get("Pack.CannotCarry");
		int carryLimit = CompanionProfile.CarryLimitFor(selected.type);
		if (Profile.ItemCount(selected.type) >= carryLimit)
			return SoulmatesText.Get("Pack.ItemLimit", selected.Name, carryLimit);
		int moved = Profile.Store(selected);
		if (moved <= 0)
			return SoulmatesText.Get("Pack.Full", Profile.PackLoad, Profile.PackCapacity);
		SyncProfileToBoundSigil();
		NPC.netUpdate = true;
		return SoulmatesText.Get(moved == 1 ? "Pack.StoredOne" : "Pack.StoredMany", moved, Profile.PackLoad, Profile.PackCapacity);
	}

	public string UnloadPack()
	{
		int moved = 0;
		for (int i = Profile.Pack.Count - 1; i >= 0; i--) {
			Item stored = Profile.Pack[i];
			int originalStack = stored.stack;
			Item leftover = Owner.GetItem(Owner.whoAmI, stored.Clone(), GetItemSettings.InventoryEntityToPlayerInventorySettings);
			moved += originalStack - (leftover.IsAir ? 0 : leftover.stack);
			if (leftover.IsAir)
				Profile.Pack.RemoveAt(i);
			else
				Profile.Pack[i] = leftover;
		}
		SyncProfileToBoundSigil();
		NPC.netUpdate = true;
		return moved > 0
			? SoulmatesText.Get(moved == 1 ? "Pack.ReturnedOne" : "Pack.ReturnedMany", moved, Profile.PackLoad, Profile.PackCapacity)
			: Profile.PackLoad == 0 ? SoulmatesText.Get("Pack.AlreadyEmpty") : SoulmatesText.Get("Pack.InventoryFull");
	}

	public string WithdrawPackSlot(int index, bool singleItem)
	{
		if (index < 0 || index >= Profile.Pack.Count || Profile.Pack[index].IsAir)
			return SoulmatesText.Get("Pack.SlotEmpty");

		Item stored = Profile.Pack[index];
		int requested = singleItem ? 1 : stored.stack;
		Item transfer = stored.Clone();
		transfer.stack = requested;
		Item leftover = Owner.GetItem(Owner.whoAmI, transfer, GetItemSettings.InventoryEntityToPlayerInventorySettings);
		int moved = requested - (leftover.IsAir ? 0 : leftover.stack);
		if (moved <= 0)
			return SoulmatesText.Get("Pack.InventoryFull");

		stored.stack -= moved;
		if (stored.stack <= 0)
			Profile.Pack.RemoveAt(index);
		SyncProfileToBoundSigil();
		SyncOwnerInventory();
		NPC.netUpdate = true;
		return SoulmatesText.Get(moved == 1 ? "Pack.WithdrewOne" : "Pack.WithdrewMany", moved, Profile.PackLoad, Profile.PackCapacity);
	}

	private static bool CanCarry(Item item) => item.ModItem is not Soulcore and not SoulboundSigil and not CompanionTrinketItem;

	private bool CanCollectLooseItem(Item item) => AvailableCarryAmount(item) > 0;

	private int AvailableCarryAmount(Item item)
	{
		if (!item.active || item.IsAir || item.stack <= 0 || !CanCarry(item) || IsRecoveryPickup(item)
			|| item.playerIndexTheItemIsReservedFor != 255 && item.playerIndexTheItemIsReservedFor != Owner.whoAmI)
			return 0;

		return Profile.GetStorableAmount(item);
	}

	private void ReconcilePackOnce()
	{
		if (packReconciled)
			return;
		packReconciled = true;
		var excessItems = new List<Item>();
		int[] storedTypes = Profile.Pack.Where(item => item is not null && !item.IsAir)
			.Select(item => item.type).Distinct().ToArray();
		foreach (int itemType in storedTypes)
			excessItems.AddRange(Profile.ExtractExcess(itemType, CompanionProfile.CarryLimitFor(itemType)));
		if (excessItems.Count == 0)
			return;

		foreach (Item returned in excessItems) {
			Item leftover = Owner.GetItem(Owner.whoAmI, returned.Clone(), GetItemSettings.InventoryEntityToPlayerInventorySettings);
			if (leftover.IsAir)
				continue;
			int itemIndex = Item.NewItem(Owner.GetSource_Misc("SoulmatesPackRepair"), Owner.Hitbox, leftover);
			if (Main.netMode == NetmodeID.Server && itemIndex >= 0 && itemIndex < Main.maxItems)
				NetMessage.SendData(MessageID.SyncItem, -1, -1, null, itemIndex);
		}
		SyncPackState();
		SyncOwnerInventory();
	}

	public void SyncProfileToBoundSigil()
	{
		foreach (Item item in Owner.inventory) {
			if (item.ModItem is SoulboundSigil sigil && sigil.Profile.Id == Profile.Id) {
				sigil.Profile = Profile.Clone();
				return;
			}
		}
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
		foreach (Item item in Owner.inventory) {
			if (item.ModItem is SoulboundSigil sigil && sigil.Profile.Id == Profile.Id)
				return sigil;
		}
		return null;
	}

}
