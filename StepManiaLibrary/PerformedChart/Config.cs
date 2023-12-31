﻿using System;
using Fumen.Converters;
using Fumen;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StepManiaLibrary.PerformedChart;

/// <summary>
/// Configuration data for PerformedChart behavior.
/// Expected Usage:
///  Deserialize from json or instantiate as needed.
///  For overrides, call SetAsOverrideOf before Init and Validate.
///  Call Init to perform needed initialization after loading and after SetAsOverrideOf.
///  Call Validate after Init to perform validation.
/// </summary>
public class Config : IConfig<Config>, IEquatable<Config>
{
	/// <summary>
	/// Tag for logging messages.
	/// </summary>
	private const string LogTag = "PerformedChartConfig";

	/// <summary>
	/// Configuration for controlling transitions.
	/// See TransitionControls.md for more information.
	/// </summary>
	public class TransitionConfig : IConfig<TransitionConfig>, IEquatable<TransitionConfig>
	{
		/// <summary>
		/// Whether or not to use this TransitionConfig.
		/// </summary>
		[JsonInclude] public bool? Enabled;

		/// <summary>
		/// Minimum number of steps which should occur between transitions.
		/// </summary>
		[JsonInclude] public int StepsPerTransitionMin = -1;

		/// <summary>
		/// Maximum number of steps which should occur between transitions.
		/// </summary>
		[JsonInclude] public int StepsPerTransitionMax = -1;

		/// <summary>
		/// Minimum total pad width for applying transition costs.
		/// </summary>
		[JsonInclude] public int MinimumPadWidth = -1;

		/// <summary>
		/// Cutoff percentage to use determining what constitutes a transition.
		/// </summary>
		[JsonInclude] public double TransitionCutoffPercentage = -1.0;

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

		#region IConfig

		/// <summary>
		/// Returns a new TransitionConfig that is a clone of this TransitionConfig.
		/// </summary>
		public TransitionConfig Clone()
		{
			// All members are value types.
			return (TransitionConfig)MemberwiseClone();
		}

		public void Init()
		{
			// No initialization required.
		}

		/// <summary>
		/// Log errors if any values are not valid and return whether or not there are errors.
		/// </summary>
		/// <param name="logId">Identifier for logging.</param>
		/// <returns>True if no errors were found and false otherwise.</returns>
		public bool Validate(string logId = null)
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

		#endregion IConfig

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
	}

	/// <summary>
	/// Configuration for controlling facing.
	/// See FacingControls.md for more information.
	/// </summary>
	public class FacingConfig : IConfig<FacingConfig>, IEquatable<FacingConfig>
	{
		/// <summary>
		/// Maximum percentage of steps which should be inward facing.
		/// </summary>
		[JsonInclude] public double MaxInwardPercentage = -1.0;

		/// <summary>
		/// Cutoff percentage to use for inward facing checks.
		/// </summary>
		[JsonInclude] public double InwardPercentageCutoff = -1.0;

		/// <summary>
		/// Maximum percentage of steps which should be outward facing.
		/// </summary>
		[JsonInclude] public double MaxOutwardPercentage = -1.0;

		/// <summary>
		/// Cutoff percentage to use for outward facing checks.
		/// </summary>
		[JsonInclude] public double OutwardPercentageCutoff = -1.0;

		/// <summary>
		/// Sets this FacingConfig to be an override of the the given other FacingConfig.
		/// Any values in this FacingConfig which are at their default, invalid values will
		/// be replaced with the corresponding values in the given other FacingConfig.
		/// </summary>
		/// <param name="other">Other FacingConfig to use as as a base.</param>
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

		#region IConfig

		/// <summary>
		/// Returns a new FacingConfig that is a clone of this FacingConfig.
		/// </summary>
		public FacingConfig Clone()
		{
			// All members are value types.
			return (FacingConfig)MemberwiseClone();
		}

		public void Init()
		{
			// No initialization required.
		}

