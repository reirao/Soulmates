#nullable enable
using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.ID;

namespace SoulmatesAuditProbe;

public sealed partial class AuditChecks
{
    private void CheckCritterSchedulingAndDiagnosis(Player owner)
    {
        Main.netMode = NetmodeID.Server; Main.myPlayer = 255; Main.dedServ = true;
        owner.Center = new Vector2(640, 640); owner.dead = false;
        for (int x = 30; x < 56; x++)
            for (int y = 28; y < 56; y++) Main.tile[x, y].ClearEverything();
        SoulboundCompanion mate = CreateCompanion(owner, 194, 2, "Diagnostic boundary");
        owner.GetModPlayer<SoulmatesPlayer>().ActiveCompanionWhoAmI = 194;
        foreach (var ability in CompanionAbilityRegistry.All) mate.Profile.SetInitiativePolicy(ability.Kind, CompanionInitiativePolicy.Never);
        mate.Profile.CritterMode = CompanionCritterMode.Company;
        mate.Profile.CritterCompanyInitiative = CompanionInitiativePolicy.Always;
        mate.Profile.GatheringInitiative = CompanionInitiativePolicy.Always;
        NPC bunny = Main.npc[176]; bunny.SetDefaults(NPCID.Bunny); bunny.active = true; bunny.Center = mate.NPC.Center + new Vector2(120, 0);
        foreach (Item item in Main.item) item.active = false;
        Item loot = Main.item[90] = new Item(ItemID.CopperOre) { active = true, whoAmI = 90 };
        loot.Center = mate.NPC.Center + new Vector2(80, 0);
        for (int i = 0; i < 12 && Field(mate, "critterTarget") is null; i++) {
            Set(mate, "autonomyDecisionTimer", 0); Call(mate, "UpdateHelpfulAutonomy");
            if (Field(mate, "critterTarget") is null) Call(mate, "CancelAutonomousActivity", 0);
        }
        Expect(ReferenceEquals(Field(mate, "critterTarget"), bunny),
            "Critter Company participates in shared attention under concurrent loot", "Twelve bounded decisions, not forced critter execution");
        mate.SetCommand(false); loot.active = false;
        Set(mate, "critterDecisionTimer", 0); Set(mate, "personalQuestionCooldown", 0);
        Expect((bool)Call(mate, "BeginChoiceQuestion", CompanionQuestion.Company)!, "Personal question fixture opens", "Server authority");
        Set(mate, "autonomyDecisionTimer", 0); Call(mate, "UpdateHelpfulAutonomy");
        Call(mate, "UpdateChoiceConversation");
        Expect(ReferenceEquals(Field(mate, "critterTarget"), bunny) && mate.HasPendingQuestion,
            "Always visit coexists with an answerable personal question", "No permission changes");
        byte[] ProfileBytes() {
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
            mate.Profile.Write(writer); writer.Flush(); return stream.ToArray();
        }
        byte[] before = ProfileBytes();
        string diagnosis = (string)Call(mate, "DiagnosticText")!;
        Expect(before.SequenceEqual(ProfileBytes()) && bunny.active,
            "Reading diagnostics cannot mutate cargo, permissions or world critters", "Binary profile unchanged");
        Expect(diagnosis.Contains("server authority") && diagnosis.Contains("Own familiar count:") && diagnosis.Contains("visiting"),
            "Diagnostic output identifies authority, active visit and pet state", "Actual production state");
        NPC replacement = Main.npc[176] = new NPC { whoAmI = 176 };
        replacement.SetDefaults(NPCID.Bunny); replacement.active = true; replacement.Center = bunny.Center;
        Call(mate, "UpdateCritterActivity");
        Expect(Field(mate, "critterTarget") is null,
            "A replaced NPC object cannot inherit the previous critter visit", "Same slot and type, different identity");
        mate.SetCommand(false); Set(mate, "critterDecisionTimer", 0); Set(mate, "autonomyDecisionTimer", 0);
        mate.Profile.CritterCompanyInitiative = CompanionInitiativePolicy.Never;
        Call(mate, "UpdateHelpfulAutonomy");
        Expect(Field(mate, "critterTarget") is null && !mate.HasPendingInitiative,
            "Never still prevents shared-scheduler critter work", "No new work or question");
        Main.netMode = NetmodeID.MultiplayerClient; Main.myPlayer = 0;
        diagnosis = (string)Call(mate, "DiagnosticText")!;
        Expect(diagnosis.Contains("client view (server owns decisions)"),
            "Client diagnostics do not claim authoritative server history", "Authority is explicitly labeled");
        Main.netMode = NetmodeID.Server; Main.myPlayer = 255;
        mate.Recall(); replacement.active = false;
    }
}
