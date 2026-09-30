#nullable enable
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
using Terraria.GameContent.UI;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Soulmates.Common.UI;

public sealed class TalkModeState : UIState
{
	private const float MinimumWidth = 540f;
	private const float MinimumHeight = 384f;
	private static float preferredWidth = 596f;
	private static float preferredHeight = 412f;
	private readonly List<UITextPanel<string>> optionButtons = [];
	private readonly List<(TalkCategory Category, TalkIconButton Button)> categoryButtons = [];
	private SoulboundSigil? sigil;
	private SoulboundCompanion? companion;
	private TalkCategory category;
	private UIPanel? rootPanel;
	private UIElement? portraitPanel;
	private UIElement? previewElement;
	private CompanionVitalsElement? vitalsElement;
	private CompanionPackElement? packElement;
	private UIText? title;
	private CompanionResponseElement? response;
	private UIText? chooseWordsLabel;
	private UIText? packLabel;
	private TalkIconButton? closeButton;
	private TalkItemButton? resizeHandle;
	private int memoryCursor;
	private bool awaitingResponse;
	private int responseWaitTicks;
	private bool resizing;
	private Vector2 resizeStartMouse;
	private Vector2 resizeStartSize;
	public bool HasActiveBinding => sigil is not null && companion?.NPC.active == true;
	private CompanionProfile? DisplayProfile => companion?.NPC.active == true ? companion.Profile : sigil?.Profile;

	public override void OnInitialize()
	{
		rootPanel = new UIPanel {
			HAlign = 0.5f,
			VAlign = 0.5f,
			Width = new StyleDimension(preferredWidth, 0f),
			Height = new StyleDimension(preferredHeight, 0f),
			BackgroundColor = new Color(33, 43, 79) * 0.97f,
			BorderColor = new Color(89, 116, 213)
		};
		Append(rootPanel);
		rootPanel.SetPadding(0f);
		UIPanel panel = rootPanel;

		title = new UIText(SoulmatesText.Get("UI.Talk.Title", ""), 0.76f) {
			Left = new StyleDimension(14f, 0f),
			Top = new StyleDimension(10f, 0f)
		};
		panel.Append(title);

		portraitPanel = new UIElement {
			Left = new StyleDimension(10f, 0f),
			Top = new StyleDimension(42f, 0f),
			Width = new StyleDimension(158f, 0f),
			Height = new StyleDimension(250f, 0f)
		};
		UIElement portrait = portraitPanel;
		panel.Append(portrait);
		previewElement = new SoulPreviewElement(() => DisplayProfile ?? new CompanionProfile()) {
			Left = new StyleDimension(6f, 0f),
			Top = new StyleDimension(2f, 0f),
			Width = new StyleDimension(-12f, 1f),
			Height = new StyleDimension(108f, 0f)
		};
		portrait.Append(previewElement);

		response = new CompanionResponseElement {
			Left = new StyleDimension(10f, 0f),
			Top = new StyleDimension(112f, 0f),
			Width = new StyleDimension(-20f, 1f),
			Height = new StyleDimension(52f, 0f)
		};
		portrait.Append(response);

		vitalsElement = new CompanionVitalsElement(() => DisplayProfile, () => companion) {
			Left = new StyleDimension(6f, 0f),
			Top = new StyleDimension(158f, 0f),
			Width = new StyleDimension(-12f, 1f),
			Height = new StyleDimension(88f, 0f)
		};
		portrait.Append(vitalsElement);

		TalkCategory[] categories = Enum.GetValues<TalkCategory>();
		for (int i = 0; i < categories.Length; i++) {
			TalkCategory chosen = categories[i];
			var button = new TalkIconButton(CategoryIcon(chosen)) {
				Left = new StyleDimension(178f + i * 46f, 0f),
				Top = new StyleDimension(40f, 0f),
				Width = new StyleDimension(40f, 0f),
				Height = new StyleDimension(40f, 0f)
			};
			button.OnLeftClick += (_, _) => SelectCategory(chosen);
			categoryButtons.Add((chosen, button));
			panel.Append(button);
		}

		chooseWordsLabel = new UIText("", 0.58f) {
			Left = new StyleDimension(178f, 0f),
			Top = new StyleDimension(86f, 0f),
			TextColor = new Color(155, 174, 203)
		};
		panel.Append(chooseWordsLabel);

		for (int i = 0; i < 3; i++) {
			int option = i;
			var button = Button("", 103f + i * 39f, 178f, 366f, new Color(63, 82, 151), 0.56f, 34f);
			button.OnLeftClick += (_, _) => Speak(option);
			optionButtons.Add(button);
			panel.Append(button);
		}

		packElement = new CompanionPackElement(() => DisplayProfile, WithdrawPackSlot) {
			Left = new StyleDimension(178f, 0f),
			Top = new StyleDimension(239f, 0f),
			Width = new StyleDimension(366f, 0f),
			Height = new StyleDimension(58f, 0f)
		};
		panel.Append(packElement);
		packLabel = new UIText("", 0.58f) {
			Left = new StyleDimension(178f, 0f),
			Top = new StyleDimension(223f, 0f),
			TextColor = new Color(155, 174, 203)
		};
		panel.Append(packLabel);

		closeButton = new TalkIconButton(EmoteID.EmoteScowl) {
			Left = new StyleDimension(514f, 0f),
			Top = new StyleDimension(6f, 0f),
			Width = new StyleDimension(34f, 0f),
			Height = new StyleDimension(34f, 0f)
		};
		closeButton.OnLeftClick += (_, _) => ModContent.GetInstance<TalkModeSystem>().Close();
		panel.Append(closeButton);

		resizeHandle = new TalkItemButton(ItemID.Ruler) {
			Width = new StyleDimension(26f, 0f),
			Height = new StyleDimension(26f, 0f)
		};
		resizeHandle.OnLeftMouseDown += (_, _) => BeginResize();
		panel.Append(resizeHandle);
		ApplyLayout();
	}

