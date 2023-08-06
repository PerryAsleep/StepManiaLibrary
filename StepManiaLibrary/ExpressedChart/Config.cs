using System;
using System.Text.Json.Serialization;
using Fumen;

namespace StepManiaLibrary.ExpressedChart;

/// <summary>
/// Enumeration of methods for parsing brackets in ExpressedCharts.
/// When encountering two arrows it can be subjective whether these
/// arrows are meant to represent a bracket step or a jump. These behaviors
/// allow a user to better control this behavior.
/// </summary>
public enum BracketParsingMethod
{
	/// <summary>
	/// Aggressively interpret steps as brackets. In most cases brackets
	/// will preferred but in some cases jumps will still be preferred.
	/// </summary>
	Aggressive,

	/// <summary>
	/// Use a balanced method of interpreting brackets.
	/// </summary>
	Balanced,

	/// <summary>
	/// Never user brackets unless there is no other option.
	/// </summary>
	NoBrackets,
}

/// <summary>
/// Enumeration of methods for determining which BracketParsingMethod should
/// be used.
/// </summary>
public enum BracketParsingDetermination
{
	/// <summary>
	/// Dynamically choose the BracketParsingMethod based on configuration values.
	/// </summary>
	ChooseMethodDynamically,

	/// <summary>
	/// Use the configuration's default method.
	/// </summary>
	UseDefaultMethod,
}

/// <summary>
/// Enumeration of methods for copying files.
/// </summary>
public enum CopyBehavior
{
	/// <summary>
	/// Do not copy the file.
	/// </summary>
	DoNotCopy,

	/// <summary>
	/// Copy the file if it is newer than the destination file.
	/// </summary>
	IfNewer,

	/// <summary>
	/// Always copy the file.
	/// </summary>
	Always,
}

/// <summary>
/// Configuration data for ExpressedChart behavior.
/// </summary>
public class Config : IConfig<Config>, IEquatable<Config>
{
	/// <summary>
	/// Tag for logging messages.
	/// </summary>
	private const string LogTag = "ExpressedChartConfig";

	/// <summary>
	/// The default method to use for parsing brackets.
	/// </summary>
	[JsonInclude] public BracketParsingMethod DefaultBracketParsingMethod = BracketParsingMethod.Balanced;

	/// <summary>
	/// How to make the determination of which BracketParsingMethod to use.
	/// </summary>
	[JsonInclude]
	public BracketParsingDetermination BracketParsingDetermination = BracketParsingDetermination.ChooseMethodDynamically;

	/// <summary>
	/// When using the ChooseMethodDynamically BracketParsingDetermination, a level under which BracketParsingMethod NoBrackets
	/// will be chosen.
	/// </summary>
	[JsonInclude] public int MinLevelForBrackets;

	/// <summary>
	/// When using the ChooseMethodDynamically BracketParsingDetermination, whether or not encountering more simultaneous
	/// arrows than can be covered without brackets should result in using BracketParsingMethod Aggressive.
	/// </summary>
	[JsonInclude] public bool UseAggressiveBracketsWhenMoreSimultaneousNotesThanCanBeCoveredWithoutBrackets;

	/// <summary>
	/// When using the ChooseMethodDynamically BracketParsingDetermination, a Balanced bracket per minute count over which
	/// should result in BracketParsingMethod Aggressive being used.
	/// </summary>
	[JsonInclude] public double BalancedBracketsPerMinuteForAggressiveBrackets;

	/// <summary>
	/// When using the ChooseMethodDynamically BracketParsingDetermination, a Balanced bracket per minute count under which
	/// should result in BracketParsingMethod NoBrackets being used.
	/// </summary>
	[JsonInclude] public double BalancedBracketsPerMinuteForNoBrackets;

	#region IConfig

	/// <summary>
	/// Returns a new Config that is a clone of this Config.
	/// </summary>
	public Config Clone()
	{
		// All members are value types.
		return (Config)MemberwiseClone();
	}

