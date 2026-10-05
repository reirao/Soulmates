#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Soulmates.Common;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace SoulmatesAuditProbe;

public sealed class ReviewStone : ModTile
{
    public override string Texture => "Terraria/Images/Tiles_1";
    public override void SetStaticDefaults() => Main.tileSolid[Type] = true;
}

public sealed partial class AuditChecks
{
    private static readonly Type SyncType = typeof(CompanionProfile).Assembly.GetType("Soulmates.Common.CompanionInventorySync")!;
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

    private static object PendingInventory(Player owner) => ((IDictionary)SyncType.GetField("Transactions", StaticPrivate)!
        .GetValue(null)!)[owner.whoAmI]!;
    private static void ResetInventoryProtocol() => SyncType.GetMethod("Reset", StaticPrivate)!.Invoke(null, null);

    private static byte[] Proposal(object pending, out Guid id, out int changedSlot)
    {
        Type type = pending.GetType();
        id = (Guid)type.GetProperty("Id")!.GetValue(pending)!;
        var changes = ((IEnumerable)type.GetProperty("Changes")!.GetValue(pending)!).Cast<object>().ToList();
        changedSlot = (byte)changes[0].GetType().GetProperty("Slot")!.GetValue(changes[0])!;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(id.ToByteArray()); writer.Write((byte)changes.Count);
        foreach (object change in changes) {
            Type c = change.GetType();
            writer.Write((byte)c.GetProperty("Slot")!.GetValue(change)!);
            ItemIO.Send((Item)c.GetProperty("Before")!.GetValue(change)!, writer, writeStack: true, writeFavorite: true);
            ItemIO.Send((Item)c.GetProperty("After")!.GetValue(change)!, writer, writeStack: true, writeFavorite: true);
        }
        foreach (byte[] token in (byte[][])type.GetProperty("BeforeTokens")!.GetValue(pending)!) writer.Write(token);
        var companion = type.GetProperty("Companion")!.GetValue(pending) as SoulboundCompanion;
        writer.Write(companion is not null); companion?.Profile.Write(writer);
        return stream.ToArray();
    }

    private static void ApplyProposal(byte[] data)
    {
        using var stream = new MemoryStream(data);
        using var reader = new BinaryReader(stream);
        SyncType.GetMethod("Apply", StaticPrivate)!.Invoke(null, new object[] { reader });
    }

    private static void Receipt(Mod mod, Player server, Player client, Guid id, bool accepted)
    {
        byte[] data = Packet(mod, "InventoryReceipt", writer => {
            writer.Write(id.ToByteArray()); writer.Write(accepted);
            writer.Write((byte)58);
            for (byte slot = 0; slot < 58; slot++) {
                writer.Write(slot); ItemIO.Send(client.inventory[slot], writer, writeStack: true, writeFavorite: true);
            }
        });
        Main.player[server.whoAmI] = server; Main.netMode = NetmodeID.Server; Main.myPlayer = 255;
        using var stream = new MemoryStream(data); using var reader = new BinaryReader(stream);
        mod.HandlePacket(reader, server.whoAmI);
    }

