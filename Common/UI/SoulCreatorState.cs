#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Soulmates.Content.Items;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Soulmates.Common.UI;

public sealed class SoulCreatorState : UIState
{
	private static readonly string[] Names = ["Luma", "Nova", "Moss", "Cinder", "Echo", "Pip", "Rune", "Mira", "AETHER"];
	private readonly CompanionProfile draft = new();
	private readonly List<Action> refreshButtons = [];
	private int nameIndex;
	private UIText? details;
	private UIText? status;
	private bool creationPending;

	public override void OnInitialize()
	{
		var panel = new UIPanel {
			HAlign = 0.5f,
			VAlign = 0.5f,
			Width = new StyleDimension(680f, 0f),
			Height = new StyleDimension(480f, 0f),
			BackgroundColor = new Color(20, 29, 48) * 0.98f,
			BorderColor = new Color(118, 154, 206)
		};
		Append(panel);

		var creatorTitle = new UIText("", 1.15f, true) {
			HAlign = 0.5f,
			Top = new StyleDimension(8f, 0f)
		};
		panel.Append(creatorTitle);
		refreshButtons.Add(() => creatorTitle.SetText(SoulmatesText.Get("UI.Creator.Title")));

		var previewPanel = new UIPanel {
			Left = new StyleDimension(14f, 0f),
			Top = new StyleDimension(46f, 0f),
			Width = new StyleDimension(250f, 0f),
			Height = new StyleDimension(344f, 0f),
			BackgroundColor = new Color(10, 17, 31),
			BorderColor = new Color(67, 93, 133)
		};
		panel.Append(previewPanel);

		previewPanel.Append(new SoulPreviewElement(() => draft) {
			Left = new StyleDimension(10f, 0f),
			Top = new StyleDimension(8f, 0f),
			Width = new StyleDimension(-20f, 1f),
			Height = new StyleDimension(220f, 0f)
		});

		details = new UIText("", 0.86f) {
			Left = new StyleDimension(10f, 0f),
			Top = new StyleDimension(232f, 0f),
			Width = new StyleDimension(-20f, 1f),
			TextOriginX = 0.5f,
			HAlign = 0.5f,
			IsWrapped = true
		};
		previewPanel.Append(details);

		float left = 280f;
		float width = 376f;
		AddCycleButton(panel, "UI.Creator.Fields.Name", 48f, left, width, () => draft.Name, () => {
			nameIndex = (nameIndex + 1) % Names.Length;
			draft.Name = Names[nameIndex];
		});
		AddCycleButton(panel, "UI.Creator.Fields.Muse", 87f, left, width, () => SoulmatesText.EnumName(draft.Muse), () => draft.Muse = Next(draft.Muse));
		AddCycleButton(panel, "UI.Creator.Fields.Form", 126f, left, width, () => SoulmatesText.EnumName(draft.Form), () => draft.Form = Next(draft.Form));
		AddCycleButton(panel, "UI.Creator.Fields.Essence", 165f, left, width, () => SoulmatesText.EnumName(draft.Essence), () => draft.Essence = Next(draft.Essence));
		AddCycleButton(panel, "UI.Creator.Fields.Aura", 204f, left, width, () => SoulmatesText.EnumName(draft.Aura), () => draft.Aura = Next(draft.Aura));
		AddCycleButton(panel, "UI.Creator.Fields.Personality", 243f, left, width, () => SoulmatesText.EnumName(draft.Personality), () => draft.Personality = Next(draft.Personality));
		AddCycleButton(panel, "UI.Creator.Fields.Talent", 282f, left, width,
			() => draft.IsAether ? SoulmatesText.Get("UI.Talk.OmniSoul") : SoulmatesText.EnumName(draft.Talent),
			() => draft.Talent = Next(draft.Talent));

		var randomize = Button("", 324f, left, width, new Color(82, 74, 116));
		refreshButtons.Add(() => randomize.SetText(SoulmatesText.Get("UI.Creator.Randomize")));
		randomize.OnLeftClick += (_, _) => RandomizeDraft();
		panel.Append(randomize);

		status = new UIText(SoulmatesText.Get("UI.Creator.RequiresSigil"), 0.8f) {
			Left = new StyleDimension(left, 0f),
			Top = new StyleDimension(362f, 0f),
			Width = new StyleDimension(width, 0f),
			TextOriginX = 0.5f,
			TextColor = Color.LightGray
		};
		panel.Append(status);

		var create = Button("", 410f, 14f, 450f, new Color(55, 129, 112));
		refreshButtons.Add(() => create.SetText(SoulmatesText.Get("UI.Creator.Create")));
		create.OnLeftClick += (_, _) => CreateCompanion();
		panel.Append(create);

		var close = Button("", 410f, 480f, 176f, new Color(120, 63, 72));
		refreshButtons.Add(() => close.SetText(SoulmatesText.Get("UI.Common.Close")));
		close.OnLeftClick += (_, _) => ModContent.GetInstance<SoulCreatorSystem>().Close();
		panel.Append(close);
		Refresh();
	}

