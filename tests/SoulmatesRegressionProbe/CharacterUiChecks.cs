#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Common.UI;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Soulmates.Content.Projectiles;
using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using Terraria.UI;

namespace SoulmatesRegressionProbe;

public sealed partial class EngineChecks
{
	private static void CheckCharacterUi(Action<bool, string> check, TalkModeState state, SoulboundCompanion mate, SoulboundSigil sigil)
	{
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		Type type = typeof(TalkModeState);
		object? Get(string field) => type.GetField(field, flags)!.GetValue(state);
		object? Call(string method, params object[] args) => type.GetMethod(method, flags)!.Invoke(state, args);
		UIElement Button(string field, int index, bool tuple = false) {
			object value = ((IEnumerable)Get(field)!).Cast<object>().ElementAt(index);
			return (UIElement)(tuple ? value.GetType().GetField("Item2")!.GetValue(value)! : value);
		}
		void Click(UIElement expected, string label) {
			Rectangle bounds = expected.GetDimensions().ToRectangle(); Vector2 position = bounds.Center.ToVector2();
			UIElement actual = state.GetElementAt(position);
			check(ReferenceEquals(actual, expected), "Character window wrong click target: " + label);
			actual?.LeftClick(new UIMouseEvent(actual, position));
		}
		Projectile[] previous = Main.projectile;
		float oldScale = Main.UIScale;
		int oldWidth = Main.screenWidth, oldHeight = Main.screenHeight;
		FieldInfo inputWidth = typeof(Terraria.GameInput.PlayerInput).GetField("_originalScreenWidth", BindingFlags.Static | BindingFlags.NonPublic)!;
		FieldInfo inputHeight = typeof(Terraria.GameInput.PlayerInput).GetField("_originalScreenHeight", BindingFlags.Static | BindingFlags.NonPublic)!;
		object oldInputWidth = inputWidth.GetValue(null)!, oldInputHeight = inputHeight.GetValue(null)!;
		try {
			// This fixture runs before the engine finalizes recipe lookup tables.
			typeof(Recipe).GetMethod("CreateRequiredItemQuickLookups", flags | BindingFlags.Static)!.Invoke(null, null);
			Main.projectile = Enumerable.Range(0, previous.Length).Select(i => new Projectile { whoAmI = i }).ToArray();
			foreach (string culture in SupportedCultures) {
				Terraria.Localization.LanguageManager.Instance.SetLanguage(culture);
				foreach (Point viewport in new[] { new Point(800, 600), new Point(1280, 720), new Point(1920, 1080) }) {
					Main.screenWidth = viewport.X; Main.screenHeight = viewport.Y;
					inputWidth.SetValue(null, viewport.X); inputHeight.SetValue(null, viewport.Y);
				foreach (float scale in new[] { 1f, 1.5f, 2f }) {
					Main.UIScale = scale; state.Bind(sigil, mate);
					UIElement root = (UIElement)Get("rootPanel")!;
					foreach (int view in new[] { 0, 1, 2, 3, 0 }) {
						Click(Button("viewButtons", view, true), "view " + view);
						check(Get("characterView")!.ToString() == new[] { "Conversation", "Equipment", "Diagnostics", "Pets" }[view], "Character view click was not wired");
						Rectangle panel = root.GetDimensions().ToRectangle();
						Rectangle reply = ((UIElement)Get("response")!).GetDimensions().ToRectangle();
						check(panel.Contains(reply) && reply.Width >= panel.Width - 30, "Character reply is outside panel or trapped in portrait");
						if (view == 0) {
							foreach (var option in (IEnumerable<UIElement>)Get("optionButtons")!)
								check(option.GetDimensions().ToRectangle().Bottom <= reply.Top, "Conversation options collide with reply");
						}
						else {
							Rectangle body = ((UIElement)Get(view == 1 ? "equipmentPanel" : view == 3 ? "petPanel" : "diagnosticText")!).GetDimensions().ToRectangle();
							check(panel.Contains(body) && body.Bottom <= reply.Top, "Character body collides with reply or exceeds viewport");
							check(!((UIElement)Get("equipmentPanel")!).ContainsPoint(Button("viewButtons", 0, true).GetDimensions().ToRectangle().Center.ToVector2()), "Equipment steals view-tab input");
							if (view == 1) {
								foreach (UIElement pet in ((IEnumerable)Get("petButtons")!).Cast<UIElement>())
									foreach (UIElement cargo in ((IEnumerable)Get("cargoButtons")!).Cast<UIElement>())
										check(!pet.GetDimensions().ToRectangle().Intersects(cargo.GetDimensions().ToRectangle()), "Pet and cargo buttons overlap at " + viewport + "/" + scale);
							}
							if (view == 3) {
								foreach (var tab in ((IEnumerable)Get("viewButtons")!).Cast<object>())
									foreach (var task in ((IEnumerable)Get("taskButtons")!).Cast<object>())
										check(!((UIElement)tab.GetType().GetField("Item2")!.GetValue(tab)!).GetDimensions().ToRectangle()
											.Intersects(((UIElement)task.GetType().GetField("Item2")!.GetValue(task)!).GetDimensions().ToRectangle()), "Character view and task buttons overlap");
							}
						}
					}
				}
				}
			}
			Main.screenWidth = 1280; Main.screenHeight = 720; inputWidth.SetValue(null, 1280); inputHeight.SetValue(null, 720);
			Main.UIScale = 1f; Terraria.Localization.LanguageManager.Instance.SetLanguage("en-US"); state.Bind(sigil, mate);
			mate.Profile.ClearCargo(); mate.Profile.Store(new Item(ItemID.ZephyrFish)); mate.Profile.Store(new Item(ItemID.Nectar));
			Click(Button("viewButtons", 1, true), "equipment");
			Click(Button("petButtons", 0), "Pet inventory");
			check(Get("characterView")!.ToString() == "Pets", "Equipment pet shortcut does not open dedicated pet management");
			UIElement pack = (UIElement)Get("petInventory")!;
			check(pack.GetType().GetField("selectedStorage", flags)!.GetValue(pack)!.Equals(CompanionStorage.Pets), "Pet inventory button opens the wrong cargo");
			void PetClick(int slot, bool right) {
				Rectangle bounds = (Rectangle)pack.GetType().GetMethod("SlotBounds", flags)!.Invoke(pack, new object[] { slot })!;
				Vector2 position = bounds.Center.ToVector2(); UIElement actual = state.GetElementAt(position);
				check(ReferenceEquals(actual, pack), "Pet cargo click is captured by another control");
				if (right) actual?.RightClick(new UIMouseEvent(actual, position)); else actual?.LeftClick(new UIMouseEvent(actual, position));
			}
			PetClick(0, false);
			check(mate.Profile.PetItemType == ItemID.ZephyrFish && Main.projectile.Count(p => p.active && p.ModProjectile is CompanionFamiliar) == 1, "Character pet slot failed to summon exactly one owned pet");
			PetClick(1, false);
			check(mate.Profile.PetItemType == ItemID.Nectar && Main.projectile.Count(p => p.active && p.ModProjectile is CompanionFamiliar) == 1, "Character pet switch left a duplicate");
			Call("RefreshPetSources");
			string PetStatus() => (string)Get("petStatusLabel")!.GetType().GetField("text", flags)!.GetValue(Get("petStatusLabel"))!;
			check(PetStatus().Contains(Lang.GetItemNameValue(ItemID.Nectar)) && PetStatus().StartsWith("Summoned:"), "Pet status does not show the actual summoned item");
			foreach (Projectile pet in Main.projectile.Where(p => p.active && p.ModProjectile is CompanionFamiliar)) pet.Kill();
			Call("RefreshPetSources");
			check(PetStatus().StartsWith("Waiting for pet:"), "Selected item without a familiar falsely appears summoned");
			PetClick(1, false);
			Click((UIElement)Get("petDismissButton")!, "dismiss slot");
			check(mate.Profile.PetItemType == 0 && mate.Profile.ItemCount(ItemID.Nectar) == 1 && mate.Profile.ItemCount(ItemID.ZephyrFish) == 1, "Character dismiss consumed or lost actual items");
			Call("RefreshPetSources"); check(PetStatus() == SoulmatesText.Get("Pets.StoredOnly"), "Dismissed pet still looks summoned");
			Item[] beforeReturn = Main.LocalPlayer.inventory.Select(item => item.Clone()).ToArray();
			PetClick(1, true);
			check(mate.Profile.ItemCount(ItemID.Nectar) == 0 && mate.Profile.PetItemType == 0, "Pet right-click did not return its real item");
			check(Main.LocalPlayer.inventory.Where(item => item.type == ItemID.Nectar).Sum(item => item.stack)
				== beforeReturn.Where(item => item.type == ItemID.Nectar).Sum(item => item.stack) + 1, "Pet right-click lost its real item");
			Main.LocalPlayer.inventory = beforeReturn;
			Item[] oldInventory = Main.LocalPlayer.inventory.Select(item => item.Clone()).ToArray();
			try {
				Main.LocalPlayer.inventory[25] = new Item(ItemID.CompanionCube);
				Main.LocalPlayer.inventory[26] = new Item(ItemID.Carrot) { favorited = true };
				Main.LocalPlayer.inventory[27] = new Item(ItemID.CopperOre);
				Call("RefreshPetSources"); state.Recalculate();
				check(((IEnumerable)Get("petSourceItems")!).Cast<object>().Count() == 1, "Pet picker includes favorites or non-pet resources");
				Click(Button("petSourceButtons", 0), "give pet from non-hotbar inventory");
				check(Main.LocalPlayer.inventory[25].IsAir && mate.Profile.ItemCount(ItemID.CompanionCube) == 1
					&& Main.LocalPlayer.inventory[26].favorited && Main.LocalPlayer.selectedItem != 25, "Pet picker failed real transfer or changed hotbar/favorites");
				Main.LocalPlayer.inventory[25] = new Item(ItemID.CompanionCube); Call("RefreshPetSources");
				Main.LocalPlayer.inventory[25] = new Item(ItemID.CopperOre);
				Click(Button("petSourceButtons", 0), "stale inventory pet");
				check(Main.LocalPlayer.inventory[25].type == ItemID.CopperOre && mate.Profile.ItemCount(ItemID.CompanionCube) == 1, "Stale picker moved a replacement item");
				Main.LocalPlayer.inventory[25] = new Item(ItemID.CompanionCube); Call("RefreshPetSources");
				Guid originalId = mate.Profile.Id; mate.Profile.Id = Guid.NewGuid();
				Click(Button("petSourceButtons", 0), "replaced companion identity");
				check(!Main.LocalPlayer.inventory[25].IsAir && !state.HasActiveBinding, "Stale pet panel gave items to a replacement identity");
				mate.Profile.Id = originalId; Main.LocalPlayer.inventory[25] = new Item(ItemID.CopperOre);
				int[] catalog = CompanionPets.SupportedItems.Take(13).ToArray();
				for (int i = 0; i < catalog.Length; i++) Main.LocalPlayer.inventory[10 + i] = new Item(catalog[i]);
				Call("RefreshPetSources"); state.Recalculate();
				Click((UIElement)Get("petSourcePager")!, "pet source next page");
				check((int)Get("petSourcePage")! == 1, "Player pet inventory cannot page");
				Click(Button("petSourceButtons", 0), "give pet from second page");
				check(Main.LocalPlayer.inventory[16].IsAir, "Second pet page dispatched the wrong inventory slot");
			}
			finally { Main.LocalPlayer.inventory = oldInventory; Call("RefreshPetSources"); }
			Click(Button("taskButtons", 0, true), "pause"); check(mate.Profile.WorkPaused, "Character pause button does nothing");
			Click(Button("taskButtons", 1, true), "resume"); check(!mate.Profile.WorkPaused, "Character resume button does nothing");
			Click(Button("taskButtons", 2, true), "abort"); check(mate.Profile.WorkPaused && mate.CurrentJob == CompanionJob.None, "Character abort button does nothing");
			Click(Button("viewButtons", 2, true), "diagnostics");
			string diagnosis = (string)((object)Get("diagnosticText")!).GetType().GetField("text", flags)!.GetValue(Get("diagnosticText"))!;
			check(diagnosis.Contains("single-player authority") && diagnosis.Contains("Critters:") && diagnosis.Contains("Own familiar count:"), "Diagnostic tab has no live brain/critter/pet state");
			Click((UIElement)Get("exportButton")!, "export");
			string report = System.IO.File.ReadAllText(System.IO.Path.Combine(Main.SavePath, "SoulmatesFeedback", "diagnostic-latest.txt"));
			check(report.Contains("Soulmates " + mate.Mod.Version) && !report.Contains(Main.LocalPlayer.name), "Local diagnosis absent, wrong version or exposes player name");
			Call("ControlTask", CompanionQuickAction.Resume);
			void Set(string field, object value) => type.GetField(field, flags)!.SetValue(state, value);
			Set("awaitingResponse", true); Set("awaitingEquipmentResponse", true); Set("expectedPetResponse", ItemID.Nectar);
			CompanionProfile acknowledgement = mate.Profile.Clone(); acknowledgement.Id = Guid.NewGuid(); acknowledgement.PetItemType = ItemID.Nectar;
			Call("ReceiveEquipmentProfile", acknowledgement, "wrong identity");
			check((bool)Get("awaitingResponse")!, "Equipment accepted another Sigil's response");
			acknowledgement.Id = mate.Profile.Id; acknowledgement.PetItemType = ItemID.ZephyrFish;
			Call("ReceiveEquipmentProfile", acknowledgement, "stale selection");
			check((bool)Get("awaitingResponse")!, "Equipment accepted a stale pet selection");
			acknowledgement.PetItemType = ItemID.Nectar; Call("ReceiveEquipmentProfile", acknowledgement, "selected");
			check(!(bool)Get("awaitingResponse")! && sigil.Profile.PetItemType == ItemID.Nectar, "Equipment acknowledgement leaves input locked");
			Set("awaitingResponse", true); Set("awaitingTaskResponse", true);
			acknowledgement.WorkPaused = true; Call("ReceiveTaskResponse", acknowledgement, "paused", true);
			check(!(bool)Get("awaitingResponse")! && mate.Profile.WorkPaused, "Task acknowledgement failed to update character/input state");
			Call("ControlTask", CompanionQuickAction.Resume);
			CheckLiveShowcase(check, state, mate, sigil);
		}
		finally {
			Main.projectile = previous; Main.UIScale = oldScale;
			Main.screenWidth = oldWidth; Main.screenHeight = oldHeight;
			inputWidth.SetValue(null, oldInputWidth); inputHeight.SetValue(null, oldInputHeight);
		}
	}
}
