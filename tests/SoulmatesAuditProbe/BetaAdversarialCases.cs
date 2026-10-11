#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Common.Dialogue;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using BigInteger = System.Numerics.BigInteger;

namespace SoulmatesAuditProbe;

public sealed partial class AuditChecks
{
    // Wire once from PostAddRecipes, before CheckPanelGeometry().
    private void CheckBetaAdversarialCases(Mod mod)
    {
        CheckBetaCargoLedger();
        CheckBetaWalletLedger();

        Player savedOwner = Main.player[0], savedOther = Main.player[1];
        NPC[] savedNpcs = Main.npc;
        Item[] savedItems = Main.item;
        int savedMode = Main.netMode, savedLocal = Main.myPlayer;
        bool savedDedicated = Main.dedServ;
        try {
            ResetInventoryProtocol();
            Main.netMode = NetmodeID.Server; Main.myPlayer = 255; Main.dedServ = true;
            Main.npc = Enumerable.Range(0, savedNpcs.Length).Select(i => new NPC { whoAmI = i }).ToArray();
            Main.item = Enumerable.Range(0, savedItems.Length).Select(i => new Item { whoAmI = i }).ToArray();
            var owner = new Player { whoAmI = 0, active = true, statLife = 100, statLifeMax2 = 100 };
            var other = new Player { whoAmI = 1, active = true, statLife = 100, statLifeMax2 = 100 };
            Main.player[0] = owner; Main.player[1] = other;
            owner.Center = other.Center = new Vector2(480f, 480f);
            SoulboundCompanion mate = CreateCompanion(owner, 180, 0, "Adversarial owner");
            SoulboundCompanion foreign = CreateCompanion(other, 181, 0, "Adversarial other");
            owner.GetModPlayer<SoulmatesPlayer>().ActiveCompanionWhoAmI = 180;
            other.GetModPlayer<SoulmatesPlayer>().ActiveCompanionWhoAmI = 181;
            CheckBetaRequestAuthority(mod, owner, mate, other, foreign);
            CheckBetaGatheringConsent(mod, owner, mate);
        }
        finally {
            ResetInventoryProtocol();
            Main.player[0] = savedOwner; Main.player[1] = savedOther;
            Main.npc = savedNpcs; Main.item = savedItems;
            Main.netMode = savedMode; Main.myPlayer = savedLocal; Main.dedServ = savedDedicated;
        }
        results.Add("Beta adversarial scope: bounded seeded cargo ledger, arbitrary-precision wallet, real server request rejection and gathering consent; no connected clients or graphical playtest.");
    }

