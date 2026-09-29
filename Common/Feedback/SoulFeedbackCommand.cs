#nullable enable
using System;
using Microsoft.Xna.Framework;
using Soulmates.Common.UI;
using Terraria.ModLoader;

namespace Soulmates.Common.Feedback;

public sealed class SoulFeedbackCommand : ModCommand
{
	public override CommandType Type => CommandType.Chat;
	public override string Command => "soulfeedback";
	public override string Usage => SoulmatesText.Get("Feedback.Usage");
	public override string Description => SoulmatesText.Get("Feedback.Description");

	public override void Action(CommandCaller caller, string input, string[] args)
	{
		string operation = args.Length == 0 ? "open" : args[0].ToLowerInvariant();
		switch (operation) {
			case "open":
				ModContent.GetInstance<FeedbackMailboxSystem>().Open();
				break;
			case "on":
				Reply(caller, SoulmatesFeedbackSystem.SetEnabled(true)
					? SoulmatesText.Get("Feedback.Enabled", SoulmatesFeedbackSystem.FeedbackFolder)
					: SoulmatesText.Get("Feedback.Error", SoulmatesFeedbackSystem.LastError));
				break;
			case "off":
				Reply(caller, SoulmatesFeedbackSystem.SetEnabled(false)
					? SoulmatesText.Get("Feedback.Disabled")
					: SoulmatesText.Get("Feedback.Error", SoulmatesFeedbackSystem.LastError));
				break;
			case "status":
				Reply(caller, SoulmatesText.Get(SoulmatesFeedbackSystem.Enabled
					? "Feedback.StatusOn" : "Feedback.StatusOff", SoulmatesFeedbackSystem.FeedbackFolder));
				break;
			case "flush":
				SoulmatesFeedbackSystem.Flush();
				Reply(caller, string.IsNullOrEmpty(SoulmatesFeedbackSystem.LastError)
					? SoulmatesText.Get("Feedback.Flushed", SoulmatesFeedbackSystem.FeedbackFolder)
					: SoulmatesText.Get("Feedback.Error", SoulmatesFeedbackSystem.LastError));
				break;
			case "note":
				string note = args.Length > 1 ? string.Join(' ', args[1..]).Trim() : "";
				if (!SoulmatesFeedbackSystem.Enabled)
					Reply(caller, SoulmatesText.Get("Feedback.NoteDisabled"));
				else if (string.IsNullOrWhiteSpace(note))
					Reply(caller, SoulmatesText.Get("Feedback.NoteMissing"));
				else {
					if (!SoulmatesFeedbackSystem.SessionActive)
						SoulmatesFeedbackSystem.BeginSession(caller.Player);
					SoulmatesFeedbackSystem.RecordNote(note);
					SoulmatesFeedbackSystem.Flush();
					Reply(caller, SoulmatesText.Get("Feedback.NoteSaved"));
				}
				break;
			case "bug":
				string bug = args.Length > 1 ? string.Join(' ', args[1..]).Trim() : "";
				if (!SoulmatesFeedbackSystem.Enabled)
					Reply(caller, SoulmatesText.Get("Feedback.NoteDisabled"));
				else if (string.IsNullOrWhiteSpace(bug))
					Reply(caller, SoulmatesText.Get("Feedback.BugMissing"));
				else {
					if (!SoulmatesFeedbackSystem.SessionActive)
						SoulmatesFeedbackSystem.BeginSession(caller.Player);
					Reply(caller, SoulmatesFeedbackSystem.RecordBug(bug)
						? SoulmatesText.Get("Feedback.BugSaved")
						: SoulmatesText.Get("Feedback.Error", SoulmatesFeedbackSystem.LastError));
				}
				break;
			default:
				throw new UsageException(Usage);
		}
	}

	private static void Reply(CommandCaller caller, string text)
		=> caller.Reply(text, Color.Lerp(Color.LightCyan, Color.White, 0.25f));
}
