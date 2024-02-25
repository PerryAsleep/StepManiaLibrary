using System;
using System.Text;
using System.Text.Json.Serialization;
using Fumen;
using Fumen.Converters;
using static StepManiaLibrary.Constants;

namespace StepManiaLibrary.PerformedChart;

/// <summary>
/// How to choose a starting lane for a foot when generating a pattern.
/// </summary>
public enum PatternConfigStartFootChoice
{
	/// <summary>
	/// Choose a starting lane automatically by having the foot step on the
	/// same lane it is already on.
	/// </summary>
	AutomaticSameLane,

	/// <summary>
	/// Choose a starting lane automatically by having the foot step on a
	/// new lane from the lane it is already on.
	/// </summary>
	AutomaticNewLane,

	/// <summary>
	/// Choose a starting lane automatically by having the foot step on either
	/// the same lane it is already on, or a new lane.
	/// </summary>
	AutomaticSameOrNewLane,

	/// <summary>
	/// Use a specified starting lane.
	/// </summary>
	SpecifiedLane,
}

/// <summary>
/// How to choose an ending lane for a foot when generating a pattern.
/// </summary>
public enum PatternConfigEndFootChoice
{
	/// <summary>
	/// Choose an ending lane automatically with no consideration given to any
	/// following steps.
	/// </summary>
	AutomaticIgnoreFollowingSteps,

	/// <summary>
	/// Choose an ending lane automatically by ending on the same lane as the
	/// foot's following step.
	/// </summary>
	AutomaticSameLaneToFollowing,

	/// <summary>
	/// Choose an ending lane automatically by ending on a lane that can step
	/// to the foot's following lane.
	/// </summary>
	AutomaticNewLaneToFollowing,

	/// <summary>
	/// Choose an ending lane automatically by ending either on the same as the
	/// foot's following step, or on a lane that can step to the foot's following lane.
	/// </summary>
	AutomaticSameOrNewLaneAsFollowing,

	/// <summary>
	/// Use a specified ending lane.
	/// </summary>
	SpecifiedLane,
}

/// <summary>
/// How to choose a starting foot when generating a pattern.
/// </summary>
public enum PatternConfigStartingFootChoice
{
	/// <summary>
	/// Choose the starting foot randomly.
	/// </summary>
	Random,

	/// <summary>
	/// Choose the starting foot automatically by having it alternate from the
	/// previous step.
	/// </summary>
	Automatic,

	/// <summary>
	/// Use a specified foot.
	/// </summary>
	Specified,
}

/// <summary>
/// Configuration for autogenerating patterns to create a PerformedChart.
/// Expected Usage:
///  Deserialize from json or instantiate as needed.
///  Call Init to perform needed initialization after loading.
///  Call Validate after Init to perform validation.
/// </summary>
public class PatternConfig : StepManiaLibrary.Config, IEquatable<PatternConfig>
{
	/// <summary>
	/// Tag for logging messages.
	/// </summary>
	private const string LogTag = "PatternConfig";

