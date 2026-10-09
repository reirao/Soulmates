#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria.ModLoader.IO;

namespace Soulmates.Common;

public enum CompanionIntentContext : byte { General, Building, Mining, Forestry, Exploring, Social, Underground }

public readonly record struct CompanionIntent(CompanionInitiativeKind Kind, CompanionIntentContext Context);

// A bounded contextual preference, not a permission or a replacement task executor.
public sealed class CompanionIntentLearning
{
	private static readonly int KindCount = Enum.GetValues<CompanionInitiativeKind>().Length;
	private static readonly int ContextCount = Enum.GetValues<CompanionIntentContext>().Length;
	private const int MaximumSupplies = 12;
	private readonly byte[] attempts = new byte[KindCount * ContextCount];
	private readonly sbyte[] outcomes = new sbyte[KindCount * ContextCount];
	private readonly sbyte[] feedback = new sbyte[KindCount * ContextCount];
	private readonly byte[] requests = new byte[KindCount * ContextCount];
	private readonly List<string> buildingSupplies = [];

	public IReadOnlyList<string> BuildingSupplies => buildingSupplies.AsReadOnly();
	public int Attempts(CompanionIntent intent) => Valid(intent) ? attempts[Index(intent)] : 0;
	public int Feedback(CompanionIntent intent) => Valid(intent) ? feedback[Index(intent)] : 0;
	public int Score(CompanionIntent intent)
	{
		if (!Valid(intent)) return 0;
		int i = Index(intent);
		return Math.Clamp(feedback[i] + outcomes[i] + requests[i] + 6 / (1 + attempts[i]), -32, 40);
	}
	public void Requested(CompanionIntent intent)
	{
		if (Valid(intent)) requests[Index(intent)] = (byte)Math.Min(8, requests[Index(intent)] + 1);
	}
	public void Observed(CompanionIntent intent)
	{
		if (Valid(intent) && requests[Index(intent)] < 4) requests[Index(intent)]++;
	}
	public void Completed(CompanionIntent intent, bool successful)
	{
		if (!Valid(intent)) return;
		int i = Index(intent);
		attempts[i] = (byte)Math.Min(32, attempts[i] + 1);
		outcomes[i] = (sbyte)Math.Clamp(outcomes[i] + (successful ? 1 : -2), -8, 8);
	}
	public void Evaluate(CompanionIntent intent, CompanionAnswer answer)
	{
		if (!Valid(intent) || !Enum.IsDefined(answer) || answer == CompanionAnswer.Later) return;
		int i = Index(intent);
		feedback[i] = (sbyte)Math.Clamp(feedback[i] + (answer == CompanionAnswer.First ? 8
			: answer == CompanionAnswer.Second ? -4 : -8), -32, 32);
	}
	public void ObserveBuildingSupply(string key)
	{
		if (key is null || !CompanionMiningRules.ValidKey(key)) return;
		buildingSupplies.Remove(key);
		buildingSupplies.Add(key);
		if (buildingSupplies.Count > MaximumSupplies) buildingSupplies.RemoveAt(0);
	}
	public bool RecognizesSupply(string key) => buildingSupplies.Contains(key);
	public CompanionIntentLearning Clone()
	{
		var clone = new CompanionIntentLearning();
		Array.Copy(attempts, clone.attempts, attempts.Length);
		Array.Copy(outcomes, clone.outcomes, outcomes.Length);
		Array.Copy(feedback, clone.feedback, feedback.Length);
		Array.Copy(requests, clone.requests, requests.Length);
		clone.buildingSupplies.AddRange(buildingSupplies);
		return clone;
	}
	public TagCompound Save()
	{
		var entries = new List<TagCompound>();
		for (int i = 0; i < attempts.Length; i++) {
			if (attempts[i] == 0 && feedback[i] == 0 && requests[i] == 0) continue;
			entries.Add(new TagCompound { ["kind"] = (byte)(i / ContextCount), ["context"] = (byte)(i % ContextCount),
				["attempts"] = (int)attempts[i], ["outcome"] = (int)outcomes[i],
				["feedback"] = (int)feedback[i], ["requests"] = (int)requests[i] });
		}
		return new TagCompound { ["entries"] = entries, ["supplies"] = buildingSupplies.ToList() };
	}
	public static CompanionIntentLearning Load(TagCompound tag)
	{
		var result = new CompanionIntentLearning();
		foreach (TagCompound entry in tag.GetList<TagCompound>("entries").Take(KindCount * ContextCount)) {
			var intent = new CompanionIntent((CompanionInitiativeKind)entry.GetByte("kind"),
				(CompanionIntentContext)entry.GetByte("context"));
			if (!Valid(intent)) continue;
			int i = Index(intent);
			result.attempts[i] = (byte)Math.Clamp(entry.GetInt("attempts"), 0, 32);
			result.outcomes[i] = (sbyte)Math.Clamp(entry.GetInt("outcome"), -8, 8);
			result.feedback[i] = (sbyte)Math.Clamp(entry.GetInt("feedback"), -32, 32);
			result.requests[i] = (byte)Math.Clamp(entry.GetInt("requests"), 0, 8);
		}
		foreach (string key in tag.GetList<string>("supplies").TakeLast(MaximumSupplies)) result.ObserveBuildingSupply(key);
		return result;
	}
	public void Write(BinaryWriter writer)
	{
		for (int i = 0; i < attempts.Length; i++) {
			writer.Write(attempts[i]); writer.Write(outcomes[i]); writer.Write(feedback[i]); writer.Write(requests[i]);
		}
		writer.Write((byte)buildingSupplies.Count);
		foreach (string key in buildingSupplies) writer.Write(key);
	}
	public static CompanionIntentLearning Read(BinaryReader reader)
	{
		var result = new CompanionIntentLearning();
		for (int i = 0; i < result.attempts.Length; i++) {
			byte attempt = reader.ReadByte(); sbyte outcome = reader.ReadSByte();
			sbyte reward = reader.ReadSByte(); byte request = reader.ReadByte();
			if (attempt > 32 || outcome is < -8 or > 8 || reward is < -32 or > 32 || request > 8)
				throw new InvalidDataException("Invalid companion learning estimate.");
			result.attempts[i] = attempt; result.outcomes[i] = outcome;
			result.feedback[i] = reward; result.requests[i] = request;
		}
		int count = reader.ReadByte();
		if (count > MaximumSupplies) throw new InvalidDataException("Too many observed building supplies.");
		for (int i = 0; i < count; i++) {
			string key = reader.ReadString();
			if (!CompanionMiningRules.ValidKey(key)) throw new InvalidDataException("Invalid observed supply.");
			result.ObserveBuildingSupply(key);
		}
		return result;
	}
	private static int Index(CompanionIntent intent) => (int)intent.Kind * ContextCount + (int)intent.Context;
	public static bool Valid(CompanionIntent intent) => (uint)intent.Kind < KindCount && (uint)intent.Context < ContextCount;
}
