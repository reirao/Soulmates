#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Soulmates.Common;
using Soulmates.Common.Dialogue;
using Soulmates.Common.UI;
using Soulmates.Content.Items;
using Soulmates.Content.Projectiles;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulmates.Content.NPCs;

public sealed class SoulboundCompanion : ModNPC
{
	private static readonly MethodInfo? ShakeTreeMethod = typeof(WorldGen).GetMethod("ShakeTree",
		BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
	private static readonly FieldInfo? TreeShakeCountField = typeof(WorldGen).GetField("numTreeShakes",
		BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

	private enum BrainState
	{
		Follow,
		Idle,
		Wander,
		Inspect,
		CatchUp,
		Stay,
		Guard
	}

	private enum AutonomyActivity : byte
	{
		None,
		FetchItem,
		AssistMining,
		InspectTreasure,
		TendForest
	}

	private enum ForestAction : byte
	{
		None,
		ShakeTree,
		ClearDeadwood,
		PlantAcorn
	}

	private const float FollowCommand = 0f;
	private const float StayCommand = 1f;
	private const float StandardDefenseRange = 320f;
	private const float GuardianDefenseRange = 448f;
	private const int AutonomousLootSweepLimit = 12;
	private const int AutonomousLootTargetTimeout = 480;
	private const int AutonomousForestSweepLimit = 5;
	private BrainState brainState;
	private int stateTimer;
	private int facing = 1;
	private int facingCooldown;
	private Vector2 idleTarget;
	private float bobSeed;
	private CompanionJob activeJob;
	private int jobTimer;
	private int jobCount;
	private Point jobTarget;
	private bool hasJobTarget;
	private int recoveryTimer;
	private int gatherPause;
	private int jobTargetItem = -1;
	private int areaEmptyTimer;
	private int revealTimer;
	private int talentCooldown;
	private int guardianTarget = -1;
	private Vector2 jobOrigin;
	private int socialTimer;
	private int interactionRewardCooldown;
	private int nativeEmoteReactionCooldown;
	private int combatReactionCooldown;
	private int speechTimer;
	private int emoteTimer;
	private int chatterSequence;
	private int townNpcInteractionCooldown;
	private int socialNpcTarget = -1;
	private int socialNpcTimer;
	private bool socialNpcGreeted;
	private AutonomyActivity autonomyActivity;
	private int autonomyDecisionTimer;
	private int autonomyActionTimer;
	private int autonomyTargetItem = -1;
	private Point autonomyTargetTile;
	private int autonomyWorkCount;
	private int autonomyDiscoveryCooldown;
	private ForestAction autonomyForestAction;
	private int learningObservationTimer;
	private int tendedForestResetTimer;
	private string speechText = "";
	private CompanionEmote activeEmote;
	private readonly HashSet<Point> failedMiningTargets = [];
	private readonly HashSet<Point> tendedForestTargets = [];

	public CompanionProfile Profile { get; set; } = new();
	private Player Owner => Main.player[(int)NPC.ai[0]];
	private ref float Command => ref NPC.ai[1];
	public string CommandName => SoulmatesText.Get(Command == StayCommand ? "Status.Stay" : "Status.Follow");
	public CompanionJob CurrentJob => activeJob;
	public string CurrentJobName => activeJob != CompanionJob.None
		? activeJob switch {
			CompanionJob.Mine => SoulmatesText.Get("Status.Mining", jobCount),
			CompanionJob.Gather => SoulmatesText.Get("Status.Gathering", jobCount),
			CompanionJob.FindTreasure => SoulmatesText.Get("Status.SensingTreasure"),
			_ => SoulmatesText.EnumName(activeJob)
		}
		: autonomyActivity != AutonomyActivity.None
			? SoulmatesText.Get($"Status.Autonomy.{autonomyActivity}")
		: guardianTarget >= 0
			? SoulmatesText.Get("Status.Guarding")
			: Profile.Energy < 12
			? SoulmatesText.Get("Status.Recovering")
			: Profile.Routine != CompanionJob.None
			? SoulmatesText.Get("Status.Assignment", SoulmatesText.EnumName(Profile.Routine))
			: SoulmatesText.Get("Status.Watching", (int)(DefenseRange / 16f));
	public int CurrentJobRadius => (activeJob != CompanionJob.None ? activeJob : Profile.Routine) switch {
		CompanionJob.Mine => MiningRadiusTiles,
		CompanionJob.Gather => GatheringRadiusTiles,
		CompanionJob.FindTreasure => TreasureRadiusTiles,
		_ => 0
	};
	private int MiningRadiusTiles => (Profile.Trinket == CompanionTrinket.DelverCharm ? 34 : 26) + Profile.RankIndex * 2;
	private int GatheringRadiusTiles => (Profile.Trinket == CompanionTrinket.HearthRibbon ? 42 : 30) + Profile.RankIndex * 2;
	private int TreasureRadiusTiles => (Profile.Trinket == CompanionTrinket.StarfinderBell ? 90 : 55) + Profile.RankIndex * 3;
	private bool GuardianSpecialist => Profile.HasTalent(CompanionTalent.Guardian) || Profile.CombatInsight >= 36;
	private bool EagerGatherer => Profile.HasTalent(CompanionTalent.Gatherer) || Profile.GatheringInsight >= 32;
	private bool MiningInstinct => Profile.HasTalent(CompanionTalent.Miner) || Profile.MiningInsight >= 20;
	private bool TreasureInstinct => Profile.HasTalent(CompanionTalent.TreasureSeeker) || Profile.ExplorationInsight >= 24;
	private float DefenseRange => (GuardianSpecialist ? GuardianDefenseRange : StandardDefenseRange)
		+ Profile.RankIndex * (GuardianSpecialist ? 24f : 16f);
	private float DefenseLeash => DefenseRange + (GuardianSpecialist ? 112f : 80f);
	private float PursuitRadius => (GuardianSpecialist ? 304f : 208f)
		+ Profile.RankIndex * (GuardianSpecialist ? 16f : 12f);
	private float PassiveAlertRange => (GuardianSpecialist ? 288f : 208f)
		+ Profile.RankIndex * 12f;

	public override void SetStaticDefaults()
	{
		Main.npcFrameCount[Type] = 1;
	}

	public override void SetDefaults()
	{
		NPC.width = 42;
		NPC.height = 46;
		NPC.lifeMax = 250;
		NPC.damage = 0;
		NPC.defense = 10;
		NPC.friendly = true;
		NPC.noGravity = true;
		NPC.noTileCollide = true;
		NPC.dontTakeDamage = true;
		NPC.netAlways = true;
		bobSeed = Main.rand.NextFloat(MathHelper.TwoPi);
		socialTimer = Main.rand.Next(1000, 1800);
		townNpcInteractionCooldown = Main.rand.Next(1200, 2200);
		autonomyDecisionTimer = Main.rand.Next(360, 720);
		tendedForestResetTimer = Main.rand.Next(1800, 3600);
	}

	public override bool CheckActive() => !TryGetOwner(out Player owner) || owner.dead;

	public override bool CanChat() => TryGetOwner(out Player owner) && owner.whoAmI == Main.myPlayer;
	public override string GetChat() => SoulmatesText.Get("UI.Talk.Greeting", Profile.Name);

	public override bool PreHoverInteract(bool mouseIntersects)
	{
		if (mouseIntersects && Main.mouseRight && Main.mouseRightRelease
			&& TryGetOwner(out Player owner) && owner.whoAmI == Main.myPlayer) {
			ModContent.GetInstance<CompanionWheelSystem>().Open(this);
			Main.LocalPlayer.mouseInterface = true;
			Main.blockMouse = true;
			Main.mouseRightRelease = false;
		}
		return false;
	}

	public override void AI()
	{
		if (!TryGetOwner(out Player owner) || owner.dead) {
			StopOwnedEffects();
			NPC.active = false;
			NPC.netUpdate = true;
			return;
		}
		if (Main.netMode != NetmodeID.MultiplayerClient && !ClaimActiveSlot(owner))
			return;

		NPC.GivenName = Profile.Name;
		Lighting.AddLight(NPC.Center, Profile.EssenceColor.ToVector3() * 1.15f);
		RevealSurroundings();
		ResumeAssignment();
		UpdateAuraDust();
		UpdateSocialState();
		UpdateLearningFromOwner();
		UpdateAutonomousSocialBehavior();
		if (talentCooldown > 0)
			talentCooldown--;
		if (UpdateTalentBehavior()) {
			recoveryTimer = 0;
			NPC.rotation = MathHelper.Lerp(NPC.rotation, NPC.velocity.X * 0.025f, 0.08f);
			UpdateFacing();
			return;
		}
		if (activeJob != CompanionJob.None) {
			recoveryTimer = 0;
			guardianTarget = -1;
			UpdateJob();
			NPC.rotation = MathHelper.Lerp(NPC.rotation, NPC.velocity.X * 0.025f, 0.08f);
			UpdateFacing();
			return;
		}
		if (UpdateHelpfulAutonomy()) {
			recoveryTimer = 0;
			NPC.rotation = MathHelper.Lerp(NPC.rotation, NPC.velocity.X * 0.025f, 0.08f);
			UpdateFacing();
			return;
		}
		if (UpdateTownNpcInteraction()) {
			RecoverEnergy(180, Profile.Trinket == CompanionTrinket.HearthRibbon ? 2 : 1, recoverMood: false);
			NPC.rotation = MathHelper.Lerp(NPC.rotation, NPC.velocity.X * 0.025f, 0.08f);
			UpdateFacing();
			return;
		}
		if (Command == StayCommand) {
			if (brainState != BrainState.Stay)
				brainState = BrainState.Stay;
			MoveTo(idleTarget + new Vector2(0f, IdleBob()), 2.2f, 0.04f);
			RecoverEnergy(120, Profile.Trinket == CompanionTrinket.HearthRibbon ? 5 : 3, recoverMood: true);
			UpdateFacing();
			return;
		}
		RecoverEnergy(180, Profile.Trinket == CompanionTrinket.HearthRibbon ? 2 : 1, recoverMood: false);

		Vector2 followTarget = Owner.Center + new Vector2(-Owner.direction * 66f, -58f);
		float distance = Vector2.Distance(NPC.Center, followTarget);
		if (distance > 1200f) {
			NPC.Center = followTarget;
			NPC.velocity = Vector2.Zero;
			brainState = BrainState.Follow;
			stateTimer = 60;
			NPC.netUpdate = true;
			return;
		}

		if (distance > 340f) {
			brainState = BrainState.CatchUp;
			stateTimer = 45;
		}

		if (stateTimer-- <= 0)
			ChooseNextState(distance, followTarget);

		switch (brainState) {
			case BrainState.Idle:
				MoveTo(followTarget + new Vector2(0f, IdleBob()), 2.4f, 0.035f);
				break;
			case BrainState.Wander:
				MoveTo(idleTarget + new Vector2(0f, IdleBob() * 0.5f), 4.2f, 0.055f);
				break;
			case BrainState.Inspect:
				Vector2 inspectOffset = new Vector2(MathF.Sin((stateTimer + bobSeed) * 0.035f) * 26f, -82f + IdleBob());
				MoveTo(Owner.Center + inspectOffset, 3.4f, 0.045f);
				break;
			case BrainState.CatchUp:
				MoveTo(followTarget, 14f, 0.13f);
				break;
			default:
				MoveTo(followTarget, 8f, 0.075f);
				break;
		}

		NPC.rotation = MathHelper.Lerp(NPC.rotation, NPC.velocity.X * 0.025f, 0.08f);
		UpdateFacing();
	}

	private bool UpdateTalentBehavior()
	{
		if (Profile.HasTalent(CompanionTalent.Healer))
			TryHealOwner();

		bool guardian = GuardianSpecialist;
		Vector2 anchor = Command == StayCommand ? idleTarget : Owner.Center;
		NPC? target = Main.netMode == NetmodeID.MultiplayerClient
			? GetSynchronizedDefenseTarget(anchor)
			: FindDefenseTarget(anchor);
		if (target is null) {
			if (Main.netMode != NetmodeID.MultiplayerClient)
				SetGuardianTarget(-1);
			return false;
		}

		if (Main.netMode != NetmodeID.MultiplayerClient)
			SetGuardianTarget(target.whoAmI);
		if (Main.netMode != NetmodeID.MultiplayerClient && socialNpcTarget >= 0)
			ClearTownNpcInteraction();
		brainState = BrainState.Guard;
		Vector2 awayFromTarget = (anchor - target.Center).SafeNormalize(new Vector2(-Owner.direction, 0f));
		Vector2 strafe = new Vector2(-awayFromTarget.Y, awayFromTarget.X)
			* MathF.Sin(Main.GlobalTimeWrappedHourly * 2.4f + bobSeed) * (guardian ? 34f : 24f);
		Vector2 guardPosition;
		if (Command == StayCommand) {
			Vector2 orbit = new Vector2(MathF.Sin(Main.GlobalTimeWrappedHourly * 1.8f + bobSeed) * 24f,
				-10f + IdleBob() * 0.45f);
			guardPosition = idleTarget + orbit;
			MoveTo(guardPosition, guardian ? 5.5f : 4.5f, guardian ? 0.12f : 0.1f);
		}
		else {
			guardPosition = target.Center + awayFromTarget * (guardian ? 145f : 125f)
				+ strafe + new Vector2(0f, -38f + IdleBob() * 0.3f);
			Vector2 fromAnchor = guardPosition - anchor;
			if (fromAnchor.LengthSquared() > PursuitRadius * PursuitRadius)
				guardPosition = anchor + fromAnchor.SafeNormalize(Vector2.UnitX) * PursuitRadius;
			MoveTo(guardPosition, guardian ? 12f : 9f, guardian ? 0.14f : 0.11f);
		}

		float attackRange = guardian ? 520f : 400f;
		if (Main.netMode != NetmodeID.MultiplayerClient && talentCooldown <= 0
			&& Vector2.DistanceSquared(NPC.Center, target.Center) < attackRange * attackRange) {
			Vector2 velocity = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX) * (guardian ? 9f : 7.5f);
			int damage = guardian
				? 8 + Profile.RankIndex * 2 + Math.Min(4, Profile.JobsCompleted / 8)
				: 4 + Profile.RankIndex + Math.Min(2, Profile.JobsCompleted / 12);
			int projectileOwner = Main.netMode == NetmodeID.Server ? 255 : Owner.whoAmI;
			int projectileIndex = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, velocity, ModContent.ProjectileType<SoulBolt>(),
				damage, 1.5f, projectileOwner, target.whoAmI, NPC.whoAmI);
			if (projectileIndex >= 0 && projectileIndex < Main.maxProjectiles) {
				Main.projectile[projectileIndex].npcProj = true;
				Main.projectile[projectileIndex].netUpdate = true;
			}
			NPC.netUpdate = true;
			talentCooldown = guardian ? Math.Max(55, 82 - Profile.RankIndex * 6) : Math.Max(90, 130 - Profile.RankIndex * 8);
		}
		return true;
	}

