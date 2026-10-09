#nullable enable
using Microsoft.Xna.Framework;
using Soulmates.Common.Dialogue;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulmates.Common.UI;

public enum SoulwheelMouseMode : byte { Terraria, Player, Companion }

// Keep the original target while the cursor moves from the world onto the wheel.
internal sealed class SoulwheelTarget
{
	public Point Tile { get; }
	public int ItemSlot { get; }
	public int NpcSlot => npcSlot;
	public int NpcType => npcType;
	private readonly Item? item;
	private readonly int itemType;
	private readonly NPC? npc;
	private readonly int npcSlot = -1;
	private readonly int npcType;
	private readonly bool hasTile;
	private readonly ushort tileType;
	private readonly short frameX;
	private readonly short frameY;

	public SoulwheelTarget(Vector2 worldMouse, int ignoredNpc)
	{
		Tile = worldMouse.ToTileCoordinates();
		ItemSlot = FindHoveredItem(worldMouse);
		if (ItemSlot >= 0) {
			item = Main.item[ItemSlot];
			itemType = item.type;
			return;
		}
		float nearest = float.MaxValue;
		for (int i = 0; i < Main.maxNPCs; i++) {
			NPC candidate = Main.npc[i];
			if (i == ignoredNpc || !candidate.active || !candidate.Hitbox.Contains(worldMouse.ToPoint())) continue;
			float distance = Vector2.DistanceSquared(candidate.Center, worldMouse);
			if (distance >= nearest) continue;
			nearest = distance;
			npc = candidate;
			npcSlot = i;
			npcType = candidate.type;
		}
		if (npc is not null || !WorldGen.InWorld(Tile.X, Tile.Y, 1)) return;
		Terraria.Tile tile = Main.tile[Tile.X, Tile.Y];
		hasTile = tile.HasTile;
		tileType = tile.TileType;
		frameX = tile.TileFrameX;
		frameY = tile.TileFrameY;
	}

	public bool IsCurrent {
		get {
			if (item is not null)
				return ReferenceEquals(Main.item[ItemSlot], item) && item.active && !item.IsAir && item.type == itemType;
			if (npc is not null)
				return ReferenceEquals(Main.npc[npcSlot], npc) && npc.active && npc.type == npcType;
			if (!WorldGen.InWorld(Tile.X, Tile.Y, 1)) return false;
			Terraria.Tile tile = Main.tile[Tile.X, Tile.Y];
			return tile.HasTile == hasTile && (!hasTile || tile.TileType == tileType
				&& tile.TileFrameX == frameX && tile.TileFrameY == frameY);
		}
	}

	public bool CanPoint => IsCurrent && (item is not null || npc is not null || hasTile);
	public bool CanOfferFallback => CanPoint && !HasNativeInteractionPriority(Tile) && (item is not null || npc is not null
		|| (!Main.tileFrameImportant[tileType] || SoulboundCompanion.IsTreeTrunk(tileType) || tileType == TileID.FallenLog)
			&& !HasPlacedObject(Tile));

	internal static bool HasPlacedObject(Point tilePosition)
	{
		if (!WorldGen.InWorld(tilePosition.X, tilePosition.Y, 1)) return false;
		Terraria.Tile tile = Main.tile[tilePosition.X, tilePosition.Y];
		return tile.HasTile && (Main.tileFrameImportant[tile.TileType] || TileID.Sets.Torch[tile.TileType])
			&& !SoulboundCompanion.IsTreeTrunk(tile.TileType) && tile.TileType != TileID.FallenLog;
	}
	internal static bool HasNativeInteractionPriority(Point tilePosition)
	{
		if (!HasPlacedObject(tilePosition)) return false;
		ushort type = Main.tile[tilePosition.X, tilePosition.Y].TileType;
		return TileID.Sets.Torch[type] || !Main.tileCut[type];
	}
	public bool CanObserveNpc(SoulboundCompanion companion) => IsCurrent && npc is not null && companion.CanTargetNpc(npcSlot);
	public bool CanInviteCritter(SoulboundCompanion companion) => IsCurrent && npc is not null && companion.CanTargetCritterCompany(npcSlot);
	public bool CanCollectCritter(SoulboundCompanion companion) => IsCurrent && npc is not null && companion.CanTargetCritterCollect(npcSlot);
	public bool CanDiscussForestry(SoulboundCompanion companion) => IsCurrent && npc is null && item is null
		&& hasTile && (SoulboundCompanion.IsTreeTrunk(tileType) || tileType == TileID.FallenLog)
		&& companion.CanTargetLook(Tile, -1);
	public bool CanOrder(SoulboundCompanion companion, CompanionTargetOrder order) => IsCurrent && npc is null && order switch {
		CompanionTargetOrder.Look => companion.CanTargetLook(Tile, ItemSlot),
		CompanionTargetOrder.Gather => item is not null && companion.CanTargetGathering(ItemSlot),
		CompanionTargetOrder.Mine => item is null && companion.CanTargetMining(Tile),
		CompanionTargetOrder.Forest => item is null && companion.TryResolveForestTarget(Tile, out _, out _),
		_ => false
	};

	public string Name => item is not null ? item.Name : npc is not null ? npc.FullName
		: hasTile && TileLoader.GetItemDropFromTypeAndStyle(tileType, 0) is int drop && drop > ItemID.None
			? Lang.GetItemNameValue(drop) : SoulmatesText.Get("Resourcefulness.UnknownMaterial");

	public int Emote => npc is not null
		? npc.townNPC ? CompanionSocialDialogue.ResidentGreetingEmote(npc.type)
			: npc.catchItem > 0 ? CompanionCritters.EmoteFor(npc) : npc.friendly ? EmoteID.EmotionAlert : EmoteID.EmoteFear
		: item is not null ? itemType is >= ItemID.CopperCoin and <= ItemID.PlatinumCoin
			? EmoteID.ItemGoldpile : EmoteID.ItemCog
		: SoulboundCompanion.IsTreeTrunk(tileType) || tileType == TileID.FallenLog ? EmoteID.MiscTree : EmoteID.ItemPickaxe;

	internal static int FindHoveredItem(Vector2 worldMouse)
	{
		int result = -1;
		float nearest = float.MaxValue;
		for (int i = 0; i < Main.maxItems; i++) {
			Item candidate = Main.item[i];
			if (!candidate.active || candidate.IsAir) continue;
			Rectangle hitbox = candidate.Hitbox;
			hitbox.Inflate(8, 8);
			if (!hitbox.Contains(worldMouse.ToPoint())) continue;
			float distance = Vector2.DistanceSquared(worldMouse, candidate.Center);
			if (distance >= nearest) continue;
			nearest = distance;
			result = i;
		}
		return result;
	}
}
