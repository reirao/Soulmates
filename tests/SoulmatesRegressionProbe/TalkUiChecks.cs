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
			Main.player[0] = new Player { whoAmI = 0, active = true };
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
			talk.Close();
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
