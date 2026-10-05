using Soulmates.Common;

namespace Soulmates.Regression;

internal static class ActivityCoordinatorChecks
{
	public static void Run(Action<bool, string> check)
	{
		CompanionActivityLane[] lanes = Enum.GetValues<CompanionActivityLane>().Where(lane => lane != CompanionActivityLane.None).ToArray();
		for (int mask = 0; mask < 1 << lanes.Length; mask++) {
			var visited = new List<CompanionActivityLane>();
			var coordinator = new CompanionActivityCoordinator(lanes.Reverse().Select(lane =>
				new CompanionActivityModule(lane, () => { visited.Add(lane); return (mask & 1 << ((int)lane - 1)) != 0; })));
			CompanionActivityLane expected = lanes.FirstOrDefault(lane => (mask & 1 << ((int)lane - 1)) != 0);
			check(coordinator.Tick() == expected && coordinator.Current == expected, "Activity priority: " + mask);
			check(visited.SequenceEqual(expected == CompanionActivityLane.None ? lanes : lanes.Take((int)expected)),
				"Activity ran twice, out of order, or after another owned the frame: " + mask);
			coordinator.Reset();
			check(coordinator.Current == CompanionActivityLane.None, "Activity reset retained a stale lane");
		}

		bool defense = false, pause = false;
		int remaining = 5;
		var resumable = new CompanionActivityCoordinator([
			new CompanionActivityModule(CompanionActivityLane.Follow, () => true),
			new CompanionActivityModule(CompanionActivityLane.Assignment, () => { if (remaining == 0) return false; remaining--; return true; }),
			new CompanionActivityModule(CompanionActivityLane.Paused, () => pause),
			new CompanionActivityModule(CompanionActivityLane.Defense, () => defense)
		]);
		check(resumable.Tick() == CompanionActivityLane.Assignment && remaining == 4, "Assignment did not start");
		defense = pause = true;
		for (int i = 0; i < 600; i++)
			check(resumable.Tick() == CompanionActivityLane.Defense && remaining == 4, "Defense advanced a suspended assignment");
		defense = false;
		check(resumable.Tick() == CompanionActivityLane.Paused && remaining == 4, "Pause did not preserve assignment state");
		pause = false;
		check(resumable.Tick() == CompanionActivityLane.Assignment && remaining == 3, "Preempted assignment failed to resume");
		for (int i = 0; i < 3; i++) resumable.Tick();
		check(resumable.Tick() == CompanionActivityLane.Follow, "Finished assignment did not release the next frame");

		void Reject(Action action, string label) {
			try { action(); check(false, label); }
			catch (ArgumentException) { check(true, label); }
		}
		Reject(() => new CompanionActivityCoordinator(null!), "Null activity registration accepted");
		Reject(() => new CompanionActivityCoordinator([null!]), "Null activity accepted");
		Reject(() => new CompanionActivityModule(CompanionActivityLane.Follow, null!), "Null activity delegate accepted");
		Reject(() => new CompanionActivityCoordinator([new CompanionActivityModule(CompanionActivityLane.None, () => true)]), "None activity accepted");
		Reject(() => new CompanionActivityCoordinator([new CompanionActivityModule((CompanionActivityLane)255, () => true)]), "Unknown activity accepted");
		Reject(() => new CompanionActivityCoordinator([
			new CompanionActivityModule(CompanionActivityLane.Follow, () => true),
			new CompanionActivityModule(CompanionActivityLane.Follow, () => false)]), "Duplicate activity accepted");
		check(new CompanionActivityCoordinator([]).Tick() == CompanionActivityLane.None, "Empty coordinator fabricated activity");
		bool fail = false;
		var faulting = new CompanionActivityCoordinator([new CompanionActivityModule(CompanionActivityLane.Follow,
			() => fail ? throw new InvalidOperationException("test") : true)]);
		faulting.Tick(); fail = true;
		try { faulting.Tick(); check(false, "Activity exception swallowed"); }
		catch (InvalidOperationException) { check(faulting.Current == CompanionActivityLane.None, "Activity exception left stale diagnostics"); }
	}
}