    private void CheckBetaCargoLedger()
    {
        int markedType = ModContent.ItemType<ReducedStackResource>();
        int[] types = [ItemID.Wood, ItemID.StoneBlock, markedType, ItemID.LesserHealingPotion];
        Expect(types.Take(3).All(type => CompanionProfile.IsResource(new Item(type)))
            && CompanionProfile.StorageFor(new Item(types[3])) == CompanionStorage.Pack,
            "Beta cargo fixtures exercise native resource and pack classification", "Three resource types and one consumable");

        foreach (int level in new[] { 1, 2, 10, CompanionProfile.MaximumLevel }) {
            var profile = new CompanionProfile { Experience = CompanionProfile.ExperienceForLevel(level) };
            var ledger = types.ToDictionary(type => type, _ => 0L);
            var random = new Random(0x5A17 + level);
            for (int wave = 0; wave < 4; wave++) {
                int[] order = (int[])types.Clone();
                for (int i = order.Length - 1; i > 0; i--) {
                    int j = random.Next(i + 1);
                    (order[i], order[j]) = (order[j], order[i]);
                }
                foreach (int type in order) {
                    int limit = type == ItemID.LesserHealingPotion ? 99 : level * 50;
                    int amount = wave switch { 0 => limit - 1, 1 => 3, 2 => limit + 7, _ => 1 };
                    Item source = BetaResource(type, amount);
                    int expected = (int)Math.Min(amount, Math.Max(0L, limit - ledger[type]));
                    int predicted = profile.GetStorableAmount(source);
                    bool canStore = profile.CanStore(source);
                    int moved = profile.Store(source);
                    ledger[type] += expected;
                    Expect(predicted == expected && canStore == (expected > 0) && moved == expected
                        && (source.IsAir ? 0 : source.stack) == amount - expected
                        && BetaSameUnits(BetaUnits(profile.CarriedItems), ledger)
                        && profile.CarriedItems.All(item => item.stack > 0 && item.stack <= item.maxStack)
                        && profile.Resources.Where(item => item.type == markedType)
                            .All(item => ((ReducedStackResource)item.ModItem).Marker == 71),
                        "Seeded mixed cargo conserves each type at level " + level + ", wave " + wave + ", type " + type,
                        "seed=" + (0x5A17 + level) + "; expected=" + expected + "; moved=" + moved
                            + "; retained=" + profile.ItemCount(type) + "; source=" + source.stack);
                }
                profile = CompanionProfile.Load(profile.Save());
                profile = BetaTransport(profile.Clone());
                profile.Normalize(); profile.Normalize();
                Expect(BetaSameUnits(BetaUnits(profile.CarriedItems), ledger)
                    && profile.ResourceCarryLimit == level * 50
                    && profile.Resources.Where(item => item.type == markedType)
                        .All(item => ((ReducedStackResource)item.ModItem).Marker == 71),
                    "Mixed cargo ledger survives save, clone, native transport and repeated normalization at level " + level,
                    "wave=" + wave + "; exact quantities and mod metadata retained");
            }

            profile.Experience = 0;
            profile.Normalize();
            Expect(BetaSameUnits(BetaUnits(profile.CarriedItems), ledger),
                "Lowering the resource tier retains cargo for explicit reconciliation", "originalLevel=" + level);
            foreach (int type in types) {
                long original = ledger[type];
                int keep = type == ItemID.LesserHealingPotion ? 49 : 25;
                List<Item> returned = profile.ExtractExcess(type, keep);
                ledger[type] = keep;
                Expect(returned.Sum(item => (long)item.stack) == original - keep
                    && returned.All(item => item.type == type && item.stack > 0 && item.stack <= item.maxStack)
                    && returned.Where(item => item.type == markedType)
                        .All(item => ((ReducedStackResource)item.ModItem).Marker == 71)
                    && BetaSameUnits(BetaUnits(profile.CarriedItems), ledger),
                    "Tier reconciliation returns every excess unit of type " + type,
                    "originalLevel=" + level + "; retained=" + keep + "; returned=" + returned.Sum(item => (long)item.stack));
                foreach (Item source in returned) {
                    int limit = type == ItemID.LesserHealingPotion ? 99 : 50;
                    int amount = source.stack;
                    int expected = (int)Math.Min(amount, Math.Max(0L, limit - ledger[type]));
                    int moved = profile.Store(source);
                    ledger[type] += expected;
                    Expect(moved == expected && (source.IsAir ? 0 : source.stack) + moved == amount
                        && BetaSameUnits(BetaUnits(profile.CarriedItems), ledger),
                        "Returned cargo can refill only the current tier without loss", "type=" + type + "; moved=" + moved);
                }
            }
        }

        var full = new CompanionProfile { Experience = CompanionProfile.ExperienceForLevel(20) };
        for (int i = 0; i < full.PackCapacity; i++) full.Pack.Add(new Item(ItemID.CopperShortsword));
        for (int i = 1; i < CompanionProfile.MaximumResourceSlots; i++) full.Resources.Add(new Item(ItemID.StoneBlock));
        full.Resources.Add(BetaResource(markedType, 99));
        Dictionary<int, long> before = BetaUnits(full.CarriedItems);
        Item partial = BetaResource(markedType, 7);
        int capacity = full.GetStorableAmount(partial), transferred = full.Store(partial);
        before[markedType]++;
        Expect(capacity == 1 && transferred == 1 && partial.stack == 6
            && full.ResourceLoad == 60 && full.PackLoad == full.PackCapacity
            && BetaSameUnits(BetaUnits(full.CarriedItems), before),
            "Full cargo permits exactly the native stack headroom", "One marked unit moved, six retained outside cargo");
        foreach (int type in new[] { markedType, ItemID.Wood, ItemID.LesserHealingPotion }) {
            Item blocked = BetaResource(type, 3);
            Expect(full.GetStorableAmount(blocked) == 0 && full.Store(blocked) == 0 && blocked.stack == 3
                && BetaSameUnits(BetaUnits(full.CarriedItems), before),
                "Full per-storage slots cannot consume a rejected source", "type=" + type + "; all three units retained");
        }
        full.Pack.Clear();
        Item medicine = new(ItemID.LesserHealingPotion, 4);
        Expect(full.Store(medicine) == 4 && medicine.IsAir && full.ItemCount(ItemID.LesserHealingPotion) == 4
            && full.ResourceLoad == 60, "Full resources leave the independent pack usable", "Four potions, 60 resource slots");
    }

