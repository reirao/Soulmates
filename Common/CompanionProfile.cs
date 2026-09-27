using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
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

public enum CompanionMuse : byte
{
	Soulkin,
	Bunny,
	BlueSlime,
	Bird,
	Squirrel
}

public enum CompanionVoice : byte
{
	Soft,
	Direct,
	Playful
}

public enum CompanionTrinket : byte
{
	None,
	StarfinderBell,
	DelverCharm,
	HearthRibbon
}

public enum CompanionJob : byte
{
	None,
	FindTreasure,
	Mine,
	Gather
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
	public CompanionMuse Muse { get; set; }
	public CompanionVoice Voice { get; set; }
	public CompanionTrinket Trinket { get; set; }
	public int Bond { get; set; }
	public int Mood { get; set; } = 100;
	public int Energy { get; set; } = 100;
	public int JobsCompleted { get; set; }
	public string LastMemory { get; set; } = "We have only just met.";
	public List<Item> Pack { get; set; } = [];

	public int PackCapacity => Trinket == CompanionTrinket.HearthRibbon ? 12 : 8;
	public int PackLoad => Pack.Count(item => !item.IsAir);

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
		Muse = Muse,
		Voice = Voice,
		Trinket = Trinket,
		Bond = Bond,
		Mood = Mood,
		Energy = Energy,
		JobsCompleted = JobsCompleted,
		LastMemory = LastMemory,
		Pack = Pack.Where(item => !item.IsAir).Select(item => item.Clone()).ToList()
	};

	public TagCompound Save() => new() {
		["id"] = Id.ToString(),
		["name"] = Name,
		["personality"] = (byte)Personality,
		["talent"] = (byte)Talent,
		["essence"] = (byte)Essence,
		["form"] = (byte)Form,
		["aura"] = (byte)Aura,
		["muse"] = (byte)Muse,
		["voice"] = (byte)Voice,
		["trinket"] = (byte)Trinket,
		["bond"] = Bond,
		["mood"] = Mood,
		["energy"] = Energy,
		["jobsCompleted"] = JobsCompleted,
		["lastMemory"] = LastMemory,
		["pack"] = Pack.Where(item => !item.IsAir).Select(ItemIO.Save).ToList()
	};

	public static CompanionProfile Load(TagCompound tag) => new() {
		Id = Guid.TryParse(tag.GetString("id"), out Guid id) ? id : Guid.NewGuid(),
		Name = tag.GetString("name") is { Length: > 0 } name ? name : "Luma",
		Personality = (CompanionPersonality)tag.GetByte("personality"),
		Talent = (CompanionTalent)tag.GetByte("talent"),
		Essence = (CompanionEssence)tag.GetByte("essence"),
		Form = tag.ContainsKey("form") ? (CompanionForm)tag.GetByte("form") : CompanionForm.Balanced,
		Aura = tag.ContainsKey("aura") ? (CompanionAura)tag.GetByte("aura") : CompanionAura.SoftGlow,
		Muse = tag.ContainsKey("muse") ? (CompanionMuse)tag.GetByte("muse") : CompanionMuse.Soulkin,
		Voice = tag.ContainsKey("voice") ? (CompanionVoice)tag.GetByte("voice") : CompanionVoice.Soft,
		Trinket = tag.ContainsKey("trinket") ? (CompanionTrinket)tag.GetByte("trinket") : CompanionTrinket.None,
		Bond = tag.GetInt("bond"),
		Mood = tag.ContainsKey("mood") ? tag.GetInt("mood") : 100,
		Energy = tag.ContainsKey("energy") ? tag.GetInt("energy") : 100,
		JobsCompleted = tag.ContainsKey("jobsCompleted") ? tag.GetInt("jobsCompleted") : 0,
		LastMemory = tag.ContainsKey("lastMemory") && tag.GetString("lastMemory") is { Length: > 0 } memory
			? memory
			: "We have only just met.",
		Pack = tag.ContainsKey("pack") ? tag.GetList<TagCompound>("pack").Select(ItemIO.Load).Where(item => !item.IsAir).ToList() : []
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
		writer.Write((byte)Muse);
		writer.Write((byte)Voice);
		writer.Write((byte)Trinket);
		writer.Write(Bond);
		writer.Write(Mood);
		writer.Write(Energy);
		writer.Write(JobsCompleted);
		writer.Write(LastMemory);
		writer.Write((byte)Math.Min(Pack.Count, byte.MaxValue));
		foreach (Item item in Pack.Take(byte.MaxValue))
			ItemIO.Send(item, writer, writeStack: true, writeFavorite: false);
	}

	public static CompanionProfile Read(BinaryReader reader)
	{
		var profile = new CompanionProfile {
			Id = Guid.TryParse(reader.ReadString(), out Guid id) ? id : Guid.NewGuid(),
			Name = reader.ReadString(),
			Personality = (CompanionPersonality)reader.ReadByte(),
			Talent = (CompanionTalent)reader.ReadByte(),
			Essence = (CompanionEssence)reader.ReadByte(),
			Form = (CompanionForm)reader.ReadByte(),
			Aura = (CompanionAura)reader.ReadByte(),
			Muse = (CompanionMuse)reader.ReadByte(),
			Voice = (CompanionVoice)reader.ReadByte(),
			Trinket = (CompanionTrinket)reader.ReadByte(),
			Bond = reader.ReadInt32(),
			Mood = reader.ReadInt32(),
			Energy = reader.ReadInt32(),
			JobsCompleted = reader.ReadInt32(),
			LastMemory = reader.ReadString()
		};
		int count = reader.ReadByte();
		for (int i = 0; i < count; i++)
			profile.Pack.Add(ItemIO.Receive(reader, readStack: true, readFavorite: false));
		return profile;
	}

	public int Store(Item source)
	{
		if (source.IsAir)
			return 0;
		int originalStack = source.stack;
		foreach (Item stored in Pack) {
			if (stored.type != source.type || stored.prefix != source.prefix || stored.stack >= stored.maxStack)
				continue;
			int moved = Math.Min(source.stack, stored.maxStack - stored.stack);
			stored.stack += moved;
			source.stack -= moved;
			if (source.stack <= 0) {
				source.TurnToAir();
				return originalStack;
			}
		}

		while (!source.IsAir && PackLoad < PackCapacity) {
			Item stored = source.Clone();
			stored.stack = Math.Min(source.stack, source.maxStack);
			stored.favorited = false;
			Pack.Add(stored);
			source.stack -= stored.stack;
			if (source.stack <= 0)
				source.TurnToAir();
		}
		return originalStack - (source.IsAir ? 0 : source.stack);
	}

	public string DescribePack()
	{
		if (PackLoad == 0)
			return "My pack is empty.";
		string contents = string.Join(", ", Pack.Where(item => !item.IsAir).Take(5).Select(item => $"{item.Name} x{item.stack}"));
		if (PackLoad > 5)
			contents += $", and {PackLoad - 5} more stacks";
		return $"I carry {contents}. ({PackLoad}/{PackCapacity})";
	}
}
