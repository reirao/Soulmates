#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using ReLogic.Content;
using ReLogic.Graphics;
using Soulmates.Common;
using Soulmates.Common.UI;
using Soulmates.Content.Items;
using Soulmates.Content.NPCs;
using Terraria;
using Terraria.ID;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.Net;
using Terraria.Net.Sockets;
using Terraria.UI;

namespace SoulmatesAuditProbe;

public sealed class SoulmatesAuditProbe : Mod { }

public sealed class ReducedStackResource : ModItem
{
    public int Marker;
    public override string Texture => "Terraria/Images/Item_23";
    public override void SetDefaults()
    {
        Item.maxStack = 100;
        Item.material = true;
    }
    public override void SaveData(TagCompound tag) => tag["marker"] = Marker;
    public override void LoadData(TagCompound tag) => Marker = tag.GetInt("marker");
    public override void NetSend(BinaryWriter writer) => writer.Write(Marker);
    public override void NetReceive(BinaryReader reader) => Marker = reader.ReadInt32();
}

// This probe never opens a listener or touches a normal character/world.
public sealed class DisconnectedSocket : ISocket
{
    public void AsyncSend(byte[] data, int offset, int size, SocketSendCallback callback, object? state = null) { }
    public void AsyncReceive(byte[] data, int offset, int size, SocketReceiveCallback callback, object? state = null) { }
    public void Close() { }
    public void Connect(RemoteAddress address) { }
    public RemoteAddress GetRemoteAddress() => null!;
    public bool IsConnected() => false;
    public bool IsDataAvailable() => false;
    public void SendQueuedPackets() { }
    public bool StartListening(SocketConnectionAccepted callback) => false;
    public void StopListening() { }
}