    private void CheckBetaWalletLedger()
    {
        BigInteger expected = BigInteger.Pow(10, 90) + BigInteger.Pow(2, 80) + 987654;
        CompanionProfile profile = CompanionProfile.Load(new TagCompound {
            ["walletCopper"] = expected.ToString(CultureInfo.InvariantCulture)
        });
        (int Type, int Value)[] coins = [
            (ItemID.CopperCoin, 1), (ItemID.SilverCoin, 100),
            (ItemID.GoldCoin, 10000), (ItemID.PlatinumCoin, 1000000)
        ];
        foreach ((int type, int value) in coins) {
            // An oversized old coin stack also probes multiplication before integer widening.
            Item source = new(type) { stack = int.MaxValue };
            int moved = profile.Store(source);
            expected += (BigInteger)int.MaxValue * value;
            Expect(moved == int.MaxValue && source.IsAir && profile.WalletCopper == expected,
                "Huge wallet adds a complete coin stack without fixed-width overflow", "coin=" + type + "; units=" + moved);
            bool removed = profile.RemoveWalletCoins(type, int.MaxValue - 17);
            expected -= (BigInteger)(int.MaxValue - 17) * value;
            Expect(removed && profile.WalletCopper == expected,
                "Huge wallet subtracts the exact widened denomination cost", "coin=" + type);
            profile = BetaTransport(CompanionProfile.Load(profile.Clone().Save()));
            profile.Normalize(); profile.Normalize();
            BigInteger reconstructed = profile.WalletCoins(ItemID.PlatinumCoin) * 1000000
                + profile.WalletCoins(ItemID.GoldCoin, true) * 10000
                + profile.WalletCoins(ItemID.SilverCoin, true) * 100
                + profile.WalletCoins(ItemID.CopperCoin, true);
            Expect(profile.WalletCopper == expected && reconstructed == expected
                && coins.All(coin => profile.WalletCoins(coin.Type) == expected / coin.Value),
                "Arbitrary-precision wallet and denominations survive every persistence path", "coin=" + type + "; balanceDigits=" + expected.ToString(CultureInfo.InvariantCulture).Length);
        }
        foreach ((int type, int amount) in new[] {
            (ItemID.PlatinumCoin, int.MinValue), (ItemID.CopperCoin, -1),
            (ItemID.GoldCoin, 0), (ItemID.Wood, int.MaxValue)
        }) {
            Expect(!profile.RemoveWalletCoins(type, amount) && profile.WalletCopper == expected,
                "Invalid wallet costs are rejected without minting or debiting money", "type=" + type + "; amount=" + amount);
        }
        CompanionProfile clone = profile.Clone();
        bool debited = clone.RemoveWalletCoins(ItemID.CopperCoin, 1);
        Expect(debited && clone.WalletCopper == expected - 1 && profile.WalletCopper == expected,
            "Cloned huge wallets have independent balances", "Only the clone loses one copper");
        var small = new CompanionProfile();
        small.Store(new Item(ItemID.CopperCoin, 99));
        Expect(!small.RemoveWalletCoins(ItemID.SilverCoin, 1) && small.WalletCopper == 99
            && small.RemoveWalletCoins(ItemID.CopperCoin, 99) && small.WalletCopper.IsZero
            && !small.RemoveWalletCoins(ItemID.CopperCoin, 1),
            "Insufficient, exact and exhausted wallet debits conserve every copper", "99 -> 0 with no overdraft");
    }