	public void Init()
	{
		// No initialization required.
	}

	public bool Validate(string logId = null)
	{
		var errors = false;
		if (BalancedBracketsPerMinuteForAggressiveBrackets < 0.0)
		{
			LogError($"BalancedBracketsPerMinuteForAggressiveBrackets "
			         + $" {BalancedBracketsPerMinuteForAggressiveBrackets} must be non-negative.",
				logId);
			errors = true;
		}

		if (BalancedBracketsPerMinuteForNoBrackets < 0.0)
		{
			LogError($"BalancedBracketsPerMinuteForNoBrackets "
			         + $" {BalancedBracketsPerMinuteForNoBrackets} must be non-negative.",
				logId);
			errors = true;
		}

		if (BalancedBracketsPerMinuteForAggressiveBrackets <= BalancedBracketsPerMinuteForNoBrackets
		    && BalancedBracketsPerMinuteForAggressiveBrackets != 0.0
		    && BalancedBracketsPerMinuteForNoBrackets != 0.0)
		{
			LogError($"BalancedBracketsPerMinuteForAggressiveBrackets"
			         + $" {BalancedBracketsPerMinuteForAggressiveBrackets} is not greater than"
			         + $" BalancedBracketsPerMinuteForNoBrackets {BalancedBracketsPerMinuteForNoBrackets}."
			         + " If these values are non-zero, BalancedBracketsPerMinuteForAggressiveBrackets must be"
			         + " greater than BalancedBracketsPerMinuteForNoBrackets.",
				logId);
			errors = true;
		}

		return !errors;
	}

	#endregion IConfig

	#region IEquatable

	public bool Equals(Config other)
	{
		if (ReferenceEquals(null, other))
			return false;
		if (ReferenceEquals(this, other))
			return true;

		if (DefaultBracketParsingMethod != other.DefaultBracketParsingMethod)
			return false;
		if (BracketParsingDetermination != other.BracketParsingDetermination)
			return false;
		if (MinLevelForBrackets != other.MinLevelForBrackets)
			return false;
		if (UseAggressiveBracketsWhenMoreSimultaneousNotesThanCanBeCoveredWithoutBrackets !=
		    other.UseAggressiveBracketsWhenMoreSimultaneousNotesThanCanBeCoveredWithoutBrackets)
			return false;
		if (!BalancedBracketsPerMinuteForAggressiveBrackets.DoubleEquals(other.BalancedBracketsPerMinuteForAggressiveBrackets))
			return false;
		if (!BalancedBracketsPerMinuteForNoBrackets.DoubleEquals(other.BalancedBracketsPerMinuteForNoBrackets))
			return false;
		return true;
	}

	public override bool Equals(object obj)
	{
		if (ReferenceEquals(null, obj))
			return false;
		if (ReferenceEquals(this, obj))
			return true;
		if (obj.GetType() != GetType())
			return false;
		return Equals((Config)obj);
	}

	public override int GetHashCode()
	{
		// ReSharper disable NonReadonlyMemberInGetHashCode
		return HashCode.Combine(
			DefaultBracketParsingMethod,
			BracketParsingDetermination,
			MinLevelForBrackets,
			UseAggressiveBracketsWhenMoreSimultaneousNotesThanCanBeCoveredWithoutBrackets,
			BalancedBracketsPerMinuteForAggressiveBrackets,
			BalancedBracketsPerMinuteForNoBrackets);
		// ReSharper restore NonReadonlyMemberInGetHashCode
	}

	#endregion IEquatable

	#region Logging

	private static void LogError(string message, string eccId)
	{
		if (string.IsNullOrEmpty(eccId))
			Logger.Error($"[{LogTag}] {message}");
		else
			Logger.Error($"[{LogTag}] [{eccId}] {message}");
	}

	#endregion Logging
}