public sealed partial class AuditChecks : ModSystem
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<string> results = [];
    private int failedExpectations;

    public override bool HijackSendData(int whoAmI, int msgType, int remoteClient, int ignoreClient,
        NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7) => true;

    public override void PostAddRecipes()
    {
        if (!Main.dedServ) return;
        try {
            Mod mod = ModLoader.GetMod("Soulmates");
            Version expectedVersion = new(Environment.GetEnvironmentVariable("SOULMATES_EXPECTED_TEST_VERSION") ?? "0.22.2");
            if (mod.Version != expectedVersion) throw new InvalidOperationException("Wrong audit package: expected " + expectedVersion + ", loaded " + mod.Version);
            Main.netMode = NetmodeID.Server;
            Main.myPlayer = 255;
            Main.maxTilesX = Main.maxTilesY = 100;
            Main.tile = (Tilemap)Activator.CreateInstance(typeof(Tilemap), Private | BindingFlags.Public,
                null, new object[] { (ushort)100, (ushort)100 }, null)!;
            Lighting.Initialize();
            Main.npc = new NPC[Main.maxNPCs];
            for (int i = 0; i < Main.npc.Length; i++) Main.npc[i] = new NPC { whoAmI = i };
            Main.item = new Item[Main.maxItems + 1];
            for (int i = 0; i < Main.item.Length; i++) Main.item[i] = new Item { whoAmI = i };
            for (int i = 0; i < Netplay.Clients.Length; i++) Netplay.Clients[i] = new RemoteClient { Socket = new DisconnectedSocket() };
            var owner = new Player { whoAmI = 0, active = true, statLife = 100, statLifeMax2 = 100 };
            Main.player[0] = owner;
            owner.Center = new Vector2(480f, 480f);
            SoulboundCompanion a = CreateCompanion(owner, 20, 0, "Old companion");
            SoulboundCompanion b = CreateCompanion(owner, 21, 1, "New companion");
            owner.GetModPlayer<SoulmatesPlayer>().ActiveCompanionWhoAmI = 20;
            b.NPC.active = false;

            a.Profile.Store(new Item(ItemID.Wood, 10));
            byte[] oldPause = Packet(mod, "QuickActionRequest", w => {
                w.Write(a.Profile.Id.ToByteArray()); w.Write((byte)CompanionQuickAction.Pause);
            });
            byte[] oldWithdrawal = Packet(mod, "PackWithdrawRequest", w => {
                w.Write(a.Profile.Id.ToByteArray());
                w.Write((byte)0); w.Write(true); w.Write((byte)CompanionStorage.Resources);
                w.Write(StorageToken(a.Profile, CompanionStorage.Resources, 0));
            });
            a.NPC.active = false;
            b.NPC.active = true;
            owner.GetModPlayer<SoulmatesPlayer>().ActiveCompanionWhoAmI = 21;
            Deliver(mod, oldPause);
            Expect(!b.Profile.WorkPaused, "Late quick-action from A must not pause B", "B.WorkPaused=" + b.Profile.WorkPaused);
            Deliver(mod, Packet(mod, "QuickActionRequest", w => {
                w.Write(b.Profile.Id.ToByteArray()); w.Write((byte)CompanionQuickAction.Pause);
            }));
            Expect(b.Profile.WorkPaused, "Current companion's valid quick-action still works", "B paused");
            Deliver(mod, Packet(mod, "QuickActionRequest", w => {
                w.Write(b.Profile.Id.ToByteArray()); w.Write((byte)CompanionQuickAction.Resume);
            }));
            Expect(!b.Profile.WorkPaused, "Current companion's resume still works", "B resumed");

            b.Profile.WorkPaused = false;
            b.Profile.Store(new Item(ItemID.StoneBlock, 10));
            int before = b.Profile.ItemCount(ItemID.StoneBlock);
            Deliver(mod, oldWithdrawal);
            Expect(b.Profile.ItemCount(ItemID.StoneBlock) == before,
                "Withdrawal from an old view must not take B's resource slot",
                "B.Stone=" + before + "->" + b.Profile.ItemCount(ItemID.StoneBlock));

            // The supported new packet family is an identity-bound control case.
            byte[] oldSettings = Packet(mod, "MiningRuleRequest", w => {
                w.Write(a.Profile.Id.ToByteArray()); w.Write(true); w.Write((int)TileID.Copper); w.Write(false);
            });
            Deliver(mod, oldSettings);
            Expect(b.Profile.AllowsAutomaticMining(TileID.Copper), "Old identity-bound mining settings are rejected", "Copper still enabled");

            Item loot1 = Main.item[10] = new Item(ItemID.Gel, 1) { active = true, whoAmI = 10, playerIndexTheItemIsReservedFor = 255 };
            Item loot2 = Main.item[11] = new Item(ItemID.Wood, 1) { active = true, whoAmI = 11, playerIndexTheItemIsReservedFor = 255 };
            loot1.Center = owner.Center + new Vector2(80, 0);
            loot2.Center = owner.Center + new Vector2(-80, 0);
            b.Profile.GatheringInitiative = CompanionInitiativePolicy.Ask;
            AskGather(b, 10);
            Expect(b.HasPendingInitiative, "First initiative fixture is valid", "Gathering item 10");
            Guid previousInitiative = b.InitiativeId;
            byte[] oldAnswer = Packet(mod, "InitiativeResponseRequest", w => {
                w.Write(b.Profile.Id.ToByteArray()); w.Write(previousInitiative.ToByteArray());
                w.Write((byte)CompanionInitiativeKind.Gathering); w.Write((byte)CompanionInitiativeResponse.Always);
            });
            Call(b, "ClearPendingInitiative", 0);
            AskGather(b, 11);
            Expect(b.HasPendingInitiative && b.InitiativeId != previousInitiative && (int)Field(b, "pendingTargetItem")! == 11,
                "Replacement initiative fixture is valid", "Gathering item 11");
            Deliver(mod, oldAnswer);
            Expect(b.HasPendingInitiative && b.Profile.GatheringInitiative == CompanionInitiativePolicy.Ask,
                "Answer for superseded item 10 must not approve item 11 or change permission",
                "pending=" + b.HasPendingInitiative + "; policy=" + b.Profile.GatheringInitiative
                    + "; activeTarget=" + Field(b, "autonomyTargetItem"));
            Guid currentInitiative = b.InitiativeId;
            byte[] validAnswer = Packet(mod, "InitiativeResponseRequest", w => {
                w.Write(b.Profile.Id.ToByteArray()); w.Write(currentInitiative.ToByteArray());
                w.Write((byte)CompanionInitiativeKind.Gathering); w.Write((byte)CompanionInitiativeResponse.Yes);
            });
            Deliver(mod, validAnswer);
            Expect(!b.HasPendingInitiative && (int)Field(b, "autonomyTargetItem")! == 11,
                "Current answer starts the intended task", "Gathering item 11");
            int energyBeforeDuplicate = b.Profile.Energy;
            Deliver(mod, validAnswer);
            Expect(b.Profile.Energy == energyBeforeDuplicate && (int)Field(b, "autonomyTargetItem")! == 11,
                "Duplicate answer cannot restart or replace work", "Task and energy unchanged");

            b.SetCommand(stay: false);
            b.Profile.ObservedPickPower = 35;
            b.Profile.LearnedMiningTiles.Clear();
            owner.selectedItem = 2;
            owner.inventory[2] = new Item(ItemID.GoldPickaxe);
            owner.controlUseItem = true;
            owner.itemAnimation = 1;
            Point dirt = new(34, 30);
            Tile tile = Main.tile[dirt.X, dirt.Y];
            tile.HasTile = true; tile.TileType = TileID.Dirt;
            Player.tileTargetX = Player.tileTargetY = 0;
            Call(b, "UpdateLearningFromOwner");
            Expect(!b.Profile.KnowsMiningMaterial(TileID.Dirt), "Polling does not infer an unobserved mining target", "Dirt not learned yet");
            Deliver(mod, MiningPacket(mod, b, dirt, owner.HeldItem.type));
            Expect(b.Profile.KnowsMiningMaterial(TileID.Dirt),
                "Server must learn the remote player's observed material without local mouse globals",
                "server target=(0,0); learnedDirt=" + b.Profile.KnowsMiningMaterial(TileID.Dirt));
            Expect(Player.tileTargetX == 0 && Player.tileTargetY == 0,
                "Remote learning never changes shared mouse globals", "Server mouse target remains (0,0)");
            Expect(b.Profile.KnowsMiningMaterial(TileID.Dirt) && b.Profile.ObservedPickPower >= owner.HeldItem.pick,
                "Validated observation carries the actual held pickaxe power",
                "learnedDirt=" + b.Profile.KnowsMiningMaterial(TileID.Dirt) + "; pick=" + b.Profile.ObservedPickPower);

            Main.netMode = NetmodeID.SinglePlayer;
            Main.myPlayer = 0;
            b.SetCommand(stay: false);
            b.Profile.Personality = CompanionPersonality.Curious;
            ((CompanionAttention)Field(b, "attention")!).Reset();
            b.Profile.QuestionCadence = CompanionQuestionCadence.Chatty;
            Set(b, "personalQuestionCooldown", 0);
            Set(b, "speechTimer", 0);
            Set(b, "guardianTarget", -1);
            bool questionStarted = (bool)Call(b, "BeginChoiceQuestion", CompanionQuestion.Company)!;
            Expect(questionStarted && b.HasPendingQuestion, "Personal question fixture starts", "Company question");
            b.Profile.GatheringInitiative = CompanionInitiativePolicy.Always;
            Set(b, "autonomyDecisionTimer", 0);
            bool helpful = (bool)Call(b, "UpdateHelpfulAutonomy")!;
            Expect(helpful && b.HasPendingQuestion, "Personal questions do not block already approved automatic gathering",
                "started=" + helpful + "; pendingQuestion=" + b.HasPendingQuestion);
            Call(b, "UpdateChoiceConversation");
            Expect(b.HasPendingQuestion && b.RespondToQuestion(b.QuestionId, CompanionAnswer.Later)
                && Field(b, "autonomyActivity")!.ToString() == "FetchItem",
                "A question remains answerable without cancelling approved work", "Later answered during gathering");

            var profile = new CompanionProfile { Experience = CompanionProfile.ExperienceForLevel(10) };
            var oversized = new Item(ModContent.ItemType<ReducedStackResource>());
            // Simulate a valid old stack after a mod reduces its current maximum stack size.
            oversized.stack = 240;
            oversized.maxStack = 500;
            int legacyAmount = oversized.stack;
            profile.Resources.Add(oversized);
            TagCompound savedProfile = profile.Save();
            Item nativeLoaded = ItemIO.Load(ItemIO.Save(oversized));
            Expect(nativeLoaded.stack == legacyAmount && nativeLoaded.maxStack == 100,
                "Native load retains all saved units with the reduced current stack maximum",
                "saved=" + legacyAmount + "; native=" + nativeLoaded.stack + "; maxStack=" + nativeLoaded.maxStack);
            CompanionProfile loadedProfile = CompanionProfile.Load(savedProfile);
            Expect(loadedProfile.ItemCount(oversized.type) == legacyAmount,
                "Normalization must preserve excess units for reconciliation rather than erase them",
                "saved=" + legacyAmount + "; normalized=" + loadedProfile.ItemCount(oversized.type));

            CheckRepairBoundaries(mod, owner, a, b);
            CheckLegacyCargo();
            CheckMiningObservation(mod, owner, b);
            CheckInitiativeIdentity(mod, owner, b);
            CheckDeepReviewBoundaries(mod, owner, b);
            CheckInventoryProtocol(mod, owner, b);
            CheckStableMiningKnowledge();
            CheckSecondReviewBoundaries(mod, owner, b);
            CheckNativeRepairs(mod, owner, b);
            CheckCritterConsentBoundary(owner);
            CheckBetaAbilityBoundaries(owner);
            CheckWorkAndPetBoundaries(mod, owner);
            CheckCritterSchedulingAndDiagnosis(owner);

            CheckPanelGeometry();
            results.Add("Audit completed. Failed safety expectations=" + failedExpectations);
        }
        catch (Exception exception) {
            results.Add("HARNESS ERROR: " + exception);
            File.WriteAllLines(Path.Combine(Main.SavePath, "Soulmates-audit-checks.txt"), results);
            Console.WriteLine(string.Join(Environment.NewLine, results));
            Environment.Exit(1);
        }
        File.WriteAllLines(Path.Combine(Main.SavePath, "Soulmates-audit-checks.txt"), results);
        Console.WriteLine(string.Join(Environment.NewLine, results));
        Environment.Exit(failedExpectations == 0 ? 0 : 2);
    }

    private void Expect(bool condition, string expectation, string evidence)
    {
        if (!condition) failedExpectations++;
        results.Add((condition ? "PASS: " : "DEFECT: ") + expectation + ". " + evidence);
    }

    private void CheckPanelGeometry()
    {
        const BindingFlags flags = Private | BindingFlags.Public;
        var font = new DynamicSpriteFont(0f, 16, '?');
        Type glyphType = typeof(DynamicSpriteFont).GetNestedType("SpriteCharacterData", flags)!;
        object glyph = Activator.CreateInstance(glyphType, flags, null, new object[] {
            null!, new Rectangle(0, 0, 8, 16), new Rectangle(0, 0, 8, 16), new Vector3(0, 8, 0)
        }, null)!;
        typeof(DynamicSpriteFont).GetField("_defaultCharacterData", flags)!.SetValue(font, glyph);
        var asset = (Asset<DynamicSpriteFont>)Activator.CreateInstance(typeof(Asset<DynamicSpriteFont>), flags,
            null, new object[] { "Audit measurement font" }, null)!;
        typeof(Asset<DynamicSpriteFont>).GetMethod("SubmitLoadedContent", flags)!.Invoke(asset, new object[] { font, null! });
        FontAssets.MouseText = FontAssets.DeathText = asset;
        Main.dedServ = false; Main.gameMenu = false; Main.myPlayer = 0;
        foreach ((Point pixels, float requestedScale) in new[] {
            (new Point(800, 600), 1f), (new Point(1280, 720), 0.85f),
            (new Point(1280, 720), 1.5f), (new Point(1920, 1080), 1.5f),
            (new Point(1920, 1080), 3f)
        }) {
            Main.screenWidth = pixels.X; Main.screenHeight = pixels.Y;
            typeof(PlayerInput).GetField("_originalScreenWidth", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, pixels.X);
            typeof(PlayerInput).GetField("_originalScreenHeight", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, pixels.Y);
            // Native scale limits depend on the real viewport, not only Main.screenWidth.
            Main.UIScale = requestedScale;
            foreach (UIState state in new UIState[] { new SoulCreatorState(), new FeedbackMailboxState() }) {
                var ui = new UserInterface();
                UserInterface.ActiveInstance = ui;
                ui.SetState(state);
                state.Recalculate();
                CalculatedStyle viewport = ui.GetDimensions();
                CalculatedStyle panel = state.Children.First().GetOuterDimensions();
                bool fits = panel.X >= viewport.X && panel.Y >= viewport.Y
                    && panel.X + panel.Width <= viewport.X + viewport.Width
                    && panel.Y + panel.Height <= viewport.Y + viewport.Height;
                bool actionsFit = state.Children.First().Children
                    .Where(child => child is Terraria.GameContent.UI.Elements.UITextPanel<string>
                        || child.GetType().Name == "TalentChoiceElement")
                    .All(child => Fits(child.GetOuterDimensions(), viewport));
                Expect(fits && actionsFit, state.GetType().Name + " panel and action bounds must fit " + pixels,
                    "requestedScale=" + requestedScale + "; effectiveScale=" + Main.UIScale
                    + "; viewport=" + viewport.Width + "x" + viewport.Height
                    + "; panel=" + panel.X + "," + panel.Y + "," + panel.Width + "," + panel.Height);
            }
        }
        results.Add("UI scope: Native layout and production UI initialization with a measurement-only font; no rendered client screenshots.");
    }

    private static bool Fits(CalculatedStyle area, CalculatedStyle viewport) => area.X >= viewport.X
        && area.Y >= viewport.Y && area.X + area.Width <= viewport.X + viewport.Width
        && area.Y + area.Height <= viewport.Y + viewport.Height;

    private static SoulboundCompanion CreateCompanion(Player owner, int index, int slot, string name)
    {
        NPC npc = Main.npc[index];
        npc.SetDefaults(ModContent.NPCType<SoulboundCompanion>());
        npc.active = true; npc.ai[0] = owner.whoAmI; npc.Center = owner.Center + new Vector2(64, -24);
        var companion = (SoulboundCompanion)npc.ModNPC;
        companion.Profile.Name = name;
        var sigil = new Item(ModContent.ItemType<SoulboundSigil>());
        ((SoulboundSigil)sigil.ModItem).Profile = companion.Profile.Clone();
        owner.inventory[slot] = sigil;
        return companion;
    }

    private static byte[] Packet(Mod mod, string name, Action<BinaryWriter> write)
    {
        byte id = Convert.ToByte(Enum.Parse(mod.GetType().GetNestedType("MessageType", BindingFlags.NonPublic)!, name));
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(id); write(writer); writer.Flush();
        return stream.ToArray();
    }

    private static void Deliver(Mod mod, byte[] packet)
    {
        using var stream = new MemoryStream(packet);
        using var reader = new BinaryReader(stream);
        mod.HandlePacket(reader, 0);
        SettleTransactions(mod);
    }

    private static void SettleTransactions(Mod mod)
    {
        Type sync = typeof(CompanionProfile).Assembly.GetType("Soulmates.Common.CompanionInventorySync")!;
        var transactions = (System.Collections.IDictionary)sync.GetField("Transactions", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        if (!transactions.Contains(0)) return;
        object pending = transactions[0]!;
        Guid id = (Guid)pending.GetType().GetProperty("Id")!.GetValue(pending)!;
        byte[] packet = Packet(mod, "InventoryReceipt", writer => {
            writer.Write(id.ToByteArray()); writer.Write(true);
            writer.Write((byte)58);
            for (byte slot = 0; slot < 58; slot++) {
                writer.Write(slot); ItemIO.Send(Main.player[0].inventory[slot], writer, writeStack: true, writeFavorite: true);
            }
        });
        using var stream = new MemoryStream(packet);
        using var reader = new BinaryReader(stream);
        mod.HandlePacket(reader, 0);
    }

    private static object? Field(SoulboundCompanion mate, string name) => typeof(SoulboundCompanion).GetField(name, Private)!.GetValue(mate);
    private static void Set(SoulboundCompanion mate, string name, object value) => typeof(SoulboundCompanion).GetField(name, Private)!.SetValue(mate, value);
    private static object? Call(SoulboundCompanion mate, string name, params object[] values) => typeof(SoulboundCompanion).GetMethod(name, Private)!.Invoke(mate, values);

    private static void AskGather(SoulboundCompanion mate, int index)
    {
        Type activity = typeof(SoulboundCompanion).GetNestedType("AutonomyActivity", BindingFlags.NonPublic)!;
        Type forest = typeof(SoulboundCompanion).GetNestedType("ForestAction", BindingFlags.NonPublic)!;
        Call(mate, "ConsiderInitiative", Enum.Parse(activity, "FetchItem"), index, Point.Zero, Enum.Parse(forest, "None"), null!);
    }
}
