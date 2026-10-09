#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Soulmates.Common;
using Soulmates.Content.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameInput;
using Soulmates.Common.UI;

namespace Soulmates.Content.NPCs;

public sealed partial class SoulboundCompanion
{
	private readonly Queue<string> diagnosticEvents = new();
	private int diagnosticTimer;
	private string lastDiagnosticState = "";
	private void TraceDiagnostic(string value)
	{
		if (diagnosticEvents.Count >= 24) diagnosticEvents.Dequeue();
		diagnosticEvents.Enqueue($"{Main.GameUpdateCount}: {value}");
	}
	private void UpdateDiagnostics()
	{
		if (++diagnosticTimer < 60) return;
		diagnosticTimer = 0;
		string state = $"{ActivityLane} / {FeedbackActivity} / critters={CritterGate()} / target={critterTarget?.type ?? 0}";
		if (state != lastDiagnosticState) { TraceDiagnostic(state); lastDiagnosticState = state; }
	}
	internal string CritterGate()
	{
		if (!TryGetOwner(out Player owner) || owner.dead) return "owner unavailable";
		if (CompanionInventorySync.IsPending(owner)) return "inventory transaction pending";
		if (Profile.WorkPaused) return "paused";
		if (guardianTarget >= 0) return "defending";
		if (Command == StayCommand) return "stay command";
		if (activeJob != CompanionJob.None || Profile.Routine != CompanionJob.None) return "explicit work active";
		if (autonomyActivity != AutonomyActivity.None) return "automatic work active";
		if (socialNpcTarget >= 0) return "resident visit active";
		if (Profile.CritterMode == CompanionCritterMode.Off) return "disabled";
		if (Profile.CritterMode == CompanionCritterMode.Watch) return Profile.AutonomyEnabled ? "watch only" : "manual only";
		if (HasPendingInitiative) return "awaiting work answer";
		var kind = Profile.CritterMode == CompanionCritterMode.Collect ? CompanionInitiativeKind.CritterCollect : CompanionInitiativeKind.CritterCompany;
		if (HasPendingQuestion && !directedCritterVisit && Profile.GetInitiativePolicy(kind) != CompanionInitiativePolicy.Always) return "awaiting conversation answer";
		if (directedCritterVisit) return "directed visit";
		if (!Profile.AutonomyEnabled) return "manual only";
		if (autonomyRecoveryPaused) return "recovering energy";
		if (!CompanionAbilityRegistry.Find(kind)!.HasStamina(Profile)) return "low energy or mood";
		if (Profile.GetInitiativePolicy(kind) == CompanionInitiativePolicy.Never) return "permission: never";
		if (Profile.CritterMode == CompanionCritterMode.Company && CritterCompanyCount() >= (Profile.IsAether ? 3 : 1)) return "company capacity reached";
		if (critterTarget is not null) return "visiting";
		if (critterDecisionTimer > 0) return $"visit cooldown: {critterDecisionTimer} ticks";
		if (!attention.IsReady(kind)) return "permission cooldown";
		return Profile.GetInitiativePolicy(kind) == CompanionInitiativePolicy.Ask ? "ready to ask" : "ready to visit";
	}
	private string CritterCandidateReason(NPC target)
	{
		string naturalRejection = CompanionCritters.NaturalRejectionReason(target);
		if (naturalRejection.Length > 0) return naturalRejection;
		if (!CompanionCritters.IsCommon(target)) return "gold critter protected";
		if (target.whoAmI < 0 || target.whoAmI >= Main.maxNPCs || !ReferenceEquals(Main.npc[target.whoAmI], target))
			return "NPC identity replaced";
		if (directedCritterVisit && target.type != directedCritterType) return "directed species changed";
		if (target.TryGetGlobalNPC(out CompanionCritterCompany company) && company.IsAssigned)
			return company.BelongsTo(this) ? "following this companion" : "following another companion";
		if (!TryGetOwner(out Player owner) || Microsoft.Xna.Framework.Vector2.DistanceSquared(target.Center, owner.Center) > 320f * 320f) return "outside owner's 20-tile range";
		if (critterTarget != target && !directedCritterVisit
			&& Microsoft.Xna.Framework.Vector2.DistanceSquared(target.Center, NPC.Center) >= 320f * 320f)
			return "outside companion's automatic 20-tile search";
		if (!CanSeeCritter(target)) return "line of sight blocked";
		if (Profile.CritterMode == CompanionCritterMode.Company && !CompanionCritters.SupportsCompany(target.type)) return "species has no company movement";
		if (Profile.CritterMode == CompanionCritterMode.Collect) {
			if (!CompanionCritters.CanUseNet(target, EffectiveCritterNet())) return "lava-proof net required";
			if (Profile.GetStorableAmount(new Item(target.catchItem)) < 1) return "equipment cargo full or item limit";
		}
		if (!directedCritterVisit && deferredCritterTicks > 0 && ReferenceEquals(target, deferredCritter)
			&& target.type == deferredCritterType) return "previous visit timed out";
		return "eligible";
	}
	internal string DiagnosticText()
	{
		var text = new StringBuilder();
		text.AppendLine($"Soulmates {Mod.Version} | Terraria {Main.versionNumber} | tModLoader {BuildInfo.tMLVersion} | {Main.netMode switch { 0 => "single-player authority", 1 => "client view (server owns decisions)", _ => "server authority" }}");
		text.AppendLine($"Lane: {ActivityLane}; activity: {FeedbackActivity}; command: {FeedbackCommand}");
		text.AppendLine($"Energy {Profile.Energy}/100; mood {Profile.Mood}/100; level {Profile.Level}; autonomy {Profile.AutonomyEnabled}; paused {Profile.WorkPaused}");
		text.AppendLine($"Job: {activeJob}; routine: {Profile.Routine}; progress {jobCount}/{jobPlannedTotal}; recovery {jobRecoveryPaused}/{autonomyRecoveryPaused}");
		text.AppendLine($"Question: {PendingQuestion} ({QuestionTicks}); initiative: {(HasPendingInitiative ? PendingInitiativeKind.ToString() : "none")} ({PendingInitiativeTicks})");
		text.AppendLine($"Critters: {Profile.CritterMode}; {CritterGate()}; company {CritterCompanyCount()}/{(Profile.IsAether ? 3 : 1)}");
		text.AppendLine($"Visit: {(critterTarget is null ? "none" : $"NPC {critterTarget.whoAmI}, type {critterTarget.type}")}; directed {directedCritterVisit}; ticks {critterVisitTicks}; net {EffectiveCritterNet().type}; catch cooldown {insectCatchCooldown}");
		text.AppendLine($"Loss: cooldown {critterLossCooldown}; queued {pendingCritterLossKey.Length > 0}; care {pendingCritterCareTicks}");
		foreach (var ability in CompanionAbilityRegistry.All)
			text.AppendLine($"Rule {ability.Kind}: {Profile.GetInitiativePolicy(ability.Kind)}; stamina {ability.HasStamina(Profile)}; attention ready {attention.IsReady(ability.Kind)}");
		text.AppendLine($"Cargo: equipment {Profile.PackLoad}/{Profile.PackCapacity}; resources {Profile.ResourceLoad}; per-type limit {Profile.ResourceCarryLimit}; wallet {Profile.WalletCopper}");
		text.AppendLine($"Pet item: {Profile.PetItemType}; supported {CompanionPets.VisualFor(Profile.PetItemType) > 0}; carried {CompanionPets.IsCarried(Profile, Profile.PetItemType)}");
		text.AppendLine($"Pet inventory: {Profile.PetItems.Count}/{CompanionProfile.MaximumPetSlots}; {string.Join(", ", Profile.PetItems.Select(item => item.type))}");
		int pets = 0;
		foreach (Projectile projectile in Main.ActiveProjectiles)
			if (projectile.ModProjectile is CompanionFamiliar familiar && familiar.BelongsTo(this)) {
				pets++;
				text.AppendLine($"Familiar {projectile.whoAmI}: item {(int)projectile.ai[1]}, frame {projectile.frame}, life {projectile.timeLeft}, distance {Microsoft.Xna.Framework.Vector2.Distance(projectile.Center, NPC.Center):0}px");
			}
		text.AppendLine($"Own familiar count: {pets}. Supported items: {string.Join(", ", CompanionPets.SupportedItems)}");
		if (!Main.dedServ) {
			text.AppendLine($"Details shortcut: {CompanionControls.DetailsKeys}; input mode: {PlayerInput.CurrentInputMode}");
			text.AppendLine($"Input gates: typing {SoulmatesUIInput.IsTyping}; captured {SoulmatesUIInput.IsCaptured}; cursor item {!Main.mouseItem.IsAir}; NPC dialogue {Main.LocalPlayer.talkNPC >= 0}");
		}
		text.AppendLine("Nearby critters (at most 12, within 30 tiles):");
		int shown = 0;
		foreach (NPC target in Main.ActiveNPCs) {
			if (shown == 12) break;
			if (target == NPC || !CompanionCritters.IsCritter(target)
				|| Microsoft.Xna.Framework.Vector2.DistanceSquared(target.Center, NPC.Center) > 480f * 480f) continue;
			shown++;
			text.AppendLine($"NPC {target.whoAmI}, type {target.type}: {CritterCandidateReason(target)}");
			float ownerDistance = TryGetOwner(out Player nearbyOwner)
				? Microsoft.Xna.Framework.Vector2.Distance(target.Center, nearbyOwner.Center) : -1f;
			text.AppendLine($"  Native: catch item {target.catchItem}; friendly {target.friendly}; life {target.life}; damage {target.damage}; release owner {target.releaseOwner}; statue {target.SpawnedFromStatue}");
			text.AppendLine($"  Distance: companion {Microsoft.Xna.Framework.Vector2.Distance(target.Center, NPC.Center):0}px; owner {ownerDistance:0}px");
		}
		if (shown == 0) text.AppendLine("none in range");
		text.AppendLine("Recent local transitions (not saved automatically):");
		foreach (string entry in diagnosticEvents) text.AppendLine(entry);
		return text.ToString();
	}
}
