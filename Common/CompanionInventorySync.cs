#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Soulmates.Common;

internal sealed class CompanionInventorySync : IDisposable
{
	internal const int SlotCount = 58;
	private sealed record Change(byte Slot, Item Before, Item After);
	private sealed record Pending(Player Owner, Guid Id, List<Change> Changes, byte[][] BeforeTokens,
		SoulboundCompanion? Companion, CompanionProfile? BeforeProfile) {
		public int RetryTicks;
		public List<Action<bool>> Responses { get; } = [];
	}
	private static readonly Dictionary<int, Pending> Transactions = [];
	private static readonly Dictionary<Guid, (bool Accepted, byte[] Inventory)> Receipts = [];
	private static readonly HashSet<int> Editing = [];
	private static readonly Dictionary<Guid, Guid> AwaitingProfiles = [];
	private static readonly HashSet<Guid> OpenReceipts = [];
	private readonly Player player;
	private readonly Item[] before;
	private readonly SoulboundCompanion? companion;
	private readonly CompanionProfile? beforeProfile;
	private bool disposed;

	public CompanionInventorySync(Player player)
	{
		this.player = player;
		before = Main.netMode == NetmodeID.Server ? player.inventory.Take(SlotCount).Select(item => item.Clone()).ToArray() : [];
		if (before.Length == 0) return;
		if (IsPending(player) || !Editing.Add(player.whoAmI))
			throw new InvalidOperationException("Overlapping companion inventory transaction.");
		companion = SoulboundCompanion.FindFor(player);
		beforeProfile = companion?.Profile.Clone();
	}

	internal static bool IsPending(Player player) => Main.netMode == NetmodeID.Server
		&& Transactions.TryGetValue(player.whoAmI, out Pending? pending) && ReferenceEquals(pending.Owner, player);
	internal static bool IsEditing(Player player) => Editing.Contains(player.whoAmI);
	internal static bool IsAwaiting(Guid profileId) => AwaitingProfiles.Values.Contains(profileId);
	internal static void RespondOrDefer(int playerIndex, Action<bool> response)
	{
		if (Transactions.TryGetValue(playerIndex, out Pending? pending)) pending.Responses.Add(response);
		else response(true);
	}
	internal static CompanionProfile VisibleProfile(SoulboundCompanion companion)
		=> Transactions.TryGetValue((int)companion.NPC.ai[0], out Pending? pending)
			&& ReferenceEquals(pending.Companion, companion) && pending.BeforeProfile is not null
			? pending.BeforeProfile : companion.Profile;

	public void Dispose()
	{
		if (disposed) return;
		disposed = true;
		if (before.Length == 0) return;
		Editing.Remove(player.whoAmI);
		var changes = new List<Change>();
		for (int slot = 0; slot < before.Length; slot++) {
			Item after = player.inventory[slot];
			// Existing Sigil metadata travels by profile identity, never by a slot replacement.
			if (before[slot].ModItem is SoulboundSigil oldSigil && after.ModItem is SoulboundSigil newSigil
				&& oldSigil.Profile.Id == newSigil.Profile.Id && before[slot].stack == after.stack
				&& before[slot].prefix == after.prefix && before[slot].favorited == after.favorited) continue;
			if (!Same(before[slot], after)) changes.Add(new((byte)slot, before[slot], after.Clone()));
		}
		if (changes.Count == 0) {
			if (companion is not null) global::Soulmates.Soulmates.SendProfileUpdate(player, companion);
			return;
		}
		var pending = new Pending(player, Guid.NewGuid(), changes, before.Select(Token).ToArray(), companion, beforeProfile);
		Transactions[player.whoAmI] = pending;
		Send(pending);
	}

