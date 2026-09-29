#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace Soulmates.Common.Feedback;

public sealed class SoulmatesFeedbackSystem : ModSystem
{
	private const int SnapshotIntervalTicks = 600;
	private const int FlushIntervalTicks = 300;
	private const long MaximumSessionBytes = 4L * 1024L * 1024L;
	private const string FolderName = "SoulmatesFeedback";
	private const string EnabledMarkerName = "enabled.txt";

	private static readonly JsonSerializerOptions CompactJson = new();
	private static readonly JsonSerializerOptions PrettyJson = new() { WriteIndented = true };
	private static readonly List<string> PendingLines = [];
	private static readonly Queue<string> RecentEvents = new();
	private static readonly Dictionary<string, long> Metrics = new(StringComparer.Ordinal);
	private static readonly Dictionary<string, object?> LatestSnapshot = new(StringComparer.Ordinal);

	private static DateTime sessionStartedUtc;
	private static string sessionId = "";
	private static string sessionPath = "";
	private static int snapshotTimer;
	private static int flushTimer;
	private static long sequence;
	private static bool sessionActive;
	private static bool rawLogCapped;
	private static string lastError = "";

	public static string FeedbackFolder => Path.Combine(Main.SavePath, FolderName);
	private static string EnabledMarkerPath => Path.Combine(FeedbackFolder, EnabledMarkerName);
	private static string SummaryPath => Path.Combine(FeedbackFolder, "latest-summary.json");
	public static bool Enabled => File.Exists(EnabledMarkerPath);
	public static bool SessionActive => sessionActive;
	public static string LastError => lastError;

	public override void OnWorldUnload() => EndSession("world_unload");
	public override void Unload() => EndSession("mod_unload");

	public static bool SetEnabled(bool enabled)
	{
		try {
			bool wasEnabled = Enabled;
			Directory.CreateDirectory(FeedbackFolder);
			if (enabled) {
				File.WriteAllText(EnabledMarkerPath,
					"Soulmates AETHER Field Notes are enabled.\n"
					+ "Files remain local and contain no player, character, world, account, chat, or exact position names.\n"
					+ "Use /soulfeedback off in game to stop future recording.\n", Encoding.UTF8);
				if (!wasEnabled && !sessionActive && !Main.gameMenu && Main.LocalPlayer.active)
					BeginSession(Main.LocalPlayer);
			}
			else {
				EndSession("disabled_by_player");
				if (File.Exists(EnabledMarkerPath))
					File.Delete(EnabledMarkerPath);
			}
			lastError = "";
			return true;
		}
		catch (Exception exception) {
			lastError = exception.Message;
			return false;
		}
	}

	public static void BeginSession(Player player)
	{
		if (!Enabled || Main.dedServ || player.whoAmI != Main.myPlayer)
			return;
		if (sessionActive)
			EndSession("session_replaced");

		try {
			Directory.CreateDirectory(FeedbackFolder);
			sessionStartedUtc = DateTime.UtcNow;
			sessionId = $"{sessionStartedUtc:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..30];
			sessionPath = Path.Combine(FeedbackFolder, $"session-{sessionId}.jsonl");
			snapshotTimer = SnapshotIntervalTicks;
			flushTimer = FlushIntervalTicks;
			sequence = 0;
			rawLogCapped = false;
			lastError = "";
			PendingLines.Clear();
			RecentEvents.Clear();
			Metrics.Clear();
			LatestSnapshot.Clear();
			sessionActive = true;
			Record("session_start",
				("mod_version", ModContent.GetInstance<global::Soulmates.Soulmates>().Version.ToString()),
				("language", Language.ActiveCulture.Name),
				("mode", GameModeName()));
			CaptureSnapshot(player, SoulboundCompanion.FindFor(player));
			Flush();
		}
		catch (Exception exception) {
			lastError = exception.Message;
			sessionActive = false;
		}
	}

	public static void Tick(Player player, SoulboundCompanion? companion)
	{
		if (!sessionActive || player.whoAmI != Main.myPlayer)
			return;
		if (--snapshotTimer <= 0) {
			snapshotTimer = SnapshotIntervalTicks;
			CaptureSnapshot(player, companion);
		}
		if (--flushTimer <= 0) {
			flushTimer = FlushIntervalTicks;
			Flush();
		}
	}

