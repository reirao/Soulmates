#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Common.Dialogue;
using Soulmates.Common.UI;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulmates;

public sealed class Soulmates : Mod
{
	private enum MessageType : byte
	{
		TalkRequest,
		TalkResponse,
		RecallRequest,
		TrinketRequest,
		PackWithdrawRequest,
		ProfileUpdate,
		EmoteRequest,
		CompanionSpeech,
		QuickActionRequest,
		QuickActionResponse,
		BehaviorObservationRequest,
		NativeEmoteRequest,
		CreateCompanionRequest,
		CreateCompanionResponse,
		InitiativePrompt,
		InitiativeResponseRequest,
		DirectOrderRequest,
		ManaRecovery,
		OwnerInventoryUpdate,
		ChoiceResponseRequest,
		RpsResult,
		NpcContextRequest,
		MiningRuleRequest,
		MiningObservationRequest,
		InventoryReceipt,
		InventoryResolved,
		MiningConfigRequest,
		PetConfigRequest
	}

	internal static ModKeybind TalkKeybind { get; private set; } = null!;
	internal static ModKeybind EmoteKeybind { get; private set; } = null!;

	public override void Load()
	{
		if (!Terraria.Main.dedServ) {
			CompanionVisuals.Load();
			TalkKeybind = KeybindLoader.RegisterKeybind(this, "TalkToCompanion", "V");
			EmoteKeybind = KeybindLoader.RegisterKeybind(this, "CompanionEmotes", "G");
		}
	}

	public override void Unload()
	{
		CompanionVisuals.Unload();
		TalkKeybind = null!;
		EmoteKeybind = null!;
	}

