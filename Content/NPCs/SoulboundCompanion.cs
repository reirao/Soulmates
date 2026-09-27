using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Soulmates.Common;
using Soulmates.Common.UI;
using Soulmates.Content.Items;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulmates.Content.NPCs;

public sealed class SoulboundCompanion : ModNPC
{
	private enum BrainState
	{
		Follow,
		Idle,
		Wander,
		Inspect,
		CatchUp,
		Stay
	}

	private const float FollowCommand = 0f;
	private const float StayCommand = 1f;
	private BrainState brainState;
	private int stateTimer;
	private int facing = 1;
	private int facingCooldown;
	private Vector2 idleTarget;
	private float bobSeed;
	private CompanionJob activeJob;
	private int jobTimer;
	private int jobCount;
	private Point jobTarget;
	private bool hasJobTarget;
	private int recoveryTimer;
	private int gatherPause;
	private int areaEmptyTimer;
	private int revealTimer;
	private Vector2 jobOrigin;
	private readonly HashSet<Point> failedMiningTargets = [];

	public CompanionProfile Profile { get; set; } = new();
	private Player Owner => Main.player[(int)NPC.ai[0]];
	private ref float Command => ref NPC.ai[1];
	public string CommandName => SoulmatesText.Get(Command == StayCommand ? "Status.Stay" : "Status.Follow");
	public CompanionJob CurrentJob => activeJob;
	public string CurrentJobName => activeJob != CompanionJob.None
		? activeJob switch {
			CompanionJob.Mine => SoulmatesText.Get("Status.Mining", jobCount),
			CompanionJob.Gather => SoulmatesText.Get("Status.Gathering", jobCount),
			CompanionJob.FindTreasure => SoulmatesText.Get("Status.SensingTreasure"),
			_ => SoulmatesText.EnumName(activeJob)
		}
		: Profile.Routine != CompanionJob.None
			? SoulmatesText.Get("Status.Assignment", SoulmatesText.EnumName(Profile.Routine))
			: SoulmatesText.Get("Status.Ready");
	public int CurrentJobRadius => (activeJob != CompanionJob.None ? activeJob : Profile.Routine) switch {
		CompanionJob.Mine => MiningRadiusTiles,
		CompanionJob.Gather => GatheringRadiusTiles,
		CompanionJob.FindTreasure => TreasureRadiusTiles,
		_ => 0
	};
	private int MiningRadiusTiles => Profile.Trinket == CompanionTrinket.DelverCharm ? 34 : 26;
	private int GatheringRadiusTiles => Profile.Trinket == CompanionTrinket.HearthRibbon ? 42 : 30;
	private int TreasureRadiusTiles => Profile.Trinket == CompanionTrinket.StarfinderBell ? 90 : 55;

	public override void SetStaticDefaults()
	{
		Main.npcFrameCount[Type] = 1;
	}

	public override void SetDefaults()
	{
		NPC.width = 42;
		NPC.height = 46;
		NPC.lifeMax = 250;
		NPC.damage = 0;
		NPC.defense = 10;
		NPC.friendly = true;
		NPC.noGravity = true;
		NPC.noTileCollide = true;
		NPC.dontTakeDamage = true;
		NPC.netAlways = true;
		bobSeed = Main.rand.NextFloat(MathHelper.TwoPi);
	}

	public override bool CheckActive() => !TryGetOwner(out Player owner) || owner.dead;

	public override bool CanChat() => TryGetOwner(out Player owner) && owner.whoAmI == Main.myPlayer;

	public override bool PreHoverInteract(bool mouseIntersects)
	{
		if (!mouseIntersects || !Main.mouseRight || !Main.mouseRightRelease)
			return true;
		if (TryGetOwner(out Player owner) && owner.whoAmI == Main.myPlayer && FindBoundSigil() is { } sigil) {
			ModContent.GetInstance<TalkModeSystem>().Open(sigil, this);
			Main.LocalPlayer.mouseInterface = true;
			Main.mouseRightRelease = false;
		}
		return false;
	}

