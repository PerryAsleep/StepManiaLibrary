using System;
using System.Text.Json.Serialization;
using Fumen;

namespace StepManiaLibrary.PerformedChart;

/// <summary>
/// Configuration for controlling facing.
/// See FacingControls.md for more information.
/// </summary>
public class FacingConfig : StepManiaLibrary.Config, IEquatable<FacingConfig>
{
	/// <summary>
	/// Tag for logging messages.
	/// </summary>
	private const string LogTag = "FacingConfig";

	/// <summary>
	/// Maximum percentage of steps which should be inward facing.
	/// </summary>
	[JsonInclude]
	public double MaxInwardPercentage
	{
		get => MaxInwardPercentageInternal;
		set
		{
			MaxInwardPercentageInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private double MaxInwardPercentageInternal = -1.0;

	/// <summary>
	/// Cutoff percentage to use for inward facing checks.
	/// </summary>
	[JsonInclude]
	public double InwardPercentageCutoff
	{
		get => InwardPercentageCutoffInternal;
		set
		{
			InwardPercentageCutoffInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private double InwardPercentageCutoffInternal = -1.0;

	/// <summary>
	/// Maximum percentage of steps which should be outward facing.
	/// </summary>
	[JsonInclude]
	public double MaxOutwardPercentage
	{
		get => MaxOutwardPercentageInternal;
		set
		{
			MaxOutwardPercentageInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private double MaxOutwardPercentageInternal = -1.0;

	/// <summary>
	/// Cutoff percentage to use for outward facing checks.
	/// </summary>
	[JsonInclude]
	public double OutwardPercentageCutoff
	{
		get => OutwardPercentageCutoffInternal;
		set
		{
			OutwardPercentageCutoffInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private double OutwardPercentageCutoffInternal = -1.0;

	/// <summary>
	/// Sets this FacingConfig to be an override of the given other FacingConfig.
	/// Any values in this FacingConfig which are at their default, invalid values will
	/// be replaced with the corresponding values in the given other FacingConfig.
	/// </summary>
	/// <param name="other">Other FacingConfig to use as a base.</param>
	public void SetAsOverrideOf(FacingConfig other)
	{
		if (MaxInwardPercentage.DoubleEquals(-1.0))
			MaxInwardPercentage = other.MaxInwardPercentage;
		if (InwardPercentageCutoff.DoubleEquals(-1.0))
			InwardPercentageCutoff = other.InwardPercentageCutoff;
		if (MaxOutwardPercentage.DoubleEquals(-1.0))
			MaxOutwardPercentage = other.MaxOutwardPercentage;
		if (OutwardPercentageCutoff.DoubleEquals(-1.0))
			OutwardPercentageCutoff = other.OutwardPercentageCutoff;
	}

	#region Config

	/// <summary>
	/// Returns a new FacingConfig that is a clone of this FacingConfig.
	/// </summary>
	public override FacingConfig Clone()
	{
		return new FacingConfig
		{
			MaxInwardPercentage = MaxInwardPercentage,
			InwardPercentageCutoff = InwardPercentageCutoff,
			MaxOutwardPercentage = MaxOutwardPercentage,
			OutwardPercentageCutoff = OutwardPercentageCutoff,
		};
	}

	public override void Init()
	{
		// No initialization required.
	}

	/// <summary>
	/// Log errors if any values are not valid and return whether or not there are errors.
	/// </summary>
	/// <param name="logId">Identifier for logging.</param>
	/// <returns>True if no errors were found and false otherwise.</returns>
	public override bool Validate(string logId = null)
	{
		var errors = false;
		if (MaxInwardPercentage < 0.0)
		{
			LogError(
				$"Negative value \"{MaxInwardPercentage}\" specified for "
				+ "MaxInwardPercentage. Expected non-negative value.",
				logId);
			errors = true;
		}

		if (MaxInwardPercentage > 1.0)
		{
			LogError(
				$"MaxInwardPercentage \"{MaxInwardPercentage}\" is greater 1.0. "
				+ "MaxInwardPercentage must be less than or equal to 1.0.",
				logId);
			errors = true;
		}

		if (InwardPercentageCutoff < 0.0)
		{
			LogError(
				$"Negative value \"{InwardPercentageCutoff}\" specified for "
				+ "InwardPercentageCutoff. Expected non-negative value.",
				logId);
			errors = true;
		}

		if (InwardPercentageCutoff > 1.0)
		{
			LogError(
				$"InwardPercentageCutoff \"{InwardPercentageCutoff}\" is greater 1.0. "
				+ "InwardPercentageCutoff must be less than or equal to 1.0.",
				logId);
			errors = true;
		}

		if (MaxOutwardPercentage < 0.0)
		{
			LogError(
				$"Negative value \"{MaxOutwardPercentage}\" specified for "
				+ "MaxOutwardPercentage. Expected non-negative value.",
				logId);
			errors = true;
		}

		if (MaxOutwardPercentage > 1.0)
		{
			LogError(
				$"MaxOutwardPercentage \"{MaxOutwardPercentage}\" is greater 1.0. "
				+ "MaxOutwardPercentage must be less than or equal to 1.0.",
				logId);
			errors = true;
		}

		if (OutwardPercentageCutoff < 0.0)
		{
			LogError(
				$"Negative value \"{OutwardPercentageCutoff}\" specified for "
				+ "OutwardPercentageCutoff. Expected non-negative value.",
				logId);
			errors = true;
		}

		if (OutwardPercentageCutoff > 1.0)
		{
			LogError(
				$"OutwardPercentageCutoff \"{OutwardPercentageCutoff}\" is greater 1.0. "
				+ "OutwardPercentageCutoff must be less than or equal to 1.0.",
				logId);
			errors = true;
		}

		return !errors;
	}

	#endregion Config

	#region Logging

	private static void LogError(string message, string pccId)
	{
		if (string.IsNullOrEmpty(pccId))
			Logger.Error($"[{LogTag}] {message}");
		else
			Logger.Error($"[{LogTag}] [{pccId}] {message}");
	}

	#endregion Logging

	#region IEquatable

	public override bool Equals(object obj)
	{
		if (ReferenceEquals(null, obj))
			return false;
		if (ReferenceEquals(this, obj))
			return true;
		if (obj.GetType() != GetType())
			return false;
		return Equals((FacingConfig)obj);
	}

	public override int GetHashCode()
	{
		// ReSharper disable NonReadonlyMemberInGetHashCode
		return HashCode.Combine(MaxInwardPercentage, InwardPercentageCutoff, MaxOutwardPercentage, OutwardPercentageCutoff);
		// ReSharper restore NonReadonlyMemberInGetHashCode
	}

	public bool Equals(FacingConfig other)
	{
		if (ReferenceEquals(null, other))
			return false;
		if (ReferenceEquals(this, other))
			return true;

		return MaxInwardPercentage.DoubleEquals(other.MaxInwardPercentage)
		       && InwardPercentageCutoff.DoubleEquals(other.InwardPercentageCutoff)
		       && MaxOutwardPercentage.DoubleEquals(other.MaxOutwardPercentage)
		       && OutwardPercentageCutoff.DoubleEquals(other.OutwardPercentageCutoff);
	}

	#endregion IEquatable
}