	internal static void SendTalkRequest(Guid profileId, TalkCategory category, int option, int memoryCursor,
		CompanionItemTopic itemTopic = CompanionItemTopic.All)
	{
		if (Main.netMode != NetmodeID.MultiplayerClient)
			return;
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.TalkRequest);
		packet.Write(profileId.ToByteArray());
		packet.Write((byte)category);
		packet.Write((byte)option);
		packet.Write(memoryCursor);
		packet.Write((byte)itemTopic);
		packet.Send();
	}

	internal static void SendRecallRequest(Guid profileId)
	{
		if (Main.netMode != NetmodeID.MultiplayerClient)
			return;
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.RecallRequest);
		packet.Write(profileId.ToByteArray());
		packet.Send();
	}

	internal static void SendTrinketRequest(Guid profileId, CompanionTrinket trinket)
	{
		if (Main.netMode != NetmodeID.MultiplayerClient)
			return;
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.TrinketRequest);
		packet.Write(profileId.ToByteArray());
		packet.Write((byte)trinket);
		packet.Send();
	}

	internal static void SendPackWithdrawRequest(CompanionProfile profile, int slot, bool singleItem, CompanionStorage storage = CompanionStorage.Pack)
	{
		if (Main.netMode != NetmodeID.MultiplayerClient)
			return;
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.PackWithdrawRequest);
		packet.Write(profile.Id.ToByteArray());
		packet.Write((byte)slot);
		packet.Write(singleItem);
		packet.Write((byte)storage);
		packet.Write(profile.StorageToken(storage, slot));
		packet.Send();
	}

	internal static void SendEmoteRequest(Guid profileId, CompanionEmote emote)
	{
		if (Main.netMode != NetmodeID.MultiplayerClient)
			return;
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.EmoteRequest);
		packet.Write(profileId.ToByteArray());
		packet.Write((byte)emote);
		packet.Send();
	}

	internal static void SendQuickActionRequest(Guid profileId, CompanionQuickAction action)
	{
		if (Main.netMode != NetmodeID.MultiplayerClient)
			return;
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.QuickActionRequest);
		packet.Write(profileId.ToByteArray());
		packet.Write((byte)action);
		packet.Send();
	}

	internal static void SendNpcContextRequest(Guid profileId, CompanionNpcAction action, int index, int expectedType)
	{
		if (Main.netMode != NetmodeID.MultiplayerClient) return;
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.NpcContextRequest);
		packet.Write(profileId.ToByteArray());
		packet.Write((byte)action);
		packet.Write((short)index);
		packet.Write(expectedType);
		packet.Send();
	}

	internal static void SendBehaviorObservation(Guid profileId, LearnedBehavior behavior)
	{
		if (Main.netMode != NetmodeID.MultiplayerClient)
			return;
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.BehaviorObservationRequest);
		packet.Write(profileId.ToByteArray());
		packet.Write((byte)behavior);
		packet.Send();
	}

	internal static void SendMiningRuleRequest(Guid profileId, bool ores, int tileType, bool enabled)
	{
		if (Main.netMode != NetmodeID.MultiplayerClient) return;
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.MiningRuleRequest);
		packet.Write(profileId.ToByteArray());
		packet.Write(ores);
		packet.Write(tileType);
		packet.Write(enabled);
		packet.Send();
	}

	internal static void SendMiningConfigRequest(Guid profileId, CompanionMiningApproach approach,
		CompanionMiningDirection direction, CompanionTunnelEnd end)
	{
		if (Main.netMode != NetmodeID.MultiplayerClient) return;
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.MiningConfigRequest);
		packet.Write(profileId.ToByteArray());
		packet.Write((byte)approach); packet.Write((byte)direction); packet.Write((byte)end);
		packet.Send();
	}

	internal static void SendRpsResult(Player owner, SoulboundCompanion companion, RpsMove playerMove, RpsMove companionMove)
	{
		if (Main.netMode != NetmodeID.Server) return;
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.RpsResult);
		packet.Write(companion.Profile.Id.ToByteArray());
		packet.Write((byte)playerMove);
		packet.Write((byte)companionMove);
		packet.Send(owner.whoAmI);
	}

	internal static void SendPetConfigRequest(CompanionProfile profile, int slot)
	{
		if (Main.netMode != NetmodeID.MultiplayerClient || slot < -1 || slot >= profile.Pack.Count) return;
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.PetConfigRequest);
		packet.Write(profile.Id.ToByteArray()); packet.Write(slot);
		packet.Write(slot < 0 ? 0 : profile.Pack[slot].type);
		packet.Write(slot < 0 ? new byte[32] : profile.StorageToken(CompanionStorage.Pack, slot));
		packet.Send();
	}

	internal static void SendCreateCompanionRequest(CompanionProfile profile)
	{
		if (Main.netMode != NetmodeID.MultiplayerClient)
			return;
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.CreateCompanionRequest);
		packet.Write(profile.Name);
		packet.Write((byte)profile.Muse);
		packet.Write((byte)profile.Form);
		packet.Write((byte)profile.Essence);
		packet.Write((byte)profile.Aura);
		packet.Write((byte)profile.Personality);
		packet.Write((byte)profile.Talent);
		packet.Send();
	}

	internal static void SendInitiativePrompt(Player player, SoulboundCompanion companion,
		CompanionInitiativeKind kind)
	{
		if (Main.netMode != NetmodeID.Server)
			return;
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.InitiativePrompt);
		packet.Write(companion.Profile.Id.ToString());
		packet.Write((byte)kind);
		packet.Write(companion.InitiativeId.ToByteArray());
		packet.Send(player.whoAmI);
	}

	internal static void SendInitiativeResponse(Guid profileId, Guid initiativeId, CompanionInitiativeKind kind,
		CompanionInitiativeResponse response)
	{
		if (Main.netMode != NetmodeID.MultiplayerClient)
			return;
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.InitiativeResponseRequest);
		packet.Write(profileId.ToByteArray());
		packet.Write(initiativeId.ToByteArray());
		packet.Write((byte)kind);
		packet.Write((byte)response);
		packet.Send();
	}

	internal static void SendDirectOrderRequest(Guid profileId, CompanionTargetOrder order, Point tileTarget, int itemTarget)
	{
		if (Main.netMode != NetmodeID.MultiplayerClient)
			return;
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.DirectOrderRequest);
		packet.Write(profileId.ToByteArray());
		packet.Write((byte)order);
		packet.Write((short)tileTarget.X);
		packet.Write((short)tileTarget.Y);
		packet.Write((short)itemTarget);
		Item? target = itemTarget >= 0 && itemTarget < Main.maxItems ? Main.item[itemTarget] : null;
		packet.Write(target?.type ?? 0);
		packet.Write(target?.prefix ?? 0);
		Tile tile = WorldGen.InWorld(tileTarget.X, tileTarget.Y) ? Main.tile[tileTarget.X, tileTarget.Y] : default;
		packet.Write(tile.HasTile ? (int)tile.TileType : -1);
		packet.Send();
	}

	internal static void SendMiningObservation(Guid profileId, Point target, int toolType)
	{
		if (Main.netMode != NetmodeID.MultiplayerClient) return;
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.MiningObservationRequest);
		packet.Write(profileId.ToByteArray());
		packet.Write((short)target.X);
		packet.Write((short)target.Y);
		packet.Write(toolType);
		packet.Send();
	}

	internal static void SendCompanionSpeech(Player player, SoulboundCompanion companion, string key, string argument = "")
	{
		if (Main.netMode != NetmodeID.Server)
			return;
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.CompanionSpeech);
		packet.Write(companion.Profile.Id.ToString());
		packet.Write(key);
		packet.Write(argument);
		packet.Send(player.whoAmI);
	}

	internal static void SendProfileUpdate(Player player, SoulboundCompanion companion, string message = "")
	{
		if (Main.netMode != NetmodeID.Server || CompanionInventorySync.IsEditing(player) || CompanionInventorySync.IsPending(player))
			return;
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.ProfileUpdate);
		packet.Write(message);
		companion.Profile.Write(packet);
		packet.Send(player.whoAmI);
	}

	public override void HandlePacket(BinaryReader reader, int whoAmI)
	{
		if (Main.netMode == NetmodeID.SinglePlayer || Main.netMode == NetmodeID.Server
			&& (whoAmI < 0 || whoAmI >= Main.maxPlayers || !Main.player[whoAmI].active))
			return;
		if (reader.BaseStream.CanSeek && reader.BaseStream.Position >= reader.BaseStream.Length)
			return;
		try {
			DispatchPacket(reader, whoAmI);
		}
		catch (Exception exception) when (exception is IOException or InvalidDataException or FormatException) {
			Logger.Debug("Ignored a malformed Soulmates packet.");
		}
	}

	private void DispatchPacket(BinaryReader reader, int whoAmI)
	{
		MessageType type = (MessageType)reader.ReadByte();
		bool serverResponse = type is MessageType.TalkResponse or MessageType.ProfileUpdate
			or MessageType.CompanionSpeech or MessageType.QuickActionResponse
			or MessageType.CreateCompanionResponse or MessageType.InitiativePrompt or MessageType.ManaRecovery
			or MessageType.OwnerInventoryUpdate or MessageType.RpsResult or MessageType.InventoryResolved;
		if (!Enum.IsDefined(type) || serverResponse != (Main.netMode == NetmodeID.MultiplayerClient))
			return;
		int minimumBytes = type switch {
			MessageType.RecallRequest => 16,
			MessageType.TalkRequest => 23,
			MessageType.PackWithdrawRequest => 51,
			MessageType.EmoteRequest or MessageType.QuickActionRequest or MessageType.TrinketRequest
				or MessageType.BehaviorObservationRequest => 17,
			MessageType.NativeEmoteRequest => 20,
			MessageType.TalkResponse or MessageType.CompanionSpeech
				or MessageType.QuickActionResponse or MessageType.CreateCompanionResponse => 3,
			MessageType.ManaRecovery => 4,
			MessageType.CreateCompanionRequest => 7,
			MessageType.ProfileUpdate => 2,
			MessageType.InitiativePrompt => 18,
			MessageType.InitiativeResponseRequest => 34,
			MessageType.DirectOrderRequest => 35,
			MessageType.OwnerInventoryUpdate => 18,
			MessageType.InventoryReceipt or MessageType.InventoryResolved => 18,
			MessageType.MiningObservationRequest => 24,
			MessageType.ChoiceResponseRequest => 33,
			MessageType.RpsResult => 18,
			MessageType.NpcContextRequest => 23,
			MessageType.MiningRuleRequest => 22,
			MessageType.MiningConfigRequest => 19,
			MessageType.PetConfigRequest => 56,
			_ => 1
		};
		if (reader.BaseStream.CanSeek && reader.BaseStream.Length - reader.BaseStream.Position < minimumBytes)
			return;
		switch (type) {
			case MessageType.TalkRequest:
				HandleTalkRequest(reader, whoAmI);
				break;
			case MessageType.TalkResponse:
				HandleTalkResponse(reader);
				break;
			case MessageType.RecallRequest:
				HandleRecallRequest(reader, whoAmI);
				break;
			case MessageType.TrinketRequest:
				HandleTrinketRequest(reader, whoAmI);
				break;
			case MessageType.PackWithdrawRequest:
				HandlePackWithdrawRequest(reader, whoAmI);
				break;
			case MessageType.PetConfigRequest:
				HandlePetConfigRequest(reader, whoAmI);
				break;
			case MessageType.ProfileUpdate:
				HandleProfileUpdate(reader);
				break;
			case MessageType.EmoteRequest:
				HandleEmoteRequest(reader, whoAmI);
				break;
			case MessageType.CompanionSpeech:
				HandleCompanionSpeech(reader);
				break;
			case MessageType.QuickActionRequest:
				HandleQuickActionRequest(reader, whoAmI);
				break;
			case MessageType.QuickActionResponse:
				HandleQuickActionResponse(reader);
				break;
			case MessageType.BehaviorObservationRequest:
				HandleBehaviorObservationRequest(reader, whoAmI);
				break;
			case MessageType.NativeEmoteRequest:
				// Reserved ordinal: native emotes use Terraria's observer, not this retired request.
				break;
			case MessageType.CreateCompanionRequest:
				HandleCreateCompanionRequest(reader, whoAmI);
				break;
			case MessageType.CreateCompanionResponse:
				HandleCreateCompanionResponse(reader);
				break;
			case MessageType.InitiativePrompt:
				HandleInitiativePrompt(reader);
				break;
			case MessageType.InitiativeResponseRequest:
				HandleInitiativeResponseRequest(reader, whoAmI);
				break;
			case MessageType.DirectOrderRequest:
				HandleDirectOrderRequest(reader, whoAmI);
				break;
			case MessageType.NpcContextRequest:
				HandleNpcContextRequest(reader, whoAmI);
				break;
			case MessageType.MiningRuleRequest:
				HandleMiningRuleRequest(reader, whoAmI);
				break;
			case MessageType.MiningConfigRequest:
				HandleMiningConfigRequest(reader, whoAmI);
				break;
			case MessageType.MiningObservationRequest:
				HandleMiningObservationRequest(reader, whoAmI);
				break;
			case MessageType.ManaRecovery:
				int amount = reader.ReadInt32();
				Player owner = Main.LocalPlayer;
				if (owner.dead || amount <= 0)
					break;
				int restored = Math.Min(amount, Math.Max(0, owner.statManaMax2 - owner.statMana));
				owner.statMana += restored;
				if (restored > 0)
					owner.ManaEffect(restored);
				break;
			case MessageType.OwnerInventoryUpdate:
				CompanionInventorySync.Apply(reader);
				break;
			case MessageType.InventoryReceipt:
				CompanionInventorySync.ReceiveReceipt(reader, whoAmI);
				break;
			case MessageType.InventoryResolved:
				CompanionInventorySync.Complete(reader);
				break;
			case MessageType.ChoiceResponseRequest:
				HandleChoiceResponseRequest(reader, whoAmI);
				break;
			case MessageType.RpsResult:
				byte[] rpsIdentity = reader.ReadBytes(16);
				if (rpsIdentity.Length != 16) throw new EndOfStreamException();
				Guid rpsProfile = new(rpsIdentity);
				RpsMove playerMove = (RpsMove)reader.ReadByte(), companionMove = (RpsMove)reader.ReadByte();
				if (rpsProfile != Guid.Empty && Enum.IsDefined(playerMove) && Enum.IsDefined(companionMove)
					&& SoulboundCompanion.FindFor(Main.LocalPlayer) is { } gameCompanion && gameCompanion.Profile.Id == rpsProfile)
					gameCompanion.ShowRpsResult(playerMove, companionMove);
				break;
		}
	}

	internal static void SendManaRecovery(Player player, int amount)
	{
		if (Main.netMode != NetmodeID.Server || amount <= 0)
			return;
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.ManaRecovery);
		packet.Write(amount);
		packet.Send(player.whoAmI);
	}

	private static SoulboundCompanion? FindRequestCompanion(Player player)
	{
		if (Main.netMode != NetmodeID.Server || !player.active || player.dead || CompanionInventorySync.IsPending(player))
			return null;
		SoulboundCompanion? companion = SoulboundCompanion.FindFor(player);
		return companion?.FindBoundSigil() is not null ? companion : null;
	}

	private static SoulboundCompanion? FindRequestCompanion(Player player, Guid profileId)
		=> profileId != Guid.Empty && FindRequestCompanion(player) is { } companion && companion.Profile.Id == profileId
			? companion : null;

	private static Guid ReadGuid(BinaryReader reader)
	{
		byte[] bytes = reader.ReadBytes(16);
		if (bytes.Length != 16) throw new EndOfStreamException();
		return new Guid(bytes);
	}

	private void HandleTalkRequest(BinaryReader reader, int whoAmI)
	{
		Guid profileId = ReadGuid(reader);
		TalkCategory category = (TalkCategory)reader.ReadByte();
		int option = reader.ReadByte();
		int memoryCursor = reader.ReadInt32();
		CompanionItemTopic itemTopic = (CompanionItemTopic)reader.ReadByte();
		if (Main.netMode != NetmodeID.Server || !Enum.IsDefined(category) || option is < 0 or > 2
			|| !Enum.IsDefined(itemTopic)
			|| whoAmI < 0 || whoAmI >= Main.maxPlayers || !Main.player[whoAmI].active)
			return;

		Player player = Main.player[whoAmI];
		if (FindRequestCompanion(player, profileId) is not { } companion)
			return;
		CompanionConversationResult result = companion.Converse(category, option, memoryCursor, itemTopic);
		SendInteractionResponse(whoAmI, companion, result, quick: false);
	}

	private static void HandleTalkResponse(BinaryReader reader)
	{
		string reply = reader.ReadString();
		bool accepted = reader.ReadBoolean();
		CompanionProfile profile = CompanionProfile.Read(reader);
		if (Main.netMode != NetmodeID.MultiplayerClient)
			return;
		ApplyClientProfile(profile);
		ModContent.GetInstance<TalkModeSystem>().ReceiveNetworkResponse(profile, reply, accepted);
	}

	private static void HandleRecallRequest(BinaryReader reader, int whoAmI)
	{
		Guid profileId = ReadGuid(reader);
		if (Main.netMode != NetmodeID.Server || whoAmI < 0 || whoAmI >= Main.maxPlayers
			|| !Main.player[whoAmI].active)
			return;
		FindRequestCompanion(Main.player[whoAmI], profileId)?.Recall();
	}

	private static void HandleTrinketRequest(BinaryReader reader, int whoAmI)
	{
		Guid profileId = ReadGuid(reader);
		CompanionTrinket trinket = (CompanionTrinket)reader.ReadByte();
		if (Main.netMode != NetmodeID.Server || trinket != CompanionTrinket.None
			|| whoAmI < 0 || whoAmI >= Main.maxPlayers || !Main.player[whoAmI].active)
			return;
		Player player = Main.player[whoAmI];
		if (FindRequestCompanion(player, profileId) is not { } companion)
			return;
		companion.EquipTrinket(trinket);
		string message = SoulmatesText.Get("Messages.TrinketRemoved", companion.Profile.Name);
		SendProfileUpdate(player, companion, message);
	}

	private void HandlePackWithdrawRequest(BinaryReader reader, int whoAmI)
	{
		Guid profileId = ReadGuid(reader);
		int slot = reader.ReadByte();
		bool singleItem = reader.ReadBoolean();
		CompanionStorage storage = (CompanionStorage)reader.ReadByte();
		byte[] token = reader.ReadBytes(32);
		int slots = storage == CompanionStorage.Wallet ? 4
			: CompanionProfile.MaximumPackSlots + CompanionProfile.MaximumResourceSlots;
		if (Main.netMode != NetmodeID.Server || !Enum.IsDefined(storage) || slot >= slots
			|| whoAmI < 0 || whoAmI >= Main.maxPlayers || !Main.player[whoAmI].active)
			return;
		Player player = Main.player[whoAmI];
		if (FindRequestCompanion(player, profileId) is not { } companion)
			return;
		bool unchanged = token.Length == 32 && token.AsSpan().SequenceEqual(companion.Profile.StorageToken(storage, slot));
		string reply = unchanged ? companion.WithdrawStorageSlot(storage, slot, singleItem)
			: SoulmatesText.Get("UI.CompanionWheel.MouseModes.TargetChanged");
		SendInteractionResponse(whoAmI, companion, new(reply, unchanged), quick: false);
	}

	private static void HandleProfileUpdate(BinaryReader reader)
	{
		string message = reader.ReadString();
		CompanionProfile profile = CompanionProfile.Read(reader);
		if (Main.netMode != NetmodeID.MultiplayerClient)
			return;
		ApplyClientProfile(profile);
		ModContent.GetInstance<TalkModeSystem>().ReceiveEquipmentProfile(profile, message);
		if (!string.IsNullOrWhiteSpace(message))
			Main.NewText(message, profile.EssenceColor);
	}

	private static void HandleEmoteRequest(BinaryReader reader, int whoAmI)
	{
		Guid profileId = ReadGuid(reader);
		CompanionEmote emote = (CompanionEmote)reader.ReadByte();
		if (Main.netMode != NetmodeID.Server || !Enum.IsDefined(emote)
			|| whoAmI < 0 || whoAmI >= Main.maxPlayers || !Main.player[whoAmI].active)
			return;
		if (FindRequestCompanion(Main.player[whoAmI], profileId) is { } companion)
			companion.PerformEmote(emote);
	}

	private static void HandleCompanionSpeech(BinaryReader reader)
	{
		Guid profileId = Guid.TryParse(reader.ReadString(), out Guid parsed) ? parsed : Guid.Empty;
		string key = reader.ReadString();
		string argument = reader.ReadString();
		if (Main.netMode != NetmodeID.MultiplayerClient || profileId == Guid.Empty)
			return;
		if (SoulboundCompanion.FindFor(Main.LocalPlayer) is { } companion && companion.Profile.Id == profileId)
			companion.ShowLocalizedSpeech(key, argument);
	}

	private void HandleQuickActionRequest(BinaryReader reader, int whoAmI)
	{
		Guid profileId = ReadGuid(reader);
		CompanionQuickAction action = (CompanionQuickAction)reader.ReadByte();
		if (Main.netMode != NetmodeID.Server || !Enum.IsDefined(action) || action == CompanionQuickAction.Details
			|| whoAmI < 0 || whoAmI >= Main.maxPlayers || !Main.player[whoAmI].active)
			return;
		if (FindRequestCompanion(Main.player[whoAmI], profileId) is not { } companion)
			return;

		CompanionConversationResult result = companion.PerformQuickAction(action);
		SendInteractionResponse(whoAmI, companion, result, quick: true);
	}

	private static void HandleQuickActionResponse(BinaryReader reader)
	{
		bool accepted = reader.ReadBoolean();
		string reply = reader.ReadString();
		CompanionProfile profile = CompanionProfile.Read(reader);
		if (Main.netMode != NetmodeID.MultiplayerClient)
			return;

		ApplyClientProfile(profile);
		ModContent.GetInstance<TalkModeSystem>().ReceiveTaskResponse(profile, reply, accepted);
		if (SoulboundCompanion.FindFor(Main.LocalPlayer) is { } companion && companion.Profile.Id == profile.Id)
			companion.ShowSpeech(reply);
		SoundEngine.PlaySound(accepted ? SoundID.Chat : SoundID.MenuClose);
	}

	private static void HandleBehaviorObservationRequest(BinaryReader reader, int whoAmI)
	{
		Guid profileId = ReadGuid(reader);
		LearnedBehavior behavior = (LearnedBehavior)reader.ReadByte();
		if (Main.netMode != NetmodeID.Server || behavior != LearnedBehavior.Gathering
			|| whoAmI < 0 || whoAmI >= Main.maxPlayers || !Main.player[whoAmI].active)
			return;
		Player player = Main.player[whoAmI];
		if (FindRequestCompanion(player, profileId) is not { } companion
			|| !player.GetModPlayer<SoulmatesPlayer>().TryAcceptGatheringObservation())
			return;
		companion.ObserveOwnerActivity(behavior);
	}

	private static void HandleMiningObservationRequest(BinaryReader reader, int whoAmI)
	{
		Guid profileId = ReadGuid(reader);
		Point target = new(reader.ReadInt16(), reader.ReadInt16());
		int toolType = reader.ReadInt32();
		if (FindRequestCompanion(Main.player[whoAmI], profileId) is not { } companion
			|| !Main.player[whoAmI].GetModPlayer<SoulmatesPlayer>().TryAcceptMiningObservation()) return;
		companion.ObserveMiningTarget(target, toolType);
	}

	private void HandleCreateCompanionRequest(BinaryReader reader, int whoAmI)
	{
		string name = CompanionCreationService.NormalizeName(reader.ReadString());
		CompanionMuse muse = (CompanionMuse)reader.ReadByte();
		CompanionForm form = (CompanionForm)reader.ReadByte();
		CompanionEssence essence = (CompanionEssence)reader.ReadByte();
		CompanionAura aura = (CompanionAura)reader.ReadByte();
		CompanionPersonality personality = (CompanionPersonality)reader.ReadByte();
		CompanionTalent talent = (CompanionTalent)reader.ReadByte();
		if (Main.netMode != NetmodeID.Server || whoAmI < 0 || whoAmI >= Main.maxPlayers
			|| !Main.player[whoAmI].active)
			return;

		Player player = Main.player[whoAmI];
		if (CompanionInventorySync.IsPending(player)) return;
		var request = new CompanionProfile {
			Name = name,
			Muse = muse,
			Form = form,
			Essence = essence,
			Aura = aura,
			Personality = personality,
			Talent = talent
		};
		CompanionCreationResult result;
		CompanionProfile profile;
		using (var inventorySync = new CompanionInventorySync(player))
			result = CompanionCreationService.TryCreate(player, request, out profile);
		SendCreateCompanionResponse(whoAmI, result, profile.Name, profile.Essence);
	}

	private static void HandleCreateCompanionResponse(BinaryReader reader)
	{
		CompanionCreationResult result = (CompanionCreationResult)reader.ReadByte();
		string name = reader.ReadString();
		CompanionEssence essence = (CompanionEssence)reader.ReadByte();
		if (Main.netMode != NetmodeID.MultiplayerClient)
			return;
		if (!Enum.IsDefined(result))
			result = CompanionCreationResult.InvalidRequest;
		if (!Enum.IsDefined(essence))
			essence = CompanionEssence.Starlight;
		ModContent.GetInstance<SoulCreatorSystem>().ReceiveNetworkResponse(result, name, essence);
	}

	private static void HandleInitiativePrompt(BinaryReader reader)
	{
		Guid profileId = Guid.TryParse(reader.ReadString(), out Guid parsed) ? parsed : Guid.Empty;
		CompanionInitiativeKind kind = (CompanionInitiativeKind)reader.ReadByte();
		Guid initiativeId = ReadGuid(reader);
		if (Main.netMode != NetmodeID.MultiplayerClient || profileId == Guid.Empty || !Enum.IsDefined(kind))
			return;
		if (SoulboundCompanion.FindFor(Main.LocalPlayer) is { } companion && companion.Profile.Id == profileId)
			companion.ReceiveInitiativePrompt(kind, initiativeId);
	}

	private static void HandleInitiativeResponseRequest(BinaryReader reader, int whoAmI)
	{
		Guid profileId = ReadGuid(reader);
		Guid initiativeId = ReadGuid(reader);
		CompanionInitiativeKind kind = (CompanionInitiativeKind)reader.ReadByte();
		CompanionInitiativeResponse response = (CompanionInitiativeResponse)reader.ReadByte();
		if (Main.netMode != NetmodeID.Server || !Enum.IsDefined(kind) || !Enum.IsDefined(response)
			|| whoAmI < 0 || whoAmI >= Main.maxPlayers || !Main.player[whoAmI].active)
			return;
		Player player = Main.player[whoAmI];
		if (FindRequestCompanion(player, profileId) is not { } companion || !companion.HasPendingInitiative
			|| companion.PendingInitiativeKind != kind || !companion.RespondToInitiative(initiativeId, response))
			return;
		SendProfileUpdate(player, companion);
	}

	internal static void SendChoiceResponse(Guid profileId, Guid questionId, CompanionAnswer answer)
	{
		if (Main.netMode != NetmodeID.MultiplayerClient) return;
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.ChoiceResponseRequest);
		packet.Write(profileId.ToByteArray());
		packet.Write(questionId.ToByteArray());
		packet.Write((byte)answer);
		packet.Send();
	}

	private static void HandleChoiceResponseRequest(BinaryReader reader, int whoAmI)
	{
		var profileId = new Guid(reader.ReadBytes(16));
		var questionId = new Guid(reader.ReadBytes(16));
		CompanionAnswer answer = (CompanionAnswer)reader.ReadByte();
		if (Main.netMode != NetmodeID.Server || !Enum.IsDefined(answer)
			|| whoAmI < 0 || whoAmI >= Main.maxPlayers || !Main.player[whoAmI].active) return;
		Player owner = Main.player[whoAmI];
		if (FindRequestCompanion(owner) is not { } companion || companion.Profile.Id != profileId
			|| !companion.RespondToQuestion(questionId, answer)) return;
		SendProfileUpdate(owner, companion);
	}

	private void HandleNpcContextRequest(BinaryReader reader, int whoAmI)
	{
		var profileId = new Guid(reader.ReadBytes(16));
		CompanionNpcAction action = (CompanionNpcAction)reader.ReadByte();
		int index = reader.ReadInt16();
		int expectedType = reader.ReadInt32();
		if (Main.netMode != NetmodeID.Server || !Enum.IsDefined(action) || whoAmI < 0 || whoAmI >= Main.maxPlayers
			|| !Main.player[whoAmI].active || FindRequestCompanion(Main.player[whoAmI]) is not { } companion
			|| companion.Profile.Id != profileId) return;
		CompanionConversationResult result = companion.PerformNpcContext(action, index, expectedType);
		SendInteractionResponse(whoAmI, companion, result, quick: true);
	}

	private void HandleMiningRuleRequest(BinaryReader reader, int whoAmI)
	{
		var profileId = new Guid(reader.ReadBytes(16));
		bool ores = reader.ReadBoolean();
		int tileType = reader.ReadInt32();
		bool enabled = reader.ReadBoolean();
		if (Main.netMode != NetmodeID.Server || FindRequestCompanion(Main.player[whoAmI]) is not { } companion
			|| companion.Profile.Id != profileId || !companion.SetAutomaticMiningRule(ores, tileType, enabled)) return;
		SendProfileUpdate(Main.player[whoAmI], companion);
	}

	private void HandleMiningConfigRequest(BinaryReader reader, int whoAmI)
	{
		Guid profileId = ReadGuid(reader);
		CompanionMiningApproach approach = (CompanionMiningApproach)reader.ReadByte();
		CompanionMiningDirection direction = (CompanionMiningDirection)reader.ReadByte();
		CompanionTunnelEnd end = (CompanionTunnelEnd)reader.ReadByte();
		if (Main.netMode != NetmodeID.Server || FindRequestCompanion(Main.player[whoAmI], profileId) is not { } companion
			|| !companion.ConfigureMining(approach, direction, end)) return;
		SendProfileUpdate(Main.player[whoAmI], companion);
	}

	private void HandlePetConfigRequest(BinaryReader reader, int whoAmI)
	{
		Guid profileId = ReadGuid(reader);
		int slot = reader.ReadInt32(), expectedType = reader.ReadInt32();
		byte[] token = reader.ReadBytes(32);
		if (Main.netMode != NetmodeID.Server || FindRequestCompanion(Main.player[whoAmI], profileId) is not { } companion
			|| !companion.ConfigurePet(slot, expectedType, token)) return;
		SendProfileUpdate(Main.player[whoAmI], companion);
	}

	private void HandleDirectOrderRequest(BinaryReader reader, int whoAmI)
	{
		Guid profileId = ReadGuid(reader);
		CompanionTargetOrder order = (CompanionTargetOrder)reader.ReadByte();
		var tileTarget = new Point(reader.ReadInt16(), reader.ReadInt16());
		int itemTarget = reader.ReadInt16();
		int expectedType = reader.ReadInt32();
		int expectedPrefix = reader.ReadInt32();
		int expectedTile = reader.ReadInt32();
		if (Main.netMode != NetmodeID.Server || !Enum.IsDefined(order)
			|| whoAmI < 0 || whoAmI >= Main.maxPlayers || !Main.player[whoAmI].active)
			return;
		if (FindRequestCompanion(Main.player[whoAmI], profileId) is not { } companion)
			return;

		bool changedItem = (order is CompanionTargetOrder.Gather or CompanionTargetOrder.Look) && itemTarget >= 0
			&& (itemTarget >= Main.maxItems || !Main.item[itemTarget].active
				|| Main.item[itemTarget].type != expectedType || Main.item[itemTarget].prefix != expectedPrefix);
		bool changedTile = order is CompanionTargetOrder.Mine or CompanionTargetOrder.Forest
			&& (!WorldGen.InWorld(tileTarget.X, tileTarget.Y)
				|| (Main.tile[tileTarget.X, tileTarget.Y].HasTile ? Main.tile[tileTarget.X, tileTarget.Y].TileType : -1) != expectedTile
				|| order == CompanionTargetOrder.Mine && expectedTile < 0);
		CompanionConversationResult result = changedItem || changedTile
			? new CompanionConversationResult(SoulmatesText.Get("TargetOrders.TargetLost"), false)
			: companion.PerformDirectOrder(order, tileTarget, itemTarget);
		SendInteractionResponse(whoAmI, companion, result, quick: true);
	}

	private void SendInteractionResponse(int playerIndex, SoulboundCompanion companion,
		CompanionConversationResult result, bool quick)
		=> CompanionInventorySync.RespondOrDefer(playerIndex, committed => {
			ModPacket packet = GetPacket();
			packet.Write((byte)(quick ? MessageType.QuickActionResponse : MessageType.TalkResponse));
			string reply = committed ? result.Reply : SoulmatesText.Get("TargetOrders.TargetLost");
			bool accepted = committed && result.Accepted;
			if (quick) { packet.Write(accepted); packet.Write(reply); }
			else { packet.Write(reply); packet.Write(accepted); }
			companion.Profile.Write(packet); packet.Send(playerIndex);
		});

	private void SendCreateCompanionResponse(int playerIndex, CompanionCreationResult result, string name, CompanionEssence essence)
	{
		if (Main.netMode != NetmodeID.Server || playerIndex < 0 || playerIndex >= Main.maxPlayers)
			return;
		CompanionInventorySync.RespondOrDefer(playerIndex, committed => {
			ModPacket packet = GetPacket();
			packet.Write((byte)MessageType.CreateCompanionResponse);
			packet.Write((byte)(committed ? result : CompanionCreationResult.InvalidRequest));
			packet.Write(name); packet.Write((byte)essence); packet.Send(playerIndex);
		});
	}

	internal static void SendOwnerInventoryUpdate(Player player, byte[] payload)
	{
		if (Main.netMode != NetmodeID.Server || !player.active
			|| player.whoAmI < 0 || player.whoAmI >= Main.maxPlayers)
			return;
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.OwnerInventoryUpdate);
		packet.Write(payload);
		packet.Send(player.whoAmI);
	}

	internal static void SendInventoryReceipt(Guid id, bool accepted, byte[] inventory)
	{
		if (Main.netMode != NetmodeID.MultiplayerClient) return;
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.InventoryReceipt);
		packet.Write(id.ToByteArray()); packet.Write(accepted); packet.Write(inventory); packet.Send();
	}

	internal static void SendInventoryResolved(Player player, Guid id, bool accepted, CompanionProfile? profile)
	{
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.InventoryResolved);
		packet.Write(id.ToByteArray()); packet.Write(accepted); packet.Write(profile is not null);
		profile?.Write(packet); packet.Send(player.whoAmI);
	}

	internal static void ApplyClientProfile(CompanionProfile profile)
	{
		Player player = Main.LocalPlayer;
		if (SoulboundCompanion.FindFor(player) is { } companion && companion.Profile.Id == profile.Id)
			companion.Profile = profile.Clone();
		for (int slot = 0; slot < player.inventory.Length; slot++) {
			Item item = player.inventory[slot];
			if (item.ModItem is SoulboundSigil sigil && sigil.Profile.Id == profile.Id) {
				sigil.Profile = profile.Clone();
				Main.clientPlayer.inventory[slot] = item.Clone();
				break;
			}
		}
	}
}
