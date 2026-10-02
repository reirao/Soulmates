#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Soulmates.Common;
using Soulmates.Common.Dialogue;
using Soulmates.Common.UI;
using Soulmates.Content.Items;
using Soulmates.Content.Projectiles;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulmates.Content.NPCs;

public sealed partial class SoulboundCompanion
{
	private void ChooseNextState(float distance, Vector2 followTarget)
	{
		if (distance > 170f || Owner.velocity.LengthSquared() > 9f) {
			brainState = BrainState.Follow;
			stateTimer = 70;
			return;
		}

		int curiosity = Profile.Personality switch {
			CompanionPersonality.Curious => 55,
			CompanionPersonality.Mischievous => 45,
			CompanionPersonality.Brave => 30,
			_ => 20
		};
		int roll = Main.rand.Next(100);
		if (roll < curiosity) {
			brainState = Main.rand.NextBool() ? BrainState.Wander : BrainState.Inspect;
			idleTarget = followTarget + Main.rand.NextVector2Circular(90f, 44f);
			stateTimer = Main.rand.Next(100, 220);
		}
		else {
			brainState = BrainState.Idle;
			stateTimer = Profile.Personality == CompanionPersonality.Gentle ? Main.rand.Next(220, 380) : Main.rand.Next(140, 280);
		}
	}

	private void MoveTo(Vector2 target, float maxSpeed, float responsiveness)
	{
		Vector2 offset = target - NPC.Center;
		float speed = MathHelper.Clamp(offset.Length() / 16f, 0f, maxSpeed);
		Vector2 desired = offset.SafeNormalize(Vector2.Zero) * speed;
		NPC.velocity = Vector2.Lerp(NPC.velocity, desired, responsiveness);
		if (offset.LengthSquared() < 64f)
			NPC.velocity *= 0.94f;
	}

	private float IdleBob() => MathF.Sin(Main.GlobalTimeWrappedHourly * 1.65f + bobSeed) * 5f;

	private void UpdateFacing()
	{
		if (TryFaceTrackedTarget()) {
			NPC.spriteDirection = facing;
			return;
		}

		if (facingCooldown > 0)
			facingCooldown--;

		if (MathF.Abs(NPC.velocity.X) > 0.55f && facingCooldown == 0) {
			int desired = NPC.velocity.X < 0f ? -1 : 1;
			if (desired != facing) {
				facing = desired;
				facingCooldown = 24;
			}
		}
		NPC.spriteDirection = facing;
	}

	private bool TryFaceTrackedTarget()
	{
		int targetIndex = guardianTarget >= 0 ? guardianTarget : socialNpcTarget >= 0 ? socialNpcTarget
			: IsAttendingCritter ? critterTarget!.whoAmI : -1;
		if (targetIndex < 0 || targetIndex >= Main.maxNPCs)
			return false;
		NPC target = Main.npc[targetIndex];
		if (!target.active || MathF.Abs(target.Center.X - NPC.Center.X) < 2f)
			return false;
		facing = target.Center.X < NPC.Center.X ? -1 : 1;
		facingCooldown = 8;
		return true;
	}

	private void UpdateAuraDust()
	{
		int chance = Profile.Aura switch {
			CompanionAura.SoulSparks => 5,
			CompanionAura.OrbitingStars => 8,
			_ => 14
		};
		if (!Main.rand.NextBool(chance))
			return;

		Dust dust = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(24f, 24f), DustID.Enchanted_Gold,
			-NPC.velocity * 0.08f, 120, Profile.EssenceColor, Profile.Aura == CompanionAura.SoftGlow ? 0.55f : 0.8f);
		dust.noGravity = true;
	}

}
