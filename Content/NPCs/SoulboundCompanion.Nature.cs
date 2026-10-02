#nullable enable
using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Soulmates.Common;
using Soulmates.Common.Dialogue;
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
	private int critterDecisionTimer;
	private int critterNoticeCooldown;
	private int critterWatchCooldown;
	private int critterVisitTicks;
	private int critterLossCooldown;
	private string pendingCritterLossKey = "";
	private string pendingCritterLossName = "";
	private NPC? greetedCritter;
	private int greetedCritterType;
	private NPC? pendingCritterNotice;
	private int pendingCritterNoticeType;
	private string pendingCritterNoticeKey = "";
	private NPC? critterTarget;
	private bool directedCritterVisit;
	private int directedCritterType;
	public bool IsAttendingCritter => critterTarget is { active: true } && CanAttendCritters();

	private void UpdateNatureCompanions()
	{
		if (insectCatchCooldown > 0) insectCatchCooldown--;
		if (critterDecisionTimer > 0) critterDecisionTimer--;
		if (critterNoticeCooldown > 0) critterNoticeCooldown--;
		if (critterWatchCooldown > 0) critterWatchCooldown--;
		if (critterLossCooldown > 0) critterLossCooldown--;
		if (critterLossCooldown == 0) pendingCritterLossKey = pendingCritterLossName = "";
		UpdateCritterLossSpeech();
		UpdateCritterNoticeSpeech();
		if (!Profile.WorkPaused && (!CanAttendCritters() || critterTarget is not null && --critterVisitTicks <= 0)) {
			critterTarget = null;
			directedCritterVisit = false;
		}
		UpdateCritterWatch();

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
		if (!Profile.IsAether) return false;
		return TryCatchCritter(insectsOnly: true);
	}

	private bool TryCatchCritter(bool insectsOnly, NPC? selected = null)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || insectCatchCooldown > 0)
			return false;
		insectCatchCooldown = 90;
		Item? net = CarriedCritterNet();
		if (net is null) return false;
		foreach (NPC insect in Main.ActiveNPCs) {
			if (selected is not null && !ReferenceEquals(selected, insect)
				|| !CompanionCritters.IsCommon(insect) || insectsOnly && !CompanionInsects.CanCatch(insect)
				|| !CompanionCritters.CanUseNet(insect, net)
				|| insect.TryGetGlobalNPC(out CompanionCritterCompany company) && company.IsAssigned
				|| Vector2.DistanceSquared(insect.Center, NPC.Center) > 96f * 96f
				|| Vector2.DistanceSquared(insect.Center, Owner.Center) > 320f * 320f
				|| !Collision.CanHitLine(NPC.Center, 1, 1, insect.Center, 1, 1)) continue;
			var testDrop = new Item(insect.catchItem);
			if (Profile.GetStorableAmount(testDrop) < 1) {
				CritterFeedback("Social.Critters.Full");
				continue;
			}
			if (!CompanionCatchReceipt.Begin(insect, Owner)) continue;
			bool caught;
			Item? nativeDrop;
			try {
				caught = Terraria.NPC.CheckCatchNPC(insect, insect.Hitbox, net, Owner,
					ItemID.Sets.LavaproofCatchingTool[net.type]) && !insect.active;
			}
			finally { nativeDrop = CompanionCatchReceipt.End(); }
			insectCatchCooldown = 300;
			if (!caught) return false;
			int collected = 0;
			int index = nativeDrop is null ? -1 : Array.IndexOf(Main.item, nativeDrop);
			if (nativeDrop is { active: true } && index >= 0 && index < Main.maxItems) {
				int type = nativeDrop.type;
				int moved = StoreLooseItem(nativeDrop);
				collected = moved;
				if (Main.netMode == NetmodeID.Server && moved > 0)
					NetMessage.SendData(MessageID.SyncItem, -1, -1, null, index);
				if (moved > 0) {
					if (CompanionInsects.NpcForItem(type) >= 0
						&& Profile.Memories.All(memory => memory.Kind != CompanionMemoryKind.InsectCompanion))
						Profile.Remember(CompanionMemoryKind.InsectCompanion, detail: Lang.GetItemNameValue(type));
					Profile.Energy = Math.Max(0, Profile.Energy - 1);
					SyncPackState();
					SoulmatesFeedbackSystem.Record("critter_caught", ("item_type", type), ("amount", moved));
				}
			}
			ShowNativeEmote(collected > 0 ? EmoteID.ItemBugNet : EmoteID.EmoteConfused, 120);
			if (collected > 0) CritterFeedback("Social.Critters.Caught", insect.TypeName);
			else CritterFeedback("Social.Critters.WorldDrop");
			return true;
		}
		return false;
	}

	private Item? CarriedCritterNet() => Profile.CarriedItems
		.Where(item => !item.IsAir && ItemID.Sets.CatchingTool[item.type])
		.OrderByDescending(item => ItemID.Sets.LavaproofCatchingTool[item.type]).FirstOrDefault();

	private bool CanAttendCritters() => Profile.CritterMode != CompanionCritterMode.Off && CanVisitCritters();
	private bool CanVisitCritters() => Profile.AutonomyEnabled && !Profile.WorkPaused && Command != StayCommand
		&& activeJob == CompanionJob.None && Profile.Routine == CompanionJob.None
		&& autonomyActivity == AutonomyActivity.None && !HasPendingInitiative && !HasPendingQuestion && guardianTarget < 0
		&& socialNpcTarget < 0 && Owner.active && !Owner.dead
		&& (directedCritterVisit || Profile.Energy >= 20 && Profile.Mood >= 20);

	private void UpdateCritterWatch()
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || !Profile.AutonomyEnabled
			|| Profile.CritterMode == CompanionCritterMode.Off || critterWatchCooldown > 0 || pendingCritterNotice is not null
			|| guardianTarget >= 0 || HasPendingInitiative || HasPendingQuestion || !Owner.active || Owner.dead)
			return;
		NPC? nearest = null;
		float distance = 320f * 320f;
		foreach (NPC candidate in Main.ActiveNPCs) {
			float current = Vector2.DistanceSquared(candidate.Center, NPC.Center);
			if (current >= distance || !CompanionCritters.IsNatural(candidate)
				|| Vector2.DistanceSquared(candidate.Center, Owner.Center) > 360f * 360f
				|| !Collision.CanHitLine(NPC.Center, 1, 1, candidate.Center, 1, 1)) continue;
			distance = current;
			nearest = candidate;
		}
		if (nearest is null) return;
		bool greeting = IsFirstCritterGreeting(nearest);
		ShowNativeEmote(greeting ? EmoteID.EmoteWink : CompanionCritters.EmoteFor(nearest), 120);
		critterWatchCooldown = 600;
		pendingCritterNotice = nearest;
		pendingCritterNoticeType = nearest.type;
		pendingCritterNoticeKey = $"Social.Critters.{(greeting ? "Greeting" : "Notice")}.{Profile.Personality}";
		UpdateCritterNoticeSpeech();
		SoulmatesFeedbackSystem.Record("critter_observed", ("npc_type", nearest.type));
	}

	private void UpdateCritterNoticeSpeech()
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || pendingCritterNotice is not { } target) return;
		if (Profile.CritterMode == CompanionCritterMode.Off || !Profile.AutonomyEnabled
			|| target.type != pendingCritterNoticeType || !CompanionCritters.IsNatural(target)
			|| Vector2.DistanceSquared(target.Center, NPC.Center) > 400f * 400f
			|| !Owner.active || Owner.dead) {
			pendingCritterNotice = null;
			pendingCritterNoticeKey = "";
			return;
		}
		if (speechTimer > 0 || critterNoticeCooldown > 0 || guardianTarget >= 0
			|| HasPendingQuestion || HasPendingInitiative || pendingCritterLossKey.Length > 0) return;
		CritterFeedback(pendingCritterNoticeKey, target.TypeName);
		pendingCritterNotice = null;
		pendingCritterNoticeKey = "";
	}

	private bool UpdateCritterActivity()
	{
		if (Profile.WorkPaused) return false;
		if (Profile.CritterMode == CompanionCritterMode.Watch) { critterTarget = null; return false; }
		if (!CanAttendCritters()) { critterTarget = null; return false; }
		bool collect = Profile.CritterMode == CompanionCritterMode.Collect;
		Item? net = collect ? CarriedCritterNet() : null;
		if (collect && net is null) { critterTarget = null; CritterFeedback("Social.Critters.NeedNet"); return false; }
		if (critterTarget is not null && !IsCritterTarget(critterTarget, net)) {
			critterTarget = null;
			if (directedCritterVisit) {
				directedCritterVisit = false;
				critterDecisionTimer = 300;
				NPC.netUpdate = true;
				CritterFeedback("TargetOrders.TargetLost");
				return false;
			}
		}
		if (critterTarget is null && critterDecisionTimer == 0) {
			critterDecisionTimer = 120;
			float nearest = 240f * 240f;
			foreach (NPC candidate in Main.ActiveNPCs) {
				float distance = Vector2.DistanceSquared(NPC.Center, candidate.Center);
				if (distance < nearest && IsCritterTarget(candidate, net)) { nearest = distance; critterTarget = candidate; }
			}
			if (critterTarget is not null) critterVisitTicks = 360;
		}
		if (critterTarget is not { } target) return false;
		if (Profile.CritterMode == CompanionCritterMode.Company && CritterCompanyCount() >= (Profile.IsAether ? 3 : 1)) {
			critterTarget = null;
			return false;
		}
		if (Vector2.DistanceSquared(NPC.Center, target.Center) > 72f * 72f) {
			MoveTo(target.Center + new Vector2(0, -24), 3f, 0.065f);
			return true;
		}
		if (Main.netMode != NetmodeID.MultiplayerClient) {
			if (collect) {
				if (insectCatchCooldown > 0) { MoveTo(target.Center + new Vector2(0, -24), 2f, 0.06f); return true; }
				TryCatchCritter(insectsOnly: false, selected: target);
			}
			else if (target.TryGetGlobalNPC(out CompanionCritterCompany company) && company.TryJoin(target, this)) {
				ShowNativeEmote(EmoteID.EmotionLove, 120);
				EmoteBubble.NewBubble(EmoteID.EmoteHappiness, new WorldUIAnchor(target), 90);
				CritterFeedback(IsFirstCritterGreeting(target)
					? $"Social.Critters.Greeting.{Profile.Personality}" : "Social.Critters.Joined", target.TypeName);
				SoulmatesFeedbackSystem.Record("critter_company", ("npc_type", target.type));
			}
		}
		critterTarget = null;
		directedCritterVisit = false;
		critterDecisionTimer = 300;
		NPC.netUpdate = true;
		return false;
	}

	private bool IsCritterTarget(NPC target, Item? net)
	{
		if (directedCritterVisit && target.type != directedCritterType
			|| !CompanionCritters.IsCommon(target) || Vector2.DistanceSquared(target.Center, Owner.Center) > 320f * 320f
			|| !Collision.CanHitLine(NPC.Center, 1, 1, target.Center, 1, 1)) return false;
		if (target.TryGetGlobalNPC(out CompanionCritterCompany company) && company.IsAssigned) return false;
		return Profile.CritterMode switch {
			CompanionCritterMode.Collect => net is not null && CompanionCritters.CanUseNet(target, net)
				&& HasCritterRoom(target),
			CompanionCritterMode.Company => CompanionCritters.CanKeepCompany(target),
			_ => true
		};
	}

	private bool HasCritterRoom(NPC target)
	{
		if (Profile.GetStorableAmount(new Item(target.catchItem)) > 0) return true;
		CritterFeedback("Social.Critters.Full");
		return false;
	}

	private int CritterCompanyCount()
	{
		int count = 0;
		foreach (NPC critter in Main.ActiveNPCs)
			if (critter.TryGetGlobalNPC(out CompanionCritterCompany company) && company.BelongsTo(this)) count++;
		return count;
	}

	internal bool CanInviteCritter(NPC critter) => NPC.active && CanAttendCritters()
		&& Profile.CritterMode == CompanionCritterMode.Company && CritterCompanyCount() < (Profile.IsAether ? 3 : 1)
		&& Vector2.DistanceSquared(critter.Center, NPC.Center) <= 96f * 96f
		&& Vector2.DistanceSquared(critter.Center, Owner.Center) <= 320f * 320f
		&& Collision.CanHitLine(NPC.Center, 1, 1, critter.Center, 1, 1);

	private void ReleaseCritterCompany()
	{
		foreach (NPC critter in Main.ActiveNPCs)
			if (critter.TryGetGlobalNPC(out CompanionCritterCompany company) && company.BelongsTo(this)) company.Leave(critter);
		critterTarget = null;
		directedCritterVisit = false;
	}

	private void CritterFeedback(string key, string argument = "")
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || critterNoticeCooldown > 0 || speechTimer > 0) return;
		critterNoticeCooldown = 1200;
		SpeakLocalized(key, argument);
	}

	private bool TrySetCritterMode(CompanionQuickAction action, out CompanionConversationResult result)
	{
		result = default;
		CompanionCritterMode? mode = action switch {
			CompanionQuickAction.CritterWatch => CompanionCritterMode.Watch,
			CompanionQuickAction.CritterCompany => CompanionCritterMode.Company,
			CompanionQuickAction.CritterCollect => CompanionCritterMode.Collect,
			CompanionQuickAction.CritterOff => CompanionCritterMode.Off,
			_ => null
		};
		if (mode is null) return false;
		if (mode == CompanionCritterMode.Collect && CarriedCritterNet() is null) {
			SoulmatesFeedbackSystem.Record("critter_mode_rejected", ("mode", mode.ToString()), ("reason", "no_carried_net"));
			result = new CompanionConversationResult(SoulmatesText.Get("Social.Critters.NeedNet"), false);
			return true;
		}
		using var inventorySync = new CompanionInventorySync(Owner);
		Profile.CritterMode = mode.Value;
		if (mode != CompanionCritterMode.Company) ReleaseCritterCompany();
		critterTarget = null;
		directedCritterVisit = false;
		pendingCritterNotice = null;
		pendingCritterNoticeKey = "";
		critterDecisionTimer = critterNoticeCooldown = critterWatchCooldown = 0;
		SyncPackState();
		SoulmatesFeedbackSystem.Record("critter_mode", ("mode", mode.ToString()));
		bool busy = mode == CompanionCritterMode.Watch ? !Profile.AutonomyEnabled
			: mode != CompanionCritterMode.Off && !CanAttendCritters();
		result = new CompanionConversationResult(SoulmatesText.Get(busy
			? "Social.Critters.Busy" : $"Social.Critters.Modes.{mode}"), true);
		return true;
	}

	internal bool CanTargetNpc(int index) => index >= 0 && index < Main.maxNPCs
		&& Main.npc[index] is { active: true, life: > 0 } target && target != NPC
		&& Vector2.DistanceSquared(target.Center, Owner.Center) <= 600f * 600f;

	internal bool CanTargetCritterCompany(int index) => CanTargetNpc(index)
		&& CompanionCritters.CanKeepCompany(Main.npc[index])
		&& (!Main.npc[index].TryGetGlobalNPC(out CompanionCritterCompany company) || !company.IsAssigned)
		&& CritterCompanyCount() < (Profile.IsAether ? 3 : 1)
		&& Vector2.DistanceSquared(Main.npc[index].Center, Owner.Center) <= 320f * 320f
		&& Collision.CanHitLine(NPC.Center, 1, 1, Main.npc[index].Center, 1, 1);

	internal bool CanTargetCritterCollect(int index) => CanTargetNpc(index)
		&& CompanionCritters.IsCommon(Main.npc[index])
		&& (!Main.npc[index].TryGetGlobalNPC(out CompanionCritterCompany company) || !company.IsAssigned)
		&& Vector2.DistanceSquared(Main.npc[index].Center, Owner.Center) <= 320f * 320f
		&& Collision.CanHitLine(NPC.Center, 1, 1, Main.npc[index].Center, 1, 1);

	internal CompanionConversationResult PerformNpcContext(CompanionNpcAction action, int index, int expectedType)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || !Enum.IsDefined(action) || !CanTargetNpc(index)
			|| Main.npc[index].type != expectedType)
			return new CompanionConversationResult(SoulmatesText.Get("TargetOrders.TargetLost"), false);
		NPC target = Main.npc[index];
		if (action is CompanionNpcAction.Company or CompanionNpcAction.Collect) {
			bool collect = action == CompanionNpcAction.Collect;
			if (!(collect ? CanTargetCritterCollect(index) : CanTargetCritterCompany(index)))
				return new(SoulmatesText.Get("TargetOrders.Invalid"), false);
			if (!Profile.AutonomyEnabled || guardianTarget >= 0 || HasPendingQuestion || HasPendingInitiative)
				return new CompanionConversationResult(SoulmatesText.Get("Social.Critters.DirectBusy"), false);
			Item? net = collect ? CarriedCritterNet() : null;
			if (collect && net is null) return new(SoulmatesText.Get("Social.Critters.NeedNet"), false);
			if (collect && (!CompanionCritters.CanUseNet(target, net!) || !HasCritterRoom(target)))
				return new(SoulmatesText.Get("TargetOrders.Invalid"), false);
			SetCommand(stay: false);
			Profile.CritterMode = collect ? CompanionCritterMode.Collect : CompanionCritterMode.Company;
			if (collect) ReleaseCritterCompany();
			directedCritterVisit = true;
			directedCritterType = target.type;
			critterTarget = target;
			critterVisitTicks = 360;
			critterDecisionTimer = 120;
			SyncPackState();
			return new CompanionConversationResult(SoulmatesText.Get(collect ? "Social.Critters.CatchSelected"
				: "Social.Critters.Inviting", target.TypeName), true);
		}
		string key = CompanionCritters.IsNatural(target) ? "TargetOrders.LookCritter"
			: target.townNPC ? "TargetOrders.LookResident"
			: !target.friendly && target.damage > 0 ? "TargetOrders.LookThreat" : "TargetOrders.LookCreature";
		ShowNativeEmote(CompanionCritters.IsNatural(target) ? CompanionCritters.EmoteFor(target)
			: target.townNPC ? EmoteID.EmoteHappiness : target.friendly ? EmoteID.EmotionAlert : EmoteID.EmoteFear, 120);
		SoulmatesFeedbackSystem.Record("npc_observation", ("npc_type", target.type));
		return new CompanionConversationResult(SoulmatesText.Get(key, target.FullName), true);
	}

	private bool IsFirstCritterGreeting(NPC target)
	{
		if (ReferenceEquals(greetedCritter, target) && greetedCritterType == target.type) return false;
		greetedCritter = target;
		greetedCritterType = target.type;
		if (Profile.Memories.All(memory => memory.Kind != CompanionMemoryKind.CritterMet || memory.Detail != target.TypeName)) {
			Profile.Remember(CompanionMemoryKind.CritterMet, detail: target.TypeName);
			SyncPackState();
		}
		return true;
	}

	internal void ObserveCritterLoss(NPC target, bool playerAttack)
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || critterLossCooldown > 0 || !CompanionCritters.IsCritter(target)
			|| !target.friendly || target.damage > 0 || target.SpawnedFromStatue || !TryGetOwner(out Player owner)
			|| owner.dead || Vector2.DistanceSquared(target.Center, NPC.Center) > 400f * 400f
			|| !Collision.CanHitLine(NPC.Center, 1, 1, target.Center, 1, 1)) return;
		critterLossCooldown = 900;
		Profile.Remember(CompanionMemoryKind.CritterLost, detail: target.TypeName);
		SyncPackState();
		bool angry = playerAttack && Profile.Personality is CompanionPersonality.Brave or CompanionPersonality.Mischievous;
		ShowNativeEmote(angry ? EmoteID.EmotionAnger : EmoteID.EmoteSadness, 180);
		pendingCritterLossKey = $"Social.Critters.{(playerAttack ? "Harmed" : "Loss")}.{Profile.Personality}";
		pendingCritterLossName = target.TypeName;
		UpdateCritterLossSpeech();
		SoulmatesFeedbackSystem.Record("critter_loss", ("npc_type", target.type), ("player_attack_credit", playerAttack));
	}

	private void UpdateCritterLossSpeech()
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || pendingCritterLossKey.Length == 0 || speechTimer > 0
			|| HasPendingQuestion || HasPendingInitiative) return;
		SpeakLocalized(pendingCritterLossKey, pendingCritterLossName);
		pendingCritterLossKey = pendingCritterLossName = "";
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