    private void CheckInventoryProtocol(Mod mod, Player server, SoulboundCompanion mate)
    {
        Item[] saved = server.inventory.Select(item => item.Clone()).ToArray();
        CompanionProfile savedProfile = mate.Profile.Clone();
        Player oldCache = Main.clientPlayer;
        try {
            ResetInventoryProtocol();
            mate.Profile.ClearCargo(); mate.Profile.Resources.Add(new Item(ItemID.Wood, 10));
            mate.SyncProfileToBoundSigil();
            var client = new Player { whoAmI = server.whoAmI, active = true };
            client.inventory = server.inventory.Select(item => item.Clone()).ToArray();
            mate.WithdrawStorageSlot(CompanionStorage.Resources, 0, true);
            byte[] proposal = Proposal(PendingInventory(server), out Guid id, out int slot);
            client.inventory[slot] = new Item(ItemID.GoldPickaxe);
            Main.player[0] = client; Main.netMode = NetmodeID.MultiplayerClient; Main.myPlayer = 0;
            Main.clientPlayer = new Player();
            ApplyProposal(proposal);
            Expect(client.inventory[slot].type == ItemID.GoldPickaxe && !client.inventory.Any(item => item.type == ItemID.Wood),
                "Conflict rejects the complete withdrawal without replacing the new client item", "Pickaxe retained, no Wood delivered");
            Receipt(mod, server, client, id, accepted: false);
            Expect(mate.Profile.ItemCount(ItemID.Wood) == 10 && server.inventory[slot].type == ItemID.GoldPickaxe,
                "Rejected receipt restores cargo and preserves the intervening owner item", "Cargo 10, pickaxe retained");
            Receipt(mod, server, client, id, accepted: true);
            Expect(mate.Profile.ItemCount(ItemID.Wood) == 10,
                "An already resolved inventory receipt cannot commit twice", "Cargo unchanged");

            ResetInventoryProtocol();
            client.inventory = server.inventory.Select(item => item.Clone()).ToArray();
            mate.WithdrawStorageSlot(CompanionStorage.Resources, 0, true);
            proposal = Proposal(PendingInventory(server), out id, out slot);
            Main.player[0] = client; Main.netMode = NetmodeID.MultiplayerClient; Main.myPlayer = 0;
            ApplyProposal(proposal);
            Expect(client.inventory[slot].type == ItemID.Wood && client.inventory[slot].stack == 1,
                "A matching preimage delivers exactly the intended withdrawal", "One Wood");
            Main.player[0] = server; Main.netMode = NetmodeID.Server; Main.myPlayer = 255;
            byte[] invalidReceipt = Packet(mod, "InventoryReceipt", w => {
                w.Write(id.ToByteArray()); w.Write(true); w.Write((byte)1); w.Write((byte)58);
                ItemIO.Send(new Item(), w, writeStack: true, writeFavorite: true);
            });
            using (var s = new MemoryStream(invalidReceipt)) using (var r = new BinaryReader(s)) mod.HandlePacket(r, 0);
            Expect(PendingInventory(server) is not null,
                "An invalid receipt does not commit or discard the pending transaction", "Pending transaction retained");
            Receipt(mod, server, client, id, accepted: true);
            Expect(mate.Profile.ItemCount(ItemID.Wood) == 9 && server.inventory[slot].stack == 1,
                "Accepted receipt conserves owner and companion quantities", "9 + 1 Wood");
            Main.player[0] = client; Main.netMode = NetmodeID.MultiplayerClient; Main.myPlayer = 0;
            client.inventory[slot] = new Item(ItemID.GoldPickaxe);
            ApplyProposal(proposal);
            Expect(client.inventory[slot].type == ItemID.GoldPickaxe,
                "A duplicate proposal never reapplies its item replacements", "Later pickaxe retained");
            byte[] completion = Packet(mod, "InventoryResolved", w => {
                w.Write(id.ToByteArray()); w.Write(true); w.Write(true); mate.Profile.Write(w);
            });
            using (var stream = new MemoryStream(completion)) using (var reader = new BinaryReader(stream)) mod.HandlePacket(reader, 256);
            mate.Profile.Mood = 77;
            using (var stream = new MemoryStream(completion)) using (var reader = new BinaryReader(stream)) mod.HandlePacket(reader, 256);
            Expect(mate.Profile.Mood == 77, "A duplicate completion cannot restore an older companion profile", "New mood retained");

            ResetInventoryProtocol();
            Main.player[0] = server; Main.netMode = NetmodeID.Server; Main.myPlayer = 255;
            server.inventory[3] = new Item(ItemID.StoneBlock, 2); server.selectedItem = 3;
            client.inventory = server.inventory.Select(item => item.Clone()).ToArray();
            mate.StoreSelectedItem();
            proposal = Proposal(PendingInventory(server), out id, out slot);
            client.inventory[3].TurnToAir(); client.inventory[4] = new Item(ItemID.StoneBlock, 2);
            Main.player[0] = client; Main.netMode = NetmodeID.MultiplayerClient; Main.myPlayer = 0;
            ApplyProposal(proposal); Receipt(mod, server, client, id, accepted: false);
            Expect(mate.Profile.ItemCount(ItemID.StoneBlock) == 0 && server.inventory[4].stack == 2,
                "Moved deposit input rejects without duplicating owner material into cargo", "Both Stone retained by owner");
        }
        finally {
            ResetInventoryProtocol(); Main.player[0] = server; Main.netMode = NetmodeID.Server; Main.myPlayer = 255;
            server.inventory = saved; mate.Profile = savedProfile; mate.SyncProfileToBoundSigil(); Main.clientPlayer = oldCache;
        }
    }

    private void CheckStableMiningKnowledge()
    {
        int modTile = ModContent.TileType<ReviewStone>();
        var profile = new CompanionProfile();
        profile.LearnMiningMaterial(modTile, 55); profile.LearnMiningMaterial(TileID.Dirt, 55);
        TagCompound saved = profile.Save();
        Expect(saved.GetList<string>("learnedMiningKeys").Contains("SoulmatesAuditProbe/ReviewStone")
            && !saved.ContainsKey("learnedMiningTiles"), "Learned modded material persists a stable content key", "No numeric save identity");
        saved["learnedMiningTiles"] = new List<int> { TileID.Stone };
        CompanionProfile loaded = CompanionProfile.Load(saved);
        Expect(loaded.KnowsMiningMaterial(modTile) && loaded.KnowsMiningMaterial(TileID.Dirt) && !loaded.KnowsMiningMaterial(TileID.Stone),
            "Stable knowledge keys take precedence over obsolete numeric save IDs", "Original material retained");
        saved["learnedMiningKeys"] = new List<string> { "AbsentMod/Material", "Terraria/0" };
        loaded = CompanionProfile.Load(saved);
        Expect(loaded.KnowsMiningMaterial(TileID.Dirt) && loaded.Save().GetList<string>("learnedMiningKeys").Contains("AbsentMod/Material"),
            "Missing mod knowledge is retained by name without aliasing a current tile", "Missing identity preserved");
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        loaded.Clone().Write(writer); stream.Position = 0; using var reader = new BinaryReader(stream);
        Expect(CompanionProfile.Read(reader).Save().GetList<string>("learnedMiningKeys").Contains("AbsentMod/Material"),
            "Missing knowledge survives clone and native profile transport", "Name retained");
        var missing = Enumerable.Range(0, CompanionProfile.MaximumLearnedMiningTiles)
            .Select(index => $"AbsentMod/Material{index}").ToList();
        loaded = CompanionProfile.Load(new TagCompound { ["learnedMiningKeys"] = missing });
        Expect(!loaded.LearnMiningMaterial(TileID.Stone, 55)
            && loaded.Save().GetList<string>("learnedMiningKeys").SequenceEqual(missing),
            "Unavailable materials retain their places in the knowledge limit", "No missing knowledge displaced");
        var legacy = new TagCompound { ["learnedMiningTiles"] = new List<int> { TileID.Dirt, modTile } };
        loaded = CompanionProfile.Load(legacy);
        Expect(loaded.KnowsMiningMaterial(TileID.Dirt) && !loaded.KnowsMiningMaterial(modTile),
            "Legacy knowledge migrates vanilla IDs but never guesses unidentified modded IDs", "Conservative migration");
    }
}
