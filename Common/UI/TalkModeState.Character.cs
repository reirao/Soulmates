#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Soulmates.Common.Dialogue;
using Soulmates.Common.Feedback;
using Soulmates.Content.Items;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Soulmates.Common.UI;

public sealed partial class TalkModeState
{
	internal enum CharacterView { Conversation, Equipment, Diagnostics, Pets }
	private CharacterView characterView;
	private readonly List<(CharacterView View, TalkItemButton Button)> viewButtons = [];
	private readonly List<TalkItemButton> petButtons = [];
	private readonly List<CompanionEquipmentSlot> equipmentSlots = [];
	private readonly List<(CompanionQuickAction Action, TalkIconButton Button)> taskButtons = [];
	private UIElement? equipmentPanel;
	private UIElement? equipmentContents;
	private CompanionResponseElement? diagnosticText;
	private TalkItemButton? exportButton;
	private readonly List<TalkItemButton> cargoButtons = [];
	private float equipmentScroll;
	private float equipmentScrollMaximum;
	private int diagnosticRefresh;
	private bool awaitingEquipmentResponse;
	private bool awaitingTaskResponse;
	private int expectedPetResponse = -1;
	private void InitializeCharacterViews()
	{
		foreach (CharacterView view in Enum.GetValues<CharacterView>()) {
			var button = new TalkItemButton(view switch {
				CharacterView.Conversation => ItemID.Book,
				CharacterView.Equipment => ItemID.Toolbox,
				CharacterView.Pets => ItemID.Carrot,
				_ => ItemID.Wrench
			});
			button.OnLeftClick += (_, _) => { if (!awaitingResponse) SelectCharacterView(view); };
			viewButtons.Add((view, button)); rootPanel!.Append(button);
		}
		foreach (CompanionQuickAction action in new[] { CompanionQuickAction.Pause, CompanionQuickAction.Resume, CompanionQuickAction.Abort }) {
			var button = new TalkIconButton(action == CompanionQuickAction.Pause ? EmoteID.EmoteSleep
				: action == CompanionQuickAction.Resume ? EmoteID.EmoteRun : EmoteID.EmoteScowl);
			button.OnLeftClick += (_, _) => ControlTask(action);
			taskButtons.Add((action, button)); rootPanel!.Append(button);
		}
		equipmentPanel = new UIElement { OverflowHidden = true }; equipmentPanel.SetPadding(0);
		equipmentContents = new UIElement(); equipmentContents.SetPadding(0); equipmentPanel.Append(equipmentContents);
		equipmentPanel.OnScrollWheel += (evt, _) => {
			if (evt.Target == packElement || evt.Target?.Parent == packElement) return;
			equipmentScroll = Math.Clamp(equipmentScroll - Math.Sign(evt.ScrollWheelValue) * 36f, 0f, equipmentScrollMaximum);
			ApplyLayout(force: true);
		};
		foreach (string kind in new[] { "Pickaxe", "Net", "Axe", "Trinket" }) {
			var slot = new CompanionEquipmentSlot(() => EquipmentItem(kind), () => EquipmentLabel(kind));
			slot.OnLeftClick += (_, _) => SetResponse(EquipmentLabel(kind), true);
			if (kind == "Trinket") slot.OnRightClick += (_, _) => RemoveTrinket();
			equipmentSlots.Add(slot); equipmentContents.Append(slot);
		}
		for (int i = 0; i < 2; i++) {
			int choice = i;
			int type = i == 0 ? ItemID.Carrot : ItemID.FallenStar;
			var button = new TalkItemButton(type);
			button.OnLeftClick += (_, _) => { if (choice == 0) OpenPetInventory(); else SelectPetItem(-1); };
			petButtons.Add(button); equipmentContents.Append(button);
		}
		packElement!.Remove(); packLabel!.Remove(); equipmentContents.Append(packElement);
		for (int i = 1; i <= 2; i++) {
			int option = i;
			var button = new TalkItemButton(i == 1 ? ItemID.Safe : ItemID.PiggyBank);
			button.OnLeftClick += (_, _) => { category = TalkCategory.Pack; Speak(option); };
			cargoButtons.Add(button); equipmentContents.Append(button);
		}
		diagnosticText = new CompanionResponseElement { LeftAligned = true, ShowHoverTooltip = false };
		exportButton = new TalkItemButton(ItemID.PaperAirplaneA);
		exportButton.OnLeftClick += (_, _) => ExportDiagnostics();
		InitializePetView();
		SelectCharacterView(CharacterView.Conversation);
	}
	internal void SelectCharacterView(CharacterView view)
	{
		characterView = view; diagnosticRefresh = 0;
		foreach (var entry in categoryButtons) entry.Button.Remove();
		foreach (var button in optionButtons) button.Remove();
		chooseWordsLabel?.Remove(); itemTopics?.Remove(); equipmentPanel?.Remove(); diagnosticText?.Remove(); exportButton?.Remove(); petPanel?.Remove();
		if (view == CharacterView.Conversation) {
			if (category == TalkCategory.Pack) category = TalkCategory.Care;
			foreach (var entry in categoryButtons) rootPanel!.Append(entry.Button);
			foreach (var button in optionButtons) rootPanel!.Append(button);
			if (chooseWordsLabel is not null) rootPanel!.Append(chooseWordsLabel);
			if (category == TalkCategory.Items && itemTopics is not null) rootPanel!.Append(itemTopics);
			if (sigil is not null) RefreshOptions();
		}
		else if (view == CharacterView.Equipment && equipmentPanel is not null) {
			rootPanel!.Append(equipmentPanel);
			// Append does not initialize a subtree that was hidden during the state's first activation.
			equipmentPanel.Activate();
		}
		else if (view == CharacterView.Diagnostics && diagnosticText is not null && exportButton is not null) {
			rootPanel!.Append(diagnosticText); rootPanel.Append(exportButton);
		}
		else if (view == CharacterView.Pets && petPanel is not null) {
			rootPanel!.Append(petPanel); petPanel.Activate(); RefreshPetSources();
		}
		UpdateCharacterViews(); ApplyLayout(force: true);
	}
	private void LayoutCharacterViews(float left, float width, float bottom)
	{
		float size = Math.Min(32f, Math.Max(1f, (width - 24f) / 7f));
		for (int i = 0; i < viewButtons.Count; i++) Place(viewButtons[i].Button, left + i * (size + 4f), 40f, size, size);
		for (int i = 0; i < taskButtons.Count; i++) Place(taskButtons[i].Button, left + width - (3 - i) * (size + 3f), 40f, size, size);
		if (equipmentPanel is not null && equipmentContents is not null) {
			Place(equipmentPanel, left, 82f, width, Math.Max(1f, bottom - 82f));
			float slot = Math.Min(44f, (width - 12f) / 4f);
			float cargoTop = petButtons.Count * (slot + 4f) + 72f <= width ? 54f : 54f + slot + 10f;
			float packTop = Math.Max(108f, cargoTop + 36f);
			float contentHeight = Math.Max(230f, packTop + 116f);
			equipmentScrollMaximum = Math.Max(0f, contentHeight - equipmentPanel.Height.Pixels);
			equipmentScroll = Math.Clamp(equipmentScroll, 0f, equipmentScrollMaximum);
			Place(equipmentContents, 0f, -equipmentScroll, width, contentHeight);
			for (int i = 0; i < equipmentSlots.Count; i++) Place(equipmentSlots[i], i * (slot + 4f), 0f, slot, slot);
			for (int i = 0; i < petButtons.Count; i++) Place(petButtons[i], i * (slot + 4f), 54f, slot, slot);
			Place(packElement!, 0f, packTop, width, 104f);
			for (int i = 0; i < cargoButtons.Count; i++) Place(cargoButtons[i], width - (2 - i) * 32f, cargoTop, 28f, 28f);
		}
		if (diagnosticText is not null) Place(diagnosticText, left, 116f, width, Math.Max(17f, bottom - 116f));
		if (exportButton is not null) Place(exportButton, left, 82f, 28f, 28f);
		LayoutPetView(left, width, bottom);
		static void Place(UIElement element, float x, float y, float w, float h) {
			element.Left.Set(x, 0f); element.Top.Set(y, 0f); element.Width.Set(w, 0f); element.Height.Set(h, 0f);
		}
	}
	private void RefreshCharacterLabels()
	{
		foreach (var entry in viewButtons) entry.Button.HoverText = SoulmatesText.Get($"UI.Character.{entry.View}");
		foreach (var entry in taskButtons) entry.Button.HoverText = SoulmatesText.EnumName(entry.Action);
		for (int i = 0; i < cargoButtons.Count; i++) cargoButtons[i].HoverText = CompanionDialogueEngine.GetOptions(TalkCategory.Pack)[i + 1];
		if (exportButton is not null) exportButton.HoverText = SoulmatesText.Get("UI.Character.Export");
		RefreshPetLabels();
	}
	private void UpdateCharacterViews()
	{
		foreach (var entry in viewButtons) entry.Button.Selected = entry.View == characterView;
		if (companion is null) return;
		foreach (var entry in taskButtons) entry.Button.Selected = entry.Action == CompanionQuickAction.Pause && companion.Profile.WorkPaused;
		for (int i = 0; i < petButtons.Count; i++) {
			petButtons[i].Selected = i == 0 && companion.Profile.PetItemType != 0;
			petButtons[i].HoverText = SoulmatesText.Get(i == 0 ? "Pets.Inventory" : "Pets.Dismiss");
		}
		if (characterView == CharacterView.Diagnostics && diagnosticRefresh-- <= 0) {
			diagnosticRefresh = 60; diagnosticText?.SetText(companion.DiagnosticText(), resetScroll: false);
		}
		if (characterView == CharacterView.Pets) RefreshPetSources();
	}
	private Item? EquipmentItem(string kind)
	{
		if (companion is null) return null;
		var items = Main.LocalPlayer.inventory.Concat(companion.Profile.CarriedItems);
		return kind switch {
			"Pickaxe" => companion.Profile.Pack.Where(i => !i.IsAir && i.pick > 0).OrderByDescending(i => i.pick).FirstOrDefault(),
			"Axe" => items.Where(i => !i.IsAir && i.axe > 0).OrderByDescending(i => i.axe).FirstOrDefault(),
			"Net" => companion.Profile.CarriedItems.Where(i => !i.IsAir && ItemID.Sets.CatchingTool[i.type])
				.OrderByDescending(i => ItemID.Sets.LavaproofCatchingTool[i.type]).FirstOrDefault() ?? new Item(ItemID.BugNet),
			"Trinket" => companion.Profile.Trinket switch {
				CompanionTrinket.StarfinderBell => new Item(ModContent.ItemType<StarfinderBell>()),
				CompanionTrinket.DelverCharm => new Item(ModContent.ItemType<DelverCharm>()),
				CompanionTrinket.HearthRibbon => new Item(ModContent.ItemType<HearthRibbon>()),
				_ => null
			},
			_ => null
		};
	}
	private string EquipmentLabel(string kind)
	{
		Item? item = EquipmentItem(kind);
		if (kind == "Pickaxe" && item is null && companion is not null)
			return SoulmatesText.Get("UI.Character.LearnedPick", Math.Max(35, companion.Profile.ObservedPickPower));
		return SoulmatesText.Get($"UI.Character.Slots.{kind}") + ": " + (item?.Name ?? SoulmatesText.Get("UI.Character.Empty"));
	}
	internal void OpenPetInventory()
	{
		if (awaitingResponse) return;
		petSourcePage = 0; petScroll = 0f;
		SelectCharacterView(CharacterView.Pets);
	}
	private void SelectPetItem(int slot)
	{
		if (companion is null || awaitingResponse || !HasActiveBinding) return;
		if (slot < -1 || slot >= companion.Profile.PetItems.Count) return;
		int type = slot < 0 ? 0 : companion.Profile.PetItems[slot].type;
		if (Main.netMode == NetmodeID.MultiplayerClient) {
			expectedPetResponse = type;
			awaitingResponse = awaitingEquipmentResponse = true; responseWaitTicks = 0; global::Soulmates.Soulmates.SendPetConfigRequest(companion.Profile, slot);
		}
		else {
			bool ok = companion.ConfigurePet(slot, type, slot < 0 ? new byte[32] : companion.Profile.StorageToken(CompanionStorage.Pets, slot));
			SetResponse(SoulmatesText.Get(ok ? type == 0 ? "Pets.Dismiss" : "Pets.Selected" : "TargetOrders.Invalid", Lang.GetItemNameValue(type)), ok);
		}
	}
	private void RemoveTrinket()
	{
		if (companion is null || awaitingResponse) return;
		if (Main.netMode == NetmodeID.MultiplayerClient) {
			expectedPetResponse = -1;
			awaitingResponse = awaitingEquipmentResponse = true; responseWaitTicks = 0; global::Soulmates.Soulmates.SendTrinketRequest(companion.Profile.Id, CompanionTrinket.None);
		}
		else companion.EquipTrinket(CompanionTrinket.None);
	}
	private void ControlTask(CompanionQuickAction action)
	{
		if (companion is null || awaitingResponse) return;
		if (Main.netMode == NetmodeID.MultiplayerClient) {
			awaitingResponse = awaitingTaskResponse = true; responseWaitTicks = 0; global::Soulmates.Soulmates.SendQuickActionRequest(companion.Profile.Id, action);
		}
		else { var result = companion.PerformQuickAction(action); SetResponse(result.Reply, result.Accepted); }
		SoundEngine.PlaySound(SoundID.MenuTick);
	}
	internal void ReceiveEquipmentProfile(CompanionProfile profile, string message)
	{
		if (!awaitingEquipmentResponse || sigil?.Profile.Id != profile.Id) return;
		if (expectedPetResponse >= 0 ? profile.PetItemType != expectedPetResponse : profile.Trinket != CompanionTrinket.None) return;
		awaitingEquipmentResponse = false;
		ReceiveNetworkResponse(profile, string.IsNullOrEmpty(message) ? SoulmatesText.Get("UI.Character.Equipment") : message, true);
	}
	internal void ReceiveTaskResponse(CompanionProfile profile, string reply, bool accepted)
	{
		if (awaitingTaskResponse && sigil?.Profile.Id == profile.Id) ReceiveNetworkResponse(profile, reply, accepted);
	}
	private void ResetCharacterResponse()
	{
		awaitingEquipmentResponse = awaitingTaskResponse = false;
		expectedPetResponse = -1;
	}
	private void ExportDiagnostics()
	{
		if (companion is null) return;
		try {
			string directory = SoulmatesFeedbackSystem.FeedbackFolder;
			Directory.CreateDirectory(directory);
			File.WriteAllText(Path.Combine(directory, "diagnostic-latest.txt"), companion.DiagnosticText(), new UTF8Encoding(false));
			SetResponse(SoulmatesText.Get("UI.Character.Exported"), true);
		}
		catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
			SetResponse(SoulmatesText.Get("UI.Character.ExportFailed"), false);
		}
	}
}

