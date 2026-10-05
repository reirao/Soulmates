#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Common.Feedback;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace SoulmatesAuditProbe;

public sealed partial class AuditChecks
{
    private void CheckDeepReviewBoundaries(Mod mod, Player owner, SoulboundCompanion mate)
    {
        Main.netMode = NetmodeID.Server;
        Main.myPlayer = 255;
        mate.SetCommand(stay: false);
        mate.Profile.Energy = mate.Profile.Mood = 100;
        mate.Profile.ClearCargo();
        mate.Profile.Talent = CompanionTalent.Gatherer;
        mate.Profile.GatheringInitiative = CompanionInitiativePolicy.Always;
        mate.NPC.Center = owner.Center + new Vector2(64, 0);
        Set(mate, "guardianTarget", -1);
        ((CompanionAttention)Field(mate, "attention")!).Reset();
        Item delayed = ReviewDrop(owner, mate, 70, ItemID.Gel, 3);
        delayed.noGrabDelay = 1000;
        object[] searchArgs = { -1, -1 };
        bool found = (bool)typeof(SoulboundCompanion).GetMethod("FindAutonomousLooseItem", Private)!.Invoke(mate, searchArgs)!;
        Expect(!found || (int)searchArgs[0] != 70,
            "Automatic gathering must respect the native pickup delay", "selected=" + searchArgs[0] + "; delay=" + delayed.noGrabDelay);
        Type activity = typeof(SoulboundCompanion).GetNestedType("AutonomyActivity", BindingFlags.NonPublic)!;
        Type forest = typeof(SoulboundCompanion).GetNestedType("ForestAction", BindingFlags.NonPublic)!;
        Call(mate, "ConsiderInitiative", Enum.Parse(activity, "FetchItem"), 70, Point.Zero, Enum.Parse(forest, "None"), null!);
        Call(mate, "UpdateAutonomousFetch");
        Expect(delayed.active && delayed.stack == 3 && mate.Profile.ItemCount(ItemID.Gel) == 0,
            "Automatic execution must not remove a delay-protected drop", "world=" + delayed.stack + "; cargo=" + mate.Profile.ItemCount(ItemID.Gel));

        mate.SetCommand(stay: false);
        Point position = new(33, 30);
        Tile block = Main.tile[position.X, position.Y];
        block.HasTile = true; block.TileType = TileID.Copper;
        byte[] oldMine = ReviewOrderPacket(mod, mate, CompanionTargetOrder.Mine, position, -1, 0, 0);
        block.TileType = TileID.Stone;
        mate.Profile.LearnMiningMaterial(TileID.Stone, 35);
        Deliver(mod, oldMine);
        Expect(mate.CurrentJob == CompanionJob.None,
            "An ore order must reject a replaced terrain target", "activeJob=" + mate.CurrentJob
                + "; selectedTile=" + Field(mate, "directedMiningTileType"));
        mate.SetCommand(stay: false);
        block.HasTile = false;

        Item widePrefix = ReviewDrop(owner, mate, 71, ItemID.CopperShortsword, 1);
        // Native Item.prefix is Int32. This metadata fixture isolates protocol width, not prefix gameplay.
        widePrefix.prefix = 300;
        Deliver(mod, ReviewOrderPacket(mod, mate, CompanionTargetOrder.Gather, Point.Zero, 71, widePrefix.type, widePrefix.prefix));
        Expect(mate.CurrentJob == CompanionJob.Gather,
            "Direct orders must read the full native prefix integer", "prefix=300; activeJob=" + mate.CurrentJob);
        mate.SetCommand(stay: false);
        widePrefix.prefix = 0;
        Deliver(mod, ReviewOrderPacket(mod, mate, CompanionTargetOrder.Gather, Point.Zero, 71, widePrefix.type, widePrefix.prefix));
        Expect(mate.CurrentJob == CompanionJob.Gather, "Zero-prefix direct-order control still works", "Gather started");
        mate.SetCommand(stay: false);
        widePrefix.active = false;

        mate.Profile.Trinket = CompanionTrinket.None;
        bool hasTrinket = owner.inventory.Any(item => item.ModItem is CompanionTrinketItem);
        Expect(!hasTrinket, "Unauthorized-trinket fixture has no trinket", "Owner has none");
        Deliver(mod, Packet(mod, "TrinketRequest", w => {
            w.Write(mate.Profile.Id.ToByteArray()); w.Write((byte)CompanionTrinket.HearthRibbon);
        }));
        Expect(mate.Profile.Trinket == CompanionTrinket.None,
            "Server must not equip a trinket the player does not own", "equipped=" + mate.Profile.Trinket);
        mate.Profile.Trinket = CompanionTrinket.None;

        mate.Profile.GatheringInitiative = CompanionInitiativePolicy.Ask;
        ((CompanionAttention)Field(mate, "attention")!).Reset();
        ReviewDrop(owner, mate, 72, ItemID.Gel, 1);
        ReviewDrop(owner, mate, 73, ItemID.Wood, 1);
        AskGather(mate, 72);
        Guid oldQuestion = mate.InitiativeId;
        byte[] oldGesture = Packet(mod, "NativeEmoteRequest", w => {
            w.Write(mate.Profile.Id.ToByteArray()); w.Write(EmoteID.EmoteWink);
        });
        Call(mate, "ClearPendingInitiative", 0);
        AskGather(mate, 73);
        Expect(mate.HasPendingInitiative && mate.InitiativeId != oldQuestion,
            "Native-gesture replacement fixture has a new question", "New target 73");
        Deliver(mod, oldGesture);
        Expect(mate.HasPendingInitiative && mate.Profile.GatheringInitiative == CompanionInitiativePolicy.Ask,
            "Obsolete native-emote request must not approve a replacement question", "pending=" + mate.HasPendingInitiative
                + "; policy=" + mate.Profile.GatheringInitiative + "; target=" + Field(mate, "autonomyTargetItem"));
        mate.SetCommand(stay: false);

        CheckDeferredCritterQuestion(mate);
        CheckConcurrentInventoryUpdate(owner);
        CheckFailedFeedbackBuffer();
        Main.netMode = NetmodeID.Server;
        Main.myPlayer = 255;
        results.Add("Deep review scope: synthetic boundary/event ordering through production handlers, not a connected multiplayer playthrough.");
    }

