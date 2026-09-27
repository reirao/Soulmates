using Soulmates.Common;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulmates.Content.Items;

public abstract class CompanionTrinketItem : ModItem
{
	protected abstract CompanionTrinket Trinket { get; }

	public override void SetDefaults()
	{
		Item.width = 24;
		Item.height = 24;
		Item.useStyle = ItemUseStyleID.HoldUp;
		Item.useTime = 20;
		Item.useAnimation = 20;
		Item.noMelee = true;
		Item.maxStack = 1;
		Item.value = Item.buyPrice(silver: 75);
		Item.rare = ItemRarityID.Blue;
		Item.UseSound = SoundID.Item4;
	}

	public override bool AltFunctionUse(Player player) => true;

	public override bool CanUseItem(Player player) => SoulboundCompanion.FindFor(player) is not null;

	public override bool? UseItem(Player player)
	{
		if (player.whoAmI != Main.myPlayer || SoulboundCompanion.FindFor(player) is not { } companion)
			return false;

		CompanionTrinket equipped = player.altFunctionUse == 2 ? CompanionTrinket.None : Trinket;
		companion.EquipTrinket(equipped);
		string message = equipped == CompanionTrinket.None
			? $"{companion.Profile.Name} removed their trinket."
			: $"{companion.Profile.Name} equipped {Item.Name}.";
		Main.NewText(message, companion.Profile.EssenceColor);
		return true;
	}
}

public sealed class StarfinderBell : CompanionTrinketItem
{
	protected override CompanionTrinket Trinket => CompanionTrinket.StarfinderBell;
	public override string Texture => $"Terraria/Images/Item_{ItemID.FairyBell}";

	public override void AddRecipes() => CreateRecipe()
		.AddIngredient(ItemID.FallenStar, 5)
		.AddRecipeGroup(RecipeGroupID.IronBar, 3)
		.AddIngredient(ItemID.Lens)
		.AddTile(TileID.Anvils)
		.Register();
}

public sealed class DelverCharm : CompanionTrinketItem
{
	protected override CompanionTrinket Trinket => CompanionTrinket.DelverCharm;
	public override string Texture => $"Terraria/Images/Item_{ItemID.MiningHelmet}";

	public override void AddRecipes() => CreateRecipe()
		.AddRecipeGroup(RecipeGroupID.IronBar, 5)
		.AddIngredient(ItemID.Amethyst, 2)
		.AddIngredient(ItemID.StoneBlock, 20)
		.AddTile(TileID.Anvils)
		.Register();
}

public sealed class HearthRibbon : CompanionTrinketItem
{
	protected override CompanionTrinket Trinket => CompanionTrinket.HearthRibbon;
	public override string Texture => $"Terraria/Images/Item_{ItemID.NaturesGift}";

	public override void AddRecipes() => CreateRecipe()
		.AddIngredient(ItemID.Silk, 5)
		.AddIngredient(ItemID.Daybloom, 2)
		.AddIngredient(ItemID.FallenStar, 2)
		.AddTile(TileID.WorkBenches)
		.Register();
}
