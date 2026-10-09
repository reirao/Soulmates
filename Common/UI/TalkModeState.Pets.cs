#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.UI;

namespace Soulmates.Common.UI;

public sealed partial class TalkModeState
{
	private UIElement? petPanel;
	private UIElement? petContents;
	private CompanionPackElement? petInventory;
	private CompanionResponseElement? petSourceLabel;
	private CompanionResponseElement? petEmptyLabel;
	private CompanionResponseElement? petStatusLabel;
	private TalkItemButton? petSourcePager;
	private TalkItemButton? petDismissButton;
	private readonly List<TalkItemButton> petSourceButtons = [];
	private readonly List<(int Slot, byte[] Token)> petSourceItems = [];
	private int petSourcePage;
	private float petScroll;
	private float petScrollMaximum;
	private const int PetSourcePageSize = 6;
	private int PetSourcePageCount => Math.Max(1, (petSourceItems.Count + PetSourcePageSize - 1) / PetSourcePageSize);

	private void InitializePetView()
	{
		petPanel = new UIElement { OverflowHidden = true }; petPanel.SetPadding(0f);
		petContents = new UIElement(); petContents.SetPadding(0f); petPanel.Append(petContents);
		petPanel.OnScrollWheel += (evt, _) => {
			if (evt.Target == petInventory || evt.Target?.Parent == petInventory) return;
			petScroll = Math.Clamp(petScroll - Math.Sign(evt.ScrollWheelValue) * 36f, 0f, petScrollMaximum);
			ApplyLayout(force: true);
		};
		petSourceLabel = new CompanionResponseElement { LeftAligned = true, ShowHoverTooltip = false };
		petEmptyLabel = new CompanionResponseElement { LeftAligned = true, ShowHoverTooltip = false };
		petContents.Append(petSourceLabel); petContents.Append(petEmptyLabel);
		petStatusLabel = new CompanionResponseElement { LeftAligned = true, ShowHoverTooltip = false };
		petContents.Append(petStatusLabel);
		petSourcePager = new TalkItemButton(ItemID.Book);
		petSourcePager.OnLeftClick += (_, _) => ChangePetSourcePage(1);
		petSourcePager.OnRightClick += (_, _) => ChangePetSourcePage(-1);
		petContents.Append(petSourcePager);
		for (int i = 0; i < PetSourcePageSize; i++) {
			int index = i;
			var button = new TalkItemButton(0);
			button.OnLeftClick += (_, _) => GivePetItem(index);
			petSourceButtons.Add(button); petContents.Append(button);
		}
		petInventory = new CompanionPackElement(() => DisplayProfile, WithdrawStorageSlot) { PetsOnly = true };
		petInventory.SelectStorage(CompanionStorage.Pets); petContents.Append(petInventory);
		petDismissButton = new TalkItemButton(ItemID.FallenStar);
		petDismissButton.OnLeftClick += (_, _) => SelectPetItem(-1);
		petContents.Append(petDismissButton);
	}

	private void LayoutPetView(float left, float width, float bottom)
	{
		if (petPanel is null || petContents is null) return;
		Place(petPanel, left, 82f, width, Math.Max(1f, bottom - 82f));
		petScrollMaximum = Math.Max(0f, 258f - petPanel.Height.Pixels);
		petScroll = Math.Clamp(petScroll, 0f, petScrollMaximum);
		Place(petContents, 0f, -petScroll, width, 258f);
		Place(petSourceLabel!, 0f, 0f, Math.Max(1f, width - 34f), 20f);
		Place(petSourcePager!, width - 28f, 0f, 28f, 28f);
		Place(petEmptyLabel!, 0f, 32f, width, 68f);
		float slot = Math.Min(40f, (width - 8f) / 3f);
		for (int i = 0; i < petSourceButtons.Count; i++)
			Place(petSourceButtons[i], i % 3 * (slot + 4f), 32f + i / 3 * (slot + 4f), slot, slot);
		Place(petDismissButton!, width - 28f, 124f, 28f, 28f);
		Place(petStatusLabel!, 0f, 122f, Math.Max(1f, width - 34f), 34f);
		Place(petInventory!, 0f, 158f, width, 100f);
		static void Place(UIElement element, float x, float y, float w, float h) {
			element.Left.Set(x, 0f); element.Top.Set(y, 0f); element.Width.Set(w, 0f); element.Height.Set(h, 0f);
		}
	}

