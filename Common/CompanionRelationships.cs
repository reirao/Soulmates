#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Soulmates.Common;

public sealed class CompanionRelationship
{
	public Guid WorldId { get; set; }
	public int NpcType { get; set; }
	public string NpcKey { get; set; } = "";
	public string Resident { get; set; } = "";
	public int Meetings { get; set; }
	public int Affinity { get; set; }
	public int LastEmote { get; set; } = -1;
	public string Rank => Affinity < 0 ? "Wary" : Affinity >= 30 ? "Friend" : Affinity >= 10 ? "Familiar" : "New";
	public CompanionRelationship Clone() => (CompanionRelationship)MemberwiseClone();

	internal void Normalize()
	{
		if (string.IsNullOrEmpty(NpcKey)) NpcKey = LegacyKey(NpcType);
		NpcType = ResolveType(NpcKey);
		Resident = CleanResident(Resident);
		Meetings = Math.Clamp(Meetings, 0, 10000);
		Affinity = Math.Clamp(Affinity, -50, 50);
		LastEmote = Math.Clamp(LastEmote, -1, short.MaxValue);
	}

	internal static string CleanResident(string name) => new((name ?? "").Trim()
		.Where(character => !char.IsControl(character)).Take(48).ToArray());

	internal static string Key(int type) => type >= NPCID.Count && type < NPCLoader.NPCCount
		? NPCLoader.GetNPC(type)?.FullName ?? LegacyKey(type) : LegacyKey(type);
	private static string LegacyKey(int type) => type > 0 && type < NPCID.Count ? $"Terraria/{type}"
		: type >= NPCID.Count && type <= ushort.MaxValue ? $"LegacyNpc/{type}" : "";
	internal static bool ValidKey(string key) => !string.IsNullOrEmpty(key) && key.Length <= 200
		&& key.IndexOf('/') > 0 && !key.Any(char.IsControl)
		&& (!key.StartsWith("Terraria/", StringComparison.Ordinal)
			|| int.TryParse(key.AsSpan(9), out int vanilla) && vanilla > 0 && vanilla < NPCID.Count);
	private static int ResolveType(string key)
	{
		if (!ValidKey(key)) return -1;
		if (key.StartsWith("Terraria/", StringComparison.Ordinal)
			&& int.TryParse(key.AsSpan(9), out int vanilla) && vanilla > 0 && vanilla < NPCID.Count) return vanilla;
		return ModContent.TryFind(key, out ModNPC npc) ? npc.Type : -1;
	}

	public TagCompound Save()
	{
		Normalize();
		return new() {
			["world"] = WorldId.ToString(), ["npcKey"] = NpcKey, ["resident"] = Resident,
			["meetings"] = Meetings, ["affinity"] = Affinity, ["emote"] = LastEmote
		};
	}

	public static CompanionRelationship Load(TagCompound tag)
	{
		var relation = new CompanionRelationship {
			WorldId = Guid.TryParse(tag.GetString("world"), out Guid id) ? id : Guid.Empty,
			NpcKey = tag.ContainsKey("npcKey") ? tag.GetString("npcKey") : LegacyKey(tag.GetInt("type")),
			Resident = tag.GetString("resident"), Meetings = tag.GetInt("meetings"),
			Affinity = tag.GetInt("affinity"), LastEmote = tag.GetInt("emote")
		};
		relation.Normalize();
		return relation;
	}

	internal void Write(BinaryWriter writer)
	{
		Normalize();
		writer.Write(WorldId.ToString());
		writer.Write(NpcKey);
		writer.Write(Resident);
		writer.Write((ushort)Meetings);
		writer.Write((sbyte)Affinity);
		writer.Write((short)LastEmote);
	}

	internal static CompanionRelationship Read(BinaryReader reader) => new() {
		WorldId = Guid.TryParse(reader.ReadString(), out Guid id) ? id : Guid.Empty,
		NpcKey = reader.ReadString(), Resident = reader.ReadString(), Meetings = reader.ReadUInt16(),
		Affinity = reader.ReadSByte(), LastEmote = reader.ReadInt16()
	};
}

public sealed partial class CompanionProfile
{
	public const int MaximumRelationships = 24;
	public List<CompanionRelationship> Relationships { get; set; } = [];

	public CompanionRelationship? FindRelationship(Guid world, int npcType, string resident)
	{
		string name = CompanionRelationship.CleanResident(resident);
		string key = CompanionRelationship.Key(npcType);
		return Relationships.FirstOrDefault(relation => relation.WorldId == world
			&& relation.NpcKey == key && relation.Resident == name);
	}

	public CompanionRelationship MeetResident(Guid world, int npcType, string resident)
	{
		CompanionRelationship relation = FindRelationship(world, npcType, resident)
			?? new CompanionRelationship { WorldId = world, NpcType = npcType,
				NpcKey = CompanionRelationship.Key(npcType), Resident = resident };
		Relationships.Remove(relation);
		string previousRank = relation.Rank;
		relation.Meetings++;
		relation.Affinity += Personality == CompanionPersonality.Gentle ? 2 : 1;
		relation.Normalize();
		Relationships.Add(relation);
		NormalizeRelationships();
		if (relation.Meetings == 1)
			Remember(CompanionMemoryKind.ResidentMet, detail: relation.Resident);
		else if (previousRank != "Friend" && relation.Rank == "Friend")
			Remember(CompanionMemoryKind.ResidentFriend, detail: relation.Resident);
		return relation;
	}

	public void RespondToResident(CompanionRelationship relation, int emote, int affinityDelta)
	{
		if (!Relationships.Contains(relation)) return;
		string previousRank = relation.Rank;
		relation.Affinity += Math.Clamp(affinityDelta, -4, 4);
		relation.LastEmote = emote;
		relation.Normalize();
		if (previousRank != "Friend" && relation.Rank == "Friend")
			Remember(CompanionMemoryKind.ResidentFriend, detail: relation.Resident);
	}

	public string RecallResident(Guid world, int cursor)
	{
		CompanionRelationship[] residents = Relationships.Where(relation => relation.WorldId == world).Reverse().ToArray();
		if (residents.Length == 0) return SoulmatesText.Get("Social.Residents.NoFriends");
		CompanionRelationship relation = residents[(int)(Math.Abs((long)cursor) % residents.Length)];
		return SoulmatesText.Get("Social.Residents.Recall", relation.Resident,
			SoulmatesText.Get($"Social.Residents.Ranks.{relation.Rank}"), relation.Meetings);
	}

	private void NormalizeRelationships()
	{
		foreach (CompanionRelationship relation in Relationships.Where(relation => relation is not null))
			relation.Normalize();
		Relationships = Relationships.Where(relation => relation is not null && relation.WorldId != Guid.Empty
			&& CompanionRelationship.ValidKey(relation.NpcKey) && relation.Resident.Length > 0)
			.GroupBy(relation => (relation.WorldId, relation.NpcKey, relation.Resident))
			.Select(group => group.Last()).TakeLast(MaximumRelationships).ToList();
	}
}
