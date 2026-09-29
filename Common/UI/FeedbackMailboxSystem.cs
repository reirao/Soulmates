#nullable enable
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace Soulmates.Common.UI;

public sealed class FeedbackMailboxSystem : ModSystem
{
	private UserInterface? mailboxInterface;
	internal FeedbackMailboxState? MailboxState { get; private set; }
	public bool IsOpen => mailboxInterface?.CurrentState is FeedbackMailboxState;

	public override void Load()
	{
		if (Main.dedServ)
			return;
		MailboxState = new FeedbackMailboxState();
		MailboxState.Activate();
		mailboxInterface = new UserInterface();
	}

	public void Open()
	{
		if (MailboxState is null || mailboxInterface is null || Main.dedServ || Main.gameMenu)
			return;
		ModContent.GetInstance<TalkModeSystem>().Close();
		ModContent.GetInstance<SoulCreatorSystem>().Close();
		ModContent.GetInstance<CompanionWheelSystem>().Close();
		ModContent.GetInstance<InitiativePromptSystem>().Close();
		MailboxState.Prepare();
		mailboxInterface.SetState(MailboxState);
		Main.playerInventory = false;
	}

	public void Close() => mailboxInterface?.SetState(null);

	public override void OnWorldUnload() => Close();

	public override void UpdateUI(GameTime gameTime)
	{
		if (IsOpen) {
			if (Main.gameMenu || Main.LocalPlayer.dead
				|| Main.keyState.IsKeyDown(Keys.Escape) && Main.oldKeyState.IsKeyUp(Keys.Escape)) {
				Close();
				return;
			}
			Main.LocalPlayer.mouseInterface = true;
			Main.playerInventory = false;
		}
		mailboxInterface?.Update(gameTime);
	}

	public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
	{
		int inventoryIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Inventory"));
		if (inventoryIndex < 0)
			inventoryIndex = layers.Count;
		layers.Insert(inventoryIndex, new LegacyGameInterfaceLayer("Soulmates: AETHER Mailbox", () => {
			mailboxInterface?.Draw(Main.spriteBatch, new GameTime());
			return true;
		}, InterfaceScaleType.UI));
	}
}
