#nullable enable
using System;
using System.Collections.Generic;

namespace Soulmates.Common;

internal static class SoulmatesTextLayout
{
	public static List<string> Wrap(string text, float maximumWidth, Func<string, float> measure)
	{
		var lines = new List<string>();
		foreach (string paragraph in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')) {
			string current = "";
			foreach (string word in paragraph.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)) {
				string candidate = current.Length == 0 ? word : current + " " + word;
				if (current.Length > 0 && measure(candidate) > maximumWidth) {
					lines.Add(current);
					current = "";
				}
				current = current.Length == 0 ? word : current + " " + word;
				// Split long names/words as text elements, never between surrogate pairs.
				while (measure(current) > maximumWidth) {
					int[] elements = System.Globalization.StringInfo.ParseCombiningCharacters(current);
					int end = current.Length;
					for (int i = 1; i < elements.Length; i++) {
						if (measure(current[..elements[i]]) <= maximumWidth)
							continue;
						end = elements[i - 1];
						break;
					}
					if (end == 0)
						end = elements.Length > 1 ? elements[1] : current.Length;
					if (end == current.Length && elements.Length > 1)
						end = elements[^1];
					lines.Add(current[..end]);
					current = current[end..];
					if (current.Length == 0)
						break;
				}
			}
			if (current.Length > 0 || paragraph.Length == 0)
				lines.Add(current);
		}
		if (lines.Count == 0)
			lines.Add("...");
		return lines;
	}
}
