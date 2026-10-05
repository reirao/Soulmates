using Soulmates.Content.NPCs;
using Terraria;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulmates.Common;

public sealed class SoulmateEmoteObserver : GlobalEmoteBubble
{
	private static bool showingReply;

	internal static void ShowReply(Player player, int emote)
	{
		// Reply bubbles use native transport, but must not re-enter generic emote rewards or send a second answer.
		bool previous = showingReply;
		showingReply = true;
		try { EmoteBubble.NewBubble(emote, new WorldUIAnchor(player), 150); }
		finally { showingReply = previous; }
	}

	public override void OnSpawn(EmoteBubble emoteBubble)
	{
		if (showingReply) return;
		if (Main.netMode != NetmodeID.MultiplayerClient && emoteBubble.anchor.entity is NPC resident
			&& resident.townNPC && resident.ModNPC is not SoulboundCompanion) {
			foreach (NPC candidate in Main.npc)
				if (candidate.active && candidate.ModNPC is SoulboundCompanion companion)
					companion.ObserveResidentEmote(resident, emoteBubble);
			return;
		}
		if (emoteBubble.anchor.entity is not Player player || player.whoAmI < 0)
			return;
		// Native message 120 already creates and observes this bubble on the server.
		// Message 91 is its broadcast, not another player request.
		if (Main.netMode == NetmodeID.MultiplayerClient) return;
		SoulboundCompanion.FindFor(player)?.ReactToNativeEmote(emoteBubble.emote);
	}
}
