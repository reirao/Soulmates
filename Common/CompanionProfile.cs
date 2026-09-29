using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
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

public enum LearnedBehavior : byte
{
	Gathering,
	Mining,
	Forestry,
	Combat,
	Exploration
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

public enum CompanionEmote : byte
{
	Wave,
	Heart,
	Cheer,
	Comfort,
	Laugh,
	Rest
}

public enum CompanionQuickAction : byte
{
	Follow,
	Stay,
	Explore,
	FindTreasure,
	Mine,
	Gather,
	ToggleAutonomy,
	Details
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

public enum BondRank : byte
{
	Newbound,
	Kindred,
	Trusted,
	Soulbound,
	Eternal
}

public enum CompanionMemoryKind : byte
{
	Awakened,
	TreasureFound,
	MiningCompleted,
	GatheringCompleted,
	GuardianVictory,
	HealerAid,
	TrinketEquipped,
	TrinketRemoved,
	BondMilestone,
	CreatureEncounter,
	ExperienceMilestone,
	SharedMoment,
	LearnedPerk
}

public sealed class CompanionMemory
{
	public CompanionMemoryKind Kind { get; set; }
	public int Amount { get; set; }
	public string Detail { get; set; } = "";

	public CompanionMemory Clone() => new() { Kind = Kind, Amount = Amount, Detail = Detail };

	public TagCompound Save() => new() {
		["kind"] = (byte)Kind,
		["amount"] = Amount,
		["detail"] = Detail
	};

	public static CompanionMemory Load(TagCompound tag)
	{
		var memory = new CompanionMemory {
			Kind = (CompanionMemoryKind)tag.GetByte("kind"),
			Amount = tag.GetInt("amount"),
			Detail = tag.GetString("detail")
		};
		memory.Normalize();
		return memory;
	}

	public void Write(BinaryWriter writer)
	{
		writer.Write((byte)Kind);
		writer.Write(Amount);
		writer.Write(Detail);
	}

	public static CompanionMemory Read(BinaryReader reader)
	{
		var memory = new CompanionMemory {
			Kind = (CompanionMemoryKind)reader.ReadByte(),
			Amount = reader.ReadInt32(),
			Detail = reader.ReadString()
		};
		memory.Normalize();
		return memory;
	}

	public string Describe() => Kind switch {
		CompanionMemoryKind.Awakened => SoulmatesText.Get("Memories.Chronicle.Awakened"),
		CompanionMemoryKind.TreasureFound => SoulmatesText.Get("Memories.Chronicle.TreasureFound", Amount),
		CompanionMemoryKind.MiningCompleted => SoulmatesText.Get("Memories.Chronicle.MiningCompleted", Amount),
		CompanionMemoryKind.GatheringCompleted => SoulmatesText.Get("Memories.Chronicle.GatheringCompleted", Amount),
		CompanionMemoryKind.GuardianVictory => SoulmatesText.Get("Memories.Chronicle.GuardianVictory", Detail),
		CompanionMemoryKind.HealerAid => SoulmatesText.Get("Memories.Chronicle.HealerAid", Amount),
		CompanionMemoryKind.TrinketEquipped => SoulmatesText.Get("Memories.Chronicle.TrinketEquipped",
			Enum.TryParse(Detail, out CompanionTrinket trinket) ? SoulmatesText.EnumName(trinket) : Detail),
		CompanionMemoryKind.TrinketRemoved => SoulmatesText.Get("Memories.Chronicle.TrinketRemoved"),
		CompanionMemoryKind.BondMilestone => SoulmatesText.Get("Memories.Chronicle.BondMilestone",
			Enum.TryParse(Detail, out BondRank rank) ? SoulmatesText.EnumName(rank) : Detail),
		CompanionMemoryKind.CreatureEncounter => SoulmatesText.Get("Memories.Chronicle.CreatureEncounter", Detail),
		CompanionMemoryKind.ExperienceMilestone => SoulmatesText.Get("Memories.Chronicle.ExperienceMilestone", Amount),
		CompanionMemoryKind.SharedMoment => SoulmatesText.Get("Memories.Chronicle.SharedMoment",
			Enum.TryParse(Detail, out CompanionEmote emote) ? SoulmatesText.EnumName(emote) : Detail),
		CompanionMemoryKind.LearnedPerk => SoulmatesText.Get("Memories.Chronicle.LearnedPerk",
			SoulmatesText.Get("Learning.Forester")),
		_ => SoulmatesText.Get("Memories.New")
	};

