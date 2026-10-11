#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
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
	private readonly record struct MiningWheelConfig(CompanionMiningApproach Approach, CompanionMiningDirection Direction, CompanionTunnelEnd End)
	{
		public static MiningWheelConfig From(CompanionProfile profile) => new(profile.MiningApproach, profile.MiningDirection, profile.TunnelEnd);
	}
	private const int MiningConfigTimeoutTicks = 600;
	private const int MaximumPendingMiningConfigs = 64;
	private SoulboundSigil? miningConfigSigil;
	private SoulboundCompanion? miningConfigCompanion;
	private Guid miningConfigId;
	private CompanionProfile? miningConfigObservedProfile;
	private MiningWheelConfig miningConfigBaseline;
	private readonly List<MiningWheelConfig> miningConfigRequests = [];
	private MiningWheelConfig miningConfigDraft;
	private int miningConfigWaitTicks;
	private int miningConfigOpenTicks;

	public override void PostUpdateInput()
	{
		ReconcileMiningConfigDraft();
		if (miningConfigSigil is not null && ++miningConfigWaitTicks >= MiningConfigTimeoutTicks)
			ResetMiningConfigDraft();
	}

	private void ResetMiningConfigDraft()
	{
		miningConfigSigil = null;
		miningConfigCompanion = null;
		miningConfigId = Guid.Empty;
		miningConfigObservedProfile = null;
		miningConfigBaseline = miningConfigDraft = default;
		miningConfigRequests.Clear();
		miningConfigWaitTicks = miningConfigOpenTicks = 0;
	}

	private void ReconcileMiningConfigDraft()
	{
		if (miningConfigSigil is null) return;
		if (Main.netMode != NetmodeID.MultiplayerClient || Main.gameMenu || Main.LocalPlayer.dead
			|| !open || HasProtectedInput || !HasCurrentCompanion || branch != RootBranch.Work
			|| !ReferenceEquals(companion, miningConfigCompanion) || companion!.Profile.Id != miningConfigId
			|| miningConfigSigil.Profile.Id != miningConfigId || !ReferenceEquals(companion.FindBoundSigil(), miningConfigSigil)
			|| openTicks < miningConfigOpenTicks) {
			ResetMiningConfigDraft();
			return;
		}
		miningConfigOpenTicks = openTicks;
		// Profile updates replace the snapshot; an unchanged snapshot is not an acknowledgement.
		if (ReferenceEquals(companion.Profile, miningConfigObservedProfile)) return;
		miningConfigObservedProfile = companion.Profile;
		MiningWheelConfig actual = MiningWheelConfig.From(companion.Profile);
		if (actual == miningConfigBaseline) return;
		// An earlier full-config reply must not discard later deliberate clicks.
		int acknowledged = miningConfigRequests.IndexOf(actual);
		if (acknowledged >= 0) {
			miningConfigRequests.RemoveRange(0, acknowledged + 1);
			miningConfigBaseline = actual;
			miningConfigWaitTicks = 0;
			if (miningConfigRequests.Count == 0) ResetMiningConfigDraft();
		}
		else ResetMiningConfigDraft();
		// Unrelated baseline updates and silently rejected requests expire without retrying.
	}

	private MiningWheelConfig CurrentMiningConfig
	{
		get {
			ReconcileMiningConfigDraft();
			return miningConfigSigil is not null ? miningConfigDraft
				: companion is not null ? MiningWheelConfig.From(companion.Profile) : default;
		}
	}

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

	private void DrawWorkActions(SpriteBatch spriteBatch, Color accent)
	{
		for (int i = 0; i < WorkActions.Length; i++) DrawNode(spriteBatch,
			WorkConfigPosition(i), 32f, accent,
			hoverLayer == HoverLayer.WorkConfig && hoverIndex == i, false, WorkActionIcon(i));
	}

	private void SetMiningConfig(CompanionMiningApproach approach, CompanionMiningDirection direction, CompanionTunnelEnd end)
	{
		MiningWheelConfig current = CurrentMiningConfig;
		// The approach picker supplies the last acknowledged direction/end from the other partial.
		ApplyMiningConfig(new(approach, miningConfigSigil is not null ? current.Direction : direction,
			miningConfigSigil is not null ? current.End : end));
	}

	private void ApplyMiningConfig(MiningWheelConfig config)
	{
		ReconcileMiningConfigDraft();
		if (!open || !HasCurrentCompanion || HasProtectedInput || companion?.FindBoundSigil() is not { } sigil
			|| sigil.Profile.Id != companionId || !Enum.IsDefined(config.Approach)
			|| !Enum.IsDefined(config.Direction) || !Enum.IsDefined(config.End)) return;
		if (Main.netMode == NetmodeID.MultiplayerClient) {
			if (miningConfigRequests.Count >= MaximumPendingMiningConfigs) return;
			if (config == CurrentMiningConfig) return;
			if (miningConfigSigil is null) {
				miningConfigSigil = sigil;
				miningConfigCompanion = companion;
				miningConfigId = companionId;
				miningConfigObservedProfile = companion.Profile;
				miningConfigBaseline = MiningWheelConfig.From(companion.Profile);
				miningConfigWaitTicks = 0;
				miningConfigOpenTicks = openTicks;
			}
			miningConfigDraft = config;
			miningConfigRequests.Add(config);
			global::Soulmates.Soulmates.SendMiningConfigRequest(miningConfigId, config.Approach, config.Direction, config.End);
		}
		else companion.ConfigureMining(config.Approach, config.Direction, config.End);
		SoundEngine.PlaySound(SoundID.MenuTick);
	}

	private void ActivateCompass(int index)
	{
		if (companion is null || index < 0 || index > 5) return;
		MiningWheelConfig config = CurrentMiningConfig;
		CompanionMiningDirection direction = index == 4 ? CompanionMiningDirection.Auto : (CompanionMiningDirection)(index + 1);
		ApplyMiningConfig(new(CompanionMiningApproach.Tunnel,
			index < 5 ? direction : config.Direction,
			index == 5 ? config.End == CompanionTunnelEnd.Passage ? CompanionTunnelEnd.Short : CompanionTunnelEnd.Passage : config.End));
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
		: SoulmatesText.Get("UI.CompanionWheel.WorkMenu.End", TunnelEndLabel(CurrentMiningConfig.End));
	private void DrawCompass(SpriteBatch spriteBatch, Color accent)
	{
		MiningWheelConfig config = CurrentMiningConfig;
		for (int i = 0; i < 6; i++) {
			var direction = i == 4 ? CompanionMiningDirection.Auto : (CompanionMiningDirection)(i + 1);
			DrawNode(spriteBatch, CompassPosition(i), 40f, accent, hoverLayer == HoverLayer.Compass && hoverIndex == i,
				i < 5 && config.Direction == direction, i < 4 ? new WheelIcon(IconKind.Direction, i)
					: i == 4 ? new WheelIcon(IconKind.Item, ItemID.Compass) : new WheelIcon(IconKind.Emote, EmoteID.ItemCog));
		}
	}
}
