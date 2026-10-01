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
		if (Main.netMode != NetmodeID.MultiplayerClient && emoteBubble.anchor.entity is NPC resident
			&& resident.townNPC && resident.ModNPC is not SoulboundCompanion) {
			foreach (NPC candidate in Main.npc)
				if (candidate.active && candidate.ModNPC is SoulboundCompanion companion)
					companion.ObserveResidentEmote(resident, emoteBubble);
			return;
		}
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
