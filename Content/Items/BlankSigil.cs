using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulmates.Content.Items;

public sealed class BlankSigil : ModItem
{
	public override string Texture => $"Terraria/Images/Item_{ItemID.Diamond}";

	public override void SetDefaults()
	{
		Item.width = 20;
		Item.height = 20;
		Item.maxStack = 99;
		Item.value = Item.buyPrice(silver: 25);
		Item.rare = ItemRarityID.Blue;
	}

	public override void AddRecipes()
	{
		CreateRecipe()
			.AddIngredient(ItemID.FallenStar, 3)
			.AddIngredient(ItemID.Amethyst)
			.AddIngredient(ItemID.Silk, 5)
			.AddTile(TileID.WorkBenches)
			.Register();
	}
}
