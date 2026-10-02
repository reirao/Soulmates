#nullable enable
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulmates.Common.UI;

public sealed partial class CompanionWheelSystem
{
	private enum ContextAction : byte { Point, Look, Gather, Mine, Forest, Emotes, Tools }
	private SoulwheelTarget? contextTarget;
	private readonly List<ContextAction> contextActions = [];
	public SoulwheelMouseMode MouseMode { get; private set; }

	public void ExitMouseMode()
	{
		MouseMode = SoulwheelMouseMode.Terraria;
		Close();
		ModContent.GetInstance<DirectOrderSystem>().Cancel();
	}

	public void SelectMouseMode(SoulwheelMouseMode mode)
	{
		if (mode is < SoulwheelMouseMode.Terraria or > SoulwheelMouseMode.Companion) return;
		SoulboundCompanion? target = companion ?? SoulboundCompanion.FindFor(Main.LocalPlayer);
		if (mode == SoulwheelMouseMode.Terraria || target?.NPC.active != true) {
			ExitMouseMode();
			return;
		}
		SoulwheelTarget? savedTarget = contextTarget;
		Vector2 savedCenter = center;
		MouseMode = mode;
		if (savedTarget is not null) OpenContext(target, savedTarget, savedCenter);
		else if (mode == SoulwheelMouseMode.Player) OpenPlayer(target);
		else Open(target, WheelContext.Companion, null);
	}

	private void CycleMouseMode()
	{
		SoulwheelMouseMode visibleMode = context == WheelContext.Player ? SoulwheelMouseMode.Player
			: context == WheelContext.Companion ? SoulwheelMouseMode.Companion : MouseMode;
		SelectMouseMode(visibleMode == SoulwheelMouseMode.Player ? SoulwheelMouseMode.Companion
			: visibleMode == SoulwheelMouseMode.Companion ? SoulwheelMouseMode.Terraria : SoulwheelMouseMode.Player);
	}

	public void OpenContext(SoulboundCompanion target, Vector2 worldMouse, Vector2 uiMouse)
		=> OpenContext(target, new SoulwheelTarget(worldMouse, target.NPC.whoAmI), uiMouse);

	internal void OpenContext(SoulboundCompanion target, SoulwheelTarget snapshot, Vector2 uiMouse)
	{
		if (MouseMode == SoulwheelMouseMode.Terraria) return;
		Open(target, WheelContext.Contextual, null);
		if (!open) return;
		center = ClampCenter(uiMouse);
		contextTarget = snapshot;
		if (MouseMode == SoulwheelMouseMode.Player) {
			if (snapshot.CanPoint) contextActions.Add(ContextAction.Point);
			contextActions.Add(ContextAction.Emotes);
		}
		else {
			foreach (CompanionTargetOrder order in PointModes)
				if (snapshot.CanOrder(target, order)) contextActions.Add(order switch {
					CompanionTargetOrder.Look => ContextAction.Look,
					CompanionTargetOrder.Gather => ContextAction.Gather,
					CompanionTargetOrder.Mine => ContextAction.Mine,
					_ => ContextAction.Forest
				});
			if (snapshot.CanPoint && contextActions.Count == 0) contextActions.Add(ContextAction.Point);
			contextActions.Add(ContextAction.Tools);
		}
	}

	private void ActivateContextAction(int index)
	{
		if (index < 0 || index >= contextActions.Count || companion?.NPC.active != true) return;
		SoulboundCompanion target = companion;
		ContextAction action = contextActions[index];
		SoulwheelTarget? snapshot = contextTarget;
		if (action == ContextAction.Emotes) { OpenEmotes(target); return; }
		if (action == ContextAction.Tools) { OpenWorld(target); return; }
		if (snapshot is null || !snapshot.IsCurrent) {
			Close();
			target.ShowSpeech(SoulmatesText.Get("UI.CompanionWheel.MouseModes.TargetChanged"));
			return;
		}
		if (action == ContextAction.Point) {
			if (!snapshot.CanPoint) return;
			int emote = snapshot.Emote;
			Close();
			EmoteBubble.MakeLocalPlayerEmote(emote);
			// Native emote observers handle NPC reactions; tile/drop inspection uses the normal order path.
			if (snapshot.CanOrder(target, CompanionTargetOrder.Look)) ExecuteContextOrder(target, snapshot, CompanionTargetOrder.Look);
			return;
		}
		CompanionTargetOrder order = action switch {
			ContextAction.Look => CompanionTargetOrder.Look,
			ContextAction.Gather => CompanionTargetOrder.Gather,
			ContextAction.Mine => CompanionTargetOrder.Mine,
			_ => CompanionTargetOrder.Forest
		};
		Close();
		if (snapshot.CanOrder(target, order)) ExecuteContextOrder(target, snapshot, order);
		else target.ShowSpeech(SoulmatesText.Get($"UI.DirectOrder.Invalid.{order}"));
	}

