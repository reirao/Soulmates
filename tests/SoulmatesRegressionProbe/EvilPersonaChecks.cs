#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.Net;

namespace SoulmatesRegressionProbe;

public sealed partial class EngineChecks
{
	private static void CheckEvilPersona(Action<bool, string> check)
	{
		check(!CompanionProfile.Load(new TagCompound()).EvilMode, "Old Sigils opted into EVIL without permission");
		foreach (bool enabled in new[] { false, true }) {
			var profile = new CompanionProfile { EvilMode = enabled };
			check(profile.Clone().EvilMode == enabled && CompanionProfile.Load(profile.Save()).EvilMode == enabled,
				"EVIL persona lost through clone or save");
			using var stream = new MemoryStream();
			using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) profile.Write(writer);
			stream.Position = 0;
			using var reader = new BinaryReader(stream, Encoding.UTF8, true);
			check(CompanionProfile.Read(reader).EvilMode == enabled && stream.Position == stream.Length,
				"Persona network profile is misaligned");
		}
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
		Player oldPlayer = Main.player[0]; NPC[] oldNpcs = Main.npc;
		int oldMode = Main.netMode, oldLocal = Main.myPlayer;
		RemoteClient[] oldClients = (RemoteClient[])Netplay.Clients.Clone();
		try {
			for (int i = 0; i < Netplay.Clients.Length; i++) Netplay.Clients[i] = new RemoteClient { Socket = new ProbeSocket() };
			foreach (int mode in new[] { NetmodeID.SinglePlayer, NetmodeID.Server }) {
				Main.netMode = mode; Main.myPlayer = 255;
				Main.npc = Enumerable.Range(0, oldNpcs.Length).Select(i => new NPC { whoAmI = i }).ToArray();
				var owner = new Player { whoAmI = 0, active = true, Center = new Vector2(500, 500) };
				Main.player[0] = owner;
				NPC npc = Main.npc[20]; npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
				npc.active = true; npc.ai[0] = 0; npc.Center = owner.Center;
				var mate = (SoulboundCompanion)npc.ModNPC;
				owner.inventory[0] = new Item(ModContent.ItemType<SoulboundSigil>());
				var sigil = (SoulboundSigil)owner.inventory[0].ModItem; sigil.Profile = mate.Profile.Clone();
				object? Get(string field) => typeof(SoulboundCompanion).GetField(field, flags)!.GetValue(mate);
				void Set(string field, object? value) => typeof(SoulboundCompanion).GetField(field, flags)!.SetValue(mate, value);
				object? Call(string method, params object[] args) => typeof(SoulboundCompanion).GetMethod(method, flags)!.Invoke(mate, args);
				mate.Profile.Store(new Item(ItemID.Wood, 13));
				mate.Profile.Store(new Item(ItemID.GoldCoin, 7));
				mate.Profile.WorkPaused = true;
				mate.Profile.AutonomyEnabled = false;
				mate.Profile.Routine = CompanionJob.Mine;
				Set("activeJob", CompanionJob.Mine);
				Set("jobCount", 5);
				foreach (var ability in CompanionAbilityRegistry.All) mate.Profile.SetInitiativePolicy(ability.Kind, CompanionInitiativePolicy.Never);
				byte[] CargoToken() => (byte[])typeof(CompanionProfile).GetMethod("StorageToken", flags)!.Invoke(mate.Profile, new object[] { CompanionStorage.Resources, 0 })!;
				byte[] cargoToken = CargoToken();
				var xp = mate.Profile.Experience; int mood = mate.Profile.Mood, energy = mate.Profile.Energy, bond = mate.Profile.Bond;
				var wallet = mate.Profile.WalletCopper;
				for (int i = 0; i < 8; i++) {
					check(mate.PerformQuickAction(CompanionQuickAction.ToggleEvil).Accepted, "Bound owner cannot toggle persona");
					check(mate.Profile.EvilMode == (i % 2 == 0) && sigil.Profile.EvilMode == mate.Profile.EvilMode, "Persona toggle did not sync to the active Sigil");
					check(mate.Profile.WorkPaused && !mate.Profile.AutonomyEnabled && (CompanionJob)Get("activeJob")! == CompanionJob.Mine
						&& mate.Profile.Routine == CompanionJob.Mine && (int)Get("jobCount")! == 5, "Persona interrupted or resumed work");
					check(CompanionAbilityRegistry.All.All(a => mate.Profile.GetInitiativePolicy(a.Kind) == CompanionInitiativePolicy.Never), "EVIL bypassed consent");
					check(CargoToken().SequenceEqual(cargoToken) && mate.Profile.WalletCopper == wallet
						&& mate.Profile.Experience == xp && mate.Profile.Mood == mood && mate.Profile.Energy == energy && mate.Profile.Bond == bond,
						"Persona toggle modified cargo, rewards or stamina");
				}
				mate.Profile.EvilMode = true;
				foreach (string original in new[] { "Social.Critters.Loss.Gentle", "Social.Critters.Harmed.Brave",
					"Autonomy.Initiative.Ask.Mining", "Conversation.Reflection.Question", "Conversation.Later", "Social.Autonomous.Context.LowMood" })
					check((string)Call("PersonaSpeechKey", original)! == original, "Persona rewrote a safety, consent or empathetic message: " + original);
				check((string)Call("PersonaSpeechKey", "Conversation.Reflection.Reply.Third")! == "Evil.Reflection.Third", "EVIL does not acknowledge negative feedback");
				Set("activeJob", CompanionJob.None); mate.Profile.Routine = CompanionJob.None;
				mate.Profile.WorkPaused = false; mate.Profile.AutonomyEnabled = true;
				Call("ClearNativeExpression");
				Call("QueueEvilCelebration", CompanionInitiativeKind.Mining);
				Set("speechTimer", 300); Call("TickEvilPersona");
				check(Get("evilCelebration") is not null && (int)Get("speechTimer")! == 300, "EVIL celebration overwrote readable speech");
				Set("speechTimer", 0); Set("guardianTarget", 19); Call("TickEvilPersona");
				check(Get("evilCelebration") is not null && (int)Get("speechTimer")! == 0, "EVIL celebration interrupted defense");
				Set("guardianTarget", -1); Set("pendingCritterLossKey", "Social.Critters.Loss.Gentle"); Call("TickEvilPersona");
				check(Get("evilCelebration") is not null, "EVIL displaced a queued critter loss reaction");
				Set("pendingCritterLossKey", ""); Call("TickEvilPersona");
				check(Get("evilCelebration") is null && (int)Get("speechTimer")! > 0 && (int)Get("evilCelebrationCooldown")! == 7200,
					"Successful work celebration was not consumed once with a bounded cooldown");
				Call("QueueEvilCelebration", CompanionInitiativeKind.Gathering);
				check(Get("evilCelebration") is null, "Celebrations ignored the anti-spam cooldown");
				Set("evilCelebrationCooldown", 0); Call("QueueEvilCelebration", CompanionInitiativeKind.Gathering);
				mate.PerformQuickAction(CompanionQuickAction.Abort);
				check(Get("evilCelebration") is null, "Abort retained a stale persona moment");
				Main.netMode = NetmodeID.MultiplayerClient;
				bool before = mate.Profile.EvilMode;
				check(!mate.PerformQuickAction(CompanionQuickAction.ToggleEvil).Accepted && mate.Profile.EvilMode == before, "Client changed its own authoritative persona");
				Main.netMode = mode;
				owner.inventory[0].TurnToAir();
				check(!mate.PerformQuickAction(CompanionQuickAction.ToggleEvil).Accepted && mate.Profile.EvilMode == before, "Unbound persona action mutated state");
			}
		}
		finally {
			Main.player[0] = oldPlayer; Main.npc = oldNpcs; Main.netMode = oldMode; Main.myPlayer = oldLocal;
			Array.Copy(oldClients, Netplay.Clients, oldClients.Length);
		}
	}
}
