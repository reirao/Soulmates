#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Common.Feedback;
using Soulmates.Common.UI;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Soulmates.Content.Items;

public sealed class SoulboundSigil : ModItem
{
	public CompanionProfile Profile { get; set; } = new();
	public override string Texture => $"Terraria/Images/Item_{ItemID.ShadowOrb}";

	public override void SetDefaults()
	{
		Item.width = 24;
		Item.height = 24;
		Item.useStyle = ItemUseStyleID.HoldUp;
		Item.useTime = 20;
		Item.useAnimation = 20;
		Item.noMelee = true;
		Item.value = Item.buyPrice(gold: 1);
		Item.rare = ItemRarityID.Orange;
		Item.UseSound = SoundID.Item8;
	}

	public override ModItem Clone(Item newEntity)
	{
		var clone = (SoulboundSigil)base.Clone(newEntity);
		clone.Profile = Profile.Clone();
		return clone;
	}

	public override bool AltFunctionUse(Player player) => player.controlUp && SoulboundCompanion.FindFor(player) is not null;

	public override bool CanUseItem(Player player)
	{
		Item.UseSound = player.altFunctionUse == 2 ? SoundID.MenuTick : SoundID.Item8;
		return !CompanionInventorySync.IsPending(player) && !ModContent.GetInstance<TalkModeSystem>().IsOpen;
	}

	public override bool? UseItem(Player player)
	{
		SoulboundCompanion? companion = SoulboundCompanion.FindFor(player);
		if (player.altFunctionUse == 2) {
			if (player.whoAmI != Main.myPlayer)
				return true;
			if (companion is not null) {
				SoulmatesFeedbackSystem.Record("companion_recalled");
				if (Main.netMode == NetmodeID.MultiplayerClient)
					Soulmates.SendRecallRequest(companion.Profile.Id);
				else
					SoulboundCompanion.RecallAllFor(player);
				Main.NewText(SoulmatesText.Get("Messages.Recalled", Profile.Name), Profile.EssenceColor);
			}
			return true;
		}
		if (player.whoAmI == Main.myPlayer)
			SoulmatesFeedbackSystem.Record("companion_summoned", ("muse", Profile.Muse.ToString()),
				("personality", Profile.Personality.ToString()), ("talent", Profile.Talent.ToString()));

		if (Main.netMode == NetmodeID.MultiplayerClient) {
			if (player.whoAmI == Main.myPlayer) {
				Main.NewText(SoulmatesText.Get("Messages.Summoned", Profile.Name), Profile.EssenceColor);
			}
			return true;
		}

		bool summoned = SummonCompanion(player);
		if (Main.netMode != NetmodeID.Server)
			Main.NewText(SoulmatesText.Get(summoned ? "Messages.Summoned" : "Messages.SummonFailed", Profile.Name), Profile.EssenceColor);
		else if (!summoned)
			ChatHelper.SendChatMessageToClient(NetworkText.FromKey("Mods.Soulmates.Messages.SummonFailed", Profile.Name),
				Profile.EssenceColor, player.whoAmI);
		return true;
	}

