using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hjson;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Common.UI;
using Game = Terraria.Main;

string root = Path.GetFullPath(args.FirstOrDefault() ?? Path.Combine(AppContext.BaseDirectory, "../../../.."));
var errors = new List<string>();
int assertions = 0;
void Check(bool success, string description)
{
	assertions++;
	if (!success) errors.Add(description);
}

Dictionary<string, string> Catalog(string culture)
{
	var result = new Dictionary<string, string>();
	void Flatten(JsonValue value, string path)
	{
		if (value is JsonObject map) {
			foreach (var entry in map)
				Flatten(entry.Value, path.Length == 0 ? entry.Key : path + "." + entry.Key);
		}
		else
			result.Add(path, (string)value);
	}
	string path = Path.Combine(root, "Localization", culture + ".hjson");
	try { Flatten(HjsonValue.Load(path), ""); }
	catch (Exception exception) {
		Console.Error.WriteLine($"Cannot read localization catalog '{path}': {exception.Message}");
		Environment.Exit(1);
	}
	return result;
}

Soulmates.Regression.ActivityCoordinatorChecks.Run(Check);

var english = Catalog("en-US");
string[] cultures = { "en-US", "de-DE", "it-IT", "fr-FR", "es-ES", "ru-RU", "pt-BR", "pl-PL", "zh-Hans" };
if (args.Contains("--catalog")) {
	foreach (var (key, value) in english) Console.WriteLine(JsonSerializer.Serialize(new[] { key["Mods.Soulmates.".Length..], value }, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
	return;
}

string translationSource = Path.Combine(root, "tests", "localization", "translations.json");
var translations = JsonSerializer.Deserialize<Dictionary<string, string[]>>(File.ReadAllText(translationSource))!;
var addedCultures = cultures.Skip(2).ToArray();
foreach (var (key, values) in translations) {
	Check(english.ContainsKey("Mods.Soulmates." + key), "Unknown translation source key: " + key);
	Check(values.Length == addedCultures.Length, "Translation language count: " + key);
}
foreach (string key in english.Keys)
	Check(translations.ContainsKey(key["Mods.Soulmates.".Length..]), "Translation source key missing: " + key);
if (args.Contains("--generate-locales")) {
	if (errors.Count != 0) throw new InvalidDataException(string.Join("\n", errors));
	for (int i = 0; i < addedCultures.Length; i++) {
		var values = translations.ToDictionary(pair => pair.Key, pair => pair.Value[i]);
		var document = new Dictionary<string, object> { ["Mods"] = new Dictionary<string, object> { ["Soulmates"] = values } };
		File.WriteAllText(Path.Combine(root, "Localization", addedCultures[i] + ".hjson"), JsonSerializer.Serialize(document,
			new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
	}
}
var catalogs = cultures.ToDictionary(culture => culture, Catalog);
string[] Placeholders(string text) => Regex.Matches(text, @"\{(\d+)(?:[^{}]*)\}")
	.Select(match => match.Groups[1].Value).Distinct().Order().ToArray();
foreach (var (culture, catalog) in catalogs) {
	foreach (var (key, value) in english) {
		Check(catalog.ContainsKey(key), culture + " key missing: " + key);
		if (!catalog.TryGetValue(key, out string? translation)) continue;
		Check(!string.IsNullOrWhiteSpace(translation), culture + " empty translation: " + key);
		Check(Placeholders(value).SequenceEqual(Placeholders(translation)), culture + " format arguments differ: " + key);
		object[] placeholders = Enumerable.Repeat<object>("sample", 12).ToArray();
		try { _ = string.Format(CultureInfo.InvariantCulture, translation, placeholders); }
		catch (FormatException) { Check(false, culture + " malformed format: " + key); }
		var lines = SoulmatesTextLayout.Wrap(translation, 40f, text => new StringInfo(text).LengthInTextElements);
		Check(lines.All(line => new StringInfo(line).LengthInTextElements <= 40), culture + " long text did not wrap: " + key);
		int index = Array.IndexOf(addedCultures, culture);
		if (index >= 0 && translations.TryGetValue(key["Mods.Soulmates.".Length..], out string[]? values)
			&& values.Length > index)
			Check(translation == values[index], culture + " generated translation out of date: " + key);
	}
	foreach (string key in catalog.Keys) Check(english.ContainsKey(key), culture + " unknown key: " + key);
}

const string prefix = "Mods.Soulmates.";
var keys = english.Keys.Where(key => key.StartsWith(prefix)).Select(key => key[prefix.Length..]).ToHashSet();
var sections = keys.Select(key => key.Split('.')[0] + ".").Distinct().ToArray();
var sourceFiles = new[] { "Common", "Content" }.SelectMany(directory => Directory.EnumerateFiles(
	Path.Combine(root, directory), "*.cs", SearchOption.AllDirectories)).Append(Path.Combine(root, "Soulmates.cs"));
int references = 0;
foreach (string file in sourceFiles) {
	var syntax = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();
	foreach (var literal in syntax.DescendantNodes().OfType<LiteralExpressionSyntax>()) {
		if (!literal.IsKind(SyntaxKind.StringLiteralExpression)) continue;
		string key = literal.Token.ValueText;
		if (!sections.Any(section => key.StartsWith(section))) continue;
		references++;
		Check(keys.Contains(key) || keys.Any(candidate => candidate.StartsWith(key.TrimEnd('.') + ".")),
			$"Missing source key in {Path.GetFileName(file)}: {key}");
	}
	foreach (var interpolated in syntax.DescendantNodes().OfType<InterpolatedStringExpressionSyntax>()) {
		string first = interpolated.Contents.FirstOrDefault() is InterpolatedStringTextSyntax start ? start.TextToken.ValueText : "";
		if (!sections.Any(section => first.StartsWith(section))) continue;
		string pattern = "^" + string.Concat(interpolated.Contents.Select(part => part is InterpolatedStringTextSyntax text
			? Regex.Escape(text.TextToken.ValueText) : "[^.]+")) + "$";
		references++;
		Check(keys.Any(key => Regex.IsMatch(key, pattern)),
			$"Missing dynamic key in {Path.GetFileName(file)}: {interpolated}");
	}
	foreach (var type in syntax.DescendantNodes().OfType<EnumDeclarationSyntax>()) {
		string enumPrefix = "Enums." + type.Identifier.ValueText + ".";
		if (!keys.Any(key => key.StartsWith(enumPrefix))) continue;
		foreach (var member in type.Members)
			Check(keys.Contains(enumPrefix + member.Identifier.ValueText), "Untranslated enum: " + enumPrefix + member.Identifier.ValueText);
	}
}

foreach (string approach in new[] { "Adaptive", "Tunnel", "Vein", "Surface" }) {
	Check(keys.Contains("Enums.CompanionMiningApproach." + approach), "Mining name: " + approach);
	Check(keys.Contains("UI.MiningApproaches.Selected." + approach), "Mining reply: " + approach);
}
foreach (string category in new[] { "Care", "Commands", "Work", "Bond", "Voice", "Pack", "Items" }) {
	for (int option = 0; option < 3; option++)
		Check(keys.Contains($"Dialogue.Options.{category}.Option{option}"), "Dialogue option: " + category + option);
}

foreach (string move in new[] { "Rock", "Paper", "Scissors" })
	Check(keys.Contains("Games.Rps.Moves." + move), "RPS move missing: " + move);
Check(keys.Contains("UI.CompanionWheel.Categories.Games"), "Companion Games category missing");
foreach (string outcome in new[] { "PlayerWin", "CompanionWin", "Draw" }) {
	Check(keys.Contains("Games.Rps.Outcomes." + outcome), "RPS outcome missing: " + outcome);
	foreach (string personality in new[] { "Curious", "Loyal", "Brave", "Gentle", "Mischievous" })
		Check(keys.Contains($"Games.Rps.Replies.{outcome}.{personality}"), "RPS character reply missing: " + outcome + personality);
}

foreach (string text in new[] { "one two three four five", "verylongwordwithoutspaces", "line one\nline two", "\r\n", "" , "Soul\U0001F49Bmates", "这些小小的发现也很珍贵", "Путешествуем вместе", "Przyjaźń i życzliwość", "Étoiles et découvertes" }) {
	var lines = SoulmatesTextLayout.Wrap(text, 8f, value => new StringInfo(value).LengthInTextElements);
	Check(lines.Count > 0, "Empty wrapped layout");
	Check(lines.All(line => new StringInfo(line).LengthInTextElements <= 8), "Wrapped text exceeds width: " + text);
	Check(lines.All(line => !line.Any(char.IsSurrogate) || !line.StartsWith('\uDC9B')), "Split surrogate pair");
}
Check(SoulmatesTextLayout.Wrap("one\ntwo", 8f, value => value.Length).SequenceEqual(new[] { "one", "two" }), "Explicit line breaks lost");
Check(SoulmatesTextLayout.FitScale(100, 50, 0.8f) == 0.5f, "Long label did not shrink");
Check(SoulmatesTextLayout.FitScale(10, 50, 0.8f) == 0.8f, "Short label grew above preferred scale");
Check(SoulmatesTextLayout.FitScale(0, 50, 0.8f) == 0.8f, "Empty label produced an invalid scale");
foreach (var (culture, catalog) in catalogs) {
	foreach (string key in new[] { "UI.Creator.Details", "UI.Mailbox.BugContext", "UI.Mailbox.FeedbackContext", "UI.Mailbox.Disabled", "UI.Mailbox.BugSaved" }) {
		string text = catalog[prefix + key];
		float width = key == "UI.Creator.Details" ? 202f : 524f;
		float height = key == "UI.Creator.Details" ? 88f : 24f;
		float Measure(string value) => new StringInfo(value).LengthInTextElements * 16f;
		float scale = SoulmatesTextLayout.FitWrappedScale(text, width, height, 0.86f, 28f, Measure);
		var lines = SoulmatesTextLayout.Wrap(text, width / scale, Measure);
		Check(scale > 0 && lines.Count * 28f * scale <= height, culture + " wrapped label exceeds height: " + key);
		Check(lines.All(line => Measure(line) * scale <= width), culture + " wrapped label exceeds width: " + key);
	}
}

foreach (float uiScale in new[] { 1f, 1.25f, 1.5f, 2f }) {
	foreach (float zoom in new[] { 1f, 1.4f, 2f }) {
		Game.UIScale = uiScale;
		Game.screenPosition = new Vector2(1200f, 800f);
		Vector2 screenCenter = new(Game.screenWidth / 2f, Game.screenHeight / 2f);
		Game.GameViewMatrix.TransformationMatrix = Matrix.CreateTranslation(-screenCenter.X, -screenCenter.Y, 0f)
			* Matrix.CreateScale(zoom, zoom, 1f) * Matrix.CreateTranslation(screenCenter.X, screenCenter.Y, 0f);
		Vector2 world = Game.screenPosition + screenCenter + new Vector2(80f, -40f);
		Game.MouseScreen = screenCenter + new Vector2(80f, -40f) * zoom;
		Terraria.GameInput.PlayerInput.MouseX = (int)Game.MouseScreen.X;
		Terraria.GameInput.PlayerInput.MouseY = (int)Game.MouseScreen.Y;
		Check(Vector2.Distance(SoulmatesUISpace.FromWorld(world), SoulmatesUISpace.Mouse) <= 1.5f,
			$"World and hit-test coordinate mismatch: UI {uiScale}, zoom {zoom}");
		Check(Vector2.Distance(SoulmatesUISpace.WorldMouse, world) <= 1.5f,
			$"Direct order world target mismatch: UI {uiScale}, zoom {zoom}");
		Rectangle tile = SoulmatesUISpace.FromWorld(new Rectangle((int)world.X, (int)world.Y, 16, 16));
		Check(Math.Abs(tile.Width - 16f * zoom / uiScale) <= 1f, "Incorrect target outline size");
		Vector2 originalViewport = SoulmatesUISpace.Viewport;
		Vector2 originalMouse = SoulmatesUISpace.Mouse;
		Game.screenWidth = (int)(1280f / uiScale);
		Game.screenHeight = (int)(720f / uiScale);
		Game.MouseScreen /= uiScale;
		Check(SoulmatesUISpace.Viewport == originalViewport && SoulmatesUISpace.Mouse == originalMouse,
			"Coordinates changed after engine SetZoom_UI");
		Check(Vector2.Distance(SoulmatesUISpace.WorldMouse, world) <= 1.5f,
			"Direct order changed after engine SetZoom_UI");
		Game.screenWidth = 1280;
		Game.screenHeight = 720;
	}
}

var directOrderSyntax = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "Common", "UI", "DirectOrderSystem.cs"))).GetRoot();
Check(!directOrderSyntax.DescendantNodes().OfType<MemberAccessExpressionSyntax>()
	.Any(member => member.ToString() == "Main.MouseWorld"), "Direct order reused hook-dependent Main.MouseWorld");
foreach (string file in new[] { "Common/SoulmatesPlayer.cs", "Content/NPCs/SoulboundCompanion.cs" }) {
	var syntax = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, file))).GetRoot();
	Check(!syntax.DescendantNodes().OfType<MemberAccessExpressionSyntax>()
		.Any(member => member.ToString() == "Main.MouseWorld"), "Right-click reused hook-dependent coordinates: " + file);
}
foreach (string mode in new[] { "Terraria", "Player", "Companion", "Point", "Look", "Gather", "Mine", "Forest", "Emotes", "Tools", "Target", "TargetChanged" })
	Check(keys.Contains("UI.CompanionWheel.MouseModes." + mode), "Mouse mode translation missing: " + mode);
