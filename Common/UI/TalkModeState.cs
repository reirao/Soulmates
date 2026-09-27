using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Soulmates.Common.Dialogue;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Soulmates.Common.UI;

public sealed class TalkModeState : UIState
{
	private readonly List<UITextPanel<string>> optionButtons = [];
	private readonly List<(TalkCategory Category, UITextPanel<string> Button)> categoryButtons = [];
	private SoulboundSigil? sigil;
	private SoulboundCompanion? companion;
	private TalkCategory category;
	private UIPanel? rootPanel;
	private UIText? title;
	private UIText? response;
	private UIText? stats;
	private UIText? chooseWordsLabel;
	private UIText? packLabel;
	private UITextPanel<string>? closeButton;
	private int memoryCursor;
	public bool HasActiveBinding => sigil is not null && companion?.NPC.active == true;

	public override void OnInitialize()
	{
		rootPanel = new UIPanel {
			HAlign = 0.5f,
			VAlign = 0.5f,
			Width = new StyleDimension(660f, 0f),
			Height = new StyleDimension(440f, 0f),
			BackgroundColor = new Color(20, 29, 48) * 0.98f,
			BorderColor = new Color(118, 154, 206)
		};
		Append(rootPanel);
		UIPanel panel = rootPanel;

		title = new UIText(SoulmatesText.Get("UI.Talk.Title", ""), 1.1f, true) {
			HAlign = 0.5f,
			Top = new StyleDimension(10f, 0f)
		};
		panel.Append(title);

		var portrait = new UIPanel {
			Left = new StyleDimension(14f, 0f),
			Top = new StyleDimension(46f, 0f),
			Width = new StyleDimension(218f, 0f),
			Height = new StyleDimension(326f, 0f),
			BackgroundColor = new Color(10, 17, 31),
			BorderColor = new Color(67, 93, 133)
		};
		panel.Append(portrait);
		portrait.Append(new SoulPreviewElement(() => sigil?.Profile ?? new CompanionProfile()) {
			Left = new StyleDimension(8f, 0f),
			Top = new StyleDimension(5f, 0f),
			Width = new StyleDimension(-16f, 1f),
			Height = new StyleDimension(178f, 0f)
		});

		response = new UIText("...", 0.86f) {
			Left = new StyleDimension(12f, 0f),
			Top = new StyleDimension(184f, 0f),
			Width = new StyleDimension(-24f, 1f),
			Height = new StyleDimension(78f, 0f),
			TextOriginX = 0.5f,
			HAlign = 0.5f,
			IsWrapped = true
		};
		portrait.Append(response);

		stats = new UIText("", 0.68f) {
			Left = new StyleDimension(8f, 0f),
			Top = new StyleDimension(270f, 0f),
			Width = new StyleDimension(-16f, 1f),
			TextOriginX = 0.5f,
			HAlign = 0.5f,
			TextColor = Color.LightGray
		};
		portrait.Append(stats);

		TalkCategory[] categories = Enum.GetValues<TalkCategory>();
		for (int i = 0; i < categories.Length; i++) {
			TalkCategory chosen = categories[i];
			int column = i % 3;
			int row = i / 3;
			var button = Button(chosen.ToString().ToUpperInvariant(), 46f + row * 35f, 248f + column * 134f, 120f,
				new Color(54, 71, 105), 0.58f, 30f);
			button.OnLeftClick += (_, _) => SelectCategory(chosen);
			categoryButtons.Add((chosen, button));
			panel.Append(button);
		}

		chooseWordsLabel = new UIText("", 0.58f) {
			Left = new StyleDimension(248f, 0f),
			Top = new StyleDimension(116f, 0f),
			TextColor = new Color(155, 174, 203)
		};
		panel.Append(chooseWordsLabel);

		for (int i = 0; i < 3; i++) {
			int option = i;
			var button = Button("", 130f + i * 54f, 248f, 388f, new Color(43, 64, 98), 0.76f, 46f);
			button.OnLeftClick += (_, _) => Speak(option);
			optionButtons.Add(button);
			panel.Append(button);
		}

		panel.Append(new CompanionPackElement(() => sigil?.Profile, () => companion, reply => SetResponse(reply, accepted: true)) {
			Left = new StyleDimension(248f, 0f),
			Top = new StyleDimension(309f, 0f),
			Width = new StyleDimension(388f, 0f),
			Height = new StyleDimension(54f, 0f)
		});
		packLabel = new UIText("", 0.58f) {
			Left = new StyleDimension(248f, 0f),
			Top = new StyleDimension(292f, 0f),
			TextColor = new Color(155, 174, 203)
		};
		panel.Append(packLabel);

		closeButton = Button("", 372f, 248f, 388f, new Color(120, 63, 72), 0.76f, 40f);
		closeButton.OnLeftClick += (_, _) => ModContent.GetInstance<TalkModeSystem>().Close();
		panel.Append(closeButton);
	}

