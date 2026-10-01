#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Soulmates.Content.NPCs;
using Soulmates.Common.Feedback;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Soulmates.Common.UI;

public sealed class InitiativePromptSystem : ModSystem
{
	private static readonly CompanionInitiativeResponse[] Responses = [
		CompanionInitiativeResponse.Yes, CompanionInitiativeResponse.No,
		CompanionInitiativeResponse.Always, CompanionInitiativeResponse.Never
	];

	private const float ResponseRadius = 104f;
	private bool open;
	private int openTicks;
	private int hoverIndex = -1;
	private bool leftMouseDown;
	private bool rightMouseDown;
	private Vector2 center;
	private SoulboundCompanion? companion;
	private Guid replyProfileId;
	private CompanionInitiativeKind replyKind;
	private int replyWaitTicks;

	public bool IsOpen => open;

	public void Open(SoulboundCompanion boundCompanion)
	{
		if (!SoulmatesUIInput.CanPresentInitiative || !boundCompanion.HasPendingInitiative || boundCompanion.IsDefending
			|| IsAwaitingReply(boundCompanion))
			return;

		ModContent.GetInstance<CompanionWheelSystem>().Close();
		ModContent.GetInstance<TalkModeSystem>().Close();
		ModContent.GetInstance<FeedbackMailboxSystem>().Close();
		ModContent.GetInstance<SoulCreatorSystem>().Close();
		ModContent.GetInstance<DirectOrderSystem>().Cancel();
		companion = boundCompanion;
		center = ClampCenter(SoulmatesUISpace.FromWorld(Main.LocalPlayer.Center));
		hoverIndex = -1;
		leftMouseDown = Main.mouseLeft;
		rightMouseDown = Main.mouseRight;
		openTicks = 0;
		open = true;
		SoulmatesFeedbackSystem.Record("initiative_prompt", ("action", boundCompanion.PendingInitiativeKind.ToString()));
		SoundEngine.PlaySound(SoundID.MenuOpen with { Volume = 0.55f, Pitch = 0.22f });
	}

	public void Close()
	{
		open = false;
		openTicks = 0;
		hoverIndex = -1;
		leftMouseDown = false;
		rightMouseDown = false;
		companion = null;
	}

	public override void OnWorldUnload()
	{
		Close();
		replyWaitTicks = 0;
		replyProfileId = Guid.Empty;
	}

	private bool IsAwaitingReply(SoulboundCompanion target) => replyWaitTicks > 0
		&& target.Profile.Id == replyProfileId && target.HasPendingInitiative && target.PendingInitiativeKind == replyKind;

	public override void UpdateUI(GameTime gameTime)
	{
		if (replyWaitTicks > 0) {
			SoulboundCompanion? current = SoulboundCompanion.FindFor(Main.LocalPlayer);
			if (current is not null && IsAwaitingReply(current)) {
				replyWaitTicks--;
				return;
			}
			replyWaitTicks = 0;
		}
		if (!open) {
			if (SoulmatesUIInput.CanPresentInitiative
				&& SoulboundCompanion.FindFor(Main.LocalPlayer) is { HasPendingInitiative: true } pending)
				Open(pending);
			return;
		}
		if (Main.gameMenu || Main.LocalPlayer.dead || Main.playerInventory || companion?.NPC.active != true
			|| !companion.HasPendingInitiative || companion.IsDefending) {
			Close();
			return;
		}

		center = ClampCenter(center);
		openTicks++;
		hoverIndex = FindHoveredResponse(SoulmatesUISpace.Mouse);

		bool leftDown = Main.mouseLeft;
		bool rightDown = Main.mouseRight;
		bool leftPressed = leftDown && !leftMouseDown;
		bool rightPressed = rightDown && !rightMouseDown;
		leftMouseDown = leftDown;
		rightMouseDown = rightDown;
		if (leftPressed && hoverIndex >= 0) {
			Main.mouseLeftRelease = false;
			Respond(Responses[hoverIndex]);
		}
		else if (openTicks > 8 && (rightPressed
			|| Main.keyState.IsKeyDown(Keys.Escape) && Main.oldKeyState.IsKeyUp(Keys.Escape))) {
			Main.mouseRightRelease = false;
			Respond(CompanionInitiativeResponse.No);
		}

		Main.LocalPlayer.mouseInterface = true;
		Main.blockMouse = true;
	}

