#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using ReLogic.Content;
using ReLogic.Graphics;
using Soulmates.Common;
using Soulmates.Common.Dialogue;
using Soulmates.Common.UI;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace SoulmatesRegressionProbe;

public sealed partial class EngineChecks
{
	private static void CheckTalkUiRouting(Action<bool, string> check)
	{
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
		var oldFont = FontAssets.MouseText;
		var oldDeathFont = FontAssets.DeathText;
		Player oldPlayer = Main.player[0];
		NPC oldNpc = Main.npc[0];
		bool oldMenu = Main.gameMenu, oldServer = Main.dedServ;
		int oldLocal = Main.myPlayer, oldMode = Main.netMode;
		var oldInterface = UserInterface.ActiveInstance;
		int oldWidth = Main.screenWidth, oldHeight = Main.screenHeight;
		FieldInfo inputWidth = typeof(PlayerInput).GetField("_originalScreenWidth", BindingFlags.Static | BindingFlags.NonPublic)!;
		FieldInfo inputHeight = typeof(PlayerInput).GetField("_originalScreenHeight", BindingFlags.Static | BindingFlags.NonPublic)!;
		object oldInputWidth = inputWidth.GetValue(null)!, oldInputHeight = inputHeight.GetValue(null)!;
		string oldCulture = Terraria.Localization.Language.ActiveCulture.Name;
		try {
			// Measurement-only font: native UI layout/events run without inventing a graphical playtest.
			var font = new DynamicSpriteFont(0f, 16, '?');
			Type glyphType = typeof(DynamicSpriteFont).GetNestedType("SpriteCharacterData", flags)!;
			object glyph = Activator.CreateInstance(glyphType, flags, null, new object[] {
				null!, new Rectangle(0, 0, 8, 16), new Rectangle(0, 0, 8, 16), new Vector3(0, 8, 0)
			}, null)!;
			typeof(DynamicSpriteFont).GetField("_defaultCharacterData", flags)!.SetValue(font, glyph);
			var asset = (Asset<DynamicSpriteFont>)Activator.CreateInstance(typeof(Asset<DynamicSpriteFont>), flags,
				null, new object[] { "UI measurement fixture" }, null)!;
			typeof(Asset<DynamicSpriteFont>).GetMethod("SubmitLoadedContent", flags)!.Invoke(asset, new object[] { font, null! });
			FontAssets.MouseText = FontAssets.DeathText = asset;
			Main.dedServ = false; Main.gameMenu = false; Main.myPlayer = 0; Main.netMode = NetmodeID.SinglePlayer;
			Main.screenWidth = 1280; Main.screenHeight = 720;
			inputWidth.SetValue(null, 1280); inputHeight.SetValue(null, 720);
			UserInterface.ActiveInstance = new UserInterface();
			Terraria.Localization.LanguageManager.Instance.SetLanguage("en-US");
			Main.player[0] = new Player { whoAmI = 0, active = true, name = "UI QA" };
			Main.player[0].SetTalkNPC(-1);
			Main.npc[0] = new NPC(); Main.npc[0].SetDefaults(ModContent.NPCType<SoulboundCompanion>());
			Main.npc[0].whoAmI = 0; Main.npc[0].active = true; Main.npc[0].ai[0] = 0;
			var mate = (SoulboundCompanion)Main.npc[0].ModNPC;
			mate.Profile.Store(new Item(ItemID.CookedFish, 2));
			mate.Profile.Store(new Item(ItemID.CopperOre, 3));
			Item sigilItem = new(ModContent.ItemType<SoulboundSigil>());
			var sigil = (SoulboundSigil)sigilItem.ModItem;
			sigil.Profile = mate.Profile.Clone(); Main.player[0].inventory[0] = sigilItem;
			var wheel = ModContent.GetInstance<CompanionWheelSystem>();
			wheel.ExitMouseMode();
			var talk = ModContent.GetInstance<TalkModeSystem>();
			talk.PostSetupContent();
			Main.playerInventory = false; Main.mouseLeft = Main.mouseRight = false;
			wheel.Open(mate);
			Type wheelType = typeof(CompanionWheelSystem);
			var roots = ((IEnumerable)wheelType.GetField("CompanionRoots", flags | BindingFlags.Static)!.GetValue(null)!).Cast<object>().ToList();
			int itemsIndex = roots.FindIndex(value => value.ToString() == "Items");
			check(itemsIndex >= 0, "Companion wheel has no reachable item-topic conversation");
			if (itemsIndex >= 0) {
				Vector2 point = (Vector2)wheelType.GetMethod("RootPosition", flags)!.Invoke(wheel, new object[] { itemsIndex })!;
				PlayerInput.MouseX = (int)(point.X * Main.UIScale); PlayerInput.MouseY = (int)(point.Y * Main.UIScale);
				Main.mouseLeft = true; wheel.UpdateUI(new GameTime()); Main.mouseLeft = false;
				check(talk.IsOpen && !wheel.IsOpen, "Items root mouse click did not open the topic conversation");
			}
			TalkModeState state = (TalkModeState)typeof(TalkModeSystem).GetField("talkState", flags)!.GetValue(talk)!;
			if (!talk.IsOpen) talk.Open(sigil, mate);
			else check((TalkCategory)typeof(TalkModeState).GetField("category", flags)!.GetValue(state)! == TalkCategory.Items,
				"Items wheel entry opened a different chat category");
			UIElement Button(string listName, int index) {
				object entry = ((IEnumerable)typeof(TalkModeState).GetField(listName, flags)!.GetValue(state)!).Cast<object>().ElementAt(index);
				return (UIElement)entry.GetType().GetField("Item2")!.GetValue(entry)!;
			}
			void Click(UIElement expected, string label) {
				CalculatedStyle area = expected.GetDimensions();
				Vector2 point = new(area.X + area.Width / 2f, area.Y + area.Height / 2f);
				UIElement actual = state.GetElementAt(point);
				check(ReferenceEquals(actual, expected), "UI hit target mismatch: " + label);
				actual?.LeftClick(new UIMouseEvent(actual, point));
			}
			var options = (List<Terraria.GameContent.UI.Elements.UITextPanel<string>>)typeof(TalkModeState).GetField("optionButtons", flags)!.GetValue(state)!;
			foreach (string culture in SupportedCultures) {
				Terraria.Localization.LanguageManager.Instance.SetLanguage(culture);
				state.Bind(sigil, mate);
				Click(Button("categoryButtons", (int)TalkCategory.Items), "Items category");
				UIElement topics = (UIElement)typeof(TalkModeState).GetField("itemTopics", flags)!.GetValue(state)!;
				check(options[0].GetDimensions().Y >= topics.GetDimensions().Y + topics.GetDimensions().Height,
					"Items category leaves option click bounds underneath topic buttons");
				foreach (CompanionItemTopic topic in Enum.GetValues<CompanionItemTopic>()) {
					Click(Button("itemTopicButtons", (int)topic), "Item topic " + topic);
					check((CompanionItemTopic)typeof(TalkModeState).GetField("itemTopic", flags)!.GetValue(state)! == topic,
						"Item topic selection did not reach state: " + topic);
					object response = typeof(TalkModeState).GetField("response", flags)!.GetValue(state)!;
					string reply = (string)response.GetType().GetField("text", flags)!.GetValue(response)!;
					check(reply.Contains(SoulmatesText.Get($"Items.Topics.{topic}"), StringComparison.OrdinalIgnoreCase),
						"Topic click did not produce the selected category reply: " + topic);
				}
				Click(Button("categoryButtons", (int)TalkCategory.Care), "Care category");
				check(options[0].GetDimensions().Y < topics.GetDimensions().Y + topics.GetDimensions().Height,
					"Leaving Items retained its shifted option click bounds");
			}
			Terraria.Localization.LanguageManager.Instance.SetLanguage("en-US");
			CheckCharacterUi(check, state, mate, sigil);
			talk.Close();
			CheckCategorizedEmoteWheel(check, mate);
			CheckMiningFilterWheel(check, mate);
			CheckContextClickFlow(check, mate);
		}
		finally {
			ModContent.GetInstance<TalkModeSystem>().Close();
			ModContent.GetInstance<CompanionWheelSystem>().ExitMouseMode();
			FontAssets.MouseText = oldFont; FontAssets.DeathText = oldDeathFont;
			Main.player[0] = oldPlayer; Main.npc[0] = oldNpc;
			Main.gameMenu = oldMenu; Main.dedServ = oldServer; Main.myPlayer = oldLocal; Main.netMode = oldMode;
			Main.screenWidth = oldWidth; Main.screenHeight = oldHeight; UserInterface.ActiveInstance = oldInterface;
			inputWidth.SetValue(null, oldInputWidth); inputHeight.SetValue(null, oldInputHeight);
			Terraria.Localization.LanguageManager.Instance.SetLanguage(oldCulture);
		}
	}

