using Terraria;
using Terraria.ModLoader;

namespace Soulmates.Common.UI;

internal static class SoulmatesUIInput
{
	public static bool IsCaptured => !Main.dedServ && !Main.gameMenu
		&& (ModContent.GetInstance<SoulCreatorSystem>().IsOpen
			|| ModContent.GetInstance<TalkModeSystem>().IsOpen
			|| ModContent.GetInstance<FeedbackMailboxSystem>().IsOpen
			|| ModContent.GetInstance<CompanionWheelSystem>().IsOpen
			|| ModContent.GetInstance<InitiativePromptSystem>().IsOpen
			|| ModContent.GetInstance<DirectOrderSystem>().IsActive);

	public static bool CanPresentInitiative => !Main.dedServ && !Main.gameMenu
		&& !Main.LocalPlayer.dead && !Main.playerInventory && Main.LocalPlayer.talkNPC < 0
		&& !IsCaptured;
}
