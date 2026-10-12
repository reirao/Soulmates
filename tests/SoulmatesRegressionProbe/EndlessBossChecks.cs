#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BigInteger = System.Numerics.BigInteger;
using Microsoft.Xna.Framework;
using Soulmates.Common;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Soulmates.Content.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.Net;

namespace SoulmatesRegressionProbe;

public sealed partial class EngineChecks
{
	private static void CheckEndlessProgressionAndBoss(Action<bool, string> check)
	{
		var growth = new CompanionProfile();
		for (int level = 2; level <= 5000; level++) {
			BigInteger threshold = CompanionProfile.ExperienceForLevel(level);
			growth.Experience = threshold - 1;
			check(growth.Level == level - 1, "Premature open-ended level: " + level);
			check(growth.GainExperience(1, out var reached) && reached == level && growth.Level == level,
				"Open-ended threshold failed: " + level);
			check(growth.ExperienceIntoLevel == 0 && growth.ExperienceNeededForNextLevel == 12 * (2 * level - 1)
				&& growth.LevelProgress == 0f, "Open-ended meter failed: " + level);
		}
		foreach (BigInteger level in new[] { new BigInteger(21), new BigInteger(int.MaxValue) + 1, BigInteger.Pow(10, 100) }) {
			growth.Experience = CompanionProfile.ExperienceForLevel(level) + 7;
			check(growth.Level == level && growth.ExperienceIntoLevel == 7 && growth.ResourceCarryLimit == 1000,
				"Large levels overflowed or bypassed cargo balance");
			using var stream = new MemoryStream();
			using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) growth.Write(writer);
			stream.Position = 0; using var reader = new BinaryReader(stream);
			foreach (var copy in new[] { growth.Clone(), CompanionProfile.Load(growth.Save()), CompanionProfile.Read(reader) })
				check(copy.Experience == growth.Experience && copy.Level == level, "Large growth failed persistence");
			check(stream.Position == stream.Length && !growth.GainExperience(-1, out _) && !growth.GainExperience(0, out _),
				"Negative gain or profile alignment changed large growth");
		}
		growth.Experience = int.MaxValue;
		growth.GainExperience(int.MaxValue, out _);
		check(growth.Experience == (BigInteger)int.MaxValue * 2, "XP addition wrapped an int");
		check(CompanionProfile.Load(new TagCompound { ["experience"] = 4332 }).Level == 20,
			"Old level-20 Sigil did not migrate exactly");
		growth.Experience = -10; growth.Normalize(); check(growth.Experience == 0 && growth.Level == 1, "Negative XP survived normalization");

