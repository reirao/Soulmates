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

namespace SoulmatesAuditProbe;

public sealed class ReviewUseGate : ModPlayer
{
    public bool BlockHealing;
    public bool BlockPotionDelay;
    public int BlockItemType;
    public override bool CanUseItem(Item item) => (!BlockHealing || item.healLife <= 0) && item.type != BlockItemType;
    public override bool ApplyPotionDelay(Item item, int potionDelay) => !BlockPotionDelay;
}

public sealed partial class AuditChecks
{
    private void CheckSecondReviewBoundaries(Mod mod, Player owner, SoulboundCompanion mate)
    {
        Item[] inventory = owner.inventory.Select(item => item.Clone()).ToArray();
        CompanionProfile profile = mate.Profile.Clone();
        Vector2 center = owner.Center;
        double surface = Main.worldSurface;
        StatModifier potionModifier = owner.PotionDelayModifier;
        WorldFileData world = Main.ActiveWorldFileData;
        try {
            ResetInventoryProtocol();
            Main.ActiveWorldFileData = new WorldFileData("", false) { UniqueId = Guid.NewGuid() };
            mate.SetCommand(stay: false);
            mate.Profile.Name = "AETHER";
            mate.Profile.Energy = mate.Profile.Mood = 100;
            mate.Profile.ClearCargo();
            mate.Profile.Resources.Add(new Item(ItemID.Acorn, 2));
            Point planting = new(40, 40);
            owner.Center = planting.ToWorldCoordinates(); mate.NPC.Center = owner.Center;
            for (int x = 36; x <= 44; x++)
                for (int y = 33; y <= 41; y++) Main.tile[x, y].ClearEverything();
            for (int x = 36; x <= 44; x++) {
                Tile ground = Main.tile[x, 41]; ground.HasTile = true; ground.TileType = TileID.Grass;
            }
            Main.netMode = NetmodeID.SinglePlayer; Main.myPlayer = 0;
            bool single = mate.PerformDirectOrder(CompanionTargetOrder.Forest, planting, -1).Accepted;
            Expect(single && Field(mate, "gatherForestAction")!.ToString() == "PlantAcorn",
                "Empty planting-space control is accepted in single-player", "accepted=" + single);
            mate.SetCommand(stay: false);
            Main.netMode = NetmodeID.Server; Main.myPlayer = 255;
            Deliver(mod, ReviewOrderPacket(mod, mate, CompanionTargetOrder.Forest, planting, -1, 0, 0));
            Expect(mate.CurrentJob == CompanionJob.Gather && Field(mate, "gatherForestAction")!.ToString() == "PlantAcorn",
                "Multiplayer accepts the same unchanged empty planting space", "job=" + mate.CurrentJob);
            mate.SetCommand(stay: false);
            Deliver(mod, ReviewOrderPacket(mod, mate, CompanionTargetOrder.Forest, new Point(40, 41), -1, 0, 0));
            Expect(mate.CurrentJob == CompanionJob.Gather && Field(mate, "gatherForestAction")!.ToString() == "PlantAcorn",
                "Occupied grass-ground planting control is accepted in multiplayer", "job=" + mate.CurrentJob);
            mate.SetCommand(stay: false);

            for (int slot = 2; slot < 58; slot++) owner.inventory[slot] = new Item();
            owner.selectedItem = 2; owner.inventory[2] = new Item(ItemID.CopperPickaxe);
            owner.controlUseItem = true; owner.itemAnimation = 1;
            Main.worldSurface = 20;
            Point dungeon = new(20, 60);
            owner.Center = dungeon.ToWorldCoordinates(); mate.NPC.Center = owner.Center;
            for (int x = 18; x <= 23; x++)
                for (int y = 57; y <= 63; y++) Main.tile[x, y].ClearEverything();
            Tile block = Main.tile[dungeon.X, dungeon.Y]; block.HasTile = true; block.TileType = TileID.BlueDungeonBrick;
            mate.Profile.ObservedPickPower = 35; mate.Profile.LearnedMiningTiles.Clear();
            MethodInfo nativePick = typeof(Player).GetMethod("GetPickaxeDamage", Private | BindingFlags.Public)!;
            int nativeDamage = (int)nativePick.Invoke(owner, new object[] { dungeon.X, dungeon.Y, 35, 0, block })!;
            Expect(nativeDamage == 0 && Main.tileDungeon[block.TileType],
                "Native dungeon progression control rejects a copper pickaxe", "native damage=" + nativeDamage);
            bool learned = (bool)typeof(SoulboundCompanion).GetMethod("ObserveMiningTarget", Private)!
                .Invoke(mate, new object[] { dungeon, owner.HeldItem.type })!;
            bool eligible = (bool)typeof(SoulboundCompanion).GetMethod("CanTargetMining", Private)!
                .Invoke(mate, new object[] { dungeon })!;
            Expect(!learned && !eligible,
                "Companion mining preserves native dungeon pickaxe progression", "learned=" + learned + "; eligible=" + eligible);
            owner.inventory[2] = new Item(ItemID.MythrilPickaxe);
            nativeDamage = (int)nativePick.Invoke(owner, new object[] { dungeon.X, dungeon.Y, owner.HeldItem.pick, 0, block })!;
            learned = (bool)typeof(SoulboundCompanion).GetMethod("ObserveMiningTarget", Private)!
                .Invoke(mate, new object[] { dungeon, owner.HeldItem.type })!;
            Expect(nativeDamage > 0 && learned && (bool)Call(mate, "CanTargetMining", dungeon)!,
                "Dungeon mining remains available with native sufficient progression", "pick=" + owner.HeldItem.pick);
            Main.worldSurface = 70; owner.inventory[2] = new Item(ItemID.CopperPickaxe); mate.Profile.ObservedPickPower = 35;
            nativeDamage = (int)nativePick.Invoke(owner, new object[] { dungeon.X, dungeon.Y, 35, 0, block })!;
            Expect(nativeDamage > 0 && (bool)Call(mate, "CanTargetMining", dungeon)!,
                "Native surface dungeon exemptions are not replaced by a blanket prohibition", "native=" + nativeDamage);
            Main.worldSurface = 20;
            block.TileType = TileID.Obsidian; owner.inventory[2] = new Item(ItemID.GoldPickaxe);
            nativeDamage = (int)nativePick.Invoke(owner, new object[] { dungeon.X, dungeon.Y, 55, 0, block })!;
            Expect(nativeDamage > 0, "Native obsidian control accepts a gold pickaxe", "native damage=" + nativeDamage);
            learned = (bool)typeof(SoulboundCompanion).GetMethod("ObserveMiningTarget", Private)!
                .Invoke(mate, new object[] { dungeon, owner.HeldItem.type })!;
            Expect(learned && mate.Profile.KnowsMiningMaterial(TileID.Obsidian),
                "Companion learns obsidian with the native sufficient gold pickaxe", "learned=" + learned);

            Main.netMode = NetmodeID.SinglePlayer; Main.myPlayer = 0;
            mate.Profile.ClearCargo(); mate.Profile.Pack.Add(new Item(ItemID.LesserHealingPotion, 2));
            // Occupied visual slots avoid needing a client font in this headless native fixture.
            Main.combatText = Enumerable.Range(0, 100).Select(_ => new CombatText { active = true }).ToArray();
            owner.statLife = 20; owner.statLifeMax2 = 100; owner.PotionDelayModifier = StatModifier.Default * 0.75f;
            Array.Clear(owner.buffType); Array.Clear(owner.buffTime);
            typeof(Player).GetMethod("ApplyPotionDelay", Private | BindingFlags.Public)!
                .Invoke(owner, new object[] { new Item(ItemID.LesserHealingPotion) });
            int nativeDelay = owner.buffTime[owner.FindBuffIndex(BuffID.PotionSickness)];
            Expect(nativeDelay == 2700, "Native healing delay respects the player's potion modifier", "ticks=" + nativeDelay);
            Array.Clear(owner.buffType); Array.Clear(owner.buffTime); owner.potionDelay = 0;
            bool used = (bool)Call(mate, "TryUsePackConsumable")!;
            int companionDelay = owner.buffTime[owner.FindBuffIndex(BuffID.PotionSickness)];
            Expect(used && companionDelay == nativeDelay,
                "Pack healing respects the same native potion delay", "used=" + used + "; native=" + nativeDelay + "; pack=" + companionDelay);
            Array.Clear(owner.buffType); Array.Clear(owner.buffTime); owner.potionDelay = 0; owner.statLife = 20;
            owner.GetModPlayer<ReviewUseGate>().BlockHealing = true;
            bool allowed = CombinedHooks.CanUseItem(owner, mate.Profile.Pack[0]);
            Expect(!allowed, "Native ModPlayer healing-use gate is active", "allowed=" + allowed);
            int amount = mate.Profile.Pack[0].stack;
            used = (bool)Call(mate, "TryUsePackConsumable")!;
            Expect(!used && mate.Profile.ItemCount(ItemID.LesserHealingPotion) == amount && owner.statLife == 20,
                "Pack consumables respect native ModPlayer use restrictions", "used=" + used + "; life=" + owner.statLife);
            owner.GetModPlayer<ReviewUseGate>().BlockHealing = false;

            Main.netMode = NetmodeID.Server; Main.myPlayer = 255;
            mate.SetCommand(stay: false); mate.Profile.ClearCargo(); mate.Profile.Resources.Add(new Item(ItemID.Wood, 10));
            mate.Profile.AutonomyEnabled = true; mate.Profile.WorkPaused = false;
            mate.Profile.Energy = mate.Profile.Mood = 100; mate.Profile.Relationships.Clear();
            Set(mate, "guardianTarget", -1); Set(mate, "townNpcInteractionCooldown", 0);
            Call(mate, "ClearTownNpcInteraction");
            mate.SyncProfileToBoundSigil();
            var client = new Player { whoAmI = 0, active = true, inventory = owner.inventory.Select(item => item.Clone()).ToArray() };
            mate.WithdrawStorageSlot(CompanionStorage.Resources, 0, true);
            object pending = PendingInventory(owner);
            Guid transaction = (Guid)pending.GetType().GetProperty("Id")!.GetValue(pending)!;
            var resident = new NPC { whoAmI = 80 };
            resident.SetDefaults(NPCID.Guide); resident.active = true; resident.GivenName = "Review Resident";
            resident.dontTakeDamageFromHostiles = false; resident.Center = mate.NPC.Center + new Vector2(24, 0);
            Main.npc[80] = resident;
            var bubble = new EmoteBubble(EmoteID.EmoteWink, new WorldUIAnchor(resident), 180);
            mate.ObserveResidentEmote(resident, bubble);
            Expect(mate.Profile.Relationships.Count == 0,
                "Resident-emote events do not mutate a pending inventory before-image", "relationships=" + mate.Profile.Relationships.Count);
            Receipt(mod, owner, client, transaction, accepted: false);
            Expect(mate.Profile.Relationships.Any(relation => relation.Resident == "Review Resident")
                && mate.Profile.ItemCount(ItemID.Wood) == 10,
                "Legitimate resident history survives a rejected cargo transaction", "relationships=" + mate.Profile.Relationships.Count
                    + "; Wood=" + mate.Profile.ItemCount(ItemID.Wood));
            Main.npc[80].active = false;
        }
        finally {
            ResetInventoryProtocol(); Main.player[0] = owner; Main.netMode = NetmodeID.Server; Main.myPlayer = 255;
            owner.GetModPlayer<ReviewUseGate>().BlockHealing = false;
            owner.inventory = inventory; owner.Center = center; owner.PotionDelayModifier = potionModifier;
            mate.SetCommand(stay: false); mate.Profile = profile; mate.SyncProfileToBoundSigil();
            Main.worldSurface = surface; Main.ActiveWorldFileData = world;
        }
        results.Add("Second review counterchecks: exact loaded local package, real native rules and synthetic production packet/event ordering.");
    }
}
