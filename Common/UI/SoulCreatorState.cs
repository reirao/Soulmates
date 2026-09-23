using System;
using Microsoft.Xna.Framework;
using Soulmates.Content.Items;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Soulmates.Common.UI;

public sealed class SoulCreatorState : UIState
{
	private static readonly string[] Names = ["Luma", "Nova", "Moss", "Cinder", "Echo", "Pip", "Rune", "Mira"];
	private readonly CompanionProfile draft = new();
	private int nameIndex;
	private UIText? preview;
	private UIText? status;

	public override void OnInitialize()
	{
		var panel = new UIPanel {
			HAlign = 0.5f,
			VAlign = 0.5f,
			Width = new StyleDimension(480f, 0f),
			Height = new StyleDimension(430f, 0f),
			BackgroundColor = new Color(26, 35, 57) * 0.96f,
			BorderColor = new Color(118, 154, 206)
		};
		Append(panel);

		var title = new UIText("SOUL CREATOR", 1.15f, true) { HAlign = 0.5f, Top = new StyleDimension(12f, 0f) };
		panel.Append(title);

		preview = new UIText("", 0.9f) {
			Top = new StyleDimension(55f, 0f),
			HAlign = 0.5f,
			TextOriginX = 0.5f,
			IsWrapped = true,
			Width = new StyleDimension(-32f, 1f)
		};
		panel.Append(preview);

		AddCycleButton(panel, "Name", 128f, () => {
			nameIndex = (nameIndex + 1) % Names.Length;
			draft.Name = Names[nameIndex];
		});
		AddCycleButton(panel, "Essence", 172f, () => draft.Essence = Next(draft.Essence));
		AddCycleButton(panel, "Personality", 216f, () => draft.Personality = Next(draft.Personality));
		AddCycleButton(panel, "Starting Talent", 260f, () => draft.Talent = Next(draft.Talent));

		status = new UIText("Requires 1 Blank Sigil", 0.8f) {
			Top = new StyleDimension(310f, 0f),
			HAlign = 0.5f,
			TextColor = Color.LightGray
		};
		panel.Append(status);

		var create = Button("CREATE SOULMATE", 350f, 0f, 0.63f, new Color(64, 128, 115));
		create.OnLeftClick += (_, _) => CreateCompanion();
		panel.Append(create);

		var close = Button("CLOSE", 350f, 0.67f, 0.30f, new Color(120, 63, 72));
		close.OnLeftClick += (_, _) => ModContent.GetInstance<SoulCreatorSystem>().Close();
		panel.Append(close);
		Refresh();
	}

	public void ResetDraft()
	{
		nameIndex = Main.rand.Next(Names.Length);
		draft.Id = Guid.NewGuid();
		draft.Name = Names[nameIndex];
		draft.Essence = (CompanionEssence)Main.rand.Next(Enum.GetValues<CompanionEssence>().Length);
		draft.Personality = (CompanionPersonality)Main.rand.Next(Enum.GetValues<CompanionPersonality>().Length);
		draft.Talent = (CompanionTalent)Main.rand.Next(Enum.GetValues<CompanionTalent>().Length);
		draft.Bond = 0;
		draft.Mood = 100;
		draft.Energy = 100;
		Refresh();
	}

	private void AddCycleButton(UIPanel panel, string label, float top, Action cycle)
	{
		var button = Button(label, top, 0f, 1f, new Color(49, 70, 104));
		button.OnLeftClick += (_, _) => {
			cycle();
			SoundEngine.PlaySound(SoundID.MenuTick);
			Refresh();
		};
		panel.Append(button);
	}

	private static UITextPanel<string> Button(string text, float top, float leftPercent, float widthPercent, Color color)
	{
		var button = new UITextPanel<string>(text, 0.82f) {
			Top = new StyleDimension(top, 0f),
			Left = new StyleDimension(0f, leftPercent),
			Width = new StyleDimension(-8f, widthPercent),
			Height = new StyleDimension(34f, 0f),
			BackgroundColor = color,
			BorderColor = color * 1.35f
		};
		button.OnMouseOver += (_, _) => button.BackgroundColor = color * 1.25f;
		button.OnMouseOut += (_, _) => button.BackgroundColor = color;
		return button;
	}

	private void CreateCompanion()
	{
		Player player = Main.LocalPlayer;
		int blankType = ModContent.ItemType<BlankSigil>();
		if (!player.HasItem(blankType)) {
			SetStatus("You need a Blank Sigil.", Color.IndianRed);
			SoundEngine.PlaySound(SoundID.MenuClose);
			return;
		}

		if (!player.ConsumeItem(blankType)) {
			SetStatus("The Blank Sigil could not be consumed.", Color.IndianRed);
			return;
		}

		var item = new Item();
		item.SetDefaults(ModContent.ItemType<SoulboundSigil>());
		((SoulboundSigil)item.ModItem).Profile = draft.Clone();
		Item leftover = player.GetItem(player.whoAmI, item, GetItemSettings.InventoryEntityToPlayerInventorySettings);
		if (!leftover.IsAir)
			Item.NewItem(player.GetSource_Misc("SoulCreator"), player.Hitbox, leftover);

		SoundEngine.PlaySound(SoundID.Item4);
		Main.NewText($"A new soul is bound: {draft.Name}", draft.EssenceColor);
		ModContent.GetInstance<SoulCreatorSystem>().Close();
	}

	private void Refresh()
	{
		if (preview is null)
			return;
		preview.SetText($"{draft.Name}\n{draft.Essence} essence  |  {draft.Personality}\nTalent: {SplitName(draft.Talent.ToString())}");
		preview.TextColor = draft.EssenceColor;
		SetStatus("Requires 1 Blank Sigil", Color.LightGray);
	}

	private void SetStatus(string text, Color color)
	{
		if (status is null)
			return;
		status.SetText(text);
		status.TextColor = color;
	}

	private static T Next<T>(T value) where T : struct, Enum
	{
		T[] values = Enum.GetValues<T>();
		return values[(Array.IndexOf(values, value) + 1) % values.Length];
	}

	private static string SplitName(string value) => System.Text.RegularExpressions.Regex.Replace(value, "([a-z])([A-Z])", "$1 $2");
}
