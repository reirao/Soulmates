#nullable enable
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
using Soulmates.Content.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace SoulmatesRegressionProbe;

public static class PersonalPlanChecks
{
	private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
	public static void Run(Action<bool, string> check)
	{
		CheckData(check);
		CheckRuntime(check);
	}
	private static void CheckData(Action<bool, string> check)
	{
		var plans = new CompanionPersonalPlans();
		var home = CompanionPersonalPlanKind.HomeVisit;
		check(plans.LastCompleted is null && plans.IsReady(home) && !plans.IsReady(CompanionPersonalPlanKind.None),
			"Personal plans invented a completion or accepted None");
		plans.Evaluate(home, CompanionAnswer.Later);
		check(plans.Preference(home) == 0 && plans.Cooldown(home) == 0, "Later punished a personal undertaking");
		plans.Evaluate(home, CompanionAnswer.Third);
		check(!plans.IsReady(home) && plans.IsReady(CompanionPersonalPlanKind.PetCompany), "Personal feedback leaked between undertakings");
		check(plans.Cooldown(home) > CompanionPersonalPlans.CompletionCooldownTicks, "Less of this did not reduce personal frequency");
		int delay = plans.Cooldown(home);
		for (int i = 0; i <= delay; i++) plans.Tick();
		check(plans.IsReady(home) && plans.Preference(home) == -8, "One negative evaluation permanently forbade a personal undertaking");
		for (int i = 0; i < 1000; i++) plans.Evaluate(home, CompanionAnswer.First);
		plans.Completed(home, "  " + new string('x', 100) + "  ");
		check(plans.Preference(home) == 32 && plans.LastCompleted?.Subject.Length == 80
			&& plans.Cooldown(home) == CompanionPersonalPlans.CompletionCooldownTicks, "Personal data exceeded its bounds");
		var clone = plans.Clone(); clone.Evaluate(home, CompanionAnswer.Third); clone.Tick();
		check(plans.Preference(home) == 32 && clone.Preference(home) == 24
			&& plans.Cooldown(home) == clone.Cooldown(home) + 1, "Sigils share mutable personal data");
		var loaded = CompanionPersonalPlans.Load(plans.Save());
		check(loaded.LastCompleted == plans.LastCompleted && loaded.Cooldown(home) == plans.Cooldown(home)
			&& loaded.Preference(home) == plans.Preference(home), "Personal save/load lost its completion or cooldown");
		check(CompanionPersonalPlans.Load(new TagCompound()).LastCompleted is null, "Old Sigil acquired a fictional undertaking");
		var malformed = new TagCompound { ["preference0"] = int.MinValue, ["cooldown0"] = int.MaxValue, ["lastKind"] = (byte)255 };
		var bounded = CompanionPersonalPlans.Load(malformed); bounded.Normalize();
		check(bounded.Preference(home) == -32 && bounded.Cooldown(home) == CompanionPersonalPlans.MaximumCooldownTicks
			&& bounded.LastCompleted is null, "Malformed saved plans were not bounded");
		byte[] bytes;
		using (var stream = new MemoryStream()) {
			using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) plans.Write(writer);
			bytes = stream.ToArray(); stream.Position = 0;
			using var reader = new BinaryReader(stream, Encoding.UTF8, true);
			var network = CompanionPersonalPlans.Read(reader);
			check(stream.Position == stream.Length && network.LastCompleted == plans.LastCompleted
				&& network.Preference(home) == 32, "Personal wire data was not aligned");
		}
		foreach (int offset in new[] { 0, 1, 2, 16 }) {
			byte[] corrupt = (byte[])bytes.Clone(); corrupt[offset] = 127;
			if (offset == 2) for (int i = 2; i < 6; i++) corrupt[i] = 255;
			bool rejected = false;
			try { using var reader = new BinaryReader(new MemoryStream(corrupt)); CompanionPersonalPlans.Read(reader); }
			catch (InvalidDataException) { rejected = true; }
			check(rejected, "Personal wire accepted invalid version, preference, cooldown or kind at " + offset);
		}
		var profile = new CompanionProfile(); profile.PersonalPlans.Completed(home, "");
		foreach (CompanionProfile copy in new[] { profile.Clone(), CompanionProfile.Load(profile.Save()) }) {
			check(copy.PersonalPlans.LastCompleted == profile.PersonalPlans.LastCompleted
				&& !ReferenceEquals(copy.PersonalPlans, profile.PersonalPlans), "Profile personal Clone/Save/Load hooks are missing");
		}
		using var wire = new MemoryStream();
		using (var writer = new BinaryWriter(wire, Encoding.UTF8, true)) profile.Write(writer);
		wire.Position = 0; using var profileReader = new BinaryReader(wire, Encoding.UTF8, true);
		check(CompanionProfile.Read(profileReader).PersonalPlans.LastCompleted == profile.PersonalPlans.LastCompleted
			&& wire.Position == wire.Length, "Profile personal Write/Read hooks are missing or misaligned");
	}
	private static void CheckRuntime(Action<bool, string> check)
	{
		Player oldPlayer = Main.player[0]; NPC[] oldNpcs = Main.npc; Projectile[] oldProjectiles = Main.projectile;
		Tilemap oldMap = Main.tile; WorldFileData oldWorld = Main.ActiveWorldFileData;
		int oldMode = Main.netMode, oldLocal = Main.myPlayer, oldWidth = Main.maxTilesX, oldHeight = Main.maxTilesY;
		bool oldDedicated = Main.dedServ;
		Guid world = new("094bd045-879b-4977-86a0-d91ef96c5ddb");
		try {
			Main.dedServ = true; Main.myPlayer = 255; Main.maxTilesX = Main.maxTilesY = 120;
			Main.tile = (Tilemap)Activator.CreateInstance(typeof(Tilemap), Flags, null, new object[] { (ushort)120, (ushort)120 }, null)!;
			Main.ActiveWorldFileData = new WorldFileData("", false) { UniqueId = world };
			for (int x = 30; x <= 42; x++) for (int y = 30; y <= 40; y++) {
				Tile tile = Main.tile[x, y];
				if (x == 30 || x == 42 || y == 30 || y == 40) { tile.HasTile = true; tile.TileType = TileID.WoodBlock; }
				else tile.WallType = WallID.Wood;
			}
			Tile chair = Main.tile[33, 38]; chair.HasTile = true; chair.TileType = TileID.Chairs;
			Point home = new(36, 35);
			foreach (int mode in new[] { NetmodeID.SinglePlayer, NetmodeID.Server }) {
				Main.netMode = mode;
				Player owner = null!; SoulboundCompanion mate = null!; SoulboundSigil sigil = null!;
				object? Call(string name, params object?[] args) => typeof(SoulboundCompanion).GetMethod(name, Flags)!.Invoke(mate, args);
				void Set(string name, object? value) => typeof(SoulboundCompanion).GetField(name, Flags)!.SetValue(mate, value);
				void Verify(bool condition, string message) => check(condition, message + ": " + mode);
				byte[] StateBytes() {
					using var stream = new MemoryStream();
					using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) Call("WritePersonalPlanState", writer);
					return stream.ToArray();
				}
				void ReadState(byte[] bytes) {
					using var stream = new MemoryStream(bytes); using var reader = new BinaryReader(stream);
					Call("ReadPersonalPlanState", reader);
					Verify(stream.Position == stream.Length, "Personal presentation wire roundtrip was not aligned");
				}
				void RejectState(byte[] bytes) {
					var kind = mate.CurrentPersonalPlan; bool returning = mate.IsReturningFromPersonalPlan;
					int remaining = mate.PersonalPlanTicksRemaining; string subject = mate.PersonalPlanSubject, reflection = mate.ReflectionSubject;
					bool rejected = false;
					try { ReadState(bytes); }
					catch (TargetInvocationException error) when (error.InnerException is InvalidDataException or EndOfStreamException) { rejected = true; }
					Verify(rejected && mate.CurrentPersonalPlan == kind && mate.IsReturningFromPersonalPlan == returning
						&& mate.PersonalPlanTicksRemaining == remaining && mate.PersonalPlanSubject == subject && mate.ReflectionSubject == reflection,
						"Malformed personal display state was accepted or partially applied");
				}
				void Reset() {
					Main.npc = Enumerable.Range(0, oldNpcs.Length).Select(i => new NPC { whoAmI = i }).ToArray();
					Main.projectile = Enumerable.Range(0, oldProjectiles.Length).Select(i => new Projectile { whoAmI = i }).ToArray();
					owner = new Player { whoAmI = 0, active = true, direction = 1,
						Center = home.ToWorldCoordinates() + new Vector2(-64, 48) };
					Main.player[0] = owner;
					NPC npc = Main.npc[20]; npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
					npc.active = true; npc.ai[0] = 0; npc.Center = owner.Center + new Vector2(-66, -58);
					mate = (SoulboundCompanion)npc.ModNPC;
					mate.Profile.AutonomyEnabled = true; mate.Profile.Routine = CompanionJob.None;
					mate.Profile.Energy = 50; mate.Profile.Mood = 50; mate.Profile.QuestionCadence = CompanionQuestionCadence.Quiet;
					owner.inventory[0] = new Item(ModContent.ItemType<SoulboundSigil>());
					sigil = (SoulboundSigil)owner.inventory[0].ModItem; sigil.Profile = mate.Profile.Clone();
					var idle = new CompanionActivityCoordinator(new[] { new CompanionActivityModule(CompanionActivityLane.Follow, () => true) });
					idle.Tick(); Set("activityCoordinator", idle); Set("personalPlanDecisionTicks", 0);
				}
				void Supply() {
					Reset(); mate.Profile.Store(new Item(ItemID.Wood, 7));
					mate.Profile.IntentLearning.ObserveBuildingSupply("Terraria/" + ItemID.Wood);
					Call("ObserveIntentContext", CompanionIntentContext.Building); Call("TickPersonalPlans");
					Verify(mate.CurrentPersonalPlan == CompanionPersonalPlanKind.SupplyReflection, "Recognized real supplies did not start a personal reflection");
				}
				void Ticks(int count, bool move = false) {
					for (int i = 0; i < count; i++) {
						Call("TickPersonalPlans");
						if (move) { Call("TickFollowActivity"); mate.NPC.position += mate.NPC.velocity; }
					}
				}
				Projectile Familiar() {
					Reset(); mate.Profile.Store(new Item(ItemID.ZephyrFish)); mate.Profile.PetItemType = ItemID.ZephyrFish;
					Projectile pet = Main.projectile[10]; pet.SetDefaults(ModContent.ProjectileType<CompanionFamiliar>());
					pet.active = true; pet.Center = mate.NPC.Center; pet.ai[0] = mate.NPC.whoAmI; pet.ai[1] = ItemID.ZephyrFish;
					typeof(CompanionFamiliar).GetMethod("Bind", Flags)!.Invoke(pet.ModProjectile, new object[] { mate.Profile.Id });
					Ticks(1);
					Verify(mate.CurrentPersonalPlan == CompanionPersonalPlanKind.PetCompany, "A real bound familiar was ignored");
					return pet;
				}
				Reset(); mate.Profile.Places.Observe(world, home); Ticks(1);
				Verify(mate.CurrentPersonalPlan == CompanionPersonalPlanKind.None, "An unclassified shelter started a home visit");
				mate.Profile.Places.RememberRole(world, home, CompanionPlaceRole.SharedHome); Set("personalPlanDecisionTicks", 0); Ticks(1);
				Verify(mate.CurrentPersonalPlan == CompanionPersonalPlanKind.HomeVisit, "A nearby confirmed home was ignored");
				Ticks(300, move: true);
				Verify(mate.IsReturningFromPersonalPlan, "Home observation did not transition into return movement");
				byte[] returningState = StateBytes(); Main.netMode = NetmodeID.MultiplayerClient; ReadState(returningState);
				Verify(mate.CurrentPersonalPlan == CompanionPersonalPlanKind.HomeVisit && mate.IsReturningFromPersonalPlan
					&& mate.PersonalPlanTicksRemaining > 0, "Client lost a returning home visit");
				Main.netMode = mode;
				Ticks(CompanionPersonalPlans.MaximumDurationTicks, move: true);
				Verify(mate.Profile.PersonalPlans.LastCompleted?.Kind == CompanionPersonalPlanKind.HomeVisit
					&& sigil.Profile.PersonalPlans.LastCompleted == mate.Profile.PersonalPlans.LastCompleted,
					"A real home visit and return did not persist completion");
				Reset(); mate.Profile.Places.RememberRole(world, home, CompanionPlaceRole.CompanionHome); Ticks(1);
				Ticks(CompanionPersonalPlans.MaximumDurationTicks);
				Verify(mate.CurrentPersonalPlan == CompanionPersonalPlanKind.None && mate.Profile.PersonalPlans.LastCompleted is null,
					"Elapsed time without reaching home fabricated completion");
				Reset(); mate.Profile.Places.RememberRole(world, home, CompanionPlaceRole.SharedHome); Ticks(1);
				mate.Profile.Places.RememberRole(world, home, CompanionPlaceRole.NotHome); Ticks(1);
				Verify(mate.CurrentPersonalPlan == CompanionPersonalPlanKind.None && mate.Profile.PersonalPlans.LastCompleted is null,
					"A player correction to NotHome was ignored");
				Reset(); mate.Profile.Places.RememberRole(Guid.NewGuid(), home, CompanionPlaceRole.SharedHome); Ticks(1);
				Verify(mate.CurrentPersonalPlan == CompanionPersonalPlanKind.None, "A home from another world was visited");
				Reset(); mate.Profile.Places.RememberRole(world, home, CompanionPlaceRole.SharedHome); Ticks(1);
				Tile roof = Main.tile[36, 30]; roof.HasTile = false; Ticks(32); roof.HasTile = true;
				Verify(mate.CurrentPersonalPlan == CompanionPersonalPlanKind.None && mate.Profile.PersonalPlans.LastCompleted is null,
					"A destroyed home still completed a personal visit");

				Supply(); int xp = mate.Profile.Experience, jobs = mate.Profile.JobsCompleted, energy = mate.Profile.Energy;
				Ticks(298);
				Verify(mate.Profile.PersonalPlans.LastCompleted is null, "Personal undertaking completed before five seconds");
				Ticks(1);
				Verify(mate.Profile.PersonalPlans.LastCompleted?.Kind == CompanionPersonalPlanKind.SupplyReflection
					&& mate.Profile.ItemCount(ItemID.Wood) == 7 && mate.Profile.Experience == xp
					&& mate.Profile.JobsCompleted == jobs && mate.Profile.Energy == energy,
					"Supply reflection fabricated inventory, XP, work or energy changes");
				Ticks(900);
				Verify(mate.CurrentPersonalPlan == CompanionPersonalPlanKind.None, "Saved cooldown allowed a repeated personal completion");
				Supply(); mate.Profile.Resources.Clear(); mate.Profile.Pack.Clear(); Ticks(1);
				Verify(mate.CurrentPersonalPlan == CompanionPersonalPlanKind.None && mate.Profile.PersonalPlans.LastCompleted is null,
					"Missing supplies still completed a reflection");
				Reset(); mate.Profile.Store(new Item(ItemID.Wood, 7)); Call("ObserveIntentContext", CompanionIntentContext.Building); Ticks(1);
				Verify(mate.CurrentPersonalPlan == CompanionPersonalPlanKind.None, "Unobserved cargo was described as a familiar building supply");

				Action[] interrupt = {
					() => mate.Profile.AutonomyEnabled = false,
					() => mate.Profile.WorkPaused = true,
					() => mate.NPC.ai[1] = 1f,
					() => Set("activeJob", CompanionJob.Mine),
					() => mate.Profile.Routine = CompanionJob.Gather,
					() => { Set("pendingQuestion", CompanionQuestion.Company); Set("questionTicks", 60); },
					() => { FieldInfo field = typeof(SoulboundCompanion).GetField("pendingAutonomyActivity", Flags)!;
						field.SetValue(mate, Enum.Parse(field.FieldType, "FetchItem")); },
					() => Set("guardianTarget", 1),
					() => { Main.npc[1].SetDefaults(NPCID.Zombie); Main.npc[1].active = true; Main.npc[1].Center = owner.Center; },
					() => owner.inventory[0].TurnToAir(),
					() => owner.dead = true,
					() => owner.active = false,
					() => owner.velocity = new Vector2(4, 0)
				};
				foreach (Action preempt in interrupt) {
					Supply(); preempt(); Ticks(1);
					Verify(mate.CurrentPersonalPlan == CompanionPersonalPlanKind.None && mate.Profile.PersonalPlans.LastCompleted is null,
						"Higher priority state failed to cancel a personal undertaking");
				}
				Supply(); Call("BeginLearningIntent", CompanionInitiativeKind.Mining, true, null);
				Verify(mate.CurrentPersonalPlan == CompanionPersonalPlanKind.None, "Work intent did not immediately preempt personal movement");
				Supply(); mate.ClearPersonalPlansRuntime();
				Verify(mate.CurrentPersonalPlan == CompanionPersonalPlanKind.None && mate.PersonalPlanTicksRemaining == 0,
					"Personal cleanup retained a sticky movement target");
				Supply(); mate.Profile.QuestionCadence = CompanionQuestionCadence.Calm; Ticks(299);
				var work = new CompanionIntent(CompanionInitiativeKind.Gathering, CompanionIntentContext.Building);
				int prior = mate.Profile.IntentLearning.Feedback(work);
				Verify(mate.ReflectionSubject == SoulmatesText.Get("PersonalPlans.Subject.SupplyReflection", Lang.GetItemNameValue(ItemID.Wood)),
					"Reflection subject did not describe the captured personal outcome");
				byte[] reflectionState = StateBytes(); Main.netMode = NetmodeID.MultiplayerClient; ReadState(reflectionState);
				Verify(mate.ReflectionSubject == SoulmatesText.Get("PersonalPlans.Subject.SupplyReflection", Lang.GetItemNameValue(ItemID.Wood)),
					"Client reflection did not use the matching captured personal completion");
				byte[] wrongReflection = (byte[])reflectionState.Clone(); wrongReflection[^1] = (byte)CompanionPersonalPlanKind.HomeVisit;
				RejectState(wrongReflection); Main.netMode = mode;
				Call("ObserveIntentContext", CompanionIntentContext.Mining);
				Call("EvaluateReflection", CompanionAnswer.Later);
				Verify(mate.Profile.PersonalPlans.Preference(CompanionPersonalPlanKind.SupplyReflection) == 0,
					"Later punished a captured personal reflection");
				Call("EvaluateReflection", CompanionAnswer.Third);
				Verify(mate.Profile.PersonalPlans.Preference(CompanionPersonalPlanKind.SupplyReflection) == -8
					&& mate.Profile.IntentLearning.Feedback(work) == prior, "Personal evaluation changed unrelated work feedback");
				Call("ClearReflection");
				Supply(); byte[] activeState = StateBytes(); Main.netMode = NetmodeID.MultiplayerClient; ReadState(activeState); Ticks(900);
				Verify(mate.PersonalPlanTicksRemaining == 899 && mate.Profile.PersonalPlans.LastCompleted is null,
					"A client advanced or completed an authoritative personal plan");
				Verify(mate.CurrentPersonalPlan == CompanionPersonalPlanKind.SupplyReflection
					&& mate.PersonalPlanSubject == Lang.GetItemNameValue(ItemID.Wood) && mate.PersonalPlanStatusText.Length > 0,
					"Client did not display the bounded current personal undertaking");
				foreach (int offset in new[] { 0, 1, 2, 3 }) {
					byte[] corrupt = (byte[])activeState.Clone(); corrupt[offset] = 127;
					if (offset == 3) { corrupt[3] = 255; corrupt[4] = 255; }
					RejectState(corrupt);
				}
				byte[] impossibleNone = (byte[])activeState.Clone(); impossibleNone[1] = 0; RejectState(impossibleNone);
				byte[] phantomReflection = (byte[])activeState.Clone(); phantomReflection[^1] = (byte)CompanionPersonalPlanKind.SupplyReflection;
				RejectState(phantomReflection);
				using (var oversized = new MemoryStream()) {
					using (var writer = new BinaryWriter(oversized, Encoding.UTF8, true)) {
						writer.Write((byte)1); writer.Write((byte)CompanionPersonalPlanKind.SupplyReflection);
						writer.Write(false); writer.Write((ushort)100); writer.Write(new string('x', 81)); writer.Write((byte)0);
					}
					RejectState(oversized.ToArray());
				}
				Main.netMode = mode;
				mate.ClearPersonalPlansRuntime(); byte[] clearedState = StateBytes(); Main.netMode = NetmodeID.MultiplayerClient; ReadState(clearedState);
				Verify(mate.CurrentPersonalPlan == CompanionPersonalPlanKind.None && mate.PersonalPlanTicksRemaining == 0
					&& mate.PersonalPlanStatusText.Length == 0, "Client retained cleared personal status"); Main.netMode = mode;

				Reset(); mate.Profile.Store(new Item(ItemID.ZephyrFish)); mate.Profile.PetItemType = ItemID.ZephyrFish;
				Ticks(1); Verify(mate.CurrentPersonalPlan == CompanionPersonalPlanKind.None, "An equipped item without a real familiar invented company");
				Projectile pet = Familiar(); Ticks(299);
				Verify(mate.Profile.PersonalPlans.LastCompleted?.Kind == CompanionPersonalPlanKind.PetCompany
					&& mate.Profile.ItemCount(ItemID.ZephyrFish) == 1, "Real familiar company failed completion or changed ownership");
				pet = Familiar();
				pet.active = false; Ticks(1);
				Verify(mate.CurrentPersonalPlan == CompanionPersonalPlanKind.None && mate.Profile.PersonalPlans.LastCompleted is null,
					"A lost familiar fabricated completed company");
				Familiar(); mate.Profile.SetInitiativePolicy(CompanionInitiativeKind.CritterCompany, CompanionInitiativePolicy.Never); Ticks(1);
				Verify(mate.CurrentPersonalPlan == CompanionPersonalPlanKind.None && mate.Profile.PersonalPlans.LastCompleted is null,
					"Personal company bypassed Never");
				Familiar(); mate.Profile.CritterMode = CompanionCritterMode.Off; Ticks(1);
				Verify(mate.CurrentPersonalPlan == CompanionPersonalPlanKind.None, "Personal company bypassed Critter Off");
				if (mode == NetmodeID.Server) {
					Supply(); Type sync = typeof(CompanionProfile).Assembly.GetType("Soulmates.Common.CompanionInventorySync")!;
					Type pending = sync.GetNestedType("Pending", BindingFlags.NonPublic)!;
					ConstructorInfo constructor = pending.GetConstructors(Flags).Single(c => c.GetParameters().Length == 6);
					var transactions = (IDictionary)sync.GetField("Transactions", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
					object? previous = transactions[0]; int cooldown = mate.Profile.PersonalPlans.Cooldown(CompanionPersonalPlanKind.SupplyReflection);
					try {
						transactions[0] = constructor.Invoke(new object?[] { owner, Guid.NewGuid(),
							Activator.CreateInstance(constructor.GetParameters()[2].ParameterType), Array.Empty<byte[]>(), mate, mate.Profile.Clone() });
						mate.AI();
						Ticks(1);
						Verify(mate.CurrentPersonalPlan == CompanionPersonalPlanKind.None
							&& mate.Profile.PersonalPlans.Cooldown(CompanionPersonalPlanKind.SupplyReflection) == cooldown,
							"Pending inventory receipt allowed personal progress or competing persistence");
					}
					finally { if (previous is null) transactions.Remove(0); else transactions[0] = previous; }
					Supply(); mate.Profile.QuestionCadence = CompanionQuestionCadence.Calm; Ticks(299);
					Guid token = Guid.NewGuid();
					Set("pendingQuestion", CompanionQuestion.Reflection); Set("questionTicks", 120); Set("questionId", token);
					FieldInfo capturedField = typeof(SoulboundCompanion).GetField("personalPlanReflection", Flags)!;
					object? captured = capturedField.GetValue(mate);
					string capturedSubject = mate.ReflectionSubject;
					byte[] capturedState = StateBytes();
					CompanionProfile beforeWithdrawal = mate.Profile.Clone();
					cooldown = mate.Profile.PersonalPlans.Cooldown(CompanionPersonalPlanKind.SupplyReflection);
					Verify(captured is not null && mate.HasPendingQuestion, "Inventory reflection fixture did not capture a completed undertaking");
					try {
						transactions[0] = constructor.Invoke(new object?[] { owner, Guid.NewGuid(),
							Activator.CreateInstance(constructor.GetParameters()[2].ParameterType), Array.Empty<byte[]>(), mate, beforeWithdrawal });
						mate.Profile.Resources.Clear(); mate.Profile.Pack.Clear(); owner.inventory[1] = new Item(ItemID.Wood, 7);
						mate.AI(); Ticks(1);
						Verify(mate.CurrentPersonalPlan == CompanionPersonalPlanKind.None && mate.HasPendingQuestion
							&& mate.PendingQuestion == CompanionQuestion.Reflection && mate.QuestionId == token && mate.QuestionTicks == 120
							&& ReferenceEquals(capturedField.GetValue(mate), captured) && mate.ReflectionSubject == capturedSubject
							&& mate.Profile.PersonalPlans.LastCompleted == beforeWithdrawal.PersonalPlans.LastCompleted
							&& mate.Profile.PersonalPlans.Cooldown(CompanionPersonalPlanKind.SupplyReflection) == cooldown
							&& capturedState.SequenceEqual(StateBytes()),
							"Pending cargo withdrawal erased or rewrote the unanswered personal reflection");
					}
					finally { if (previous is null) transactions.Remove(0); else transactions[0] = previous; }
					Call("EvaluateReflection", CompanionAnswer.Third);
					Verify(mate.Profile.PersonalPlans.Preference(CompanionPersonalPlanKind.SupplyReflection) == -8
						&& mate.Profile.ItemCount(ItemID.Wood) == 0 && owner.inventory[1].stack == 7,
						"Reflection lost its feedback target after the withdrawn supplies were gone");
					Call("ClearChoiceQuestion");
				}
			}
		}
		finally {
			Main.player[0] = oldPlayer; Main.npc = oldNpcs; Main.projectile = oldProjectiles;
			Main.tile = oldMap; Main.ActiveWorldFileData = oldWorld; Main.netMode = oldMode; Main.myPlayer = oldLocal;
			Main.maxTilesX = oldWidth; Main.maxTilesY = oldHeight; Main.dedServ = oldDedicated;
		}
	}
}