	public override void AI()
	{
		if (!TryGetOwner(out Player owner) || owner.dead) {
			NPC.active = false;
			return;
		}

		NPC.GivenName = Profile.Name;
		Lighting.AddLight(NPC.Center, Profile.EssenceColor.ToVector3() * 1.15f);
		RevealSurroundings();
		ResumeAssignment();
		UpdateAuraDust();
		if (activeJob != CompanionJob.None) {
			UpdateJob();
			NPC.rotation = MathHelper.Lerp(NPC.rotation, NPC.velocity.X * 0.025f, 0.08f);
			UpdateFacing();
			return;
		}

		if (Command == StayCommand) {
			if (brainState != BrainState.Stay) {
				brainState = BrainState.Stay;
				idleTarget = NPC.Center;
			}
			MoveTo(idleTarget + new Vector2(0f, IdleBob()), 2.2f, 0.04f);
			if (++recoveryTimer >= 300) {
				recoveryTimer = 0;
				Profile.Energy = Math.Clamp(Profile.Energy + (Profile.Trinket == CompanionTrinket.HearthRibbon ? 3 : 2), 0, 100);
				Profile.Mood = Math.Clamp(Profile.Mood + 1, 0, 100);
				SyncProfileToBoundSigil();
			}
			UpdateFacing();
			return;
		}
		recoveryTimer = 0;

		Vector2 followTarget = Owner.Center + new Vector2(-Owner.direction * 66f, -58f);
		float distance = Vector2.Distance(NPC.Center, followTarget);
		if (distance > 1200f) {
			NPC.Center = followTarget;
			NPC.velocity = Vector2.Zero;
			brainState = BrainState.Follow;
			stateTimer = 60;
			NPC.netUpdate = true;
			return;
		}

		if (distance > 340f) {
			brainState = BrainState.CatchUp;
			stateTimer = 45;
		}

		if (stateTimer-- <= 0)
			ChooseNextState(distance, followTarget);

		switch (brainState) {
			case BrainState.Idle:
				MoveTo(followTarget + new Vector2(0f, IdleBob()), 2.4f, 0.035f);
				break;
			case BrainState.Wander:
				MoveTo(idleTarget + new Vector2(0f, IdleBob() * 0.5f), 4.2f, 0.055f);
				break;
			case BrainState.Inspect:
				Vector2 inspectOffset = new Vector2(MathF.Sin((stateTimer + bobSeed) * 0.035f) * 26f, -82f + IdleBob());
				MoveTo(Owner.Center + inspectOffset, 3.4f, 0.045f);
				break;
			case BrainState.CatchUp:
				MoveTo(followTarget, 14f, 0.13f);
				break;
			default:
				MoveTo(followTarget, 8f, 0.075f);
				break;
		}

		NPC.rotation = MathHelper.Lerp(NPC.rotation, NPC.velocity.X * 0.025f, 0.08f);
		UpdateFacing();
	}

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

	private float IdleBob() => MathF.Sin(Main.GlobalTimeWrappedHourly * 2.2f + bobSeed) * 7f;

