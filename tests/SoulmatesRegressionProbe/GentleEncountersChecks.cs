using System;
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
	private static void CheckGentleEncounters(Action<bool, string> check)
	{
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
		foreach (CompanionQuestionCadence cadence in Enum.GetValues<CompanionQuestionCadence>()) {
			var profile = new CompanionProfile { QuestionCadence = cadence };
			check(profile.Clone().QuestionCadence == cadence && CompanionProfile.Load(profile.Save()).QuestionCadence == cadence,
				"Question cadence lost through clone or save");
			using var stream = new MemoryStream();
			using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) profile.Write(writer);
			stream.Position = 0;
			using var reader = new BinaryReader(stream, Encoding.UTF8, true);
			check(CompanionProfile.Read(reader).QuestionCadence == cadence && stream.Position == stream.Length,
				"Question cadence lost or misaligned the binary profile");
		}
		check(CompanionProfile.Load(new TagCompound()).QuestionCadence == CompanionQuestionCadence.Calm
			&& CompanionProfile.Load(new TagCompound { ["questionCadence"] = (byte)255 }).QuestionCadence == CompanionQuestionCadence.Calm,
			"Legacy or invalid question cadence did not default to Calm");

		Player oldPlayer = Main.player[0]; NPC[] oldNpcs = Main.npc; Item[] oldItems = Main.item;
		Tilemap oldMap = Main.tile;
		int oldWidth = Main.maxTilesX, oldHeight = Main.maxTilesY, oldMode = Main.netMode, oldLocal = Main.myPlayer;
		RemoteClient[] oldClients = (RemoteClient[])Netplay.Clients.Clone();
		try {
			for (int i = 0; i < Netplay.Clients.Length; i++) Netplay.Clients[i] = new RemoteClient { Socket = new ProbeSocket() };
			foreach (int mode in new[] { NetmodeID.SinglePlayer, NetmodeID.Server }) {
				Main.netMode = mode; Main.myPlayer = 255;
				Main.maxTilesX = Main.maxTilesY = 100;
				Main.tile = (Tilemap)Activator.CreateInstance(typeof(Tilemap), flags, null, new object[] { (ushort)100, (ushort)100 }, null)!;
				for (int x = 1; x < 99; x++) { Tile floor = Main.tile[x, 40]; floor.HasTile = true; floor.TileType = TileID.Dirt; }
				Main.npc = Enumerable.Range(0, oldNpcs.Length).Select(i => new NPC { whoAmI = i }).ToArray();
				Main.item = Enumerable.Range(0, oldItems.Length).Select(i => new Item { whoAmI = i }).ToArray();
				var owner = new Player { whoAmI = 0, active = true, Center = new Vector2(600, 580) };
				Main.player[0] = owner;
				NPC npc = Main.npc[20]; npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
				npc.active = true; npc.ai[0] = 0; npc.Center = owner.Center;
				var mate = (SoulboundCompanion)npc.ModNPC;
				owner.inventory[0] = new Item(ModContent.ItemType<SoulboundSigil>());
				var sigil = (SoulboundSigil)owner.inventory[0].ModItem; sigil.Profile = mate.Profile.Clone();
				void Set(string field, object? value) => typeof(SoulboundCompanion).GetField(field, flags)!.SetValue(mate, value);
				object? Get(string field) => typeof(SoulboundCompanion).GetField(field, flags)!.GetValue(mate);
				object? Call(string method, params object[] args) => typeof(SoulboundCompanion).GetMethod(method, flags)!.Invoke(mate, args);
				bool Begin(CompanionQuestion question) => (bool)Call("BeginChoiceQuestion", question)!;
				NPC Bunny(Vector2 position) {
					NPC bunny = Main.npc[19]; bunny.SetDefaults(NPCID.Bunny); bunny.active = true; bunny.Center = position;
					return bunny;
				}
				mate.Profile.Store(new Item(ItemID.SilverCoin, 10)); Set("personalQuestionCooldown", 0);
				check(Begin(CompanionQuestion.Company) && (int)Get("personalQuestionCooldown")! == 18000,
					"Calm did not apply the common five-minute question interval");
				mate.RespondToQuestion(mate.QuestionId, CompanionAnswer.Later); Set("speechTimer", 0);
				check(!Begin(CompanionQuestion.Wallet), "Wallet bypassed the personal-question interval");
				Set("personalQuestionCooldown", 1); Call("UpdateChoiceConversation");
				check((int)Get("personalQuestionCooldown")! == 0, "Personal-question interval did not advance");
				var result = mate.PerformQuickAction(CompanionQuickAction.QuestionsQuiet);
				check(result.Accepted && sigil.Profile.QuestionCadence == CompanionQuestionCadence.Quiet
					&& mate.Profile.MiningInitiative == CompanionInitiativePolicy.Ask,
					"Quiet was not saved or changed work permissions");
				Set("personalQuestionCooldown", 0);
				check(!Begin(CompanionQuestion.Company) && !Begin(CompanionQuestion.Wallet), "Quiet still asked personal questions");
				mate.PerformQuickAction(CompanionQuickAction.QuestionsChatty); Set("personalQuestionCooldown", 0);
				check(Begin(CompanionQuestion.Wallet) && (int)Get("personalQuestionCooldown")! == 7200,
					"Chatty did not apply the common two-minute interval");
				mate.PerformQuickAction(CompanionQuickAction.QuestionsQuiet);
				check(!mate.HasPendingQuestion, "Changing question settings retained an unwanted question");
				Main.netMode = NetmodeID.MultiplayerClient;
				check(!mate.PerformQuickAction(CompanionQuickAction.QuestionsChatty).Accepted
					&& mate.Profile.QuestionCadence == CompanionQuestionCadence.Quiet,
					"Client independently changed saved question settings");
				Main.netMode = mode;
				mate.PerformQuickAction(CompanionQuickAction.QuestionsCalm);
				Set("personalQuestionCooldown", 0); Set("speechTimer", 0);
				check(Begin(CompanionQuestion.Company), "Critter fixture could not begin its existing question");
				NPC target = Bunny(npc.Center + new Vector2(180, 35));
				result = (CompanionConversationResult)Call("PerformNpcContext", CompanionNpcAction.Company, 19, NPCID.Bunny)!;
				check(result.Accepted && !mate.HasPendingQuestion && ReferenceEquals(Get("critterTarget"), target),
					"Direct critter invitation did not replace a pending personal question");
				Set("guardianTarget", 18); int duration = (int)Get("critterVisitTicks")!;
				for (int i = 0; i < 120; i++) { Call("UpdateNatureCompanions"); Call("UpdateCritterActivity"); }
				check(ReferenceEquals(Get("critterTarget"), target) && (int)Get("critterVisitTicks")! == duration,
					"Brief combat erased or exhausted the selected critter visit");
				Set("guardianTarget", -1); mate.Profile.WorkPaused = true;
				Call("UpdateNatureCompanions"); Call("UpdateCritterActivity");
				check(ReferenceEquals(Get("critterTarget"), target) && (int)Get("critterVisitTicks")! == duration,
					"Pause discarded its selected critter visit");
				mate.Profile.WorkPaused = false; Set("speechTimer", 600);
				Set("critterVisitTicks", 1); Call("UpdateNatureCompanions");
				check(Get("critterTarget") is null && ReferenceEquals(Get("deferredCritter"), target)
					&& (string)Get("speechText")! == SoulmatesText.Get("TargetOrders.CritterTimeout"),
					"Failed direct visit disappeared without feedback or bounded retry exclusion");
				Set("critterDecisionTimer", 0); Call("UpdateCritterActivity");
				check(Get("critterTarget") is null, "Timed-out critter was immediately selected again");
				result = (CompanionConversationResult)Call("PerformNpcContext", CompanionNpcAction.Company, 19, NPCID.Bunny)!;
				check(result.Accepted, "Explicit retry was blocked by automatic critter deferral");
				var company = target.GetGlobalNPC<CompanionCritterCompany>();
				bool Assigned() => (bool)typeof(CompanionCritterCompany).GetMethod("BelongsTo", flags)!.Invoke(company, new object[] { mate })!;
				float originalDistance = Vector2.Distance(npc.Center, target.Center);
				npc.netUpdate = false;
				for (int tick = 0; tick < 240 && !Assigned(); tick++) {
					npc.AI();
					npc.position += npc.velocity;
				}
				check(Assigned() && Vector2.Distance(npc.Center, target.Center) < originalDistance,
					"Production companion AI did not complete an unobstructed critter approach");
				check((bool)typeof(CompanionCritterCompany).GetMethod("BelongsTo", flags)!.Invoke(company, new object[] { mate })!
					&& npc.netUpdate && target.active, "Ground-level visit did not bind the selected real critter");
				NPC spare = Main.npc[18]; spare.SetDefaults(NPCID.Bunny); spare.active = true; spare.Center = npc.Center;
				Set("critterDecisionTimer", 0); npc.netUpdate = false; Call("UpdateCritterActivity");
				check(Get("critterTarget") is null && !npc.netUpdate, "Full Company selected and synchronized a phantom visit");
				spare.active = false;
				Vector2 position = target.position; int health = target.life, drop = target.catchItem;
				npc.Center = target.Center + new Vector2(180, -30); target.velocity = new Vector2(-2, 0);
				target.AI();
				check(target.velocity.X > 0 && target.velocity.X <= 1.8f && target.position == position
					&& target.life == health && target.catchItem == drop && !target.noTileCollide,
					"Real walking AI overrode catch-up guidance or changed native critter state");
				npc.Center = target.Center + new Vector2(90, -30); target.velocity.X = -2;
				company.PostAI(target); check(target.velocity.X > 0, "Critter catch-up stopped before reaching its comfort zone");
				npc.Center = target.Center + new Vector2(55, -30); target.velocity.X = -2;
				company.PostAI(target); check(target.velocity.X == -2, "Settled critter could not resume native wandering");
				npc.Center = target.Center + new Vector2(100, -30); target.velocity.X = -2;
				company.PostAI(target); check(target.velocity.X == -2, "Critter oscillated within the comfortable distance band");
				npc.Center = owner.Center;
				mate.PerformQuickAction(CompanionQuickAction.CritterOff);
				mate.PerformQuickAction(CompanionQuickAction.CritterCompany);
				SettleServerInventory(ModLoader.GetMod("Soulmates"));
				mate.PerformQuickAction(CompanionQuickAction.CritterCompanyPolicy);
				target.Center = owner.Center + new Vector2(180, 0);
				Main.netMode = NetmodeID.MultiplayerClient; Set("critterTarget", null); Set("critterDecisionTimer", 0);
				Call("UpdateCritterActivity");
				check(Get("critterTarget") is null, "Client independently chose an automatic critter target");
				Main.netMode = mode; npc.netUpdate = false; Set("autonomyDecisionTimer", 0);
				Call("UpdateHelpfulAutonomy"); Call("UpdateCritterActivity");
				if (mate.HasPendingInitiative) mate.RespondToInitiative(mate.InitiativeId, CompanionInitiativeResponse.Yes);
				check(ReferenceEquals(Get("critterTarget"), target) && npc.netUpdate, "Authority did not synchronize its automatic critter selection");
				using var snapshot = new MemoryStream();
				using (var writer = new BinaryWriter(snapshot, Encoding.UTF8, true)) mate.SendExtraAI(writer);
				NPC remoteNpc = Main.npc[21]; remoteNpc.SetDefaults(ModContent.NPCType<SoulboundCompanion>()); remoteNpc.ai[0] = 0;
				Main.netMode = NetmodeID.MultiplayerClient; snapshot.Position = 0;
				using (var reader = new BinaryReader(snapshot, Encoding.UTF8, true)) ((SoulboundCompanion)remoteNpc.ModNPC).ReceiveExtraAI(reader);
				check(ReferenceEquals(typeof(SoulboundCompanion).GetField("critterTarget", flags)!.GetValue(remoteNpc.ModNPC), target),
					"Automatic critter visit did not arrive on a multiplayer receiver");
				Main.netMode = mode; mate.Recall();
				check(Get("deferredCritter") is null && target.active, "Recall kept a retry exclusion or deleted the native critter");
				target.active = false; remoteNpc.active = false;
				npc.active = true; mate.Profile.CritterMode = CompanionCritterMode.Watch;
				Set("critterWatchCooldown", 0); Call("UpdateCritterWatch");
				check((int)Get("critterWatchCooldown")! == 120, "Empty critter watch rescanned all NPCs every tick");
				npc.active = false;
			}
		}
		finally {
			Main.player[0] = oldPlayer; Main.npc = oldNpcs; Main.item = oldItems; Main.tile = oldMap;
			Main.maxTilesX = oldWidth; Main.maxTilesY = oldHeight; Main.netMode = oldMode; Main.myPlayer = oldLocal;
			for (int i = 0; i < Netplay.Clients.Length; i++) Netplay.Clients[i] = oldClients[i];
		}
	}
}
