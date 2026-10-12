#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Soulmates.Common;
using Soulmates.Common.UI;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.GameContent.UI;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;

namespace SoulmatesRegressionProbe;

public sealed partial class EngineChecks
{
	private static void CheckWheelBeta(Action<bool, string> check)
	{
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
		Type wheelType = typeof(CompanionWheelSystem);
		var wheel = ModContent.GetInstance<CompanionWheelSystem>();
		var direct = ModContent.GetInstance<DirectOrderSystem>();
		Player[] oldPlayers = Main.player;
		NPC[] oldNpcs = Main.npc;
		Item[] oldItems = Main.item;
		Tilemap oldMap = Main.tile;
		WorldFileData oldWorld = Main.ActiveWorldFileData;
		Item oldMouseItem = Main.mouseItem;
		int oldLocal = Main.myPlayer, oldMode = Main.netMode, oldWidth = Main.maxTilesX, oldHeight = Main.maxTilesY;
		int oldMouseX = PlayerInput.MouseX, oldMouseY = PlayerInput.MouseY;
		float oldScale = Main.UIScaleWanted;
		Vector2 oldScreenPosition = Main.screenPosition;
		var oldView = Main.GameViewMatrix;
		var oldCulture = Terraria.Localization.Language.ActiveCulture;
		KeyboardState oldKeys = Main.keyState, oldOldKeys = Main.oldKeyState;
		bool oldWriting = PlayerInput.WritingText;
		var bubbles = (Dictionary<int, EmoteBubble>)typeof(EmoteBubble).GetField("byID", flags)!.GetValue(null)!;
		var oldBubbles = bubbles.ToArray();
		string[] flagNames = { "dedServ", "gameMenu", "playerInventory", "mouseLeft", "mouseRight",
			"mouseLeftRelease", "mouseRightRelease", "blockMouse", "drawingPlayerChat", "editSign", "editChest" };
		var oldFlags = flagNames.ToDictionary(name => name, name => typeof(Main).GetField(name, flags)!.GetValue(null));
		FieldInfo inputWidth = typeof(PlayerInput).GetField("_originalScreenWidth", flags)!;
		FieldInfo inputHeight = typeof(PlayerInput).GetField("_originalScreenHeight", flags)!;
		object oldInputWidth = inputWidth.GetValue(null)!, oldInputHeight = inputHeight.GetValue(null)!;
		object? Get(string name) => wheelType.GetField(name, flags)!.GetValue(wheel);
		object? Call(string name, params object[] args) => wheelType.GetMethod(name, flags)!.Invoke(wheel, args);
		object Branch(string name) => Enum.Parse(wheelType.GetNestedType("RootBranch", flags)!, name);
		string[] ContextActions() => ((IEnumerable)Get("contextActions")!).Cast<object>().Select(value => value.ToString()!).ToArray();
		void Click(Vector2 point) {
			PlayerInput.MouseX = (int)(point.X * Main.UIScale); PlayerInput.MouseY = (int)(point.Y * Main.UIScale);
			Main.mouseLeft = false; wheel.UpdateUI(new GameTime());
			Main.mouseLeft = true; wheel.UpdateUI(new GameTime());
			Main.mouseLeft = false; wheel.UpdateUI(new GameTime());
		}
		try {
			foreach (string name in flagNames) typeof(Main).GetField(name, flags)!.SetValue(null, false);
			Main.myPlayer = 0; Main.netMode = NetmodeID.SinglePlayer;
			Main.maxTilesX = Main.maxTilesY = 100;
			Main.tile = (Tilemap)Activator.CreateInstance(typeof(Tilemap), flags, null, new object[] { (ushort)100, (ushort)100 }, null)!;
			Main.player = (Player[])oldPlayers.Clone();
			Main.player[0] = new Player { whoAmI = 0, active = true, Center = new Vector2(400, 400) };
			Main.LocalPlayer.SetTalkNPC(-1);
			Main.npc = Enumerable.Range(0, oldNpcs.Length).Select(i => new NPC { whoAmI = i }).ToArray();
			Main.item = Enumerable.Range(0, oldItems.Length).Select(i => new Item { whoAmI = i }).ToArray();
			Main.npc[0].SetDefaults(ModContent.NPCType<SoulboundCompanion>());
			Main.npc[0].active = true; Main.npc[0].ai[0] = 0; Main.npc[0].Center = new Vector2(400, 340);
			var mate = (SoulboundCompanion)Main.npc[0].ModNPC;
			mate.Profile.Name = "AETHER";
			Main.LocalPlayer.inventory[0] = new Item(ModContent.ItemType<SoulboundSigil>());
			((SoulboundSigil)Main.LocalPlayer.inventory[0].ModItem).Profile = mate.Profile.Clone();
			Main.LocalPlayer.inventory[1] = new Item(ItemID.CopperPickaxe);
			Main.LocalPlayer.selectedItem = 1;
			Main.item[10] = new Item(ItemID.Gel, 7) { whoAmI = 10, active = true, playerIndexTheItemIsReservedFor = 255 };
			Main.item[10].Center = new Vector2(500, 450);
			Main.npc[1].SetDefaults(NPCID.Bunny); Main.npc[1].active = true; Main.npc[1].Center = new Vector2(600, 450);
			Main.mouseItem = new Item(); PlayerInput.WritingText = false;
			Main.keyState = Main.oldKeyState = new KeyboardState();
			Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(null!);
			Main.GameViewMatrix.SetViewportOverride(new Viewport(0, 0, 1280, 720));
			Main.GameViewMatrix.Zoom = Vector2.One; Main.screenPosition = Vector2.Zero;
			inputWidth.SetValue(null, 1280); inputHeight.SetValue(null, 720); Main.UIScale = 1f;
			wheel.ExitMouseMode();

			var commands = (CompanionQuickAction[])wheelType.GetField("CommandActions", flags)!.GetValue(null)!;
			int evilIndex = Array.IndexOf(commands, CompanionQuickAction.ToggleEvil);
			check(evilIndex >= 0 && commands.Count(value => value == CompanionQuickAction.ToggleEvil) == 1,
				"EVIL toggle is missing or duplicated in companion wheel Commands");
			check(!((IEnumerable)wheelType.GetField("PlayerRoots", flags)!.GetValue(null)!).Cast<object>().Contains(Branch("Commands")),
				"Player wheel exposes companion EVIL settings");
			if (evilIndex >= 0) {
				object[] roots = ((IEnumerable)wheelType.GetField("CompanionRoots", flags)!.GetValue(null)!).Cast<object>().ToArray();
				int commandsRoot = Array.FindIndex(roots, value => value.Equals(Branch("Commands")));
				check(commandsRoot >= 0, "Companion Commands root is unreachable");
				foreach (bool enabled in new[] { false, true }) {
					mate.Profile.EvilMode = enabled;
					wheel.Open(mate);
					if (commandsRoot >= 0) Click((Vector2)Call("RootPosition", commandsRoot)!);
					check(Get("branch")?.ToString() == "Commands", "Companion root click fails to open EVIL settings");
					string label = (string)Call("BranchLabel", evilIndex)!;
					check(label == SoulmatesText.Get(enabled ? "UI.Wheel.EvilOn" : "UI.Wheel.EvilOff") && !label.StartsWith("Mods."),
						"EVIL wheel label does not reflect its current per-sigil state");
					object icon = Call("BranchIcon", Branch("Commands"), evilIndex)!;
					int iconValue = (int)icon.GetType().GetProperty("Value", flags)!.GetValue(icon)!;
					check(icon.GetType().GetProperty("Kind", flags)!.GetValue(icon)!.ToString() == "Emote"
						&& iconValue >= 0 && iconValue < EmoteID.Count,
						"EVIL wheel toggle does not use a valid native emote icon");
					Vector2 point = (Vector2)Call("BranchPosition", Branch("Commands"), evilIndex, commands.Length)!;
					PlayerInput.MouseX = (int)point.X; PlayerInput.MouseY = (int)point.Y;
					Call("FindHoveredNode");
					check(Get("hoverLayer")!.ToString() == "Branch" && (int)Get("hoverIndex")! == evilIndex,
						"Visible EVIL wheel toggle cannot be reached by its click target");
					Click(point);
					check(!wheel.IsOpen && mate.Profile.EvilMode == !enabled,
						"EVIL wheel click failed to dispatch the existing ToggleEvil quick action");
				}

				int evilRoot = Array.FindIndex(roots, value => value.Equals(Branch("Evil")));
				int gamesRoot = Array.FindIndex(roots, value => value.Equals(Branch("Games")));
				check(evilRoot >= 0 && gamesRoot >= 0, "Direct EVIL/Games roots are missing");
				mate.Profile.EvilMode = false; wheel.Open(mate);
				Click((Vector2)Call("RootPosition", evilRoot)!);
				check(!wheel.IsOpen && mate.Profile.EvilMode, "Direct EVIL icon does not dispatch through real wheel input");
				wheel.Open(mate); Click((Vector2)Call("RootPosition", gamesRoot)!);
				check((int)Call("BranchNodeCount", Branch("Games"))! == 2, "Secret boss symbol did not unlock with EVIL");
				Vector2 challengePoint = (Vector2)Call("BranchPosition", Branch("Games"), 1, 2)!;
				Click(challengePoint);
				check(wheel.IsOpen && !mate.IsBossChallenge && (bool)Get("bossConfirmation")!, "First boss click skipped explicit confirmation");
				wheel.Close(); wheel.Open(mate); Click((Vector2)Call("RootPosition", gamesRoot)!);
				check(!(bool)Get("bossConfirmation")!, "Reopening wheel retained a stale boss confirmation");
				challengePoint = (Vector2)Call("BranchPosition", Branch("Games"), 1, 2)!;
				Click(challengePoint); Click(challengePoint);
				check(!wheel.IsOpen && mate.IsBossChallenge, "Confirmed Games symbol did not transform the companion");
				wheel.Open(mate); Click((Vector2)Call("RootPosition", gamesRoot)!);
				Click((Vector2)Call("BranchPosition", Branch("Games"), 1, 2)!);
				check(!mate.IsBossChallenge && !wheel.IsOpen, "Games stop symbol failed to end the duel");
				mate.Profile.WorkPaused = false;

				Type mateType = typeof(SoulboundCompanion);
				object? MateGet(string name) => mateType.GetField(name, flags)!.GetValue(mate);
				void MateSet(string name, object? value) => mateType.GetField(name, flags)!.SetValue(mate, value);
				string[] preservedFields = { "pendingQuestion", "questionId", "questionTicks", "speechText", "speechTimer", "speechDuration",
					"nativeExpression", "nativeExpressionStep", "nativeExpressionTicks", "activeNativeBubble" };
				var beforeQuestion = preservedFields.ToDictionary(name => name, MateGet);
				try {
					Guid token = Guid.NewGuid();
					MateSet("pendingQuestion", CompanionQuestion.Company); MateSet("questionId", token); MateSet("questionTicks", 900);
					mate.ShowSpeech("A question still waiting for your answer.");
					mateType.GetMethod("ShowNativeExpression", flags)!.Invoke(mate, new object[] { EmoteID.EmoteConfused, EmoteID.EmotionLove, -1 });
					var pinned = preservedFields.ToDictionary(name => name, MateGet);
					int[] expression = ((int[])MateGet("nativeExpression")!).ToArray();
					foreach (bool enabled in new[] { false, true }) {
						mate.Profile.EvilMode = enabled;
						wheel.Open(mate);
						if (commandsRoot >= 0) Click((Vector2)Call("RootPosition", commandsRoot)!);
						Click((Vector2)Call("BranchPosition", Branch("Commands"), evilIndex, commands.Length)!);
						check(!wheel.IsOpen && mate.Profile.EvilMode == !enabled && mate.FindBoundSigil()!.Profile.EvilMode == !enabled,
							"Pending-question EVIL wheel click failed to toggle and sync persona");
						check(mate.HasPendingQuestion && mate.QuestionId == token && mate.QuestionTicks == 900,
							"EVIL wheel click answered or replaced the pending question token");
						check(new[] { "speechText", "speechTimer", "speechDuration" }.All(name => Equals(MateGet(name), pinned[name])),
							"EVIL wheel click overwrote pinned question speech");
						check(((int[])MateGet("nativeExpression")!).SequenceEqual(expression)
							&& new[] { "nativeExpressionStep", "nativeExpressionTicks", "activeNativeBubble" }.All(name => Equals(MateGet(name), pinned[name])),
							"EVIL wheel click replaced the pending question expression");
					}
				}
				finally { foreach (var entry in beforeQuestion) MateSet(entry.Key, entry.Value); }
			}

			foreach ((int width, int height) in new[] { (640, 360), (1280, 720), (1920, 1080), (2560, 1440) })
			foreach (float requestedScale in new[] { 1f, 1.5f, 2f }) {
				inputWidth.SetValue(null, width); inputHeight.SetValue(null, height); Main.UIScale = requestedScale;
				// Terraria clamps requested UI scaling to the viewport's supported maximum.
				float uiScale = Main.UIScale;
				check(Main.UIScaleMatrix.M11 == uiScale && Main.UIScaleMatrix.M22 == uiScale,
					"Command fixture UI scale and drawing matrix disagree");
				wheel.Open(mate); Call("ActivateRoot", Branch("Commands"));
				float scale = Math.Min(1f, Math.Min(width / uiScale / 460f, height / uiScale / 540f));
				for (int i = 0; i < commands.Length; i++) {
					Vector2 a = (Vector2)Call("BranchPosition", Branch("Commands"), i, commands.Length)!;
					PlayerInput.MouseX = (int)(a.X * uiScale); PlayerInput.MouseY = (int)(a.Y * uiScale);
					Call("FindHoveredNode");
					check(Get("hoverLayer")!.ToString() == "Branch" && (int)Get("hoverIndex")! == i,
						"Command button loses its own hit target at " + width + "x" + height + " / UI scale " + uiScale
						+ " (requested " + requestedScale + "): " + commands[i]);
					for (int j = i + 1; j < commands.Length; j++) {
						Vector2 b = (Vector2)Call("BranchPosition", Branch("Commands"), j, commands.Length)!;
						check(Vector2.Distance(a, b) > 42f * scale, "Command hit targets overlap after adding EVIL");
					}
				}
			}
			inputWidth.SetValue(null, 1280); inputHeight.SetValue(null, 720); Main.UIScale = 1f;

			// Native category Back stays in the leaf's folder; paging never dispatches a bond action.
			wheel.OpenEmotes(mate);
			object[] Categories() => ((IEnumerable)wheelType.GetProperty("ActiveNativeCategories", flags)!.GetValue(wheel)!).Cast<object>().ToArray();
			int CategoryIndex(string key) => Array.FindIndex(Categories(), category => (string)category.GetType().GetProperty("Key")!.GetValue(category)! == key);
			Call("ActivateBranch", CategoryIndex("Beings"));
			Call("ActivateBranch", CategoryIndex("Town"));
			int nativeCount = (int)Call("NativeNodeCount")!;
			Call("ActivateNative", nativeCount - 1);
			check((int)Get("nativePage")! == 1, "Native Next arrow did not enter the second page");
			object previous = Call("NativeIcon", 0)!;
			check(previous.GetType().GetProperty("Kind", flags)!.GetValue(previous)!.ToString() == "Back",
				"Native previous page uses an executable emote icon");
			Call("ActivateNative", 0);
			check((int)Get("nativePage")! == 0 && wheel.IsOpen, "Native Previous arrow left the category");
			Call("ActivateNative", -1); Call("ActivateNative", 999);
			check((int)Get("nativePage")! == 0 && wheel.IsOpen && (string)Call("NativeLabel", -1)! == "",
				"Invalid native node index executed or exposed a stale page entry");
			Call("StepBack");
			check((int)Get("nativeCategory")! == -1 && ((IEnumerable)Get("nativeGroups")!).Cast<object>().Count() == 1,
				"Back from a native leaf skipped its containing folder");
			Call("StepBack"); Call("StepBack");
			check(wheel.IsOpen && Get("branch") is null, "Native folder Back failed to return to the player roots");

			Type targetType = wheelType.Assembly.GetType("Soulmates.Common.UI.SoulwheelTarget")!;
			object Snapshot(Vector2 point) => Activator.CreateInstance(targetType, point, mate.NPC.whoAmI)!;
			bool Current(object snapshot) => (bool)targetType.GetProperty("IsCurrent")!.GetValue(snapshot)!;
			object itemSnapshot = Snapshot(Main.item[10].Center);
			Main.item[10].Center += new Vector2(4, 0); Main.item[10].stack--;
			check(Current(itemSnapshot), "Normal movement or partial pickup invalidates an existing item target");
			Main.item[10].active = false;
			check(!Current(itemSnapshot), "Inactive drop remains a current wheel target");
			Main.item[10].active = true;
			check(!Current(itemSnapshot), "Reactivated drop revives an invalidated wheel snapshot");
			itemSnapshot = Snapshot(Main.item[10].Center);
			Main.item[10].prefix = 1;
			check(!Current(itemSnapshot), "In-place item replacement retains its old wheel identity");
			Main.item[10].prefix = 0;
			object npcSnapshot = Snapshot(Main.npc[1].Center);
			Main.npc[1].active = false;
			check(!Current(npcSnapshot), "Inactive NPC remains a current wheel target");
			Main.npc[1].active = true;
			check(!Current(npcSnapshot), "Reactivated NPC revives an invalidated wheel snapshot");
			Tile ore = Main.tile[30, 30]; ore.HasTile = true; ore.TileType = TileID.Copper;
			object tileSnapshot = Snapshot(new Vector2(488, 488));
			ore.HasTile = false;
			check(!Current(tileSnapshot), "Removed tile remains a current wheel target");
			ore.HasTile = true;
			check(!Current(tileSnapshot), "Replaced tile revives an invalidated wheel snapshot");

			wheel.OpenContext(mate, Main.item[10].Center, new Vector2(400, 300));
			int gather = Array.IndexOf(ContextActions(), "Gather");
			check(gather >= 0, "Loose-item context lost its Gather action");
			FieldInfo directedItem = typeof(SoulboundCompanion).GetField("jobTargetItem", flags)!;
			object? oldDirectedItem = directedItem.GetValue(mate);
			Main.item[10].active = false; wheel.UpdateUI(new GameTime()); Main.item[10].active = true;
			if (gather >= 0) Call("ActivateContextAction", gather);
			check(!wheel.IsOpen && Equals(directedItem.GetValue(mate), oldDirectedItem),
				"Frame-observed stale item accepted a later context command");

			foreach (string protectedFlag in new[] { "drawingPlayerChat", "editSign", "editChest" }) {
				wheel.OpenPlayer(mate);
				typeof(Main).GetField(protectedFlag, flags)!.SetValue(null, true);
				wheel.UpdateUI(new GameTime());
				check(!wheel.IsOpen, "Wheel retains input while native text entry is active: " + protectedFlag);
				direct.Begin(mate, CompanionTargetOrder.Gather);
				check(!direct.IsActive, "Directed tool opens over protected native text entry: " + protectedFlag);
				typeof(Main).GetField(protectedFlag, flags)!.SetValue(null, false);
				direct.Begin(mate, CompanionTargetOrder.Gather);
				typeof(Main).GetField(protectedFlag, flags)!.SetValue(null, true);
				direct.UpdateUI(new GameTime());
				check(!direct.IsActive, "Directed tool survives newly opened native text entry: " + protectedFlag);
				typeof(Main).GetField(protectedFlag, flags)!.SetValue(null, false);
			}
			wheel.OpenPlayer(mate); PlayerInput.WritingText = true; wheel.UpdateUI(new GameTime());
			check(!wheel.IsOpen, "Wheel retains input while a native text field is writing");
			PlayerInput.WritingText = false;
			wheel.OpenPlayer(mate);
			Main.mouseItem = new Item(ItemID.Gel);
			wheel.OpenContext(mate, Main.item[10].Center, new Vector2(400, 300));
			check(Get("context")!.ToString() == "Player" && ContextActions().Length == 0,
				"Rejected contextual open mutates a previously open wheel");
			wheel.UpdateUI(new GameTime());
			check(!wheel.IsOpen && Main.mouseItem.type == ItemID.Gel, "Wheel hides or consumes a cursor-held item");
			Main.mouseItem = new Item();
			wheel.OpenPlayer(mate); Main.LocalPlayer.SetTalkNPC(1); wheel.UpdateUI(new GameTime());
			check(!wheel.IsOpen, "Wheel steals input from a newly opened native conversation");
			Main.LocalPlayer.SetTalkNPC(-1);

			Guid identity = mate.Profile.Id;
			wheel.OpenPlayer(mate); mate.Profile.Id = Guid.NewGuid(); wheel.UpdateUI(new GameTime());
			check(!wheel.IsOpen && wheel.MouseMode == SoulwheelMouseMode.Terraria, "Open wheel survives a profile identity switch");
			mate.Profile.Id = identity;
			direct.Begin(mate, CompanionTargetOrder.Gather); mate.Profile.Id = Guid.NewGuid(); direct.UpdateUI(new GameTime());
			check(!direct.IsActive, "Directed tool survives a profile identity switch");
			mate.Profile.Id = identity;
			wheel.OpenPlayer(mate); mate.NPC.ai[0] = 1; wheel.UpdateUI(new GameTime());
			check(!wheel.IsOpen, "Wheel dispatch remains bound after its companion changes owner");
			mate.NPC.ai[0] = 0;

			// UI hover clicks must not become world orders when dragged away while still held.
			Main.LocalPlayer.mouseInterface = false; Main.mouseLeft = Main.mouseRight = false;
			direct.Begin(mate, CompanionTargetOrder.Gather);
			PlayerInput.MouseX = (int)Main.item[10].Center.X; PlayerInput.MouseY = (int)Main.item[10].Center.Y;
			Main.LocalPlayer.mouseInterface = true; Main.mouseLeft = true; direct.UpdateUI(new GameTime());
			check(Equals(directedItem.GetValue(mate), oldDirectedItem), "Directed tool sends a world command through native UI");
			Main.LocalPlayer.mouseInterface = false; direct.UpdateUI(new GameTime());
			check(Equals(directedItem.GetValue(mate), oldDirectedItem), "Held native UI click becomes a fresh directed order");
			Main.mouseLeft = false; direct.UpdateUI(new GameTime());
			Main.blockMouse = false; Main.mouseLeftRelease = true; Main.mouseLeft = true; direct.UpdateUI(new GameTime());
			check((int)directedItem.GetValue(mate)! == 10 && Main.blockMouse && !Main.mouseLeftRelease,
				"Successful direct order fails to capture its selection click");

			mate.SetCommand(false); direct.Cancel();
			Main.mouseLeft = false;
			Main.ActiveWorldFileData = new WorldFileData("", false) { UniqueId = Guid.NewGuid() };
			for (int x = 20; x <= 42; x++) for (int y = 20; y <= 34; y++) {
				if (x == 20 || x == 42 || y == 20 || y == 34) PlaceTile(x, y, TileID.WoodBlock);
				else Main.tile[x, y].WallType = WallID.Wood;
			}
			PlaceTile(22, 32, TileID.Chairs);
			Main.LocalPlayer.Center = mate.NPC.Center = new Point(30, 25).ToWorldCoordinates();
			mate.Profile.Routine = CompanionJob.None; mate.Profile.WorkPaused = true;
			mate.Profile.AutonomyEnabled = false; mate.Profile.QuestionCadence = CompanionQuestionCadence.Quiet;
			var prompt = ModContent.GetInstance<InitiativePromptSystem>();
			object[] homeRoots = ((IEnumerable)wheelType.GetField("CompanionRoots", flags)!.GetValue(null)!).Cast<object>().ToArray();
			int homeIndex = Array.FindIndex(homeRoots, value => value.Equals(Branch("Home")));
			check(homeIndex >= 0 && homeRoots.Count(value => value.Equals(Branch("Home"))) == 1,
				"Home entry is missing or duplicated in the companion wheel");
			check(!((IEnumerable)wheelType.GetField("PlayerRoots", flags)!.GetValue(null)!).Cast<object>().Contains(Branch("Home")),
				"Companion Home configuration leaked into the player wheel");
			foreach ((int width, int height) in new[] { (640, 360), (1280, 720), (1920, 1080), (2560, 1440) })
			foreach (float requestedScale in new[] { 1f, 1.5f, 2f }) {
				inputWidth.SetValue(null, width); inputHeight.SetValue(null, height); Main.UIScale = requestedScale;
				wheel.Open(mate);
				for (int i = 0; i < homeRoots.Length; i++) {
					Vector2 point = (Vector2)Call("RootPosition", i)!;
					PlayerInput.MouseX = (int)(point.X * Main.UIScale); PlayerInput.MouseY = (int)(point.Y * Main.UIScale);
					Call("FindHoveredNode");
					check(Get("hoverLayer")!.ToString() == "Root" && (int)Get("hoverIndex")! == i,
						"Companion root lost its hit target after adding Home: " + homeRoots[i]);
					float scale = Math.Min(1f, Math.Min(width / Main.UIScale / 460f, height / Main.UIScale / 540f));
					for (int j = i + 1; j < homeRoots.Length; j++)
						check(Vector2.Distance(point, (Vector2)Call("RootPosition", j)!) > 42f * scale,
							"Companion root hit targets overlap after adding Home");
				}
				if (homeIndex >= 0) Click((Vector2)Call("RootPosition", homeIndex)!);
				check(!wheel.IsOpen && prompt.IsOpen && mate.PendingQuestion == CompanionQuestion.Home,
					"Visible Home node did not open the production answer wheel");
				Guid homeToken = mate.QuestionId;
				check(mate.RespondToQuestion(homeToken, CompanionAnswer.Later) && !mate.HasPendingQuestion,
					"Home answer wheel could not finish its captured question");
				prompt.Close();
			}
			object homeIcon = Call("RootIcon", Branch("Home"))!;
			check(homeIcon.GetType().GetProperty("Kind", flags)!.GetValue(homeIcon)!.ToString() == "Home",
				"Home wheel does not use its native map-home icon");
			foreach (string culture in new[] { "en-US", "de-DE", "it-IT", "fr-FR", "es-ES", "ru-RU", "pt-BR", "pl-PL", "zh-Hans" }) {
				Terraria.Localization.LanguageManager.Instance.SetLanguage(culture);
				check((string)Call("RootLabel", Branch("Home"))! == SoulmatesText.Get("UI.CompanionWheel.Categories.Home")
					&& !SoulmatesText.Get("Conversation.Home.RequestedQuestion").StartsWith("Mods."),
					"Home controls leak localization keys: " + culture);
			}
		}
		finally {
			wheel.ExitMouseMode(); direct.Cancel();
			ModContent.GetInstance<InitiativePromptSystem>().Close();
			Main.ActiveWorldFileData = oldWorld;
			Terraria.Localization.LanguageManager.Instance.SetLanguage(oldCulture);
			bubbles.Clear(); foreach (var entry in oldBubbles) bubbles.Add(entry.Key, entry.Value);
			Main.player = oldPlayers; Main.npc = oldNpcs; Main.item = oldItems; Main.tile = oldMap; Main.mouseItem = oldMouseItem;
			Main.myPlayer = oldLocal; Main.netMode = oldMode; Main.maxTilesX = oldWidth; Main.maxTilesY = oldHeight;
			Main.screenPosition = oldScreenPosition; Main.GameViewMatrix = oldView;
			Main.keyState = oldKeys; Main.oldKeyState = oldOldKeys; PlayerInput.WritingText = oldWriting;
			PlayerInput.MouseX = oldMouseX; PlayerInput.MouseY = oldMouseY;
			inputWidth.SetValue(null, oldInputWidth); inputHeight.SetValue(null, oldInputHeight);
			Main.UIScale = oldScale;
			foreach (var entry in oldFlags) typeof(Main).GetField(entry.Key, flags)!.SetValue(null, entry.Value);
		}
	}
}