	private void Normalize()
	{
		if (!Enum.IsDefined(Kind))
			Kind = CompanionMemoryKind.Awakened;
		Amount = Math.Max(0, Amount);
		Detail = Detail?.Trim() ?? "";
		if (Detail.Length > 80)
			Detail = Detail[..80];
	}
}

public sealed class CompanionProfile
{
	public const int MaximumPackSlots = 12;
	public const int MaximumMemories = 8;
	public const int MaximumLevel = 20;
	public const int ForesterUnlockInsight = 24;
	public const int ItemCarryLimit = 99;
	public const int ForestrySupplyLimit = 12;

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
	public CompanionJob Routine { get; set; }
	public bool AutonomyEnabled { get; set; } = true;
	public int Bond { get; set; }
	public int Mood { get; set; } = 100;
	public int Energy { get; set; } = 100;
	public int JobsCompleted { get; set; }
	public int Experience { get; set; }
	public int DefeatedEnemies { get; set; }
	public int Interactions { get; set; }
	public int GatheringInsight { get; set; }
	public int MiningInsight { get; set; }
	public int ForestryInsight { get; set; }
	public int CombatInsight { get; set; }
	public int ExplorationInsight { get; set; }
	public string LastMemory { get; set; } = SoulmatesText.Get("Memories.New");
	public List<Item> Pack { get; set; } = [];
	public List<CompanionMemory> Memories { get; set; } = [];

