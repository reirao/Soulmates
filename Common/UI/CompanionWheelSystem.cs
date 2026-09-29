#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Soulmates.Common.Dialogue;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Soulmates.Common.UI;

public sealed class CompanionWheelSystem : ModSystem
{
	private static readonly CompanionQuickAction[] Entries = [
		CompanionQuickAction.Follow,
		CompanionQuickAction.Stay,
		CompanionQuickAction.Explore,
		CompanionQuickAction.FindTreasure,
		CompanionQuickAction.Mine,
		CompanionQuickAction.Gather,
		CompanionQuickAction.ToggleAutonomy,
		CompanionQuickAction.Details
	];

	private const float WheelRadius = 108f;
	private bool open;
	private int selected = -1;
	private int openTicks;
	private Vector2 center;
	private SoulboundCompanion? companion;

	public bool IsOpen => open;

	public void Open(SoulboundCompanion boundCompanion)
	{
		if (Main.dedServ || Main.gameMenu || Main.LocalPlayer.dead || open)
			return;

		ModContent.GetInstance<EmoteWheelSystem>().Close();
		ModContent.GetInstance<TalkModeSystem>().Close();
		companion = boundCompanion;
		center = new Vector2(
			Math.Clamp(Main.MouseScreen.X, 148f, Main.screenWidth - 148f),
			Math.Clamp(Main.MouseScreen.Y, 148f, Main.screenHeight - 148f));
		selected = -1;
		openTicks = 0;
		open = true;
		SoundEngine.PlaySound(SoundID.MenuOpen with { Volume = 0.55f, Pitch = 0.18f });
	}

	public void Close()
	{
		open = false;
		selected = -1;
		openTicks = 0;
		companion = null;
	}