	public void Bind(SoulboundSigil boundSigil, SoulboundCompanion boundCompanion)
	{
		sigil = boundSigil;
		companion = boundCompanion;
		category = TalkCategory.Care;
		memoryCursor = 0;
		RefreshLocalizedLabels();
		if (title is not null)
			title.SetText(SoulmatesText.Get("UI.Talk.Title", sigil.Profile.Name.ToUpperInvariant()));
		if (rootPanel is not null)
			rootPanel.BorderColor = Color.Lerp(sigil.Profile.EssenceColor, Color.White, 0.25f);
		if (response is not null) {
			response.SetText(SoulmatesText.Get("UI.Talk.Greeting", sigil.Profile.Name));
			response.TextColor = sigil.Profile.EssenceColor;
		}
		RefreshOptions();
		RefreshStats();
		RefreshCategoryStyles();
	}

	private void RefreshLocalizedLabels()
	{
		foreach ((TalkCategory buttonCategory, UITextPanel<string> button) in categoryButtons)
			button.SetText(SoulmatesText.EnumName(buttonCategory).ToUpperInvariant());
		chooseWordsLabel?.SetText(SoulmatesText.Get("UI.Talk.ChooseWords"));
		packLabel?.SetText(SoulmatesText.Get("UI.Talk.Pack"));
		closeButton?.SetText(SoulmatesText.Get("UI.Common.Close"));
	}

	public void Unbind()
	{
		sigil = null;
		companion = null;
	}

	private void SelectCategory(TalkCategory selected)
	{
		category = selected;
		SoundEngine.PlaySound(SoundID.MenuTick);
		RefreshOptions();
		RefreshCategoryStyles();
	}

	public override void Update(GameTime gameTime)
	{
		base.Update(gameTime);
		if (sigil is null)
			return;
		RefreshStats();
		RefreshCategoryStyles();
	}

	private void Speak(int option)
	{
		if (sigil is null || companion is null || !companion.NPC.active) {
			ModContent.GetInstance<TalkModeSystem>().Close();
			return;
		}
		CompanionProfile profile = sigil.Profile;
		DialogueResult result = CompanionDialogueEngine.Speak(profile, category, option);
		profile.ChangeBond(result.BondDelta);
		profile.Mood = Math.Clamp(profile.Mood + result.MoodDelta, 0, 100);
		profile.Energy = Math.Clamp(profile.Energy + result.EnergyDelta, 0, 100);
		ApplyAction(profile, result.Action);
		companion.Profile = profile.Clone();
		companion.NPC.netUpdate = true;

		string reply = result.Action switch {
			SpeechAction.ShowPack => profile.DescribePack(),
			SpeechAction.StoreHeldItem => companion.StoreSelectedItem(),
			SpeechAction.UnloadPack => companion.UnloadPack(),
			SpeechAction.RecallMemory => profile.RecallMemory(memoryCursor++),
			_ => result.Reply
		};
		SetResponse(reply, result.Accepted);
		SoundEngine.PlaySound(result.Accepted ? SoundID.Chat : SoundID.MenuClose);
		RefreshStats();
	}

	private void SetResponse(string text, bool accepted)
	{
		if (response is null || sigil is null)
			return;
		response.SetText($"\"{text}\"");
		response.TextColor = accepted ? sigil.Profile.EssenceColor : Color.IndianRed;
	}

