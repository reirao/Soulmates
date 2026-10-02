#nullable enable
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Common.Feedback;
using Terraria;
using Terraria.ID;

namespace Soulmates.Content.NPCs;

public sealed partial class SoulboundCompanion
{
	private int rpsCooldown;

	public bool PlayRockPaperScissors(RpsMove playerMove)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || !System.Enum.IsDefined(playerMove)
			|| rpsCooldown > 0 || !NPC.active || !TryGetOwner(out Player owner) || owner.dead
			|| FindBoundSigil() is null || HasPendingQuestion || HasPendingInitiative
			|| Vector2.DistanceSquared(NPC.Center, owner.Center) > 480f * 480f)
			return false;

		// Draw independently of the player's move; only authority resolves a round.
		RpsMove companionMove = (RpsMove)Main.rand.Next(3);
		rpsCooldown = 180;
		nativeEmoteReactionCooldown = 180;
		RpsOutcome outcome = CompanionRps.Resolve(playerMove, companionMove);
		StartEmote(outcome == RpsOutcome.CompanionWin ? CompanionEmote.Cheer : CompanionEmote.Wave, 180);
		ShowNativeEmote(CompanionRps.Emote(companionMove), 180);
		ShowRpsResult(playerMove, companionMove);
		if (Main.netMode == NetmodeID.Server)
			global::Soulmates.Soulmates.SendRpsResult(owner, this, playerMove, companionMove);
		SoulmatesFeedbackSystem.Record("rps_round", ("player_move", playerMove.ToString()),
			("companion_move", companionMove.ToString()), ("outcome", outcome.ToString()));
		return true;
	}

	public void ShowRpsResult(RpsMove playerMove, RpsMove companionMove)
	{
		if (!System.Enum.IsDefined(playerMove) || !System.Enum.IsDefined(companionMove)) return;
		RpsOutcome outcome = CompanionRps.Resolve(playerMove, companionMove);
		string text = SoulmatesText.Get("Games.Rps.Round", SoulmatesText.Get($"Games.Rps.Moves.{playerMove}"),
			SoulmatesText.Get($"Games.Rps.Moves.{companionMove}"), SoulmatesText.Get($"Games.Rps.Outcomes.{outcome}"));
		if (Profile.Voice != CompanionVoice.Direct)
			text += " " + SoulmatesText.Get(Profile.Energy < 5 || Profile.Mood < 15
				? "Games.Rps.Quiet" : $"Games.Rps.Replies.{outcome}.{Profile.Personality}");
		ShowSpeech(text);
	}
}