	/// <summary>
	/// How many notes per beat should be generated.
	/// These correspond to SMCommon.ValidDenominators.
	/// </summary>
	[JsonInclude]
	public int BeatSubDivision
	{
		get => BeatSubDivisionInternal;
		set
		{
			BeatSubDivisionInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private int BeatSubDivisionInternal = 4;

	/// <summary>
	/// How to choose the starting foot.
	/// </summary>
	[JsonInclude]
	public PatternConfigStartingFootChoice StartingFootChoice
	{
		get => StartingFootChoiceInternal;
		set
		{
			StartingFootChoiceInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private PatternConfigStartingFootChoice StartingFootChoiceInternal;

	/// <summary>
	/// Specified starting foot to use when StartingFootChoice is Specified.
	/// </summary>
	[JsonInclude]
	public int StartingFootSpecified
	{
		get => StartingFootSpecifiedInternal;
		set
		{
			StartingFootSpecifiedInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private int StartingFootSpecifiedInternal;

	/// <summary>
	/// How to choose the starting lane for the left foot.
	/// </summary>
	[JsonInclude]
	public PatternConfigStartFootChoice LeftFootStartChoice
	{
		get => LeftFootStartChoiceInternal;
		set
		{
			LeftFootStartChoiceInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private PatternConfigStartFootChoice LeftFootStartChoiceInternal;

	/// <summary>
	/// Specified starting lane for the left foot to use when LeftFootStartChoice is SpecifiedLane.
	/// </summary>
	[JsonInclude]
	public int LeftFootStartLaneSpecified
	{
		get => LeftFootStartLaneSpecifiedInternal;
		set
		{
			LeftFootStartLaneSpecifiedInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private int LeftFootStartLaneSpecifiedInternal;

	/// <summary>
	/// How to choose the ending lane for the left foot.
	/// </summary>
	[JsonInclude]
	public PatternConfigEndFootChoice LeftFootEndChoice
	{
		get => LeftFootEndChoiceInternal;
		set
		{
			LeftFootEndChoiceInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private PatternConfigEndFootChoice LeftFootEndChoiceInternal;

	/// <summary>
	/// Specified ending lane for the left foot to use when LeftFootEndChoice is SpecifiedLane.
	/// </summary>
	[JsonInclude]
	public int LeftFootEndLaneSpecified
	{
		get => LeftFootEndLaneSpecifiedInternal;
		set
		{
			LeftFootEndLaneSpecifiedInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private int LeftFootEndLaneSpecifiedInternal;

	/// <summary>
	/// How to choose the starting lane for the right foot.
	/// </summary>
	[JsonInclude]
	public PatternConfigStartFootChoice RightFootStartChoice
	{
		get => RightFootStartChoiceInternal;
		set
		{
			RightFootStartChoiceInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private PatternConfigStartFootChoice RightFootStartChoiceInternal;

	/// <summary>
	/// Specified starting lane for the right foot to use when RightFootStartChoice is SpecifiedLane.
	/// </summary>
	[JsonInclude]
	public int RightFootStartLaneSpecified
	{
		get => RightFootStartLaneSpecifiedInternal;
		set
		{
			RightFootStartLaneSpecifiedInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private int RightFootStartLaneSpecifiedInternal;

	/// <summary>
	/// How to choose the ending lane for the right foot.
	/// </summary>
	[JsonInclude]
	public PatternConfigEndFootChoice RightFootEndChoice
	{
		get => RightFootEndChoiceInternal;
		set
		{
			RightFootEndChoiceInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private PatternConfigEndFootChoice RightFootEndChoiceInternal;

	/// <summary>
	/// Specified ending lane for the right foot to use when RightFootEndChoice is SpecifiedLane.
	/// </summary>
	[JsonInclude]
	public int RightFootEndLaneSpecified
	{
		get => RightFootEndLaneSpecifiedInternal;
		set
		{
			RightFootEndLaneSpecifiedInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private int RightFootEndLaneSpecifiedInternal;

	/// <summary>
	/// Weight of SameArrow steps in the pattern.
	/// </summary>
	[JsonInclude]
	public int SameArrowStepWeight
	{
		get => SameArrowStepWeightInternal;
		set
		{
			SameArrowStepWeightInternal = value;
			RefreshStepWeightsNormalized();
			Notify(NotificationConfigChanged, this);
		}
	}

	private int SameArrowStepWeightInternal;

	/// <summary>
	/// Weight of NewArrow steps in the pattern.
	/// </summary>
	[JsonInclude]
	public int NewArrowStepWeight
	{
		get => NewArrowStepWeightInternal;
		set
		{
			NewArrowStepWeightInternal = value;
			RefreshStepWeightsNormalized();
			Notify(NotificationConfigChanged, this);
		}
	}

	private int NewArrowStepWeightInternal;

	/// <summary>
	/// How frequently to update the cost associated with deviating from the desired StepTypes.
	/// </summary>
	[JsonInclude]
	public int StepTypeCheckPeriod
	{
		get => StepTypeCheckPeriodInternal;
		set
		{
			StepTypeCheckPeriodInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private int StepTypeCheckPeriodInternal;

	/// <summary>
	/// Whether or not to limit the number of same arrow steps per foot in a row.
	/// </summary>
	[JsonInclude]
	public bool LimitSameArrowsInARowPerFoot
	{
		get => LimitSameArrowsInARowPerFootInternal;
		set
		{
			LimitSameArrowsInARowPerFootInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private bool LimitSameArrowsInARowPerFootInternal;

	/// <summary>
	/// Maximum number of same arrows in a row per foot to use if LimitSameArrowsInARowPerFoot
	/// is true.
	/// </summary>
	[JsonInclude]
	public int MaxSameArrowsInARowPerFoot
	{
		get => MaxSameArrowsInARowPerFootInternal;
		set
		{
			MaxSameArrowsInARowPerFootInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private int MaxSameArrowsInARowPerFootInternal;

	/// <summary>
	/// Normalized weight of SameArrow steps.
	/// All normalized wights sum to 1.0.
	/// </summary>
	[JsonIgnore]
	public double SameArrowStepWeightNormalized { get; private set; }

	/// <summary>
	/// Normalized weight of SameArrow steps.
	/// All normalized wights sum to 1.0.
	/// </summary>
	[JsonIgnore]
	public double NewArrowStepWeightNormalized { get; private set; }

	/// <summary>
	/// Refreshes the normalized arrow weights from their non-normalized values.
	/// </summary>
	public void RefreshStepWeightsNormalized()
	{
		double totalStepTypeWeight = SameArrowStepWeight + NewArrowStepWeight;
		SameArrowStepWeightNormalized = totalStepTypeWeight > 0 ? SameArrowStepWeight / totalStepTypeWeight : 0.0;
		NewArrowStepWeightNormalized = totalStepTypeWeight > 0 ? NewArrowStepWeight / totalStepTypeWeight : 0.0;
	}

	#region Config

	/// <summary>
	/// Returns a new PatternConfig that is a clone of this PatternConfig.
	/// </summary>
	public override PatternConfig Clone()
	{
		return new PatternConfig
		{
			BeatSubDivision = BeatSubDivision,
			StartingFootChoice = StartingFootChoice,
			StartingFootSpecified = StartingFootSpecified,
			LeftFootStartChoice = LeftFootStartChoice,
			LeftFootStartLaneSpecified = LeftFootStartLaneSpecified,
			LeftFootEndChoice = LeftFootEndChoice,
			LeftFootEndLaneSpecified = LeftFootEndLaneSpecified,
			RightFootStartChoice = RightFootStartChoice,
			RightFootStartLaneSpecified = RightFootStartLaneSpecified,
			RightFootEndChoice = RightFootEndChoice,
			RightFootEndLaneSpecified = RightFootEndLaneSpecified,
			SameArrowStepWeight = SameArrowStepWeight,
			NewArrowStepWeight = NewArrowStepWeight,
			StepTypeCheckPeriod = StepTypeCheckPeriod,
			LimitSameArrowsInARowPerFoot = LimitSameArrowsInARowPerFoot,
			MaxSameArrowsInARowPerFoot = MaxSameArrowsInARowPerFoot,
			SameArrowStepWeightNormalized = SameArrowStepWeightNormalized,
			NewArrowStepWeightNormalized = NewArrowStepWeightNormalized,
		};
	}

	/// <summary>
	/// Initialize data.
	/// Called and before Validate.
	/// </summary>
	public override void Init()
	{
		RefreshStepWeightsNormalized();
	}

	/// <summary>
	/// Log errors if any values are not valid and return whether or not there are errors.
	/// </summary>
	/// <param name="logId">Identifier for logging.</param>
	/// <returns>True if no errors were found and false otherwise.</returns>
	public override bool Validate(string logId = null)
	{
		var errors = false;

		var beatSubDivisionIsValid = false;
		foreach (var validDenominator in SMCommon.ValidDenominators)
		{
			if (BeatSubDivision == validDenominator)
			{
				beatSubDivisionIsValid = true;
				break;
			}
		}

		if (!beatSubDivisionIsValid)
		{
			var sb = new StringBuilder();
			var first = true;
			foreach (var validDenominator in SMCommon.ValidDenominators)
			{
				if (!first)
					sb.Append(", ");
				sb.Append(validDenominator);
				first = false;
			}

			LogError($"BeatSubDivision {BeatSubDivision} is not a valid value. Allowed values are {sb}.", logId);
			errors = true;
		}

		if (SameArrowStepWeight < 0)
		{
			LogError(
				$"Negative value \"{SameArrowStepWeight}\" "
				+ "specified for SameArrowStepWeight. Expected non-negative value.",
				logId);
			errors = true;
		}

		if (NewArrowStepWeight < 0)
		{
			LogError(
				$"Negative value \"{NewArrowStepWeight}\" "
				+ "specified for NewArrowStepWeight. Expected non-negative value.",
				logId);
			errors = true;
		}

		if (StartingFootSpecified != L && StartingFootSpecified != R)
		{
			LogError(
				$"Invalid value \"{StartingFootSpecified}\" "
				+ "specified for StartingFootSpecified. Expected 0 (left) or 1 (right).",
				logId);
			errors = true;
		}

		if (LeftFootStartLaneSpecified < 0)
		{
			LogError(
				$"Negative value \"{LeftFootStartLaneSpecified}\" "
				+ "specified for LeftFootStartLaneSpecified. Expected non-negative value.",
				logId);
			errors = true;
		}

		if (LeftFootEndLaneSpecified < 0)
		{
			LogError(
				$"Negative value \"{LeftFootEndLaneSpecified}\" "
				+ "specified for LeftFootEndLaneSpecified. Expected non-negative value.",
				logId);
			errors = true;
		}

		if (RightFootStartLaneSpecified < 0)
		{
			LogError(
				$"Negative value \"{RightFootStartLaneSpecified}\" "
				+ "specified for RightFootStartLaneSpecified. Expected non-negative value.",
				logId);
			errors = true;
		}

		if (RightFootEndLaneSpecified < 0)
		{
			LogError(
				$"Negative value \"{RightFootEndLaneSpecified}\" "
				+ "specified for RightFootEndLaneSpecified. Expected non-negative value.",
				logId);
			errors = true;
		}

		return !errors;
	}

	#endregion Config

	#region IEquatable

	public bool Equals(PatternConfig other)
	{
		if (ReferenceEquals(null, other))
			return false;
		if (ReferenceEquals(this, other))
			return true;

		return
			BeatSubDivision == other.BeatSubDivision
			&& StartingFootChoice == other.StartingFootChoice
			&& StartingFootSpecified == other.StartingFootSpecified
			&& LeftFootStartChoice == other.LeftFootStartChoice
			&& LeftFootStartLaneSpecified == other.LeftFootStartLaneSpecified
			&& LeftFootEndChoice == other.LeftFootEndChoice
			&& LeftFootEndLaneSpecified == other.LeftFootEndLaneSpecified
			&& RightFootStartChoice == other.RightFootStartChoice
			&& RightFootStartLaneSpecified == other.RightFootStartLaneSpecified
			&& RightFootEndChoice == other.RightFootEndChoice
			&& RightFootEndLaneSpecified == other.RightFootEndLaneSpecified
			&& SameArrowStepWeight == other.SameArrowStepWeight
			&& NewArrowStepWeight == other.NewArrowStepWeight
			&& StepTypeCheckPeriod == other.StepTypeCheckPeriod
			&& LimitSameArrowsInARowPerFoot == other.LimitSameArrowsInARowPerFoot
			&& MaxSameArrowsInARowPerFoot == other.MaxSameArrowsInARowPerFoot
			&& SameArrowStepWeightNormalized.DoubleEquals(other.SameArrowStepWeightNormalized)
			&& NewArrowStepWeightNormalized.DoubleEquals(other.NewArrowStepWeightNormalized);
	}

	public override bool Equals(object obj)
	{
		if (ReferenceEquals(null, obj))
			return false;
		if (ReferenceEquals(this, obj))
			return true;
		if (obj.GetType() != GetType())
			return false;
		return Equals((PatternConfig)obj);
	}

	public override int GetHashCode()
	{
		// ReSharper disable NonReadonlyMemberInGetHashCode
		return HashCode.Combine(
			BeatSubDivision,
			StartingFootChoice,
			StartingFootSpecified,
			LeftFootStartChoice,
			LeftFootStartLaneSpecified,
			LeftFootEndChoice,
			LeftFootEndLaneSpecified,
			HashCode.Combine(
				RightFootStartChoice,
				RightFootStartLaneSpecified,
				RightFootEndChoice,
				RightFootEndLaneSpecified,
				SameArrowStepWeight,
				NewArrowStepWeight,
				LimitSameArrowsInARowPerFoot,
				HashCode.Combine(
					MaxSameArrowsInARowPerFoot,
					SameArrowStepWeightNormalized,
					NewArrowStepWeightNormalized,
					StepTypeCheckPeriod)));
		// ReSharper restore NonReadonlyMemberInGetHashCode
	}

	#endregion IEquatable

	#region Logging

	private static void LogError(string message, string logId)
	{
		if (string.IsNullOrEmpty(logId))
			Logger.Error($"[{LogTag}] {message}");
		else
			Logger.Error($"[{LogTag}] [{logId}] {message}");
	}

	#endregion Logging
}
