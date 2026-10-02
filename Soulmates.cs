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
		MiningRuleRequest
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

	internal static void SendTalkRequest(TalkCategory category, int option, int memoryCursor,
		CompanionItemTopic itemTopic = CompanionItemTopic.All)
	{
		if (Main.netMode != NetmodeID.MultiplayerClient)
			return;
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.TalkRequest);
		packet.Write((byte)category);
		packet.Write((byte)option);
		packet.Write(memoryCursor);
		packet.Write((byte)itemTopic);
		packet.Send();
	}

	internal static void SendRecallRequest()
	{
		if (Main.netMode != NetmodeID.MultiplayerClient)
			return;
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.RecallRequest);
		packet.Send();
	}

	internal static void SendTrinketRequest(CompanionTrinket trinket)
	{
		if (Main.netMode != NetmodeID.MultiplayerClient)
			return;
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.TrinketRequest);
		packet.Write((byte)trinket);
		packet.Send();
	}

	internal static void SendPackWithdrawRequest(int slot, bool singleItem, CompanionStorage storage = CompanionStorage.Pack)
	{
		if (Main.netMode != NetmodeID.MultiplayerClient)
			return;
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.PackWithdrawRequest);
		packet.Write((byte)slot);
		packet.Write(singleItem);
		packet.Write((byte)storage);
		packet.Send();
	}

	internal static void SendEmoteRequest(CompanionEmote emote)
	{
		if (Main.netMode != NetmodeID.MultiplayerClient)
			return;
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.EmoteRequest);
		packet.Write((byte)emote);
		packet.Send();
	}

	internal static void SendQuickActionRequest(CompanionQuickAction action)
	{
		if (Main.netMode != NetmodeID.MultiplayerClient)
			return;
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.QuickActionRequest);
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

	internal static void SendBehaviorObservation(LearnedBehavior behavior)
	{
		if (Main.netMode != NetmodeID.MultiplayerClient)
			return;
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.BehaviorObservationRequest);
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

	internal static void SendNativeEmoteRequest(int emoteId)
	{
		if (Main.netMode != NetmodeID.MultiplayerClient)
			return;
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.NativeEmoteRequest);
		packet.Write(emoteId);
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
		packet.Send(player.whoAmI);
	}

	internal static void SendInitiativeResponse(CompanionInitiativeKind kind,
		CompanionInitiativeResponse response)
	{
		if (Main.netMode != NetmodeID.MultiplayerClient)
			return;
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.InitiativeResponseRequest);
		packet.Write((byte)kind);
		packet.Write((byte)response);
		packet.Send();
	}

	internal static void SendDirectOrderRequest(CompanionTargetOrder order, Point tileTarget, int itemTarget)
	{
		if (Main.netMode != NetmodeID.MultiplayerClient)
			return;
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.DirectOrderRequest);
		packet.Write((byte)order);
		packet.Write((short)tileTarget.X);
		packet.Write((short)tileTarget.Y);
		packet.Write((short)itemTarget);
		Item? target = itemTarget >= 0 && itemTarget < Main.maxItems ? Main.item[itemTarget] : null;
		packet.Write(target?.type ?? 0);
		packet.Write(target?.prefix ?? (byte)0);
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
		if (Main.netMode != NetmodeID.Server)
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
			or MessageType.OwnerInventoryUpdate or MessageType.RpsResult;
		if (!Enum.IsDefined(type) || serverResponse != (Main.netMode == NetmodeID.MultiplayerClient))
			return;
		int minimumBytes = type switch {
			MessageType.RecallRequest => 0,
			MessageType.TalkRequest => 7,
			MessageType.PackWithdrawRequest or MessageType.TalkResponse or MessageType.CompanionSpeech
				or MessageType.QuickActionResponse or MessageType.CreateCompanionResponse => 3,
			MessageType.NativeEmoteRequest or MessageType.ManaRecovery => 4,
			MessageType.CreateCompanionRequest => 7,
			MessageType.ProfileUpdate or MessageType.InitiativePrompt or MessageType.InitiativeResponseRequest => 2,
			MessageType.DirectOrderRequest => 12,
			MessageType.ChoiceResponseRequest => 33,
			MessageType.RpsResult => 18,
			MessageType.NpcContextRequest => 23,
			MessageType.MiningRuleRequest => 22,
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
				HandleRecallRequest(whoAmI);
				break;
			case MessageType.TrinketRequest:
				HandleTrinketRequest(reader, whoAmI);
				break;
			case MessageType.PackWithdrawRequest:
				HandlePackWithdrawRequest(reader, whoAmI);
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
				HandleNativeEmoteRequest(reader, whoAmI);
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
		if (Main.netMode != NetmodeID.Server || !player.active || player.dead)
			return null;
		SoulboundCompanion? companion = SoulboundCompanion.FindFor(player);
		return companion?.FindBoundSigil() is not null ? companion : null;
	}

	private void HandleTalkRequest(BinaryReader reader, int whoAmI)
	{
		TalkCategory category = (TalkCategory)reader.ReadByte();
		int option = reader.ReadByte();
		int memoryCursor = reader.ReadInt32();
		CompanionItemTopic itemTopic = (CompanionItemTopic)reader.ReadByte();
		if (Main.netMode != NetmodeID.Server || !Enum.IsDefined(category) || option is < 0 or > 2
			|| !Enum.IsDefined(itemTopic)
			|| whoAmI < 0 || whoAmI >= Main.maxPlayers || !Main.player[whoAmI].active)
			return;

		Player player = Main.player[whoAmI];
		if (FindRequestCompanion(player) is not { } companion)
			return;
		CompanionConversationResult result = companion.Converse(category, option, memoryCursor, itemTopic);
		ModPacket packet = GetPacket();
		packet.Write((byte)MessageType.TalkResponse);
		packet.Write(result.Reply);
		packet.Write(result.Accepted);
		companion.Profile.Write(packet);
		packet.Send(whoAmI);
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

	private static void HandleRecallRequest(int whoAmI)
	{
		if (Main.netMode != NetmodeID.Server || whoAmI < 0 || whoAmI >= Main.maxPlayers
			|| !Main.player[whoAmI].active)
			return;
		SoulboundCompanion.RecallAllFor(Main.player[whoAmI]);
	}

	private static void HandleTrinketRequest(BinaryReader reader, int whoAmI)
	{
		CompanionTrinket trinket = (CompanionTrinket)reader.ReadByte();
		if (Main.netMode != NetmodeID.Server || !Enum.IsDefined(trinket)
			|| whoAmI < 0 || whoAmI >= Main.maxPlayers || !Main.player[whoAmI].active)
			return;
		Player player = Main.player[whoAmI];
		if (FindRequestCompanion(player) is not { } companion)
			return;
		companion.EquipTrinket(trinket);
		string message = trinket == CompanionTrinket.None
			? SoulmatesText.Get("Messages.TrinketRemoved", companion.Profile.Name)
			: SoulmatesText.Get("Messages.TrinketEquipped", companion.Profile.Name, SoulmatesText.EnumName(trinket));
		SendProfileUpdate(player, companion, message);
	}

	private void HandlePackWithdrawRequest(BinaryReader reader, int whoAmI)
	{
		int slot = reader.ReadByte();
		bool singleItem = reader.ReadBoolean();
		CompanionStorage storage = (CompanionStorage)reader.ReadByte();
		int slots = storage == CompanionStorage.Wallet ? 4
			: CompanionProfile.MaximumPackSlots + CompanionProfile.MaximumResourceSlots;
		if (Main.netMode != NetmodeID.Server || !Enum.IsDefined(storage) || slot >= slots
			|| whoAmI < 0 || whoAmI >= Main.maxPlayers || !Main.player[whoAmI].active)
			return;
		Player player = Main.player[whoAmI];
		if (FindRequestCompanion(player) is not { } companion)
			return;
		string reply = companion.WithdrawStorageSlot(storage, slot, singleItem);
		ModPacket packet = GetPacket();
		packet.Write((byte)MessageType.TalkResponse);
		packet.Write(reply);
		packet.Write(true);
		companion.Profile.Write(packet);
		packet.Send(whoAmI);
	}

	private static void HandleProfileUpdate(BinaryReader reader)
	{
		string message = reader.ReadString();
		CompanionProfile profile = CompanionProfile.Read(reader);
		if (Main.netMode != NetmodeID.MultiplayerClient)
			return;
		ApplyClientProfile(profile);
		if (!string.IsNullOrWhiteSpace(message))
			Main.NewText(message, profile.EssenceColor);
	}

	private static void HandleEmoteRequest(BinaryReader reader, int whoAmI)
	{
		CompanionEmote emote = (CompanionEmote)reader.ReadByte();
		if (Main.netMode != NetmodeID.Server || !Enum.IsDefined(emote)
			|| whoAmI < 0 || whoAmI >= Main.maxPlayers || !Main.player[whoAmI].active)
			return;
		if (FindRequestCompanion(Main.player[whoAmI]) is { } companion)
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
		CompanionQuickAction action = (CompanionQuickAction)reader.ReadByte();
		if (Main.netMode != NetmodeID.Server || !Enum.IsDefined(action) || action == CompanionQuickAction.Details
			|| whoAmI < 0 || whoAmI >= Main.maxPlayers || !Main.player[whoAmI].active)
			return;
		if (FindRequestCompanion(Main.player[whoAmI]) is not { } companion)
			return;

		CompanionConversationResult result = companion.PerformQuickAction(action);
		ModPacket packet = GetPacket();
		packet.Write((byte)MessageType.QuickActionResponse);
		packet.Write(result.Accepted);
		packet.Write(result.Reply);
		companion.Profile.Write(packet);
		packet.Send(whoAmI);
	}

	private static void HandleQuickActionResponse(BinaryReader reader)
	{
		bool accepted = reader.ReadBoolean();
		string reply = reader.ReadString();
		CompanionProfile profile = CompanionProfile.Read(reader);
		if (Main.netMode != NetmodeID.MultiplayerClient)
			return;

		ApplyClientProfile(profile);
		if (SoulboundCompanion.FindFor(Main.LocalPlayer) is { } companion && companion.Profile.Id == profile.Id)
			companion.ShowSpeech(reply);
		SoundEngine.PlaySound(accepted ? SoundID.Chat : SoundID.MenuClose);
	}

	private static void HandleBehaviorObservationRequest(BinaryReader reader, int whoAmI)
	{
		LearnedBehavior behavior = (LearnedBehavior)reader.ReadByte();
		if (Main.netMode != NetmodeID.Server || behavior != LearnedBehavior.Gathering
			|| whoAmI < 0 || whoAmI >= Main.maxPlayers || !Main.player[whoAmI].active)
			return;
		Player player = Main.player[whoAmI];
		if (!player.GetModPlayer<SoulmatesPlayer>().TryAcceptGatheringObservation())
			return;
		if (FindRequestCompanion(player) is { } companion)
			companion.ObserveOwnerActivity(behavior);
	}

	private static void HandleNativeEmoteRequest(BinaryReader reader, int whoAmI)
	{
		int emoteId = reader.ReadInt32();
		if (Main.netMode != NetmodeID.Server || emoteId < 0 || emoteId >= EmoteBubbleLoader.EmoteBubbleCount
			|| whoAmI < 0 || whoAmI >= Main.maxPlayers || !Main.player[whoAmI].active)
			return;
		if (FindRequestCompanion(Main.player[whoAmI]) is { } companion)
			companion.ReactToNativeEmote(emoteId);
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
		if (Main.netMode != NetmodeID.MultiplayerClient || profileId == Guid.Empty || !Enum.IsDefined(kind))
			return;
		if (SoulboundCompanion.FindFor(Main.LocalPlayer) is { } companion && companion.Profile.Id == profileId)
			companion.ReceiveInitiativePrompt(kind);
	}

	private static void HandleInitiativeResponseRequest(BinaryReader reader, int whoAmI)
	{
		CompanionInitiativeKind kind = (CompanionInitiativeKind)reader.ReadByte();
		CompanionInitiativeResponse response = (CompanionInitiativeResponse)reader.ReadByte();
		if (Main.netMode != NetmodeID.Server || !Enum.IsDefined(kind) || !Enum.IsDefined(response)
			|| whoAmI < 0 || whoAmI >= Main.maxPlayers || !Main.player[whoAmI].active)
			return;
		Player player = Main.player[whoAmI];
		if (FindRequestCompanion(player) is not { } companion || !companion.HasPendingInitiative
			|| companion.PendingInitiativeKind != kind || !companion.RespondToInitiative(response))
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
		ModPacket packet = GetPacket();
		packet.Write((byte)MessageType.QuickActionResponse);
		packet.Write(result.Accepted);
		packet.Write(result.Reply);
		companion.Profile.Write(packet);
		packet.Send(whoAmI);
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

	private void HandleDirectOrderRequest(BinaryReader reader, int whoAmI)
	{
		CompanionTargetOrder order = (CompanionTargetOrder)reader.ReadByte();
		var tileTarget = new Point(reader.ReadInt16(), reader.ReadInt16());
		int itemTarget = reader.ReadInt16();
		int expectedType = reader.ReadInt32();
		byte expectedPrefix = reader.ReadByte();
		if (Main.netMode != NetmodeID.Server || !Enum.IsDefined(order)
			|| whoAmI < 0 || whoAmI >= Main.maxPlayers || !Main.player[whoAmI].active)
			return;
		if (FindRequestCompanion(Main.player[whoAmI]) is not { } companion)
			return;

		bool changedItem = (order is CompanionTargetOrder.Gather or CompanionTargetOrder.Look) && itemTarget >= 0
			&& (itemTarget >= Main.maxItems || !Main.item[itemTarget].active
				|| Main.item[itemTarget].type != expectedType || Main.item[itemTarget].prefix != expectedPrefix);
		CompanionConversationResult result = changedItem
			? new CompanionConversationResult(SoulmatesText.Get("TargetOrders.TargetLost"), false)
			: companion.PerformDirectOrder(order, tileTarget, itemTarget);
		ModPacket packet = GetPacket();
		packet.Write((byte)MessageType.QuickActionResponse);
		packet.Write(result.Accepted);
		packet.Write(result.Reply);
		companion.Profile.Write(packet);
		packet.Send(whoAmI);
	}

	private void SendCreateCompanionResponse(int playerIndex, CompanionCreationResult result, string name, CompanionEssence essence)
	{
		if (Main.netMode != NetmodeID.Server || playerIndex < 0 || playerIndex >= Main.maxPlayers)
			return;
		ModPacket packet = GetPacket();
		packet.Write((byte)MessageType.CreateCompanionResponse);
		packet.Write((byte)result);
		packet.Write(name);
		packet.Write((byte)essence);
		packet.Send(playerIndex);
	}

	internal static void SendOwnerInventoryUpdate(Player player, IReadOnlyList<(byte Slot, byte[] Data)> changes)
	{
		if (Main.netMode != NetmodeID.Server || changes.Count == 0 || !player.active
			|| player.whoAmI < 0 || player.whoAmI >= Main.maxPlayers)
			return;
		// Native SyncEquipment ignores unsolicited updates to the owning client's inventory.
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.OwnerInventoryUpdate);
		packet.Write((byte)changes.Count);
		foreach ((byte slot, byte[] data) in changes) {
			packet.Write(slot);
			packet.Write(data);
		}
		packet.Send(player.whoAmI);
	}

	private static void ApplyClientProfile(CompanionProfile profile)
	{
		Player player = Main.LocalPlayer;
		if (SoulboundCompanion.FindFor(player) is { } companion && companion.Profile.Id == profile.Id)
			companion.Profile = profile.Clone();
		foreach (Item item in player.inventory) {
			if (item.ModItem is SoulboundSigil sigil && sigil.Profile.Id == profile.Id) {
				sigil.Profile = profile.Clone();
				break;
			}
		}
	}
}