	private void UpdateFacing()
	{
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

	private void UpdateJob()
	{
		jobTimer++;
		UpdateJobEffects();
		switch (activeJob) {
			case CompanionJob.FindTreasure:
				UpdateTreasureJob();
				break;
			case CompanionJob.Mine:
				UpdateMiningJob();
				break;
			case CompanionJob.Gather:
				UpdateGatherJob();
				break;
		}
	}

	private void UpdateJobEffects()
	{
		if (jobTimer % 8 != 0)
			return;
		Color color = JobColor(activeJob);
		Vector2 position = NPC.Center + Main.rand.NextVector2Circular(30f, 22f);
		Dust dust = Dust.NewDustPerfect(position, DustID.Enchanted_Gold, -NPC.velocity * 0.05f, 110, color, 0.72f);
		dust.noGravity = true;
	}

	private void ResumeAssignment()
	{
		if (activeJob != CompanionJob.None || Profile.Routine == CompanionJob.None)
			return;
		if (Profile.Energy < 12 || Profile.Mood < 20) {
			Command = StayCommand;
			return;
		}
		if (Profile.Routine == CompanionJob.Gather && Profile.PackLoad >= Profile.PackCapacity) {
			CompleteJob(SoulmatesText.Get("Jobs.PackFull"), success: false);
			return;
		}
		BeginJob(Profile.Routine);
		SyncProfileToBoundSigil();
	}

	private void UpdateTreasureJob()
	{
		if (!hasJobTarget && !FindNearestChest(out jobTarget)) {
			CompleteJob(SoulmatesText.Get("Jobs.Treasure.Empty", TreasureRadiusTiles), success: false);
			return;
		}
		hasJobTarget = true;
		Vector2 target = jobTarget.ToWorldCoordinates(16f, -24f);
		MoveTo(target, 8f, 0.08f);
		if (Main.rand.NextBool(5)) {
			Dust dust = Dust.NewDustPerfect(Vector2.Lerp(NPC.Center, target, Main.rand.NextFloat()), DustID.Enchanted_Gold,
				Vector2.Zero, 100, Profile.EssenceColor, 0.8f);
			dust.noGravity = true;
		}

		if (Vector2.DistanceSquared(NPC.Center, target) < 90f * 90f || jobTimer > 600) {
			string direction = DescribeDirection(target - Owner.Center);
			int tiles = (int)(Vector2.Distance(target, Owner.Center) / 16f);
			CompleteJob(SoulmatesText.Get("Jobs.Treasure.Found", direction, tiles), success: true);
		}
	}

	private void UpdateMiningJob()
	{
		if (!hasJobTarget && !FindMiningTarget(out jobTarget)) {
			string report = failedMiningTargets.Count > 0
				? SoulmatesText.Get("Jobs.Mining.Protected", jobCount, failedMiningTargets.Count)
				: jobCount > 0
					? SoulmatesText.Get("Jobs.Mining.Cleared", jobCount, MiningRadiusTiles)
					: SoulmatesText.Get("Jobs.Mining.Empty", MiningRadiusTiles);
			CompleteJob(report, jobCount > 0);
			return;
		}
		hasJobTarget = true;
		Vector2 target = jobTarget.ToWorldCoordinates();
		MoveTo(target, 7f, 0.09f);
		if (Vector2.DistanceSquared(NPC.Center, target) > 58f * 58f)
			return;

		int miningDelay = Profile.Trinket == CompanionTrinket.DelverCharm ? 18 : 28;
		if (jobTimer % miningDelay != 0)
			return;
		if (Main.netMode == NetmodeID.MultiplayerClient)
			return;
		if (!WorldGen.InWorld(jobTarget.X, jobTarget.Y, 10)
			|| !Main.tile[jobTarget.X, jobTarget.Y].HasTile
			|| !IsMineableWorkTile(Main.tile[jobTarget.X, jobTarget.Y].TileType)) {
			hasJobTarget = false;
			return;
		}
		WorldGen.KillTile(jobTarget.X, jobTarget.Y);
		if (!Main.tile[jobTarget.X, jobTarget.Y].HasTile) {
			jobCount++;
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 0, jobTarget.X, jobTarget.Y);
		}
		else
			failedMiningTargets.Add(jobTarget);
		hasJobTarget = false;
		if (jobTimer > 7200)
			CompleteJob(SoulmatesText.Get("Jobs.Mining.Timeout", jobCount), success: false);
	}

	private void UpdateGatherJob()
	{
		if (gatherPause > 0) {
			gatherPause--;
			MoveTo(jobOrigin + new Vector2(0f, -52f + IdleBob()), 7f, 0.08f);
			return;
		}
		int itemIndex = FindNearestLooseItem();
		if (itemIndex < 0) {
			areaEmptyTimer++;
			MoveTo(jobOrigin + new Vector2(0f, -54f + IdleBob()), 5f, 0.06f);
			if (areaEmptyTimer >= 90)
				CompleteJob(jobCount > 0
					? SoulmatesText.Get("Jobs.Gathering.Cleared", jobCount, GatheringRadiusTiles)
					: SoulmatesText.Get("Jobs.Gathering.Empty", GatheringRadiusTiles), jobCount > 0);
			return;
		}
		areaEmptyTimer = 0;

		Item item = Main.item[itemIndex];
		MoveTo(item.Center, 9f, 0.1f);
		if (Vector2.DistanceSquared(NPC.Center, item.Center) < 42f * 42f) {
			if (!CanCarry(item)) {
				gatherPause = 45;
				return;
			}
			int moved = Profile.Store(item);
			if (moved <= 0) {
				CompleteJob(SoulmatesText.Get("Jobs.PackFull"), success: false);
				return;
			}
			jobCount += moved;
			if (item.IsAir)
				item.active = false;
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendData(MessageID.SyncItem, -1, -1, null, itemIndex);
			SyncProfileToBoundSigil();
			gatherPause = 30;
		}
		if (jobTimer > 7200)
			CompleteJob(SoulmatesText.Get("Jobs.Gathering.Timeout", jobCount), success: false);
	}

	private bool FindNearestChest(out Point result)
	{
		result = Point.Zero;
		float radius = TreasureRadiusTiles * 16f;
		float bestDistance = radius * radius;
		foreach (Chest? chest in Main.chest) {
			if (chest is null)
				continue;
			Vector2 position = new Vector2(chest.x * 16f, chest.y * 16f);
			float distance = Vector2.DistanceSquared(jobOrigin, position);
			if (distance >= bestDistance)
				continue;
			bestDistance = distance;
			result = new Point(chest.x, chest.y);
		}
		return result != Point.Zero;
	}

