using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Soulmates.Common;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace SoulmatesRegressionProbe;

public sealed partial class EngineChecks
{
	private static void FlushInventoryPackets(Mod mod, Player server, Player client, int offset)
	{
		Type messages = mod.GetType().GetNestedType("MessageType", BindingFlags.NonPublic)!;
		byte receipt = Convert.ToByte(Enum.Parse(messages, "InventoryReceipt"));
		for (int index = 0; index < ProbeSocket.Sent.Count; index++) {
			byte[] packet = ProbeSocket.Sent[index];
			if (packet[2] != MessageID.ModPacket) continue;
			bool toServer = packet[offset] == receipt;
			Main.player[0] = toServer ? server : client;
			Main.netMode = toServer ? NetmodeID.Server : NetmodeID.MultiplayerClient;
			Main.myPlayer = toServer ? 255 : 0;
			using var stream = new MemoryStream(packet); stream.Position = offset;
			using var reader = new BinaryReader(stream); mod.HandlePacket(reader, toServer ? 0 : 256);
		}
		Main.player[0] = client; Main.netMode = NetmodeID.MultiplayerClient; Main.myPlayer = 0;
	}

	private static void ResetInventoryTransactions() => typeof(CompanionProfile).Assembly
		.GetType("Soulmates.Common.CompanionInventorySync")!
		.GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null);

	private static void SettleServerInventory(Mod mod)
	{
		if (Main.netMode != NetmodeID.Server) return;
		Type sync = typeof(CompanionProfile).Assembly.GetType("Soulmates.Common.CompanionInventorySync")!;
		var pending = (IDictionary)sync.GetField("Transactions", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
		if (!pending.Contains(0)) return;
		object transaction = pending[0]!;
		Guid id = (Guid)transaction.GetType().GetProperty("Id")!.GetValue(transaction)!;
		using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
		writer.Write(Convert.ToByte(Enum.Parse(mod.GetType().GetNestedType("MessageType", BindingFlags.NonPublic)!, "InventoryReceipt")));
		writer.Write(id.ToByteArray()); writer.Write(true);
		writer.Write((byte)58);
		for (byte slot = 0; slot < 58; slot++) {
			writer.Write(slot); ItemIO.Send(Main.player[0].inventory[slot], writer, writeStack: true, writeFavorite: true);
		}
		stream.Position = 0; using var reader = new BinaryReader(stream); mod.HandlePacket(reader, 0);
	}
}
