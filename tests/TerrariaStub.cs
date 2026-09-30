using Microsoft.Xna.Framework;

namespace Terraria
{

// Only screen state is needed to exercise the production coordinate conversions.
internal static class Main
{
	public static int screenWidth = 1280;
	public static int screenHeight = 720;
	public static float UIScale = 1f;
	public static Vector2 MouseScreen;
	public static Vector2 screenPosition;
	public static readonly ViewMatrix GameViewMatrix = new();
}

internal sealed class ViewMatrix
{
	public Matrix TransformationMatrix = Matrix.Identity;
}
}

namespace Terraria.GameInput
{
	internal static class PlayerInput
	{
		public static Vector2 OriginalScreenSize = new(1280f, 720f);
		public static int MouseX;
		public static int MouseY;
	}
}
