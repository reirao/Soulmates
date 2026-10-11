#nullable enable
using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Content.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulmates.Content.NPCs;

public sealed partial class SoulboundCompanion
{
	private int retinueSpawnCooldown;
	internal Vector2 RetinueAnchor => Command == StayCommand ? idleTarget : NPC.Center;
	internal NPC? RetinueTarget => TryGetOwner(out Player owner) && !owner.dead
		&& !CompanionInventorySync.IsPending(owner)
		? GetSynchronizedDefenseTarget(Command == StayCommand ? idleTarget : owner.Center) : null;
	internal bool CanMaintainRetinue => NPC.active && TryGetOwner(out Player owner) && !owner.dead
		&& FindBoundSigil() is not null;

	internal bool ConfigureRetinue(int slot, int expectedType, byte[] token)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || !CanMaintainRetinue
			|| CompanionInventorySync.IsPending(Owner) || token.Length != 32) return false;
		if (slot == -1 && expectedType == 0) Profile.RetinueEnabled = false;
		else {
			if (slot < 0 || slot >= Profile.SummonItems.Count || Profile.SummonItems[slot].type != expectedType
				|| !CompanionRetinue.IsStaff(Profile.SummonItems[slot])
				|| !token.AsSpan().SequenceEqual(Profile.StorageToken(CompanionStorage.Summons, slot))) return false;
			Profile.RetinueItemType = expectedType;
			Profile.RetinueEnabled = true;
		}
		UpdateRetinue();
		SyncProfileToBoundSigil();
		NPC.netUpdate = true;
		return true;
	}

	private void UpdateRetinue()
	{
		if (Main.netMode == NetmodeID.MultiplayerClient) return;
		Profile.NormalizeRetinueSelection();
		bool available = CanMaintainRetinue;
		var wanted = CompanionRetinue.ActiveItems(Profile).Select(item => item.type).ToHashSet();
		foreach (Projectile projectile in Main.ActiveProjectiles) {
			if (projectile.ModProjectile is CompanionRetinueBolt bolt && bolt.BelongsTo(this)) {
				if (!available || !CompanionRetinue.IsActive(Profile, (int)projectile.ai[1])) projectile.Kill();
				continue;
			}
			if (projectile.ModProjectile is not CompanionRetainer retainer || !retainer.BelongsTo(this)) continue;
			if (!available || !wanted.Remove((int)projectile.ai[1])) projectile.Kill();
		}
		if (!available || CompanionInventorySync.IsPending(Owner) || retinueSpawnCooldown-- > 0 || wanted.Count == 0) return;
		// Stagger large gifts and failed allocations; never spawn an army every frame.
		retinueSpawnCooldown = 12;
		int itemType = wanted.First();
		Item item = Profile.SummonItems.First(item => item.type == itemType);
		int index = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
			ModContent.ProjectileType<CompanionRetainer>(), CompanionRetinue.Damage(Profile, item), item.knockBack,
			Main.myPlayer, NPC.whoAmI, itemType);
		if (index >= 0 && index < Main.maxProjectiles && Main.projectile[index].ModProjectile is CompanionRetainer created) {
			created.Bind(Profile.Id);
			Main.projectile[index].npcProj = true;
			Main.projectile[index].netUpdate = true;
		}
	}
}
