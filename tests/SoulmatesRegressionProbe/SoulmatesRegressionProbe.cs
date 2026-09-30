using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Hjson;
using Soulmates.Common;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Soulmates.Common.UI;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace SoulmatesRegressionProbe;

public sealed class SoulmatesRegressionProbe : Mod { }

public sealed class EngineChecks : ModSystem
{
	public override void PostSetupContent()
	{
		if (!Main.dedServ)
			return;
		int assertions = 0;
		var failures = new List<string>();
		void Check(bool condition, string label)
		{
			assertions++;
			if (!condition) failures.Add(label);
		}
		try {
			Mod soulmates = ModLoader.GetMod("Soulmates");
			Check(soulmates.Version == new Version(0, 12, 2), "Wrong packaged version: " + soulmates.Version);
			var core = new Item(ModContent.ItemType<Soulcore>());
			Check(!core.consumable, "Soulcore marked consumable");
			Check(!ItemLoader.ConsumeItem(core, new Player()), "Inventory right-click consumes the reusable Soulcore");
			CheckMenuInput(Check, core);
			CheckWorldPickup(Check);
			foreach (string culture in new[] { "en-US", "de-DE" }) {
				LanguageManager.Instance.SetLanguage(culture);
				JsonValue catalog = HjsonValue.Parse(Encoding.UTF8.GetString(soulmates.GetFileBytes("Localization/" + culture + ".hjson")));
				void Walk(JsonValue value, string path)
				{
					if (value is JsonObject map) {
						foreach (var entry in map)
							Walk(entry.Value, path.Length == 0 ? entry.Key : path + "." + entry.Key);
					}
					else {
						Check(Language.Exists(path), culture + ": unregistered key " + path);
						Check(Language.GetTextValue(path) == (string)value, culture + ": wrong runtime text " + path);
					}
				}
				Walk(catalog, "");
				var legacy = new CompanionProfile { LastMemory = "Mods.Soulmates.Memories.New" };
				Check(legacy.LastMemory == SoulmatesText.Get("Memories.New"), "Saved localization key not repaired");
				legacy.LastMemory = "Mods.Soulmates.Missing.OldKey";
				Check(legacy.LastMemory == SoulmatesText.Get("Memories.New"), "Broken saved key leaked into a reply");
				Check(new CompanionProfile().LastMemory == SoulmatesText.Get("Memories.New"), "Default memory cached too early");
			}

			var profile = new CompanionProfile { MiningApproach = CompanionMiningApproach.Surface };
			foreach (int type in new[] { ItemID.Gel, ItemID.Acorn, ItemID.StoneBlock, ItemID.Wood, ItemID.CopperOre, ItemID.DirtBlock }) {
				var item = new Item(type, 140);
				int limit = CompanionProfile.CarryLimitFor(type);
				Check(profile.Store(item) == limit, "Incorrect insertion limit for item " + type);
				Check(profile.ItemCount(type) == limit && item.stack == 140 - limit, "Insertion lost or duplicated item " + type);
				var repeated = new Item(type, 40);
				Check(profile.Store(repeated) == 0 && repeated.stack == 40, "Repeated pickup exceeded reserve for " + type);
			}
			Check(profile.PackLoad == 6, "Unlike item types merged");
			var clone = profile.Clone();
			clone.Pack[0].stack--;
			Check(profile.Pack[0].stack == 99, "Cloned pack shares mutable items");
			CompanionProfile loaded = CompanionProfile.Load(profile.Save());
			Check(loaded.PackLoad == 6 && loaded.ItemCount(ItemID.Acorn) == 12
				&& loaded.ItemCount(ItemID.Gel) == 99 && loaded.MiningApproach == CompanionMiningApproach.Surface,
				"Save/load lost cargo or mining approach");
			using (var stream = new MemoryStream()) {
				using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true)) profile.Write(writer);
				stream.Position = 0;
				using var reader = new BinaryReader(stream);
				CompanionProfile networked = CompanionProfile.Read(reader);
				Check(networked.PackLoad == 6 && networked.ItemCount(ItemID.Gel) == 99
					&& networked.MiningApproach == CompanionMiningApproach.Surface && stream.Position == stream.Length,
					"Network roundtrip lost cargo or changed packet layout");
			}
			var full = new CompanionProfile();
			for (int type = 1; full.PackLoad < full.PackCapacity && type < 100; type++) {
				var item = new Item(type, 1);
				full.Store(item);
			}
			var overflow = new Item(ItemID.Acorn, 12);
			Check(full.Store(overflow) == 0 && overflow.stack == 12, "Full pack consumed an item");

