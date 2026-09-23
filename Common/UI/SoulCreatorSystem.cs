using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace Soulmates.Common.UI;

public sealed class SoulCreatorSystem : ModSystem
{
	private UserInterface? creatorInterface;
	internal SoulCreatorState? CreatorState { get; private set; }

	public override void Load()
	{
		if (Main.dedServ)
			return;

		CreatorState = new SoulCreatorState();
		CreatorState.Activate();
		creatorInterface = new UserInterface();
	}

	public void Open()
	{
		if (CreatorState is null || creatorInterface is null)
			return;
		CreatorState.ResetDraft();
		creatorInterface.SetState(CreatorState);
		Main.playerInventory = true;
	}

	public void Close() => creatorInterface?.SetState(null);

	public override void UpdateUI(GameTime gameTime)
	{
		creatorInterface?.Update(gameTime);
	}

	public override void ModifyInterfaceLayers(System.Collections.Generic.List<GameInterfaceLayer> layers)
	{
		int inventoryIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Inventory"));
		if (inventoryIndex < 0)
			return;

		layers.Insert(inventoryIndex, new LegacyGameInterfaceLayer("Soulmates: Soul Creator", () => {
			creatorInterface?.Draw(Main.spriteBatch, new GameTime());
			return true;
		}, InterfaceScaleType.UI));
	}
}
