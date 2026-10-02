using System;
using Microsoft.Xna.Framework;

namespace Soulmates.Common.UI;

internal static class SoulwheelLayout
{
	public const float OuterRadius = 178f;
	public const float LabelOffset = 250f;
	public const float StatusOffset = 274f;
	public static float Scale(Vector2 viewport) => Math.Min(1f, Math.Min(viewport.X / 460f, viewport.Y / 540f));
	public static Vector2 ModePosition(Vector2 center, int index, Vector2 viewport)
		=> center + new Vector2((index - 1) * 44f, 222f) * Scale(viewport);
	public static Vector2 ClampCenter(Vector2 desired, Vector2 viewport)
	{
		float margin = (OuterRadius + 32f) * Scale(viewport);
		float bottomMargin = 304f * Scale(viewport);
		float x = viewport.X <= margin * 2f ? viewport.X * 0.5f : Math.Clamp(desired.X, margin, viewport.X - margin);
		float y = viewport.Y <= margin + bottomMargin ? viewport.Y * 0.5f : Math.Clamp(desired.Y, margin, viewport.Y - bottomMargin);
		return new Vector2(x, y);
	}
}
