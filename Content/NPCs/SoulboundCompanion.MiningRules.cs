using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Common.Feedback;
using Terraria;
using Terraria.ID;
using Terraria.ObjectData;

namespace Soulmates.Content.NPCs;

public sealed partial class SoulboundCompanion
{
	private static readonly Point[] MiningNeighbours = [new(0, -1), new(-1, 0), new(1, 0), new(0, 1)];

	private static bool HasMiningDecorationNeighbour(int x, int y)
	{
		// Conservatively preserve floor, wall and ceiling supports, including modded decorations.
		foreach (Point offset in MiningNeighbours) {
			Tile neighbour = Main.tile[x + offset.X, y + offset.Y];
			if (neighbour.HasTile && (Main.tileFrameImportant[neighbour.TileType]
				|| Main.tileCut[neighbour.TileType] || Main.tileAxe[neighbour.TileType]
				|| IsTreeTrunk(neighbour.TileType) || TileObjectData.GetTileData(neighbour) is not null)) return true;
		}
		return false;
	}

	private bool CanAutomaticallyMineTile(int x, int y) => WorldGen.InWorld(x, y, 10)
		&& Profile.AllowsAutomaticMining(Main.tile[x, y].TileType)
		&& IsAllowedByMiningApproach(new Point(x, y), Main.tile[x, y].TileType)
		&& CanMineTile(x, y, includeLearnedMaterials: true);

	public bool SetAutomaticMiningRule(bool ores, int tileType, bool enabled)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || !TryGetOwner(out Player owner) || owner.dead) return false;
		List<int> types = CompanionMiningRules.Types(Profile, ores);
		if (tileType != -1 && !types.Contains(tileType) || types.Count == 0) return false;
		IEnumerable<int> selected = tileType == -1 ? types : [tileType];
		var rules = new HashSet<string>(ores ? Profile.BlockedAutoMiningTiles : Profile.AllowedAutoMiningMaterials);
		foreach (int type in selected) {
			string key = CompanionMiningRules.Key(type);
			if (ores == enabled) rules.Remove(key);
			else rules.Add(key);
		}
		if (rules.Count > CompanionMiningRules.MaximumRules) return false;
		if (ores) Profile.BlockedAutoMiningTiles = rules.OrderBy(key => key).ToList();
		else Profile.AllowedAutoMiningMaterials = rules.OrderBy(key => key).ToList();
		if (autonomyActivity == AutonomyActivity.AssistMining
			&& !CanAutomaticallyMineTile(autonomyTargetTile.X, autonomyTargetTile.Y)) CancelAutonomousActivity(0);
		if (pendingAutonomyActivity == AutonomyActivity.AssistMining
			&& !CanAutomaticallyMineTile(pendingTargetTile.X, pendingTargetTile.Y)) ClearPendingInitiative();
		autonomyDecisionTimer = 0;
		SyncPackState();
		SoulmatesFeedbackSystem.Record("automatic_mining_rule", ("ores", ores), ("tile_type", tileType), ("enabled", enabled));
		return true;
	}
}
