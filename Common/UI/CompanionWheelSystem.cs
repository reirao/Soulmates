#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Soulmates.Common.Dialogue;
using Soulmates.Content.NPCs;
using Soulmates.Common.Feedback;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Soulmates.Common.UI;

public sealed class CompanionWheelSystem : ModSystem
{
	private enum RootBranch : byte { Commands, Work, Bond, Emotes, Pack, Details, Mailbox }
	private enum HoverLayer : byte { None, Center, Root, Branch, Native, MiningApproach, OreTarget }
	private enum IconKind : byte { Emote, Item }
	private enum WheelContext : byte { Companion, Player }
	private enum WheelWorkAction : byte { FindTreasure, MineArea, GatherArea, MineTarget, GatherTarget, MiningApproach }

	private readonly record struct WheelIcon(IconKind Kind, int Value);
	private readonly record struct NearbyOreChoice(Point Tile, int ItemType);
	private sealed record NativeCategory(string Key, int Icon, int[] Entries);

	private static readonly RootBranch[] CompanionRoots = [
		RootBranch.Commands, RootBranch.Work, RootBranch.Bond, RootBranch.Pack, RootBranch.Details, RootBranch.Mailbox
	];
	private static readonly RootBranch[] PlayerRoots = [];
	private static readonly CompanionQuickAction[] CommandActions = [
		CompanionQuickAction.Follow, CompanionQuickAction.Stay,
		CompanionQuickAction.Explore, CompanionQuickAction.ToggleAutonomy,
		CompanionQuickAction.ResetInitiativeRules
	];
	private static readonly WheelWorkAction[] WorkActions = [
		WheelWorkAction.FindTreasure, WheelWorkAction.MineArea, WheelWorkAction.GatherArea,
		WheelWorkAction.MineTarget, WheelWorkAction.GatherTarget, WheelWorkAction.MiningApproach
	];
	private static readonly CompanionMiningApproach[] MiningApproaches = Enum.GetValues<CompanionMiningApproach>();
	private static readonly CompanionEmote[] BondEmotes = [
		CompanionEmote.Wave, CompanionEmote.Heart, CompanionEmote.Cheer,
		CompanionEmote.Comfort, CompanionEmote.Laugh, CompanionEmote.Rest
	];

	private static readonly NativeCategory[] NativeCategories = [
		new("General", EmoteID.EmoteHappiness, [
			EmoteID.EmotionLove, EmoteID.EmotionAnger, EmoteID.EmotionCry, EmoteID.EmotionAlert,
			EmoteID.EmoteLaugh, EmoteID.EmoteFear, EmoteID.EmoteNote, EmoteID.EmoteConfused,
			EmoteID.EmoteKiss, EmoteID.EmoteSleep, EmoteID.EmoteRun, EmoteID.EmoteKick,
			EmoteID.EmoteFight, EmoteID.EmoteEating, EmoteID.EmoteSadness, EmoteID.EmoteAnger,
			EmoteID.EmoteHappiness, EmoteID.EmoteWink, EmoteID.EmoteScowl, EmoteID.EmoteSilly,
			EmoteID.Peckish, EmoteID.Hungry, EmoteID.Starving, EmoteID.LucyTheAxe
		]),
		new("Rps", EmoteID.RPSRock, [
			EmoteID.RPSWinScissors, EmoteID.RPSWinRock, EmoteID.RPSWinPaper,
			EmoteID.RPSScissors, EmoteID.RPSRock, EmoteID.RPSPaper
		]),
		new("Items", EmoteID.ItemGoldpile, [
			EmoteID.ItemRing, EmoteID.ItemLifePotion, EmoteID.ItemManaPotion, EmoteID.ItemSoup,
			EmoteID.ItemCookedFish, EmoteID.ItemAle, EmoteID.ItemSword, EmoteID.ItemFishingRod,
			EmoteID.ItemBugNet, EmoteID.ItemDynamite, EmoteID.ItemMinishark, EmoteID.ItemCog,
			EmoteID.ItemTombstone, EmoteID.ItemGoldpile, EmoteID.ItemDiamondRing, EmoteID.ItemPickaxe,
			EmoteID.PartyPresent, EmoteID.PartyBalloons, EmoteID.PartyCake, EmoteID.PartyHats,
			EmoteID.ItemBeer, EmoteID.ItemDefenderMedal
		]),
		new("Nature", EmoteID.MiscTree, [
			EmoteID.WeatherRain, EmoteID.WeatherLightning, EmoteID.WeatherRainbow, EmoteID.MiscTree,
			EmoteID.EventBloodmoon, EmoteID.EventEclipse, EmoteID.EventPumpkin, EmoteID.EventSnow,
			EmoteID.BiomeSky, EmoteID.BiomeOtherworld, EmoteID.BiomeJungle, EmoteID.BiomeCrimson,
			EmoteID.BiomeCorruption, EmoteID.BiomeHallow, EmoteID.BiomeDesert, EmoteID.BiomeBeach,
			EmoteID.BiomeRocklayer, EmoteID.BiomeLavalayer, EmoteID.BiomeSnow, EmoteID.WeatherSunny,
			EmoteID.WeatherCloudy, EmoteID.WeatherStorming, EmoteID.WeatherSnowstorm,
			EmoteID.EventMeteor, EmoteID.MiscFire
		]),
		new("Town", EmoteID.TownGuide, [
			EmoteID.TownMerchant, EmoteID.TownNurse, EmoteID.TownArmsDealer, EmoteID.TownDryad,
			EmoteID.TownGuide, EmoteID.TownOldman, EmoteID.TownDemolitionist, EmoteID.TownClothier,
			EmoteID.TownGoblinTinkerer, EmoteID.TownWizard, EmoteID.TownMechanic, EmoteID.TownSanta,
			EmoteID.TownTruffle, EmoteID.TownSteampunker, EmoteID.TownDyeTrader, EmoteID.TownPartyGirl,
			EmoteID.TownCyborg, EmoteID.TownPainter, EmoteID.TownWitchDoctor, EmoteID.TownPirate,
			EmoteID.TownStylist, EmoteID.TownTravellingMerchant, EmoteID.TownAngler,
			EmoteID.TownSkeletonMerchant, EmoteID.TownTaxCollector, EmoteID.TownBartender,
			EmoteID.TownGolfer, EmoteID.TownBestiaryGirl, EmoteID.TownBestiaryGirlFox, EmoteID.TownPrincess
		]),
		new("Creatures", EmoteID.CritterBunny, [
			EmoteID.CritterBee, EmoteID.CritterSlime, EmoteID.CritterZombie, EmoteID.CritterBunny,
			EmoteID.CritterButterfly, EmoteID.CritterGoblin, EmoteID.CritterPirate,
			EmoteID.CritterSnowman, EmoteID.CritterSpider, EmoteID.CritterBird, EmoteID.CritterMouse,
			EmoteID.CritterGoldfish, EmoteID.CritterMartian, EmoteID.CritterSkeleton
		]),
		new("Dangers", EmoteID.BossEoC, [
			EmoteID.DebuffPoison, EmoteID.DebuffBurn, EmoteID.DebuffSilence, EmoteID.DebuffCurse,
			EmoteID.BossEoC, EmoteID.BossEoW, EmoteID.BossBoC, EmoteID.BossQueenBee,
			EmoteID.BossSkeletron, EmoteID.BossWoF, EmoteID.BossDestroyer, EmoteID.BossSkeletronPrime,
			EmoteID.BossTwins, EmoteID.BossPlantera, EmoteID.BossGolem, EmoteID.BossFishron,
			EmoteID.BossKingSlime, EmoteID.BossCultist, EmoteID.BossMoonmoon,
			EmoteID.BossMourningWood, EmoteID.BossPumpking, EmoteID.BossEverscream,
			EmoteID.BossIceQueen, EmoteID.BossSantank, EmoteID.BossPirateship,
			EmoteID.BossMartianship, EmoteID.EventOldOnesArmy, EmoteID.BossEmpressOfLight,
			EmoteID.BossQueenSlime, EmoteID.BossDeerclops
		])
	];

