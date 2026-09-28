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
	private UIText? chooseWordsLabel;
	private UIText? packLabel;
	private UITextPanel<string>? closeButton;
	private int memoryCursor;
	private bool awaitingResponse;
	public bool HasActiveBinding => sigil is not null && companion?.NPC.active == true;

	public override void OnInitialize()
	{
		rootPanel = new UIPanel {
			HAlign = 0.5f,
			VAlign = 0.5f,
			Width = new StyleDimension(640f, 0f),
			Height = new StyleDimension(410f, 0f),
			BackgroundColor = new Color(20, 29, 48) * 0.98f,
			BorderColor = new Color(118, 154, 206)
		};
		Append(rootPanel);
		UIPanel panel = rootPanel;

		title = new UIText(SoulmatesText.Get("UI.Talk.Title", ""), 1f, true) {
			HAlign = 0.5f,
			Top = new StyleDimension(10f, 0f)
		};
		panel.Append(title);

		var portrait = new UIPanel {
			Left = new StyleDimension(12f, 0f),
			Top = new StyleDimension(44f, 0f),
			Width = new StyleDimension(210f, 0f),
			Height = new StyleDimension(310f, 0f),
			BackgroundColor = new Color(10, 17, 31),
			BorderColor = new Color(67, 93, 133)
		};
		panel.Append(portrait);
		portrait.Append(new SoulPreviewElement(() => sigil?.Profile ?? new CompanionProfile()) {
			Left = new StyleDimension(8f, 0f),
			Top = new StyleDimension(5f, 0f),
			Width = new StyleDimension(-16f, 1f),
			Height = new StyleDimension(150f, 0f)
		});

		response = new UIText("...", 0.76f) {
			Left = new StyleDimension(12f, 0f),
			Top = new StyleDimension(158f, 0f),
			Width = new StyleDimension(-24f, 1f),
			Height = new StyleDimension(68f, 0f),
			TextOriginX = 0.5f,
			HAlign = 0.5f,
			IsWrapped = true
		};
		portrait.Append(response);

		portrait.Append(new CompanionVitalsElement(() => sigil?.Profile, () => companion) {
			Left = new StyleDimension(8f, 0f),
			Top = new StyleDimension(232f, 0f),
			Width = new StyleDimension(-16f, 1f),
			Height = new StyleDimension(68f, 0f)
		});

		TalkCategory[] categories = Enum.GetValues<TalkCategory>();
		for (int i = 0; i < categories.Length; i++) {
			TalkCategory chosen = categories[i];
			int column = i % 3;
			int row = i / 3;
			var button = Button(chosen.ToString().ToUpperInvariant(), 44f + row * 33f, 234f + column * 128f, 116f,
				new Color(54, 71, 105), 0.58f, 30f);
			button.OnLeftClick += (_, _) => SelectCategory(chosen);
			categoryButtons.Add((chosen, button));
			panel.Append(button);
		}

		chooseWordsLabel = new UIText("", 0.58f) {
			Left = new StyleDimension(234f, 0f),
			Top = new StyleDimension(112f, 0f),
			TextColor = new Color(155, 174, 203)
		};
		panel.Append(chooseWordsLabel);

		for (int i = 0; i < 3; i++) {
			int option = i;
			var button = Button("", 126f + i * 50f, 234f, 372f, new Color(43, 64, 98), 0.66f, 42f);
			button.OnLeftClick += (_, _) => Speak(option);
			optionButtons.Add(button);
			panel.Append(button);
		}

		panel.Append(new CompanionPackElement(() => sigil?.Profile, WithdrawPackSlot) {
			Left = new StyleDimension(234f, 0f),
			Top = new StyleDimension(296f, 0f),
			Width = new StyleDimension(372f, 0f),
			Height = new StyleDimension(54f, 0f)
		});
		packLabel = new UIText("", 0.58f) {
			Left = new StyleDimension(234f, 0f),
			Top = new StyleDimension(279f, 0f),
			TextColor = new Color(155, 174, 203)
		};
		panel.Append(packLabel);

		closeButton = Button("", 356f, 234f, 372f, new Color(120, 63, 72), 0.7f, 34f);
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
		if (sigil is null)
			return;
		RefreshCategoryStyles();
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
		response.TextColor = accepted ? sigil.Profile.EssenceColor : Color.IndianRed;
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
		string growth = profile.Level >= CompanionProfile.MaximumLevel
			? SoulmatesText.Get("UI.Talk.GrowthMax", profile.Level)
			: SoulmatesText.Get("UI.Talk.Growth", profile.Level, profile.ExperienceIntoLevel, profile.ExperienceNeededForNextLevel);
		string identity = $"{SoulmatesText.EnumName(profile.Talent).ToUpperInvariant()} | {growth} | "
			+ SoulmatesText.EnumName(profile.Rank).ToUpperInvariant();
		Utils.DrawBorderString(spriteBatch, identity, topLeft, Color.LightGray, 0.4f);
		DrawMeter(spriteBatch, topLeft + new Vector2(0f, 18f), SoulmatesText.Get("UI.Talk.MoodShort"), profile.Mood,
			new Color(225, 117, 156));
		DrawMeter(spriteBatch, topLeft + new Vector2(0f, 36f), SoulmatesText.Get("UI.Talk.EnergyShort"), profile.Energy,
			profile.Energy < 20 ? new Color(225, 101, 92) : new Color(94, 196, 225));
		string status = getCompanion()?.CurrentJobName ?? SoulmatesText.Get("Status.Ready");
		string footer = $"{SoulmatesText.Get("UI.Talk.BondShort")} {profile.Bond} | "
			+ $"{SoulmatesText.Get("UI.Talk.PackShort")} {profile.PackLoad}/{profile.PackCapacity} | {status}";
		Utils.DrawBorderString(spriteBatch, footer.ToUpperInvariant(), topLeft + new Vector2(0f, 55f), profile.EssenceColor, 0.43f);
	}

	private static void DrawMeter(SpriteBatch spriteBatch, Vector2 position, string label, int value, Color color)
	{
		Texture2D pixel = TextureAssets.MagicPixel.Value;
		Utils.DrawBorderString(spriteBatch, label.ToUpperInvariant(), position, Color.LightGray, 0.43f);
		float barX = position.X + 50f;
		const float barWidth = 112f;
		spriteBatch.Draw(pixel, new Rectangle((int)barX, (int)position.Y + 3, (int)barWidth, 7), new Color(33, 40, 55));
		int fill = (int)(barWidth * Math.Clamp(value, 0, 100) / 100f);
		if (fill > 0)
			spriteBatch.Draw(pixel, new Rectangle((int)barX, (int)position.Y + 3, fill, 7), color);
		Utils.DrawBorderString(spriteBatch, value.ToString(), new Vector2(barX + barWidth + 5f, position.Y - 1f), Color.White, 0.4f);
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
