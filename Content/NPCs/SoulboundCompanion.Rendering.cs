#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Soulmates.Common;
using Soulmates.Common.Dialogue;
using Soulmates.Common.UI;
using Soulmates.Content.Items;
using Soulmates.Content.Projectiles;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulmates.Content.NPCs;

public sealed partial class SoulboundCompanion
{
	private void UpdateEmoteEffects()
	{
		if (Main.dedServ || emoteTimer % 6 != 0)
			return;
		int dustType = activeEmote switch {
			CompanionEmote.Heart or CompanionEmote.Comfort => DustID.PinkTorch,
			CompanionEmote.Cheer => DustID.GoldFlame,
			CompanionEmote.Laugh => DustID.Confetti,
			CompanionEmote.Rest => DustID.BlueTorch,
			_ => DustID.Enchanted_Gold
		};
		Vector2 position = NPC.Center + new Vector2(Main.rand.NextFloat(-24f, 24f), Main.rand.NextFloat(-42f, -12f));
		Dust dust = Dust.NewDustPerfect(position, dustType, Main.rand.NextVector2Circular(0.8f, 0.8f), 90,
			Profile.EssenceColor, 0.85f);
		dust.noGravity = true;
	}
	public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
	{
		CompanionMuse muse = Enum.IsDefined(Profile.Muse) ? Profile.Muse : CompanionMuse.Soulkin;
		bool actionFrame = guardianTarget >= 0 || activeJob != CompanionJob.None
			|| autonomyActivity != AutonomyActivity.None || NPC.velocity.LengthSquared() > 20f;
		Texture2D texture = CompanionVisuals.GetTexture(muse, actionFrame);
		Rectangle source = CompanionVisuals.GetFrame(muse, texture, actionFrame);
		Vector2 center = NPC.Center - screenPos + new Vector2(0f, IdleBob() * 0.18f) + EmoteDrawOffset();
		Vector2 origin = source.Size() * 0.5f;
		float breath = 1f + MathF.Sin(Main.GlobalTimeWrappedHourly * 2f + bobSeed) * 0.025f;
		float emoteScale = emoteTimer > 0 && activeEmote is CompanionEmote.Heart or CompanionEmote.Cheer
			? 1.04f + MathF.Sin(Main.GlobalTimeWrappedHourly * 6f) * 0.025f
			: 1f;
		Vector2 formScale = Profile.Form switch {
			CompanionForm.Round => new Vector2(1.12f, 0.92f),
			CompanionForm.Wisp => new Vector2(0.88f, 1.14f),
			_ => Vector2.One
		};
		Vector2 scale = formScale * (64f / Math.Max(source.Width, source.Height)) * breath * emoteScale;
		bool flipHorizontally = muse == CompanionMuse.Squirrel ? facing > 0 : facing < 0;
		SpriteEffects effects = flipHorizontally ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
		Color tint = Color.Lerp(Color.White, Profile.EssenceColor, 0.34f);
		float drawRotation = NPC.rotation + EmoteRotationOffset();
		DrawJobOrbit(spriteBatch, center);

		for (int i = 0; i < 4; i++) {
			Vector2 glowOffset = new Vector2(2f, 0f).RotatedBy(MathHelper.PiOver2 * i);
			spriteBatch.Draw(texture, center + glowOffset, source, Profile.EssenceColor * 0.18f, drawRotation, origin, scale, effects, 0f);
		}
		spriteBatch.Draw(texture, center, source, tint, drawRotation, origin, scale, effects, 0f);
		DrawSpeechBubble(spriteBatch, center);
		return false;
	}

	private Vector2 EmoteDrawOffset()
	{
		if (emoteTimer <= 0)
			return Vector2.Zero;
		float time = Main.GlobalTimeWrappedHourly;
		return activeEmote switch {
			CompanionEmote.Cheer => new Vector2(0f, -MathF.Abs(MathF.Sin(time * 7f)) * 9f),
			CompanionEmote.Heart or CompanionEmote.Comfort => new Vector2(0f, MathF.Sin(time * 4f) * 4f - 3f),
			CompanionEmote.Laugh => new Vector2(MathF.Sin(time * 15f) * 3f, 0f),
			CompanionEmote.Rest => new Vector2(0f, 7f),
			_ => Vector2.Zero
		};
	}

	private float EmoteRotationOffset()
	{
		if (emoteTimer <= 0)
			return 0f;
		float time = Main.GlobalTimeWrappedHourly;
		return activeEmote switch {
			CompanionEmote.Wave => MathF.Sin(time * 7f) * 0.13f,
			CompanionEmote.Laugh => MathF.Sin(time * 15f) * 0.055f,
			CompanionEmote.Rest => facing * 0.08f,
			_ => 0f
		};
	}