		const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
		Player oldPlayer = Main.player[0]; NPC[] oldNpcs = Main.npc; Projectile[] oldProjectiles = Main.projectile;
		int oldMode = Main.netMode, oldLocal = Main.myPlayer;
		RemoteClient[] oldClients = (RemoteClient[])Netplay.Clients.Clone();
		try {
			for (int i = 0; i < Netplay.Clients.Length; i++) Netplay.Clients[i] = new RemoteClient { Socket = new ProbeSocket() };
			foreach (int mode in new[] { NetmodeID.SinglePlayer, NetmodeID.Server }) {
				Main.netMode = mode; Main.myPlayer = mode == NetmodeID.Server ? 255 : 0;
				Main.npc = Enumerable.Range(0, oldNpcs.Length).Select(i => new NPC { whoAmI = i }).ToArray();
				Main.projectile = Enumerable.Range(0, oldProjectiles.Length).Select(i => new Projectile { whoAmI = i }).ToArray();
				var owner = new Player { whoAmI = 0, active = true, statLifeMax2 = 200, Center = new Vector2(500, 500) };
				Main.player[0] = owner;
				NPC npc = Main.npc[20]; npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
				npc.active = true; npc.ai[0] = 0; npc.Center = owner.Center;
				var mate = (SoulboundCompanion)npc.ModNPC;
				owner.inventory[0] = new Item(ModContent.ItemType<SoulboundSigil>());
				var sigil = (SoulboundSigil)owner.inventory[0].ModItem; sigil.Profile = mate.Profile.Clone();
				void Set(string field, object value) => typeof(SoulboundCompanion).GetField(field, flags)!.SetValue(mate, value);
				check(!mate.PerformQuickAction(CompanionQuickAction.BossChallenge).Accepted && !mate.IsBossChallenge,
					"Boss started without explicit EVIL opt-in");
				mate.Profile.EvilMode = true;
				mate.Profile.Store(new Item(ItemID.Wood, 13)); mate.Profile.Store(new Item(ItemID.GoldCoin, 7));
				var wallet = mate.Profile.WalletCopper; var xp = mate.Profile.Experience;
				int energy = mate.Profile.Energy, mood = mate.Profile.Mood, bond = mate.Profile.Bond;
				bool Start() => mate.PerformQuickAction(CompanionQuickAction.BossChallenge).Accepted && mate.IsBossChallenge;
				check(Start() && npc.boss && !npc.friendly && !npc.dontTakeDamage && npc.lifeMax >= 1500
					&& mate.Profile.WorkPaused && npc.width == 72, "Boss transformation is not actually hostile or paused");
				int cooldown = 0;
				check(!mate.CanHitPlayer(owner, ref cooldown), "Warmup contact damage is active");
				var stranger = new Player { whoAmI = 1, active = true };
				check(mate.CanBeHitByItem(owner, new Item(ItemID.CopperShortsword)) == true
					&& mate.CanBeHitByItem(stranger, new Item(ItemID.CopperShortsword)) == false
					&& mate.CanBeHitByProjectile(new Projectile { owner = 1, friendly = true }) == false,
					"Uninvited player can damage this private challenge");
				for (int phase = 1; phase <= 3; phase++) {
					npc.life = phase == 1 ? npc.lifeMax : phase == 2 ? npc.lifeMax / 2 : npc.lifeMax / 4;
					Set("bossChallengeTicks", 179); npc.UpdateNPC(20);
					Projectile[] shots = Main.projectile.Where(p => p.active && p.ModProjectile is CompanionBossSpark).ToArray();
					check(mate.BossChallengePhase == phase && shots.Length == 1 + phase * 2, "Boss phase did not fire its pattern: " + phase);
					foreach (Projectile shot in shots) {
						check(shot.hostile && !shot.friendly && shot.ModProjectile.CanHitPlayer(owner)
							&& !shot.ModProjectile.CanHitPlayer(stranger) && shot.ModProjectile.CanHitNPC(new NPC()) == false,
							$"Challenge projectile boundary: hostile={shot.hostile}, friendly={shot.friendly}, hitOwner={shot.ModProjectile.CanHitPlayer(owner)}, hitOther={shot.ModProjectile.CanHitPlayer(stranger)}, ai={string.Join(',', shot.ai)}, npcIndex={npc.whoAmI}, session={mate.BossChallengeSession}, active={npc.active}");
						shot.Kill();
					}
				}
				check(mate.CanHitPlayer(owner, ref cooldown) && !mate.CanHitPlayer(stranger, ref cooldown)
					&& !mate.CanHitNPC(new NPC()), "Contact damage is not limited to the challenger");
				using (var stream = new MemoryStream()) {
					using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) mate.SendExtraAI(writer);
					stream.Position = 0; using var reader = new BinaryReader(stream);
					var copyNpc = new NPC(); copyNpc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
					var copy = (SoulboundCompanion)copyNpc.ModNPC; copy.ReceiveExtraAI(reader);
					check(stream.Position == stream.Length && copy.IsBossChallenge && copyNpc.boss && !copyNpc.friendly
						&& copyNpc.lifeMax == npc.lifeMax, "Boss health/state failed extra-AI roundtrip");
				}
				npc.StrikeNPC(new NPC.HitInfo { Damage = 100000, HitDirection = 1, HideCombatText = true }, noPlayerInteraction: true);
				check(npc.active && !mate.IsBossChallenge && npc.friendly && !npc.boss && npc.dontTakeDamage
					&& npc.life == 250 && npc.width == 42, "Native lethal hit killed the persistent companion");
				check(mate.Profile.ItemCount(ItemID.Wood) == 13 && mate.Profile.WalletCopper == wallet
					&& mate.Profile.Experience == xp && mate.Profile.Energy == energy && mate.Profile.Mood == mood
					&& mate.Profile.Bond == bond && sigil.Profile.Id == mate.Profile.Id,
					"Challenge mutated conserved cargo, identity or rewards");
				check(Start(), "Cannot replay the completed challenge");
				Set("bossChallengeTicks", 179); mate.AI();
				mate.PerformQuickAction(CompanionQuickAction.Abort);
				check(!mate.IsBossChallenge && !Main.projectile.Any(p => p.active && p.ModProjectile is CompanionBossSpark),
					"Abort left hostile challenge effects alive: " + string.Join(';', Main.projectile.Where(p => p.active && p.ModProjectile is CompanionBossSpark).Select(p => string.Join(',', p.ai))));
				check(Start(), "Cannot start timeout check"); Set("bossChallengeTicks", 17999); mate.AI();
				check(!mate.IsBossChallenge, "Challenge ignored its time limit");
				check(Start(), "Cannot start recall check"); mate.Recall();
				check(!npc.active && !mate.IsBossChallenge && !npc.boss, "Recall persisted boss state");
				npc.active = true;
				Main.netMode = NetmodeID.MultiplayerClient;
				check(!mate.PerformQuickAction(CompanionQuickAction.BossChallenge).Accepted && !mate.IsBossChallenge,
					"Client started an authoritative challenge");
				Main.netMode = mode;
				owner.inventory[0].TurnToAir();
				check(!mate.PerformQuickAction(CompanionQuickAction.BossChallenge).Accepted, "Missing Sigil started a boss");
			}
		}
		finally {
			Main.player[0] = oldPlayer; Main.npc = oldNpcs; Main.projectile = oldProjectiles;
			Main.netMode = oldMode; Main.myPlayer = oldLocal; Array.Copy(oldClients, Netplay.Clients, oldClients.Length);
		}
	}
}
