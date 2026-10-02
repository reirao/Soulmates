#nullable enable
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace Soulmates.Content.NPCs;

public sealed partial class SoulboundCompanion
{
	private bool HasPruningAxe() => Owner.inventory.Concat(Profile.CarriedItems).Any(item => !item.IsAir && item.axe > 0);

	private static bool IsBareTreeBranch(Point target)
	{
		if (!WorldGen.InWorld(target.X, target.Y, 10)) return false;
		Tile branch = Main.tile[target.X, target.Y];
		if (!branch.HasTile || branch.TileType != TileID.Trees) return false;
		// These native frames are side twigs, not the visually similar central stem frames.
		int stemOffset = branch.TileFrameX == 66 && branch.TileFrameY is 0 or 22 or 44 ? 1
			: branch.TileFrameX == 88 && branch.TileFrameY is 66 or 88 or 110 ? -1 : 0;
		if (stemOffset == 0) return false;
		int stem = target.X + stemOffset;
		for (int y = target.Y - 1; y <= target.Y + 1; y++) {
			Tile tile = Main.tile[stem, y];
			if (!tile.HasTile || tile.TileType != TileID.Trees) return false;
		}
		return !IsTreeAt(target.X, target.Y - 1) && !IsTreeAt(target.X, target.Y + 1);
	}

	private static bool IsTreeAt(int x, int y) => Main.tile[x, y].HasTile && Main.tile[x, y].TileType == TileID.Trees;

	private bool PruneBranch(Point target)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || !HasPruningAxe() || !IsBareTreeBranch(target)) return false;
		WorldGen.KillTile(target.X, target.Y);
		if (Main.tile[target.X, target.Y].HasTile) return false;
		if (Main.netMode == NetmodeID.Server)
			NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 0, target.X, target.Y);
		return true;
	}
}
