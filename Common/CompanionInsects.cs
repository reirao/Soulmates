#nullable enable
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulmates.Common;

public static class CompanionInsects
{
	public const int MaximumFlock = 6;
	public static readonly int[] NpcTypes = [NPCID.Butterfly, NPCID.GoldButterfly, NPCID.Firefly, NPCID.LightningBug];
	public static bool CanCatch(NPC insect) => insect.active && insect.catchItem > 0 && !insect.SpawnedFromStatue
		&& insect.releaseOwner == 255 && Array.IndexOf(NpcTypes, insect.type) >= 0;

	public static int NpcForItem(int type) => type switch {
		ItemID.MonarchButterfly or ItemID.PurpleEmperorButterfly or ItemID.RedAdmiralButterfly
			or ItemID.UlyssesButterfly or ItemID.SulphurButterfly or ItemID.TreeNymphButterfly
			or ItemID.ZebraSwallowtailButterfly or ItemID.JuliaButterfly => NPCID.Butterfly,
		ItemID.GoldButterfly => NPCID.GoldButterfly,
		ItemID.Firefly => NPCID.Firefly, ItemID.LightningBug => NPCID.LightningBug,
		_ => -1
	};
}

// Observe the native catch drop, then route that exact item through the existing cargo transaction.
public sealed class CompanionCatchReceipt : GlobalItem
{
	private static int expectedType;
	private static NPC? caughtEntity;
	private static Player? catcher;
	private static List<Item>? drops;
	internal static bool Begin(NPC insect, Player owner)
	{
		if (drops is not null) return false;
		expectedType = insect.catchItem;
		caughtEntity = insect;
		catcher = owner;
		drops = [];
		return true;
	}
	internal static Item? End()
	{
		Item? result = drops is { Count: 1 } ? drops[0] : null;
		drops = null;
		expectedType = 0;
		caughtEntity = null;
		catcher = null;
		return result;
	}
	public override void OnSpawn(Item item, IEntitySource source)
	{
		if (drops is not null && drops.Count < 2 && source is EntitySource_Caught caught
			&& ReferenceEquals(caught.Entity, caughtEntity) && ReferenceEquals(caught.Catcher, catcher)
			&& item.type == expectedType)
			drops.Add(item);
	}
	public override void Unload() => End();
}

public sealed class CompanionInsectAssets : ModSystem
{
	public override void PostSetupContent()
	{
		if (!Main.dedServ)
			foreach (int type in CompanionInsects.NpcTypes)
				Main.instance.LoadNPC(type);
	}
}
