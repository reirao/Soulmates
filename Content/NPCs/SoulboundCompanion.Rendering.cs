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
	private string cachedSpeech = "";
	private string cachedSpeechName = "";
	private float cachedSpeechWidth;
	private object? cachedSpeechFont;
	private List<string> speechLines = [];
	private Vector2 speechSize;
	private float speechSide = 1f;
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
		DrawInsectCompanions(spriteBatch, center);

		for (int i = 0; i < 4; i++) {
			Vector2 glowOffset = new Vector2(2f, 0f).RotatedBy(MathHelper.PiOver2 * i);
			spriteBatch.Draw(texture, center + glowOffset, source, Profile.EssenceColor * 0.18f, drawRotation, origin, scale, effects, 0f);
		}
		spriteBatch.Draw(texture, center, source, tint, drawRotation, origin, scale, effects, 0f);
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

	internal void DrawSpeechBubble(SpriteBatch spriteBatch)
	{
		if (speechTimer <= 0 || string.IsNullOrWhiteSpace(speechText))
			return;
		Vector2 viewport = SoulmatesUISpace.Viewport;
		Vector2 speaker = SoulmatesUISpace.FromWorld(NPC.Center);
		if (speaker.X < -64f || speaker.X > viewport.X + 64f || speaker.Y < -64f || speaker.Y > viewport.Y + 64f)
			return;
		float maximumWidth = Math.Min(280f, Math.Max(60f, viewport.X - 44f));
		var font = FontAssets.MouseText.Value;
		if (cachedSpeech != speechText || cachedSpeechName != Profile.Name
			|| cachedSpeechWidth != maximumWidth || cachedSpeechFont != font) {
			cachedSpeech = speechText;
			cachedSpeechName = Profile.Name;
			cachedSpeechWidth = maximumWidth;
			cachedSpeechFont = font;
			speechLines = SoulmatesTextLayout.Wrap(speechText, maximumWidth, line => font.MeasureString(line).X * 0.8f);
			float width = Math.Min(maximumWidth, font.MeasureString(Profile.Name).X * 0.62f);
			foreach (string line in speechLines)
				width = Math.Max(width, font.MeasureString(line).X * 0.8f);
			speechSize = new Vector2(width + 24f, speechLines.Count * 23f + 34f);
		}
		float elapsed = Math.Max(0, speechDuration - speechTimer);
		float opacity = Math.Min(MathHelper.Clamp(elapsed / 12f, 0f, 1f),
			MathHelper.Clamp(speechTimer / 120f, 0f, 1f));
		Vector2 anchor = SoulmatesUISpace.FromWorld(speechAnchorWorld == Vector2.Zero ? NPC.Center : speechAnchorWorld);
		Vector2 trail = SoulmatesUISpace.FromWorld(speechTrailWorld == Vector2.Zero ? NPC.Center : speechTrailWorld);
		float trailDistance = Vector2.Distance(anchor, trail);
		if (trailDistance > 24f && trailDistance < 180f) {
			float trailOpacity = opacity * 0.12f * MathHelper.Clamp((trailDistance - 24f) / 48f, 0f, 1f);
			DrawSpeechBubbleAt(spriteBatch, trail, speaker, trailOpacity, drawTail: false);
		}
		DrawSpeechBubbleAt(spriteBatch, anchor, speaker, opacity, drawTail: true);
	}

	private void DrawSpeechBubbleAt(SpriteBatch spriteBatch, Vector2 anchor, Vector2 speaker, float opacity, bool drawTail)
	{
		if (opacity <= 0.01f)
			return;
		Vector2 viewport = SoulmatesUISpace.Viewport;
		Vector2 position = new(anchor.X + speechSide * (speechSize.X * 0.5f + 36f) - speechSize.X * 0.5f,
			anchor.Y - 88f - speechSize.Y);
		if (position.Y < 52f)
			position.Y = anchor.Y + 64f;
		position.X = Math.Clamp(position.X, 8f, Math.Max(8f, viewport.X - speechSize.X - 8f));
		position.Y = Math.Clamp(position.Y, 52f, Math.Max(52f, viewport.Y - speechSize.Y - 10f));
		Rectangle background = new((int)position.X, (int)position.Y, (int)speechSize.X, (int)speechSize.Y);
		Texture2D pixel = TextureAssets.MagicPixel.Value;
		if (drawTail) {
			Vector2 endpoint = new(Math.Clamp(speaker.X, background.Left + 12f, background.Right - 12f),
				speaker.Y < background.Top ? background.Top : background.Bottom);
			Vector2 mouth = speaker + new Vector2(0f, -30f);
			float distance = Vector2.Distance(mouth, endpoint);
			if (distance is > 10f and < 220f) {
				for (int i = 1; i <= 3; i++) {
					Vector2 dot = Vector2.Lerp(mouth, endpoint, i / 4f);
					int size = i + 1;
					spriteBatch.Draw(pixel, new Rectangle((int)dot.X - size / 2, (int)dot.Y - size / 2, size, size),
						Profile.EssenceColor * (opacity * 0.65f));
				}
			}
		}
		spriteBatch.Draw(pixel, background, new Color(11, 17, 29) * (0.94f * opacity));
		spriteBatch.Draw(pixel, new Rectangle(background.X, background.Y, background.Width, 2),
			Profile.EssenceColor * (0.9f * opacity));
		float nameWidth = FontAssets.MouseText.Value.MeasureString(Profile.Name).X;
		float nameScale = nameWidth <= 0f ? 0.62f : Math.Min(0.62f, (speechSize.X - 24f) / nameWidth);
		Utils.DrawBorderString(spriteBatch, Profile.Name, position + new Vector2(12f, 7f),
			Color.Lerp(Profile.EssenceColor, Color.White, 0.3f) * opacity, nameScale);
		for (int i = 0; i < speechLines.Count; i++)
			Utils.DrawBorderString(spriteBatch, speechLines[i], position + new Vector2(12f, 27f + i * 23f),
				Color.White * opacity, 0.8f);
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
		if (activeJob != CompanionJob.None && jobPlannedTotal > 0) {
			const int barWidth = 34;
			float progress = MathHelper.Clamp(jobCount / (float)jobPlannedTotal, 0f, 1f);
			var background = new Rectangle((int)center.X - barWidth / 2, (int)center.Y - 49, barWidth, 4);
			spriteBatch.Draw(pixel, background, new Color(8, 13, 23) * 0.82f);
			int fillWidth = (int)MathF.Round((barWidth - 2) * progress);
			if (fillWidth > 0)
				spriteBatch.Draw(pixel, new Rectangle(background.X + 1, background.Y + 1, fillWidth, 2), color * 0.95f);
		}
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
