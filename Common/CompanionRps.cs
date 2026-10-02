using System;
using Terraria.GameContent.UI;

namespace Soulmates.Common;

public enum RpsMove : byte { Rock, Paper, Scissors }
public enum RpsOutcome : byte { PlayerWin, CompanionWin, Draw }

public static class CompanionRps
{
	public static bool TryGetMove(int emote, out RpsMove move)
	{
		move = emote switch {
			EmoteID.RPSRock => RpsMove.Rock,
			EmoteID.RPSPaper => RpsMove.Paper,
			EmoteID.RPSScissors => RpsMove.Scissors,
			_ => (RpsMove)byte.MaxValue
		};
		return Enum.IsDefined(move);
	}

	public static int Emote(RpsMove move) => move switch {
		RpsMove.Rock => EmoteID.RPSRock,
		RpsMove.Paper => EmoteID.RPSPaper,
		RpsMove.Scissors => EmoteID.RPSScissors,
		_ => throw new ArgumentOutOfRangeException(nameof(move))
	};

	public static RpsOutcome Resolve(RpsMove player, RpsMove companion)
	{
		if (!Enum.IsDefined(player)) throw new ArgumentOutOfRangeException(nameof(player));
		if (!Enum.IsDefined(companion)) throw new ArgumentOutOfRangeException(nameof(companion));
		if (player == companion) return RpsOutcome.Draw;
		return ((int)player - (int)companion + 3) % 3 == 1
			? RpsOutcome.PlayerWin : RpsOutcome.CompanionWin;
	}
}
