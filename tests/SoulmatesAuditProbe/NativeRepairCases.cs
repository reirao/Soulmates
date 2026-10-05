#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace SoulmatesAuditProbe;

public sealed class ReviewResident : ModNPC
{
    public override string Texture => "Terraria/Images/NPC_22";
    public override void SetDefaults() => NPC.CloneDefaults(NPCID.Guide);
}

public sealed class ReviewConsumableHooks : GlobalItem
{
    public static bool Tracking, KeepItem;
    public static int UseCalls, ConsumeCalls, ConsumedCalls;
    private static bool Applies(Item item) => Tracking && item.type == ItemID.LesserHealingPotion;
    public override bool? UseItem(Item item, Player player) { if (Applies(item)) UseCalls++; return null; }
    public override bool ConsumeItem(Item item, Player player)
    {
        if (!Applies(item)) return true;
        ConsumeCalls++;
        return !KeepItem;
    }
    public override void OnConsumeItem(Item item, Player player) { if (Applies(item)) ConsumedCalls++; }
    public static void Reset(bool keep = false)
    {
        Tracking = true; KeepItem = keep; UseCalls = ConsumeCalls = ConsumedCalls = 0;
    }
}

public sealed partial class AuditChecks
{
    private void CheckNativeRepairs(Mod mod, Player owner, SoulboundCompanion mate)
    {
        CompanionProfile profile = mate.Profile.Clone();
        Item[] inventory = owner.inventory.Select(item => item.Clone()).ToArray();
        Vector2 center = owner.Center;
        WorldFileData world = Main.ActiveWorldFileData;
        StatModifier modifier = owner.PotionDelayModifier;
        try {
            ResetInventoryProtocol(); mate.SetCommand(stay: false);
            Main.ActiveWorldFileData = new WorldFileData("", false) { UniqueId = Guid.NewGuid() };
            Main.netMode = NetmodeID.SinglePlayer; Main.myPlayer = 0;
            Main.combatText = Enumerable.Range(0, 100).Select(_ => new CombatText { active = true }).ToArray();
            owner.PotionDelayModifier = StatModifier.Default; owner.statLifeMax2 = 100;
            foreach (int type in new[] { ItemID.Mushroom, ItemID.RestorationPotion }) {
                Item item = new(type, 2);
                ClearHealing(owner); mate.Profile.ClearCargo(); mate.Profile.Pack.Add(item);
                if (item.potion) typeof(Player).GetMethod("ApplyPotionDelay", Private)!.Invoke(owner, new object[] { item });
                int slot = owner.FindBuffIndex(BuffID.PotionSickness);
                int nativeDelay = slot < 0 ? 0 : owner.buffTime[slot];
                ClearHealing(owner);
                bool used = (bool)Call(mate, "TryUsePackConsumable")!;
                slot = owner.FindBuffIndex(BuffID.PotionSickness);
                int packDelay = slot < 0 ? 0 : owner.buffTime[slot];
                Expect(used && nativeDelay == packDelay && owner.potionDelay == packDelay,
                    "Pack healing retains native item-specific delay for " + type, "native=" + nativeDelay + "; pack=" + packDelay);
            }
            mate.Profile.ClearCargo(); mate.Profile.Pack.Add(new Item(ItemID.LesserHealingPotion, 2));
            ClearHealing(owner); ReviewConsumableHooks.Reset(keep: true);
            bool accepted = (bool)Call(mate, "TryUsePackConsumable")!;
            Expect(accepted && owner.statLife == 70 && mate.Profile.ItemCount(ItemID.LesserHealingPotion) == 2
                && ReviewConsumableHooks.UseCalls == 1 && ReviewConsumableHooks.ConsumeCalls == 1 && ReviewConsumableHooks.ConsumedCalls == 0,
                "A native consume veto permits effects but keeps every carried unit", "life=" + owner.statLife + "; count=" + mate.Profile.ItemCount(ItemID.LesserHealingPotion));
            ClearHealing(owner); ReviewConsumableHooks.Reset();
            accepted = (bool)Call(mate, "TryUsePackConsumable")!;
            Expect(accepted && mate.Profile.ItemCount(ItemID.LesserHealingPotion) == 1
                && ReviewConsumableHooks.UseCalls == 1 && ReviewConsumableHooks.ConsumeCalls == 1 && ReviewConsumableHooks.ConsumedCalls == 1,
                "Successful native consumption runs each hook exactly once", "onConsume=" + ReviewConsumableHooks.ConsumedCalls);
            ClearHealing(owner); owner.GetModPlayer<ReviewUseGate>().BlockPotionDelay = true;
            accepted = (bool)Call(mate, "TryUsePackConsumable")!;
            Expect(accepted && owner.statLife == 70 && owner.potionDelay == 0 && !owner.HasBuff(BuffID.PotionSickness),
                "A native potion-delay veto is not replaced by a forced sickness buff", "delay=" + owner.potionDelay);
            owner.GetModPlayer<ReviewUseGate>().BlockPotionDelay = false;
            ReviewConsumableHooks.Tracking = false;
            mate.Profile.Pack.Add(new Item(ItemID.LesserHealingPotion)); ClearHealing(owner); owner.cursed = true;
            accepted = (bool)Call(mate, "TryUsePackConsumable")!;
            Expect(!accepted && owner.statLife == 20 && mate.Profile.ItemCount(ItemID.LesserHealingPotion) == 1,
                "Cursed owners cannot bypass native consumable-use restrictions", "used=" + accepted);
            owner.cursed = false;
            ClearHealing(owner); mate.Profile.ClearCargo();
            mate.Profile.Pack.Add(new Item(ItemID.GreaterHealingPotion));
            mate.Profile.Pack.Add(new Item(ItemID.LesserHealingPotion));
            owner.GetModPlayer<ReviewUseGate>().BlockItemType = ItemID.GreaterHealingPotion;
            accepted = (bool)Call(mate, "TryUsePackConsumable")!;
            Expect(accepted && owner.statLife == 70 && mate.Profile.ItemCount(ItemID.GreaterHealingPotion) == 1
                && mate.Profile.ItemCount(ItemID.LesserHealingPotion) == 0,
                "A forbidden best potion does not suppress a permitted alternative", "life=" + owner.statLife);
            owner.GetModPlayer<ReviewUseGate>().BlockItemType = 0;

            Main.netMode = NetmodeID.Server; Main.myPlayer = 255;
            owner.Center = new Vector2(480, 480); mate.NPC.Center = owner.Center;
            for (int i = 2; i < 58; i++) owner.inventory[i] = new Item();
            foreach (string scenario in new[] { "accepted", "replaced", "renamed", "recalled", "world" }) {
                ResetInventoryProtocol(); mate.SetCommand(stay: false);
                mate.Profile.Name = "AETHER"; mate.Profile.AutonomyEnabled = true;
                mate.Profile.WorkPaused = false; mate.Profile.Energy = mate.Profile.Mood = 100;
                mate.Profile.Relationships.Clear(); mate.Profile.ClearCargo(); mate.Profile.Resources.Add(new Item(ItemID.Wood, 10));
                Set(mate, "guardianTarget", -1); Set(mate, "townNpcInteractionCooldown", 0);
                Call(mate, "ClearTownNpcInteraction"); mate.SyncProfileToBoundSigil();
                mate.WithdrawStorageSlot(CompanionStorage.Resources, 0, true);
                object pending = PendingInventory(owner);
                Guid transaction = (Guid)pending.GetType().GetProperty("Id")!.GetValue(pending)!;
                var client = new Player { whoAmI = 0, active = true, inventory = owner.inventory.Select(item => item.Clone()).ToArray() };
                var resident = new NPC { whoAmI = 80 };
                resident.SetDefaults(NPCID.Guide); resident.active = true; resident.GivenName = "Receipt Resident";
                resident.dontTakeDamageFromHostiles = false; resident.Center = owner.Center + new Vector2(24, 0);
                Main.npc[80] = resident;
                var bubble = new EmoteBubble(EmoteID.EmoteWink, new WorldUIAnchor(resident), 180);
                if (scenario == "accepted") {
                    var distant = new NPC { whoAmI = 81 };
                    distant.SetDefaults(NPCID.Merchant); distant.active = true;
                    distant.Center = new Vector2(2000, 2000); Main.npc[81] = distant;
                    mate.ObserveResidentEmote(distant, new EmoteBubble(EmoteID.EmoteWink, new WorldUIAnchor(distant), 180));
                    Expect(!(bool)Field(mate, "residentEmoteDeferred")!,
                        "A distant emote cannot occupy the deferred local conversation slot", "out-of-range ignored");
                    distant.active = false;
                }
                mate.ObserveResidentEmote(resident, bubble); mate.ObserveResidentEmote(resident, bubble);
                Expect(mate.Profile.Relationships.Count == 0, "Resident observations stay deferred until " + scenario + " settlement", "frozen profile");
                if (scenario == "replaced") Main.npc[80] = new NPC { whoAmI = 80 };
                if (scenario == "renamed") resident.GivenName = "Someone Else";
                if (scenario == "recalled") mate.NPC.active = false;
                if (scenario == "world") Main.ActiveWorldFileData = new WorldFileData("", false) { UniqueId = Guid.NewGuid() };
                Receipt(mod, owner, client, transaction, accepted: true);
                Expect(scenario == "accepted" ? mate.Profile.Relationships.Count == 1 && mate.Profile.Relationships[0].Meetings == 1
                    : mate.Profile.Relationships.Count == 0, "Deferred resident event validates identity for " + scenario,
                    "relationships=" + mate.Profile.Relationships.Count);
                mate.NPC.active = true; Main.npc[80].active = false;
            }

            CheckResidentKeys();
        }
        finally {
            ReviewConsumableHooks.Tracking = false; ResetInventoryProtocol();
            owner.GetModPlayer<ReviewUseGate>().BlockPotionDelay = false; owner.cursed = false;
            owner.GetModPlayer<ReviewUseGate>().BlockItemType = 0;
            owner.inventory = inventory; owner.Center = center; owner.PotionDelayModifier = modifier;
            ClearHealing(owner); mate.NPC.active = true; mate.SetCommand(stay: false);
            mate.Profile = profile; mate.SyncProfileToBoundSigil(); Main.ActiveWorldFileData = world;
            Main.netMode = NetmodeID.Server; Main.myPlayer = 255;
        }
    }