	public static void Record(string eventName, params (string Key, object? Value)[] fields)
	{
		if (!sessionActive || string.IsNullOrWhiteSpace(eventName))
			return;

		var entry = new Dictionary<string, object?>(StringComparer.Ordinal) {
			["schema"] = 1,
			["sequence"] = ++sequence,
			["utc"] = DateTime.UtcNow.ToString("O"),
			["elapsed_seconds"] = Math.Max(0, (int)(DateTime.UtcNow - sessionStartedUtc).TotalSeconds),
			["event"] = eventName
		};
		foreach ((string key, object? value) in fields) {
			if (string.IsNullOrWhiteSpace(key) || value is null)
				continue;
			object safeValue = value is string text ? Sanitize(text) : value;
			entry[key] = safeValue;
			if (key is "action" or "behavior" or "job" or "response")
				Increment($"{eventName}.{safeValue}");
		}
		Increment(eventName);
		PendingLines.Add(JsonSerializer.Serialize(entry, CompactJson));
		RecentEvents.Enqueue(eventName);
		while (RecentEvents.Count > 16)
			RecentEvents.Dequeue();
		if (PendingLines.Count >= 64)
			Flush();
	}

	public static void RecordNote(string note)
	{
		string safeNote = Sanitize(note);
		if (!string.IsNullOrWhiteSpace(safeNote))
			Record("player_note", ("note", safeNote));
	}

	public static bool RecordBug(string note)
	{
		if (!sessionActive || string.IsNullOrWhiteSpace(note))
			return false;
		try {
			string safeNote = Sanitize(note);
			var report = new Dictionary<string, object?>(StringComparer.Ordinal) {
				["schema"] = 1,
				["utc"] = DateTime.UtcNow.ToString("O"),
				["session_id"] = sessionId,
				["description"] = safeNote,
				["recent_event_types"] = RecentEvents.ToArray(),
				["context"] = new Dictionary<string, object?>(LatestSnapshot)
			};
			Directory.CreateDirectory(FeedbackFolder);
			File.AppendAllText(Path.Combine(FeedbackFolder, "bug-inbox.jsonl"),
				JsonSerializer.Serialize(report, CompactJson) + Environment.NewLine, Encoding.UTF8);
			Record("bug_report", ("description", safeNote));
			Flush();
			return true;
		}
		catch (Exception exception) {
			lastError = exception.Message;
			return false;
		}
	}

	public static void Flush()
	{
		if (!sessionActive)
			return;
		try {
			Directory.CreateDirectory(FeedbackFolder);
			if (!rawLogCapped && PendingLines.Count > 0) {
				long currentLength = File.Exists(sessionPath) ? new FileInfo(sessionPath).Length : 0L;
				long pendingBytes = PendingLines.Sum(line => Encoding.UTF8.GetByteCount(line) + 2L);
				if (currentLength + pendingBytes <= MaximumSessionBytes)
					File.AppendAllLines(sessionPath, PendingLines, Encoding.UTF8);
				else
					rawLogCapped = true;
			}
			PendingLines.Clear();
			WriteSummary();
			lastError = "";
		}
		catch (Exception exception) {
			lastError = exception.Message;
		}
	}

	private static void EndSession(string reason)
	{
		if (!sessionActive)
			return;
		Record("session_end", ("reason", reason));
		Flush();
		sessionActive = false;
		PendingLines.Clear();
	}

	private static void CaptureSnapshot(Player player, SoulboundCompanion? companion)
	{
		string movement = player.itemAnimation > 0 ? "using_item"
			: player.velocity.LengthSquared() > 36f ? "moving_fast"
			: player.velocity.LengthSquared() > 1f ? "moving"
			: "still";
		string distance = companion is null ? "absent" : DistanceBand(player.Center, companion.NPC.Center);
		LatestSnapshot.Clear();
		LatestSnapshot["biome"] = BiomeName(player);
		LatestSnapshot["movement"] = movement;
		LatestSnapshot["life_percent"] = player.statLifeMax2 <= 0 ? 0 : player.statLife * 100 / player.statLifeMax2;
		LatestSnapshot["companion_distance"] = distance;
		LatestSnapshot["daytime"] = Main.dayTime;
		LatestSnapshot["raining"] = Main.raining;
		if (companion is not null) {
			LatestSnapshot["command"] = companion.FeedbackCommand;
			LatestSnapshot["activity"] = companion.FeedbackActivity;
			LatestSnapshot["energy"] = companion.Profile.Energy;
			LatestSnapshot["mood"] = companion.Profile.Mood;
			LatestSnapshot["bond"] = companion.Profile.Bond;
			LatestSnapshot["level"] = companion.Profile.Level;
			LatestSnapshot["pack_load"] = companion.Profile.PackLoad;
			LatestSnapshot["pack_capacity"] = companion.Profile.PackCapacity;
			LatestSnapshot["autonomy_enabled"] = companion.Profile.AutonomyEnabled;
		}

		Record("snapshot", LatestSnapshot.Select(pair => (pair.Key, pair.Value)).ToArray());
		Increment($"snapshot.movement.{movement}");
		Increment($"snapshot.biome.{BiomeName(player)}");
	}

