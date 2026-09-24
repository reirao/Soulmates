using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulmates.Common;

public static class CompanionVisuals
{
	public static Texture2D GetTexture(CompanionMuse muse)
	{
		if (muse == CompanionMuse.Soulkin)
			return ModContent.Request<Texture2D>("Soulmates/Content/NPCs/SoulboundCompanion", AssetRequestMode.ImmediateLoad).Value;

		return TextureAssets.Npc[GetNpcId(muse)].Value;
	}

	public static Rectangle GetFrame(CompanionMuse muse, Texture2D texture)
	{
		if (muse == CompanionMuse.Soulkin)
			return texture.Bounds;

		int frames = Main.npcFrameCount[GetNpcId(muse)];
		return new Rectangle(0, 0, texture.Width, texture.Height / frames);
	}

	private static int GetNpcId(CompanionMuse muse) => muse switch {
		CompanionMuse.Bunny => NPCID.Bunny,
		CompanionMuse.BlueSlime => NPCID.BlueSlime,
		CompanionMuse.Bird => NPCID.Bird,
		CompanionMuse.Squirrel => NPCID.Squirrel,
		_ => NPCID.Bunny
	};
}
