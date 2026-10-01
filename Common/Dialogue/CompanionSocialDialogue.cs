using Soulmates.Common;
using Terraria.ID;
using Terraria.GameContent.UI;

namespace Soulmates.Common.Dialogue;

public readonly record struct SocialReply(int Emote, CompanionEmote Gesture, int Affinity, string Key);

public static class CompanionSocialDialogue
{
	public static string ReplyKey(SocialReply reply, CompanionPersonality personality)
		=> reply.Key is "Affection" or "Joy" or "Comfort" or "Anger"
			? $"Social.Residents.Replies.{reply.Key}.{personality}"
			: $"Social.Residents.Shared.{reply.Key}";

	public static SocialReply Respond(int emote, CompanionPersonality personality) => emote switch {
		EmoteID.EmotionLove or EmoteID.EmoteKiss or EmoteID.EmoteWink => new(
			EmoteID.EmotionLove, CompanionEmote.Heart, 3, "Affection"),
		EmoteID.EmoteHappiness or EmoteID.EmoteLaugh or EmoteID.EmoteSilly => new(
			personality == CompanionPersonality.Mischievous ? EmoteID.EmoteLaugh : EmoteID.EmoteHappiness,
			CompanionEmote.Cheer, 2, "Joy"),
		EmoteID.EmotionCry or EmoteID.EmoteSadness or EmoteID.EmoteFear => new(
			EmoteID.EmotionLove, CompanionEmote.Comfort, 2, "Comfort"),
		EmoteID.EmotionAnger or EmoteID.EmoteAnger or EmoteID.EmoteFight or EmoteID.EmoteScowl => new(
			personality == CompanionPersonality.Brave ? EmoteID.EmoteScowl : EmoteID.EmoteConfused,
			CompanionEmote.Comfort, personality == CompanionPersonality.Brave ? -1 : -3, "Anger"),
		EmoteID.MiscTree => new(EmoteID.MiscTree, CompanionEmote.Wave, 1, "Nature"),
		EmoteID.ItemPickaxe => new(EmoteID.ItemPickaxe, CompanionEmote.Cheer, 1, "Mining"),
		EmoteID.ItemGoldpile => new(EmoteID.EmoteWink, CompanionEmote.Wave, 1, "Trade"),
		EmoteID.EmoteSleep => new(EmoteID.EmoteSleep, CompanionEmote.Rest, 0, "Rest"),
		EmoteID.EmoteConfused or EmoteID.EmotionAlert => new(
			personality == CompanionPersonality.Curious ? EmoteID.EmoteConfused : EmoteID.EmoteWink,
			CompanionEmote.Wave, 1, "Question"),
		_ => new(EmoteID.EmoteWink, CompanionEmote.Wave, 0, "Listening")
	};

	public static string ResidentRole(int type) => type switch {
		NPCID.Guide => "Guide", NPCID.Nurse => "Nurse", NPCID.Merchant => "Merchant",
		NPCID.Dryad => "Dryad", NPCID.BestiaryGirl => "Zoologist",
		NPCID.ArmsDealer or NPCID.Demolitionist => "Defender",
		NPCID.GoblinTinkerer or NPCID.Mechanic => "Maker",
		_ => "Neighbor"
	};

	public static int ResidentGreetingEmote(int type) => ResidentRole(type) switch {
		"Dryad" or "Zoologist" => EmoteID.MiscTree,
		"Merchant" => EmoteID.ItemGoldpile, "Nurse" => EmoteID.EmotionLove,
		"Defender" => EmoteID.EmoteFight, "Maker" => EmoteID.ItemPickaxe,
		_ => EmoteID.EmoteWink
	};
}