	public BondRank Rank => Bond switch {
		>= 85 => BondRank.Eternal,
		>= 65 => BondRank.Soulbound,
		>= 40 => BondRank.Trusted,
		>= 20 => BondRank.Kindred,
		_ => BondRank.Newbound
	};
	public int RankIndex => (int)Rank;
	public int Level {
		get {
			for (int level = MaximumLevel; level > 1; level--) {
				if (Experience >= ExperienceForLevel(level))
					return level;
			}
			return 1;
		}
	}
	public int ExperienceIntoLevel => Level >= MaximumLevel ? 0 : Experience - ExperienceForLevel(Level);
	public int ExperienceNeededForNextLevel => Level >= MaximumLevel
		? 0
		: ExperienceForLevel(Level + 1) - ExperienceForLevel(Level);
	public int PackCapacity => Trinket == CompanionTrinket.HearthRibbon
		? MaximumPackSlots
		: Math.Min(MaximumPackSlots, 8 + (Rank >= BondRank.Soulbound ? 1 : 0) + (Rank >= BondRank.Eternal ? 1 : 0));
	public int PackLoad => Pack.Count(item => !item.IsAir);
	public string LatestMemory => Memories.Count > 0 ? Memories[^1].Describe() : LastMemory;
	public bool IsAether => Name.Equals("AETHER", StringComparison.OrdinalIgnoreCase);
	public bool ForesterUnlocked => IsAether || Talent == CompanionTalent.Gatherer
		|| ForestryInsight >= ForesterUnlockInsight;
	public LearnedBehavior DominantLearnedBehavior {
		get {
			LearnedBehavior result = LearnedBehavior.Gathering;
			int best = GatheringInsight;
			if (MiningInsight > best) {
				result = LearnedBehavior.Mining;
				best = MiningInsight;
			}
			if (ForestryInsight > best) {
				result = LearnedBehavior.Forestry;
				best = ForestryInsight;
			}
			if (CombatInsight > best) {
				result = LearnedBehavior.Combat;
				best = CombatInsight;
			}
			if (ExplorationInsight > best)
				result = LearnedBehavior.Exploration;
			return result;
		}
	}
	public int DominantInsight => GetInsight(DominantLearnedBehavior);

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
		Routine = Routine,
		AutonomyEnabled = AutonomyEnabled,
		Bond = Bond,
		Mood = Mood,
		Energy = Energy,
		JobsCompleted = JobsCompleted,
		Experience = Experience,
		DefeatedEnemies = DefeatedEnemies,
		Interactions = Interactions,
		GatheringInsight = GatheringInsight,
		MiningInsight = MiningInsight,
		ForestryInsight = ForestryInsight,
		CombatInsight = CombatInsight,
		ExplorationInsight = ExplorationInsight,
		LastMemory = LastMemory,
		Pack = Pack.Where(item => !item.IsAir).Select(item => item.Clone()).ToList(),
		Memories = Memories.Select(memory => memory.Clone()).ToList()
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
		["routine"] = (byte)Routine,
		["autonomyEnabled"] = AutonomyEnabled,
		["bond"] = Bond,
		["mood"] = Mood,
		["energy"] = Energy,
		["jobsCompleted"] = JobsCompleted,
		["experience"] = Experience,
		["defeatedEnemies"] = DefeatedEnemies,
		["interactions"] = Interactions,
		["gatheringInsight"] = GatheringInsight,
		["miningInsight"] = MiningInsight,
		["forestryInsight"] = ForestryInsight,
		["combatInsight"] = CombatInsight,
		["explorationInsight"] = ExplorationInsight,
		["lastMemory"] = LastMemory,
		["pack"] = Pack.Where(item => !item.IsAir).Select(ItemIO.Save).ToList(),
		["memories"] = Memories.Select(memory => memory.Save()).ToList()
	};

