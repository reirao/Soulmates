using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Soulmates.Common;
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
	private int revealTimer;

	public CompanionProfile Profile { get; set; } = new();
	private Player Owner => Main.player[(int)NPC.ai[0]];
	private ref float Command => ref NPC.ai[1];
	public string CommandName => Command == StayCommand ? "Stay" : "Follow";
	public CompanionJob CurrentJob => activeJob;
	public string CurrentJobName => activeJob == CompanionJob.None ? "None" : SplitName(activeJob.ToString());

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

	public override bool CheckActive() => !Owner.active || Owner.dead;

	public override void AI()
	{
		if (!Owner.active || Owner.dead) {
			NPC.active = false;
			return;
		}

		NPC.GivenName = Profile.Name;
		Lighting.AddLight(NPC.Center, Profile.EssenceColor.ToVector3() * 1.15f);
		RevealSurroundings();
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

	private void UpdateTreasureJob()
	{
		if (!hasJobTarget && !FindNearestChest(out jobTarget)) {
			CompleteJob("I searched carefully, but sensed no unopened places nearby.", success: false);
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
			CompleteJob($"I found a treasure signal {direction}, about {tiles} tiles away.", success: true);
		}
	}

	private void UpdateMiningJob()
	{
		int goal = Profile.Trinket == CompanionTrinket.DelverCharm ? 5 : 3;
		if (!hasJobTarget && !FindMiningTarget(out jobTarget)) {
			CompleteJob(jobCount > 0 ? $"I finished after mining {jobCount} blocks." : "I could not find safe stone or ore nearby.", jobCount > 0);
			return;
		}
		hasJobTarget = true;
		Vector2 target = jobTarget.ToWorldCoordinates();
		MoveTo(target, 7f, 0.09f);
		if (Vector2.DistanceSquared(NPC.Center, target) > 58f * 58f)
			return;

		if (jobTimer % 35 != 0)
			return;
		WorldGen.KillTile(jobTarget.X, jobTarget.Y);
		if (!Main.tile[jobTarget.X, jobTarget.Y].HasTile) {
			jobCount++;
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 0, jobTarget.X, jobTarget.Y);
		}
		hasJobTarget = false;
		if (jobCount >= goal || jobTimer > 900)
			CompleteJob($"I mined {jobCount} blocks for us.", jobCount > 0);
	}

	private void UpdateGatherJob()
	{
		if (gatherPause > 0) {
			gatherPause--;
			MoveTo(Owner.Center + new Vector2(-Owner.direction * 54f, -52f), 7f, 0.08f);
			return;
		}
		int itemIndex = FindNearestLooseItem();
		if (itemIndex < 0) {
			if (jobCount > 0 || jobTimer > 180)
				CompleteJob(jobCount > 0 ? $"I gathered {jobCount} loose items." : "Nothing nearby needs gathering.", jobCount > 0);
			else
				MoveTo(Owner.Center + new Vector2(-Owner.direction * 80f, -60f), 5f, 0.06f);
			return;
		}

		Item item = Main.item[itemIndex];
		MoveTo(item.Center, 9f, 0.1f);
		if (Vector2.DistanceSquared(NPC.Center, item.Center) < 42f * 42f) {
			if (!CanCarry(item)) {
				gatherPause = 45;
				return;
			}
			int moved = Profile.Store(item);
			if (moved <= 0) {
				CompleteJob("My pack is full. We should unload it before I gather more.", jobCount > 0);
				return;
			}
			jobCount += moved;
			if (item.IsAir)
				item.active = false;
			SyncProfileToBoundSigil();
			gatherPause = 30;
		}
		if (jobCount >= (Profile.Trinket == CompanionTrinket.HearthRibbon ? 8 : 5) || jobTimer > 720)
			CompleteJob($"I gathered {jobCount} loose items.", jobCount > 0);
	}

	private bool FindNearestChest(out Point result)
	{
		result = Point.Zero;
		float radius = (Profile.Trinket == CompanionTrinket.StarfinderBell ? 90f : 55f) * 16f;
		float bestDistance = radius * radius;
		foreach (Chest? chest in Main.chest) {
			if (chest is null)
				continue;
			Vector2 position = new Vector2(chest.x * 16f, chest.y * 16f);
			float distance = Vector2.DistanceSquared(Owner.Center, position);
			if (distance >= bestDistance)
				continue;
			bestDistance = distance;
			result = new Point(chest.x, chest.y);
		}
		return result != Point.Zero;
	}

	private bool FindMiningTarget(out Point result)
	{
		Point center = Owner.Center.ToTileCoordinates();
		result = Point.Zero;
		float bestScore = float.MaxValue;
		for (int x = center.X - 22; x <= center.X + 22; x++) {
			for (int y = center.Y - 18; y <= center.Y + 22; y++) {
				if (!WorldGen.InWorld(x, y, 10))
					continue;
				Tile tile = Main.tile[x, y];
				if (!tile.HasTile || !IsMineableWorkTile(tile.TileType))
					continue;
				bool ore = IsEarlyOre(tile.TileType);
				if (!ore && (y < center.Y + 3 || Math.Abs(x - center.X) < 5))
					continue;
				float score = Vector2.DistanceSquared(new Vector2(x, y), center.ToVector2()) + (ore ? -500f : 0f);
				if (score >= bestScore)
					continue;
				bestScore = score;
				result = new Point(x, y);
			}
		}
		return result != Point.Zero;
	}

	private int FindNearestLooseItem()
	{
		int result = -1;
		float radius = (Profile.Trinket == CompanionTrinket.HearthRibbon ? 42f : 28f) * 16f;
		float bestDistance = radius * radius;
		for (int i = 0; i < Main.maxItems; i++) {
			Item item = Main.item[i];
			if (!item.active || item.IsAir || !CanCarry(item))
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

	private static bool IsMineableWorkTile(ushort type) => IsEarlyOre(type) || type == TileID.Stone;

	private static bool IsEarlyOre(ushort type) => type is TileID.Copper or TileID.Tin or TileID.Iron or TileID.Lead
		or TileID.Silver or TileID.Tungsten or TileID.Gold or TileID.Platinum;

	private static string DescribeDirection(Vector2 offset)
	{
		string vertical = MathF.Abs(offset.Y) > 96f ? (offset.Y < 0f ? "above" : "below") : "";
		string horizontal = MathF.Abs(offset.X) > 96f ? (offset.X < 0f ? "to the west" : "to the east") : "nearby";
		return vertical.Length > 0 && horizontal != "nearby" ? $"{vertical} and {horizontal}" : vertical.Length > 0 ? vertical : horizontal;
	}

	private void CompleteJob(string memory, bool success)
	{
		Profile.LastMemory = memory;
		if (success) {
			Profile.JobsCompleted++;
			Profile.Bond = Math.Clamp(Profile.Bond + 2, 0, 100);
			Profile.Mood = Math.Clamp(Profile.Mood + 1, 0, 100);
		}
		SyncProfileToBoundSigil();
		Main.NewText($"{Profile.Name}: {memory}", success ? Profile.EssenceColor : Color.LightGray);
		activeJob = CompanionJob.None;
		jobTimer = 0;
		jobCount = 0;
		gatherPause = 0;
		hasJobTarget = false;
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

		for (int i = 0; i < 4; i++) {
			Vector2 glowOffset = new Vector2(2f, 0f).RotatedBy(MathHelper.PiOver2 * i);
			spriteBatch.Draw(texture, center + glowOffset, source, Profile.EssenceColor * 0.18f, NPC.rotation, origin, scale, effects, 0f);
		}
		spriteBatch.Draw(texture, center, source, tint, NPC.rotation, origin, scale, effects, 0f);
		return false;
	}

	public override Color? GetAlpha(Color drawColor) => Color.Lerp(drawColor, Profile.EssenceColor, 0.42f);
	public override void SendExtraAI(BinaryWriter writer) => Profile.Write(writer);
	public override void ReceiveExtraAI(BinaryReader reader) => Profile = CompanionProfile.Read(reader);

	public void ToggleCommand()
	{
		Command = Command == StayCommand ? FollowCommand : StayCommand;
		brainState = Command == StayCommand ? BrainState.Stay : BrainState.Follow;
		stateTimer = 1;
		if (Command == StayCommand)
			idleTarget = NPC.Center;
		NPC.netUpdate = true;
	}

	public void SetCommand(bool stay)
	{
		Command = stay ? StayCommand : FollowCommand;
		brainState = stay ? BrainState.Stay : BrainState.Follow;
		stateTimer = 1;
		if (stay)
			idleTarget = NPC.Center;
		NPC.netUpdate = true;
	}

	public void AskToExplore()
	{
		Command = FollowCommand;
		brainState = BrainState.Inspect;
		stateTimer = Main.rand.Next(180, 320);
		idleTarget = Owner.Center + Main.rand.NextVector2Circular(120f, 60f);
		NPC.netUpdate = true;
	}

	public void StartJob(CompanionJob job)
	{
		activeJob = job;
		jobTimer = 0;
		jobCount = 0;
		gatherPause = 0;
		hasJobTarget = false;
		Command = FollowCommand;
		NPC.netUpdate = true;
	}

	public void EquipTrinket(CompanionTrinket trinket)
	{
		Profile.Trinket = trinket;
		Profile.LastMemory = trinket == CompanionTrinket.None
			? "You let me travel light again."
			: $"You entrusted me with the {SplitName(trinket.ToString())}.";
		SyncProfileToBoundSigil();
		NPC.netUpdate = true;
	}

	public string StoreSelectedItem()
	{
		Item selected = Owner.inventory[Owner.selectedItem];
		if (selected.IsAir)
			return "Your selected hotbar slot is empty.";
		if (!CanCarry(selected))
			return "That item anchors our bond. I should not carry it inside my pack.";
		int moved = Profile.Store(selected);
		if (moved <= 0)
			return $"My pack is full. ({Profile.PackLoad}/{Profile.PackCapacity})";
		SyncProfileToBoundSigil();
		NPC.netUpdate = true;
		return $"I stored {moved} item{(moved == 1 ? "" : "s")}. ({Profile.PackLoad}/{Profile.PackCapacity})";
	}

	public string UnloadPack()
	{
		int moved = 0;
		for (int i = Profile.Pack.Count - 1; i >= 0; i--) {
			Item stored = Profile.Pack[i];
			int originalStack = stored.stack;
			Item leftover = Owner.GetItem(Owner.whoAmI, stored, GetItemSettings.InventoryEntityToPlayerInventorySettings);
			moved += originalStack - (leftover.IsAir ? 0 : leftover.stack);
			if (leftover.IsAir)
				Profile.Pack.RemoveAt(i);
			else
				Profile.Pack[i] = leftover;
		}
		SyncProfileToBoundSigil();
		NPC.netUpdate = true;
		return moved > 0
			? $"I returned {moved} item{(moved == 1 ? "" : "s")}. ({Profile.PackLoad}/{Profile.PackCapacity})"
			: Profile.PackLoad == 0 ? "My pack is already empty." : "Your inventory has no room for my cargo.";
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

	private static string SplitName(string value) => System.Text.RegularExpressions.Regex.Replace(value, "([a-z])([A-Z])", "$1 $2");
}
