#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Common.UI;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.ID;
using Terraria.GameInput;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.Net;

namespace SoulmatesRegressionProbe;

public sealed partial class EngineChecks
{
	private static void CheckIntentLearning(Action<bool, string> check)
	{
		var mine = new CompanionIntent(CompanionInitiativeKind.Mining, CompanionIntentContext.Building);
		var elsewhere = mine with { Context = CompanionIntentContext.Underground };
		var profile = new CompanionProfile();
		int neutral = profile.IntentLearning.Score(mine);
		profile.IntentLearning.Completed(mine, true);
		profile.IntentLearning.Evaluate(mine, CompanionAnswer.First);
		check(profile.IntentLearning.Score(mine) > neutral, "Player approval did not affect a learned intention");
		check(profile.IntentLearning.Feedback(elsewhere) == 0, "Evaluation leaked into another situation");
		int approved = profile.IntentLearning.Feedback(mine);
		profile.IntentLearning.Evaluate(mine, CompanionAnswer.Later);
		check(profile.IntentLearning.Feedback(mine) == approved, "Later punished an intention");
		profile.IntentLearning.Evaluate(mine, CompanionAnswer.Third);
		check(profile.IntentLearning.Feedback(mine) < approved, "Negative evaluation did not affect preference");
		for (int i = 0; i < 10000; i++) {
			profile.IntentLearning.Requested(mine); profile.IntentLearning.Completed(mine, true);
			profile.IntentLearning.Evaluate(mine, CompanionAnswer.First);
		}
		check(profile.IntentLearning.Attempts(mine) == 32 && profile.IntentLearning.Score(mine) <= 40,
			"Repeated work overflowed learned estimates");
		profile.IntentLearning.ObserveBuildingSupply("Terraria/9");
		CompanionProfile clone = profile.Clone(), saved = CompanionProfile.Load(profile.Save());
		clone.IntentLearning.Evaluate(mine, CompanionAnswer.Third);
		check(profile.IntentLearning.Feedback(mine) == 32 && clone.IntentLearning.Feedback(mine) == 24,
			"Companions share mutable preferences");
		check(saved.IntentLearning.Feedback(mine) == 32 && saved.IntentLearning.RecognizesSupply("Terraria/9"),
			"Save/load lost learned preferences or construction supplies");
		check(CompanionProfile.Load(new TagCompound()).IntentLearning.Attempts(mine) == 0,
			"Old Sigil invented learned outcomes");
		var observing = new CompanionIntentLearning();
		for (int i = 0; i < 100; i++) observing.Observed(mine);
		check(observing.Score(mine) > neutral && observing.Feedback(mine) == 0 && observing.Attempts(mine) == 0,
			"Observation was ignored or mislabeled as player approval or completed work");
		byte[] corrupt = new byte[169]; corrupt[0] = 33;
		try {
			using var bad = new BinaryReader(new MemoryStream(corrupt)); CompanionIntentLearning.Read(bad);
			check(false, "Oversized wire learning estimate was accepted");
		}
		catch (InvalidDataException) { check(true, "Oversized wire learning estimate rejected"); }
		using (var stream = new MemoryStream()) {
			using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) profile.Write(writer);
			stream.Position = 0;
			using var reader = new BinaryReader(stream, Encoding.UTF8, true);
			var network = CompanionProfile.Read(reader);
			check(stream.Position == stream.Length && network.IntentLearning.Feedback(mine) == 32
				&& network.IntentLearning.RecognizesSupply("Terraria/9"), "Learning wire round-trip is misaligned");
		}
		var attention = new CompanionAttention();
		var gather = new CompanionIntent(CompanionInitiativeKind.Gathering, CompanionIntentContext.Building);
		profile.IntentLearning.Evaluate(gather, CompanionAnswer.Third);
		check(attention.Choose(new[] { new CompanionOpportunity(mine.Kind, 40 + profile.IntentLearning.Score(mine)),
			new CompanionOpportunity(gather.Kind, 40 + profile.IntentLearning.Score(gather)) }) == mine.Kind,
			"Existing attention ignored learned relevance");
		for (int i = 0; i < 20; i++) profile.IntentLearning.ObserveBuildingSupply("Terraria/" + i);
		check(profile.IntentLearning.BuildingSupplies.Count == 12 && !profile.IntentLearning.RecognizesSupply("Terraria/0"),
			"Construction observations grew without bound");
		check(CompanionAbilityRegistry.All.Count == Enum.GetValues<CompanionInitiativeKind>().Length,
			"Learning abilities drifted from the registry");

