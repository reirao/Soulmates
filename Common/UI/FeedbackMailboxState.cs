#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Soulmates.Common.Feedback;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace Soulmates.Common.UI;

public sealed class FeedbackMailboxState : UIState
{
	private enum MailKind { Feedback, Bug }

	private MailKind mailKind;
	private MailboxInputElement? input;
	private UIText? context;
	private UIText? status;
	private UITextPanel<string>? feedbackButton;
	private UITextPanel<string>? bugButton;
	private UITextPanel<string>? recordingButton;
	private UITextPanel<string>? sendButton;

	public override void OnInitialize()
	{
		var panel = new UIPanel {
			HAlign = 0.5f,
			VAlign = 0.5f,
			Width = new StyleDimension(560f, 0f),
			Height = new StyleDimension(354f, 0f),
			BackgroundColor = new Color(20, 29, 48) * 0.98f,
			BorderColor = new Color(118, 154, 206)
		};
		Append(panel);

		var title = new UIText(MailboxText("Title", "AETHER MAILBOX", "AETHER-BRIEFKASTEN"), 1.05f, true) {
			HAlign = 0.5f,
			Top = new StyleDimension(8f, 0f)
		};
		panel.Append(title);

		var recipient = new UIText(MailboxText("Recipient", "TO: LOCAL SOULMATES FIELD NOTES",
			"AN: LOKALE SOULMATES FIELD NOTES"), 0.68f) {
			Left = new StyleDimension(18f, 0f),
			Top = new StyleDimension(48f, 0f),
			TextColor = new Color(166, 191, 220)
		};
		panel.Append(recipient);

		feedbackButton = Button(MailboxText("Feedback", "FEEDBACK", "FEEDBACK"), 76f, 18f, 156f, new Color(49, 78, 116));
		feedbackButton.OnLeftClick += (_, _) => SelectKind(MailKind.Feedback);
		panel.Append(feedbackButton);
		bugButton = Button(MailboxText("Bug", "BUG REPORT", "BUGBERICHT"), 76f, 182f, 156f, new Color(111, 63, 72));
		bugButton.OnLeftClick += (_, _) => SelectKind(MailKind.Bug);
		panel.Append(bugButton);

		recordingButton = Button("", 76f, 346f, 196f, new Color(54, 91, 79));
		recordingButton.OnLeftClick += (_, _) => ToggleRecording();
		panel.Append(recordingButton);

		context = new UIText("", 0.66f) {
			Left = new StyleDimension(18f, 0f),
			Top = new StyleDimension(116f, 0f),
			Width = new StyleDimension(-36f, 1f),
			TextColor = Color.LightGray
		};
		panel.Append(context);

		input = new MailboxInputElement(() => SoulmatesText.Get(mailKind == MailKind.Bug
			? "UI.Mailbox.BugPlaceholder" : "UI.Mailbox.FeedbackPlaceholder")) {
			Left = new StyleDimension(18f, 0f),
			Top = new StyleDimension(143f, 0f),
			Width = new StyleDimension(-36f, 1f),
			Height = new StyleDimension(116f, 0f)
		};
		panel.Append(input);

		status = new UIText("", 0.67f) {
			Left = new StyleDimension(18f, 0f),
			Top = new StyleDimension(269f, 0f),
			Width = new StyleDimension(-36f, 1f),
			TextColor = Color.LightGray
		};
		panel.Append(status);

		sendButton = Button(SoulmatesText.Get("UI.Mailbox.Send"), 303f, 18f, 338f, new Color(48, 113, 96));
		sendButton.OnLeftClick += (_, _) => Send();
		panel.Append(sendButton);

		var close = Button(CommonText("UI.Common.Close", "CLOSE", "SCHLIESSEN"), 303f, 364f, 178f, new Color(120, 63, 72));
		close.OnLeftClick += (_, _) => ModContent.GetInstance<FeedbackMailboxSystem>().Close();
		panel.Append(close);
		Refresh();
	}

