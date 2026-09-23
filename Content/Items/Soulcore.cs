using Soulmates.Common.UI;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulmates.Content.Items;

public sealed class Soulcore : ModItem
{
	public override string Texture => $"Terraria/Images/Item_{ItemID.FallenStar}";

	public override void SetDefaults()
	{
		Item.width = 28;
		Item.height = 28;
		Item.useStyle = ItemUseStyleID.HoldUp;
		Item.useTime = 20;
		Item.useAnimation = 20;
		Item.noMelee = true;
		Item.value = Item.buyPrice(gold: 1);
		Item.rare = ItemRarityID.Green;
		Item.UseSound = SoundID.Item4;
	}

	public override bool? UseItem(Player player)
	{
		if (player.whoAmI == Main.myPlayer)
			ModContent.GetInstance<SoulCreatorSystem>().Open();

		return true;
	}

	public override void AddRecipes()
	{
		CreateRecipe()
			.AddIngredient(ItemID.FallenStar, 8)
			.AddIngredient(ItemID.Amethyst, 5)
			.AddIngredient(ItemID.StoneBlock, 10)
			.AddTile(TileID.WorkBenches)
			.Register();
	}
}
