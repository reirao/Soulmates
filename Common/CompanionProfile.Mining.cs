using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulmates.Common;

public sealed partial class CompanionProfile
{
	private List<string> missingMiningKnowledge = [];

	private List<string> MiningKnowledgeKeys() => LearnedMiningTiles.Select(CompanionMiningRules.Key)
		.Concat(missingMiningKnowledge).Where(CompanionMiningRules.ValidKey).Distinct()
		.Take(MaximumLearnedMiningTiles).ToList();

	private void LoadMiningKnowledge(IEnumerable<string> keys)
	{
		LearnedMiningTiles.Clear();
		missingMiningKnowledge.Clear();
		foreach (string key in keys.Where(key => key is not null && CompanionMiningRules.ValidKey(key))
			.Distinct().Take(MaximumLearnedMiningTiles)) {
			if (key.StartsWith("Terraria/", System.StringComparison.Ordinal)
				&& int.TryParse(key.AsSpan(9), out int vanilla) && vanilla >= 0 && vanilla < TileID.Count)
				LearnedMiningTiles.Add(vanilla);
			else if (ModContent.TryFind(key, out ModTile tile)) LearnedMiningTiles.Add(tile.Type);
			else missingMiningKnowledge.Add(key);
		}
	}

	public bool AllowsAutomaticMining(int tileType) => tileType is >= 0 and <= ushort.MaxValue
		&& (CompanionMiningRules.IsOre((ushort)tileType)
			? !BlockedAutoMiningTiles.Contains(CompanionMiningRules.Key(tileType))
			: KnowsMiningMaterial(tileType) && AllowedAutoMiningMaterials.Contains(CompanionMiningRules.Key(tileType)));

	private void NormalizeMiningRules()
	{
		BlockedAutoMiningTiles = NormalizeRules(BlockedAutoMiningTiles);
		AllowedAutoMiningMaterials = NormalizeRules(AllowedAutoMiningMaterials);
	}

	private static List<string> NormalizeRules(IEnumerable<string> rules) => rules
		.Where(key => key is not null && CompanionMiningRules.ValidKey(key)).Distinct()
		.Take(CompanionMiningRules.MaximumRules).ToList();

	private static void WriteMiningRules(BinaryWriter writer, IEnumerable<string> rules)
	{
		List<string> entries = NormalizeRules(rules);
		writer.Write((ushort)entries.Count);
		foreach (string key in entries) writer.Write(key);
	}

	private static List<string> ReadMiningRules(BinaryReader reader)
	{
		int count = reader.ReadUInt16();
		if (count > CompanionMiningRules.MaximumRules) throw new InvalidDataException("Too many mining rules.");
		var rules = new List<string>(count);
		for (int i = 0; i < count; i++) {
			string key = reader.ReadString();
			if (!CompanionMiningRules.ValidKey(key)) throw new InvalidDataException("Invalid mining rule.");
			rules.Add(key);
		}
		return rules;
	}
}
