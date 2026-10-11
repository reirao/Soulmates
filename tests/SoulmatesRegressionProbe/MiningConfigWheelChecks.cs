#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Common.UI;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace SoulmatesRegressionProbe;

public sealed partial class EngineChecks
{
	private static void CheckMiningConfigWheel(Action<bool, string> check)
	{
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
		Type type = typeof(CompanionWheelSystem);
		var wheel = new CompanionWheelSystem();
		var mod = ModContent.GetInstance<global::Soulmates.Soulmates>();
		Player[] oldPlayers = Main.player;
		NPC[] oldNpcs = Main.npc;
		Player oldClientPlayer = Main.clientPlayer;
		Item oldMouseItem = Main.mouseItem;
		int oldLocal = Main.myPlayer, oldMode = Main.netMode;
		bool oldWriting = PlayerInput.WritingText, oldRecording = ProbeSocket.Recording;
		var oldSocket = Netplay.Connection.Socket;
		byte[][] oldPackets = ProbeSocket.Sent.ToArray();
		string[] flagNames = { "dedServ", "gameMenu", "playerInventory", "drawingPlayerChat", "editSign", "editChest" };
		var oldFlags = flagNames.ToDictionary(name => name, name => typeof(Main).GetField(name, flags)!.GetValue(null));
		object? Get(string name) => type.GetField(name, flags)!.GetValue(wheel);
		void Set(string name, object value) => type.GetField(name, flags)!.SetValue(wheel, value);
		object? Call(string name, params object[] args) => type.GetMethod(name, flags)!.Invoke(wheel, args);
		bool Pending() => Get("miningConfigSigil") is not null;
		(CompanionMiningApproach Approach, CompanionMiningDirection Direction, CompanionTunnelEnd End) Draft() {
			object config = type.GetProperty("CurrentMiningConfig", flags)!.GetValue(wheel)!;
			Type configType = config.GetType();
			return ((CompanionMiningApproach)configType.GetProperty("Approach")!.GetValue(config)!,
				(CompanionMiningDirection)configType.GetProperty("Direction")!.GetValue(config)!,
				(CompanionTunnelEnd)configType.GetProperty("End")!.GetValue(config)!);
		}
		try {
			foreach (string name in flagNames) typeof(Main).GetField(name, flags)!.SetValue(null, false);
			Main.myPlayer = 0; Main.netMode = NetmodeID.MultiplayerClient;
			Main.player = (Player[])oldPlayers.Clone();
			var owner = new Player { whoAmI = 0, active = true, Center = new Vector2(400, 400) };
			Main.player[0] = owner; Main.clientPlayer = new Player { whoAmI = 0 };
			owner.SetTalkNPC(-1); Main.mouseItem = new Item(); PlayerInput.WritingText = false;
			Main.npc = Enumerable.Range(0, oldNpcs.Length).Select(i => new NPC { whoAmI = i }).ToArray();
			NPC npc = Main.npc[0]; npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
			npc.active = true; npc.ai[0] = 0; npc.Center = owner.Center;
			var mate = (SoulboundCompanion)npc.ModNPC;
			Item sigilItem = new(ModContent.ItemType<SoulboundSigil>());
			var sigil = (SoulboundSigil)sigilItem.ModItem;
			Guid id = mate.Profile.Id;
			Netplay.Connection.Socket = new ProbeSocket(); ProbeSocket.Recording = true;
			using var header = mod.GetPacket();
			int payloadOffset = (int)header.BaseStream.Position;
			byte requestType = Convert.ToByte(Enum.Parse(mod.GetType().GetNestedType("MessageType", flags)!, "MiningConfigRequest"));
			List<(Guid Id, CompanionMiningApproach Approach, CompanionMiningDirection Direction, CompanionTunnelEnd End)> Requests() {
				var result = new List<(Guid, CompanionMiningApproach, CompanionMiningDirection, CompanionTunnelEnd)>();
				foreach (byte[] packet in ProbeSocket.Sent) {
					if (packet.Length <= payloadOffset || packet[2] != MessageID.ModPacket || packet[payloadOffset] != requestType) continue;
					using var stream = new MemoryStream(packet); stream.Position = payloadOffset + 1;
					using var reader = new BinaryReader(stream);
					result.Add((new Guid(reader.ReadBytes(16)), (CompanionMiningApproach)reader.ReadByte(),
						(CompanionMiningDirection)reader.ReadByte(), (CompanionTunnelEnd)reader.ReadByte()));
					check(stream.Position == stream.Length, "Mining wheel changed the existing full-config packet shape");
				}
				return result;
			}
			void Bind() {
				wheel.Close(); wheel.PostUpdateInput(); Main.netMode = NetmodeID.MultiplayerClient;
				mate.Profile = mate.Profile.Clone(); mate.Profile.Id = id;
				mate.Profile.MiningApproach = CompanionMiningApproach.Tunnel;
				mate.Profile.MiningDirection = CompanionMiningDirection.Auto; mate.Profile.TunnelEnd = CompanionTunnelEnd.Short;
				sigil.Profile = mate.Profile.Clone(); owner.inventory[0] = sigilItem;
				Set("companion", mate); Set("companionId", id); Set("open", true); Set("openTicks", 10);
				Set("branch", Enum.Parse(type.GetNestedType("RootBranch", flags)!, "Work"));
				Set("workPage", Enum.Parse(type.GetNestedType("WorkPage", flags)!, "Compass"));
				ProbeSocket.Sent.Clear();
			}
			void Ack(CompanionMiningApproach approach, CompanionMiningDirection direction, CompanionTunnelEnd end, Guid? profileId = null, bool poll = true) {
				CompanionProfile profile = mate.Profile.Clone(); profile.Id = profileId ?? id;
				profile.MiningApproach = approach; profile.MiningDirection = direction; profile.TunnelEnd = end;
				mod.GetType().GetMethod("ApplyClientProfile", flags)!.Invoke(null, new object[] { profile });
				if (poll) wheel.PostUpdateInput();
			}
			void RapidCompass() { Call("ActivateCompass", 5); Call("ActivateCompass", 1); }

			Bind(); RapidCompass();
			var requests = Requests();
			check(requests.Count == 2 && requests[0] == (id, CompanionMiningApproach.Tunnel, CompanionMiningDirection.Auto, CompanionTunnelEnd.Passage)
				&& requests[1] == (id, CompanionMiningApproach.Tunnel, CompanionMiningDirection.Right, CompanionTunnelEnd.Passage),
				"Rapid compass edits failed to send both merged settings before acknowledgement");
			check(Draft() == (CompanionMiningApproach.Tunnel, CompanionMiningDirection.Right, CompanionTunnelEnd.Passage),
				"Rapid end then direction without server acknowledgement lost one of the edits");
			check(mate.Profile.MiningDirection == CompanionMiningDirection.Auto && mate.Profile.TunnelEnd == CompanionTunnelEnd.Short
				&& sigil.Profile.MiningDirection == CompanionMiningDirection.Auto && sigil.Profile.TunnelEnd == CompanionTunnelEnd.Short,
				"Pending mining config optimistically mutated an authoritative companion or Sigil profile");
			check((string)Call("CompassLabel", 5)! == SoulmatesText.Get("UI.CompanionWheel.WorkMenu.End",
				SoulmatesText.Get("UI.CompanionWheel.WorkMenu.Ends.Passage")), "Compass label ignores the pending tunnel end");
			Ack(CompanionMiningApproach.Tunnel, CompanionMiningDirection.Auto, CompanionTunnelEnd.Passage, Guid.NewGuid());
			check(Pending() && Requests().Count == 2, "Another Sigil's profile acknowledged the pending mining request");
			Ack(CompanionMiningApproach.Tunnel, CompanionMiningDirection.Auto, CompanionTunnelEnd.Passage);
			requests = Requests();
			check(requests.Count == 2 && requests[1] == (id, CompanionMiningApproach.Tunnel, CompanionMiningDirection.Right, CompanionTunnelEnd.Passage)
				&& Pending(), "Intermediate acknowledgement discarded or resent the merged compass draft");
			check(mate.Profile.MiningDirection == CompanionMiningDirection.Auto && Draft().Direction == CompanionMiningDirection.Right,
				"Intermediate acknowledgement erased pending direction or changed authority optimistically");
			Ack(CompanionMiningApproach.Tunnel, CompanionMiningDirection.Auto, CompanionTunnelEnd.Passage);
			check(Pending() && Requests().Count == 2, "Repeated intermediate profile retried or discarded the final request");
			Ack(CompanionMiningApproach.Tunnel, CompanionMiningDirection.Right, CompanionTunnelEnd.Passage);
			check(!Pending() && Draft().Direction == CompanionMiningDirection.Right && sigil.Profile.TunnelEnd == CompanionTunnelEnd.Passage,
				"Final mining acknowledgement failed to retire the draft or sync the Sigil");

			Bind(); RapidCompass();
			Call("SetMiningConfig", CompanionMiningApproach.Vein, CompanionMiningDirection.Auto, CompanionTunnelEnd.Short);
			check(Draft() == (CompanionMiningApproach.Vein, CompanionMiningDirection.Right, CompanionTunnelEnd.Passage),
				"Approach picker overwrote pending compass settings with its stale profile arguments");
			Ack(CompanionMiningApproach.Tunnel, CompanionMiningDirection.Auto, CompanionTunnelEnd.Passage);
			requests = Requests();
			check(requests.Count == 3 && requests[2] == (id, CompanionMiningApproach.Vein, CompanionMiningDirection.Right, CompanionTunnelEnd.Passage),
				"Merged approach/direction/end did not use the existing mining-config request");

			Bind(); Call("ActivateCompass", 5); Call("ActivateCompass", 5);
			check(Pending() && Draft().End == CompanionTunnelEnd.Short, "Returning the draft to baseline retired requests before acknowledgement");
			Ack(CompanionMiningApproach.Tunnel, CompanionMiningDirection.Auto, CompanionTunnelEnd.Passage);
			requests = Requests();
			check(requests.Count == 2 && requests[1].End == CompanionTunnelEnd.Short && Pending(),
				"Second end toggle was lost behind the first acknowledgement");
			Ack(CompanionMiningApproach.Tunnel, CompanionMiningDirection.Auto, CompanionTunnelEnd.Short);
			check(!Pending(), "Final acknowledgement of a return-to-baseline edit retained the draft");
			Bind(); RapidCompass();
			Ack(CompanionMiningApproach.Tunnel, CompanionMiningDirection.Auto, CompanionTunnelEnd.Passage, poll: false);
			Ack(CompanionMiningApproach.Tunnel, CompanionMiningDirection.Right, CompanionTunnelEnd.Passage);
			check(!Pending() && Requests().Count == 2 && Draft().Direction == CompanionMiningDirection.Right,
				"Multiple acknowledgements in one frame retained or resent an already accepted draft");

			Bind(); RapidCompass();
			Ack(CompanionMiningApproach.Vein, CompanionMiningDirection.Left, CompanionTunnelEnd.Short);
			check(!Pending() && Requests().Count == 2 && Draft().Approach == CompanionMiningApproach.Vein,
				"Conflicting authoritative response replayed the rejected/stale mining draft");
			Bind(); RapidCompass();
			Ack(CompanionMiningApproach.Tunnel, CompanionMiningDirection.Auto, CompanionTunnelEnd.Short);
			int timeout = (int)type.GetField("MiningConfigTimeoutTicks", flags)!.GetRawConstantValue()!;
			for (int i = 0; i < timeout; i++) wheel.PostUpdateInput();
			check(!Pending() && Requests().Count == 2 && Draft().End == CompanionTunnelEnd.Short,
				"Silently rejected mining request never expired or automatically retried an edit");
			Ack(CompanionMiningApproach.Tunnel, CompanionMiningDirection.Auto, CompanionTunnelEnd.Passage);
			check(!Pending() && Requests().Count == 2, "Late acknowledgement resurrected an expired draft");

			Bind(); RapidCompass(); wheel.Close(); wheel.PostUpdateInput();
			requests = Requests();
			check(!Pending() && requests.Count == 2
				&& requests[1] == (id, CompanionMiningApproach.Tunnel, CompanionMiningDirection.Right, CompanionTunnelEnd.Passage),
				"Close before first acknowledgement discarded the last deliberate compass click");
			Ack(CompanionMiningApproach.Tunnel, CompanionMiningDirection.Auto, CompanionTunnelEnd.Passage);
			Ack(CompanionMiningApproach.Tunnel, CompanionMiningDirection.Right, CompanionTunnelEnd.Passage);
			check(!wheel.IsOpen && !Pending() && Requests().Count == 2 && mate.Profile.MiningDirection == CompanionMiningDirection.Right
				&& mate.Profile.TunnelEnd == CompanionTunnelEnd.Passage && sigil.Profile.MiningDirection == CompanionMiningDirection.Right,
				"Delayed acknowledgements after closing lost selected settings, reopened the wheel or sent another request");
			Bind(); RapidCompass(); wheel.Close();
			Set("companion", mate); Set("companionId", id); Set("open", true); Set("openTicks", 0);
			Set("branch", Enum.Parse(type.GetNestedType("RootBranch", flags)!, "Work"));
			wheel.PostUpdateInput();
			check(!Pending() && Requests().Count == 2, "Reopening before the next input frame reused a closed wheel's draft");
			Bind(); RapidCompass();
			owner.inventory[0] = sigilItem.Clone(); wheel.PostUpdateInput();
			check(!Pending() && Requests().Count == 2, "Replacement Sigil with the same profile ID inherited the old UI draft");
			Bind(); RapidCompass(); sigil.Profile.Id = Guid.NewGuid(); wheel.PostUpdateInput();
			check(!Pending() && Requests().Count == 2, "Changed captured Sigil identity retained the pending mining draft");
			Bind(); RapidCompass(); mate.Profile.Id = Guid.NewGuid(); wheel.PostUpdateInput();
			check(!Pending() && Requests().Count == 2, "Changed companion identity retained the pending mining draft");
			Bind(); RapidCompass(); Main.playerInventory = true; wheel.PostUpdateInput(); Main.playerInventory = false;
			check(!Pending() && Requests().Count == 2, "Protected inventory input retained pending compass edits");

			Bind();
			int maximum = (int)type.GetField("MaximumPendingMiningConfigs", flags)!.GetRawConstantValue()!;
			for (int i = 0; i < maximum + 10; i++) Call("ActivateCompass", 5);
			requests = Requests();
			check(requests.Count == maximum && Draft().End == requests[^1].End,
				"Unacknowledged mining edits grew beyond the bound or displayed an unsent setting");

			Bind(); Main.netMode = NetmodeID.SinglePlayer;
			Call("SetMiningConfig", CompanionMiningApproach.Vein, CompanionMiningDirection.Left, CompanionTunnelEnd.Short);
			check(!Pending() && Requests().Count == 0 && mate.Profile.MiningApproach == CompanionMiningApproach.Vein
				&& mate.Profile.MiningDirection == CompanionMiningDirection.Left && sigil.Profile.MiningDirection == CompanionMiningDirection.Left,
				"UI-local multiplayer draft changed single-player mining configuration");
		}
		finally {
			wheel.Close(); wheel.PostUpdateInput();
			Netplay.Connection.Socket = oldSocket; ProbeSocket.Recording = oldRecording;
			ProbeSocket.Sent.Clear(); ProbeSocket.Sent.AddRange(oldPackets);
			Main.player = oldPlayers; Main.npc = oldNpcs; Main.clientPlayer = oldClientPlayer;
			Main.myPlayer = oldLocal; Main.netMode = oldMode; Main.mouseItem = oldMouseItem; PlayerInput.WritingText = oldWriting;
			foreach (var entry in oldFlags) typeof(Main).GetField(entry.Key, flags)!.SetValue(null, entry.Value);
		}
	}
}