	public void ResetDraft()
	{
		nameIndex = Main.rand.Next(Names.Length);
		draft.Id = Guid.NewGuid();
		draft.Name = Names[nameIndex];
		draft.Muse = CompanionMuse.Soulkin;
		draft.Form = (CompanionForm)Main.rand.Next(Enum.GetValues<CompanionForm>().Length);
		draft.Essence = (CompanionEssence)Main.rand.Next(Enum.GetValues<CompanionEssence>().Length);
		draft.Aura = (CompanionAura)Main.rand.Next(Enum.GetValues<CompanionAura>().Length);
		draft.Personality = (CompanionPersonality)Main.rand.Next(Enum.GetValues<CompanionPersonality>().Length);
		draft.Talent = (CompanionTalent)Main.rand.Next(Enum.GetValues<CompanionTalent>().Length);
		draft.Bond = 0;
		draft.Mood = 100;
		draft.Energy = 100;
		draft.Voice = CompanionVoice.Soft;
		draft.Trinket = CompanionTrinket.None;
		draft.Routine = CompanionJob.None;
		draft.AutonomyEnabled = true;
		draft.JobsCompleted = 0;
		draft.Experience = 0;
		draft.DefeatedEnemies = 0;
		draft.Interactions = 0;
		draft.GatheringInsight = 0;
		draft.MiningInsight = 0;
		draft.ForestryInsight = 0;
		draft.CombatInsight = 0;
		draft.ExplorationInsight = 0;
		draft.LastMemory = SoulmatesText.Get("Memories.New");
		draft.Pack.Clear();
		draft.Memories.Clear();
		creationPending = false;
		SoundEngine.PlaySound(SoundID.MenuTick);
		Refresh();
	}

	private void RandomizeDraft()
	{
		ResetDraft();
		draft.Muse = (CompanionMuse)Main.rand.Next(Enum.GetValues<CompanionMuse>().Length);
		Refresh();
	}

	private void AddCycleButton(UIPanel panel, string labelKey, float top, float left, float width, Func<string> value, Action cycle)
	{
		var button = Button(SoulmatesText.Get("UI.Creator.Field", SoulmatesText.Get(labelKey), value()), top, left, width, new Color(43, 64, 98));
		refreshButtons.Add(() => button.SetText(SoulmatesText.Get("UI.Creator.Field", SoulmatesText.Get(labelKey), value())));
		button.OnLeftClick += (_, _) => {
			cycle();
			SoundEngine.PlaySound(SoundID.MenuTick);
			Refresh();
		};
		panel.Append(button);
	}

	private static UITextPanel<string> Button(string text, float top, float left, float width, Color color)
	{
		var button = new UITextPanel<string>(text, 0.82f) {
			Top = new StyleDimension(top, 0f),
			Left = new StyleDimension(left, 0f),
			Width = new StyleDimension(width, 0f),
			Height = new StyleDimension(33f, 0f),
			BackgroundColor = color,
			BorderColor = color * 1.35f
		};
		button.OnMouseOver += (_, _) => button.BackgroundColor = color * 1.25f;
		button.OnMouseOut += (_, _) => button.BackgroundColor = color;
		return button;
	}

	private void CreateCompanion()
	{
		if (creationPending)
			return;
		Player player = Main.LocalPlayer;
		int blankType = ModContent.ItemType<BlankSigil>();
		if (!player.HasItem(blankType)) {
			SetStatus(SoulmatesText.Get("UI.Creator.NeedSigil"), Color.IndianRed);
			SoundEngine.PlaySound(SoundID.MenuClose);
			return;
		}

		if (Main.netMode == NetmodeID.MultiplayerClient) {
			creationPending = true;
			SetStatus(SoulmatesText.Get("UI.Creator.Creating"), draft.EssenceColor);
			global::Soulmates.Soulmates.SendCreateCompanionRequest(draft);
			return;
		}

		if (!player.ConsumeItem(blankType)) {
			SetStatus(SoulmatesText.Get("UI.Creator.ConsumeFailed"), Color.IndianRed);
			return;
		}

		var item = new Item();
		item.SetDefaults(ModContent.ItemType<SoulboundSigil>());
		CompanionProfile createdProfile = draft.Clone();
		createdProfile.Remember(CompanionMemoryKind.Awakened);
		((SoulboundSigil)item.ModItem).Profile = createdProfile;
		Item leftover = player.GetItem(player.whoAmI, item, GetItemSettings.InventoryEntityToPlayerInventorySettings);
		if (!leftover.IsAir)
			Item.NewItem(player.GetSource_Misc("SoulCreator"), player.Hitbox, leftover);

		SoundEngine.PlaySound(SoundID.Item4);
		Main.NewText(SoulmatesText.Get("Messages.SoulCreated", draft.Name), draft.EssenceColor);
		ModContent.GetInstance<SoulCreatorSystem>().Close();
	}

