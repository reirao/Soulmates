#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader.IO;

namespace Soulmates.Common;

// Read real summon items from the loaded engine; never activate buffs on the owning player.
public static class CompanionPets
{
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
		if (!CompanionPets.IsCarried(this, PetItemType)) PetItemType = 0;
	}
}
