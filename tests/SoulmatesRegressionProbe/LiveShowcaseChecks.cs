#nullable enable
using System;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Common.UI;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.UI;

namespace SoulmatesRegressionProbe;

public sealed partial class EngineChecks
{
	private static void CheckLiveShowcase(Action<bool, string> check, TalkModeState state,
		SoulboundCompanion mate, SoulboundSigil sigil)
	{
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
		Type stateType = typeof(TalkModeState), mateType = typeof(SoulboundCompanion);
		object GetState(string field) => stateType.GetField(field, flags)!.GetValue(state)!;
		void SetMate(string field, object value) => mateType.GetField(field, flags)!.SetValue(mate, value);
		object GetMate(string field) => mateType.GetField(field, flags)!.GetValue(mate)!;
		string CargoState(UIElement pack) => $"cargo={mate.Profile.ItemCount(ItemID.Wood)}, owner={Main.LocalPlayer.inventory.Where(i => i.type == ItemID.Wood).Sum(i => i.stack)}, "
			+ $"storage={pack.GetType().GetField("selectedStorage", flags)!.GetValue(pack)}, active={mate.NPC.active}, waiting={GetState("awaitingResponse")}, net={Main.netMode}, "
			+ $"reply={GetState("response").GetType().GetField("text", flags)!.GetValue(GetState("response"))}";
		void TickSpeech() => mateType.GetMethod("UpdateSocialState", flags)!.Invoke(mate, null);
		float oldScale = Main.UIScale;
		int oldMouseX = PlayerInput.MouseX, oldMouseY = PlayerInput.MouseY;
		Item[] oldInventory = Main.LocalPlayer.inventory;
		CompanionProfile oldProfile = mate.Profile.Clone();
		Vector2 oldOwnerCenter = Main.LocalPlayer.Center;
		NPC oldCritter = Main.npc[177];
		try {
			// PostAddRecipes runs before native lookup finalization; local-player transfers refresh recipes.
			typeof(Recipe).GetMethod("CreateRequiredItemQuickLookups", flags)!.Invoke(null, null);
			foreach (float scale in new[] { 1f, 1.5f, 2f }) {
				Main.UIScale = scale;
				Main.LocalPlayer.inventory = Enumerable.Range(0, oldInventory.Length).Select(_ => new Item()).ToArray();
				Main.LocalPlayer.inventory[0] = oldInventory[0];
				mate.Profile.ClearCargo(); mate.Profile.Store(new Item(ItemID.Wood, 12));
				mate.Profile.WorkPaused = true;
				state.Bind(sigil, mate, Soulmates.Common.Dialogue.TalkCategory.Pack);
				UIElement pack = (UIElement)GetState("packElement");
				check(pack.Children.Count() == Enum.GetValues<CompanionStorage>().Length + 1, "Showcase cargo tabs were never initialized or initialized twice at scale " + scale);
				Rectangle slot = (Rectangle)pack.GetType().GetMethod("CalculateSlotBounds", flags)!
					.Invoke(null, new object[] { pack.GetDimensions().ToRectangle(), 0, false })!;
				Vector2 point = slot.Center.ToVector2();
				UIElement actual = state.GetElementAt(point);
				check(ReferenceEquals(actual, pack), "Showcase resource slot is not the native click target at scale " + scale);
				// Event coordinates are authoritative even if another hook changes the raw mouse globals.
				PlayerInput.MouseX = PlayerInput.MouseY = 0;
				actual?.RightClick(new UIMouseEvent(actual, point));
				check(mate.Profile.ItemCount(ItemID.Wood) == 11
					&& Main.LocalPlayer.inventory.Where(i => i.type == ItemID.Wood).Sum(i => i.stack) == 1,
					"Showcase right-click withdrawal ignored its event position or violated conservation at scale " + scale + ": " + CargoState(pack));
				actual?.LeftClick(new UIMouseEvent(actual, point));
				check(mate.Profile.ItemCount(ItemID.Wood) == 0
					&& Main.LocalPlayer.inventory.Where(i => i.type == ItemID.Wood).Sum(i => i.stack) == 12,
					"Showcase full-stack withdrawal ignored its event position or violated conservation at scale " + scale + ": " + CargoState(pack));
				for (int i = 1; i < Main.LocalPlayer.inventory.Length; i++)
					Main.LocalPlayer.inventory[i] = new Item(ItemID.CopperShortsword);
				Main.LocalPlayer.inventory[1] = new Item(ItemID.Wood);
				int capacity = Main.LocalPlayer.inventory[1].maxStack;
				Main.LocalPlayer.inventory[1].stack = capacity - 2;
				mate.Profile.Store(new Item(ItemID.Wood, 8));
				state.Bind(sigil, mate, Soulmates.Common.Dialogue.TalkCategory.Pack);
				actual = state.GetElementAt(point);
				actual?.LeftClick(new UIMouseEvent(actual, point));
				check(mate.Profile.ItemCount(ItemID.Wood) == 6 && Main.LocalPlayer.inventory[1].stack == capacity,
					"Showcase partial UI withdrawal lost its remainder at scale " + scale + ": " + CargoState(pack));
				actual?.LeftClick(new UIMouseEvent(actual, point));
				check(mate.Profile.ItemCount(ItemID.Wood) == 6 && Main.LocalPlayer.inventory[1].stack == capacity,
					"Showcase full inventory changed cargo on repeat click at scale " + scale + ": " + CargoState(pack));
			}

			mate.Profile.WorkPaused = false;
			SetMate("pendingQuestion", CompanionQuestion.Company); SetMate("questionTicks", 900);
			SetMate("questionId", Guid.NewGuid()); mate.ShowSpeech("A question still waiting for your answer.");
			SetMate("speechTimer", 125);
			for (int tick = 0; tick < 180; tick++) TickSpeech();
			check((int)GetMate("speechTimer") > 120 && ((string)GetMate("speechText")).Length > 0,
				"Showcase pending personal question faded before its answer wheel resolved");
			SetMate("pendingQuestion", CompanionQuestion.None); SetMate("questionTicks", 0);
			SetMate("pendingAutonomyActivity", Enum.Parse(mateType.GetNestedType("AutonomyActivity", flags)!, "TendForest"));
			SetMate("pendingInitiativeTimer", 900); mate.ShowSpeech("May I tend this tree?"); SetMate("speechTimer", 125);
			for (int tick = 0; tick < 180; tick++) TickSpeech();
			check((int)GetMate("speechTimer") > 120, "Showcase pending work question faded before consent");
			SetMate("pendingAutonomyActivity", Enum.Parse(mateType.GetNestedType("AutonomyActivity", flags)!, "None"));
			SetMate("pendingInitiativeTimer", 0);
			for (int tick = 0; tick < 180; tick++) TickSpeech();
			check((int)GetMate("speechTimer") == 0 && ((string)GetMate("speechText")).Length == 0,
				"Showcase resolved question left immortal speech");
			string diagnosis = (string)mateType.GetMethod("DiagnosticText", flags)!.Invoke(mate, null)!;
			check(diagnosis.Contains("tModLoader " + Terraria.ModLoader.BuildInfo.tMLVersion),
				"Showcase diagnosis labels Terraria's assembly version as the loader version");
			check(Main.itemAnimations[Terraria.ModLoader.ModContent.ItemType<Soulcore>()] is not null,
				"Showcase Soulcore renders a whole native animation strip instead of one frame");
			var rejection = typeof(CompanionCritters).GetMethod("NaturalRejectionReason", flags)!;
			var candidateReason = mateType.GetMethod("CritterCandidateReason", flags)!;
			NPC Natural() {
				NPC target = Main.npc[177] = new NPC { whoAmI = 177 };
				target.SetDefaults(NPCID.Bunny); target.active = true; target.Center = mate.NPC.Center;
				return target;
			}
			NPC bunny = Natural();
			check((string)rejection.Invoke(null, new object[] { bunny })! == "" && CompanionCritters.IsNatural(bunny),
				"Showcase diagnosis rejects the unchanged native bunny");
			Natural();
			check((string)candidateReason.Invoke(mate, new object[] { bunny })! == "NPC identity replaced",
				"Showcase diagnosis calls a replaced native NPC eligible");
			foreach (var boundary in new (Action<NPC> Change, string Reason)[] {
				(npc => npc.active = false, "inactive"), (npc => npc.life = 0, "dead"),
				(npc => npc.catchItem = 0, "no valid native catch item"),
				(npc => npc.townNPC = true, "town NPC protected"),
				(npc => npc.boss = true, "boss protected"), (npc => npc.damage = 1, "damaging NPC protected"),
				(npc => npc.SpawnedFromStatue = true, "statue protected"),
				(npc => npc.releaseOwner = 0, "released animal protected")
			}) {
				bunny = Natural(); boundary.Change(bunny);
				check(!CompanionCritters.IsNatural(bunny)
					&& (string)candidateReason.Invoke(mate, new object[] { bunny })! == boundary.Reason,
					"Showcase critter diagnostic hides its native rejection condition: " + boundary.Reason);
			}
			bunny = Natural(); mate.Profile.CritterMode = CompanionCritterMode.Company;
			Main.LocalPlayer.Center = mate.NPC.Center;
			bunny.Center += new Vector2(350, 0);
			check(((string)candidateReason.Invoke(mate, new object[] { bunny })!).Contains("owner"),
				"Showcase diagnosis omitted the owner range gate");
			Main.LocalPlayer.Center = bunny.Center;
			check(((string)candidateReason.Invoke(mate, new object[] { bunny })!).Contains("automatic 20-tile search"),
				"Showcase diagnosis omitted the companion search-radius gate");
			var avoid = mateType.GetMethod("SpeechBubblePositionAvoiding", flags)!;
			foreach (Vector2 viewport in new[] { new Vector2(800, 600), new Vector2(1152, 864), new Vector2(3440, 1369) }) {
				Rectangle map = new((int)viewport.X - 310, 72, 280, 292);
				foreach (float side in new[] { -1f, 1f }) {
					Vector2 size = new(240, 110), anchor = new(viewport.X - 140, 300);
					Vector2 position = (Vector2)avoid.Invoke(null, new object[] { anchor, size, viewport, side, map })!;
					Rectangle bubble = new((int)position.X, (int)position.Y, (int)size.X, (int)size.Y);
					check(!bubble.Intersects(map) && bubble.Left >= 8 && bubble.Top >= 52
						&& bubble.Right <= viewport.X - 8 && bubble.Bottom <= viewport.Y - 10,
						"Showcase speech overlaps the minimap or escapes viewport " + viewport + "/" + side);
				}
			}
		}
		finally {
			SetMate("pendingQuestion", CompanionQuestion.None); SetMate("questionTicks", 0);
			SetMate("pendingAutonomyActivity", Enum.Parse(mateType.GetNestedType("AutonomyActivity", flags)!, "None"));
			SetMate("pendingInitiativeTimer", 0);
			mate.Profile = oldProfile; Main.LocalPlayer.inventory = oldInventory;
			Main.LocalPlayer.Center = oldOwnerCenter;
			Main.npc[177] = oldCritter;
			Main.UIScale = oldScale; PlayerInput.MouseX = oldMouseX; PlayerInput.MouseY = oldMouseY;
			state.Bind(sigil, mate);
		}
	}
}
