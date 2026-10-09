#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulmates.Common.UI;

public sealed partial class CompanionWheelSystem
{
	private enum WorkPage : byte { Closed, Actions, Compass }
	private WorkPage workPage;
	private bool ShowWorkActions => branch == RootBranch.Work && workPage == WorkPage.Actions
		&& !miningApproachMenu && !miningFilterMenu && !nearbyOreMenu;
	private int MiningApproachNodeCount => MiningApproaches.Length + 2;

	private void ActivateWorkConfig(int index)
	{
		if (companion?.NPC.active != true || index < 0 || index >= WorkActions.Length) return;
		if (WorkActions[index] == WheelWorkAction.MiningApproach) {
			ResetMiningFilter();
			miningApproachMenu = true;
			nearbyOreMenu = false;
			nearbyOreChoices.Clear();
			SoundEngine.PlaySound(SoundID.MenuTick);
		}
		else ExecuteWorkAction(WorkActions[index]);
	}

	private void RepeatWork()
	{
		if (companion?.Profile.LastWork is { IsValid: true, TargetOrder: { } order }) {
			var target = companion;
			Close();
			ModContent.GetInstance<DirectOrderSystem>().Begin(target, order);
		}
		else ExecuteQuickAction(CompanionQuickAction.RepeatLastWork);
	}

	private string WorkMenuLabel(int index) => index == 0 ? SoulmatesText.Get("UI.CompanionWheel.WorkMenu.Config")
		: companion?.Profile.LastWork is { IsValid: true } recipe
			? SoulmatesText.Get("UI.CompanionWheel.WorkMenu.Last", WorkRecipeLabel(recipe))
			: SoulmatesText.Get("UI.CompanionWheel.WorkMenu.NoLast");

	private static string WorkRecipeLabel(CompanionWorkRecipe recipe)
	{
		string name = SoulmatesText.Get($"UI.CompanionWheel.WorkActions.{recipe.Kind switch {
			CompanionWorkKind.Treasure => WheelWorkAction.FindTreasure,
			CompanionWorkKind.MineArea => WheelWorkAction.MineArea,
			CompanionWorkKind.GatherArea => WheelWorkAction.GatherArea,
			CompanionWorkKind.MineTarget => WheelWorkAction.MineTarget,
			CompanionWorkKind.GatherTarget => WheelWorkAction.GatherTarget,
			_ => WheelWorkAction.ForestTarget
		}}");
		return recipe.Kind == CompanionWorkKind.MineArea ? name + " / " + SoulmatesText.EnumName(recipe.Approach)
			+ (recipe.Approach == CompanionMiningApproach.Tunnel ? " / " + CompassDirectionLabel(recipe.Direction)
				+ " / " + TunnelEndLabel(recipe.End) : "") : name;
	}

	private Vector2 WorkConfigPosition(int index) => center + FanAngle(RootAngle(Array.IndexOf(ActiveRoots, RootBranch.Work)),
		index, WorkActions.Length, MathHelper.ToRadians(148f)).ToRotationVector2() * NativeRadius * LayoutScale;

	private void DrawWorkActions(SpriteBatch spriteBatch, Color accent, float reveal)
	{
		Vector2 parent = BranchPosition(RootBranch.Work, 0, BranchNodeCount(RootBranch.Work));
		for (int i = 0; i < WorkActions.Length; i++) DrawNode(spriteBatch,
			Vector2.Lerp(parent, WorkConfigPosition(i), reveal), 32f, accent,
			hoverLayer == HoverLayer.WorkConfig && hoverIndex == i, false, WorkActionIcon(i));
	}

	private void SetMiningConfig(CompanionMiningApproach approach, CompanionMiningDirection direction, CompanionTunnelEnd end)
	{
		if (companion?.NPC.active != true) return;
		if (Main.netMode == NetmodeID.MultiplayerClient)
			global::Soulmates.Soulmates.SendMiningConfigRequest(companion.Profile.Id, approach, direction, end);
		else companion.ConfigureMining(approach, direction, end);
		SoundEngine.PlaySound(SoundID.MenuTick);
	}

	private void ActivateCompass(int index)
	{
		if (companion is null || index < 0 || index > 5) return;
		var profile = companion.Profile;
		CompanionMiningDirection direction = index == 4 ? CompanionMiningDirection.Auto : (CompanionMiningDirection)(index + 1);
		SetMiningConfig(CompanionMiningApproach.Tunnel,
			index < 5 ? direction : profile.MiningDirection,
			index == 5 ? profile.TunnelEnd == CompanionTunnelEnd.Passage ? CompanionTunnelEnd.Short : CompanionTunnelEnd.Passage : profile.TunnelEnd);
	}
	private Vector2 CompassPosition(int index) => center + (index switch {
		0 => new Vector2(0, -96), 1 => new Vector2(96, 0), 2 => new Vector2(0, 96), 3 => new Vector2(-96, 0),
		4 => new Vector2(-66, 66), _ => new Vector2(66, 66)
	}) * LayoutScale;
	private static string CompassDirectionLabel(CompanionMiningDirection direction)
		=> SoulmatesText.Get($"UI.CompanionWheel.WorkMenu.Directions.{direction}");
	private static string TunnelEndLabel(CompanionTunnelEnd end) => SoulmatesText.Get($"UI.CompanionWheel.WorkMenu.Ends.{end}");
	private string CompassLabel(int index) => index < 5
		? CompassDirectionLabel(index == 4 ? CompanionMiningDirection.Auto : (CompanionMiningDirection)(index + 1))
		: SoulmatesText.Get("UI.CompanionWheel.WorkMenu.End", TunnelEndLabel(companion!.Profile.TunnelEnd));
	private void DrawCompass(SpriteBatch spriteBatch, Color accent)
	{
		for (int i = 0; i < 6; i++) {
			var direction = i == 4 ? CompanionMiningDirection.Auto : (CompanionMiningDirection)(i + 1);
			DrawNode(spriteBatch, CompassPosition(i), 40f, accent, hoverLayer == HoverLayer.Compass && hoverIndex == i,
				i < 5 && companion!.Profile.MiningDirection == direction, i < 4 ? new WheelIcon(IconKind.Direction, i)
					: i == 4 ? new WheelIcon(IconKind.Item, ItemID.Compass) : new WheelIcon(IconKind.Emote, EmoteID.ItemCog));
		}
	}
}
