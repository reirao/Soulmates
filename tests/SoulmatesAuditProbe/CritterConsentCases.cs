#nullable enable
using System;
using System.Reflection;
using Soulmates.Common;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace SoulmatesAuditProbe;

public sealed partial class AuditChecks
{
    private void CheckCritterConsentBoundary(Player owner)
    {
        Main.netMode = NetmodeID.Server; Main.myPlayer = 255; Main.dedServ = true;
        SettleTransactions(ModLoader.GetMod("Soulmates"));
        NPC npc = Main.npc[195]; npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
        npc.active = true; npc.ai[0] = owner.whoAmI; npc.Center = owner.Center;
        var mate = (SoulboundCompanion)npc.ModNPC;
        owner.inventory[2] = new Item(ModContent.ItemType<SoulboundSigil>());
        ((SoulboundSigil)owner.inventory[2].ModItem).Profile = mate.Profile.Clone();
        Type activities = typeof(SoulboundCompanion).GetNestedType("AutonomyActivity", BindingFlags.NonPublic)!;
        Type forest = typeof(SoulboundCompanion).GetNestedType("ForestAction", BindingFlags.NonPublic)!;
        object invite = Enum.Parse(activities, "InviteCritter"), catchCritter = Enum.Parse(activities, "CatchCritter");
        NPC target = Main.npc[196]; target.SetDefaults(NPCID.Bunny); target.active = true; target.Center = npc.Center;
        bool Consider(object activity, NPC creature) => (bool)Call(mate, "ConsiderInitiative", activity, -1,
            Microsoft.Xna.Framework.Point.Zero, Enum.Parse(forest, "None"), creature)!;

        mate.Profile.CritterMode = CompanionCritterMode.Company;
        Consider(invite, target); Guid first = mate.InitiativeId;
        Expect(mate.HasPendingInitiative && mate.PendingInitiativeKind == CompanionInitiativeKind.CritterCompany
            && Field(mate, "critterTarget") is null, "Critter company waits for consent", "Company defaults to Ask");
        NPC other = Main.npc[197]; other.SetDefaults(NPCID.Bunny); other.active = true; other.Center = npc.Center;
        Consider(invite, other);
        Expect(mate.InitiativeId == first, "New autonomous opportunity cannot replace an unanswered permission", "Original token retained");
        Main.netMode = NetmodeID.MultiplayerClient;
        Expect(!mate.RespondToInitiative(first, CompanionInitiativeResponse.Always)
            && !mate.PerformQuickAction(CompanionQuickAction.CritterCompanyPolicy).Accepted,
            "Client cannot grant critter consent or edit its policy independently", "Authority remains responsible");
        Main.netMode = NetmodeID.Server;
        NPC replacement = new(); replacement.SetDefaults(NPCID.Bunny); replacement.whoAmI = 196;
        replacement.active = true; replacement.Center = target.Center; Main.npc[196] = replacement;
        Expect(!mate.RespondToInitiative(first, CompanionInitiativeResponse.Always)
            && mate.Profile.CritterCompanyInitiative == CompanionInitiativePolicy.Ask,
            "Reused same-species NPC slot cannot inherit consent", "Reference identity, not only NPC type");

        ((CompanionAttention)Field(mate, "attention")!).Reset();
        Consider(invite, replacement); Guid second = mate.InitiativeId;
        mate.Profile.WorkPaused = true;
        Expect(!mate.RespondToInitiative(second, CompanionInitiativeResponse.Always)
            && !mate.HasPendingInitiative, "Paused work rejects a pending permission answer", "No Company action or saved Always");
        mate.Profile.WorkPaused = false;
        ((CompanionAttention)Field(mate, "attention")!).Reset();
        Consider(invite, replacement);
        Expect(mate.RespondToInitiative(mate.InitiativeId, CompanionInitiativeResponse.Always)
            && ReferenceEquals(Field(mate, "critterTarget"), replacement)
            && mate.Profile.CritterCollectInitiative == CompanionInitiativePolicy.Ask,
            "Always for Company does not authorize Collect", "Two independent ability rules");
        mate.PerformQuickAction(CompanionQuickAction.ResetInitiativeRules);
        Expect(Field(mate, "critterTarget") is null && mate.Profile.CritterCompanyInitiative == CompanionInitiativePolicy.Ask,
            "Reset cancels the approved automatic critter visit", "Ask restored before any creature changes");

        mate.Profile.CritterMode = CompanionCritterMode.Collect;
        mate.Profile.CritterCollectInitiative = CompanionInitiativePolicy.Never;
        Consider(catchCritter, replacement);
        Expect(!mate.HasPendingInitiative && Field(mate, "critterTarget") is null && replacement.active,
            "Never blocks automatic catching even with a built-in net", "No permission bypass");
        mate.Profile.CritterCollectInitiative = CompanionInitiativePolicy.Ask;
        Consider(catchCritter, replacement); Guid catchToken = mate.InitiativeId;
        mate.Profile.CritterMode = CompanionCritterMode.Watch;
        Expect(!mate.RespondToInitiative(catchToken, CompanionInitiativeResponse.Always)
            && mate.Profile.CritterCollectInitiative == CompanionInitiativePolicy.Ask && replacement.active,
            "Mode change invalidates a pending catch permission", "Watch remains non-destructive");
        npc.active = target.active = replacement.active = other.active = false;
    }
}
