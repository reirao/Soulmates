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
	public override void SendExtraAI(BinaryWriter writer)
	{
		Profile.Write(writer);
		writer.Write((byte)activeJob);
		writer.Write(jobCount);
		writer.Write(jobPlannedTotal);
		writer.Write(jobRecoveryPaused);
		writer.Write((short)guardianTarget);
		writer.Write(idleTarget.X);
		writer.Write(idleTarget.Y);
		writer.Write((byte)activeEmote);
		writer.Write((short)Math.Clamp(emoteTimer, 0, short.MaxValue));
		writer.Write((short)socialNpcTarget);
		writer.Write((short)Math.Clamp(socialNpcTimer, 0, short.MaxValue));
		writer.Write(socialNpcGreeted);
		writer.Write((byte)autonomyActivity);
		writer.Write((short)autonomyTargetItem);
		writer.Write((short)autonomyTargetTile.X);
		writer.Write((short)autonomyTargetTile.Y);
		writer.Write((byte)autonomyForestAction);
		writer.Write((short)Math.Clamp(autonomyActionTimer, 0, short.MaxValue));
		writer.Write((byte)Math.Clamp(autonomyWorkCount, 0, byte.MaxValue));
		writer.Write((byte)pendingAutonomyActivity);
		writer.Write((short)Math.Clamp(pendingInitiativeTimer, 0, short.MaxValue));
		writer.Write((short)pendingTargetItem);
		writer.Write((short)pendingTargetTile.X);
		writer.Write((short)pendingTargetTile.Y);
		writer.Write((byte)pendingForestAction);
		writer.Write((byte)pendingQuestion);
		writer.Write(questionId.ToByteArray());
		writer.Write((short)Math.Clamp(questionTicks, 0, InitiativeResponseTicks));
	}

	public override void ReceiveExtraAI(BinaryReader reader)
	{
		Profile = CompanionProfile.Read(reader);
		activeJob = (CompanionJob)reader.ReadByte();
		jobCount = reader.ReadInt32();
		jobPlannedTotal = reader.ReadInt32();
		jobRecoveryPaused = reader.ReadBoolean();
		guardianTarget = reader.ReadInt16();
		idleTarget = new Vector2(reader.ReadSingle(), reader.ReadSingle());
		activeEmote = (CompanionEmote)reader.ReadByte();
		emoteTimer = reader.ReadInt16();
		socialNpcTarget = reader.ReadInt16();
		socialNpcTimer = reader.ReadInt16();
		socialNpcGreeted = reader.ReadBoolean();
		autonomyActivity = (AutonomyActivity)reader.ReadByte();
		autonomyTargetItem = reader.ReadInt16();
		autonomyTargetTile = new Point(reader.ReadInt16(), reader.ReadInt16());
		autonomyForestAction = (ForestAction)reader.ReadByte();
		autonomyActionTimer = reader.ReadInt16();
		autonomyWorkCount = reader.ReadByte();
		pendingAutonomyActivity = (AutonomyActivity)reader.ReadByte();
		pendingInitiativeTimer = reader.ReadInt16();
		pendingTargetItem = reader.ReadInt16();
		pendingTargetTile = new Point(reader.ReadInt16(), reader.ReadInt16());
		pendingForestAction = (ForestAction)reader.ReadByte();
		CompanionQuestion question = (CompanionQuestion)reader.ReadByte();
		byte[] token = reader.ReadBytes(16);
		if (token.Length != 16) throw new EndOfStreamException();
		int ticks = reader.ReadInt16();
		var id = new Guid(token);
		if (!Enum.IsDefined(question) || ticks < 0 || ticks > InitiativeResponseTicks
			|| (question == CompanionQuestion.None ? ticks != 0 || id != Guid.Empty : ticks == 0 || id == Guid.Empty))
			throw new InvalidDataException("Invalid companion question.");
		pendingQuestion = question;
		questionId = id;
		questionTicks = ticks;
		if (Main.netMode != NetmodeID.MultiplayerClient || !TryGetOwner(out Player owner) || owner.whoAmI != Main.myPlayer)
			return;
		foreach (Item item in owner.inventory) {
			if (item.ModItem is SoulboundSigil sigil && sigil.Profile.Id == Profile.Id) {
				sigil.Profile = Profile.Clone();
				break;
			}
		}
	}
}
