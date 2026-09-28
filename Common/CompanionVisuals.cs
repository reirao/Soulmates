using System;
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
			return ModContent.Request<Texture2D>("Soulmates/Content/NPCs/SoulboundCompanionSheet", AssetRequestMode.ImmediateLoad).Value;

		return TextureAssets.Npc[GetNpcId(muse)].Value;
	}

	public static Rectangle GetFrame(CompanionMuse muse, Texture2D texture, bool action = false)
	{
		int frameSpeed = action ? 12 : 30;
		int animationFrame = (int)(Main.GameUpdateCount / frameSpeed);
		if (muse == CompanionMuse.Soulkin) {
			const int columns = 4;
			const int rows = 2;
			int width = texture.Width / columns;
			int height = texture.Height / rows;
			return new Rectangle(animationFrame % columns * width, action ? height : 0, width, height);
		}

		int frames = Math.Max(1, Main.npcFrameCount[GetNpcId(muse)]);
		int frame = animationFrame % frames;
		return new Rectangle(0, frame * texture.Height / frames, texture.Width, texture.Height / frames);
	}

	private static int GetNpcId(CompanionMuse muse) => muse switch {
		CompanionMuse.Bunny => NPCID.Bunny,
		CompanionMuse.BlueSlime => NPCID.BlueSlime,
		CompanionMuse.Bird => NPCID.Bird,
		CompanionMuse.Squirrel => NPCID.Squirrel,
		_ => NPCID.Bunny
	};
}
