#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Common.Dialogue;
using Soulmates.Common.UI;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader;

namespace SoulmatesAuditProbe;

public sealed partial class AuditChecks
{
    private static byte[] StorageToken(CompanionProfile profile, CompanionStorage storage, int slot)
        => (byte[])typeof(CompanionProfile).GetMethod("StorageToken", Private)!
            .Invoke(profile, new object[] { storage, slot })!;

    private static byte[] MiningPacket(Mod mod, SoulboundCompanion mate, Point point, int tool)
        => Packet(mod, "MiningObservationRequest", w => {
            w.Write(mate.Profile.Id.ToByteArray()); w.Write((short)point.X); w.Write((short)point.Y); w.Write(tool);
        });

    private static byte[] WithdrawPacket(Mod mod, SoulboundCompanion mate, CompanionStorage storage, int slot)
        => Packet(mod, "PackWithdrawRequest", w => {
            w.Write(mate.Profile.Id.ToByteArray()); w.Write((byte)slot); w.Write(true); w.Write((byte)storage);
            w.Write(StorageToken(mate.Profile, storage, slot));
        });

    private void CheckRepairBoundaries(Mod mod, Player owner, SoulboundCompanion old, SoulboundCompanion mate)
    {
        Main.netMode = NetmodeID.Server; Main.myPlayer = 255;
        mate.SetCommand(stay: false);
        mate.Profile.WorkPaused = false;
        byte[][] late = [
            Packet(mod, "RecallRequest", w => w.Write(old.Profile.Id.ToByteArray())),
            Packet(mod, "TalkRequest", w => {
                w.Write(old.Profile.Id.ToByteArray()); w.Write((byte)TalkCategory.Commands);
                w.Write((byte)1); w.Write(0); w.Write((byte)CompanionItemTopic.All);
            }),
            Packet(mod, "EmoteRequest", w => { w.Write(old.Profile.Id.ToByteArray()); w.Write((byte)0); }),
            Packet(mod, "TrinketRequest", w => { w.Write(old.Profile.Id.ToByteArray()); w.Write((byte)0); }),
            Packet(mod, "NativeEmoteRequest", w => { w.Write(old.Profile.Id.ToByteArray()); w.Write(EmoteID.RPSScissors); }),
            Packet(mod, "BehaviorObservationRequest", w => { w.Write(old.Profile.Id.ToByteArray()); w.Write((byte)LearnedBehavior.Gathering); }),
            Packet(mod, "DirectOrderRequest", w => {
                w.Write(old.Profile.Id.ToByteArray()); w.Write((byte)CompanionTargetOrder.Gather);
                w.Write((short)0); w.Write((short)0); w.Write((short)10); w.Write(ItemID.Gel); w.Write((byte)0);
            })
        ];
        int insight = mate.Profile.GatheringInsight, bond = mate.Profile.Bond;
        foreach (byte[] request in late) {
            Deliver(mod, request);
            Expect(mate.NPC.active && !mate.Profile.WorkPaused && mate.CurrentJob == CompanionJob.None
                && mate.Profile.GatheringInsight == insight && mate.Profile.Bond == bond
                && (int)Field(mate, "rpsCooldown")! == 0,
                "Old companion identity rejected by packet family " + request[0], "Current companion unchanged");
        }

        mate.Profile.ClearCargo();
        mate.Profile.Resources.Add(new Item(ItemID.Wood, 10));
        byte[] oldSlot = WithdrawPacket(mod, mate, CompanionStorage.Resources, 0);
        mate.Profile.Resources[0] = new Item(ItemID.StoneBlock, 10);
        Deliver(mod, oldSlot);
        Expect(mate.Profile.ItemCount(ItemID.StoneBlock) == 10,
            "Compacted or replaced slot cannot withdraw a different item", "Stone remains 10");
        byte[] oldQuantity = WithdrawPacket(mod, mate, CompanionStorage.Resources, 0);
        mate.Profile.Resources[0].stack = 11;
        Deliver(mod, oldQuantity);
        Expect(mate.Profile.ItemCount(ItemID.StoneBlock) == 11,
            "Changed stack quantity invalidates an old view", "Stone remains 11");
        int ownerBefore = owner.inventory.Where(item => item.type == ItemID.StoneBlock).Sum(item => item.stack);
        Deliver(mod, WithdrawPacket(mod, mate, CompanionStorage.Resources, 0));
        int ownerAfter = owner.inventory.Where(item => item.type == ItemID.StoneBlock).Sum(item => item.stack);
        Expect(mate.Profile.ItemCount(ItemID.StoneBlock) == 10 && ownerAfter == ownerBefore + 1,
            "Fresh valid withdrawal transfers exactly one item", "Storage and owner conserved");

        mate.Profile.Store(new Item(ItemID.CopperCoin, 200));
        byte[] oldWallet = WithdrawPacket(mod, mate, CompanionStorage.Wallet, 3);
        mate.Profile.Store(new Item(ItemID.CopperCoin, 1));
        Deliver(mod, oldWallet);
        Expect(mate.Profile.WalletCopper == 201, "Changed wallet invalidates an old withdrawal", "Balance stays 201");
        Deliver(mod, WithdrawPacket(mod, mate, CompanionStorage.Wallet, 3));
        Expect(mate.Profile.WalletCopper == 200, "Fresh valid wallet withdrawal works", "One copper transferred");

        mate.Profile.ClearCargo();
        var marked = new Item(ModContent.ItemType<ReducedStackResource>(), 4);
        mate.Profile.Resources.Add(marked);
        ((ReducedStackResource)marked.ModItem).Marker = 7;
        byte[] oldMetadata = WithdrawPacket(mod, mate, CompanionStorage.Resources, 0);
        ((ReducedStackResource)marked.ModItem).Marker = 8;
        Deliver(mod, oldMetadata);
        Expect(mate.Profile.ItemCount(marked.type) == 4,
            "Changed native-network item metadata invalidates the old view", "All marked units retained");
        Deliver(mod, WithdrawPacket(mod, mate, CompanionStorage.Resources, 0));
        Expect(mate.Profile.ItemCount(marked.type) == 3
            && owner.inventory.Any(item => item.type == marked.type && ((ReducedStackResource)item.ModItem).Marker == 8),
            "Fresh withdrawal preserves modded metadata", "Marker 8 transferred");

        var legacy = new Item(marked.type) { stack = 240 };
        mate.Profile.Resources.Clear(); mate.Profile.Resources.Add(legacy);
        for (int i = 1; i < CompanionProfile.MaximumResourceSlots; i++)
            mate.Profile.Resources.Add(new Item(ItemID.StoneBlock, 1));
        int heldBefore = owner.inventory.Where(item => item.type == marked.type).Sum(item => item.stack);
        mate.WithdrawStorageSlot(CompanionStorage.Resources, 0, singleItem: false);
        SettleTransactions(ModLoader.GetMod("Soulmates"));
        Expect(mate.Profile.ItemCount(marked.type) == 140
            && owner.inventory.Where(item => item.type == marked.type).Sum(item => item.stack) == heldBefore + 100
            && owner.inventory.Where(item => item.type == marked.type).All(item => item.stack <= item.maxStack),
            "Legacy withdrawal transfers one legal stack without losing its remainder", "100 transferred, 140 retained");
        Item[] inventoryBeforeFull = owner.inventory.Select(item => item.Clone()).ToArray();
        foreach (Item item in owner.inventory) {
            if (item.IsAir) item.SetDefaults(ItemID.CopperShortsword);
            item.stack = item.maxStack;
        }
        mate.UnloadPack();
        SettleTransactions(ModLoader.GetMod("Soulmates"));
        Expect(mate.Profile.ItemCount(marked.type) == 140 && mate.Profile.Resources.Count == CompanionProfile.MaximumResourceSlots,
            "Full owner inventory leaves all legacy remainder in cargo", "No units discarded or world drops created");
        owner.inventory = inventoryBeforeFull;
        mate.UnloadPack();
        SettleTransactions(ModLoader.GetMod("Soulmates"));
        Expect(mate.Profile.ItemCount(marked.type) == 0
            && owner.inventory.Where(item => item.type == marked.type).Sum(item => item.stack) == heldBefore + 240
            && owner.inventory.Where(item => item.type == marked.type).All(item => item.stack <= item.maxStack),
            "Unload returns every legacy unit in legal native stacks", "240 units conserved");

        Main.netMode = NetmodeID.SinglePlayer; Main.myPlayer = 0;
        mate.SetCommand(stay: false);
        ((CompanionAttention)Field(mate, "attention")!).Reset();
        mate.Profile.GatheringInitiative = CompanionInitiativePolicy.Ask;
        Set(mate, "personalQuestionCooldown", 0); Set(mate, "guardianTarget", -1);
        bool question = (bool)Call(mate, "BeginChoiceQuestion", CompanionQuestion.Company)!;
        Set(mate, "autonomyDecisionTimer", 0);
        Call(mate, "UpdateHelpfulAutonomy");
        Expect(question && mate.HasPendingQuestion && !mate.HasPendingInitiative
            && Field(mate, "autonomyActivity")!.ToString() == "None",
            "Unapproved work cannot bypass a personal question", "Ask policy still waits for consent");
        Call(mate, "ClearChoiceQuestion");
    }

