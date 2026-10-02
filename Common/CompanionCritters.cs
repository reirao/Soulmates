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

public static class CompanionCritters
{
	public static bool IsNatural(NPC npc) => npc.active && npc.life > 0 && npc.catchItem > 0 && npc.catchItem < ItemLoader.ItemCount
		&& npc.type > NPCID.None && npc.type < NPCID.Sets.CountsAsCritter.Length && NPCID.Sets.CountsAsCritter[npc.type]
		&& npc.friendly && !npc.townNPC && !npc.boss && npc.damage == 0 && !npc.SpawnedFromStatue && npc.releaseOwner == 255;

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

// Guide existing native critters only. Their normal AI, collisions, health and catch drops stay intact.
public sealed class CompanionCritterCompany : GlobalNPC
{
	private short companionIndex = -1;
	private Guid companionId;
	private int syncTicks;
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
		critter.netUpdate = true;
		return true;
	}

	internal void Leave(NPC npc)
	{
		if (!IsAssigned || Main.netMode == NetmodeID.MultiplayerClient) return;
		companionIndex = -1;
		companionId = Guid.Empty;
		npc.netUpdate = true;
	}

	public override void PostAI(NPC npc)
	{
		if (!IsAssigned) return;
		SoulboundCompanion? companion = companionIndex < Main.maxNPCs
			? Main.npc[companionIndex].ModNPC as SoulboundCompanion : null;
		if (!CompanionCritters.CanKeepCompany(npc) || companion is null || !companion.NPC.active || !BelongsTo(companion)
			|| companion.NPC.ai[0] < 0 || companion.NPC.ai[0] >= Main.maxPlayers
			|| companion.Profile.CritterMode != CompanionCritterMode.Company || !companion.Profile.AutonomyEnabled
			|| !Main.player[(int)companion.NPC.ai[0]].active || Main.player[(int)companion.NPC.ai[0]].dead
			|| Vector2.DistanceSquared(npc.Center, companion.NPC.Center) > 640f * 640f) {
			Leave(npc);
			return;
		}
		Vector2 offset = companion.NPC.Center - npc.Center;
		if (Collision.CanHitLine(npc.Center, 1, 1, companion.NPC.Center, 1, 1)) {
			if (npc.noGravity) {
				Vector2 desired = offset.SafeNormalize(Vector2.Zero) * Math.Min(2.8f, Math.Max(0f, offset.Length() - 42f) / 32f);
				npc.velocity = Vector2.Lerp(npc.velocity, desired, 0.08f);
			}
			else if (MathF.Abs(offset.X) > 48f)
				npc.velocity.X = MathHelper.Lerp(npc.velocity.X, MathF.Sign(offset.X) * 1.8f, 0.1f);
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
		if (!bitReader.ReadBit()) { companionIndex = -1; companionId = Guid.Empty; return; }
		short index = reader.ReadInt16();
		byte[] id = reader.ReadBytes(16);
		if (index < 0 || index >= Main.maxNPCs || id.Length != 16) throw new InvalidDataException("Invalid critter company binding.");
		companionIndex = index;
		companionId = new Guid(id);
	}
}
