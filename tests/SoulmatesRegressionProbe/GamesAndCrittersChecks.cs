#nullable enable
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Common.UI;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.Net;

namespace SoulmatesRegressionProbe;

public sealed partial class EngineChecks
{
	private static void CheckGamesAndCritters(Action<bool, string> check)
	{
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
		Player oldPlayer = Main.player[0]; NPC[] oldNpcs = Main.npc; Item[] oldItems = Main.item;
		Tilemap oldMap = Main.tile;
		int oldWidth = Main.maxTilesX, oldHeight = Main.maxTilesY, oldMode = Main.netMode, oldLocal = Main.myPlayer;
		RemoteClient[] oldClients = (RemoteClient[])Netplay.Clients.Clone();
		var bubbles = (IDictionary)typeof(EmoteBubble).GetField("byID", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(null)!;
		try {
			for (int i = 0; i < Netplay.Clients.Length; i++) Netplay.Clients[i] = new RemoteClient { Socket = new ProbeSocket() };
			foreach (int mode in new[] { NetmodeID.SinglePlayer, NetmodeID.Server }) {
				Main.netMode = mode; Main.myPlayer = 255;
				Main.maxTilesX = Main.maxTilesY = 100;
				Main.tile = (Tilemap)Activator.CreateInstance(typeof(Tilemap), flags, null, new object[] { (ushort)100, (ushort)100 }, null)!;
				Main.npc = Enumerable.Range(0, oldNpcs.Length).Select(i => new NPC { whoAmI = i }).ToArray();
				Main.item = Enumerable.Range(0, oldItems.Length).Select(i => new Item { whoAmI = i }).ToArray();
				var owner = new Player { whoAmI = 0, active = true, Center = new Vector2(700, 500) };
				Main.player[0] = owner;
				NPC npc = Main.npc[20]; npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
				npc.active = true; npc.ai[0] = 0; npc.Center = owner.Center;
				var mate = (SoulboundCompanion)npc.ModNPC;
				owner.inventory[0] = new Item(ModContent.ItemType<SoulboundSigil>());
				((SoulboundSigil)owner.inventory[0].ModItem).Profile = mate.Profile.Clone();
				void Set(string field, object value) => typeof(SoulboundCompanion).GetField(field, flags)!.SetValue(mate, value);
				object? Get(string field) => typeof(SoulboundCompanion).GetField(field, flags)!.GetValue(mate);
				object? Call(string method, params object[] args) => typeof(SoulboundCompanion).GetMethod(method, flags)!.Invoke(mate, args);
				NPC Critter(int type) {
					NPC critter = Main.npc[19]; critter.SetDefaults(type); critter.active = true; critter.Center = npc.Center;
					return critter;
				}
				Item baseline = (Item)Call("EffectiveCritterNet")!;
				check(baseline.type == ItemID.BugNet && ReferenceEquals(baseline, Call("EffectiveCritterNet"))
					&& mate.Profile.PackLoad == 0, "Basic net capability was absent, reallocated or consumed an equipment slot");
				NPC bunny = Critter(NPCID.Bunny); Vector2 position = bunny.position; int life = bunny.life;
				Set("personalQuestionCooldown", 0);
				check((bool)Call("BeginChoiceQuestion", CompanionQuestion.Company)!, "Critter observation fixture could not open a question");
				int before = bubbles.Count;
				Call("UpdateCritterWatch");
				check(ReferenceEquals(Get("pendingCritterNotice"), bunny) && bubbles.Count == before && mate.HasPendingQuestion,
					"Watch ignored a critter during a question or overwrote the question's emotes");
				Call("ClearChoiceQuestion"); Set("speechTimer", 0); Set("socialTimer", 0);
				Call("ClearNativeExpression");
				Set("ambientStoryCooldown", 0); Set("personalQuestionCooldown", 0); Set("companyQuestionCooldown", 0);
				Call("UpdateAutonomousSocialBehavior"); Call("UpdateAmbientStories"); Call("UpdateChoiceConversation");
				check((int)Get("speechTimer")! == 0 && !mate.HasPendingQuestion && ReferenceEquals(Get("pendingCritterNotice"), bunny),
					"New ambient speech or a personal question stole a queued critter greeting");
				RecoveryPacketSpy.Watching = true; RecoveryPacketSpy.Packets.Clear();
				Call("UpdateCritterNoticeSpeech");
				check(Get("pendingCritterNotice") is null && (int)Get("speechTimer")! > 0
					&& ((int[])Get("nativeExpression")!).SequenceEqual(new[] { EmoteID.CritterBunny, EmoteID.EmoteWink }),
					"Delivered critter greeting omitted its species-and-greeting expression");
				check(bubbles.Values.Cast<EmoteBubble>().Any(b => ReferenceEquals(b.anchor.entity, bunny)
					&& b.emote == EmoteID.EmoteHappiness && b.lifeTime == 180), "The greeted critter gave no visible native reply");
				if (mode == NetmodeID.Server)
					check(RecoveryPacketSpy.Packets.Count(p => p.Type == MessageID.SyncEmoteBubble) >= 2,
						"Authority did not synchronize both sides of the critter greeting");
				RecoveryPacketSpy.Watching = false; RecoveryPacketSpy.Packets.Clear();
				check(bunny.active && bunny.life == life && bunny.position == position && mate.Profile.PackLoad == 0,
					"Watch moved, captured or damaged the greeted critter");
				before = bubbles.Count;
				for (int tick = 0; tick < 120; tick++) Call("UpdateCritterWatch");
				check(bubbles.Count == before, "Watch emitted repeated greeting bubbles during its cooldown");
				for (int tick = 0; tick < 180; tick++) Call("UpdateNativeExpression");
				check(EmoteBubble.GetExistingEmoteBubble((int)Get("activeNativeBubble")!)!.emote == EmoteID.EmoteWink,
					"Critter expression never advanced from species to greeting");
				Set("critterWatchCooldown", 0); Set("speechTimer", 100);
				Call("UpdateCritterWatch"); bunny.Center += new Vector2(450, 0);
				before = bubbles.Count; Set("speechTimer", 0); Call("UpdateCritterNoticeSpeech");
				check(Get("pendingCritterNotice") is null && bubbles.Count == before, "A departed critter received a stale reply");
				bunny.Center = npc.Center;
				Call("ClearNativeExpression"); Set("critterWatchCooldown", 0); Call("UpdateCritterWatch");
				mate.PerformQuickAction(CompanionQuickAction.CritterOff);
				check(Get("pendingCritterNotice") is null, "Off retained an undelivered critter greeting");

				NPC lava = Critter(NPCID.Lavafly); Set("insectCatchCooldown", 0);
				check(!(bool)Call("TryCatchCritter", false, lava)! && lava.active,
					"Built-in ordinary net bypassed native lava restrictions");
				mate.Profile.Store(new Item(ItemID.GoldenBugNet));
				Item upgrade = (Item)Call("EffectiveCritterNet")!;
				check(upgrade.type == ItemID.GoldenBugNet && !ReferenceEquals(upgrade, baseline)
					&& mate.Profile.ItemCount(ItemID.BugNet) == 0, "A carried lava-proof upgrade failed to replace the baseline capability");
				Set("insectCatchCooldown", 0); int caughtType = lava.catchItem;
				check((bool)Call("TryCatchCritter", false, lava)! && !lava.active && mate.Profile.ItemCount(caughtType) == 1,
					"Carried lava-proof net failed a real native catch or its cargo receipt");
				check(Main.item.All(item => !item.active || item.type != caughtType), "Native catch left a duplicate loose drop");
				mate.Profile.ClearCargo(); check(ReferenceEquals(baseline, Call("EffectiveCritterNet")),
					"Removing the carried upgrade lost the cached baseline net");
				Main.netMode = NetmodeID.MultiplayerClient; bunny = Critter(NPCID.Bunny); Set("insectCatchCooldown", 0);
				check(!(bool)Call("TryCatchCritter", false, bunny)! && bunny.active && mate.Profile.PackLoad == 0,
					"A client used its built-in net without authoritative permission");
				Main.netMode = mode;
				foreach (int type in new[] { NPCID.Bunny, NPCID.Squirrel, NPCID.SquirrelRed, NPCID.Penguin, NPCID.PenguinBlack,
					NPCID.Bird, NPCID.BirdBlue, NPCID.BirdRed, NPCID.Butterfly, NPCID.Firefly, NPCID.LightningBug,
					NPCID.Grasshopper, NPCID.Duck, NPCID.DuckWhite, NPCID.Worm, NPCID.GoldBunny }) {
					NPC lost = Critter(type); Set("critterLossCooldown", 0); Set("speechTimer", 0);
					SettleServerInventory(ModLoader.GetMod("Soulmates"));
					Set("critterCareCooldown", 20000);
					check(CompanionCritters.IsNatural(lost), "Native critter-death fixture was excluded: " + type);
					int experience = mate.Profile.Experience;
					lost.StrikeNPC(new NPC.HitInfo { Damage = 1000, HitDirection = 1, HideCombatText = true }, noPlayerInteraction: true);
					SettleServerInventory(ModLoader.GetMod("Soulmates"));
					check(!lost.active && (string)Get("speechText")! == SoulmatesText.Get($"Social.Critters.Loss.{mate.Profile.Personality}", lost.TypeName)
						&& mate.Profile.Experience == experience, "Native death hook missed a critter or awarded XP: " + type + "/" + mode);
				}
			}
		}
		finally {
			RecoveryPacketSpy.Watching = false; RecoveryPacketSpy.Packets.Clear();
			Main.player[0] = oldPlayer; Main.npc = oldNpcs; Main.item = oldItems; Main.tile = oldMap;
			Main.maxTilesX = oldWidth; Main.maxTilesY = oldHeight; Main.netMode = oldMode; Main.myPlayer = oldLocal;
			for (int i = 0; i < Netplay.Clients.Length; i++) Netplay.Clients[i] = oldClients[i];
		}
	}

	private static void CheckTreeContext(Action<bool, string> check)
	{
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
		Player oldPlayer = Main.player[0]; NPC[] oldNpcs = Main.npc; Item[] oldItems = Main.item;
		Tilemap oldMap = Main.tile;
		int oldWidth = Main.maxTilesX, oldHeight = Main.maxTilesY, oldMode = Main.netMode, oldLocal = Main.myPlayer;
		bool oldDedicated = Main.dedServ, oldMenu = Main.gameMenu, oldInventory = Main.playerInventory;
		try {
			Main.netMode = NetmodeID.SinglePlayer; Main.myPlayer = 0;
			Main.maxTilesX = Main.maxTilesY = 100;
			Main.tile = (Tilemap)Activator.CreateInstance(typeof(Tilemap), flags, null, new object[] { (ushort)100, (ushort)100 }, null)!;
			Main.npc = Enumerable.Range(0, oldNpcs.Length).Select(i => new NPC { whoAmI = i }).ToArray();
			Main.item = Enumerable.Range(0, oldItems.Length).Select(i => new Item { whoAmI = i }).ToArray();
			var owner = new Player { whoAmI = 0, active = true, Center = new Point(37, 34).ToWorldCoordinates() };
			Main.player[0] = owner;
			NPC npc = Main.npc[20]; npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
			npc.active = true; npc.ai[0] = 0; npc.Center = owner.Center;
			var mate = (SoulboundCompanion)npc.ModNPC; mate.Profile.Name = "AETHER";
			owner.inventory[0] = new Item(ModContent.ItemType<SoulboundSigil>());
			((SoulboundSigil)owner.inventory[0].ModItem).Profile = mate.Profile.Clone();
			Type snapshotType = typeof(CompanionWheelSystem).Assembly.GetType("Soulmates.Common.UI.SoulwheelTarget")!;
			var wheel = new CompanionWheelSystem();
			Main.dedServ = Main.gameMenu = Main.playerInventory = false;
			void Tree(ushort type, ushort soil, int x = 40) {
				for (int y = 35; y < 40; y++) {
					Tile stem = Main.tile[x, y]; stem.HasTile = true; stem.TileType = type; stem.TileFrameX = stem.TileFrameY = 0;
				}
				Tile ground = Main.tile[x, 40]; ground.HasTile = true; ground.TileType = soil;
			}
			foreach ((ushort type, ushort soil) in new[] { (TileID.Trees, TileID.Grass), (TileID.Trees, TileID.SnowBlock),
				(TileID.Trees, TileID.CorruptGrass), (TileID.Trees, TileID.CrimsonGrass), (TileID.Trees, TileID.JungleGrass),
				(TileID.Trees, TileID.HallowedGrass), (TileID.PalmTree, TileID.Sand), (TileID.TreeAsh, TileID.AshGrass),
				(TileID.VanityTreeSakura, TileID.Grass), (TileID.VanityTreeYellowWillow, TileID.Grass),
				(TileID.TreeDiamond, TileID.Stone), (TileID.MushroomTrees, TileID.MushroomGrass) }) {
				Tree(type, soil);
				object snapshot = Activator.CreateInstance(snapshotType, new Point(40, 36).ToWorldCoordinates(), npc.whoAmI)!;
				check((bool)snapshotType.GetProperty("CanOfferFallback")!.GetValue(snapshot)!
					&& (int)snapshotType.GetProperty("Emote")!.GetValue(snapshot)! == EmoteID.MiscTree,
					"Native tree family lost its fallback context or tree symbol: " + type + "/" + soil);
				typeof(SoulboundCompanion).GetField("treeShakeCooldown", flags)!.SetValue(mate, 600);
				wheel.OpenContext(mate, new Point(40, 36).ToWorldCoordinates(), new Vector2(400, 300));
				var actions = (IEnumerable)typeof(CompanionWheelSystem).GetField("contextActions", flags)!.GetValue(wheel)!;
				check(wheel.IsOpen && actions.Cast<object>().Any(action => action.ToString() == "Forest"),
					"Shake cooldown hid Forestry for a native tree family: " + type + "/" + soil);
				wheel.Close();
			}
			Main.dedServ = true;
			Tree(TileID.TreeDiamond, TileID.Stone);
			typeof(SoulboundCompanion).GetField("treeShakeCooldown", flags)!.SetValue(mate, 0);
			object[] resolve = { new Point(40, 36), Point.Zero, Enum.ToObject(typeof(SoulboundCompanion).GetMethod("TryResolveForestTarget", flags)!.GetParameters()[2].ParameterType.GetElementType()!, 0) };
			check(!(bool)typeof(SoulboundCompanion).GetMethod("TryResolveForestTarget", flags)!.Invoke(mate, resolve)!,
				"Unsupported native gem-tree soil became a shake assignment");
			Tree(TileID.Trees, TileID.SnowBlock, 43);
			object[] find = { owner.Center, Point.Zero, resolve[2] };
			check((bool)typeof(SoulboundCompanion).GetMethod("FindForestTask", flags)!.Invoke(mate, find)!
				&& (Point)find[1] == new Point(43, 40), "An unsupported nearer tree starved an actionable snow tree");
		}
		finally {
			Main.player[0] = oldPlayer; Main.npc = oldNpcs; Main.item = oldItems; Main.tile = oldMap;
			Main.maxTilesX = oldWidth; Main.maxTilesY = oldHeight; Main.netMode = oldMode; Main.myPlayer = oldLocal;
			Main.dedServ = oldDedicated; Main.gameMenu = oldMenu; Main.playerInventory = oldInventory;
		}
	}

	private static void CheckCritterPermissions(Action<bool, string> check)
	{
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
		foreach (CompanionInitiativeKind kind in Enum.GetValues<CompanionInitiativeKind>()) {
			check(CompanionProfile.Load(new TagCompound()).GetInitiativePolicy(kind) == CompanionInitiativePolicy.Ask,
				"A legacy profile failed to default a registered ability to Ask: " + kind);
			foreach (CompanionInitiativePolicy policy in Enum.GetValues<CompanionInitiativePolicy>()) {
				var profile = new CompanionProfile(); profile.SetInitiativePolicy(kind, policy);
				check(profile.Clone().GetInitiativePolicy(kind) == policy && CompanionProfile.Load(profile.Save()).GetInitiativePolicy(kind) == policy,
					"Ability consent was not wired to clone and save: " + kind);
				using var stream = new MemoryStream();
				using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) profile.Write(writer);
				stream.Position = 0; using var reader = new BinaryReader(stream);
				check(CompanionProfile.Read(reader).GetInitiativePolicy(kind) == policy && stream.Position == stream.Length,
					"Ability consent was not wired to multiplayer serialization: " + kind);
				check(Enum.GetValues<CompanionInitiativeKind>().Where(other => other != kind)
					.All(other => profile.GetInitiativePolicy(other) == CompanionInitiativePolicy.Ask), "Consent leaked into another ability: " + kind);
				profile.ResetInitiativePolicies();
				check(profile.GetInitiativePolicy(kind) == CompanionInitiativePolicy.Ask, "Reset omitted an ability: " + kind);
			}
		}
		check(new CompanionProfile().GetInitiativePolicy((CompanionInitiativeKind)255) == CompanionInitiativePolicy.Never,
			"An unregistered future ability inherited permission from Gathering");
		Type activityType = typeof(SoulboundCompanion).GetNestedType("AutonomyActivity", BindingFlags.NonPublic)!;
		var mappedKinds = Enum.GetValues(activityType).Cast<object>().Where(activity => activity.ToString() != "None")
			.Select(activity => (CompanionInitiativeKind)typeof(SoulboundCompanion).GetMethod("InitiativeKindFor", flags | BindingFlags.Static)!.Invoke(null, new[] { activity })!).ToArray();
		check(mappedKinds.Distinct().Count() == Enum.GetValues<CompanionInitiativeKind>().Length,
			"An autonomous ability is missing its explicit consent registration");
		var oldView = Main.GameViewMatrix;
		Player oldPlayer = Main.player[0]; NPC[] oldNpcs = Main.npc; Item[] oldItems = Main.item;
		Tilemap oldMap = Main.tile;
		int oldWidth = Main.maxTilesX, oldHeight = Main.maxTilesY, oldMode = Main.netMode, oldLocal = Main.myPlayer;
		bool oldDedicated = Main.dedServ, oldMenu = Main.gameMenu, oldInventory = Main.playerInventory;
		RemoteClient[] oldClients = (RemoteClient[])Netplay.Clients.Clone();
		try {
			Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(null!);
			Main.GameViewMatrix.SetViewportOverride(new Microsoft.Xna.Framework.Graphics.Viewport(0, 0, 1280, 720));
			for (int i = 0; i < Netplay.Clients.Length; i++) Netplay.Clients[i] = new RemoteClient { Socket = new ProbeSocket() };
			foreach (int mode in new[] { NetmodeID.SinglePlayer, NetmodeID.Server })
			foreach (CompanionCritterMode behavior in new[] { CompanionCritterMode.Company, CompanionCritterMode.Collect })
			foreach (CompanionInitiativeResponse answer in Enum.GetValues<CompanionInitiativeResponse>()) {
				Main.netMode = mode; Main.myPlayer = 0; Main.gameMenu = Main.playerInventory = false;
				Main.dedServ = mode == NetmodeID.Server;
				Main.maxTilesX = Main.maxTilesY = 100;
				Main.tile = (Tilemap)Activator.CreateInstance(typeof(Tilemap), flags, null, new object[] { (ushort)100, (ushort)100 }, null)!;
				Main.npc = Enumerable.Range(0, oldNpcs.Length).Select(i => new NPC { whoAmI = i }).ToArray();
				Main.item = Enumerable.Range(0, oldItems.Length).Select(i => new Item { whoAmI = i }).ToArray();
				var owner = new Player { whoAmI = 0, active = true, Center = new Vector2(700, 500) };
				Main.player[0] = owner;
				NPC npc = Main.npc[20]; npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
				npc.active = true; npc.ai[0] = 0; npc.Center = owner.Center;
				var mate = (SoulboundCompanion)npc.ModNPC; mate.Profile.Name = "AETHER";
				mate.Profile.CritterMode = behavior; mate.Profile.QuestionCadence = CompanionQuestionCadence.Quiet;
				owner.inventory[0] = new Item(ModContent.ItemType<SoulboundSigil>());
				((SoulboundSigil)owner.inventory[0].ModItem).Profile = mate.Profile.Clone();
				void Set(string field, object value) => typeof(SoulboundCompanion).GetField(field, flags)!.SetValue(mate, value);
				object? Get(string field) => typeof(SoulboundCompanion).GetField(field, flags)!.GetValue(mate);
				void Activity() => typeof(SoulboundCompanion).GetMethod("UpdateCritterActivity", flags)!.Invoke(mate, null);
				bool Assigned(NPC critter) => (bool)typeof(CompanionCritterCompany).GetProperty("IsAssigned", flags)!
					.GetValue(critter.GetGlobalNPC<CompanionCritterCompany>())!;
				NPC Bunny(int index) {
					NPC bunny = Main.npc[index]; bunny.SetDefaults(NPCID.Bunny); bunny.active = true; bunny.Center = npc.Center;
					return bunny;
				}
				ModContent.GetInstance<InitiativePromptSystem>().Close(); ModContent.GetInstance<CompanionWheelSystem>().Close();
				NPC target = Bunny(19); Set("speechTimer", 1000);
				CompanionInitiativeKind kind = behavior == CompanionCritterMode.Company
					? CompanionInitiativeKind.CritterCompany : CompanionInitiativeKind.CritterCollect;
				Activity(); Guid token = mate.InitiativeId;
				check(mate.HasPendingInitiative && mate.PendingInitiativeKind == kind && token != Guid.Empty && target.active
					&& Get("critterTarget") is null && mate.Profile.ItemCount(ItemID.Bunny) == 0,
					"Automatic critter action bypassed Ask: " + behavior + "/" + mode);
				check(!mate.RespondToInitiative(Guid.NewGuid(), answer) && mate.HasPendingInitiative,
					"A forged token answered a critter permission prompt");
				check(mate.RespondToInitiative(token, answer) && !mate.RespondToInitiative(token, answer),
					"Critter permission was rejected or could be answered twice: " + answer);
				ModContent.GetInstance<InitiativePromptSystem>().Close();
				if (answer is CompanionInitiativeResponse.Yes or CompanionInitiativeResponse.Always) {
					Activity();
					check(behavior == CompanionCritterMode.Collect ? !target.active && mate.Profile.ItemCount(ItemID.Bunny) == 1
						: Assigned(target),
						"Approved critter action did not execute its native operation: " + behavior + "/" + answer);
				}
				else check(target.active && !Assigned(target) && Get("critterTarget") is null,
					"Declined critter action still changed the creature: " + answer);
				CompanionInitiativePolicy policy = answer == CompanionInitiativeResponse.Always ? CompanionInitiativePolicy.Always
					: answer == CompanionInitiativeResponse.Never ? CompanionInitiativePolicy.Never : CompanionInitiativePolicy.Ask;
				check(mate.Profile.GetInitiativePolicy(kind) == policy && CompanionProfile.Load(mate.Profile.Save()).GetInitiativePolicy(kind) == policy
					&& mate.Profile.Clone().GetInitiativePolicy(kind) == policy,
					"Critter consent lost its independent persistent rule");
				CompanionInitiativeKind other = kind == CompanionInitiativeKind.CritterCompany
					? CompanionInitiativeKind.CritterCollect : CompanionInitiativeKind.CritterCompany;
				check(mate.Profile.GetInitiativePolicy(other) == CompanionInitiativePolicy.Ask,
					"Company consent silently granted catching, or catching changed company consent");
				NPC next = Bunny(18); Set("critterDecisionTimer", 0); Set("insectCatchCooldown", 0);
				Activity();
				check(answer == CompanionInitiativeResponse.Always ? !mate.HasPendingInitiative
					&& (behavior == CompanionCritterMode.Collect ? !next.active : Assigned(next))
					: answer == CompanionInitiativeResponse.Yes ? mate.HasPendingInitiative && mate.InitiativeId != token
					: !mate.HasPendingInitiative && next.active && !Assigned(next),
					"Critter Yes/No/Always/Never scope was inconsistent on the next opportunity: " + answer + "/" + behavior);
				if (mate.HasPendingInitiative) {
					Guid stale = mate.InitiativeId; next.SetDefaults(NPCID.Squirrel); next.active = true; next.Center = npc.Center;
					check(!mate.RespondToInitiative(stale, CompanionInitiativeResponse.Always) && !mate.HasPendingInitiative
						&& next.active && mate.Profile.GetInitiativePolicy(kind) == CompanionInitiativePolicy.Ask,
						"A changed critter was caught or granted Always with a stale answer");
				}
				mate.Profile.ResetInitiativePolicies();
				check(mate.Profile.CritterCompanyInitiative == CompanionInitiativePolicy.Ask
					&& mate.Profile.CritterCollectInitiative == CompanionInitiativePolicy.Ask, "Reset did not restore critter consent to Ask");
				ModContent.GetInstance<InitiativePromptSystem>().Close();
			}
		}
		finally {
			ModContent.GetInstance<InitiativePromptSystem>().Close();
			Main.GameViewMatrix = oldView;
			Main.player[0] = oldPlayer; Main.npc = oldNpcs; Main.item = oldItems; Main.tile = oldMap;
			Main.maxTilesX = oldWidth; Main.maxTilesY = oldHeight; Main.netMode = oldMode; Main.myPlayer = oldLocal;
			Main.dedServ = oldDedicated; Main.gameMenu = oldMenu; Main.playerInventory = oldInventory;
			for (int i = 0; i < Netplay.Clients.Length; i++) Netplay.Clients[i] = oldClients[i];
		}
	}
}
