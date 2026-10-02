using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Soulmates.Common;

public sealed partial class CompanionProfile
{
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
