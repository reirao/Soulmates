using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Soulmates.Common;
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
		return true;
	}

	public override bool? UseItem(Player player)
	{
		if (player.whoAmI != Main.myPlayer)
			return true;

		SoulboundCompanion? companion = SoulboundCompanion.FindFor(player);
		if (player.altFunctionUse == 2 && companion is not null) {
			if (player.controlUp) {
				companion.Recall();
				Main.NewText($"{Profile.Name} returned to the Soulbound Sigil.", Profile.EssenceColor);
			}
			else {
				companion.ToggleCommand();
				Main.NewText($"{Profile.Name}: {companion.CommandName}", Profile.EssenceColor);
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

		Main.NewText($"{Profile.Name} answers your call.", Profile.EssenceColor);
		return true;
	}

	public override void ModifyTooltips(List<TooltipLine> tooltips)
	{
		tooltips.Add(new TooltipLine(Mod, "BoundTo", $"Bound to {Profile.Name}") { OverrideColor = Profile.EssenceColor });
		tooltips.Add(new TooltipLine(Mod, "Appearance", $"{SplitName(Profile.Muse.ToString())} muse  |  {Profile.Form} form  |  {SplitName(Profile.Aura.ToString())}"));
		tooltips.Add(new TooltipLine(Mod, "Identity", $"{Profile.Personality}  |  {SplitName(Profile.Talent.ToString())}"));
		tooltips.Add(new TooltipLine(Mod, "Bond", $"Bond {Profile.Bond}  |  Mood {Profile.Mood}  |  Energy {Profile.Energy}"));
		tooltips.Add(new TooltipLine(Mod, "Controls", "Use: summon  |  Right-click: Follow/Stay  |  Up + Right-click: recall"));
	}

	private static string SplitName(string value) => System.Text.RegularExpressions.Regex.Replace(value, "([a-z])([A-Z])", "$1 $2");

	public override void SaveData(TagCompound tag) => tag["profile"] = Profile.Save();
	public override void LoadData(TagCompound tag) => Profile = CompanionProfile.Load(tag.GetCompound("profile"));
	public override void NetSend(BinaryWriter writer) => Profile.Write(writer);
	public override void NetReceive(BinaryReader reader) => Profile = CompanionProfile.Read(reader);
}