    private void CheckBetaRequestAuthority(Mod mod, Player owner, SoulboundCompanion mate, Player other, SoulboundCompanion foreign)
    {
        mate.Profile.Resources.Add(new Item(ItemID.Wood, 10));
        TagCompound funded = mate.Profile.Save();
        funded["walletCopper"] = (BigInteger.Pow(10, 90) + 987654).ToString(CultureInfo.InvariantCulture);
        mate.Profile = CompanionProfile.Load(funded);
        foreach (CompanionInitiativeKind kind in Enum.GetValues<CompanionInitiativeKind>()) {
            mate.Profile.SetInitiativePolicy(kind, CompanionInitiativePolicy.Never);
            foreign.Profile.SetInitiativePolicy(kind, CompanionInitiativePolicy.Never);
        }
        BetaDrop(owner, 190);
        mate.SyncProfileToBoundSigil(); foreign.SyncProfileToBoundSigil();
        Guid oldIdentity = mate.Profile.Id;
        mate.Profile.Id = new Guid("c039873f-c837-4ebc-80ce-56665c72ddf1");
        ((SoulboundSigil)owner.inventory[0].ModItem).Profile = mate.Profile.Clone();

        (string Name, Func<Guid, byte[]> Make)[] requests = [
            ("Pause", id => BetaQuickPacket(mod, id, CompanionQuickAction.Pause)),
            ("ToggleEvil", id => BetaQuickPacket(mod, id, CompanionQuickAction.ToggleEvil)),
            ("Reset permissions", id => BetaQuickPacket(mod, id, CompanionQuickAction.ResetInitiativeRules)),
            ("Gathering policy", id => BetaQuickPacket(mod, id, CompanionQuickAction.GatheringPolicy)),
            ("Stay", id => Packet(mod, "TalkRequest", w => {
                w.Write(id.ToByteArray()); w.Write((byte)TalkCategory.Commands); w.Write((byte)1);
                w.Write(0); w.Write((byte)CompanionItemTopic.All);
            })),
            ("Recall", id => Packet(mod, "RecallRequest", w => w.Write(id.ToByteArray()))),
            ("Mining rule", id => Packet(mod, "MiningRuleRequest", w => {
                w.Write(id.ToByteArray()); w.Write(true); w.Write((int)TileID.Copper); w.Write(false);
            })),
            ("Gather target", id => BetaGatherPacket(mod, id, 190)),
            ("Withdraw", id => Packet(mod, "PackWithdrawRequest", w => {
                w.Write(id.ToByteArray()); w.Write((byte)0); w.Write(true); w.Write((byte)CompanionStorage.Resources);
                w.Write(StorageToken(mate.Profile, CompanionStorage.Resources, 0));
            })),
            ("Huge-wallet withdrawal", id => Packet(mod, "PackWithdrawRequest", w => {
                w.Write(id.ToByteArray()); w.Write((byte)0); w.Write(true); w.Write((byte)CompanionStorage.Wallet);
                w.Write(StorageToken(mate.Profile, CompanionStorage.Wallet, 0));
            }))
        ];
        foreach ((string name, Func<Guid, byte[]> make) in requests) {
            foreach ((string reason, Guid identity, int sender) in new[] {
                ("rebound NPC's old identity", oldIdentity, 0), ("empty identity", Guid.Empty, 0),
                ("other owner", mate.Profile.Id, 1), ("negative sender", mate.Profile.Id, -1),
                ("out-of-range sender", mate.Profile.Id, Main.maxPlayers)
            }) {
                byte[] before = BetaAuthoritySnapshot(owner, mate, other, foreign);
                BetaDeliver(mod, make(identity), sender);
                Expect(before.AsSpan().SequenceEqual(BetaAuthoritySnapshot(owner, mate, other, foreign)),
                    name + " rejects " + reason + " without changing either owner", "Profiles, permissions, inventories and task targets unchanged");
            }
        }
        foreach (string reason in new[] { "dead owner", "inactive owner", "missing bound sigil", "pending inventory" }) {
            Item sigil = owner.inventory[0];
            if (reason == "dead owner") owner.dead = true;
            if (reason == "inactive owner") owner.active = false;
            if (reason == "missing bound sigil") owner.inventory[0] = new Item();
            if (reason == "pending inventory") mate.WithdrawStorageSlot(CompanionStorage.Resources, 0, true);
            if (reason == "pending inventory")
                Expect(PendingInventory(owner) is not null, "Authority rejection fixture has a real pending transaction", "One provisional withdrawal");
            foreach ((string name, Func<Guid, byte[]> make) in requests) {
                byte[] before = BetaAuthoritySnapshot(owner, mate, other, foreign);
                BetaDeliver(mod, make(mate.Profile.Id), 0);
                Expect(before.AsSpan().SequenceEqual(BetaAuthoritySnapshot(owner, mate, other, foreign)),
                    name + " rejects " + reason + " without partial mutation", "Complete state unchanged");
            }
            if (reason == "pending inventory") SettleTransactions(mod);
            owner.dead = false; owner.active = true; owner.inventory[0] = sigil;
        }

        foreach ((string name, Func<Guid, byte[]> make) in requests) {
            byte[] packet = make(mate.Profile.Id);
            byte[] before = BetaAuthoritySnapshot(owner, mate, other, foreign);
            BetaDeliver(mod, packet[..^1], 0);
            Expect(before.AsSpan().SequenceEqual(BetaAuthoritySnapshot(owner, mate, other, foreign)),
                "Truncated " + name + " rejects before mutating state", "One required payload byte missing");
        }
        byte[] forgedProfile = Packet(mod, "ProfileUpdate", w => {
            w.Write("forged server response");
            CompanionProfile forged = mate.Profile.Clone(); forged.ResetInitiativePolicies(); forged.ClearCargo(); forged.Write(w);
        });
        byte[] authorityBefore = BetaAuthoritySnapshot(owner, mate, other, foreign);
        BetaDeliver(mod, forgedProfile, 0);
        Expect(authorityBefore.AsSpan().SequenceEqual(BetaAuthoritySnapshot(owner, mate, other, foreign)),
            "A client cannot inject a server profile response", "Cargo and denied permissions retained");

        CompanionProfile expectedEvil = mate.Profile.Clone();
        expectedEvil.EvilMode = !expectedEvil.EvilMode;
        byte[] onlyEvilChanged = BetaAuthoritySnapshot(owner, mate, other, foreign, expectedEvil);
        BetaDeliver(mod, BetaQuickPacket(mod, mate.Profile.Id, CompanionQuickAction.ToggleEvil), 0);
        Expect(onlyEvilChanged.AsSpan().SequenceEqual(BetaAuthoritySnapshot(owner, mate, other, foreign)),
            "Valid B ToggleEvil changes only its persona and bound sigil", "Cargo, wallet, permissions, assignment and other owner unchanged");
        foreach (int sender in new[] { 0, 1 }) {
            byte[] before = BetaAuthoritySnapshot(owner, mate, other, foreign);
            BetaDeliver(mod, BetaQuickPacket(mod, oldIdentity, CompanionQuickAction.ToggleEvil), sender);
            BetaDeliver(mod, BetaQuickPacket(mod, oldIdentity, CompanionQuickAction.ToggleEvil), sender);
            Expect(before.AsSpan().SequenceEqual(BetaAuthoritySnapshot(owner, mate, other, foreign)),
                "Replayed A ToggleEvil cannot undo B's valid persona change", "sender=" + sender + "; two stale deliveries");
        }
        byte[] beforeForeignToggle = BetaAuthoritySnapshot(owner, mate, other, foreign);
        BetaDeliver(mod, BetaQuickPacket(mod, mate.Profile.Id, CompanionQuickAction.ToggleEvil), 1);
        Expect(beforeForeignToggle.AsSpan().SequenceEqual(BetaAuthoritySnapshot(owner, mate, other, foreign)),
            "Another owner cannot undo B's current persona", "Current identity alone grants no authority");
        expectedEvil = mate.Profile.Clone(); expectedEvil.EvilMode = !expectedEvil.EvilMode;
        onlyEvilChanged = BetaAuthoritySnapshot(owner, mate, other, foreign, expectedEvil);
        BetaDeliver(mod, BetaQuickPacket(mod, mate.Profile.Id, CompanionQuickAction.ToggleEvil), 0);
        Expect(onlyEvilChanged.AsSpan().SequenceEqual(BetaAuthoritySnapshot(owner, mate, other, foreign)),
            "Current owner can turn the persona off without cargo or permission side effects", "Both directions obey the same boundary");

        BigInteger walletBefore = mate.Profile.WalletCopper, ownerMoneyBefore = BetaMoney(owner.inventory);
        byte[] withdrawal = WithdrawPacket(mod, mate, CompanionStorage.Wallet, 0);
        BetaDeliver(mod, withdrawal, 0); SettleTransactions(mod);
        Expect(mate.Profile.WalletCopper == walletBefore - 1000000
            && BetaMoney(owner.inventory) == ownerMoneyBefore + 1000000
            && mate.Profile.WalletCopper + BetaMoney(owner.inventory) == walletBefore + ownerMoneyBefore,
            "Huge-wallet server withdrawal conserves exact copper across owner and companion", "Exactly one platinum transferred");
        byte[] afterWithdrawal = BetaAuthoritySnapshot(owner, mate, other, foreign);
        BetaDeliver(mod, withdrawal, 0);
        Expect(afterWithdrawal.AsSpan().SequenceEqual(BetaAuthoritySnapshot(owner, mate, other, foreign)),
            "A replayed huge-wallet token cannot withdraw a second time", "Changed arbitrary-precision balance invalidates the token");

        foreach ((int energy, int mood) in new[] { (4, 100), (100, 14) }) {
            mate.Profile.Energy = energy; mate.Profile.Mood = mood; mate.SyncProfileToBoundSigil();
            byte[] before = BetaAuthoritySnapshot(owner, mate, other, foreign);
            BetaDeliver(mod, BetaGatherPacket(mod, mate.Profile.Id, 190), 0);
            Expect(before.AsSpan().SequenceEqual(BetaAuthoritySnapshot(owner, mate, other, foreign)),
                "Denied direct gathering cannot start work or change permission", "energy=" + energy + "; mood=" + mood);
        }
        mate.Profile.Energy = mate.Profile.Mood = 100;
        BetaDeliver(mod, BetaQuickPacket(mod, mate.Profile.Id, CompanionQuickAction.Pause), 0);
        Expect(mate.Profile.WorkPaused && !foreign.Profile.WorkPaused,
            "Current owner identity remains usable after rejected commands", "Pause affects only the intended profile");
        BetaDeliver(mod, BetaQuickPacket(mod, mate.Profile.Id, CompanionQuickAction.Resume), 0);
        BetaDeliver(mod, BetaGatherPacket(mod, mate.Profile.Id, 190), 0);
        Expect(!mate.Profile.WorkPaused && mate.CurrentJob == CompanionJob.Gather
            && mate.Profile.GatheringInitiative == CompanionInitiativePolicy.Never,
            "Valid explicit gathering works independently of automatic permission", "Real server target packet accepted; Never retained");
        mate.SetCommand(stay: false);
    }