    private static void ClearHealing(Player owner)
    {
        Array.Clear(owner.buffType); Array.Clear(owner.buffTime); owner.potionDelay = 0; owner.statLife = 20;
    }

    private void CheckResidentKeys()
    {
        Guid world = Guid.NewGuid(); int type = ModContent.NPCType<ReviewResident>();
        string key = ModContent.GetInstance<ReviewResident>().FullName;
        var profile = new CompanionProfile();
        CompanionRelationship relation = profile.MeetResident(world, type, "Stable Resident");
        TagCompound saved = relation.Save();
        Expect(saved.GetString("npcKey") == key && !saved.ContainsKey("type"),
            "Modded residents persist content names, not numeric save identities", key);
        saved["type"] = (int)NPCID.Merchant;
        CompanionRelationship loaded = CompanionRelationship.Load(saved);
        Expect(loaded.NpcType == type && loaded.NpcKey == key,
            "Stable resident keys override obsolete numeric save IDs", "native type=" + loaded.NpcType);
        loaded.NpcType = NPCID.Guide;
        profile.Relationships = new() { loaded }; profile.Normalize();
        Expect(ReferenceEquals(profile.FindRelationship(world, type, "Stable Resident"), loaded)
            && profile.FindRelationship(world, NPCID.Guide, "Stable Resident") is null,
            "Resident lookup never aliases poisoned numeric IDs", "key identity");
        saved["npcKey"] = "MissingResidents/FormerResident";
        loaded = CompanionRelationship.Load(saved); profile.Relationships = new() { loaded }; profile.Normalize();
        Expect(loaded.NpcType == -1 && profile.Relationships.Count == 1 && profile.FindRelationship(world, NPCID.Merchant, "Stable Resident") is null,
            "Missing resident content retains history without current-NPC aliasing", loaded.NpcKey);
        using var stream = new MemoryStream();
        profile.Clone().Write(new BinaryWriter(stream)); stream.Position = 0;
        CompanionProfile received = CompanionProfile.Read(new BinaryReader(stream));
        Expect(received.Relationships.Single().NpcKey == loaded.NpcKey && received.Relationships[0].NpcType == -1,
            "Missing resident identity survives clone and native binary transport", "name retained");
        saved.Remove("npcKey"); saved["type"] = (int)NPCID.Guide;
        loaded = CompanionRelationship.Load(saved);
        Expect(loaded.NpcType == NPCID.Guide && loaded.NpcKey == "Terraria/" + NPCID.Guide,
            "Legacy vanilla relationships migrate without losing resident history", loaded.NpcKey);
        saved["type"] = type; loaded = CompanionRelationship.Load(saved);
        Expect(loaded.NpcType == -1 && loaded.NpcKey == "LegacyNpc/" + type,
            "Unidentified legacy modded relationships are preserved but never guessed", loaded.NpcKey);
    }
}
