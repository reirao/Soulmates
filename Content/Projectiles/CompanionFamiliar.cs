#nullable enable
using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Soulmates.Common;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulmates.Content.Projectiles;

public sealed class CompanionFamiliar : ModProjectile
{
	private Guid companionId;
	private int bindingWait;
	public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.ZephyrFish}";
	internal void Bind(Guid id) => companionId = id;
	internal bool BelongsTo(SoulboundCompanion companion) => companionId == companion.Profile.Id
		&& (int)Projectile.ai[0] == companion.NPC.whoAmI;
	public override void SetDefaults()
	{
		Projectile.width = Projectile.height = 24;
		Projectile.aiStyle = -1;
		Projectile.friendly = Projectile.hostile = false;
		Projectile.tileCollide = false;
		Projectile.ignoreWater = true;
		Projectile.penetrate = -1;
		Projectile.timeLeft = 60;
		Projectile.netImportant = true;
	}
	public override bool? CanDamage() => false;
	public override void OnSpawn(IEntitySource source)
	{
		if (source is EntitySource_Parent { Entity: NPC { ModNPC: SoulboundCompanion companion } }) {
			companionId = companion.Profile.Id;
			Projectile.ai[0] = companion.NPC.whoAmI;
			Projectile.ai[1] = companion.Profile.PetItemType;
		}
	}
	public override void AI()
	{
		int index = (int)Projectile.ai[0];
		SoulboundCompanion? companion = index >= 0 && index < Main.maxNPCs && Main.npc[index].active
			? Main.npc[index].ModNPC as SoulboundCompanion : null;
		if (companion is null || !BelongsTo(companion)) {
			// A joining client can receive the projectile before the companion's profile.
			if (Main.netMode == NetmodeID.MultiplayerClient && ++bindingWait < 60) { Projectile.timeLeft = 2; return; }
			Projectile.Kill(); return;
		}
		int owner = (int)companion.NPC.ai[0], itemType = (int)Projectile.ai[1];
		if (owner < 0 || owner >= Main.maxPlayers || !Main.player[owner].active || Main.player[owner].dead) {
			Projectile.Kill(); return;
		}
		if (companion.Profile.PetItemType != itemType || !CompanionPets.IsCarried(companion.Profile, itemType)) {
			if (Main.netMode == NetmodeID.MultiplayerClient && ++bindingWait < 60) { Projectile.timeLeft = 2; return; }
			Projectile.Kill(); return;
		}
		bindingWait = 0;
		Projectile.timeLeft = 2;
		Vector2 goal = companion.NPC.Center + new Vector2(-companion.NPC.direction * 52f,
			-12f + MathF.Sin(Projectile.localAI[0]++ / 45f) * 5f);
		Vector2 offset = goal - Projectile.Center;
		if (offset.LengthSquared() > 800f * 800f) { Projectile.Center = goal; Projectile.velocity = Vector2.Zero; Projectile.netUpdate = true; }
		else Projectile.velocity = Vector2.Lerp(Projectile.velocity, offset.SafeNormalize(Vector2.Zero)
			* Math.Min(8f, offset.Length() / 12f), 0.12f);
		if (MathF.Abs(Projectile.velocity.X) > 0.25f) Projectile.spriteDirection = Projectile.velocity.X > 0 ? -1 : 1;
		int frames = Math.Max(1, Main.projFrames[CompanionPets.VisualFor(itemType)]);
		if (++Projectile.frameCounter >= (itemType == ItemID.Nectar ? 8 : 14)) {
			Projectile.frameCounter = 0; Projectile.frame = (Projectile.frame + 1) % frames;
		}
	}
	public override bool PreDraw(ref Color lightColor)
	{
		int visual = CompanionPets.VisualFor((int)Projectile.ai[1]);
		if (visual == 0) return false;
		Main.instance.LoadProjectile(visual);
		Texture2D texture = TextureAssets.Projectile[visual].Value;
		int frames = Math.Max(1, Main.projFrames[visual]);
		Rectangle frame = texture.Frame(1, frames, 0, Projectile.frame % frames);
		Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, frame, lightColor, 0f,
			frame.Size() * 0.5f, 1f, Projectile.spriteDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
		return false;
	}
	public override void SendExtraAI(BinaryWriter writer) => writer.Write(companionId.ToByteArray());
	public override void ReceiveExtraAI(BinaryReader reader)
	{
		byte[] bytes = reader.ReadBytes(16);
		if (bytes.Length != 16 || (companionId = new Guid(bytes)) == Guid.Empty)
			throw new InvalidDataException("Invalid familiar binding.");
	}
}
