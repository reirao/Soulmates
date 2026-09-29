#nullable enable
using System;
using System.Text;
using Soulmates.Content.Items;
using Terraria;
using Terraria.ModLoader;

namespace Soulmates.Common;

internal enum CompanionCreationResult : byte
{
	Success,
	MissingBlankSigil,
	InventoryFull,
	InvalidRequest
}

internal static class CompanionCreationService
{
	private const int MainInventorySlotCount = 50;

	public static CompanionCreationResult TryCreate(Player player, CompanionProfile template,
		out CompanionProfile createdProfile)
	{
		createdProfile = CreateFreshProfile(template);
		if (!IsValidTemplate(template))
			return CompanionCreationResult.InvalidRequest;

		int blankSlot = FindBlankSigil(player);
		if (blankSlot < 0)
			return CompanionCreationResult.MissingBlankSigil;

		Item blank = player.inventory[blankSlot];
		int destinationSlot = blank.stack == 1 ? blankSlot : FindEmptyInventorySlot(player);
		if (destinationSlot < 0)
			return CompanionCreationResult.InventoryFull;

		var boundItem = new Item();
		boundItem.SetDefaults(ModContent.ItemType<SoulboundSigil>());
		if (boundItem.ModItem is not SoulboundSigil sigil)
			return CompanionCreationResult.InvalidRequest;

		sigil.Profile = createdProfile.Clone();
		if (blank.stack == 1)
			player.inventory[blankSlot] = boundItem;
		else {
			blank.stack--;
			player.inventory[destinationSlot] = boundItem;
		}
		return CompanionCreationResult.Success;
	}

	public static string NormalizeName(string? name)
	{
		var result = new StringBuilder(24);
		foreach (char character in (name ?? "").Trim()) {
			if (!char.IsControl(character) && result.Length < 24)
				result.Append(character);
		}
		return result.Length > 0 ? result.ToString() : "Luma";
	}

	private static CompanionProfile CreateFreshProfile(CompanionProfile template)
	{
		var profile = new CompanionProfile {
			Id = Guid.NewGuid(),
			Name = NormalizeName(template.Name),
			Muse = template.Muse,
			Form = template.Form,
			Essence = template.Essence,
			Aura = template.Aura,
			Personality = template.Personality,
			Talent = template.Talent,
			Voice = CompanionVoice.Soft,
			AutonomyEnabled = true
		};
		profile.Normalize();
		profile.Remember(CompanionMemoryKind.Awakened);
		return profile;
	}

	private static bool IsValidTemplate(CompanionProfile template) => Enum.IsDefined(template.Muse)
		&& Enum.IsDefined(template.Form) && Enum.IsDefined(template.Essence) && Enum.IsDefined(template.Aura)
		&& Enum.IsDefined(template.Personality) && Enum.IsDefined(template.Talent);

	private static int FindBlankSigil(Player player)
	{
		int blankType = ModContent.ItemType<BlankSigil>();
		int length = Math.Min(MainInventorySlotCount, player.inventory.Length);
		for (int slot = 0; slot < length; slot++) {
			if (!player.inventory[slot].IsAir && player.inventory[slot].type == blankType)
				return slot;
		}
		return -1;
	}

	private static int FindEmptyInventorySlot(Player player)
	{
		int length = Math.Min(MainInventorySlotCount, player.inventory.Length);
		for (int slot = 0; slot < length; slot++) {
			if (player.inventory[slot].IsAir)
				return slot;
		}
		return -1;
	}
}