	private void RefreshPetLabels()
	{
		petSourceLabel?.SetText(SoulmatesText.Get("Pets.YourItems"));
		petEmptyLabel?.SetText(SoulmatesText.Get("Pets.NoItems"));
		if (petSourcePager is not null) petSourcePager.HoverText = SoulmatesText.Get("Storage.PageHint");
		if (petDismissButton is not null) petDismissButton.HoverText = SoulmatesText.Get("Pets.Dismiss");
	}

	private void RefreshPetSources()
	{
		if (petStatusLabel is not null && companion is not null) {
			string status = companion.Profile.PetItemType == 0 ? SoulmatesText.Get("Pets.StoredOnly")
				: SoulmatesText.Get(companion.HasEquippedPet ? "Pets.Active" : "Pets.Waiting",
					Lang.GetItemNameValue(companion.Profile.PetItemType));
			petStatusLabel.SetText(status, resetScroll: false);
		}
		petSourceItems.Clear();
		foreach (int slot in Enumerable.Range(0, Math.Min(50, Main.LocalPlayer.inventory.Length))) {
			Item item = Main.LocalPlayer.inventory[slot];
			if (!item.favorited && CompanionPets.IsPetItem(item)) petSourceItems.Add((slot, CompanionInventorySync.Token(item)));
		}
		petSourcePage = Math.Clamp(petSourcePage, 0, PetSourcePageCount - 1);
		for (int i = 0; i < petSourceButtons.Count; i++) {
			int index = petSourcePage * PetSourcePageSize + i;
			TalkItemButton button = petSourceButtons[i];
			bool present = index < petSourceItems.Count;
			button.ItemType = present ? Main.LocalPlayer.inventory[petSourceItems[index].Slot].type : 0;
			button.HoverText = present ? SoulmatesText.Get("Pets.Give", Lang.GetItemNameValue(button.ItemType)) : "";
			button.IgnoresMouseInteraction = !present || awaitingResponse;
			if (present && button.Parent is null) petContents!.Append(button);
			if (!present) button.Remove();
		}
		if (petEmptyLabel is not null) {
			petEmptyLabel.Remove();
			if (petSourceItems.Count == 0) petContents!.Append(petEmptyLabel);
		}
		if (petSourcePager is not null) {
			petSourcePager.BadgeText = $"{petSourcePage + 1}/{PetSourcePageCount}";
			petSourcePager.IgnoresMouseInteraction = PetSourcePageCount <= 1 || awaitingResponse;
		}
	}

	private void ChangePetSourcePage(int direction)
	{
		if (awaitingResponse) return;
		petSourcePage = (petSourcePage + direction + PetSourcePageCount) % PetSourcePageCount;
		RefreshPetSources();
	}

	private void GivePetItem(int index)
	{
		int source = petSourcePage * PetSourcePageSize + index;
		if (awaitingResponse || !HasActiveBinding || index < 0 || index >= PetSourcePageSize
			|| source >= petSourceItems.Count) return;
		var item = petSourceItems[source];
		if (Main.netMode == NetmodeID.MultiplayerClient) {
			awaitingResponse = true; responseWaitTicks = 0;
			global::Soulmates.Soulmates.SendPetStoreRequest(companion!.Profile.Id, item.Slot, item.Token);
		}
		else {
			bool accepted = companion!.StorePetItem(item.Slot, item.Token, out string reply);
			SetResponse(reply, accepted);
		}
		RefreshPetSources(); Recalculate();
	}
}
