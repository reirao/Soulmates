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
	private static void CheckWorkBeta(Action<bool, string> check)
	{
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
		Type type = typeof(SoulboundCompanion);
		Player oldPlayer = Main.player[0]; NPC[] oldNpcs = Main.npc; Item[] oldItems = Main.item;
		Chest[] oldChests = Main.chest; Tilemap oldMap = Main.tile;
		int oldWidth = Main.maxTilesX, oldHeight = Main.maxTilesY, oldMode = Main.netMode, oldLocal = Main.myPlayer;
		bool oldDedicated = Main.dedServ;
		RemoteClient[] oldClients = (RemoteClient[])Netplay.Clients.Clone();
		try {
			Main.dedServ = true;
			for (int i = 0; i < Netplay.Clients.Length; i++) Netplay.Clients[i] = new RemoteClient { Socket = new ProbeSocket() };
			foreach (int mode in new[] { NetmodeID.SinglePlayer, NetmodeID.Server }) {
				Main.netMode = mode; Main.myPlayer = 255; Main.maxTilesX = Main.maxTilesY = 120;
				Main.tile = (Tilemap)Activator.CreateInstance(typeof(Tilemap), flags, null, new object[] { (ushort)120, (ushort)120 }, null)!;
				Main.npc = Enumerable.Range(0, oldNpcs.Length).Select(i => new NPC { whoAmI = i }).ToArray();
				Main.item = Enumerable.Range(0, oldItems.Length).Select(i => new Item { whoAmI = i }).ToArray();
				Main.chest = new Chest[oldChests.Length];
				var owner = new Player { whoAmI = 0, active = true, Center = new Point(50, 50).ToWorldCoordinates(), statLife = 100, statLifeMax2 = 100 };
				Main.player[0] = owner;
				NPC npc = Main.npc[20]; npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
				npc.active = true; npc.ai[0] = 0; npc.Center = owner.Center;
				var mate = (SoulboundCompanion)npc.ModNPC;
				mate.Profile.Name = "Work beta fixture"; mate.Profile.QuestionCadence = CompanionQuestionCadence.Quiet;
				mate.Profile.CritterMode = CompanionCritterMode.Off;
				owner.inventory[0] = new Item(ModContent.ItemType<SoulboundSigil>());
				var sigil = (SoulboundSigil)owner.inventory[0].ModItem; sigil.Profile = mate.Profile.Clone();
				object? Call(string method, params object?[] args) => type.GetMethod(method, flags)!.Invoke(mate, args);
				void Set(string field, object? value) => type.GetField(field, flags)!.SetValue(mate, value);
				object? Get(string field) => type.GetField(field, flags)!.GetValue(mate);
				object Activity(string name) => Enum.Parse(type.GetField("autonomyActivity", flags)!.FieldType, name);
				object Forest(string name) => Enum.Parse(type.GetField("gatherForestAction", flags)!.FieldType, name);
				var coordinator = (CompanionActivityCoordinator)type.GetProperty("Activities", flags)!.GetValue(mate)!;
				Set("speechTimer", 20000); Set("socialTimer", 20000); Set("townNpcInteractionCooldown", 20000);
				Set("attackCooldown", int.MaxValue);

				mate.Profile.AutonomyEnabled = true; mate.Profile.Energy = 100; mate.Profile.Mood = 14;
				Set("recoveryTimer", 0); Call("UpdateAutonomyRecovery");
				check((bool)Get("autonomyRecoveryPaused")! && !(bool)Call("CanRunInitiative", CompanionInitiativeKind.Gathering)!,
					"Low mood did not enter automatic recovery or still allowed work: " + mode);
				for (int tick = 0; tick < 719; tick++) { Call("TickFollowActivity"); Call("UpdateAutonomyRecovery"); }
				check(mate.Profile.Mood == 19 && (bool)Get("autonomyRecoveryPaused")!, "Mood recovery resumed before its reserve: " + mode);
				Call("TickFollowActivity"); Call("UpdateAutonomyRecovery");
				check(mate.Profile.Mood == 20 && !(bool)Get("autonomyRecoveryPaused")! && (int)Get("autonomyDecisionTimer")! == 0
					&& sigil.Profile.Mood == 20, "Follow could not recover low mood and become ready again: " + mode);
				Main.netMode = NetmodeID.MultiplayerClient; mate.Profile.Mood = 1; mate.Profile.Energy = 14;
				Call("UpdateAutonomyRecovery"); Call("TickFollowActivity");
				check(mate.Profile.Mood == 1 && mate.Profile.Energy == 14 && !(bool)Get("autonomyRecoveryPaused")!,
					"Client mutated authoritative recovery state: " + mode);
				Main.netMode = mode; mate.Profile.Energy = mate.Profile.Mood = 100; mate.Profile.AutonomyEnabled = false;
				Call("UpdateAutonomyRecovery"); npc.Center = owner.Center;

				NPC dummy = Main.npc[18]; dummy.SetDefaults(NPCID.TargetDummy); dummy.active = true; dummy.Center = owner.Center + new Vector2(24, 0);
				NPC zombie = Main.npc[19]; zombie.SetDefaults(NPCID.Zombie); zombie.active = true; zombie.target = 0;
				zombie.Center = owner.Center + new Vector2(120, 0);
				Set("guardianTarget", -1);
				check(ReferenceEquals(Call("FindDefenseTarget", owner.Center), zombie), "Nearby training dummy hid a real enemy: " + mode);
				Set("guardianTarget", dummy.whoAmI);
				check(ReferenceEquals(Call("FindDefenseTarget", owner.Center), zombie), "Latched training dummy starved a real enemy: " + mode);
				zombie.active = false;
				check(ReferenceEquals(Call("FindDefenseTarget", owner.Center), dummy), "Training target stopped working without real enemies: " + mode);
				dummy.active = false; Set("guardianTarget", -1);

				Item drop = Main.item[10]; drop.SetDefaults(ItemID.Gel); drop.active = true; drop.stack = 3;
				drop.Center = owner.Center + new Vector2(150, 0); drop.playerIndexTheItemIsReservedFor = 255;
				check(mate.PerformDirectOrder(CompanionTargetOrder.Gather, Point.Zero, 10).Accepted, "Work beta could not start directed gathering: " + mode);
				Set("jobTimer", 17); mate.PerformQuickAction(CompanionQuickAction.Pause); zombie.active = true;
				check(coordinator.Tick() == CompanionActivityLane.Defense && (int)Get("jobTimer")! == 17 && drop.stack == 3,
					"Combat advanced paused work or consumed its target: " + mode);
				zombie.active = false;
				check(coordinator.Tick() == CompanionActivityLane.Paused && (int)Get("jobTimer")! == 17 && mate.Profile.WorkPaused,
					"Combat completion released the explicit Pause: " + mode);
				mate.PerformQuickAction(CompanionQuickAction.Resume);
				check(coordinator.Tick() == CompanionActivityLane.Assignment && (int)Get("jobTimer")! == 18 && (int)Get("jobTargetItem")! == 10,
					"Resume lost the preempted directed target: " + mode);
				mate.PerformQuickAction(CompanionQuickAction.Abort);
				check(coordinator.Tick() == CompanionActivityLane.Paused && mate.CurrentJob == CompanionJob.None && drop.active && drop.stack == 3,
					"Abort retained work or consumed untouched cargo: " + mode);
				mate.SetCommand(true);
				check(coordinator.Tick() == CompanionActivityLane.Stay && mate.CurrentJob == CompanionJob.None, "Stay restarted aborted work: " + mode);
				mate.SetCommand(false); drop.active = false;

				int jobs = mate.Profile.JobsCompleted, xp = mate.Profile.Experience;
				foreach (bool forest in new[] { true, false }) {
					mate.StartJob(CompanionJob.Gather); Set("jobTimer", 7200); Set("gatherPause", 45);
					if (forest) { Set("gatherForestAction", Forest("PlantAcorn")); Set("jobTarget", new Point(75, 50)); }
					coordinator.Tick();
					check(mate.CurrentJob == CompanionJob.None && mate.Profile.Routine == CompanionJob.None
						&& mate.Profile.LastMemory == SoulmatesText.Get("Jobs.Gathering.Timeout", 0)
						&& mate.Profile.JobsCompleted == jobs && mate.Profile.Experience == xp,
						"Gathering bypassed its timeout during a forest task or pause: " + forest + "/" + mode);
				}

				Point chestTarget = new(52, 50), replacementChest = new(58, 50);
				Main.chest[0] = new Chest { x = chestTarget.X, y = chestTarget.Y };
				npc.Center = owner.Center - new Vector2(250, 0); mate.StartJob(CompanionJob.FindTreasure);
				Call("UpdateTreasureJob");
				check((bool)Get("hasJobTarget")! && (Point)Get("jobTarget")! == chestTarget, "Treasure fixture did not acquire a chest: " + mode);
				Main.chest[0] = null!; npc.Center = chestTarget.ToWorldCoordinates(16f, -24f); Call("UpdateTreasureJob");
				check(mate.CurrentJob == CompanionJob.None && mate.Profile.JobsCompleted == jobs && mate.Profile.Experience == xp,
					"Removed chest was reported and rewarded as a treasure find: " + mode);
				Main.chest[0] = new Chest { x = chestTarget.X, y = chestTarget.Y };
				Main.chest[1] = new Chest { x = replacementChest.X, y = replacementChest.Y };
				npc.Center = owner.Center - new Vector2(250, 0); mate.StartJob(CompanionJob.FindTreasure); Call("UpdateTreasureJob");
				Main.chest[0] = null!; Call("UpdateTreasureJob");
				check(mate.CurrentJob == CompanionJob.FindTreasure && (Point)Get("jobTarget")! == replacementChest,
					"Removed treasure target did not yield to another chest: " + mode);
				npc.Center = replacementChest.ToWorldCoordinates(16f, -24f); Call("UpdateTreasureJob");
				check(mate.Profile.JobsCompleted == jobs + 1 && mate.Profile.Experience > xp, "Retargeted treasure did not complete once: " + mode);
				jobs = mate.Profile.JobsCompleted; xp = mate.Profile.Experience;
				Call("CompleteJob", "duplicate", true, null, 0);
				check(mate.Profile.JobsCompleted == jobs && mate.Profile.Experience == xp, "Idle completion duplicated a job reward: " + mode);
				mate.StartJob(CompanionJob.Gather); Main.netMode = NetmodeID.MultiplayerClient;
				Call("CompleteJob", "client", true, null, 0);
				check(mate.CurrentJob == CompanionJob.Gather && mate.Profile.JobsCompleted == jobs && mate.Profile.Experience == xp,
					"Client completed and rewarded authoritative work: " + mode);
				Main.netMode = mode; mate.SetCommand(false); Main.chest[1] = null!;

				mate.Profile.AutonomyEnabled = true; mate.Profile.Talent = CompanionTalent.TreasureSeeker;
				var treasureIntent = new CompanionIntent(CompanionInitiativeKind.Treasure, CompanionIntentContext.General);
				int attempts = mate.Profile.IntentLearning.Attempts(treasureIntent);
				Set("proposedIntentContext", treasureIntent.Context); Set("speechTimer", 0); Set("speechText", "");
				Call("BeginAutonomousActivity", Activity("InspectTreasure"), -1, chestTarget, Forest("None"), null);
				npc.Center = chestTarget.ToWorldCoordinates(16f, -24f); Call("UpdateAutonomousActivity");
				check(Get("autonomyActivity")!.ToString() == "None" && mate.Profile.IntentLearning.Attempts(treasureIntent) == attempts + 1
					&& mate.Profile.Experience == xp && (string)Get("speechText")! == "",
					"Automatic treasure announced a removed chest or lost its failed outcome: " + mode);

				mate.SetCommand(false); mate.Profile.Talent = CompanionTalent.Gatherer;
				Point invalidForest = new(77, 77), validForest = new(52, 50);
				Tile farLog = Main.tile[invalidForest.X, invalidForest.Y]; farLog.HasTile = true; farLog.TileType = TileID.FallenLog;
				Tile nearLog = Main.tile[validForest.X, validForest.Y]; nearLog.HasTile = true; nearLog.TileType = TileID.FallenLog;
				npc.Center = new Point(69, 69).ToWorldCoordinates();
				object?[] findForest = { owner.Center, Point.Zero, Forest("None") };
				check((bool)type.GetMethod("FindForestTask", flags)!.Invoke(mate, findForest)! && (Point)findForest[1]! == validForest,
					"Out-of-consent-range forestry target starved an actionable target: " + mode);
				mate.StartJob(CompanionJob.Gather);
				check((bool)type.GetMethod("FindForestTask", flags)!.Invoke(mate, findForest)! && (Point)findForest[1]! == invalidForest,
					"Automatic range filtering incorrectly narrowed explicit gathering: " + mode);
				mate.SetCommand(false); farLog.HasTile = nearLog.HasTile = false;

				mate.Profile.Talent = CompanionTalent.Miner; mate.Profile.MiningInitiative = CompanionInitiativePolicy.Always;
				mate.Profile.Energy = 16; mate.Profile.Mood = 100; Set("workEffort", 6);
				Point ore = new(52, 50), nextOre = new(53, 50);
				Tile firstOre = Main.tile[ore.X, ore.Y]; firstOre.HasTile = true; firstOre.TileType = TileID.Copper;
				Tile secondOre = Main.tile[nextOre.X, nextOre.Y]; secondOre.HasTile = true; secondOre.TileType = TileID.Copper;
				var miningIntent = new CompanionIntent(CompanionInitiativeKind.Mining, CompanionIntentContext.General);
				attempts = mate.Profile.IntentLearning.Attempts(miningIntent); xp = mate.Profile.Experience;
				Set("proposedIntentContext", miningIntent.Context); npc.Center = ore.ToWorldCoordinates();
				Call("BeginAutonomousActivity", Activity("AssistMining"), -1, ore, Forest("None"), null);
				Set("autonomyActionTimer", 31); Call("UpdateAutonomousActivity");
				check(!firstOre.HasTile && secondOre.HasTile && mate.Profile.Energy == 15 && Get("autonomyActivity")!.ToString() == "None"
					&& mate.Profile.IntentLearning.Attempts(miningIntent) == attempts + 1 && mate.Profile.Experience == xp + 1,
					"Low-energy mining discarded or duplicated the completed tile's result: " + mode);
				Call("UpdateHelpfulAutonomy");
				check(mate.Profile.IntentLearning.Attempts(miningIntent) == attempts + 1 && mate.Profile.Experience == xp + 1,
					"Mining recovery settled the same result twice: " + mode);
			}
		}
		finally {
			Main.player[0] = oldPlayer; Main.npc = oldNpcs; Main.item = oldItems; Main.chest = oldChests; Main.tile = oldMap;
			Main.maxTilesX = oldWidth; Main.maxTilesY = oldHeight; Main.netMode = oldMode; Main.myPlayer = oldLocal; Main.dedServ = oldDedicated;
			for (int i = 0; i < oldClients.Length; i++) Netplay.Clients[i] = oldClients[i];
		}
	}
}
