using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;

namespace Soulmates.Common.UI;

internal static class SoulmatesUISpace
{
	// Main's mouse and screen fields change between world/UI hooks; the raw input state does not.
	public static Vector2 Viewport => PlayerInput.OriginalScreenSize / Main.UIScale;
	public static Vector2 Mouse => new Vector2(PlayerInput.MouseX, PlayerInput.MouseY) / Main.UIScale;

	public static Vector2 FromWorld(Vector2 position)
		=> Vector2.Transform(position - Main.screenPosition, Main.GameViewMatrix.TransformationMatrix) / Main.UIScale;

	public static Rectangle FromWorld(Rectangle rectangle)
	{
		Vector2 topLeft = FromWorld(new Vector2(rectangle.Left, rectangle.Top));
		Vector2 bottomRight = FromWorld(new Vector2(rectangle.Right, rectangle.Bottom));
		return new Rectangle((int)MathF.Floor(topLeft.X), (int)MathF.Floor(topLeft.Y),
			(int)MathF.Ceiling(bottomRight.X - topLeft.X), (int)MathF.Ceiling(bottomRight.Y - topLeft.Y));
	}
}
