#nullable enable
using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Soulmates.Common;

public enum CompanionNpcAction : byte { Look, Company, Collect }

public static class CompanionCritters
{
	public static bool IsCritter(NPC npc) => npc.catchItem > 0 || npc.type > NPCID.None
		&& npc.type < NPCID.Sets.CountsAsCritter.Length && NPCID.Sets.CountsAsCritter[npc.type];

	public static bool IsNatural(NPC npc) => NaturalRejectionReason(npc).Length == 0;
	public static bool IsHarmless(NPC npc) => npc.catchItem > 0 && npc.catchItem < ItemLoader.ItemCount
		&& npc.type > NPCID.None && npc.type < NPCID.Sets.CountsAsCritter.Length
		&& NPCID.Sets.CountsAsCritter[npc.type] && !npc.townNPC && !npc.boss && npc.damage == 0;

	internal static string NaturalRejectionReason(NPC npc)
	{
		if (!npc.active) return "inactive";
		if (npc.life <= 0) return "dead";
		if (npc.catchItem <= 0 || npc.catchItem >= ItemLoader.ItemCount) return "no valid native catch item";
		if (npc.type <= NPCID.None || npc.type >= NPCID.Sets.CountsAsCritter.Length
			|| !NPCID.Sets.CountsAsCritter[npc.type]) return "native critter flag absent";
		// Native spawn immunity temporarily sets friendly; it is not a critter classification.
		if (npc.townNPC) return "town NPC protected";
		if (npc.boss) return "boss protected";
		if (npc.damage != 0) return "damaging NPC protected";
		if (npc.SpawnedFromStatue) return "statue protected";
		if (npc.releaseOwner != 255) return "released animal protected";
		return "";
	}

	public static bool IsCommon(NPC npc) => IsNatural(npc) && !NPCID.Sets.GoldCrittersCollection.Contains(npc.type);
	public static bool CanKeepCompany(NPC npc) => IsCommon(npc) && SupportsCompany(npc.type);
	public static bool SupportsCompany(int type) => type is NPCID.Bunny or NPCID.Squirrel or NPCID.SquirrelRed
		or NPCID.Bird or NPCID.BirdBlue or NPCID.BirdRed or NPCID.Butterfly or NPCID.Firefly or NPCID.LightningBug;

	public static bool CanUseNet(NPC npc, Item net) => !net.IsAir && ItemID.Sets.CatchingTool[net.type]
		&& (!ItemID.Sets.IsLavaBait[npc.catchItem] || ItemID.Sets.LavaproofCatchingTool[net.type]);

	public static int EmoteFor(NPC npc) => npc.type switch {
		NPCID.Bunny => EmoteID.CritterBunny,
		NPCID.Bird or NPCID.BirdBlue or NPCID.BirdRed => EmoteID.CritterBird,
		NPCID.Butterfly or NPCID.GoldButterfly or NPCID.Firefly or NPCID.LightningBug => EmoteID.CritterButterfly,
		_ => EmoteID.EmotionLove
	};
}

public sealed class CompanionCritterLife : GlobalNPC
{
	public override void OnKill(NPC npc)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || !CompanionCritters.IsHarmless(npc)
			|| npc.SpawnedFromStatue) return;
		bool playerAttack = npc.lastInteraction >= 0 && npc.lastInteraction < Main.maxPlayers
			&& npc.playerInteraction[npc.lastInteraction];
		foreach (NPC candidate in Main.ActiveNPCs)
			if (candidate.ModNPC is SoulboundCompanion companion)
				companion.ObserveCritterLoss(npc, playerAttack);
	}
}

// Guide existing native critters only. Their normal AI, collisions, health and catch drops stay intact.
public sealed class CompanionCritterCompany : GlobalNPC
{
	private short companionIndex = -1;
	private Guid companionId;
	private int syncTicks;
	private bool catchingUp;
	public override bool InstancePerEntity => true;
	public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => CompanionCritters.SupportsCompany(entity.type);
	internal bool IsAssigned => companionIndex >= 0;
	internal bool BelongsTo(SoulboundCompanion companion) => companionIndex == companion.NPC.whoAmI
		&& companionId == companion.Profile.Id;