	private bool FindMiningTarget(out Point result)
	{
		Point center = jobOrigin.ToTileCoordinates();
		result = Point.Zero;
		float bestScore = float.MaxValue;
		int radius = MiningRadiusTiles;
		for (int x = center.X - radius; x <= center.X + radius; x++) {
			for (int y = center.Y - radius; y <= center.Y + radius; y++) {
				if (!WorldGen.InWorld(x, y, 10))
					continue;
				Tile tile = Main.tile[x, y];
				if (!tile.HasTile || !IsEarlyOre(tile.TileType))
					continue;
				var candidate = new Point(x, y);
				if (failedMiningTargets.Contains(candidate))
					continue;
				if (Vector2.DistanceSquared(candidate.ToVector2(), center.ToVector2()) > radius * radius)
					continue;
				float score = Vector2.DistanceSquared(candidate.ToWorldCoordinates(), NPC.Center);
				if (score >= bestScore)
					continue;
				bestScore = score;
				result = candidate;
			}
		}
		return result != Point.Zero;
	}

	private int FindNearestLooseItem()
	{
		int result = -1;
		float radius = GatheringRadiusTiles * 16f;
		float bestDistance = float.MaxValue;
		for (int i = 0; i < Main.maxItems; i++) {
			Item item = Main.item[i];
			if (!item.active || item.IsAir || !CanCarry(item))
				continue;
			if (Vector2.DistanceSquared(jobOrigin, item.Center) >= radius * radius)
				continue;
			float distance = Vector2.DistanceSquared(NPC.Center, item.Center);
			if (distance >= bestDistance)
				continue;
			bestDistance = distance;
			result = i;
		}
		return result;
	}

	private void RevealSurroundings()
	{
		if (Main.dedServ || Owner.whoAmI != Main.myPlayer || ++revealTimer < 10)
			return;
		revealTimer = 0;
		Point center = NPC.Center.ToTileCoordinates();
		const int radius = 9;
		for (int x = center.X - radius; x <= center.X + radius; x++) {
			for (int y = center.Y - radius; y <= center.Y + radius; y++) {
				if (!WorldGen.InWorld(x, y, 10))
					continue;
				float distance = Vector2.Distance(new Vector2(x, y), center.ToVector2());
				if (distance > radius)
					continue;
				byte light = (byte)MathHelper.Clamp(255f - distance * 18f, 80f, 255f);
				Main.Map.Update(x, y, light);
			}
		}
	}

	private static bool IsMineableWorkTile(ushort type) => IsEarlyOre(type);

	private static bool IsEarlyOre(ushort type) => type is TileID.Copper or TileID.Tin or TileID.Iron or TileID.Lead
		or TileID.Silver or TileID.Tungsten or TileID.Gold or TileID.Platinum;

	private static string DescribeDirection(Vector2 offset)
	{
		bool hasVertical = MathF.Abs(offset.Y) > 96f;
		bool hasHorizontal = MathF.Abs(offset.X) > 96f;
		string vertical = SoulmatesText.Get(offset.Y < 0f ? "Directions.Above" : "Directions.Below");
		string horizontal = SoulmatesText.Get(offset.X < 0f ? "Directions.West" : "Directions.East");
		if (hasVertical && hasHorizontal)
			return SoulmatesText.Get("Directions.Combined", vertical, horizontal);
		if (hasVertical)
			return vertical;
		return hasHorizontal ? horizontal : SoulmatesText.Get("Directions.Nearby");
	}

	private void CompleteJob(string memory, bool success)
	{
		Profile.LastMemory = memory;
		if (success) {
			Profile.JobsCompleted++;
			Profile.Bond = Math.Clamp(Profile.Bond + 2, 0, 100);
			Profile.Mood = Math.Clamp(Profile.Mood + 1, 0, 100);
		}
		Profile.Routine = CompanionJob.None;
		SyncProfileToBoundSigil();
		Main.NewText($"{Profile.Name}: {memory}", success ? Profile.EssenceColor : Color.LightGray);
		activeJob = CompanionJob.None;
		jobTimer = 0;
		jobCount = 0;
		gatherPause = 0;
		areaEmptyTimer = 0;
		hasJobTarget = false;
		jobOrigin = Vector2.Zero;
		failedMiningTargets.Clear();
		brainState = BrainState.Follow;
		stateTimer = 1;
		NPC.netUpdate = true;
	}

