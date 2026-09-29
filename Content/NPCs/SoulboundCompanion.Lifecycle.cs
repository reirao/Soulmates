#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Soulmates.Common;
using Soulmates.Common.Dialogue;
using Soulmates.Common.UI;
using Soulmates.Content.Items;
using Soulmates.Content.Projectiles;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulmates.Content.NPCs;

public sealed partial class SoulboundCompanion
{
	public void Recall()
	{
		SyncProfileToBoundSigil();
		SyncOwnerInventory();
		StopOwnedEffects();
		if (Owner.active)
			Owner.GetModPlayer<SoulmatesPlayer>().ActiveCompanionWhoAmI = -1;
		NPC.active = false;
		NPC.netUpdate = true;
		if (Main.netMode == NetmodeID.Server)
			NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
	}

	private void SyncOwnerInventory()
	{
		if (Main.netMode != NetmodeID.Server)
			return;
		for (int slot = 0; slot < Owner.inventory.Length; slot++)
			NetMessage.SendData(MessageID.SyncEquipment, Owner.whoAmI, -1, null, Owner.whoAmI, slot);
	}

	public static SoulboundCompanion? FindFor(Player player)
	{
		SoulmatesPlayer state = player.GetModPlayer<SoulmatesPlayer>();
		if (IsOwnedCompanion(state.ActiveCompanionWhoAmI, player))
			return Main.npc[state.ActiveCompanionWhoAmI].ModNPC as SoulboundCompanion;

		for (int i = 0; i < Main.maxNPCs; i++) {
			NPC npc = Main.npc[i];
			if (IsOwnedCompanion(i, player)) {
				state.ActiveCompanionWhoAmI = i;
				return npc.ModNPC as SoulboundCompanion;
			}
		}
		state.ActiveCompanionWhoAmI = -1;
		return null;
	}

	public static void RecallAllFor(Player player)
	{
		for (int i = 0; i < Main.maxNPCs; i++) {
			if (IsOwnedCompanion(i, player) && Main.npc[i].ModNPC is SoulboundCompanion companion)
				companion.Recall();
		}
		player.GetModPlayer<SoulmatesPlayer>().ActiveCompanionWhoAmI = -1;
	}

	private bool ClaimActiveSlot(Player owner)
	{
		SoulmatesPlayer state = owner.GetModPlayer<SoulmatesPlayer>();
		if (state.ActiveCompanionWhoAmI == NPC.whoAmI)
			return true;
		if (IsOwnedCompanion(state.ActiveCompanionWhoAmI, owner)) {
			int keeper = state.ActiveCompanionWhoAmI;
			Recall();
			state.ActiveCompanionWhoAmI = keeper;
			return false;
		}
		state.ActiveCompanionWhoAmI = NPC.whoAmI;
		return true;
	}

	private void StopOwnedEffects()
	{
		for (int i = 0; i < Main.maxProjectiles; i++) {
			Projectile projectile = Main.projectile[i];
			if (projectile.active && projectile.type == ModContent.ProjectileType<SoulBolt>()
				&& (int)projectile.ai[1] == NPC.whoAmI)
				projectile.Kill();
		}
		guardianTarget = -1;
		activeJob = CompanionJob.None;
		jobRecoveryPaused = false;
		jobPlannedTotal = 0;
		miningPlanReady = false;
		plannedMiningTargets.Clear();
		plannedMiningCursor = 0;
		CancelAutonomousActivity();
		speechTimer = 0;
		speechDuration = 0;
		speechText = "";
		speechAnchorWorld = Vector2.Zero;
		speechTrailWorld = Vector2.Zero;
	}

	private static bool IsOwnedCompanion(int index, Player player)
	{
		if (index < 0 || index >= Main.maxNPCs)
			return false;
		NPC npc = Main.npc[index];
		return npc.active && npc.type == ModContent.NPCType<SoulboundCompanion>() && (int)npc.ai[0] == player.whoAmI;
	}

	private bool TryGetOwner(out Player owner)
	{
		int index = (int)NPC.ai[0];
		if (index < 0 || index >= Main.maxPlayers) {
			owner = null!;
			return false;
		}
		owner = Main.player[index];
		return owner.active;
	}

}
