using System;
using Terraria.Localization;

namespace Soulmates.Common;

public static class SoulmatesText
{
	private const string Root = "Mods.Soulmates.";

	public static string Get(string key, params object[] args) => Language.GetTextValue(Root + key, args);

	public static string EnumName<T>(T value) where T : struct, Enum => Get($"Enums.{typeof(T).Name}.{value}");
}
