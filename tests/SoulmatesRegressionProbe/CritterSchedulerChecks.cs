#nullable enable
using System;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Net;

namespace SoulmatesRegressionProbe;

public sealed partial class EngineChecks
{
	private static void CheckCritterScheduling(Action<bool, string> check)
	{
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		NPC[] oldNpcs = Main.npc; Item[] oldItems = Main.item; Player oldPlayer = Main.player[0];
		int oldMode = Main.netMode, oldLocal = Main.myPlayer;
		bool oldDedicated = Main.dedServ, oldMenu = Main.gameMenu, oldInventory = Main.playerInventory;
		var oldView = Main.GameViewMatrix;
		RemoteClient[] clients = (RemoteClient[])Netplay.Clients.Clone();
		try {
			Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(null!);
			Main.GameViewMatrix.SetViewportOverride(new Microsoft.Xna.Framework.Graphics.Viewport(0, 0, 1280, 720));
			for (int i = 0; i < clients.Length; i++) Netplay.Clients[i] = new RemoteClient { Socket = new ProbeSocket() };
			foreach (int mode in new[] { NetmodeID.SinglePlayer, NetmodeID.Server }) {
				Main.netMode = mode; Main.myPlayer = mode == NetmodeID.Server ? 255 : 0;
				Main.dedServ = mode == NetmodeID.Server; Main.gameMenu = Main.playerInventory = false;
				Main.npc = Enumerable.Range(0, oldNpcs.Length).Select(i => new NPC { whoAmI = i }).ToArray();
				Main.item = Enumerable.Range(0, oldItems.Length).Select(i => new Item { whoAmI = i }).ToArray();
				var owner = new Player { whoAmI = 0, active = true, Center = new Vector2(650, 500) }; Main.player[0] = owner;
				owner.SetTalkNPC(-1);
				NPC npc = Main.npc[20]; npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
				npc.active = true; npc.ai[0] = 0; npc.Center = owner.Center;
				var mate = (SoulboundCompanion)npc.ModNPC;
				owner.inventory[0] = new Item(ModContent.ItemType<SoulboundSigil>());
				((SoulboundSigil)owner.inventory[0].ModItem).Profile = mate.Profile.Clone();
				void Set(string field, object value) => typeof(SoulboundCompanion).GetField(field, flags)!.SetValue(mate, value);
				object? Call(string method, params object[] args) => typeof(SoulboundCompanion).GetMethod(method, flags)!.Invoke(mate, args);
				object? Get(string field) => typeof(SoulboundCompanion).GetField(field, flags)!.GetValue(mate);
				mate.Profile.CritterMode = CompanionCritterMode.Company;
				foreach (var ability in CompanionAbilityRegistry.All) mate.Profile.SetInitiativePolicy(ability.Kind, CompanionInitiativePolicy.Never);
				mate.Profile.CritterCompanyInitiative = CompanionInitiativePolicy.Always;
				mate.Profile.GatheringInitiative = CompanionInitiativePolicy.Always;
				NPC bunny = Main.npc[19]; bunny.SetDefaults(NPCID.Bunny); bunny.active = true; bunny.Center = npc.Center + new Vector2(160, 0);
				Item loot = Main.item[0]; loot.SetDefaults(ItemID.CopperOre); loot.stack = 1; loot.active = true; loot.Center = npc.Center + new Vector2(120, 0);
				for (int decision = 0; decision < 12 && Get("critterTarget") is null; decision++) {
					Set("autonomyDecisionTimer", 0); Call("UpdateHelpfulAutonomy");
					if (Get("critterTarget") is null) Call("CancelAutonomousActivity", 0);
				}
				check(ReferenceEquals(Get("critterTarget"), bunny), "Critter company starved outside the shared opportunity scheduler: " + mode);
				mate.SetCommand(false); Set("personalQuestionCooldown", 0);
				Set("critterDecisionTimer", 0);
				check((bool)Call("BeginChoiceQuestion", CompanionQuestion.Company)!, "Question fixture failed");
				Set("autonomyDecisionTimer", 0); loot.active = false; Call("UpdateHelpfulAutonomy");
				check(ReferenceEquals(Get("critterTarget"), bunny), "Always-approved critter work was blocked by an unrelated personal question: " + mode);
				Call("UpdateChoiceConversation");
				check(mate.HasPendingQuestion && (bool)Call("CanKeepQuestion")!, "Approved critter visit discarded an answerable personal question: " + mode);
				mate.SetCommand(false); Set("personalQuestionCooldown", 0); Set("critterDecisionTimer", 0);
				mate.Profile.CritterCompanyInitiative = CompanionInitiativePolicy.Ask;
				check((bool)Call("BeginChoiceQuestion", CompanionQuestion.Company)!, "Ask-policy question fixture failed");
				Set("autonomyDecisionTimer", 0); Call("UpdateHelpfulAutonomy");
				check(Get("critterTarget") is null && !mate.HasPendingInitiative, "Critter Ask displaced an unanswered personal question: " + mode);
				Call("ClearChoiceQuestion"); Set("autonomyDecisionTimer", 0); Call("UpdateHelpfulAutonomy");
				check(mate.HasPendingInitiative && Get("critterTarget") is null, "Critter Ask started work without approval: " + mode);
				ModContent.GetInstance<Soulmates.Common.UI.InitiativePromptSystem>().Close();
				mate.SetCommand(false); Set("critterDecisionTimer", 0); Set("autonomyDecisionTimer", 0);
				mate.Profile.CritterCompanyInitiative = CompanionInitiativePolicy.Never;
				Call("UpdateHelpfulAutonomy");
				check(!mate.HasPendingInitiative && Get("critterTarget") is null, "Shared scheduler bypassed Critter Never: " + mode);
				mate.Profile.CritterMode = CompanionCritterMode.Collect;
				mate.Profile.CritterCollectInitiative = CompanionInitiativePolicy.Always;
				Set("autonomyDecisionTimer", 0); Call("UpdateHelpfulAutonomy");
				check(ReferenceEquals(Get("critterTarget"), bunny), "Shared scheduler cannot dispatch Collect with the built-in net: " + mode);
			}
		}
		finally {
			Main.npc = oldNpcs; Main.item = oldItems; Main.player[0] = oldPlayer;
			Main.netMode = oldMode; Main.myPlayer = oldLocal;
			Main.dedServ = oldDedicated; Main.gameMenu = oldMenu; Main.playerInventory = oldInventory;
			Main.GameViewMatrix = oldView;
			ModContent.GetInstance<Soulmates.Common.UI.InitiativePromptSystem>().Close();
			for (int i = 0; i < clients.Length; i++) Netplay.Clients[i] = clients[i];
		}
	}
}