	private const int NativePageSize = 7;
	private const float RootRadius = 72f;
	private const float BranchRadius = 124f;
	private const float NativeRadius = 178f;
	private static float LayoutScale => Math.Min(1f,
		Math.Min(SoulmatesUISpace.Viewport.X / 460f, SoulmatesUISpace.Viewport.Y / 500f));
	private bool open;
	private int openTicks;
	private Vector2 center;
	private SoulboundCompanion? companion;
	private WheelContext context;
	private RootBranch? branch;
	private int nativeCategory = -1;
	private int nativePage;
	private bool miningApproachMenu;
	private bool nearbyOreMenu;
	private readonly List<NearbyOreChoice> nearbyOreChoices = [];
	private HoverLayer hoverLayer;
	private int hoverIndex = -1;
	private bool leftMouseDown;
	private bool rightMouseDown;
	private RootBranch[] ActiveRoots => context == WheelContext.Companion ? CompanionRoots : PlayerRoots;

	public bool IsOpen => open;

	public void Open(SoulboundCompanion boundCompanion)
	{
		if (boundCompanion.HasPendingInitiative)
			ModContent.GetInstance<InitiativePromptSystem>().Open(boundCompanion);
		else
			Open(boundCompanion, WheelContext.Companion, null);
	}
	public void OpenEmotes(SoulboundCompanion boundCompanion) => Open(boundCompanion, WheelContext.Player, RootBranch.Emotes);

	private void Open(SoulboundCompanion boundCompanion, WheelContext wheelContext, RootBranch? initialBranch)
	{
		if (Main.dedServ || Main.gameMenu || Main.LocalPlayer.dead || Main.playerInventory)
			return;
		ModContent.GetInstance<DirectOrderSystem>().Cancel();
		ModContent.GetInstance<TalkModeSystem>().Close();
		ModContent.GetInstance<FeedbackMailboxSystem>().Close();
		ModContent.GetInstance<InitiativePromptSystem>().Close();
		ModContent.GetInstance<SoulCreatorSystem>().Close();
		companion = boundCompanion;
		context = wheelContext;
		center = ClampCenter(SoulmatesUISpace.Mouse);
		branch = initialBranch;
		nativeCategory = -1;
		nativePage = 0;
		miningApproachMenu = false;
		nearbyOreMenu = false;
		nearbyOreChoices.Clear();
		hoverLayer = HoverLayer.None;
		hoverIndex = -1;
		leftMouseDown = Main.mouseLeft;
		rightMouseDown = Main.mouseRight;
		openTicks = 0;
		open = true;
		SoundEngine.PlaySound(SoundID.MenuOpen with { Volume = 0.55f, Pitch = 0.18f });
	}

	public void Close()
	{
		open = false;
		openTicks = 0;
		companion = null;
		branch = null;
		nativeCategory = -1;
		nativePage = 0;
		miningApproachMenu = false;
		nearbyOreMenu = false;
		nearbyOreChoices.Clear();
		hoverLayer = HoverLayer.None;
		hoverIndex = -1;
		leftMouseDown = false;
		rightMouseDown = false;
	}

	public override void OnWorldUnload() => Close();

