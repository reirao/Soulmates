using Terraria.ModLoader;

namespace Soulmates;

public sealed class Soulmates : Mod
{
	internal static ModKeybind TalkKeybind { get; private set; } = null!;

	public override void Load()
	{
		if (!Terraria.Main.dedServ)
			TalkKeybind = KeybindLoader.RegisterKeybind(this, "TalkToCompanion", "V");
	}

	public override void Unload()
	{
		TalkKeybind = null!;
	}
}
