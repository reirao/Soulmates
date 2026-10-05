#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria.GameContent.UI;
using Terraria.ModLoader.IO;

namespace Soulmates.Common;

public sealed record CompanionAbilityDefinition(
	CompanionInitiativeKind Kind, byte ActivityCode, string ActivityName,
	CompanionQuickAction PolicyAction, string SaveKey, int Emote, int RuleEmote,
	int MinimumEnergy, int MinimumMood,
	Func<CompanionProfile, CompanionInitiativePolicy> ReadPolicy,
	Action<CompanionProfile, CompanionInitiativePolicy> WritePolicy)
{
	public bool HasStamina(CompanionProfile profile) => profile.Energy >= MinimumEnergy && profile.Mood >= MinimumMood;
}

// Stable kind/activity codes and legacy tag names are part of the save/network contract.
public static class CompanionAbilityRegistry
{
	public static IReadOnlyList<CompanionAbilityDefinition> All { get; } = Array.AsReadOnly(new[] {
		new CompanionAbilityDefinition(CompanionInitiativeKind.Gathering, 1, "FetchItem", CompanionQuickAction.GatheringPolicy,
			"gatheringInitiative", EmoteID.EmotionAlert, EmoteID.ItemGoldpile, 16, 15,
			p => p.GatheringInitiative, (p, value) => p.GatheringInitiative = value),
		new CompanionAbilityDefinition(CompanionInitiativeKind.Mining, 2, "AssistMining", CompanionQuickAction.MiningPolicy,
			"miningInitiative", EmoteID.ItemPickaxe, EmoteID.ItemPickaxe, 16, 15,
			p => p.MiningInitiative, (p, value) => p.MiningInitiative = value),
		new CompanionAbilityDefinition(CompanionInitiativeKind.Forestry, 4, "TendForest", CompanionQuickAction.ForestryPolicy,
			"forestryInitiative", EmoteID.MiscTree, EmoteID.MiscTree, 16, 15,
			p => p.ForestryInitiative, (p, value) => p.ForestryInitiative = value),
		new CompanionAbilityDefinition(CompanionInitiativeKind.Treasure, 3, "InspectTreasure", CompanionQuickAction.TreasurePolicy,
			"treasureInitiative", EmoteID.ItemGoldpile, EmoteID.ItemDiamondRing, 16, 15,
			p => p.TreasureInitiative, (p, value) => p.TreasureInitiative = value),
		new CompanionAbilityDefinition(CompanionInitiativeKind.CritterCompany, 5, "InviteCritter", CompanionQuickAction.CritterCompanyPolicy,
			"critterCompanyInitiative", EmoteID.CritterBunny, EmoteID.CritterBunny, 20, 20,
			p => p.CritterCompanyInitiative, (p, value) => p.CritterCompanyInitiative = value),
		new CompanionAbilityDefinition(CompanionInitiativeKind.CritterCollect, 6, "CatchCritter", CompanionQuickAction.CritterCollectPolicy,
			"critterCollectInitiative", EmoteID.ItemBugNet, EmoteID.ItemBugNet, 20, 20,
			p => p.CritterCollectInitiative, (p, value) => p.CritterCollectInitiative = value)
	});
	private static readonly Dictionary<byte, CompanionAbilityDefinition> ByActivity = All.ToDictionary(ability => ability.ActivityCode);

	static CompanionAbilityRegistry()
	{
		if (All.Count != Enum.GetValues<CompanionInitiativeKind>().Length
			|| All.Select(a => a.Kind).Distinct().Count() != All.Count
			|| All.Select(a => a.ActivityCode).Distinct().Count() != All.Count
			|| All.Select(a => a.ActivityName).Distinct().Count() != All.Count
			|| All.Select(a => a.SaveKey).Distinct().Count() != All.Count
			|| All.Select(a => a.PolicyAction).Distinct().Count() != All.Count
			|| All.Where((a, i) => (int)a.Kind != i || a.ActivityCode == 0).Any())
			throw new InvalidOperationException("Incomplete or ambiguous companion ability registration.");
	}

	public static CompanionAbilityDefinition? Find(CompanionInitiativeKind kind)
		=> Enum.IsDefined(kind) ? All[(int)kind] : null;
	public static CompanionAbilityDefinition? Find(CompanionQuickAction action)
		=> All.FirstOrDefault(ability => ability.PolicyAction == action);
	public static CompanionAbilityDefinition ForActivity(byte code)
		=> ByActivity.TryGetValue(code, out CompanionAbilityDefinition? ability) ? ability
			: throw new ArgumentOutOfRangeException(nameof(code), code, "Unregistered companion activity.");

	public static void CopyPolicies(CompanionProfile source, CompanionProfile destination)
	{
		foreach (CompanionAbilityDefinition ability in All) ability.WritePolicy(destination, ability.ReadPolicy(source));
	}
	public static void SavePolicies(CompanionProfile profile, TagCompound tag)
	{
		foreach (CompanionAbilityDefinition ability in All) tag[ability.SaveKey] = (byte)ability.ReadPolicy(profile);
	}
	public static void LoadPolicies(CompanionProfile profile, TagCompound tag)
	{
		foreach (CompanionAbilityDefinition ability in All)
			ability.WritePolicy(profile, Normalize(tag.ContainsKey(ability.SaveKey)
				? (CompanionInitiativePolicy)tag.GetByte(ability.SaveKey) : CompanionInitiativePolicy.Ask));
	}
	public static void NormalizePolicies(CompanionProfile profile)
	{
		foreach (CompanionAbilityDefinition ability in All) ability.WritePolicy(profile, Normalize(ability.ReadPolicy(profile)));
	}
	public static void ResetPolicies(CompanionProfile profile)
	{
		foreach (CompanionAbilityDefinition ability in All) ability.WritePolicy(profile, CompanionInitiativePolicy.Ask);
	}
	public static void WritePolicies(BinaryWriter writer, CompanionProfile profile, bool legacy)
	{
		foreach (CompanionAbilityDefinition ability in All)
			if (((int)ability.Kind < 4) == legacy) writer.Write((byte)ability.ReadPolicy(profile));
	}
	public static void ReadPolicies(BinaryReader reader, CompanionProfile profile, bool legacy)
	{
		foreach (CompanionAbilityDefinition ability in All)
			if (((int)ability.Kind < 4) == legacy) ability.WritePolicy(profile, Normalize((CompanionInitiativePolicy)reader.ReadByte()));
	}
	public static CompanionInitiativePolicy Normalize(CompanionInitiativePolicy policy)
		=> Enum.IsDefined(policy) ? policy : CompanionInitiativePolicy.Ask;
}
