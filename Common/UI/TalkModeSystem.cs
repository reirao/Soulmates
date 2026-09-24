using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace Soulmates.Common.UI;

public sealed class TalkModeSystem : ModSystem
{
	private UserInterface? talkInterface;
	private TalkModeState? talkState;
	public bool IsOpen => talkInterface?.CurrentState is TalkModeState;

	public override void Load()
	{
		if (Main.dedServ)
			return;
		talkState = new TalkModeState();
		talkState.Activate();
		talkInterface = new UserInterface();
	}

	public void Open(SoulboundSigil sigil, SoulboundCompanion companion)
	{
		if (talkState is null || talkInterface is null || IsOpen)
			return;
		ModContent.GetInstance<SoulCreatorSystem>().Close();
		talkState.Bind(sigil, companion);
		talkInterface.SetState(talkState);
		Main.playerInventory = false;
	}

	public void Close() => talkInterface?.SetState(null);

	public override void UpdateUI(GameTime gameTime)
	{
		if (IsOpen) {
			if (Main.gameMenu || Main.keyState.IsKeyDown(Keys.Escape) && Main.oldKeyState.IsKeyUp(Keys.Escape)) {
				Close();
				return;
			}
			Main.LocalPlayer.mouseInterface = true;
			Main.playerInventory = false;
		}
		talkInterface?.Update(gameTime);
	}

	public override void ModifyInterfaceLayers(System.Collections.Generic.List<GameInterfaceLayer> layers)
	{
		int inventoryIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Inventory"));
		if (inventoryIndex < 0)
			return;

		layers.Insert(inventoryIndex, new LegacyGameInterfaceLayer("Soulmates: Talk Mode", () => {
			talkInterface?.Draw(Main.spriteBatch, new GameTime());
			return true;
		}, InterfaceScaleType.UI));
	}
}