		/// <summary>
		/// Log errors if any values are not valid and return whether or not there are errors.
		/// </summary>
		/// <param name="logId">Identifier for logging.</param>
		/// <returns>True if no errors were found and false otherwise.</returns>
		public bool Validate(string logId = null)
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

		#endregion IConfig

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

	/// <summary>
	/// Configuration for tightening steps.
	/// See PerformedChart.md for more information.
	/// </summary>
	public class StepTighteningConfig : IConfig<StepTighteningConfig>, IEquatable<StepTighteningConfig>
	{
		private const double InvalidMinPanelDistance = -100.0;

		/// <summary>
		/// Whether or not to tighten travel speed.
		/// </summary>
		[JsonInclude] public bool? SpeedTighteningEnabled;

		/// <summary>
		/// When limiting travel speed, the lower time for the tightening range.
		/// Time in seconds between steps for one foot.
		/// </summary>
		[JsonInclude] public double SpeedMinTimeSeconds = -1.0;

		/// <summary>
		/// When limiting travel speed, the higher time for the tightening range.
		/// Time in seconds between steps for one foot.
		/// </summary>
		[JsonInclude] public double SpeedMaxTimeSeconds = -1.0;

		/// <summary>
		/// Minimum distance in panel lengths for speed tightening rules to apply.
		/// </summary>
		[JsonInclude] public double SpeedTighteningMinDistance = -1.0;

		/// <summary>
		/// Whether or not to tighten travel distance.
		/// </summary>
		[JsonInclude] public bool? DistanceTighteningEnabled;

		/// <summary>
		/// When limiting travel distance, the lower distance for the tightening range.
		/// Distance is in panel widths.
		/// </summary>
		[JsonInclude] public double DistanceMin = -1.0;

		/// <summary>
		/// When limiting travel distance, the higher distance for the tightening range.
		/// Distance is in panel widths.
		/// </summary>
		[JsonInclude] public double DistanceMax = -1.0;

		/// <summary>
		/// Whether or not to tighten stretch.
		/// </summary>
		[JsonInclude] public bool? StretchTighteningEnabled;

		/// <summary>
		/// When limiting stretch, the lower distance for the tightening range.
		/// Distance is in panels width.
		/// </summary>
		[JsonInclude] public double StretchDistanceMin = -1.0;

		/// <summary>
		/// When limiting stretch, the higher distance for the tightening range.
		/// Distance is in panels width.
		/// </summary>
		[JsonInclude] public double StretchDistanceMax = -1.0;

		/// <summary>
		/// The minimum distance that the foot needs to move laterally into a panel in
		/// order to comfortably step on it.
		/// </summary>
		[JsonInclude] public double LateralMinPanelDistance = InvalidMinPanelDistance;

		/// <summary>
		/// The minimum distance that the foot needs to move longitudinally into a panel
		/// in order to comfortable step on it.
		/// </summary>
		[JsonInclude] public double LongitudinalMinPanelDistance = InvalidMinPanelDistance;

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

		#region IConfig

		/// <summary>
		/// Returns a new StepTighteningConfig that is a clone of this StepTighteningConfig.
		/// </summary>
		public StepTighteningConfig Clone()
		{
			// All members are value types.
			return (StepTighteningConfig)MemberwiseClone();
		}

		/// <summary>
		/// Initialize data.
		/// Called after SetAsOverrideOf and before Validate.
		/// </summary>
		public void Init()
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
		public bool Validate(string logId = null)
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

		#endregion IConfig

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

	/// <summary>
	/// Configuration for tightening lateral body movement.
	/// See PerformedChart.md for more information.
	/// </summary>
	public class LateralTighteningConfig : IConfig<LateralTighteningConfig>, IEquatable<LateralTighteningConfig>
	{
		/// <summary>
		/// Whether or not to use this LateralTighteningConfig.
		/// </summary>
		[JsonInclude] public bool? Enabled;