    private static Item ReviewDrop(Player owner, SoulboundCompanion mate, int index, int type, int amount)
    {
        Item item = Main.item[index] = new Item(type, amount) {
            active = true, whoAmI = index, playerIndexTheItemIsReservedFor = 255
        };
        item.Center = mate.NPC.Center;
        return item;
    }

    private static byte[] ReviewOrderPacket(Mod mod, SoulboundCompanion mate, CompanionTargetOrder order,
        Point point, int itemIndex, int type, int prefix) => Packet(mod, "DirectOrderRequest", w => {
            w.Write(mate.Profile.Id.ToByteArray()); w.Write((byte)order);
            w.Write((short)point.X); w.Write((short)point.Y); w.Write((short)itemIndex);
            w.Write(type); w.Write(prefix);
            w.Write(WorldGen.InWorld(point.X, point.Y) && Main.tile[point.X, point.Y].HasTile
                ? (int)Main.tile[point.X, point.Y].TileType : -1);
        });

    private void CheckDeferredCritterQuestion(SoulboundCompanion mate)
    {
        mate.Profile.AutonomyEnabled = true;
        mate.Profile.WorkPaused = false;
        mate.Profile.Routine = CompanionJob.None;
        mate.Profile.QuestionCadence = CompanionQuestionCadence.Chatty;
        mate.Profile.Mood = mate.Profile.Energy = 100;
        Set(mate, "guardianTarget", -1);
        Set(mate, "socialNpcTarget", -1);
        Set(mate, "speechTimer", 0);
        Set(mate, "pendingCritterLossKey", "");
        Call(mate, "ClearChoiceQuestion");
        Call(mate, "ReleaseCritterCompany");
        Set(mate, "personalQuestionCooldown", 0);
        Set(mate, "critterCareCooldown", 0);
        Set(mate, "pendingCritterCareTicks", 1800);
        Set(mate, "pendingCritterCareName", "Bunny");
        bool opened = (bool)Call(mate, "BeginChoiceQuestion", CompanionQuestion.CritterCare)!;
        if (!opened) throw new InvalidOperationException("Critter-care question control fixture cannot open.");
        Expect(mate.PendingQuestion == CompanionQuestion.CritterCare,
            "Critter-care control opens without a general conversation cooldown", "Question active");
        Call(mate, "ClearChoiceQuestion");
        Set(mate, "speechTimer", 0);
        Set(mate, "critterCareCooldown", 0);
        Set(mate, "personalQuestionCooldown", 7200);
        Set(mate, "pendingCritterCareTicks", 1800);
        Set(mate, "pendingCritterCareName", "Bunny");
        for (int tick = 0; tick < 1801; tick++) Call(mate, "UpdateChoiceConversation");
        Expect((int)Field(mate, "pendingCritterCareTicks")! > 0 || mate.PendingQuestion == CompanionQuestion.CritterCare,
            "A deferred critter-care question must survive the existing general question cooldown",
            "pendingTicks=" + Field(mate, "pendingCritterCareTicks")
                + "; generalCooldown=" + Field(mate, "personalQuestionCooldown")
                + "; question=" + mate.PendingQuestion);
        for (int tick = 0; tick < 5400; tick++) Call(mate, "UpdateChoiceConversation");
        Expect(mate.PendingQuestion == CompanionQuestion.CritterCare,
            "Deferred care opens an answerable question after the general cooldown expires", "Care question active");
        Call(mate, "ClearChoiceQuestion");
    }

