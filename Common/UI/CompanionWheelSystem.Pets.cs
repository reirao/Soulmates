#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace Soulmates.Common.UI;

public sealed partial class CompanionWheelSystem
{
	private bool petsMenu;
	private int PetNodeCount => CompanionPets.SupportedItems.Count + 1;
	private Vector2 PetPosition(int index) => center + FanAngle(-MathHelper.PiOver2, index, PetNodeCount,
		MathHelper.ToRadians(160f)).ToRotationVector2() * BranchRadius * LayoutScale;
	private string PetLabel(int index)
	{
		if (index == CompanionPets.SupportedItems.Count) return SoulmatesText.Get("Pets.Dismiss");
		int itemType = CompanionPets.SupportedItems[index];
		return SoulmatesText.Get(companion!.Profile.PetItemType == itemType ? "Pets.Selected"
			: CompanionPets.IsCarried(companion.Profile, itemType) ? "Pets.Choose" : "Pets.Missing", Lang.GetItemNameValue(itemType));
	}
	private void ActivatePet(int index)
	{
		if (companion is null || index < 0 || index >= PetNodeCount) return;
		int type = index < CompanionPets.SupportedItems.Count ? CompanionPets.SupportedItems[index] : 0;
		int slot = type == 0 ? -1 : companion.Profile.Pack.FindIndex(item => !item.IsAir && item.type == type);
		if (type != 0 && slot < 0) { SoundEngine.PlaySound(SoundID.MenuTick); return; }
		if (Main.netMode == NetmodeID.MultiplayerClient) global::Soulmates.Soulmates.SendPetConfigRequest(companion.Profile, slot);
		else companion.ConfigurePet(slot, type, slot < 0 ? new byte[32] : companion.Profile.StorageToken(CompanionStorage.Pack, slot));
		SoundEngine.PlaySound(SoundID.MenuTick);
	}
	private void DrawPets(SpriteBatch spriteBatch, Color accent)
	{
		for (int i = 0; i < PetNodeCount; i++) {
			int type = i < CompanionPets.SupportedItems.Count ? CompanionPets.SupportedItems[i] : 0;
			bool enabled = type == 0 || CompanionPets.IsCarried(companion!.Profile, type);
			DrawNode(spriteBatch, PetPosition(i), 40f, enabled ? accent : Color.Gray,
				hoverLayer == HoverLayer.Pet && hoverIndex == i, type != 0 && companion!.Profile.PetItemType == type,
				type == 0 ? new WheelIcon(IconKind.Close, 0) : new WheelIcon(IconKind.Item, type));
		}
	}
}
