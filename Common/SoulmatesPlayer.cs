using Microsoft.Xna.Framework;
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
		TalkModeSystem talkMode = ModContent.GetInstance<TalkModeSystem>();
		if (Soulmates.EmoteKeybind.JustReleased)
			emoteWheel.Release();
		if (Soulmates.EmoteKeybind.JustPressed && companion is not null && !Main.playerInventory
			&& !talkMode.IsOpen)
			emoteWheel.Open(companion);
		if (companion is not null)
			TryOpenSelfEmoteWheel(companion, emoteWheel, talkMode);

		if (!Soulmates.TalkKeybind.JustPressed || companion is null)
			return;
		if (companion.FindBoundSigil() is not { } sigil)
			return;
		talkMode.Open(sigil, companion);
	}

	private void TryOpenSelfEmoteWheel(SoulboundCompanion companion, EmoteWheelSystem emoteWheel,
		TalkModeSystem talkMode)
	{
		if (!Main.mouseRight || !Main.mouseRightRelease || Main.playerInventory || Player.mouseInterface
			|| talkMode.IsOpen || emoteWheel.IsOpen)
			return;

		Point mouseWorld = Main.MouseWorld.ToPoint();
		if (companion.NPC.Hitbox.Contains(mouseWorld))
			return;

		Rectangle selfInteractionBounds = Player.Hitbox;
		selfInteractionBounds.Inflate(14, 8);
		if (!selfInteractionBounds.Contains(mouseWorld))
			return;

		emoteWheel.Open(companion, releaseOnRightMouseUp: true);
		Player.mouseInterface = true;
		Main.blockMouse = true;
		Main.mouseRightRelease = false;
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
