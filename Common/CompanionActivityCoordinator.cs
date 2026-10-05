using System;
using System.Collections.Generic;
using System.Linq;

namespace Soulmates.Common;

public enum CompanionActivityLane : byte
{
	None, Defense, Paused, Assignment, Critters, AutomaticWork, Residents, Stay, AwaitingReply, Follow
}

public interface ICompanionActivity
{
	CompanionActivityLane Lane { get; }
	bool TryTick();
}

public sealed class CompanionActivityModule : ICompanionActivity
{
	private readonly Func<bool> tick;
	public CompanionActivityLane Lane { get; }
	public CompanionActivityModule(CompanionActivityLane lane, Func<bool> tick)
	{
		ArgumentNullException.ThrowIfNull(tick);
		Lane = lane;
		this.tick = tick;
	}
	public bool TryTick() => tick();
}

// Preemption keeps module state intact. Explicit commands own cancellation and reset.
public sealed class CompanionActivityCoordinator
{
	private readonly ICompanionActivity[] modules;
	public CompanionActivityLane Current { get; private set; }
	public CompanionActivityCoordinator(IEnumerable<ICompanionActivity> activities)
	{
		ArgumentNullException.ThrowIfNull(activities);
		modules = activities.ToArray();
		if (modules.Any(module => module is null || module.Lane == CompanionActivityLane.None || !Enum.IsDefined(module.Lane))
			|| modules.Select(module => module.Lane).Distinct().Count() != modules.Length)
			throw new ArgumentException("Activity lanes must be unique and registered.", nameof(activities));
		Array.Sort(modules, (left, right) => left.Lane.CompareTo(right.Lane));
	}
	public CompanionActivityLane Tick()
	{
		Current = CompanionActivityLane.None;
		foreach (ICompanionActivity module in modules) {
			if (!module.TryTick()) continue;
			return Current = module.Lane;
		}
		return Current = CompanionActivityLane.None;
	}
	public void Reset() => Current = CompanionActivityLane.None;
}
