using System;
using System.Text.Json.Serialization;
using Fumen;

namespace StepManiaLibrary.PerformedChart;

/// <summary>
/// Configuration for controlling transitions.
/// See TransitionControls.md for more information.
/// </summary>
public class TransitionConfig : StepManiaLibrary.Config, IEquatable<TransitionConfig>
{
	/// <summary>
	/// Tag for logging messages.
	/// </summary>/
	private const string LogTag = "TransitionConfig";

	/// <summary>
	/// Whether or not to use this TransitionConfig.
	/// </summary>
	[JsonInclude]
	public bool? Enabled
	{
		get => EnabledInternal;
		set
		{
			EnabledInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private bool? EnabledInternal;

	/// <summary>
	/// Minimum number of steps which should occur between transitions.
	/// </summary>
	[JsonInclude]
	public int StepsPerTransitionMin
	{
		get => StepsPerTransitionMinInternal;
		set
		{
			StepsPerTransitionMinInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private int StepsPerTransitionMinInternal = -1;

	/// <summary>
	/// Maximum number of steps which should occur between transitions.
	/// </summary>
	[JsonInclude]
	public int StepsPerTransitionMax
	{
		get => StepsPerTransitionMaxInternal;
		set
		{
			StepsPerTransitionMaxInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private int StepsPerTransitionMaxInternal = -1;

	/// <summary>
	/// Minimum total pad width for applying transition costs.
	/// </summary>
	[JsonInclude]
	public int MinimumPadWidth
	{
		get => MinimumPadWidthInternal;
		set
		{
			MinimumPadWidthInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private int MinimumPadWidthInternal = -1;

	/// <summary>
	/// Cutoff percentage to use determining what constitutes a transition.
	/// </summary>
	[JsonInclude]
	public double TransitionCutoffPercentage
	{
		get => TransitionCutoffPercentageInternal;
		set
		{
			TransitionCutoffPercentageInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private double TransitionCutoffPercentageInternal = -1.0;

	/// <summary>
	/// Sets this TransitionConfig to be an override of the the given other TransitionConfig.
	/// Any values in this TransitionConfig which are at their default, invalid values will
	/// be replaced with the corresponding values in the given other TransitionConfig.
	/// </summary>
	/// <param name="other">Other TransitionConfig to use as as a base.</param>
	public void SetAsOverrideOf(TransitionConfig other)
	{
		Enabled ??= other.Enabled;
		if (StepsPerTransitionMin == -1)
			StepsPerTransitionMin = other.StepsPerTransitionMin;
		if (StepsPerTransitionMax == -1)
			StepsPerTransitionMax = other.StepsPerTransitionMax;
		if (MinimumPadWidth == -1)
			MinimumPadWidth = other.MinimumPadWidth;
		if (TransitionCutoffPercentage.DoubleEquals(-1.0))
			TransitionCutoffPercentage = other.TransitionCutoffPercentage;
	}

	#region Config

	/// <summary>
	/// Returns a new TransitionConfig that is a clone of this TransitionConfig.
	/// </summary>
	public override TransitionConfig Clone()
	{
		return new TransitionConfig
		{
			Enabled = Enabled,
			StepsPerTransitionMin = StepsPerTransitionMin,
			StepsPerTransitionMax = StepsPerTransitionMax,
			MinimumPadWidth = MinimumPadWidth,
			TransitionCutoffPercentage = TransitionCutoffPercentage,
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

		if (StepsPerTransitionMin < 0)
		{
			LogError(
				$"Negative value \"{StepsPerTransitionMin}\" specified for "
				+ "StepsPerTransitionMin. Expected non-negative value.",
				logId);
			errors = true;
		}

		if (MinimumPadWidth < 0)
		{
			LogError(
				$"Negative value \"{MinimumPadWidth}\" specified for "
				+ "MinimumPadWidth. Expected non-negative value.",
				logId);
			errors = true;
		}

		if (TransitionCutoffPercentage < 0.0)
		{
			LogError(
				$"Negative value \"{TransitionCutoffPercentage}\" specified for "
				+ "TransitionCutoffPercentage. Expected non-negative value.",
				logId);
			errors = true;
		}

		if (TransitionCutoffPercentage > 1.0)
		{
			LogError(
				$"TransitionCutoffPercentage \"{TransitionCutoffPercentage}\" is greater 1.0. "
				+ "TransitionCutoffPercentage must be less than or equal to 1.0.",
				logId);
			errors = true;
		}

		return !errors;
	}

	#endregion Config

	public bool IsEnabled()
	{
		if (Enabled == null)
			return false;
		return Enabled.Value;
	}

	#region IEquatable

	public override bool Equals(object obj)
	{
		if (ReferenceEquals(null, obj))
			return false;
		if (ReferenceEquals(this, obj))
			return true;
		if (obj.GetType() != GetType())
			return false;
		return Equals((TransitionConfig)obj);
	}

	public override int GetHashCode()
	{
		// ReSharper disable NonReadonlyMemberInGetHashCode
		return HashCode.Combine(Enabled, StepsPerTransitionMin, StepsPerTransitionMax, MinimumPadWidth,
			TransitionCutoffPercentage);
		// ReSharper restore NonReadonlyMemberInGetHashCode
	}

	public bool Equals(TransitionConfig other)
	{
		if (ReferenceEquals(null, other))
			return false;
		if (ReferenceEquals(this, other))
			return true;

		return Enabled == other.Enabled
		       && StepsPerTransitionMin == other.StepsPerTransitionMin
		       && StepsPerTransitionMax == other.StepsPerTransitionMax
		       && MinimumPadWidth == other.MinimumPadWidth
		       && TransitionCutoffPercentage.DoubleEquals(other.TransitionCutoffPercentage);
	}

	#endregion IEquatable

	#region Logging

	private static void LogError(string message, string pccId)
	{
		if (string.IsNullOrEmpty(pccId))
			Logger.Error($"[{LogTag}] {message}");
		else
			Logger.Error($"[{LogTag}] [{pccId}] {message}");
	}

	#endregion Logging
}
