#nullable enable
using System;
using System.IO;
using Terraria.ModLoader.IO;

namespace Soulmates.Common;

public enum CompanionMiningDirection : byte { Auto, Up, Right, Down, Left }
public enum CompanionTunnelEnd : byte { Passage, Short }
public enum CompanionWorkKind : byte { Treasure, MineArea, GatherArea, MineTarget, GatherTarget, ForestTarget }

// Recipes deliberately contain no world coordinates, item slots or NPC identities.
public readonly record struct CompanionWorkRecipe(CompanionWorkKind Kind, CompanionMiningApproach Approach,
	CompanionMiningDirection Direction, CompanionTunnelEnd End)
{
	public bool IsValid => Enum.IsDefined(Kind) && Enum.IsDefined(Approach) && Enum.IsDefined(Direction) && Enum.IsDefined(End);
	public CompanionTargetOrder? TargetOrder => Kind switch {
		CompanionWorkKind.MineTarget => CompanionTargetOrder.Mine,
		CompanionWorkKind.GatherTarget => CompanionTargetOrder.Gather,
		CompanionWorkKind.ForestTarget => CompanionTargetOrder.Forest,
		_ => null
	};
	internal void Write(BinaryWriter writer)
	{
		writer.Write((byte)Kind); writer.Write((byte)Approach);
		writer.Write((byte)Direction); writer.Write((byte)End);
	}
	internal static CompanionWorkRecipe Read(BinaryReader reader) => new((CompanionWorkKind)reader.ReadByte(),
		(CompanionMiningApproach)reader.ReadByte(), (CompanionMiningDirection)reader.ReadByte(), (CompanionTunnelEnd)reader.ReadByte());
}

public sealed partial class CompanionProfile
{
	public CompanionMiningDirection MiningDirection { get; set; }
	public CompanionTunnelEnd TunnelEnd { get; set; }
	public CompanionWorkRecipe? LastWork { get; set; }

	public CompanionWorkRecipe WorkRecipe(CompanionWorkKind kind) => new(kind, MiningApproach, MiningDirection, TunnelEnd);
	private void SaveWork(TagCompound tag)
	{
		tag["miningDirection"] = (byte)MiningDirection;
		tag["tunnelEnd"] = (byte)TunnelEnd;
		if (LastWork is { IsValid: true } recipe) tag["lastWork"] = new TagCompound {
			["kind"] = (byte)recipe.Kind, ["approach"] = (byte)recipe.Approach,
			["direction"] = (byte)recipe.Direction, ["end"] = (byte)recipe.End
		};
	}
	private void LoadWork(TagCompound tag)
	{
		MiningDirection = (CompanionMiningDirection)tag.GetByte("miningDirection");
		TunnelEnd = (CompanionTunnelEnd)tag.GetByte("tunnelEnd");
		if (tag.ContainsKey("lastWork")) {
			TagCompound recipe = tag.GetCompound("lastWork");
			if (recipe.ContainsKey("kind") && recipe.ContainsKey("approach") && recipe.ContainsKey("direction") && recipe.ContainsKey("end"))
				LastWork = new((CompanionWorkKind)recipe.GetByte("kind"), (CompanionMiningApproach)recipe.GetByte("approach"),
					(CompanionMiningDirection)recipe.GetByte("direction"), (CompanionTunnelEnd)recipe.GetByte("end"));
		}
	}
	private void WriteWork(BinaryWriter writer)
	{
		writer.Write((byte)MiningDirection); writer.Write((byte)TunnelEnd);
		bool valid = LastWork is { IsValid: true };
		writer.Write(valid);
		if (valid) LastWork!.Value.Write(writer);
	}
	private void ReadWork(BinaryReader reader)
	{
		MiningDirection = (CompanionMiningDirection)reader.ReadByte();
		TunnelEnd = (CompanionTunnelEnd)reader.ReadByte();
		if (reader.ReadBoolean()) LastWork = CompanionWorkRecipe.Read(reader);
		if (!Enum.IsDefined(MiningDirection) || !Enum.IsDefined(TunnelEnd) || LastWork is { IsValid: false })
			throw new InvalidDataException("Invalid work recipe.");
	}
	private void NormalizeWork()
	{
		if (!Enum.IsDefined(MiningDirection)) MiningDirection = CompanionMiningDirection.Auto;
		if (!Enum.IsDefined(TunnelEnd)) TunnelEnd = CompanionTunnelEnd.Passage;
		if (LastWork is { IsValid: false }) LastWork = null;
	}
}