	public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
	{
		Texture2D texture = CompanionVisuals.GetTexture(Profile.Muse);
		Rectangle source = CompanionVisuals.GetFrame(Profile.Muse, texture);
		Vector2 center = NPC.Center - screenPos + new Vector2(0f, IdleBob() * 0.18f);
		Vector2 origin = source.Size() * 0.5f;
		float breath = 1f + MathF.Sin(Main.GlobalTimeWrappedHourly * 2f + bobSeed) * 0.025f;
		Vector2 formScale = Profile.Form switch {
			CompanionForm.Round => new Vector2(1.12f, 0.92f),
			CompanionForm.Wisp => new Vector2(0.88f, 1.14f),
			_ => Vector2.One
		};
		Vector2 scale = formScale * (64f / Math.Max(source.Width, source.Height)) * breath;
		SpriteEffects effects = facing < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
		Color tint = Color.Lerp(Color.White, Profile.EssenceColor, 0.34f);
		DrawJobOrbit(spriteBatch, center);

		for (int i = 0; i < 4; i++) {
			Vector2 glowOffset = new Vector2(2f, 0f).RotatedBy(MathHelper.PiOver2 * i);
			spriteBatch.Draw(texture, center + glowOffset, source, Profile.EssenceColor * 0.18f, NPC.rotation, origin, scale, effects, 0f);
		}
		spriteBatch.Draw(texture, center, source, tint, NPC.rotation, origin, scale, effects, 0f);
		return false;
	}

	private void DrawJobOrbit(SpriteBatch spriteBatch, Vector2 center)
	{
		if (activeJob == CompanionJob.None)
			return;
		Texture2D pixel = TextureAssets.MagicPixel.Value;
		Color color = JobColor(activeJob);
		float time = Main.GlobalTimeWrappedHourly * 2.5f;
		for (int i = 0; i < 3; i++) {
			float angle = time + MathHelper.TwoPi * i / 3f;
			Vector2 point = center + new Vector2(MathF.Cos(angle) * 35f, MathF.Sin(angle) * 13f - 4f);
			int size = i == jobTimer / 10 % 3 ? 5 : 3;
			spriteBatch.Draw(pixel, new Rectangle((int)point.X - size / 2, (int)point.Y - size / 2, size, size), color * 0.88f);
		}
		float pulse = 0.35f + (MathF.Sin(time * 1.6f) + 1f) * 0.12f;
		spriteBatch.Draw(pixel, new Rectangle((int)center.X - 16, (int)center.Y - 43, 32, 2), color * pulse);
	}

	private Color JobColor(CompanionJob job) => job switch {
		CompanionJob.Mine => new Color(100, 188, 255),
		CompanionJob.Gather => new Color(121, 230, 151),
		CompanionJob.FindTreasure => new Color(255, 221, 104),
		_ => Profile.EssenceColor
	};

	public override Color? GetAlpha(Color drawColor) => Color.Lerp(drawColor, Profile.EssenceColor, 0.42f);
	public override void SendExtraAI(BinaryWriter writer) => Profile.Write(writer);
	public override void ReceiveExtraAI(BinaryReader reader) => Profile = CompanionProfile.Read(reader);

	public void ToggleCommand()
	{
		CancelAssignment();
		Command = Command == StayCommand ? FollowCommand : StayCommand;
		brainState = Command == StayCommand ? BrainState.Stay : BrainState.Follow;
		stateTimer = 1;
		if (Command == StayCommand)
			idleTarget = NPC.Center;
		NPC.netUpdate = true;
	}

	public void SetCommand(bool stay)
	{
		CancelAssignment();
		Command = stay ? StayCommand : FollowCommand;
		brainState = stay ? BrainState.Stay : BrainState.Follow;
		stateTimer = 1;
		if (stay)
			idleTarget = NPC.Center;
		NPC.netUpdate = true;
	}

	public void AskToExplore()
	{
		CancelAssignment();
		Command = FollowCommand;
		brainState = BrainState.Inspect;
		stateTimer = Main.rand.Next(180, 320);
		idleTarget = Owner.Center + Main.rand.NextVector2Circular(120f, 60f);
		NPC.netUpdate = true;
	}

	public void StartJob(CompanionJob job)
	{
		Profile.Routine = job;
		BeginJob(job);
	}