	private static void WriteSummary()
	{
		var summary = new Dictionary<string, object?>(StringComparer.Ordinal) {
			["schema"] = 1,
			["purpose"] = "Local, opt-in AETHER Field Notes for improving Soulmates behavior.",
			["privacy"] = "No account, player, character, world, chat, or exact position names are recorded.",
			["session_id"] = sessionId,
			["session_file"] = Path.GetFileName(sessionPath),
			["started_utc"] = sessionStartedUtc.ToString("O"),
			["updated_utc"] = DateTime.UtcNow.ToString("O"),
			["duration_seconds"] = Math.Max(0, (int)(DateTime.UtcNow - sessionStartedUtc).TotalSeconds),
			["raw_log_capped"] = rawLogCapped,
			["events"] = Metrics.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value),
			["latest_snapshot"] = new Dictionary<string, object?>(LatestSnapshot),
			["behavior_signals"] = BuildBehaviorSignals()
		};
		string temporaryPath = SummaryPath + ".tmp";
		File.WriteAllText(temporaryPath, JsonSerializer.Serialize(summary, PrettyJson), Encoding.UTF8);
		File.Move(temporaryPath, SummaryPath, overwrite: true);
	}

	private static string[] BuildBehaviorSignals()
	{
		var signals = new List<string>();
		long yes = Metric("initiative_response.Yes") + Metric("initiative_response.Always");
		long no = Metric("initiative_response.No") + Metric("initiative_response.Never");
		if (yes + no >= 3)
			signals.Add(yes > no ? "Player usually welcomes companion initiative."
				: "Player usually prefers direct control over companion initiative.");

		(string Name, long Count)[] interests = [
			("gathering", Metric("behavior_observed.Gathering") + Metric("player_activity.Gathering") + Metric("player_pickup")),
			("mining", Metric("behavior_observed.Mining") + Metric("player_activity.Mining")),
			("forestry", Metric("behavior_observed.Forestry") + Metric("player_activity.Forestry")),
			("combat", Metric("behavior_observed.Combat") + Metric("player_activity.Combat")),
			("exploration", Metric("behavior_observed.Exploration") + Metric("player_activity.Exploration"))
		];
		(string Name, long Count) dominant = interests.OrderByDescending(entry => entry.Count).First();
		if (dominant.Count >= 3)
			signals.Add($"Most observed attention currently leans toward {dominant.Name}.");

		long moving = Metric("snapshot.movement.moving") + Metric("snapshot.movement.moving_fast");
		long still = Metric("snapshot.movement.still");
		if (moving + still >= 4)
			signals.Add(moving > still ? "Play rhythm is mobile; proactive short tasks may fit."
				: "Play rhythm is measured; nearby observations and patient company may fit.");
		if (signals.Count == 0)
			signals.Add("Not enough observations yet; keep this interpretation tentative.");
		return [.. signals];
	}

	private static string BiomeName(Player player)
	{
		if (player.ZoneDungeon) return "dungeon";
		if (player.ZoneUnderworldHeight) return "underworld";
		if (player.ZoneGlowshroom) return "glowing_mushroom";
		if (player.ZoneCorrupt) return "corruption";
		if (player.ZoneCrimson) return "crimson";
		if (player.ZoneHallow) return "hallow";
		if (player.ZoneJungle) return "jungle";
		if (player.ZoneSnow) return "snow";
		if (player.ZoneDesert) return "desert";
		if (player.ZoneBeach) return "beach";
		if (player.ZoneRockLayerHeight) return "caverns";
		if (player.ZoneDirtLayerHeight) return "underground";
		return "surface";
	}

	private static string DistanceBand(Microsoft.Xna.Framework.Vector2 first, Microsoft.Xna.Framework.Vector2 second)
	{
		float distanceSquared = Microsoft.Xna.Framework.Vector2.DistanceSquared(first, second);
		if (distanceSquared <= 8f * 16f * 8f * 16f) return "near";
		if (distanceSquared <= 24f * 16f * 24f * 16f) return "visible";
		if (distanceSquared <= 60f * 16f * 60f * 16f) return "far";
		return "very_far";
	}

	private static string GameModeName() => Main.netMode switch {
		NetmodeID.SinglePlayer => "single_player",
		NetmodeID.MultiplayerClient => "multiplayer_client",
		_ => "server"
	};

	private static string Sanitize(string value)
	{
		string result = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
		return result.Length <= 180 ? result : result[..180];
	}

	private static void Increment(string key)
	{
		Metrics.TryGetValue(key, out long count);
		Metrics[key] = count + 1;
	}

	private static long Metric(string key) => Metrics.TryGetValue(key, out long count) ? count : 0;
}
