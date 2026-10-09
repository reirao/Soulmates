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
	private int serverMiningObservationCooldown;
	private Point observedMiningTarget;
	private int observedMiningTool;
	private int observedMiningType = -1;
	private Guid observedMiningProfile;
	private int miningObservationCooldown;
	private int feedbackActivityCooldown;
	private bool rightMouseDown;
	private bool soulmatesCapturedInput;
	private bool queuedSelfSoulwheel;
	private Vector2 queuedSoulwheelUiPosition;
	private Guid queuedSoulwheelProfile;
	private byte queuedSoulwheelKind;
	private SoulwheelMouseMode queuedSoulwheelMode;
	private int queuedChest;
	private int queuedTalkNpc;
	private SoulwheelTarget? queuedSoulwheelTarget;
	private Guid feedbackProfileId;
	private readonly Dictionary<int, int> feedbackPackCounts = [];
	private readonly Dictionary<int, int> feedbackCurrentCounts = [];
	private readonly HashSet<int> feedbackObservedTypes = [];

	public int ActiveCompanionWhoAmI { get; set; } = -1;

	public override void Initialize()
	{
		ActiveCompanionWhoAmI = -1;
		rightMouseDown = false;
		soulmatesCapturedInput = false;
		queuedSelfSoulwheel = false;
		feedbackProfileId = Guid.Empty;
		feedbackPackCounts.Clear();
		observedMiningType = -1;
		observedMiningProfile = Guid.Empty;
		miningObservationCooldown = 0;
		serverMiningObservationCooldown = 0;
	}

	public override void UpdateDead()
	{
		ActiveCompanionWhoAmI = -1;
		rightMouseDown = false;
		soulmatesCapturedInput = false;
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
		if (serverMiningObservationCooldown > 0)
			serverMiningObservationCooldown--;
		if (miningObservationCooldown > 0) miningObservationCooldown--;
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

		Dictionary<int, int> current = feedbackCurrentCounts;
		current.Clear();
		foreach (Item item in companion.Profile.CarriedItems) {
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

		HashSet<int> allTypes = feedbackObservedTypes;
		allTypes.Clear();
		allTypes.UnionWith(feedbackPackCounts.Keys);
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

	internal bool TryAcceptMiningObservation()
	{
		if (serverMiningObservationCooldown > 0) return false;
		serverMiningObservationCooldown = 10;
		return true;
	}

	internal void ObserveMiningTarget(Point target, int toolType)
	{
		if (!WorldGen.InWorld(target.X, target.Y, 10) || !Main.tile[target.X, target.Y].HasTile
			|| SoulboundCompanion.FindFor(Player) is not { } companion) return;
		int type = Main.tile[target.X, target.Y].TileType;
		if (miningObservationCooldown > 0 && observedMiningProfile == companion.Profile.Id
			&& observedMiningType == type && observedMiningTarget == target && observedMiningTool == toolType) return;
		if (Main.netMode == NetmodeID.MultiplayerClient) {
			if (Player.whoAmI != Main.myPlayer) return;
			Soulmates.SendMiningObservation(companion.Profile.Id, target, toolType);
		}
		else if (!companion.ObserveMiningTarget(target, toolType)) return;
		observedMiningType = type;
		observedMiningProfile = companion.Profile.Id;
		miningObservationCooldown = 10;
		observedMiningTarget = target;
		observedMiningTool = toolType;
	}

	public override bool OnPickup(Item item)
	{
		if (Player.whoAmI != Main.myPlayer || item.type is ItemID.Heart or ItemID.Star)
			return true;
		SoulmatesFeedbackSystem.Record("player_pickup", ("item_type", item.type), ("amount", item.stack));
		if (pickupObservationCooldown > 0)
			return true;
		pickupObservationCooldown = 20;
		if (SoulboundCompanion.FindFor(Player) is { } companion) {
			if (Main.netMode == NetmodeID.MultiplayerClient)
				Soulmates.SendBehaviorObservation(companion.Profile.Id, LearnedBehavior.Gathering);
			else
				companion.ObserveOwnerActivity(LearnedBehavior.Gathering);
		}
		return true;
	}

	public override void SetControls()
	{
		if (Main.dedServ || Player.whoAmI != Main.myPlayer)
			return;
		if (!Main.mouseLeft && !Main.mouseRight)
			soulmatesCapturedInput = false;
		if (SoulmatesUIInput.IsCaptured)
			soulmatesCapturedInput = true;
		if (Main.mouseRight && CanCaptureModeClick())
			soulmatesCapturedInput = true;
		// A menu-closing click belongs to the menu until the mouse is released.
		if (soulmatesCapturedInput) {
			Player.controlUseItem = false;
			Player.controlUseTile = false;
		}
	}

	private bool CanCaptureModeClick() => !Main.gameMenu && !Player.dead && !Main.playerInventory
		&& !Player.mouseInterface && Player.talkNPC < 0 && !SoulmatesUIInput.IsTyping && Main.mouseItem.IsAir
		&& ModContent.GetInstance<CompanionWheelSystem>().MouseMode != SoulwheelMouseMode.Terraria
		&& SoulboundCompanion.FindFor(Player) is not null;

	public override bool CanUseItem(Item item) => Main.dedServ || Player.whoAmI != Main.myPlayer
		|| !soulmatesCapturedInput && !SoulmatesUIInput.IsCaptured;

	public override void ProcessTriggers(TriggersSet triggersSet)
	{
		if (Player.whoAmI != Main.myPlayer)
			return;
		bool rightPressed = Main.mouseRight && !rightMouseDown;
		rightMouseDown = Main.mouseRight;
		if (Main.gameMenu || Player.dead || SoulmatesUIInput.IsTyping) {
			queuedSelfSoulwheel = false;
			return;
		}
		SoulboundCompanion? companion = SoulboundCompanion.FindFor(Player);
		CompanionWheelSystem companionWheel = ModContent.GetInstance<CompanionWheelSystem>();
		InitiativePromptSystem initiativePrompt = ModContent.GetInstance<InitiativePromptSystem>();
		TalkModeSystem talkMode = ModContent.GetInstance<TalkModeSystem>();
		FeedbackMailboxSystem mailbox = ModContent.GetInstance<FeedbackMailboxSystem>();
		// The character shortcut must also work while its own wheel captures mouse input.
		if (Soulmates.TalkKeybind.JustPressed && !initiativePrompt.IsOpen && !mailbox.IsOpen
			&& !ModContent.GetInstance<SoulCreatorSystem>().IsOpen && Main.mouseItem.IsAir && Player.talkNPC < 0) {
			queuedSelfSoulwheel = false;
			if (talkMode.IsOpen) talkMode.Close();
			else if (companion?.FindBoundSigil() is { } sigil) talkMode.Open(sigil, companion);
			return;
		}
		if (SoulmatesUIInput.IsCaptured) {
			queuedSelfSoulwheel = false;
			return;
		}
		if (Soulmates.EmoteKeybind.JustPressed && companion is not null && !Main.playerInventory
			&& !talkMode.IsOpen && !companionWheel.IsOpen && !initiativePrompt.IsOpen && !mailbox.IsOpen) {
			companionWheel.OpenEmotes(companion);
			SoulmatesFeedbackSystem.Record("player_emote_wheel_opened", ("input", "keybind"));
		}
		if (companion is not null && rightPressed)
			QueueSelfSoulwheel(companion, companionWheel, initiativePrompt, talkMode);

	}

	private void QueueSelfSoulwheel(SoulboundCompanion companion, CompanionWheelSystem companionWheel,
		InitiativePromptSystem initiativePrompt, TalkModeSystem talkMode)
	{
		queuedSelfSoulwheel = false;
		if (Main.playerInventory || Player.mouseInterface || Player.talkNPC >= 0 || !Main.mouseItem.IsAir || SoulmatesUIInput.IsCaptured
			|| talkMode.IsOpen || companionWheel.IsOpen || initiativePrompt.IsOpen)
			return;

		Point mouseWorld = SoulmatesUISpace.WorldMouse.ToPoint();
		if (companionWheel.MouseMode == SoulwheelMouseMode.Terraria
			&& SoulwheelTarget.HasNativeInteractionPriority(mouseWorld.ToVector2().ToTileCoordinates())) return;
		Rectangle selfInteractionBounds = Player.Hitbox;
		selfInteractionBounds.Inflate(14, 8);
		bool onCompanion = companion.NPC.Hitbox.Contains(mouseWorld)
			&& (!selfInteractionBounds.Contains(mouseWorld) || CompanionIsCloser(companion, mouseWorld));
		bool onPlayer = !onCompanion && selfInteractionBounds.Contains(mouseWorld);
		var snapshot = !onCompanion && !onPlayer ? new SoulwheelTarget(mouseWorld.ToVector2(), companion.NPC.whoAmI) : null;
		queuedSoulwheelMode = companionWheel.MouseMode;
		if (queuedSoulwheelMode == SoulwheelMouseMode.Terraria
			&& (Player.altFunctionUse == 2 || !onCompanion && !onPlayer && snapshot?.CanOfferFallback != true)) return;

		queuedSoulwheelUiPosition = SoulmatesUISpace.Mouse;
		queuedSoulwheelKind = onCompanion ? (byte)1 : onPlayer ? (byte)0 : (byte)2;
		queuedSoulwheelTarget = snapshot;
		queuedSoulwheelProfile = companion.Profile.Id;
		queuedChest = Player.chest;
		queuedTalkNpc = Player.talkNPC;
		queuedSelfSoulwheel = true;
	}

	private void OpenQueuedSelfSoulwheel(SoulboundCompanion? companion)
	{
		if (!queuedSelfSoulwheel)
			return;
		queuedSelfSoulwheel = false;
		if (companion is null || companion.Profile.Id != queuedSoulwheelProfile)
			return;

		CompanionWheelSystem companionWheel = ModContent.GetInstance<CompanionWheelSystem>();
		InitiativePromptSystem initiativePrompt = ModContent.GetInstance<InitiativePromptSystem>();
		TalkModeSystem talkMode = ModContent.GetInstance<TalkModeSystem>();
		FeedbackMailboxSystem mailbox = ModContent.GetInstance<FeedbackMailboxSystem>();
		if (Main.playerInventory || Player.mouseInterface || SoulmatesUIInput.IsTyping || SoulmatesUIInput.IsCaptured
			|| !Main.mouseItem.IsAir || Player.chest != queuedChest || Player.talkNPC != queuedTalkNpc
			|| queuedSoulwheelMode == SoulwheelMouseMode.Terraria && (Player.altFunctionUse == 2 || Player.itemAnimation > 0)
			|| talkMode.IsOpen || companionWheel.IsOpen
			|| initiativePrompt.IsOpen || mailbox.IsOpen)
			return;

		if (companionWheel.MouseMode != queuedSoulwheelMode) return;
		if (queuedSoulwheelKind == 1) companionWheel.Open(companion);
		else if (queuedSoulwheelKind == 0) companionWheel.OpenPlayer(companion);
		else if (queuedSoulwheelTarget is { } snapshot) companionWheel.OpenContext(companion, snapshot, queuedSoulwheelUiPosition);
		SoulmatesFeedbackSystem.Record("context_wheel_opened", ("mode", queuedSoulwheelMode.ToString()),
			("input", "right_click"));
		soulmatesCapturedInput = true;
		Player.mouseInterface = true;
		Main.blockMouse = true;
		Main.mouseRightRelease = false;
	}

	private bool CompanionIsCloser(SoulboundCompanion companion, Point mouseWorld)
	{
		if (!companion.NPC.Hitbox.Contains(mouseWorld))
			return false;
		Vector2 mouse = mouseWorld.ToVector2();
		return Vector2.DistanceSquared(mouse, companion.NPC.Center)
			< Vector2.DistanceSquared(mouse, Player.Center);
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
