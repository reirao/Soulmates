using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BigInteger = System.Numerics.BigInteger;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader.IO;

namespace Soulmates.Common;

public sealed partial class CompanionProfile
{
	public void ClearCargo()
	{
		Pack.Clear();
		Resources.Clear();
		WalletCopper = BigInteger.Zero;
	}

	public static bool IsResource(Item item) => !item.IsAir && CoinValue(item.type) == 0 && item.maxStack > 1
		&& (item.material || item.createTile >= TileID.Dirt || item.createWall > 0 || item.ammo > AmmoID.None)
		&& (item.damage <= 0 || item.ammo > AmmoID.None) && item.pick <= 0 && item.axe <= 0 && item.hammer <= 0
		&& item.healLife <= 0 && item.healMana <= 0 && item.buffType <= 0
		&& item.bait <= 0 && !item.accessory;

	public static CompanionStorage StorageFor(Item item) => CoinValue(item.type) > 0
		? CompanionStorage.Wallet : IsResource(item) ? CompanionStorage.Resources : CompanionStorage.Pack;

	public List<Item> StorageItems(CompanionStorage storage) => storage switch {
		CompanionStorage.Resources => Resources,
		CompanionStorage.Pack => Pack,
		_ => throw new ArgumentOutOfRangeException(nameof(storage))
	};

	internal byte[] StorageToken(CompanionStorage storage, int index)
	{
		using var stream = new MemoryStream();
		using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true)) {
			writer.Write((byte)storage);
			writer.Write(index);
			if (storage == CompanionStorage.Wallet)
				writer.Write(WalletCopper.ToString(CultureInfo.InvariantCulture));
			else {
				List<Item> items = StorageItems(storage);
				ItemIO.Send(index >= 0 && index < items.Count ? items[index] : new Item(), writer,
					writeStack: true, writeFavorite: false);
			}
		}
		return SHA256.HashData(stream.ToArray());
	}

	public static int CoinValue(int itemType) => itemType switch {
		ItemID.CopperCoin => 1,
		ItemID.SilverCoin => 100,
		ItemID.GoldCoin => 10_000,
		ItemID.PlatinumCoin => 1_000_000,
		_ => 0
	};

	public static int WalletCoinType(int slot) => slot switch {
		0 => ItemID.PlatinumCoin,
		1 => ItemID.GoldCoin,
		2 => ItemID.SilverCoin,
		3 => ItemID.CopperCoin,
		_ => ItemID.None
	};

	public BigInteger WalletCoins(int itemType, bool denominationOnly = false)
	{
		int value = CoinValue(itemType);
		if (value <= 0)
			return BigInteger.Zero;
		BigInteger coins = WalletCopper / value;
		return denominationOnly && itemType != ItemID.PlatinumCoin ? coins % 100 : coins;
	}

	public bool RemoveWalletCoins(int itemType, int amount)
	{
		int value = CoinValue(itemType);
		BigInteger cost = (BigInteger)value * amount;
		if (value <= 0 || amount <= 0 || WalletCopper < cost)
			return false;
		WalletCopper -= cost;
		return true;
	}

	public string DescribeWallet() => SoulmatesText.Get("Storage.Money", FormatCoinCount(WalletCoins(ItemID.PlatinumCoin)),
		WalletCoins(ItemID.GoldCoin, true), WalletCoins(ItemID.SilverCoin, true), WalletCoins(ItemID.CopperCoin, true));

	public static string FormatCoinCount(BigInteger amount)
	{
		string digits = amount.ToString(CultureInfo.InvariantCulture);
		return digits.Length <= 7 ? digits : digits[..3] + "e" + (digits.Length - 3);
	}

	private static BigInteger ParseWallet(string value) => BigInteger.TryParse(value,
		NumberStyles.None, CultureInfo.InvariantCulture, out BigInteger amount) && amount.Sign >= 0
		? amount : BigInteger.Zero;

	private static void WriteStorage(BinaryWriter writer, List<Item> storage)
	{
		Item[] items = storage.Where(item => !item.IsAir).ToArray();
		if (items.Length > MaximumPackSlots + MaximumResourceSlots)
			throw new InvalidDataException("Companion cargo exceeds the storage protocol.");
		writer.Write((byte)items.Length);
		foreach (Item item in items)
			ItemIO.Send(item, writer, writeStack: true, writeFavorite: false);
	}

	private static List<Item> ReadStorage(BinaryReader reader)
	{
		int count = reader.ReadByte();
		if (count > MaximumPackSlots + MaximumResourceSlots)
			throw new InvalidDataException("Invalid companion cargo count.");
		var items = new List<Item>(count);
		for (int i = 0; i < count; i++)
			items.Add(ItemIO.Receive(reader, readStack: true, readFavorite: false));
		return items;
	}
}