	public void Bind(SoulboundSigil boundSigil, SoulboundCompanion boundCompanion, TalkCategory initialCategory = TalkCategory.Care)
	{
		sigil = boundSigil;
		companion = boundCompanion;
		sigil.Profile = companion.Profile.Clone();
		category = Enum.IsDefined(initialCategory) ? initialCategory : TalkCategory.Care;
		memoryCursor = 0;
		awaitingResponse = false;
		responseWaitTicks = 0;
		resizing = false;
		ApplyLayout();
		Recalculate();
		RefreshLocalizedLabels();
		if (title is not null)
			title.SetText(SoulmatesText.Get("UI.Talk.Title", companion.Profile.Name.ToUpperInvariant()));
		if (rootPanel is not null)
			rootPanel.BorderColor = Color.Lerp(companion.Profile.EssenceColor, Color.White, 0.25f);
		if (response is not null) {
			response.SetText(SoulmatesText.Get("UI.Talk.Greeting", companion.Profile.Name));
			response.TextColor = companion.Profile.EssenceColor;
		}
		RefreshOptions();
		RefreshCategoryStyles();
	}

	private void RefreshLocalizedLabels()
	{
		foreach ((TalkCategory buttonCategory, TalkIconButton button) in categoryButtons)
			button.HoverText = SoulmatesText.EnumName(buttonCategory);
		chooseWordsLabel?.SetText(SoulmatesText.Get("UI.Talk.ChooseWords"));
		packLabel?.SetText(SoulmatesText.Get("UI.Talk.Pack"));
		if (closeButton is not null)
			closeButton.HoverText = SoulmatesText.Get("UI.Common.Close");
		if (resizeHandle is not null)
			resizeHandle.HoverText = SoulmatesText.Get("UI.Talk.Resize");
	}

	public void Unbind()
	{
		sigil = null;
		companion = null;
		awaitingResponse = false;
		responseWaitTicks = 0;
		resizing = false;
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
		UpdateResize();
		ApplyLayout();
		if (awaitingResponse && ++responseWaitTicks >= 600) {
			awaitingResponse = false;
			responseWaitTicks = 0;
			SetResponse(SoulmatesText.Get("UI.Talk.ResponseTimeout"), accepted: false);
		}
		if (sigil is null)
			return;
		RefreshCategoryStyles();
	}

	private void BeginResize()
	{
		resizing = true;
		resizeStartMouse = SoulmatesUISpace.Mouse;
		resizeStartSize = new Vector2(rootPanel?.Width.Pixels ?? preferredWidth, rootPanel?.Height.Pixels ?? preferredHeight);
	}

