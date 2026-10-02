using System;

namespace Soulmates.Common.Dialogue;

public enum TalkCategory
{
	Care,
	Commands,
	Work,
	Bond,
	Voice,
	Pack,
	Items
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
	UnloadPack,
	RecallMemory,
	RecallResident
}

public readonly record struct DialogueResult(string Reply, bool Accepted, SpeechAction Action, int BondDelta, int MoodDelta, int EnergyDelta);
public readonly record struct CompanionConversationResult(string Reply, bool Accepted);

public static class CompanionDialogueEngine
{
	public static string ObserveTogether(CompanionProfile profile, string factKey, params object[] args)
	{
		string fact = SoulmatesText.Get(factKey, args);
		if (profile.Voice == CompanionVoice.Direct) return fact;
		string replyKey = profile.Energy < 5 ? "TargetOrders.Noticing.Rest"
			: profile.Mood < 15 ? "TargetOrders.Noticing.Quiet"
			: $"TargetOrders.Noticing.{profile.Voice}.{profile.Personality}";
		return fact + " " + SoulmatesText.Get(replyKey);
	}

	public static string[] GetOptions(TalkCategory category) => [
		SoulmatesText.Get($"Dialogue.Options.{category}.Option0"),
		SoulmatesText.Get($"Dialogue.Options.{category}.Option1"),
		SoulmatesText.Get($"Dialogue.Options.{category}.Option2")
	];

	public static int GetEnergyChange(TalkCategory category, int option) => (category, Math.Clamp(option, 0, 2)) switch {
		(TalkCategory.Care, 2) => 30,
		(TalkCategory.Commands, 0) => -1,
		(TalkCategory.Commands, 2) => -4,
		(TalkCategory.Work, 0) => -6,
		(TalkCategory.Work, 1) => -8,
		(TalkCategory.Work, 2) => -5,
		_ => 0
	};

	public static DialogueResult Speak(CompanionProfile profile, TalkCategory category, int option)
	{
		option = Math.Clamp(option, 0, 2);
		if (category == TalkCategory.Voice)
			return ConfigureVoice(profile, option);
		if (category == TalkCategory.Pack)
			return Pack(option);

		bool demanding = category is TalkCategory.Commands or TalkCategory.Work;
		int requiredEnergy = Math.Max(1, -GetEnergyChange(category, option));
		int requiredMood = category == TalkCategory.Work ? 20 : 10;
		if (profile.Personality == CompanionPersonality.Loyal)
			requiredMood -= 5;
		if (profile.Personality == CompanionPersonality.Brave)
			requiredEnergy = Math.Max(1, requiredEnergy - 1);
		if (demanding && (profile.Energy < requiredEnergy || profile.Mood < requiredMood))
			return Refusal(profile, category, requiredEnergy, requiredMood);

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
		_ => new DialogueResult(Styled(profile, "Dialogue.Replies.Care.Rest"), true, SpeechAction.Rest, 1, 2,
			GetEnergyChange(TalkCategory.Care, 2))
	};

	private static DialogueResult Command(CompanionProfile profile, int option) => option switch {
		0 => new DialogueResult(Styled(profile, "Dialogue.Replies.Commands.Follow"), true, SpeechAction.Follow, 1, 0,
			GetEnergyChange(TalkCategory.Commands, 0)),
		1 => new DialogueResult(Styled(profile, "Dialogue.Replies.Commands.Stay"), true, SpeechAction.Stay, 0, 0, 0),
		_ => new DialogueResult(Styled(profile, "Dialogue.Replies.Commands.Explore"), true, SpeechAction.Explore, 1, 2,
			GetEnergyChange(TalkCategory.Commands, 2))
	};

	private static DialogueResult Work(CompanionProfile profile, int option) => option switch {
		0 => new DialogueResult(Styled(profile, "Dialogue.Replies.Work.Treasure"), true, SpeechAction.FindTreasure, 1, 1,
			GetEnergyChange(TalkCategory.Work, 0)),
		1 => new DialogueResult(Styled(profile, "Dialogue.Replies.Work.Mine"), true, SpeechAction.Mine, 1, 0,
			GetEnergyChange(TalkCategory.Work, 1)),
		_ => new DialogueResult(Styled(profile, "Dialogue.Replies.Work.Gather"), true, SpeechAction.Gather, 1, 1,
			GetEnergyChange(TalkCategory.Work, 2))
	};

	private static DialogueResult Bond(CompanionProfile profile, int option) => option switch {
		0 => new DialogueResult(Styled(profile, profile.Bond >= 20 ? "Dialogue.Replies.Bond.TrustHigh" : "Dialogue.Replies.Bond.TrustLow"), true, SpeechAction.None, 2, 1, 0),
		1 => new DialogueResult("", true, SpeechAction.RecallMemory, 0, 0, 0),
		_ => new DialogueResult("", true, SpeechAction.RecallResident, 0, 0, 0)
	};

	private static DialogueResult Refusal(CompanionProfile profile, TalkCategory category, int requiredEnergy, int requiredMood)
	{
		bool lacksEnergy = profile.Energy < requiredEnergy;
		string reply = Styled(profile, lacksEnergy ? "Dialogue.Replies.Refusal.Energy" : "Dialogue.Replies.Refusal.Mood");
		reply += " " + SoulmatesText.Get(lacksEnergy
			? "Dialogue.Replies.Refusal.EnergyStatus"
			: "Dialogue.Replies.Refusal.MoodStatus", lacksEnergy ? profile.Energy : profile.Mood,
			lacksEnergy ? requiredEnergy : requiredMood);
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

	private static string Styled(CompanionProfile profile, string key) => SoulmatesText.Get($"{key}.{profile.Voice}");
}
