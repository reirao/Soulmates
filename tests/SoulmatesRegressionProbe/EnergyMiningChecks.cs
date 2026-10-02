using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Common.Dialogue;
using Soulmates.Common.UI;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Net;

namespace SoulmatesRegressionProbe;

public sealed partial class EngineChecks
{
	private static void CheckEnergyAndMiningRules(Action<bool, string> check, Mod mod)
	{
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
		Player oldPlayer = Main.player[0]; NPC[] oldNpcs = Main.npc; Item[] oldItems = Main.item;
		Tilemap oldMap = Main.tile;
		int oldWidth = Main.maxTilesX, oldHeight = Main.maxTilesY, oldMode = Main.netMode, oldLocal = Main.myPlayer;
		RemoteClient[] oldClients = (RemoteClient[])Netplay.Clients.Clone();
		try {
			for (int i = 0; i < Netplay.Clients.Length; i++) Netplay.Clients[i] = new RemoteClient { Socket = new ProbeSocket() };
			foreach (int mode in new[] { NetmodeID.SinglePlayer, NetmodeID.Server }) {
				Main.netMode = mode; Main.myPlayer = mode == NetmodeID.Server ? 255 : 0;
				Main.maxTilesX = Main.maxTilesY = 100;
				Main.tile = (Tilemap)Activator.CreateInstance(typeof(Tilemap), flags, null, new object[] { (ushort)100, (ushort)100 }, null)!;
				Main.npc = Enumerable.Range(0, oldNpcs.Length).Select(i => new NPC { whoAmI = i }).ToArray();
				Main.item = Enumerable.Range(0, oldItems.Length).Select(i => new Item { whoAmI = i }).ToArray();
				var owner = new Player { whoAmI = 0, active = true, name = "Energy QA", Center = new Vector2(400, 400), statLife = 100, statLifeMax2 = 100 };
				Main.player[0] = owner;
				NPC npc = Main.npc[20]; npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
				npc.active = true; npc.ai[0] = 0; npc.Center = owner.Center;
				var mate = (SoulboundCompanion)npc.ModNPC;
				mate.Profile.Talent = CompanionTalent.Miner;
				owner.inventory[0] = new Item(ModContent.ItemType<SoulboundSigil>());
				owner.inventory[1] = new Item(ItemID.CopperPickaxe);
				((SoulboundSigil)owner.inventory[0].ModItem).Profile = mate.Profile.Clone();
				void Set(string field, object value) => typeof(SoulboundCompanion).GetField(field, flags)!.SetValue(mate, value);
				object Get(string field) => typeof(SoulboundCompanion).GetField(field, flags)!.GetValue(mate)!;
				object? Call(string method, params object[] args) => typeof(SoulboundCompanion).GetMethod(method, flags)!.Invoke(mate, args);
				void Tick(int count) { for (int i = 0; i < count; i++) { npc.AI(); npc.position += npc.velocity; } }
				Set("speechTimer", 20000); Set("socialTimer", 20000); Set("ambientStoryCooldown", 20000);
				Set("companyQuestionCooldown", 20000); Set("walletQuestionCooldown", 20000);
				mate.Profile.AutonomyEnabled = false; mate.Profile.Energy = 50;
				Tick(1800);
				check(mate.Profile.Energy == 80, "Full AI follow recovery is not one energy per second: " + mode);
				Set("workEffort", 0); mate.Profile.Energy = 100;
				for (int i = 0; i < 7; i++) Call("SpendWorkEnergy", 1);
				check(mate.Profile.Energy == 100, "Seven small pickups spent a full energy point");
				Call("SpendWorkEnergy", 1); check(mate.Profile.Energy == 99, "Eighth pickup did not settle its energy cost");
				Call("SpendWorkEnergy", -1); check(mate.Profile.Energy == 99, "Negative work effort created energy");
				Main.netMode = NetmodeID.MultiplayerClient; Call("SpendWorkEnergy", 8);
				check(mate.Profile.Energy == 99 && (int)Get("workEffort") == 0, "Client spent authoritative work energy");
				Main.netMode = mode;
				mate.Profile.Energy = 60; Set("workEffort", 0);
				var oreTiles = new List<Point>();
				for (int x = 20; x < 44; x++) for (int y = 29; y < 33; y++) {
					Tile tile = Main.tile[x, y]; tile.HasTile = true; tile.TileType = TileID.Copper; oreTiles.Add(new Point(x, y));
				}
				mate.StartJob(CompanionJob.Mine);
				for (int i = 0; i < 6000 && mate.CurrentJob != CompanionJob.None; i++) Tick(1);
				check(oreTiles.All(p => !Main.tile[p.X, p.Y].HasTile) && mate.Profile.JobsCompleted > 0,
					"Full AI did not finish a 96-block ore job");
				check(mate.Profile.Energy >= 45 && !(bool)Get("jobRecoveryPaused"), "Working recovery was lost or 96 ore blocks exhausted the companion");
				Point ground = new(28, 25); Tile soil = Main.tile[ground.X, ground.Y]; soil.HasTile = true; soil.TileType = TileID.Dirt;
				mate.Profile.LearnMiningMaterial(TileID.Dirt, 35);
				check((bool)Call("CanMineTile", ground.X, ground.Y, true)!, "Plain observed dirt was unexpectedly unmineable");
				foreach (ushort decoration in new ushort[] { TileID.Saplings, TileID.Plants, TileID.SmallPiles, TileID.Containers, TileID.Torches }) {
					Tile above = Main.tile[ground.X, ground.Y - 1]; above.HasTile = true; above.TileType = decoration;
					check(!(bool)Call("CanMineTile", ground.X, ground.Y, true)!, "Mining would uproot decoration " + decoration);
					check(!mate.PerformDirectOrder(CompanionTargetOrder.Mine, ground, -1).Accepted && above.HasTile && soil.HasTile,
						"Direct order bypassed decoration protection " + decoration);
					above.HasTile = false;
				}
				check(mate.PerformDirectOrder(CompanionTargetOrder.Mine, ground, -1).Accepted, "Plain directed dirt target rejected");
				Tile latePlant = Main.tile[ground.X, ground.Y - 1]; latePlant.HasTile = true; latePlant.TileType = TileID.Saplings;
				Tick(60); check(soil.HasTile && latePlant.HasTile, "Preplanned mining ignored a newly placed sapling");
				latePlant.HasTile = false;
				mate.SetCommand(stay: false);
				check(mate.Profile.AllowsAutomaticMining(TileID.Copper) && !mate.Profile.AllowsAutomaticMining(TileID.Dirt),
					"Default rules enabled observed terrain or blocked ores");
				check(mate.SetAutomaticMiningRule(true, TileID.Copper, false) && !mate.Profile.AllowsAutomaticMining(TileID.Copper),
					"Individual ore exclusion failed");
				check(mate.SetAutomaticMiningRule(false, TileID.Dirt, true) && mate.Profile.AllowsAutomaticMining(TileID.Dirt),
					"Explicit observed-block opt-in failed");
				check((bool)Call("CanAutomaticallyMineTile", 28, 25)!, "Permitted observed dirt not usable automatically");
				check(!mate.SetAutomaticMiningRule(false, TileID.LihzahrdBrick, true), "Unobserved material gained automatic permission");
				mate.Profile.MiningApproach = CompanionMiningApproach.Vein;
				check(!(bool)Call("CanAutomaticallyMineTile", 28, 25)!, "Vein mode ignored its ore-only restriction");
				mate.Profile.MiningApproach = CompanionMiningApproach.Adaptive;
				if (mode == NetmodeID.Server) {
					Type messageType = mod.GetType().GetNestedType("MessageType", BindingFlags.NonPublic)!;
					byte requestType = Convert.ToByte(Enum.Parse(messageType, "MiningRuleRequest"));
					void Request(Guid id, bool ores, int type, bool enabled) {
						using var stream = new MemoryStream();
						using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) {
							writer.Write(requestType); writer.Write(id.ToByteArray()); writer.Write(ores); writer.Write(type); writer.Write(enabled);
						}
						stream.Position = 0; using var reader = new BinaryReader(stream); mod.HandlePacket(reader, 0);
					}
					Request(Guid.NewGuid(), true, TileID.Copper, true);
					check(!mate.Profile.AllowsAutomaticMining(TileID.Copper), "Wrong-companion packet changed mining rules");
					Request(mate.Profile.Id, true, TileID.Copper, true);
					check(mate.Profile.AllowsAutomaticMining(TileID.Copper), "Valid mining-rule packet did not reach authority");
					Request(mate.Profile.Id, true, TileID.Copper, false);
					Request(mate.Profile.Id, false, TileID.LihzahrdBrick, true);
					check(!mate.Profile.AllowsAutomaticMining(TileID.LihzahrdBrick), "Packet enabled an unobserved protected tile");
				}
				CompanionProfile clone = mate.Profile.Clone(), loaded = CompanionProfile.Load(mate.Profile.Save());
				foreach (CompanionProfile p in new[] { clone, loaded }) check(!p.AllowsAutomaticMining(TileID.Copper) && p.AllowsAutomaticMining(TileID.Dirt),
					"Mining rules lost during clone/save/load");
				clone.BlockedAutoMiningTiles.Clear(); check(!mate.Profile.AllowsAutomaticMining(TileID.Copper), "Cloned rules share mutable storage");
				var legacyTag = mate.Profile.Save();
				legacyTag.Remove("blockedAutoMiningTiles"); legacyTag.Remove("allowedAutoMiningMaterials");
				CompanionProfile legacy = CompanionProfile.Load(legacyTag);
				check(legacy.Id == mate.Profile.Id && legacy.AllowsAutomaticMining(TileID.Copper)
					&& !legacy.AllowsAutomaticMining(TileID.Dirt), "Existing Sigil without mining rules did not load safe defaults");
				clone.BlockedAutoMiningTiles.Add("AbsentMod/PreciousOre");
				check(CompanionProfile.Load(clone.Save()).BlockedAutoMiningTiles.Contains("AbsentMod/PreciousOre"),
					"Temporarily absent mod content lost its persistent exclusion");
				using (var stream = new MemoryStream()) {
					using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) mate.SendExtraAI(writer);
					stream.Position = 0; using var reader = new BinaryReader(stream);
					var copy = new SoulboundCompanion(); copy.ReceiveExtraAI(reader);
					check(stream.Position == stream.Length && !copy.Profile.AllowsAutomaticMining(TileID.Copper)
						&& copy.Profile.AllowsAutomaticMining(TileID.Dirt), "Mining rule network roundtrip failed");
				}
				mate.Profile.AutonomyEnabled = true; mate.Profile.Energy = 15; Call("UpdateAttentionClock");
				check((bool)Get("autonomyRecoveryPaused"), "Autonomy did not enter low-energy recovery");
				using (var stream = new MemoryStream()) {
					using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) mate.SendExtraAI(writer);
					stream.Position = 0; using var reader = new BinaryReader(stream);
					var copy = new SoulboundCompanion(); copy.ReceiveExtraAI(reader);
					check((bool)typeof(SoulboundCompanion).GetField("autonomyRecoveryPaused", flags)!.GetValue(copy)!
						&& stream.Position == stream.Length, "Active recovery status was not synchronized");
				}
				mate.Profile.Energy = 29; Call("UpdateAttentionClock");
				check((bool)Get("autonomyRecoveryPaused") && !(bool)Call("UpdateHelpfulAutonomy")!, "Autonomy restarted without its recovery reserve");
				mate.Profile.Energy = 30; Call("UpdateAttentionClock");
				check(!(bool)Get("autonomyRecoveryPaused") && (int)Get("autonomyDecisionTimer") == 0, "Recovered autonomy did not become ready promptly");
				mate.Profile.MiningInitiative = CompanionInitiativePolicy.Always;
				mate.Profile.GatheringInitiative = mate.Profile.ForestryInitiative = mate.Profile.TreasureInitiative = CompanionInitiativePolicy.Never;
				((CompanionAttention)Get("attention")).Reset();
				Call("UpdateHelpfulAutonomy");
				check(Get("autonomyActivity").ToString() == "AssistMining", "Approved observed material was not selected by the actual scheduler");
				mate.SetAutomaticMiningRule(false, TileID.Dirt, false);
				check(Get("autonomyActivity").ToString() == "None" && soil.HasTile, "Changing a rule did not stop the now-disallowed automatic target");
				Main.netMode = NetmodeID.MultiplayerClient;
				check(!mate.SetAutomaticMiningRule(true, TileID.Copper, true) && !mate.Profile.AllowsAutomaticMining(TileID.Copper),
					"Client changed authoritative mining rules");
				Main.netMode = mode;
				check(CompanionDialogueEngine.GetEnergyChange(TalkCategory.Commands, 0) == 0
					&& CompanionDialogueEngine.GetEnergyChange(TalkCategory.Work, 1) == -2, "Command energy costs are inconsistent with their UI");
			}
		}
		finally {
			Main.player[0] = oldPlayer; Main.npc = oldNpcs; Main.item = oldItems; Main.tile = oldMap;
			Main.maxTilesX = oldWidth; Main.maxTilesY = oldHeight; Main.netMode = oldMode; Main.myPlayer = oldLocal;
			for (int i = 0; i < oldClients.Length; i++) Netplay.Clients[i] = oldClients[i];
		}
	}

	private static void CheckMiningFilterWheel(Action<bool, string> check, SoulboundCompanion mate)
	{
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
		var wheel = ModContent.GetInstance<CompanionWheelSystem>(); Type type = typeof(CompanionWheelSystem);
		object branch = Enum.Parse(type.GetNestedType("RootBranch", flags)!, "Work");
		void Click(string position, params object[] args) {
			Vector2 p = position == "center" ? (Vector2)type.GetField("center", flags)!.GetValue(wheel)!
				: (Vector2)type.GetMethod(position, flags)!.Invoke(wheel, args)!;
			PlayerInput.MouseX = (int)(p.X * Main.UIScale); PlayerInput.MouseY = (int)(p.Y * Main.UIScale);
			Main.mouseLeft = false; wheel.UpdateUI(new GameTime()); Main.mouseLeft = true; wheel.UpdateUI(new GameTime());
			Main.mouseLeft = false; wheel.UpdateUI(new GameTime());
		}
		float oldScale = Main.UIScale;
		List<int> oldLearned = mate.Profile.LearnedMiningTiles.ToList();
		try {
			mate.Profile.LearnMiningMaterial(TileID.Dirt, 35);
			foreach (float scale in new[] { 1f, 1.5f, 2f }) foreach (string culture in SupportedCultures) {
				Main.UIScale = scale; Terraria.Localization.LanguageManager.Instance.SetLanguage(culture);
				wheel.Open(mate); type.GetMethod("ActivateRoot", flags)!.Invoke(wheel, new[] { branch });
				Click("BranchPosition", branch, 5, 8);
				Click("MiningApproachPosition", 4);
				check((bool)type.GetField("miningFilterMenu", flags)!.GetValue(wheel)!, "Automatic mining settings click missed");
				Click("MiningFilterPosition", 1); Click("MiningFilterPosition", 1);
				check(mate.Profile.AllowsAutomaticMining(TileID.Dirt), "Observed-block wheel click failed");
				Click("MiningFilterPosition", 1);
				check(!mate.Profile.AllowsAutomaticMining(TileID.Dirt), "Observed-block wheel toggle cannot turn off");
				Click("center"); check((int)type.GetField("miningFilterCategory", flags)!.GetValue(wheel)! == -1, "Mining filter Back skipped its category layer");
				Click("MiningFilterPosition", 0);
				Click("MiningFilterPosition", 1);
				List<int> ores = CompanionMiningRules.Types(mate.Profile, true);
				check(!mate.Profile.AllowsAutomaticMining(ores[0]), "Ore filter click failed");
				Click("MiningFilterPosition", 1); check(mate.Profile.AllowsAutomaticMining(ores[0]), "Ore filter cannot re-enable target");
				Click("MiningFilterPosition", 0);
				check(ores.All(ore => !mate.Profile.AllowsAutomaticMining(ore)), "All-ores toggle left some ore enabled");
				Click("MiningFilterPosition", 0);
				check(ores.All(mate.Profile.AllowsAutomaticMining), "All-ores toggle failed to restore ores");
				string label = (string)type.GetMethod("MiningFilterLabel", flags)!.Invoke(wheel, new object[] { 0 })!;
				check(!label.Contains("Mods.Soulmates") && label.Contains(SoulmatesText.Get("UI.CompanionWheel.MiningFilter.AllOres")), "Mining rule hover leaks a localization key");
				int nodes = (int)type.GetProperty("MiningFilterNodeCount", flags)!.GetValue(wheel)!;
				Click("MiningFilterPosition", nodes - 1);
				check((int)type.GetField("miningFilterPage", flags)!.GetValue(wheel)! == 1, "Mining filter Next page missed");
				Click("MiningFilterPosition", (int)type.GetProperty("MiningFilterEntries", flags)!.GetValue(wheel)! + 1);
				check((int)type.GetField("miningFilterPage", flags)!.GetValue(wheel)! == 0, "Mining filter Previous page missed");
				Click("center"); Click("center");
				check(!(bool)type.GetField("miningFilterMenu", flags)!.GetValue(wheel)!
					&& (bool)type.GetField("miningApproachMenu", flags)!.GetValue(wheel)!, "Mining filter Back did not restore mining approaches");
			}
		}
		finally { wheel.Close(); Main.UIScale = oldScale; mate.Profile.LearnedMiningTiles = oldLearned; }
	}
}