	private void UpdateResize()
	{
		if (!resizing)
			return;
		if (!Main.mouseLeft) {
			resizing = false;
			return;
		}
		Vector2 delta = SoulmatesUISpace.Mouse - resizeStartMouse;
		Vector2 viewport = SoulmatesUISpace.Viewport;
		float maximumWidth = Math.Max(240f, viewport.X - 24f);
		float maximumHeight = Math.Max(240f, viewport.Y - 24f);
		preferredWidth = Math.Clamp(resizeStartSize.X + delta.X * 2f, Math.Min(MinimumWidth, maximumWidth), maximumWidth);
		preferredHeight = Math.Clamp(resizeStartSize.Y + delta.Y * 2f, Math.Min(MinimumHeight, maximumHeight), maximumHeight);
		ApplyLayout();
		Recalculate();
		Main.LocalPlayer.mouseInterface = true;
	}

	private void ApplyLayout(bool force = false)
	{
		if (rootPanel is null || portraitPanel is null || previewElement is null || vitalsElement is null
			|| packElement is null || resizeHandle is null || closeButton is null)
			return;
		Vector2 viewport = SoulmatesUISpace.Viewport;
		float width = Math.Min(preferredWidth, Math.Max(240f, viewport.X - 24f));
		float height = Math.Min(preferredHeight, Math.Max(240f, viewport.Y - 24f));
		float portraitWidth = Math.Clamp(width * 0.31f, 100f, 180f);
		float portraitHeight = height - 56f;
		float rightLeft = portraitWidth + 26f;
		float rightWidth = width - rightLeft - 12f;
		float previewHeight = Math.Clamp(portraitHeight - 232f, 40f, 148f);
		bool changed = rootPanel.Width.Pixels != width || rootPanel.Height.Pixels != height
			|| portraitPanel.Width.Pixels != portraitWidth;
		if (!changed && !force)
			return;

		rootPanel.Width.Set(width, 0f);
		rootPanel.Height.Set(height, 0f);
		if (title is not null) {
			float titleWidth = FontAssets.MouseText.Value.MeasureString(title.Text).X;
			title.SetText(title.Text, titleWidth <= 0f ? 0.76f : Math.Min(0.76f, (width - 76f) / titleWidth), false);
		}
		portraitPanel.Height.Set(portraitHeight, 0f);
		portraitPanel.Width.Set(portraitWidth, 0f);
		previewElement.Height.Set(previewHeight, 0f);
		if (response is not null) {
			response.Top.Set(previewHeight + 6f, 0f);
			response.Height.Set(56f, 0f);
		}
		vitalsElement.Top.Set(previewHeight + 66f, 0f);
		vitalsElement.Height.Set(Math.Max(80f, portraitHeight - previewHeight - 70f), 0f);

		float categorySize = Math.Min(40f, rightWidth / categoryButtons.Count - 4f);
		float categoryStep = Math.Min(48f, (rightWidth - categorySize) / Math.Max(1, categoryButtons.Count - 1));
		for (int i = 0; i < categoryButtons.Count; i++) {
			TalkIconButton button = categoryButtons[i].Button;
			button.Left.Set(rightLeft + i * categoryStep, 0f);
			button.Width.Set(categorySize, 0f);
			button.Height.Set(categorySize, 0f);
		}
		chooseWordsLabel?.Left.Set(rightLeft, 0f);
		for (int i = 0; i < optionButtons.Count; i++) {
			UITextPanel<string> button = optionButtons[i];
			button.Left.Set(rightLeft, 0f);
			button.Width.Set(rightWidth, 0f);
			float availableHeight = Math.Max(78f, height - 202f);
			float step = Math.Min(49f, availableHeight / 3f);
			button.Top.Set(106f + i * step, 0f);
			button.Height.Set(step - 6f, 0f);
			string text = button.Text;
			float textWidth = FontAssets.MouseText.Value.MeasureString(text).X;
			button.SetText(text, textWidth <= 0f ? 0.62f : Math.Min(0.62f, (rightWidth - 20f) / textWidth), false);
		}
		packElement.Left.Set(rightLeft, 0f);
		packElement.Top.Set(height - 78f, 0f);
		packElement.Width.Set(Math.Max(60f, rightWidth - 30f), 0f);
		packLabel?.Left.Set(rightLeft, 0f);
		packLabel?.Top.Set(height - 98f, 0f);
		closeButton.Left.Set(width - 46f, 0f);
		resizeHandle.Left.Set(width - 38f, 0f);
		resizeHandle.Top.Set(height - 38f, 0f);
		if (changed)
			Recalculate();
	}

