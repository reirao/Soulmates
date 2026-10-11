#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Common.Feedback;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Soulmates.Content.Projectiles;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.Net;

namespace SoulmatesRegressionProbe;

public sealed partial class EngineChecks
{
	private static void CheckRetinue(Action<bool, string> check, Mod mod)
	{
		const BindingFlags instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		const BindingFlags statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
		Player[] oldPlayers = Main.player; Player oldClientPlayer = Main.clientPlayer;
		NPC[] oldNpcs = Main.npc; Projectile[] oldProjectiles = Main.projectile; Item[] oldItems = Main.item;
		var oldDust = Main.dust; var oldGore = Main.gore; var oldCombatText = Main.combatText;
		int[,] oldIdentities = Main.projectileIdentity;
		Tilemap oldTiles = Main.tile;
		int oldWidth = Main.maxTilesX, oldHeight = Main.maxTilesY, oldMode = Main.netMode, oldLocal = Main.myPlayer;
		float oldLeft = Main.leftWorld, oldRight = Main.rightWorld, oldTop = Main.topWorld, oldBottom = Main.bottomWorld;
		bool oldMenu = Main.gameMenu; var oldRandom = Main.rand;
		RemoteClient[] oldClients = (RemoteClient[])Netplay.Clients.Clone();
		var oldBuffers = NetMessage.buffer;
		bool oldRecording = ProbeSocket.Recording, oldWatching = RecoveryPacketSpy.Watching;
		var oldSent = ProbeSocket.Sent.ToArray(); var oldPackets = RecoveryPacketSpy.Packets.ToArray();
		var collisionState = typeof(Collision).GetFields(statics).Where(field => !field.IsInitOnly && !field.IsLiteral)
			.Select(field => (Field: field, Value: field.GetValue(null))).ToArray();
		FieldInfo feedbackActive = typeof(SoulmatesFeedbackSystem).GetField("sessionActive", statics)!;
		object? oldFeedbackActive = feedbackActive.GetValue(null);
		var restoreTransactions = new List<Action>();
		Type sync = typeof(CompanionProfile).Assembly.GetType("Soulmates.Common.CompanionInventorySync")!;
		object? Call(SoulboundCompanion mate, string name, params object[] args)
			=> typeof(SoulboundCompanion).GetMethod(name, instance)!.Invoke(mate, args);
		bool Configure(SoulboundCompanion mate, int slot, int type, byte[] token)
			=> (bool)Call(mate, "ConfigureRetinue", slot, type, token)!;
		bool Maintain(SoulboundCompanion mate)
			=> (bool)typeof(SoulboundCompanion).GetProperty("CanMaintainRetinue", instance)!.GetValue(mate)!;
		NPC? Target(SoulboundCompanion mate)
			=> (NPC?)typeof(SoulboundCompanion).GetProperty("RetinueTarget", instance)!.GetValue(mate);
		void SetTarget(SoulboundCompanion mate, int index)
			=> typeof(SoulboundCompanion).GetField("guardianTarget", instance)!.SetValue(mate, index);
		byte[] Bytes(Item item, bool favorite = false) {
			using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
			ItemIO.Send(item, writer, writeStack: true, writeFavorite: favorite);
			return stream.ToArray();
		}
		byte[] SourceToken(Item item) => SHA256.HashData(Bytes(item, true));
		byte[] StorageToken(SoulboundCompanion mate, int slot) => (byte[])typeof(CompanionProfile)
			.GetMethod("StorageToken", instance)!.Invoke(mate.Profile, new object[] { CompanionStorage.Summons, slot })!;
		bool Store(SoulboundCompanion mate, int slot, byte[] token) {
			object[] args = { slot, token, "" };
			return (bool)Call(mate, "StorePetItem", args)!;
		}
		bool Belongs(Projectile projectile, SoulboundCompanion mate) => projectile.ModProjectile is CompanionRetainer or CompanionRetinueBolt
			&& (bool)projectile.ModProjectile.GetType().GetMethod("BelongsTo", instance)!.Invoke(projectile.ModProjectile, new object[] { mate })!;
		Projectile[] Effects(SoulboundCompanion mate) => Main.projectile.Where(p => p.active && Belongs(p, mate)).ToArray();
		Projectile[] Retainers(SoulboundCompanion mate) => Effects(mate).Where(p => p.ModProjectile is CompanionRetainer).ToArray();
		void Update(SoulboundCompanion mate) => Call(mate, "UpdateRetinue");
		void Tick(SoulboundCompanion mate, int count) {
			for (int tick = 0; tick < count; tick++) {
				Update(mate);
				foreach (Projectile projectile in Main.projectile.Where(p => p.active
					&& p.ModProjectile is CompanionRetainer or CompanionRetinueBolt).ToArray())
					if (projectile.active) projectile.Update(projectile.whoAmI);
			}
		}
		CompanionProfile BinaryCopy(CompanionProfile profile) {
			using var stream = new MemoryStream();
			using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) profile.Write(writer);
			stream.Position = 0; using var reader = new BinaryReader(stream);
			CompanionProfile result = CompanionProfile.Read(reader);
			check(stream.Position == stream.Length, "Retinue: binary profile left unread bytes");
			return result;
		}
		try {
			// Preserve existing transactions, including pending work owned by other fixtures.
			foreach (string name in new[] { "Transactions", "Receipts", "Editing", "AwaitingProfiles", "OpenReceipts" }) {
				object collection = sync.GetField(name, statics)!.GetValue(null)!;
				if (collection is IDictionary dictionary) {
					var entries = new List<DictionaryEntry>();
					foreach (DictionaryEntry entry in dictionary) entries.Add(entry);
					restoreTransactions.Add(() => { dictionary.Clear(); foreach (DictionaryEntry entry in entries) dictionary.Add(entry.Key, entry.Value); });
					dictionary.Clear();
				}
				else {
					object[] entries = ((IEnumerable)collection).Cast<object>().ToArray();
					MethodInfo clear = collection.GetType().GetMethod("Clear")!, add = collection.GetType().GetMethod("Add")!;
					restoreTransactions.Add(() => { clear.Invoke(collection, null); foreach (object entry in entries) add.Invoke(collection, new[] { entry }); });
					clear.Invoke(collection, null);
				}
			}
			feedbackActive.SetValue(null, false);
			Main.rand = new Terraria.Utilities.UnifiedRandom(20261011);
			ProbeSocket.Recording = false; RecoveryPacketSpy.Watching = true;
			NetMessage.buffer = Enumerable.Range(0, oldBuffers.Length).Select(i => new MessageBuffer { whoAmI = i }).ToArray();
			for (int i = 0; i < Netplay.Clients.Length; i++) Netplay.Clients[i] = new RemoteClient { Socket = new ProbeSocket() };
			Main.maxTilesX = 240; Main.maxTilesY = 160; Main.gameMenu = false;
			Main.leftWorld = Main.topWorld = 0; Main.rightWorld = 240 * 16; Main.bottomWorld = 160 * 16;
			Main.tile = (Tilemap)Activator.CreateInstance(typeof(Tilemap), instance, null, new object[] { (ushort)240, (ushort)160 }, null)!;
			Main.dust = Enumerable.Range(0, oldDust.Length).Select(_ => new Dust()).ToArray();
			Main.gore = Enumerable.Range(0, oldGore.Length).Select(_ => new Gore()).ToArray();
			// Saturate cosmetic text slots, as in the native combat fixture, to avoid headless font allocation.
			Main.combatText = Enumerable.Range(0, oldCombatText.Length).Select(_ => new CombatText { active = true }).ToArray();
			Main.item = Enumerable.Range(0, oldItems.Length).Select(i => new Item { whoAmI = i }).ToArray();
			Main.clientPlayer = new Player();

			// Native classification is the oracle; do not discover the catalog through the API under test.
			var native = new List<int>();
			for (int type = 1; type < ItemID.Count; type++) {
				var item = new Item(type);
				if (!item.CountsAsClass(DamageClass.Summon) || item.damage <= 0) continue;
				ContentSamples.ProjectilesByType.TryGetValue(item.shoot, out Projectile? shot);
				bool staff = !item.consumable && item.shoot > 0 && shot is not null && (shot.minion || shot.sentry || item.sentry);
				check(CompanionRetinue.IsStaff(item) == staff, $"Retinue: native summon classification disagrees for {type}/{item.Name}");
				if (staff) native.Add(type);
				mod.Logger.Info($"Retinue native {(staff ? "covered" : "excluded")}: {type}/{item.Name}; shoot={item.shoot}; "
					+ (staff ? $"visual={CompanionRetinue.VisualFor(type)}, minion={shot!.minion}, sentry={item.sentry || shot.sentry}"
						: item.consumable ? "consumable weapon" : "projectile is neither a native minion nor a sentry (including whips)"));
			}
			int[] catalog = native.ToArray();
			int[] required = { ItemID.SlimeStaff, ItemID.BabyBirdStaff, ItemID.FlinxStaff, ItemID.VampireFrogStaff,
				ItemID.AbigailsFlower, ItemID.HornetStaff, ItemID.ImpStaff, ItemID.SpiderStaff, ItemID.OpticStaff,
				ItemID.PirateStaff, ItemID.PygmyStaff, ItemID.RavenStaff, ItemID.TempestStaff, ItemID.XenoStaff,
				ItemID.DeadlySphereStaff, ItemID.SanguineStaff, ItemID.StormTigerStaff, ItemID.StardustCellStaff,
				ItemID.StardustDragonStaff, ItemID.QueenSpiderStaff, ItemID.StaffoftheFrostHydra,
				ItemID.RainbowCrystalStaff, ItemID.MoonlordTurretStaff };
			foreach (int type in required) check(catalog.Contains(type), "Retinue: native staff omitted from discovery: " + type);
			check(catalog.Length >= required.Length && catalog.Length <= CompanionProfile.MaximumSummonSlots
				&& CompanionProfile.MaximumSummonSlots == 64, "Retinue: native catalog or separate 64-slot storage is incomplete");
			foreach (int type in new[] { ItemID.None, ItemID.CopperOre, ItemID.Wood, ItemID.Carrot, ItemID.ZephyrFish,
				ItemID.RubyStaff, ItemID.BlizzardStaff, ItemID.BlandWhip }) {
				check(!CompanionRetinue.IsStaff(new Item(type)) && CompanionRetinue.VisualFor(type) == 0,
					"Retinue: non-summoning item accepted: " + type);
				mod.Logger.Info($"Retinue explicit exclusion: {type}/{Lang.GetItemNameValue(type)} (resource, pet, magic staff, whip or air)");
			}
			check(CompanionRetinue.VisualFor(-1) == 0 && CompanionRetinue.VisualFor(int.MaxValue) == 0,
				"Retinue: unknown item type acquired native artwork");
			if (catalog.Length < required.Length) return;

			foreach (int type in catalog) {
				var profile = new CompanionProfile { RetinueEnabled = true, RetinueItemType = type };
				var source = new Item(type); source.Prefix(PrefixID.Ruthless);
				byte[] metadata = Bytes(source);
				check(CompanionProfile.StorageFor(source) == CompanionStorage.Summons && profile.Store(source) == 1 && source.IsAir,
					"Retinue: native staff failed separate storage: " + type);
				check(profile.StorageItems(CompanionStorage.Summons) == profile.SummonItems && profile.ItemCount(type) == 1,
					"Retinue: summon storage lost ownership: " + type);
				check(CompanionRetinue.VisualFor(type) == new Item(type).shoot && Main.projFrames[CompanionRetinue.VisualFor(type)] >= 1,
					"Retinue: missing native artwork or animation frames: " + type);
				foreach (CompanionProfile copy in new[] { profile.Clone(), CompanionProfile.Load(profile.Save()), BinaryCopy(profile) }) {
					check(copy.RetinueEnabled && copy.RetinueItemType == type && copy.SummonItems.Count == 1
						&& copy.Pack.Count == 0 && copy.PetItems.Count == 0 && copy.ItemCount(type) == 1
						&& Bytes(copy.SummonItems[0]).SequenceEqual(metadata), "Retinue: save/clone/binary lost staff metadata or selection: " + type);
					check(!ReferenceEquals(copy.SummonItems, profile.SummonItems) && !ReferenceEquals(copy.SummonItems[0], profile.SummonItems[0]),
						"Retinue: copied cargo aliases original staff: " + type);
					copy.SummonItems[0].stack++;
					check(profile.ItemCount(type) == 1, "Retinue: copy mutation changed original staff: " + type);
				}
				profile.RetinueEnabled = false;
				foreach (CompanionProfile copy in new[] { profile.Clone(), CompanionProfile.Load(profile.Save()), BinaryCopy(profile) })
					check(!copy.RetinueEnabled && copy.RetinueItemType == type && copy.ItemCount(type) == 1
						&& !CompanionRetinue.ActiveItems(copy).Any(), "Retinue: explicit dismissal was not persistent: " + type);
			}
			var selection = new CompanionProfile { Name = "AETHER", RetinueEnabled = true, RetinueItemType = catalog.Last(),
				SummonItems = catalog.Select(type => new Item(type)).Concat(new[] { new Item(ItemID.SlimeStaff) }).ToList() };
			check(CompanionRetinue.ActiveItems(selection).Select(item => item.type).Order().SequenceEqual(catalog.Order()),
				"Retinue: ActiveItems omitted an AETHER staff or counted a duplicate");
			selection.Name = "Luma";
			foreach (int level in new[] { 1, 9, 10, 19, 20 }) {
				selection.Experience = CompanionProfile.ExperienceForLevel(level);
				Item[] active = CompanionRetinue.ActiveItems(selection).ToArray();
				check(active.Length == 1 + level / 10 && active[0].type == selection.RetinueItemType
					&& active.Select(item => item.type).Distinct().Count() == active.Length && active.All(selection.SummonItems.Contains),
					"Retinue: ActiveItems capacity/preference/ownership incorrect at level " + level);
				foreach (int type in catalog) {
					var item = new Item(type);
					check(CompanionRetinue.Damage(selection, item) == Math.Clamp(item.damage / 2, 2, 20 + level * 2),
						$"Retinue: adapted damage escaped its level cap for staff {type}, level {level}");
				}
			}
			var full = new CompanionProfile();
			for (int i = 0; i < full.PackCapacity; i++) full.Store(new Item(ItemID.CopperShortsword));
			for (int i = 0; i < 64; i++) check(full.Store(new Item(ItemID.SlimeStaff)) == 1, "Retinue: full Pack blocked summon slot " + i);
			var overflow = new Item(ItemID.ImpStaff); byte[] overflowMetadata = Bytes(overflow);
			check(full.StorageCapacity(CompanionStorage.Summons) == 64 && full.SummonItems.Count == 64
				&& full.Store(overflow) == 0 && Bytes(overflow).SequenceEqual(overflowMetadata), "Retinue: full summon storage lost cargo or exceeded 64 slots");
			var legacyStaff = new Item(ItemID.SlimeStaff); legacyStaff.Prefix(PrefixID.Ruthless);
			var legacy = new TagCompound { ["pack"] = new List<TagCompound> { ItemIO.Save(legacyStaff), ItemIO.Save(legacyStaff.Clone()),
				ItemIO.Save(new Item(ItemID.ImpStaff)), ItemIO.Save(new Item(ItemID.Wood, 7)) } };
			CompanionProfile migrated = CompanionProfile.Load(legacy); migrated.Normalize(); migrated.Normalize();
			check(migrated.SummonItems.Count == 3 && migrated.ItemCount(ItemID.SlimeStaff) == 2 && migrated.ItemCount(ItemID.ImpStaff) == 1
				&& migrated.ItemCount(ItemID.Wood) == 7 && !migrated.Pack.Any(CompanionRetinue.IsStaff)
				&& Bytes(migrated.SummonItems[0]).SequenceEqual(Bytes(legacyStaff)), "Retinue: legacy Pack migration lost, duplicated or stripped staffs");
			foreach (CompanionProfile copy in new[] { migrated.Clone(), CompanionProfile.Load(migrated.Save()), BinaryCopy(migrated) })
				check(copy.ItemCount(ItemID.SlimeStaff) == 2 && copy.SummonItems.Count == 3, "Retinue: migrated cargo changed on the next save");
			var empty = CompanionProfile.Load(new TagCompound());
			check(empty.SummonItems.Count == 0 && !empty.RetinueEnabled && empty.RetinueItemType == 0, "Retinue: old empty Sigil gained phantom summons");
			full.RetinueEnabled = true; full.RetinueItemType = ItemID.SlimeStaff; full.ClearCargo(); full.Normalize();
			check(full.SummonItems.Count == 0 && !full.RetinueEnabled && full.RetinueItemType == 0, "Retinue: ClearCargo left active or selected summons");
			var tooMany = new CompanionProfile { SummonItems = Enumerable.Range(0, 65).Select(_ => new Item(ItemID.SlimeStaff)).ToList() };
			bool refused = false;
			try { BinaryCopy(tooMany); } catch (InvalidDataException) { refused = true; }
			check(refused, "Retinue: binary transport accepted 65 summon slots");

			foreach (int mode in new[] { NetmodeID.SinglePlayer, NetmodeID.Server }) {
				ResetInventoryTransactions();
				Main.netMode = mode; Main.myPlayer = mode == NetmodeID.Server ? 255 : 0;
				Main.player = Enumerable.Range(0, oldPlayers.Length).Select(i => new Player { whoAmI = i }).ToArray();
				Main.npc = Enumerable.Range(0, oldNpcs.Length).Select(i => new NPC { whoAmI = i }).ToArray();
				Main.projectile = Enumerable.Range(0, oldProjectiles.Length).Select(i => new Projectile { whoAmI = i }).ToArray();
				Main.projectileIdentity = (int[,])oldIdentities.Clone();
				SoulboundCompanion Create(int npcIndex, int playerIndex, string name) {
					Player player = Main.player[playerIndex]; player.active = true; player.dead = false;
					player.statLife = player.statLifeMax2 = 100; player.Center = new Vector2(1280 + playerIndex * 240, 900);
					NPC npc = Main.npc[npcIndex]; npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
					npc.active = true; npc.ai[0] = playerIndex; npc.Center = player.Center;
					var mate = (SoulboundCompanion)npc.ModNPC; mate.Profile.Name = name; mate.Profile.AutonomyEnabled = false;
					player.inventory[0] = new Item(ModContent.ItemType<SoulboundSigil>());
					((SoulboundSigil)player.inventory[0].ModItem).Profile = mate.Profile.Clone();
					player.GetModPlayer<SoulmatesPlayer>().ActiveCompanionWhoAmI = npcIndex;
					return mate;
				}
				SoulboundCompanion mate = Create(20, 0, "AETHER"); Player owner = Main.player[0];
				void Assert(bool condition, string label) => check(condition, $"Retinue mode={mode}: {label}");
				SoulboundSigil Sigil() => (SoulboundSigil)owner.inventory[0].ModItem;
				int InventoryCount(int type) => owner.inventory.Where(item => item.type == type).Sum(item => item.stack);
				bool Equip(int type) {
					int slot = mate.Profile.SummonItems.FindIndex(item => item.type == type);
					return slot >= 0 && Configure(mate, slot, type, StorageToken(mate, slot));
				}
				void Gift(int type) {
					int slot = Array.FindIndex(owner.inventory, 1, 49, item => item.IsAir);
					Assert(slot >= 1, "gift fixture ran out of source slots"); if (slot < 1) return;
					owner.inventory[slot] = new Item(type); byte[] metadata = Bytes(owner.inventory[slot]);
					int before = mate.Profile.ItemCount(type);
					Assert(Store(mate, slot, SourceToken(owner.inventory[slot])), "real gift refused native staff " + type);
					SettleServerInventory(mod);
					Assert(owner.inventory[slot].IsAir && mate.Profile.ItemCount(type) == before + 1
						&& mate.Profile.SummonItems.Any(item => Bytes(item).SequenceEqual(metadata)), "gift lost item or metadata " + type);
				}
				owner.maxMinions = 0; owner.numMinions = 2; owner.slotsMinions = 3.25f; owner.maxTurrets = 0;
				owner.MinionAttackTargetNPC = 7; owner.buffType[0] = BuffID.BabySlime; owner.buffTime[0] = 12345;
				int[] buffs = (int[])owner.buffType.Clone(), buffTimes = (int[])owner.buffTime.Clone();
				int[] ownedCounts = (int[])owner.ownedProjectileCounts.Clone();
				Projectile playerMinion = Main.projectile[Main.maxProjectiles - 1]; playerMinion.SetDefaults(ProjectileID.BabySlime);
				playerMinion.active = true; playerMinion.owner = 0; playerMinion.Center = owner.Center; playerMinion.timeLeft = 12345;
				void PlayerUntouched() => Assert(owner.buffType.SequenceEqual(buffs) && owner.buffTime.SequenceEqual(buffTimes)
					&& owner.maxMinions == 0 && owner.numMinions == 2 && owner.slotsMinions == 3.25f && owner.maxTurrets == 0
					&& owner.MinionAttackTargetNPC == 7 && owner.ownedProjectileCounts.SequenceEqual(ownedCounts)
					&& playerMinion.active && playerMinion.type == ProjectileID.BabySlime && playerMinion.timeLeft == 12345,
					"summons changed player buffs, minion/sentry limits, occupied slots, target or existing minion");

				owner.inventory[25] = new Item(ItemID.SlimeStaff); owner.inventory[25].Prefix(PrefixID.Ruthless);
				byte[] giftMetadata = Bytes(owner.inventory[25]); byte[] giftToken = SourceToken(owner.inventory[25]);
				Assert(!Store(mate, 25, new byte[32]) && !Store(mate, 25, giftToken[..31])
					&& !Store(mate, -1, giftToken) && !Store(mate, 50, giftToken), "invalid gift slot/token accepted");
				owner.inventory[25].favorited = true; byte[] favoriteMetadata = Bytes(owner.inventory[25], true);
				Assert(!Store(mate, 25, SourceToken(owner.inventory[25])) && Bytes(owner.inventory[25], true).SequenceEqual(favoriteMetadata),
					"favorited staff was taken or mutated"); owner.inventory[25].favorited = false;
				owner.inventory[25].Prefix(PrefixID.Demonic);
				Assert(!Store(mate, 25, giftToken) && mate.Profile.SummonItems.Count == 0, "stale source metadata token accepted");
				owner.inventory[25] = new Item(ItemID.SlimeStaff); owner.inventory[25].Prefix(PrefixID.Ruthless);
				giftToken = SourceToken(owner.inventory[25]);
				owner.inventory[26] = new Item(ItemID.Wood, 7);
				Assert(!Store(mate, 26, SourceToken(owner.inventory[26])) && owner.inventory[26].stack == 7, "pet/staff transfer accepted ordinary cargo");
				NPC pendingEnemy = Main.npc[21]; pendingEnemy.SetDefaults(NPCID.BlueSlime); pendingEnemy.active = true;
				pendingEnemy.life = pendingEnemy.lifeMax = 10000; pendingEnemy.Center = owner.Center + new Vector2(80, 0);
				SetTarget(mate, 21); Assert(Target(mate) == pendingEnemy, "pending-target fixture has no valid initial hostile");
				Assert(Store(mate, 25, giftToken), "first real staff gift failed");
				Assert(owner.inventory[25].IsAir && mate.Profile.ItemCount(ItemID.SlimeStaff) == 1
					&& Bytes(mate.Profile.SummonItems[0]).SequenceEqual(giftMetadata), "first gift failed conservation/metadata");
				Projectile? first = Retainers(mate).SingleOrDefault();
				if (mode == NetmodeID.Server) {
					Assert(Maintain(mate), "pending inventory transaction made existing summons unavailable");
					Assert(Target(mate) is null, "pending inventory transaction retained a combat target");
					Tick(mate, 4);
					Assert(first is not null && first.active && Retainers(mate).SingleOrDefault() == first,
						"pending gift transaction despawned or replaced existing summon");
					Assert(!Configure(mate, 0, ItemID.SlimeStaff, StorageToken(mate, 0)), "pending inventory transaction accepted configuration");
				}
				SetTarget(mate, -1); pendingEnemy.active = false;
				SettleServerInventory(mod); Tick(mate, 40);
				Assert(!Store(mate, 25, giftToken) && mate.Profile.ItemCount(ItemID.SlimeStaff) == 1, "replayed gift duplicated staff");
				Assert(mate.Profile.RetinueEnabled && mate.Profile.RetinueItemType == ItemID.SlimeStaff
					&& Retainers(mate).Length == 1 && Sigil().Profile.ItemCount(ItemID.SlimeStaff) == 1, "first gift did not select/spawn/persist its summon");
				foreach (int type in catalog.Where(type => type != ItemID.SlimeStaff)) Gift(type);
				Gift(ItemID.SlimeStaff); Gift(ItemID.SlimeStaff);
				Tick(mate, (catalog.Length + 2) * 20);
				Projectile[] army = Retainers(mate);
				Assert(army.Length == catalog.Length && army.Select(p => (int)p.ai[1]).Order().SequenceEqual(catalog.Order()),
					"AETHER did not summon every distinct gifted native staff, or duplicates added minions");
				foreach (Projectile projectile in army) {
					Assert((int)projectile.ai[0] == mate.NPC.whoAmI && projectile.aiStyle == -1 && projectile.npcProj && !projectile.minion
						&& !projectile.sentry && projectile.minionSlots == 0 && projectile.friendly && !projectile.hostile,
						"retainer used player minion AI/slots or lost companion binding: " + projectile.ai[1]);
					Assert(projectile.frame >= 0 && projectile.frame < Main.projFrames[CompanionRetinue.VisualFor((int)projectile.ai[1])]
						&& projectile.localAI[0] > 0, "native Update did not advance adapted artwork AI: " + projectile.ai[1]);
					using var binding = new MemoryStream(); using var writer = new BinaryWriter(binding);
					projectile.ModProjectile.SendExtraAI(writer);
					Assert(binding.ToArray().SequenceEqual(mate.Profile.Id.ToByteArray()), "retainer did not transport its companion GUID");
				}
				var stable = army.ToDictionary(p => (int)p.ai[1], p => p);
				Tick(mate, 120);
				Assert(Retainers(mate).All(p => stable.TryGetValue((int)p.ai[1], out Projectile? original) && p == original),
					"healthy summons were continually respawned"); PlayerUntouched();
				mate.Profile.Name = "Luma";
				foreach (int level in new[] { 1, 9, 10, 19, 20 }) {
					mate.Profile.Experience = CompanionProfile.ExperienceForLevel(level);
					int preferred = catalog.Last(); Assert(Equip(preferred), "could not select owned preferred staff"); Tick(mate, 100);
					int[] active = Retainers(mate).Select(p => (int)p.ai[1]).ToArray();
					Assert(mate.Profile.Level == level && active.Length == 1 + level / 10 && active.Contains(preferred)
						&& active.Distinct().Count() == active.Length, "normal capacity/preferred selection incorrect at level " + level);
				}
				Assert(Configure(mate, -1, 0, new byte[32]) && Effects(mate).Length == 0, "dismiss did not immediately remove summons");
				Gift(ItemID.HornetStaff); Tick(mate, 60);
				Assert(!mate.Profile.RetinueEnabled && !Sigil().Profile.RetinueEnabled && Effects(mate).Length == 0,
					"later staff gift overrode explicit dismissal");
				int staleSlot = mate.Profile.SummonItems.FindIndex(item => item.type == ItemID.SlimeStaff);
				byte[] stale = StorageToken(mate, staleSlot); mate.Profile.SummonItems[staleSlot].Prefix(PrefixID.Demonic);
				Assert(!Configure(mate, staleSlot, ItemID.SlimeStaff, stale) && !Configure(mate, staleSlot, ItemID.CopperOre, StorageToken(mate, staleSlot))
					&& !Configure(mate, int.MaxValue, ItemID.SlimeStaff, new byte[32]) && !Configure(mate, staleSlot, ItemID.SlimeStaff, new byte[31])
					&& !Configure(mate, staleSlot, ItemID.SlimeStaff, new byte[33]) && !mate.Profile.RetinueEnabled,
					"stale/wrong-type/out-of-range/malformed configure changed dismissal");
				mate.Profile.Experience = 0; Assert(Equip(ItemID.SlimeStaff), "fresh storage token was rejected"); Tick(mate, 40);

				NPC enemy = Main.npc[21]; enemy.SetDefaults(NPCID.BlueSlime); enemy.active = true; enemy.target = 0;
				enemy.life = enemy.lifeMax = 10000; enemy.defense = 0; enemy.knockBackResist = 0; enemy.Center = owner.Center + new Vector2(80, 0);
				NPC critter = Main.npc[22]; critter.SetDefaults(NPCID.Bunny); critter.active = true; critter.friendly = false; critter.Center = enemy.Center;
				// Keep native critter classification but make an accidental hit nonlethal, so failure cannot trigger loot/world changes.
				critter.life = 10000; int critterLife = critter.life;
				NPC resident = Main.npc[24]; resident.SetDefaults(NPCID.Guide); resident.active = true; resident.life = 10000; resident.Center = enemy.Center;
				int residentLife = resident.life; SetTarget(mate, 21);
				Assert(Target(mate) == enemy, "synchronized hostile target was unavailable");
				Projectile? melee = Retainers(mate).SingleOrDefault();
				Assert(melee is not null, "melee retainer missing before native damage check");
				if (melee is not null) {
					int life = enemy.life; melee.Center = enemy.Center; melee.velocity = Vector2.Zero; melee.Update(melee.whoAmI);
					Assert(enemy.life < life && melee.damage == CompanionRetinue.Damage(mate.Profile, mate.Profile.SummonItems[staleSlot])
						&& enemy.playerInteraction[0], "actual Projectile.Update did not apply credited companion melee damage");
					Assert(critter.life == critterLife && resident.life == residentLife, "native melee collision harmed a critter or resident");
					SetTarget(mate, 22); Assert(Target(mate) is null, "retinue selected a native critter");
					melee.Center = critter.Center; melee.Update(melee.whoAmI);
					Assert(critter.life == critterLife && melee.ModProjectile.CanDamage() == false && melee.ModProjectile.CanHitNPC(critter) == false,
						"adapted melee AI damaged or pursued a native critter");
				}
				SetTarget(mate, 21); enemy.dontTakeDamage = true; Assert(Target(mate) is null, "invulnerable hostile accepted"); enemy.dontTakeDamage = false;
				enemy.friendly = true; Assert(Target(mate) is null, "friendly hostile accepted"); enemy.friendly = false;
				SetTarget(mate, -1); Assert(Equip(ItemID.ImpStaff), "ranged staff selection failed"); Tick(mate, 60);
				Projectile? Fire(SoulboundCompanion companion) {
					SetTarget(companion, 21);
					Projectile? ranged = Retainers(companion).FirstOrDefault(p => (int)p.ai[1] == ItemID.ImpStaff);
					if (ranged is null) return null;
					typeof(CompanionRetainer).GetField("attackCooldown", instance)!.SetValue(ranged.ModProjectile, 0);
					ranged.Center = enemy.Center - new Vector2(80, 0); ranged.ModProjectile.AI();
					return Effects(companion).LastOrDefault(p => p.ModProjectile is CompanionRetinueBolt);
				}
				int firstSpawnPacket = ProbeSocket.Sent.Count;
				bool wasRecording = ProbeSocket.Recording, wasWatching = RecoveryPacketSpy.Watching;
				int clientState = Netplay.Clients[0].State; bool wasBroadcasting = NetMessage.buffer[0].broadcast;
				Projectile? bolt;
				try {
					if (mode == NetmodeID.Server) {
						RecoveryPacketSpy.Watching = false; ProbeSocket.Recording = true;
						Netplay.Clients[0].State = 10; NetMessage.buffer[0].broadcast = true;
					}
					bolt = Fire(mate);
				}
				finally {
					ProbeSocket.Recording = wasRecording; RecoveryPacketSpy.Watching = wasWatching;
					Netplay.Clients[0].State = clientState; NetMessage.buffer[0].broadcast = wasBroadcasting;
				}
				if (mode == NetmodeID.Server) {
					byte[]? packet = ProbeSocket.Sent.Skip(firstSpawnPacket)
						.FirstOrDefault(data => data.Length > 2 && data[2] == MessageID.SyncProjectile);
					bool initiallyBound = false;
					if (packet is not null) {
						Projectile[] serverProjectiles = Main.projectile; int[,] serverIdentities = Main.projectileIdentity;
						int serverLocal = Main.myPlayer;
						try {
							// Decode only the automatic spawn packet; the caller's later Bind/netUpdate cannot repair it.
							Main.projectile = Enumerable.Range(0, serverProjectiles.Length).Select(i => new Projectile { whoAmI = i }).ToArray();
							Main.projectileIdentity = (int[,])serverIdentities.Clone();
							Main.netMode = NetmodeID.MultiplayerClient; Main.myPlayer = 0;
							var incoming = new MessageBuffer { whoAmI = 256 }; incoming.ResetReader();
							Array.Copy(packet, 2, incoming.readBuffer, 0, packet.Length - 2);
							incoming.GetData(0, packet.Length - 2, out int messageType);
							Projectile? received = Main.projectile.SingleOrDefault(p => p.active && p.ModProjectile is CompanionRetinueBolt);
							if (received is not null) {
								using var binding = new MemoryStream(); using var writer = new BinaryWriter(binding);
								received.ModProjectile.SendExtraAI(writer);
								initiallyBound = messageType == MessageID.SyncProjectile && Belongs(received, mate)
									&& received.ai[1] == ItemID.ImpStaff && received.ai[2] == enemy.whoAmI
									&& binding.ToArray().SequenceEqual(mate.Profile.Id.ToByteArray());
							}
						}
						finally {
							Main.projectile = serverProjectiles; Main.projectileIdentity = serverIdentities;
							Main.netMode = mode; Main.myPlayer = serverLocal;
						}
					}
					Assert(initiallyBound, "first native bolt spawn packet lacked its companion GUID/target before post-spawn binding");
				}
				Assert(bolt is not null, "adapted ranged AI did not spawn a real bound bolt");
				if (bolt is not null) {
					Assert((int)bolt.ai[0] == mate.NPC.whoAmI && (int)bolt.ai[1] == ItemID.ImpStaff
						&& bolt.npcProj && !bolt.minion && !bolt.sentry && bolt.ModProjectile.CanHitNPC(critter) == false,
						"ranged bolt binding/slot independence/critter exclusion incorrect");
					using (var binding = new MemoryStream()) {
						using var writer = new BinaryWriter(binding); bolt.ModProjectile.SendExtraAI(writer);
						Assert(binding.ToArray().SequenceEqual(mate.Profile.Id.ToByteArray()), "ranged bolt did not transport its companion GUID");
					}
					int life = enemy.life; bolt.Center = enemy.Center; bolt.velocity = Vector2.Zero; bolt.Update(bolt.whoAmI);
					Assert(enemy.life < life && critter.life == critterLife && resident.life == residentLife,
						"actual Projectile.Update did not apply safe ranged damage");
				}
				bolt = Fire(mate); Assert(bolt is not null, "could not prepare dismissal bolt");
				Assert(Configure(mate, -1, 0, new byte[32]) && Effects(mate).Length == 0 && (bolt is null || !bolt.active),
					"dismissal did not immediately kill bound retainers AND bolts");
				SetTarget(mate, -1); Assert(Equip(ItemID.ImpStaff), "could not re-enable before withdrawal"); Tick(mate, 40); bolt = Fire(mate);
				int impSlot = mate.Profile.SummonItems.FindIndex(item => item.type == ItemID.ImpStaff);
				Item[] inventory = owner.inventory;
				try {
					owner.inventory = inventory.Select(item => item.Clone()).ToArray();
					for (int i = 1; i < owner.inventory.Length; i++) owner.inventory[i] = new Item(ItemID.CopperShortsword);
					mate.WithdrawStorageSlot(CompanionStorage.Summons, impSlot, false); SettleServerInventory(mod);
					Assert(mate.Profile.ItemCount(ItemID.ImpStaff) == 1 && Retainers(mate).Any(p => (int)p.ai[1] == ItemID.ImpStaff),
						"full owner inventory erased owned/equipped staff");
				}
				finally { owner.inventory = inventory; }
				int returned = InventoryCount(ItemID.ImpStaff); byte[] returnedMetadata = Bytes(mate.Profile.SummonItems[impSlot]);
				mate.WithdrawStorageSlot(CompanionStorage.Summons, impSlot, false);
				Assert(!Effects(mate).Any(p => (int)p.ai[1] == ItemID.ImpStaff) && (bolt is null || !bolt.active),
					"staff withdrawal did not immediately remove its retainer AND in-flight bolt"); SettleServerInventory(mod);
				Assert(mate.Profile.ItemCount(ItemID.ImpStaff) == 0 && InventoryCount(ItemID.ImpStaff) == returned + 1
					&& owner.inventory.Any(item => Bytes(item).SequenceEqual(returnedMetadata)), "staff return failed conservation/metadata");
				SetTarget(mate, -1);
				var totals = catalog.ToDictionary(type => type, type => mate.Profile.ItemCount(type) + InventoryCount(type));
				mate.UnloadPack(); SettleServerInventory(mod);
				Assert(mate.Profile.SummonItems.Count == 0 && Effects(mate).Length == 0
					&& totals.All(pair => InventoryCount(pair.Key) == pair.Value), "return-all lost native staffs or left retinue effects"); PlayerUntouched();

				Gift(ItemID.SlimeStaff); Gift(ItemID.ImpStaff); Assert(Equip(ItemID.ImpStaff), "lifecycle staff selection failed"); Tick(mate, 60);
				SoulboundCompanion other = Create(23, 1, "Other"); other.Profile.Store(new Item(ItemID.ImpStaff));
				Assert(Configure(other, 0, ItemID.ImpStaff, StorageToken(other, 0)), "second companion could not configure its own retinue"); Tick(other, 60);
				Projectile? ownBolt = Fire(mate), otherBolt = Fire(other);
				Assert(Retainers(mate).Length == 1 && Retainers(other).Length == 1 && ownBolt is not null && otherBolt is not null
					&& Effects(mate).All(p => !Belongs(p, other)) && Effects(other).All(p => !Belongs(p, mate)),
					"two NPCs sharing a staff type did not bind independently");
				Projectile[] otherEffects = Effects(other); Projectile[] staleEffects = Effects(mate);
				Guid identity = mate.Profile.Id; mate.Profile.Id = Guid.NewGuid(); Sigil().Profile = mate.Profile.Clone();
				Assert(Maintain(mate), "replacement GUID fixture was not independently bound to its Sigil");
				foreach (Projectile projectile in staleEffects) projectile.ModProjectile.AI();
				Assert(staleEffects.All(p => !p.active) && otherEffects.All(p => p.active), "reused NPC slot retained stale GUID summons or killed another companion's effects");
				mate.Profile.Id = identity; Sigil().Profile = mate.Profile.Clone(); SetTarget(mate, -1); Tick(mate, 40);
				Main.netMode = NetmodeID.MultiplayerClient;
				Projectile[] authoritative = Effects(mate); int selected = mate.Profile.RetinueItemType;
				Assert(!Configure(mate, -1, 0, new byte[32]) && !Configure(mate, 0, ItemID.SlimeStaff, StorageToken(mate, 0))
					&& mate.Profile.RetinueEnabled && mate.Profile.RetinueItemType == selected, "client independently changed retinue configuration");
				SetTarget(mate, 21); foreach (Projectile projectile in authoritative) projectile.ModProjectile.AI();
				Assert(Effects(mate).SequenceEqual(authoritative) && authoritative.All(p => p.ModProjectile.CanDamage() == false),
					"client independently spawned a ranged bolt or enabled damage");
				int clientGift = Array.FindIndex(owner.inventory, 0, 50, CompanionRetinue.IsStaff);
				if (clientGift >= 0) {
					byte[] clientSource = Bytes(owner.inventory[clientGift], true); int cargo = mate.Profile.SummonItems.Count;
					Assert(!Store(mate, clientGift, SourceToken(owner.inventory[clientGift])) && mate.Profile.SummonItems.Count == cargo
						&& Bytes(owner.inventory[clientGift], true).SequenceEqual(clientSource), "client independently transferred a staff gift");
				}
				foreach (Projectile projectile in authoritative) projectile.active = false;
				Tick(mate, 60); Assert(Effects(mate).Length == 0, "client independently spawned missing retainers");
				Main.netMode = mode; Main.myPlayer = mode == NetmodeID.Server ? 255 : 0; SetTarget(mate, -1); Tick(mate, 40);
				Fire(mate); otherEffects = Effects(other); owner.dead = true; Update(mate);
				Assert(!Maintain(mate) && Effects(mate).Length == 0 && otherEffects.All(p => p.active), "owner death did not clean exactly its retinue");
				owner.dead = false; SetTarget(mate, -1); Tick(mate, 40);
				Item boundSigil = owner.inventory[0]; owner.inventory[0] = new Item(); Update(mate);
				Assert(!Maintain(mate) && Effects(mate).Length == 0, "missing Sigil left retinue active"); owner.inventory[0] = boundSigil;
				Tick(mate, 40); Fire(mate); otherEffects = Effects(other);
				mate.Recall(); Assert(!mate.NPC.active && Effects(mate).Length == 0 && otherEffects.All(p => p.active)
					&& Sigil().Profile.ItemCount(ItemID.ImpStaff) == 1 && Sigil().Profile.RetinueEnabled, "recall lost staff/selection or removed another companion's effects");
				// Exercise the actual Sigil switch path, which recalls the previous NPC before creating the replacement.
				Assert((bool)typeof(SoulboundSigil).GetMethod("SummonCompanion", instance)!.Invoke(Sigil(), new object[] { owner })!, "could not resummon for Sigil switch");
				SoulboundCompanion resumed = SoulboundCompanion.FindFor(owner)!; Tick(resumed, 60); Fire(resumed);
				Projectile[] switchedEffects = Effects(resumed);
				int switchSlot = Array.FindIndex(owner.inventory, 1, 49, item => item.IsAir);
				Assert(switchSlot >= 1, "Sigil switch fixture has no empty slot");
				if (switchSlot < 1) continue;
				owner.inventory[switchSlot] = new Item(ModContent.ItemType<SoulboundSigil>()); var replacement = (SoulboundSigil)owner.inventory[switchSlot].ModItem;
				otherEffects = Effects(other);
				Assert((bool)typeof(SoulboundSigil).GetMethod("SummonCompanion", instance)!.Invoke(replacement, new object[] { owner })!
					&& (!resumed.NPC.active || !ReferenceEquals(resumed.NPC.ModNPC, resumed))
					&& SoulboundCompanion.FindFor(owner)?.Profile.Id == replacement.Profile.Id
					&& switchedEffects.All(p => !p.active) && otherEffects.All(p => p.active)
					&& Sigil().Profile.ItemCount(ItemID.ImpStaff) == 1 && replacement.Profile.SummonItems.Count == 0,
					"Sigil switch leaked old retinue or transferred staff ownership to a different identity");
				SoulboundCompanion.FindFor(owner)!.Recall(); other.Recall();

				SoulboundCompanion saturated = Create(30, 0, "AETHER");
				foreach (int type in catalog) saturated.Profile.Store(new Item(type));
				saturated.Profile.RetinueEnabled = true; saturated.Profile.RetinueItemType = catalog[0]; saturated.SyncProfileToBoundSigil();
				// Terraria reuses the oldest projectile when full. Refill that slot to keep pressure constant and count allocations.
				for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i] = new Projectile {
					whoAmI = i, active = true, type = ProjectileID.WoodenArrowFriendly, owner = 255, timeLeft = 10000
				};
				int allocations = 0, allocationTicks = 0;
				for (int tick = 0; tick < 60; tick++) {
					Update(saturated);
					Projectile[] spawned = Retainers(saturated); allocations += spawned.Length;
					if (spawned.Length > 0) allocationTicks++;
					Assert(spawned.Length <= 1, "full pool spawned an army in one update");
					foreach (Projectile projectile in spawned) Main.projectile[projectile.whoAmI] = new Projectile {
						whoAmI = projectile.whoAmI, active = true, type = ProjectileID.WoodenArrowFriendly, owner = 255, timeLeft = 10000
					};
				}
				Assert(allocations <= 10 && allocationTicks <= 10, "full projectile pool caused a respawn storm: " + allocations);
				Main.projectile = Enumerable.Range(0, oldProjectiles.Length).Select(i => new Projectile { whoAmI = i }).ToArray();
				Tick(saturated, (catalog.Length + 2) * 20);
				Assert(Retainers(saturated).Length == catalog.Length && saturated.Profile.SummonItems.Count == catalog.Length,
					"retinue did not recover after projectile pool pressure ended"); saturated.Recall();
			}
		}
		finally {
			foreach (Action restore in restoreTransactions) restore();
			feedbackActive.SetValue(null, oldFeedbackActive);
			Main.player = oldPlayers; Main.clientPlayer = oldClientPlayer; Main.npc = oldNpcs; Main.projectile = oldProjectiles; Main.item = oldItems;
			Main.projectileIdentity = oldIdentities; Main.dust = oldDust; Main.gore = oldGore; Main.combatText = oldCombatText;
			Main.tile = oldTiles; Main.maxTilesX = oldWidth; Main.maxTilesY = oldHeight;
			Main.leftWorld = oldLeft; Main.rightWorld = oldRight; Main.topWorld = oldTop; Main.bottomWorld = oldBottom;
			Main.netMode = oldMode; Main.myPlayer = oldLocal; Main.gameMenu = oldMenu; Main.rand = oldRandom;
			NetMessage.buffer = oldBuffers;
			for (int i = 0; i < Netplay.Clients.Length; i++) Netplay.Clients[i] = oldClients[i];
			ProbeSocket.Recording = oldRecording; ProbeSocket.Sent.Clear(); ProbeSocket.Sent.AddRange(oldSent);
			RecoveryPacketSpy.Watching = oldWatching; RecoveryPacketSpy.Packets.Clear(); RecoveryPacketSpy.Packets.AddRange(oldPackets);
			foreach (var entry in collisionState) entry.Field.SetValue(null, entry.Value);
		}
	}
}
