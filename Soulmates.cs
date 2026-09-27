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
		ProfileUpdate
	}

	internal static ModKeybind TalkKeybind { get; private set; } = null!;

	public override void Load()
	{
		if (!Terraria.Main.dedServ)
			TalkKeybind = KeybindLoader.RegisterKeybind(this, "TalkToCompanion", "V");
	}

	public override void Unload()
	{
		TalkKeybind = null!;
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
			case MessageType.ProfileUpdate:
				HandleProfileUpdate(reader);
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
		if (SoulboundCompanion.FindFor(Main.player[whoAmI]) is { } companion)
			companion.Recall();
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