		const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
		Player oldPlayer = Main.player[0]; NPC[] oldNpcs = Main.npc; Item[] oldItems = Main.item;
		int oldMode = Main.netMode, oldLocal = Main.myPlayer;
		bool oldDedicated = Main.dedServ, oldMenu = Main.gameMenu, oldInventory = Main.playerInventory;
		bool oldLeft = Main.mouseLeft, oldRight = Main.mouseRight, oldBlock = Main.blockMouse;
		int oldMouseX = PlayerInput.MouseX, oldMouseY = PlayerInput.MouseY;
		var oldView = Main.GameViewMatrix;
		float oldScale = Main.UIScale;
		FieldInfo width = typeof(PlayerInput).GetField("_originalScreenWidth", BindingFlags.Static | BindingFlags.NonPublic)!;
		FieldInfo height = typeof(PlayerInput).GetField("_originalScreenHeight", BindingFlags.Static | BindingFlags.NonPublic)!;
		object oldWidth = width.GetValue(null)!, oldHeight = height.GetValue(null)!;
		RemoteClient[] oldClients = (RemoteClient[])Netplay.Clients.Clone();
		try {
			for (int i = 0; i < Netplay.Clients.Length; i++) Netplay.Clients[i] = new RemoteClient { Socket = new ProbeSocket() };
			foreach (int mode in new[] { NetmodeID.SinglePlayer, NetmodeID.Server }) {
				Main.netMode = mode; Main.myPlayer = 255;
				Main.npc = Enumerable.Range(0, oldNpcs.Length).Select(i => new NPC { whoAmI = i }).ToArray();
				Main.item = Enumerable.Range(0, oldItems.Length).Select(i => new Item { whoAmI = i }).ToArray();
				var owner = new Player { whoAmI = 0, active = true, Center = new Vector2(700, 500) };
				Main.player[0] = owner;
				NPC npc = Main.npc[20]; npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
				npc.active = true; npc.ai[0] = 0; npc.Center = owner.Center;
				var mate = (SoulboundCompanion)npc.ModNPC;
				owner.inventory[0] = new Item(ModContent.ItemType<SoulboundSigil>());
				var sigil = (SoulboundSigil)owner.inventory[0].ModItem; sigil.Profile = mate.Profile.Clone();
				void Set(string field, object value) => typeof(SoulboundCompanion).GetField(field, flags)!.SetValue(mate, value);
				object? Get(string field) => typeof(SoulboundCompanion).GetField(field, flags)!.GetValue(mate);
				object? Call(string method, params object?[] args) => typeof(SoulboundCompanion).GetMethod(method, flags)!.Invoke(mate, args);
				owner.inventory[1] = new Item(ItemID.Wood); owner.selectedItem = 1; owner.controlUseItem = true;
				owner.chest = -1; owner.SetTalkNPC(-1); Set("learningObservationTimer", 0);
				Call("UpdateLearningFromOwner");
				owner.controlUseItem = false; owner.itemAnimation = 0;
				check((CompanionIntentContext)Call("CurrentIntentContext")! == CompanionIntentContext.Building
					&& mate.Profile.IntentLearning.RecognizesSupply("Terraria/" + ItemID.Wood), "Building use was not connected to learning");
				Item drop = Main.item[10]; drop.SetDefaults(ItemID.Gel); drop.stack = 3; drop.active = true;
				drop.Center = npc.Center; drop.playerIndexTheItemIsReservedFor = 255;
				check(mate.PerformDirectOrder(CompanionTargetOrder.Gather, Point.Zero, 10).Accepted, "Learning fixture could not order real pickup");
				mate.PerformQuickAction(CompanionQuickAction.ToggleAutonomy);
				check(Get("learningIntent") is not null && mate.CurrentJob == CompanionJob.Gather,
					"Disabling autonomy erased the still-active direct intention");
				mate.PerformQuickAction(CompanionQuickAction.ResetInitiativeRules);
				check(Get("learningIntent") is not null, "Resetting automatic consent erased a direct intention");
				mate.PerformQuickAction(CompanionQuickAction.ToggleAutonomy);
				Call("UpdateGatherJob");
				var actual = new CompanionIntent(CompanionInitiativeKind.Gathering, CompanionIntentContext.Building);
				check(mate.Profile.ItemCount(ItemID.Gel) == 3 && !drop.active && mate.Profile.IntentLearning.Attempts(actual) == 1,
					"Actual cargo completion did not record exactly one outcome");
				Call("ObserveIntentContext", CompanionIntentContext.Mining);
				check((CompanionIntent?)Get("reflectionIntent") == actual, "A new observation rewrote the completed experiment");
				check((bool)Call("BeginChoiceQuestion", CompanionQuestion.Reflection)!, "Completed action could not ask for evaluation");
				Guid token = mate.QuestionId;
				int xp = mate.Profile.Experience;
				check(mate.RespondToQuestion(token, CompanionAnswer.First) && !mate.RespondToQuestion(token, CompanionAnswer.First),
					"Evaluation token was rejected or replayed");
				check(mate.Profile.IntentLearning.Feedback(actual) == 8 && sigil.Profile.IntentLearning.Feedback(actual) == 8
					&& mate.Profile.Experience == xp && mate.Profile.GatheringInitiative == CompanionInitiativePolicy.Ask,
					"Evaluation failed to persist, granted XP or changed consent");
				check(mate.Profile.IntentLearning.Feedback(actual with { Context = CompanionIntentContext.Mining }) == 0,
					"Delayed answer learned from the current context instead of the completed action");
				Call("BeginLearningIntent", CompanionInitiativeKind.Mining, false, (CompanionIntentContext?)CompanionIntentContext.Building);
				mate.SetCommand(true);
				check(Get("learningIntent") is null && mate.Profile.IntentLearning.Attempts(mine) == 0,
					"Player cancellation was recorded as a failed experiment");
				mate.SetCommand(false);
				Set("reflectionCooldown", 0);
				Set("personalQuestionCooldown", 0);
				Call("BeginLearningIntent", CompanionInitiativeKind.Mining, false, (CompanionIntentContext?)CompanionIntentContext.Building);
				Call("CompleteLearningIntent", true);
				check((bool)Call("BeginChoiceQuestion", CompanionQuestion.Reflection)!, "Second evaluation fixture did not open");
				Set("questionTicks", 1); Call("UpdateChoiceConversation");
				check(!mate.HasPendingQuestion && Get("reflectionIntent") is null && mate.Profile.IntentLearning.Feedback(mine) == 0,
					"Unanswered evaluation punished or retained its result");
				mate.Profile.MiningInitiative = CompanionInitiativePolicy.Never;
				for (int i = 0; i < 30; i++) mate.Profile.IntentLearning.Evaluate(mine, CompanionAnswer.First);
				check(!(bool)Call("CanRunInitiative", CompanionInitiativeKind.Mining)!, "Learned preference overrode Never");
				mate.Profile.MiningInitiative = CompanionInitiativePolicy.Always; mate.Profile.WorkPaused = true;
				check(!(bool)Call("CanRunInitiative", CompanionInitiativeKind.Mining)!, "Learned preference overrode Pause");
				mate.Profile.WorkPaused = false; mate.Profile.QuestionCadence = CompanionQuestionCadence.Quiet;
				Set("reflectionCooldown", 0); Call("BeginLearningIntent", CompanionInitiativeKind.Forestry, false, null);
				Call("CompleteLearningIntent", true);
				check(Get("reflectionIntent") is null && !(bool)Call("BeginChoiceQuestion", CompanionQuestion.Reflection)!,
					"Quiet still asked for evaluation");
				int before = mate.Profile.IntentLearning.Attempts(actual);
				Main.netMode = NetmodeID.MultiplayerClient;
				Call("BeginLearningIntent", CompanionInitiativeKind.Gathering, true, (CompanionIntentContext?)CompanionIntentContext.Building);
				Call("CompleteLearningIntent", true);
				check(mate.Profile.IntentLearning.Attempts(actual) == before, "Client manufactured a learned outcome");
				if (mode == NetmodeID.SinglePlayer) {
					Main.netMode = mode; Main.myPlayer = 0; Main.dedServ = Main.gameMenu = Main.playerInventory = false;
					Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(null!);
					Main.GameViewMatrix.SetViewportOverride(new Microsoft.Xna.Framework.Graphics.Viewport(0, 0, 1280, 720));
					Main.UIScale = 1f; width.SetValue(null, 1280); height.SetValue(null, 720);
					mate.Profile.QuestionCadence = CompanionQuestionCadence.Calm;
					var prompt = ModContent.GetInstance<InitiativePromptSystem>();
					for (int index = 0; index < 4; index++) {
						prompt.Close(); Set("reflectionCooldown", 0); Set("personalQuestionCooldown", 0);
						Call("BeginLearningIntent", CompanionInitiativeKind.Treasure, false, (CompanionIntentContext?)CompanionIntentContext.Exploring);
						Call("CompleteLearningIntent", true);
						check((bool)Call("BeginChoiceQuestion", CompanionQuestion.Reflection)!, "UI reflection fixture did not open");
						var reflected = new CompanionIntent(CompanionInitiativeKind.Treasure, CompanionIntentContext.Exploring);
						int prior = mate.Profile.IntentLearning.Feedback(reflected);
						Main.mouseLeft = Main.mouseRight = false;
						prompt.Open(mate);
						check(prompt.IsOpen, "Existing answer wheel did not accept learning question");
						Vector2 point = (Vector2)typeof(InitiativePromptSystem).GetMethod("ResponsePosition", flags)!.Invoke(prompt, new object[] { index })!;
						PlayerInput.MouseX = (int)(point.X * Main.UIScale); PlayerInput.MouseY = (int)(point.Y * Main.UIScale);
						Main.mouseLeft = true; prompt.UpdateUI(new GameTime()); Main.mouseLeft = false;
						check(!mate.HasPendingQuestion && !prompt.IsOpen, "Native UI click did not answer learning question: " + index);
						int expected = prior + (index == 0 ? 8 : index == 1 ? -4 : index == 2 ? -8 : 0);
						check(mate.Profile.IntentLearning.Feedback(reflected) == expected, "Native UI click learned the wrong answer: " + index);
					}
					Main.dedServ = oldDedicated;
				}
			}
		}
		finally {
			Main.player[0] = oldPlayer; Main.npc = oldNpcs; Main.item = oldItems;
			Main.netMode = oldMode; Main.myPlayer = oldLocal;
			Main.dedServ = oldDedicated; Main.gameMenu = oldMenu; Main.playerInventory = oldInventory;
			Main.mouseLeft = oldLeft; Main.mouseRight = oldRight; Main.blockMouse = oldBlock;
			PlayerInput.MouseX = oldMouseX; PlayerInput.MouseY = oldMouseY;
			Main.GameViewMatrix = oldView; Main.UIScale = oldScale; width.SetValue(null, oldWidth); height.SetValue(null, oldHeight);
			ModContent.GetInstance<InitiativePromptSystem>().Close();
			for (int i = 0; i < oldClients.Length; i++) Netplay.Clients[i] = oldClients[i];
		}
	}
}
