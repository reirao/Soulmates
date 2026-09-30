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

public sealed partial class SoulboundCompanion
{
	internal bool CanTargetMining(Point target)
	{
		if (!WorldGen.InWorld(target.X, target.Y, 10)
			|| Vector2.DistanceSquared(Owner.Center, target.ToWorldCoordinates()) > MathF.Pow((MiningRadiusTiles + 8) * 16f, 2f))
			return false;
		return CanMineTile(target.X, target.Y, includeLearnedMaterials: true);
	}

	internal bool CanTargetGathering(int itemIndex)
	{
		if (itemIndex < 0 || itemIndex >= Main.maxItems)
			return false;
		Item item = Main.item[itemIndex];
		return item.active && !item.IsAir && CanCollectLooseItem(item)
			&& Vector2.DistanceSquared(Owner.Center, item.Center) <= MathF.Pow((GatheringRadiusTiles + 8) * 16f, 2f);
	}

	internal List<(Point Tile, int ItemType)> FindNearbyOreTargets(int maximumTypes = 7)
	{
		var nearestByDrop = new Dictionary<int, (Point Tile, float Distance)>();
		Point center = Owner.Center.ToTileCoordinates();
		int radius = MiningRadiusTiles + 8;
		for (int x = center.X - radius; x <= center.X + radius; x++) {
			for (int y = center.Y - radius; y <= center.Y + radius; y++) {
				if (!WorldGen.InWorld(x, y, 10))
					continue;
				var target = new Point(x, y);
				if (Vector2.DistanceSquared(target.ToVector2(), center.ToVector2()) > radius * radius)
					continue;
				Tile tile = Main.tile[x, y];
				if (!tile.HasTile || !IsOreTile(tile.TileType) || !CanTargetMining(target))
					continue;
				int itemType = TileLoader.GetItemDropFromTypeAndStyle(tile.TileType, 0);
				if (itemType <= ItemID.None)
					continue;
				float distance = Vector2.DistanceSquared(target.ToWorldCoordinates(), Owner.Center);
				if (!nearestByDrop.TryGetValue(itemType, out var known) || distance < known.Distance)
					nearestByDrop[itemType] = (target, distance);
			}
		}
		return nearestByDrop
			.OrderBy(entry => entry.Value.Distance)
			.Take(Math.Clamp(maximumTypes, 1, 8))
			.Select(entry => (entry.Value.Tile, entry.Key))
			.ToList();
	}

