#nullable enable
using System;
using System.IO;
using System.Linq;
using Terraria.ModLoader.IO;

namespace Soulmates.Common;

// Separate from initiative/work enums: these observations grant no task permissions.
public enum CompanionPersonalPlanKind : byte { None, HomeVisit, PetCompany, SupplyReflection }

public sealed record CompanionPersonalPlanMemory(CompanionPersonalPlanKind Kind, string Subject);

// One Sigil's bounded preferences and last completed undertaking; never an active job.
public sealed class CompanionPersonalPlans
{
	public const int MaximumDurationTicks = 900;
	public const int MinimumDurationTicks = 300;
	public const int CompletionCooldownTicks = 18000;
	public const int MaximumCooldownTicks = CompletionCooldownTicks * 3;
	public const int InterruptedCooldownTicks = 1800;
	private const int KindCount = 3;
	private const int MaximumSubjectLength = 80;
	private const byte WireVersion = 1;
	private readonly sbyte[] preferences = new sbyte[KindCount];
	private readonly int[] cooldowns = new int[KindCount];
	public CompanionPersonalPlanMemory? LastCompleted { get; private set; }

	public static bool Valid(CompanionPersonalPlanKind kind) => kind >= CompanionPersonalPlanKind.HomeVisit
		&& kind <= CompanionPersonalPlanKind.SupplyReflection;
	public int Preference(CompanionPersonalPlanKind kind) => Valid(kind) ? preferences[(int)kind - 1] : 0;
	public int Cooldown(CompanionPersonalPlanKind kind) => Valid(kind) ? cooldowns[(int)kind - 1] : 0;
	public bool IsReady(CompanionPersonalPlanKind kind) => Valid(kind) && Cooldown(kind) == 0;
	public void Tick()
	{
		for (int i = 0; i < KindCount; i++) if (cooldowns[i] > 0) cooldowns[i]--;
	}
	public void Defer(CompanionPersonalPlanKind kind, int ticks)
	{
		if (Valid(kind)) cooldowns[(int)kind - 1] = Math.Max(Cooldown(kind), Math.Clamp(ticks, 0, MaximumCooldownTicks));
	}
	public void Completed(CompanionPersonalPlanKind kind, string subject)
	{
		if (!Valid(kind)) return;
		LastCompleted = new(kind, NormalizeSubject(subject));
		Defer(kind, PreferredCooldown(kind));
	}
	public void Evaluate(CompanionPersonalPlanKind kind, CompanionAnswer answer)
	{
		if (!Valid(kind) || !Enum.IsDefined(answer) || answer == CompanionAnswer.Later) return;
		preferences[(int)kind - 1] = (sbyte)Math.Clamp(Preference(kind)
			+ (answer == CompanionAnswer.First ? 8 : answer == CompanionAnswer.Second ? -4 : -8), -32, 32);
		if (answer != CompanionAnswer.First) Defer(kind, PreferredCooldown(kind));
	}
	private int PreferredCooldown(CompanionPersonalPlanKind kind) => CompletionCooldownTicks + Math.Max(0, -Preference(kind)) * 900;
	public void Normalize()
	{
		for (int i = 0; i < KindCount; i++) {
			preferences[i] = (sbyte)Math.Clamp((int)preferences[i], -32, 32);
			cooldowns[i] = Math.Clamp(cooldowns[i], 0, MaximumCooldownTicks);
		}
		LastCompleted = LastCompleted is { } memory && Valid(memory.Kind)
			? memory with { Subject = NormalizeSubject(memory.Subject) } : null;
	}
	public CompanionPersonalPlans Clone()
	{
		var clone = new CompanionPersonalPlans { LastCompleted = LastCompleted };
		Array.Copy(preferences, clone.preferences, KindCount);
		Array.Copy(cooldowns, clone.cooldowns, KindCount);
		return clone;
	}
	public TagCompound Save()
	{
		var tag = new TagCompound();
		for (int i = 0; i < KindCount; i++) {
			tag[$"preference{i}"] = (int)preferences[i];
			tag[$"cooldown{i}"] = cooldowns[i];
		}
		if (LastCompleted is { } memory) {
			tag["lastKind"] = (byte)memory.Kind;
			tag["lastSubject"] = memory.Subject;
		}
		return tag;
	}
	public static CompanionPersonalPlans Load(TagCompound tag)
	{
		var result = new CompanionPersonalPlans();
		for (int i = 0; i < KindCount; i++) {
			result.preferences[i] = (sbyte)Math.Clamp(tag.GetInt($"preference{i}"), -32, 32);
			result.cooldowns[i] = Math.Clamp(tag.GetInt($"cooldown{i}"), 0, MaximumCooldownTicks);
		}
		var kind = (CompanionPersonalPlanKind)tag.GetByte("lastKind");
		if (Valid(kind)) result.LastCompleted = new(kind, NormalizeSubject(tag.GetString("lastSubject")));
		return result;
	}
	public void Write(BinaryWriter writer)
	{
		writer.Write(WireVersion);
		for (int i = 0; i < KindCount; i++) { writer.Write(preferences[i]); writer.Write(cooldowns[i]); }
		writer.Write((byte)(LastCompleted?.Kind ?? CompanionPersonalPlanKind.None));
		if (LastCompleted is { } memory) writer.Write(memory.Subject);
	}
	public static CompanionPersonalPlans Read(BinaryReader reader)
	{
		if (reader.ReadByte() != WireVersion) throw new InvalidDataException("Unsupported personal plan data.");
		var result = new CompanionPersonalPlans();
		for (int i = 0; i < KindCount; i++) {
			sbyte preference = reader.ReadSByte(); int cooldown = reader.ReadInt32();
			if (preference is < -32 or > 32 || cooldown is < 0 or > MaximumCooldownTicks)
				throw new InvalidDataException("Invalid personal plan estimate.");
			result.preferences[i] = preference; result.cooldowns[i] = cooldown;
		}
		var kind = (CompanionPersonalPlanKind)reader.ReadByte();
		if (kind == CompanionPersonalPlanKind.None) return result;
		if (!Valid(kind)) throw new InvalidDataException("Invalid completed personal plan.");
		string subject = reader.ReadString();
		if (subject.Length > MaximumSubjectLength || subject != NormalizeSubject(subject))
			throw new InvalidDataException("Invalid personal plan subject.");
		result.LastCompleted = new(kind, subject);
		return result;
	}
	internal static string NormalizeSubject(string? subject)
	{
		string value = string.Concat((subject?.Trim() ?? "").Where(c => !char.IsControl(c)));
		return value.Length > MaximumSubjectLength ? value[..MaximumSubjectLength] : value;
	}
}

public sealed partial class CompanionProfile
{
	public CompanionPersonalPlans PersonalPlans { get; private set; } = new();
	public string LastPersonalPlanMemory => PersonalPlans.LastCompleted is { } memory
		? SoulmatesText.Get($"PersonalPlans.Memory.{memory.Kind}", memory.Subject) : "";

	// CompanionProfile's owner must explicitly connect these alongside its other save/wire hooks.
	private void SavePersonalPlans(TagCompound tag) => tag["personalPlans"] = PersonalPlans.Save();
	private void LoadPersonalPlans(TagCompound tag) => PersonalPlans = tag.ContainsKey("personalPlans")
		? CompanionPersonalPlans.Load(tag.GetCompound("personalPlans")) : new();
	private void ClonePersonalPlansTo(CompanionProfile clone) => clone.PersonalPlans = PersonalPlans.Clone();
	private void WritePersonalPlans(BinaryWriter writer) => PersonalPlans.Write(writer);
	private void ReadPersonalPlans(BinaryReader reader) => PersonalPlans = CompanionPersonalPlans.Read(reader);
	private void NormalizePersonalPlans() => PersonalPlans.Normalize();
}
