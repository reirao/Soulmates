#nullable enable
using System;
using Terraria;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulmates.Content.NPCs;

public sealed partial class SoulboundCompanion
{
	private const int NativeExpressionBeat = 180;
	private int activeNativeBubble = -1;
	private int[] nativeExpression = [];
	private int nativeExpressionStep;
	private int nativeExpressionTicks;

	private void ShowNativeEmote(int emoteId, int duration)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || nativeExpressionTicks > 0
			|| emoteId < 0 || emoteId >= EmoteBubbleLoader.EmoteBubbleCount)
			return;
		EmoteBubble? current = EmoteBubble.GetExistingEmoteBubble(activeNativeBubble);
		if (current is not null && ReferenceEquals(current.anchor.entity, NPC)
			&& current.emote == emoteId && current.lifeTime > 12)
			return;
		RetireNativeBubble();
		activeNativeBubble = EmoteBubble.NewBubble(emoteId, new WorldUIAnchor(NPC), Math.Clamp(duration, 150, 360));
	}

	private void ShowNativeExpression(int first, int second, int third = -1)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || !Valid(first) || !Valid(second)
			|| third != -1 && !Valid(third))
			return;
		if (nativeExpressionTicks > 0 && nativeExpression.Length == (third < 0 ? 2 : 3)
			&& nativeExpression[0] == first && nativeExpression[1] == second
			&& (third < 0 || nativeExpression[2] == third))
			return;
		ClearNativeExpression();
		RetireNativeBubble();
		nativeExpression = third < 0 ? [first, second] : [first, second, third];
		ShowNativeEmote(first, NativeExpressionBeat);
		nativeExpressionTicks = NativeExpressionBeat;

		static bool Valid(int emote) => emote >= 0 && emote < EmoteBubbleLoader.EmoteBubbleCount;
	}

	private void UpdateNativeExpression()
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || nativeExpressionTicks <= 0
			|| --nativeExpressionTicks > 0)
			return;
		if (++nativeExpressionStep >= nativeExpression.Length) {
			ClearNativeExpression();
			return;
		}
		ShowNativeEmote(nativeExpression[nativeExpressionStep], NativeExpressionBeat);
		nativeExpressionTicks = NativeExpressionBeat;
	}

	private void ClearNativeExpression()
	{
		nativeExpression = [];
		nativeExpressionStep = nativeExpressionTicks = 0;
	}

	private void RetireNativeBubble()
	{
		EmoteBubble? previous = EmoteBubble.GetExistingEmoteBubble(activeNativeBubble);
		activeNativeBubble = -1;
		if (previous is null || !ReferenceEquals(previous.anchor.entity, NPC) || previous.lifeTime <= 0)
			return;
		previous.lifeTime = 0;
		// NPC bubbles are not deduplicated by Terraria's player-only OnBubbleChange path.
		if (Main.netMode == NetmodeID.Server)
			NetMessage.SendData(MessageID.SyncEmoteBubble, number: previous.WhoAmI, number2: 255);
	}
}
