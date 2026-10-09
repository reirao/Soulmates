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
using Terraria.ModLoader.IO;
using Terraria.Net;

namespace SoulmatesRegressionProbe;

public sealed partial class EngineChecks
{
	private static void CheckCompanionPets(Action<bool, string> check, Mod mod)
	{
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
		int[] catalog = CompanionPets.SupportedItems.ToArray();
		File.WriteAllLines(Path.Combine(Main.SavePath, "Soulmates-pet-catalog.txt"), catalog.Select(type => $"{type}: {Lang.GetItemNameValue(type)} -> {CompanionPets.VisualFor(type)}"));
		check(catalog.Contains(ItemID.Carrot) && catalog.Contains(ItemID.CompanionCube) && catalog.Contains(ItemID.WispinaBottle)
			&& !catalog.Contains(ItemID.CopperOre) && !catalog.Contains(ItemID.SlimeStaff), "Dynamic pet detection omits real pets or includes weapons/resources");
		var capacity = new CompanionProfile();
		for (int i = 0; i < capacity.PackCapacity; i++) capacity.Store(new Item(ItemID.CopperShortsword));
		foreach (int itemType in catalog.Take(CompanionProfile.MaximumPetSlots))
			check(capacity.Store(new Item(itemType)) == 1, "Full equipment pack blocked a separate pet slot");
		Item overflow = new Item(catalog[CompanionProfile.MaximumPetSlots]);
		check(capacity.PetItems.Count == CompanionProfile.MaximumPetSlots && capacity.Store(overflow) == 0 && !overflow.IsAir,
			"Full pet inventory loses items or exceeds its slot limit");
		var legacy = new TagCompound { ["pack"] = new[] { ItemIO.Save(new Item(ItemID.ZephyrFish)), ItemIO.Save(new Item(ItemID.Wood, 7)) }.ToList(),
			["petItemType"] = (int)ItemID.ZephyrFish };
		CompanionProfile migrated = CompanionProfile.Load(legacy);
		migrated.Normalize(); migrated.Normalize();
		check(migrated.PetItems.Count == 1 && migrated.Pack.Count == 0 && migrated.ItemCount(ItemID.Wood) == 7
			&& migrated.PetItemType == ItemID.ZephyrFish, "Old pet cargo lost ownership, duplicated or failed to migrate");
		foreach (int itemType in CompanionPets.SupportedItems) {
			var profile = new CompanionProfile(); profile.Store(new Item(itemType)); profile.PetItemType = itemType;
			check(Main.projFrames[CompanionPets.VisualFor(itemType)] >= 1, "Detected native pet has no usable frames");
			using var stream = new MemoryStream();
			using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) profile.Write(writer);
			stream.Position = 0; using var reader = new BinaryReader(stream);
			foreach (var copy in new[] { profile.Clone(), CompanionProfile.Load(profile.Save()), CompanionProfile.Read(reader) }) {
				check(copy.PetItemType == itemType && copy.ItemCount(itemType) == 1, "Pet selection lost item ownership through save or transport");
				check(!ReferenceEquals(copy.PetItems[0], profile.PetItems[0]), "Pet selection shallow-copied the owned item");
			}
			check(stream.Position == stream.Length, "Pet transport leaves extra bytes");
			stream.Position = stream.Length - 4;
			using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) writer.Write((int)ItemID.CopperOre);
			stream.Position = 0; bool refused = false;
			try { using var invalid = new BinaryReader(stream, Encoding.UTF8, true); CompanionProfile.Read(invalid); }
			catch (InvalidDataException) { refused = true; }
			check(refused, "Network profile accepted unsupported pet type");
			profile.ClearCargo(); profile.PetItemType = itemType; profile.Normalize();
			check(profile.PetItemType == 0, "Saved selection invented an unowned pet");
		}
		check(CompanionProfile.Load(new TagCompound()).PetItemType == 0, "Old Sigil gained a phantom pet");
		var brokenCargo = new CompanionProfile { PetItemType = ItemID.Nectar }; brokenCargo.Pack.Add(null!); brokenCargo.Normalize();
		check(brokenCargo.PetItemType == 0, "Null legacy cargo retained a pet or crashed normalization");
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
				NPC npc = Main.npc[20]; npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>()); npc.active = true; npc.ai[0] = 0; npc.Center = owner.Center;
				var mate = (SoulboundCompanion)npc.ModNPC; mate.Profile.AutonomyEnabled = false;
				owner.inventory[0] = new Item(ModContent.ItemType<SoulboundSigil>());
				var sigil = (SoulboundSigil)owner.inventory[0].ModItem; sigil.Profile = mate.Profile.Clone();
				object? Call(string method, params object[] args) => typeof(SoulboundCompanion).GetMethod(method, flags)!.Invoke(mate, args);
				byte[] Token(int slot) => (byte[])typeof(CompanionProfile).GetMethod("StorageToken", flags)!.Invoke(mate.Profile,
					new object[] { CompanionStorage.Pets, slot })!;
				bool Equip(int slot, int type, byte[] token) => (bool)Call("ConfigurePet", slot, type, token)!;
				Projectile[] Pets() => Main.projectile.Where(p => p.active && p.ModProjectile is CompanionFamiliar).ToArray();
				int[] oldBuffs = (int[])owner.buffType.Clone();
				string State() => $"mode={mode}, selection={mate.Profile.PetItemType}, count={Pets().Length}, item={mate.Profile.ItemCount(ItemID.ZephyrFish)}, buffs={owner.buffType.SequenceEqual(oldBuffs)}; "
					+ string.Join("; ", Pets().Select(p => $"slot={p.whoAmI},ai={string.Join(',', p.ai)},life={p.timeLeft},active={p.active}"));
				byte[] ItemToken(Item item) {
					using var data = new MemoryStream(); using var writer = new BinaryWriter(data);
					ItemIO.Send(item, writer, writeStack: true, writeFavorite: true);
					return System.Security.Cryptography.SHA256.HashData(data.ToArray());
				}
				void StoreRequest(Guid id, int slot, byte[] token, int trim = 0) {
					using var data = new MemoryStream(); using (var writer = new BinaryWriter(data, Encoding.UTF8, true)) {
						writer.Write(Convert.ToByte(Enum.Parse(mod.GetType().GetNestedType("MessageType", BindingFlags.NonPublic)!, "PetStoreRequest")));
						writer.Write(id.ToByteArray()); writer.Write((byte)slot); writer.Write(token);
					}
					if (trim > 0) data.SetLength(data.Length - trim);
					data.Position = 0; using var reader = new BinaryReader(data); mod.HandlePacket(reader, 0);
				}
				owner.inventory[25] = new Item(ItemID.Carrot); byte[] giftToken = ItemToken(owner.inventory[25]);
				if (mode == NetmodeID.Server) {
					StoreRequest(Guid.NewGuid(), 25, giftToken); StoreRequest(mate.Profile.Id, 25, new byte[32]);
					StoreRequest(mate.Profile.Id, 255, giftToken); StoreRequest(mate.Profile.Id, 25, giftToken, 1);
					check(mate.Profile.PetItems.Count == 0 && owner.inventory[25].type == ItemID.Carrot, "Invalid pet gifts modified ownership");
					owner.inventory[25].favorited = true; StoreRequest(mate.Profile.Id, 25, ItemToken(owner.inventory[25]));
					check(mate.Profile.PetItems.Count == 0 && owner.inventory[25].favorited, "Pet gift stole a favorited item");
					owner.inventory[25].favorited = false;
					StoreRequest(mate.Profile.Id, 25, giftToken); StoreRequest(mate.Profile.Id, 25, giftToken);
					SettleServerInventory(mod);
				}
				else {
					object[] args = { 25, giftToken, "" };
					check((bool)typeof(SoulboundCompanion).GetMethod("StorePetItem", flags)!.Invoke(mate, args)!, "Single-player pet gift failed");
				}
				check(owner.inventory[25].IsAir && mate.Profile.ItemCount(ItemID.Carrot) == 1, "Pet gift loses or duplicates the item");
				Call("UpdateEquippedPet");
				check(mate.Profile.PetItemType == ItemID.Carrot && Pets().Length == 1, "First real gift was stored without selecting and spawning its pet");
				mate.WithdrawStorageSlot(CompanionStorage.Pets, 0, false); SettleServerInventory(mod);
				check(mate.Profile.PetItems.Count == 0 && owner.inventory.Count(item => item.type == ItemID.Carrot) == 1, "Gift withdrawal fails conservation");
				mate.Profile.Store(new Item(ItemID.ZephyrFish)); mate.Profile.Store(new Item(ItemID.Nectar)); mate.Profile.Store(new Item(ItemID.Carrot));
				check(!Equip(2, ItemID.CopperOre, Token(2)) && Pets().Length == 0, "A non-pet item was spawned");
				check(Equip(2, ItemID.Carrot, Token(2)) && Pets().Length == 1, "Real Bunny summon item was not detected");
				Equip(-1, 0, new byte[32]);
				owner.selectedItem = 1; owner.inventory[1] = new Item(ItemID.CompanionCube);
				mate.StoreSelectedItem(); SettleServerInventory(mod);
				check(mate.Profile.PetItemType == 0 && Pets().Length == 0, "An additional gift overrode explicit dismissal");
				check(owner.inventory[1].IsAir && mate.Profile.PetItems.Count == 4 && mate.Profile.ItemCount(ItemID.CompanionCube) == 1,
					"Pet deposit did not use the real owner inventory transaction");
				check(Equip(3, ItemID.CompanionCube, Token(3)), "Deposited pet item cannot be selected");
				Item[] inventory = owner.inventory;
				owner.inventory = inventory.Select(item => item.Clone()).ToArray();
				for (int i = 1; i < owner.inventory.Length; i++) owner.inventory[i] = new Item(ItemID.CopperShortsword);
				mate.WithdrawStorageSlot(CompanionStorage.Pets, 3, false); SettleServerInventory(mod);
				check(mate.Profile.ItemCount(ItemID.CompanionCube) == 1 && Pets().Length == 1, "Full player inventory erased an equipped pet item");
				owner.inventory = inventory;
				mate.WithdrawStorageSlot(CompanionStorage.Pets, 3, false); SettleServerInventory(mod);
				check(mate.Profile.ItemCount(ItemID.CompanionCube) == 0 && Pets().Length == 0 && mate.Profile.PetItemType == 0
					&& owner.inventory.Count(item => item.type == ItemID.CompanionCube) == 1,
					"Pet withdrawal failed conservation or immediate summon cleanup");
				sigil = (SoulboundSigil)owner.inventory[0].ModItem; // Server receipts replace inventory Item instances.
				check(!Equip(0, ItemID.ZephyrFish, new byte[32]), "Stale pet item token was accepted");
				check(Equip(0, ItemID.ZephyrFish, Token(0)) && Pets().Length == 1 && sigil.Profile.PetItemType == ItemID.ZephyrFish,
					"Owned pet did not spawn or sync to its Sigil");
				for (int i = 0; i < 80; i++) Call("UpdateEquippedPet");
				check(Pets().Length == 1 && mate.Profile.ItemCount(ItemID.ZephyrFish) == 1 && owner.buffType.SequenceEqual(oldBuffs),
					"Pet duplicates, consumes an item or modifies player buffs: " + State());
				Projectile pet = Pets()[0]; pet.Center = npc.Center - new Vector2(200, 0); pet.ModProjectile.AI();
				check(pet.velocity.X > 0 && pet.damage == 0 && !pet.friendly && pet.ModProjectile.CanDamage() == false,
					"Pet does not follow the companion or can damage critters");
				for (int i = 0; i < 40; i++) pet.ModProjectile.AI();
				check(pet.frame > 0 && pet.frame < Main.projFrames[ProjectileID.ZephyrFish], "Pet did not advance its native sprite frames");
				mate.Profile.WorkPaused = true; pet.ModProjectile.AI(); check(pet.active, "Pause deleted the cosmetic pet");
				check(Equip(1, ItemID.Nectar, Token(1)) && Pets().Length == 1 && (int)Pets()[0].ai[1] == ItemID.Nectar,
					"Switching pets left the previous familiar active");
				mate.Profile.PetItems.RemoveAt(1); Call("UpdateEquippedPet");
				check(Pets().Length == 0 && mate.Profile.PetItemType == 0 && sigil.Profile.PetItemType == 0,
					"Withdrawn item left an active or persistent pet");
				if (mode == NetmodeID.Server) {
					Type messages = mod.GetType().GetNestedType("MessageType", BindingFlags.NonPublic)!;
					byte message = Convert.ToByte(Enum.Parse(messages, "PetConfigRequest"));
					void Request(Guid id, int slot, int type, byte[] token, int trim = 0) {
						using var stream = new MemoryStream(); using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) {
							writer.Write(message); writer.Write(id.ToByteArray()); writer.Write(slot); writer.Write(type); writer.Write(token);
						}
						if (trim > 0) stream.SetLength(stream.Length - trim);
						stream.Position = 0; using var reader = new BinaryReader(stream); mod.HandlePacket(reader, 0);
					}
					Request(Guid.NewGuid(), 0, ItemID.ZephyrFish, Token(0)); Request(mate.Profile.Id, 0, ItemID.ZephyrFish, new byte[32]);
					Request(mate.Profile.Id, 0, ItemID.ZephyrFish, Token(0), 1); Request(mate.Profile.Id, int.MaxValue, ItemID.ZephyrFish, new byte[32]);
					check(Pets().Length == 0, "Stale, truncated or invalid pet request spawned an unowned pet");
					Request(mate.Profile.Id, 0, ItemID.ZephyrFish, Token(0)); check(Pets().Length == 1, "Valid owned pet packet was not wired");
					Request(mate.Profile.Id, -1, 0, new byte[32]); check(Pets().Length == 0, "Dismiss packet failed");
				}
				check(Equip(0, ItemID.ZephyrFish, Token(0)), "Cannot equip before lifecycle check");
				pet = Pets()[0]; var id = mate.Profile.Id; mate.Profile.Id = Guid.NewGuid(); pet.ModProjectile.AI();
				check(!pet.active, "Pet followed a different companion reusing the NPC slot"); mate.Profile.Id = id;
				Call("UpdateEquippedPet"); pet = Pets()[0]; owner.dead = true; pet.ModProjectile.AI();
				check(!pet.active, "Pet survived owner death"); owner.dead = false;
				Call("UpdateEquippedPet"); Main.netMode = NetmodeID.MultiplayerClient;
				check(!Equip(-1, 0, new byte[32]) && Pets().Length == 1, "Client directly mutated pet configuration"); Main.netMode = mode;
				mate.Recall(); check(Pets().Length == 0 && sigil.Profile.PetItemType == ItemID.ZephyrFish
					&& sigil.Profile.ItemCount(ItemID.ZephyrFish) == 1, "Recall lost pet ownership or left a familiar active");
			}
		}
		finally {
			Main.player[0] = oldPlayer; Main.npc = oldNpcs; Main.projectile = oldProjectiles;
			Main.netMode = oldMode; Main.myPlayer = oldLocal;
			for (int i = 0; i < Netplay.Clients.Length; i++) Netplay.Clients[i] = oldClients[i];
		}
	}
}
