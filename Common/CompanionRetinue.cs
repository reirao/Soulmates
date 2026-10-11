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

public enum CompanionRetinueStyle : byte { Melee, Ranged, Sentry }

// Native item ownership, with companion-bound combat rather than player buffs/minion slots.
public static class CompanionRetinue
{
	public static bool IsStaff(Item item) => !item.IsAir && !item.consumable && item.damage > 0
		&& item.shoot > ProjectileID.None && ContentSamples.ProjectilesByType.TryGetValue(item.shoot, out Projectile? shot)
		&& (shot.minion || shot.sentry || item.sentry);
	public static int VisualFor(int itemType) => ContentSamples.ItemsByType.TryGetValue(itemType, out Item? item)
		&& IsStaff(item) ? item.shoot : 0;
	public static CompanionRetinueStyle Style(Item item)
	{
		if (item.sentry || ContentSamples.ProjectilesByType.TryGetValue(item.shoot, out Projectile? shot) && shot.sentry)
			return CompanionRetinueStyle.Sentry;
		return item.type is ItemID.ImpStaff or ItemID.HornetStaff or ItemID.OpticStaff or ItemID.PygmyStaff
			or ItemID.TempestStaff or ItemID.XenoStaff or ItemID.StardustCellStaff
			? CompanionRetinueStyle.Ranged : CompanionRetinueStyle.Melee;
	}
	public static IEnumerable<Item> ActiveItems(CompanionProfile profile)
	{
		if (!profile.RetinueEnabled) return [];
		IEnumerable<Item> owned = profile.SummonItems.Where(IsStaff).DistinctBy(item => item.type);
		if (profile.IsAether) return owned;
		return owned.OrderByDescending(item => item.type == profile.RetinueItemType).Take(1 + profile.Level / 10);
	}
	public static bool IsActive(CompanionProfile profile, int itemType) => ActiveItems(profile).Any(item => item.type == itemType);
	public static int Damage(CompanionProfile profile, Item item) => Math.Clamp(item.damage / 2, 2, 20 + profile.Level * 2);
}

public sealed partial class CompanionProfile
{
	public const int MaximumSummonSlots = 64;
	public List<Item> SummonItems { get; set; } = [];
	public bool RetinueEnabled { get; set; }
	public int RetinueItemType { get; set; }

	private void SaveRetinue(TagCompound tag)
	{
		tag["summonItems"] = SummonItems.Where(item => !item.IsAir).Select(ItemIO.Save).ToList();
		tag["retinueEnabled"] = RetinueEnabled;
		Item? selected = SummonItems.FirstOrDefault(item => item.type == RetinueItemType);
		if (selected is not null) tag["retinueSelection"] = ItemIO.Save(selected);
	}
	private void LoadRetinue(TagCompound tag)
	{
		SummonItems = tag.ContainsKey("summonItems") ? tag.GetList<TagCompound>("summonItems").Select(ItemIO.Load).ToList() : [];
		RetinueEnabled = tag.GetBool("retinueEnabled");
		RetinueItemType = tag.ContainsKey("retinueSelection") ? ItemIO.Load(tag.GetCompound("retinueSelection")).type : 0;
	}
	private void WriteRetinue(BinaryWriter writer)
	{
		WriteStorage(writer, SummonItems);
		writer.Write(RetinueEnabled);
		writer.Write(RetinueItemType);
	}
	private void ReadRetinue(BinaryReader reader)
	{
		SummonItems = ReadStorage(reader);
		if (SummonItems.Count > MaximumSummonSlots) throw new InvalidDataException("Too many companion summon items.");
		RetinueEnabled = reader.ReadBoolean();
		RetinueItemType = reader.ReadInt32();
	}
	private void NormalizeRetinue()
	{
		if (!SummonItems.Any(item => item.type == RetinueItemType && CompanionRetinue.IsStaff(item)))
			RetinueItemType = SummonItems.FirstOrDefault(CompanionRetinue.IsStaff)?.type ?? 0;
		if (RetinueItemType == 0) RetinueEnabled = false;
	}
	internal void NormalizeRetinueSelection() => NormalizeRetinue();
}