	private void BeginJob(CompanionJob job)
	{
		activeJob = job;
		jobTimer = 0;
		jobCount = 0;
		gatherPause = 0;
		areaEmptyTimer = 0;
		hasJobTarget = false;
		jobOrigin = Owner.Center;
		failedMiningTargets.Clear();
		Command = FollowCommand;
		NPC.netUpdate = true;
	}

	private void CancelAssignment()
	{
		activeJob = CompanionJob.None;
		Profile.Routine = CompanionJob.None;
		jobTimer = 0;
		jobCount = 0;
		gatherPause = 0;
		areaEmptyTimer = 0;
		hasJobTarget = false;
		jobOrigin = Vector2.Zero;
		failedMiningTargets.Clear();
	}

	public void EquipTrinket(CompanionTrinket trinket)
	{
		Profile.Trinket = trinket;
		Profile.LastMemory = trinket == CompanionTrinket.None
			? SoulmatesText.Get("Memories.TrinketRemoved")
			: SoulmatesText.Get("Memories.TrinketEquipped", SoulmatesText.EnumName(trinket));
		SyncProfileToBoundSigil();
		NPC.netUpdate = true;
	}

	public string StoreSelectedItem()
	{
		Item selected = Owner.inventory[Owner.selectedItem];
		if (selected.IsAir)
			return SoulmatesText.Get("Pack.SelectedEmpty");
		if (!CanCarry(selected))
			return SoulmatesText.Get("Pack.CannotCarry");
		int moved = Profile.Store(selected);
		if (moved <= 0)
			return SoulmatesText.Get("Pack.Full", Profile.PackLoad, Profile.PackCapacity);
		SyncProfileToBoundSigil();
		NPC.netUpdate = true;
		return SoulmatesText.Get(moved == 1 ? "Pack.StoredOne" : "Pack.StoredMany", moved, Profile.PackLoad, Profile.PackCapacity);
	}

	public string UnloadPack()
	{
		int moved = 0;
		for (int i = Profile.Pack.Count - 1; i >= 0; i--) {
			Item stored = Profile.Pack[i];
			int originalStack = stored.stack;
			Item leftover = Owner.GetItem(Owner.whoAmI, stored.Clone(), GetItemSettings.InventoryEntityToPlayerInventorySettings);
			moved += originalStack - (leftover.IsAir ? 0 : leftover.stack);
			if (leftover.IsAir)
				Profile.Pack.RemoveAt(i);
			else
				Profile.Pack[i] = leftover;
		}
		SyncProfileToBoundSigil();
		NPC.netUpdate = true;
		return moved > 0
			? SoulmatesText.Get(moved == 1 ? "Pack.ReturnedOne" : "Pack.ReturnedMany", moved, Profile.PackLoad, Profile.PackCapacity)
			: Profile.PackLoad == 0 ? SoulmatesText.Get("Pack.AlreadyEmpty") : SoulmatesText.Get("Pack.InventoryFull");
	}

	private static bool CanCarry(Item item) => item.ModItem is not Soulcore and not SoulboundSigil and not CompanionTrinketItem;

	public void SyncProfileToBoundSigil()
	{
		foreach (Item item in Owner.inventory) {
			if (item.ModItem is SoulboundSigil sigil && sigil.Profile.Id == Profile.Id) {
				sigil.Profile = Profile.Clone();
				return;
			}
		}
	}

	public SoulboundSigil? FindBoundSigil()
	{
		foreach (Item item in Owner.inventory) {
			if (item.ModItem is SoulboundSigil sigil && sigil.Profile.Id == Profile.Id)
				return sigil;
		}
		return null;
	}

	public void Recall()
	{
		SyncProfileToBoundSigil();
		if (Owner.active)
			Owner.GetModPlayer<SoulmatesPlayer>().ActiveCompanionWhoAmI = -1;
		NPC.active = false;
		NPC.netUpdate = true;
	}

	public static SoulboundCompanion? FindFor(Player player)
	{
		for (int i = 0; i < Main.maxNPCs; i++) {
			NPC npc = Main.npc[i];
			if (npc.active && npc.type == ModContent.NPCType<SoulboundCompanion>() && (int)npc.ai[0] == player.whoAmI)
				return npc.ModNPC as SoulboundCompanion;
		}
		return null;
	}

	private bool TryGetOwner(out Player owner)
	{
		int index = (int)NPC.ai[0];
		if (index < 0 || index >= Main.maxPlayers) {
			owner = null!;
			return false;
		}
		owner = Main.player[index];
		return owner.active;
	}

}
