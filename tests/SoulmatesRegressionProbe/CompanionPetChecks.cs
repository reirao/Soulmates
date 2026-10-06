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
		foreach (int itemType in CompanionPets.SupportedItems) {
			var profile = new CompanionProfile(); profile.Store(new Item(itemType)); profile.PetItemType = itemType;
			check(Main.projFrames[CompanionPets.VisualFor(itemType)] > 1, "Supported native pet has no animation frames");
			using var stream = new MemoryStream();
			using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) profile.Write(writer);
			stream.Position = 0; using var reader = new BinaryReader(stream);
			foreach (var copy in new[] { profile.Clone(), CompanionProfile.Load(profile.Save()), CompanionProfile.Read(reader) }) {
				check(copy.PetItemType == itemType && copy.ItemCount(itemType) == 1, "Pet selection lost item ownership through save or transport");
				check(!ReferenceEquals(copy.Pack[0], profile.Pack[0]), "Pet selection shallow-copied the owned item");
			}
			check(stream.Position == stream.Length, "Pet transport leaves extra bytes");
			stream.Position = stream.Length - 4;
			using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) writer.Write(ItemID.CopperOre);
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
					new object[] { CompanionStorage.Pack, slot })!;
				bool Equip(int slot, int type, byte[] token) => (bool)Call("ConfigurePet", slot, type, token)!;
				Projectile[] Pets() => Main.projectile.Where(p => p.active && p.ModProjectile is CompanionFamiliar).ToArray();
				int[] oldBuffs = (int[])owner.buffType.Clone();
				string State() => $"mode={mode}, selection={mate.Profile.PetItemType}, count={Pets().Length}, item={mate.Profile.ItemCount(ItemID.ZephyrFish)}, buffs={owner.buffType.SequenceEqual(oldBuffs)}; "
					+ string.Join("; ", Pets().Select(p => $"slot={p.whoAmI},ai={string.Join(',', p.ai)},life={p.timeLeft},active={p.active}"));
				mate.Profile.Store(new Item(ItemID.ZephyrFish)); mate.Profile.Store(new Item(ItemID.Nectar)); mate.Profile.Store(new Item(ItemID.Carrot));
				check(!Equip(2, ItemID.Carrot, Token(2)) && Pets().Length == 0, "Unsupported pet was spawned");
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
				mate.Profile.Pack.RemoveAt(1); Call("UpdateEquippedPet");
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
