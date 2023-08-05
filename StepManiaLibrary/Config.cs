﻿using System;
using System.Text.Json.Serialization;
using Fumen;

namespace StepManiaLibrary;

/// <summary>
/// Enumeration of methods for overwriting charts.
/// </summary>
public enum OverwriteBehavior
{
	/// <summary>
	/// Do no overwrite charts that match the output type.
	/// </summary>
	DoNotOverwrite,

	/// <summary>
	/// Overwrite existing charts if they were generated by this program.
	/// </summary>
	IfFumenGenerated,

	/// <summary>
	/// Overwrite existing charts if they were generated by this program and they
	/// were generated at an older version.
	/// </summary>
	IfFumenGeneratedAndNewerVersion,

	/// <summary>
	/// Always overwrite any existing charts.
	/// </summary>
	Always,
}

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
public class ExpressedChartConfig : IEquatable<ExpressedChartConfig>
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

	/// <summary>
	/// Returns a new ExpressedChartConfig that is a clone of this ExpressedChartConfig.
	/// </summary>
	public ExpressedChartConfig Clone()
	{
		// All members are value types.
		return (ExpressedChartConfig)MemberwiseClone();
	}

	public bool Validate(string eccId = null)
	{
		var errors = false;
		if (BalancedBracketsPerMinuteForAggressiveBrackets < 0.0)
		{
			LogError($"ExpressedChartConfig \"{eccId}\" BalancedBracketsPerMinuteForAggressiveBrackets "
			         + $" {BalancedBracketsPerMinuteForAggressiveBrackets} must be non-negative.",
				eccId);
			errors = true;
		}

		if (BalancedBracketsPerMinuteForNoBrackets < 0.0)
		{
			LogError($"ExpressedChartConfig \"{eccId}\" BalancedBracketsPerMinuteForNoBrackets "
			         + $" {BalancedBracketsPerMinuteForNoBrackets} must be non-negative.",
				eccId);
			errors = true;
		}

		if (BalancedBracketsPerMinuteForAggressiveBrackets <= BalancedBracketsPerMinuteForNoBrackets
		    && BalancedBracketsPerMinuteForAggressiveBrackets != 0.0
		    && BalancedBracketsPerMinuteForNoBrackets != 0.0)
		{
			LogError($"ExpressedChartConfig \"{eccId}\" BalancedBracketsPerMinuteForAggressiveBrackets"
			         + $" {BalancedBracketsPerMinuteForAggressiveBrackets} is not greater than"
			         + $" BalancedBracketsPerMinuteForNoBrackets {BalancedBracketsPerMinuteForNoBrackets}."
			         + " If these values are non-zero, BalancedBracketsPerMinuteForAggressiveBrackets must be"
			         + " greater than BalancedBracketsPerMinuteForNoBrackets.",
				eccId);
			errors = true;
		}

		return !errors;
	}

	#region IEquatable

	public bool Equals(ExpressedChartConfig other)
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
		return Equals((ExpressedChartConfig)obj);
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

public class LoggerConfig
{
	/// <summary>
	/// Tag for logging messages.
	/// </summary>
	private const string LogTag = "LoggerConfig";

	/// <summary>
	/// Logging log level.
	/// </summary>
	[JsonInclude] public LogLevel LogLevel = LogLevel.Info;

	/// <summary>
	/// Whether or not log to a file.
	/// </summary>
	[JsonInclude] public bool LogToFile;

	/// <summary>
	/// Directory to use for writing the log file.
	/// </summary>
	[JsonInclude] public string LogDirectory;

	/// <summary>
	/// Interval in seconds after which to flush the log file to disk.
	/// </summary>
	[JsonInclude] public int LogFlushIntervalSeconds;

	/// <summary>
	/// Log buffer size in bytes. When full the buffer log will flush to disk.
	/// </summary>
	[JsonInclude] public int LogBufferSizeBytes;

	/// <summary>
	/// Whether or not to log to the console.
	/// </summary>
	[JsonInclude] public bool LogToConsole;

	public bool Validate()
	{
		var errors = false;

		if (LogToFile)
		{
			if (string.IsNullOrEmpty(LogDirectory))
			{
				LogError("LogToFile is true, but no LogDirectory specified.");
				errors = true;
			}

			if (LogBufferSizeBytes <= 0)
			{
				LogError("Expected a non-negative LogBufferSizeBytes.");
				errors = true;
			}
		}

		return !errors;
	}

	#region Logging

	private static void LogError(string message)
	{
		Logger.Error($"[{LogTag}] {message}");
	}

	#endregion Logging
}