internal sealed class CompanionEquipmentSlot(Func<Item?> item, Func<string> label) : UIElement
{
	private Item? displayedItem;
	private string displayedLabel = "";
	private int refresh;
	public override void Update(GameTime gameTime)
	{
		base.Update(gameTime);
		if (refresh-- > 0) return;
		refresh = 30; displayedItem = item(); displayedLabel = label();
	}
	protected override void DrawSelf(SpriteBatch spriteBatch)
	{
		Rectangle area = GetDimensions().ToRectangle();
		spriteBatch.Draw(TextureAssets.InventoryBack.Value, area, IsMouseHovering ? Color.White : Color.White * 0.85f);
		Item? value = displayedItem;
		if (value is not null && !value.IsAir) {
			Main.instance.LoadItem(value.type);
			Texture2D texture = TextureAssets.Item[value.type].Value;
			Rectangle frame = Main.itemAnimations[value.type]?.GetFrame(texture) ?? texture.Bounds;
			float scale = Math.Min(area.Width * 0.7f / frame.Width, area.Height * 0.7f / frame.Height);
			spriteBatch.Draw(texture, area.Center.ToVector2(), frame, Color.White, 0f, frame.Size() * 0.5f, scale, SpriteEffects.None, 0f);
		}
		if (IsMouseHovering) { Main.LocalPlayer.mouseInterface = true; Main.hoverItemName = displayedLabel; }
	}
}
