#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria.ModLoader.IO;

namespace Soulmates.Common;

public sealed class CompanionRelationship
{
	public Guid WorldId { get; set; }
	public int NpcType { get; set; }
	public string Resident { get; set; } = "";
	public int Meetings { get; set; }
	public int Affinity { get; set; }
	public int LastEmote { get; set; } = -1;
	public string Rank => Affinity < 0 ? "Wary" : Affinity >= 30 ? "Friend" : Affinity >= 10 ? "Familiar" : "New";
	public CompanionRelationship Clone() => (CompanionRelationship)MemberwiseClone();

	internal void Normalize()
	{
		Resident = CleanResident(Resident);
		Meetings = Math.Clamp(Meetings, 0, 10000);
		Affinity = Math.Clamp(Affinity, -50, 50);
		LastEmote = Math.Clamp(LastEmote, -1, short.MaxValue);
	}

	internal static string CleanResident(string name) => new((name ?? "").Trim()
		.Where(character => !char.IsControl(character)).Take(48).ToArray());

	public TagCompound Save() => new() {
		["world"] = WorldId.ToString(), ["type"] = NpcType, ["resident"] = Resident,
		["meetings"] = Meetings, ["affinity"] = Affinity, ["emote"] = LastEmote
	};

	public static CompanionRelationship Load(TagCompound tag) => new() {
		WorldId = Guid.TryParse(tag.GetString("world"), out Guid id) ? id : Guid.Empty,
		NpcType = tag.GetInt("type"), Resident = tag.GetString("resident"),
		Meetings = tag.GetInt("meetings"), Affinity = tag.GetInt("affinity"), LastEmote = tag.GetInt("emote")
	};

	internal void Write(BinaryWriter writer)
	{
		writer.Write(WorldId.ToString());
		writer.Write(NpcType);
		writer.Write(Resident);
		writer.Write((ushort)Meetings);
		writer.Write((sbyte)Affinity);
		writer.Write((short)LastEmote);
	}

	internal static CompanionRelationship Read(BinaryReader reader) => new() {
		WorldId = Guid.TryParse(reader.ReadString(), out Guid id) ? id : Guid.Empty,
		NpcType = reader.ReadInt32(), Resident = reader.ReadString(), Meetings = reader.ReadUInt16(),
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
		return Relationships.FirstOrDefault(relation => relation.WorldId == world
			&& relation.NpcType == npcType && relation.Resident == name);
	}

	public CompanionRelationship MeetResident(Guid world, int npcType, string resident)
	{
		CompanionRelationship relation = FindRelationship(world, npcType, resident)
			?? new CompanionRelationship { WorldId = world, NpcType = npcType, Resident = resident };
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
			&& relation.NpcType > 0 && relation.NpcType <= ushort.MaxValue && relation.Resident.Length > 0)
			.GroupBy(relation => (relation.WorldId, relation.NpcType, relation.Resident))
			.Select(group => group.Last()).TakeLast(MaximumRelationships).ToList();
	}
}
