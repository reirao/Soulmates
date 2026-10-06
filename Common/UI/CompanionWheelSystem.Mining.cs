using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.UI;
using Terraria.ID;

namespace Soulmates.Common.UI;

public sealed partial class CompanionWheelSystem
{
	private const int MiningFilterPageSize = 5;
	private bool miningFilterMenu;
	private int miningFilterCategory = -1;
	private int miningFilterPage;
	private readonly List<int> miningFilterTypes = [];
	private int MiningFilterPageCount => Math.Max(1, (miningFilterTypes.Count + MiningFilterPageSize - 1) / MiningFilterPageSize);
	private int MiningFilterEntries => Math.Min(MiningFilterPageSize, Math.Max(0, miningFilterTypes.Count - miningFilterPage * MiningFilterPageSize));
	private int MiningFilterNodeCount => miningFilterCategory < 0 ? 2 : 1 + MiningFilterEntries
		+ (miningFilterPage > 0 ? 1 : 0) + (miningFilterPage + 1 < MiningFilterPageCount ? 1 : 0);

	private void ResetMiningFilter()
	{
		miningFilterMenu = false;
		miningFilterCategory = -1;
		miningFilterPage = 0;
		miningFilterTypes.Clear();
	}

	private int MiningFilterEntry(int index)
	{
		if (index == 0) return -1;
		if (index <= MiningFilterEntries) return miningFilterTypes[miningFilterPage * MiningFilterPageSize + index - 1];
		return miningFilterPage > 0 && index == MiningFilterEntries + 1 ? -2 : -3;
	}

	private bool MiningFilterEnabled(int index) => companion is not null && (index == 0
		? miningFilterTypes.Count > 0 && miningFilterTypes.All(companion.Profile.AllowsAutomaticMining)
		: MiningFilterEntry(index) is >= 0 and var type && companion.Profile.AllowsAutomaticMining(type));

	private void ActivateMiningFilter(int index)
	{
		if (companion?.NPC.active != true || index < 0 || index >= MiningFilterNodeCount) return;
		if (miningFilterCategory < 0) {
			miningFilterCategory = index;
			miningFilterPage = 0;
			miningFilterTypes.Clear();
			miningFilterTypes.AddRange(CompanionMiningRules.Types(companion.Profile, index == 0));
		}
		else {
			int type = MiningFilterEntry(index);
			if (type is -2 or -3) miningFilterPage += type == -2 ? -1 : 1;
			else if (miningFilterTypes.Count > 0) {
				bool enabled = !MiningFilterEnabled(index);
				if (Main.netMode == NetmodeID.MultiplayerClient)
					global::Soulmates.Soulmates.SendMiningRuleRequest(companion.Profile.Id, miningFilterCategory == 0, type, enabled);
				else companion.SetAutomaticMiningRule(miningFilterCategory == 0, type, enabled);
			}
		}
		SoundEngine.PlaySound(SoundID.MenuTick);
	}

	private Vector2 MiningFilterPosition(int index)
	{
		float rootAngle = RootAngle(Array.IndexOf(ActiveRoots, RootBranch.Work));
		float branchAngle = FanAngle(rootAngle, Array.IndexOf(WorkActions, WheelWorkAction.MiningApproach),
			WorkActions.Length, MathHelper.ToRadians(136f));
		return center + FanAngle(branchAngle, index, MiningFilterNodeCount, MathHelper.ToRadians(148f))
			.ToRotationVector2() * NativeRadius * LayoutScale;
	}

	private string MiningFilterLabel(int index)
	{
		if (index < 0 || index >= MiningFilterNodeCount) return "";
		if (miningFilterCategory < 0) return SoulmatesText.Get(index == 0
			? "UI.CompanionWheel.MiningFilter.Ores" : "UI.CompanionWheel.MiningFilter.Blocks");
		if (miningFilterTypes.Count == 0) return SoulmatesText.Get("UI.CompanionWheel.MiningFilter.Empty");
		int type = MiningFilterEntry(index);
		if (type is -2 or -3) return SoulmatesText.Get(type == -2 ? "UI.CompanionWheel.Previous" : "UI.CompanionWheel.Next");
		string name = type >= 0 ? CompanionMiningRules.Name(type)
			: SoulmatesText.Get(miningFilterCategory == 0 ? "UI.CompanionWheel.MiningFilter.AllOres" : "UI.CompanionWheel.MiningFilter.AllBlocks");
		return SoulmatesText.Get("UI.CompanionWheel.MiningFilter.Rule", name,
			SoulmatesText.Get(MiningFilterEnabled(index) ? "UI.CompanionWheel.MiningFilter.Enabled" : "UI.CompanionWheel.MiningFilter.Disabled"));
	}

	private void DrawMiningFilter(SpriteBatch spriteBatch, Color accent, float reveal)
	{
		Vector2 parent = WorkConfigPosition(Array.IndexOf(WorkActions, WheelWorkAction.MiningApproach));
		for (int i = 0; i < MiningFilterNodeCount; i++) {
			int type = miningFilterCategory < 0 ? -1 : MiningFilterEntry(i);
			WheelIcon icon = miningFilterCategory < 0
				? new WheelIcon(IconKind.Item, i == 0 ? ItemID.CopperOre : ItemID.DirtBlock)
				: type == -2 ? new WheelIcon(IconKind.Back, 0)
				: type == -3 ? new WheelIcon(IconKind.Forward, 0)
				: type < 0 ? new WheelIcon(IconKind.Emote, EmoteID.ItemCog)
				: CompanionMiningRules.ItemType(type) is > ItemID.None and var itemType
					? new WheelIcon(IconKind.Item, itemType) : new WheelIcon(IconKind.Emote, EmoteID.ItemPickaxe);
			bool enabled = miningFilterCategory >= 0 && MiningFilterEnabled(i);
			DrawNode(spriteBatch, Vector2.Lerp(parent, MiningFilterPosition(i), reveal), 32f,
				miningFilterCategory < 0 || enabled || type is -2 or -3 ? accent : Color.Gray,
				hoverLayer == HoverLayer.MiningFilter && hoverIndex == i, enabled, icon);
		}
	}
}