    private void CheckBetaGatheringConsent(Mod mod, Player owner, SoulboundCompanion mate)
    {
        foreach (string gate in new[] {
            "Never", "autonomy disabled", "paused", "energy", "mood", "dead owner",
            "foreign reservation", "pickup delay", "same-type replacement", "type limit"
        }) {
            mate.SetCommand(stay: false); mate.Profile.ClearCargo();
            mate.Profile.AutonomyEnabled = true; mate.Profile.Energy = mate.Profile.Mood = 100;
            mate.Profile.GatheringInitiative = CompanionInitiativePolicy.Ask;
            owner.dead = false;
            ((CompanionAttention)Field(mate, "attention")!).Reset();
            Item drop = BetaDrop(owner, 190);
            AskGather(mate, 190);
            Guid token = mate.InitiativeId;
            Expect(mate.HasPendingInitiative && token != Guid.Empty,
                "Gathering revocation fixture has a valid live Ask offer", "gate=" + gate);
            switch (gate) {
                case "Never": mate.Profile.GatheringInitiative = CompanionInitiativePolicy.Never; break;
                case "autonomy disabled": mate.Profile.AutonomyEnabled = false; break;
                case "paused": mate.Profile.WorkPaused = true; break;
                case "energy": mate.Profile.Energy = 15; break;
                case "mood": mate.Profile.Mood = 14; break;
                case "dead owner": owner.dead = true; break;
                case "foreign reservation": drop.playerIndexTheItemIsReservedFor = 1; break;
                case "pickup delay": drop.noGrabDelay = 60; break;
                case "same-type replacement": drop = BetaDrop(owner, 190); break;
                case "type limit": mate.Profile.Store(new Item(ItemID.Wood, mate.Profile.ResourceCarryLimit)); break;
            }
            mate.SyncProfileToBoundSigil();
            byte[] profileBefore = BetaProfileBytes(mate.Profile);
            Dictionary<int, long> inventoryBefore = BetaUnits(owner.inventory);
            BetaDeliver(mod, BetaAnswerPacket(mod, mate, token, CompanionInitiativeKind.Gathering, CompanionInitiativeResponse.Always), 0);
            Expect(profileBefore.AsSpan().SequenceEqual(BetaProfileBytes(mate.Profile))
                && BetaSameUnits(BetaUnits(owner.inventory), inventoryBefore)
                && drop.active && drop.type == ItemID.Wood && drop.stack == 4
                && mate.CurrentJob == CompanionJob.None && Field(mate, "autonomyActivity")!.ToString() == "None",
                "Delayed Always cannot bypass the current gathering gate or persist consent", "gate=" + gate);
        }

        owner.dead = false; mate.SetCommand(stay: false); mate.Profile.ClearCargo();
        mate.Profile.AutonomyEnabled = true; mate.Profile.Energy = mate.Profile.Mood = 100;
        mate.Profile.GatheringInitiative = CompanionInitiativePolicy.Ask;
        ((CompanionAttention)Field(mate, "attention")!).Reset();
        BetaDrop(owner, 190); AskGather(mate, 190);
        Guid current = mate.InitiativeId;
        foreach ((Guid token, CompanionInitiativeKind kind, CompanionInitiativeResponse answer) in new[] {
            (Guid.Empty, CompanionInitiativeKind.Gathering, CompanionInitiativeResponse.Always),
            (new Guid("8c51fa63-ff2c-41fd-b8e9-d99c5f771dd2"), CompanionInitiativeKind.Gathering, CompanionInitiativeResponse.Always),
            (current, CompanionInitiativeKind.Mining, CompanionInitiativeResponse.Always),
            (current, CompanionInitiativeKind.Gathering, (CompanionInitiativeResponse)255)
        }) {
            byte[] before = BetaProfileBytes(mate.Profile);
            BetaDeliver(mod, BetaAnswerPacket(mod, mate, token, kind, answer), 0);
            Expect(mate.HasPendingInitiative && mate.InitiativeId == current
                && before.AsSpan().SequenceEqual(BetaProfileBytes(mate.Profile))
                && Field(mate, "autonomyActivity")!.ToString() == "None",
                "Mismatched or invalid consent leaves the current offer answerable", "kind=" + kind + "; answer=" + answer);
        }
        BetaDeliver(mod, BetaAnswerPacket(mod, mate, current, CompanionInitiativeKind.Gathering, CompanionInitiativeResponse.Yes), 0);
        Expect(!mate.HasPendingInitiative && mate.Profile.GatheringInitiative == CompanionInitiativePolicy.Ask
            && (int)Field(mate, "autonomyTargetItem")! == 190
            && Field(mate, "autonomyActivity")!.ToString() == "FetchItem",
            "Current one-target consent starts the exact gathering task", "Yes retains Ask");
        byte[] acceptedProfile = BetaProfileBytes(mate.Profile);
        BetaDeliver(mod, BetaAnswerPacket(mod, mate, current, CompanionInitiativeKind.Gathering, CompanionInitiativeResponse.Always), 0);
        Expect(acceptedProfile.AsSpan().SequenceEqual(BetaProfileBytes(mate.Profile))
            && (int)Field(mate, "autonomyTargetItem")! == 190 && Main.item[190].stack == 4,
            "A replayed answer cannot upgrade one-target consent to Always", "No permission, cargo or target mutation");
        mate.SetCommand(stay: false);
    }