	private void Speak(int option)
	{
		if (awaitingResponse)
			return;
		if (sigil is null || companion is null || !companion.NPC.active) {
			ModContent.GetInstance<TalkModeSystem>().Close();
			return;
		}

		if (Main.netMode == NetmodeID.MultiplayerClient) {
			awaitingResponse = true;
			responseWaitTicks = 0;
			Soulmates.SendTalkRequest(category, option, memoryCursor++);
			SoundEngine.PlaySound(SoundID.MenuTick);
			return;
		}

		CompanionConversationResult result = companion.Converse(category, option, memoryCursor++);
		sigil.Profile = companion.Profile.Clone();
		SetResponse(result.Reply, result.Accepted);
		SoundEngine.PlaySound(result.Accepted ? SoundID.Chat : SoundID.MenuClose);
	}

	internal void ReceiveNetworkResponse(CompanionProfile profile, string reply, bool accepted)
	{
		if (sigil is null || sigil.Profile.Id != profile.Id)
			return;
		awaitingResponse = false;
		responseWaitTicks = 0;
		sigil.Profile = profile.Clone();
		if (companion?.NPC.active == true)
			companion.Profile = profile.Clone();
		SetResponse(reply, accepted);
		SoundEngine.PlaySound(accepted ? SoundID.Chat : SoundID.MenuClose);
	}

	private void WithdrawPackSlot(int index, bool singleItem)
	{
		if (awaitingResponse || companion?.NPC.active != true)
			return;
		if (Main.netMode == NetmodeID.MultiplayerClient) {
			awaitingResponse = true;
			responseWaitTicks = 0;
			Soulmates.SendPackWithdrawRequest(index, singleItem);
		}
		else {
			string reply = companion.WithdrawPackSlot(index, singleItem);
			sigil!.Profile = companion.Profile.Clone();
			SetResponse(reply, accepted: true);
		}
		SoundEngine.PlaySound(SoundID.Grab);
	}

	private void SetResponse(string text, bool accepted)
	{
		if (response is null || sigil is null)
			return;
		response.SetText($"\"{text}\"");
		response.TextColor = accepted ? DisplayProfile?.EssenceColor ?? Color.White : Color.IndianRed;
	}

	private void RefreshOptions()
	{
		string[] options = CompanionDialogueEngine.GetOptions(category);
		for (int i = 0; i < optionButtons.Count; i++) {
			int energyChange = CompanionDialogueEngine.GetEnergyChange(category, i);
			string energy = energyChange < 0
				? SoulmatesText.Get("UI.Talk.EnergyCost", -energyChange)
				: energyChange > 0 ? SoulmatesText.Get("UI.Talk.EnergyGain", energyChange) : "";
			optionButtons[i].SetText(string.IsNullOrEmpty(energy)
				? $"\"{options[i]}\""
				: $"\"{options[i]}\"  {energy}");
		}
		ApplyLayout(force: true);
	}

	private void RefreshCategoryStyles()
	{
		Color accent = DisplayProfile?.EssenceColor ?? new Color(118, 154, 206);
		foreach ((TalkCategory buttonCategory, TalkIconButton button) in categoryButtons) {
			button.Selected = buttonCategory == category;
			button.Accent = accent;
		}
		foreach (UITextPanel<string> button in optionButtons)
			button.BorderColor = Color.Lerp(new Color(58, 80, 122), accent, 0.35f);
	}

	private static int CategoryIcon(TalkCategory category) => category switch {
		TalkCategory.Care => EmoteID.EmoteHappiness,
		TalkCategory.Commands => EmoteID.EmoteRun,
		TalkCategory.Work => EmoteID.ItemPickaxe,
		TalkCategory.Bond => EmoteID.EmotionLove,
		TalkCategory.Voice => EmoteID.EmoteNote,
		_ => EmoteID.ItemGoldpile
	};

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

internal sealed class TalkIconButton(int emoteId) : UIElement
{
	public string HoverText { get; set; } = "";
	public bool Selected { get; set; }
	public Color Accent { get; set; } = Color.White;

