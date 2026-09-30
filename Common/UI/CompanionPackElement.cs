#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.UI;

namespace Soulmates.Common.UI;

internal sealed class CompanionPackElement(
	Func<CompanionProfile?> getProfile,
	Action<CompanionStorage, int, bool> withdraw) : UIElement
{
	private readonly List<(CompanionStorage Storage, TalkItemButton Button)> tabs = [];
	private TalkItemButton? pageButton;
	private CompanionStorage selectedStorage;
	private int page;
	private int PageCount => selectedStorage == CompanionStorage.Resources
		? Math.Max(1, ((getProfile()?.ResourceLoad ?? 0) + 11) / 12) : 1;

	public void Reset()
	{
		selectedStorage = CompanionStorage.Pack;
		page = 0;
	}

	public override void OnInitialize()
	{
		foreach (CompanionStorage storage in Enum.GetValues<CompanionStorage>()) {
			int icon = storage switch {
				CompanionStorage.Resources => ItemID.CopperOre,
				CompanionStorage.Wallet => ItemID.GoldCoin,
				_ => ItemID.PiggyBank
			};
			var button = new TalkItemButton(icon) {
				Left = new StyleDimension((int)storage * 30f, 0f),
				Width = new StyleDimension(26f, 0f),
				Height = new StyleDimension(26f, 0f)
			};
			button.OnLeftClick += (_, _) => { selectedStorage = storage; page = 0; };
			tabs.Add((storage, button));
			Append(button);
		}
		pageButton = new TalkItemButton(ItemID.Book) {
			Left = new StyleDimension(90f, 0f),
			Width = new StyleDimension(26f, 0f),
			Height = new StyleDimension(26f, 0f)
		};
		pageButton.OnLeftClick += (_, _) => ChangePage(1);
		pageButton.OnRightClick += (_, _) => ChangePage(-1);
		Append(pageButton);
		OnScrollWheel += (evt, _) => ChangePage(evt.ScrollWheelValue > 0 ? -1 : 1);
		OnLeftClick += (_, _) => Withdraw(singleItem: false);
		OnRightClick += (_, _) => Withdraw(singleItem: true);
	}

	private void ChangePage(int change)
		=> page = (page + change + PageCount) % PageCount;

	public override void Update(GameTime gameTime)
	{
		LayoutTabs();
		base.Update(gameTime);
	}

	private void LayoutTabs()
	{
		float step = Math.Min(30f, GetDimensions().Width / 4f);
		float size = Math.Max(1f, step - Math.Min(4f, step * 0.2f));
		foreach ((CompanionStorage storage, TalkItemButton button) in tabs) {
			button.Left.Set((int)storage * step, 0f);
			button.Width.Set(size, 0f);
			button.Height.Set(size, 0f);
			button.Recalculate();
		}
		if (pageButton is not null) {
			pageButton.Left.Set(3f * step, 0f);
			pageButton.Width.Set(size, 0f);
			pageButton.Height.Set(size, 0f);
			pageButton.IgnoresMouseInteraction = PageCount <= 1;
			pageButton.Recalculate();
		}
	}

	protected override void DrawSelf(SpriteBatch spriteBatch)
	{
		CompanionProfile? profile = getProfile();
		if (profile is null)
			return;
		page = Math.Clamp(page, 0, PageCount - 1);
		foreach ((CompanionStorage storage, TalkItemButton button) in tabs) {
			button.Selected = storage == selectedStorage;
			button.HoverText = SoulmatesText.EnumName(storage);
		}
		if (pageButton is not null) {
			pageButton.Selected = selectedStorage == CompanionStorage.Resources && PageCount > 1;
			pageButton.HoverText = SoulmatesText.Get("Storage.PageHint");
		}
		CalculatedStyle area = GetDimensions();
		string summary = selectedStorage switch {
			CompanionStorage.Resources => SoulmatesText.Get("Storage.ResourceSummary", profile.ResourceCarryLimit, page + 1, PageCount),
			CompanionStorage.Wallet => SoulmatesText.Get("Storage.Unlimited"),
			_ => $"{profile.PackLoad}/{profile.PackCapacity}"
		};
		DrawFitted(spriteBatch, summary, new Vector2(area.X + 122f, area.Y + 6f), area.Width - 122f, 0.5f);
		List<Item>? items = selectedStorage == CompanionStorage.Wallet ? null : profile.StorageItems(selectedStorage);
		int slots = selectedStorage == CompanionStorage.Wallet ? 4 : 12;
		for (int slot = 0; slot < slots; slot++) {
			Rectangle bounds = SlotBounds(slot);
			int index = page * 12 + slot;
			bool enabled = selectedStorage != CompanionStorage.Pack || index < profile.PackCapacity;
			Texture2D background = TextureAssets.InventoryBack.Value;
			spriteBatch.Draw(background, bounds, enabled ? Color.White : Color.White * 0.35f);
			Item? item = items is not null && index < items.Count ? items[index] : null;
			int coinType = selectedStorage == CompanionStorage.Wallet ? CompanionProfile.WalletCoinType(slot) : 0;
			int type = coinType > 0 ? coinType : item?.type ?? 0;
			if (type <= 0)
				continue;
			Main.instance.LoadItem(type);
			Texture2D texture = TextureAssets.Item[type].Value;
			Rectangle frame = Main.itemAnimations[type]?.GetFrame(texture) ?? texture.Bounds;
			float scale = Math.Min(bounds.Width * 0.7f / frame.Width, bounds.Height * 0.7f / frame.Height);
			spriteBatch.Draw(texture, bounds.Center.ToVector2(), frame, Color.White, 0f, frame.Size() * 0.5f,
				scale, SpriteEffects.None, 0f);
			string count = coinType > 0
				? CompanionProfile.FormatCoinCount(profile.WalletCoins(coinType, denominationOnly: true))
				: item!.stack.ToString();
			float countScale = Math.Min(0.55f, (bounds.Width - 2f) / Math.Max(1f, FontAssets.MouseText.Value.MeasureString(count).X));
			Utils.DrawBorderString(spriteBatch, count, new Vector2(bounds.Right - 2f, bounds.Bottom - 2f),
				Color.White, countScale, 1f, 1f);
			if (!bounds.Contains(SoulmatesUISpace.Mouse.ToPoint()))
				continue;
			Main.LocalPlayer.mouseInterface = true;
			if (coinType > 0)
				Main.hoverItemName = SoulmatesText.Get("Storage.WalletHint", Lang.GetItemNameValue(coinType),
					CompanionProfile.FormatCoinCount(profile.WalletCoins(coinType)), profile.DescribeWallet());
			else {
				Main.HoverItem = item!.Clone();
				Main.hoverItemName = item.HoverName;
			}
		}
		if (selectedStorage == CompanionStorage.Wallet)
			DrawFitted(spriteBatch, profile.DescribeWallet(), new Vector2(area.X, area.Y + 75f), area.Width, 0.52f);
	}

	private void Withdraw(bool singleItem)
	{
		int slots = selectedStorage == CompanionStorage.Wallet ? 4 : 12;
		for (int slot = 0; slot < slots; slot++) {
			if (SlotBounds(slot).Contains(SoulmatesUISpace.Mouse.ToPoint())) {
				withdraw(selectedStorage, page * 12 + slot, singleItem);
				return;
			}
		}
	}

	private Rectangle SlotBounds(int slot)
		=> CalculateSlotBounds(GetDimensions().ToRectangle(), slot, selectedStorage == CompanionStorage.Wallet);

	internal static Rectangle CalculateSlotBounds(Rectangle area, int slot, bool wallet)
	{
		int columns = wallet ? 4 : 6;
		float gap = Math.Min(4f, Math.Max(0f, area.Width / (columns * 3f)));
		int size = Math.Max(1, (int)Math.Min(30f, (area.Width - gap * (columns - 1)) / columns));
		float totalWidth = size * columns + gap * (columns - 1);
		return new Rectangle((int)(area.X + (area.Width - totalWidth) * 0.5f + (slot % columns) * (size + gap)),
			(int)(area.Y + 34f + (slot / columns) * (size + gap)), size, size);
	}

	private static void DrawFitted(SpriteBatch spriteBatch, string text, Vector2 position, float width, float scale)
	{
		if (width <= 0f)
			return;
		float measured = FontAssets.MouseText.Value.MeasureString(text).X;
		Utils.DrawBorderString(spriteBatch, text, position, Color.LightGray,
			Math.Min(scale, width / Math.Max(1f, measured)));
	}
}