		/// <summary>
		/// The relative notes per second over which patterns should cost more.
		/// </summary>
		[JsonInclude] public double RelativeNPS = -1.0;

		/// <summary>
		/// The absolute notes per second over which patterns should cost more.
		/// </summary>
		[JsonInclude] public double AbsoluteNPS = -1.0;

		/// <summary>
		/// The lateral body speed in arrows per second over which patterns should cost more.
		/// </summary>
		[JsonInclude] public double Speed = -1.0;

		/// <summary>
		/// Sets this LateralTighteningConfig to be an override of the the given other LateralTighteningConfig.
		/// Any values in this LateralTighteningConfig which are at their default, invalid values will
		/// be replaced with the corresponding values in the given other LateralTighteningConfig.
		/// </summary>
		/// <param name="other">Other LateralTighteningConfig to use as as a base.</param>
		public void SetAsOverrideOf(LateralTighteningConfig other)
		{
			Enabled ??= other.Enabled;
			if (RelativeNPS.DoubleEquals(-1.0))
				RelativeNPS = other.RelativeNPS;
			if (AbsoluteNPS.DoubleEquals(-1.0))
				AbsoluteNPS = other.AbsoluteNPS;
			if (Speed.DoubleEquals(-1.0))
				Speed = other.Speed;
		}

		#region IConfig

		/// <summary>
		/// Returns a new LateralTighteningConfig that is a clone of this LateralTighteningConfig.
		/// </summary>
		public LateralTighteningConfig Clone()
		{
			// All members are value types.
			return (LateralTighteningConfig)MemberwiseClone();
		}

		public void Init()
		{
			// No initialization required.
		}

		/// <summary>
		/// Log errors if any values are not valid and return whether or not there are errors.
		/// </summary>
		/// <param name="logId">Identifier for logging.</param>
		/// <returns>True if no errors were found and false otherwise.</returns>
		public bool Validate(string logId = null)
		{
			var errors = false;

			if (RelativeNPS < 0.0)
			{
				LogError(
					$"Negative value \"{RelativeNPS}\" specified for "
					+ "RelativeNPS. Expected non-negative value.",
					logId);
				errors = true;
			}

			if (AbsoluteNPS < 0.0)
			{
				LogError(
					$"Negative value \"{AbsoluteNPS}\" specified for "
					+ "AbsoluteNPS. Expected non-negative value.",
					logId);
				errors = true;
			}

			if (Speed < 0.0)
			{
				LogError(
					$"Negative value \"{Speed}\" specified for "
					+ "Speed. Expected non-negative value.",
					logId);
				errors = true;
			}

			return !errors;
		}

		#endregion IConfig

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
			return Equals((LateralTighteningConfig)obj);
		}

		public override int GetHashCode()
		{
			// ReSharper disable NonReadonlyMemberInGetHashCode
			return HashCode.Combine(Enabled, RelativeNPS, AbsoluteNPS, Speed);
			// ReSharper restore NonReadonlyMemberInGetHashCode
		}

		public bool Equals(LateralTighteningConfig other)
		{
			if (ReferenceEquals(null, other))
				return false;
			if (ReferenceEquals(this, other))
				return true;

			return Enabled == other.Enabled
			       && RelativeNPS.DoubleEquals(other.RelativeNPS)
			       && AbsoluteNPS.DoubleEquals(other.AbsoluteNPS)
			       && Speed.DoubleEquals(other.Speed);
		}

		#endregion IEquatable
	}

	/// <summary>
	/// TransitionConfig.
	/// </summary>
	[JsonInclude] public TransitionConfig Transitions = new();

	/// <summary>
	/// FacingConfig.
	/// </summary>
	[JsonInclude] public FacingConfig Facing = new();

	/// <summary>
	/// LateralTighteningConfig.
	/// </summary>
	[JsonInclude] public LateralTighteningConfig LateralTightening = new();