	protected override void DrawSelf(SpriteBatch spriteBatch)
	{
		base.DrawSelf(spriteBatch);
		CalculatedStyle area = GetDimensions();
		float size = Math.Min(area.Width, area.Height);
		Vector2 center = new(area.X + area.Width * 0.5f, area.Y + area.Height * 0.5f);
		Texture2D slot = TextureAssets.InventoryBack.Value;
		Color slotColor = IsMouseHovering
			? Color.White
			: Selected ? Color.Lerp(Color.White, Accent, 0.34f) : Color.White * 0.9f;
		spriteBatch.Draw(slot, center, null, slotColor, 0f, slot.Size() * 0.5f,
			size / slot.Width, SpriteEffects.None, 0f);
		DrawEmote(spriteBatch, center, size * 0.62f, emoteId);
		if (!IsMouseHovering || string.IsNullOrWhiteSpace(HoverText))
			return;
		Main.LocalPlayer.mouseInterface = true;
		Main.hoverItemName = HoverText;
	}

	private static void DrawEmote(SpriteBatch spriteBatch, Vector2 position, float maximumSize, int id)
	{
		if (id < 0 || id >= EmoteID.Count)
			return;
		Texture2D sheet = TextureAssets.Extra[ExtrasID.EmoteBubble].Value;
		int frame = (int)(Main.GlobalTimeWrappedHourly * 3f) % 2;
		int column = id % EmoteBubble.EMOTE_SHEET_EMOTES_PER_ROW * 2 + frame;
		int row = 1 + id / EmoteBubble.EMOTE_SHEET_EMOTES_PER_ROW;
		Rectangle source = sheet.Frame(EmoteBubble.EMOTE_SHEET_HORIZONTAL_FRAMES,
			EmoteBubble.EMOTE_SHEET_VERTICAL_FRAMES, column, row);
		float scale = Math.Min(maximumSize / source.Width, maximumSize / source.Height);
		spriteBatch.Draw(sheet, position, source, Color.White, 0f, source.Size() * 0.5f,
			scale, SpriteEffects.None, 0f);
	}
}

internal sealed class TalkItemButton(int itemType) : UIElement
{
	public string HoverText { get; set; } = "";

	protected override void DrawSelf(SpriteBatch spriteBatch)
	{
		base.DrawSelf(spriteBatch);
		CalculatedStyle area = GetDimensions();
		float size = Math.Min(area.Width, area.Height);
		Vector2 center = new(area.X + area.Width * 0.5f, area.Y + area.Height * 0.5f);
		Texture2D slot = TextureAssets.InventoryBack.Value;
		spriteBatch.Draw(slot, center, null, IsMouseHovering ? Color.White : Color.White * 0.86f,
			0f, slot.Size() * 0.5f, size / slot.Width, SpriteEffects.None, 0f);
		Main.instance.LoadItem(itemType);
		Texture2D texture = TextureAssets.Item[itemType].Value;
		Rectangle source = Main.itemAnimations[itemType]?.GetFrame(texture) ?? texture.Bounds;
		float scale = Math.Min(size * 0.58f / source.Width, size * 0.58f / source.Height);
		spriteBatch.Draw(texture, center, source, Color.White, 0f, source.Size() * 0.5f,
			scale, SpriteEffects.None, 0f);
		if (!IsMouseHovering || string.IsNullOrWhiteSpace(HoverText))
			return;
		Main.LocalPlayer.mouseInterface = true;
		Main.hoverItemName = HoverText;
	}
}

internal sealed class CompanionResponseElement : UIElement
{
	private string text = "...";
	private string cachedText = "";
	private float cachedWidth;
	private List<string> lines = [];
	public Color TextColor { get; set; } = Color.White;
	public void SetText(string value) => text = value;

