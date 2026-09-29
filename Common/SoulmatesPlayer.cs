#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Soulmates.Common.Feedback;
using Soulmates.Common.UI;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Soulmates.Common;

public sealed class SoulmatesPlayer : ModPlayer
{
	private bool starterKitClaimed;
	private int pickupObservationCooldown;
	private int serverGatheringObservationCooldown;
	private int feedbackActivityCooldown;
	private bool rightMouseDown;
	private bool queuedSelfSoulwheel;
	private Point queuedSelfSoulwheelPosition;
	private Guid feedbackProfileId;
	private readonly Dictionary<int, int> feedbackPackCounts = [];

	public int ActiveCompanionWhoAmI { get; set; } = -1;

	public override void Initialize()
	{
		ActiveCompanionWhoAmI = -1;
		rightMouseDown = false;
		queuedSelfSoulwheel = false;
		feedbackProfileId = Guid.Empty;
		feedbackPackCounts.Clear();
	}

	public override void UpdateDead()
	{
		ActiveCompanionWhoAmI = -1;
		rightMouseDown = false;
		queuedSelfSoulwheel = false;
		feedbackProfileId = Guid.Empty;
		feedbackPackCounts.Clear();
	}

	public override void PostUpdate()
	{
		if (pickupObservationCooldown > 0)
			pickupObservationCooldown--;
		if (serverGatheringObservationCooldown > 0)
			serverGatheringObservationCooldown--;
		if (Player.whoAmI == Main.myPlayer) {
			SoulboundCompanion? companion = SoulboundCompanion.FindFor(Player);
			OpenQueuedSelfSoulwheel(companion);
			SoulmatesFeedbackSystem.Tick(Player, companion);
			TrackFeedbackPack(companion);
			TrackFeedbackActivity(companion);
		}
	}

	private void TrackFeedbackPack(SoulboundCompanion? companion)
	{
		if (!SoulmatesFeedbackSystem.SessionActive || companion is null) {
			feedbackProfileId = Guid.Empty;
			feedbackPackCounts.Clear();
			return;
		}

		var current = new Dictionary<int, int>();
		foreach (Item item in companion.Profile.Pack) {
			if (item.IsAir || item.stack <= 0)
				continue;
			current.TryGetValue(item.type, out int amount);
			current[item.type] = amount + item.stack;
		}
		if (feedbackProfileId != companion.Profile.Id) {
			feedbackProfileId = companion.Profile.Id;
			feedbackPackCounts.Clear();
			foreach ((int type, int amount) in current)
				feedbackPackCounts[type] = amount;
			SoulmatesFeedbackSystem.Record("pack_observed_initial", ("pack_load", companion.Profile.PackLoad),
				("distinct_types", current.Count));
			return;
		}

		var allTypes = new HashSet<int>(feedbackPackCounts.Keys);
		allTypes.UnionWith(current.Keys);
		foreach (int itemType in allTypes) {
			feedbackPackCounts.TryGetValue(itemType, out int before);
			current.TryGetValue(itemType, out int after);
			if (before == after)
				continue;
			SoulmatesFeedbackSystem.Record("pack_observed_delta", ("item_type", itemType),
				("delta", after - before), ("type_total_after", after), ("pack_load", companion.Profile.PackLoad));
		}
		feedbackPackCounts.Clear();
		foreach ((int type, int amount) in current)
			feedbackPackCounts[type] = amount;
	}

	private void TrackFeedbackActivity(SoulboundCompanion? companion)
	{
		if (!SoulmatesFeedbackSystem.SessionActive || companion is null || --feedbackActivityCooldown > 0)
			return;
		feedbackActivityCooldown = 60;
		LearnedBehavior? behavior = null;
		if (Player.chest >= 0) {
			behavior = LearnedBehavior.Exploration;
			feedbackActivityCooldown = 180;
		}
		else if (Player.controlUseItem || Player.itemAnimation > 0) {
			Item held = Player.HeldItem;
			if (held.axe > 0 || held.createTile == TileID.Saplings || held.type == ItemID.Acorn)
				behavior = LearnedBehavior.Forestry;
			else if (held.pick > 0)
				behavior = LearnedBehavior.Mining;
			else if (held.damage > 0)
				behavior = LearnedBehavior.Combat;
		}
		if (behavior is LearnedBehavior observed)
			SoulmatesFeedbackSystem.Record("player_activity", ("behavior", observed.ToString()));
	}

	internal bool TryAcceptGatheringObservation()
	{
		if (serverGatheringObservationCooldown > 0)
			return false;
		serverGatheringObservationCooldown = 20;
		return true;
	}