	private static void CheckCategorizedEmoteWheel(Action<bool, string> check, SoulboundCompanion mate)
	{
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
		Type type = typeof(CompanionWheelSystem);
		var wheel = ModContent.GetInstance<CompanionWheelSystem>();
		var categories = ((IEnumerable)type.GetField("NativeCategories", flags)!.GetValue(null)!).Cast<object>().ToArray();
		var groups = ((IEnumerable)type.GetField("NativeItemCategories", flags)!.GetValue(null)!).Cast<object>().ToArray();
		var bond = ((IEnumerable)type.GetField("BondCategories", flags)!.GetValue(null)!).Cast<object>().ToArray();
		string Key(object group) => (string)group.GetType().GetProperty("Key", flags)!.GetValue(group)!;
		int[] Entries(object group) => (int[])group.GetType().GetProperty("Entries", flags)!.GetValue(group)!;
		object[] Children(object group) => ((IEnumerable?)group.GetType().GetProperty("Children", flags)!.GetValue(group))?.Cast<object>().ToArray() ?? [];
		IEnumerable<(object Node, int[] Path)> Leaves(object[] nodes, int[] path) {
			check(nodes.Length is > 0 and <= 7, "Emote folder is empty or overcrowded");
			for (int i = 0; i < nodes.Length; i++) {
				int[] next = [.. path, i];
				object[] children = Children(nodes[i]);
				if (children.Length == 0) yield return (nodes[i], next);
				else {
					check(Entries(nodes[i]).Length == 0, "Emote folder mixes executable symbols with children");
					foreach (var leaf in Leaves(children, next)) yield return leaf;
				}
			}
		}
		var leaves = Leaves(categories, []).ToArray();
		int[] all = leaves.SelectMany(leaf => Entries(leaf.Node)).ToArray();
		check(all.Length == EmoteID.Count && all.Distinct().Count() == EmoteID.Count
			&& all.Order().SequenceEqual(Enumerable.Range(0, EmoteID.Count)), "Semantic categories lost or duplicated native emotes");
		check(categories.Select(Key).SequenceEqual(new[] { "Feelings", "Gestures", "Activities", "Items", "World", "Beings", "Notifications" }),
			"Top-level emote meanings were replaced with a flat list");
		check(groups.Length == 7 && groups.SelectMany(Entries).Distinct().Count() == 23
			&& Entries(groups.Single(group => Key(group) == "Weapons")).Contains(EmoteID.LucyTheAxe),
			"Item groups lost their symbols or Lucy's weapon classification");
		check(Entries(leaves.Single(leaf => Key(leaf.Node) == "Needs").Node).SequenceEqual(new[] { EmoteID.Peckish, EmoteID.Hungry, EmoteID.Starving })
			&& Entries(leaves.Single(leaf => Key(leaf.Node) == "Signals").Node).SequenceEqual(new[] { EmoteID.EmotionAlert, EmoteID.EmoteNote }),
			"Hunger and notifications remain mixed into feelings");
		check(bond.Select(Key).SequenceEqual(new[] { "Feelings", "Gestures", "Care" })
			&& bond.SelectMany(Entries).Order().SequenceEqual(Enum.GetValues<CompanionEmote>().Select(value => (int)value).Order()),
			"Bond categories lost or duplicated companion interactions");
		object branch = Enum.Parse(type.GetNestedType("RootBranch", flags)!, "Emotes");
		object bondBranch = Enum.Parse(type.GetNestedType("RootBranch", flags)!, "Bond");
		Vector2 Position(string method, params object[] args) => (Vector2)type.GetMethod(method, flags)!.Invoke(wheel, args)!;
		int Category() => (int)type.GetField("nativeCategory", flags)!.GetValue(wheel)!;
		int Page() => (int)type.GetField("nativePage", flags)!.GetValue(wheel)!;
		int Depth() => ((IEnumerable)type.GetField("nativeGroups", flags)!.GetValue(wheel)!).Cast<object>().Count();
		int Count() => (int)type.GetMethod("NativeNodeCount", flags)!.Invoke(wheel, null)!;
		void Back() => Click((Vector2)type.GetField("center", flags)!.GetValue(wheel)!);
		void Click(Vector2 point) {
			PlayerInput.MouseX = (int)(point.X * Main.UIScale); PlayerInput.MouseY = (int)(point.Y * Main.UIScale);
			Main.mouseLeft = false; wheel.UpdateUI(new GameTime());
			Main.mouseLeft = true; wheel.UpdateUI(new GameTime());
			Main.mouseLeft = false; wheel.UpdateUI(new GameTime());
		}
		void OpenPath(int[] path) {
			Main.mouseLeft = Main.mouseRight = false; wheel.OpenEmotes(mate);
			object[] current = categories;
			for (int depth = 0; depth < path.Length; depth++) {
				int index = path[depth];
				string label = (string)type.GetMethod("BranchLabel", flags)!.Invoke(wheel, new object[] { index })!;
				bool item = (bool)current[index].GetType().GetProperty("ItemGroup", flags)!.GetValue(current[index])!;
				check(label == SoulmatesText.Get("UI.CompanionWheel." + (item ? "ItemGroups." : "EmoteGroups.") + Key(current[index])),
					"Folder has the wrong localized hover label");
				Click(Position("BranchPosition", branch, index, current.Length));
				bool folder = Children(current[index]).Length > 0;
				check(wheel.IsOpen && Depth() == (folder ? depth + 1 : depth)
					&& Category() == (folder ? -1 : index) && Page() == 0,
					"Category click did not enter the expected folder/leaf: " + Key(current[index]));
				current = Children(current[index]);
			}
		}
		var bubbles = (Dictionary<int, EmoteBubble>)typeof(EmoteBubble).GetField("byID", flags)!.GetValue(null)!;
		var oldBubbles = bubbles.ToArray();
		float oldScale = Main.UIScale;
		try {
			foreach (float scale in new[] { 1f, 1.5f, 2f }) {
				Main.UIScale = scale;
				foreach (string culture in SupportedCultures) {
					Terraria.Localization.LanguageManager.Instance.SetLanguage(culture);
					foreach (var leaf in leaves) {
						int[] entries = Entries(leaf.Node);
						check(entries.Length > 0, "Executable emote category is empty");
						for (int entry = 0; entry < entries.Length; entry++) {
							OpenPath(leaf.Path);
							string label = (string)type.GetMethod("BranchLabel", flags)!.Invoke(wheel, new object[] { leaf.Path[^1] })!;
							bool item = (bool)leaf.Node.GetType().GetProperty("ItemGroup", flags)!.GetValue(leaf.Node)!;
							check(label == SoulmatesText.Get("UI.CompanionWheel." + (item ? "ItemGroups." : "EmoteGroups.") + Key(leaf.Node)),
								"Wrong localized category hover label");
							for (int page = 0; page < entry / 7; page++) Click(Position("NativePosition", Count() - 1, Count()));
							check(Page() == entry / 7, "Next-page click missed its emote page");
							int slot = entry % 7 + (Page() > 0 ? 1 : 0);
							object icon = type.GetMethod("NativeIcon", flags)!.Invoke(wheel, new object[] { slot })!;
							check((int)icon.GetType().GetProperty("Value", flags)!.GetValue(icon)! == entries[entry], "Displayed emote differs from selected entry");
							string text = (string)type.GetMethod("NativeLabel", flags)!.Invoke(wheel, new object[] { slot })!;
							check(text == Lang.GetEmojiName(entries[entry]).Value, "Native symbol lost its engine-localized tooltip");
							bubbles.Clear();
							Click(Position("NativePosition", slot, Count()));
							check(!wheel.IsOpen && bubbles.Values.Cast<EmoteBubble>().Any(bubble => bubble.emote == entries[entry]
								&& bubble.anchor.entity == Main.LocalPlayer), "Symbol click did not emit the selected native player emote");
						}
						OpenPath(leaf.Path);
						if (entries.Length > 7) {
							Click(Position("NativePosition", Count() - 1, Count()));
							Click(Position("NativePosition", 0, Count()));
							check(Page() == 0 && wheel.IsOpen, "Previous-page arrow left its category");
						}
						Back();
						check(Category() == -1 && Depth() == leaf.Path.Length - 1 && Page() == 0, "Back skipped a leaf's category level");
						for (int depth = leaf.Path.Length - 2; depth >= 0; depth--) {
							Back(); check(Depth() == depth && wheel.IsOpen, "Back skipped a folder or closed the wheel");
						}
						Back(); check(type.GetField("branch", flags)!.GetValue(wheel) is null && wheel.IsOpen, "Back did not return to player's root wheel");
					}
					for (int group = 0; group < bond.Length; group++) {
						foreach (int entry in Enumerable.Range(0, Entries(bond[group]).Length)) {
							Main.mouseLeft = Main.mouseRight = false;
							wheel.Open(mate);
							var roots = ((IEnumerable)type.GetField("CompanionRoots", flags)!.GetValue(null)!).Cast<object>().ToArray();
							Click(Position("RootPosition", Array.FindIndex(roots, value => value.ToString() == "Bond")));
							check(Category() == -1 && Depth() == 0, "Companion wheel retained player navigation");
							Click(Position("BranchPosition", bondBranch, group, bond.Length));
							check(Category() == group && Count() == Entries(bond[group]).Length, "Bond category click missed");
							string label = (string)type.GetMethod("NativeLabel", flags)!.Invoke(wheel, new object[] { entry })!;
							check(label == SoulmatesText.EnumName((CompanionEmote)Entries(bond[group])[entry]), "Bond action lost its localized name");
							Click(Position("NativePosition", entry, Count()));
							check(!wheel.IsOpen && (CompanionEmote)typeof(SoulboundCompanion).GetField("activeEmote", flags)!.GetValue(mate)!
								== (CompanionEmote)Entries(bond[group])[entry], "Bond emote click did not dispatch its own interaction");
						}
					}
					OpenPath(leaves.Single(leaf => Key(leaf.Node) == "Town").Path);
					string pathLabel = (string)type.GetMethod("EmoteBreadcrumb", flags)!.Invoke(wheel, null)!;
					check(pathLabel == SoulmatesText.Get("UI.CompanionWheel.Categories.Emotes") + " > "
						+ SoulmatesText.Get("UI.CompanionWheel.EmoteGroups.Beings") + " > "
						+ SoulmatesText.Get("UI.CompanionWheel.EmoteGroups.Town"), "Emote breadcrumb lost its parent or localized leaf");
					Click(Position("NativePosition", Count() - 1, Count()));
					Click(Position("RootPosition", 0));
					check(Category() == -1 && Depth() == 0 && Page() == 0 && type.GetField("branch", flags)!.GetValue(wheel) is null,
						"Toggling the root retained an old leaf/page path");
					Click(Position("RootPosition", 0));
					check(Category() == -1 && Depth() == 0 && type.GetField("branch", flags)!.GetValue(wheel)!.Equals(branch),
						"Reopening the root did not start at the category list");
					Click(Position("RootPosition", 1));
					check(Category() == -1 && Depth() == 0 && type.GetField("context", flags)!.GetValue(wheel)!.ToString() == "World",
						"Changing to Point retained native emote navigation");
					wheel.Close();
				}
			}
		}
		finally {
			wheel.ExitMouseMode(); Main.UIScale = oldScale;
			bubbles.Clear(); foreach (var entry in oldBubbles) bubbles.Add(entry.Key, entry.Value);
			Terraria.Localization.LanguageManager.Instance.SetLanguage("en-US");
			Main.mouseLeft = Main.mouseRight = false;
		}
	}

