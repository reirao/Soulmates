#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Common.Dialogue;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Soulmates.Content.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Net;

namespace SoulmatesRegressionProbe;

public sealed partial class EngineChecks
{
	private static void CheckCritterPetRuntime(Action<bool, string> check, Mod mod)
	{
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
		Player[] oldPlayers = Main.player; NPC[] oldNpcs = Main.npc;
		Item[] oldItems = Main.item; Projectile[] oldProjectiles = Main.projectile; Tilemap oldMap = Main.tile;
		int oldWidth = Main.maxTilesX, oldHeight = Main.maxTilesY, oldMode = Main.netMode, oldLocal = Main.myPlayer;
		bool oldMenu = Main.gameMenu, oldDay = Main.dayTime, oldRain = Main.raining, oldBloodMoon = Main.bloodMoon;
		RemoteClient[] oldClients = (RemoteClient[])Netplay.Clients.Clone();
		var trace = new List<string>();
		try {
			for (int i = 0; i < Netplay.Clients.Length; i++) Netplay.Clients[i] = new RemoteClient { Socket = new ProbeSocket() };
			Main.maxTilesX = 240; Main.maxTilesY = 160; Main.gameMenu = false; Main.dayTime = true; Main.raining = Main.bloodMoon = false;
			Main.tile = (Tilemap)Activator.CreateInstance(typeof(Tilemap), flags, null,
				new object[] { (ushort)240, (ushort)160 }, null)!;
			for (int x = 1; x < 239; x++) for (int y = 90; y < 159; y++) {
				Tile ground = Main.tile[x, y]; ground.HasTile = true; ground.TileType = TileID.Grass;
			}
			foreach (int mode in new[] { NetmodeID.SinglePlayer, NetmodeID.Server }) {
				Main.netMode = mode; Main.myPlayer = mode == NetmodeID.Server ? 255 : 0;
				Main.player = Enumerable.Range(0, oldPlayers.Length).Select(i => new Player { whoAmI = i }).ToArray();
				Main.npc = Enumerable.Range(0, oldNpcs.Length).Select(i => new NPC { whoAmI = i }).ToArray();
				Main.item = Enumerable.Range(0, oldItems.Length).Select(i => new Item { whoAmI = i }).ToArray();
				Main.projectile = Enumerable.Range(0, oldProjectiles.Length).Select(i => new Projectile { whoAmI = i }).ToArray();
				Player owner = Main.player[0]; owner.active = true; owner.Bottom = new Vector2(1280, 1440);
				NPC npc = Main.npc[20]; npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
				npc.active = true; npc.ai[0] = 0; npc.Center = owner.Center + new Vector2(-50, -60);
				var mate = (SoulboundCompanion)npc.ModNPC;
				mate.Profile.AutonomyEnabled = false;
				owner.inventory[0] = new Item(ModContent.ItemType<SoulboundSigil>());
				var sigil = (SoulboundSigil)owner.inventory[0].ModItem; sigil.Profile = mate.Profile.Clone();
				object? Call(string method, params object[] args) => typeof(SoulboundCompanion).GetMethod(method, flags)!.Invoke(mate, args);
				object? Field(string name) => typeof(SoulboundCompanion).GetField(name, flags)!.GetValue(mate);
				string State(NPC animal) => $"mate={npc.Center}; animal={animal.Center}; active={animal.active}; type={animal.type}; natural={CompanionCritters.IsNatural(animal)}; reason={typeof(CompanionCritters).GetMethod("NaturalRejectionReason", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { animal })}; life={animal.life}; friendly={animal.friendly}; released={animal.releaseOwner}; damage={animal.damage}; mode={mate.Profile.CritterMode}; target={Field("critterTarget") is NPC}; directed={Field("directedCritterVisit")}; lane={typeof(SoulboundCompanion).GetProperty("ActivityLane", flags)!.GetValue(mate)}; canAttend={Call("CanAttendCritters")}; visible={Call("CanSeeCritter", animal)}; speech={Field("speechText")}";
				void Tick(int count) {
					for (int tick = 0; tick < count; tick++) {
						for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active) Main.npc[i].UpdateNPC(i);
						for (int i = 0; i < Main.maxProjectiles; i++) if (Main.projectile[i].active) Main.projectile[i].Update(i);
						SettleServerInventory(mod);
					}
				}
				NPC Critter(int type) {
					NPC animal = Main.npc[19] = new NPC { whoAmI = 19 };
					animal.SetDefaults(type); animal.active = true;
					animal.Bottom = new Vector2(owner.Center.X + 140, 1440);
					animal.direction = animal.spriteDirection = 1; animal.velocity.X = 1;
					return animal;
				}
				foreach (int type in new[] { NPCID.Bunny, NPCID.Squirrel, NPCID.Bird }) {
					NPC animal = Critter(type);
					typeof(NPC).GetField("catchableNPCTempImmunityCounter", flags)!.SetValue(animal, 1);
					typeof(NPC).GetField("catchableNPCOriginallyFriendly", flags)!.SetValue(animal, false);
					animal.UpdateNPC(19);
					check(!animal.friendly && CompanionCritters.IsNatural(animal), $"Native immunity expiry rejects harmless critter {type}, mode {mode}");
					var reply = (CompanionConversationResult)Call("PerformNpcContext", CompanionNpcAction.Company, 19, type)!;
					check(reply.Accepted, $"Runtime company refused {type}, mode {mode}");
					trace.Add($"company start {type}/{mode}: {State(animal)}");
					for (int tick = 0; tick < 240; tick++) {
						Tick(1);
						if (tick < 45 || tick % 40 == 0) trace.Add($"company tick {tick}, {type}/{mode}: {State(animal)}");
					}
					var company = animal.GetGlobalNPC<CompanionCritterCompany>();
					bool Joined() => (bool)typeof(CompanionCritterCompany).GetMethod("BelongsTo", flags)!.Invoke(company, new object[] { mate })!;
					check(Joined(), $"Native updates did not complete company visit {type}, mode {mode}; mate={npc.Center}, animal={animal.Center}, target={Call("CritterModeStatus", mate.Profile.CritterMode)}");
					float initialX = animal.Center.X;
					owner.position.X += 240; Tick(240);
					check(Joined() && animal.active && animal.Center.X > initialX + 20,
						$"Native critter failed to follow after recruitment {type}, mode {mode}; initialX={initialX}, {State(animal)}");
					mate.PerformQuickAction(CompanionQuickAction.CritterOff);
					check(!Joined() && animal.active, $"Native company Off removed or retained animal {type}, mode {mode}");
					animal.active = false; owner.position.X -= 240; Tick(80);
				}
				foreach (int type in new[] { NPCID.Bunny, NPCID.Butterfly, NPCID.Firefly }) {
					Main.dayTime = type != NPCID.Firefly;
					NPC animal = Critter(type); int itemType = animal.catchItem;
					var reply = (CompanionConversationResult)Call("PerformNpcContext", CompanionNpcAction.Collect, 19, type)!;
					check(reply.Accepted, $"Runtime catch refused {type}, mode {mode}");
					Tick(360);
					// Native butterfly AI chooses its species-specific catch item after spawning.
					itemType = animal.catchItem;
					trace.Add($"catch end {type}/{mode}: {State(animal)}; cargo={mate.Profile.ItemCount(itemType)}");
					check(!animal.active && mate.Profile.ItemCount(itemType) == 1 && sigil.Profile.ItemCount(itemType) == 1,
						$"Native updates failed catch/cargo {type}, mode {mode}; active={animal.active}, amount={mate.Profile.ItemCount(itemType)}, animal={animal.Center}, mate={npc.Center}");
					animal.active = false;
					mate.PerformQuickAction(CompanionQuickAction.CritterOff); Tick(320);
				}
				Main.dayTime = true;
				NPC automatic = Critter(NPCID.Bunny);
				npc.Center = owner.Center + new Vector2(-50, -60); npc.velocity = Vector2.Zero;
				mate.Profile.AutonomyEnabled = true; mate.Profile.QuestionCadence = CompanionQuestionCadence.Quiet;
				foreach (var ability in CompanionAbilityRegistry.All) mate.Profile.SetInitiativePolicy(ability.Kind, CompanionInitiativePolicy.Never);
				mate.Profile.CritterCompanyInitiative = CompanionInitiativePolicy.Always;
				mate.PerformQuickAction(CompanionQuickAction.CritterCompany); Tick(360);
				var autoCompany = automatic.GetGlobalNPC<CompanionCritterCompany>();
				check((bool)typeof(CompanionCritterCompany).GetMethod("BelongsTo", flags)!.Invoke(autoCompany, new object[] { mate })!,
					$"Native automatic Company does not reach a consenting critter, mode {mode}");
				mate.PerformQuickAction(CompanionQuickAction.CritterOff); automatic.active = false;
				automatic = Critter(NPCID.Bunny);
				npc.Center = owner.Center + new Vector2(-50, -60); npc.velocity = Vector2.Zero;
				mate.Profile.CritterCompanyInitiative = CompanionInitiativePolicy.Never;
				mate.Profile.CritterCollectInitiative = CompanionInitiativePolicy.Always;
				int beforeCatch = mate.Profile.ItemCount(ItemID.Bunny);
				mate.PerformQuickAction(CompanionQuickAction.CritterCollect); Tick(360);
				check(!automatic.active && mate.Profile.ItemCount(ItemID.Bunny) == beforeCatch + 1
					&& sigil.Profile.ItemCount(ItemID.Bunny) == beforeCatch + 1,
					$"Native automatic Collect loses its caught critter or cargo, mode {mode}");
				mate.PerformQuickAction(CompanionQuickAction.CritterOff); mate.Profile.AutonomyEnabled = false;
				NPC witness = Critter(NPCID.Bunny); npc.Center = owner.Center + new Vector2(-50, -60);
				int wallX = (int)((npc.Center.X + witness.Center.X) / 32f);
				for (int y = 1; y < 90; y++) { Tile wall = Main.tile[wallX, y]; wall.HasTile = true; wall.TileType = TileID.Stone; }
				check(!(bool)Call("CanSeeCritter", witness)! && !(bool)Call("CanTargetCritterCollect", 19)!,
					$"Critter visibility fallback sees through a solid wall, mode {mode}");
				for (int y = 1; y < 90; y++) { Tile wall = Main.tile[wallX, y]; wall.HasTile = false; }
				typeof(NPC).GetField("catchableNPCTempImmunityCounter", flags)!.SetValue(witness, 1);
				typeof(NPC).GetField("catchableNPCOriginallyFriendly", flags)!.SetValue(witness, false);
				witness.UpdateNPC(19);
				check(!witness.friendly && CompanionCritters.IsNatural(witness), $"Aged native death fixture rejected, mode {mode}");
				mate.ShowSpeech("I was just thinking about our walk.");
				int beforeExperience = mate.Profile.Experience;
				witness.StrikeNPC(new NPC.HitInfo { Damage = 1000, HitDirection = 1, HideCombatText = true }, noPlayerInteraction: true);
				check(!witness.active && (int)Field("pendingCritterLossTicks")! > 0
					&& mate.Profile.Memories.Any(memory => memory.Kind == CompanionMemoryKind.CritterLost),
					$"Aged native critter death produced no queued reaction/memory, mode {mode}");
				string loss = SoulmatesText.Get($"Social.Critters.Loss.{mate.Profile.Personality}", witness.TypeName);
				bool heardLoss = false;
				for (int tick = 0; tick < 1200; tick++) { Tick(1); heardLoss |= (string)Field("speechText")! == loss; }
				check(heardLoss && (int)Field("pendingCritterLossTicks")! == 0 && mate.Profile.Experience == beforeExperience,
					$"Aged critter loss disappears behind speech or awards kill XP, mode {mode}");
				Projectile[] Pets() => Main.projectile.Where(p => p.active && p.ModProjectile is CompanionFamiliar).ToArray();
				foreach (int itemType in CompanionPets.SupportedItems) {
					mate.Profile.Store(new Item(itemType));
					int slot = mate.Profile.Pack.FindIndex(item => item.type == itemType);
					byte[] token = (byte[])typeof(CompanionProfile).GetMethod("StorageToken", flags)!.Invoke(mate.Profile,
						new object[] { CompanionStorage.Pack, slot })!;
					check((bool)Call("ConfigurePet", slot, itemType, token)!, $"Runtime pet configuration failed {itemType}, mode {mode}");
					Projectile pet = Pets().Single(); int petIndex = pet.whoAmI; bool animated = false;
					for (int tick = 0; tick < 600; tick++) {
						if (tick >= 200 && tick < 320) owner.position.X += 1;
						Tick(1); animated |= pet.frame > 0;
					}
					check(pet.active && Pets().Length == 1 && Pets()[0].whoAmI == petIndex && animated,
						$"Native pet updates lose lifetime, identity or animation {itemType}, mode {mode}");
					check(Vector2.Distance(pet.Center, npc.Center) < 90 && pet.damage == 0 && mate.Profile.ItemCount(itemType) == 1,
						$"Native pet failed companion follow/ownership {itemType}, mode {mode}");
				}
				mate.Recall(); Tick(3);
				check(!npc.active && Pets().Length == 0, $"Native recall left a pet behind, mode {mode}");
			}
		}
		finally {
			File.WriteAllLines(Path.Combine(Main.SavePath, "Soulmates-critter-runtime-trace.txt"), trace);
			Main.player = oldPlayers; Main.npc = oldNpcs; Main.item = oldItems; Main.projectile = oldProjectiles; Main.tile = oldMap;
			Main.maxTilesX = oldWidth; Main.maxTilesY = oldHeight; Main.netMode = oldMode; Main.myPlayer = oldLocal; Main.gameMenu = oldMenu;
			Main.dayTime = oldDay; Main.raining = oldRain; Main.bloodMoon = oldBloodMoon;
			for (int i = 0; i < Netplay.Clients.Length; i++) Netplay.Clients[i] = oldClients[i];
		}
	}
}