	public void Prepare()
	{
		mailKind = MailKind.Feedback;
		input?.Clear();
		if (input is not null)
			input.Focused = true;
		SetStatus(SoulmatesText.Get("UI.Mailbox.Ready"), Color.LightGray);
		Refresh();
	}

	private void SelectKind(MailKind selected)
	{
		mailKind = selected;
		if (input is not null)
			input.Focused = true;
		SetStatus(SoulmatesText.Get("UI.Mailbox.Ready"), Color.LightGray);
		Refresh();
		SoundEngine.PlaySound(SoundID.MenuTick);
	}

	private void ToggleRecording()
	{
		bool enabled = !SoulmatesFeedbackSystem.Enabled;
		bool changed = SoulmatesFeedbackSystem.SetEnabled(enabled);
		SetStatus(changed
			? SoulmatesText.Get(enabled ? "UI.Mailbox.Enabled" : "UI.Mailbox.Disabled")
			: SoulmatesText.Get("Feedback.Error", SoulmatesFeedbackSystem.LastError),
			changed ? Color.LightCyan : Color.IndianRed);
		Refresh();
		SoundEngine.PlaySound(changed ? SoundID.MenuTick : SoundID.MenuClose);
	}

	private void Send()
	{
		if (input is null || string.IsNullOrWhiteSpace(input.Text)) {
			SetStatus(SoulmatesText.Get("UI.Mailbox.Empty"), Color.IndianRed);
			SoundEngine.PlaySound(SoundID.MenuClose);
			return;
		}
		if (!SoulmatesFeedbackSystem.Enabled && !SoulmatesFeedbackSystem.SetEnabled(true)) {
			SetStatus(SoulmatesText.Get("Feedback.Error", SoulmatesFeedbackSystem.LastError), Color.IndianRed);
			SoundEngine.PlaySound(SoundID.MenuClose);
			return;
		}
		if (!SoulmatesFeedbackSystem.SessionActive)
			SoulmatesFeedbackSystem.BeginSession(Main.LocalPlayer);

		bool saved;
		if (mailKind == MailKind.Bug)
			saved = SoulmatesFeedbackSystem.RecordBug(input.Text);
		else {
			SoulmatesFeedbackSystem.RecordNote(input.Text);
			SoulmatesFeedbackSystem.Flush();
			saved = string.IsNullOrEmpty(SoulmatesFeedbackSystem.LastError);
		}
		if (!saved) {
			SetStatus(SoulmatesText.Get("Feedback.Error", SoulmatesFeedbackSystem.LastError), Color.IndianRed);
			SoundEngine.PlaySound(SoundID.MenuClose);
			return;
		}

		input.Clear();
		SetStatus(SoulmatesText.Get(mailKind == MailKind.Bug
			? "UI.Mailbox.BugSaved" : "UI.Mailbox.FeedbackSaved"), Color.LightGreen);
		Refresh();
		SoundEngine.PlaySound(SoundID.Chat with { Volume = 0.65f, Pitch = 0.12f });
	}

	private void Refresh()
	{
		if (context is null || feedbackButton is null || bugButton is null || recordingButton is null || sendButton is null)
			return;
		feedbackButton.BackgroundColor = mailKind == MailKind.Feedback ? new Color(69, 109, 157) : new Color(42, 60, 88);
		bugButton.BackgroundColor = mailKind == MailKind.Bug ? new Color(151, 76, 82) : new Color(78, 50, 61);
		context.SetText(SoulmatesText.Get(mailKind == MailKind.Bug
			? "UI.Mailbox.BugContext" : "UI.Mailbox.FeedbackContext"));
		recordingButton.SetText(SoulmatesText.Get(SoulmatesFeedbackSystem.Enabled
			? "UI.Mailbox.RecordingOn" : "UI.Mailbox.RecordingOff"));
		recordingButton.BackgroundColor = SoulmatesFeedbackSystem.Enabled
			? new Color(54, 107, 87) : new Color(83, 69, 75);
		sendButton.SetText(SoulmatesText.Get(SoulmatesFeedbackSystem.Enabled
			? "UI.Mailbox.Send" : "UI.Mailbox.EnableAndSend"));
	}

