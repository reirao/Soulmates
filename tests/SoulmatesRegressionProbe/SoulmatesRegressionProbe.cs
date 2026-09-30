using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BigInteger = System.Numerics.BigInteger;
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
			Check(soulmates.Version == new Version(0, 13, 0), "Wrong packaged version: " + soulmates.Version);
			var core = new Item(ModContent.ItemType<Soulcore>());
			Check(!core.consumable, "Soulcore marked consumable");
			Check(!ItemLoader.ConsumeItem(core, new Player()), "Inventory right-click consumes the reusable Soulcore");
			CheckMenuInput(Check, core);
			CheckWorldPickup(Check);
			CheckCargoGrowthAndWallet(Check);
			CheckCargoLayout(Check);
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
				int limit = profile.CarryLimitFor(item);
				Check(profile.Store(item) == limit, "Incorrect insertion limit for item " + type);
				Check(profile.ItemCount(type) == limit && item.stack == 140 - limit, "Insertion lost or duplicated item " + type);
				var repeated = new Item(type, 40);
				Check(profile.Store(repeated) == 0 && repeated.stack == 40, "Repeated pickup exceeded reserve for " + type);
			}
			Check(profile.ResourceLoad == 6 && profile.PackLoad == 0, "Unlike item types merged or resources occupied equipment");
			var clone = profile.Clone();
			clone.Resources[0].stack--;
			Check(profile.Resources[0].stack == 50, "Cloned resources share mutable items");
			CompanionProfile loaded = CompanionProfile.Load(profile.Save());
			Check(loaded.ResourceLoad == 6 && loaded.ItemCount(ItemID.Acorn) == 50
				&& loaded.ItemCount(ItemID.Gel) == 50 && loaded.MiningApproach == CompanionMiningApproach.Surface,
				"Save/load lost cargo or mining approach");
			using (var stream = new MemoryStream()) {
				using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true)) profile.Write(writer);
				stream.Position = 0;
				using var reader = new BinaryReader(stream);
				CompanionProfile networked = CompanionProfile.Read(reader);
				Check(networked.ResourceLoad == 6 && networked.ItemCount(ItemID.Gel) == 50
					&& networked.MiningApproach == CompanionMiningApproach.Surface && stream.Position == stream.Length,
					"Network roundtrip lost cargo or changed packet layout");
			}
			var full = new CompanionProfile();
			FillEquipment(full);
			var overflow = new Item(ItemID.CopperShortsword, 1);
			Check(full.Store(overflow) == 0 && overflow.stack == 1, "Full equipment pack consumed an item");
			Check(full.Store(new Item(ItemID.Acorn, 12)) == 12, "Equipment fullness blocked separate resources");

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
				int limit = companion.Profile.CarryLimitFor(drop);
				check(Collect(drop) == limit, "World pickup limit failed: " + type);
				check(drop.active && drop.type == type && drop.stack == 137 - limit,
					"Uncollected remainder disappeared or changed type: " + type);
				for (int repeat = 0; repeat < 100; repeat++)
					check(Collect(drop) == 0 && companion.Profile.ItemCount(type) + drop.stack == 137,
						"Repeated world pickup duplicated or lost units: " + type + "/" + repeat);
			}
			check(companion.Profile.ResourceLoad == 5 && companion.Profile.PackLoad == 0, "World pickup merged different item types");
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
			FillResources(companion.Profile, ItemID.DirtBlock);
			var overflow = new Item(ItemID.DirtBlock, 5) { active = true, playerIndexTheItemIsReservedFor = 255 };
			check(Collect(overflow) == 0 && overflow.stack == 5 && overflow.active,
				"Full pack destroyed a world drop");
			var bound = new Item(ModContent.ItemType<SoulboundSigil>());
			var sigil = (SoulboundSigil)bound.ModItem;
			sigil.Profile = companion.Profile.Clone();
			Main.player[0].inventory[0] = bound;
			companion.SyncProfileToBoundSigil();
			check(sigil.Profile.ItemCount(ItemID.Gel) == 50 && sigil.Profile.ItemCount(ItemID.Acorn) == 50,
				"World cargo did not persist to its bound sigil");
		}
		finally {
			Main.player[0] = original;
		}
	}

	private static void FillEquipment(CompanionProfile profile)
	{
		while (profile.PackLoad < profile.PackCapacity) {
			if (profile.Store(new Item(ItemID.CopperShortsword, 1)) == 0)
				throw new InvalidOperationException("Could not fill equipment slots.");
		}
	}

	private static void FillResources(CompanionProfile profile, int excludedType)
	{
		for (int type = 1; type < ItemID.Count && profile.ResourceLoad < CompanionProfile.MaximumResourceSlots; type++) {
			var item = new Item(type, 1);
			if (type != excludedType && CompanionProfile.IsResource(item))
				profile.Store(item);
		}
		if (profile.ResourceLoad != CompanionProfile.MaximumResourceSlots)
			throw new InvalidOperationException("Could not fill resource slots.");
	}

	private static void CheckCargoGrowthAndWallet(Action<bool, string> check)
	{
		foreach (int type in new[] { ItemID.Wood, ItemID.StoneBlock, ItemID.Gel, ItemID.Acorn, ItemID.CopperOre }) {
			for (int level = 1; level <= 20; level++) {
				var profile = new CompanionProfile { Experience = CompanionProfile.ExperienceForLevel(level) };
				var source = new Item(type, 1100);
				int expected = level * 50;
				check(profile.ResourceCarryLimit == expected, "Wrong resource capacity at level " + level);
				check(profile.Store(source) == expected && profile.ItemCount(type) == expected
					&& source.stack == 1100 - expected, "Level-scaled resource transfer failed: " + level + "/" + type);
				check(profile.Store(source) == 0, "Full resource type accepted repeated pickup");
			}
		}
		var growing = new CompanionProfile();
		growing.Store(new Item(ItemID.Wood, 50));
		growing.Experience = CompanionProfile.ExperienceForLevel(10);
		check(growing.Store(new Item(ItemID.Wood, 500)) == 450 && growing.ItemCount(ItemID.Wood) == 500,
			"Level 10 did not expand existing storage to 500");
		growing.Experience = CompanionProfile.ExperienceForLevel(20);
		check(growing.Store(new Item(ItemID.Wood, 600)) == 500 && growing.ItemCount(ItemID.Wood) == 1000,
			"Level 20 did not expand existing storage to 1000");
		check(!CompanionProfile.IsResource(new Item(ItemID.LesserHealingPotion))
			&& !CompanionProfile.IsResource(new Item(ItemID.CopperShortsword))
			&& CompanionProfile.IsResource(new Item(ItemID.Torch))
			&& !CompanionProfile.IsResource(new Item(ItemID.GoldCoin)), "Storage classification misplaced usable equipment or coins");

		var legacy = new CompanionProfile();
		var oldTag = legacy.Save();
		oldTag.Remove("resources");
		oldTag.Remove("walletCopper");
		oldTag["pack"] = new List<Terraria.ModLoader.IO.TagCompound> {
			Terraria.ModLoader.IO.ItemIO.Save(new Item(ItemID.Gel, 99)),
			Terraria.ModLoader.IO.ItemIO.Save(new Item(ItemID.CopperShortsword, 1)),
			Terraria.ModLoader.IO.ItemIO.Save(new Item(ItemID.GoldCoin, 99))
		};
		legacy = CompanionProfile.Load(oldTag);
		check(legacy.ResourceLoad == 1 && legacy.PackLoad == 1 && legacy.ItemCount(ItemID.Gel) == 99
			&& legacy.WalletCopper == 990_000, "Legacy migration lost items or coins");
		legacy.Normalize();
		check(legacy.WalletCopper == 990_000, "Repeated normalization duplicated legacy coins");
		List<Item> excess = legacy.ExtractExcess(ItemID.Gel, legacy.ResourceCarryLimit);
		check(excess.Count == 1 && excess[0].stack == 49 && legacy.ItemCount(ItemID.Gel) == 50,
			"Legacy overflow was not preserved for safe return");

		var wallet = new CompanionProfile();
		FillEquipment(wallet);
		FillResources(wallet, ItemID.DirtBlock);
		BigInteger expectedBalance = BigInteger.Zero;
		foreach (int type in new[] { ItemID.CopperCoin, ItemID.SilverCoin, ItemID.GoldCoin, ItemID.PlatinumCoin }) {
			var source = new Item(type, 137);
			expectedBalance += (BigInteger)137 * CompanionProfile.CoinValue(type);
			check(wallet.Store(source) == 137 && source.IsAir && wallet.WalletCopper == expectedBalance,
				"Full cargo blocked or duplicated coins of type " + type);
		}
		check(wallet.PackLoad == wallet.PackCapacity && wallet.ResourceLoad == 60,
			"Wallet used ordinary cargo slots");
		var hugeTag = wallet.Save();
		BigInteger huge = BigInteger.Pow(10, 60) + 321;
		hugeTag["walletCopper"] = huge.ToString();
		wallet = CompanionProfile.Load(hugeTag);
		check(wallet.WalletCopper == huge && wallet.Store(new Item(ItemID.PlatinumCoin, 9999)) == 9999
			&& wallet.WalletCopper == huge + 9_999_000_000L, "Unlimited wallet overflowed or truncated");
		CompanionProfile saved = CompanionProfile.Load(wallet.Save());
		check(saved.WalletCopper == wallet.WalletCopper && saved.ResourceLoad == 60,
			"Saving lost the unlimited wallet or resources");
		CompanionProfile clone = wallet.Clone();
		clone.RemoveWalletCoins(ItemID.CopperCoin, 1);
		clone.Resources[0].stack++;
		check(clone.WalletCopper == wallet.WalletCopper - 1 && clone.Resources[0].stack != wallet.Resources[0].stack,
			"Cloned cargo or wallet shared mutable state");
		using (var stream = new MemoryStream()) {
			using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) wallet.Write(writer);
			stream.Position = 0;
			using var reader = new BinaryReader(stream);
			CompanionProfile networked = CompanionProfile.Read(reader);
			check(networked.WalletCopper == wallet.WalletCopper && networked.ResourceLoad == 60
				&& networked.PackLoad == wallet.PackLoad && stream.Position == stream.Length,
				"Network serialization lost huge wallet or separated cargo");
		}
		BigInteger before = wallet.WalletCopper;
		check(!wallet.RemoveWalletCoins(ItemID.Wood, 1) && !wallet.RemoveWalletCoins(ItemID.GoldCoin, -1)
			&& wallet.WalletCopper == before, "Invalid withdrawal created or destroyed money");
		wallet.ClearCargo();
		check(wallet.PackLoad == 0 && wallet.ResourceLoad == 0 && wallet.WalletCopper.IsZero,
			"New-companion reset retained cargo or money");
		CheckCargoWithdrawals(check);
	}

	private static void CheckCargoWithdrawals(Action<bool, string> check)
	{
		Player original = Main.player[0];
		int originalMode = Main.netMode;
		try {
			Main.netMode = NetmodeID.SinglePlayer;
			var owner = new Player { whoAmI = 0, active = true };
			Main.player[0] = owner;
			var npc = new NPC();
			npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
			var companion = (SoulboundCompanion)npc.ModNPC;
			companion.Profile.Store(new Item(ItemID.GoldCoin, 137));
			companion.Profile.Store(new Item(ItemID.Wood, 50));
			BigInteger Coins() {
				BigInteger balance = BigInteger.Zero;
				foreach (Item item in owner.inventory)
					if (!item.IsAir) balance += (BigInteger)item.stack * CompanionProfile.CoinValue(item.type);
				return balance;
			}
			BigInteger total = companion.Profile.WalletCopper + Coins();
			companion.WithdrawStorageSlot(CompanionStorage.Wallet, 1, singleItem: true);
			check(companion.Profile.WalletCopper == 1_360_000 && Coins() == 10_000
				&& companion.Profile.WalletCopper + Coins() == total, "Gold withdrawal violated currency conservation");
			for (int i = 0; i < owner.inventory.Length; i++)
				owner.inventory[i] = new Item(ItemID.CopperShortsword, 1);
			BigInteger before = companion.Profile.WalletCopper;
			companion.WithdrawStorageSlot(CompanionStorage.Wallet, 0, singleItem: false);
			check(companion.Profile.WalletCopper == before, "Full player inventory destroyed wallet money");
			owner.inventory[0] = new Item(ItemID.Wood);
			owner.inventory[0].stack = owner.inventory[0].maxStack - 3;
			companion.WithdrawStorageSlot(CompanionStorage.Resources, 0, singleItem: false);
			check(companion.Profile.ItemCount(ItemID.Wood) == 47
				&& owner.inventory[0].stack == owner.inventory[0].maxStack,
				"Partial resource withdrawal destroyed or duplicated leftovers");
			companion.WithdrawStorageSlot((CompanionStorage)255, 0, true);
			check(companion.Profile.ItemCount(ItemID.Wood) == 47 && companion.Profile.WalletCopper == before,
				"Invalid storage request changed cargo");
			MethodInfo collect = typeof(SoulboundCompanion).GetMethod("StoreLooseItem",
				BindingFlags.Instance | BindingFlags.NonPublic)!;
			var drop = new Item(ItemID.SilverCoin, 137) { active = true, playerIndexTheItemIsReservedFor = 255 };
			check((int)collect.Invoke(companion, new object[] { drop })! == 137 && !drop.active && drop.IsAir
				&& companion.Profile.WalletCopper == before + 13_700, "World coin pickup was not atomic");
			check((int)collect.Invoke(companion, new object[] { drop })! == 0
				&& companion.Profile.WalletCopper == before + 13_700, "Repeated world coin pickup duplicated money");
			var hugeTag = companion.Profile.Save();
			BigInteger huge = BigInteger.Pow(10, 60);
			hugeTag["walletCopper"] = huge.ToString();
			companion.Profile = CompanionProfile.Load(hugeTag);
			var platinum = new Item(ItemID.PlatinumCoin, 9999) { active = true, playerIndexTheItemIsReservedFor = 255 };
			check((int)collect.Invoke(companion, new object[] { platinum })! == 9999 && platinum.IsAir
				&& companion.Profile.WalletCopper == huge + 9_999_000_000L,
				"World pickup overflowed an already enormous wallet");

			for (int i = 0; i < owner.inventory.Length; i++)
				owner.inventory[i] = new Item();
			companion.Profile = new CompanionProfile();
			companion.Profile.Pack.Add(new Item(ItemID.Gel, 99));
			companion.Profile.Pack.Add(new Item(ItemID.GoldCoin, 99));
			MethodInfo reconcile = typeof(SoulboundCompanion).GetMethod("ReconcilePackOnce",
				BindingFlags.Instance | BindingFlags.NonPublic)!;
			reconcile.Invoke(companion, null);
			int returnedGel = 0;
			foreach (Item item in owner.inventory)
				if (item.type == ItemID.Gel) returnedGel += item.stack;
			check(companion.Profile.ItemCount(ItemID.Gel) == 50 && returnedGel == 49
				&& companion.Profile.WalletCopper == 990_000, "Summon migration failed to return legacy resource excess");
			reconcile.Invoke(companion, null);
			check(companion.Profile.WalletCopper == 990_000 && companion.Profile.ItemCount(ItemID.Gel) == 50,
				"Repeated summon migration duplicated coins or resources");
		}
		finally {
			Main.player[0] = original;
			Main.netMode = originalMode;
		}
	}

	private static void CheckCargoLayout(Action<bool, string> check)
	{
		var profile = new CompanionProfile();
		FillResources(profile, ItemID.None);
		Type type = typeof(TalkModeState).Assembly.GetType("Soulmates.Common.UI.CompanionPackElement")!;
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
		var element = (Terraria.UI.UIElement)Activator.CreateInstance(type, flags | BindingFlags.Public, null,
			new object[] { new Func<CompanionProfile>(() => profile), new Action<CompanionStorage, int, bool>((_, _, _) => { }) }, null)!;
		MethodInfo slotBounds = type.GetMethod("CalculateSlotBounds", BindingFlags.Static | BindingFlags.NonPublic)!;
		FieldInfo storage = type.GetField("selectedStorage", flags)!;
		FieldInfo page = type.GetField("page", flags)!;
		MethodInfo changePage = type.GetMethod("ChangePage", flags)!;
		foreach (CompanionStorage kind in Enum.GetValues<CompanionStorage>()) {
			storage.SetValue(element, kind);
			int slots = kind == CompanionStorage.Wallet ? 4 : 12;
			foreach (int width in new[] { 40, 60, 90, 180, 300, 600 }) {
				Rectangle area = new(10, 20, width, 104);
				var bounds = new List<Rectangle>();
				for (int slot = 0; slot < slots; slot++) {
					Rectangle rectangle = (Rectangle)slotBounds.Invoke(null, new object[] { area, slot, kind == CompanionStorage.Wallet })!;
					check(area.Contains(rectangle) && rectangle.Width > 0 && rectangle.Top >= area.Top + 34,
						$"Cargo slot escaped bounds or overlapped tabs: {kind}/{width}/{slot}, area {area}, slot {rectangle}");
					foreach (Rectangle other in bounds)
						check(!rectangle.Intersects(other), $"Cargo slot hitboxes overlap: {kind}/{width}/{slot}");
					bounds.Add(rectangle);
				}
			}
		}
		storage.SetValue(element, CompanionStorage.Resources);
		page.SetValue(element, 0);
		changePage.Invoke(element, new object[] { -1 });
		check((int)page.GetValue(element)! == 4, "Resource page did not wrap backwards to the fifth page");
		changePage.Invoke(element, new object[] { 1 });
		check((int)page.GetValue(element)! == 0, "Resource page did not wrap forwards to the first page");
		storage.SetValue(element, CompanionStorage.Wallet);
		changePage.Invoke(element, new object[] { 1 });
		check((int)page.GetValue(element)! == 0, "Wallet inherited a resource page offset");
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
