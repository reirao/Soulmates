using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Soulmates.Common.UI;

public sealed class EmoteWheelSystem : ModSystem
{
	private static readonly CompanionEmote[] Entries = [
		CompanionEmote.Wave,
		CompanionEmote.Heart,
		CompanionEmote.Cheer,
		CompanionEmote.Laugh,
		CompanionEmote.Comfort,
		CompanionEmote.Rest
	];

	private const float WheelRadius = 82f;
	private bool open;
	private int selected = -1;
	private int openTicks;
	private Vector2 center;
	private SoulboundCompanion? companion;
	private bool releaseOnRightMouseUp;

	public bool IsOpen => open;

	public void Open(SoulboundCompanion boundCompanion, bool releaseOnRightMouseUp = false)
	{
		if (Main.dedServ || Main.gameMenu || Main.LocalPlayer.dead || open)
			return;
		companion = boundCompanion;
		this.releaseOnRightMouseUp = releaseOnRightMouseUp;
		center = new Vector2(
			Math.Clamp(Main.MouseScreen.X, 118f, Main.screenWidth - 118f),
			Math.Clamp(Main.MouseScreen.Y, 118f, Main.screenHeight - 118f));
		selected = -1;
		openTicks = 0;
		open = true;
		SoundEngine.PlaySound(SoundID.MenuOpen with { Volume = 0.55f, Pitch = 0.25f });
	}

	public void Release()
	{
		if (!open)
			return;
		SoulboundCompanion? target = companion;
		int choice = selected;
		Close();
		if (target?.NPC.active != true || choice < 0 || choice >= Entries.Length) {
			SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.45f });
			return;
		}

		CompanionEmote emote = Entries[choice];
		if (Main.netMode == NetmodeID.MultiplayerClient)
			global::Soulmates.Soulmates.SendEmoteRequest(emote);
		else
			target.PerformEmote(emote);
		SoundEngine.PlaySound(SoundID.Chat with { Volume = 0.6f, Pitch = 0.2f });
	}

	public void Close()
	{
		open = false;
		selected = -1;
		openTicks = 0;
		companion = null;
		releaseOnRightMouseUp = false;
	}

	public override void UpdateUI(GameTime gameTime)
	{
		if (!open)
			return;
		if (Main.gameMenu || Main.LocalPlayer.dead || companion?.NPC.active != true) {
			Close();
			return;
		}
		if (releaseOnRightMouseUp && !Main.mouseRight) {
			Release();
			return;
		}

		Main.LocalPlayer.mouseInterface = true;
		Main.blockMouse = true;
		openTicks++;
		Vector2 offset = Main.MouseScreen - center;
		if (offset.LengthSquared() < 30f * 30f) {
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
		layers.Insert(mouseIndex, new LegacyGameInterfaceLayer("Soulmates: Emote Wheel", Draw, InterfaceScaleType.UI));
	}

	private bool Draw()
	{
		if (!open || companion?.NPC.active != true)
			return true;

		SpriteBatch spriteBatch = Main.spriteBatch;
		Texture2D pixel = TextureAssets.MagicPixel.Value;
		Color accent = companion.Profile.EssenceColor;
		float reveal = MathHelper.Clamp(openTicks / 8f, 0f, 1f);
		float pulse = 1f + MathF.Sin(Main.GlobalTimeWrappedHourly * 5f) * 0.06f;

		for (int i = 0; i < Entries.Length; i++) {
			float angle = -MathHelper.PiOver2 + MathHelper.TwoPi * i / Entries.Length;
			Vector2 direction = angle.ToRotationVector2();
			Vector2 node = center + direction * WheelRadius * reveal;
			DrawLine(spriteBatch, pixel, center + direction * 29f, node - direction * 24f,
				(i == selected ? accent : accent * 0.32f), i == selected ? 3f : 2f);

			bool active = i == selected;
			float size = (active ? 48f : 40f) * (active ? pulse : 1f);
			Color fill = active ? Color.Lerp(new Color(24, 31, 48), accent, 0.62f) : new Color(24, 31, 48) * 0.94f;
			DrawDiamond(spriteBatch, pixel, node, size + 6f, active ? Color.White * 0.85f : accent * 0.55f);
			DrawDiamond(spriteBatch, pixel, node, size, fill);

			string label = SoulmatesText.EnumName(Entries[i]).ToUpperInvariant();
			Vector2 textSize = FontAssets.MouseText.Value.MeasureString(label) * 0.58f;
			Utils.DrawBorderString(spriteBatch, label, node - textSize * 0.5f, active ? Color.White : Color.LightGray, 0.58f);
		}

		DrawDiamond(spriteBatch, pixel, center, 50f * pulse, accent * 0.7f);
		DrawDiamond(spriteBatch, pixel, center, 42f * pulse, new Color(13, 20, 34) * 0.98f);
		string centerText = selected >= 0
			? SoulmatesText.EnumName(Entries[selected]).ToUpperInvariant()
			: SoulmatesText.Get("UI.Emotes.Center");
		Vector2 centerSize = FontAssets.MouseText.Value.MeasureString(centerText) * 0.62f;
		Utils.DrawBorderString(spriteBatch, centerText, center - centerSize * 0.5f,
			selected >= 0 ? Color.White : accent, 0.62f);
		return true;
	}

	private static void DrawDiamond(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float size, Color color)
	{
		float side = size / MathF.Sqrt(2f);
		Vector2 origin = new(pixel.Width * 0.5f, pixel.Height * 0.5f);
		Vector2 scale = new(side / pixel.Width, side / pixel.Height);
		spriteBatch.Draw(pixel, center, null, color, MathHelper.PiOver4, origin, scale, SpriteEffects.None, 0f);
	}

	private static void DrawLine(SpriteBatch spriteBatch, Texture2D pixel, Vector2 start, Vector2 end, Color color, float width)
	{
		Vector2 edge = end - start;
		Vector2 origin = new(0f, pixel.Height * 0.5f);
		Vector2 scale = new(edge.Length() / pixel.Width, width / pixel.Height);
		spriteBatch.Draw(pixel, start, null, color, edge.ToRotation(), origin, scale, SpriteEffects.None, 0f);
	}
}
