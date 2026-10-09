#nullable enable
using System;
using System.IO;
using System.Text;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Common.Dialogue;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace SoulmatesAuditProbe;

public sealed partial class AuditChecks
{
    private void CheckIntentLearningBoundaries(Mod mod, Player owner)
    {
        SettleTransactions(mod);
        Main.netMode = NetmodeID.Server; Main.myPlayer = 255;
        foreach (NPC other in Main.npc) other.active = false;
        foreach (Item item in Main.item) item.TurnToAir();
        owner.dead = false; owner.Center = new Vector2(700, 500);
        owner.controlUseItem = false; owner.itemAnimation = 0; owner.chest = -1; owner.SetTalkNPC(-1);
        SoulboundCompanion mate = CreateCompanion(owner, 198, 8, "Learner");
        owner.GetModPlayer<SoulmatesPlayer>().ActiveCompanionWhoAmI = 198;
        mate.NPC.Center = owner.Center;
        mate.Profile.CritterMode = CompanionCritterMode.Off;
        foreach (var ability in CompanionAbilityRegistry.All) mate.Profile.SetInitiativePolicy(ability.Kind, CompanionInitiativePolicy.Never);
        mate.Profile.GatheringInitiative = CompanionInitiativePolicy.Always;
        owner.inventory[1] = new Item(ItemID.Wood); owner.selectedItem = 1; owner.controlUseItem = true;
        Set(mate, "learningObservationTimer", 0); Call(mate, "UpdateLearningFromOwner");
        owner.controlUseItem = false; owner.itemAnimation = 0;
        Item gel = Main.item[10]; gel.SetDefaults(ItemID.Gel); gel.stack = 3; gel.active = true;
        gel.Center = mate.NPC.Center; gel.playerIndexTheItemIsReservedFor = 255;
        Set(mate, "autonomyDecisionTimer", 0); Set(mate, "nearbyPickupTimer", 0);
        mate.NPC.AI();
        Expect(Field(mate, "autonomyActivity")!.ToString() == "FetchItem", "Native AI selects an existing authorized activity", "No new executor");
        for (int i = 0; i < 4; i++) mate.NPC.AI();
        var experiment = new CompanionIntent(CompanionInitiativeKind.Gathering, CompanionIntentContext.Building);
        Expect(!gel.active && mate.Profile.ItemCount(ItemID.Gel) == 3 && mate.Profile.IntentLearning.Attempts(experiment) == 1,
            "Automatic pickup conserves cargo and records one completed experiment", "Native AI, not a forged outcome");
        Set(mate, "speechTimer", 0);
        mate.NPC.AI();
        Expect(mate.PendingQuestion == CompanionQuestion.Reflection && mate.ReflectionContext == CompanionIntentContext.Building,
            "Native AI offers evaluation for the completed context", "Existing question channel");
        Guid question = mate.QuestionId;
        int mood = mate.Profile.Mood, xp = mate.Profile.Experience;
        byte[] response = Packet(mod, "ChoiceResponseRequest", writer => {
            writer.Write(mate.Profile.Id.ToByteArray()); writer.Write(question.ToByteArray()); writer.Write((byte)CompanionAnswer.Third);
        });
        Deliver(mod, response);
        Expect(!mate.HasPendingQuestion && mate.Profile.IntentLearning.Feedback(experiment) == -8,
            "Authoritative response learns from a real completed action", "One negative contextual evaluation");
        Deliver(mod, response);
        Expect(mate.Profile.IntentLearning.Feedback(experiment) == -8 && mate.Profile.Mood == mood && mate.Profile.Experience == xp,
            "Negative feedback is not punishment, XP farming or a replay", "No repeated reward");
        Expect(mate.Profile.GatheringInitiative == CompanionInitiativePolicy.Always && mate.Profile.MiningInitiative == CompanionInitiativePolicy.Never,
            "Learning never changes explicit permissions", "Preference is not consent");
        CompanionProfile saved = CompanionProfile.Load(mate.Profile.Save());
        Expect(saved.IntentLearning.Feedback(experiment) == -8 && saved.IntentLearning.RecognizesSupply("Terraria/" + ItemID.Wood),
            "Individual learning survives the Sigil save", "Stable item content key");
        Set(mate, "reflectionCooldown", 0);
        Call(mate, "BeginLearningIntent", CompanionInitiativeKind.Treasure, false, CompanionIntentContext.Exploring);
        Call(mate, "CompleteLearningIntent", true);
        Expect(!(bool)Call(mate, "BeginChoiceQuestion", CompanionQuestion.Reflection)!,
            "Reflection respects the shared personal-question interval", "No immediate second popup");
        Set(mate, "personalQuestionCooldown", 0);
        Expect((bool)Call(mate, "BeginChoiceQuestion", CompanionQuestion.Reflection)!, "Deferred reflection can open later", "Context retained");
        question = mate.QuestionId;
        using (var stream = new MemoryStream()) {
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) mate.SendExtraAI(writer);
            stream.Position = 0;
            NPC remoteNpc = new(); remoteNpc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
            remoteNpc.active = true; remoteNpc.ai[0] = owner.whoAmI;
            var remote = (SoulboundCompanion)remoteNpc.ModNPC;
            using var reader = new BinaryReader(stream, Encoding.UTF8, true);
            remote.ReceiveExtraAI(reader);
            Expect(stream.Position == stream.Length && remote.PendingQuestion == CompanionQuestion.Reflection
                && remote.QuestionId == question && remote.ReflectionKind == CompanionInitiativeKind.Treasure
                && remote.ReflectionContext == CompanionIntentContext.Exploring,
                "Question transport preserves exact learning identity and context", "No client-side experiment");
        }
        mate.PerformQuickAction(CompanionQuickAction.Abort);
        Expect(!mate.RespondToQuestion(question, CompanionAnswer.First) && Field(mate, "reflectionIntent") is null,
            "Abort invalidates a learning answer", "No stale-token reward");
        mate.PerformQuickAction(CompanionQuickAction.Resume);
        Call(mate, "BeginLearningIntent", CompanionInitiativeKind.Mining, false, CompanionIntentContext.Building);
        mate.PerformQuickAction(CompanionQuickAction.Pause);
        Expect(Field(mate, "learningIntent") is not null,
            "Pause retains the existing intention without failure learning", "Resume may continue");
        mate.PerformQuickAction(CompanionQuickAction.Abort);
        Expect(Field(mate, "learningIntent") is null && mate.Profile.IntentLearning.Attempts(new(CompanionInitiativeKind.Mining, CompanionIntentContext.Building)) == 0,
            "Abort clears unfinished intention without negative credit", "Player control is not failure");
        mate.PerformQuickAction(CompanionQuickAction.Resume);
        NPC bunny = Main.npc[197]; bunny.SetDefaults(NPCID.Bunny); bunny.active = true; bunny.Center = mate.NPC.Center;
        var catchOrder = (CompanionConversationResult)Call(mate, "PerformNpcContext", CompanionNpcAction.Collect, 197, NPCID.Bunny)!;
        mate.NPC.AI();
        var catchIntent = new CompanionIntent(CompanionInitiativeKind.CritterCollect, CompanionIntentContext.Building);
        Expect(catchOrder.Accepted && !bunny.active && mate.Profile.ItemCount(ItemID.Bunny) == 1
            && mate.Profile.IntentLearning.Attempts(catchIntent) == 1 && Field(mate, "learningIntent") is null,
            "Native critter catch feeds the same learning lifecycle", "One real catch, no phantom ongoing intent");
        bunny.SetDefaults(NPCID.Bunny); bunny.active = true; bunny.Center = mate.NPC.Center;
        var companyOrder = (CompanionConversationResult)Call(mate, "PerformNpcContext", CompanionNpcAction.Company, 197, NPCID.Bunny)!;
        mate.NPC.AI();
        var companyIntent = new CompanionIntent(CompanionInitiativeKind.CritterCompany, CompanionIntentContext.Building);
        Expect(companyOrder.Accepted && bunny.active && mate.Profile.IntentLearning.Attempts(companyIntent) == 1,
            "A real company visit records its own outcome, not catching", "Separate learned abilities");
        mate.PerformQuickAction(CompanionQuickAction.Abort);
        Expect(Field(mate, "reflectionIntent") is null,
            "Abort also clears a completed evaluation that has not opened yet", "No old result after resume");
        mate.NPC.active = false;
    }
}
