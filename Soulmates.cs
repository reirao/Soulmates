using System;
using System.IO;
using Soulmates.Common;
using Soulmates.Common.Dialogue;
using Soulmates.Common.UI;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Terraria;
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
		SummonRequest,
		TrinketRequest,
		PackWithdrawRequest,
		ProfileUpdate,
		EmoteRequest,
		CompanionSpeech
	}

	internal static ModKeybind TalkKeybind { get; private set; } = null!;
	internal static ModKeybind EmoteKeybind { get; private set; } = null!;

	public override void Load()
	{
		if (!Terraria.Main.dedServ)
			TalkKeybind = KeybindLoader.RegisterKeybind(this, "TalkToCompanion", "V");
		if (!Terraria.Main.dedServ)
			EmoteKeybind = KeybindLoader.RegisterKeybind(this, "CompanionEmotes", "G");
	}

	public override void Unload()
	{
		TalkKeybind = null!;
		EmoteKeybind = null!;
	}

	internal static void SendTalkRequest(TalkCategory category, int option, int memoryCursor)
	{
		if (Main.netMode != NetmodeID.MultiplayerClient)
			return;
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.TalkRequest);
		packet.Write((byte)category);
		packet.Write((byte)option);
		packet.Write(memoryCursor);
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

	internal static void SendSummonRequest(Guid profileId)
	{
		if (Main.netMode != NetmodeID.MultiplayerClient)
			return;
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.SummonRequest);
		packet.Write(profileId.ToString());
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

	internal static void SendPackWithdrawRequest(int slot, bool singleItem)
	{
		if (Main.netMode != NetmodeID.MultiplayerClient)
			return;
		ModPacket packet = ModContent.GetInstance<Soulmates>().GetPacket();
		packet.Write((byte)MessageType.PackWithdrawRequest);
		packet.Write((byte)slot);
		packet.Write(singleItem);
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
		MessageType type = (MessageType)reader.ReadByte();
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
			case MessageType.SummonRequest:
				HandleSummonRequest(reader, whoAmI);
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
		}
	}

	private void HandleTalkRequest(BinaryReader reader, int whoAmI)
	{
		TalkCategory category = (TalkCategory)reader.ReadByte();
		int option = reader.ReadByte();
		int memoryCursor = reader.ReadInt32();
		if (Main.netMode != NetmodeID.Server || !Enum.IsDefined(category) || option is < 0 or > 2
			|| whoAmI < 0 || whoAmI >= Main.maxPlayers || !Main.player[whoAmI].active)
			return;

		Player player = Main.player[whoAmI];
		if (SoulboundCompanion.FindFor(player) is not { } companion)
			return;
		CompanionConversationResult result = companion.Converse(category, option, memoryCursor);
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
		if (Main.netMode != NetmodeID.Server || whoAmI < 0 || whoAmI >= Main.maxPlayers)
			return;
		SoulboundCompanion.RecallAllFor(Main.player[whoAmI]);
	}

	private static void HandleSummonRequest(BinaryReader reader, int whoAmI)
	{
		Guid profileId = Guid.TryParse(reader.ReadString(), out Guid parsed) ? parsed : Guid.Empty;
		if (Main.netMode != NetmodeID.Server || profileId == Guid.Empty
			|| whoAmI < 0 || whoAmI >= Main.maxPlayers || !Main.player[whoAmI].active)
			return;
		Player player = Main.player[whoAmI];
		foreach (Item item in player.inventory) {
			if (item.ModItem is SoulboundSigil sigil && sigil.Profile.Id == profileId) {
				sigil.SummonCompanion(player);
				break;
			}
		}
	}

	private static void HandleTrinketRequest(BinaryReader reader, int whoAmI)
	{
		CompanionTrinket trinket = (CompanionTrinket)reader.ReadByte();
		if (Main.netMode != NetmodeID.Server || !Enum.IsDefined(trinket)
			|| whoAmI < 0 || whoAmI >= Main.maxPlayers || !Main.player[whoAmI].active)
			return;
		Player player = Main.player[whoAmI];
		if (SoulboundCompanion.FindFor(player) is not { } companion)
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
		if (Main.netMode != NetmodeID.Server || slot >= CompanionProfile.MaximumPackSlots
			|| whoAmI < 0 || whoAmI >= Main.maxPlayers || !Main.player[whoAmI].active)
			return;
		Player player = Main.player[whoAmI];
		if (SoulboundCompanion.FindFor(player) is not { } companion)
			return;
		string reply = companion.WithdrawPackSlot(slot, singleItem);
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
		if (SoulboundCompanion.FindFor(Main.player[whoAmI]) is { } companion)
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
			companion.ShowSpeech(string.IsNullOrEmpty(argument) ? SoulmatesText.Get(key) : SoulmatesText.Get(key, argument));
	}

	private static void ApplyClientProfile(CompanionProfile profile)
	{
		Player player = Main.LocalPlayer;
		if (SoulboundCompanion.FindFor(player) is { } companion)
			companion.Profile = profile.Clone();
		foreach (Item item in player.inventory) {
			if (item.ModItem is SoulboundSigil sigil && sigil.Profile.Id == profile.Id) {
				sigil.Profile = profile.Clone();
				break;
			}
		}
	}
}
