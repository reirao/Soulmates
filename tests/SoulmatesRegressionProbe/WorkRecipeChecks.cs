#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Common.Dialogue;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.Net;

namespace SoulmatesRegressionProbe;

public sealed partial class EngineChecks
{
	private static void CheckWorkRecipes(Action<bool, string> check, Mod mod)
	{
		foreach (CompanionWorkKind kind in Enum.GetValues<CompanionWorkKind>())
		foreach (CompanionMiningApproach approach in Enum.GetValues<CompanionMiningApproach>())
		foreach (CompanionMiningDirection direction in Enum.GetValues<CompanionMiningDirection>())
		foreach (CompanionTunnelEnd end in Enum.GetValues<CompanionTunnelEnd>()) {
			var profile = new CompanionProfile { MiningDirection = direction, TunnelEnd = end,
				LastWork = new(kind, approach, direction, end) };
			using var stream = new MemoryStream();
			using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) profile.Write(writer);
			stream.Position = 0; using var reader = new BinaryReader(stream, Encoding.UTF8, true);
			foreach (CompanionProfile saved in new[] { profile.Clone(), CompanionProfile.Load(profile.Save()), CompanionProfile.Read(reader) })
				check(saved.LastWork == profile.LastWork && saved.MiningDirection == direction && saved.TunnelEnd == end,
					"Work recipe lost in clone, save or transport");
			check(stream.Position == stream.Length, "Work recipe transport leaves unread bytes");
		}
		CompanionProfile legacy = CompanionProfile.Load(new TagCompound());
		check(legacy.LastWork is null && legacy.MiningDirection == CompanionMiningDirection.Auto
			&& legacy.TunnelEnd == CompanionTunnelEnd.Passage, "Old Sigil gained an invented last order");
		check(CompanionProfile.Load(new TagCompound { ["lastWork"] = new TagCompound() }).LastWork is null,
			"Incomplete saved work recipe invented a treasure order");
		var corrupt = new CompanionProfile { MiningDirection = (CompanionMiningDirection)255,
			TunnelEnd = (CompanionTunnelEnd)255, LastWork = new((CompanionWorkKind)255, 0, 0, 0) };
		corrupt.Normalize(); check(corrupt.LastWork is null && corrupt.MiningDirection == CompanionMiningDirection.Auto,
			"Corrupt saved work recipe survived normalization");

