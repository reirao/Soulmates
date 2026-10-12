#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Soulmates.Common;
using Soulmates.Content.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Soulmates.Common.Feedback;

namespace Soulmates.Content.NPCs;

public sealed partial class SoulboundCompanion
{
	internal bool ConfigurePet(int slot, int expectedType, byte[] token)
	{
		if (IsBossChallenge || Main.netMode == NetmodeID.MultiplayerClient || !NPC.active || !TryGetOwner(out Player owner) || owner.dead
			|| FindBoundSigil() is null || CompanionInventorySync.IsPending(owner) || token.Length != 32) return false;
		if (slot == -1 && expectedType == 0) Profile.PetItemType = 0;
		else {
			if (slot < 0 || slot >= Profile.PetItems.Count || !token.AsSpan().SequenceEqual(Profile.StorageToken(CompanionStorage.Pets, slot))
				|| Profile.PetItems[slot].type != expectedType || !CompanionPets.IsPetItem(Profile.PetItems[slot])) return false;
			Profile.PetItemType = expectedType;
		}
		SyncProfileToBoundSigil();
		NPC.netUpdate = true;
		UpdateEquippedPet();
		SoulmatesFeedbackSystem.Record("pet_selection", ("item_type", Profile.PetItemType), ("summoned", HasEquippedPet));
		return true;
	}

	internal bool HasEquippedPet => EquippedPetCount > 0;
	internal int EquippedPetCount {
		get {
			if (!NPC.active || !TryGetOwner(out Player owner) || owner.dead || IsBossChallenge) return 0;
			var expected = CompanionPets.Followers(Profile).ToHashSet();
			var present = new HashSet<(int ItemType, int Copy)>();
			foreach (Projectile projectile in Main.ActiveProjectiles)
				if (projectile.ModProjectile is CompanionFamiliar pet && pet.BelongsTo(this)
					&& projectile.ai[1] == (int)projectile.ai[1] && projectile.ai[2] == (int)projectile.ai[2]
					&& expected.Contains(((int)projectile.ai[1], (int)projectile.ai[2])))
					present.Add(((int)projectile.ai[1], (int)projectile.ai[2]));
			return present.Count;
		}
	}

	private void UpdateEquippedPet()
	{
		if (Main.netMode == NetmodeID.MultiplayerClient) return;
		bool hasOwner = TryGetOwner(out Player owner);
		bool available = !IsBossChallenge && NPC.active && hasOwner && !owner.dead;
		if (available && Profile.PetItemType != 0 && !CompanionPets.IsCarried(Profile, Profile.PetItemType)) {
			Profile.PetItemType = Profile.PetItems.FirstOrDefault(item => CompanionPets.IsPetItem(item))?.type ?? 0;
			SyncProfileToBoundSigil();
			NPC.netUpdate = true;
		}
		var expected = available ? CompanionPets.Followers(Profile).ToArray() : [];
		var missing = expected.ToHashSet();
		foreach (Projectile projectile in Main.ActiveProjectiles) {
			if (projectile.ModProjectile is not CompanionFamiliar pet || projectile.ai[0] != NPC.whoAmI) continue;
			if (!pet.BelongsTo(this) || projectile.ai[1] != (int)projectile.ai[1] || projectile.ai[2] != (int)projectile.ai[2]
				|| !missing.Remove(((int)projectile.ai[1], (int)projectile.ai[2]))) projectile.Kill();
		}
		for (int formation = 0; formation < expected.Length; formation++) {
			var follower = expected[formation];
			if (!missing.Contains(follower)) continue;
			// Do not replace unrelated projectiles if the world's projectile pool is full.
			if (!Main.projectile.Take(Main.maxProjectiles).Any(projectile => !projectile.active)) break;
			var position = NPC.Center + CompanionPets.FollowerOffset(formation, expected.Length, NPC.direction, 0f);
			int index = Projectile.NewProjectile(NPC.GetSource_FromAI(), position, Microsoft.Xna.Framework.Vector2.Zero,
				ModContent.ProjectileType<CompanionFamiliar>(), 0, 0f,
				Main.netMode == NetmodeID.Server ? Main.maxPlayers : owner.whoAmI,
				NPC.whoAmI, follower.ItemType, follower.Copy);
			if (index >= 0 && index < Main.maxProjectiles && Main.projectile[index].ModProjectile is CompanionFamiliar created) {
				Main.projectile[index].ai[0] = NPC.whoAmI;
				Main.projectile[index].ai[1] = follower.ItemType;
				Main.projectile[index].ai[2] = follower.Copy;
				Main.projectile[index].frameCounter = formation * 3 % 14;
				created.Bind(Profile.Id);
				TraceDiagnostic($"familiar spawned: item {follower.ItemType}, copy {follower.Copy}, projectile {index}");
				Main.projectile[index].netUpdate = true;
			}
		}
	}
}
