#nullable enable
using System;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace SoulmatesAuditProbe;

public sealed partial class AuditChecks
{
	private void CheckCarefulSalvagePackets(Mod mod)
	{
		Player[] oldPlayers = Main.player; NPC[] oldNpcs = Main.npc; Item[] oldItems = Main.item;
		Tilemap oldMap = Main.tile; WorldFileData oldWorld = Main.ActiveWorldFileData;
		int oldX = Main.maxTilesX, oldY = Main.maxTilesY;
		var protection = ModContent.GetInstance<SalvageProtectionSystem>();
		var saved = new TagCompound(); protection.SaveWorldData(saved);
		try {
			Main.netMode = NetmodeID.Server; Main.myPlayer = 255; Main.dedServ = true;
			Main.maxTilesX = Main.maxTilesY = 120;
			Main.tile = (Tilemap)Activator.CreateInstance(typeof(Tilemap), Private | BindingFlags.Public,
				null, new object[] { (ushort)120, (ushort)120 }, null)!;
			Main.ActiveWorldFileData = new WorldFileData("", false) { UniqueId = Guid.NewGuid() };
			Main.player = Enumerable.Range(0, oldPlayers.Length).Select(i => new Player { whoAmI = i }).ToArray();
			Main.npc = Enumerable.Range(0, oldNpcs.Length).Select(i => new NPC { whoAmI = i }).ToArray();
			Main.item = Enumerable.Range(0, oldItems.Length).Select(i => new Item { whoAmI = i }).ToArray();
			var owner = Main.player[0]; owner.active = true; owner.Center = new Point(50, 50).ToWorldCoordinates();
			owner.SpawnX = owner.SpawnY = -1;
			var other = Main.player[1]; other.active = true; other.Center = owner.Center; other.SpawnX = other.SpawnY = -1;
			SoulboundCompanion mate = CreateCompanion(owner, 180, 0, "Salvage owner"), foreign = CreateCompanion(other, 181, 0, "Other owner");
			owner.GetModPlayer<SoulmatesPlayer>().ActiveCompanionWhoAmI = 180;
			other.GetModPlayer<SoulmatesPlayer>().ActiveCompanionWhoAmI = 181;
			protection.ClearWorld();
			Point point = new(50, 50); Tile web = Main.tile[point]; web.HasTile = true; web.TileType = TileID.Cobweb;
			Type salvage = typeof(CompanionProfile).Assembly.GetType("Soulmates.Common.CompanionSalvage")!;
			object target = salvage.GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [point])!;
			ulong signature = (ulong)target.GetType().GetProperty("Fingerprint")!.GetValue(target)!;
			byte[] Request(Guid profile, ulong? hash = null) => Packet(mod, "SalvageRequest", w => {
				w.Write(profile.ToByteArray()); w.Write(Main.ActiveWorldFileData.UniqueId.ToByteArray());
				w.Write((short)point.X); w.Write((short)point.Y); w.Write(hash ?? signature);
			});
			byte[] Answer(Guid profile, Guid token, CompanionAnswer answer) => Packet(mod, "ChoiceResponseRequest", w => {
				w.Write(profile.ToByteArray()); w.Write(token.ToByteArray()); w.Write((byte)answer);
			});
			Deliver(mod, Request(foreign.Profile.Id)); Deliver(mod, Request(Guid.NewGuid())); Deliver(mod, Request(mate.Profile.Id, 123));
			Expect(!mate.HasPendingQuestion && !foreign.HasPendingQuestion && web.HasTile,
				"Salvage packets enforce owner, Sigil and captured footprint identity", "Actual server handler rejects foreign and stale requests");
			byte[] valid = Request(mate.Profile.Id);
			for (int length = 1; length < valid.Length; length++) Deliver(mod, valid[..length]);
			Expect(!mate.HasPendingQuestion && web.HasTile, "Truncated salvage packets fail closed", "Every truncated length through the real handler");
			Deliver(mod, valid); Guid token = mate.QuestionId;
			Expect(mate.PendingQuestion == CompanionQuestion.Salvage && mate.CurrentJob == CompanionJob.None && web.HasTile,
				"Valid salvage packet only presents a question", "No client-created demolition permit");
			Deliver(mod, Answer(foreign.Profile.Id, token, CompanionAnswer.First));
			Deliver(mod, Answer(mate.Profile.Id, Guid.NewGuid(), CompanionAnswer.First));
			Expect(mate.HasPendingQuestion && mate.CurrentJob == CompanionJob.None, "Foreign and replayed answers cannot approve salvage", "Exact question token and owner required");
			Deliver(mod, Answer(mate.Profile.Id, token, CompanionAnswer.First));
			Expect(mate.CurrentJob == CompanionJob.Salvage && !mate.HasPendingQuestion, "Actual owner response dispatches a single salvage job", "Same question-response protocol as existing conversations");
			mate.PerformQuickAction(CompanionQuickAction.Abort); Deliver(mod, Answer(mate.Profile.Id, token, CompanionAnswer.First));
			Expect(mate.CurrentJob == CompanionJob.None && web.HasTile, "An aborted permit cannot be replayed", "No tile or cargo changes");
			NPC animal = Main.npc[19]; animal.SetDefaults(NPCID.Scorpion); animal.active = true; animal.Center = owner.Center;
			byte[] Meet(Guid profile) => Packet(mod, "NpcContextRequest", w => {
				w.Write(profile.ToByteArray()); w.Write((byte)CompanionNpcAction.Meet); w.Write((short)19); w.Write((int)NPCID.Scorpion);
			});
			Deliver(mod, Meet(foreign.Profile.Id));
			Expect(!mate.HasPendingQuestion && animal.active, "Foreign Meet request cannot command another companion", "Actual NPC context packet");
			Deliver(mod, Meet(mate.Profile.Id)); token = mate.QuestionId;
			Expect(mate.PendingQuestion == CompanionQuestion.CritterEncounter && animal.active, "Meet packet opens an individual critter question", "Scorpion remains a native living NPC");
			animal.SetDefaults(NPCID.Scorpion); animal.active = true;
			Deliver(mod, Answer(mate.Profile.Id, token, CompanionAnswer.Second));
			Expect(!mate.HasPendingQuestion && Field(mate, "critterTarget") is null && animal.active,
				"Same-object same-species respawn cannot inherit encounter permission", "SetDefaults invalidates the original live identity");
			byte[] Speech(Guid profile, string key, string argument, string? second = null) => Packet(mod, "CompanionSpeech", w => {
				w.Write(profile.ToString()); w.Write(key); w.Write(argument); if (second is not null) w.Write(second);
			});
			const string key = "Salvage.Question";
			mate.ShowSpeech("unchanged");
			byte[] speech = Speech(mate.Profile.Id, key, "2", "cobweb");
			Deliver(mod, speech);
			Expect((string)Field(mate, "speechText")! == "unchanged", "A client cannot spoof server-only companion speech", "Packet direction remains enforced");
			Main.netMode = NetmodeID.MultiplayerClient; Main.myPlayer = 0; Main.dedServ = false;
			Deliver(mod, Speech(foreign.Profile.Id, key, "2", "cobweb"));
			Expect((string)Field(mate, "speechText")! == "unchanged", "Foreign profile speech does not affect the owner's companion", "Profile identity survives the localized protocol");
			Deliver(mod, speech);
			Expect((string)Field(mate, "speechText")! == SoulmatesText.Get(key, "2", "cobweb"),
				"Two-argument salvage speech formats on the receiving client", "Selected object name and captured count survive packet decoding");
			Deliver(mod, Speech(mate.Profile.Id, "Social.Critters.CatchReceipt", "Scorpion"));
			object receipts = Field(mate, "pendingCatchNotices")!;
			Expect((int)receipts.GetType().GetProperty("Count")!.GetValue(receipts)! == 1,
				"Busy client queues an arriving catch receipt", "Server/client speech clocks cannot discard the result");
			Set(mate, "speechTimer", 0);
			Call(mate, "UpdateCatchNotice");
			Expect((string)Field(mate, "speechText")! == SoulmatesText.Get("Social.Critters.CatchReceipt", "Scorpion"),
				"Existing single-argument speech remains compatible", "Native catch receipts keep their item name");
		} finally {
			Main.player = oldPlayers; Main.npc = oldNpcs; Main.item = oldItems; Main.tile = oldMap;
			Main.maxTilesX = oldX; Main.maxTilesY = oldY; Main.ActiveWorldFileData = oldWorld;
			protection.ClearWorld(); protection.LoadWorldData(saved);
		}
	}
}
