#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Soulmates.Content.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Net;

namespace SoulmatesRegressionProbe;

public sealed partial class EngineChecks
{
	private static void CheckStackedPets(Action<bool, string> check, Mod mod)
	{
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		int sap = ItemID.EucaluptusSap;
		var profile = new CompanionProfile();
		for (int i = 0; i < 9; i++) check(profile.Store(new Item(sap)) == 1, "Repeated sap gift failed to stack");
		profile.PetItemType = sap;
		check(profile.PetItems.Count == 1 && profile.PetItems[0].stack == 9
			&& profile.PetItems[0].maxStack == new Item(sap).maxStack, "Pet stacking changed native item limits or used extra slots");
		using var stream = new MemoryStream();
		using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) profile.Write(writer);
		stream.Position = 0;
		using var reader = new BinaryReader(stream, Encoding.UTF8, true);
		foreach (CompanionProfile copy in new[] { profile.Clone(), CompanionProfile.Load(profile.Save()), CompanionProfile.Read(reader) }) {
			copy.Normalize(); copy.Normalize();
			check(copy.PetItems.Count == 1 && copy.ItemCount(sap) == 9 && copy.PetItemType == sap,
				"Stacked pets lost count or selection through clone/save/network/normalization");
			check(!ReferenceEquals(profile.PetItems[0], copy.PetItems[0]), "Pet stack shared mutable ownership with a copied profile");
		}
		check(stream.Position == stream.Length, "Stacked pet transport left trailing bytes");
		int moddedType = ModContent.ItemType<CritterBetaPetItem>();
		var modded = new CompanionProfile();
		Item markedA = new Item(moddedType), markedB = new Item(moddedType);
		((CritterBetaPetItem)markedA.ModItem).Marker = 71; ((CritterBetaPetItem)markedB.ModItem).Marker = 72;
		modded.Store(markedA); modded.Store(markedB); modded.Normalize();
		check(modded.PetItems.Count == 2 && modded.PetItems.Select(item => ((CritterBetaPetItem)item.ModItem).Marker)
			.SequenceEqual(new[] { 71, 72 }), "Unstackable modded pet items were forced together and lost custom data");
		var legacy = new CompanionProfile { PetItemType = sap };
		for (int i = 0; i < 12; i++) legacy.PetItems.Add(new Item(sap));
		legacy.Normalize(); legacy.Normalize();
		check(legacy.PetItems.Count == 1 && legacy.PetItems[0].stack == 12, "Legacy duplicate pets did not compact without loss");
		foreach (int leader in new[] { sap, ItemID.ZephyrFish, ItemID.Nectar, ItemID.Carrot, 0 }) {
			var mixed = new CompanionProfile { PetItemType = leader };
			mixed.PetItems.AddRange(new[] { new Item(sap) { stack = 0 }, new Item(ItemID.ZephyrFish, 2),
				new Item(sap, 4), new Item(ItemID.Nectar, 3), new Item(sap, 2) });
			var order = CompanionPets.Followers(mixed).ToArray();
			for (int i = 0; i < order.Length; i++)
				check(CompanionPets.TryGetFormation(mixed, order[i].ItemType, order[i].Copy, out int rank, out int total)
					&& rank == i && total == order.Length, "Legacy mixed stacks disagree on pet formation order");
			check(!CompanionPets.TryGetFormation(mixed, sap, 6, out _, out _), "Unowned pet copy gained a formation slot");
		}
		var bounded = new CompanionProfile();
		Item excessive = new Item(sap, 100);
		check(bounded.Store(excessive) == CompanionPets.MaximumFollowers && excessive.stack == 100 - CompanionPets.MaximumFollowers,
			"Pet capacity consumed an overflow item or failed to accept the permitted part");
		Item other = new Item(ItemID.ZephyrFish);
		check(bounded.Store(other) == 0 && !other.IsAir, "Global pet capacity accepted another type above its limit");
		bounded.PetItems[0].stack += 5; bounded.PetItemType = sap; bounded.Normalize();
		check(CompanionPets.OwnedCount(bounded) == CompanionPets.MaximumFollowers + 5
			&& CompanionPets.Followers(bounded).Count() == CompanionPets.MaximumFollowers,
			"Legacy over-cap cargo was erased or spawned an unbounded flock");
		for (int count = 2; count <= CompanionPets.MaximumFollowers; count++) {
			for (int i = 0; i < count; i++) {
				Vector2 a = CompanionPets.FollowerOffset(i, count, 1, 160);
				check(float.IsFinite(a.X) && float.IsFinite(a.Y) && a.Length() < 550f, "Pet formation escaped its companion leash");
				for (int j = 0; j < i; j++) check(Vector2.Distance(a, CompanionPets.FollowerOffset(j, count, -1, 160)) > 72f,
					"Pet formation overlaps the maximum rendered pet frame");
			}
		}
		Player oldPlayer = Main.player[0]; NPC[] oldNpcs = Main.npc; Projectile[] oldProjectiles = Main.projectile;
		int oldMode = Main.netMode, oldLocal = Main.myPlayer;
		RemoteClient[] oldClients = (RemoteClient[])Netplay.Clients.Clone();
		try {
			for (int i = 0; i < Netplay.Clients.Length; i++) Netplay.Clients[i] = new RemoteClient { Socket = new ProbeSocket() };
			foreach (int mode in new[] { NetmodeID.SinglePlayer, NetmodeID.Server }) {
				Main.netMode = mode; Main.myPlayer = mode == NetmodeID.Server ? 255 : 0;
				Main.npc = Enumerable.Range(0, oldNpcs.Length).Select(i => new NPC { whoAmI = i }).ToArray();
				Main.projectile = Enumerable.Range(0, oldProjectiles.Length).Select(i => new Projectile { whoAmI = i }).ToArray();
				var owner = new Player { whoAmI = 0, active = true, Center = new Vector2(500, 500) };
				Main.player[0] = owner;
				NPC npc = Main.npc[20]; npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
				npc.active = true; npc.ai[0] = 0; npc.Center = owner.Center;
				var mate = (SoulboundCompanion)npc.ModNPC; mate.Profile.AutonomyEnabled = false;
				owner.inventory[0] = new Item(ModContent.ItemType<SoulboundSigil>());
				((SoulboundSigil)owner.inventory[0].ModItem).Profile = mate.Profile.Clone();
				object? Call(string method, params object[] args) => typeof(SoulboundCompanion).GetMethod(method, flags)!.Invoke(mate, args);
				byte[] Token(int slot) => (byte[])typeof(CompanionProfile).GetMethod("StorageToken", flags)!
					.Invoke(mate.Profile, new object[] { CompanionStorage.Pets, slot })!;
				Projectile[] Pets() => Main.projectile.Where(p => p.active && p.ModProjectile is CompanionFamiliar).ToArray();
				int[] buffs = (int[])owner.buffType.Clone();
				foreach (int type in new[] { sap, sap, sap, ItemID.ZephyrFish, ItemID.ZephyrFish }) {
					owner.inventory[25] = new Item(type);
					byte[] token;
					using (var data = new MemoryStream()) {
						using (var writer = new BinaryWriter(data, Encoding.UTF8, true)) Terraria.ModLoader.IO.ItemIO.Send(owner.inventory[25], writer, writeStack: true, writeFavorite: true);
						token = System.Security.Cryptography.SHA256.HashData(data.ToArray());
					}
					object[] args = { 25, token, "" };
					check((bool)typeof(SoulboundCompanion).GetMethod("StorePetItem", flags)!.Invoke(mate, args)!, "Stacked pet gift was not accepted");
					SettleServerInventory(mod);
					check(owner.inventory[25].IsAir, "Stacked pet gift retained a second owner copy");
				}
				check(mate.Profile.PetItems.Count == 2 && mate.Profile.ItemCount(sap) == 3 && Pets().Length == 5,
					"Mixed pet stacks did not produce one familiar per owned unit");
				int[] identities = Pets().Select(p => p.whoAmI).OrderBy(i => i).ToArray();
				var expected = CompanionPets.Followers(mate.Profile).ToArray();
				for (int i = 0; i < expected.Length; i++)
					check(CompanionPets.TryGetFormation(mate.Profile, expected[i].ItemType, expected[i].Copy, out int position, out int size)
						&& position == i && size == expected.Length, "Pet spawn and movement disagree on formation order");
				for (int tick = 0; tick < 360; tick++) {
					Call("UpdateEquippedPet");
					foreach (Projectile pet in Pets()) { pet.ModProjectile.AI(); pet.position += pet.velocity; }
				}
				check(Pets().Select(p => p.whoAmI).OrderBy(i => i).SequenceEqual(identities)
					&& owner.buffType.SequenceEqual(buffs), "Stacked familiar maintenance respawned pets or changed player buffs");
				Projectile[] pets = Pets();
				for (int i = 0; i < pets.Length; i++) {
					check(pets[i].damage == 0 && !pets[i].friendly && pets[i].ModProjectile.CanDamage() == false,
						"Stacked familiar can hurt a critter");
					for (int j = 0; j < i; j++) check(Vector2.Distance(pets[i].Center, pets[j].Center) > 72,
						"Settled stacked familiars share a position");
				}
				check(CompanionPets.TryGetFormation(mate.Profile, sap, 2, out int rank, out int total) && rank == 2 && total == 5,
					"Stack copy formation does not match its owned identity");
				mate.WithdrawStorageSlot(CompanionStorage.Pets, 0, true); SettleServerInventory(mod);
				check(Pets().Length == 4 && mate.Profile.ItemCount(sap) == 2
					&& owner.inventory.Where(item => item.type == sap).Sum(item => item.stack) == 1,
					"Single-item withdrawal failed exact pet and item conservation");
				check(!(bool)Call("ConfigurePet", 0, sap, new byte[32])!, "Stale stack token accepted a pet configuration");
				check((bool)Call("ConfigurePet", 1, (int)ItemID.ZephyrFish, Token(1))! && Pets().Length == 4,
					"Changing the flock leader duplicated or removed pets");
				check(CompanionPets.TryGetFormation(mate.Profile, ItemID.ZephyrFish, 0, out rank, out total) && rank == 0 && total == 4,
					"Chosen pet did not take the leading formation position");
				for (int i = 1; i < owner.inventory.Length; i++) owner.inventory[i] = new Item(ItemID.CopperShortsword);
				mate.WithdrawStorageSlot(CompanionStorage.Pets, 0, false); SettleServerInventory(mod);
				check(mate.Profile.ItemCount(sap) == 2 && Pets().Length == 4, "Full owner inventory erased a pet stack");
				for (int i = 1; i < owner.inventory.Length; i++) owner.inventory[i] = new Item();
				mate.UnloadPack(); SettleServerInventory(mod);
				check(mate.Profile.PetItems.Count == 0 && Pets().Length == 0 && mate.Profile.PetItemType == 0
					&& owner.inventory.Where(item => item.type == sap).Sum(item => item.stack) == 2
					&& owner.inventory.Where(item => item.type == ItemID.ZephyrFish).Sum(item => item.stack) == 2,
					"Unloading stacked pets lost items or left ghost familiars");
				check(owner.inventory.All(item => item.IsAir || item.stack <= item.maxStack), "Companion-only stacks leaked into player inventory");
				mate.Profile.Store(new Item(sap, CompanionPets.MaximumFollowers)); mate.Profile.PetItemType = sap;
				Call("UpdateEquippedPet");
				check(Pets().Length == CompanionPets.MaximumFollowers, "Full pet bag did not spawn one familiar per item");
				check(Pets().Select(p => ((int)p.ai[1], (int)p.ai[2])).Distinct().Count() == Pets().Length,
					"Pet copies share an ownership identity");
				Projectile removed = Pets()[0]; removed.Kill();
				for (int i = 0; i < Main.maxProjectiles; i++) {
					if (Main.projectile[i].active) continue;
					Main.projectile[i].SetDefaults(ProjectileID.WoodenArrowFriendly); Main.projectile[i].active = true;
				}
				Call("UpdateEquippedPet");
				check(Pets().Length == CompanionPets.MaximumFollowers - 1
					&& Main.projectile.Take(Main.maxProjectiles).All(projectile => projectile.active),
					"Full native projectile pool overwrote an unrelated projectile to spawn a pet");
				Projectile space = Main.projectile.First(projectile => projectile.active && projectile.ModProjectile is not CompanionFamiliar);
				space.Kill(); Call("UpdateEquippedPet");
				check(Pets().Length == CompanionPets.MaximumFollowers, "Waiting familiar did not recover after a projectile slot became free");
				mate.Recall();
				check(Pets().Length == 0 && ((SoulboundSigil)owner.inventory[0].ModItem).Profile.ItemCount(sap) == CompanionPets.MaximumFollowers,
					"Recalling a full flock lost stored items or left pets behind");
			}
		}
		finally {
			Main.player[0] = oldPlayer; Main.npc = oldNpcs; Main.projectile = oldProjectiles;
			Main.netMode = oldMode; Main.myPlayer = oldLocal;
			for (int i = 0; i < Netplay.Clients.Length; i++) Netplay.Clients[i] = oldClients[i];
		}
	}
}
