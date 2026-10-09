#nullable enable
using System.Linq;
using System.Collections.Generic;
using Terraria.GameInput;

namespace Soulmates.Common.UI;

internal static class CompanionControls
{
	internal static string DetailsKeys {
		get {
			var keybind = global::Soulmates.Soulmates.TalkKeybind;
			if (keybind is null || PlayerInput.CurrentProfile is null)
				return SoulmatesText.Get("UI.Character.Unbound");
			try {
				var keys = keybind.GetAssignedKeys(InputMode.Keyboard);
				return keys.Count == 0 ? SoulmatesText.Get("UI.Character.Unbound") : string.Join(", ", keys.Distinct());
			}
			catch (KeyNotFoundException) { return SoulmatesText.Get("UI.Character.Unbound"); }
		}
	}
}
