using System;
using System.Collections.Generic;

namespace Soulmates.Common;

public readonly record struct CompanionOpportunity(CompanionInitiativeKind Kind, int Relevance);

// Ages actionable opportunities, not unavailable tasks. Player rules filter offers before selection.
public sealed class CompanionAttention
{
	private static readonly int KindCount = Enum.GetValues<CompanionInitiativeKind>().Length;
	private readonly int[] waiting = new int[KindCount];
	private readonly int[] cooldowns = new int[KindCount];

	public void Tick()
	{
		for (int i = 0; i < cooldowns.Length; i++)
			if (cooldowns[i] > 0) cooldowns[i]--;
	}

	public bool IsReady(CompanionInitiativeKind kind) => Valid(kind) && cooldowns[(int)kind] == 0;

	public void Defer(CompanionInitiativeKind kind, int ticks)
	{
		if (!Valid(kind)) return;
		cooldowns[(int)kind] = Math.Max(cooldowns[(int)kind], Math.Max(0, ticks));
		waiting[(int)kind] = 0;
	}

	public void Reset()
	{
		Array.Clear(waiting);
		Array.Clear(cooldowns);
	}

	public CompanionInitiativeKind? Choose(IReadOnlyList<CompanionOpportunity> opportunities)
	{
		Span<int> relevance = stackalloc int[KindCount];
		relevance.Fill(-1);
		foreach (CompanionOpportunity opportunity in opportunities) {
			if (!IsReady(opportunity.Kind)) continue;
			int index = (int)opportunity.Kind;
			relevance[index] = Math.Max(relevance[index], Math.Clamp(opportunity.Relevance, 0, 100));
		}
		int chosen = -1;
		int best = -1;
		for (int i = 0; i < relevance.Length; i++) {
			if (relevance[i] < 0) { waiting[i] = 0; continue; }
			int score = relevance[i] + waiting[i] * 20;
			if (score > best) { chosen = i; best = score; }
		}
		if (chosen < 0) return null;
		for (int i = 0; i < waiting.Length; i++)
			if (relevance[i] >= 0) waiting[i] = i == chosen ? 0 : Math.Min(8, waiting[i] + 1);
		return (CompanionInitiativeKind)chosen;
	}

	private static bool Valid(CompanionInitiativeKind kind) => (uint)kind < KindCount;
}
