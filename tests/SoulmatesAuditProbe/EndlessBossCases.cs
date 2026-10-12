#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Common.Dialogue;
using Soulmates.Content.NPCs;
using Soulmates.Content.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace SoulmatesAuditProbe;

public sealed partial class AuditChecks
{
    private void CheckEndlessBossBoundaries(Mod mod)
    {
        Player oldOwner = Main.player[0], oldOther = Main.player[1]; NPC[] oldNpcs = Main.npc;
        Projectile[] oldShots = Main.projectile;
        try {
            Main.npc = Enumerable.Range(0, Main.maxNPCs).Select(i => new NPC { whoAmI = i }).ToArray();
            Main.projectile = Enumerable.Range(0, Main.maxProjectiles).Select(i => new Projectile { whoAmI = i }).ToArray();
            var owner = new Player { whoAmI = 0, active = true, statLife = 200, statLifeMax2 = 200, Center = new Vector2(500, 500) };
            var other = new Player { whoAmI = 1, active = true, Center = owner.Center };
            Main.player[0] = owner; Main.player[1] = other;
            var mate = CreateCompanion(owner, 180, 0, "Challenge owner");
            var foreign = CreateCompanion(other, 181, 0, "Other owner");
            owner.GetModPlayer<SoulmatesPlayer>().ActiveCompanionWhoAmI = 180;
            other.GetModPlayer<SoulmatesPlayer>().ActiveCompanionWhoAmI = 181;
            mate.Profile.EvilMode = foreign.Profile.EvilMode = true;
            Deliver(mod, BetaQuickPacket(mod, foreign.Profile.Id, CompanionQuickAction.BossChallenge));
            Expect(!foreign.IsBossChallenge && !mate.IsBossChallenge, "Foreign boss command is rejected", "Actual server packet as player 0");
            Deliver(mod, BetaQuickPacket(mod, Guid.NewGuid(), CompanionQuickAction.BossChallenge));
            Expect(!mate.IsBossChallenge, "Stale boss command is rejected", "Unknown Sigil id");
            Main.npc[2].active = Main.npc[2].boss = true;
            Deliver(mod, BetaQuickPacket(mod, mate.Profile.Id, CompanionQuickAction.BossChallenge));
            Expect(!mate.IsBossChallenge, "Active world boss blocks the optional challenge", "No second boss");
            Main.npc[2].active = false;
            Deliver(mod, BetaQuickPacket(mod, mate.Profile.Id, CompanionQuickAction.BossChallenge));
            Expect(mate.IsBossChallenge && mate.NPC.boss && !mate.NPC.friendly, "Valid owner request starts a real boss NPC", "Actual server handler");
            bool otherCompanionThreat = (bool)typeof(SoulboundCompanion).GetMethod("IsThreat", Private | System.Reflection.BindingFlags.Static)!.Invoke(null, new object[] { mate.NPC })!;
            Expect(!otherCompanionThreat, "Other companions do not chase this private challenge", "Production threat predicate");
            var xp = mate.Profile.Experience; int energy = mate.Profile.Energy;
            Expect(!mate.Converse(TalkCategory.Work, 1, 0).Accepted
                && !mate.PerformDirectOrder(CompanionTargetOrder.Mine, new Point(30, 30), -1).Accepted
                && !mate.PerformQuickAction(CompanionQuickAction.Resume).Accepted,
                "Other entry points cannot resume work during a duel", "Talk, pointed order and quick-action gates");
            mate.PerformEmote(CompanionEmote.Heart); mate.ReactToNativeEmote(Terraria.GameContent.UI.EmoteID.RPSRock);
            Expect(mate.Profile.Experience == xp && mate.Profile.Energy == energy && mate.Profile.WorkPaused,
                "Duel emotes cannot farm rewards or alter work state", "Direct and native emote entry points");
            mate.NPC.AddBuff(BuffID.OnFire, 300);
            Deliver(mod, BetaQuickPacket(mod, mate.Profile.Id, CompanionQuickAction.Abort));
            Expect(!mate.IsBossChallenge && mate.NPC.friendly && mate.NPC.dontTakeDamage
                && mate.NPC.buffTime.All(time => time == 0), "Abort restores an undamaged, debuff-free companion", "No lingering burn damage");
            Deliver(mod, BetaQuickPacket(mod, mate.Profile.Id, CompanionQuickAction.BossChallenge));
            Deliver(mod, BetaQuickPacket(mod, mate.Profile.Id, CompanionQuickAction.ToggleEvil));
            Expect(!mate.IsBossChallenge && !mate.Profile.EvilMode, "Disabling EVIL safely ends a running challenge", "Actual owner packets");
            var growth = new CompanionProfile { Experience = CompanionProfile.ExperienceForLevel(BigInteger.Pow(10, 60)) };
            BigInteger start = growth.Experience;
            growth.GainExperience(int.MaxValue, out _);
            Expect(growth.Experience == start + int.MaxValue && growth.Level == BigInteger.Pow(10, 60)
                && growth.ResourceCarryLimit == 1000, "Large growth remains exact and preserves resource balance", "Beyond Int64 and floating-point precision");
            using var wire = new MemoryStream();
            using (var writer = new BinaryWriter(wire, System.Text.Encoding.UTF8, true)) growth.Write(writer);
            byte[] bytes = wire.ToArray(); bytes[^1] = (byte)'!';
            bool rejected = false;
            try { using var reader = new BinaryReader(new MemoryStream(bytes)); CompanionProfile.Read(reader); }
            catch (InvalidDataException) { rejected = true; }
            Expect(rejected, "Malformed XP tail is rejected rather than silently reset", "Corrupted actual wire payload");
        }
        finally {
            Main.player[0] = oldOwner; Main.player[1] = oldOther; Main.npc = oldNpcs; Main.projectile = oldShots;
        }
    }
}
