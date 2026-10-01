using System.Collections.Generic;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace Soulmates.Common.UI;

public sealed class CompanionSpeechSystem : ModSystem
{
	internal static bool CanShowSpeech => !Main.gameMenu && !Main.playerInventory
		&& Main.LocalPlayer.talkNPC < 0 && !ModContent.GetInstance<TalkModeSystem>().IsOpen
		&& !ModContent.GetInstance<FeedbackMailboxSystem>().IsOpen
		&& !ModContent.GetInstance<SoulCreatorSystem>().IsOpen
		&& !ModContent.GetInstance<CompanionWheelSystem>().IsOpen;

	public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
	{
		int mouseIndex = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
		layers.Insert(mouseIndex < 0 ? layers.Count : mouseIndex,
			new LegacyGameInterfaceLayer("Soulmates: Companion Speech", Draw, InterfaceScaleType.UI));
	}

	private static bool Draw()
	{
		if (!CanShowSpeech)
			return true;
		foreach (NPC npc in Main.ActiveNPCs) {
			if (npc.ModNPC is SoulboundCompanion companion)
				companion.DrawSpeechBubble(Main.spriteBatch);
		}
		return true;
	}
}