	private void Respond(CompanionInitiativeResponse response)
	{
		SoulboundCompanion? target = companion;
		if (target?.NPC.active != true || !target.HasPendingInitiative) {
			Close();
			return;
		}

		CompanionInitiativeKind kind = target.PendingInitiativeKind;
		SoulmatesFeedbackSystem.Record("initiative_response", ("action", kind.ToString()),
			("response", response.ToString()));
		Close();
		if (Main.netMode == NetmodeID.MultiplayerClient) {
			replyProfileId = target.Profile.Id;
			replyKind = kind;
			replyWaitTicks = 600;
			global::Soulmates.Soulmates.SendInitiativeResponse(kind, response);
		}
		else
			target.RespondToInitiative(response);
		SoundEngine.PlaySound(response is CompanionInitiativeResponse.Yes or CompanionInitiativeResponse.Always
			? SoundID.Chat : SoundID.MenuClose, Main.LocalPlayer.Center);
	}

	public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
	{
		int mouseIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
		if (mouseIndex < 0)
			mouseIndex = layers.Count;
		layers.Insert(mouseIndex, new LegacyGameInterfaceLayer("Soulmates: Initiative Prompt", Draw,
			InterfaceScaleType.UI));
	}

	private bool Draw()
	{
		if (!open || companion?.NPC.active != true)
			return true;

		SpriteBatch spriteBatch = Main.spriteBatch;
		Color accent = companion.Profile.EssenceColor;
		float reveal = MathHelper.Clamp(openTicks / 8f, 0f, 1f);
		float pulse = 1f + MathF.Sin(Main.GlobalTimeWrappedHourly * 4f) * 0.035f;
		for (int i = 0; i < Responses.Length; i++) {
			Vector2 position = Vector2.Lerp(center, ResponsePosition(i), reveal);
			bool hovered = hoverIndex == i;
			DrawNode(spriteBatch, position, hovered ? 43f * pulse : 38f, accent, hovered,
				ResponseEmote(Responses[i]));
		}
		DrawNode(spriteBatch, center, 46f * pulse, accent, false,
			SoulboundCompanion.InitiativeEmote(companion.PendingInitiativeKind));
		DrawPrompt(spriteBatch, accent);
		DrawHoverLabel(spriteBatch, accent);
		return true;
	}

	private void DrawPrompt(SpriteBatch spriteBatch, Color accent)
	{
		string prompt = SoulmatesText.Get("UI.CompanionWheel.InitiativePrompt",
			SoulmatesText.EnumName(companion!.PendingInitiativeKind)).ToUpperInvariant();
		float scale = FitTextScale(prompt, 300f, 0.58f);
		Vector2 size = FontAssets.MouseText.Value.MeasureString(prompt) * scale;
		Vector2 position = new(center.X - size.X * 0.5f, center.Y - 78f);
		Texture2D pixel = TextureAssets.MagicPixel.Value;
		Rectangle background = new((int)position.X - 9, (int)position.Y - 5,
			(int)size.X + 18, (int)size.Y + 10);
		spriteBatch.Draw(pixel, background, new Color(9, 14, 25) * 0.9f);
		spriteBatch.Draw(pixel, new Rectangle(background.X, background.Y, background.Width, 2), accent * 0.9f);
		float remaining = Math.Clamp(companion.PendingInitiativeTicks / 3600f, 0f, 1f);
		spriteBatch.Draw(pixel, new Rectangle(background.X, background.Bottom + 3,
			(int)(background.Width * remaining), 2), accent * 0.85f);
		Utils.DrawBorderString(spriteBatch, prompt, position, Color.Lerp(Color.White, accent, 0.2f), scale);
	}

