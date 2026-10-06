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

namespace SoulmatesAuditProbe;

public sealed partial class AuditChecks
{
    private void CheckWorkAndPetBoundaries(Mod mod, Player owner)
    {
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
        mate.Profile.Store(new Item(ItemID.Nectar));
        mate.Profile.Store(new Item(ItemID.ZephyrFish));
        byte[] PetPacket(int slot, int type, byte[] token) => Packet(mod, "PetConfigRequest", w => {
            w.Write(mate.Profile.Id.ToByteArray()); w.Write(slot); w.Write(type); w.Write(token);
        });
        byte[] stale = PetPacket(0, ItemID.Nectar, StorageToken(mate.Profile, CompanionStorage.Pack, 0));
        mate.Profile.Pack.Reverse();
        Deliver(mod, stale);
        Expect(mate.Profile.PetItemType == 0, "Reordered pack rejects a stale pet selection", "No item consumed or summon authorized");
        int nativeSlot = Projectile.NewProjectile(mate.NPC.GetSource_FromAI(), owner.Center,
            Microsoft.Xna.Framework.Vector2.Zero, ProjectileID.ZephyrFish, 0, 0f, owner.whoAmI);
        Projectile nativePet = Main.projectile[nativeSlot];
        int pets() => Main.projectile.Count(p => p.active && p.ModProjectile is CompanionFamiliar);
        Deliver(mod, PetPacket(0, ItemID.ZephyrFish, StorageToken(mate.Profile, CompanionStorage.Pack, 0)));
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
        Deliver(mod, PetPacket(0, ItemID.ZephyrFish, StorageToken(mate.Profile, CompanionStorage.Pack, 0)));
        mate.Recall();
        var sigil = (SoulboundSigil)owner.inventory[2].ModItem;
        Expect(pets() == 0 && nativePet.active && sigil.Profile.PetItemType == ItemID.ZephyrFish
            && sigil.Profile.ItemCount(ItemID.ZephyrFish) == 1,
            "Recall retires only the companion's familiar and retains the real item", "Player pet survives");
        nativePet.Kill();
    }
}