	private void ApplyAction(CompanionProfile profile, SpeechAction action)
	{
		if (companion is null)
			return;

		switch (action) {
			case SpeechAction.Follow:
				profile.Routine = CompanionJob.None;
				companion.SetCommand(stay: false);
				break;
			case SpeechAction.Stay:
			case SpeechAction.Rest:
				profile.Routine = CompanionJob.None;
				companion.SetCommand(stay: true);
				break;
			case SpeechAction.Explore:
				profile.Routine = CompanionJob.None;
				companion.AskToExplore();
				break;
			case SpeechAction.FindTreasure:
				profile.Routine = CompanionJob.FindTreasure;
				companion.StartJob(CompanionJob.FindTreasure);
				break;
			case SpeechAction.Mine:
				profile.Routine = CompanionJob.Mine;
				companion.StartJob(CompanionJob.Mine);
				break;
			case SpeechAction.Gather:
				profile.Routine = CompanionJob.Gather;
				companion.StartJob(CompanionJob.Gather);
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
		string job = companion?.CurrentJobName ?? SoulmatesText.Get("Status.Ready");
		int radius = companion?.CurrentJobRadius ?? 0;
		string area = radius > 0 ? SoulmatesText.Get("UI.Talk.Radius", radius) : "";
		stats.SetText(SoulmatesText.Get("UI.Talk.Stats", SoulmatesText.EnumName(profile.Rank).ToUpperInvariant(), profile.Bond,
			profile.Mood, profile.Energy, profile.PackLoad, profile.PackCapacity,
			job.ToUpperInvariant(), area.ToUpperInvariant()));
	}

	private void RefreshCategoryStyles()
	{
		Color accent = sigil?.Profile.EssenceColor ?? new Color(118, 154, 206);
		foreach ((TalkCategory buttonCategory, UITextPanel<string> button) in categoryButtons) {
			bool selected = buttonCategory == category;
			button.BackgroundColor = selected ? Color.Lerp(new Color(35, 48, 74), accent, 0.55f) : new Color(54, 71, 105);
			button.BorderColor = selected ? Color.Lerp(accent, Color.White, 0.3f) : new Color(73, 96, 142);
		}
		foreach (UITextPanel<string> button in optionButtons)
			button.BorderColor = Color.Lerp(new Color(58, 80, 122), accent, 0.35f);
	}

	private static UITextPanel<string> Button(string text, float top, float left, float width, Color color, float scale, float height)
	{
		var button = new UITextPanel<string>(text, scale) {
			Top = new StyleDimension(top, 0f),
			Left = new StyleDimension(left, 0f),
			Width = new StyleDimension(width, 0f),
			Height = new StyleDimension(height, 0f),
			BackgroundColor = color,
			BorderColor = color * 1.35f
		};
		button.OnMouseOver += (_, _) => button.BackgroundColor = color * 1.25f;
		button.OnMouseOut += (_, _) => button.BackgroundColor = color;
		return button;
	}

}

internal sealed class CompanionPackElement(
	Func<CompanionProfile?> getProfile,
	Func<SoulboundCompanion?> getCompanion,
	Action<string> report) : UIElement
{
	public override void OnInitialize()
	{
		OnLeftClick += (_, _) => Withdraw(singleItem: false);
		OnRightClick += (_, _) => Withdraw(singleItem: true);
	}

	protected override void DrawSelf(SpriteBatch spriteBatch)
	{
		base.DrawSelf(spriteBatch);
		CompanionProfile? profile = getProfile();
		if (profile is null)
			return;

		CalculatedStyle area = GetDimensions();
		const float slotSize = 27f;
		const float gap = 4f;
		float totalWidth = slotSize * 6f + gap * 5f;
		Vector2 start = new(area.X + (area.Width - totalWidth) * 0.5f, area.Y);
		Texture2D slotTexture = TextureAssets.InventoryBack.Value;
		for (int i = 0; i < 12; i++) {
			Vector2 position = start + new Vector2((i % 6) * (slotSize + gap), (i / 6) * (slotSize + gap));
			Color slotColor = i < profile.PackCapacity ? Color.White : new Color(35, 40, 52) * 0.65f;
			spriteBatch.Draw(slotTexture, position, null, slotColor, 0f, Vector2.Zero, slotSize / slotTexture.Width, SpriteEffects.None, 0f);
			if (i >= profile.Pack.Count || profile.Pack[i].IsAir)
				continue;

			Item item = profile.Pack[i];
			Texture2D texture = TextureAssets.Item[item.type].Value;
			Rectangle frame = Main.itemAnimations[item.type]?.GetFrame(texture) ?? texture.Bounds;
			float scale = Math.Min(20f / frame.Width, 20f / frame.Height);
			Vector2 center = position + new Vector2(slotSize * 0.5f);
			spriteBatch.Draw(texture, center, frame, Color.White, 0f, frame.Size() * 0.5f, scale, SpriteEffects.None, 0f);
			if (item.stack > 1)
				Utils.DrawBorderString(spriteBatch, item.stack.ToString(), position + new Vector2(slotSize - 2f, slotSize - 2f), Color.White, 0.55f, 1f, 1f);
		}

		int hovered = SlotAt(Main.MouseScreen);
		if (hovered >= 0 && hovered < profile.Pack.Count && !profile.Pack[hovered].IsAir) {
			Main.LocalPlayer.mouseInterface = true;
			Main.HoverItem = profile.Pack[hovered].Clone();
			Main.hoverItemName = profile.Pack[hovered].HoverName;
		}
	}

	private void Withdraw(bool singleItem)
	{
		SoulboundCompanion? companion = getCompanion();
		if (companion is null)
			return;
		int index = SlotAt(Main.MouseScreen);
		if (index < 0)
			return;
		string responseText = companion.WithdrawPackSlot(index, singleItem);
		report(responseText);
		SoundEngine.PlaySound(SoundID.Grab);
	}

	private int SlotAt(Vector2 mousePosition)
	{
		CalculatedStyle area = GetDimensions();
		const float slotSize = 27f;
		const float gap = 4f;
		float totalWidth = slotSize * 6f + gap * 5f;
		Vector2 start = new(area.X + (area.Width - totalWidth) * 0.5f, area.Y);
		for (int i = 0; i < 12; i++) {
			Vector2 position = start + new Vector2((i % 6) * (slotSize + gap), (i / 6) * (slotSize + gap));
			if (new Rectangle((int)position.X, (int)position.Y, (int)slotSize, (int)slotSize).Contains(mousePosition.ToPoint()))
				return i;
		}
		return -1;
	}
}