	private static void ExecuteContextOrder(SoulboundCompanion target, SoulwheelTarget snapshot, CompanionTargetOrder order)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient)
			global::Soulmates.Soulmates.SendDirectOrderRequest(order, snapshot.Tile, snapshot.ItemSlot);
		else {
			var result = target.PerformDirectOrder(order, snapshot.Tile, snapshot.ItemSlot);
			target.ShowSpeech(result.Reply);
			SoundEngine.PlaySound(result.Accepted ? SoundID.Chat : SoundID.MenuClose);
		}
	}

	private Vector2 ContextActionPosition(int index) => center
		+ (-MathHelper.PiOver2 + MathHelper.TwoPi * index / contextActions.Count).ToRotationVector2() * RootRadius * LayoutScale;
	private WheelIcon ContextActionIcon(ContextAction action) => action switch {
		ContextAction.Point => new(IconKind.Emote, contextTarget?.Emote ?? EmoteID.EmotionAlert),
		ContextAction.Look => new(IconKind.Item, ItemID.Binoculars),
		ContextAction.Gather => new(IconKind.Item, ItemID.TreasureMagnet),
		ContextAction.Mine => new(IconKind.Item, ItemID.CopperPickaxe),
		ContextAction.Forest => new(IconKind.Emote, EmoteID.MiscTree),
		ContextAction.Emotes => new(IconKind.Emote, EmoteID.EmoteHappiness),
		_ => new(IconKind.Emote, EmoteID.ItemCog)
	};
	private string ContextActionLabel(int index) => index < 0 || index >= contextActions.Count ? ""
		: contextActions[index] is ContextAction.Emotes or ContextAction.Tools
			? SoulmatesText.Get($"UI.CompanionWheel.MouseModes.{contextActions[index]}")
			: SoulmatesText.Get("UI.CompanionWheel.MouseModes.Target",
				SoulmatesText.Get($"UI.CompanionWheel.MouseModes.{contextActions[index]}"), contextTarget?.Name ?? "");

	private Vector2 MouseModePosition(int index) => SoulwheelLayout.ModePosition(center, index, SoulmatesUISpace.Viewport);
	private static WheelIcon MouseModeIcon(SoulwheelMouseMode mode) => mode switch {
		SoulwheelMouseMode.Player => new(IconKind.Emote, EmoteID.EmoteHappiness),
		SoulwheelMouseMode.Companion => new(IconKind.Item, ItemID.ShadowOrb),
		_ => new(IconKind.Emote, EmoteID.ItemCog)
	};
	private static string MouseModeLabel(SoulwheelMouseMode mode) => SoulmatesText.Get($"UI.CompanionWheel.MouseModes.{mode}");
	private void DrawMouseModeSelector(SpriteBatch spriteBatch, Color accent)
	{
		for (int i = 0; i < 3; i++)
			DrawNode(spriteBatch, MouseModePosition(i), 30f, accent,
				hoverLayer == HoverLayer.MouseMode && hoverIndex == i, MouseMode == (SoulwheelMouseMode)i,
				MouseModeIcon((SoulwheelMouseMode)i));
	}
	private void DrawMouseModeCursor()
	{
		if (MouseMode == SoulwheelMouseMode.Terraria || Main.gameMenu || Main.LocalPlayer.dead
			|| Main.playerInventory || Main.LocalPlayer.mouseInterface || SoulmatesUIInput.IsTyping || SoulmatesUIInput.IsCaptured) return;
		Vector2 viewport = SoulmatesUISpace.Viewport;
		Vector2 position = Vector2.Clamp(SoulmatesUISpace.Mouse + new Vector2(24f, 24f), new Vector2(15f),
			Vector2.Max(new Vector2(15f), viewport - new Vector2(15f)));
		DrawIcon(Main.spriteBatch, position, 20f, MouseModeIcon(MouseMode), Color.White);
	}
}
