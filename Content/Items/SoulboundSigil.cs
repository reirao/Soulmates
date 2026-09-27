using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Common.UI;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
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

	public override bool AltFunctionUse(Player player) => true;

	public override bool CanUseItem(Player player)
	{
		Item.UseSound = player.altFunctionUse == 2 ? SoundID.MenuTick : SoundID.Item8;
		return !ModContent.GetInstance<TalkModeSystem>().IsOpen;
	}

	public override bool? UseItem(Player player)
	{
		if (player.whoAmI != Main.myPlayer)
			return true;

		SoulboundCompanion? companion = SoulboundCompanion.FindFor(player);
		if (player.altFunctionUse == 2 && companion is not null) {
			if (player.controlUp) {
				companion.Recall();
				Main.NewText(SoulmatesText.Get("Messages.Recalled", Profile.Name), Profile.EssenceColor);
			}
			else {
				ModContent.GetInstance<TalkModeSystem>().Open(this, companion);
			}
			return true;
		}

		if (companion is not null)
			companion.Recall();

		int index = NPC.NewNPC(new EntitySource_ItemUse(player, Item), (int)player.Center.X, (int)player.Center.Y - 48,
			ModContent.NPCType<SoulboundCompanion>(), ai0: player.whoAmI);
		if (Main.npc[index].ModNPC is SoulboundCompanion created) {
			created.Profile = Profile.Clone();
			created.NPC.netUpdate = true;
			player.GetModPlayer<SoulmatesPlayer>().ActiveCompanionWhoAmI = index;
		}

		Main.NewText(SoulmatesText.Get("Messages.Summoned", Profile.Name), Profile.EssenceColor);
		return true;
	}

	public override void ModifyTooltips(List<TooltipLine> tooltips)
	{
		tooltips.Add(new TooltipLine(Mod, "BoundTo", SoulmatesText.Get("Tooltips.Sigil.BoundTo", Profile.Name)) { OverrideColor = Profile.EssenceColor });
		tooltips.Add(new TooltipLine(Mod, "Appearance", SoulmatesText.Get("Tooltips.Sigil.Appearance", SoulmatesText.EnumName(Profile.Muse),
			SoulmatesText.EnumName(Profile.Form), SoulmatesText.EnumName(Profile.Aura))));
		tooltips.Add(new TooltipLine(Mod, "Identity", SoulmatesText.Get("Tooltips.Sigil.Identity", SoulmatesText.EnumName(Profile.Personality), SoulmatesText.EnumName(Profile.Talent))));
		tooltips.Add(new TooltipLine(Mod, "Bond", SoulmatesText.Get("Tooltips.Sigil.Stats", Profile.Bond, Profile.Mood, Profile.Energy)));
		tooltips.Add(new TooltipLine(Mod, "Voice", SoulmatesText.Get("Tooltips.Sigil.Voice", SoulmatesText.EnumName(Profile.Voice))));
		tooltips.Add(new TooltipLine(Mod, "Trinket", SoulmatesText.Get("Tooltips.Sigil.Trinket", SoulmatesText.EnumName(Profile.Trinket),
			Profile.PackLoad, Profile.PackCapacity, Profile.JobsCompleted)));
		if (Profile.Routine != CompanionJob.None)
			tooltips.Add(new TooltipLine(Mod, "Assignment", SoulmatesText.Get("Tooltips.Sigil.Assignment", SoulmatesText.EnumName(Profile.Routine))));
		tooltips.Add(new TooltipLine(Mod, "Controls", SoulmatesText.Get("Tooltips.Sigil.Controls")));
	}

	public override void SaveData(TagCompound tag) => tag["profile"] = Profile.Save();
	public override void LoadData(TagCompound tag) => Profile = CompanionProfile.Load(tag.GetCompound("profile"));
	public override void NetSend(BinaryWriter writer) => Profile.Write(writer);
	public override void NetReceive(BinaryReader reader) => Profile = CompanionProfile.Read(reader);
}
