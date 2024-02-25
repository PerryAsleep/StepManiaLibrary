using System;
using System.Text.Json.Serialization;
using Fumen;

namespace StepManiaLibrary.PerformedChart;

/// <summary>
/// Configuration for tightening steps.
/// See PerformedChart.md for more information.
/// </summary>
public class StepTighteningConfig : StepManiaLibrary.Config, IEquatable<StepTighteningConfig>
{
	/// <summary>
	/// Tag for logging messages.
	/// </summary>
	private const string LogTag = "StepTighteningConfig";

	private const double InvalidMinPanelDistance = -100.0;

	/// <summary>
	/// Whether or not to tighten travel speed.
	/// </summary>
	[JsonInclude]
	public bool? SpeedTighteningEnabled
	{
		get => SpeedTighteningEnabledInternal;
		set
		{
			SpeedTighteningEnabledInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private bool? SpeedTighteningEnabledInternal;

	/// <summary>
	/// When limiting travel speed, the lower time for the tightening range.
	/// Time in seconds between steps for one foot.
	/// </summary>
	[JsonInclude]
	public double SpeedMinTimeSeconds
	{
		get => SpeedMinTimeSecondsInternal;
		set
		{
			SpeedMinTimeSecondsInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private double SpeedMinTimeSecondsInternal = -1.0;

	/// <summary>
	/// When limiting travel speed, the higher time for the tightening range.
	/// Time in seconds between steps for one foot.
	/// </summary>
	[JsonInclude]
	public double SpeedMaxTimeSeconds
	{
		get => SpeedMaxTimeSecondsInternal;
		set
		{
			SpeedMaxTimeSecondsInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private double SpeedMaxTimeSecondsInternal = -1.0;

	/// <summary>
	/// Minimum distance in panel lengths for speed tightening rules to apply.
	/// </summary>
	[JsonInclude]
	public double SpeedTighteningMinDistance
	{
		get => SpeedTighteningMinDistanceInternal;
		set
		{
			SpeedTighteningMinDistanceInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private double SpeedTighteningMinDistanceInternal = -1.0;

	/// <summary>
	/// Whether or not to tighten travel distance.
	/// </summary>
	[JsonInclude]
	public bool? DistanceTighteningEnabled
	{
		get => DistanceTighteningEnabledInternal;
		set
		{
			DistanceTighteningEnabledInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private bool? DistanceTighteningEnabledInternal;

	/// <summary>
	/// When limiting travel distance, the lower distance for the tightening range.
	/// Distance is in panel widths.
	/// </summary>
	[JsonInclude]
	public double DistanceMin
	{
		get => DistanceMinInternal;
		set
		{
			DistanceMinInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private double DistanceMinInternal = -1.0;

	/// <summary>
	/// When limiting travel distance, the higher distance for the tightening range.
	/// Distance is in panel widths.
	/// </summary>
	[JsonInclude]
	public double DistanceMax
	{
		get => DistanceMaxInternal;
		set
		{
			DistanceMaxInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private double DistanceMaxInternal = -1.0;

	/// <summary>
	/// Whether or not to tighten stretch.
	/// </summary>
	[JsonInclude]
	public bool? StretchTighteningEnabled
	{
		get => StretchTighteningEnabledInternal;
		set
		{
			StretchTighteningEnabledInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private bool? StretchTighteningEnabledInternal;

	/// <summary>
	/// When limiting stretch, the lower distance for the tightening range.
	/// Distance is in panels width.
	/// </summary>
	[JsonInclude]
	public double StretchDistanceMin
	{
		get => StretchDistanceMinInternal;
		set
		{
			StretchDistanceMinInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private double StretchDistanceMinInternal = -1.0;

	/// <summary>
	/// When limiting stretch, the higher distance for the tightening range.
	/// Distance is in panels width.
	/// </summary>
	[JsonInclude]
	public double StretchDistanceMax
	{
		get => StretchDistanceMaxInternal;
		set
		{
			StretchDistanceMaxInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private double StretchDistanceMaxInternal = -1.0;

	/// <summary>
	/// The minimum distance that the foot needs to move laterally into a panel in
	/// order to comfortably step on it.
	/// </summary>
	[JsonInclude]
	public double LateralMinPanelDistance
	{
		get => LateralMinPanelDistanceInternal;
		set
		{
			LateralMinPanelDistanceInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private double LateralMinPanelDistanceInternal = InvalidMinPanelDistance;

	/// <summary>
	/// The minimum distance that the foot needs to move longitudinally into a panel
	/// in order to comfortable step on it.
	/// </summary>
	[JsonInclude]
	public double LongitudinalMinPanelDistance
	{
		get => LongitudinalMinPanelDistanceInternal;
		set
		{
			LongitudinalMinPanelDistanceInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private double LongitudinalMinPanelDistanceInternal = InvalidMinPanelDistance;

	/// <summary>
	/// Sets this StepTighteningConfig to be an override of the the given other StepTighteningConfig.
	/// Any values in this StepTighteningConfig which are at their default, invalid values will
	/// be replaced with the corresponding values in the given other StepTighteningConfig.
	/// Called before Init and Validate.
	/// </summary>
	/// <param name="other">Other StepTighteningConfig to use as as a base.</param>
	public void SetAsOverrideOf(StepTighteningConfig other)
	{
		SpeedTighteningEnabled ??= other.SpeedTighteningEnabled;
		if (SpeedMinTimeSeconds.DoubleEquals(-1.0))
			SpeedMinTimeSeconds = other.SpeedMinTimeSeconds;
		if (SpeedMaxTimeSeconds.DoubleEquals(-1.0))
			SpeedMaxTimeSeconds = other.SpeedMaxTimeSeconds;
		if (SpeedTighteningMinDistance.DoubleEquals(-1.0))
			SpeedTighteningMinDistance = other.SpeedTighteningMinDistance;
		DistanceTighteningEnabled ??= other.DistanceTighteningEnabled;
		if (DistanceMin.DoubleEquals(-1.0))
			DistanceMin = other.DistanceMin;
		if (DistanceMax.DoubleEquals(-1.0))
			DistanceMax = other.DistanceMax;
		StretchTighteningEnabled ??= other.StretchTighteningEnabled;
		if (StretchDistanceMin.DoubleEquals(-1.0))
			StretchDistanceMin = other.StretchDistanceMin;
		if (StretchDistanceMax.DoubleEquals(-1.0))
			StretchDistanceMax = other.StretchDistanceMax;
		if (LateralMinPanelDistance.DoubleEquals(InvalidMinPanelDistance))
			LateralMinPanelDistance = other.LateralMinPanelDistance;
		if (LongitudinalMinPanelDistance.DoubleEquals(InvalidMinPanelDistance))
			LongitudinalMinPanelDistance = other.LongitudinalMinPanelDistance;
	}

	#region Config

	/// <summary>
	/// Returns a new StepTighteningConfig that is a clone of this StepTighteningConfig.
	/// </summary>
	public override StepTighteningConfig Clone()
	{
		return new StepTighteningConfig
		{
			SpeedTighteningEnabled = SpeedTighteningEnabled,
			SpeedMinTimeSeconds = SpeedMinTimeSeconds,
			SpeedMaxTimeSeconds = SpeedMaxTimeSeconds,
			SpeedTighteningMinDistance = SpeedTighteningMinDistance,
			DistanceTighteningEnabled = DistanceTighteningEnabled,
			DistanceMin = DistanceMin,
			DistanceMax = DistanceMax,
			StretchTighteningEnabled = StretchTighteningEnabled,
			StretchDistanceMin = StretchDistanceMin,
			StretchDistanceMax = StretchDistanceMax,
			LateralMinPanelDistance = LateralMinPanelDistance,
			LongitudinalMinPanelDistance = LongitudinalMinPanelDistance,
		};
	}

	/// <summary>
	/// Initialize data.
	/// Called after SetAsOverrideOf and before Validate.
	/// </summary>
	public override void Init()
	{
		if (LateralMinPanelDistance.DoubleEquals(InvalidMinPanelDistance))
			LateralMinPanelDistance = ArrowData.HalfPanelWidth;
		if (LongitudinalMinPanelDistance.DoubleEquals(InvalidMinPanelDistance))
			LongitudinalMinPanelDistance = ArrowData.HalfPanelHeight;
	}

	/// <summary>
	/// Log errors if any values are not valid and return whether or not there are errors.
	/// Called after SetAsOverrideOf and Init.
	/// </summary>
	/// <param name="logId">Identifier for logging.</param>
	/// <returns>True if no errors were found and false otherwise.</returns>
	public override bool Validate(string logId = null)
	{
		var errors = false;
		if (SpeedMinTimeSeconds < 0.0)
		{
			LogError(
				$"Negative value \"{SpeedMinTimeSeconds}\" "
				+ "specified for SpeedMinTimeSeconds. Expected non-negative value.",
				logId);
			errors = true;
		}

		if (SpeedMaxTimeSeconds < 0.0)
		{
			LogError(
				$"Negative value \"{SpeedMaxTimeSeconds}\" "
				+ "specified for SpeedMaxTimeSeconds. Expected non-negative value.",
				logId);
			errors = true;
		}

		if (SpeedMinTimeSeconds > SpeedMaxTimeSeconds)
		{
			LogError(
				$"SpeedMinTimeSeconds \"{SpeedMinTimeSeconds}\" "
				+ $"is greater than SpeedMaxTimeSeconds \"{SpeedMaxTimeSeconds}\". "
				+ "SpeedMinTimeSeconds must be less than or equal to SpeedMaxTimeSeconds.",
				logId);
			errors = true;
		}

		if (SpeedTighteningMinDistance < 0.0)
		{
			LogError(
				$"Negative value \"{SpeedTighteningMinDistance}\" "
				+ "specified for SpeedTighteningMinDistance. Expected non-negative value.",
				logId);
			errors = true;
		}

		if (DistanceMin < 0.0)
		{
			LogError(
				$"Negative value \"{DistanceMin}\" "
				+ "specified for DistanceMin. Expected non-negative value.",
				logId);
			errors = true;
		}

		if (DistanceMax < 0.0)
		{
			LogError(
				$"Negative value \"{DistanceMax}\" "
				+ "specified for DistanceMax. Expected non-negative value.",
				logId);
			errors = true;
		}

		if (DistanceMin > DistanceMax)
		{
			LogError(
				$"DistanceMin \"{DistanceMin}\" "
				+ $"is greater than DistanceMax \"{DistanceMax}\". "
				+ "DistanceMin must be less than or equal to DistanceMax.",
				logId);
			errors = true;
		}

		if (StretchDistanceMin < 0.0)
		{
			LogError(
				$"Negative value \"{StretchDistanceMin}\" "
				+ "specified for StretchDistanceMin. Expected non-negative value.",
				logId);
			errors = true;
		}

		if (StretchDistanceMax < 0.0)
		{
			LogError(
				$"Negative value \"{StretchDistanceMax}\" "
				+ "specified for StretchDistanceMax. Expected non-negative value.",
				logId);
			errors = true;
		}

		if (StretchDistanceMin > StretchDistanceMax)
		{
			LogError(
				$"StretchDistanceMin \"{StretchDistanceMin}\" "
				+ $"is greater than StretchDistanceMax \"{StretchDistanceMax}\". "
				+ "StretchDistanceMin must be less than or equal to StretchDistanceMax.",
				logId);
			errors = true;
		}

		return !errors;
	}

	#endregion Config

	public bool IsSpeedTighteningEnabled()
	{
		if (SpeedTighteningEnabled == null)
			return false;
		return SpeedTighteningEnabled.Value;
	}

	public bool IsDistanceTighteningEnabled()
	{
		if (DistanceTighteningEnabled == null)
			return false;
		return DistanceTighteningEnabled.Value;
	}

	public bool IsStretchTighteningEnabled()
	{
		if (StretchTighteningEnabled == null)
			return false;
		return StretchTighteningEnabled.Value;
	}

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
		return Equals((StepTighteningConfig)obj);
	}

	public override int GetHashCode()
	{
		// ReSharper disable NonReadonlyMemberInGetHashCode
		return HashCode.Combine(HashCode.Combine(SpeedTighteningEnabled,
				SpeedMinTimeSeconds,
				SpeedMaxTimeSeconds,
				SpeedTighteningMinDistance,
				DistanceTighteningEnabled,
				DistanceMin,
				DistanceMax,
				StretchTighteningEnabled),
			StretchDistanceMin,
			StretchDistanceMax,
			LateralMinPanelDistance,
			LongitudinalMinPanelDistance);
		// ReSharper restore NonReadonlyMemberInGetHashCode
	}

	public bool Equals(StepTighteningConfig other)
	{
		if (ReferenceEquals(null, other))
			return false;
		if (ReferenceEquals(this, other))
			return true;

		return SpeedTighteningEnabled == other.SpeedTighteningEnabled
		       && SpeedMinTimeSeconds.DoubleEquals(other.SpeedMinTimeSeconds)
		       && SpeedMaxTimeSeconds.DoubleEquals(other.SpeedMaxTimeSeconds)
		       && SpeedTighteningMinDistance.DoubleEquals(other.SpeedTighteningMinDistance)
		       && DistanceTighteningEnabled == other.DistanceTighteningEnabled
		       && DistanceMin.DoubleEquals(other.DistanceMin)
		       && DistanceMax.DoubleEquals(other.DistanceMax)
		       && StretchTighteningEnabled == other.StretchTighteningEnabled
		       && StretchDistanceMin.DoubleEquals(other.StretchDistanceMin)
		       && StretchDistanceMax.DoubleEquals(other.StretchDistanceMax)
		       && LateralMinPanelDistance.DoubleEquals(other.LateralMinPanelDistance)
		       && LongitudinalMinPanelDistance.DoubleEquals(other.LongitudinalMinPanelDistance);
	}

	#endregion IEquatable
}