	public override bool OnPickup(Item item)
	{
		if (Player.whoAmI != Main.myPlayer || item.type is ItemID.Heart or ItemID.Star)
			return true;
		SoulmatesFeedbackSystem.Record("player_pickup", ("item_type", item.type), ("amount", item.stack));
		if (pickupObservationCooldown > 0)
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
		CompanionWheelSystem companionWheel = ModContent.GetInstance<CompanionWheelSystem>();
		InitiativePromptSystem initiativePrompt = ModContent.GetInstance<InitiativePromptSystem>();
		TalkModeSystem talkMode = ModContent.GetInstance<TalkModeSystem>();
		FeedbackMailboxSystem mailbox = ModContent.GetInstance<FeedbackMailboxSystem>();
		bool rightPressed = Main.mouseRight && !rightMouseDown;
		rightMouseDown = Main.mouseRight;
		if (Soulmates.EmoteKeybind.JustPressed && companion is not null && !Main.playerInventory
			&& !talkMode.IsOpen && !companionWheel.IsOpen && !initiativePrompt.IsOpen && !mailbox.IsOpen)
			companionWheel.OpenEmotes(companion);
		if (companion is not null && rightPressed)
			QueueSelfSoulwheel(companion, companionWheel, initiativePrompt, talkMode);

		if (!Soulmates.TalkKeybind.JustPressed || companion is null || companionWheel.IsOpen
			|| initiativePrompt.IsOpen || mailbox.IsOpen)
			return;
		if (companion.FindBoundSigil() is not { } sigil)
			return;
		talkMode.Open(sigil, companion);
	}

	private void QueueSelfSoulwheel(SoulboundCompanion companion, CompanionWheelSystem companionWheel,
		InitiativePromptSystem initiativePrompt, TalkModeSystem talkMode)
	{
		queuedSelfSoulwheel = false;
		if (Main.playerInventory || Player.mouseInterface
			|| talkMode.IsOpen || companionWheel.IsOpen || initiativePrompt.IsOpen
			|| !Main.HoverItem.IsAir || Player.cursorItemIconEnabled
			|| ItemLoader.AltFunctionUse(Player.HeldItem, Player))
			return;

		Point mouseWorld = Main.MouseWorld.ToPoint();
		if (companion.NPC.Hitbox.Contains(mouseWorld))
			return;

		Rectangle selfInteractionBounds = Player.Hitbox;
		selfInteractionBounds.Inflate(14, 8);
		if (!selfInteractionBounds.Contains(mouseWorld) || HasWorldInteractionAt(mouseWorld))
			return;

		queuedSelfSoulwheelPosition = mouseWorld;
		queuedSelfSoulwheel = true;
	}

	private void OpenQueuedSelfSoulwheel(SoulboundCompanion? companion)
	{
		if (!queuedSelfSoulwheel)
			return;
		queuedSelfSoulwheel = false;
		if (companion is null)
			return;

		CompanionWheelSystem companionWheel = ModContent.GetInstance<CompanionWheelSystem>();
		InitiativePromptSystem initiativePrompt = ModContent.GetInstance<InitiativePromptSystem>();
		TalkModeSystem talkMode = ModContent.GetInstance<TalkModeSystem>();
		FeedbackMailboxSystem mailbox = ModContent.GetInstance<FeedbackMailboxSystem>();
		if (Main.playerInventory || Player.mouseInterface || talkMode.IsOpen || companionWheel.IsOpen
			|| initiativePrompt.IsOpen || mailbox.IsOpen || Player.tileInteractionHappened
			|| Main.HasInteractibleObjectThatIsNotATile
			|| Player.cursorItemIconEnabled || !Main.HoverItem.IsAir)
			return;

		Point mouseWorld = queuedSelfSoulwheelPosition;
		if (companion.NPC.Hitbox.Contains(mouseWorld))
			return;
		Rectangle selfInteractionBounds = Player.Hitbox;
		selfInteractionBounds.Inflate(14, 8);
		if (!selfInteractionBounds.Contains(mouseWorld) || HasWorldInteractionAt(mouseWorld))
			return;

		companionWheel.OpenEmotes(companion);
		Player.mouseInterface = true;
		Main.blockMouse = true;
		Main.mouseRightRelease = false;
	}

	private static bool HasWorldInteractionAt(Point mouseWorld)
	{
		Point tilePosition = mouseWorld.ToVector2().ToTileCoordinates();
		if (!WorldGen.InWorld(tilePosition.X, tilePosition.Y, 1))
			return true;
		Tile tile = Main.tile[tilePosition.X, tilePosition.Y];
		// Never steal a right-click from a tile. The player wheel is intentionally
		// limited to the clear air around the character's body.
		if (tile.HasTile)
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
		if (Player.whoAmI == Main.myPlayer)
			SoulmatesFeedbackSystem.BeginSession(Player);
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
