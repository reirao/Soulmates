#nullable enable
using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Common.UI;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace SoulmatesRegressionProbe;

public sealed partial class EngineChecks
{
	private static void CheckPromptBeta(Action<bool, string> check)
	{
		foreach (CompanionQuestion question in Enum.GetValues<CompanionQuestion>()) {
			if (question == CompanionQuestion.None) continue;
			string key = $"Conversation.{question}.Heading";
			check(!SoulmatesText.Get(key).StartsWith("Mods.Soulmates.", StringComparison.Ordinal), "Question heading is untranslated: " + question);
			foreach (CompanionAnswer answer in Enum.GetValues<CompanionAnswer>()) {
				key = $"Conversation.{question}.Choices.{answer}";
				check(!SoulmatesText.Get(key).StartsWith("Mods.Soulmates.", StringComparison.Ordinal), "Question answer is untranslated: " + question + "/" + answer);
			}
		}
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
		var prompt = ModContent.GetInstance<InitiativePromptSystem>();
		Player oldPlayer = Main.player[0]; NPC oldNpc = Main.npc[20]; Item oldMouse = Main.mouseItem;
		int oldLocal = Main.myPlayer, oldMode = Main.netMode;
		bool oldDedicated = Main.dedServ, oldMenu = Main.gameMenu, oldInventory = Main.playerInventory;
		try {
			Main.myPlayer = 0; Main.netMode = NetmodeID.SinglePlayer; Main.dedServ = false; Main.gameMenu = false; Main.playerInventory = false;
			var owner = new Player { whoAmI = 0, active = true, Center = new Vector2(500, 500) }; Main.player[0] = owner;
			Main.npc[20] = new NPC { whoAmI = 20 }; NPC npc = Main.npc[20];
			npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>()); npc.active = true; npc.ai[0] = 0; npc.Center = owner.Center;
			var mate = (SoulboundCompanion)npc.ModNPC;
			owner.inventory[0] = new Item(ModContent.ItemType<SoulboundSigil>());
			((SoulboundSigil)owner.inventory[0].ModItem).Profile = mate.Profile.Clone();
			Guid token = Guid.NewGuid();
			typeof(SoulboundCompanion).GetField("pendingQuestion", flags)!.SetValue(mate, CompanionQuestion.Company);
			typeof(SoulboundCompanion).GetField("questionId", flags)!.SetValue(mate, token);
			typeof(SoulboundCompanion).GetField("questionTicks", flags)!.SetValue(mate, 900);
			void Arm() {
				prompt.Close();
				typeof(InitiativePromptSystem).GetField("open", flags)!.SetValue(prompt, true);
				typeof(InitiativePromptSystem).GetField("companion", flags)!.SetValue(prompt, mate);
				typeof(InitiativePromptSystem).GetField("conversation", flags)!.SetValue(prompt, true);
				typeof(InitiativePromptSystem).GetField("openProfileId", flags)!.SetValue(prompt, mate.Profile.Id);
				typeof(InitiativePromptSystem).GetField("openQuestionId", flags)!.SetValue(prompt, token);
			}
			Main.mouseItem = new Item(ItemID.Wood, 9);
			prompt.Close(); prompt.Open(mate);
			check(!prompt.IsOpen && mate.QuestionId == token, "Prompt opened over a cursor-held item");
			Arm(); prompt.UpdateUI(new GameTime());
			check(!prompt.IsOpen && mate.QuestionId == token && Main.mouseItem.stack == 9, "Prompt did not yield to a held item without answering");
			Arm(); typeof(InitiativePromptSystem).GetMethod("Respond", flags)!.Invoke(prompt, new object[] { 0 });
			check(!prompt.IsOpen && mate.QuestionId == token && mate.Profile.GatheringInitiative == CompanionInitiativePolicy.Ask,
				"Protected prompt dispatch granted gathering consent");
			Main.mouseItem = new Item(); Main.playerInventory = true;
			Arm(); typeof(InitiativePromptSystem).GetMethod("Respond", flags)!.Invoke(prompt, new object[] { 0 });
			check(!prompt.IsOpen && mate.QuestionId == token, "Inventory-open prompt dispatch answered anyway");
		}
		finally {
			prompt.Close(); Main.player[0] = oldPlayer; Main.npc[20] = oldNpc; Main.mouseItem = oldMouse;
			Main.myPlayer = oldLocal; Main.netMode = oldMode; Main.dedServ = oldDedicated; Main.gameMenu = oldMenu; Main.playerInventory = oldInventory;
		}
	}
}
