using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria.ModLoader.IO;

namespace Soulmates.Common;

public enum CompanionPersonality : byte
{
	Curious,
	Loyal,
	Brave,
	Gentle,
	Mischievous
}

public enum CompanionTalent : byte
{
	TreasureSeeker,
	Miner,
	Guardian,
	Gatherer,
	Healer
}

public enum CompanionEssence : byte
{
	Starlight,
	Ember,
	Verdant,
	Tide,
	Amethyst
}

public enum CompanionForm : byte
{
	Balanced,
	Round,
	Wisp
}

public enum CompanionAura : byte
{
	SoftGlow,
	OrbitingStars,
	SoulSparks
}

public sealed class CompanionProfile
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public string Name { get; set; } = "Luma";
	public CompanionPersonality Personality { get; set; }
	public CompanionTalent Talent { get; set; }
	public CompanionEssence Essence { get; set; }
	public CompanionForm Form { get; set; }
	public CompanionAura Aura { get; set; }
	public int Bond { get; set; }
	public int Mood { get; set; } = 100;
	public int Energy { get; set; } = 100;

	public Color EssenceColor => Essence switch {
		CompanionEssence.Ember => new Color(255, 121, 77),
		CompanionEssence.Verdant => new Color(105, 218, 121),
		CompanionEssence.Tide => new Color(80, 190, 240),
		CompanionEssence.Amethyst => new Color(194, 119, 255),
		_ => new Color(255, 235, 145)
	};

	public CompanionProfile Clone() => new() {
		Id = Id,
		Name = Name,
		Personality = Personality,
		Talent = Talent,
		Essence = Essence,
		Form = Form,
		Aura = Aura,
		Bond = Bond,
		Mood = Mood,
		Energy = Energy
	};

	public TagCompound Save() => new() {
		["id"] = Id.ToString(),
		["name"] = Name,
		["personality"] = (byte)Personality,
		["talent"] = (byte)Talent,
		["essence"] = (byte)Essence,
		["form"] = (byte)Form,
		["aura"] = (byte)Aura,
		["bond"] = Bond,
		["mood"] = Mood,
		["energy"] = Energy
	};

	public static CompanionProfile Load(TagCompound tag) => new() {
		Id = Guid.TryParse(tag.GetString("id"), out Guid id) ? id : Guid.NewGuid(),
		Name = tag.GetString("name") is { Length: > 0 } name ? name : "Luma",
		Personality = (CompanionPersonality)tag.GetByte("personality"),
		Talent = (CompanionTalent)tag.GetByte("talent"),
		Essence = (CompanionEssence)tag.GetByte("essence"),
		Form = tag.ContainsKey("form") ? (CompanionForm)tag.GetByte("form") : CompanionForm.Balanced,
		Aura = tag.ContainsKey("aura") ? (CompanionAura)tag.GetByte("aura") : CompanionAura.SoftGlow,
		Bond = tag.GetInt("bond"),
		Mood = tag.ContainsKey("mood") ? tag.GetInt("mood") : 100,
		Energy = tag.ContainsKey("energy") ? tag.GetInt("energy") : 100
	};

	public void Write(BinaryWriter writer)
	{
		writer.Write(Id.ToString());
		writer.Write(Name);
		writer.Write((byte)Personality);
		writer.Write((byte)Talent);
		writer.Write((byte)Essence);
		writer.Write((byte)Form);
		writer.Write((byte)Aura);
		writer.Write(Bond);
		writer.Write(Mood);
		writer.Write(Energy);
	}

	public static CompanionProfile Read(BinaryReader reader) => new() {
		Id = Guid.TryParse(reader.ReadString(), out Guid id) ? id : Guid.NewGuid(),
		Name = reader.ReadString(),
		Personality = (CompanionPersonality)reader.ReadByte(),
		Talent = (CompanionTalent)reader.ReadByte(),
		Essence = (CompanionEssence)reader.ReadByte(),
		Form = (CompanionForm)reader.ReadByte(),
		Aura = (CompanionAura)reader.ReadByte(),
		Bond = reader.ReadInt32(),
		Mood = reader.ReadInt32(),
		Energy = reader.ReadInt32()
	};
}