	private void DrawSpeechBubble(SpriteBatch spriteBatch, Vector2 companionCenter)
	{
		if (speechTimer <= 0 || string.IsNullOrWhiteSpace(speechText))
			return;
		float zoom = Math.Max(1f, Main.GameViewMatrix.Zoom.X);
		float textScale = 0.68f / zoom;
		List<string> lines = WrapSpeech(speechText, 220f / zoom, textScale);
		float width = 0f;
		foreach (string line in lines)
			width = Math.Max(width, FontAssets.MouseText.Value.MeasureString(line).X * textScale);
		float lineHeight = 20f / zoom;
		float paddingX = 11f / zoom;
		float paddingY = 8f / zoom;
		float height = lines.Count * lineHeight;
		float halfWidth = width * 0.5f + paddingX;
		float halfHeight = height * 0.5f + paddingY;
		Vector2 viewportCenter = new(Main.screenWidth * 0.5f, Main.screenHeight * 0.5f);
		float visibleLeft = viewportCenter.X * (1f - 1f / zoom);
		float visibleRight = viewportCenter.X * (1f + 1f / zoom);
		float visibleTop = viewportCenter.Y * (1f - 1f / zoom);
		float visibleBottom = viewportCenter.Y * (1f + 1f / zoom);
		float safeTop = visibleTop + 54f / zoom;
		float side = facing >= 0 ? -1f : 1f;
		float sideOffset = halfWidth + 40f / zoom;
		float preferredX = companionCenter.X + side * sideOffset;
		float minimumX = visibleLeft + halfWidth + 8f / zoom;
		float maximumX = visibleRight - halfWidth - 8f / zoom;
		if (preferredX < minimumX || preferredX > maximumX)
			preferredX = companionCenter.X - side * sideOffset;
		Vector2 bubbleCenter = new(preferredX, companionCenter.Y - (72f / zoom + halfHeight));
		if (bubbleCenter.Y - halfHeight < safeTop)
			bubbleCenter.Y = companionCenter.Y + 54f / zoom + halfHeight;
		bubbleCenter.X = MathHelper.Clamp(bubbleCenter.X, minimumX, maximumX);
		bubbleCenter.Y = MathHelper.Clamp(bubbleCenter.Y, safeTop + halfHeight,
			visibleBottom - halfHeight - 10f / zoom);
		Rectangle background = new((int)(bubbleCenter.X - width * 0.5f - paddingX), (int)(bubbleCenter.Y - height * 0.5f - paddingY),
			(int)(width + paddingX * 2f), (int)(height + paddingY * 2f));
		Texture2D pixel = TextureAssets.MagicPixel.Value;
		spriteBatch.Draw(pixel, background, new Color(11, 17, 29) * 0.9f);
		spriteBatch.Draw(pixel, new Rectangle(background.X, background.Y, background.Width, 2), Profile.EssenceColor * 0.9f);
		for (int i = 0; i < lines.Count; i++) {
			Vector2 size = FontAssets.MouseText.Value.MeasureString(lines[i]) * textScale;
			Vector2 position = new(bubbleCenter.X - size.X * 0.5f, background.Y + 6f / zoom + i * lineHeight);
			Utils.DrawBorderString(spriteBatch, lines[i], position, Color.White, textScale);
		}
	}

	private static List<string> WrapSpeech(string text, float maximumWidth, float scale)
	{
		var lines = new List<string>();
		string current = "";
		foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries)) {
			string candidate = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
			if (!string.IsNullOrEmpty(current) && FontAssets.MouseText.Value.MeasureString(candidate).X * scale > maximumWidth) {
				lines.Add(current);
				current = word;
			}
			else
				current = candidate;
		}
		if (!string.IsNullOrEmpty(current))
			lines.Add(current);
		if (lines.Count == 0)
			lines.Add("...");
		return lines;
	}

	private void DrawJobOrbit(SpriteBatch spriteBatch, Vector2 center)
	{
		bool defending = guardianTarget >= 0;
		bool autonomous = autonomyActivity != AutonomyActivity.None;
		if (activeJob == CompanionJob.None && !defending && !autonomous)
			return;
		Texture2D pixel = TextureAssets.MagicPixel.Value;
		Color color = defending
			? Color.Lerp(Profile.EssenceColor, Color.White, 0.28f)
			: autonomous ? AutonomyColor(autonomyActivity) : JobColor(activeJob);
		float time = Main.GlobalTimeWrappedHourly * (defending ? 4.2f : autonomous ? 3.2f : 2.5f);
		for (int i = 0; i < 3; i++) {
			float angle = time + MathHelper.TwoPi * i / 3f;
			Vector2 point = center + new Vector2(MathF.Cos(angle) * (defending ? 39f : 35f), MathF.Sin(angle) * 13f - 4f);
			int size = i == jobTimer / 10 % 3 ? 5 : 3;
			spriteBatch.Draw(pixel, new Rectangle((int)point.X - size / 2, (int)point.Y - size / 2, size, size), color * 0.88f);
		}
		float pulse = 0.35f + (MathF.Sin(time * 1.6f) + 1f) * (defending ? 0.2f : 0.12f);
		spriteBatch.Draw(pixel, new Rectangle((int)center.X - 16, (int)center.Y - 43, 32, 2), color * pulse);
	}

	private Color JobColor(CompanionJob job) => job switch {
		CompanionJob.Mine => new Color(100, 188, 255),
		CompanionJob.Gather => new Color(121, 230, 151),
		CompanionJob.FindTreasure => new Color(255, 221, 104),
		_ => Profile.EssenceColor
	};

	private Color AutonomyColor(AutonomyActivity activity) => activity switch {
		AutonomyActivity.FetchItem => new Color(121, 230, 151),
		AutonomyActivity.AssistMining => new Color(100, 188, 255),
		AutonomyActivity.InspectTreasure => new Color(255, 221, 104),
		_ => Profile.EssenceColor
	};

	public override Color? GetAlpha(Color drawColor) => Color.Lerp(drawColor, Profile.EssenceColor, 0.42f);
}
