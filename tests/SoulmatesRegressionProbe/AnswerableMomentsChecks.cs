using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Net;

namespace SoulmatesRegressionProbe;

public sealed partial class EngineChecks
{
	private static void CheckAnswerableMoments(Action<bool, string> check)
	{
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
		Player oldPlayer = Main.player[0]; NPC[] oldNpcs = Main.npc; Item[] oldItems = Main.item;
		Tilemap oldMap = Main.tile;
		int oldWidth = Main.maxTilesX, oldHeight = Main.maxTilesY, oldMode = Main.netMode, oldLocal = Main.myPlayer;
		RemoteClient[] oldClients = (RemoteClient[])Netplay.Clients.Clone();
		var oldSocket = Netplay.Connection.Socket;
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
				mate.Profile.Personality = CompanionPersonality.Gentle;
				mate.Profile.Bond = 10; mate.Profile.Mood = 50;
				owner.inventory[0] = new Item(ModContent.ItemType<SoulboundSigil>());
				var sigil = (SoulboundSigil)owner.inventory[0].ModItem;
				sigil.Profile = mate.Profile.Clone();
				void Set(string field, object value) => typeof(SoulboundCompanion).GetField(field, flags)!.SetValue(mate, value);
				object Get(string field) => typeof(SoulboundCompanion).GetField(field, flags)!.GetValue(mate)!;
				object? Call(string method, params object[] args) => typeof(SoulboundCompanion).GetMethod(method, flags)!.Invoke(mate, args);
				NPC Bunny(Vector2 position) {
					NPC bunny = Main.npc[19]; bunny.SetDefaults(NPCID.Bunny); bunny.active = true; bunny.Center = position;
					return bunny;
				}
				bool Begin(CompanionQuestion question) {
					Set("personalQuestionCooldown", 0);
					return (bool)Call("BeginChoiceQuestion", question)!;
				}
				check(Begin(CompanionQuestion.Company), "Reaction fixture could not ask a question");
				Guid questionId = mate.QuestionId;
				NPC lost = Bunny(npc.Center + new Vector2(50, 0));
				lost.StrikeNPC(new NPC.HitInfo { Damage = 100, HitDirection = 1, HideCombatText = true }, noPlayerInteraction: true);
				for (int i = 0; i < 1000; i++) { Call("UpdateChoiceConversation"); Call("UpdateNatureCompanions"); }
				check((int)Get("critterLossCooldown") == 0 && ((string)Get("pendingCritterLossKey")).Length > 0,
					"An unanswered question erased grief when the reaction cooldown elapsed");
				check(mate.QuestionId == questionId, "Queued grief replaced the player's pending question");
				int bubbleStart = bubbles.Count, bondBefore = mate.Profile.Bond, interactions = mate.Profile.Interactions;
				mate.RespondToQuestion(questionId, CompanionAnswer.Later);
				check(bubbles.Values.Cast<EmoteBubble>().Skip(bubbleStart).Any(b => ReferenceEquals(b.anchor.entity, owner) && b.emote == EmoteID.EmoteSleep),
					"Answer did not produce a native player reply bubble");
				check(mate.Profile.Bond == bondBefore && mate.Profile.Interactions == interactions,
					"Reply bubble re-entered generic emote rewards");
				Set("speechTimer", 0); Call("UpdateCritterLossSpeech");
				check((string)Get("speechText") == SoulmatesText.Get("Social.Critters.Loss.Gentle", lost.TypeName)
					&& (int)Get("pendingCritterCareTicks") > 0, "Deferred critter grief was not delivered with a follow-up available");
				Set("speechTimer", 0); Set("personalQuestionCooldown", 0);
				bool dedicated = Main.dedServ, menu = Main.gameMenu, inventory = Main.playerInventory;
				int localPlayer = Main.myPlayer;
				try {
					Main.dedServ = false; Main.gameMenu = false; Main.playerInventory = false; Main.myPlayer = 0;
					Call("UpdateChoiceConversation");
				}
				finally { Main.dedServ = dedicated; Main.gameMenu = menu; Main.playerInventory = inventory; Main.myPlayer = localPlayer; }
				check(mate.PendingQuestion == CompanionQuestion.CritterCare, "Idle companion did not offer answerable critter care");
				int emote = (int)typeof(SoulboundCompanion).GetMethod("QuestionAnswerEmote", flags | BindingFlags.Static)!
					.Invoke(null, new object[] { CompanionQuestion.CritterCare, 1 })!;
				EmoteBubble.NewBubble(emote, new WorldUIAnchor(owner), 150);
				check(!mate.HasPendingQuestion && mate.Profile.CritterMode == CompanionCritterMode.Company
					&& mate.Profile.Voice == CompanionVoice.Playful && sigil.Profile.CritterMode == CompanionCritterMode.Company,
					"Native critter-care reply did not persist company and tone");
				check(mate.Profile.MiningInitiative == CompanionInitiativePolicy.Ask && mate.Profile.Experience == 0,
					"Critter-care conversation granted mining permission or XP");

				foreach (CompanionAnswer answer in Enum.GetValues<CompanionAnswer>()) {
					Set("pendingCritterCareTicks", 600); Set("pendingCritterCareName", "Bunny");
					check(Begin(CompanionQuestion.CritterCare), "Critter-care question refused fixture answer " + answer);
					var priorMode = mate.Profile.CritterMode; var priorVoice = mate.Profile.Voice;
					mate.RespondToQuestion(mate.QuestionId, answer);
					check(answer == CompanionAnswer.Later ? mate.Profile.CritterMode == priorMode && mate.Profile.Voice == priorVoice
						: mate.Profile.CritterMode == (answer == CompanionAnswer.Second ? CompanionCritterMode.Company : CompanionCritterMode.Watch),
						"Critter-care answer applied the wrong permission " + answer);
				}
				Set("pendingCritterCareTicks", 0); Set("pendingCritterCareName", "");
				check(!Begin(CompanionQuestion.CritterCare), "Critter-care question invented an unobserved death");
				Set("speechTimer", 0); Begin(CompanionQuestion.Company); questionId = mate.QuestionId;
				Set("guardianTarget", 18); Call("UpdateChoiceConversation");
				check(mate.QuestionId == questionId && !mate.RespondToQuestion(questionId, CompanionAnswer.First),
					"Short combat destroyed or answered the pending question");
				Set("guardianTarget", -1); Set("nativeEmoteReactionCooldown", 45);
				EmoteBubble.NewBubble(EmoteID.EmoteLaugh, new WorldUIAnchor(owner), 150);
				check(!mate.HasPendingQuestion && mate.Profile.Voice == CompanionVoice.Playful,
					"An ordinary emote cooldown blocked a valid question answer");
				Begin(CompanionQuestion.Company); string speech = (string)Get("speechText");
				check(!mate.ShowLocalizedSpeech("Social.Autonomous.Context.Rain") && (string)Get("speechText") == speech,
					"Ambient speech overwrote an unanswered question");
				mate.RespondToQuestion(mate.QuestionId, CompanionAnswer.Later);

				Set("critterLossCooldown", 0); Set("speechTimer", 2000); Set("critterCareCooldown", 20000);
				npc.Center = new Vector2(400, 500); owner.Center = new Vector2(700, 500);
				lost = Bunny(new Vector2(1000, 500));
				lost.StrikeNPC(new NPC.HitInfo { Damage = 100, HitDirection = 1, HideCombatText = true }, noPlayerInteraction: true);
				check(((string)Get("pendingCritterLossKey")).Length > 0, "Companion missed a nearby death witnessed by its owner");
				for (int i = 0; i < 7200; i++) Call("UpdateNatureCompanions");
				check(((string)Get("pendingCritterLossKey")).Length == 0, "Blocked grief queue survived indefinitely");

				Set("actionAnimationTicks", 0); npc.velocity = new Vector2(5.1f, 0); Call("UpdateSocialState");
				check((int)Get("actionAnimationTicks") == 24, "Fast movement did not enter the action animation");
				npc.velocity = Vector2.Zero; for (int i = 0; i < 23; i++) Call("UpdateSocialState");
				check((int)Get("actionAnimationTicks") == 1, "Brief movement gap flickered back to idle animation");
				Call("UpdateSocialState"); check((int)Get("actionAnimationTicks") == 0, "Settled companion never returned to idle animation");

				mate.ShowSpeech("One quiet line."); Set("speechTimer", (int)Get("speechTimer") - 30);
				int remaining = (int)Get("speechTimer"); Vector2 speechAnchor = (Vector2)Get("speechAnchorWorld");
				float speechSide = (float)Get("speechSide");
				npc.Center = owner.Center + new Vector2(80, 0);
				mate.ShowSpeech("  One quiet line.  ");
				check((int)Get("speechTimer") == remaining && (Vector2)Get("speechAnchorWorld") == speechAnchor
					&& (float)Get("speechSide") == speechSide, "Repeated speech restarted its fade, anchor or side");
				Set("speechTimer", 0); mate.ShowSpeech("One quiet line.");
				check((int)Get("speechTimer") == (int)Get("speechDuration") && (float)Get("speechSide") == 1f,
					"A finished speech line could not be shown again");
				var layout = typeof(SoulboundCompanion).GetMethod("SpeechBubblePosition", BindingFlags.Static | BindingFlags.NonPublic)!;
				foreach (Vector2 viewport in new[] { new Vector2(320, 240), new Vector2(960, 540), new Vector2(1920, 1080) }) {
					Vector2 previous = Vector2.Zero;
					for (int y = 220; y <= 234; y++) {
						Vector2 position = (Vector2)layout.Invoke(null, new object[] { new Vector2(viewport.X / 2, y), new Vector2(180, 86), viewport, 1f })!;
						check(position.Y >= 52 && position.Y + 86 <= viewport.Y - 10 && position.X >= 8 && position.X + 180 <= viewport.X - 8,
							"Speech escaped viewport bounds");
						if (y > 220) check(Math.Abs(position.Y - previous.Y) <= 1f,
							"Speech jumped below its speaker at the top-edge threshold");
						previous = position;
					}
				}

				Call("ShowNativeExpression", EmoteID.MiscTree, EmoteID.EmoteConfused, -1);
				int firstBubble = (int)Get("activeNativeBubble"), beforeDuplicate = bubbles.Count;
				EmoteBubble first = EmoteBubble.GetExistingEmoteBubble(firstBubble)!;
				check(first.emote == EmoteID.MiscTree && first.lifeTime == 180, "Expression did not start with a readable native symbol");
				for (int i = 0; i < 90; i++) Call("UpdateNativeExpression");
				Call("ShowNativeExpression", EmoteID.MiscTree, EmoteID.EmoteConfused, -1);
				check((int)Get("nativeExpressionTicks") == 90 && bubbles.Count == beforeDuplicate,
					"Repeated expression restarted its first symbol");
				typeof(SoulboundCompanion).GetMethod("ShowNativeEmote", flags, null, new[] { typeof(int), typeof(int) }, null)!
					.Invoke(mate, new object[] { EmoteID.ItemGoldpile, 90 });
				check(bubbles.Count == beforeDuplicate, "An incidental emote interrupted an expression");
				RecoveryPacketSpy.Packets.Clear(); RecoveryPacketSpy.Watching = mode == NetmodeID.Server;
				for (int i = 0; i < 90; i++) Call("UpdateNativeExpression");
				check(first.lifeTime == 0 && EmoteBubble.GetExistingEmoteBubble((int)Get("activeNativeBubble"))!.emote == EmoteID.EmoteConfused,
					"Expression did not advance without overlapping its old bubble");
				check(bubbles.Values.Cast<EmoteBubble>().Count(b => ReferenceEquals(b.anchor.entity, npc) && b.lifeTime > 0) == 1,
					"Companion displayed overlapping native bubbles");
				if (mode == NetmodeID.Server) check(RecoveryPacketSpy.Packets.Any(p => p.Type == MessageID.SyncEmoteBubble && p.Player == firstBubble && p.Amount == 255),
					"Replaced native bubble was not retired on multiplayer clients");
				RecoveryPacketSpy.Watching = false; RecoveryPacketSpy.Packets.Clear();
				for (int i = 0; i < 180; i++) Call("UpdateNativeExpression");
				check((int)Get("nativeExpressionTicks") == 0 && ((int[])Get("nativeExpression")).Length == 0,
					"Completed expression continued indefinitely");
				Call("ShowNativeExpression", EmoteID.ItemGoldpile, EmoteID.EmotionLove, EmoteID.EmoteConfused);
				for (int i = 0; i < 180; i++) Call("UpdateNativeExpression");
				check(EmoteBubble.GetExistingEmoteBubble((int)Get("activeNativeBubble"))!.emote == EmoteID.EmotionLove,
					"Wallet expression skipped its middle symbol");
				for (int i = 0; i < 180; i++) Call("UpdateNativeExpression");
				check(EmoteBubble.GetExistingEmoteBubble((int)Get("activeNativeBubble"))!.emote == EmoteID.EmoteConfused,
					"Wallet expression did not finish with its question");
				int validBubble = (int)Get("activeNativeBubble");
				Call("ShowNativeExpression", -1, EmoteID.EmoteConfused, -1);
				check((int)Get("activeNativeBubble") == validBubble, "Invalid expression replaced a valid native bubble");
				mate.SetCommand(stay: true);
				check((int)Get("nativeExpressionTicks") == 0, "A direct command kept an old question expression running");
				mate.SetCommand(stay: false);
				Call("ShowNativeExpression", EmoteID.MiscTree, EmoteID.EmoteConfused, -1);
				mate.StartJob(CompanionJob.Gather);
				check((int)Get("nativeExpressionTicks") == 0 && !mate.HasPendingQuestion,
					"Direct work retained an old question expression");
				mate.SetCommand(stay: false);

				Set("brainState", Enum.Parse(typeof(SoulboundCompanion).GetField("brainState", flags)!.FieldType, "Wander"));
				Set("stateTimer", 200); Set("idleTarget", new Vector2(650, 470)); Set("facing", -1);
				using var stream = new MemoryStream();
				using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) mate.SendExtraAI(writer);
				byte[] payload = stream.ToArray();
				NPC remoteNpc = Main.npc[18]; remoteNpc.SetDefaults(ModContent.NPCType<SoulboundCompanion>()); remoteNpc.ai[0] = 0;
				var remote = (SoulboundCompanion)remoteNpc.ModNPC;
				Main.netMode = NetmodeID.MultiplayerClient;
				stream.Position = 0; using (var reader = new BinaryReader(stream, Encoding.UTF8, true)) remote.ReceiveExtraAI(reader);
				check(typeof(SoulboundCompanion).GetField("brainState", flags)!.GetValue(remote)!.ToString() == "Wander"
					&& (int)typeof(SoulboundCompanion).GetField("stateTimer", flags)!.GetValue(remote)! == 200
					&& (int)typeof(SoulboundCompanion).GetField("facing", flags)!.GetValue(remote)! == -1,
					"Multiplayer movement lost the authority's state, duration or facing");
				Call("ChooseNextState", 200f, owner.Center);
				check(Get("brainState").ToString() == "Wander", "Client chose an independent random idle destination");
				npc.velocity = new Vector2(6, 0); Call("UpdateFacing");
				check((int)Get("facing") == -1 && npc.spriteDirection == -1,
					"Multiplayer client overrode its synchronized facing with local velocity");
				payload[^4] = 255;
				bool rejected = false;
				try { using var bad = new BinaryReader(new MemoryStream(payload)); remote.ReceiveExtraAI(bad); }
				catch (InvalidDataException) { rejected = true; }
				check(rejected, "Invalid multiplayer movement state was accepted");
				Main.myPlayer = 0; Netplay.Connection.Socket = new ProbeSocket(); ProbeSocket.Recording = true; ProbeSocket.Sent.Clear();
				var observed = new EmoteBubble(EmoteID.EmoteLaugh, new WorldUIAnchor(owner), 150);
				new SoulmateEmoteObserver().OnSpawn(observed);
				check(ProbeSocket.Sent.Count == 0, "Received native emote echoed back as another multiplayer request");
				int receivedBubbleCount = bubbles.Count;
				Call("ShowNativeExpression", EmoteID.MiscTree, EmoteID.EmoteConfused, -1);
				check(bubbles.Count == receivedBubbleCount, "Multiplayer client invented its own native expression");
				ProbeSocket.Recording = false;
				Main.netMode = mode; Main.myPlayer = 255;
				Set("pendingCritterLossKey", "Social.Critters.Loss.Gentle"); Set("pendingCritterCareTicks", 1000);
				Call("ShowNativeExpression", EmoteID.CritterBunny, EmoteID.EmoteSadness, -1);
				EmoteBubble recalled = EmoteBubble.GetExistingEmoteBubble((int)Get("activeNativeBubble"))!;
				mate.Recall();
				check(!npc.active && ((string)Get("pendingCritterLossKey")).Length == 0 && (int)Get("pendingCritterCareTicks") == 0,
					"Switching companions retained the previous soul's event queue");
				check(recalled.lifeTime == 0 && (int)Get("activeNativeBubble") == -1 && (int)Get("nativeExpressionTicks") == 0,
					"Recall retained the companion's native emote sequence");
			}
		}
		finally {
			RecoveryPacketSpy.Watching = false; RecoveryPacketSpy.Packets.Clear();
			ProbeSocket.Recording = false; ProbeSocket.Sent.Clear(); Netplay.Connection.Socket = oldSocket;
			Main.player[0] = oldPlayer; Main.npc = oldNpcs; Main.item = oldItems; Main.tile = oldMap;
			Main.maxTilesX = oldWidth; Main.maxTilesY = oldHeight; Main.netMode = oldMode; Main.myPlayer = oldLocal;
			for (int i = 0; i < Netplay.Clients.Length; i++) Netplay.Clients[i] = oldClients[i];
		}
	}
}
