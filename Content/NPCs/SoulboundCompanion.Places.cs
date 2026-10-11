#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Common.Dialogue;
using Terraria;
using Terraria.ID;

namespace Soulmates.Content.NPCs;

public sealed partial class SoulboundCompanion
{
	private int placeScanTicks;
	private int outsidePlaceTicks;
	private Guid lastPlaceWorld;
	private Point? lastVisitedPlace;
	private Guid questionPlaceWorld;
	private Point? questionPlaceAnchor;
	private Guid homeChestWorld;
	private readonly Dictionary<Point, bool> homeChestCache = [];
	private Guid PlaceWorld => Main.ActiveWorldFileData?.UniqueId ?? Guid.Empty;

	private CompanionPlaceObservation NearbyPlace()
		=> TryGetOwner(out Player owner) && Vector2.DistanceSquared(owner.Center, NPC.Center) <= 320f * 320f
			? CompanionPlaces.SampleAt(owner, NPC, owner.Center.ToTileCoordinates()) : default;

	private void TickPlaceAwareness()
	{
		if (Main.netMode == NetmodeID.MultiplayerClient || --placeScanTicks > 0) return;
		placeScanTicks = 180;
		homeChestCache.Clear();
		Guid world = PlaceWorld;
		if (world == Guid.Empty) return;
		CompanionPlaceObservation observation = NearbyPlace();
		if (!observation.IsCandidate) {
			outsidePlaceTicks = Math.Min(720, outsidePlaceTicks + 180);
			return;
		}
		bool returning = lastPlaceWorld != world || lastVisitedPlace is not { } last
			|| Vector2.DistanceSquared(last.ToVector2(), observation.Anchor.ToVector2()) > CompanionPlaces.NearbyRadius * CompanionPlaces.NearbyRadius
			|| outsidePlaceTicks >= 600;
		outsidePlaceTicks = 0;
		lastPlaceWorld = world;
		lastVisitedPlace = observation.Anchor;
		if (!returning) return;
		Profile.Places.Observe(world, observation.Anchor);
		SyncProfileToBoundSigil();
		NPC.netUpdate = true;
		TraceDiagnostic("place: separated return; " + (observation.OwnerBedSpawn ? "owner bed" : observation.AssignedHousing ? "resident housing" : "possible shelter"));
	}

	private string? PlaceSpeechKey()
	{
		CompanionPlaceObservation observation = NearbyPlace();
		if (!observation.Valid || PlaceWorld == Guid.Empty) return null;
		CompanionPlace? memory = Profile.Places.FindNearby(PlaceWorld, observation.Anchor);
		if (!observation.IsCandidate) return memory?.IsHome == true ? "Places.FormerHome" : null;
		if (memory?.Role == CompanionPlaceRole.SharedHome) return "Places.SharedHome";
		if (memory?.Role == CompanionPlaceRole.CompanionHome) return "Places.CompanionHome";
		if (observation.OwnerBedSpawn) return "Places.PlayerHome";
		if (observation.AssignedHousing) return "Places.Residence";
		if (memory?.Role == CompanionPlaceRole.NotHome) return null;
		return memory?.IsFamiliar == true ? "Places.Familiar" : "Places.Shelter";
	}

	private bool CanOfferHomeQuestion()
	{
		CompanionPlaceObservation observation = NearbyPlace();
		return observation.IsCandidate && !observation.AssignedHousing
			&& Profile.Places.FindNearby(PlaceWorld, observation.Anchor) is { IsFamiliar: true, Role: CompanionPlaceRole.Shelter };
	}
	private bool CaptureHomeQuestion() => TryCaptureHomeQuestion(requireFamiliar: true);

	private bool TryCaptureHomeQuestion(bool requireFamiliar)
	{
		CompanionPlaceObservation observation = NearbyPlace();
		if (PlaceWorld == Guid.Empty || !observation.IsCandidate || observation.AssignedHousing
			|| requireFamiliar && !CanOfferHomeQuestion()) return false;
		questionPlaceWorld = PlaceWorld;
		questionPlaceAnchor = observation.Anchor;
		return true;
	}

	private CompanionConversationResult DiscussHome()
	{
		if (!NPC.active || !TryGetOwner(out Player owner) || owner.dead || FindBoundSigil() is null
			|| CompanionInventorySync.IsPending(owner)) return new(SoulmatesText.Get("TargetOrders.Invalid"), false);
		// Never replace an unanswered question or permission request with a configuration click.
		if (HasPendingQuestion || HasPendingInitiative) return new("", HasPendingQuestion);
		if (!CanKeepConversation() || autonomyActivity != AutonomyActivity.None || IsAttendingCritter || guardianTarget >= 0)
			return new(SoulmatesText.Get("Conversation.Home.Busy"), false);
		CompanionPlaceObservation observation = NearbyPlace();
		if (observation.AssignedHousing) return new(SoulmatesText.Get("Conversation.Home.ResidentHome"), false);
		if (!TryCaptureHomeQuestion(requireFamiliar: false))
			return new(SoulmatesText.Get("Conversation.Home.NoShelter"), false);
		StartChoiceQuestion(CompanionQuestion.Home, playerRequested: true);
		return new("", true);
	}
	private void ClearHomeQuestion()
	{
		questionPlaceWorld = Guid.Empty;
		questionPlaceAnchor = null;
	}
	private bool EvaluateHomeQuestion(CompanionAnswer answer)
	{
		if (answer == CompanionAnswer.Later) return true;
		if (questionPlaceAnchor is not { } anchor || questionPlaceWorld == Guid.Empty || questionPlaceWorld != PlaceWorld
			|| !TryGetOwner(out Player owner)) return false;
		CompanionPlaceObservation current = CompanionPlaces.SampleAt(owner, NPC, anchor);
		if (!current.IsCandidate || current.AssignedHousing
			|| Vector2.DistanceSquared(current.Anchor.ToVector2(), anchor.ToVector2()) > CompanionPlaces.NearbyRadius * CompanionPlaces.NearbyRadius) return false;
		CompanionPlaceRole role = answer switch {
			CompanionAnswer.First => CompanionPlaceRole.SharedHome,
			CompanionAnswer.Second => CompanionPlaceRole.CompanionHome,
			_ => CompanionPlaceRole.NotHome
		};
		Profile.Places.RememberRole(questionPlaceWorld, anchor, role);
		homeChestCache.Clear();
		TraceDiagnostic($"place: player classified a captured shelter as {role}");
		return true;
	}

	private bool IsKnownHomeChest(Point tile)
	{
		Guid world = PlaceWorld;
		if (homeChestWorld != world) { homeChestCache.Clear(); homeChestWorld = world; }
		if (homeChestCache.TryGetValue(tile, out bool known)) return known;
		if (!TryGetOwner(out Player owner) || world == Guid.Empty) return false;
		CompanionPlaceObservation observation = CompanionPlaces.SampleAt(owner, NPC, tile);
		known = observation.IsCandidate && (observation.OwnerBedSpawn || observation.AssignedHousing
			|| Profile.Places.FindNearby(world, observation.Anchor)?.IsHome == true);
		if (homeChestCache.Count >= 32) {
			using var oldest = homeChestCache.Keys.GetEnumerator();
			if (oldest.MoveNext()) homeChestCache.Remove(oldest.Current);
		}
		homeChestCache[tile] = known;
		return known;
	}
}
