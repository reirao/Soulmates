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
		if (Player.whoAmI != Main.myPlayer)
			return;
		SoulboundCompanion? companion = SoulboundCompanion.FindFor(Player);
		EmoteWheelSystem emoteWheel = ModContent.GetInstance<EmoteWheelSystem>();
		if (Soulmates.EmoteKeybind.JustReleased)
			emoteWheel.Release();
		if (Soulmates.EmoteKeybind.JustPressed && companion is not null && !Main.playerInventory
			&& !ModContent.GetInstance<TalkModeSystem>().IsOpen)
			emoteWheel.Open(companion);

		if (!Soulmates.TalkKeybind.JustPressed || companion is null)
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
