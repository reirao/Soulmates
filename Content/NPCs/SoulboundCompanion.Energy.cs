using System;
using Soulmates.Common.Feedback;
using Terraria;
using Terraria.GameContent.UI;
using Terraria.ID;

namespace Soulmates.Content.NPCs;

public sealed partial class SoulboundCompanion
{
	private const int WorkEffortPerEnergy = 8;
	private const int RecoveryReserve = 30;
	private int workEffort;
	private bool autonomyRecoveryPaused;

	private void SpendWorkEnergy(int effort)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || effort <= 0) return;
		int total = workEffort + Math.Min(effort, WorkEffortPerEnergy * 100);
		Profile.Energy = Math.Max(0, Profile.Energy - total / WorkEffortPerEnergy);
		workEffort = total % WorkEffortPerEnergy;
	}

	private void UpdateAutonomyRecovery()
	{
		if (Main.netMode == NetmodeID.MultiplayerClient) return;
		bool recovering = Profile.AutonomyEnabled
			&& Profile.Energy < (autonomyRecoveryPaused ? RecoveryReserve : 16);
		if (recovering == autonomyRecoveryPaused) return;
		autonomyRecoveryPaused = recovering;
		if (recovering) ShowNativeEmote(EmoteID.EmoteSleep, 120);
		else autonomyDecisionTimer = 0;
		SoulmatesFeedbackSystem.Record(recovering ? "autonomy_recovery_started" : "autonomy_recovery_finished",
			("energy", Profile.Energy));
		NPC.netUpdate = true;
	}
}