    private void CheckConcurrentInventoryUpdate(Player owner)
    {
        Main.netMode = NetmodeID.MultiplayerClient;
        Main.myPlayer = owner.whoAmI;
        Item original = owner.inventory[3];
        Player originalClientPlayer = Main.clientPlayer;
        Main.clientPlayer = new Player();
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) {
            writer.Write(Guid.NewGuid().ToByteArray()); writer.Write((byte)1); writer.Write((byte)3);
            ItemIO.Send(new Item(), writer, writeStack: true, writeFavorite: true);
            ItemIO.Send(new Item(ItemID.Wood, 9), writer, writeStack: true, writeFavorite: true);
            for (int slot = 0; slot < 58; slot++) writer.Write(new byte[32]);
            writer.Write(false);
        }
        owner.inventory[3] = new Item(ItemID.GoldPickaxe);
        stream.Position = 0;
        using var reader = new BinaryReader(stream);
        Type sync = typeof(CompanionProfile).Assembly.GetType("Soulmates.Common.CompanionInventorySync")!;
        sync.GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { reader });
        Expect(owner.inventory[3].type == ItemID.GoldPickaxe,
            "An unsolicited inventory update must preserve an intervening local slot change or reject the conflict",
            "slot3=" + owner.inventory[3].type + "; expectedPickaxe=" + ItemID.GoldPickaxe);
        owner.inventory[3] = original;
        Main.clientPlayer = originalClientPlayer;
    }

    private void CheckFailedFeedbackBuffer()
    {
        Type feedback = typeof(SoulmatesFeedbackSystem);
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        FieldInfo active = feedback.GetField("sessionActive", flags)!, path = feedback.GetField("sessionPath", flags)!;
        bool originalActive = (bool)active.GetValue(null)!;
        string originalPath = (string)path.GetValue(null)!;
        var lines = (List<string>)feedback.GetField("PendingLines", flags)!.GetValue(null)!;
        string[] originalLines = lines.ToArray();
        string blockedFile = Path.Combine(Main.SavePath, "audit-write-blocked");
        Directory.CreateDirectory(blockedFile);
        active.SetValue(null, true); path.SetValue(null, blockedFile);
        lines.Clear();
        for (int i = 0; i < 128; i++) SoulmatesFeedbackSystem.Record("audit_write_failure", ("amount", i));
        Expect(lines.Count <= 64, "Write failures must not grow the in-memory feedback buffer without a bound",
            "buffered=" + lines.Count + "; ioError=" + !string.IsNullOrEmpty(SoulmatesFeedbackSystem.LastError));
        lines.Clear(); lines.AddRange(originalLines);
        active.SetValue(null, originalActive); path.SetValue(null, originalPath);
    }
}
