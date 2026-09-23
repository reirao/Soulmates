using Soulmates.Content.Items;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Soulmates.Common;

public sealed class SoulmatesPlayer : ModPlayer
{
	private bool starterKitClaimed;

	public int ActiveCompanionWhoAmI { get; set; } = -1;

	public override void Initialize()
	{
		ActiveCompanionWhoAmI = -1;
	}

	public override void UpdateDead()
	{
		ActiveCompanionWhoAmI = -1;
	}

	public override void OnEnterWorld()
	{
		if (starterKitClaimed || Player.whoAmI != Main.myPlayer)
			return;

		Player.QuickSpawnItem(Player.GetSource_Misc("SoulmatesStarterKit"), ModContent.ItemType<Soulcore>());
		Player.QuickSpawnItem(Player.GetSource_Misc("SoulmatesStarterKit"), ModContent.ItemType<BlankSigil>(), 3);
		starterKitClaimed = true;
		Main.NewText("A Soulcore stirs in your hands...", 255, 235, 145);
	}

	public override void SaveData(TagCompound tag)
	{
		if (starterKitClaimed)
			tag["starterKitClaimed"] = true;
	}

	public override void LoadData(TagCompound tag)
	{
		starterKitClaimed = tag.GetBool("starterKitClaimed");
	}
}
