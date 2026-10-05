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
		if (SoulboundCompanion.FindFor(player) is not { } companion)
			return false;

		CompanionTrinket equipped = player.altFunctionUse == 2 ? CompanionTrinket.None : Trinket;
		if (Main.netMode == NetmodeID.MultiplayerClient) {
			if (player.whoAmI == Main.myPlayer && player.altFunctionUse == 2)
				Soulmates.SendTrinketRequest(companion.Profile.Id, equipped);
			return true;
		}

		companion.EquipTrinket(equipped);
		string message = equipped == CompanionTrinket.None
			? SoulmatesText.Get("Messages.TrinketRemoved", companion.Profile.Name)
			: SoulmatesText.Get("Messages.TrinketEquipped", companion.Profile.Name, Item.Name);
		if (Main.netMode == NetmodeID.Server)
			Soulmates.SendProfileUpdate(player, companion, message);
		else
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
