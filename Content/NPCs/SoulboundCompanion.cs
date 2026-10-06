#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Soulmates.Common;
using Soulmates.Common.Feedback;
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

public sealed partial class SoulboundCompanion : ModNPC
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
		TendForest,
		InviteCritter,
		CatchCritter
	}

	internal enum ForestAction : byte
	{
		None,
		ShakeTree,
		ClearDeadwood,
		PlantAcorn,
		PruneBranch
	}

	private const float FollowCommand = 0f;
	private const float StayCommand = 1f;
	private const float StandardDefenseRange = 320f;
	private const float GuardianDefenseRange = 448f;
	private const int AutonomousLootSweepLimit = 12;
	private const int AutonomousLootTargetTimeout = 480;
	private const int AutonomousForestSweepLimit = 5;
	private const int AutonomousMiningSweepLimit = 24;
	private const int DirectedMiningTargetLimit = 24;
	private BrainState brainState;
	private int stateTimer;
	private int facing = 1;
	private int facingCooldown;
	private int actionAnimationTicks;
	private Vector2 idleTarget;
	private float bobSeed;
	private CompanionJob activeJob;
	private int jobTimer;
	private int jobCount;
	private Point jobTarget;
	private bool hasJobTarget;
	private bool directedJob;
	private ushort directedMiningTileType;
	private readonly HashSet<Point> directedMiningTargets = [];
	private readonly List<Point> plannedMiningTargets = [];
	private int plannedMiningCursor;
	private bool miningPlanReady;
	private bool jobRecoveryPaused;
	private int jobPlannedTotal;
	private int recoveryTimer;
	private int gatherPause;
	private int jobTargetItem = -1;
	private Item? directedLootIdentity;
	private int directedLootType;
	private ForestAction gatherForestAction;
	private int areaEmptyTimer;
	private int revealTimer;
	private Point lastRevealCenter = new(-1, -1);
	private int attackCooldown;
	private int healingCooldown;
	private int guardianTarget = -1;
	private Vector2 jobOrigin;
	private int socialTimer;
	private int interactionRewardCooldown;
	private int nativeEmoteReactionCooldown;
	private int combatReactionCooldown;
	private int speechTimer;
	private int speechDuration;
	private int emoteTimer;
	private int chatterSequence;
	private int townNpcInteractionCooldown;
	private int socialNpcTarget = -1;
	private int socialNpcTimer;
	private bool socialNpcGreeted;
	private NPC? socialNpcIdentity;
	private int socialNpcType;
	private string socialResidentName = "";
	private EmoteBubble? socialResidentBubble;
	private bool socialNpcReplied;
	private int socialReplyDelay;
	private AutonomyActivity autonomyActivity;
	private AutonomyActivity pendingAutonomyActivity;
	private int autonomyDecisionTimer;
	private int autonomyActionTimer;
	private int autonomyTargetItem = -1;
	private Point autonomyTargetTile;
	private int autonomyWorkCount;
	private int autonomyAttemptCount;
	private int autonomyDiscoveryCooldown;
	private ForestAction autonomyForestAction;
	private int pendingInitiativeTimer;
	private Guid initiativeId;
	private int pendingTargetItem = -1;
	private Point pendingTargetTile;
	private ForestAction pendingForestAction;
	private readonly CompanionAttention attention = new();
	private readonly List<CompanionOpportunity> opportunities = new(4);
	private Item? pendingLootIdentity;
	private int pendingLootType;
	private int pendingLootPrefix;
	private int autonomyAnnouncementCooldown;
	private int learningObservationTimer;
	private readonly Dictionary<LearnedBehavior, int> imitationSignals = [];
	private int imitationCueTimer;
	private int tendedForestResetTimer;
	private int failedTreeShakeStreak;
	private int treeShakeCooldown;
	private bool previousDaytime;
	private bool packReconciled;
	private string speechText = "";
	private Vector2 speechAnchorWorld;
	private Vector2 speechTrailWorld;
	private CompanionEmote activeEmote;
	private readonly HashSet<Point> failedMiningTargets = [];
	private readonly HashSet<Point> tendedForestTargets = [];

	public CompanionProfile Profile { get; set; } = new();
	private Player Owner => Main.player[(int)NPC.ai[0]];
	private ref float Command => ref NPC.ai[1];
	public string CommandName => SoulmatesText.Get(Command == StayCommand ? "Status.Stay" : "Status.Follow");
	internal string FeedbackCommand => Command == StayCommand ? "stay" : "follow";
	internal string FeedbackActivity => guardianTarget >= 0 ? "guarding"
		: Profile.WorkPaused ? "paused"
		: activeJob != CompanionJob.None ? $"job_{activeJob}"
		: HasPendingQuestion ? $"conversation_{PendingQuestion}"
		: pendingAutonomyActivity != AutonomyActivity.None ? $"awaiting_{InitiativeKindFor(pendingAutonomyActivity)}"
		: autonomyActivity != AutonomyActivity.None ? $"autonomy_{autonomyActivity}"
		: IsAttendingCritter ? $"critter_{Profile.CritterMode}"
		: socialNpcTarget >= 0 ? "socializing"
		: brainState.ToString();
	public CompanionJob CurrentJob => activeJob;
	public bool HasPendingInitiative => pendingAutonomyActivity != AutonomyActivity.None;
	public CompanionInitiativeKind PendingInitiativeKind => InitiativeKindFor(pendingAutonomyActivity);
	public int PendingInitiativeTicks => Math.Max(0, pendingInitiativeTimer);
	public Guid InitiativeId => initiativeId;
	public bool IsDefending => guardianTarget >= 0;
	public string CurrentJobName => guardianTarget >= 0 ? SoulmatesText.Get("Status.Guarding")
		: Profile.WorkPaused ? SoulmatesText.Get("WorkControl.Status") : activeJob != CompanionJob.None
		? jobRecoveryPaused
			? jobPlannedTotal > 0
				? SoulmatesText.Get("Status.AssignmentPausedProgress", SoulmatesText.EnumName(activeJob), jobCount, jobPlannedTotal)
				: SoulmatesText.Get("Status.AssignmentPaused", SoulmatesText.EnumName(activeJob))
			: activeJob switch {
			CompanionJob.Mine when jobPlannedTotal > 0 => SoulmatesText.Get("Status.MiningProgress", jobCount, jobPlannedTotal),
			CompanionJob.Mine => SoulmatesText.Get("Status.Mining", jobCount),
			CompanionJob.Gather => SoulmatesText.Get("Status.Gathering", jobCount),
			CompanionJob.FindTreasure => SoulmatesText.Get("Status.SensingTreasure"),
			_ => SoulmatesText.EnumName(activeJob)
		}
		: pendingAutonomyActivity != AutonomyActivity.None
			? SoulmatesText.Get("Status.AwaitingAnswer", SoulmatesText.EnumName(PendingInitiativeKind))
		: autonomyActivity != AutonomyActivity.None
			? SoulmatesText.Get($"Status.Autonomy.{autonomyActivity}")
		: IsAttendingCritter
			? SoulmatesText.Get("Status.Assignment", SoulmatesText.EnumName(Profile.CritterMode == CompanionCritterMode.Collect
				? CompanionInitiativeKind.CritterCollect : CompanionInitiativeKind.CritterCompany))
			: autonomyRecoveryPaused || Profile.Energy < 12
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
	private bool GuardianSpecialist => Profile.HasTalent(CompanionTalent.Guardian)
		|| Profile.CombatInsight >= CompanionProfile.CombatUnlockInsight;
	private bool EagerGatherer => Profile.HasTalent(CompanionTalent.Gatherer)
		|| Profile.GatheringInsight >= CompanionProfile.GatheringUnlockInsight;
	private bool MiningInstinct => Profile.HasTalent(CompanionTalent.Miner)
		|| Profile.MiningInsight >= CompanionProfile.MiningUnlockInsight;
	private bool TreasureInstinct => Profile.HasTalent(CompanionTalent.TreasureSeeker)
		|| Profile.ExplorationInsight >= CompanionProfile.ExplorationUnlockInsight;
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
		autonomyDecisionTimer = Main.rand.Next(30, 90);
		tendedForestResetTimer = Main.rand.Next(1800, 3600);
		previousDaytime = Main.dayTime;
	}

	public override bool CheckActive() => !TryGetOwner(out Player owner) || owner.dead;

	public override bool CanChat() => TryGetOwner(out Player owner) && owner.whoAmI == Main.myPlayer;
	public override string GetChat() => SoulmatesText.Get("UI.Talk.Greeting", Profile.Name);

	// The player input router opens the wheel after native interactions have run.
	public override bool PreHoverInteract(bool mouseIntersects) => false;

	public override void AI()
	{
		if (!TryGetOwner(out Player owner) || owner.dead) {
			StopOwnedEffects();
			NPC.active = false;
			NPC.netUpdate = true;
			return;
		}
		if (Main.netMode != NetmodeID.MultiplayerClient) {
			if (CompanionInventorySync.IsPending(owner)) { NPC.velocity *= 0.8f; return; }
			if (!ClaimActiveSlot(owner))
				return;
			if (FindBoundSigil() is null) {
				Recall();
				return;
			}
			ReconcilePackOnce();
		}

		NPC.GivenName = Profile.Name;
		Lighting.AddLight(NPC.Center, Profile.EssenceColor.ToVector3() * 1.15f);
		RevealSurroundings();
		UpdateForestAwareness();
		ResumeAssignment();
		UpdateAuraDust();
		UpdateSocialState();
		UpdateLearningFromOwner();
		UpdateAttentionClock();
		if (!Profile.WorkPaused) {
			UpdateNearbyPickup();
			UpdateChoiceConversation();
		}
		UpdateAutonomousSocialBehavior();
		if (!Profile.WorkPaused) UpdateResourcefulness();
		UpdateNatureCompanions();
		UpdateEquippedPet();
		UpdateAmbientStories();
		if (attackCooldown > 0)
			attackCooldown--;
		if (healingCooldown > 0)
			healingCooldown--;
		Activities.Tick();
		UpdateDiagnostics();
	}

}