	internal bool TryJoin(NPC critter, SoulboundCompanion companion)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || IsAssigned || !CompanionCritters.CanKeepCompany(critter)
			|| !companion.CanInviteCritter(critter))
			return false;
		companionIndex = (short)companion.NPC.whoAmI;
		companionId = companion.Profile.Id;
		catchingUp = false;
		critter.netUpdate = true;
		return true;
	}

	internal void Leave(NPC npc)
	{
		if (!IsAssigned || Main.netMode == NetmodeID.MultiplayerClient) return;
		companionIndex = -1;
		companionId = Guid.Empty;
		catchingUp = false;
		npc.netUpdate = true;
	}

	public override void PostAI(NPC npc)
	{
		if (!IsAssigned) return;
		SoulboundCompanion? companion = companionIndex < Main.maxNPCs
			? Main.npc[companionIndex].ModNPC as SoulboundCompanion : null;
		if (!CompanionCritters.CanKeepCompany(npc) || companion is null || !companion.NPC.active || !BelongsTo(companion)
			|| companion.NPC.ai[0] < 0 || companion.NPC.ai[0] >= Main.maxPlayers
			|| companion.Profile.CritterMode != CompanionCritterMode.Company
			|| !Main.player[(int)companion.NPC.ai[0]].active || Main.player[(int)companion.NPC.ai[0]].dead
			|| Vector2.DistanceSquared(npc.Center, companion.NPC.Center) > 640f * 640f) {
			Leave(npc);
			return;
		}
		Vector2 offset = companion.NPC.Center - npc.Center;
		float separation = npc.noGravity ? offset.Length() : MathF.Abs(offset.X);
		if (separation > 96f) catchingUp = true;
		else if (separation < 48f) catchingUp = false;
		bool lineOfSight = Collision.CanHitLine(npc.position, npc.width, npc.height,
			companion.NPC.position, companion.NPC.width, companion.NPC.height);
		if (catchingUp && !companion.Profile.WorkPaused && (lineOfSight || !npc.noGravity && MathF.Abs(offset.Y) <= 160f)) {
			if (npc.noGravity) {
				Vector2 desired = offset.SafeNormalize(Vector2.Zero) * Math.Min(4f, Math.Max(0f, offset.Length() - 36f) / 24f);
				npc.velocity = desired;
			}
			else if (MathF.Abs(offset.X) > 48f) {
				// Native walking AI can set an opposing velocity every tick; a weak blend never reverses it.
				npc.velocity.X = MathF.Sign(offset.X) * Math.Min(2.4f, (MathF.Abs(offset.X) - 24f) / 28f);
				if (npc.collideX && npc.collideY && npc.velocity.Y == 0f)
					npc.velocity.Y = -4f;
			}
			if (MathF.Abs(npc.velocity.X) > 0.2f)
				npc.direction = npc.velocity.X < 0 ? -1 : 1;
		}
		if (Main.netMode != NetmodeID.MultiplayerClient && ++syncTicks >= 60) {
			syncTicks = 0;
			npc.netUpdate = true;
		}
	}

	public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter writer)
	{
		bitWriter.WriteBit(IsAssigned);
		if (!IsAssigned) return;
		writer.Write(companionIndex);
		writer.Write(companionId.ToByteArray());
	}

	public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader reader)
	{
		if (!bitReader.ReadBit()) { companionIndex = -1; companionId = Guid.Empty; catchingUp = false; return; }
		short index = reader.ReadInt16();
		byte[] id = reader.ReadBytes(16);
		if (index < 0 || index >= Main.maxNPCs || id.Length != 16) throw new InvalidDataException("Invalid critter company binding.");
		Guid identity = new(id);
		if (companionIndex != index || companionId != identity) catchingUp = false;
		companionIndex = index;
		companionId = identity;
	}
}
