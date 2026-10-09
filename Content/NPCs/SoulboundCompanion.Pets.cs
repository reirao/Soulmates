#nullable enable
using System;
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
		if (Main.netMode == NetmodeID.MultiplayerClient || !TryGetOwner(out Player owner) || owner.dead
			|| FindBoundSigil() is null || CompanionInventorySync.IsPending(owner) || token.Length != 32) return false;
		if (slot == -1 && expectedType == 0) Profile.PetItemType = 0;
		else {
			if (slot < 0 || slot >= Profile.PetItems.Count || !token.AsSpan().SequenceEqual(Profile.StorageToken(CompanionStorage.Pets, slot))
				|| Profile.PetItems[slot].type != expectedType || !CompanionPets.IsCarried(Profile, expectedType)) return false;
			Profile.PetItemType = expectedType;
		}
		SyncProfileToBoundSigil();
		NPC.netUpdate = true;
		UpdateEquippedPet();
		SoulmatesFeedbackSystem.Record("pet_selection", ("item_type", Profile.PetItemType), ("summoned", HasEquippedPet));
		return true;
	}

	internal bool HasEquippedPet {
		get {
			if (Profile.PetItemType == 0) return false;
			foreach (Projectile projectile in Main.ActiveProjectiles)
				if (projectile.ModProjectile is CompanionFamiliar pet && pet.BelongsTo(this)
					&& (int)projectile.ai[1] == Profile.PetItemType) return true;
			return false;
		}
	}

	private void UpdateEquippedPet()
	{
		if (Main.netMode == NetmodeID.MultiplayerClient) return;
		if (Profile.PetItemType != 0 && !CompanionPets.IsCarried(Profile, Profile.PetItemType)) {
			Profile.PetItemType = 0;
			SyncProfileToBoundSigil();
			NPC.netUpdate = true;
		}
		bool present = false;
		foreach (Projectile projectile in Main.ActiveProjectiles) {
			if (projectile.ModProjectile is not CompanionFamiliar pet || !pet.BelongsTo(this)) continue;
			if (present || Profile.PetItemType == 0 || (int)projectile.ai[1] != Profile.PetItemType) projectile.Kill();
			else present = true;
		}
		if (present || Profile.PetItemType == 0 || !TryGetOwner(out Player owner) || owner.dead) return;
		int index = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Microsoft.Xna.Framework.Vector2.Zero,
			ModContent.ProjectileType<CompanionFamiliar>(), 0, 0f,
			Main.netMode == NetmodeID.Server ? Main.maxPlayers : owner.whoAmI,
			NPC.whoAmI, Profile.PetItemType);
		if (index >= 0 && index < Main.maxProjectiles && Main.projectile[index].ModProjectile is CompanionFamiliar created) {
			Main.projectile[index].ai[0] = NPC.whoAmI;
			Main.projectile[index].ai[1] = Profile.PetItemType;
			created.Bind(Profile.Id);
			TraceDiagnostic($"familiar spawned: item {Profile.PetItemType}, projectile {index}");
			Main.projectile[index].netUpdate = true;
		}
	}
}
