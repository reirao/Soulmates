using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Soulmates.Common.Dialogue;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Soulmates.Common.UI;

public sealed class TalkModeState : UIState
{
	private readonly List<UITextPanel<string>> optionButtons = [];
	private SoulboundSigil? sigil;
	private SoulboundCompanion? companion;
	private TalkCategory category;
	private UIText? response;
	private UIText? stats;

	public override void OnInitialize()
	{
		var panel = new UIPanel {
			HAlign = 0.5f,
			VAlign = 0.5f,
			Width = new StyleDimension(740f, 0f),
			Height = new StyleDimension(500f, 0f),
			BackgroundColor = new Color(20, 29, 48) * 0.98f,
			BorderColor = new Color(118, 154, 206)
		};
		Append(panel);

		panel.Append(new UIText("TALK MODE", 1.1f, true) {
			HAlign = 0.5f,
			Top = new StyleDimension(10f, 0f)
		});

		var portrait = new UIPanel {
			Left = new StyleDimension(18f, 0f),
			Top = new StyleDimension(52f, 0f),
			Width = new StyleDimension(260f, 0f),
			Height = new StyleDimension(368f, 0f),
			BackgroundColor = new Color(10, 17, 31),
			BorderColor = new Color(67, 93, 133)
		};
		panel.Append(portrait);
		portrait.Append(new SoulPreviewElement(() => sigil?.Profile ?? new CompanionProfile()) {
			Left = new StyleDimension(8f, 0f),
			Top = new StyleDimension(5f, 0f),
			Width = new StyleDimension(-16f, 1f),
			Height = new StyleDimension(215f, 0f)
		});

		response = new UIText("...", 0.86f) {
			Left = new StyleDimension(12f, 0f),
			Top = new StyleDimension(225f, 0f),
			Width = new StyleDimension(-24f, 1f),
			Height = new StyleDimension(85f, 0f),
			TextOriginX = 0.5f,
			HAlign = 0.5f,
			IsWrapped = true
		};
		portrait.Append(response);

		stats = new UIText("", 0.75f) {
			Left = new StyleDimension(8f, 0f),
			Top = new StyleDimension(325f, 0f),
			Width = new StyleDimension(-16f, 1f),
			TextOriginX = 0.5f,
			HAlign = 0.5f,
			TextColor = Color.LightGray
		};
		portrait.Append(stats);

		string[] categories = Enum.GetNames<TalkCategory>();
		for (int i = 0; i < categories.Length; i++) {
			TalkCategory chosen = (TalkCategory)i;
			var button = Button(categories[i].ToUpperInvariant(), 55f, 296f + i * 84f, 78f, new Color(54, 71, 105), 0.68f);
			button.OnLeftClick += (_, _) => SelectCategory(chosen);
			panel.Append(button);
		}

		for (int i = 0; i < 3; i++) {
			int option = i;
			var button = Button("", 118f + i * 76f, 296f, 408f, new Color(43, 64, 98), 0.82f);
			button.OnLeftClick += (_, _) => Speak(option);
			optionButtons.Add(button);
			panel.Append(button);
		}

		var close = Button("CLOSE", 435f, 296f, 408f, new Color(120, 63, 72), 0.8f);
		close.OnLeftClick += (_, _) => ModContent.GetInstance<TalkModeSystem>().Close();
		panel.Append(close);
	}

	public void Bind(SoulboundSigil boundSigil, SoulboundCompanion boundCompanion)
	{
		sigil = boundSigil;
		companion = boundCompanion;
		category = TalkCategory.Care;
		if (response is not null) {
			response.SetText($"{sigil.Profile.Name} looks at you expectantly.");
			response.TextColor = sigil.Profile.EssenceColor;
		}
		RefreshOptions();
		RefreshStats();
	}

	private void SelectCategory(TalkCategory selected)
	{
		category = selected;
		SoundEngine.PlaySound(SoundID.MenuTick);
		RefreshOptions();
	}

	private void Speak(int option)
	{
		if (sigil is null || companion is null || !companion.NPC.active) {
			ModContent.GetInstance<TalkModeSystem>().Close();
			return;
		}

		CompanionProfile profile = sigil.Profile;
		DialogueResult result = CompanionDialogueEngine.Speak(profile, category, option);
		profile.Bond = Math.Clamp(profile.Bond + result.BondDelta, 0, 100);
		profile.Mood = Math.Clamp(profile.Mood + result.MoodDelta, 0, 100);
		profile.Energy = Math.Clamp(profile.Energy + result.EnergyDelta, 0, 100);
		ApplyAction(profile, result.Action);
		companion.Profile = profile.Clone();
		companion.NPC.netUpdate = true;

		if (response is not null) {
			response.SetText($"\"{result.Reply}\"");
			response.TextColor = result.Accepted ? profile.EssenceColor : Color.IndianRed;
		}
		SoundEngine.PlaySound(result.Accepted ? SoundID.Chat : SoundID.MenuClose);
		RefreshStats();
	}

	private void ApplyAction(CompanionProfile profile, SpeechAction action)
	{
		if (companion is null)
			return;

		switch (action) {
			case SpeechAction.Follow:
				companion.SetCommand(stay: false);
				break;
			case SpeechAction.Stay:
			case SpeechAction.Rest:
				companion.SetCommand(stay: true);
				break;
			case SpeechAction.Explore:
				companion.AskToExplore();
				break;
			case SpeechAction.VoiceSoft:
				profile.Voice = CompanionVoice.Soft;
				break;
			case SpeechAction.VoiceDirect:
				profile.Voice = CompanionVoice.Direct;
				break;
			case SpeechAction.VoicePlayful:
				profile.Voice = CompanionVoice.Playful;
				break;
		}
	}

	private void RefreshOptions()
	{
		string[] options = CompanionDialogueEngine.GetOptions(category);
		for (int i = 0; i < optionButtons.Count; i++)
			optionButtons[i].SetText($"\"{options[i]}\"");
	}

	private void RefreshStats()
	{
		if (stats is null || sigil is null)
			return;
		CompanionProfile profile = sigil.Profile;
		stats.SetText($"Bond {profile.Bond}  |  Mood {profile.Mood}\nEnergy {profile.Energy}  |  Voice {profile.Voice}");
	}

	private static UITextPanel<string> Button(string text, float top, float left, float width, Color color, float scale)
	{
		var button = new UITextPanel<string>(text, scale) {
			Top = new StyleDimension(top, 0f),
			Left = new StyleDimension(left, 0f),
			Width = new StyleDimension(width, 0f),
			Height = new StyleDimension(52f, 0f),
			BackgroundColor = color,
			BorderColor = color * 1.35f
		};
		button.OnMouseOver += (_, _) => button.BackgroundColor = color * 1.25f;
		button.OnMouseOut += (_, _) => button.BackgroundColor = color;
		return button;
	}
}