	public CompanionConversationResult PerformDirectOrder(CompanionTargetOrder order, Point tileTarget, int itemTarget)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient)
			return new CompanionConversationResult(SoulmatesText.Get("TargetOrders.Invalid"), false);
		if (Profile.Energy < 5)
			return new CompanionConversationResult(SoulmatesText.Get("TargetOrders.LowEnergy"), false);
		if (Profile.Mood < 15)
			return new CompanionConversationResult(SoulmatesText.Get("TargetOrders.LowMood"), false);

		return order switch {
			CompanionTargetOrder.Mine => StartDirectedMining(tileTarget),
			CompanionTargetOrder.Gather => StartDirectedGathering(itemTarget),
			_ => new CompanionConversationResult(SoulmatesText.Get("TargetOrders.Invalid"), false)
		};
	}

	private CompanionConversationResult StartDirectedMining(Point target)
	{
		if (!CanTargetMining(target))
			return new CompanionConversationResult(SoulmatesText.Get("TargetOrders.CannotMine"), false);

		ushort tileType = Main.tile[target.X, target.Y].TileType;
		BeginJob(CompanionJob.Mine);
		Profile.Routine = CompanionJob.None;
		directedJob = true;
		directedMiningTileType = tileType;
		BuildDirectedMiningTargets(target, tileType);
		miningPlanReady = true;
		jobPlannedTotal = directedMiningTargets.Count;
		jobOrigin = target.ToWorldCoordinates();
		jobTarget = target;
		hasJobTarget = true;
		SyncProfileToBoundSigil();
		NPC.netUpdate = true;

		string materialName = DirectedMaterialName();
		SoulmatesFeedbackSystem.Record("direct_order_started", ("order", "mine"),
			("tile_type", tileType), ("target_count", directedMiningTargets.Count));
		return new CompanionConversationResult(SoulmatesText.Get("TargetOrders.MineAccepted", materialName), true);
	}

	private CompanionConversationResult StartDirectedGathering(int itemIndex)
	{
		if (!CanTargetGathering(itemIndex))
			return new CompanionConversationResult(SoulmatesText.Get("TargetOrders.CannotGather"), false);

		Item item = Main.item[itemIndex];
		string itemName = item.Name;
		BeginJob(CompanionJob.Gather);
		Profile.Routine = CompanionJob.None;
		directedJob = true;
		jobOrigin = item.Center;
		jobTargetItem = itemIndex;
		jobPlannedTotal = item.stack;
		SyncProfileToBoundSigil();
		NPC.netUpdate = true;

		SoulmatesFeedbackSystem.Record("direct_order_started", ("order", "gather"),
			("item_type", item.type), ("amount", item.stack));
		return new CompanionConversationResult(SoulmatesText.Get("TargetOrders.GatherAccepted", itemName), true);
	}

	private void BuildDirectedMiningTargets(Point origin, ushort tileType)
	{
		directedMiningTargets.Clear();
		var pending = new Queue<Point>();
		var visited = new HashSet<Point>();
		pending.Enqueue(origin);
		while (pending.Count > 0 && directedMiningTargets.Count < DirectedMiningTargetLimit) {
			Point current = pending.Dequeue();
			if (!visited.Add(current) || !WorldGen.InWorld(current.X, current.Y, 10)
				|| Vector2.DistanceSquared(current.ToVector2(), origin.ToVector2()) > 12f * 12f)
				continue;
			Tile tile = Main.tile[current.X, current.Y];
			if (!tile.HasTile || tile.TileType != tileType
				|| !CanMineTile(current.X, current.Y, includeLearnedMaterials: true))
				continue;
			directedMiningTargets.Add(current);
			for (int x = -1; x <= 1; x++) {
				for (int y = -1; y <= 1; y++) {
					if (x != 0 || y != 0)
						pending.Enqueue(new Point(current.X + x, current.Y + y));
				}
			}
		}
	}

	private string DirectedMaterialName()
	{
		int dropType = TileLoader.GetItemDropFromTypeAndStyle(directedMiningTileType, 0);
		return dropType > ItemID.None
			? Lang.GetItemNameValue(dropType)
			: SoulmatesText.Get("Resourcefulness.UnknownMaterial");
	}

	private void ClearDirectedJob()
	{
		directedJob = false;
		directedMiningTileType = 0;
		directedMiningTargets.Clear();
	}

	private void UpdateJob()
	{
		if (Main.netMode == NetmodeID.MultiplayerClient) {
			if (!jobRecoveryPaused) {
				jobTimer++;
				UpdateJobEffects();
			}
			return;
		}
		if (jobRecoveryPaused) {
			UpdatePausedJobRecovery();
			return;
		}
		jobTimer++;
		UpdateJobEffects();
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

	private void UpdatePausedJobRecovery()
	{
		MoveTo(idleTarget + new Vector2(0f, IdleBob()), 1.6f, 0.028f);
		RecoverEnergy(120, Profile.Trinket == CompanionTrinket.HearthRibbon ? 5 : 3, recoverMood: true);
		if (Profile.Energy < 12 || Profile.Mood < 20)
			return;

		jobRecoveryPaused = false;
		recoveryTimer = 0;
		Command = FollowCommand;
		brainState = BrainState.Follow;
		stateTimer = 1;
		ShowNativeEmote(EmoteID.EmoteHappiness, 90);
		SoulmatesFeedbackSystem.Record("job_resumed_after_recovery", ("job", activeJob.ToString()),
			("work_count", jobCount), ("planned_total", jobPlannedTotal), ("energy", Profile.Energy));
		SyncProfileToBoundSigil();
		NPC.netUpdate = true;
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
		MoveTo(target, 6.5f, 0.07f);
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
		if (jobTimer > 7200) {
			CompleteJob(SoulmatesText.Get("Jobs.Mining.Timeout", jobCount), success: false);
			return;
		}
		if (!hasJobTarget && !FindMiningTarget(out jobTarget)) {
			string report = directedJob
				? jobCount > 0
					? SoulmatesText.Get("TargetOrders.Mined", jobCount, DirectedMaterialName())
					: SoulmatesText.Get("TargetOrders.TargetLost")
				: failedMiningTargets.Count > 0
				? SoulmatesText.Get("Jobs.Mining.Protected", jobCount, failedMiningTargets.Count)
				: jobCount > 0
					? SoulmatesText.Get("Jobs.Mining.Cleared", jobCount, MiningRadiusTiles)
					: SoulmatesText.Get(Profile.MiningApproach == CompanionMiningApproach.Tunnel
						? "Jobs.Mining.TunnelBlocked" : "Jobs.Mining.Empty", MiningRadiusTiles);
			CompleteJob(report, jobCount > 0, CompanionMemoryKind.MiningCompleted, jobCount);
			return;
		}
		hasJobTarget = true;
		Vector2 target = jobTarget.ToWorldCoordinates();
		MoveTo(target, 6f, 0.075f);
		if (Vector2.DistanceSquared(NPC.Center, target) > 58f * 58f)
			return;

		int miningDelay = Profile.Trinket == CompanionTrinket.DelverCharm ? 18 : 28;
		if (jobTimer % miningDelay != 0)
			return;
		if (Main.netMode == NetmodeID.MultiplayerClient)
			return;
		if (!WorldGen.InWorld(jobTarget.X, jobTarget.Y, 10)
			|| !Main.tile[jobTarget.X, jobTarget.Y].HasTile
			|| !CanMineTile(jobTarget.X, jobTarget.Y, includeLearnedMaterials: true)
			|| directedJob && Main.tile[jobTarget.X, jobTarget.Y].TileType != directedMiningTileType
			|| !directedJob && !IsAllowedByMiningApproach(jobTarget, Main.tile[jobTarget.X, jobTarget.Y].TileType)) {
			DiscardMiningTarget(jobTarget);
			hasJobTarget = false;
			return;
		}
		ushort minedTileType = Main.tile[jobTarget.X, jobTarget.Y].TileType;
		int pickPower = EffectivePickPower(out int pickItemType);
		WorldGen.KillTile(jobTarget.X, jobTarget.Y);
		if (!Main.tile[jobTarget.X, jobTarget.Y].HasTile) {
			jobCount++;
			SoulmatesFeedbackSystem.Record("companion_mined_tile", ("tile_type", minedTileType),
				("pick_power", pickPower), ("pick_item_type", pickItemType), ("autonomous", false));
			Profile.Energy = Math.Max(0, Profile.Energy - 1);
			CollectNearbyLooseItems(target, 96f, 6);
			SyncProfileToBoundSigil();
			NPC.netUpdate = true;
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 0, jobTarget.X, jobTarget.Y);
		}
		else
			failedMiningTargets.Add(jobTarget);
		DiscardMiningTarget(jobTarget);
		hasJobTarget = false;
	}

	private void UpdateGatherJob()
	{
		if (gatherForestAction != ForestAction.None) {
			UpdateGatherForestTask();
			return;
		}
		if (gatherPause > 0) {
			gatherPause--;
			MoveTo(jobOrigin + new Vector2(0f, -52f + IdleBob()), 5.5f, 0.065f);
			return;
		}
		if (directedJob && !IsValidGatherTarget(jobTargetItem)) {
			CompleteJob(SoulmatesText.Get("TargetOrders.TargetLost"), success: false);
			return;
		}
		if (!directedJob && !IsValidGatherTarget(jobTargetItem) && areaEmptyTimer % 10 == 0)
			jobTargetItem = FindNearestLooseItem();
		if (jobTargetItem < 0) {
			bool checkForest = areaEmptyTimer % 30 == 0;
			if (checkForest && FindForestTask(jobOrigin, out jobTarget, out gatherForestAction)) {
				areaEmptyTimer = 0;
				hasJobTarget = true;
				UpdateGatherForestTask();
				return;
			}
			areaEmptyTimer++;
			MoveTo(jobOrigin + new Vector2(0f, -54f + IdleBob()), 4f, 0.05f);
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
		MoveTo(item.Center + new Vector2(0f, -8f), 6.5f, 0.07f);
		if (Vector2.DistanceSquared(NPC.Center, item.Center) < 42f * 42f) {
			if (Main.netMode == NetmodeID.MultiplayerClient)
				return;
			if (!CanCollectLooseItem(item)) {
				gatherPause = 45;
				return;
			}
			string itemName = item.Name;
			int moved = StoreLooseItem(item);
			if (moved <= 0) {
				CompleteJob(SoulmatesText.Get("Jobs.PackFull"), success: false);
				return;
			}
			jobCount += moved;
			Profile.Energy = Math.Max(0, Profile.Energy - 1);
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendData(MessageID.SyncItem, -1, -1, null, jobTargetItem);
			SyncPackState();
			jobTargetItem = -1;
			gatherPause = 6;
			if (directedJob) {
				CompleteJob(SoulmatesText.Get("TargetOrders.Gathered", moved, itemName), success: true,
					CompanionMemoryKind.GatheringCompleted, moved);
				return;
			}
		}
		if (jobTimer > 7200)
			CompleteJob(SoulmatesText.Get("Jobs.Gathering.Timeout", jobCount), success: false);
	}

	private void UpdateGatherForestTask()
	{
		Vector2 target = jobTarget.ToWorldCoordinates();
		MoveTo(target + new Vector2(0f, -18f), 6f, 0.07f);
		if (Vector2.DistanceSquared(NPC.Center, target) > 68f * 68f || jobTimer % 24 != 0)
			return;

		ForestAction action = gatherForestAction;
		bool success = PerformForestAction(jobTarget, action);
		SoulmatesFeedbackSystem.Record("forestry_action", ("action", action.ToString()),
			("success", success), ("pack_load", Profile.PackLoad));
		tendedForestTargets.Add(jobTarget);
		gatherForestAction = ForestAction.None;
		hasJobTarget = false;
		jobTarget = Point.Zero;
		gatherPause = success ? 6 : 2;
		if (!success)
			return;

		jobCount++;
		Profile.Energy = Math.Max(0, Profile.Energy - 1);
		Profile.GainExperience(Profile.ForesterUnlocked ? 2 : 1, out _);
		ShowNativeEmote(EmoteID.MiscTree, 75);
		SyncPackState();
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
		if (directedJob) {
			directedMiningTargets.RemoveWhere(candidate => !WorldGen.InWorld(candidate.X, candidate.Y, 10)
				|| !Main.tile[candidate.X, candidate.Y].HasTile
				|| Main.tile[candidate.X, candidate.Y].TileType != directedMiningTileType
				|| !CanMineTile(candidate.X, candidate.Y, includeLearnedMaterials: true));
			result = Point.Zero;
			float nearest = float.MaxValue;
			foreach (Point candidate in directedMiningTargets) {
				float distance = Vector2.DistanceSquared(candidate.ToWorldCoordinates(), NPC.Center);
				if (distance >= nearest)
					continue;
				nearest = distance;
				result = candidate;
			}
			return result != Point.Zero;
		}

		if (!miningPlanReady)
			PlanAreaMiningTargets();
		while (plannedMiningCursor < plannedMiningTargets.Count) {
			Point candidate = plannedMiningTargets[plannedMiningCursor];
			if (WorldGen.InWorld(candidate.X, candidate.Y, 10)
				&& Main.tile[candidate.X, candidate.Y].HasTile
				&& !failedMiningTargets.Contains(candidate)
				&& IsAllowedByMiningApproach(candidate, Main.tile[candidate.X, candidate.Y].TileType)
				&& CanMineTile(candidate.X, candidate.Y, includeLearnedMaterials: true)) {
				result = candidate;
				return true;
			}
			plannedMiningCursor++;
		}
		result = Point.Zero;
		return false;
	}

	private void PlanAreaMiningTargets()
	{
		plannedMiningTargets.Clear();
		plannedMiningCursor = 0;
		Point center = jobOrigin.ToTileCoordinates();
		int radius = MiningRadiusTiles;
		var candidates = new List<Point>();
		for (int x = center.X - radius; x <= center.X + radius; x++) {
			for (int y = center.Y - radius; y <= center.Y + radius; y++) {
				if (!WorldGen.InWorld(x, y, 10))
					continue;
				Point candidate = new(x, y);
				if (Vector2.DistanceSquared(candidate.ToVector2(), center.ToVector2()) > radius * radius)
					continue;
				Tile tile = Main.tile[x, y];
				if (tile.HasTile && CanMineTile(x, y, includeLearnedMaterials: true)
					&& IsAllowedByMiningApproach(candidate, tile.TileType))
					candidates.Add(candidate);
			}
		}

		if (Profile.MiningApproach == CompanionMiningApproach.Tunnel)
			BuildTunnelMiningPlan(candidates, center, radius);
		else {
			plannedMiningTargets.AddRange(candidates);
			plannedMiningTargets.Sort(CompareMiningTargets);
		}
		jobPlannedTotal = jobCount + plannedMiningTargets.Count;
		miningPlanReady = true;
		SoulmatesFeedbackSystem.Record("mining_plan_created", ("target_count", plannedMiningTargets.Count),
			("planned_total", jobPlannedTotal), ("radius_tiles", radius),
			("approach", Profile.MiningApproach.ToString()));
		NPC.netUpdate = true;
	}

	private bool IsAllowedByMiningApproach(Point candidate, ushort tileType)
	{
		bool exposed = IsExposedMiningTile(candidate);
		return Profile.MiningApproach switch {
			CompanionMiningApproach.Vein => IsOreTile(tileType),
			CompanionMiningApproach.Surface => exposed,
			CompanionMiningApproach.Tunnel => !IsGravityMiningMaterial(tileType) || exposed,
			_ => !IsGravityMiningMaterial(tileType) || exposed
		};
	}

	private int CompareMiningTargets(Point left, Point right)
	{
		if (Profile.MiningApproach == CompanionMiningApproach.Adaptive) {
			bool leftOre = IsOreTile(Main.tile[left.X, left.Y].TileType);
			bool rightOre = IsOreTile(Main.tile[right.X, right.Y].TileType);
			if (leftOre != rightOre)
				return leftOre ? -1 : 1;
			bool leftExposed = IsExposedMiningTile(left);
			bool rightExposed = IsExposedMiningTile(right);
			if (leftExposed != rightExposed)
				return leftExposed ? -1 : 1;
		}
		float leftDistance = Vector2.DistanceSquared(left.ToWorldCoordinates(), jobOrigin);
		float rightDistance = Vector2.DistanceSquared(right.ToWorldCoordinates(), jobOrigin);
		return leftDistance.CompareTo(rightDistance);
	}

	private void BuildTunnelMiningPlan(List<Point> candidates, Point center, int radius)
	{
		if (candidates.Count == 0)
			return;
		var available = candidates.ToHashSet();
		Point? oreTarget = candidates
			.Where(point => IsOreTile(Main.tile[point.X, point.Y].TileType))
			.OrderBy(point => Vector2.DistanceSquared(point.ToVector2(), center.ToVector2()))
			.Select(point => (Point?)point)
			.FirstOrDefault();
		Point destination = oreTarget ?? new Point(center.X, center.Y + Math.Min(radius, 24));
		int dx = Math.Abs(destination.X - center.X);
		int dy = Math.Abs(destination.Y - center.Y);
		Point corridorOffset = dx >= dy ? new Point(0, -1) : new Point(1, 0);
		bool openedWall = false;
		int openRun = 0;

		foreach (Point step in TraceTileLine(center, destination)) {
			if (!WorldGen.InWorld(step.X, step.Y, 10))
				break;
			Tile tile = Main.tile[step.X, step.Y];
			if (!tile.HasTile || tile.IsActuated || tile.TileType >= Main.tileSolid.Length
				|| !Main.tileSolid[tile.TileType]) {
				if (openedWall && ++openRun >= 2)
					break;
				continue;
			}
			openRun = 0;
			if (!available.Contains(step))
				break;
			AddMiningPlanTarget(step);
			openedWall = true;

			Point companionSpace = new(step.X + corridorOffset.X, step.Y + corridorOffset.Y);
			if (available.Contains(companionSpace)
				&& !IsGravityMiningMaterial(Main.tile[companionSpace.X, companionSpace.Y].TileType))
				AddMiningPlanTarget(companionSpace);
		}

		if (oreTarget is Point ore && plannedMiningTargets.Contains(ore))
			AppendConnectedOreVein(ore, available, 24);
	}

	private void AppendConnectedOreVein(Point origin, HashSet<Point> available, int limit)
	{
		ushort tileType = Main.tile[origin.X, origin.Y].TileType;
		var pending = new Queue<Point>();
		var visited = new HashSet<Point>();
		pending.Enqueue(origin);
		int matched = 0;
		while (pending.Count > 0 && matched < limit) {
			Point current = pending.Dequeue();
			if (!visited.Add(current) || !available.Contains(current)
				|| Main.tile[current.X, current.Y].TileType != tileType)
				continue;
			AddMiningPlanTarget(current);
			matched++;
			pending.Enqueue(new Point(current.X - 1, current.Y));
			pending.Enqueue(new Point(current.X + 1, current.Y));
			pending.Enqueue(new Point(current.X, current.Y - 1));
			pending.Enqueue(new Point(current.X, current.Y + 1));
		}
	}

	private void AddMiningPlanTarget(Point target)
	{
		if (!plannedMiningTargets.Contains(target))
			plannedMiningTargets.Add(target);
	}

	private static IEnumerable<Point> TraceTileLine(Point start, Point end)
	{
		int x = start.X;
		int y = start.Y;
		int dx = Math.Abs(end.X - start.X);
		int sx = start.X < end.X ? 1 : -1;
		int dy = -Math.Abs(end.Y - start.Y);
		int sy = start.Y < end.Y ? 1 : -1;
		int error = dx + dy;
		while (true) {
			yield return new Point(x, y);
			if (x == end.X && y == end.Y)
				yield break;
			int doubled = error * 2;
			if (doubled >= dy) {
				error += dy;
				x += sx;
			}
			if (doubled <= dx) {
				error += dx;
				y += sy;
			}
		}
	}

	private static bool IsExposedMiningTile(Point point)
	{
		ReadOnlySpan<Point> neighbors = [
			new Point(point.X - 1, point.Y), new Point(point.X + 1, point.Y),
			new Point(point.X, point.Y - 1), new Point(point.X, point.Y + 1)
		];
		foreach (Point neighbor in neighbors) {
			if (!WorldGen.InWorld(neighbor.X, neighbor.Y, 10))
				continue;
			Tile tile = Main.tile[neighbor.X, neighbor.Y];
			if (!tile.HasTile || tile.IsActuated || tile.TileType >= Main.tileSolid.Length
				|| !Main.tileSolid[tile.TileType])
				return true;
		}
		return false;
	}

	private static bool IsGravityMiningMaterial(ushort type) => type is TileID.Sand or TileID.Ebonsand
		or TileID.Crimsand or TileID.Pearlsand or TileID.Silt or TileID.Slush;

	private void DiscardMiningTarget(Point target)
	{
		if (directedJob) {
			directedMiningTargets.Remove(target);
			return;
		}
		if (plannedMiningCursor < plannedMiningTargets.Count
			&& plannedMiningTargets[plannedMiningCursor] == target)
			plannedMiningCursor++;
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
			if (!CanCollectLooseItem(item))
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
		if (!worldItem.active || worldItem.IsAir || worldItem.stack <= 0)
			return 0;

		int itemType = worldItem.type;
		int worldStackBefore = worldItem.stack;
		int available = AvailableCarryAmount(worldItem);
		if (available <= 0)
			return 0;
		int matchingItemsBefore = Profile.ItemCount(worldItem.type);
		var packBefore = new List<Item>(Profile.Pack.Count);
		foreach (Item stored in Profile.Pack)
			packBefore.Add(stored.Clone());
		Item transfer = worldItem.Clone();
		transfer.stack = available;
		int moved = Math.Clamp(Profile.Store(transfer), 0, available);
		int confirmed = Math.Clamp(Profile.ItemCount(worldItem.type) - matchingItemsBefore, 0, available);
		if (moved <= 0 || confirmed != moved) {
			SoulmatesFeedbackSystem.Record("pack_transaction_rejected", ("item_type", itemType),
				("available", available), ("reported_moved", moved), ("confirmed_moved", confirmed),
				("world_stack_before", worldStackBefore), ("pack_load", Profile.PackLoad));
			Profile.Pack = packBefore;
			return 0;
		}

		worldItem.stack -= moved;
		if (worldItem.stack <= 0) {
			worldItem.TurnToAir();
			worldItem.active = false;
		}
		SoulmatesFeedbackSystem.Record("pack_auto_collect", ("item_type", itemType), ("amount", moved),
			("world_stack_before", worldStackBefore), ("world_stack_after", Math.Max(0, worldStackBefore - moved)),
			("type_total_after", Profile.ItemCount(itemType)), ("pack_load", Profile.PackLoad));
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

		if (movedTotal > 0)
			SyncPackState();
		return movedTotal;
	}

	private void RevealSurroundings()
	{
		if (Main.dedServ || Owner.whoAmI != Main.myPlayer || ++revealTimer < 10)
			return;
		revealTimer = 0;
		Point center = NPC.Center.ToTileCoordinates();
		if (center == lastRevealCenter)
			return;
		lastRevealCenter = center;
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
		SoulmatesFeedbackSystem.Record("job_completed", ("job", activeJob.ToString()),
			("success", success), ("work_count", jobCount), ("memory_amount", memoryAmount),
			("pack_load", Profile.PackLoad));
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
		jobPlannedTotal = 0;
		jobRecoveryPaused = false;
		miningPlanReady = false;
		plannedMiningTargets.Clear();
		plannedMiningCursor = 0;
		gatherPause = 0;
		jobTargetItem = -1;
		gatherForestAction = ForestAction.None;
		areaEmptyTimer = 0;
		hasJobTarget = false;
		jobOrigin = Vector2.Zero;
		failedMiningTargets.Clear();
		ClearDirectedJob();
		brainState = BrainState.Follow;
		stateTimer = 1;
		NPC.netUpdate = true;
	}

}
