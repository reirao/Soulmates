#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Soulmates.Common;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Soulmates.Content.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace SoulmatesAuditProbe;

public sealed partial class AuditChecks
{
    private void CheckWorkAndPetBoundaries(Mod mod, Player owner)
    {
        var custom = new Item(ModContent.ItemType<MetadataPetItem>());
        ((MetadataPetItem)custom.ModItem).Marker = 947;
        var profile = new CompanionProfile(); profile.Store(custom);
        // Store consumes the source stack; selection uses the owned item, never a fabricated summon.
        profile.PetItemType = profile.PetItems[0].type;
        Expect(CompanionPets.SupportedItems.Contains(profile.PetItemType) && CompanionPets.VisualFor(profile.PetItemType) == ProjectileID.Bunny,
            "Registered mod pet is discovered without adding an item whitelist", "Metadata supplies its native visual");
        TagCompound save = profile.Save(); save["petItemType"] = ItemID.CopperOre;
        CompanionProfile loaded = CompanionProfile.Load(save), clone = profile.Clone();
        using var bytes = new MemoryStream(); using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true)) profile.Write(writer);
        bytes.Position = 0; using var reader = new BinaryReader(bytes); CompanionProfile network = CompanionProfile.Read(reader);
        Expect(new[] { loaded, clone, network }.All(p => p.PetItemType == profile.PetItemType && p.PetItems.Count == 1
            && ((MetadataPetItem)p.PetItems[0].ModItem).Marker == 947 && !ReferenceEquals(p.PetItems[0], profile.PetItems[0])),
            "Pet item custom data survives clone, save and profile transport", "Save selection uses stable ItemIO identity, not the raw saved type");
        Main.netMode = NetmodeID.Server; Main.myPlayer = 255; Main.dedServ = true;
        owner.dead = false;
        SoulboundCompanion mate = CreateCompanion(owner, 193, 2, "Work and pet boundary");
        owner.GetModPlayer<SoulmatesPlayer>().ActiveCompanionWhoAmI = 193;
        mate.Profile.AutonomyEnabled = false;
        Expect(!mate.PerformQuickAction(CompanionQuickAction.RepeatLastWork).Accepted && mate.Profile.LastWork is null,
            "New companion has no invented Last command", "No terrain or cargo action");
        var recipe = new CompanionWorkRecipe(CompanionWorkKind.GatherTarget, CompanionMiningApproach.Tunnel,
            CompanionMiningDirection.Down, CompanionTunnelEnd.Short);
        mate.Profile.LastWork = recipe;
        Expect(!mate.PerformQuickAction(CompanionQuickAction.RepeatLastWork).Accepted && mate.Profile.LastWork == recipe,
            "Directed repeat without a fresh target cannot replay a cached location", "Last stays unchanged");
        byte[] settings = Packet(mod, "MiningConfigRequest", w => {
            w.Write(Guid.NewGuid().ToByteArray()); w.Write((byte)1); w.Write((byte)3); w.Write((byte)1);
        });
        Deliver(mod, settings);
        Expect(mate.Profile.MiningApproach == CompanionMiningApproach.Adaptive && mate.Profile.LastWork == recipe,
            "Stale work settings cannot configure the active companion", "Identity-bound packet");
        Item oldSource = owner.inventory[25].Clone();
        Item gift = new Item(ModContent.ItemType<MetadataPetItem>());
        ((MetadataPetItem)gift.ModItem).Marker = 111;
        owner.inventory[25] = gift;
        byte[] GiftToken() {
            using var data = new MemoryStream(); using var writer = new BinaryWriter(data);
            ItemIO.Send(owner.inventory[25], writer, writeStack: true, writeFavorite: true);
            return System.Security.Cryptography.SHA256.HashData(data.ToArray());
        }
        byte[] Gift(Guid id, byte[] token) => Packet(mod, "PetStoreRequest", w => {
            w.Write(id.ToByteArray()); w.Write((byte)25); w.Write(token);
        });
        byte[] token = GiftToken();
        ((MetadataPetItem)gift.ModItem).Marker = 112;
        Deliver(mod, Gift(mate.Profile.Id, token));
        Deliver(mod, Gift(Guid.NewGuid(), GiftToken()));
        Expect(mate.Profile.PetItems.Count == 0 && ((MetadataPetItem)owner.inventory[25].ModItem).Marker == 112,
            "Pet gifts validate source metadata and active companion identity", "Changed source remains with owner");
        foreach (int petType in CompanionPets.SupportedItems.Take(CompanionProfile.MaximumPetSlots)) mate.Profile.Store(new Item(petType));
        Deliver(mod, Gift(mate.Profile.Id, GiftToken()));
        Expect(!owner.inventory[25].IsAir && mate.Profile.PetItems.Count == CompanionProfile.MaximumPetSlots,
            "A full pet inventory refuses gifts without losing the source", "Twelve owned slots retained");
        mate.Profile.ClearCargo(); mate.SyncProfileToBoundSigil();
        var client = new Player { whoAmI = owner.whoAmI, active = true };
        client.inventory = owner.inventory.Select(item => item.Clone()).ToArray();
        byte[] request = Gift(mate.Profile.Id, GiftToken());
        using (var pendingData = new MemoryStream(request)) using (var pendingReader = new BinaryReader(pendingData)) mod.HandlePacket(pendingReader, 0);
        Proposal(PendingInventory(owner), out Guid receiptId, out _);
        client.inventory[25] = new Item(ItemID.Wood, 9);
        Receipt(mod, owner, client, receiptId, accepted: false);
        Expect(mate.Profile.PetItems.Count == 0 && owner.inventory[25].type == ItemID.Wood && owner.inventory[25].stack == 9,
            "Rejected pet gift receipt restores ownership without overwriting a newer inventory item", "No phantom pet after rollback");
        owner.inventory[25] = new Item(ModContent.ItemType<MetadataPetItem>());
        ((MetadataPetItem)owner.inventory[25].ModItem).Marker = 113;
        request = Gift(mate.Profile.Id, GiftToken()); Deliver(mod, request); Deliver(mod, request);
        Expect(owner.inventory[25].IsAir && mate.Profile.PetItems.Count == 1 && ((MetadataPetItem)mate.Profile.PetItems[0].ModItem).Marker == 113,
            "A valid pet gift preserves custom data and cannot replay", "One owned item from non-hotbar slot 25");
        mate.Profile.ClearCargo(); mate.SyncProfileToBoundSigil(); owner.inventory[25] = oldSource;
        mate.Profile.Store(new Item(ItemID.Nectar));
        mate.Profile.Store(new Item(ItemID.ZephyrFish));
        byte[] PetPacket(int slot, int type, byte[] token) => Packet(mod, "PetConfigRequest", w => {
            w.Write(mate.Profile.Id.ToByteArray()); w.Write(slot); w.Write(type); w.Write(token);
        });
        byte[] stale = PetPacket(0, ItemID.Nectar, StorageToken(mate.Profile, CompanionStorage.Pets, 0));
        mate.Profile.PetItems.Reverse();
        Deliver(mod, stale);
        Expect(mate.Profile.PetItemType == 0, "Reordered pack rejects a stale pet selection", "No item consumed or summon authorized");
        int nativeSlot = Projectile.NewProjectile(mate.NPC.GetSource_FromAI(), owner.Center,
            Microsoft.Xna.Framework.Vector2.Zero, ProjectileID.ZephyrFish, 0, 0f, owner.whoAmI);
        Projectile nativePet = Main.projectile[nativeSlot];
        int pets() => Main.projectile.Count(p => p.active && p.ModProjectile is CompanionFamiliar);
        Deliver(mod, PetPacket(0, ItemID.ZephyrFish, StorageToken(mate.Profile, CompanionStorage.Pets, 0)));
        Expect(pets() == 1 && mate.Profile.ItemCount(ItemID.ZephyrFish) == 1 && nativePet.active,
            "Companion familiar coexists with the player's native pet", "Real item remains in cargo");
        Projectile familiar = Main.projectile.First(p => p.active && p.ModProjectile is CompanionFamiliar);
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) familiar.ModProjectile.SendExtraAI(writer);
        Expect(stream.ToArray().SequenceEqual(mate.Profile.Id.ToByteArray()),
            "Spawned familiar already contains its full companion identity", "Binding exists before synchronization");
        Main.netMode = NetmodeID.MultiplayerClient; mate.Profile.PetItemType = 0;
        for (int i = 0; i < 10; i++) familiar.ModProjectile.AI();
        Expect(familiar.active, "Joining client tolerates a delayed selected-pet profile", "Bounded synchronization grace");
        mate.Profile.PetItemType = ItemID.ZephyrFish; familiar.ModProjectile.AI();
        Expect(familiar.active, "Delayed profile restores normal familiar life", "No duplicate spawn needed");
        mate.Profile.PetItemType = 0;
        for (int i = 0; i < 61 && familiar.active; i++) familiar.ModProjectile.AI();
        Expect(!familiar.active, "Client grace cannot keep a detached familiar forever", "Expired after bounded wait");
        Main.netMode = NetmodeID.Server;
        Deliver(mod, PetPacket(0, ItemID.ZephyrFish, StorageToken(mate.Profile, CompanionStorage.Pets, 0)));
        mate.Recall();
        var sigil = (SoulboundSigil)owner.inventory[2].ModItem;
        Expect(pets() == 0 && nativePet.active && sigil.Profile.PetItemType == ItemID.ZephyrFish
            && sigil.Profile.ItemCount(ItemID.ZephyrFish) == 1,
            "Recall retires only the companion's familiar and retains the real item", "Player pet survives");
        nativePet.Kill();
    }
}
