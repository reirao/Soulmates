#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Soulmates.Common;

// Read real summon items from the loaded engine; never activate buffs on the owning player.
public static class CompanionPets
{
	// A bounded, companion-only stack. Vanilla inventory limits and player buffs stay untouched.
	public const int MaximumFollowers = 64;
	public static int StackLimit(Item item) => IsPetItem(item) && item.ModItem is null
		? MaximumFollowers : Math.Max(1, item.maxStack);
	public static int OwnedCount(CompanionProfile profile) => (int)Math.Min(int.MaxValue,
		profile.PetItems.Where(item => item is not null && IsPetItem(item)).Sum(item => (long)Math.Max(0, item.stack)));
	public static IEnumerable<(int ItemType, int Copy)> Followers(CompanionProfile profile)
	{
		if (profile.PetItemType == 0) yield break;
		int remaining = MaximumFollowers;
		foreach (int type in profile.PetItems.Where(item => item is not null && IsPetItem(item)).Select(item => item.type)
			.Distinct().OrderBy(type => type != profile.PetItemType)) {
			int count = (int)Math.Min(remaining, profile.PetItems.Where(item => item is not null && item.type == type)
				.Sum(item => (long)Math.Max(0, item.stack)));
			for (int copy = 0; copy < count; copy++) yield return (type, copy);
			remaining -= count;
			if (remaining == 0) yield break;
		}
	}
	public static bool TryGetFormation(CompanionProfile profile, int itemType, int copy, out int index, out int total)
	{
		index = -1;
		total = 0;
		if (profile.PetItemType == 0 || copy < 0 || copy >= MaximumFollowers) return false;
		for (int pass = 0; pass <= profile.PetItems.Count && total < MaximumFollowers; pass++) {
			int i = pass - 1;
			Item? item = i < 0 ? profile.PetItems.FirstOrDefault(carried => carried is not null && !carried.IsAir && carried.type == profile.PetItemType)
				: profile.PetItems[i];
			if (item is null || !IsPetItem(item) || (i >= 0 && item.type == profile.PetItemType)) continue;
			bool seen = false;
			for (int j = 0; j < i; j++) seen |= profile.PetItems[j] is { IsAir: false } earlier && earlier.type == item.type;
			if (seen) continue;
			long owned = 0;
			foreach (Item carried in profile.PetItems)
				if (carried is not null && carried.type == item.type) owned += Math.Max(0, carried.stack);
			int count = (int)Math.Min(MaximumFollowers - total, owned);
			if (item.type == itemType && copy < count) index = total + copy;
			total += count;
		}
		return index >= 0;
	}
	public static Vector2 FollowerOffset(int index, int total, int direction, float time)
	{
		float bob = MathF.Sin(time / 45f + index * 1.7f) * 5f;
		if (total <= 1) return new Vector2(-direction * 52f, -12f + bob);
		int ring = 0, capacity = 8;
		while (index >= capacity) { index -= capacity; ring++; capacity += 4; }
		float angle = -MathHelper.PiOver2 + MathHelper.TwoPi * index / capacity + ring * 0.21f;
		return angle.ToRotationVector2() * (112f + ring * 96f) + new Vector2(0f, -18f + bob);
	}
	internal static void CompactStacks(List<Item> items)
	{
		for (int i = 0; i < items.Count; i++) {
			Item destination = items[i];
			for (int j = i + 1; !destination.IsAir && destination.stack < StackLimit(destination) && j < items.Count; j++) {
				Item source = items[j];
				if (source.IsAir || source.type != destination.type || !ItemLoader.CanStack(destination, source)) continue;
				ItemLoader.StackItems(destination, source, out _, infiniteSource: false,
					numToTransfer: Math.Min(source.stack, StackLimit(destination) - destination.stack));
				if (source.stack <= 0) source.TurnToAir();
			}
		}
		items.RemoveAll(item => item.IsAir);
	}
	public static IEnumerable<int> SupportedItems => ContentSamples.ItemsByType.Values
		.Where(item => VisualFor(item.type) > 0).Select(item => item.type).OrderBy(type => type);
	public static int VisualFor(int itemType)
	{
		if (!ContentSamples.ItemsByType.TryGetValue(itemType, out Item? item) || item.IsAir
			|| item.buffType <= 0 || item.buffType >= Main.vanityPet.Length || item.buffType >= Main.lightPet.Length
			|| (!Main.vanityPet[item.buffType] && !Main.lightPet[item.buffType]) || item.consumable
			|| item.damage > 0 || !ContentSamples.ProjectilesByType.TryGetValue(item.shoot, out Projectile? projectile)
			|| projectile.minion || projectile.sentry || projectile.hostile) return 0;
		return item.shoot;
	}
	public static bool IsPetItem(Item item) => !item.IsAir && VisualFor(item.type) > 0;
	public static bool IsCarried(CompanionProfile profile, int itemType) => VisualFor(itemType) > 0
		&& profile.PetItems.Any(item => item is not null && !item.IsAir && item.type == itemType);
}

public sealed partial class CompanionProfile
{
	public int PetItemType { get; set; }
	public const int MaximumPetSlots = 12;
	public List<Item> PetItems { get; set; } = [];
	private void SavePet(TagCompound tag)
	{
		tag["petItemType"] = PetItemType;
		Item? selected = PetItems.FirstOrDefault(item => !item.IsAir && item.type == PetItemType);
		if (selected is not null) tag["petSelection"] = ItemIO.Save(selected);
		tag["petItems"] = PetItems.Where(item => !item.IsAir).Select(ItemIO.Save).ToList();
	}
	private void LoadPet(TagCompound tag)
	{
		PetItemType = tag.ContainsKey("petSelection") ? ItemIO.Load(tag.GetCompound("petSelection")).type
			: tag.GetInt("petItemType");
		PetItems = tag.ContainsKey("petItems") ? tag.GetList<TagCompound>("petItems").Select(ItemIO.Load).ToList() : [];
	}
	private void WritePet(BinaryWriter writer)
	{
		WriteStorage(writer, PetItems);
		writer.Write(PetItemType);
	}
	private void ReadPet(BinaryReader reader)
	{
		PetItems = ReadStorage(reader);
		PetItemType = reader.ReadInt32();
		if (PetItemType != 0 && CompanionPets.VisualFor(PetItemType) == 0)
			throw new InvalidDataException("Unsupported companion pet.");
	}
	private void NormalizePet()
	{
		if (PetItemType != 0 && !CompanionPets.IsCarried(this, PetItemType))
			PetItemType = PetItems.FirstOrDefault(item => CompanionPets.IsPetItem(item))?.type ?? 0;
	}
}
