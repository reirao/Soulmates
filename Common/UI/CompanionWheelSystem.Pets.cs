#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulmates.Common.UI;

public sealed partial class CompanionWheelSystem
{
	private bool petsMenu;
	private int petPage;
	private const int PetPageSize = 4;
	private int PetPageCount => Math.Max(1, ((companion?.Profile.PetItems.Count ?? 0) + PetPageSize - 1) / PetPageSize);
	private int CurrentPetPage => Math.Clamp(petPage, 0, PetPageCount - 1);
	private int PetEntries => Math.Clamp((companion?.Profile.PetItems.Count ?? 0) - CurrentPetPage * PetPageSize, 0, PetPageSize);
	private int PetNodeCount => PetEntries + 2 + (PetPageCount > 1 ? 2 : 0);
	private Vector2 PetPosition(int index) => center + FanAngle(-MathHelper.PiOver2, index, PetNodeCount,
		MathHelper.ToRadians(160f)).ToRotationVector2() * BranchRadius * LayoutScale;
	private string PetLabel(int index)
	{
		if (index == PetEntries) return SoulmatesText.Get("Pets.Inventory");
		if (index == PetEntries + 1) return SoulmatesText.Get("Pets.Dismiss");
		if (index > PetEntries + 1) return SoulmatesText.Get(index == PetEntries + 2 ? "UI.CompanionWheel.Previous" : "UI.CompanionWheel.Next");
		int itemType = companion!.Profile.PetItems[CurrentPetPage * PetPageSize + index].type;
		return SoulmatesText.Get(companion.Profile.PetItemType == itemType ? "Pets.Selected" : "Pets.Choose", Lang.GetItemNameValue(itemType));
	}
	private void ActivatePet(int index)
	{
		if (companion is null || index < 0 || index >= PetNodeCount) return;
		if (index == PetEntries) {
			var target = companion;
			if (target.FindBoundSigil() is { } sigil) {
				Close();
				ModContent.GetInstance<TalkModeSystem>().OpenPetInventory(sigil, target);
			}
			return;
		}
		if (index > PetEntries + 1) {
			petPage = (CurrentPetPage + (index == PetEntries + 2 ? -1 : 1) + PetPageCount) % PetPageCount;
			SoundEngine.PlaySound(SoundID.MenuTick);
			return;
		}
		int slot = index == PetEntries + 1 ? -1 : CurrentPetPage * PetPageSize + index;
		int type = slot < 0 ? 0 : companion.Profile.PetItems[slot].type;
		if (Main.netMode == NetmodeID.MultiplayerClient) global::Soulmates.Soulmates.SendPetConfigRequest(companion.Profile, slot);
		else companion.ConfigurePet(slot, type, slot < 0 ? new byte[32] : companion.Profile.StorageToken(CompanionStorage.Pets, slot));
		SoundEngine.PlaySound(SoundID.MenuTick);
	}
	private void DrawPets(SpriteBatch spriteBatch, Color accent)
	{
		for (int i = 0; i < PetNodeCount; i++) {
			int type = i < PetEntries ? companion!.Profile.PetItems[CurrentPetPage * PetPageSize + i].type : 0;
			WheelIcon icon = type != 0 ? new WheelIcon(IconKind.Item, type)
				: i == PetEntries ? new WheelIcon(IconKind.Item, ItemID.PiggyBank)
				: new WheelIcon(i == PetEntries + 1 ? IconKind.Close : i == PetEntries + 2 ? IconKind.Back : IconKind.Forward, 0);
			DrawNode(spriteBatch, PetPosition(i), 40f, accent,
				hoverLayer == HoverLayer.Pet && hoverIndex == i, type != 0 && companion!.Profile.PetItemType == type,
				icon);
		}
	}
}
