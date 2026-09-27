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
	public static string[] GetOptions(TalkCategory category) => [
		SoulmatesText.Get($"Dialogue.Options.{category}.Option0"),
		SoulmatesText.Get($"Dialogue.Options.{category}.Option1"),
		SoulmatesText.Get($"Dialogue.Options.{category}.Option2")
	];

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
			_ => new DialogueResult(SoulmatesText.Get("Dialogue.Replies.Fallback"), true, SpeechAction.None, 0, 0, 0)
		};
	}

	private static DialogueResult Care(CompanionProfile profile, int option) => option switch {
		0 => new DialogueResult(Styled(profile, "Dialogue.Replies.Care.Feeling"), true, SpeechAction.None, 1, 1, 0),
		1 => new DialogueResult(Styled(profile, "Dialogue.Replies.Care.Praise"), true, SpeechAction.None, 2, 4, 0),
		_ => new DialogueResult(Styled(profile, "Dialogue.Replies.Care.Rest"), true, SpeechAction.Rest, 1, 2, 12)
	};

	private static DialogueResult Command(CompanionProfile profile, int option) => option switch {
		0 => new DialogueResult(Styled(profile, "Dialogue.Replies.Commands.Follow"), true, SpeechAction.Follow, 1, 0, -1),
		1 => new DialogueResult(Styled(profile, "Dialogue.Replies.Commands.Stay"), true, SpeechAction.Stay, 0, 0, 0),
		_ => new DialogueResult(Styled(profile, "Dialogue.Replies.Commands.Explore"), true, SpeechAction.Explore, 1, 2, -4)
	};

	private static DialogueResult Work(CompanionProfile profile, int option) => option switch {
		0 => new DialogueResult(Styled(profile, "Dialogue.Replies.Work.Treasure"), true, SpeechAction.FindTreasure, 1, 1, -6),
		1 => new DialogueResult(Styled(profile, "Dialogue.Replies.Work.Mine"), true, SpeechAction.Mine, 1, 0, -8),
		_ => new DialogueResult(Styled(profile, "Dialogue.Replies.Work.Gather"), true, SpeechAction.Gather, 1, 1, -5)
	};

	private static DialogueResult Bond(CompanionProfile profile, int option) => option switch {
		0 => new DialogueResult(Styled(profile, profile.Bond >= 20 ? "Dialogue.Replies.Bond.TrustHigh" : "Dialogue.Replies.Bond.TrustLow"), true, SpeechAction.None, 2, 1, 0),
		1 => new DialogueResult(profile.JobsCompleted > 0 ? profile.LastMemory : PersonalitySecret(profile), true, SpeechAction.None, 2, 2, 0),
		_ => new DialogueResult(Styled(profile, "Dialogue.Replies.Bond.Glad"), true, SpeechAction.None, 3, 4, 0)
	};

	private static DialogueResult Refusal(CompanionProfile profile, TalkCategory category, int requiredEnergy)
	{
		string reply = Styled(profile, profile.Energy < requiredEnergy
			? "Dialogue.Replies.Refusal.Energy"
			: "Dialogue.Replies.Refusal.Mood");
		return new DialogueResult(reply, false, SpeechAction.None, category == TalkCategory.Commands ? -1 : 0, -1, 0);
	}

	private static DialogueResult ConfigureVoice(CompanionProfile profile, int option)
	{
		SpeechAction action = option switch {
			0 => SpeechAction.VoiceSoft,
			1 => SpeechAction.VoiceDirect,
			_ => SpeechAction.VoicePlayful
		};
		string reply = SoulmatesText.Get($"Dialogue.Replies.Voice.Option{option}");
		return new DialogueResult(reply, true, action, 1, 1, 0);
	}

	private static DialogueResult Pack(int option) => option switch {
		0 => new DialogueResult(SoulmatesText.Get("Dialogue.Replies.Pack.Show"), true, SpeechAction.ShowPack, 0, 0, 0),
		1 => new DialogueResult(SoulmatesText.Get("Dialogue.Replies.Pack.Store"), true, SpeechAction.StoreHeldItem, 0, 0, 0),
		_ => new DialogueResult(SoulmatesText.Get("Dialogue.Replies.Pack.Unload"), true, SpeechAction.UnloadPack, 0, 0, 0)
	};

	private static string PersonalitySecret(CompanionProfile profile) => profile.Personality switch {
		CompanionPersonality.Curious => Styled(profile, "Dialogue.Replies.Secrets.Curious"),
		CompanionPersonality.Loyal => Styled(profile, "Dialogue.Replies.Secrets.Loyal"),
		CompanionPersonality.Brave => Styled(profile, "Dialogue.Replies.Secrets.Brave"),
		CompanionPersonality.Gentle => Styled(profile, "Dialogue.Replies.Secrets.Gentle"),
		_ => Styled(profile, "Dialogue.Replies.Secrets.Mischievous")
	};

	private static string Styled(CompanionProfile profile, string key) => SoulmatesText.Get($"{key}.{profile.Voice}");
}