foreach (Vector2 physicalSize in new[] { new Vector2(800, 600), new Vector2(1280, 720), new Vector2(1920, 1080), new Vector2(2560, 1440) }) {
	foreach (float uiScale in new[] { 1f, 1.25f, 1.5f, 2f }) {
		Vector2 viewport = physicalSize / uiScale;
		float scale = SoulwheelLayout.Scale(viewport);
		foreach (Vector2 desired in new[] { Vector2.Zero, viewport, viewport * 0.5f, new Vector2(-100, 10000) }) {
			Vector2 center = SoulwheelLayout.ClampCenter(desired, viewport);
			Check(center.X - 202f * scale >= 0 && center.X + 202f * scale <= viewport.X,
				"Soulwheel outer ring clipped horizontally");
			Check(center.Y - 202f * scale >= 0 && center.Y + 304f * scale <= viewport.Y,
				"Soulwheel outer ring/status clipped vertically");
			for (int i = 0; i < 3; i++) {
				Vector2 position = SoulwheelLayout.ModePosition(center, i, viewport);
				Check(position.Y - 18f * scale > center.Y + (SoulwheelLayout.OuterRadius + 19f) * scale,
					"Mouse-mode selector overlaps native emotes");
				Check(position.Y + 18f * scale < center.Y + SoulwheelLayout.LabelOffset * scale,
					"Mouse-mode selector overlaps hover label");
				if (i > 0) Check(Vector2.Distance(position, SoulwheelLayout.ModePosition(center, i - 1, viewport)) > 36f * scale,
					"Mouse-mode hit targets overlap");
			}
		}
	}
}

foreach (string system in new[] { "TalkModeSystem", "SoulCreatorSystem", "FeedbackMailboxSystem" }) {
	var syntax = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "Common", "UI", system + ".cs"))).GetRoot();
	Check(!syntax.DescendantNodes().OfType<MethodDeclarationSyntax>().Any(method => method.Identifier.ValueText == "Load"),
		"UI initializes before localization: " + system);
}
foreach (string description in new[] { "description.txt", "description_workshop.txt", "description_workshop_de.txt" })
	Check(System.Text.Encoding.UTF8.GetByteCount(File.ReadAllText(Path.Combine(root, description))) < 8000,
		"Workshop description exceeds 8000 bytes: " + description);

Console.WriteLine($"Checked {english.Count} keys in {cultures.Length} languages, {references} source references and {assertions} assertions.");
foreach (string error in errors) Console.Error.WriteLine(error);
if (errors.Count > 0) Environment.Exit(1);
Console.WriteLine("Localization, text wrapping, UI transforms and initialization checks passed.");
