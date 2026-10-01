using System.Globalization;
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

var english = Catalog("en-US");
var german = Catalog("de-DE");
string[] Placeholders(string text) => Regex.Matches(text, @"\{(\d+)(?:[^{}]*)\}")
	.Select(match => match.Groups[1].Value).Distinct().Order().ToArray();
foreach (var (key, value) in english) {
	Check(german.ContainsKey(key), "German key missing: " + key);
	if (!german.TryGetValue(key, out string? translation)) continue;
	Check(Placeholders(value).SequenceEqual(Placeholders(translation)), "Format arguments differ: " + key);
	object[] placeholders = Enumerable.Repeat<object>("sample", 12).ToArray();
	try { _ = string.Format(CultureInfo.InvariantCulture, value, placeholders); }
	catch (FormatException) { Check(false, "Malformed English format: " + key); }
	try { _ = string.Format(CultureInfo.InvariantCulture, translation, placeholders); }
	catch (FormatException) { Check(false, "Malformed German format: " + key); }
}
foreach (string key in german.Keys)
	Check(english.ContainsKey(key), "English key missing: " + key);

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
foreach (string category in new[] { "Care", "Commands", "Work", "Bond", "Voice", "Pack" }) {
	for (int option = 0; option < 3; option++)
		Check(keys.Contains($"Dialogue.Options.{category}.Option{option}"), "Dialogue option: " + category + option);
}

foreach (string text in new[] { "one two three four five", "verylongwordwithoutspaces", "line one\nline two", "\r\n", "" , "Soul\U0001F49Bmates" }) {
	var lines = SoulmatesTextLayout.Wrap(text, 8f, value => new StringInfo(value).LengthInTextElements);
	Check(lines.Count > 0, "Empty wrapped layout");
	Check(lines.All(line => new StringInfo(line).LengthInTextElements <= 8), "Wrapped text exceeds width: " + text);
	Check(lines.All(line => !line.Any(char.IsSurrogate) || !line.StartsWith('\uDC9B')), "Split surrogate pair");
}
Check(SoulmatesTextLayout.Wrap("one\ntwo", 8f, value => value.Length).SequenceEqual(new[] { "one", "two" }), "Explicit line breaks lost");

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
		Rectangle tile = SoulmatesUISpace.FromWorld(new Rectangle((int)world.X, (int)world.Y, 16, 16));
		Check(Math.Abs(tile.Width - 16f * zoom / uiScale) <= 1f, "Incorrect target outline size");
		Vector2 originalViewport = SoulmatesUISpace.Viewport;
		Vector2 originalMouse = SoulmatesUISpace.Mouse;
		Game.screenWidth = (int)(1280f / uiScale);
		Game.screenHeight = (int)(720f / uiScale);
		Game.MouseScreen /= uiScale;
		Check(SoulmatesUISpace.Viewport == originalViewport && SoulmatesUISpace.Mouse == originalMouse,
			"Coordinates changed after engine SetZoom_UI");
		Game.screenWidth = 1280;
		Game.screenHeight = 720;
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

Console.WriteLine($"Checked {english.Count} bilingual keys, {references} source references and {assertions} assertions.");
foreach (string error in errors) Console.Error.WriteLine(error);
if (errors.Count > 0) Environment.Exit(1);
Console.WriteLine("Localization, text wrapping, UI transforms and initialization checks passed.");
