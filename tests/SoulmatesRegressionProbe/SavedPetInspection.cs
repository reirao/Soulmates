#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ModLoader.IO;

namespace SoulmatesRegressionProbe;

public sealed partial class EngineChecks
{
	private static void InspectSavedPetState()
	{
		string? path = Environment.GetEnvironmentVariable("SOULMATES_INSPECT_PLAYER");
		if (string.IsNullOrEmpty(path)) return;
		var lines = new List<string>();
		void Walk(object value)
		{
			if (value is TagCompound tag) {
				if (tag.ContainsKey("petItemType")) {
					lines.Add("Selected pet item: " + tag.GetInt("petItemType"));
					foreach (TagCompound itemTag in tag.GetList<TagCompound>("petItems")) {
						Item item = ItemIO.Load(itemTag);
						lines.Add($"Stored pet: {item.type}; stack {item.stack}");
					}
				}
				foreach (var entry in tag) Walk(entry.Value);
			}
			else if (value is IList list) foreach (object entry in list) Walk(entry);
		}
		Walk(TagIO.FromFile(path));
		File.WriteAllLines(Path.Combine(Main.SavePath, "saved-pet-inspection.txt"), lines);
	}
}