	public override void UpdateUI(GameTime gameTime)
	{
		if (!open)
			return;
		if (Main.gameMenu || Main.LocalPlayer.dead || Main.playerInventory || companion?.NPC.active != true
			|| Main.keyState.IsKeyDown(Keys.Escape) && Main.oldKeyState.IsKeyUp(Keys.Escape)) {
			Close();
			return;
		}
		bool leftDown = Main.mouseLeft;
		bool rightDown = Main.mouseRight;
		bool leftPressed = leftDown && !leftMouseDown;
		bool rightPressed = rightDown && !rightMouseDown;
		leftMouseDown = leftDown;
		rightMouseDown = rightDown;

		center = ClampCenter(center);
		openTicks++;
		FindHoveredNode();
		if (leftPressed) {
			Main.mouseLeftRelease = false;
			ActivateHovered();
		}
		else if (openTicks > 8 && rightPressed) {
			Main.mouseRightRelease = false;
			StepBack();
		}

		Main.LocalPlayer.mouseInterface = true;
		Main.blockMouse = true;
	}

	private void FindHoveredNode()
	{
		hoverLayer = HoverLayer.None;
		hoverIndex = -1;
		Vector2 mouse = SoulmatesUISpace.Mouse;
		if (Hit(mouse, center, 25f)) {
			hoverLayer = HoverLayer.Center;
			return;
		}
		if (nativeCategory >= 0) {
			int count = NativeNodeCount();
			for (int i = 0; i < count; i++) {
				if (!Hit(mouse, NativePosition(i, count), 19f))
					continue;
				hoverLayer = HoverLayer.Native;
				hoverIndex = i;
				return;
			}
		}
		if (miningApproachMenu) {
			for (int i = 0; i < MiningApproaches.Length; i++) {
				if (!Hit(mouse, MiningApproachPosition(i), 19f))
					continue;
				hoverLayer = HoverLayer.MiningApproach;
				hoverIndex = i;
				return;
			}
		}
		if (nearbyOreMenu) {
			for (int i = 0; i < nearbyOreChoices.Count + 1; i++) {
				if (!Hit(mouse, OreTargetPosition(i), 19f))
					continue;
				hoverLayer = HoverLayer.OreTarget;
				hoverIndex = i;
				return;
			}
		}
		if (branch is RootBranch activeBranch && activeBranch is not RootBranch.Pack and not RootBranch.Details) {
			int count = BranchNodeCount(activeBranch);
			for (int i = 0; i < count; i++) {
				if (!Hit(mouse, BranchPosition(activeBranch, i, count), 21f))
					continue;
				hoverLayer = HoverLayer.Branch;
				hoverIndex = i;
				return;
			}
		}
		RootBranch[] roots = ActiveRoots;
		for (int i = 0; i < roots.Length; i++) {
			if (!Hit(mouse, RootPosition(i), 24f))
				continue;
			hoverLayer = HoverLayer.Root;
			hoverIndex = i;
			return;
		}
	}