	private void DrawHoverLabel(SpriteBatch spriteBatch, Color accent)
	{
		if (hoverIndex < 0 || hoverIndex >= Responses.Length)
			return;
		string label = SoulmatesText.Get($"UI.CompanionWheel.InitiativeResponses.{Responses[hoverIndex]}");
		float scale = FitTextScale(label, 300f, 0.68f);
		Vector2 size = FontAssets.MouseText.Value.MeasureString(label) * scale;
		Vector2 position = new(center.X - size.X * 0.5f, center.Y + ResponseRadius + 24f);
		Utils.DrawBorderString(spriteBatch, label, position, Color.Lerp(Color.White, accent, 0.15f), scale);
	}

	private int FindHoveredResponse(Vector2 mouse)
	{
		for (int i = 0; i < Responses.Length; i++) {
			if (Vector2.DistanceSquared(mouse, ResponsePosition(i)) <= 22f * 22f)
				return i;
		}
		return -1;
	}

	private Vector2 ResponsePosition(int index)
	{
		float spread = MathHelper.ToRadians(160f);
		float angle = MathHelper.PiOver2 - spread * 0.5f + spread * index / (Responses.Length - 1f);
		return center + angle.ToRotationVector2() * ResponseRadius;
	}

	private static int ResponseEmote(CompanionInitiativeResponse response) => response switch {
		CompanionInitiativeResponse.Yes => EmoteID.EmoteHappiness,
		CompanionInitiativeResponse.No => EmoteID.EmoteConfused,
		CompanionInitiativeResponse.Always => EmoteID.EmoteWink,
		_ => EmoteID.EmoteScowl
	};

	private static Vector2 ClampCenter(Vector2 desired)
	{
		const float horizontalMargin = ResponseRadius + 48f;
		const float topMargin = 96f;
		const float bottomMargin = ResponseRadius + 56f;
		Vector2 viewport = SoulmatesUISpace.Viewport;
		float x = viewport.X <= horizontalMargin * 2f
			? viewport.X * 0.5f : Math.Clamp(desired.X, horizontalMargin, viewport.X - horizontalMargin);
		float y = viewport.Y <= topMargin + bottomMargin
			? viewport.Y * 0.5f : Math.Clamp(desired.Y, topMargin, viewport.Y - bottomMargin);
		return new Vector2(x, y);
	}

	private static void DrawNode(SpriteBatch spriteBatch, Vector2 position, float size, Color accent,
		bool hovered, int emoteId)
	{
		Texture2D slot = TextureAssets.InventoryBack.Value;
		Color slotColor = hovered ? Color.White : Color.Lerp(Color.White, accent, 0.12f) * 0.94f;
		spriteBatch.Draw(slot, position, null, slotColor, 0f, slot.Size() * 0.5f,
			size / slot.Width, SpriteEffects.None, 0f);
		DrawEmoteIcon(spriteBatch, position, size * 0.64f, emoteId);
	}

	private static void DrawEmoteIcon(SpriteBatch spriteBatch, Vector2 position, float maximumSize, int emoteId)
	{
		if (emoteId < 0 || emoteId >= EmoteID.Count)
			return;
		Texture2D sheet = TextureAssets.Extra[ExtrasID.EmoteBubble].Value;
		int animationFrame = (int)(Main.GlobalTimeWrappedHourly * 3f) % 2;
		int column = emoteId % EmoteBubble.EMOTE_SHEET_EMOTES_PER_ROW * 2 + animationFrame;
		int row = 1 + emoteId / EmoteBubble.EMOTE_SHEET_EMOTES_PER_ROW;
		Rectangle source = sheet.Frame(EmoteBubble.EMOTE_SHEET_HORIZONTAL_FRAMES,
			EmoteBubble.EMOTE_SHEET_VERTICAL_FRAMES, column, row);
		float scale = Math.Min(maximumSize / source.Width, maximumSize / source.Height);
		spriteBatch.Draw(sheet, position, source, Color.White, 0f, source.Size() * 0.5f,
			scale, SpriteEffects.None, 0f);
	}

	private static float FitTextScale(string text, float maximumWidth, float preferredScale)
	{
		float width = FontAssets.MouseText.Value.MeasureString(text).X;
		return width <= 0f ? preferredScale : Math.Min(preferredScale, maximumWidth / width);
	}
}