	protected override void DrawSelf(SpriteBatch spriteBatch)
	{
		CalculatedStyle area = GetDimensions();
		if (cachedText != text || cachedWidth != area.Width) {
			cachedText = text;
			cachedWidth = area.Width;
			lines = SoulmatesTextLayout.Wrap(text, Math.Max(20f, area.Width),
				line => FontAssets.MouseText.Value.MeasureString(line).X * 0.58f);
		}
		int visibleLines = Math.Min(lines.Count, Math.Max(1, (int)(area.Height / 17f)));
		for (int i = 0; i < visibleLines; i++) {
			string line = i == visibleLines - 1 && lines.Count > visibleLines ? "..." : lines[i];
			float width = FontAssets.MouseText.Value.MeasureString(line).X * 0.58f;
			Utils.DrawBorderString(spriteBatch, line, new Vector2(area.X + (area.Width - width) * 0.5f, area.Y + i * 17f),
				TextColor, 0.58f);
		}
		if (IsMouseHovering)
			Main.hoverItemName = text;
	}
}

internal sealed class CompanionVitalsElement(
	Func<CompanionProfile?> getProfile,
	Func<SoulboundCompanion?> getCompanion) : UIElement
{
	protected override void DrawSelf(SpriteBatch spriteBatch)
	{
		base.DrawSelf(spriteBatch);
		CompanionProfile? profile = getProfile();
		if (profile is null)
			return;

		CalculatedStyle area = GetDimensions();
		Vector2 topLeft = new(area.X, area.Y);
		float rowScale = Math.Min(1f, area.Height / 166f);
		float width = area.Width;
		string talent = profile.IsAether
			? SoulmatesText.Get("UI.Talk.OmniSoul")
			: SoulmatesText.EnumName(profile.Talent);
		string identity = $"{talent.ToUpperInvariant()} | {SoulmatesText.EnumName(profile.Rank).ToUpperInvariant()}";
		DrawLine(identity, 0f, Color.LightGray, 0.42f);
		int experience = profile.Level >= CompanionProfile.MaximumLevel
			? 1
			: profile.ExperienceIntoLevel;
		int experienceMaximum = profile.Level >= CompanionProfile.MaximumLevel
			? 1
			: Math.Max(1, profile.ExperienceNeededForNextLevel);
		string experienceValue = profile.Level >= CompanionProfile.MaximumLevel
			? SoulmatesText.Get("UI.Talk.LevelMax", profile.Level)
			: SoulmatesText.Get("UI.Talk.LevelProgress", profile.Level, experience, experienceMaximum);
		DrawLine(experienceValue, 18f, Color.White, 0.52f);
		DrawBar(spriteBatch, topLeft + new Vector2(0f, 34f * rowScale), width,
			experience, experienceMaximum, profile.EssenceColor, rowScale);
		DrawMeter(SoulmatesText.Get("UI.Talk.MoodShort"), profile.Mood, 48f, new Color(225, 117, 156));
		DrawMeter(SoulmatesText.Get("UI.Talk.EnergyShort"), profile.Energy, 68f,
			profile.Energy < 20 ? new Color(225, 101, 92) : new Color(94, 196, 225));
		string status = getCompanion()?.CurrentJobName ?? SoulmatesText.Get("Status.Ready");
		string footer = $"{SoulmatesText.Get("UI.Talk.BondShort")} {profile.Bond} | "
			+ $"{SoulmatesText.Get("UI.Talk.PackShort")} {profile.PackLoad}/{profile.PackCapacity}";
		DrawLine(footer, 88f, profile.EssenceColor, 0.43f);
		DrawLine(status, 106f, Color.LightGray, 0.43f);
		string miningApproach = SoulmatesText.Get("UI.Talk.MiningApproach",
			SoulmatesText.EnumName(profile.MiningApproach));
		DrawLine(miningApproach, 124f, Color.Lerp(profile.EssenceColor, Color.White, 0.2f), 0.4f);
		LearnedBehavior dominantBehavior = profile.DominantLearnedBehavior;
		string dominantPerk = profile.HasLearnedPerk(dominantBehavior)
			? CompanionProfile.LearnedPerkName(dominantBehavior)
			: SoulmatesText.Get("Learning.PerkProgress", CompanionProfile.LearnedPerkName(dominantBehavior),
				profile.GetInsight(dominantBehavior), CompanionProfile.LearnedPerkUnlockInsight(dominantBehavior));
		string learning = profile.IsAether
			? SoulmatesText.Get("UI.Talk.AetherLearning")
			: profile.DominantInsight <= 0
				? SoulmatesText.Get("UI.Talk.ObservingProgress", dominantPerk)
				: SoulmatesText.Get("UI.Talk.Learning", SoulmatesText.EnumName(profile.DominantLearnedBehavior),
					profile.DominantInsight, dominantPerk);
		DrawLine(learning, 144f, Color.Lerp(profile.EssenceColor, Color.White, 0.35f), 0.4f);
		if (IsMouseHovering)
			Main.hoverItemName = identity + "\n" + experienceValue + "\n" + footer + "\n" + status
				+ "\n" + miningApproach + "\n" + learning;

		void DrawLine(string text, float y, Color color, float scale)
			=> Utils.DrawBorderString(spriteBatch, text, topLeft + new Vector2(0f, y * rowScale), color,
				FitScale(text, width, scale * rowScale));

		void DrawMeter(string label, int value, float y, Color color)
		{
			float labelWidth = width * 0.6f;
			string text = $"{label} {value}";
			Utils.DrawBorderString(spriteBatch, text, topLeft + new Vector2(0f, y * rowScale), Color.LightGray,
				FitScale(text, labelWidth - 4f, 0.43f * rowScale));
			DrawBar(spriteBatch, topLeft + new Vector2(labelWidth, (y + 4f) * rowScale),
				width - labelWidth, value, 100, color, rowScale);
		}
	}

	private static void DrawBar(SpriteBatch spriteBatch, Vector2 position, float width, int value, int maximum, Color color, float scale)
	{
		Texture2D pixel = TextureAssets.MagicPixel.Value;
		int height = Math.Max(3, (int)(7f * scale));
		spriteBatch.Draw(pixel, new Rectangle((int)position.X, (int)position.Y, (int)width, height), new Color(33, 40, 55));
		int fill = (int)(width * Math.Clamp(value, 0, Math.Max(1, maximum)) / Math.Max(1f, maximum));
		if (fill > 0)
			spriteBatch.Draw(pixel, new Rectangle((int)position.X, (int)position.Y, fill, height), color);
	}

	private static float FitScale(string text, float maximumWidth, float preferredScale)
	{
		float width = FontAssets.MouseText.Value.MeasureString(text).X;
		return width <= 0f ? preferredScale : Math.Min(preferredScale, maximumWidth / width);
	}
}

internal sealed class CompanionPackElement(
	Func<CompanionProfile?> getProfile,
	Action<int, bool> withdraw) : UIElement
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
		float slotSize = Math.Min(27f, Math.Max(8f, (area.Width - 20f) / 6f));
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
			Main.instance.LoadItem(item.type);
			Texture2D texture = TextureAssets.Item[item.type].Value;
			Rectangle frame = Main.itemAnimations[item.type]?.GetFrame(texture) ?? texture.Bounds;
			float iconSize = Math.Min(20f, slotSize * 0.75f);
			float scale = Math.Min(iconSize / frame.Width, iconSize / frame.Height);
			Vector2 center = position + new Vector2(slotSize * 0.5f);
			spriteBatch.Draw(texture, center, frame, Color.White, 0f, frame.Size() * 0.5f, scale, SpriteEffects.None, 0f);
			if (item.stack > 1) {
				string count = item.stack.ToString();
				float countScale = Math.Min(0.55f, (slotSize - 2f) / Math.Max(1f, FontAssets.MouseText.Value.MeasureString(count).X));
				Utils.DrawBorderString(spriteBatch, count, position + new Vector2(slotSize - 2f, slotSize - 2f), Color.White, countScale, 1f, 1f);
			}
		}

		int hovered = SlotAt(SoulmatesUISpace.Mouse);
		if (hovered >= 0 && hovered < profile.Pack.Count && !profile.Pack[hovered].IsAir) {
			Main.LocalPlayer.mouseInterface = true;
			Main.HoverItem = profile.Pack[hovered].Clone();
			Main.hoverItemName = profile.Pack[hovered].HoverName;
		}
	}

	private void Withdraw(bool singleItem)
	{
		int index = SlotAt(SoulmatesUISpace.Mouse);
		if (index < 0)
			return;
		withdraw(index, singleItem);
	}

	private int SlotAt(Vector2 mousePosition)
	{
		CalculatedStyle area = GetDimensions();
		float slotSize = Math.Min(27f, Math.Max(8f, (area.Width - 20f) / 6f));
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
