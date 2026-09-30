#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Soulmates.Common.Dialogue;
using Soulmates.Common.Feedback;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Soulmates.Common.UI;

public sealed class DirectOrderSystem : ModSystem
{
	private bool active;
	private bool leftMouseDown;
	private bool rightMouseDown;
	private CompanionTargetOrder order;
	private SoulboundCompanion? companion;
	private string cachedLabel = "";
	private string wrappedLabel = "";
	private float cachedLabelWidth;

	public bool IsActive => active;

	public void Begin(SoulboundCompanion target, CompanionTargetOrder targetOrder)
	{
		if (Main.dedServ || Main.gameMenu || Main.LocalPlayer.dead || target.NPC.active != true)
			return;
		companion = target;
		order = targetOrder;
		leftMouseDown = Main.mouseLeft;
		rightMouseDown = Main.mouseRight;
		active = true;
		SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.55f, Pitch = 0.2f });
	}

	public void Cancel()
	{
		active = false;
		companion = null;
		leftMouseDown = false;
		rightMouseDown = false;
	}

	public override void OnWorldUnload() => Cancel();

	public override void UpdateUI(GameTime gameTime)
	{
		if (!active)
			return;
		if (Main.gameMenu || Main.LocalPlayer.dead || Main.playerInventory || companion?.NPC.active != true) {
			Cancel();
			return;
		}

		bool leftDown = Main.mouseLeft;
		bool rightDown = Main.mouseRight;
		bool leftPressed = leftDown && !leftMouseDown;
		bool rightPressed = rightDown && !rightMouseDown;
		leftMouseDown = leftDown;
		rightMouseDown = rightDown;

		if (rightPressed || Main.keyState.IsKeyDown(Keys.Escape) && Main.oldKeyState.IsKeyUp(Keys.Escape)) {
			Main.mouseRightRelease = false;
			Cancel();
			SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.45f });
			return;
		}

		if (leftPressed) {
			Main.mouseLeftRelease = false;
			if (TryResolveTarget(out Point tileTarget, out int itemTarget)) {
				SoulboundCompanion target = companion;
				CompanionTargetOrder selectedOrder = order;
				Cancel();
				SoulmatesFeedbackSystem.Record("direct_order_selected", ("order", selectedOrder.ToString()),
					("tile_x", tileTarget.X), ("tile_y", tileTarget.Y), ("item_index", itemTarget));
				if (Main.netMode == NetmodeID.MultiplayerClient)
					global::Soulmates.Soulmates.SendDirectOrderRequest(selectedOrder, tileTarget, itemTarget);
				else {
					CompanionConversationResult result = target.PerformDirectOrder(selectedOrder, tileTarget, itemTarget);
					target.ShowSpeech(result.Reply);
					SoundEngine.PlaySound(result.Accepted ? SoundID.Chat : SoundID.MenuClose);
				}
				return;
			}
			SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.35f, Pitch = 0.2f });
		}

		Main.LocalPlayer.mouseInterface = true;
		Main.blockMouse = true;
	}

	private bool TryResolveTarget(out Point tileTarget, out int itemTarget)
	{
		tileTarget = Main.MouseWorld.ToTileCoordinates();
		itemTarget = FindHoveredItem();
		if (companion?.NPC.active != true)
			return false;
		return order == CompanionTargetOrder.Mine
			? companion.CanTargetMining(tileTarget)
			: companion.CanTargetGathering(itemTarget);
	}

	private static int FindHoveredItem()
	{
		Vector2 mouse = Main.MouseWorld;
		int result = -1;
		float nearest = float.MaxValue;
		for (int i = 0; i < Main.maxItems; i++) {
			Item item = Main.item[i];
			if (!item.active || item.IsAir)
				continue;
			Rectangle hitbox = item.Hitbox;
			hitbox.Inflate(8, 8);
			if (!hitbox.Contains(mouse.ToPoint()))
				continue;
			float distance = Vector2.DistanceSquared(mouse, item.Center);
			if (distance >= nearest)
				continue;
			nearest = distance;
			result = i;
		}
		return result;
	}

	public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
	{
		int mouseIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
		if (mouseIndex < 0)
			mouseIndex = layers.Count;
		layers.Insert(mouseIndex, new LegacyGameInterfaceLayer("Soulmates: Direct Order", Draw,
			InterfaceScaleType.UI));
	}

	private bool Draw()
	{
		if (!active || companion?.NPC.active != true)
			return true;

		bool valid = TryResolveTarget(out Point tileTarget, out int itemTarget);
		Color accent = valid ? companion.Profile.EssenceColor : new Color(215, 95, 95);
		Rectangle targetRectangle = order == CompanionTargetOrder.Mine
			? SoulmatesUISpace.FromWorld(new Rectangle(tileTarget.X * 16, tileTarget.Y * 16, 16, 16))
			: ItemScreenRectangle(itemTarget);
		if (targetRectangle.Width > 0)
			DrawOutline(Main.spriteBatch, targetRectangle, accent, 2);

		int iconType = order == CompanionTargetOrder.Mine ? ItemID.CopperPickaxe : ItemID.TreasureMagnet;
		Vector2 iconPosition = SoulmatesUISpace.Mouse + new Vector2(30f, 24f);
		DrawCursorIcon(Main.spriteBatch, iconPosition, iconType, accent);

		string label = TargetLabel(valid, tileTarget, itemTarget);
		float scale = 0.62f;
		Vector2 viewport = SoulmatesUISpace.Viewport;
		float labelWidth = Math.Min(440f, Math.Max(20f, viewport.X - 20f));
		if (cachedLabel != label || cachedLabelWidth != labelWidth) {
			cachedLabel = label;
			cachedLabelWidth = labelWidth;
			wrappedLabel = string.Join("\n", SoulmatesTextLayout.Wrap(label, labelWidth,
				line => FontAssets.MouseText.Value.MeasureString(line).X * scale));
		}
		Vector2 size = FontAssets.MouseText.Value.MeasureString(wrappedLabel) * scale;
		Vector2 labelPosition = SoulmatesUISpace.Mouse + new Vector2(24f, 48f);
		labelPosition.X = Math.Clamp(labelPosition.X, 10f, Math.Max(10f, viewport.X - size.X - 10f));
		labelPosition.Y = Math.Clamp(labelPosition.Y, 10f, Math.Max(10f, viewport.Y - size.Y - 10f));
		Utils.DrawBorderString(Main.spriteBatch, wrappedLabel, labelPosition, Color.Lerp(Color.White, accent, 0.25f), scale);
		return true;
	}

	private string TargetLabel(bool valid, Point tileTarget, int itemTarget)
	{
		if (!valid)
			return SoulmatesText.Get(order == CompanionTargetOrder.Mine
				? "UI.DirectOrder.MineInvalid"
				: "UI.DirectOrder.GatherInvalid");
		if (order == CompanionTargetOrder.Gather)
			return SoulmatesText.Get("UI.DirectOrder.GatherValid", Main.item[itemTarget].Name);

		ushort tileType = Main.tile[tileTarget.X, tileTarget.Y].TileType;
		int dropType = TileLoader.GetItemDropFromTypeAndStyle(tileType, 0);
		string name = dropType > ItemID.None
			? Lang.GetItemNameValue(dropType)
			: SoulmatesText.Get("Resourcefulness.UnknownMaterial");
		return SoulmatesText.Get("UI.DirectOrder.MineValid", name);
	}

	private static Rectangle ItemScreenRectangle(int itemIndex)
	{
		if (itemIndex < 0 || itemIndex >= Main.maxItems || !Main.item[itemIndex].active)
			return Rectangle.Empty;
		return SoulmatesUISpace.FromWorld(Main.item[itemIndex].Hitbox);
	}

	private static void DrawOutline(SpriteBatch spriteBatch, Rectangle rectangle, Color color, int thickness)
	{
		Texture2D pixel = TextureAssets.MagicPixel.Value;
		spriteBatch.Draw(pixel, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, thickness), color);
		spriteBatch.Draw(pixel, new Rectangle(rectangle.X, rectangle.Bottom - thickness, rectangle.Width, thickness), color);
		spriteBatch.Draw(pixel, new Rectangle(rectangle.X, rectangle.Y, thickness, rectangle.Height), color);
		spriteBatch.Draw(pixel, new Rectangle(rectangle.Right - thickness, rectangle.Y, thickness, rectangle.Height), color);
	}

	private static void DrawCursorIcon(SpriteBatch spriteBatch, Vector2 position, int itemType, Color accent)
	{
		Texture2D slot = TextureAssets.InventoryBack.Value;
		spriteBatch.Draw(slot, position, null, Color.Lerp(Color.White, accent, 0.18f), 0f,
			slot.Size() * 0.5f, 34f / slot.Width, SpriteEffects.None, 0f);
		Main.instance.LoadItem(itemType);
		Texture2D texture = TextureAssets.Item[itemType].Value;
		Rectangle source = Main.itemAnimations[itemType]?.GetFrame(texture) ?? texture.Bounds;
		float scale = Math.Min(22f / source.Width, 22f / source.Height);
		spriteBatch.Draw(texture, position, source, Color.White, 0f, source.Size() * 0.5f,
			scale, SpriteEffects.None, 0f);
	}
}
