#nullable enable
using System;
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
using Terraria.ModLoader.IO;
using Terraria.Net;

namespace SoulmatesRegressionProbe;

public sealed partial class EngineChecks
{
	private static void CheckCritterBetaRefinement(Action<bool, string> check, Mod mod)
	{
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
		Player[] oldPlayers = Main.player; NPC[] oldNpcs = Main.npc;
		Item[] oldItems = Main.item; Projectile[] oldProjectiles = Main.projectile; Tilemap oldTiles = Main.tile;
		int oldWidth = Main.maxTilesX, oldHeight = Main.maxTilesY, oldMode = Main.netMode, oldLocal = Main.myPlayer;
		bool oldDedicated = Main.dedServ, oldMenu = Main.gameMenu;
		var oldRandom = Main.rand; var oldView = Main.GameViewMatrix;
		RemoteClient[] oldClients = (RemoteClient[])Netplay.Clients.Clone();
		try {
			Main.rand = new Terraria.Utilities.UnifiedRandom(20261011);
			Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(null!);
			Main.GameViewMatrix.SetViewportOverride(new Microsoft.Xna.Framework.Graphics.Viewport(0, 0, 1280, 720));
			Main.maxTilesX = 200; Main.maxTilesY = 120; Main.dedServ = true; Main.gameMenu = false;
			Main.tile = (Tilemap)Activator.CreateInstance(typeof(Tilemap), flags, null,
				new object[] { (ushort)200, (ushort)120 }, null)!;
			for (int i = 0; i < Netplay.Clients.Length; i++) Netplay.Clients[i] = new RemoteClient { Socket = new ProbeSocket() };
			foreach (int mode in new[] { NetmodeID.SinglePlayer, NetmodeID.Server }) {
				Main.netMode = mode; Main.myPlayer = mode == NetmodeID.Server ? 255 : 0;
				Main.player = Enumerable.Range(0, oldPlayers.Length).Select(i => new Player { whoAmI = i }).ToArray();
				Main.npc = Enumerable.Range(0, oldNpcs.Length).Select(i => new NPC { whoAmI = i }).ToArray();
				Main.item = Enumerable.Range(0, oldItems.Length).Select(i => new Item { whoAmI = i }).ToArray();
				Main.projectile = Enumerable.Range(0, oldProjectiles.Length).Select(i => new Projectile { whoAmI = i }).ToArray();
				Player owner = Main.player[0]; owner.active = true; owner.Center = new Vector2(640, 640);
				NPC npc = Main.npc[20]; npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
				npc.active = true; npc.ai[0] = 0; npc.Center = owner.Center;
				var mate = (SoulboundCompanion)npc.ModNPC;
				owner.inventory[0] = new Item(ModContent.ItemType<SoulboundSigil>());
				((SoulboundSigil)owner.inventory[0].ModItem).Profile = mate.Profile.Clone();
				mate.Profile.QuestionCadence = CompanionQuestionCadence.Quiet;
				object? Call(string name, params object[] args) => typeof(SoulboundCompanion).GetMethod(name, flags)!.Invoke(mate, args);
				object? Get(string name) => typeof(SoulboundCompanion).GetField(name, flags)!.GetValue(mate);
				void Set(string name, object value) => typeof(SoulboundCompanion).GetField(name, flags)!.SetValue(mate, value);
				NPC Critter(int type = NPCID.Bunny) {
					NPC animal = Main.npc[19]; animal.SetDefaults(type); animal.active = true; animal.Center = npc.Center;
					return animal;
				}
				CompanionConversationResult Request(CompanionNpcAction action, NPC animal) =>
					(CompanionConversationResult)Call("PerformNpcContext", action, animal.whoAmI, animal.type)!;
				bool Joined(NPC animal) => (bool)typeof(CompanionCritterCompany).GetMethod("BelongsTo", flags)!
					.Invoke(animal.GetGlobalNPC<CompanionCritterCompany>(), new object[] { mate })!;
				int Losses() => mate.Profile.Memories.Count(memory => memory.Kind == CompanionMemoryKind.CritterLost);
				Set("speechTimer", 1000);

				NPC bunny = Critter(); mate.Profile.AutonomyEnabled = true;
				mate.Profile.CritterMode = CompanionCritterMode.Company;
				Type activities = typeof(SoulboundCompanion).GetNestedType("AutonomyActivity", BindingFlags.NonPublic)!;
				Type forest = typeof(SoulboundCompanion).GetNestedType("ForestAction", BindingFlags.NonPublic)!;
				object invite = Enum.Parse(activities, "InviteCritter"), noForest = Enum.Parse(forest, "None");
				Call("ConsiderInitiative", invite, -1, Point.Zero, noForest, bunny);
				Guid permission = mate.InitiativeId;
				check(mate.HasPendingInitiative && permission != Guid.Empty, $"Critter beta consent fixture failed: {mode}");
				bunny = Critter();
				check(!mate.RespondToInitiative(permission, CompanionInitiativeResponse.Always)
					&& !mate.HasPendingInitiative && mate.Profile.CritterCompanyInitiative == CompanionInitiativePolicy.Ask,
					$"Same-object same-species reset inherited an unanswered permission: {mode}");
				mate.Profile.AutonomyEnabled = false; mate.Profile.CritterCompanyInitiative = CompanionInitiativePolicy.Never;
				check(Request(CompanionNpcAction.Company, bunny).Accepted, $"Direct Company incorrectly requires automatic permission: {mode}");
				bunny = Critter();
				check(!(bool)Call("UpdateCritterActivity")! && Get("critterTarget") is null && !Joined(bunny)
					&& !(bool)Get("directedCritterVisit")!, $"Same-object reset inherited a directed visit: {mode}");
				check(Request(CompanionNpcAction.Company, bunny).Accepted, $"Fresh explicit Company refused after reset: {mode}");
				Main.netMode = NetmodeID.MultiplayerClient;
				check((bool)Call("UpdateCritterActivity")! && ReferenceEquals(Get("critterTarget"), bunny) && !Joined(bunny),
					$"Client completed or discarded an authoritative visit on reaching its target: {mode}");
				Main.netMode = mode;
				check((bool)Call("UpdateCritterActivity")! && Joined(bunny)
					&& mate.Profile.CritterCompanyInitiative == CompanionInitiativePolicy.Never,
					$"Fresh direct Company failed or changed automatic permission: {mode}");

				CompanionCritterCompany company = bunny.GetGlobalNPC<CompanionCritterCompany>();
				bunny.Center = npc.Center + new Vector2(-190, 200); bunny.velocity = new Vector2(-1, -3);
				bunny.noGravity = false; company.PostAI(bunny);
				check(bunny.velocity == new Vector2(-1, -3) && !bunny.noTileCollide,
					$"Ground critter steered toward a vertically unreachable companion despite line of sight: {mode}");
				bunny.Center = npc.Center + new Vector2(-190, 0); bunny.velocity = new Vector2(-1, -3); company.PostAI(bunny);
				check(bunny.velocity.X > 0 && bunny.velocity.X <= 2.4f && bunny.velocity.Y == -3,
					$"Ground company did not follow within native horizontal bounds or replaced native vertical motion: {mode}");
				if (mode == NetmodeID.Server) {
					owner.selectedItem = 25; owner.inventory[25] = new Item(ItemID.Wood, 3); mate.StoreSelectedItem();
					bunny.velocity = new Vector2(-1, -3); company.PostAI(bunny);
					check(bunny.velocity == new Vector2(-1, -3) && Joined(bunny),
						"Pending native inventory receipt did not suspend company steering");
					NPC waiting = Main.npc[18]; waiting.SetDefaults(NPCID.Firefly); waiting.active = true; waiting.Center = npc.Center;
					Set("insectCatchCooldown", 0);
					check(!(bool)Call("TryCatchCritter", false, waiting)! && waiting.active && (int)Get("insectCatchCooldown")! == 0,
						"Pending native inventory receipt allowed a new catch transaction");
					SettleServerInventory(mod); waiting.active = false;
				}
				mate.Profile.WorkPaused = true; bunny.velocity = new Vector2(-1, -3); company.PostAI(bunny);
				check(bunny.velocity == new Vector2(-1, -3) && Joined(bunny), $"Paused Company forced movement or discarded its live critter: {mode}");
				mate.Profile.WorkPaused = false; Call("ReleaseCritterCompany"); bunny = Critter();
				mate.Profile.AutonomyEnabled = true; mate.Profile.CritterCompanyInitiative = CompanionInitiativePolicy.Always;
				Call("BeginAutonomousActivity", invite, -1, Point.Zero, noForest, bunny);
				mate.Profile.CritterCompanyInitiative = CompanionInitiativePolicy.Never;
				check(!(bool)Call("UpdateCritterActivity")! && !Joined(bunny) && Get("critterTarget") is null,
					$"Revoked automatic permission still recruited a critter: {mode}");

				mate.Profile.CritterMode = CompanionCritterMode.Watch; Set("critterWatchCooldown", 0); Set("speechTimer", 1000);
				Call("UpdateCritterWatch"); check(ReferenceEquals(Get("pendingCritterNotice"), bunny), $"Critter beta notice fixture failed: {mode}");
				bunny = Critter(); check(Get("pendingCritterNotice") is null, $"Same-object reset retained a queued greeting: {mode}");
				Set("critterWatchCooldown", 0); Call("UpdateCritterWatch");
				NPC replacement = Main.npc[19] = new NPC { whoAmI = 19 }; replacement.SetDefaults(NPCID.Bunny);
				replacement.active = true; replacement.Center = npc.Center; Set("speechTimer", 0); Call("UpdateCritterNoticeSpeech");
				check(Get("pendingCritterNotice") is null, $"A displaced NPC object retained a queued greeting: {mode}");

				bunny = replacement; mate.Profile.AutonomyEnabled = false; Set("speechTimer", 1000); Set("critterLossCooldown", 0);
				int losses = Losses(); var experience = mate.Profile.Experience;
				Call("ObserveCritterLoss", bunny, false);
				check(Losses() == losses && (int)Get("pendingCritterLossTicks")! == 0, $"Living critter generated a death reaction: {mode}");
				Set("insectCatchCooldown", 0); int catchType = bunny.catchItem;
				check((bool)Call("TryCatchCritter", false, bunny)! && !bunny.active && bunny.life > 0
					&& mate.Profile.ItemCount(catchType) == 1, $"Native beta catch did not conserve its real drop: {mode}");
				Call("ObserveCritterLoss", bunny, false);
				check(Losses() == losses && (int)Get("pendingCritterLossTicks")! == 0 && mate.Profile.Experience == experience,
					$"Harmless native catch generated loss memory, grief or kill XP: {mode}");
				bunny = Critter(); bunny.StrikeNPC(new NPC.HitInfo { Damage = 1000, HitDirection = 1, HideCombatText = true }, noPlayerInteraction: true);
				SettleServerInventory(mod);
				check(!bunny.active && Losses() == losses + 1 && (int)Get("pendingCritterLossTicks")! > 0
					&& mate.Profile.Experience == experience, $"Actual critter death failed its loss reaction or awarded kill XP: {mode}");
				bunny = Critter(); bunny.catchItem = int.MaxValue;
				check(!CompanionCritters.CanUseNet(bunny, new Item(ItemID.BugNet)), $"Invalid native catch metadata passed the net guard: {mode}");
				bunny.catchItem = ItemID.Bunny;
				check(!CompanionCritters.CanUseNet(bunny, new Item(ItemID.BugNet) { type = int.MaxValue }),
					$"Invalid tool metadata passed the net guard: {mode}");
				bunny.active = false;

				mate.Profile.Store(new Item(ModContent.ItemType<CritterBetaPetItem>()));
				mate.Profile.Store(new Item(ItemID.ZephyrFish));
				int slot = mate.Profile.PetItems.FindIndex(item => item.type == ModContent.ItemType<CritterBetaPetItem>());
				var owned = (CritterBetaPetItem)mate.Profile.PetItems[slot].ModItem; owned.Marker = 17;
				byte[] Token(int petSlot) => (byte[])typeof(CompanionProfile).GetMethod("StorageToken", flags)!
					.Invoke(mate.Profile, new object[] { CompanionStorage.Pets, petSlot })!;
				bool Equip(int petSlot, int type, byte[] token) => (bool)Call("ConfigurePet", petSlot, type, token)!;
				Projectile[] Pets() => Main.projectile.Where(p => p.active && p.ModProjectile is CompanionFamiliar).ToArray();
				int type = mate.Profile.PetItems[slot].type; byte[] staleToken = Token(slot); owned.Marker = 18;
				check(!Equip(slot, type, staleToken) && mate.Profile.PetItemType == 0 && Pets().Length == 0,
					$"Changed pet item metadata inherited an old selection token: {mode}");
				check(Equip(slot, type, Token(slot)) && Pets().Length == 2 && owned.Marker == 18,
					$"Valid pet metadata selection was refused or mutated: {mode}");
				mate.Profile.PetItems.Add(new Item(type) { stack = 0 }); int emptySlot = mate.Profile.PetItems.Count - 1;
				check(!Equip(emptySlot, type, Token(emptySlot)) && Pets().Length == 2 && mate.Profile.PetItemType == type,
					$"Empty pet slot borrowed authorization from another owned item of its type: {mode}");
				mate.Profile.PetItems.RemoveAt(emptySlot);
				using (var stream = new MemoryStream()) {
					using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) mate.Profile.Write(writer);
					stream.Position = 0; using var reader = new BinaryReader(stream);
					foreach (CompanionProfile copy in new[] { mate.Profile.Clone(), CompanionProfile.Load(mate.Profile.Save()), CompanionProfile.Read(reader) })
						check(copy.PetItemType == type && copy.PetItems.Single(item => item.type == type).ModItem is CritterBetaPetItem { Marker: 18 }
							&& !ReferenceEquals(copy.PetItems.Single(item => item.type == type), mate.Profile.PetItems[slot]),
							$"Pet item metadata or selection failed clone/save/wire round trip: {mode}");
					check(stream.Position == stream.Length, $"Pet beta profile transport did not consume exactly its bytes: {mode}");
				}
				int nativeIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero, ProjectileID.ZephyrFish, 0, 0f, owner.whoAmI);
				Projectile nativePet = Main.projectile[nativeIndex];
				int otherIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
					ModContent.ProjectileType<CompanionFamiliar>(), 0, 0f, owner.whoAmI);
				Projectile otherPet = Main.projectile[otherIndex]; otherPet.ai[0] = 21;
				Projectile pet = Pets().Single(p => p.ai[0] == npc.whoAmI && p.ai[1] == type);
				owner.dead = true; Call("UpdateEquippedPet");
				check(!pet.active && nativePet.active && otherPet.active && mate.Profile.PetItemType == type && owned.Marker == 18,
					$"Pet update retained a dead owner's familiar, culled an unrelated pet or lost its item: {mode}");
				owner.dead = false; Call("UpdateEquippedPet"); pet = Pets().Single(p => p.ai[0] == npc.whoAmI && p.ai[1] == type);
				npc.active = false;
				check(!Equip(slot, type, Token(slot)), $"Inactive companion accepted a pet configuration: {mode}");
				Call("UpdateEquippedPet");
				check(!pet.active && nativePet.active && otherPet.active, $"Inactive companion retained its familiar or culled other pets: {mode}");
				npc.active = true; Call("UpdateEquippedPet"); pet = Pets().Single(p => p.ai[0] == npc.whoAmI && p.ai[1] == type);
				Guid originalId = mate.Profile.Id; mate.Profile.Id = Guid.NewGuid(); Call("UpdateEquippedPet");
				check(Pets().Count(p => p.ai[0] == npc.whoAmI) == 2 && nativePet.active && otherPet.active,
					$"Pet update retained a stale companion identity in the reused slot: {mode}");
				mate.Profile.Id = originalId; Call("UpdateEquippedPet"); pet = Pets().Single(p => p.ai[0] == npc.whoAmI && p.ai[1] == type);
				Main.netMode = NetmodeID.MultiplayerClient; mate.Profile.PetItemType = 0; pet.ModProjectile.AI();
				check(pet.active && !(bool)typeof(CompanionFamiliar).GetField("readyToDraw", flags)!.GetValue(pet.ModProjectile)!,
					$"Client drew a familiar while waiting for a matching selected-pet profile: {mode}");
				mate.Profile.PetItemType = type; pet.ModProjectile.AI();
				check(pet.active && (bool)typeof(CompanionFamiliar).GetField("readyToDraw", flags)!.GetValue(pet.ModProjectile)!,
					$"Client did not restore its familiar after delayed metadata arrived: {mode}");
				Main.netMode = mode;
				int fishSlot = mate.Profile.PetItems.FindIndex(item => item.type == ItemID.ZephyrFish);
				check(Equip(fishSlot, ItemID.ZephyrFish, Token(fishSlot)) && Pets().Count(p => p.ai[0] == npc.whoAmI) == 2
					&& Pets().Count(p => p.ai[0] == npc.whoAmI && p.ai[1] == ItemID.ZephyrFish) == 1
					&& nativePet.active && otherPet.active && owned.Marker == 18 && mate.Profile.ItemCount(type) == 1,
					$"Pet switching duplicated familiars, lost metadata or culled another pet: {mode}");
				check(Equip(-1, 0, new byte[32]) && Pets().All(p => p.ai[0] != npc.whoAmI)
					&& nativePet.active && otherPet.active && mate.Profile.ItemCount(type) == 1,
					$"Pet dismissal lost its real item or culled another pet: {mode}");
				SettleServerInventory(mod);
			}
		}
		finally {
			Main.player = oldPlayers; Main.npc = oldNpcs; Main.item = oldItems; Main.projectile = oldProjectiles; Main.tile = oldTiles;
			Main.maxTilesX = oldWidth; Main.maxTilesY = oldHeight; Main.netMode = oldMode; Main.myPlayer = oldLocal;
			Main.dedServ = oldDedicated; Main.gameMenu = oldMenu; Main.rand = oldRandom; Main.GameViewMatrix = oldView;
			for (int i = 0; i < Netplay.Clients.Length; i++) Netplay.Clients[i] = oldClients[i];
		}
	}
}

public sealed class CritterBetaPetItem : ModItem
{
	public int Marker;
	public override string Texture => $"Terraria/Images/Item_{ItemID.Carrot}";
	public override void SetDefaults()
	{
		Item.DefaultToVanitypet(ProjectileID.Bunny, BuffID.PetBunny);
	}
	public override void SaveData(TagCompound tag) => tag["marker"] = Marker;
	public override void LoadData(TagCompound tag) => Marker = tag.GetInt("marker");
	public override void NetSend(BinaryWriter writer) => writer.Write(Marker);
	public override void NetReceive(BinaryReader reader) => Marker = reader.ReadInt32();
}
