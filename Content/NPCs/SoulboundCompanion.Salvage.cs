#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Common.Dialogue;
using Soulmates.Common.Feedback;
using Terraria;
using Terraria.GameContent.UI;
using Terraria.ID;

namespace Soulmates.Content.NPCs;

public sealed partial class SoulboundCompanion
{
	private List<SalvageTarget> pendingSalvage = [];
	private readonly Queue<SalvageTarget> salvageWork = [];
	private int salvageDelay;
	private int salvageSkipped;
	private Vector2 salvageDropCenter;
	internal bool CanTargetSalvage(Point cursor) => TryGetOwner(out Player owner) && !owner.dead
		&& Vector2.DistanceSquared(owner.Center, cursor.ToWorldCoordinates()) <= 560f * 560f
		&& CompanionSalvage.Capture(cursor) is not null;

	internal CompanionConversationResult RequestSalvage(Point cursor, Guid world, ulong fingerprint)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || IsBossChallenge || !NPC.active
			|| !TryGetOwner(out Player owner) || owner.dead || FindBoundSigil() is null
			|| Vector2.DistanceSquared(owner.Center, NPC.Center) > 560f * 560f
			|| CompanionInventorySync.IsPending(owner) || !CanTargetSalvage(cursor))
			return new(SoulmatesText.Get("TargetOrders.Invalid"), false);
		SalvageTarget? selected = CompanionSalvage.Capture(cursor);
		if (selected is null || selected.World != world || selected.Fingerprint != fingerprint)
			return new(SoulmatesText.Get("Salvage.Changed"), false);
		if (!CanSalvageNow(selected)) return new(SoulmatesText.Get("Salvage.Protected"), false);
		if (Profile.Energy < 5) return new(SoulmatesText.Get("TargetOrders.LowEnergy"), false);
		if (Profile.Mood < 15) return new(SoulmatesText.Get("TargetOrders.LowMood"), false);
		CancelAssignment();
		Profile.WorkPaused = false;
		Command = FollowCommand;
		pendingSalvage = CompanionSalvage.Nearby(selected, Profile, owner, NPC)
			.Where(CanSalvageNow).ToList();
		if (pendingSalvage.Count == 0) return new(SoulmatesText.Get("Salvage.Protected"), false);
		StartChoiceQuestion(CompanionQuestion.Salvage, playerRequested: true);
		SoulmatesFeedbackSystem.Record("salvage_requested", ("kind", selected.Kind.ToString()),
			("targets", pendingSalvage.Count), ("origin", "unknown"));
		return new(SoulmatesText.Get("Salvage.Question", pendingSalvage.Count, selected.Name), true);
	}

	private bool CanSalvageNow(SalvageTarget target)
	{
		if (!TryGetOwner(out Player owner) || owner.dead || FindBoundSigil() is null
			|| Vector2.DistanceSquared(owner.Center, target.Center) > 600f * 600f
			|| !CompanionSalvage.IsSafe(target, Profile, owner, NPC)) return false;
		if (target.Kind != SalvageKind.Gem || target.Type == TileID.ExposedGems) return true;
		Point point = target.Anchor;
		return IsExposedMiningTile(point) && CompanionNativeRules.CanPick(owner, point.X, point.Y, EffectivePickPower(out _));
	}

	private bool RespondToSalvage(CompanionAnswer answer)
	{
		bool areaConfirmation = pendingQuestion == CompanionQuestion.SalvageArea;
		List<SalvageTarget> captured = pendingSalvage.ToList();
		if (captured.Count == 0 || captured.Any(target => !CanSalvageNow(target))) {
			ClearChoiceQuestion(); SpeakLocalized("Salvage.Changed"); return false;
		}
		if (!areaConfirmation && answer == CompanionAnswer.Second) {
			ClearChoiceQuestion(); pendingSalvage = captured;
			StartChoiceQuestion(CompanionQuestion.SalvageArea, playerRequested: true);
			return true;
		}
		ClearChoiceQuestion();
		if (answer != CompanionAnswer.First) { SpeakLocalized("Salvage.Cancelled"); return true; }
		List<SalvageTarget> approved = areaConfirmation ? captured : [captured[0]];
		BeginJob(CompanionJob.Salvage);
		Profile.Routine = CompanionJob.None;
		jobOrigin = approved[0].Center;
		jobPlannedTotal = approved.Count;
		foreach (SalvageTarget target in approved) salvageWork.Enqueue(target);
		SpeakLocalized("Salvage.Started", approved.Count.ToString());
		ShowNativeEmote(EmoteID.ItemPickaxe, 120);
		SyncPackState();
		SoulmatesFeedbackSystem.Record("salvage_approved", ("targets", approved.Count), ("area", areaConfirmation));
		return true;
	}

	private void ClearSalvageWork()
	{
		salvageWork.Clear(); salvageDelay = salvageSkipped = 0; salvageDropCenter = Vector2.Zero;
	}
	private void UpdateSalvageJob()
	{
		if (Main.netMode == NetmodeID.MultiplayerClient) return;
		if (!TryGetOwner(out Player owner) || owner.dead || FindBoundSigil() is null || jobTimer > 7200) {
			CancelAssignment(); return;
		}
		if (salvageDelay > 0) {
			MoveTo(salvageDropCenter + new Vector2(0, -22), 4f, 0.06f);
			// Native drops remain real world items. The shared cargo transaction owns pickup.
			CollectNearbyLooseItems(salvageDropCenter, 48f, 4);
			--salvageDelay; return;
		}
		if (salvageWork.Count == 0) {
			string line = SoulmatesText.Get("Salvage.Completed", jobCount, salvageSkipped);
			SpeakLocalized("Salvage.Completed", jobCount.ToString(), salvageSkipped.ToString()); CompleteJob(line, jobCount > 0); return;
		}
		SalvageTarget target = salvageWork.Peek();
		if (!CanSalvageNow(target)) {
			salvageWork.Dequeue(); salvageSkipped++; SpeakLocalized("Salvage.Changed"); return;
		}
		if (Profile.PackLoad >= Profile.PackCapacity || Profile.ResourceLoad >= CompanionProfile.MaximumResourceSlots) {
			string line = SoulmatesText.Get("Jobs.PackFull");
			ShowSpeech(line); CompleteJob(line, false); return;
		}
		MoveTo(target.Center + new Vector2(0, -22), 5.5f, 0.065f);
		if (Vector2.DistanceSquared(NPC.Center, target.Center) > 64f * 64f || jobTimer % 24 != 0) return;
		// Recheck immediately before the sole mutation, including every part of the multi-tile.
		if (!CanSalvageNow(target) || CompanionInventorySync.IsPending(owner)) return;
		WorldGen.KillTile(target.Anchor.X, target.Anchor.Y, noItem: false);
		if (Main.netMode == NetmodeID.Server)
			NetMessage.SendTileSquare(-1, target.Bounds.X - 1, target.Bounds.Y - 1, target.Bounds.Width + 2, target.Bounds.Height + 2);
		salvageWork.Dequeue();
		bool removed = target.Cells.All(cell => !Main.tile[cell.Position].HasTile);
		if (!removed) { salvageSkipped++; SpeakLocalized("Salvage.Changed"); return; }
		jobCount++; Profile.Energy = Math.Max(0, Profile.Energy - 1);
		salvageDropCenter = target.Center; salvageDelay = 36;
		ShowNativeEmote(EmoteID.ItemPickaxe, 90);
		SyncPackState();
		SoulmatesFeedbackSystem.Record("salvage_completed_object", ("kind", target.Kind.ToString()), ("tile_type", target.Type),
			("remaining", salvageWork.Count));
	}
}