	private static void Send(Pending pending)
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter(stream);
		writer.Write(pending.Id.ToByteArray()); writer.Write((byte)pending.Changes.Count);
		foreach (Change change in pending.Changes) {
			writer.Write(change.Slot);
			ItemIO.Send(change.Before, writer, writeStack: true, writeFavorite: true);
			ItemIO.Send(change.After, writer, writeStack: true, writeFavorite: true);
		}
		foreach (byte[] token in pending.BeforeTokens) writer.Write(token);
		writer.Write(pending.Companion is not null);
		pending.Companion?.Profile.Write(writer);
		global::Soulmates.Soulmates.SendOwnerInventoryUpdate(pending.Owner, stream.ToArray());
		pending.RetryTicks = 120;
	}

	internal static void Apply(BinaryReader reader)
	{
		Guid id = ReadId(reader);
		int count = reader.ReadByte();
		if (id == Guid.Empty || count is < 1 or > SlotCount) throw new InvalidDataException("Invalid inventory transaction.");
		var changes = new List<Change>();
		var slots = new HashSet<int>();
		Player owner = Main.LocalPlayer;
		for (int i = 0; i < count; i++) {
			byte slot = reader.ReadByte();
			if (slot >= SlotCount || slot >= owner.inventory.Length || !slots.Add(slot))
				throw new InvalidDataException("Invalid inventory update slot.");
			changes.Add(new(slot, ItemIO.Receive(reader, readStack: true, readFavorite: true),
				ItemIO.Receive(reader, readStack: true, readFavorite: true)));
		}
		var beforeTokens = new byte[SlotCount][];
		for (int slot = 0; slot < SlotCount; slot++) {
			beforeTokens[slot] = reader.ReadBytes(32);
			if (beforeTokens[slot].Length != 32) throw new EndOfStreamException();
		}
		CompanionProfile? profile = reader.ReadBoolean() ? CompanionProfile.Read(reader) : null;
		if (!Receipts.TryGetValue(id, out var receipt)) {
			bool accepted = changes.All(change => Same(owner.inventory[change.Slot], change.Before))
				&& (profile is null || owner.inventory.Take(SlotCount).Any(item => item.ModItem is SoulboundSigil sigil
					&& sigil.Profile.Id == profile.Id));
			if (accepted) {
				foreach (Change change in changes) {
					owner.inventory[change.Slot] = change.After.Clone();
					Main.clientPlayer.inventory[change.Slot] = change.After.Clone();
				}
				if (profile is not null) global::Soulmates.Soulmates.ApplyClientProfile(profile);
				Recipe.FindRecipes(canDelayCheck: true);
			}
			using var stream = new MemoryStream();
			using var writer = new BinaryWriter(stream);
			var changedSlots = Enumerable.Range(0, SlotCount).Where(slot => slots.Contains(slot)
				|| !Token(owner.inventory[slot]).AsSpan().SequenceEqual(beforeTokens[slot])).ToList();
			writer.Write((byte)changedSlots.Count);
			foreach (int slot in changedSlots) {
				writer.Write((byte)slot);
				ItemIO.Send(owner.inventory[slot], writer, writeStack: true, writeFavorite: true);
			}
			receipt = (accepted, stream.ToArray());
			if (Receipts.Count >= 128) Receipts.Remove(Receipts.Keys.First());
			Receipts[id] = receipt;
			OpenReceipts.Add(id);
			if (profile is not null) AwaitingProfiles[id] = profile.Id;
		}
		global::Soulmates.Soulmates.SendInventoryReceipt(id, receipt.Accepted, receipt.Inventory);
	}

	internal static void ReceiveReceipt(BinaryReader reader, int whoAmI)
	{
		Guid id = ReadId(reader);
		bool accepted = reader.ReadBoolean();
		if (!Transactions.TryGetValue(whoAmI, out Pending? pending) || pending.Id != id
			|| !ReferenceEquals(pending.Owner, Main.player[whoAmI])) return;
		int count = reader.ReadByte();
		if (count > SlotCount) throw new InvalidDataException("Too many inventory receipt slots.");
		var inventory = new Dictionary<byte, Item>();
		for (int i = 0; i < count; i++) {
			byte slot = reader.ReadByte();
			if (slot >= SlotCount || inventory.ContainsKey(slot)) throw new InvalidDataException("Invalid inventory receipt slot.");
			inventory[slot] = ItemIO.Receive(reader, readStack: true, readFavorite: true);
		}
		if (pending.Changes.Any(change => !inventory.ContainsKey(change.Slot)))
			throw new InvalidDataException("Missing inventory receipt slot.");
		accepted &= pending.Changes.All(change => Same(inventory[change.Slot], change.After));
		Transactions.Remove(whoAmI);
		// The receipt precedes later native inventory edits on the same reliable client connection.
		foreach ((byte slot, Item item) in inventory) pending.Owner.inventory[slot] = item;
		if (!accepted && pending.Companion is not null && pending.BeforeProfile is not null)
			pending.Companion.Profile = pending.BeforeProfile.Clone();
		if (pending.Companion is not null) {
			pending.Companion.SyncProfileToBoundSigil();
			pending.Companion.NPC.netUpdate = true;
		}
		global::Soulmates.Soulmates.SendInventoryResolved(pending.Owner, id, accepted, pending.Companion?.Profile);
		foreach (Action<bool> response in pending.Responses) response(accepted);
	}

	internal static void Complete(BinaryReader reader)
	{
		Guid id = ReadId(reader);
		bool accepted = reader.ReadBoolean();
		CompanionProfile? profile = reader.ReadBoolean() ? CompanionProfile.Read(reader) : null;
		if (!OpenReceipts.Remove(id)) return;
		AwaitingProfiles.Remove(id);
		if (profile is not null) global::Soulmates.Soulmates.ApplyClientProfile(profile);
		if (!accepted) Main.NewText(SoulmatesText.Get("TargetOrders.TargetLost"));
	}

	internal static void Tick()
	{
		foreach (Pending pending in Transactions.Values.ToArray()) {
			if (!pending.Owner.active || !ReferenceEquals(Main.player[pending.Owner.whoAmI], pending.Owner))
				Transactions.Remove(pending.Owner.whoAmI);
			else if (--pending.RetryTicks <= 0) Send(pending);
		}
	}
	internal static void Reset() { Transactions.Clear(); Receipts.Clear(); Editing.Clear(); AwaitingProfiles.Clear(); OpenReceipts.Clear(); }
	private static Guid ReadId(BinaryReader reader)
	{
		byte[] bytes = reader.ReadBytes(16);
		if (bytes.Length != 16) throw new EndOfStreamException();
		return new Guid(bytes);
	}
	private static bool Same(Item a, Item b) => Serialize(a).AsSpan().SequenceEqual(Serialize(b));
	internal static byte[] Token(Item item) => SHA256.HashData(Serialize(item));
	private static byte[] Serialize(Item item)
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter(stream);
		ItemIO.Send(item, writer, writeStack: true, writeFavorite: true);
		return stream.ToArray();
	}
}

public sealed class CompanionInventoryTransactions : ModSystem
{
	public override void PostUpdatePlayers() { if (Main.netMode == NetmodeID.Server) CompanionInventorySync.Tick(); }
	public override void OnWorldUnload() => CompanionInventorySync.Reset();
	public override void Unload() => CompanionInventorySync.Reset();
}