	private void Release()
	{
		if (!open)
			return;

		SoulboundCompanion? target = companion;
		int choice = selected;
		Close();
		if (target?.NPC.active != true || choice < 0 || choice >= Entries.Length) {
			SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.42f });
			return;
		}

		CompanionQuickAction action = Entries[choice];
		if (action == CompanionQuickAction.Details) {
			if (target.FindBoundSigil() is { } sigil)
				ModContent.GetInstance<TalkModeSystem>().Open(sigil, target);
			SoundEngine.PlaySound(SoundID.MenuOpen with { Volume = 0.6f });
			return;
		}

		if (Main.netMode == NetmodeID.MultiplayerClient)
			global::Soulmates.Soulmates.SendQuickActionRequest(action);
		else {
			CompanionConversationResult result = target.PerformQuickAction(action);
			target.ShowSpeech(result.Reply);
			SoundEngine.PlaySound(result.Accepted ? SoundID.Chat : SoundID.MenuClose);
		}
	}

	public override void UpdateUI(GameTime gameTime)
	{
		if (!open)
			return;
		if (Main.gameMenu || Main.LocalPlayer.dead || companion?.NPC.active != true) {
			Close();
			return;
		}
		if (!Main.mouseRight) {
			Release();
			return;
		}

		Main.LocalPlayer.mouseInterface = true;
		Main.blockMouse = true;
		openTicks++;
		Vector2 offset = Main.MouseScreen - center;
		if (offset.LengthSquared() < 34f * 34f) {
			selected = -1;
			return;
		}

		float angle = MathF.Atan2(offset.Y, offset.X) + MathHelper.PiOver2;
		if (angle < 0f)
			angle += MathHelper.TwoPi;
		selected = (int)MathF.Round(angle / (MathHelper.TwoPi / Entries.Length)) % Entries.Length;
	}

	public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
	{
		int mouseIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
		if (mouseIndex < 0)
			mouseIndex = layers.Count;
		layers.Insert(mouseIndex, new LegacyGameInterfaceLayer("Soulmates: Companion Wheel", Draw, InterfaceScaleType.UI));
	}

	private bool Draw()
	{
		if (!open || companion?.NPC.active != true)
			return true;

		SpriteBatch spriteBatch = Main.spriteBatch;
		Texture2D pixel = TextureAssets.MagicPixel.Value;
		Color accent = companion.Profile.EssenceColor;
		float reveal = MathHelper.Clamp(openTicks / 7f, 0f, 1f);
		float pulse = 1f + MathF.Sin(Main.GlobalTimeWrappedHourly * 5f) * 0.045f;

		for (int i = 0; i < Entries.Length; i++) {
			float angle = -MathHelper.PiOver2 + MathHelper.TwoPi * i / Entries.Length;
			Vector2 direction = angle.ToRotationVector2();
			Vector2 node = center + direction * WheelRadius * reveal;
			bool active = i == selected;
			for (int dot = 0; dot < 3; dot++) {
				Vector2 dotPosition = center + direction * (48f + dot * 15f) * reveal;
				DrawDiamond(spriteBatch, pixel, dotPosition, active ? 4f : 3f,
					active ? accent * 0.82f : accent * 0.28f);
			}

			float size = (active ? 52f : 46f) * (active ? pulse : 1f);
			Color fill = active ? Color.Lerp(new Color(24, 31, 48), accent, 0.58f) : new Color(22, 30, 47) * 0.96f;
			DrawDiamond(spriteBatch, pixel, node, size + 6f, active ? Color.White * 0.82f : accent * 0.5f);
			DrawDiamond(spriteBatch, pixel, node, size, fill);

			string label = Label(Entries[i], companion.Profile).ToUpperInvariant();
			float scale = FitTextScale(label, 38f, 0.51f);
			Vector2 textSize = FontAssets.MouseText.Value.MeasureString(label) * scale;
			Utils.DrawBorderString(spriteBatch, label, node - textSize * 0.5f,
				active ? Color.White : Color.LightGray, scale);
		}

		DrawDiamond(spriteBatch, pixel, center, 61f * pulse, accent * 0.72f);
		DrawDiamond(spriteBatch, pixel, center, 53f * pulse, new Color(12, 19, 32) * 0.98f);
		string centerLabel = selected >= 0
			? Label(Entries[selected], companion.Profile).ToUpperInvariant()
			: SoulmatesText.Get("UI.CompanionWheel.Center");
		float centerScale = FitTextScale(centerLabel, 43f, 0.57f);
		Vector2 centerSize = FontAssets.MouseText.Value.MeasureString(centerLabel) * centerScale;
		Utils.DrawBorderString(spriteBatch, centerLabel, center - centerSize * 0.5f,
			selected >= 0 ? Color.White : accent, centerScale);

		string status = companion.CurrentJobName.ToUpperInvariant();
		float statusScale = FitTextScale(status, 250f, 0.48f);
		Vector2 statusSize = FontAssets.MouseText.Value.MeasureString(status) * statusScale;
		Vector2 statusPosition = center + new Vector2(-statusSize.X * 0.5f, WheelRadius + 41f);
		Utils.DrawBorderString(spriteBatch, status, statusPosition, Color.Lerp(Color.LightGray, accent, 0.35f), statusScale);
		return true;
	}

	private static string Label(CompanionQuickAction action, CompanionProfile profile)
	{
		if (action == CompanionQuickAction.ToggleAutonomy)
			return SoulmatesText.Get(profile.AutonomyEnabled
				? "UI.CompanionWheel.AutonomyOn"
				: "UI.CompanionWheel.AutonomyOff");
		return SoulmatesText.Get($"UI.CompanionWheel.Actions.{action}");
	}

	private static void DrawDiamond(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float size, Color color)
	{
		float side = size / MathF.Sqrt(2f);
		Vector2 origin = new(pixel.Width * 0.5f, pixel.Height * 0.5f);
		Vector2 scale = new(side / pixel.Width, side / pixel.Height);
		spriteBatch.Draw(pixel, center, null, color, MathHelper.PiOver4, origin, scale, SpriteEffects.None, 0f);
	}

	private static float FitTextScale(string text, float maximumWidth, float preferredScale)
	{
		float width = FontAssets.MouseText.Value.MeasureString(text).X;
		return width <= 0f ? preferredScale : Math.Min(preferredScale, maximumWidth / width);
	}
}
