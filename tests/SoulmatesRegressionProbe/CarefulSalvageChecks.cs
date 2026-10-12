#nullable enable
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Common.Dialogue;
using Soulmates.Common.UI;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.Net;
using Terraria.ObjectData;

namespace SoulmatesRegressionProbe;

public sealed partial class EngineChecks
{
	private static void CheckCarefulSalvage(Action<bool, string> check, Mod mod)
	{
		const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
		Tilemap oldMap = Main.tile;
		Player[] oldPlayers = Main.player; NPC[] oldNpcs = Main.npc; Item[] oldItems = Main.item;
		WorldFileData oldWorld = Main.ActiveWorldFileData;
		int oldMode = Main.netMode, oldLocal = Main.myPlayer, oldX = Main.maxTilesX, oldY = Main.maxTilesY;
		bool oldGen = WorldGen.gen, oldDedicated = Main.dedServ;
		RemoteClient[] oldClients = (RemoteClient[])Netplay.Clients.Clone();
		var protection = ModContent.GetInstance<SalvageProtectionSystem>();
		var oldProtection = new TagCompound(); protection.SaveWorldData(oldProtection);
		Type salvageType = typeof(CompanionProfile).Assembly.GetType("Soulmates.Common.CompanionSalvage")!;
		Type targetType = typeof(CompanionProfile).Assembly.GetType("Soulmates.Common.SalvageTarget")!;
		Type mateType = typeof(SoulboundCompanion);
		object Capture(Point point) => salvageType.GetMethod("Capture", flags)!.Invoke(null, [point])!;
		ulong Fingerprint(object target) => (ulong)targetType.GetProperty("Fingerprint")!.GetValue(target)!;
		try {
			Main.dedServ = true; Main.myPlayer = 255; WorldGen.gen = false;
			for (int i = 0; i < Netplay.Clients.Length; i++) Netplay.Clients[i] = new RemoteClient { Socket = new ProbeSocket() };
			foreach (int mode in new[] { NetmodeID.SinglePlayer, NetmodeID.Server }) {
				void Verify(bool condition, string text) => check(condition, "Salvage " + mode + ": " + text);
				Main.netMode = mode; Main.maxTilesX = Main.maxTilesY = 120;
				Main.tile = (Tilemap)Activator.CreateInstance(typeof(Tilemap), flags, null, new object[] { (ushort)120, (ushort)120 }, null)!;
				Main.ActiveWorldFileData = new WorldFileData("", false) { UniqueId = Guid.NewGuid() };
				Main.player = Enumerable.Range(0, oldPlayers.Length).Select(i => new Player { whoAmI = i }).ToArray();
				Main.npc = Enumerable.Range(0, oldNpcs.Length).Select(i => new NPC { whoAmI = i }).ToArray();
				Main.item = Enumerable.Range(0, oldItems.Length).Select(i => new Item { whoAmI = i }).ToArray();
				Player owner = Main.player[0]; owner.active = true; owner.SpawnX = owner.SpawnY = -1;
				owner.Center = new Point(50, 50).ToWorldCoordinates(); owner.statLife = owner.statLifeMax2 = 100;
				NPC npc = Main.npc[20]; npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
				npc.active = true; npc.ai[0] = 0; npc.homeless = true; npc.Center = owner.Center;
				var mate = (SoulboundCompanion)npc.ModNPC;
				mate.Profile.AutonomyEnabled = false; mate.Profile.QuestionCadence = CompanionQuestionCadence.Quiet;
				mate.Profile.Energy = mate.Profile.Mood = 100;
				owner.inventory[0] = new Item(ModContent.ItemType<SoulboundSigil>());
				var sigil = (SoulboundSigil)owner.inventory[0].ModItem; sigil.Profile = mate.Profile.Clone();
				object? Call(string method, params object[] args) => mateType.GetMethod(method, flags)!.Invoke(mate, args);
				void Set(string field, object value) => mateType.GetField(field, flags)!.SetValue(mate, value);
				CompanionConversationResult Request(Point point, ulong? fingerprint = null, Guid? world = null) {
					object target = Capture(point);
					return (CompanionConversationResult)Call("RequestSalvage", point,
						world ?? Main.ActiveWorldFileData.UniqueId, fingerprint ?? Fingerprint(target))!;
				}
				void Tile(Point point, ushort type, short frameX = 0, short frameY = 0) {
					Terraria.Tile cell = Main.tile[point]; cell.ClearEverything(); cell.HasTile = true;
					cell.TileType = type; cell.TileFrameX = frameX; cell.TileFrameY = frameY;
				}
				Point first = new(50, 50), second = new(54, 50), third = new(58, 50);
				Terraria.Tile firstTile = Main.tile[first];
				void Reset() {
					Call("CancelAssignment"); protection.ClearWorld(); mate.Profile.Places = new CompanionPlaces();
					mate.Profile.ClearCargo(); mate.Profile.Energy = mate.Profile.Mood = 100;
					owner.active = true; owner.dead = false; npc.Center = owner.Center = first.ToWorldCoordinates();
					Main.ActiveWorldFileData.UniqueId = Guid.NewGuid();
					for (int x = 35; x < 78; x++) for (int y = 35; y < 70; y++) Main.tile[x, y].ClearEverything();
					foreach (Item item in Main.item) item.active = false;
					Tile(first, TileID.Cobweb); Tile(second, TileID.Cobweb, 18); sigil.Profile = mate.Profile.Clone();
				}
				void Work(int ticks = 70) {
					for (int i = 0; i < ticks; i++) {
						if (mate.CurrentJob == CompanionJob.Salvage) {
							IEnumerable queue = (IEnumerable)mateType.GetField("salvageWork", flags)!.GetValue(mate)!;
							object? next = queue.Cast<object>().FirstOrDefault();
							if (next is not null) npc.Center = (Vector2)targetType.GetProperty("Center")!.GetValue(next)!;
						}
						foreach (Item item in Main.item) if (item.active && item.noGrabDelay > 0) item.noGrabDelay--;
						Call("UpdateJob");
					}
				}

				Reset();
				Verify(Request(first).Accepted && mate.PendingQuestion == CompanionQuestion.Salvage
					&& mate.CurrentJob == CompanionJob.None && Main.tile[first].HasTile, "request removed an unanswered object");
				Guid token = mate.QuestionId;
				Verify(!mate.RespondToQuestion(Guid.NewGuid(), CompanionAnswer.First), "foreign token accepted");
				Verify(mate.RespondToQuestion(token, CompanionAnswer.First) && mate.CurrentJob == CompanionJob.Salvage, "single approval not dispatched");
				Verify(!mate.RespondToQuestion(token, CompanionAnswer.First), "answer replay accepted");
				Work();
				Verify(!Main.tile[first].HasTile && Main.tile[second].HasTile && mate.CurrentJob == CompanionJob.None, "single permit spread to neighbours or never completed");
				Verify(mate.Profile.ItemCount(ItemID.Cobweb) + Main.item.Where(i => i.active && i.type == ItemID.Cobweb).Sum(i => i.stack) == 1,
					"native web drop was duplicated or lost");
				Verify(mate.Profile.ItemCount(ItemID.Cobweb) == 1, "native web drop never entered shared cargo");

				Reset(); Request(first); token = mate.QuestionId;
				Verify(mate.RespondToQuestion(token, CompanionAnswer.Second) && mate.PendingQuestion == CompanionQuestion.SalvageArea
					&& mate.QuestionId != token && mate.CurrentJob == CompanionJob.None, "area work started without second confirmation");
				Tile(third, TileID.Cobweb); token = mate.QuestionId;
				Verify(mate.RespondToQuestion(token, CompanionAnswer.First), "area confirmation refused"); Work(130);
				Verify(!Main.tile[first].HasTile && !Main.tile[second].HasTile && Main.tile[third].HasTile, "captured selection expanded to a later find");
				Verify(mate.Profile.ItemCount(ItemID.Cobweb) == 2, "approved matching web variants did not collect exactly twice");

				foreach (CompanionAnswer answer in new[] { CompanionAnswer.Third, CompanionAnswer.Later }) {
					Reset(); Request(first); Verify(mate.RespondToQuestion(mate.QuestionId, answer), "cancel answer failed");
					Work(); Verify(Main.tile[first].HasTile && mate.CurrentJob == CompanionJob.None, "cancel removed a tile");
				}
				Reset(); Request(first); Main.tile[first].TileFrameX += 18;
				Verify(!mate.RespondToQuestion(mate.QuestionId, CompanionAnswer.First) && !mate.HasPendingQuestion, "changed frame retained permit");
				Reset(); Request(first); Main.ActiveWorldFileData.UniqueId = Guid.NewGuid();
				Verify(!mate.RespondToQuestion(mate.QuestionId, CompanionAnswer.First), "different world retained permit");
				Reset(); Verify(!Request(first, 123).Accepted && !Request(first, world: Guid.NewGuid()).Accepted, "stale mouse/world signature accepted");
				Reset(); Request(first); firstTile.RedWire = true;
				Verify(!mate.RespondToQuestion(mate.QuestionId, CompanionAnswer.First), "new wire did not invalidate answer");

				Reset(); Request(first); mate.RespondToQuestion(mate.QuestionId, CompanionAnswer.First);
				mate.PerformQuickAction(CompanionQuickAction.Pause); Set("jobTimer", 23); Call("TickPausedActivity");
				Verify(Main.tile[first].HasTile && mate.CurrentJob == CompanionJob.Salvage, "Pause destroyed the approved job");
				mate.PerformQuickAction(CompanionQuickAction.Resume); Work(); Verify(!Main.tile[first].HasTile, "Resume lost the permit");
				Reset(); Request(first); mate.RespondToQuestion(mate.QuestionId, CompanionAnswer.First);
				mate.PerformQuickAction(CompanionQuickAction.Abort); Work(); Verify(Main.tile[first].HasTile, "Abort retained a destructive queue");
				Reset(); Request(first); mate.RespondToQuestion(mate.QuestionId, CompanionAnswer.First);
				firstTile.BlueWire = true; Work(); Verify(Main.tile[first].HasTile, "execution ignored a newly protected target");
				Reset(); mate.StartJob(CompanionJob.Salvage); Work(); Verify(Main.tile[first].HasTile, "uncaptured StartJob bypassed consent");

				foreach (Action action in new Action[] {
					() => firstTile.RedWire = true, () => firstTile.HasActuator = true,
					() => firstTile.TileColor = 2, () => firstTile.IsTileFullbright = true,
					() => Main.tile[first].WallType = WallID.Wood,
					() => { firstTile.LiquidAmount = 100; firstTile.LiquidType = LiquidID.Lava; },
					() => mate.Profile.Places.RememberRole(Main.ActiveWorldFileData.UniqueId, first, CompanionPlaceRole.CompanionHome),
					() => { owner.SpawnX = first.X; owner.SpawnY = first.Y; },
				}) {
					Reset(); owner.SpawnX = owner.SpawnY = -1; action();
					Verify(!Request(first).Accepted && Main.tile[first].HasTile, "sensitive environment permitted salvage");
				}
				owner.SpawnX = owner.SpawnY = -1;
				Reset(); firstTile.LiquidAmount = 255; firstTile.LiquidType = LiquidID.Water;
				Verify(Request(first).Accepted, "ordinary underwater find was rejected just for water");

				Reset();
				WorldGen.PlaceTile(55, 54, TileID.Stone, mute: true, forced: true);
				Verify(protection.IsProtected(new Point(55, 54)), "runtime native placement was not protected");
				Verify(!Request(first).Accepted, "nearby observed construction was not protected");
				var saved = new TagCompound(); protection.SaveWorldData(saved); protection.ClearWorld(); protection.LoadWorldData(saved);
				Verify(protection.IsProtected(new Point(55, 54)) && !Request(first).Accepted, "world save/load forgot construction");
				Reset(); WorldGen.gen = true; WorldGen.PlaceTile(55, 54, TileID.Stone, mute: true, forced: true); WorldGen.gen = false;
				Verify(!protection.IsProtected(new Point(55, 54)), "world generation was labelled as player construction");

				Reset(); Tile(first, TileID.Amethyst);
				Verify(Request(first).Accepted, "exposed gem resource not supported");
				Tile(new(first.X, first.Y - 1), TileID.Sand);
				Verify(!Request(first).Accepted && Main.tile[first].HasTile, "gem supporting falling terrain was not protected");
				Reset(); Tile(first, TileID.Containers);
				Verify(Capture(first) is null, "storage entered the supported whitelist");
				Reset();
				for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++) Tile(new(first.X + x, first.Y + y), TileID.Pots, (short)(18 * x), (short)(18 * y));
				Tile(new(first.X, first.Y + 2), TileID.Stone); Tile(new(first.X + 1, first.Y + 2), TileID.Stone);
				object pot = Capture(new(first.X + 1, first.Y + 1));
				TileObjectData? potData = TileObjectData.GetTileData(Main.tile[first.X + 1, first.Y + 1]);
				Verify(pot is not null && (Rectangle)targetType.GetProperty("Bounds")!.GetValue(pot)! == new Rectangle(50, 50, 2, 2),
					$"pot bottom-right did not capture whole object (data {potData?.Width}x{potData?.Height}, full {potData?.CoordinateFullWidth}x{potData?.CoordinateFullHeight}, heights {string.Join(',', potData?.CoordinateHeights ?? [])})");
				if (pot is not null) { Verify(Request(new(first.X + 1, first.Y + 1)).Accepted, "pot request failed"); mate.RespondToQuestion(mate.QuestionId, CompanionAnswer.First); Work(); }
				Verify(!Main.tile[first].HasTile && !Main.tile[first.X + 1, first.Y + 1].HasTile && Main.tile[first.X, first.Y + 2].HasTile,
					"native pot destruction left fragments or removed supports");
				foreach (ushort type in new ushort[] { TileID.Coral, TileID.Books, TileID.Bottles, TileID.WaterCandle,
					TileID.Statues, TileID.Banners, TileID.Painting2X3, TileID.Painting3X2, TileID.Painting3X3, TileID.Painting4X3, TileID.Painting6X4 }) {
					Reset(); Main.tile[first].ClearEverything(); Main.tile[second].ClearEverything();
					for (int x = 42; x <= 60; x++) {
						Tile(new(x, 54), type == TileID.Coral ? TileID.Sand : TileID.Stone);
						Tile(new(x, 55), TileID.Stone);
						for (int y = 42; y < 54; y++) Main.tile[x, y].WallType = WallID.DirtUnsafe;
					}
					WorldGen.gen = true;
					switch (type) {
						case TileID.Books or TileID.Bottles or TileID.WaterCandle: WorldGen.Place1x1(50, 53, type, 0); break;
						case TileID.Banners: Tile(new(50, 48), TileID.Stone); WorldGen.PlaceBanner(50, 49, type, 100); break;
						case TileID.Painting2X3: WorldGen.Place2x3Wall(50, 49, type, 0); break;
						case TileID.Painting3X2: WorldGen.Place3x2Wall(50, 49, type, 0); break;
						case TileID.Painting3X3: WorldGen.Place3x3Wall(50, 49, type, 0); break;
						case TileID.Painting4X3: WorldGen.Place4x3Wall(50, 49, type, 0); break;
						case TileID.Painting6X4: WorldGen.Place6x4Wall(50, 49, type, 0); break;
						default: WorldGen.PlaceTile(50, 53, type, mute: true, forced: true); break;
					}
					WorldGen.gen = false;
					Point? placed = null;
					for (int x = 42; x <= 60; x++) for (int y = 42; y < 54; y++)
						if (Main.tile[x, y].HasTile && Main.tile[x, y].TileType == type) placed ??= new Point(x, y);
					Verify(placed is not null, "native decoration fixture did not place type " + type);
					if (placed is not { } found) continue;
					object? captured = Capture(found);
					Verify(captured is not null, "native decoration capture failed type " + type);
					if (captured is null) continue;
					Verify(Request(found).Accepted, "native decoration blocked unexpectedly type " + type);
					mate.RespondToQuestion(mate.QuestionId, CompanionAnswer.First); Work();
					int remains = 0;
					for (int x = 42; x <= 60; x++) for (int y = 42; y < 54; y++)
						if (Main.tile[x, y].HasTile && Main.tile[x, y].TileType == type) remains++;
					Verify(remains == 0, "native decoration salvage left fragments type " + type);
				}

				Reset(); Main.netMode = NetmodeID.MultiplayerClient;
				Verify(!Request(first).Accepted, "client directly granted itself a permit");
				Main.netMode = mode; Request(first); token = mate.QuestionId; Main.netMode = NetmodeID.MultiplayerClient;
				Verify(!mate.RespondToQuestion(token, CompanionAnswer.First) && Main.tile[first].HasTile, "client-side response destroyed an object");
				Main.netMode = mode;
				Reset(); Request(first); token = mate.QuestionId; owner.inventory[0].TurnToAir();
				Verify(!mate.RespondToQuestion(token, CompanionAnswer.First), "missing Sigil kept destructive permission");
			}
		} finally {
			Main.tile = oldMap; Main.maxTilesX = oldX; Main.maxTilesY = oldY; Main.player = oldPlayers;
			Main.npc = oldNpcs; Main.item = oldItems; Main.ActiveWorldFileData = oldWorld;
			Main.netMode = oldMode; Main.myPlayer = oldLocal; Main.dedServ = oldDedicated; WorldGen.gen = oldGen;
			for (int i = 0; i < Netplay.Clients.Length; i++) Netplay.Clients[i] = oldClients[i];
			protection.ClearWorld(); protection.LoadWorldData(oldProtection);
		}
	}
}