    private static Item BetaResource(int type, int amount)
    {
        var item = new Item(type) { stack = amount };
        if (item.ModItem is ReducedStackResource marked) marked.Marker = 71;
        return item;
    }

    private static Dictionary<int, long> BetaUnits(IEnumerable<Item> items) => items
        .Where(item => !item.IsAir && item.stack > 0).GroupBy(item => item.type)
        .ToDictionary(group => group.Key, group => group.Sum(item => (long)item.stack));

    private static BigInteger BetaMoney(IEnumerable<Item> items) => items.Where(item => !item.IsAir)
        .Aggregate(BigInteger.Zero, (total, item) => total + (BigInteger)item.stack * CompanionProfile.CoinValue(item.type));

    private static bool BetaSameUnits(Dictionary<int, long> actual, Dictionary<int, long> expected)
        => actual.All(pair => expected.TryGetValue(pair.Key, out long amount) && amount == pair.Value)
            && expected.All(pair => actual.GetValueOrDefault(pair.Key) == pair.Value);

    private static byte[] BetaProfileBytes(CompanionProfile profile)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        profile.Write(writer); writer.Flush(); return stream.ToArray();
    }

    private static CompanionProfile BetaTransport(CompanionProfile profile)
    {
        using var stream = new MemoryStream(BetaProfileBytes(profile));
        using var reader = new BinaryReader(stream);
        return CompanionProfile.Read(reader);
    }

    private static Item BetaDrop(Player owner, int index)
    {
        Item item = Main.item[index] = new Item(ItemID.Wood, 4) {
            active = true, whoAmI = index, playerIndexTheItemIsReservedFor = 255
        };
        item.Center = owner.Center + new Vector2(40f, 0f);
        return item;
    }

    private static void BetaDeliver(Mod mod, byte[] packet, int sender)
    {
        // Do not auto-acknowledge: pending-transaction rejection is itself under test.
        using var stream = new MemoryStream(packet);
        using var reader = new BinaryReader(stream);
        mod.HandlePacket(reader, sender);
    }

    private static byte[] BetaQuickPacket(Mod mod, Guid identity, CompanionQuickAction action)
        => Packet(mod, "QuickActionRequest", w => { w.Write(identity.ToByteArray()); w.Write((byte)action); });

    private static byte[] BetaGatherPacket(Mod mod, Guid identity, int target)
        => Packet(mod, "DirectOrderRequest", w => {
            w.Write(identity.ToByteArray()); w.Write((byte)CompanionTargetOrder.Gather);
            w.Write((short)0); w.Write((short)0); w.Write((short)target);
            w.Write(Main.item[target].type); w.Write(Main.item[target].prefix); w.Write(-1);
        });

    private static byte[] BetaAnswerPacket(Mod mod, SoulboundCompanion mate, Guid token,
        CompanionInitiativeKind kind, CompanionInitiativeResponse answer)
        => Packet(mod, "InitiativeResponseRequest", w => {
            w.Write(mate.Profile.Id.ToByteArray()); w.Write(token.ToByteArray());
            w.Write((byte)kind); w.Write((byte)answer);
        });

    private static byte[] BetaAuthoritySnapshot(Player owner, SoulboundCompanion mate, Player other, SoulboundCompanion foreign,
        CompanionProfile? expectedOwnerProfile = null)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        foreach ((Player player, SoulboundCompanion companion) in new[] { (owner, mate), (other, foreign) }) {
            CompanionProfile? replacement = ReferenceEquals(player, owner) ? expectedOwnerProfile : null;
            (replacement ?? companion.Profile).Write(writer);
            foreach (Item item in player.inventory) {
                Item serialized = item;
                if (replacement is not null && item.ModItem is SoulboundSigil sigil && sigil.Profile.Id == replacement.Id) {
                    serialized = item.Clone();
                    ((SoulboundSigil)serialized.ModItem).Profile = replacement.Clone();
                }
                ItemIO.Send(serialized, writer, writeStack: true, writeFavorite: true);
            }
            writer.Write(player.active); writer.Write(player.dead);
            writer.Write(companion.NPC.active); writer.Write(companion.NPC.ai[1]);
            writer.Write(companion.NPC.Center.X); writer.Write(companion.NPC.Center.Y);
            writer.Write((byte)companion.CurrentJob); writer.Write(companion.InitiativeId.ToByteArray());
            writer.Write(companion.HasPendingInitiative);
            foreach (string field in new[] { "autonomyActivity", "autonomyTargetItem", "pendingTargetItem", "jobTargetItem", "directedJob" })
                writer.Write(Field(companion, field)!.ToString()!);
        }
        Item drop = Main.item[190];
        ItemIO.Send(drop, writer, writeStack: true, writeFavorite: true);
        writer.Write(drop.active); writer.Write(drop.playerIndexTheItemIsReservedFor); writer.Write(drop.noGrabDelay);
        writer.Flush(); return stream.ToArray();
    }
}
