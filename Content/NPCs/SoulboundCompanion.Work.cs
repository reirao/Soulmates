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
			|| !CanMineTile(jobTarget.X, jobTarget.Y, includeLearnedMaterials: true)) {
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
		hasJobTarget = false;
		if (jobTimer > 7200)
			CompleteJob(SoulmatesText.Get("Jobs.Mining.Timeout", jobCount), success: false);
	}

	private void UpdateGatherJob()
	{
		if (gatherForestAction != ForestAction.None) {
			UpdateGatherForestTask();
			return;
		}
		if (gatherPause > 0) {
			gatherPause--;
			MoveTo(jobOrigin + new Vector2(0f, -52f + IdleBob()), 7f, 0.08f);
			return;
		}
		if (!IsValidGatherTarget(jobTargetItem))
			jobTargetItem = FindNearestLooseItem();
		if (jobTargetItem < 0) {
			if (FindForestTask(jobOrigin, out jobTarget, out gatherForestAction)) {
				areaEmptyTimer = 0;
				hasJobTarget = true;
				UpdateGatherForestTask();
				return;
			}
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
			SyncPackState();
			jobTargetItem = -1;
			gatherPause = 6;
		}
		if (jobTimer > 7200)
			CompleteJob(SoulmatesText.Get("Jobs.Gathering.Timeout", jobCount), success: false);
	}

	private void UpdateGatherForestTask()
	{
		Vector2 target = jobTarget.ToWorldCoordinates();
		MoveTo(target + new Vector2(0f, -18f), 7f, 0.085f);
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
		Point center = jobOrigin.ToTileCoordinates();
		result = Point.Zero;
		float bestScore = float.MaxValue;
		int radius = MiningRadiusTiles;
		for (int x = center.X - radius; x <= center.X + radius; x++) {
			for (int y = center.Y - radius; y <= center.Y + radius; y++) {
				if (!WorldGen.InWorld(x, y, 10))
					continue;
				Tile tile = Main.tile[x, y];
				if (!tile.HasTile || !CanMineTile(x, y, includeLearnedMaterials: true))
					continue;
				var candidate = new Point(x, y);
				if (failedMiningTargets.Contains(candidate))
					continue;
				if (Vector2.DistanceSquared(candidate.ToVector2(), center.ToVector2()) > radius * radius)
					continue;
				float score = Vector2.DistanceSquared(candidate.ToWorldCoordinates(), NPC.Center);
				if (IsOreTile(tile.TileType))
					score *= 0.35f;
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
		gatherPause = 0;
		jobTargetItem = -1;
		gatherForestAction = ForestAction.None;
		areaEmptyTimer = 0;
		hasJobTarget = false;
		jobOrigin = Vector2.Zero;
		failedMiningTargets.Clear();
		brainState = BrainState.Follow;
		stateTimer = 1;
		NPC.netUpdate = true;
	}

}
