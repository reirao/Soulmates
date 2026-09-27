using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Soulmates.Common.UI;
using Terraria;
using Terraria.GameInput;
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

	public override void ProcessTriggers(TriggersSet triggersSet)
	{
		if (!Soulmates.TalkKeybind.JustPressed || Player.whoAmI != Main.myPlayer)
			return;
		if (SoulboundCompanion.FindFor(Player) is not { } companion)
			return;
		if (companion.FindBoundSigil() is not { } sigil)
			return;
		ModContent.GetInstance<TalkModeSystem>().Open(sigil, companion);
	}

	public override void OnEnterWorld()
	{
		if (starterKitClaimed || Player.whoAmI != Main.myPlayer)
			return;

		Player.QuickSpawnItem(Player.GetSource_Misc("SoulmatesStarterKit"), ModContent.ItemType<Soulcore>());
		Player.QuickSpawnItem(Player.GetSource_Misc("SoulmatesStarterKit"), ModContent.ItemType<BlankSigil>(), 3);
		starterKitClaimed = true;
		Main.NewText(SoulmatesText.Get("Messages.StarterKit"), 255, 235, 145);
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