			Main.maxTilesX = 100;
			Main.maxTilesY = 100;
			Main.tile = (Tilemap)Activator.CreateInstance(typeof(Tilemap), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
				null, new object[] { (ushort)100, (ushort)100 }, null)!;
			var companion = new SoulboundCompanion();
			var available = new HashSet<Point>();
			for (int x = 20; x < 44; x++) {
				Tile tile = Main.tile[x, 40];
				tile.HasTile = true;
				tile.TileType = TileID.Copper;
				available.Add(new Point(x, 40));
			}
			const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
			typeof(SoulboundCompanion).GetMethod("AppendConnectedOreVein", flags)!
				.Invoke(companion, new object[] { new Point(20, 40), available, 24 });
			var targets = (List<Point>)typeof(SoulboundCompanion).GetField("plannedMiningTargets", flags)!.GetValue(companion)!;
			Check(targets.Count == 24, "Vein limit counted empty neighbors instead of real ore blocks");
			companion.Profile.MiningApproach = CompanionMiningApproach.Surface;
			Point candidate = new(50, 50);
			foreach (Point point in new[] { candidate, new Point(49, 50), new Point(51, 50), new Point(50, 49), new Point(50, 51) }) {
				Tile tile = Main.tile[point.X, point.Y];
				tile.HasTile = true;
				tile.TileType = TileID.Stone;
			}
			MethodInfo allowed = typeof(SoulboundCompanion).GetMethod("IsAllowedByMiningApproach", flags)!;
			bool IsAllowed(ushort type) => (bool)allowed.Invoke(companion, new object[] { candidate, type })!;
			Check(!IsAllowed(TileID.Stone), "Surface allowed buried material");
			Tile neighbor = Main.tile[49, 50];
			neighbor.HasTile = false;
			Check(IsAllowed(TileID.Stone), "Surface rejected an exposed edge");
			neighbor.HasTile = true;
			Check(!IsAllowed(TileID.Stone), "Surface failed to recheck a newly blocked edge");
			companion.Profile.MiningApproach = CompanionMiningApproach.Adaptive;
			Check(!IsAllowed(TileID.Sand), "Adaptive allowed buried unstable material");
		}
		catch (Exception error) {
			failures.Add(error.ToString());
		}
		string report = $"ENGINE CHECKS: {assertions} assertions, {failures.Count} failures\n" + string.Join("\n", failures);
		File.WriteAllText(Path.Combine(Main.SavePath, "Soulmates-engine-checks.txt"), report);
		Console.WriteLine(report);
		// This isolated probe never joins or loads a player's world.
		Environment.Exit(failures.Count == 0 ? 0 : 1);
	}

	private static void CheckWorldPickup(Action<bool, string> check)
	{
		Player original = Main.player[0];
		try {
			Main.player[0] = new Player { whoAmI = 0, active = true };
			var npc = new NPC();
			npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
			var companion = (SoulboundCompanion)npc.ModNPC;
			MethodInfo collect = typeof(SoulboundCompanion).GetMethod("StoreLooseItem",
				BindingFlags.Instance | BindingFlags.NonPublic)!;
			int Collect(Item item) => (int)collect.Invoke(companion, new object[] { item })!;
			foreach (int type in new[] { ItemID.Gel, ItemID.Acorn, ItemID.StoneBlock, ItemID.Wood, ItemID.CopperOre }) {
				var drop = new Item(type, 137) { active = true, playerIndexTheItemIsReservedFor = 255 };
				int limit = CompanionProfile.CarryLimitFor(type);
				check(Collect(drop) == limit, "World pickup limit failed: " + type);
				check(drop.active && drop.type == type && drop.stack == 137 - limit,
					"Uncollected remainder disappeared or changed type: " + type);
				for (int repeat = 0; repeat < 100; repeat++)
					check(Collect(drop) == 0 && companion.Profile.ItemCount(type) + drop.stack == 137,
						"Repeated world pickup duplicated or lost units: " + type + "/" + repeat);
			}
			check(companion.Profile.PackLoad == 5, "World pickup merged different item types");
			foreach (int type in new[] { ItemID.Heart, ItemID.Star, ModContent.ItemType<Soulcore>(), ModContent.ItemType<SoulboundSigil>() }) {
				var protectedDrop = new Item(type, 1) { active = true, playerIndexTheItemIsReservedFor = 255 };
				check(Collect(protectedDrop) == 0 && protectedDrop.active && protectedDrop.stack == 1,
					"Pickup consumed a protected item: " + type);
			}
			var reserved = new Item(ItemID.IronOre, 7) { active = true, playerIndexTheItemIsReservedFor = 1 };
			check(Collect(reserved) == 0 && reserved.stack == 7, "Pickup stole another player's reservation");
			reserved.playerIndexTheItemIsReservedFor = 0;
			check(Collect(reserved) == 7 && !reserved.active && reserved.IsAir,
				"Owner-reserved pickup did not finish atomically");
			while (companion.Profile.PackLoad < companion.Profile.PackCapacity)
				companion.Profile.Store(new Item(ItemID.IronBar + companion.Profile.PackLoad, 1));
			var overflow = new Item(ItemID.DirtBlock, 5) { active = true, playerIndexTheItemIsReservedFor = 255 };
			check(Collect(overflow) == 0 && overflow.stack == 5 && overflow.active,
				"Full pack destroyed a world drop");
			var bound = new Item(ModContent.ItemType<SoulboundSigil>());
			var sigil = (SoulboundSigil)bound.ModItem;
			sigil.Profile = companion.Profile.Clone();
			Main.player[0].inventory[0] = bound;
			companion.SyncProfileToBoundSigil();
			check(sigil.Profile.ItemCount(ItemID.Gel) == 99 && sigil.Profile.ItemCount(ItemID.Acorn) == 12,
				"World cargo did not persist to its bound sigil");
		}
		finally {
			Main.player[0] = original;
		}
	}

	private static void CheckMenuInput(Action<bool, string> check, Item core)
	{
		bool oldMenu = Main.gameMenu;
		int oldPlayer = Main.myPlayer;
		Player original = Main.player[0];
		try {
			Main.dedServ = false;
			Main.gameMenu = false;
			Main.myPlayer = 0;
			Main.player[0] = new Player { whoAmI = 0, active = true };
			Player player = Main.player[0];
			player.SetTalkNPC(-1);
			SoulmatesPlayer controls = player.GetModPlayer<SoulmatesPlayer>();
			Main.mouseLeft = Main.mouseRight = false;
			foreach ((ModSystem system, string field) in new (ModSystem, string)[] {
				(ModContent.GetInstance<CompanionWheelSystem>(), "open"),
				(ModContent.GetInstance<InitiativePromptSystem>(), "open"),
				(ModContent.GetInstance<DirectOrderSystem>(), "active")
			}) {
				FieldInfo state = system.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!;
				state.SetValue(system, true);
				check(!controls.CanUseItem(core), system.Name + " allowed held item use");
				player.controlUseItem = player.controlUseTile = true;
				Main.mouseLeft = true;
				controls.SetControls();
				check(!player.controlUseItem && !player.controlUseTile, system.Name + " leaked controls");
				Type input = typeof(SoulmatesPlayer).Assembly.GetType("Soulmates.Common.UI.SoulmatesUIInput")!;
				bool CanPrompt() => (bool)input.GetProperty("CanPresentInitiative")!.GetValue(null)!;
				check(!CanPrompt(), system.Name + " can be replaced by a prompt");
				state.SetValue(system, false);
				controls.SetControls();
				check(!controls.CanUseItem(core), system.Name + " leaked closing click");
				Main.mouseLeft = Main.mouseRight = false;
				controls.SetControls();
				check(controls.CanUseItem(core), system.Name + " kept items blocked after release");
				check(CanPrompt(), system.Name + " failed to release deferred prompt");
				player.SetTalkNPC(1);
				check(!CanPrompt(), "Prompt interrupts a vanilla NPC conversation");
				PropertyInfo speech = typeof(CompanionSpeechSystem).GetProperty("CanShowSpeech",
					BindingFlags.Static | BindingFlags.NonPublic)!;
				check(!(bool)speech.GetValue(null)!, "Speech covers a vanilla NPC conversation");
				player.SetTalkNPC(-1);
				Main.playerInventory = true;
				check(!CanPrompt(), "Prompt interrupts inventory");
				check(!(bool)speech.GetValue(null)!, "Speech covers inventory");
				Main.playerInventory = false;
				check((bool)speech.GetValue(null)!, "Speech stays hidden after vanilla UI closes");
			}
		}
		finally {
			Main.dedServ = true;
			Main.gameMenu = oldMenu;
			Main.myPlayer = oldPlayer;
			Main.player[0] = original;
			Main.mouseLeft = Main.mouseRight = false;
			Main.playerInventory = false;
		}
	}
}