	private void SetStatus(string text, Color color)
	{
		if (status is null)
			return;
		status.SetText(text);
		status.TextColor = color;
	}

	private static UITextPanel<string> Button(string text, float top, float left, float width, Color color)
	{
		var button = new UITextPanel<string>(text, 0.75f) {
			Top = new StyleDimension(top, 0f),
			Left = new StyleDimension(left, 0f),
			Width = new StyleDimension(width, 0f),
			Height = new StyleDimension(34f, 0f),
			BackgroundColor = color,
			BorderColor = color * 1.3f
		};
		return button;
	}

	private static string MailboxText(string key, string english, string german)
		=> CommonText($"UI.Mailbox.{key}", english, german);

	private static string CommonText(string key, string english, string german)
	{
		string translated = SoulmatesText.Get(key);
		return translated == $"Mods.Soulmates.{key}"
			? Language.ActiveCulture.Name.StartsWith("de", StringComparison.OrdinalIgnoreCase) ? german : english
			: translated;
	}

}

internal sealed class MailboxInputElement(Func<string> placeholder) : UIElement
{
	private const int MaximumLength = 360;
	public string Text { get; private set; } = "";
	public bool Focused { get; set; }

	public override void OnInitialize()
	{
		OnLeftClick += (_, _) => Focused = true;
	}

	public void Clear()
	{
		Text = "";
		Focused = false;
	}

	public override void Update(GameTime gameTime)
	{
		base.Update(gameTime);
		if (IsMouseHovering)
			Main.LocalPlayer.mouseInterface = true;
	}

	protected override void DrawSelf(SpriteBatch spriteBatch)
	{
		base.DrawSelf(spriteBatch);
		CalculatedStyle area = GetDimensions();
		Rectangle box = new((int)area.X, (int)area.Y, (int)area.Width, (int)area.Height);
		Texture2D pixel = TextureAssets.MagicPixel.Value;
		if (Focused) {
			Main.LocalPlayer.mouseInterface = true;
			PlayerInput.WritingText = true;
			Main.instance.HandleIME();
			string next = Main.GetInputText(Text, false).Replace('\r', ' ').Replace('\n', ' ');
			Text = next.Length <= MaximumLength ? next : next[..MaximumLength];
		}
		spriteBatch.Draw(pixel, box, new Color(9, 16, 29) * 0.96f);
		DrawBorder(spriteBatch, pixel, box, Focused ? new Color(133, 192, 225) : new Color(63, 88, 125));

		string displayed = string.IsNullOrEmpty(Text) ? placeholder() : Text;
		if (Focused && (int)(Main.GlobalTimeWrappedHourly * 2f) % 2 == 0)
			displayed += " |";
		string wrapped = FontAssets.MouseText.Value.CreateWrappedText(displayed, (area.Width - 24f) / 0.72f);
		Color color = string.IsNullOrEmpty(Text) ? new Color(121, 139, 164) : Color.White;
		Utils.DrawBorderString(spriteBatch, wrapped, new Vector2(area.X + 12f, area.Y + 10f), color, 0.72f);
		string counter = $"{Text.Length}/{MaximumLength}";
		Vector2 size = FontAssets.MouseText.Value.MeasureString(counter) * 0.52f;
		Utils.DrawBorderString(spriteBatch, counter,
			new Vector2(area.X + area.Width - size.X - 8f, area.Y + area.Height - size.Y - 6f), Color.Gray, 0.52f);
	}

	private static void DrawBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle box, Color color)
	{
		spriteBatch.Draw(pixel, new Rectangle(box.X, box.Y, box.Width, 2), color);
		spriteBatch.Draw(pixel, new Rectangle(box.X, box.Bottom - 2, box.Width, 2), color);
		spriteBatch.Draw(pixel, new Rectangle(box.X, box.Y, 2, box.Height), color);
		spriteBatch.Draw(pixel, new Rectangle(box.Right - 2, box.Y, 2, box.Height), color);
	}
}
