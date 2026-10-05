#nullable enable
using System;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace SoulmatesAuditProbe;

public sealed partial class AuditChecks
{
    private void CheckBetaAbilityBoundaries(Player owner)
    {
        Main.netMode = NetmodeID.Server; Main.myPlayer = 255; Main.dedServ = true;
        SoulboundCompanion mate = CreateCompanion(owner, 195, 2, "Beta boundary");
        byte[] Snapshot() {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            mate.Profile.Write(writer); writer.Flush(); return stream.ToArray();
        }
        byte[] original = Snapshot();
        Main.netMode = NetmodeID.MultiplayerClient;
        bool rejected = true;
        foreach (CompanionQuickAction action in Enum.GetValues<CompanionQuickAction>())
            rejected &= !mate.PerformQuickAction(action).Accepted;
        Expect(rejected && Convert.ToBase64String(Snapshot()) == Convert.ToBase64String(original),
            "Every quick-action API rejects independent client mutation", "Mode, mining, commands and permission edits leave the profile unchanged");
        Main.netMode = NetmodeID.Server;
        Expect(!mate.PerformQuickAction((CompanionQuickAction)255).Accepted
            && !mate.PerformQuickAction(CompanionQuickAction.Details).Accepted
            && !mate.PerformQuickAction(CompanionQuickAction.QuestionSettings).Accepted
            && Convert.ToBase64String(Snapshot()) == Convert.ToBase64String(original),
            "Unknown and UI-only actions cannot fall through into care", "No accidental mood, energy or bond reward");
        Type activities = typeof(SoulboundCompanion).GetNestedType("AutonomyActivity", BindingFlags.NonPublic)!;
        Type forest = typeof(SoulboundCompanion).GetNestedType("ForestAction", BindingFlags.NonPublic)!;
        NPC target = Main.npc[196]; target.SetDefaults(NPCID.Bunny); target.active = true; target.Center = mate.NPC.Center;
        mate.Profile.CritterMode = CompanionCritterMode.Collect;
        void Offer() {
            ((CompanionAttention)Field(mate, "attention")!).Reset();
            Call(mate, "ConsiderInitiative", Enum.Parse(activities, "CatchCritter"), -1, Point.Zero, Enum.Parse(forest, "None"), target);
        }
        Offer(); Guid token = mate.InitiativeId;
        Expect(mate.HasPendingInitiative, "Beta permission boundary has a valid catch offer", "Collect starts at Ask");
        Set(mate, "autonomyRecoveryPaused", true);
        Expect(!mate.RespondToInitiative(token, CompanionInitiativeResponse.Always)
            && mate.Profile.CritterCollectInitiative == CompanionInitiativePolicy.Ask && target.active,
            "Recovery hold invalidates an old Always answer", "The reserve cannot be bypassed by delayed consent");
        Offer();
        Expect(!mate.HasPendingInitiative && Field(mate, "critterTarget") is null,
            "The same recovery gate prevents a fresh offer", "New and answered actions share one gate");
        Set(mate, "autonomyRecoveryPaused", false); Offer(); token = mate.InitiativeId;
        mate.Profile.Routine = CompanionJob.Mine;
        Expect(!mate.RespondToInitiative(token, CompanionInitiativeResponse.Always) && target.active
            && mate.Profile.CritterCollectInitiative == CompanionInitiativePolicy.Ask,
            "A changed explicit routine invalidates old automatic consent", "No catch or persisted Always");
        mate.Profile.Routine = CompanionJob.None; Offer(); token = mate.InitiativeId;
        mate.Profile.CritterCollectInitiative = CompanionInitiativePolicy.Never;
        Expect(!mate.RespondToInitiative(token, CompanionInitiativeResponse.Always) && target.active
            && mate.Profile.CritterCollectInitiative == CompanionInitiativePolicy.Never,
            "A changed Never rule invalidates old automatic consent", "A delayed answer cannot re-enable the ability");
        mate.Profile.CritterCollectInitiative = CompanionInitiativePolicy.Ask; Offer();
        Expect(mate.RespondToInitiative(mate.InitiativeId, CompanionInitiativeResponse.Yes),
            "Removing the hold restores legitimate one-target consent", "Yes leaves the persistent rule at Ask");
        mate.PerformQuickAction(CompanionQuickAction.Pause);
        var coordinator = (CompanionActivityCoordinator)typeof(SoulboundCompanion).GetProperty("Activities", Private)!.GetValue(mate)!;
        Expect(coordinator.Tick() == CompanionActivityLane.Paused && target.active && mate.Profile.ItemCount(ItemID.Bunny) == 0,
            "Pause blocks a previously approved native capture", "No creature or cargo mutation");
        mate.PerformQuickAction(CompanionQuickAction.Resume);
        Expect(coordinator.Tick() == CompanionActivityLane.Critters && !target.active && mate.Profile.ItemCount(ItemID.Bunny) == 1,
            "Resume completes the retained catch in its own frame", "Native catch receipt, exactly one unit");
        Expect(coordinator.Tick() != CompanionActivityLane.Critters && mate.Profile.ItemCount(ItemID.Bunny) == 1,
            "Completed catch cannot replay in the next frame", "No extra creature item");
        mate.Recall();
    }
}
