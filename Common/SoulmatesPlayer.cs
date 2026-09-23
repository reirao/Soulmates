using Terraria.ModLoader;

namespace Soulmates.Common;

public sealed class SoulmatesPlayer : ModPlayer
{
	public int ActiveCompanionWhoAmI { get; set; } = -1;

	public override void Initialize()
	{
		ActiveCompanionWhoAmI = -1;
	}

	public override void UpdateDead()
	{
		ActiveCompanionWhoAmI = -1;
	}
}
