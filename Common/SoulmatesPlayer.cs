using Microsoft.Xna.Framework;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Soulmates.Common.UI;
using Terraria;
using Terraria.GameContent.UI.States;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI;

namespace Soulmates.Common;

public sealed class SoulmatesPlayer : ModPlayer
{
	private bool starterKitClaimed;
	private int pickupObservationCooldown;

	public int ActiveCompanionWhoAmI { get; set; } = -1;

	public override void Initialize()
	{
		ActiveCompanionWhoAmI = -1;
	}

	public override void UpdateDead()
	{
		ActiveCompanionWhoAmI = -1;
	}

	public override void PostUpdate()
	{
		if (pickupObservationCooldown > 0)
			pickupObservationCooldown--;
	}

	public override bool OnPickup(Item item)
	{
		if (Player.whoAmI != Main.myPlayer || pickupObservationCooldown > 0
			|| item.type is ItemID.Heart or ItemID.Star)
			return true;
		pickupObservationCooldown = 20;
		if (Main.netMode == NetmodeID.MultiplayerClient)
			Soulmates.SendBehaviorObservation(LearnedBehavior.Gathering);
		else
			SoulboundCompanion.FindFor(Player)?.ObserveOwnerActivity(LearnedBehavior.Gathering);
		return true;
	}

	public override void ProcessTriggers(TriggersSet triggersSet)
	{
		if (Player.whoAmI != Main.myPlayer)
			return;
		SoulboundCompanion? companion = SoulboundCompanion.FindFor(Player);
		EmoteWheelSystem emoteWheel = ModContent.GetInstance<EmoteWheelSystem>();
		CompanionWheelSystem companionWheel = ModContent.GetInstance<CompanionWheelSystem>();
		TalkModeSystem talkMode = ModContent.GetInstance<TalkModeSystem>();
		if (Soulmates.EmoteKeybind.JustReleased)
			emoteWheel.Release();
		if (Soulmates.EmoteKeybind.JustPressed && companion is not null && !Main.playerInventory
			&& !talkMode.IsOpen && !companionWheel.IsOpen)
			emoteWheel.Open(companion);
		if (companion is not null)
			TryOpenSelfEmoteMenu(companion, emoteWheel, talkMode);

		if (!Soulmates.TalkKeybind.JustPressed || companion is null || companionWheel.IsOpen)
			return;
		if (companion.FindBoundSigil() is not { } sigil)
			return;
		talkMode.Open(sigil, companion);
	}

	private void TryOpenSelfEmoteMenu(SoulboundCompanion companion, EmoteWheelSystem emoteWheel,
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
		if (!selfInteractionBounds.Contains(mouseWorld) || HasWorldInteractionAt(mouseWorld))
			return;

		emoteWheel.Close();
		IngameFancyUI.OpenUIState(new UIEmotesMenu());
		Player.mouseInterface = true;
		Main.blockMouse = true;
		Main.mouseRightRelease = false;
	}

	private static bool HasWorldInteractionAt(Point mouseWorld)
	{
		Point tilePosition = Main.MouseWorld.ToTileCoordinates();
		if (!WorldGen.InWorld(tilePosition.X, tilePosition.Y, 1)
			|| Main.tile[tilePosition.X, tilePosition.Y].HasTile)
			return true;

		for (int i = 0; i < Main.maxNPCs; i++) {
			NPC npc = Main.npc[i];
			if (npc.active && npc.Hitbox.Contains(mouseWorld))
				return true;
		}
		return false;
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
