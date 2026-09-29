#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

public sealed partial class SoulboundCompanion
{
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

		if (FindForestTask(Owner.Center, out Point forestTarget, out ForestAction forestAction)) {
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
			SyncPackState();
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendData(MessageID.SyncItem, -1, -1, null, collectedItemIndex);
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

		bool success = PerformForestAction(autonomyTargetTile, autonomyForestAction);
		tendedForestTargets.Add(autonomyTargetTile);
		if (success) {
			autonomyWorkCount++;
			Profile.Energy = Math.Max(0, Profile.Energy - 1);
			Profile.GainExperience(2, out _);
			ShowNativeEmote(EmoteID.MiscTree, 75);
			SyncPackState();
		}

		int sweepLimit = Profile.ForesterUnlocked ? AutonomousForestSweepLimit : 1;
		if (Profile.Energy >= 16 && autonomyWorkCount < sweepLimit
			&& TryRetargetAutonomousForestry())
			return true;
		FinishAutonomousForestry();
		return false;
	}

	private bool TryRetargetAutonomousForestry()
	{
		if (!FindForestTask(Owner.Center, out Point nextTarget, out ForestAction nextAction))
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

	private bool PerformForestAction(Point target, ForestAction action) => action switch {
		ForestAction.ShakeTree => ShakeTree(target),
		ForestAction.ClearDeadwood => ClearDeadwood(target),
		ForestAction.PlantAcorn => PlantAcorn(target),
		_ => false
	};

	private bool ShakeTree(Point target)
	{
		if (!WorldGen.InWorld(target.X, target.Y, 10))
			return false;
		Tile ground = Main.tile[target.X, target.Y];
		if (!ground.HasTile || WorldGen.GetTreeType(ground.TileType) == Terraria.Enums.TreeTypes.None)
			return false;
		if (ShakeTreeMethod is null)
			return false;
		int previousShakes = TreeShakeCountField?.GetValue(null) is int count ? count : -1;
		try {
			ShakeTreeMethod.Invoke(null, [target.X, target.Y]);
		}
		catch (Exception exception) when (exception is TargetInvocationException or MethodAccessException or ArgumentException) {
			return false;
		}
		if (previousShakes >= 0 && TreeShakeCountField?.GetValue(null) is int currentShakes
			&& currentShakes <= previousShakes)
			return false;
		return true;
	}

	private bool ClearDeadwood(Point target)
	{
		if (!WorldGen.InWorld(target.X, target.Y, 10)
			|| !Main.tile[target.X, target.Y].HasTile
			|| Main.tile[target.X, target.Y].TileType != TileID.FallenLog)
			return false;

		WorldGen.KillTile(target.X, target.Y);
		bool success = !Main.tile[target.X, target.Y].HasTile;
		if (success) {
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 0,
					target.X, target.Y);
		}
		return success;
	}

	private bool PlantAcorn(Point target)
	{
		if (!HasPackItem(ItemID.Acorn) || !CanPlantAcornAt(target.X, target.Y))
			return false;
		bool placed = WorldGen.PlaceTile(target.X, target.Y, TileID.Saplings,
			mute: true, forced: false, plr: Owner.whoAmI, style: 0);
		if (!placed || !Main.tile[target.X, target.Y].HasTile)
			return false;

		ConsumePackItem(ItemID.Acorn);
		WorldGen.SquareTileFrame(target.X, target.Y);
		if (Main.netMode == NetmodeID.Server)
			NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 1,
				target.X, target.Y, TileID.Saplings);
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

	private bool FindForestTask(Vector2 searchCenter, out Point result, out ForestAction action)
	{
		result = Point.Zero;
		action = ForestAction.None;
		Point center = searchCenter.ToTileCoordinates();
		float bestScore = float.MaxValue;
		bool canPlant = Profile.ForesterUnlocked && HasPackItem(ItemID.Acorn);
		int radius = Profile.ForesterUnlocked ? 28 : 22;
		for (int x = center.X - radius; x <= center.X + radius; x++) {
			for (int y = center.Y - radius; y <= center.Y + radius; y++) {
				if (!WorldGen.InWorld(x, y, 10))
					continue;
				Tile tile = Main.tile[x, y];
				Point candidate;
				ForestAction candidateAction;
				float priority;
				if (tile.HasTile && tile.TileType == TileID.FallenLog) {
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

		for (int scanY = y - 4; scanY <= y; scanY++) {
			if (!WorldGen.InWorld(x, scanY, 10) || Main.tile[x, scanY].HasTile)
				return false;
		}
		for (int scanX = x - 3; scanX <= x + 3; scanX++) {
			for (int scanY = y - 6; scanY <= y + 1; scanY++) {
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

	private static bool IsRecoveryPickup(Item item) => item.type is ItemID.Heart or ItemID.Star;
}