	internal void ReceiveNetworkResponse(bool accepted, string companionName, CompanionEssence essence)
	{
		creationPending = false;
		if (!accepted) {
			SetStatus(SoulmatesText.Get("UI.Creator.ConsumeFailed"), Color.IndianRed);
			SoundEngine.PlaySound(SoundID.MenuClose);
			return;
		}

		SoundEngine.PlaySound(SoundID.Item4);
		Color color = new CompanionProfile { Essence = essence }.EssenceColor;
		Main.NewText(SoulmatesText.Get("Messages.SoulCreated", companionName), color);
		ModContent.GetInstance<SoulCreatorSystem>().Close();
	}

	private void Refresh()
	{
		if (details is null)
			return;
		foreach (Action refreshButton in refreshButtons)
			refreshButton();
		string talent = draft.IsAether ? SoulmatesText.Get("UI.Talk.OmniSoul") : SoulmatesText.EnumName(draft.Talent);
		details.SetText(SoulmatesText.Get("UI.Creator.Details", draft.Name, SoulmatesText.EnumName(draft.Muse), SoulmatesText.EnumName(draft.Form),
			SoulmatesText.EnumName(draft.Essence), SoulmatesText.EnumName(draft.Aura), SoulmatesText.EnumName(draft.Personality), talent));
		details.TextColor = draft.EssenceColor;
		SetStatus(SoulmatesText.Get("UI.Creator.RequiresSigil"), Color.LightGray);
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

}

internal sealed class SoulPreviewElement(Func<CompanionProfile> getProfile) : UIElement
{
	protected override void DrawSelf(SpriteBatch spriteBatch)
	{
		base.DrawSelf(spriteBatch);
		CompanionProfile profile = getProfile();
		CalculatedStyle area = GetDimensions();
		Vector2 center = new(area.X + area.Width * 0.5f, area.Y + area.Height * 0.52f);
		float time = Main.GlobalTimeWrappedHourly;
		center.Y += MathF.Sin(time * 2.1f) * 6f;
		DrawAura(spriteBatch, profile, center, time);

		Texture2D pet = CompanionVisuals.GetTexture(profile.Muse);
		Rectangle source = CompanionVisuals.GetFrame(profile.Muse, pet);
		Vector2 formScale = profile.Form switch {
			CompanionForm.Round => new Vector2(1.12f, 0.92f),
			CompanionForm.Wisp => new Vector2(0.88f, 1.14f),
			_ => Vector2.One
		};
		float baseScale = 190f / Math.Max(source.Width, source.Height);
		Color tint = Color.Lerp(Color.White, profile.EssenceColor, 0.36f);
		Vector2 origin = source.Size() * 0.5f;
		float rotation = MathF.Sin(time * 1.4f) * 0.025f;

		for (int i = 0; i < 4; i++) {
			Vector2 glowOffset = new Vector2(3f, 0f).RotatedBy(MathHelper.PiOver2 * i);
			spriteBatch.Draw(pet, center + glowOffset, source, profile.EssenceColor * 0.16f, rotation, origin, formScale * baseScale, SpriteEffects.None, 0f);
		}
		spriteBatch.Draw(pet, center, source, tint, rotation, origin, formScale * baseScale, SpriteEffects.None, 0f);
	}

	private static void DrawAura(SpriteBatch spriteBatch, CompanionProfile profile, Vector2 center, float time)
	{
		Texture2D pixel = TextureAssets.MagicPixel.Value;
		int count = profile.Aura == CompanionAura.SoftGlow ? 4 : 7;
		for (int i = 0; i < count; i++) {
			float phase = time * (profile.Aura == CompanionAura.SoulSparks ? 1.8f : 0.8f) + MathHelper.TwoPi * i / count;
			float radius = profile.Aura == CompanionAura.SoftGlow ? 58f : 82f;
			Vector2 point = center + new Vector2(MathF.Cos(phase) * radius, MathF.Sin(phase) * radius * 0.55f);
			float size = profile.Aura == CompanionAura.OrbitingStars ? 6f : 3f + (i % 2) * 2f;
			spriteBatch.Draw(pixel, new Rectangle((int)(point.X - size), (int)(point.Y - size), (int)(size * 2f), (int)(size * 2f)), profile.EssenceColor * 0.75f);
		}
	}
}
