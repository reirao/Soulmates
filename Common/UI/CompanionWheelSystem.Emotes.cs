#nullable enable
using System.Linq;
using Terraria.GameContent.UI;

namespace Soulmates.Common.UI;

public sealed partial class CompanionWheelSystem
{
	// A folder has children, a leaf has entries. Player symbols and bond actions share navigation, not dispatch.
	private sealed record NativeCategory(string Key, int Icon, int[] Entries,
		NativeCategory[]? Children = null, bool ItemGroup = false);

	private static NativeCategory Group(string key, int icon, params NativeCategory[] children)
		=> new(key, icon, [], children);

	private static readonly NativeCategory[] NativeItemCategories = [
		new("Food", EmoteID.ItemSoup, [EmoteID.ItemSoup, EmoteID.ItemCookedFish, EmoteID.ItemAle, EmoteID.ItemBeer, EmoteID.PartyCake], ItemGroup: true),
		new("Recovery", EmoteID.ItemLifePotion, [EmoteID.ItemLifePotion, EmoteID.ItemManaPotion], ItemGroup: true),
		new("Tools", EmoteID.ItemPickaxe, [EmoteID.ItemPickaxe, EmoteID.ItemFishingRod, EmoteID.ItemBugNet], ItemGroup: true),
		new("Weapons", EmoteID.ItemSword, [EmoteID.ItemSword, EmoteID.ItemMinishark, EmoteID.ItemDynamite, EmoteID.LucyTheAxe], ItemGroup: true),
		new("Materials", EmoteID.ItemCog, [EmoteID.ItemCog, EmoteID.ItemTombstone], ItemGroup: true),
		new("Valuables", EmoteID.ItemGoldpile, [EmoteID.ItemGoldpile, EmoteID.ItemRing, EmoteID.ItemDiamondRing, EmoteID.ItemDefenderMedal], ItemGroup: true),
		new("Party", EmoteID.PartyPresent, [EmoteID.PartyPresent, EmoteID.PartyBalloons, EmoteID.PartyHats], ItemGroup: true)
	];

