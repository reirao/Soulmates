#nullable enable
using System;
using System.Globalization;
using System.IO;
using BigInteger = System.Numerics.BigInteger;

namespace Soulmates.Common;

public sealed partial class CompanionProfile
{
	private BigInteger cachedExperience = -1;
	private BigInteger cachedLevel = 1;
	public BigInteger Level {
		get {
			if (cachedExperience != Experience) {
				cachedExperience = Experience;
				cachedLevel = IntegerSquareRoot(BigInteger.Max(0, Experience) / 12) + 1;
			}
			return cachedLevel;
		}
	}
	// Growth is open-ended; existing combat, cargo and retinue balance stays bounded.
	public int BalancedLevel => (int)BigInteger.Min(Level, BalanceLevel);
	public BigInteger ExperienceIntoLevel => Experience - ExperienceForLevel(Level);
	public BigInteger ExperienceNeededForNextLevel => ExperienceForLevel(Level + 1) - ExperienceForLevel(Level);
	public float LevelProgress => (float)(ExperienceIntoLevel * 10000 / ExperienceNeededForNextLevel) / 10000f;
	public static BigInteger ExperienceForLevel(BigInteger level)
	{
		BigInteger step = BigInteger.Max(1, level) - 1;
		return step * step * 12;
	}
	private static BigInteger IntegerSquareRoot(BigInteger value)
	{
		if (value <= 0) return 0;
		BigInteger estimate = BigInteger.One << checked((int)((value.GetBitLength() + 1) / 2));
		while (true) {
			BigInteger next = (estimate + value / estimate) / 2;
			if (next >= estimate) return estimate;
			estimate = next;
		}
	}
	private static BigInteger ParseExperience(string value) => value.Length <= 4096
		&& BigInteger.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out BigInteger amount)
		? amount : BigInteger.Zero;
	private static BigInteger ReadExperience(BinaryReader reader)
	{
		string value = reader.ReadString();
		BigInteger amount = ParseExperience(value);
		if (value.Length == 0 || value.Length > 4096 || amount.ToString(CultureInfo.InvariantCulture) != value)
			throw new InvalidDataException("Invalid companion experience.");
		return amount;
	}
}