	public static CompanionProfile Load(TagCompound tag)
	{
		var profile = new CompanionProfile {
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
			Routine = tag.ContainsKey("routine") ? (CompanionJob)tag.GetByte("routine") : CompanionJob.None,
			AutonomyEnabled = !tag.ContainsKey("autonomyEnabled") || tag.GetBool("autonomyEnabled"),
			Bond = tag.GetInt("bond"),
			Mood = tag.ContainsKey("mood") ? tag.GetInt("mood") : 100,
			Energy = tag.ContainsKey("energy") ? tag.GetInt("energy") : 100,
			JobsCompleted = tag.ContainsKey("jobsCompleted") ? tag.GetInt("jobsCompleted") : 0,
			Experience = tag.ContainsKey("experience") ? tag.GetInt("experience") : 0,
			DefeatedEnemies = tag.ContainsKey("defeatedEnemies") ? tag.GetInt("defeatedEnemies") : 0,
			Interactions = tag.ContainsKey("interactions") ? tag.GetInt("interactions") : 0,
			GatheringInsight = tag.ContainsKey("gatheringInsight") ? tag.GetInt("gatheringInsight") : 0,
			MiningInsight = tag.ContainsKey("miningInsight") ? tag.GetInt("miningInsight") : 0,
			ForestryInsight = tag.ContainsKey("forestryInsight") ? tag.GetInt("forestryInsight") : 0,
			CombatInsight = tag.ContainsKey("combatInsight") ? tag.GetInt("combatInsight") : 0,
			ExplorationInsight = tag.ContainsKey("explorationInsight") ? tag.GetInt("explorationInsight") : 0,
			LastMemory = tag.ContainsKey("lastMemory") && tag.GetString("lastMemory") is { Length: > 0 } memory
				? memory
				: SoulmatesText.Get("Memories.New"),
			Pack = tag.ContainsKey("pack") ? tag.GetList<TagCompound>("pack").Select(ItemIO.Load).Where(item => !item.IsAir).ToList() : [],
			Memories = tag.ContainsKey("memories")
				? tag.GetList<TagCompound>("memories").Select(CompanionMemory.Load).ToList()
				: []
		};
		profile.Normalize();
		return profile;
	}

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
		writer.Write((byte)Routine);
		writer.Write(AutonomyEnabled);
		writer.Write(Bond);
		writer.Write(Mood);
		writer.Write(Energy);
		writer.Write(JobsCompleted);
		writer.Write(Experience);
		writer.Write(DefeatedEnemies);
		writer.Write(Interactions);
		writer.Write((byte)GatheringInsight);
		writer.Write((byte)MiningInsight);
		writer.Write((byte)ForestryInsight);
		writer.Write((byte)CombatInsight);
		writer.Write((byte)ExplorationInsight);
		writer.Write(LastMemory);
		Item[] items = Pack.Where(item => !item.IsAir).Take(MaximumPackSlots).ToArray();
		writer.Write((byte)items.Length);
		foreach (Item item in items)
			ItemIO.Send(item, writer, writeStack: true, writeFavorite: false);
		CompanionMemory[] memories = Memories.TakeLast(MaximumMemories).ToArray();
		writer.Write((byte)memories.Length);
		foreach (CompanionMemory memory in memories)
			memory.Write(writer);
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
			Routine = (CompanionJob)reader.ReadByte(),
			AutonomyEnabled = reader.ReadBoolean(),
			Bond = reader.ReadInt32(),
			Mood = reader.ReadInt32(),
			Energy = reader.ReadInt32(),
			JobsCompleted = reader.ReadInt32(),
			Experience = reader.ReadInt32(),
			DefeatedEnemies = reader.ReadInt32(),
			Interactions = reader.ReadInt32(),
			GatheringInsight = reader.ReadByte(),
			MiningInsight = reader.ReadByte(),
			ForestryInsight = reader.ReadByte(),
			CombatInsight = reader.ReadByte(),
			ExplorationInsight = reader.ReadByte(),
			LastMemory = reader.ReadString()
		};
		int count = reader.ReadByte();
		for (int i = 0; i < count; i++)
			profile.Pack.Add(ItemIO.Receive(reader, readStack: true, readFavorite: false));
		int memoryCount = reader.ReadByte();
		for (int i = 0; i < memoryCount; i++)
			profile.Memories.Add(CompanionMemory.Read(reader));
		profile.Normalize();
		return profile;
	}

	public void ChangeBond(int amount)
	{
		BondRank previous = Rank;
		Bond = Math.Clamp(Bond + amount, 0, 100);
		if ((int)Rank > (int)previous)
			Remember(CompanionMemoryKind.BondMilestone, detail: Rank.ToString());
	}

	public bool GainExperience(int amount, out int newLevel)
	{
		int previousLevel = Level;
		Experience = Math.Clamp(Experience + Math.Max(0, amount), 0, ExperienceForLevel(MaximumLevel));
		newLevel = Level;
		if (newLevel > previousLevel)
			Remember(CompanionMemoryKind.ExperienceMilestone, newLevel);
		return newLevel > previousLevel;
	}

	public bool HasTalent(CompanionTalent talent) => IsAether || Talent == talent;

	public int GetInsight(LearnedBehavior behavior) => behavior switch {
		LearnedBehavior.Gathering => GatheringInsight,
		LearnedBehavior.Mining => MiningInsight,
		LearnedBehavior.Forestry => ForestryInsight,
		LearnedBehavior.Combat => CombatInsight,
		_ => ExplorationInsight
	};

	public bool Observe(LearnedBehavior behavior, int amount = 1)
	{
		int previousForestry = ForestryInsight;
		int value = Math.Clamp(GetInsight(behavior) + Math.Max(0, amount), 0, 100);
		switch (behavior) {
			case LearnedBehavior.Gathering:
				GatheringInsight = value;
				break;
			case LearnedBehavior.Mining:
				MiningInsight = value;
				break;
			case LearnedBehavior.Forestry:
				ForestryInsight = value;
				break;
			case LearnedBehavior.Combat:
				CombatInsight = value;
				break;
			case LearnedBehavior.Exploration:
				ExplorationInsight = value;
				break;
		}

		bool unlockedForester = !IsAether && previousForestry < ForesterUnlockInsight
			&& ForestryInsight >= ForesterUnlockInsight;
		if (unlockedForester)
			Remember(CompanionMemoryKind.LearnedPerk, detail: "Forester");
		return unlockedForester;
	}

	public static int ExperienceForLevel(int level)
	{
		int step = Math.Clamp(level, 1, MaximumLevel) - 1;
		return step * step * 12;
	}

	public void Remember(CompanionMemoryKind kind, int amount = 0, string detail = "")
	{
		var memory = new CompanionMemory { Kind = kind, Amount = amount, Detail = detail };
		Memories.Add(memory);
		if (Memories.Count > MaximumMemories)
			Memories.RemoveRange(0, Memories.Count - MaximumMemories);
		LastMemory = memory.Describe();
	}

	public string RecallMemory(int fromNewest)
	{
		if (Memories.Count == 0)
			return LastMemory;
		int index = Memories.Count - 1 - Math.Abs(fromNewest % Memories.Count);
		return Memories[index].Describe();
	}

	public int Store(Item source)
	{
		NormalizePack();
		int permitted = GetStorableAmount(source);
		if (permitted <= 0)
			return 0;

		Item transfer = source.Clone();
		transfer.stack = permitted;
		foreach (Item stored in Pack) {
			if (stored.type != transfer.type || stored.stack >= stored.maxStack
				|| !ItemLoader.CanStack(stored, transfer))
				continue;
			ItemLoader.TryStackItems(stored, transfer, out _, infiniteSource: false);
			if (transfer.IsAir || transfer.stack <= 0)
				break;
		}

		while (!transfer.IsAir && transfer.stack > 0 && PackLoad < PackCapacity) {
			Item stored = transfer.Clone();
			stored.stack = Math.Min(transfer.stack, transfer.maxStack);
			stored.favorited = false;
			Pack.Add(stored);
			transfer.stack -= stored.stack;
			if (transfer.stack <= 0)
				transfer.TurnToAir();
		}

		int moved = permitted - (transfer.IsAir ? 0 : transfer.stack);
		source.stack -= moved;
		if (source.stack <= 0)
			source.TurnToAir();
		return moved;
	}

	public int GetStorableAmount(Item source)
	{
		if (source.IsAir || source.stack <= 0)
			return 0;

		int permitted = Math.Min(source.stack,
			Math.Max(0, CarryLimitFor(source.type) - ItemCount(source.type)));
		if (permitted <= 0)
			return 0;

		int occupiedSlots = 0;
		int available = 0;
		foreach (Item stored in Pack) {
			if (stored is null || stored.IsAir)
				continue;
			occupiedSlots++;
			if (stored.type == source.type && stored.stack < stored.maxStack
				&& ItemLoader.CanStack(stored, source)) {
				available += stored.maxStack - stored.stack;
				if (available >= permitted)
					return permitted;
			}
		}

		int freeSlots = Math.Max(0, PackCapacity - occupiedSlots);
		long totalCapacity = (long)available + (long)freeSlots * Math.Max(1, source.maxStack);
		return (int)Math.Min(permitted, Math.Min(int.MaxValue, totalCapacity));
	}

	public bool CanStore(Item source) => GetStorableAmount(source) > 0;

	public static int CarryLimitFor(int itemType)
		=> itemType == ItemID.Acorn ? ForestrySupplyLimit : ItemCarryLimit;

	public int ItemCount(int itemType)
	{
		long total = 0;
		foreach (Item item in Pack) {
			if (item is not null && !item.IsAir && item.type == itemType)
				total += item.stack;
		}
		return (int)Math.Min(int.MaxValue, total);
	}

	public List<Item> ExtractExcess(int itemType, int maximum)
	{
		NormalizePack();
		int remaining = Math.Max(0, maximum);
		var excess = new List<Item>();
		for (int i = 0; i < Pack.Count; i++) {
			Item item = Pack[i];
			if (item is null || item.IsAir || item.type != itemType)
				continue;
			int keep = Math.Min(item.stack, remaining);
			int removed = item.stack - keep;
			if (removed > 0) {
				Item returned = item.Clone();
				returned.stack = removed;
				returned.favorited = false;
				excess.Add(returned);
			}
			item.stack = keep;
			remaining -= keep;
		}
		NormalizePack();
		return excess;
	}

	public void Normalize()
	{
		if (Id == Guid.Empty)
			Id = Guid.NewGuid();
		Name = string.IsNullOrWhiteSpace(Name) ? "Luma" : Name.Trim();
		if (Name.Length > 24)
			Name = Name[..24];
		Personality = ValidEnum(Personality, CompanionPersonality.Curious);
		Talent = ValidEnum(Talent, CompanionTalent.TreasureSeeker);
		Essence = ValidEnum(Essence, CompanionEssence.Starlight);
		Form = ValidEnum(Form, CompanionForm.Balanced);
		Aura = ValidEnum(Aura, CompanionAura.SoftGlow);
		Muse = ValidEnum(Muse, CompanionMuse.Soulkin);
		Voice = ValidEnum(Voice, CompanionVoice.Soft);
		Trinket = ValidEnum(Trinket, CompanionTrinket.None);
		Routine = ValidEnum(Routine, CompanionJob.None);
		Bond = Math.Clamp(Bond, 0, 100);
		Mood = Math.Clamp(Mood, 0, 100);
		Energy = Math.Clamp(Energy, 0, 100);
		JobsCompleted = Math.Max(0, JobsCompleted);
		Experience = Math.Clamp(Experience, 0, ExperienceForLevel(MaximumLevel));
		DefeatedEnemies = Math.Max(0, DefeatedEnemies);
		Interactions = Math.Max(0, Interactions);
		GatheringInsight = Math.Clamp(GatheringInsight, 0, 100);
		MiningInsight = Math.Clamp(MiningInsight, 0, 100);
		ForestryInsight = Math.Clamp(ForestryInsight, 0, 100);
		CombatInsight = Math.Clamp(CombatInsight, 0, 100);
		ExplorationInsight = Math.Clamp(ExplorationInsight, 0, 100);
		LastMemory = string.IsNullOrWhiteSpace(LastMemory) ? SoulmatesText.Get("Memories.New") : LastMemory.Trim();
		if (LastMemory.Length > 240)
			LastMemory = LastMemory[..240];
		NormalizePack();
		Memories = Memories.Where(memory => memory is not null).TakeLast(MaximumMemories).ToList();
	}

	private void NormalizePack()
	{
		Pack = Pack.Where(item => item is not null && !item.IsAir).Take(MaximumPackSlots).ToList();
		foreach (Item item in Pack)
			item.stack = Math.Clamp(item.stack, 1, Math.Max(1, item.maxStack));
	}

	private static T ValidEnum<T>(T value, T fallback) where T : struct, Enum => Enum.IsDefined(value) ? value : fallback;

	public string DescribePack()
	{
		if (PackLoad == 0)
			return SoulmatesText.Get("Pack.Empty");
		string contents = string.Join(", ", Pack.Where(item => !item.IsAir).Take(5).Select(item => $"{item.Name} x{item.stack}"));
		if (PackLoad > 5)
			contents += SoulmatesText.Get("Pack.AndMoreStacks", PackLoad - 5);
		return SoulmatesText.Get("Pack.Description", contents, PackLoad, PackCapacity);
	}
}
