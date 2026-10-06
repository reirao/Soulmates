#nullable enable
using System.Linq;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader.IO;
using System.IO;

namespace Soulmates.Common;

// Only simple flying vanity sprites are supported; native player buffs and pet slots are untouched.
public static class CompanionPets
{
	public static IReadOnlyList<int> SupportedItems { get; } = Array.AsReadOnly(new int[] { ItemID.ZephyrFish, ItemID.Nectar });
	public static int VisualFor(int itemType) => itemType switch {
		ItemID.ZephyrFish => ProjectileID.ZephyrFish,
		ItemID.Nectar => ProjectileID.BabyHornet,
		_ => 0
	};
	public static bool IsCarried(CompanionProfile profile, int itemType) => VisualFor(itemType) > 0
		&& profile.Pack.Any(item => item is not null && !item.IsAir && item.type == itemType);
}

public sealed partial class CompanionProfile
{
	public int PetItemType { get; set; }
	private void SavePet(TagCompound tag) => tag["petItemType"] = PetItemType;
	private void LoadPet(TagCompound tag) => PetItemType = tag.GetInt("petItemType");
	private void WritePet(BinaryWriter writer) => writer.Write(PetItemType);
	private void ReadPet(BinaryReader reader)
	{
		PetItemType = reader.ReadInt32();
		if (PetItemType != 0 && CompanionPets.VisualFor(PetItemType) == 0)
			throw new InvalidDataException("Unsupported companion pet.");
	}
	private void NormalizePet()
	{
		if (!CompanionPets.IsCarried(this, PetItemType)) PetItemType = 0;
	}
}