		const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
		Player oldPlayer = Main.player[0]; NPC[] oldNpcs = Main.npc; Item[] oldItems = Main.item; Tilemap oldMap = Main.tile;
		int oldWidth = Main.maxTilesX, oldHeight = Main.maxTilesY, oldMode = Main.netMode, oldLocal = Main.myPlayer;
		RemoteClient[] oldClients = (RemoteClient[])Netplay.Clients.Clone();
		try {
			for (int i = 0; i < Netplay.Clients.Length; i++) Netplay.Clients[i] = new RemoteClient { Socket = new ProbeSocket() };
			foreach (int mode in new[] { NetmodeID.SinglePlayer, NetmodeID.Server }) {
				Main.netMode = mode; Main.myPlayer = mode == NetmodeID.Server ? 255 : 0;
				Main.maxTilesX = Main.maxTilesY = 120;
				void ClearTerrain() => Main.tile = (Tilemap)Activator.CreateInstance(typeof(Tilemap), flags, null,
					new object[] { (ushort)120, (ushort)120 }, null)!;
				ClearTerrain();
				Main.npc = Enumerable.Range(0, oldNpcs.Length).Select(i => new NPC { whoAmI = i }).ToArray();
				Main.item = Enumerable.Range(0, oldItems.Length).Select(i => new Item { whoAmI = i }).ToArray();
				var owner = new Player { whoAmI = 0, active = true, Center = new Point(50, 50).ToWorldCoordinates() };
				Main.player[0] = owner;
				NPC npc = Main.npc[20]; npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
				npc.active = true; npc.ai[0] = 0; npc.Center = owner.Center;
				var mate = (SoulboundCompanion)npc.ModNPC; mate.Profile.AutonomyEnabled = false;
				mate.Profile.Talent = CompanionTalent.Miner;
				owner.inventory[0] = new Item(ModContent.ItemType<SoulboundSigil>());
				owner.inventory[1] = new Item(ItemID.CopperPickaxe);
				var sigil = (SoulboundSigil)owner.inventory[0].ModItem; sigil.Profile = mate.Profile.Clone();
				object? Call(string name, params object[] args) => typeof(SoulboundCompanion).GetMethod(name, flags)!.Invoke(mate, args);
				void Set(string name, object value) => typeof(SoulboundCompanion).GetField(name, flags)!.SetValue(mate, value);
				void Fill() { for (int x = 22; x <= 78; x++) for (int y = 22; y <= 78; y++) {
					Tile tile = Main.tile[x, y]; tile.HasTile = true; tile.TileType = TileID.Copper;
				} }
				List<Point> Plan() => (List<Point>)typeof(SoulboundCompanion).GetField("plannedMiningTargets", flags)!.GetValue(mate)!;
				Set("speechTimer", 20000); Set("socialTimer", 20000); Set("ambientStoryCooldown", 20000);
				Set("personalQuestionCooldown", 20000); Set("companyQuestionCooldown", 20000);
				foreach (CompanionMiningDirection direction in new[] { CompanionMiningDirection.Up, CompanionMiningDirection.Right,
					CompanionMiningDirection.Down, CompanionMiningDirection.Left }) {
					ClearTerrain(); Fill();
					check((bool)Call("ConfigureMining", CompanionMiningApproach.Tunnel, direction, CompanionTunnelEnd.Short)!, "Valid compass config rejected");
					mate.StartJob(CompanionJob.Mine); Call("PlanAreaMiningTargets");
					Point origin = owner.Center.ToTileCoordinates();
					Point delta = direction switch { CompanionMiningDirection.Up => new(0, -1), CompanionMiningDirection.Right => new(1, 0),
						CompanionMiningDirection.Down => new(0, 1), _ => new(-1, 0) };
					check(Plan().Count > 0 && Plan().All(p => {
						int progress = (p.X - origin.X) * delta.X + (p.Y - origin.Y) * delta.Y;
						return progress >= 0 && progress <= 8;
						}), "Compass plan escaped its selected direction or length: " + direction);
					check(Plan().Count <= (delta.Y == 0 ? 27 : 18), "Tunnel cross-section exceeded its bound");
				}
				ClearTerrain(); Fill();
				Call("ConfigureMining", CompanionMiningApproach.Tunnel, CompanionMiningDirection.Right, CompanionTunnelEnd.Passage);
				for (int x = 55; x <= 56; x++) for (int y = 49; y <= 51; y++) { Tile cell = Main.tile[x, y]; cell.HasTile = false; }
				mate.StartJob(CompanionJob.Mine); Call("PlanAreaMiningTargets");
				check(Plan().All(p => p.X < 55), "Until-passage mined past a player-sized opening");
				ClearTerrain(); Fill(); Tile pocket = Main.tile[55, 49]; pocket.HasTile = false;
				mate.StartJob(CompanionJob.Mine); Call("PlanAreaMiningTargets");
				check(Plan().Any(p => p.X > 55), "Single-tile pocket was misreported as a passage");
				ClearTerrain(); Fill(); Main.tile[53, 50].LiquidAmount = 100;
				mate.StartJob(CompanionJob.Mine); Call("PlanAreaMiningTargets");
				check(Plan().All(p => p.X < 53), "Tunnel plan crossed liquid");
				ClearTerrain(); Fill();
				Tile protectedTile = Main.tile[53, 50]; protectedTile.TileType = TileID.LihzahrdBrick;
				mate.StartJob(CompanionJob.Mine); Call("PlanAreaMiningTargets");
				check(Plan().All(p => p.X < 53), "Tunnel plan skipped over a protected obstruction");
				ClearTerrain(); Fill();
				mate.StartJob(CompanionJob.Mine); Call("PlanAreaMiningTargets");
				Main.tile[50, 49].LiquidAmount = 100;
				object[] find = { Point.Zero };
				check(!(bool)typeof(SoulboundCompanion).GetMethod("FindMiningTarget", flags)!.Invoke(mate, find)!,
					"A planned tunnel ignored newly introduced liquid");
				CompanionWorkRecipe? remembered = mate.Profile.LastWork;
				Call("ConfigureMining", CompanionMiningApproach.Vein, CompanionMiningDirection.Up, CompanionTunnelEnd.Short);
				check(mate.Profile.LastWork == remembered, "Changing config overwrote the last accepted recipe");
				mate.Profile.Energy = 0;
				check(!mate.PerformQuickAction(CompanionQuickAction.RepeatLastWork).Accepted && mate.Profile.LastWork == remembered
					&& mate.Profile.MiningApproach == CompanionMiningApproach.Vein && sigil.Profile.MiningApproach == CompanionMiningApproach.Vein,
					"Rejected repeat mutated the recipe or mining settings");
				mate.Profile.Energy = mate.Profile.Mood = 100;
				check(mate.PerformQuickAction(CompanionQuickAction.RepeatLastWork).Accepted
					&& mate.Profile.MiningApproach == CompanionMiningApproach.Tunnel && mate.Profile.MiningDirection == CompanionMiningDirection.Right,
					"Last did not restore the accepted area-mining recipe");
				mate.SetCommand(false); ClearTerrain();
				NPC bunny = Main.npc[19]; bunny.SetDefaults(NPCID.Bunny); bunny.active = true; bunny.Center = npc.Center;
				mate.Profile.CritterCompanyInitiative = CompanionInitiativePolicy.Never;
				var answer = (CompanionConversationResult)Call("PerformNpcContext", CompanionNpcAction.Company, 19, bunny.type)!;
				check(answer.Accepted && !mate.Profile.AutonomyEnabled, "Explicit critter company still requires autonomy");
				npc.AI(); var company = bunny.GetGlobalNPC<CompanionCritterCompany>();
				check((bool)typeof(CompanionCritterCompany).GetMethod("BelongsTo", flags)!.Invoke(company, new object[] { mate })!
					&& mate.Profile.CritterCompanyInitiative == CompanionInitiativePolicy.Never, "Direct company failed or granted automatic consent");
				bunny.Center = npc.Center - new Vector2(190, 0); bunny.velocity.X = -1;
				company.PostAI(bunny);
				check(bunny.velocity.X > 0 && bunny.active, "Manual native critter follower stopped when autonomy was off");
				bunny.collideX = bunny.collideY = true; bunny.velocity.Y = 0;
				company.PostAI(bunny);
				check(bunny.velocity.Y < 0 && !bunny.noTileCollide, "Ground critter did not hop at a small obstacle or gained noclip");
				mate.Profile.WorkPaused = true; bunny.velocity = new Vector2(-1, 0); company.PostAI(bunny);
				check(bunny.velocity.X == -1 && bunny.velocity.Y == 0, "Paused company kept forcing a follow or jump");
				mate.Profile.WorkPaused = false; mate.PerformQuickAction(CompanionQuickAction.CritterOff);
				check(!(bool)typeof(CompanionCritterCompany).GetProperty("IsAssigned", flags)!.GetValue(company)!
					&& bunny.active, "Off did not release the real follower alive");
				mate.Profile.CritterCollectInitiative = CompanionInitiativePolicy.Never; mate.Profile.ClearCargo();
				bunny.Center = npc.Center; Set("insectCatchCooldown", 0);
				answer = (CompanionConversationResult)Call("PerformNpcContext", CompanionNpcAction.Collect, 19, bunny.type)!;
				check(answer.Accepted, "Explicit catch requires autonomy or a purchased net");
				npc.AI(); SettleServerInventory(mod);
				check(!bunny.active && mate.Profile.ItemCount(ItemID.Bunny) == 1
					&& mate.Profile.CritterCollectInitiative == CompanionInitiativePolicy.Never, "Directed native catch lost cargo or changed consent");
				mate.SetCommand(false); mate.Profile.ClearCargo(); mate.Profile.Store(new Item(ItemID.Carrot));
				check(CompanionItemTopics.IsPetItem(new Item(ItemID.Carrot)) && !CompanionItemTopics.IsPetItem(new Item(ItemID.CopperOre)),
					"Native pet item detection confuses resources with pets");
				owner.selectedItem = 2; owner.inventory[2] = new Item(ItemID.Carrot);
				check(mate.Converse(TalkCategory.Items, 1, 0).Reply.Contains(SoulmatesText.Get("Items.Uses.Pets")),
					"Carried pet item still implies a companion summon ability");
				Tile ore = Main.tile[52, 50]; ore.HasTile = true; ore.TileType = TileID.Copper;
				check(mate.PerformDirectOrder(CompanionTargetOrder.Mine, new Point(52, 50), -1).Accepted
					&& mate.Profile.LastWork?.Kind == CompanionWorkKind.MineTarget, "Accepted target order did not persist its intent");
				remembered = mate.Profile.LastWork;
				check(!mate.PerformDirectOrder(CompanionTargetOrder.Mine, new Point(55, 55), -1).Accepted && mate.Profile.LastWork == remembered,
					"Lost target replaced Last with a failed order");
				mate.SetCommand(false);
				if (mode == NetmodeID.Server) {
					Type messages = mod.GetType().GetNestedType("MessageType", BindingFlags.NonPublic)!;
					byte type = Convert.ToByte(Enum.Parse(messages, "MiningConfigRequest"));
					void Config(Guid id, byte approach, byte direction, byte end, int trim = 0) {
						using var stream = new MemoryStream();
						using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) {
							writer.Write(type); writer.Write(id.ToByteArray()); writer.Write(approach); writer.Write(direction); writer.Write(end);
						}
						if (trim > 0) stream.SetLength(stream.Length - trim);
						stream.Position = 0; using var reader = new BinaryReader(stream); mod.HandlePacket(reader, 0);
					}
					Config(mate.Profile.Id, 1, 4, 1);
					check(mate.Profile.MiningApproach == CompanionMiningApproach.Tunnel && mate.Profile.MiningDirection == CompanionMiningDirection.Left
						&& mate.Profile.TunnelEnd == CompanionTunnelEnd.Short, "Mining config packet did not reach the bound companion");
					Config(Guid.NewGuid(), 0, 0, 0); Config(mate.Profile.Id, 255, 0, 0);
					Config(mate.Profile.Id, 0, 255, 0); Config(mate.Profile.Id, 0, 0, 255);
					Config(mate.Profile.Id, 0, 0, 0, 1);
					check(mate.Profile.MiningDirection == CompanionMiningDirection.Left && mate.Profile.LastWork == remembered,
						"Stale, corrupt or truncated config packet mutated state");
				}
				Main.netMode = NetmodeID.MultiplayerClient;
				check(!(bool)Call("ConfigureMining", CompanionMiningApproach.Tunnel, CompanionMiningDirection.Left, CompanionTunnelEnd.Short)!
					&& !mate.PerformQuickAction(CompanionQuickAction.RepeatLastWork).Accepted, "Client changed authoritative work directly");
				Main.netMode = mode;
			}
		}
		finally {
			Main.player[0] = oldPlayer; Main.npc = oldNpcs; Main.item = oldItems; Main.tile = oldMap;
			Main.maxTilesX = oldWidth; Main.maxTilesY = oldHeight; Main.netMode = oldMode; Main.myPlayer = oldLocal;
			for (int i = 0; i < Netplay.Clients.Length; i++) Netplay.Clients[i] = oldClients[i];
		}
	}
}
