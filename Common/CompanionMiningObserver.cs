using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace Soulmates.Common;

public sealed class CompanionMiningObserver : ModSystem
{
	public override void Load() => On_Player.PickTile += ObservePick;
	public override void Unload() => On_Player.PickTile -= ObservePick;

	private static void ObservePick(On_Player.orig_PickTile original, Player player, int x, int y, int pickPower)
	{
		// Capture before native mining can remove a one-hit block or send its removal to the server.
		if (player.HeldItem.pick > 0 && player.HeldItem.pick == pickPower)
			player.GetModPlayer<SoulmatesPlayer>().ObserveMiningTarget(new Point(x, y), player.HeldItem.type);
		original(player, x, y, pickPower);
	}
}