	private void UpdateSocialState()
	{
		if (speechTimer > 0)
			speechTimer--;
		else
			speechText = "";
		if (interactionRewardCooldown > 0)
			interactionRewardCooldown--;
		if (nativeEmoteReactionCooldown > 0)
			nativeEmoteReactionCooldown--;
		if (combatReactionCooldown > 0)
			combatReactionCooldown--;
		if (townNpcInteractionCooldown > 0)
			townNpcInteractionCooldown--;
		if (tendedForestResetTimer > 0)
			tendedForestResetTimer--;
		else {
			tendedForestTargets.Clear();
			tendedForestResetTimer = 3600;
		}
		if (emoteTimer <= 0)
			return;
		emoteTimer--;
		UpdateEmoteEffects();
	}

	private void UpdateAutonomousSocialBehavior()
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || --socialTimer > 0)
			return;
		int minimum = Profile.Personality == CompanionPersonality.Mischievous ? 1200 : 1500;
		int maximum = Profile.Personality == CompanionPersonality.Gentle ? 3000 : 2600;
		socialTimer = Main.rand.Next(minimum, maximum);
		if (activeJob != CompanionJob.None || autonomyActivity != AutonomyActivity.None
			|| guardianTarget >= 0 || socialNpcTarget >= 0 || Owner.dead
			|| Vector2.DistanceSquared(NPC.Center, Owner.Center) > 520f * 520f)
			return;
		if (TryBeginTownNpcInteraction())
			return;

		string key;
		if (Profile.Energy < 20)
			key = "Social.Autonomous.Context.LowEnergy";
		else if (Profile.Mood < 25)
			key = "Social.Autonomous.Context.LowMood";
		else if (Owner.statLife < Owner.statLifeMax2 / 3)
			key = "Social.Autonomous.Context.Hurt";
		else if (Main.raining)
			key = "Social.Autonomous.Context.Rain";
		else if (Owner.ZoneRockLayerHeight || Owner.ZoneUnderworldHeight)
			key = "Social.Autonomous.Context.Underground";
		else if (!Main.dayTime)
			key = "Social.Autonomous.Context.Night";
		else if (Command == StayCommand)
			key = "Social.Autonomous.Context.Stay";
		else {
			int line = chatterSequence++ % 3;
			key = $"Social.Autonomous.{Profile.Personality}.Line{line}";
		}

		CompanionEmote gesture = Profile.Energy < 20
			? CompanionEmote.Rest
			: Profile.Mood < 25 || Owner.statLife < Owner.statLifeMax2 / 3
				? CompanionEmote.Comfort
				: PersonalityGesture();
		StartEmote(gesture, 110);
		ShowNativeEmote(gesture, 130);
		if (Main.rand.NextBool(4))
			SpeakLocalized(key);
	}

	private void UpdateLearningFromOwner()
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || --learningObservationTimer > 0
			|| Vector2.DistanceSquared(NPC.Center, Owner.Center) > 520f * 520f)
			return;

		learningObservationTimer = 60;
		if (Owner.chest >= 0) {
			learningObservationTimer = 180;
			ObserveOwnerActivity(LearnedBehavior.Exploration);
			return;
		}
		if (!Owner.controlUseItem && Owner.itemAnimation <= 0)
			return;

		Item held = Owner.HeldItem;
		if (held.axe > 0 || held.createTile == TileID.Saplings || held.type == ItemID.Acorn)
			ObserveOwnerActivity(LearnedBehavior.Forestry);
		else if (held.pick > 0)
			ObserveOwnerActivity(LearnedBehavior.Mining);
		else if (held.damage > 0)
			ObserveOwnerActivity(LearnedBehavior.Combat);
	}

	public void ObserveOwnerActivity(LearnedBehavior behavior, int amount = 1)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || !Enum.IsDefined(behavior))
			return;
		int before = Profile.GetInsight(behavior);
		bool unlockedForester = Profile.Observe(behavior, amount);
		int after = Profile.GetInsight(behavior);
		if (after == before)
			return;

		SyncProfileToBoundSigil();
		bool syncMilestone = unlockedForester || after % 5 == 0;
		string message = "";
		if (unlockedForester) {
			Profile.GainExperience(5, out _);
			StartEmote(CompanionEmote.Cheer, 120);
			ShowNativeEmote(CompanionEmote.Cheer, 140);
			SpeakLocalized("Learning.ForesterUnlocked");
			message = SoulmatesText.Get("Messages.PerkUnlocked", Profile.Name, SoulmatesText.Get("Learning.Forester"));
		}

		if (!syncMilestone)
			return;
		NPC.netUpdate = true;
		if (Main.netMode == NetmodeID.Server)
			global::Soulmates.Soulmates.SendProfileUpdate(Owner, this, message);
		else if (!string.IsNullOrEmpty(message))
			Main.NewText(message, Profile.EssenceColor);
	}

	private bool UpdateHelpfulAutonomy()
	{
		if (autonomyDiscoveryCooldown > 0 && Main.netMode != NetmodeID.MultiplayerClient)
			autonomyDiscoveryCooldown--;

		if (!Profile.AutonomyEnabled || Command == StayCommand || Profile.Energy < 16 || Profile.Mood < 15) {
			if (autonomyActivity != AutonomyActivity.None && Main.netMode != NetmodeID.MultiplayerClient)
				CancelAutonomousActivity();
			return false;
		}
		if (autonomyActivity != AutonomyActivity.None
			&& Vector2.DistanceSquared(NPC.Center, Owner.Center) > 560f * 560f) {
			if (Main.netMode != NetmodeID.MultiplayerClient)
				CancelAutonomousActivity(180);
			return false;
		}

		if (autonomyActivity != AutonomyActivity.None)
			return UpdateAutonomousActivity();
		if (socialNpcTarget >= 0)
			return false;
		if (Main.netMode == NetmodeID.MultiplayerClient || --autonomyDecisionTimer > 0)
			return false;

		bool eagerGatherer = EagerGatherer;
		int minimum = eagerGatherer ? 120
			: Profile.Personality is CompanionPersonality.Curious or CompanionPersonality.Mischievous ? 200 : 260;
		int maximum = eagerGatherer ? 260 : 480;
		autonomyDecisionTimer = Main.rand.Next(minimum, maximum);
		if (Vector2.DistanceSquared(NPC.Center, Owner.Center) > 440f * 440f)
			return false;

		if (FindAutonomousLooseItem(out int itemIndex)) {
			BeginAutonomousActivity(AutonomyActivity.FetchItem);
			autonomyTargetItem = itemIndex;
			return true;
		}

		if (MiningInstinct && Owner.HeldItem.pick > 0 && Owner.controlUseItem
			&& FindAutonomousOre(out Point ore)) {
			BeginAutonomousActivity(AutonomyActivity.AssistMining);
			autonomyTargetTile = ore;
			return true;
		}

		if (Profile.ForesterUnlocked && FindAutonomousForestTask(out Point forestTarget, out ForestAction forestAction)) {
			BeginAutonomousActivity(AutonomyActivity.TendForest);
			autonomyTargetTile = forestTarget;
			autonomyForestAction = forestAction;
			return true;
		}

		if (TreasureInstinct && autonomyDiscoveryCooldown <= 0
			&& FindAutonomousChest(out Point chest)) {
			BeginAutonomousActivity(AutonomyActivity.InspectTreasure);
			autonomyTargetTile = chest;
			return true;
		}

		if (Main.rand.NextBool(3))
			PerformAutonomousMoment();
		return false;
	}

	private bool UpdateAutonomousActivity()
	{
		autonomyActionTimer++;
		return autonomyActivity switch {
			AutonomyActivity.FetchItem => UpdateAutonomousFetch(),
			AutonomyActivity.AssistMining => UpdateAutonomousMining(),
			AutonomyActivity.InspectTreasure => UpdateAutonomousTreasure(),
			AutonomyActivity.TendForest => UpdateAutonomousForestry(),
			_ => false
		};
	}

	private bool UpdateAutonomousFetch()
	{
		if (autonomyTargetItem < 0 || autonomyTargetItem >= Main.maxItems) {
			if (Main.netMode == NetmodeID.MultiplayerClient)
				return false;
			if (TryRetargetAutonomousLoot())
				return true;
			FinishAutonomousLootSweep();
			return false;
		}

		Item item = Main.item[autonomyTargetItem];
		bool targetTimedOut = autonomyActionTimer > AutonomousLootTargetTimeout;
		if (!item.active || item.IsAir
			|| Main.netMode != NetmodeID.MultiplayerClient && !CanCollectLooseItem(item)
			|| targetTimedOut) {
			if (Main.netMode == NetmodeID.MultiplayerClient)
				return false;
			int excludedTarget = targetTimedOut ? autonomyTargetItem : -1;
			if (TryRetargetAutonomousLoot(excludedTarget))
				return true;
			FinishAutonomousLootSweep();
			return false;
		}

		MoveTo(item.Center + new Vector2(0f, -10f), EagerGatherer ? 8.5f : 7.5f, 0.085f);
		if (Vector2.DistanceSquared(NPC.Center, item.Center) >= 42f * 42f || Main.netMode == NetmodeID.MultiplayerClient)
			return true;

		int collectedItemIndex = autonomyTargetItem;
		int moved = StoreLooseItem(item);
		if (moved > 0) {
			autonomyWorkCount++;
			Profile.Energy = Math.Max(0, Profile.Energy - 1);
			Profile.GainExperience(EagerGatherer ? 2 : 1, out _);
			SyncProfileToBoundSigil();
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendData(MessageID.SyncItem, -1, -1, null, collectedItemIndex);
			NPC.netUpdate = true;
		}

		if (moved <= 0 || Profile.Energy < 16 || autonomyWorkCount >= AutonomousLootSweepLimit
			|| !TryRetargetAutonomousLoot()) {
			FinishAutonomousLootSweep();
			return false;
		}
		return true;
	}

	private bool TryRetargetAutonomousLoot(int excludedItem = -1)
	{
		autonomyTargetItem = -1;
		autonomyActionTimer = 0;
		if (!FindAutonomousLooseItem(out int nextTarget, excludedItem))
			return false;
		autonomyTargetItem = nextTarget;
		NPC.netUpdate = true;
		return true;
	}

	private void FinishAutonomousLootSweep()
	{
		if (autonomyWorkCount > 0 && Main.rand.NextBool(3)) {
			StartEmote(CompanionEmote.Cheer, 90);
			ShowNativeEmote(CompanionEmote.Cheer, 110);
		}
		CancelAutonomousActivity(EagerGatherer ? 45 : 90);
	}

	private bool UpdateAutonomousMining()
	{
		Vector2 target = autonomyTargetTile.ToWorldCoordinates();
		MoveTo(target, 7f, 0.09f);
		if (Main.netMode == NetmodeID.MultiplayerClient)
			return true;
		if (autonomyActionTimer > 720 || Profile.Energy < 12) {
			FinishAutonomousMining();
			return false;
		}
		if (Vector2.DistanceSquared(NPC.Center, target) > 58f * 58f || autonomyActionTimer % 32 != 0)
			return true;

		if (!WorldGen.InWorld(autonomyTargetTile.X, autonomyTargetTile.Y, 10)
			|| !Main.tile[autonomyTargetTile.X, autonomyTargetTile.Y].HasTile
			|| !IsEarlyOre(Main.tile[autonomyTargetTile.X, autonomyTargetTile.Y].TileType)) {
			if (!FindAutonomousOre(out autonomyTargetTile))
				FinishAutonomousMining();
			NPC.netUpdate = true;
			return autonomyActivity != AutonomyActivity.None;
		}

		WorldGen.KillTile(autonomyTargetTile.X, autonomyTargetTile.Y);
		if (!Main.tile[autonomyTargetTile.X, autonomyTargetTile.Y].HasTile) {
			autonomyWorkCount++;
			Profile.Energy = Math.Max(0, Profile.Energy - 1);
			CollectNearbyLooseItems(target, 96f, 6);
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 0, autonomyTargetTile.X, autonomyTargetTile.Y);
		}
		else {
			FinishAutonomousMining();
			return false;
		}

		if (autonomyWorkCount >= 3 || !FindAutonomousOre(out autonomyTargetTile))
			FinishAutonomousMining();
		NPC.netUpdate = true;
		return autonomyActivity != AutonomyActivity.None;
	}

	private bool UpdateAutonomousTreasure()
	{
		Vector2 target = autonomyTargetTile.ToWorldCoordinates(16f, -24f);
		MoveTo(target, 6.5f, 0.085f);
		if (Main.netMode == NetmodeID.MultiplayerClient)
			return true;
		if (Vector2.DistanceSquared(NPC.Center, target) > 92f * 92f && autonomyActionTimer <= 540)
			return true;

		string direction = DescribeDirection(target - Owner.Center);
		StartEmote(CompanionEmote.Wave, 110);
		ShowNativeEmote(CompanionEmote.Wave, 130);
		SpeakLocalized("Autonomy.Treasure", direction);
		autonomyDiscoveryCooldown = 2400;
		CancelAutonomousActivity(520);
		return false;
	}

	private bool UpdateAutonomousForestry()
	{
		Vector2 target = autonomyTargetTile.ToWorldCoordinates();
		MoveTo(target + new Vector2(0f, -18f), 6.5f, 0.075f);
		if (Main.netMode == NetmodeID.MultiplayerClient)
			return true;
		if (autonomyActionTimer > 600) {
			tendedForestTargets.Add(autonomyTargetTile);
			if (TryRetargetAutonomousForestry())
				return true;
			FinishAutonomousForestry();
			return false;
		}
		if (Vector2.DistanceSquared(NPC.Center, target) > 68f * 68f || autonomyActionTimer % 30 != 0)
			return true;

		bool success = autonomyForestAction switch {
			ForestAction.ShakeTree => ShakeAutonomousTree(),
			ForestAction.ClearDeadwood => ClearAutonomousDeadwood(),
			ForestAction.PlantAcorn => PlantAutonomousAcorn(),
			_ => false
		};
		tendedForestTargets.Add(autonomyTargetTile);
		if (success) {
			autonomyWorkCount++;
			Profile.Energy = Math.Max(0, Profile.Energy - 1);
			Profile.GainExperience(2, out _);
			SyncProfileToBoundSigil();
			NPC.netUpdate = true;
		}

		if (Profile.Energy >= 16 && autonomyWorkCount < AutonomousForestSweepLimit
			&& TryRetargetAutonomousForestry())
			return true;
		FinishAutonomousForestry();
		return false;
	}

	private bool TryRetargetAutonomousForestry()
	{
		if (!FindAutonomousForestTask(out Point nextTarget, out ForestAction nextAction))
			return false;
		autonomyTargetTile = nextTarget;
		autonomyForestAction = nextAction;
		autonomyActionTimer = 0;
		NPC.netUpdate = true;
		return true;
	}

	private void FinishAutonomousForestry()
	{
		if (autonomyWorkCount > 0 && Main.rand.NextBool(2)) {
			StartEmote(CompanionEmote.Cheer, 75);
			ShowNativeEmote(CompanionEmote.Cheer, 90);
		}
		CancelAutonomousActivity(autonomyWorkCount > 0 ? 180 : 260);
	}

	private bool ShakeAutonomousTree()
	{
		if (!WorldGen.InWorld(autonomyTargetTile.X, autonomyTargetTile.Y, 10))
			return false;
		Tile tile = Main.tile[autonomyTargetTile.X, autonomyTargetTile.Y];
		if (!tile.HasTile || !IsTreeTrunk(tile.TileType))
			return false;
		if (ShakeTreeMethod is null)
			return false;
		int previousShakes = TreeShakeCountField?.GetValue(null) is int count ? count : -1;
		try {
			ShakeTreeMethod.Invoke(null, [autonomyTargetTile.X, autonomyTargetTile.Y]);
		}
		catch (Exception exception) when (exception is TargetInvocationException or MethodAccessException or ArgumentException) {
			return false;
		}
		if (previousShakes >= 0 && TreeShakeCountField?.GetValue(null) is int currentShakes
			&& currentShakes <= previousShakes)
			return false;
		CollectNearbyLooseItems(autonomyTargetTile.ToWorldCoordinates(), 360f, 8);
		return true;
	}

	private bool ClearAutonomousDeadwood()
	{
		if (!WorldGen.InWorld(autonomyTargetTile.X, autonomyTargetTile.Y, 10)
			|| !Main.tile[autonomyTargetTile.X, autonomyTargetTile.Y].HasTile
			|| Main.tile[autonomyTargetTile.X, autonomyTargetTile.Y].TileType != TileID.FallenLog)
			return false;

		WorldGen.KillTile(autonomyTargetTile.X, autonomyTargetTile.Y);
		bool success = !Main.tile[autonomyTargetTile.X, autonomyTargetTile.Y].HasTile;
		if (success) {
			CollectNearbyLooseItems(autonomyTargetTile.ToWorldCoordinates(), 240f, 8);
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 0,
					autonomyTargetTile.X, autonomyTargetTile.Y);
		}
		return success;
	}

	private bool PlantAutonomousAcorn()
	{
		if (!HasPackItem(ItemID.Acorn) || !CanPlantAcornAt(autonomyTargetTile.X, autonomyTargetTile.Y))
			return false;
		bool placed = WorldGen.PlaceTile(autonomyTargetTile.X, autonomyTargetTile.Y, TileID.Saplings,
			mute: true, forced: false, plr: Owner.whoAmI, style: 0);
		if (!placed || !Main.tile[autonomyTargetTile.X, autonomyTargetTile.Y].HasTile)
			return false;

		ConsumePackItem(ItemID.Acorn);
		WorldGen.SquareTileFrame(autonomyTargetTile.X, autonomyTargetTile.Y);
		if (Main.netMode == NetmodeID.Server)
			NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 1,
				autonomyTargetTile.X, autonomyTargetTile.Y, TileID.Saplings);
		return true;
	}

	private bool FindAutonomousLooseItem(out int result, int excludedItem = -1)
	{
		result = -1;
		float radius = (EagerGatherer ? 20f : 13f) * 16f;
		if (Profile.Trinket == CompanionTrinket.HearthRibbon)
			radius += 80f;
		float bestScore = float.MaxValue;
		for (int i = 0; i < Main.maxItems; i++) {
			Item item = Main.item[i];
			if (i == excludedItem || !item.active || item.IsAir || !CanCollectLooseItem(item)
				|| item.playerIndexTheItemIsReservedFor != 255 && item.playerIndexTheItemIsReservedFor != Owner.whoAmI)
				continue;
			float ownerDistance = Vector2.DistanceSquared(Owner.Center, item.Center);
			if (ownerDistance >= radius * radius)
				continue;
			float score = Vector2.DistanceSquared(NPC.Center, item.Center);
			if (item.type == ItemID.FallenStar && TreasureInstinct)
				score *= 0.55f;
			if (score >= bestScore)
				continue;
			bestScore = score;
			result = i;
		}
		return result >= 0;
	}

	private bool FindAutonomousOre(out Point result)
	{
		Point center = Owner.Center.ToTileCoordinates();
		result = Point.Zero;
		float bestScore = float.MaxValue;
		const int radius = 9;
		for (int x = center.X - radius; x <= center.X + radius; x++) {
			for (int y = center.Y - radius; y <= center.Y + radius; y++) {
				if (!WorldGen.InWorld(x, y, 10))
					continue;
				Tile tile = Main.tile[x, y];
				if (!tile.HasTile || !IsEarlyOre(tile.TileType))
					continue;
				var candidate = new Point(x, y);
				float score = Vector2.DistanceSquared(candidate.ToWorldCoordinates(), NPC.Center);
				if (score >= bestScore)
					continue;
				bestScore = score;
				result = candidate;
			}
		}
		return result != Point.Zero;
	}

	private bool FindAutonomousForestTask(out Point result, out ForestAction action)
	{
		result = Point.Zero;
		action = ForestAction.None;
		Point center = Owner.Center.ToTileCoordinates();
		float bestScore = float.MaxValue;
		bool canPlant = HasPackItem(ItemID.Acorn);
		const int radius = 18;
		for (int x = center.X - radius; x <= center.X + radius; x++) {
			for (int y = center.Y - radius; y <= center.Y + radius; y++) {
				if (!WorldGen.InWorld(x, y, 10))
					continue;
				Tile tile = Main.tile[x, y];
				Point candidate;
				ForestAction candidateAction;
				float priority;
				if (tile.HasTile && tile.TileType == TileID.FallenLog && tile.WallType == WallID.None) {
					candidate = new Point(x, y);
					candidateAction = ForestAction.ClearDeadwood;
					priority = 0.55f;
				}
				else if (tile.HasTile && IsTreeTrunk(tile.TileType)) {
					WorldGen.GetTreeBottom(x, y, out int treeX, out int treeY);
					candidate = new Point(treeX, treeY);
					if (!WorldGen.InWorld(candidate.X, candidate.Y, 10))
						continue;
					candidateAction = ForestAction.ShakeTree;
					priority = 1f;
				}
				else if (canPlant && y + 1 < Main.maxTilesY && Main.tile[x, y + 1].HasTile
					&& IsAcornGround(Main.tile[x, y + 1].TileType) && CanPlantAcornAt(x, y)) {
					candidate = new Point(x, y);
					candidateAction = ForestAction.PlantAcorn;
					priority = 1.35f;
				}
				else
					continue;

				if (candidate == Point.Zero || tendedForestTargets.Contains(candidate))
					continue;
				float score = Vector2.DistanceSquared(candidate.ToWorldCoordinates(), NPC.Center) * priority;
				if (candidateAction == ForestAction.PlantAcorn)
					score += 12000f;
				if (score >= bestScore)
					continue;
				bestScore = score;
				result = candidate;
				action = candidateAction;
			}
		}
		return action != ForestAction.None;
	}

	private bool CanPlantAcornAt(int x, int y)
	{
		if (!WorldGen.InWorld(x, y, 10) || Main.tile[x, y].HasTile || Main.tile[x, y].WallType != WallID.None)
			return false;
		Tile ground = Main.tile[x, y + 1];
		if (!ground.HasTile || !IsAcornGround(ground.TileType))
			return false;

		for (int scanX = x - 2; scanX <= x + 2; scanX++) {
			for (int scanY = y - 5; scanY <= y; scanY++) {
				if (!WorldGen.InWorld(scanX, scanY, 10) || Main.tile[scanX, scanY].HasTile)
					return false;
			}
		}
		for (int scanX = x - 5; scanX <= x + 5; scanX++) {
			for (int scanY = y - 8; scanY <= y + 1; scanY++) {
				if (!WorldGen.InWorld(scanX, scanY, 10))
					continue;
				Tile nearby = Main.tile[scanX, scanY];
				if (nearby.HasTile && (nearby.TileType == TileID.Saplings || IsTreeTrunk(nearby.TileType)))
					return false;
			}
		}
		return true;
	}

	private static bool IsTreeTrunk(ushort type) => type < TileID.Sets.IsATreeTrunk.Length && TileID.Sets.IsATreeTrunk[type];

	private static bool IsAcornGround(ushort type) => type is TileID.Grass or TileID.CorruptGrass
		or TileID.CrimsonGrass or TileID.HallowedGrass or TileID.JungleGrass;

	private bool FindAutonomousChest(out Point result)
	{
		result = Point.Zero;
		float radius = 18f * 16f;
		float bestDistance = radius * radius;
		foreach (Chest? chest in Main.chest) {
			if (chest is null)
				continue;
			Vector2 position = new(chest.x * 16f, chest.y * 16f);
			float distance = Vector2.DistanceSquared(Owner.Center, position);
			if (distance < 72f * 72f || distance >= bestDistance)
				continue;
			bestDistance = distance;
			result = new Point(chest.x, chest.y);
		}
		return result != Point.Zero;
	}

	private void BeginAutonomousActivity(AutonomyActivity activity)
	{
		autonomyActivity = activity;
		autonomyActionTimer = 0;
		autonomyTargetItem = -1;
		autonomyTargetTile = Point.Zero;
		autonomyWorkCount = 0;
		autonomyForestAction = ForestAction.None;
		ClearTownNpcInteraction();
		NPC.netUpdate = true;
	}

	private void FinishAutonomousMining()
	{
		if (autonomyWorkCount > 0) {
			Profile.GainExperience(Math.Min(3, autonomyWorkCount), out _);
			SyncProfileToBoundSigil();
			StartEmote(CompanionEmote.Cheer, 90);
			ShowNativeEmote(CompanionEmote.Cheer, 110);
			SpeakLocalized("Autonomy.Mined", autonomyWorkCount.ToString());
		}
		CancelAutonomousActivity(600);
	}

	private void CancelAutonomousActivity(int nextDecisionDelay = 240)
	{
		bool changed = autonomyActivity != AutonomyActivity.None;
		autonomyActivity = AutonomyActivity.None;
		autonomyActionTimer = 0;
		autonomyTargetItem = -1;
		autonomyTargetTile = Point.Zero;
		autonomyWorkCount = 0;
		autonomyForestAction = ForestAction.None;
		autonomyDecisionTimer = Math.Max(autonomyDecisionTimer, nextDecisionDelay);
		if (changed)
			NPC.netUpdate = true;
	}

	private void PerformAutonomousMoment()
	{
		CompanionEmote gesture = PersonalityGesture();
		StartEmote(gesture, 100);
		ShowNativeEmote(gesture, 120);
		brainState = BrainState.Inspect;
		stateTimer = Main.rand.Next(90, 160);
		idleTarget = Owner.Center + Main.rand.NextVector2Circular(92f, 46f);
		if (Main.rand.NextBool(3))
			SpeakLocalized($"Autonomy.Surprise.{Profile.Personality}");
		NPC.netUpdate = true;
	}

	private static bool IsRecoveryPickup(Item item) => item.type is ItemID.Heart or ItemID.Star;

	private bool TryBeginTownNpcInteraction()
	{
		if (Command == StayCommand || socialNpcTarget >= 0 || townNpcInteractionCooldown > 0
			|| !Main.rand.NextBool(2))
			return false;

		NPC? nearest = null;
		float bestDistance = 260f * 260f;
		for (int i = 0; i < Main.maxNPCs; i++) {
			NPC candidate = Main.npc[i];
			if (!IsSocialTownNpc(candidate)
				|| Vector2.DistanceSquared(candidate.Center, Owner.Center) > 560f * 560f)
				continue;
			float distance = Vector2.DistanceSquared(candidate.Center, NPC.Center);
			if (distance >= bestDistance)
				continue;
			bestDistance = distance;
			nearest = candidate;
		}
		if (nearest is null)
			return false;

		socialNpcTarget = nearest.whoAmI;
		socialNpcTimer = 300;
		socialNpcGreeted = false;
		townNpcInteractionCooldown = Main.rand.Next(1800, 3000);
		NPC.netUpdate = true;
		return true;
	}

	private bool UpdateTownNpcInteraction()
	{
		if (socialNpcTarget < 0)
			return false;
		if (Command == StayCommand || socialNpcTarget >= Main.maxNPCs || socialNpcTimer-- <= 0) {
			ClearTownNpcInteraction();
			return false;
		}

		NPC townNpc = Main.npc[socialNpcTarget];
		if (!IsSocialTownNpc(townNpc)
			|| Vector2.DistanceSquared(townNpc.Center, Owner.Center) > 640f * 640f) {
			ClearTownNpcInteraction();
			return false;
		}

		float side = NPC.Center.X <= townNpc.Center.X ? -1f : 1f;
		Vector2 meetingPoint = townNpc.Center + new Vector2(side * 62f, -44f + IdleBob() * 0.35f);
		MoveTo(meetingPoint, 5.2f, 0.09f);
		if (Main.netMode != NetmodeID.MultiplayerClient && !socialNpcGreeted
			&& Vector2.DistanceSquared(NPC.Center, townNpc.Center) < 126f * 126f) {
			CompanionEmote gesture = PersonalityGesture();
			facing = townNpc.Center.X < NPC.Center.X ? -1 : 1;
			StartEmote(gesture, 130);
			ShowNativeEmote(gesture, 150);
			EmoteBubble.NewBubbleNPC(new WorldUIAnchor(townNpc), 150, new WorldUIAnchor(NPC));
			socialNpcGreeted = true;
			socialNpcTimer = Math.Min(socialNpcTimer, 150);
			NPC.netUpdate = true;
		}
		return true;
	}

	private CompanionEmote PersonalityGesture() => Profile.Personality switch {
		CompanionPersonality.Gentle => CompanionEmote.Heart,
		CompanionPersonality.Brave => CompanionEmote.Cheer,
		CompanionPersonality.Mischievous => CompanionEmote.Laugh,
		CompanionPersonality.Curious => CompanionEmote.Wave,
		_ => CompanionEmote.Comfort
	};

	private static bool IsSocialTownNpc(NPC candidate) => candidate.active && candidate.townNPC
		&& candidate.life > 0 && !candidate.dontTakeDamageFromHostiles;

	private void ClearTownNpcInteraction()
	{
		socialNpcTarget = -1;
		socialNpcTimer = 0;
		socialNpcGreeted = false;
		if (Main.netMode != NetmodeID.MultiplayerClient)
			NPC.netUpdate = true;
	}

	private void UpdateEmoteEffects()
	{
		if (Main.dedServ || emoteTimer % 6 != 0)
			return;
		int dustType = activeEmote switch {
			CompanionEmote.Heart or CompanionEmote.Comfort => DustID.PinkTorch,
			CompanionEmote.Cheer => DustID.GoldFlame,
			CompanionEmote.Laugh => DustID.Confetti,
			CompanionEmote.Rest => DustID.BlueTorch,
			_ => DustID.Enchanted_Gold
		};
		Vector2 position = NPC.Center + new Vector2(Main.rand.NextFloat(-24f, 24f), Main.rand.NextFloat(-42f, -12f));
		Dust dust = Dust.NewDustPerfect(position, dustType, Main.rand.NextVector2Circular(0.8f, 0.8f), 90,
			Profile.EssenceColor, 0.85f);
		dust.noGravity = true;
	}

	private NPC? GetSynchronizedDefenseTarget(Vector2 anchor)
	{
		if (guardianTarget < 0 || guardianTarget >= Main.maxNPCs)
			return null;
		NPC target = Main.npc[guardianTarget];
		float leash = DefenseLeash;
		return IsThreat(target)
			&& Vector2.DistanceSquared(anchor, target.Center) <= leash * leash
			? target
			: null;
	}

	private NPC? FindDefenseTarget(Vector2 anchor)
	{
		if (guardianTarget >= 0 && guardianTarget < Main.maxNPCs) {
			NPC current = Main.npc[guardianTarget];
			float leash = DefenseLeash;
			if (IsThreat(current)
				&& Vector2.DistanceSquared(anchor, current.Center) <= leash * leash)
				return current;
		}
		return FindNearestThreat(anchor, DefenseRange);
	}

	private void SetGuardianTarget(int target)
	{
		if (guardianTarget == target)
			return;
		guardianTarget = target;
		if (target >= 0)
			talentCooldown = Math.Min(talentCooldown, 8);
		if (Main.netMode != NetmodeID.MultiplayerClient)
			NPC.netUpdate = true;
	}

	private void RecoverEnergy(int interval, int amount, bool recoverMood)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || ++recoveryTimer < interval)
			return;
		recoveryTimer = 0;
		int previousEnergy = Profile.Energy;
		int previousMood = Profile.Mood;
		Profile.Energy = Math.Clamp(Profile.Energy + amount, 0, 100);
		if (recoverMood)
			Profile.Mood = Math.Clamp(Profile.Mood + 1, 0, 100);
		if (Profile.Energy == previousEnergy && Profile.Mood == previousMood)
			return;
		SyncProfileToBoundSigil();
		NPC.netUpdate = true;
	}

	private void TryHealOwner()
	{
		if (Main.netMode == NetmodeID.MultiplayerClient)
			return;
		if (talentCooldown > 0 || Profile.Energy < 5 || Owner.statLife >= Owner.statLifeMax2)
			return;

		int missingLife = Owner.statLifeMax2 - Owner.statLife;
		int minimumNeed = Math.Max(6, Owner.statLifeMax2 / 20);
		if (missingLife < minimumNeed)
			return;

		int amount = Math.Min(missingLife, 5 + Profile.RankIndex * 2);
		Owner.statLife += amount;
		Owner.HealEffect(amount, broadcast: true);
		Profile.Energy = Math.Max(0, Profile.Energy - 4);
		Profile.Remember(CompanionMemoryKind.HealerAid, amount);
		bool leveledUp = Profile.GainExperience(2, out int newLevel);
		talentCooldown = Math.Max(360, 660 - Profile.RankIndex * 60);
		SyncProfileToBoundSigil();
		for (int i = 0; i < 12; i++) {
			Dust dust = Dust.NewDustPerfect(Owner.Center + Main.rand.NextVector2Circular(26f, 38f), DustID.HealingPlus,
				Main.rand.NextVector2Circular(0.7f, 0.7f), 80, Profile.EssenceColor, 0.9f);
			dust.noGravity = true;
		}
		string message = SoulmatesText.Get("Messages.Healed", Profile.Name, amount);
		if (leveledUp)
			message += " " + SoulmatesText.Get("Messages.LevelUp", Profile.Name, newLevel);
		if (Main.netMode == NetmodeID.Server)
			global::Soulmates.Soulmates.SendProfileUpdate(Owner, this, message);
		else
			Main.NewText(message, Profile.EssenceColor);
	}

	private NPC? FindNearestThreat(Vector2 anchor, float range)
	{
		NPC? result = null;
		float bestScore = float.MaxValue;
		for (int i = 0; i < Main.maxNPCs; i++) {
			NPC candidate = Main.npc[i];
			if (!IsThreat(candidate))
				continue;
			float anchorDistance = Vector2.DistanceSquared(anchor, candidate.Center);
			bool defendingOwner = candidate.target == Owner.whoAmI;
			float alertRange = defendingOwner || candidate.type == NPCID.TargetDummy ? range : Math.Min(range, PassiveAlertRange);
			if (anchorDistance > alertRange * alertRange)
				continue;
			float score = anchorDistance;
			if (defendingOwner)
				score *= 0.55f;
			if (score >= bestScore)
				continue;
			bestScore = score;
			result = candidate;
		}
		return result;
	}

	private static bool IsThreat(NPC candidate)
	{
		if (!candidate.active || candidate.friendly || candidate.lifeMax <= 5)
			return false;
		if (candidate.type == NPCID.TargetDummy)
			return true;
		return candidate.chaseable && !candidate.immortal;
	}

	private void ChooseNextState(float distance, Vector2 followTarget)
	{
		if (distance > 170f || Owner.velocity.LengthSquared() > 9f) {
			brainState = BrainState.Follow;
			stateTimer = 70;
			return;
		}

		int curiosity = Profile.Personality switch {
			CompanionPersonality.Curious => 55,
			CompanionPersonality.Mischievous => 45,
			CompanionPersonality.Brave => 30,
			_ => 20
		};
		int roll = Main.rand.Next(100);
		if (roll < curiosity) {
			brainState = Main.rand.NextBool() ? BrainState.Wander : BrainState.Inspect;
			idleTarget = followTarget + Main.rand.NextVector2Circular(90f, 44f);
			stateTimer = Main.rand.Next(100, 220);
		}
		else {
			brainState = BrainState.Idle;
			stateTimer = Profile.Personality == CompanionPersonality.Gentle ? Main.rand.Next(220, 380) : Main.rand.Next(140, 280);
		}
	}

	private void MoveTo(Vector2 target, float maxSpeed, float responsiveness)
	{
		Vector2 offset = target - NPC.Center;
		float speed = MathHelper.Clamp(offset.Length() / 16f, 0f, maxSpeed);
		Vector2 desired = offset.SafeNormalize(Vector2.Zero) * speed;
		NPC.velocity = Vector2.Lerp(NPC.velocity, desired, responsiveness);
		if (offset.LengthSquared() < 64f)
			NPC.velocity *= 0.94f;
	}

	private float IdleBob() => MathF.Sin(Main.GlobalTimeWrappedHourly * 2.2f + bobSeed) * 7f;

	private void UpdateFacing()
	{
		if (facingCooldown > 0)
			facingCooldown--;

		if (MathF.Abs(NPC.velocity.X) > 0.55f && facingCooldown == 0) {
			int desired = NPC.velocity.X < 0f ? -1 : 1;
			if (desired != facing) {
				facing = desired;
				facingCooldown = 24;
			}
		}
		NPC.spriteDirection = facing;
	}

	private void UpdateAuraDust()
	{
		int chance = Profile.Aura switch {
			CompanionAura.SoulSparks => 5,
			CompanionAura.OrbitingStars => 8,
			_ => 14
		};
		if (!Main.rand.NextBool(chance))
			return;

		Dust dust = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(24f, 24f), DustID.Enchanted_Gold,
			-NPC.velocity * 0.08f, 120, Profile.EssenceColor, Profile.Aura == CompanionAura.SoftGlow ? 0.55f : 0.8f);
		dust.noGravity = true;
	}

	private void UpdateJob()
	{
		jobTimer++;
		UpdateJobEffects();
		if (Main.netMode == NetmodeID.MultiplayerClient)
			return;
		if (Profile.Energy < 5 || Profile.Mood < 15) {
			PauseAssignmentForRecovery();
			return;
		}
		switch (activeJob) {
			case CompanionJob.FindTreasure:
				UpdateTreasureJob();
				break;
			case CompanionJob.Mine:
				UpdateMiningJob();
				break;
			case CompanionJob.Gather:
				UpdateGatherJob();
				break;
		}
	}

	private void UpdateJobEffects()
	{
		if (jobTimer % 8 != 0)
			return;
		Color color = JobColor(activeJob);
		Vector2 position = NPC.Center + Main.rand.NextVector2Circular(30f, 22f);
		Dust dust = Dust.NewDustPerfect(position, DustID.Enchanted_Gold, -NPC.velocity * 0.05f, 110, color, 0.72f);
		dust.noGravity = true;
	}

	private void ResumeAssignment()
	{
		if (Main.netMode == NetmodeID.MultiplayerClient)
			return;
		if (activeJob != CompanionJob.None || Profile.Routine == CompanionJob.None)
			return;
		if (Profile.Energy < 12 || Profile.Mood < 20) {
			if (Command != StayCommand)
				idleTarget = NPC.Center;
			Command = StayCommand;
			brainState = BrainState.Stay;
			return;
		}
		BeginJob(Profile.Routine);
		SyncProfileToBoundSigil();
	}

	private void UpdateTreasureJob()
	{
		if (!hasJobTarget && !FindNearestChest(out jobTarget)) {
			CompleteJob(SoulmatesText.Get("Jobs.Treasure.Empty", TreasureRadiusTiles), success: false);
			return;
		}
		hasJobTarget = true;
		Vector2 target = jobTarget.ToWorldCoordinates(16f, -24f);
		MoveTo(target, 8f, 0.08f);
		if (Main.rand.NextBool(5)) {
			Dust dust = Dust.NewDustPerfect(Vector2.Lerp(NPC.Center, target, Main.rand.NextFloat()), DustID.Enchanted_Gold,
				Vector2.Zero, 100, Profile.EssenceColor, 0.8f);
			dust.noGravity = true;
		}

		if (Vector2.DistanceSquared(NPC.Center, target) < 90f * 90f || jobTimer > 600) {
			string direction = DescribeDirection(target - Owner.Center);
			int tiles = (int)(Vector2.Distance(target, Owner.Center) / 16f);
			CompleteJob(SoulmatesText.Get("Jobs.Treasure.Found", direction, tiles), success: true,
				CompanionMemoryKind.TreasureFound, tiles);
		}
	}

	private void UpdateMiningJob()
	{
		if (!hasJobTarget && !FindMiningTarget(out jobTarget)) {
			string report = failedMiningTargets.Count > 0
				? SoulmatesText.Get("Jobs.Mining.Protected", jobCount, failedMiningTargets.Count)
				: jobCount > 0
					? SoulmatesText.Get("Jobs.Mining.Cleared", jobCount, MiningRadiusTiles)
					: SoulmatesText.Get("Jobs.Mining.Empty", MiningRadiusTiles);
			CompleteJob(report, jobCount > 0, CompanionMemoryKind.MiningCompleted, jobCount);
			return;
		}
		hasJobTarget = true;
		Vector2 target = jobTarget.ToWorldCoordinates();
		MoveTo(target, 7f, 0.09f);
		if (Vector2.DistanceSquared(NPC.Center, target) > 58f * 58f)
			return;

		int miningDelay = Profile.Trinket == CompanionTrinket.DelverCharm ? 18 : 28;
		if (jobTimer % miningDelay != 0)
			return;
		if (Main.netMode == NetmodeID.MultiplayerClient)
			return;
		if (!WorldGen.InWorld(jobTarget.X, jobTarget.Y, 10)
			|| !Main.tile[jobTarget.X, jobTarget.Y].HasTile
			|| !IsMineableWorkTile(Main.tile[jobTarget.X, jobTarget.Y].TileType)) {
			hasJobTarget = false;
			return;
		}
		WorldGen.KillTile(jobTarget.X, jobTarget.Y);
		if (!Main.tile[jobTarget.X, jobTarget.Y].HasTile) {
			jobCount++;
			Profile.Energy = Math.Max(0, Profile.Energy - 1);
			CollectNearbyLooseItems(target, 96f, 6);
			SyncProfileToBoundSigil();
			NPC.netUpdate = true;
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 0, jobTarget.X, jobTarget.Y);
		}
		else
			failedMiningTargets.Add(jobTarget);
		hasJobTarget = false;
		if (jobTimer > 7200)
			CompleteJob(SoulmatesText.Get("Jobs.Mining.Timeout", jobCount), success: false);
	}

	private void UpdateGatherJob()
	{
		if (gatherPause > 0) {
			gatherPause--;
			MoveTo(jobOrigin + new Vector2(0f, -52f + IdleBob()), 7f, 0.08f);
			return;
		}
		if (!IsValidGatherTarget(jobTargetItem))
			jobTargetItem = FindNearestLooseItem();
		if (jobTargetItem < 0) {
			areaEmptyTimer++;
			MoveTo(jobOrigin + new Vector2(0f, -54f + IdleBob()), 5f, 0.06f);
			if (areaEmptyTimer >= 90) {
				bool blockedByPack = HasNearbyCarryableItem();
				CompleteJob(blockedByPack
					? SoulmatesText.Get("Jobs.PackFull")
					: jobCount > 0
						? SoulmatesText.Get("Jobs.Gathering.Cleared", jobCount, GatheringRadiusTiles)
						: SoulmatesText.Get("Jobs.Gathering.Empty", GatheringRadiusTiles),
					jobCount > 0 && !blockedByPack, CompanionMemoryKind.GatheringCompleted, jobCount);
			}
			return;
		}
		areaEmptyTimer = 0;

		Item item = Main.item[jobTargetItem];
		MoveTo(item.Center + new Vector2(0f, -8f), 8f, 0.08f);
		if (Vector2.DistanceSquared(NPC.Center, item.Center) < 42f * 42f) {
			if (Main.netMode == NetmodeID.MultiplayerClient)
				return;
			if (!CanCollectLooseItem(item)) {
				gatherPause = 45;
				return;
			}
			int moved = StoreLooseItem(item);
			if (moved <= 0) {
				CompleteJob(SoulmatesText.Get("Jobs.PackFull"), success: false);
				return;
			}
			jobCount += moved;
			Profile.Energy = Math.Max(0, Profile.Energy - 1);
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendData(MessageID.SyncItem, -1, -1, null, jobTargetItem);
			SyncProfileToBoundSigil();
			NPC.netUpdate = true;
			jobTargetItem = -1;
			gatherPause = 6;
		}
		if (jobTimer > 7200)
			CompleteJob(SoulmatesText.Get("Jobs.Gathering.Timeout", jobCount), success: false);
	}

	private bool FindNearestChest(out Point result)
	{
		result = Point.Zero;
		float radius = TreasureRadiusTiles * 16f;
		float bestDistance = radius * radius;
		foreach (Chest? chest in Main.chest) {
			if (chest is null)
				continue;
			Vector2 position = new Vector2(chest.x * 16f, chest.y * 16f);
			float distance = Vector2.DistanceSquared(jobOrigin, position);
			if (distance >= bestDistance)
				continue;
			bestDistance = distance;
			result = new Point(chest.x, chest.y);
		}
		return result != Point.Zero;
	}

	private bool FindMiningTarget(out Point result)
	{
		Point center = jobOrigin.ToTileCoordinates();
		result = Point.Zero;
		float bestScore = float.MaxValue;
		int radius = MiningRadiusTiles;
		for (int x = center.X - radius; x <= center.X + radius; x++) {
			for (int y = center.Y - radius; y <= center.Y + radius; y++) {
				if (!WorldGen.InWorld(x, y, 10))
					continue;
				Tile tile = Main.tile[x, y];
				if (!tile.HasTile || !IsEarlyOre(tile.TileType))
					continue;
				var candidate = new Point(x, y);
				if (failedMiningTargets.Contains(candidate))
					continue;
				if (Vector2.DistanceSquared(candidate.ToVector2(), center.ToVector2()) > radius * radius)
					continue;
				float score = Vector2.DistanceSquared(candidate.ToWorldCoordinates(), NPC.Center);
				if (score >= bestScore)
					continue;
				bestScore = score;
				result = candidate;
			}
		}
		return result != Point.Zero;
	}

	private int FindNearestLooseItem()
	{
		int result = -1;
		float radius = GatheringRadiusTiles * 16f;
		float bestDistance = float.MaxValue;
		for (int i = 0; i < Main.maxItems; i++) {
			Item item = Main.item[i];
			if (!item.active || item.IsAir || !CanCollectLooseItem(item)
				|| item.playerIndexTheItemIsReservedFor != 255 && item.playerIndexTheItemIsReservedFor != Owner.whoAmI)
				continue;
			if (Vector2.DistanceSquared(jobOrigin, item.Center) >= radius * radius)
				continue;
			float distance = Vector2.DistanceSquared(NPC.Center, item.Center);
			if (distance >= bestDistance)
				continue;
			bestDistance = distance;
			result = i;
		}
		return result;
	}

	private bool HasNearbyCarryableItem()
	{
		float radius = GatheringRadiusTiles * 16f;
		for (int i = 0; i < Main.maxItems; i++) {
			Item item = Main.item[i];
			if (!item.active || item.IsAir || !CanCarry(item) || IsRecoveryPickup(item)
				|| item.playerIndexTheItemIsReservedFor != 255 && item.playerIndexTheItemIsReservedFor != Owner.whoAmI)
				continue;
			if (Vector2.DistanceSquared(jobOrigin, item.Center) < radius * radius)
				return true;
		}
		return false;
	}

	private bool IsValidGatherTarget(int itemIndex)
	{
		if (itemIndex < 0 || itemIndex >= Main.maxItems)
			return false;
		Item item = Main.item[itemIndex];
		float radius = GatheringRadiusTiles * 16f;
		return item.active && !item.IsAir && CanCollectLooseItem(item)
			&& (item.playerIndexTheItemIsReservedFor == 255 || item.playerIndexTheItemIsReservedFor == Owner.whoAmI)
			&& Vector2.DistanceSquared(jobOrigin, item.Center) < radius * radius;
	}

	private int StoreLooseItem(Item worldItem)
	{
		if (!worldItem.active || worldItem.IsAir || worldItem.stack <= 0 || !CanCollectLooseItem(worldItem))
			return 0;

		int available = worldItem.stack;
		Item transfer = worldItem.Clone();
		transfer.stack = available;
		int moved = Math.Clamp(Profile.Store(transfer), 0, available);
		if (moved <= 0)
			return 0;

		worldItem.stack = available - moved;
		if (worldItem.stack <= 0) {
			worldItem.TurnToAir();
			worldItem.active = false;
		}
		return moved;
	}

	private bool HasPackItem(int itemType) => Profile.Pack.Exists(item => !item.IsAir && item.type == itemType && item.stack > 0);

	private bool ConsumePackItem(int itemType)
	{
		for (int i = 0; i < Profile.Pack.Count; i++) {
			Item item = Profile.Pack[i];
			if (item.IsAir || item.type != itemType || item.stack <= 0)
				continue;
			item.stack--;
			if (item.stack <= 0)
				Profile.Pack.RemoveAt(i);
			return true;
		}
		return false;
	}

	private int CollectNearbyLooseItems(Vector2 center, float radius, int maximumStacks)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient)
			return 0;

		int movedTotal = 0;
		int movedStacks = 0;
		float radiusSquared = radius * radius;
		for (int i = 0; i < Main.maxItems && movedStacks < maximumStacks; i++) {
			Item item = Main.item[i];
			if (!item.active || item.IsAir || !CanCollectLooseItem(item)
				|| item.playerIndexTheItemIsReservedFor != 255 && item.playerIndexTheItemIsReservedFor != Owner.whoAmI
				|| Vector2.DistanceSquared(center, item.Center) > radiusSquared)
				continue;

			int moved = StoreLooseItem(item);
			if (moved <= 0)
				continue;

			movedTotal += moved;
			movedStacks++;
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendData(MessageID.SyncItem, -1, -1, null, i);
		}

		if (movedTotal > 0) {
			SyncProfileToBoundSigil();
			NPC.netUpdate = true;
		}
		return movedTotal;
	}

	private void RevealSurroundings()
	{
		if (Main.dedServ || Owner.whoAmI != Main.myPlayer || ++revealTimer < 10)
			return;
		revealTimer = 0;
		Point center = NPC.Center.ToTileCoordinates();
		const int radius = 9;
		for (int x = center.X - radius; x <= center.X + radius; x++) {
			for (int y = center.Y - radius; y <= center.Y + radius; y++) {
				if (!WorldGen.InWorld(x, y, 10))
					continue;
				float distance = Vector2.Distance(new Vector2(x, y), center.ToVector2());
				if (distance > radius)
					continue;
				byte light = (byte)MathHelper.Clamp(255f - distance * 18f, 80f, 255f);
				Main.Map.Update(x, y, light);
			}
		}
	}

	private static bool IsMineableWorkTile(ushort type) => IsEarlyOre(type);

	private static bool IsEarlyOre(ushort type) => type is TileID.Copper or TileID.Tin or TileID.Iron or TileID.Lead
		or TileID.Silver or TileID.Tungsten or TileID.Gold or TileID.Platinum;

	private static string DescribeDirection(Vector2 offset)
	{
		bool hasVertical = MathF.Abs(offset.Y) > 96f;
		bool hasHorizontal = MathF.Abs(offset.X) > 96f;
		string vertical = SoulmatesText.Get(offset.Y < 0f ? "Directions.Above" : "Directions.Below");
		string horizontal = SoulmatesText.Get(offset.X < 0f ? "Directions.West" : "Directions.East");
		if (hasVertical && hasHorizontal)
			return SoulmatesText.Get("Directions.Combined", vertical, horizontal);
		if (hasVertical)
			return vertical;
		return hasHorizontal ? horizontal : SoulmatesText.Get("Directions.Nearby");
	}

	private void CompleteJob(string memory, bool success, CompanionMemoryKind? memoryKind = null, int memoryAmount = 0)
	{
		Profile.LastMemory = memory;
		bool leveledUp = false;
		int newLevel = Profile.Level;
		if (success) {
			Profile.JobsCompleted++;
			Profile.ChangeBond(2);
			Profile.Mood = Math.Clamp(Profile.Mood + 1, 0, 100);
			leveledUp = Profile.GainExperience(Math.Clamp(3 + Math.Max(0, memoryAmount) / 5, 3, 10), out newLevel);
			if (memoryKind is { } kind)
				Profile.Remember(kind, memoryAmount);
		}
		Profile.Routine = CompanionJob.None;
		SyncProfileToBoundSigil();
		string message = $"{Profile.Name}: {memory}";
		if (leveledUp)
			message += " " + SoulmatesText.Get("Messages.LevelUp", Profile.Name, newLevel);
		if (Main.netMode == NetmodeID.Server)
			global::Soulmates.Soulmates.SendProfileUpdate(Owner, this, message);
		else
			Main.NewText(message, success ? Profile.EssenceColor : Color.LightGray);
		activeJob = CompanionJob.None;
		jobTimer = 0;
		jobCount = 0;
		gatherPause = 0;
		jobTargetItem = -1;
		areaEmptyTimer = 0;
		hasJobTarget = false;
		jobOrigin = Vector2.Zero;
		failedMiningTargets.Clear();
		brainState = BrainState.Follow;
		stateTimer = 1;
		NPC.netUpdate = true;
	}

	public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
	{
		CompanionMuse muse = Enum.IsDefined(Profile.Muse) ? Profile.Muse : CompanionMuse.Soulkin;
		bool actionFrame = guardianTarget >= 0 || activeJob != CompanionJob.None
			|| autonomyActivity != AutonomyActivity.None || NPC.velocity.LengthSquared() > 20f;
		Texture2D texture = CompanionVisuals.GetTexture(muse, actionFrame);
		Rectangle source = CompanionVisuals.GetFrame(muse, texture, actionFrame);
		Vector2 center = NPC.Center - screenPos + new Vector2(0f, IdleBob() * 0.18f) + EmoteDrawOffset();
		Vector2 origin = source.Size() * 0.5f;
		float breath = 1f + MathF.Sin(Main.GlobalTimeWrappedHourly * 2f + bobSeed) * 0.025f;
		float emoteScale = emoteTimer > 0 && activeEmote is CompanionEmote.Heart or CompanionEmote.Cheer
			? 1.04f + MathF.Sin(Main.GlobalTimeWrappedHourly * 6f) * 0.025f
			: 1f;
		Vector2 formScale = Profile.Form switch {
			CompanionForm.Round => new Vector2(1.12f, 0.92f),
			CompanionForm.Wisp => new Vector2(0.88f, 1.14f),
			_ => Vector2.One
		};
		Vector2 scale = formScale * (64f / Math.Max(source.Width, source.Height)) * breath * emoteScale;
		SpriteEffects effects = facing < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
		Color tint = Color.Lerp(Color.White, Profile.EssenceColor, 0.34f);
		float drawRotation = NPC.rotation + EmoteRotationOffset();
		DrawJobOrbit(spriteBatch, center);

		for (int i = 0; i < 4; i++) {
			Vector2 glowOffset = new Vector2(2f, 0f).RotatedBy(MathHelper.PiOver2 * i);
			spriteBatch.Draw(texture, center + glowOffset, source, Profile.EssenceColor * 0.18f, drawRotation, origin, scale, effects, 0f);
		}
		spriteBatch.Draw(texture, center, source, tint, drawRotation, origin, scale, effects, 0f);
		DrawSpeechBubble(spriteBatch, center);
		return false;
	}

	private Vector2 EmoteDrawOffset()
	{
		if (emoteTimer <= 0)
			return Vector2.Zero;
		float time = Main.GlobalTimeWrappedHourly;
		return activeEmote switch {
			CompanionEmote.Cheer => new Vector2(0f, -MathF.Abs(MathF.Sin(time * 7f)) * 9f),
			CompanionEmote.Heart or CompanionEmote.Comfort => new Vector2(0f, MathF.Sin(time * 4f) * 4f - 3f),
			CompanionEmote.Laugh => new Vector2(MathF.Sin(time * 15f) * 3f, 0f),
			CompanionEmote.Rest => new Vector2(0f, 7f),
			_ => Vector2.Zero
		};
	}

	private float EmoteRotationOffset()
	{
		if (emoteTimer <= 0)
			return 0f;
		float time = Main.GlobalTimeWrappedHourly;
		return activeEmote switch {
			CompanionEmote.Wave => MathF.Sin(time * 7f) * 0.13f,
			CompanionEmote.Laugh => MathF.Sin(time * 15f) * 0.055f,
			CompanionEmote.Rest => facing * 0.08f,
			_ => 0f
		};
	}

	private void DrawSpeechBubble(SpriteBatch spriteBatch, Vector2 companionCenter)
	{
		if (speechTimer <= 0 || string.IsNullOrWhiteSpace(speechText))
			return;
		float zoom = Math.Max(1f, Main.GameViewMatrix.Zoom.X);
		float textScale = 0.68f / zoom;
		List<string> lines = WrapSpeech(speechText, 220f / zoom, textScale);
		float width = 0f;
		foreach (string line in lines)
			width = Math.Max(width, FontAssets.MouseText.Value.MeasureString(line).X * textScale);
		float lineHeight = 20f / zoom;
		float paddingX = 11f / zoom;
		float paddingY = 8f / zoom;
		float height = lines.Count * lineHeight;
		float halfWidth = width * 0.5f + paddingX;
		float halfHeight = height * 0.5f + paddingY;
		Vector2 viewportCenter = new(Main.screenWidth * 0.5f, Main.screenHeight * 0.5f);
		float visibleLeft = viewportCenter.X * (1f - 1f / zoom);
		float visibleRight = viewportCenter.X * (1f + 1f / zoom);
		float visibleTop = viewportCenter.Y * (1f - 1f / zoom);
		float visibleBottom = viewportCenter.Y * (1f + 1f / zoom);
		float safeTop = visibleTop + 54f / zoom;
		Vector2 bubbleCenter = companionCenter + new Vector2(0f, -(68f / zoom + halfHeight));
		if (bubbleCenter.Y - halfHeight < safeTop)
			bubbleCenter.Y = companionCenter.Y + 54f / zoom + halfHeight;
		bubbleCenter.X = MathHelper.Clamp(bubbleCenter.X, visibleLeft + halfWidth + 8f / zoom,
			visibleRight - halfWidth - 8f / zoom);
		bubbleCenter.Y = MathHelper.Clamp(bubbleCenter.Y, safeTop + halfHeight,
			visibleBottom - halfHeight - 10f / zoom);
		Rectangle background = new((int)(bubbleCenter.X - width * 0.5f - paddingX), (int)(bubbleCenter.Y - height * 0.5f - paddingY),
			(int)(width + paddingX * 2f), (int)(height + paddingY * 2f));
		Texture2D pixel = TextureAssets.MagicPixel.Value;
		spriteBatch.Draw(pixel, background, new Color(11, 17, 29) * 0.9f);
		spriteBatch.Draw(pixel, new Rectangle(background.X, background.Y, background.Width, 2), Profile.EssenceColor * 0.9f);
		for (int i = 0; i < lines.Count; i++) {
			Vector2 size = FontAssets.MouseText.Value.MeasureString(lines[i]) * textScale;
			Vector2 position = new(bubbleCenter.X - size.X * 0.5f, background.Y + 6f / zoom + i * lineHeight);
			Utils.DrawBorderString(spriteBatch, lines[i], position, Color.White, textScale);
		}
	}

	private static List<string> WrapSpeech(string text, float maximumWidth, float scale)
	{
		var lines = new List<string>();
		string current = "";
		foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries)) {
			string candidate = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
			if (!string.IsNullOrEmpty(current) && FontAssets.MouseText.Value.MeasureString(candidate).X * scale > maximumWidth) {
				lines.Add(current);
				current = word;
			}
			else
				current = candidate;
		}
		if (!string.IsNullOrEmpty(current))
			lines.Add(current);
		if (lines.Count == 0)
			lines.Add("...");
		return lines;
	}

	private void DrawJobOrbit(SpriteBatch spriteBatch, Vector2 center)
	{
		bool defending = guardianTarget >= 0;
		bool autonomous = autonomyActivity != AutonomyActivity.None;
		if (activeJob == CompanionJob.None && !defending && !autonomous)
			return;
		Texture2D pixel = TextureAssets.MagicPixel.Value;
		Color color = defending
			? Color.Lerp(Profile.EssenceColor, Color.White, 0.28f)
			: autonomous ? AutonomyColor(autonomyActivity) : JobColor(activeJob);
		float time = Main.GlobalTimeWrappedHourly * (defending ? 4.2f : autonomous ? 3.2f : 2.5f);
		for (int i = 0; i < 3; i++) {
			float angle = time + MathHelper.TwoPi * i / 3f;
			Vector2 point = center + new Vector2(MathF.Cos(angle) * (defending ? 39f : 35f), MathF.Sin(angle) * 13f - 4f);
			int size = i == jobTimer / 10 % 3 ? 5 : 3;
			spriteBatch.Draw(pixel, new Rectangle((int)point.X - size / 2, (int)point.Y - size / 2, size, size), color * 0.88f);
		}
		float pulse = 0.35f + (MathF.Sin(time * 1.6f) + 1f) * (defending ? 0.2f : 0.12f);
		spriteBatch.Draw(pixel, new Rectangle((int)center.X - 16, (int)center.Y - 43, 32, 2), color * pulse);
	}

	private Color JobColor(CompanionJob job) => job switch {
		CompanionJob.Mine => new Color(100, 188, 255),
		CompanionJob.Gather => new Color(121, 230, 151),
		CompanionJob.FindTreasure => new Color(255, 221, 104),
		_ => Profile.EssenceColor
	};

	private Color AutonomyColor(AutonomyActivity activity) => activity switch {
		AutonomyActivity.FetchItem => new Color(121, 230, 151),
		AutonomyActivity.AssistMining => new Color(100, 188, 255),
		AutonomyActivity.InspectTreasure => new Color(255, 221, 104),
		_ => Profile.EssenceColor
	};

	public override Color? GetAlpha(Color drawColor) => Color.Lerp(drawColor, Profile.EssenceColor, 0.42f);
	public override void SendExtraAI(BinaryWriter writer)
	{
		Profile.Write(writer);
		writer.Write((byte)activeJob);
		writer.Write(jobCount);
		writer.Write((short)guardianTarget);
		writer.Write(idleTarget.X);
		writer.Write(idleTarget.Y);
		writer.Write((byte)activeEmote);
		writer.Write((short)Math.Clamp(emoteTimer, 0, short.MaxValue));
		writer.Write((short)socialNpcTarget);
		writer.Write((short)Math.Clamp(socialNpcTimer, 0, short.MaxValue));
		writer.Write(socialNpcGreeted);
		writer.Write((byte)autonomyActivity);
		writer.Write((short)autonomyTargetItem);
		writer.Write((short)autonomyTargetTile.X);
		writer.Write((short)autonomyTargetTile.Y);
		writer.Write((byte)autonomyForestAction);
		writer.Write((short)Math.Clamp(autonomyActionTimer, 0, short.MaxValue));
		writer.Write((byte)Math.Clamp(autonomyWorkCount, 0, byte.MaxValue));
	}

	public override void ReceiveExtraAI(BinaryReader reader)
	{
		Profile = CompanionProfile.Read(reader);
		activeJob = (CompanionJob)reader.ReadByte();
		jobCount = reader.ReadInt32();
		guardianTarget = reader.ReadInt16();
		idleTarget = new Vector2(reader.ReadSingle(), reader.ReadSingle());
		activeEmote = (CompanionEmote)reader.ReadByte();
		emoteTimer = reader.ReadInt16();
		socialNpcTarget = reader.ReadInt16();
		socialNpcTimer = reader.ReadInt16();
		socialNpcGreeted = reader.ReadBoolean();
		autonomyActivity = (AutonomyActivity)reader.ReadByte();
		autonomyTargetItem = reader.ReadInt16();
		autonomyTargetTile = new Point(reader.ReadInt16(), reader.ReadInt16());
		autonomyForestAction = (ForestAction)reader.ReadByte();
		autonomyActionTimer = reader.ReadInt16();
		autonomyWorkCount = reader.ReadByte();
		if (Main.netMode != NetmodeID.MultiplayerClient || !TryGetOwner(out Player owner) || owner.whoAmI != Main.myPlayer)
			return;
		foreach (Item item in owner.inventory) {
			if (item.ModItem is SoulboundSigil sigil && sigil.Profile.Id == Profile.Id) {
				sigil.Profile = Profile.Clone();
				break;
			}
		}
	}

	public void ToggleCommand()
	{
		CancelAssignment();
		Command = Command == StayCommand ? FollowCommand : StayCommand;
		brainState = Command == StayCommand ? BrainState.Stay : BrainState.Follow;
		stateTimer = 1;
		if (Command == StayCommand)
			idleTarget = NPC.Center;
		NPC.netUpdate = true;
	}

	public void SetCommand(bool stay)
	{
		CancelAssignment();
		Command = stay ? StayCommand : FollowCommand;
		brainState = stay ? BrainState.Stay : BrainState.Follow;
		stateTimer = 1;
		if (stay)
			idleTarget = NPC.Center;
		NPC.netUpdate = true;
	}

	public void AskToExplore()
	{
		CancelAssignment();
		Command = FollowCommand;
		brainState = BrainState.Inspect;
		stateTimer = Main.rand.Next(180, 320);
		idleTarget = Owner.Center + Main.rand.NextVector2Circular(120f, 60f);
		NPC.netUpdate = true;
	}

	public void StartJob(CompanionJob job)
	{
		Profile.Routine = job;
		BeginJob(job);
	}

	public CompanionConversationResult PerformQuickAction(CompanionQuickAction action)
	{
		if (action == CompanionQuickAction.ToggleAutonomy) {
			Profile.AutonomyEnabled = !Profile.AutonomyEnabled;
			if (!Profile.AutonomyEnabled)
				CancelAutonomousActivity();
			string reply = SoulmatesText.Get(Profile.AutonomyEnabled
				? "Autonomy.Enabled"
				: "Autonomy.Disabled");
			SyncProfileToBoundSigil();
			NPC.netUpdate = true;
			return new CompanionConversationResult(reply, true);
		}

		(TalkCategory category, int option) request = action switch {
			CompanionQuickAction.Follow => (TalkCategory.Commands, 0),
			CompanionQuickAction.Stay => (TalkCategory.Commands, 1),
			CompanionQuickAction.Explore => (TalkCategory.Commands, 2),
			CompanionQuickAction.FindTreasure => (TalkCategory.Work, 0),
			CompanionQuickAction.Mine => (TalkCategory.Work, 1),
			CompanionQuickAction.Gather => (TalkCategory.Work, 2),
			_ => (TalkCategory.Care, 0)
		};
		return Converse(request.category, request.option, 0);
	}

	public CompanionConversationResult Converse(TalkCategory category, int option, int memoryCursor)
	{
		DialogueResult result = CompanionDialogueEngine.Speak(Profile, category, option);
		Profile.ChangeBond(result.BondDelta);
		Profile.Mood = Math.Clamp(Profile.Mood + result.MoodDelta, 0, 100);
		Profile.Energy = Math.Clamp(Profile.Energy + result.EnergyDelta, 0, 100);
		ApplyConversationAction(result.Action);

		string reply = result.Action switch {
			SpeechAction.ShowPack => Profile.DescribePack(),
			SpeechAction.StoreHeldItem => StoreSelectedItem(),
			SpeechAction.UnloadPack => UnloadPack(),
			SpeechAction.RecallMemory => Profile.RecallMemory(memoryCursor),
			_ => result.Reply
		};
		if (result.Action is SpeechAction.StoreHeldItem or SpeechAction.UnloadPack)
			SyncOwnerInventory();
		SyncProfileToBoundSigil();
		NPC.netUpdate = true;
		return new CompanionConversationResult(reply, result.Accepted);
	}

	private void ApplyConversationAction(SpeechAction action)
	{
		switch (action) {
			case SpeechAction.Follow:
				Profile.Routine = CompanionJob.None;
				SetCommand(stay: false);
				break;
			case SpeechAction.Stay:
			case SpeechAction.Rest:
				Profile.Routine = CompanionJob.None;
				SetCommand(stay: true);
				break;
			case SpeechAction.Explore:
				Profile.Routine = CompanionJob.None;
				AskToExplore();
				break;
			case SpeechAction.FindTreasure:
				StartJob(CompanionJob.FindTreasure);
				break;
			case SpeechAction.Mine:
				StartJob(CompanionJob.Mine);
				break;
			case SpeechAction.Gather:
				StartJob(CompanionJob.Gather);
				break;
			case SpeechAction.VoiceSoft:
				Profile.Voice = CompanionVoice.Soft;
				break;
			case SpeechAction.VoiceDirect:
				Profile.Voice = CompanionVoice.Direct;
				break;
			case SpeechAction.VoicePlayful:
				Profile.Voice = CompanionVoice.Playful;
				break;
		}
	}

	private void BeginJob(CompanionJob job)
	{
		if (!TryGetOwner(out Player owner)) {
			CancelAssignment();
			return;
		}
		CancelAutonomousActivity();
		ClearTownNpcInteraction();
		activeJob = job;
		jobTimer = 0;
		jobCount = 0;
		gatherPause = 0;
		jobTargetItem = -1;
		areaEmptyTimer = 0;
		hasJobTarget = false;
		jobOrigin = owner.Center;
		failedMiningTargets.Clear();
		Command = FollowCommand;
		NPC.netUpdate = true;
	}

	private void CancelAssignment()
	{
		ClearTownNpcInteraction();
		CancelAutonomousActivity();
		activeJob = CompanionJob.None;
		Profile.Routine = CompanionJob.None;
		jobTimer = 0;
		jobCount = 0;
		gatherPause = 0;
		jobTargetItem = -1;
		areaEmptyTimer = 0;
		hasJobTarget = false;
		jobOrigin = Vector2.Zero;
		failedMiningTargets.Clear();
	}

	public void EquipTrinket(CompanionTrinket trinket)
	{
		Profile.Trinket = trinket;
		if (trinket == CompanionTrinket.None)
			Profile.Remember(CompanionMemoryKind.TrinketRemoved);
		else
			Profile.Remember(CompanionMemoryKind.TrinketEquipped, detail: trinket.ToString());
		SyncProfileToBoundSigil();
		NPC.netUpdate = true;
	}

	public void ShowSpeech(string text)
	{
		speechText = string.IsNullOrWhiteSpace(text) ? "..." : text.Trim();
		if (speechText.Length > 180)
			speechText = speechText[..180];
		speechTimer = Math.Clamp(180 + speechText.Length * 2, 210, 360);
	}

	public void PerformEmote(CompanionEmote emote) => PerformEmote(emote, -1, applyRestCommand: true);

	public void ReactToNativeEmote(int emoteId)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || emoteId < 0
			|| emoteId >= EmoteBubbleLoader.EmoteBubbleCount
			|| nativeEmoteReactionCooldown > 0)
			return;
		nativeEmoteReactionCooldown = 45;
		PerformEmote(RelationshipEmoteFor(emoteId), emoteId, applyRestCommand: false);
	}

	private void PerformEmote(CompanionEmote emote, int nativeEmoteId, bool applyRestCommand)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || !Enum.IsDefined(emote))
			return;

		bool rewarded = interactionRewardCooldown <= 0;
		bool leveledUp = false;
		int newLevel = Profile.Level;
		if (rewarded) {
			(int bond, int mood, int energy, int experience) = emote switch {
				CompanionEmote.Wave => (1, 1, 0, 1),
				CompanionEmote.Heart => (2, 4, 0, 2),
				CompanionEmote.Cheer => (1, 2, 2, 2),
				CompanionEmote.Comfort => (2, 5, 3, 2),
				CompanionEmote.Laugh => (1, 4, 0, 1),
				_ => (2, 2, 8, 2)
			};
			Profile.ChangeBond(bond);
			Profile.Mood = Math.Clamp(Profile.Mood + mood, 0, 100);
			Profile.Energy = Math.Clamp(Profile.Energy + energy, 0, 100);
			Profile.Interactions++;
			leveledUp = Profile.GainExperience(experience, out newLevel);
			if (Profile.Interactions == 1 || Profile.Interactions % 5 == 0)
				Profile.Remember(CompanionMemoryKind.SharedMoment, detail: emote.ToString());
			interactionRewardCooldown = 300;
		}

		if (applyRestCommand && emote == CompanionEmote.Rest)
			SetCommand(stay: true);
		StartEmote(emote, 150);
		if (nativeEmoteId >= 0)
			ShowNativeEmote(nativeEmoteId, 150);
		else
			ShowNativeEmote(emote, 150);
		if (Main.rand.NextBool(4))
			SpeakLocalized($"Social.Emotes.{emote}.{Profile.Personality}");
		SyncProfileToBoundSigil();
		NPC.netUpdate = true;
		string message = leveledUp ? SoulmatesText.Get("Messages.LevelUp", Profile.Name, newLevel) : "";
		if (Main.netMode == NetmodeID.Server)
			global::Soulmates.Soulmates.SendProfileUpdate(Owner, this, message);
		else if (!string.IsNullOrEmpty(message))
			Main.NewText(message, Profile.EssenceColor);
	}

	private static CompanionEmote RelationshipEmoteFor(int emoteId) => emoteId switch {
		EmoteID.EmotionLove or EmoteID.EmoteKiss or EmoteID.EmoteWink => CompanionEmote.Heart,
		EmoteID.EmoteHappiness => CompanionEmote.Cheer,
		EmoteID.EmoteLaugh or EmoteID.EmoteSilly => CompanionEmote.Laugh,
		EmoteID.EmoteSleep => CompanionEmote.Rest,
		EmoteID.EmotionCry or EmoteID.EmoteSadness or EmoteID.EmoteFear or EmoteID.EmoteConfused
			=> CompanionEmote.Comfort,
		EmoteID.EmotionAnger or EmoteID.EmoteAnger or EmoteID.EmoteFight or EmoteID.EmoteScowl
			=> CompanionEmote.Cheer,
		_ => CompanionEmote.Wave
	};

	public void RecordDefeat(NPC defeated)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || defeated.type == NPCID.TargetDummy)
			return;
		bool creature = defeated.catchItem > 0 || defeated.type != NPCID.None && defeated.type < NPCID.Sets.CountsAsCritter.Length
			&& NPCID.Sets.CountsAsCritter[defeated.type];
		int experience = defeated.boss ? 12 : creature ? 1 : Math.Clamp(1 + defeated.lifeMax / 120, 1, 7);
		Profile.DefeatedEnemies++;
		bool leveledUp = Profile.GainExperience(experience, out int newLevel);

		if (creature) {
			Profile.Remember(CompanionMemoryKind.CreatureEncounter, detail: defeated.TypeName);
			int moodDelta = Profile.Personality switch {
				CompanionPersonality.Gentle => -5,
				CompanionPersonality.Curious => -1,
				CompanionPersonality.Mischievous => -1,
				_ => 0
			};
			Profile.Mood = Math.Clamp(Profile.Mood + moodDelta, 0, 100);
		}
		else {
			if (Profile.HasTalent(CompanionTalent.Guardian))
				Profile.Remember(CompanionMemoryKind.GuardianVictory, detail: defeated.TypeName);
			Profile.Mood = Math.Min(100, Profile.Mood + 1);
		}

		if (combatReactionCooldown <= 0 || defeated.boss || creature) {
			string kind = defeated.boss ? "Boss" : creature ? "Creature" : "Victory";
			CompanionEmote reaction = creature && Profile.Personality == CompanionPersonality.Gentle
				? CompanionEmote.Comfort
				: CompanionEmote.Cheer;
			StartEmote(reaction, 130);
			ShowNativeEmote(reaction, 140);
			if (defeated.boss || Main.rand.NextBool(5))
				SpeakLocalized($"Social.Combat.{kind}.{Profile.Personality}");
			combatReactionCooldown = creature ? 360 : 240;
		}

		SyncProfileToBoundSigil();
		NPC.netUpdate = true;
		string message = leveledUp ? SoulmatesText.Get("Messages.LevelUp", Profile.Name, newLevel) : "";
		if (Main.netMode == NetmodeID.Server)
			global::Soulmates.Soulmates.SendProfileUpdate(Owner, this, message);
		else if (!string.IsNullOrEmpty(message))
			Main.NewText(message, Profile.EssenceColor);
	}

	private void StartEmote(CompanionEmote emote, int duration)
	{
		activeEmote = emote;
		emoteTimer = Math.Max(emoteTimer, duration);
		NPC.netUpdate = true;
	}

	private void PauseAssignmentForRecovery()
	{
		activeJob = CompanionJob.None;
		jobTimer = 0;
		jobCount = 0;
		gatherPause = 0;
		jobTargetItem = -1;
		areaEmptyTimer = 0;
		hasJobTarget = false;
		jobOrigin = Vector2.Zero;
		failedMiningTargets.Clear();
		Command = StayCommand;
		brainState = BrainState.Stay;
		idleTarget = NPC.Center;
		SyncProfileToBoundSigil();
		NPC.netUpdate = true;
	}

	private void ShowNativeEmote(CompanionEmote emote, int duration)
	{
		int emoteId = emote switch {
			CompanionEmote.Heart => EmoteID.EmotionLove,
			CompanionEmote.Cheer => EmoteID.EmoteHappiness,
			CompanionEmote.Laugh => EmoteID.EmoteLaugh,
			CompanionEmote.Comfort => EmoteID.EmoteKiss,
			CompanionEmote.Rest => EmoteID.EmoteSleep,
			_ => EmoteID.EmoteWink
		};
		ShowNativeEmote(emoteId, duration);
	}

	private void ShowNativeEmote(int emoteId, int duration)
	{
		if (emoteId >= 0 && emoteId < EmoteBubbleLoader.EmoteBubbleCount)
			EmoteBubble.NewBubble(emoteId, new WorldUIAnchor(NPC), duration);
	}

	private void SpeakLocalized(string key, string argument = "")
	{
		if (Main.netMode == NetmodeID.Server)
			global::Soulmates.Soulmates.SendCompanionSpeech(Owner, this, key, argument);
		else
			ShowSpeech(string.IsNullOrEmpty(argument) ? SoulmatesText.Get(key) : SoulmatesText.Get(key, argument));
	}

	public string StoreSelectedItem()
	{
		Item selected = Owner.inventory[Owner.selectedItem];
		if (selected.IsAir)
			return SoulmatesText.Get("Pack.SelectedEmpty");
		if (!CanCarry(selected))
			return SoulmatesText.Get("Pack.CannotCarry");
		int moved = Profile.Store(selected);
		if (moved <= 0)
			return SoulmatesText.Get("Pack.Full", Profile.PackLoad, Profile.PackCapacity);
		SyncProfileToBoundSigil();
		NPC.netUpdate = true;
		return SoulmatesText.Get(moved == 1 ? "Pack.StoredOne" : "Pack.StoredMany", moved, Profile.PackLoad, Profile.PackCapacity);
	}

	public string UnloadPack()
	{
		int moved = 0;
		for (int i = Profile.Pack.Count - 1; i >= 0; i--) {
			Item stored = Profile.Pack[i];
			int originalStack = stored.stack;
			Item leftover = Owner.GetItem(Owner.whoAmI, stored.Clone(), GetItemSettings.InventoryEntityToPlayerInventorySettings);
			moved += originalStack - (leftover.IsAir ? 0 : leftover.stack);
			if (leftover.IsAir)
				Profile.Pack.RemoveAt(i);
			else
				Profile.Pack[i] = leftover;
		}
		SyncProfileToBoundSigil();
		NPC.netUpdate = true;
		return moved > 0
			? SoulmatesText.Get(moved == 1 ? "Pack.ReturnedOne" : "Pack.ReturnedMany", moved, Profile.PackLoad, Profile.PackCapacity)
			: Profile.PackLoad == 0 ? SoulmatesText.Get("Pack.AlreadyEmpty") : SoulmatesText.Get("Pack.InventoryFull");
	}

	public string WithdrawPackSlot(int index, bool singleItem)
	{
		if (index < 0 || index >= Profile.Pack.Count || Profile.Pack[index].IsAir)
			return SoulmatesText.Get("Pack.SlotEmpty");

		Item stored = Profile.Pack[index];
		int requested = singleItem ? 1 : stored.stack;
		Item transfer = stored.Clone();
		transfer.stack = requested;
		Item leftover = Owner.GetItem(Owner.whoAmI, transfer, GetItemSettings.InventoryEntityToPlayerInventorySettings);
		int moved = requested - (leftover.IsAir ? 0 : leftover.stack);
		if (moved <= 0)
			return SoulmatesText.Get("Pack.InventoryFull");

		stored.stack -= moved;
		if (stored.stack <= 0)
			Profile.Pack.RemoveAt(index);
		SyncProfileToBoundSigil();
		SyncOwnerInventory();
		NPC.netUpdate = true;
		return SoulmatesText.Get(moved == 1 ? "Pack.WithdrewOne" : "Pack.WithdrewMany", moved, Profile.PackLoad, Profile.PackCapacity);
	}

	private static bool CanCarry(Item item) => item.ModItem is not Soulcore and not SoulboundSigil and not CompanionTrinketItem;

	private bool CanCollectLooseItem(Item item) => CanCarry(item) && !IsRecoveryPickup(item) && Profile.CanStore(item)
		&& (item.playerIndexTheItemIsReservedFor == 255 || item.playerIndexTheItemIsReservedFor == Owner.whoAmI);

	public void SyncProfileToBoundSigil()
	{
		foreach (Item item in Owner.inventory) {
			if (item.ModItem is SoulboundSigil sigil && sigil.Profile.Id == Profile.Id) {
				sigil.Profile = Profile.Clone();
				return;
			}
		}
	}

	public SoulboundSigil? FindBoundSigil()
	{
		foreach (Item item in Owner.inventory) {
			if (item.ModItem is SoulboundSigil sigil && sigil.Profile.Id == Profile.Id)
				return sigil;
		}
		return null;
	}

	public void Recall()
	{
		SyncProfileToBoundSigil();
		SyncOwnerInventory();
		StopOwnedEffects();
		if (Owner.active)
			Owner.GetModPlayer<SoulmatesPlayer>().ActiveCompanionWhoAmI = -1;
		NPC.active = false;
		NPC.netUpdate = true;
		if (Main.netMode == NetmodeID.Server)
			NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
	}

	private void SyncOwnerInventory()
	{
		if (Main.netMode != NetmodeID.Server)
			return;
		for (int slot = 0; slot < Owner.inventory.Length; slot++)
			NetMessage.SendData(MessageID.SyncEquipment, Owner.whoAmI, -1, null, Owner.whoAmI, slot);
	}

	public static SoulboundCompanion? FindFor(Player player)
	{
		SoulmatesPlayer state = player.GetModPlayer<SoulmatesPlayer>();
		if (IsOwnedCompanion(state.ActiveCompanionWhoAmI, player))
			return Main.npc[state.ActiveCompanionWhoAmI].ModNPC as SoulboundCompanion;

		for (int i = 0; i < Main.maxNPCs; i++) {
			NPC npc = Main.npc[i];
			if (IsOwnedCompanion(i, player)) {
				state.ActiveCompanionWhoAmI = i;
				return npc.ModNPC as SoulboundCompanion;
			}
		}
		state.ActiveCompanionWhoAmI = -1;
		return null;
	}

	public static void RecallAllFor(Player player)
	{
		for (int i = 0; i < Main.maxNPCs; i++) {
			if (IsOwnedCompanion(i, player) && Main.npc[i].ModNPC is SoulboundCompanion companion)
				companion.Recall();
		}
		player.GetModPlayer<SoulmatesPlayer>().ActiveCompanionWhoAmI = -1;
	}

	private bool ClaimActiveSlot(Player owner)
	{
		SoulmatesPlayer state = owner.GetModPlayer<SoulmatesPlayer>();
		if (state.ActiveCompanionWhoAmI == NPC.whoAmI)
			return true;
		if (IsOwnedCompanion(state.ActiveCompanionWhoAmI, owner)) {
			int keeper = state.ActiveCompanionWhoAmI;
			Recall();
			state.ActiveCompanionWhoAmI = keeper;
			return false;
		}
		state.ActiveCompanionWhoAmI = NPC.whoAmI;
		return true;
	}

	private void StopOwnedEffects()
	{
		for (int i = 0; i < Main.maxProjectiles; i++) {
			Projectile projectile = Main.projectile[i];
			if (projectile.active && projectile.type == ModContent.ProjectileType<SoulBolt>()
				&& (int)projectile.ai[1] == NPC.whoAmI)
				projectile.Kill();
		}
		guardianTarget = -1;
		activeJob = CompanionJob.None;
		CancelAutonomousActivity();
		speechTimer = 0;
		speechText = "";
	}

	private static bool IsOwnedCompanion(int index, Player player)
	{
		if (index < 0 || index >= Main.maxNPCs)
			return false;
		NPC npc = Main.npc[index];
		return npc.active && npc.type == ModContent.NPCType<SoulboundCompanion>() && (int)npc.ai[0] == player.whoAmI;
	}

	private bool TryGetOwner(out Player owner)
	{
		int index = (int)NPC.ai[0];
		if (index < 0 || index >= Main.maxPlayers) {
			owner = null!;
			return false;
		}
		owner = Main.player[index];
		return owner.active;
	}

}
