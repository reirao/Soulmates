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
	private const float MinimumWidth = 500f;
	private const float MinimumHeight = 310f;
	private static float preferredWidth = 540f;
	private static float preferredHeight = 310f;
	private readonly List<UITextPanel<string>> optionButtons = [];
	private readonly List<(TalkCategory Category, TalkIconButton Button)> categoryButtons = [];
	private SoulboundSigil? sigil;
	private SoulboundCompanion? companion;
	private TalkCategory category;
	private UIPanel? rootPanel;
	private UIPanel? portraitPanel;
	private UIElement? previewElement;
	private CompanionVitalsElement? vitalsElement;
	private CompanionPackElement? packElement;
	private UIText? title;
	private UIText? response;
	private UIText? chooseWordsLabel;
	private UIText? packLabel;
	private TalkIconButton? closeButton;
	private TalkItemButton? resizeHandle;
	private int memoryCursor;
	private bool awaitingResponse;
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
		UIPanel panel = rootPanel;

		title = new UIText(SoulmatesText.Get("UI.Talk.Title", ""), 0.76f) {
			Left = new StyleDimension(14f, 0f),
			Top = new StyleDimension(10f, 0f)
		};
		panel.Append(title);

		portraitPanel = new UIPanel {
			Left = new StyleDimension(10f, 0f),
			Top = new StyleDimension(42f, 0f),
			Width = new StyleDimension(158f, 0f),
			Height = new StyleDimension(250f, 0f),
			BackgroundColor = new Color(43, 56, 103) * 0.88f,
			BorderColor = new Color(89, 116, 213)
		};
		UIPanel portrait = portraitPanel;
		panel.Append(portrait);
		previewElement = new SoulPreviewElement(() => DisplayProfile ?? new CompanionProfile()) {
			Left = new StyleDimension(6f, 0f),
			Top = new StyleDimension(2f, 0f),
			Width = new StyleDimension(-12f, 1f),
			Height = new StyleDimension(108f, 0f)
		};
		portrait.Append(previewElement);

		response = new UIText("...", 0.58f) {
			Left = new StyleDimension(10f, 0f),
			Top = new StyleDimension(112f, 0f),
			Width = new StyleDimension(-20f, 1f),
			Height = new StyleDimension(52f, 0f),
			TextOriginX = 0.5f,
			HAlign = 0.5f,
			IsWrapped = true
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
			Height = new StyleDimension(50f, 0f)
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
		if (sigil is null)
			return;
		RefreshCategoryStyles();
	}

	private void BeginResize()
	{
		resizing = true;
		resizeStartMouse = Main.MouseScreen;
		resizeStartSize = new Vector2(preferredWidth, preferredHeight);
	}

	private void UpdateResize()
	{
		if (!resizing)
			return;
		if (!Main.mouseLeft) {
			resizing = false;
			return;
		}
		Vector2 delta = Main.MouseScreen - resizeStartMouse;
		float maximumWidth = Math.Max(MinimumWidth, Main.screenWidth - 32f);
		float maximumHeight = Math.Max(MinimumHeight, Main.screenHeight - 32f);
		preferredWidth = Math.Clamp(resizeStartSize.X + delta.X * 2f, MinimumWidth, maximumWidth);
		preferredHeight = Math.Clamp(resizeStartSize.Y + delta.Y * 2f, MinimumHeight, maximumHeight);
		ApplyLayout();
		Recalculate();
		Main.LocalPlayer.mouseInterface = true;
	}

	private void ApplyLayout()
	{
		if (rootPanel is null || portraitPanel is null || previewElement is null || vitalsElement is null
			|| packElement is null || resizeHandle is null || closeButton is null)
			return;
		float width = preferredWidth;
		float height = preferredHeight;
		float portraitHeight = height - 60f;
		float rightWidth = width - 194f;
		float previewHeight = Math.Clamp(portraitHeight * 0.43f, 98f, 150f);

		rootPanel.Width.Set(width, 0f);
		rootPanel.Height.Set(height, 0f);
		portraitPanel.Height.Set(portraitHeight, 0f);
		previewElement.Height.Set(previewHeight, 0f);
		if (response is not null) {
			response.Top.Set(previewHeight + 6f, 0f);
			response.Height.Set(46f, 0f);
		}
		vitalsElement.Top.Set(previewHeight + 55f, 0f);
		vitalsElement.Height.Set(Math.Max(86f, portraitHeight - previewHeight - 59f), 0f);

		float categoryStep = Math.Min(46f, Math.Max(40f,
			(rightWidth - 40f) / Math.Max(1, categoryButtons.Count - 1)));
		for (int i = 0; i < categoryButtons.Count; i++)
			categoryButtons[i].Button.Left.Set(178f + i * categoryStep, 0f);
		foreach (UITextPanel<string> button in optionButtons)
			button.Width.Set(rightWidth, 0f);
		packElement.Top.Set(height - 71f, 0f);
		packElement.Width.Set(Math.Max(190f, rightWidth - 30f), 0f);
		packLabel?.Top.Set(height - 87f, 0f);
		closeButton.Left.Set(width - 46f, 0f);
		resizeHandle.Left.Set(width - 38f, 0f);
		resizeHandle.Top.Set(height - 38f, 0f);
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
		awaitingResponse = false;
		if (sigil is null || sigil.Profile.Id != profile.Id)
			return;
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
		string talent = profile.IsAether
			? SoulmatesText.Get("UI.Talk.OmniSoul")
			: SoulmatesText.EnumName(profile.Talent);
		string identity = $"{talent.ToUpperInvariant()} | {SoulmatesText.EnumName(profile.Rank).ToUpperInvariant()}";
		Utils.DrawBorderString(spriteBatch, identity, topLeft, Color.LightGray, FitScale(identity, 142f, 0.36f));
		int experience = profile.Level >= CompanionProfile.MaximumLevel
			? 1
			: profile.ExperienceIntoLevel;
		int experienceMaximum = profile.Level >= CompanionProfile.MaximumLevel
			? 1
			: Math.Max(1, profile.ExperienceNeededForNextLevel);
		string experienceValue = profile.Level >= CompanionProfile.MaximumLevel
			? SoulmatesText.Get("UI.Talk.LevelMax", profile.Level)
			: SoulmatesText.Get("UI.Talk.LevelProgress", profile.Level, experience, experienceMaximum);
		DrawMeter(spriteBatch, topLeft + new Vector2(0f, 15f), experienceValue, experience, experienceMaximum,
			profile.EssenceColor);
		DrawMeter(spriteBatch, topLeft + new Vector2(0f, 30f), SoulmatesText.Get("UI.Talk.MoodShort"), profile.Mood, 100,
			new Color(225, 117, 156));
		DrawMeter(spriteBatch, topLeft + new Vector2(0f, 45f), SoulmatesText.Get("UI.Talk.EnergyShort"), profile.Energy, 100,
			profile.Energy < 20 ? new Color(225, 101, 92) : new Color(94, 196, 225));
		string status = getCompanion()?.CurrentJobName ?? SoulmatesText.Get("Status.Ready");
		string footer = $"{SoulmatesText.Get("UI.Talk.BondShort")} {profile.Bond} | "
			+ $"{SoulmatesText.Get("UI.Talk.PackShort")} {profile.PackLoad}/{profile.PackCapacity} | {status}";
		footer = footer.ToUpperInvariant();
		Utils.DrawBorderString(spriteBatch, footer, topLeft + new Vector2(0f, 60f), profile.EssenceColor,
			FitScale(footer, 142f, 0.36f));
		string miningApproach = SoulmatesText.Get("UI.Talk.MiningApproach",
			SoulmatesText.Get($"MiningApproaches.Names.{profile.MiningApproach}")).ToUpperInvariant();
		Utils.DrawBorderString(spriteBatch, miningApproach, topLeft + new Vector2(0f, 74f),
			Color.Lerp(profile.EssenceColor, Color.White, 0.2f), FitScale(miningApproach, 142f, 0.32f));
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
		learning = learning.ToUpperInvariant();
		Utils.DrawBorderString(spriteBatch, learning, topLeft + new Vector2(0f, 88f),
			Color.Lerp(profile.EssenceColor, Color.White, 0.35f), FitScale(learning, 142f, 0.31f));
	}

	private static void DrawMeter(SpriteBatch spriteBatch, Vector2 position, string label, int value, int maximum, Color color)
	{
		Texture2D pixel = TextureAssets.MagicPixel.Value;
		string upperLabel = label.ToUpperInvariant();
		Utils.DrawBorderString(spriteBatch, upperLabel, position, Color.LightGray,
			FitScale(upperLabel, 58f, 0.34f));
		float barX = position.X + 61f;
		const float barWidth = 78f;
		spriteBatch.Draw(pixel, new Rectangle((int)barX, (int)position.Y + 3, (int)barWidth, 7), new Color(33, 40, 55));
		int fill = (int)(barWidth * Math.Clamp(value, 0, Math.Max(1, maximum)) / Math.Max(1f, maximum));
		if (fill > 0)
			spriteBatch.Draw(pixel, new Rectangle((int)barX, (int)position.Y + 3, fill, 7), color);
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
			Main.instance.LoadItem(item.type);
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
		int index = SlotAt(Main.MouseScreen);
		if (index < 0)
			return;
		withdraw(index, singleItem);
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