	/// <summary>
	/// StepTighteningConfig.
	/// </summary>
	[JsonInclude] public StepTighteningConfig StepTightening = new();

	/// <summary>
	/// Dictionary of StepMania StepsType to a List of integers representing weights
	/// for each lane. When generating a PerformedChart we should try to match these weights
	/// for distributing arrows.
	/// </summary>
	[JsonInclude] public Dictionary<string, List<int>> ArrowWeights = new();

	/// <summary>
	/// Normalized ArrowWeights.
	/// Values sum to 1.0.
	/// </summary>
	[JsonIgnore] public Dictionary<string, List<double>> ArrowWeightsNormalized = new();

	/// <summary>
	/// Sets this Config to be an override of the the given other Config.
	/// Any values in this Config which are at their default, invalid values will
	/// be replaced with the corresponding values in the given other Config.
	/// Called before Init and Validate.
	/// </summary>
	/// <param name="other">Other Config to use as as a base.</param>
	public void SetAsOverrideOf(Config other)
	{
		LateralTightening.SetAsOverrideOf(other.LateralTightening);
		StepTightening.SetAsOverrideOf(other.StepTightening);
		Facing.SetAsOverrideOf(other.Facing);

		foreach (var kvp in other.ArrowWeights)
		{
			if (!ArrowWeights.ContainsKey(kvp.Key))
			{
				ArrowWeights.Add(kvp.Key, new List<int>(kvp.Value));
			}
		}
	}

	#region IConfig

	/// <summary>
	/// Returns a new Config that is a clone of this Config.
	/// </summary>
	public Config Clone()
	{
		var newConfig = new Config
		{
			Facing = Facing.Clone(),
			LateralTightening = LateralTightening.Clone(),
			StepTightening = StepTightening.Clone(),
			Transitions = Transitions.Clone(),
			ArrowWeights = new Dictionary<string, List<int>>(),
			ArrowWeightsNormalized = new Dictionary<string, List<double>>(),
		};

		foreach (var kvp in ArrowWeights)
		{
			var newList = new List<int>();
			newList.AddRange(kvp.Value);
			newConfig.ArrowWeights.Add(kvp.Key, newList);
		}

		foreach (var kvp in ArrowWeightsNormalized)
		{
			var newList = new List<double>();
			newList.AddRange(kvp.Value);
			newConfig.ArrowWeightsNormalized.Add(kvp.Key, newList);
		}

		return newConfig;
	}

	/// <summary>
	/// Perform post-load initialization.
	/// Called after SetAsOverrideOf and before Validate.
	/// </summary>
	public void Init()
	{
		StepTightening.Init();

		// Init normalized arrow weights.
		if (ArrowWeights != null)
		{
			ArrowWeightsNormalized = new Dictionary<string, List<double>>();
			foreach (var entry in ArrowWeights)
			{
				RefreshArrowWeightsNormalized(entry.Key);
			}
		}
	}

	/// <summary>
	/// Log errors if any values are not valid and return whether or not there are errors.
	/// Called after SetAsOverrideOf and Init.
	/// </summary>
	/// <param name="logId">Identifier for logging.</param>
	/// <returns>True if no errors were found and false otherwise.</returns>
	public bool Validate(string logId = null)
	{
		var errors = !LateralTightening.Validate(logId);
		errors = !StepTightening.Validate(logId) || errors;
		errors = !Facing.Validate(logId) || errors;
		errors = !Transitions.Validate(logId) || errors;
		return !errors;
	}

	#endregion IConfig

	/// <summary>
	/// Gets the desired arrow weights for the given chart type.
	/// Values normalized to sum to 1.0.
	/// </summary>
	/// <returns>List of normalized weights.</returns>
	public List<double> GetArrowWeightsNormalized(string chartType)
	{
		if (ArrowWeightsNormalized.TryGetValue(chartType, out var weights))
			return weights;
		return new List<double>();
	}

