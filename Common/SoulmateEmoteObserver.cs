using Soulmates.Content.NPCs;
using Terraria;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulmates.Common;

public sealed class SoulmateEmoteObserver : GlobalEmoteBubble
{
	public override void OnSpawn(EmoteBubble emoteBubble)
	{
		if (emoteBubble.anchor.entity is not Player player || player.whoAmI < 0)
			return;
		if (Main.netMode == NetmodeID.MultiplayerClient) {
			if (player.whoAmI == Main.myPlayer)
				Soulmates.SendNativeEmoteRequest(emoteBubble.emote);
			return;
		}
		SoulboundCompanion.FindFor(player)?.ReactToNativeEmote(emoteBubble.emote);
	}
}