	private static readonly NativeCategory[] NativeCategories = [
		Group("Feelings", EmoteID.EmoteHappiness,
			new("Warm", EmoteID.EmotionLove, [EmoteID.EmotionLove, EmoteID.EmoteHappiness, EmoteID.EmoteLaugh]),
			new("Upset", EmoteID.EmotionCry, [EmoteID.EmotionCry, EmoteID.EmoteSadness, EmoteID.EmotionAnger, EmoteID.EmoteAnger, EmoteID.EmoteScowl]),
			new("Uncertain", EmoteID.EmoteConfused, [EmoteID.EmoteFear, EmoteID.EmoteConfused])),
		new("Gestures", EmoteID.EmoteWink, [EmoteID.EmoteKiss, EmoteID.EmoteWink, EmoteID.EmoteSilly]),
		Group("Activities", EmoteID.EmoteRun,
			new("Everyday", EmoteID.EmoteEating, [EmoteID.EmoteSleep, EmoteID.EmoteRun, EmoteID.EmoteEating]),
			new("Combat", EmoteID.EmoteFight, [EmoteID.EmoteKick, EmoteID.EmoteFight]),
			new("Games", EmoteID.RPSRock, [EmoteID.RPSScissors, EmoteID.RPSRock, EmoteID.RPSPaper,
				EmoteID.RPSWinScissors, EmoteID.RPSWinRock, EmoteID.RPSWinPaper])),
		Group("Items", EmoteID.ItemGoldpile, NativeItemCategories),
		Group("World", EmoteID.MiscTree,
			new("Weather", EmoteID.WeatherSunny, [EmoteID.WeatherRain, EmoteID.WeatherLightning, EmoteID.WeatherRainbow,
				EmoteID.WeatherSunny, EmoteID.WeatherCloudy, EmoteID.WeatherStorming, EmoteID.WeatherSnowstorm]),
			new("Biomes", EmoteID.BiomeJungle, [EmoteID.BiomeSky, EmoteID.BiomeOtherworld, EmoteID.BiomeJungle,
				EmoteID.BiomeCrimson, EmoteID.BiomeCorruption, EmoteID.BiomeHallow, EmoteID.BiomeDesert,
				EmoteID.BiomeBeach, EmoteID.BiomeRocklayer, EmoteID.BiomeLavalayer, EmoteID.BiomeSnow]),
			new("Events", EmoteID.EventBloodmoon, [EmoteID.EventBloodmoon, EmoteID.EventEclipse,
				EmoteID.EventPumpkin, EmoteID.EventSnow, EmoteID.EventMeteor, EmoteID.EventOldOnesArmy]),
			new("Nature", EmoteID.MiscTree, [EmoteID.MiscTree, EmoteID.MiscFire])),
		Group("Beings", EmoteID.TownGuide,
			new("Town", EmoteID.TownGuide, [
				EmoteID.TownMerchant, EmoteID.TownNurse, EmoteID.TownArmsDealer, EmoteID.TownDryad,
				EmoteID.TownGuide, EmoteID.TownOldman, EmoteID.TownDemolitionist, EmoteID.TownClothier,
				EmoteID.TownGoblinTinkerer, EmoteID.TownWizard, EmoteID.TownMechanic, EmoteID.TownSanta,
				EmoteID.TownTruffle, EmoteID.TownSteampunker, EmoteID.TownDyeTrader, EmoteID.TownPartyGirl,
				EmoteID.TownCyborg, EmoteID.TownPainter, EmoteID.TownWitchDoctor, EmoteID.TownPirate,
				EmoteID.TownStylist, EmoteID.TownTravellingMerchant, EmoteID.TownAngler,
				EmoteID.TownSkeletonMerchant, EmoteID.TownTaxCollector, EmoteID.TownBartender,
				EmoteID.TownGolfer, EmoteID.TownBestiaryGirl, EmoteID.TownBestiaryGirlFox, EmoteID.TownPrincess]),
			new("Creatures", EmoteID.CritterBunny, [
				EmoteID.CritterBee, EmoteID.CritterSlime, EmoteID.CritterZombie, EmoteID.CritterBunny,
				EmoteID.CritterButterfly, EmoteID.CritterGoblin, EmoteID.CritterPirate,
				EmoteID.CritterSnowman, EmoteID.CritterSpider, EmoteID.CritterBird, EmoteID.CritterMouse,
				EmoteID.CritterGoldfish, EmoteID.CritterMartian, EmoteID.CritterSkeleton])),
		Group("Notifications", EmoteID.EmotionAlert,
			new("Signals", EmoteID.EmotionAlert, [EmoteID.EmotionAlert, EmoteID.EmoteNote]),
			new("Needs", EmoteID.Hungry, [EmoteID.Peckish, EmoteID.Hungry, EmoteID.Starving]),
			new("Debuffs", EmoteID.DebuffPoison, [EmoteID.DebuffPoison, EmoteID.DebuffBurn, EmoteID.DebuffSilence, EmoteID.DebuffCurse]),
			new("Bosses", EmoteID.BossEoC, [
				EmoteID.BossEoC, EmoteID.BossEoW, EmoteID.BossBoC, EmoteID.BossQueenBee,
				EmoteID.BossSkeletron, EmoteID.BossWoF, EmoteID.BossDestroyer, EmoteID.BossSkeletronPrime,
				EmoteID.BossTwins, EmoteID.BossPlantera, EmoteID.BossGolem, EmoteID.BossFishron,
				EmoteID.BossKingSlime, EmoteID.BossCultist, EmoteID.BossMoonmoon,
				EmoteID.BossMourningWood, EmoteID.BossPumpking, EmoteID.BossEverscream,
				EmoteID.BossIceQueen, EmoteID.BossSantank, EmoteID.BossPirateship,
				EmoteID.BossMartianship, EmoteID.BossEmpressOfLight, EmoteID.BossQueenSlime, EmoteID.BossDeerclops]))
	];

	private static readonly NativeCategory[] BondCategories = [
		new("Feelings", EmoteID.EmotionLove, [(int)CompanionEmote.Heart, (int)CompanionEmote.Laugh]),
		new("Gestures", EmoteID.EmoteWink, [(int)CompanionEmote.Wave, (int)CompanionEmote.Cheer]),
		new("Care", EmoteID.EmoteKiss, [(int)CompanionEmote.Comfort, (int)CompanionEmote.Rest])
	];

	private static string EmoteCategoryLabel(NativeCategory category) => SoulmatesText.Get(category.ItemGroup
		? $"UI.CompanionWheel.ItemGroups.{category.Key}" : $"UI.CompanionWheel.EmoteGroups.{category.Key}");

	private string EmoteBreadcrumb()
	{
		var path = nativeGroups.Reverse().Select(EmoteCategoryLabel);
		if (nativeCategory >= 0) path = path.Append(EmoteCategoryLabel(ActiveNativeCategories[nativeCategory]));
		return string.Join(" > ", path.Prepend(RootLabel(branch!.Value)));
	}

	private static int BondIcon(CompanionEmote emote) => emote switch {
		CompanionEmote.Wave => EmoteID.EmoteHappiness, CompanionEmote.Heart => EmoteID.EmotionLove,
		CompanionEmote.Cheer => EmoteID.EmoteNote, CompanionEmote.Comfort => EmoteID.EmoteKiss,
		CompanionEmote.Laugh => EmoteID.EmoteLaugh, _ => EmoteID.EmoteSleep
	};
}
