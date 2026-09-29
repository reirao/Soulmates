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
	private const int SoulkinFrameCount = 4;
	private static Asset<Texture2D>[] idleFrames = [];
	private static Asset<Texture2D>[] actionFrames = [];

	internal static void Load()
	{
		idleFrames = LoadFrames("Idle");
		actionFrames = LoadFrames("Action");
	}

	internal static void Unload()
	{
		idleFrames = [];
		actionFrames = [];
	}

	public static Texture2D GetTexture(CompanionMuse muse, bool action = false)
	{
		if (muse == CompanionMuse.Soulkin) {
			int frame = AnimationFrame(action, SoulkinFrameCount);
			Asset<Texture2D>[] frames = action ? actionFrames : idleFrames;
			if (frames.Length != SoulkinFrameCount)
				Load();
			return (action ? actionFrames : idleFrames)[frame].Value;
		}

		int npcId = GetNpcId(muse);
		Main.instance.LoadNPC(npcId);
		return TextureAssets.Npc[npcId].Value;
	}

	public static Rectangle GetFrame(CompanionMuse muse, Texture2D texture, bool action = false)
	{
		if (muse == CompanionMuse.Soulkin)
			return texture.Bounds;

		int frames = Math.Max(1, Main.npcFrameCount[GetNpcId(muse)]);
		int frame = AnimationFrame(action, frames);
		return new Rectangle(0, frame * texture.Height / frames, texture.Width, texture.Height / frames);
	}

	private static int AnimationFrame(bool action, int count)
	{
		int frameSpeed = action ? 12 : 30;
		return (int)(Main.GameUpdateCount / frameSpeed % (uint)Math.Max(1, count));
	}

	private static Asset<Texture2D>[] LoadFrames(string state)
	{
		var frames = new Asset<Texture2D>[SoulkinFrameCount];
		for (int i = 0; i < frames.Length; i++)
			frames[i] = ModContent.Request<Texture2D>($"Soulmates/Content/NPCs/Soulkin/{state}{i}");
		return frames;
	}

	private static int GetNpcId(CompanionMuse muse) => muse switch {
		CompanionMuse.Bunny => NPCID.Bunny,
		CompanionMuse.BlueSlime => NPCID.BlueSlime,
		CompanionMuse.Bird => NPCID.Bird,
		CompanionMuse.Squirrel => NPCID.Squirrel,
		_ => NPCID.Bunny
	};
}