	private static void CheckContextClickFlow(Action<bool, string> check, SoulboundCompanion mate)
	{
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
		var wheel = ModContent.GetInstance<CompanionWheelSystem>();
		var controls = Main.LocalPlayer.GetModPlayer<SoulmatesPlayer>();
		Type modType = typeof(global::Soulmates.Soulmates);
		FieldInfo talkKey = modType.GetField("<TalkKeybind>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)!;
		FieldInfo emoteKey = modType.GetField("<EmoteKeybind>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)!;
		object? oldTalkKey = talkKey.GetValue(null), oldEmoteKey = emoteKey.GetValue(null);
		Item oldItem = Main.item[11]; NPC oldBunny = Main.npc[1];
		var oldView = Main.GameViewMatrix;
		var keys = new List<string>();
		try {
			foreach (var entry in new[] { (talkKey, "ProbeTalk"), (emoteKey, "ProbeEmotes") }) {
				var key = (ModKeybind)Activator.CreateInstance(typeof(ModKeybind), flags, null,
					new object[] { ModLoader.GetMod("Soulmates"), entry.Item2, "G" }, null)!;
				entry.Item1.SetValue(null, key);
				string fullName = (string)typeof(ModKeybind).GetProperty("FullName", flags)!.GetValue(key)!;
				PlayerInput.Triggers.JustPressed.KeyStatus[fullName] = false;
				keys.Add(fullName);
			}
			Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(null!);
			Main.GameViewMatrix.SetViewportOverride(new Microsoft.Xna.Framework.Graphics.Viewport(0, 0, 1280, 720));
			Main.GameViewMatrix.Zoom = new Vector2(1.4f);
			Main.LocalPlayer.Center = new Vector2(400, 400); mate.NPC.Center = new Vector2(400, 340);
			Main.LocalPlayer.inventory[1] = new Item(ItemID.CopperPickaxe); Main.LocalPlayer.selectedItem = 1;
			Main.item[11] = new Item(ItemID.CopperOre, 3) { active = true, playerIndexTheItemIsReservedFor = 255 };
			Main.item[11].Center = new Vector2(500, 400);
			Main.npc[1] = new NPC(); Main.npc[1].SetDefaults(NPCID.Bunny);
			Main.npc[1].active = true; Main.npc[1].whoAmI = 1; Main.npc[1].Center = new Vector2(550, 400);
			void WorldRightClick(Vector2 point) {
				Main.playerInventory = false; Main.mouseItem = new Item(); Main.LocalPlayer.mouseInterface = false;
				Main.mouseRight = false; Main.mouseLeft = false;
				controls.ProcessTriggers(new TriggersSet()); controls.SetControls();
				Vector2 screen = Vector2.Transform(point - Main.screenPosition, Main.GameViewMatrix.TransformationMatrix);
				PlayerInput.MouseX = (int)screen.X; PlayerInput.MouseY = (int)screen.Y;
				Main.mouseRight = true;
				controls.ProcessTriggers(new TriggersSet()); controls.SetControls(); controls.PostUpdate();
				wheel.UpdateUI(new GameTime());
				Main.mouseRight = false; wheel.UpdateUI(new GameTime());
			}
			void WheelClick(Vector2 point) {
				PlayerInput.MouseX = (int)(point.X * Main.UIScale); PlayerInput.MouseY = (int)(point.Y * Main.UIScale);
				Main.mouseLeft = false; wheel.UpdateUI(new GameTime());
				Main.mouseLeft = true; wheel.UpdateUI(new GameTime());
				Main.mouseLeft = false; wheel.UpdateUI(new GameTime());
			}
			List<string> Actions() => ((IEnumerable)typeof(CompanionWheelSystem).GetField("contextActions", flags)!.GetValue(wheel)!)
				.Cast<object>().Select(value => value.ToString()!).ToList();
			void ClickAction(string name) {
				int index = Actions().IndexOf(name);
				check(index >= 0, "Context click flow has no action: " + name);
				if (index >= 0) WheelClick((Vector2)typeof(CompanionWheelSystem).GetMethod("ContextActionPosition", flags)!.Invoke(wheel, new object[] { index })!);
			}
			wheel.OpenPlayer(mate);
			WheelClick((Vector2)typeof(CompanionWheelSystem).GetMethod("MouseModePosition", flags)!.Invoke(wheel, new object[] { 2 })!);
			check(!wheel.IsOpen && wheel.MouseMode == SoulwheelMouseMode.Companion, "Mode-button click cannot arm the next world context");
			WorldRightClick(Main.item[11].Center);
			check(wheel.IsOpen && Actions().Contains("Gather"), "Production right-click hooks fail to resolve the selected drop");
			ClickAction("Gather");
			check(!wheel.IsOpen && (int)typeof(SoulboundCompanion).GetField("jobTargetItem", flags)!.GetValue(mate)! == 11,
				"Context Gather mouse click fails to dispatch its original item target");
			WorldRightClick(Main.npc[1].Center);
			check(wheel.IsOpen && Actions().Contains("Look"), "Production right-click hooks fail to resolve the selected critter");
			ClickAction("Look");
			check(!wheel.IsOpen && typeof(SoulboundCompanion).GetField("speechText", flags)!.GetValue(mate) is string speech
				&& speech.Contains(Main.npc[1].FullName), "Context Look mouse click fails to speak about the selected NPC");
			wheel.ExitMouseMode();
			WorldRightClick(Main.LocalPlayer.Center);
			check(wheel.IsOpen && typeof(CompanionWheelSystem).GetField("context", flags)!.GetValue(wheel)!.ToString() == "Player",
				"Production self-right-click does not open the player's own wheel");
			wheel.ExitMouseMode();
			WorldRightClick(mate.NPC.Center);
			check(wheel.IsOpen && typeof(CompanionWheelSystem).GetField("context", flags)!.GetValue(wheel)!.ToString() == "Companion",
				"Production companion-right-click does not open its own wheel");
		}
		finally {
			wheel.ExitMouseMode(); talkKey.SetValue(null, oldTalkKey); emoteKey.SetValue(null, oldEmoteKey);
			foreach (string key in keys) PlayerInput.Triggers.JustPressed.KeyStatus.Remove(key);
			Main.item[11] = oldItem; Main.npc[1] = oldBunny; Main.GameViewMatrix = oldView;
			Main.mouseLeft = Main.mouseRight = false;
		}
	}
}
