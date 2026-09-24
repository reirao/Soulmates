using System;
using Terraria;

namespace Soulmates.Common.Dialogue;

public enum TalkCategory
{
	Care,
	Commands,
	Work,
	Bond,
	Voice
}

public enum SpeechAction
{
	None,
	Follow,
	Stay,
	Explore,
	Rest,
	VoiceSoft,
	VoiceDirect,
	VoicePlayful
}

public readonly record struct DialogueResult(string Reply, bool Accepted, SpeechAction Action, int BondDelta, int MoodDelta, int EnergyDelta);

public static class CompanionDialogueEngine
{
	private static readonly string[][] Options = [
		["How are you feeling?", "You did well today.", "Do you need a rest?"],
		["Please follow me.", "Wait here for me.", "Come explore with me."],
		["Look around for treasure.", "Can you help me mine?", "Gather anything interesting."],
		["Do you trust me?", "Tell me something about you.", "I am glad you are here."],
		["Speak softly with me.", "Be direct with me.", "Be more playful."]
	];

	public static string[] GetOptions(TalkCategory category) => Options[(int)category];

	public static DialogueResult Speak(CompanionProfile profile, TalkCategory category, int option)
	{
		option = Math.Clamp(option, 0, 2);
		if (category == TalkCategory.Voice)
			return ConfigureVoice(profile, option);

		bool demanding = category is TalkCategory.Commands or TalkCategory.Work;
		int refusalChance = demanding ? 18 : 2;
		refusalChance += Math.Max(0, 45 - profile.Energy) / 2;
		refusalChance += Math.Max(0, 40 - profile.Mood) / 3;
		refusalChance -= Math.Min(15, profile.Bond / 4);
		refusalChance += profile.Personality == CompanionPersonality.Mischievous ? 8 : 0;
		refusalChance -= profile.Personality == CompanionPersonality.Loyal ? 8 : 0;

		if (demanding && Main.rand.Next(100) < Math.Clamp(refusalChance, 4, 70))
			return Refusal(profile, category);

		return category switch {
			TalkCategory.Care => Care(profile, option),
			TalkCategory.Commands => Command(profile, option),
			TalkCategory.Work => Work(profile, option),
			TalkCategory.Bond => Bond(profile, option),
			_ => new DialogueResult("...", true, SpeechAction.None, 0, 0, 0)
		};
	}

	private static DialogueResult Care(CompanionProfile profile, int option) => option switch {
		0 => new DialogueResult(Styled(profile, "I feel safe near you.", "Mood stable. Energy noted.", "Still sparkling. Mostly."), true, SpeechAction.None, 1, 1, 0),
		1 => new DialogueResult(Styled(profile, "Thank you. I will remember that.", "Acknowledged. I did well.", "Obviously. But say it again."), true, SpeechAction.None, 2, 4, 0),
		_ => new DialogueResult(Styled(profile, "A quiet moment sounds lovely.", "Yes. Rest would help.", "Only if naps count as important work."), true, SpeechAction.Rest, 1, 2, 12)
	};

	private static DialogueResult Command(CompanionProfile profile, int option) => option switch {
		0 => new DialogueResult(Styled(profile, "I will stay close.", "Following.", "Lead on, hero."), true, SpeechAction.Follow, 1, 0, -1),
		1 => new DialogueResult(Styled(profile, "I will wait for your return.", "Holding position.", "I shall guard this extremely important patch of air."), true, SpeechAction.Stay, 0, 0, 0),
		_ => new DialogueResult(Styled(profile, "Let us see what the world is hiding.", "Exploration accepted.", "Adventure first. Sensible decisions later."), true, SpeechAction.Explore, 1, 2, -4)
	};

	private static DialogueResult Work(CompanionProfile profile, int option) => option switch {
		0 => new DialogueResult(Styled(profile, "I will listen for things that glitter.", "Beginning treasure sweep.", "If it shines, I am claiming first look."), true, SpeechAction.Explore, 1, 1, -6),
		1 => new DialogueResult(Styled(profile, "I can scout the stone, but I am still learning to mine it.", "Mining assistance queued. Tool use is still developing.", "I can judge the rocks very sternly for now."), true, SpeechAction.Explore, 1, 0, -5),
		_ => new DialogueResult(Styled(profile, "I will look for anything worth bringing home.", "Gathering sweep started.", "Interesting is a wonderfully dangerous category."), true, SpeechAction.Explore, 1, 1, -5)
	};

	private static DialogueResult Bond(CompanionProfile profile, int option) => option switch {
		0 => new DialogueResult(Styled(profile, profile.Bond >= 20 ? "More each day." : "I think trust grows when we keep choosing each other.", profile.Bond >= 20 ? "Trust confirmed." : "Trust is forming.", profile.Bond >= 20 ? "Enough to follow you underground. That is serious." : "Ask me again after fewer lava incidents."), true, SpeechAction.None, 2, 1, 0),
		1 => new DialogueResult(PersonalitySecret(profile), true, SpeechAction.None, 2, 2, 0),
		_ => new DialogueResult(Styled(profile, "And I am glad you called me into being.", "The bond is mutual.", "Good. You are keeping me."), true, SpeechAction.None, 3, 4, 0)
	};

	private static DialogueResult Refusal(CompanionProfile profile, TalkCategory category)
	{
		string reply = profile.Energy < 35
			? Styled(profile, "Not yet. I need to rest first.", "No. Energy too low.", "My soul says yes. The rest of me says nap.")
			: profile.Mood < 35
				? Styled(profile, "I do not feel like doing that right now.", "Request declined.", "Nope. Ask me nicely later.")
				: Styled(profile, "Could we do something else instead?", "Not now.", "Counter-offer: absolutely anything else.");
		return new DialogueResult(reply, false, SpeechAction.None, category == TalkCategory.Commands ? -1 : 0, -1, 0);
	}

	private static DialogueResult ConfigureVoice(CompanionProfile profile, int option)
	{
		SpeechAction action = option switch {
			0 => SpeechAction.VoiceSoft,
			1 => SpeechAction.VoiceDirect,
			_ => SpeechAction.VoicePlayful
		};
		string reply = option switch {
			0 => "I will choose gentler words.",
			1 => "Understood. I will be clear.",
			_ => "Oh, this is going to be fun."
		};
		return new DialogueResult(reply, true, action, 1, 1, 0);
	}

	private static string PersonalitySecret(CompanionProfile profile) => profile.Personality switch {
		CompanionPersonality.Curious => Styled(profile, "Sometimes I wonder whether stars dream about us.", "I study everything when you are not looking.", "I have questions about every chest. Especially locked ones."),
		CompanionPersonality.Loyal => Styled(profile, "I always know which footsteps are yours.", "I track your position constantly.", "I would recognize your chaos anywhere."),
		CompanionPersonality.Brave => Styled(profile, "I get frightened too. I simply move with you anyway.", "Fear does not alter the objective.", "I am fearless, except around suspiciously quiet caves."),
		CompanionPersonality.Gentle => Styled(profile, "I like the quiet after the rain.", "Peace improves focus.", "My secret is that I name the slimes."),
		_ => Styled(profile, "I sometimes move your things when you are not looking.", "I have relocated several objects.", "That missing torch? No idea.")
	};

	private static string Styled(CompanionProfile profile, string soft, string direct, string playful) => profile.Voice switch {
		CompanionVoice.Direct => direct,
		CompanionVoice.Playful => playful,
		_ => soft
	};
}
