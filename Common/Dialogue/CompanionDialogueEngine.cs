using System;

namespace Soulmates.Common.Dialogue;

public enum TalkCategory
{
	Care,
	Commands,
	Work,
	Bond,
	Voice,
	Pack
}

public enum SpeechAction
{
	None,
	Follow,
	Stay,
	Explore,
	FindTreasure,
	Mine,
	Gather,
	Rest,
	VoiceSoft,
	VoiceDirect,
	VoicePlayful,
	ShowPack,
	StoreHeldItem,
	UnloadPack
}

public readonly record struct DialogueResult(string Reply, bool Accepted, SpeechAction Action, int BondDelta, int MoodDelta, int EnergyDelta);

public static class CompanionDialogueEngine
{
	private static readonly string[][] Options = [
		["How are you feeling?", "You did well today.", "Do you need a rest?"],
		["Please follow me.", "Wait here for me.", "Come explore with me."],
		["Look around for treasure.", "Can you help me mine?", "Gather anything interesting."],
		["Do you trust me?", "Tell me something about you.", "I am glad you are here."],
		["Speak softly with me.", "Be direct with me.", "Be more playful."],
		["What are you carrying?", "Take my selected hotbar item.", "Give me everything you carry."]
	];

	public static string[] GetOptions(TalkCategory category) => Options[(int)category];

	public static DialogueResult Speak(CompanionProfile profile, TalkCategory category, int option)
	{
		option = Math.Clamp(option, 0, 2);
		if (category == TalkCategory.Voice)
			return ConfigureVoice(profile, option);
		if (category == TalkCategory.Pack)
			return Pack(option);

		bool demanding = category is TalkCategory.Commands or TalkCategory.Work;
		int requiredEnergy = category == TalkCategory.Work ? 12 : 3;
		int requiredMood = category == TalkCategory.Work ? 20 : 10;
		if (profile.Personality == CompanionPersonality.Loyal)
			requiredMood -= 5;
		if (profile.Personality == CompanionPersonality.Brave)
			requiredEnergy -= 2;
		if (demanding && (profile.Energy < requiredEnergy || profile.Mood < requiredMood))
			return Refusal(profile, category, requiredEnergy);

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
		0 => new DialogueResult(Styled(profile, "I will listen for things that glitter.", "Beginning treasure sweep.", "If it shines, I am claiming first look."), true, SpeechAction.FindTreasure, 1, 1, -6),
		1 => new DialogueResult(Styled(profile, "Show me the stone. I will work carefully.", "Mining task accepted.", "Time to negotiate with the rocks."), true, SpeechAction.Mine, 1, 0, -8),
		_ => new DialogueResult(Styled(profile, "I will bring back what was left behind.", "Gathering sweep started.", "Interesting is a wonderfully dangerous category."), true, SpeechAction.Gather, 1, 1, -5)
	};

	private static DialogueResult Bond(CompanionProfile profile, int option) => option switch {
		0 => new DialogueResult(Styled(profile, profile.Bond >= 20 ? "More each day." : "I think trust grows when we keep choosing each other.", profile.Bond >= 20 ? "Trust confirmed." : "Trust is forming.", profile.Bond >= 20 ? "Enough to follow you underground. That is serious." : "Ask me again after fewer lava incidents."), true, SpeechAction.None, 2, 1, 0),
		1 => new DialogueResult(profile.JobsCompleted > 0 ? profile.LastMemory : PersonalitySecret(profile), true, SpeechAction.None, 2, 2, 0),
		_ => new DialogueResult(Styled(profile, "And I am glad you called me into being.", "The bond is mutual.", "Good. You are keeping me."), true, SpeechAction.None, 3, 4, 0)
	};

	private static DialogueResult Refusal(CompanionProfile profile, TalkCategory category, int requiredEnergy)
	{
		string reply = profile.Energy < requiredEnergy
			? Styled(profile, "Not yet. I need to rest first.", "No. Energy too low.", "My soul says yes. The rest of me says nap.")
			: Styled(profile, "I do not feel like doing that right now.", "Mood too low. Request declined.", "Nope. A little kindness first.");
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

	private static DialogueResult Pack(int option) => option switch {
		0 => new DialogueResult("Let me check.", true, SpeechAction.ShowPack, 0, 0, 0),
		1 => new DialogueResult("I will keep it safe.", true, SpeechAction.StoreHeldItem, 0, 0, 0),
		_ => new DialogueResult("Here. Everything is accounted for.", true, SpeechAction.UnloadPack, 0, 0, 0)
	};

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
