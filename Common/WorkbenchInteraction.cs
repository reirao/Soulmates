#nullable enable
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulmates.Common;

public sealed class WorkbenchInteraction : GlobalTile
{
	public override void RightClick(int i, int j, int type)
	{
		if (type != TileID.WorkBenches || Main.dedServ || Main.gameMenu || Main.LocalPlayer.dead)
			return;

		Main.playerInventory = true;
		Main.recBigList = false;
		Main.LocalPlayer.tileInteractionHappened = true;
		Recipe.FindRecipes();
		SoundEngine.PlaySound(SoundID.MenuOpen with { Volume = 0.5f });
	}
}