	internal bool SummonCompanion(Player player)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || !player.active || player.dead || CompanionInventorySync.IsPending(player))
			return false;
		Profile.Normalize();
		SoulboundCompanion.RecallAllFor(player);

		int index = NPC.NewNPC(new EntitySource_ItemUse(player, Item), (int)player.Center.X, (int)player.Center.Y - 48,
			ModContent.NPCType<SoulboundCompanion>(), ai0: player.whoAmI);
		if (index < 0 || index >= Main.maxNPCs || Main.npc[index].ModNPC is not SoulboundCompanion created)
			return false;
		created.Profile = Profile.Clone();
		created.NPC.netUpdate = true;
		player.GetModPlayer<SoulmatesPlayer>().ActiveCompanionWhoAmI = index;
		if (Main.netMode == NetmodeID.Server)
			NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, index);
		return true;
	}

	public override void ModifyTooltips(List<TooltipLine> tooltips)
	{
		string talent = Profile.IsAether
			? SoulmatesText.Get("UI.Talk.OmniSoul")
			: SoulmatesText.EnumName(Profile.Talent);
		tooltips.Add(new TooltipLine(Mod, "BoundTo", SoulmatesText.Get("Tooltips.Sigil.BoundTo", Profile.Name)) { OverrideColor = Profile.EssenceColor });
		tooltips.Add(new TooltipLine(Mod, "Appearance", SoulmatesText.Get("Tooltips.Sigil.Appearance", SoulmatesText.EnumName(Profile.Muse),
			SoulmatesText.EnumName(Profile.Form), SoulmatesText.EnumName(Profile.Aura))));
		tooltips.Add(new TooltipLine(Mod, "Identity", SoulmatesText.Get("Tooltips.Sigil.Identity", SoulmatesText.EnumName(Profile.Personality), talent)));
		tooltips.Add(new TooltipLine(Mod, "Bond", SoulmatesText.Get("Tooltips.Sigil.Stats", SoulmatesText.EnumName(Profile.Rank),
			Profile.Bond, Profile.Mood, Profile.Energy)));
		tooltips.Add(new TooltipLine(Mod, "Growth", SoulmatesText.Get("Tooltips.Sigil.Growth", Profile.Level,
			Profile.Experience, CompanionProfile.ExperienceForLevel(Math.Min(CompanionProfile.MaximumLevel, Profile.Level + 1)),
			Profile.DefeatedEnemies, Profile.Interactions)));
		tooltips.Add(new TooltipLine(Mod, "Voice", SoulmatesText.Get("Tooltips.Sigil.Voice", SoulmatesText.EnumName(Profile.Voice))));
		tooltips.Add(new TooltipLine(Mod, "Trinket", SoulmatesText.Get("Tooltips.Sigil.Trinket", SoulmatesText.EnumName(Profile.Trinket),
			Profile.PackLoad, Profile.PackCapacity, Profile.JobsCompleted)));
		tooltips.Add(new TooltipLine(Mod, "Resources", SoulmatesText.Get("Storage.Tooltip",
			Profile.ResourceLoad, Profile.ResourceCarryLimit, Profile.DescribeWallet())));
		LearnedBehavior dominantBehavior = Profile.DominantLearnedBehavior;
		string dominantPerk = Profile.HasLearnedPerk(dominantBehavior)
			? CompanionProfile.LearnedPerkName(dominantBehavior)
			: SoulmatesText.Get("Learning.PerkProgress", CompanionProfile.LearnedPerkName(dominantBehavior),
				Profile.GetInsight(dominantBehavior), CompanionProfile.LearnedPerkUnlockInsight(dominantBehavior));
		string learning = Profile.IsAether
			? SoulmatesText.Get("Tooltips.Sigil.AetherLearning")
			: Profile.DominantInsight <= 0
				? SoulmatesText.Get("Tooltips.Sigil.ObservingProgress", dominantPerk)
				: SoulmatesText.Get("Tooltips.Sigil.Learning", SoulmatesText.EnumName(Profile.DominantLearnedBehavior),
					Profile.DominantInsight, dominantPerk);
		tooltips.Add(new TooltipLine(Mod, "Learning", learning) { OverrideColor = Color.Lerp(Profile.EssenceColor, Color.White, 0.25f) });
		tooltips.Add(new TooltipLine(Mod, "Resourcefulness", SoulmatesText.Get("Tooltips.Sigil.Resourcefulness",
			Math.Max(35, Profile.ObservedPickPower), Profile.LearnedMiningTiles.Count)));
		tooltips.Add(new TooltipLine(Mod, "MiningApproach", SoulmatesText.Get("Tooltips.Sigil.MiningApproach",
			SoulmatesText.EnumName(Profile.MiningApproach))) { OverrideColor = Profile.EssenceColor });
		if (Profile.Routine != CompanionJob.None)
			tooltips.Add(new TooltipLine(Mod, "Assignment", SoulmatesText.Get("Tooltips.Sigil.Assignment", SoulmatesText.EnumName(Profile.Routine))));
		tooltips.Add(new TooltipLine(Mod, "Controls", SoulmatesText.Get("Tooltips.Sigil.Controls")));
		tooltips.Add(new TooltipLine(Mod, "Emotes", SoulmatesText.Get("Tooltips.Sigil.Emotes")));
	}

	public override void SaveData(TagCompound tag) => tag["profile"] = Profile.Save();
	public override void LoadData(TagCompound tag) => Profile = CompanionProfile.Load(tag.GetCompound("profile"));
	public override void NetSend(BinaryWriter writer) => Profile.Write(writer);
	public override void NetReceive(BinaryReader reader) => Profile = CompanionProfile.Read(reader);
}