	/// <summary>
	/// Refreshes the normalized arrow weights from their non-normalized values.
	/// </summary>
	public void RefreshArrowWeightsNormalized(string chartTypeString)
	{
		if (ArrowWeights.TryGetValue(chartTypeString, out var weights))
		{
			var normalizedWeights = new List<double>();
			var sum = 0;
			foreach (var weight in weights)
				sum += weight;
			foreach (var weight in weights)
				normalizedWeights.Add((double)weight / sum);
			ArrowWeightsNormalized[chartTypeString] = normalizedWeights;
		}
	}

	/// <summary>
	/// Log errors if any ArrowWeights are misconfigured.
	/// </summary>
	/// <param name="chartType">String identifier of the ChartType.</param>
	/// <param name="smChartType">ChartType. May not be valid.</param>
	/// <param name="smChartTypeValid">Whether the given ChartType is valid.</param>
	/// <param name="pccId">Identifier for logging.</param>
	/// <returns>True if no errors were found and false otherwise.</returns>
	public bool ValidateArrowWeights(
		string chartType,
		SMCommon.ChartType smChartType,
		bool smChartTypeValid,
		string pccId = null)
	{
		var errors = false;

		var desiredWeightsValid = ArrowWeights != null
		                          && ArrowWeights.ContainsKey(chartType);
		if (!desiredWeightsValid)
		{
			LogError($"No ArrowWeights specified for \"{chartType}\".", pccId);
			errors = true;
		}

		if (smChartTypeValid && desiredWeightsValid)
		{
			var expectedNumArrows = SMCommon.GetChartProperties(smChartType).GetNumInputs();
			if (ArrowWeights[chartType].Count != expectedNumArrows)
			{
				LogError($"ArrowWeights[\"{chartType}\"] has "
				         + $"{ArrowWeights[chartType].Count} entries. Expected {expectedNumArrows}.",
					pccId);
				errors = true;
			}

			foreach (var weight in ArrowWeights[chartType])
			{
				if (weight < 0)
				{
					LogError($"Negative weight \"{weight}\" in ArrowWeights[\"{chartType}\"].",
						pccId);
					errors = true;
				}
			}
		}

		return !errors;
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

	public bool Equals(Config other)
	{
		if (ReferenceEquals(null, other))
			return false;
		if (ReferenceEquals(this, other))
			return true;

		// Compare ArrowWeights.
		if (ArrowWeights.Count != other.ArrowWeights.Count)
			return false;
		foreach (var (key, weights) in ArrowWeights)
		{
			if (!other.ArrowWeights.ContainsKey(key))
				return false;
			var otherWeights = other.ArrowWeights[key];
			if (weights.Count != otherWeights.Count)
				return false;
			for (var i = 0; i < weights.Count; i++)
				if (weights[i] != otherWeights[i])
					return false;
		}

		// Compare ArrowWeightsNormalized.
		if (ArrowWeightsNormalized.Count != other.ArrowWeightsNormalized.Count)
			return false;
		foreach (var (key, weights) in ArrowWeightsNormalized)
		{
			if (!other.ArrowWeightsNormalized.ContainsKey(key))
				return false;
			var otherWeights = other.ArrowWeightsNormalized[key];
			if (weights.Count != otherWeights.Count)
				return false;
			for (var i = 0; i < weights.Count; i++)
				if (!weights[i].DoubleEquals(otherWeights[i]))
					return false;
		}

		if (!Transitions.Equals(other.Transitions))
			return false;
		if (!Facing.Equals(other.Facing))
			return false;
		if (!LateralTightening.Equals(other.LateralTightening))
			return false;
		if (!StepTightening.Equals(other.StepTightening))
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
			Transitions,
			Facing,
			LateralTightening,
			StepTightening,
			ArrowWeights,
			ArrowWeightsNormalized);
		// ReSharper restore NonReadonlyMemberInGetHashCode
	}

	#endregion IEquatable
}
