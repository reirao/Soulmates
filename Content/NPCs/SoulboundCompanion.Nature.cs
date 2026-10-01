#nullable enable
using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Soulmates.Common;
using Soulmates.Common.Feedback;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI;
using Terraria.ID;

namespace Soulmates.Content.NPCs;

public sealed partial class SoulboundCompanion
{
	private int insectCatchCooldown;
	private readonly int[] insectFlock = new int[CompanionInsects.MaximumFlock];
	private int insectFlockCount;
	private int insectFlockRefresh;

	private void UpdateNatureCompanions()
	{
		if (insectCatchCooldown > 0) insectCatchCooldown--;
		if (Profile.IsAether && Profile.AutonomyEnabled && Command != StayCommand
			&& Profile.ForestryInitiative == CompanionInitiativePolicy.Always && activeJob == CompanionJob.None
			&& autonomyActivity == AutonomyActivity.None && !HasPendingInitiative && guardianTarget < 0
			&& socialNpcTarget < 0 && Profile.Energy >= 20 && Profile.Mood >= 20)
			TryCatchNearbyInsect();

		if (Main.dedServ || --insectFlockRefresh > 0) return;
		insectFlockRefresh = 30;
		insectFlockCount = 0;
		if (!Profile.IsAether) return;
		foreach (Item item in Profile.CarriedItems) {
			int npcType = CompanionInsects.NpcForItem(item.type);
			if (item.IsAir || npcType < 0) continue;
			for (int i = 0; i < item.stack && insectFlockCount < insectFlock.Length; i++)
				insectFlock[insectFlockCount++] = npcType;
			if (insectFlockCount == insectFlock.Length) break;
		}
	}

	private bool TryCatchNearbyInsect()
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || !Profile.IsAether || insectCatchCooldown > 0)
			return false;
		insectCatchCooldown = 90;
		Item? net = Profile.CarriedItems.FirstOrDefault(item => !item.IsAir && ItemID.Sets.CatchingTool[item.type]);
		if (net is null) return false;
		foreach (NPC insect in Main.ActiveNPCs) {
			if (!CompanionInsects.CanCatch(insect)
				|| Vector2.DistanceSquared(insect.Center, NPC.Center) > 96f * 96f
				|| Vector2.DistanceSquared(insect.Center, Owner.Center) > 320f * 320f
				|| !Collision.CanHitLine(NPC.Center, 1, 1, insect.Center, 1, 1)) continue;
			var testDrop = new Item(insect.catchItem);
			if (Profile.GetStorableAmount(testDrop) < 1 || !CompanionCatchReceipt.Begin(insect, Owner)) continue;
			bool caught;
			Item? nativeDrop;
			try {
				caught = Terraria.NPC.CheckCatchNPC(insect, insect.Hitbox, net, Owner,
					ItemID.Sets.LavaproofCatchingTool[net.type]) && !insect.active;
			}
			finally { nativeDrop = CompanionCatchReceipt.End(); }
			insectCatchCooldown = 300;
			if (!caught) return false;
			int index = nativeDrop is null ? -1 : Array.IndexOf(Main.item, nativeDrop);
			if (nativeDrop is { active: true } && index >= 0 && index < Main.maxItems) {
				int type = nativeDrop.type;
				int moved = StoreLooseItem(nativeDrop);
				if (Main.netMode == NetmodeID.Server && moved > 0)
					NetMessage.SendData(MessageID.SyncItem, -1, -1, null, index);
				if (moved > 0) {
					if (Profile.Memories.All(memory => memory.Kind != CompanionMemoryKind.InsectCompanion))
						Profile.Remember(CompanionMemoryKind.InsectCompanion, detail: Lang.GetItemNameValue(type));
					Profile.Energy = Math.Max(0, Profile.Energy - 1);
					SyncPackState();
					SoulmatesFeedbackSystem.Record("insect_caught", ("item_type", type), ("amount", moved));
				}
			}
			ShowNativeEmote(EmoteID.EmotionLove, 120);
			if (speechTimer <= 0) SpeakLocalized("Social.Nature.Insect", insect.TypeName);
			return true;
		}
		return false;
	}

	private void DrawInsectCompanions(SpriteBatch spriteBatch, Vector2 center)
	{
		if (!Profile.IsAether) return;
		for (int i = 0; i < insectFlockCount; i++) {
			int type = insectFlock[i];
			if (TextureAssets.Npc[type]?.IsLoaded != true) continue;
			Texture2D texture = TextureAssets.Npc[type].Value;
			int frames = Math.Max(1, Main.npcFrameCount[type]);
			int height = texture.Height / frames;
			int frame = (int)((Main.GameUpdateCount / 12 + (ulong)i) % (ulong)frames);
			Rectangle source = new(0, height * frame, texture.Width, height);
			float angle = Main.GlobalTimeWrappedHourly * (0.7f + i * 0.04f) + i * MathHelper.TwoPi / insectFlockCount;
			Vector2 offset = new(MathF.Cos(angle) * (38f + i * 3f), MathF.Sin(angle) * 23f - 5f);
			spriteBatch.Draw(texture, center + offset, source, Color.White, 0f, source.Size() * 0.5f,
				Math.Min(1f, 20f / Math.Max(source.Width, source.Height)),
				MathF.Sin(angle) > 0f ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
		}
	}
}
