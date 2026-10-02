#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader.IO;

namespace Soulmates.Common;

internal sealed class CompanionInventorySync : IDisposable
{
	internal const int SlotCount = 58;
	private readonly Player player;
	private readonly byte[][] before;
	private bool disposed;

	public CompanionInventorySync(Player player)
	{
		this.player = player;
		before = Main.netMode == NetmodeID.Server ? new byte[Math.Min(SlotCount, player.inventory.Length)][] : [];
		for (int slot = 0; slot < before.Length; slot++)
			before[slot] = Serialize(player.inventory[slot]);
	}

	public void Dispose()
	{
		if (disposed) return;
		disposed = true;
		if (Main.netMode != NetmodeID.Server || before.Length == 0)
			return;
		var changes = new List<(byte Slot, byte[] Data)>();
		for (int slot = 0; slot < before.Length; slot++) {
			byte[] after = Serialize(player.inventory[slot]);
			if (!before[slot].AsSpan().SequenceEqual(after))
				changes.Add(((byte)slot, after));
		}
		global::Soulmates.Soulmates.SendOwnerInventoryUpdate(player, changes);
	}

	internal static void Apply(BinaryReader reader)
	{
		int count = reader.ReadByte();
		if (count > SlotCount)
			throw new InvalidDataException("Too many inventory updates.");
		var changes = new List<(int Slot, Item Item)>();
		var slots = new HashSet<int>();
		Player owner = Main.LocalPlayer;
		// Parse the whole transaction before changing inventory; cursor slot 58 stays client-owned.
		for (int i = 0; i < count; i++) {
			int slot = reader.ReadByte();
			if (slot >= SlotCount || slot >= owner.inventory.Length || !slots.Add(slot))
				throw new InvalidDataException("Invalid inventory update slot.");
			changes.Add((slot, ItemIO.Receive(reader, readStack: true, readFavorite: true)));
		}
		foreach ((int slot, Item item) in changes) {
			owner.inventory[slot] = item;
			Main.clientPlayer.inventory[slot] = item.Clone();
		}
		foreach ((int slot, _) in changes)
			NetMessage.SendData(MessageID.SyncEquipment, number: owner.whoAmI, number2: slot);
		if (count > 0)
			Recipe.FindRecipes(canDelayCheck: true);
	}

	private static byte[] Serialize(Item item)
	{
		using var stream = new MemoryStream();
		using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
			ItemIO.Send(item, writer, writeStack: true, writeFavorite: true);
		return stream.ToArray();
	}
}
