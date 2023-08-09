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
public class PatternConfig : IConfig<PatternConfig>, IEquatable<PatternConfig>
{
	/// <summary>
	/// Tag for logging messages.
	/// </summary>
	private const string LogTag = "PatternConfig";

	/// <summary>
	/// How many notes per beat should be generated.
	/// These correspond to SMCommon.ValidDenominators.
	/// </summary>
	[JsonInclude] public int BeatSubDivision = 4;

	/// <summary>
	/// How to choose the starting foot.
	/// </summary>
	[JsonInclude] public PatternConfigStartingFootChoice StartingFootChoice;

	/// <summary>
	/// Specified starting foot to use when StartingFootChoice is Specified.
	/// </summary>
	[JsonInclude] public int StartingFootSpecified;

	/// <summary>
	/// How to choose the starting lane for the left foot.
	/// </summary>
	[JsonInclude] public PatternConfigStartFootChoice LeftFootStartChoice;

	/// <summary>
	/// Specified starting lane for the left foot to use when LeftFootStartChoice is SpecifiedLane.
	/// </summary>
	[JsonInclude] public int LeftFootStartLaneSpecified;

	/// <summary>
	/// How to choose the ending lane for the left foot.
	/// </summary>
	[JsonInclude] public PatternConfigEndFootChoice LeftFootEndChoice;

	/// <summary>
	/// Specified ending lane for the left foot to use when LeftFootEndChoice is SpecifiedLane.
	/// </summary>
	[JsonInclude] public int LeftFootEndLaneSpecified;

	/// <summary>
	/// How to choose the starting lane for the right foot.
	/// </summary>
	[JsonInclude] public PatternConfigStartFootChoice RightFootStartChoice;

	/// <summary>
	/// Specified starting lane for the right foot to use when RightFootStartChoice is SpecifiedLane.
	/// </summary>
	[JsonInclude] public int RightFootStartLaneSpecified;

	/// <summary>
	/// How to choose the ending lane for the right foot.
	/// </summary>
	[JsonInclude] public PatternConfigEndFootChoice RightFootEndChoice;

	/// <summary>
	/// Specified ending lane for the right foot to use when RightFootEndChoice is SpecifiedLane.
	/// </summary>
	[JsonInclude] public int RightFootEndLaneSpecified;

	/// <summary>
	/// Weight of SameArrow steps in the pattern.
	/// </summary>
	[JsonInclude] public int SameArrowStepWeight;

	/// <summary>
	/// Weight of NewArrow steps in the pattern.
	/// </summary>
	[JsonInclude] public int NewArrowStepWeight;

	/// <summary>
	/// Maximum number of same arrows in a row per foot.
	/// If less than zero then no limit will be imposed.
	/// </summary>
	[JsonInclude] public int MaxSameArrowsInARowPerFoot = -1;

	/// <summary>
	/// Normalized weight of SameArrow steps.
	/// All normalized wights sum to 1.0.
	/// </summary>
	[JsonIgnore] public double SameArrowStepWeightNormalized;

	/// <summary>
	/// Normalized weight of SameArrow steps.
	/// All normalized wights sum to 1.0.
	/// </summary>
	[JsonIgnore] public double NewArrowStepWeightNormalized;

	#region IConfig

	/// <summary>
	/// Returns a new PatternConfig that is a clone of this PatternConfig.
	/// </summary>
	public PatternConfig Clone()
	{
		// All members are value types.
		return (PatternConfig)MemberwiseClone();
	}

	/// <summary>
	/// Initialize data.
	/// Called and before Validate.
	/// </summary>
	public void Init()
	{
		double totalStepTypeWeight = SameArrowStepWeight + NewArrowStepWeight;
		SameArrowStepWeightNormalized = SameArrowStepWeight / totalStepTypeWeight;
		NewArrowStepWeightNormalized = NewArrowStepWeight / totalStepTypeWeight;
	}

	/// <summary>
	/// Log errors if any values are not valid and return whether or not there are errors.
	/// </summary>
	/// <param name="logId">Identifier for logging.</param>
	/// <returns>True if no errors were found and false otherwise.</returns>
	public bool Validate(string logId = null)
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

	#endregion IConfig

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
				MaxSameArrowsInARowPerFoot,
				HashCode.Combine(
					SameArrowStepWeightNormalized,
					NewArrowStepWeightNormalized)));
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