	private void ActivateHovered()
	{
		switch (hoverLayer) {
			case HoverLayer.Center: StepBack(); break;
			case HoverLayer.Root: ActivateRoot(ActiveRoots[hoverIndex]); break;
			case HoverLayer.Branch: ActivateBranch(hoverIndex); break;
			case HoverLayer.Native: ActivateNative(hoverIndex); break;
			case HoverLayer.MiningApproach: ActivateMiningApproach(hoverIndex); break;
			case HoverLayer.OreTarget: ActivateOreTarget(hoverIndex); break;
			default:
				Close();
				SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.42f });
				break;
		}
	}

	private void ActivateRoot(RootBranch selected)
	{
		if (selected == RootBranch.Pack) {
			OpenDetails(TalkCategory.Pack);
			return;
		}
		if (selected == RootBranch.Details) {
			OpenDetails(TalkCategory.Care);
			return;
		}
		if (selected == RootBranch.Mailbox) {
			Close();
			ModContent.GetInstance<FeedbackMailboxSystem>().Open();
			SoundEngine.PlaySound(SoundID.MenuOpen with { Volume = 0.6f });
			return;
		}
		branch = branch == selected ? null : selected;
		nativeCategory = -1;
		nativePage = 0;
		miningApproachMenu = false;
		nearbyOreMenu = false;
		nearbyOreChoices.Clear();
		SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.55f, Pitch = 0.18f });
	}

	private void ActivateBranch(int index)
	{
		if (companion?.NPC.active != true || branch is not RootBranch activeBranch)
			return;
		switch (activeBranch) {
			case RootBranch.Commands:
				if (index >= 0 && index < CommandActions.Length) ExecuteQuickAction(CommandActions[index]);
				break;
			case RootBranch.Work:
				if (index >= 0 && index < WorkActions.Length) {
					if (WorkActions[index] == WheelWorkAction.MiningApproach) {
						miningApproachMenu = !miningApproachMenu;
						nearbyOreMenu = false;
						nearbyOreChoices.Clear();
						SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f, Pitch = 0.25f });
					}
					else
						ExecuteWorkAction(WorkActions[index]);
				}
				break;
			case RootBranch.Bond:
				if (index >= 0 && index < BondEmotes.Length) ExecuteCompanionEmote(BondEmotes[index]);
				break;
			case RootBranch.Emotes:
				if (index >= 0 && index < NativeCategories.Length) {
					nativeCategory = index;
					nativePage = 0;
					SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f, Pitch = 0.25f });
				}
				break;
		}
	}

	private void ActivateOreTarget(int index)
	{
		if (companion?.NPC.active != true)
			return;
		if (index == nearbyOreChoices.Count) {
			SoulboundCompanion target = companion;
			Close();
			ModContent.GetInstance<DirectOrderSystem>().Begin(target, CompanionTargetOrder.Mine);
			return;
		}
		if (index < 0 || index >= nearbyOreChoices.Count)
			return;

		NearbyOreChoice choice = nearbyOreChoices[index];
		SoulboundCompanion selected = companion;
		if (!selected.CanTargetMining(choice.Tile)
			|| TileLoader.GetItemDropFromTypeAndStyle(Main.tile[choice.Tile.X, choice.Tile.Y].TileType, 0) != choice.ItemType) {
			nearbyOreChoices.Clear();
			nearbyOreChoices.AddRange(selected.FindNearbyOreTargets()
				.Select(candidate => new NearbyOreChoice(candidate.Tile, candidate.ItemType)));
			SoundEngine.PlaySound(SoundID.MenuTick);
			return;
		}
		SoulmatesFeedbackSystem.Record("nearby_ore_selected", ("item_type", choice.ItemType),
			("tile_x", choice.Tile.X), ("tile_y", choice.Tile.Y));
		Close();
		if (Main.netMode == NetmodeID.MultiplayerClient)
			global::Soulmates.Soulmates.SendDirectOrderRequest(CompanionTargetOrder.Mine, choice.Tile, -1);
		else {
			CompanionConversationResult result = selected.PerformDirectOrder(CompanionTargetOrder.Mine, choice.Tile, -1);
			selected.ShowSpeech(result.Reply);
			SoundEngine.PlaySound(result.Accepted ? SoundID.Chat : SoundID.MenuClose);
		}
	}

	private void ActivateMiningApproach(int index)
	{
		if (index < 0 || index >= MiningApproaches.Length)
			return;
		CompanionQuickAction action = MiningApproaches[index] switch {
			CompanionMiningApproach.Tunnel => CompanionQuickAction.MiningTunnel,
			CompanionMiningApproach.Vein => CompanionQuickAction.MiningVein,
			CompanionMiningApproach.Surface => CompanionQuickAction.MiningSurface,
			_ => CompanionQuickAction.MiningAdaptive
		};
		ExecuteQuickAction(action);
	}

	private void ActivateNative(int index)
	{
		if (nativeCategory < 0 || nativeCategory >= NativeCategories.Length)
			return;
		NativeCategory category = NativeCategories[nativeCategory];
		int pageCount = NativePageCount(category);
		int entriesOnPage = EntriesOnNativePage(category);
		bool hasPrevious = nativePage > 0;
		bool hasNext = nativePage + 1 < pageCount;
		int cursor = index;
		if (hasPrevious) {
			if (cursor == 0) {
				nativePage--;
				SoundEngine.PlaySound(SoundID.MenuTick);
				return;
			}
			cursor--;
		}
		if (cursor < entriesOnPage) {
			int emoteIndex = nativePage * NativePageSize + cursor;
			if (emoteIndex >= 0 && emoteIndex < category.Entries.Length) {
				int emoteId = category.Entries[emoteIndex];
				Close();
				EmoteBubble.MakeLocalPlayerEmote(emoteId);
				SoundEngine.PlaySound(SoundID.Chat with { Volume = 0.55f, Pitch = 0.18f });
			}
			return;
		}
		if (hasNext && cursor == entriesOnPage) {
			nativePage++;
			SoundEngine.PlaySound(SoundID.MenuTick);
		}
	}

	private void ExecuteWorkAction(WheelWorkAction action)
	{
		if (action == WheelWorkAction.MineTarget && companion?.NPC.active == true) {
			nearbyOreChoices.Clear();
			nearbyOreChoices.AddRange(companion.FindNearbyOreTargets()
				.Select(choice => new NearbyOreChoice(choice.Tile, choice.ItemType)));
			nearbyOreMenu = true;
			miningApproachMenu = false;
			SoulmatesFeedbackSystem.Record("nearby_ore_menu_opened", ("choice_count", nearbyOreChoices.Count));
			SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f, Pitch = 0.25f });
			return;
		}
		if (action == WheelWorkAction.GatherTarget) {
			SoulboundCompanion? target = companion;
			SoulmatesFeedbackSystem.Record("target_mode_opened", ("order", action.ToString()));
			Close();
			if (target?.NPC.active == true) {
				ModContent.GetInstance<DirectOrderSystem>().Begin(target, CompanionTargetOrder.Gather);
			}
			return;
		}

		CompanionQuickAction quickAction = action switch {
			WheelWorkAction.FindTreasure => CompanionQuickAction.FindTreasure,
			WheelWorkAction.MineArea => CompanionQuickAction.Mine,
			_ => CompanionQuickAction.Gather
		};
		ExecuteQuickAction(quickAction);
	}

	private void ExecuteQuickAction(CompanionQuickAction action)
	{
		SoulboundCompanion? target = companion;
		SoulmatesFeedbackSystem.Record("quick_action", ("action", action.ToString()));
		Close();
		if (target?.NPC.active != true)
			return;
		if (Main.netMode == NetmodeID.MultiplayerClient)
			global::Soulmates.Soulmates.SendQuickActionRequest(action);
		else {
			CompanionConversationResult result = target.PerformQuickAction(action);
			target.ShowSpeech(result.Reply);
			SoundEngine.PlaySound(result.Accepted ? SoundID.Chat : SoundID.MenuClose);
		}
	}

	private void ExecuteCompanionEmote(CompanionEmote emote)
	{
		SoulboundCompanion? target = companion;
		SoulmatesFeedbackSystem.Record("companion_emote", ("action", emote.ToString()));
		Close();
		if (target?.NPC.active != true)
			return;
		if (Main.netMode == NetmodeID.MultiplayerClient)
			global::Soulmates.Soulmates.SendEmoteRequest(emote);
		else
			target.PerformEmote(emote);
		SoundEngine.PlaySound(SoundID.Chat with { Volume = 0.55f, Pitch = 0.18f });
	}

	private void OpenDetails(TalkCategory initialCategory)
	{
		SoulboundCompanion? target = companion;
		Close();
		if (target?.NPC.active == true && target.FindBoundSigil() is { } sigil) {
			ModContent.GetInstance<TalkModeSystem>().Open(sigil, target, initialCategory);
			SoundEngine.PlaySound(SoundID.MenuOpen with { Volume = 0.6f });
		}
	}

	private void StepBack()
	{
		if (nearbyOreMenu) {
			nearbyOreMenu = false;
			nearbyOreChoices.Clear();
			SoundEngine.PlaySound(SoundID.MenuTick);
			return;
		}
		if (miningApproachMenu) {
			miningApproachMenu = false;
			SoundEngine.PlaySound(SoundID.MenuTick);
			return;
		}
		if (nativeCategory >= 0) {
			nativeCategory = -1;
			nativePage = 0;
			SoundEngine.PlaySound(SoundID.MenuTick);
			return;
		}
		if (context == WheelContext.Player) {
			Close();
			SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.42f });
			return;
		}
		if (branch is not null) {
			branch = null;
			SoundEngine.PlaySound(SoundID.MenuTick);
			return;
		}
		Close();
		SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.42f });
	}

	public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
	{
		int mouseIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
		if (mouseIndex < 0)
			mouseIndex = layers.Count;
		layers.Insert(mouseIndex, new LegacyGameInterfaceLayer("Soulmates: Soulwheel", Draw, InterfaceScaleType.UI));
	}

	private bool Draw()
	{
		if (!open || companion?.NPC.active != true)
			return true;
		SpriteBatch spriteBatch = Main.spriteBatch;
		Color accent = context == WheelContext.Player
			? new Color(255, 216, 112)
			: companion.Profile.EssenceColor;
		float reveal = MathHelper.Clamp(openTicks / 8f, 0f, 1f);
		float pulse = 1f + MathF.Sin(Main.GlobalTimeWrappedHourly * 4f) * 0.035f;
		RootBranch[] roots = ActiveRoots;
		for (int i = 0; i < roots.Length; i++) {
			Vector2 position = Vector2.Lerp(center, RootPosition(i), reveal);
			bool hovered = hoverLayer == HoverLayer.Root && hoverIndex == i;
			bool active = branch == roots[i];
			DrawNode(spriteBatch, position, hovered ? 46f * pulse : 41f, accent, hovered, active, RootIcon(roots[i]));
		}
		if (branch is RootBranch activeBranch && activeBranch is not RootBranch.Pack and not RootBranch.Details) {
			int count = BranchNodeCount(activeBranch);
			Vector2 parent = context == WheelContext.Player
				? center
				: RootPosition(Array.IndexOf(roots, activeBranch));
			for (int i = 0; i < count; i++) {
				Vector2 destination = BranchPosition(activeBranch, i, count);
				Vector2 position = Vector2.Lerp(parent, destination, reveal);
				bool hovered = hoverLayer == HoverLayer.Branch && hoverIndex == i;
				bool active = activeBranch == RootBranch.Emotes && nativeCategory == i
					|| activeBranch == RootBranch.Work && WorkActions[i] == WheelWorkAction.MiningApproach
						&& miningApproachMenu
					|| activeBranch == RootBranch.Work && WorkActions[i] == WheelWorkAction.MineTarget
						&& nearbyOreMenu;
				DrawNode(spriteBatch, position, hovered ? 41f * pulse : 36f, accent, hovered, active, BranchIcon(activeBranch, i));
			}
		}
		if (nativeCategory >= 0 && branch == RootBranch.Emotes) {
			int count = NativeNodeCount();
			Vector2 parent = BranchPosition(RootBranch.Emotes, nativeCategory, NativeCategories.Length);
			for (int i = 0; i < count; i++) {
				Vector2 destination = NativePosition(i, count);
				Vector2 position = Vector2.Lerp(parent, destination, reveal);
				bool hovered = hoverLayer == HoverLayer.Native && hoverIndex == i;
				DrawNode(spriteBatch, position, hovered ? 36f * pulse : 32f, accent, hovered, false, NativeIcon(i));
			}
		}
		if (miningApproachMenu && branch == RootBranch.Work) {
			Vector2 parent = BranchPosition(RootBranch.Work,
				Array.IndexOf(WorkActions, WheelWorkAction.MiningApproach), WorkActions.Length);
			for (int i = 0; i < MiningApproaches.Length; i++) {
				Vector2 position = Vector2.Lerp(parent, MiningApproachPosition(i), reveal);
				bool hovered = hoverLayer == HoverLayer.MiningApproach && hoverIndex == i;
				bool active = companion.Profile.MiningApproach == MiningApproaches[i];
				DrawNode(spriteBatch, position, hovered ? 36f * pulse : 32f, accent, hovered, active,
					MiningApproachIcon(MiningApproaches[i]));
			}
		}
		if (nearbyOreMenu && branch == RootBranch.Work) {
			Vector2 parent = BranchPosition(RootBranch.Work,
				Array.IndexOf(WorkActions, WheelWorkAction.MineTarget), WorkActions.Length);
			for (int i = 0; i < nearbyOreChoices.Count + 1; i++) {
				Vector2 position = Vector2.Lerp(parent, OreTargetPosition(i), reveal);
				bool hovered = hoverLayer == HoverLayer.OreTarget && hoverIndex == i;
				DrawNode(spriteBatch, position, hovered ? 36f * pulse : 32f, accent, hovered, false,
					OreTargetIcon(i));
			}
		}
		DrawCenter(spriteBatch, accent, pulse);
		DrawHoverLabel(spriteBatch, accent);
		if (context == WheelContext.Companion)
			DrawStatus(spriteBatch, accent);
		return true;
	}

	private void DrawCenter(SpriteBatch spriteBatch, Color accent, float pulse)
	{
		bool hovered = hoverLayer == HoverLayer.Center;
		bool hasParentLayer = nativeCategory >= 0 || miningApproachMenu || nearbyOreMenu
			|| context == WheelContext.Companion && branch is not null;
		WheelIcon icon = hasParentLayer
			? new WheelIcon(IconKind.Item, ItemID.RecallPotion)
			: context == WheelContext.Player
				? new WheelIcon(IconKind.Emote, EmoteID.EmoteHappiness)
				: new WheelIcon(IconKind.Emote, EmoteID.EmotionLove);
		DrawNode(spriteBatch, center, (hovered ? 51f : 46f) * pulse, accent, hovered, !hasParentLayer,
			icon);
	}

	private void DrawHoverLabel(SpriteBatch spriteBatch, Color accent)
	{
		string label = HoverLabel();
		if (string.IsNullOrWhiteSpace(label))
			return;
		float scale = FitTextScale(label, 300f, 0.72f);
		Vector2 size = FontAssets.MouseText.Value.MeasureString(label) * scale;
		Vector2 position = new(center.X - size.X * 0.5f, center.Y + (NativeRadius + 27f) * LayoutScale);
		Utils.DrawBorderString(spriteBatch, label, position, Color.Lerp(Color.White, accent, 0.15f), scale);
	}

	private void DrawStatus(SpriteBatch spriteBatch, Color accent)
	{
		CompanionProfile profile = companion!.Profile;
		string status = SoulmatesText.Get("UI.CompanionWheel.Status", companion.CurrentJobName,
			profile.Level, profile.Energy, profile.PackLoad, profile.PackCapacity,
			SoulmatesText.EnumName(profile.MiningApproach));
		float scale = FitTextScale(status, 340f, 0.5f);
		Vector2 size = FontAssets.MouseText.Value.MeasureString(status) * scale;
		Vector2 position = new(center.X - size.X * 0.5f, center.Y + (NativeRadius + 50f) * LayoutScale);
		Utils.DrawBorderString(spriteBatch, status.ToUpperInvariant(), position,
			Color.Lerp(Color.LightGray, accent, 0.35f), scale);
	}

	private string HoverLabel() => hoverLayer switch {
		HoverLayer.Center => nativeCategory < 0 && !miningApproachMenu && !nearbyOreMenu
			&& (context == WheelContext.Player || branch is null)
			? SoulmatesText.Get("UI.CompanionWheel.Close")
			: SoulmatesText.Get("UI.CompanionWheel.Back"),
		HoverLayer.Root when hoverIndex >= 0 && hoverIndex < ActiveRoots.Length => RootLabel(ActiveRoots[hoverIndex]),
		HoverLayer.Branch => BranchLabel(hoverIndex),
		HoverLayer.Native => NativeLabel(hoverIndex),
		HoverLayer.MiningApproach when hoverIndex >= 0 && hoverIndex < MiningApproaches.Length
			=> SoulmatesText.Get($"UI.CompanionWheel.MiningApproaches.{MiningApproaches[hoverIndex]}"),
		HoverLayer.OreTarget => OreTargetLabel(hoverIndex),
		_ => branch is RootBranch activeBranch ? RootLabel(activeBranch) : SoulmatesText.Get("UI.CompanionWheel.Center")
	};

	private string BranchLabel(int index)
	{
		if (branch is not RootBranch activeBranch)
			return "";
		return activeBranch switch {
			RootBranch.Commands when index >= 0 && index < CommandActions.Length => QuickActionLabel(CommandActions[index]),
			RootBranch.Work when index >= 0 && index < WorkActions.Length
				=> SoulmatesText.Get($"UI.CompanionWheel.WorkActions.{WorkActions[index]}"),
			RootBranch.Bond when index >= 0 && index < BondEmotes.Length => SoulmatesText.EnumName(BondEmotes[index]),
			RootBranch.Emotes when index >= 0 && index < NativeCategories.Length
				=> SoulmatesText.Get($"UI.CompanionWheel.NativeCategories.{NativeCategories[index].Key}"),
			_ => ""
		};
	}

	private string OreTargetLabel(int index)
	{
		if (index == nearbyOreChoices.Count)
			return SoulmatesText.Get("UI.CompanionWheel.NearbyOres.Manual");
		if (index < 0 || index >= nearbyOreChoices.Count)
			return "";
		return SoulmatesText.Get("UI.CompanionWheel.NearbyOres.Select",
			Lang.GetItemNameValue(nearbyOreChoices[index].ItemType));
	}

	private string NativeLabel(int index)
	{
		if (nativeCategory < 0 || nativeCategory >= NativeCategories.Length)
			return "";
		NativeCategory category = NativeCategories[nativeCategory];
		int entriesOnPage = EntriesOnNativePage(category);
		bool hasPrevious = nativePage > 0;
		bool hasNext = nativePage + 1 < NativePageCount(category);
		int cursor = index;
		if (hasPrevious) {
			if (cursor == 0)
				return SoulmatesText.Get("UI.CompanionWheel.Previous");
			cursor--;
		}
		if (cursor < entriesOnPage) {
			int emoteIndex = nativePage * NativePageSize + cursor;
			return emoteIndex < category.Entries.Length ? Lang.GetEmojiName(category.Entries[emoteIndex]).Value : "";
		}
		return hasNext && cursor == entriesOnPage ? SoulmatesText.Get("UI.CompanionWheel.Next") : "";
	}

	private static WheelIcon RootIcon(RootBranch root) => root switch {
		RootBranch.Commands => new WheelIcon(IconKind.Emote, EmoteID.EmoteFight),
		RootBranch.Work => new WheelIcon(IconKind.Emote, EmoteID.ItemPickaxe),
		RootBranch.Bond => new WheelIcon(IconKind.Emote, EmoteID.EmotionLove),
		RootBranch.Emotes => new WheelIcon(IconKind.Emote, EmoteID.EmoteHappiness),
		RootBranch.Pack => new WheelIcon(IconKind.Item, ItemID.PiggyBank),
		RootBranch.Mailbox => new WheelIcon(IconKind.Item, ItemID.PaperAirplaneA),
		_ => new WheelIcon(IconKind.Item, ItemID.Book)
	};

	private static WheelIcon BranchIcon(RootBranch activeBranch, int index)
	{
		if (activeBranch == RootBranch.Commands && index >= 0 && index < CommandActions.Length)
			return new WheelIcon(IconKind.Emote, CommandActions[index] switch {
				CompanionQuickAction.Follow => EmoteID.EmoteRun, CompanionQuickAction.Stay => EmoteID.EmoteSleep,
				CompanionQuickAction.Explore => EmoteID.EmotionAlert,
				CompanionQuickAction.ResetInitiativeRules => EmoteID.EmoteConfused,
				_ => EmoteID.EmoteWink
			});
		if (activeBranch == RootBranch.Work && index >= 0 && index < WorkActions.Length)
			return WorkActions[index] switch {
				WheelWorkAction.FindTreasure => new WheelIcon(IconKind.Emote, EmoteID.ItemGoldpile),
				WheelWorkAction.MineArea => new WheelIcon(IconKind.Emote, EmoteID.ItemPickaxe),
				WheelWorkAction.GatherArea => new WheelIcon(IconKind.Emote, EmoteID.MiscTree),
				WheelWorkAction.MineTarget => new WheelIcon(IconKind.Item, ItemID.CopperPickaxe),
				WheelWorkAction.MiningApproach => new WheelIcon(IconKind.Item, ItemID.MiningPotion),
				_ => new WheelIcon(IconKind.Item, ItemID.TreasureMagnet)
			};
		if (activeBranch == RootBranch.Bond && index >= 0 && index < BondEmotes.Length)
			return new WheelIcon(IconKind.Emote, BondEmotes[index] switch {
				CompanionEmote.Wave => EmoteID.EmoteHappiness, CompanionEmote.Heart => EmoteID.EmotionLove,
				CompanionEmote.Cheer => EmoteID.EmoteNote, CompanionEmote.Comfort => EmoteID.EmoteKiss,
				CompanionEmote.Laugh => EmoteID.EmoteLaugh, _ => EmoteID.EmoteSleep
			});
		if (activeBranch == RootBranch.Emotes && index >= 0 && index < NativeCategories.Length)
			return new WheelIcon(IconKind.Emote, NativeCategories[index].Icon);
		return new WheelIcon(IconKind.Emote, EmoteID.EmoteConfused);
	}

	private static WheelIcon MiningApproachIcon(CompanionMiningApproach approach) => approach switch {
		CompanionMiningApproach.Tunnel => new WheelIcon(IconKind.Item, ItemID.CopperPickaxe),
		CompanionMiningApproach.Vein => new WheelIcon(IconKind.Item, ItemID.SpelunkerPotion),
		CompanionMiningApproach.Surface => new WheelIcon(IconKind.Item, ItemID.SandBlock),
		_ => new WheelIcon(IconKind.Item, ItemID.MiningPotion)
	};

	private WheelIcon OreTargetIcon(int index) => index >= 0 && index < nearbyOreChoices.Count
		? new WheelIcon(IconKind.Item, nearbyOreChoices[index].ItemType)
		: new WheelIcon(IconKind.Item, ItemID.CopperPickaxe);

	private WheelIcon NativeIcon(int index)
	{
		NativeCategory category = NativeCategories[nativeCategory];
		int entriesOnPage = EntriesOnNativePage(category);
		bool hasPrevious = nativePage > 0;
		bool hasNext = nativePage + 1 < NativePageCount(category);
		int cursor = index;
		if (hasPrevious) {
			if (cursor == 0) return new WheelIcon(IconKind.Item, ItemID.RecallPotion);
			cursor--;
		}
		if (cursor < entriesOnPage) {
			int emoteIndex = nativePage * NativePageSize + cursor;
			return new WheelIcon(IconKind.Emote, category.Entries[emoteIndex]);
		}
		return hasNext && cursor == entriesOnPage
			? new WheelIcon(IconKind.Item, ItemID.TeleportationPotion)
			: new WheelIcon(IconKind.Emote, EmoteID.EmoteConfused);
	}

	private static string RootLabel(RootBranch root) => SoulmatesText.Get($"UI.CompanionWheel.Categories.{root}");

	private string QuickActionLabel(CompanionQuickAction action)
	{
		if (action == CompanionQuickAction.ToggleAutonomy)
			return SoulmatesText.Get(companion!.Profile.AutonomyEnabled
				? "UI.CompanionWheel.AutonomyOn" : "UI.CompanionWheel.AutonomyOff");
		return SoulmatesText.Get($"UI.CompanionWheel.Actions.{action}");
	}

	private static int BranchNodeCount(RootBranch activeBranch) => activeBranch switch {
		RootBranch.Commands => CommandActions.Length, RootBranch.Work => WorkActions.Length,
		RootBranch.Bond => BondEmotes.Length, RootBranch.Emotes => NativeCategories.Length, _ => 0
	};

	private int NativeNodeCount()
	{
		NativeCategory category = NativeCategories[nativeCategory];
		int count = EntriesOnNativePage(category);
		if (nativePage > 0) count++;
		if (nativePage + 1 < NativePageCount(category)) count++;
		return count;
	}

	private static int NativePageCount(NativeCategory category)
		=> Math.Max(1, (category.Entries.Length + NativePageSize - 1) / NativePageSize);

	private int EntriesOnNativePage(NativeCategory category)
		=> Math.Clamp(category.Entries.Length - nativePage * NativePageSize, 0, NativePageSize);

	private Vector2 RootPosition(int index) => center + RootAngle(index).ToRotationVector2() * RootRadius * LayoutScale;

	private Vector2 BranchPosition(RootBranch activeBranch, int index, int count)
	{
		float rootAngle = context == WheelContext.Player
			? -MathHelper.PiOver2
			: RootAngle(Array.IndexOf(ActiveRoots, activeBranch));
		float angle = FanAngle(rootAngle, index, count, MathHelper.ToRadians(136f));
		return center + angle.ToRotationVector2() * BranchRadius * LayoutScale;
	}

	private Vector2 NativePosition(int index, int count)
	{
		float rootAngle = context == WheelContext.Player
			? -MathHelper.PiOver2
			: RootAngle(Array.IndexOf(ActiveRoots, RootBranch.Emotes));
		float categoryAngle = FanAngle(rootAngle,
			nativeCategory, NativeCategories.Length, MathHelper.ToRadians(136f));
		float angle = FanAngle(categoryAngle, index, count, MathHelper.ToRadians(148f));
		return center + angle.ToRotationVector2() * NativeRadius * LayoutScale;
	}

	private Vector2 MiningApproachPosition(int index)
	{
		float rootAngle = RootAngle(Array.IndexOf(ActiveRoots, RootBranch.Work));
		float branchAngle = FanAngle(rootAngle, Array.IndexOf(WorkActions, WheelWorkAction.MiningApproach),
			WorkActions.Length, MathHelper.ToRadians(136f));
		float angle = FanAngle(branchAngle, index, MiningApproaches.Length, MathHelper.ToRadians(112f));
		return center + angle.ToRotationVector2() * NativeRadius * LayoutScale;
	}

	private Vector2 OreTargetPosition(int index)
	{
		float rootAngle = RootAngle(Array.IndexOf(ActiveRoots, RootBranch.Work));
		float branchAngle = FanAngle(rootAngle, Array.IndexOf(WorkActions, WheelWorkAction.MineTarget),
			WorkActions.Length, MathHelper.ToRadians(136f));
		float angle = FanAngle(branchAngle, index, nearbyOreChoices.Count + 1, MathHelper.ToRadians(148f));
		return center + angle.ToRotationVector2() * NativeRadius * LayoutScale;
	}

	private float RootAngle(int index) => -MathHelper.PiOver2 + MathHelper.TwoPi * index / ActiveRoots.Length;
	private static float FanAngle(float centerAngle, int index, int count, float spread)
		=> count <= 1 ? centerAngle : centerAngle - spread * 0.5f + spread * index / (count - 1f);

	private static Vector2 ClampCenter(Vector2 desired)
	{
		float margin = (NativeRadius + 32f) * LayoutScale;
		float bottomMargin = (NativeRadius + 80f) * LayoutScale;
		Vector2 viewport = SoulmatesUISpace.Viewport;
		float x = viewport.X <= margin * 2f ? viewport.X * 0.5f : Math.Clamp(desired.X, margin, viewport.X - margin);
		float y = viewport.Y <= margin + bottomMargin ? viewport.Y * 0.5f : Math.Clamp(desired.Y, margin, viewport.Y - bottomMargin);
		return new Vector2(x, y);
	}

	private static bool Hit(Vector2 point, Vector2 node, float radius)
		=> Vector2.DistanceSquared(point, node) <= radius * radius * LayoutScale * LayoutScale;

	private static void DrawNode(SpriteBatch spriteBatch, Vector2 position, float size, Color accent,
		bool hovered, bool active, WheelIcon icon)
	{
		size *= LayoutScale;
		Texture2D slot = TextureAssets.InventoryBack.Value;
		Color slotColor = hovered
			? Color.White
			: active ? Color.Lerp(Color.White, accent, 0.34f) : Color.White * 0.9f;
		Vector2 origin = slot.Size() * 0.5f;
		spriteBatch.Draw(slot, position, null, slotColor, 0f, origin, size / slot.Width, SpriteEffects.None, 0f);
		DrawIcon(spriteBatch, position, size * 0.64f, icon, Color.White);
	}

	private static void DrawIcon(SpriteBatch spriteBatch, Vector2 position, float maximumSize, WheelIcon icon, Color color)
	{
		switch (icon.Kind) {
			case IconKind.Emote: DrawEmoteIcon(spriteBatch, position, maximumSize, icon.Value, color); break;
			case IconKind.Item: DrawItemIcon(spriteBatch, position, maximumSize, icon.Value, color); break;
		}
	}

	private static void DrawEmoteIcon(SpriteBatch spriteBatch, Vector2 position, float maximumSize, int emoteId, Color color)
	{
		if (emoteId < 0 || emoteId >= EmoteID.Count)
			return;
		Texture2D sheet = TextureAssets.Extra[ExtrasID.EmoteBubble].Value;
		int animationFrame = (int)(Main.GlobalTimeWrappedHourly * 3f) % 2;
		int column = emoteId % EmoteBubble.EMOTE_SHEET_EMOTES_PER_ROW * 2 + animationFrame;
		int row = 1 + emoteId / EmoteBubble.EMOTE_SHEET_EMOTES_PER_ROW;
		Rectangle source = sheet.Frame(EmoteBubble.EMOTE_SHEET_HORIZONTAL_FRAMES,
			EmoteBubble.EMOTE_SHEET_VERTICAL_FRAMES, column, row);
		float scale = Math.Min(maximumSize / source.Width, maximumSize / source.Height);
		spriteBatch.Draw(sheet, position, source, color, 0f, source.Size() * 0.5f, scale, SpriteEffects.None, 0f);
	}

	private static void DrawItemIcon(SpriteBatch spriteBatch, Vector2 position, float maximumSize, int itemType, Color color)
	{
		Main.instance.LoadItem(itemType);
		Texture2D texture = TextureAssets.Item[itemType].Value;
		Rectangle source = Main.itemAnimations[itemType]?.GetFrame(texture) ?? texture.Bounds;
		float scale = Math.Min(maximumSize / source.Width, maximumSize / source.Height);
		spriteBatch.Draw(texture, position, source, color, 0f, source.Size() * 0.5f, scale, SpriteEffects.None, 0f);
	}

	private static float FitTextScale(string text, float maximumWidth, float preferredScale)
	{
		float width = FontAssets.MouseText.Value.MeasureString(text).X;
		return width <= 0f ? preferredScale : Math.Min(preferredScale, maximumWidth / width);
	}
}