    private void CheckLegacyCargo()
    {
        int type = ModContent.ItemType<ReducedStackResource>();
        Item Legacy() => new(type) { stack = 240, maxStack = 500, favorited = true };
        var profile = new CompanionProfile { Experience = CompanionProfile.ExperienceForLevel(20) };
        profile.Resources.Add(Legacy());
        CompanionProfile loaded = CompanionProfile.Load(profile.Save());
        Expect(loaded.Resources.Count == 3 && loaded.ItemCount(type) == 240
            && loaded.Resources.All(item => item.stack <= item.maxStack && item.favorited),
            "Reduced stack maximum splits without losing metadata or units", "100 + 100 + 40");
        loaded.Normalize(); loaded.Normalize();
        Expect(loaded.Resources.Count == 3 && loaded.ItemCount(type) == 240,
            "Repeated cargo normalization is idempotent", "Three stacks, 240 units");
        var full = new CompanionProfile { Experience = CompanionProfile.ExperienceForLevel(20) };
        full.Resources.Add(Legacy());
        for (int i = 1; i < CompanionProfile.MaximumResourceSlots; i++)
            full.Resources.Add(new Item(ItemID.StoneBlock, 1));
        loaded = CompanionProfile.Load(full.Save());
        Expect(loaded.Resources.Count == CompanionProfile.MaximumResourceSlots && loaded.ItemCount(type) == 240,
            "Full cargo retains legacy remainder rather than deleting it", "All units retained within slot bound");
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) loaded.Write(writer);
        stream.Position = 0;
        using var reader = new BinaryReader(stream);
        CompanionProfile transported = CompanionProfile.Read(reader);
        Expect(transported.ItemCount(type) == 240 && transported.Resources.Count == CompanionProfile.MaximumResourceSlots,
            "Full legacy cargo survives binary transport", "240 units retained");
        var returned = transported.ExtractExcess(type, 99);
        Expect(returned.Sum(item => item.stack) + transported.ItemCount(type) == 240
            && returned.All(item => item.stack > 0 && item.stack <= item.maxStack),
            "Reconciliation returns legal native stacks and conserves cargo", "99 retained, 141 returned");
    }

    private void CheckMiningObservation(Mod mod, Player owner, SoulboundCompanion mate)
    {
        Main.netMode = NetmodeID.Server; Main.myPlayer = 255;
        mate.SetCommand(stay: false);
        mate.Profile.LearnedMiningTiles.Clear(); mate.Profile.ObservedPickPower = 35;
        owner.selectedItem = 2; owner.inventory[2] = new Item(ItemID.GoldPickaxe);
        owner.controlUseItem = true; owner.itemAnimation = 1;
        Point sand = new(32, 30);
        Tile sandTile = Main.tile[sand.X, sand.Y];
        sandTile.HasTile = true; sandTile.TileType = TileID.Sand;
        void ResetCooldown() => typeof(SoulmatesPlayer).GetField("serverMiningObservationCooldown", Private)!
            .SetValue(owner.GetModPlayer<SoulmatesPlayer>(), 0);
        ResetCooldown(); Deliver(mod, MiningPacket(mod, mate, sand, ItemID.CopperPickaxe));
        Expect(!mate.Profile.KnowsMiningMaterial(TileID.Sand), "Incorrect held-tool identity cannot teach a material", "Sand unknown");
        ResetCooldown();
        Deliver(mod, Packet(mod, "MiningObservationRequest", w => {
            w.Write(Guid.NewGuid().ToByteArray()); w.Write((short)sand.X); w.Write((short)sand.Y); w.Write(owner.HeldItem.type);
        }));
        Expect(!mate.Profile.KnowsMiningMaterial(TileID.Sand), "Wrong companion identity cannot teach a material", "Sand unknown");
        ResetCooldown(); owner.controlUseItem = false; owner.itemAnimation = 0;
        Deliver(mod, MiningPacket(mod, mate, sand, owner.HeldItem.type));
        Expect(!mate.Profile.KnowsMiningMaterial(TileID.Sand), "Idle owner cannot submit mining observations", "Sand unknown");
        ResetCooldown(); owner.controlUseItem = true; owner.itemAnimation = 1;
        Point distant = new(70, 70);
        Tile distantTile = Main.tile[distant.X, distant.Y];
        distantTile.HasTile = true; distantTile.TileType = TileID.Sand;
        Deliver(mod, MiningPacket(mod, mate, distant, owner.HeldItem.type));
        Expect(!mate.Profile.KnowsMiningMaterial(TileID.Sand), "Out-of-reach terrain cannot be learned", "Sand unknown");
        ResetCooldown(); Player.tileTargetX = 70; Player.tileTargetY = 70;
        Deliver(mod, MiningPacket(mod, mate, sand, owner.HeldItem.type));
        Expect(mate.Profile.KnowsMiningMaterial(TileID.Sand) && mate.Profile.ObservedPickPower == owner.HeldItem.pick,
            "Reachable real target learns independently of unrelated mouse globals", "Sand learned with Gold Pickaxe");

        Main.netMode = NetmodeID.SinglePlayer; Main.myPlayer = 0;
        Point dirt = new(31, 30);
        Tile dirtTile = Main.tile[dirt.X, dirt.Y];
        dirtTile.HasTile = true; dirtTile.TileType = TileID.Dirt;
        mate.Profile.LearnedMiningTiles.Clear();
        owner.PickTile(dirt.X, dirt.Y, owner.HeldItem.pick);
        Expect(mate.Profile.KnowsMiningMaterial(TileID.Dirt),
            "Actual native PickTile hook teaches before a one-hit block disappears", "Single-player hook observed Dirt");
        Expect(!Main.tile[dirt.X, dirt.Y].HasTile,
            "One-hit fixture actually removes its native tile", "Observation survived original PickTile removal");
    }

    private void CheckInitiativeIdentity(Mod mod, Player owner, SoulboundCompanion mate)
    {
        Main.netMode = NetmodeID.Server; Main.myPlayer = 255;
        mate.SetCommand(stay: false);
        ((CompanionAttention)Field(mate, "attention")!).Reset();
        mate.Profile.GatheringInitiative = CompanionInitiativePolicy.Ask;
        AskGather(mate, 10);
        Guid token = mate.InitiativeId;
        Expect(mate.HasPendingInitiative && token != Guid.Empty,
            "Synchronization fixture starts a real pending initiative", "Gathering token created");
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            mate.SendExtraAI(writer);
        Set(mate, "initiativeId", Guid.Empty);
        stream.Position = 0;
        using (var reader = new BinaryReader(stream, Encoding.UTF8, true)) mate.ReceiveExtraAI(reader);
        Expect(mate.HasPendingInitiative && mate.InitiativeId == token && token != Guid.Empty,
            "Authoritative NPC synchronization preserves the exact question token", "ExtraAI round-trip");
        var prompt = ModContent.GetInstance<InitiativePromptSystem>();
        typeof(InitiativePromptSystem).GetField("replyWaitTicks", Private)!.SetValue(prompt, 600);
        typeof(InitiativePromptSystem).GetField("replyProfileId", Private)!.SetValue(prompt, mate.Profile.Id);
        typeof(InitiativePromptSystem).GetField("replyKind", Private)!.SetValue(prompt, CompanionInitiativeKind.Gathering);
        typeof(InitiativePromptSystem).GetField("replyInitiativeId", Private)!.SetValue(prompt, token);
        bool Waiting() => (bool)typeof(InitiativePromptSystem).GetMethod("IsAwaitingReply", Private)!
            .Invoke(prompt, new object[] { mate })!;
        Expect(Waiting(), "Answered prompt waits only for its own acknowledgement", "Original token waiting");
        Call(mate, "ClearPendingInitiative", 0); AskGather(mate, 11);
        Expect(!Waiting(), "Replacement question is not hidden by the previous reply wait", "New token visible");
        Main.netMode = NetmodeID.SinglePlayer; Main.myPlayer = 0;
        typeof(InitiativePromptSystem).GetField("companion", Private)!.SetValue(prompt, mate);
        typeof(InitiativePromptSystem).GetField("openProfileId", Private)!.SetValue(prompt, mate.Profile.Id);
        typeof(InitiativePromptSystem).GetField("openInitiativeKind", Private)!.SetValue(prompt, CompanionInitiativeKind.Gathering);
        typeof(InitiativePromptSystem).GetField("openInitiativeId", Private)!.SetValue(prompt, token);
        typeof(InitiativePromptSystem).GetMethod("Respond", Private)!.Invoke(prompt, new object[] { 2 });
        Expect(mate.HasPendingInitiative && mate.Profile.GatheringInitiative == CompanionInitiativePolicy.Ask
            && (int)Field(mate, "pendingTargetItem")! == 11,
            "Stale radial click cannot authorize a replacement question in single-player", "Ask and target preserved");
        Main.netMode = NetmodeID.MultiplayerClient; Main.myPlayer = 0;
        Guid replacement = mate.InitiativeId;
        mate.ReceiveInitiativePrompt(CompanionInitiativeKind.Gathering, token);
        Expect(mate.InitiativeId == replacement, "Delayed prompt notification cannot replace synchronized state", "New token preserved");
        Main.netMode = NetmodeID.Server; Main.myPlayer = 255;
        Set(mate, "pendingInitiativeTimer", 0);
        Expect(!mate.RespondToInitiative(replacement, CompanionInitiativeResponse.Always)
            && mate.Profile.GatheringInitiative == CompanionInitiativePolicy.Ask,
            "Expired correct token cannot save Always permission", "Expiry honored");
        Call(mate, "ClearPendingInitiative", 0);
        Main.netMode = NetmodeID.MultiplayerClient; Main.myPlayer = 0;
        mate.ReceiveInitiativePrompt(CompanionInitiativeKind.Gathering, token);
        Expect(!mate.HasPendingInitiative, "Old prompt cannot resurrect completed work", "No fabricated pending task");
        prompt.OnWorldUnload();
    }
}
