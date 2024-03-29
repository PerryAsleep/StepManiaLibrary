﻿using System;
using System.Diagnostics;
using System.Text;
using static StepManiaLibrary.Constants;

namespace StepManiaLibrary;

/// <summary>
/// Step types that can be applied to a GraphLinkInstance or a GraphNodeInstance
/// to capture specific types of steps in a chart that aren't relevant in the
/// StepGraph.
/// </summary>
public enum InstanceStepType
{
	/// <summary>
	/// No special instance step type.
	/// </summary>
	Default,

	Roll,
	Fake,
	Lift,
}

/// <summary>
/// Link between GraphNodes in a StepGraph.
/// Represents what each foot does to move from one GraphNode to a set of other GraphNodes.
/// </summary>
[DebuggerDisplay("{ToString()}")]
public class GraphLink : IEquatable<GraphLink>
{
	/// <summary>
	/// The state for one FootPortion within a GraphLink.
	/// </summary>
	public readonly struct FootArrowState
	{
		/// <summary>
		/// StepType performed with the FootPortion.
		/// </summary>
		public readonly StepType Step;

		/// <summary>
		/// FootAction performed with the FootPortion.
		/// </summary>
		public readonly FootAction Action;

		/// <summary>
		/// Whether this is a valid FootArrowState within the GraphLink's Links.
		/// </summary>
		public readonly bool Valid;

		public FootArrowState(StepType step, FootAction action)
		{
			Step = step;
			Action = action;
			Valid = true;
		}

		public FootArrowState(StepType step, FootAction action, bool valid)
		{
			Step = step;
			Action = action;
			Valid = valid;
		}

		#region IEquatable Implementation

		public override bool Equals(object obj)
		{
			if (obj == null)
				return false;
			if (obj is FootArrowState f)
				return Step == f.Step && Action == f.Action && Valid == f.Valid;
			return false;
		}

		public override int GetHashCode()
		{
			var hash = 17;
			hash = unchecked(hash * 31 + (int)Step);
			hash = unchecked(hash * 31 + (int)Action);
			hash = unchecked(hash * 31 + (Valid ? 1 : 0));
			return hash;
		}

		#endregion
	}

	/// <summary>
	/// The actions of both feet.
	/// First index is the foot.
	/// Second index is the foot portion ([0-NumFootPortions)).
	/// For brackets, Heel (index 0) and Toe (index 1) are used for the second index.
	/// For normal steps, DefaultFootPortion (index 0) is used for the second index.
	/// </summary>
	public readonly FootArrowState[,] Links = new FootArrowState[NumFeet, NumFootPortions];

	/// <summary>
	/// Returns whether this link is completely blank.
	/// This represents a skipped step.
	/// This is normally not valid in a StepGraph and is used for fallbacks that
	/// need to remove steps.
	/// </summary>
	/// <returns>True if this GraphLink represents a blank link and false otherwise.</returns>
	public bool IsBlank()
	{
		for (var f = 0; f < NumFeet; f++)
		{
			for (var p = 0; p < NumFootPortions; p++)
			{
				if (Links[f, p].Valid)
				{
					return false;
				}
			}
		}

		return true;
	}

	/// <summary>
	/// Whether or not this link represents a jump with both feet.
	/// Includes bracket jumps
	/// </summary>
	/// <returns>True if this link is a jump and false otherwise.</returns>
	public bool IsJump()
	{
		for (var f = 0; f < NumFeet; f++)
		{
			var footHasStep = false;
			for (var p = 0; p < NumFootPortions; p++)
			{
				if (Links[f, p].Valid && Links[f, p].Action != FootAction.Release)
				{
					footHasStep = true;
					break;
				}
			}

			if (!footHasStep)
				return false;
		}

		return true;
	}

	/// <summary>
	/// Whether or not this link represents a release with any foot.
	/// </summary>
	/// <returns>True if this link is a release and false otherwise.</returns>
	public bool IsRelease()
	{
		for (var f = 0; f < NumFeet; f++)
		{
			for (var p = 0; p < NumFootPortions; p++)
			{
				if (Links[f, p].Valid && Links[f, p].Action == FootAction.Release)
					return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Whether or not this link represents a step with one foot, regardless of if
	/// that is a bracket step.
	/// </summary>
	/// <param name="foot">The foot in question.</param>
	/// <returns>True if this link is a step with the given foot and false otherwise.</returns>
	public bool IsStepWithFoot(int foot)
	{
		// If this foot does not step then this is not a step with this foot.
		var thisFootSteps = false;
		for (var p = 0; p < NumFootPortions; p++)
		{
			if (Links[foot, p].Valid && Links[foot, p].Action != FootAction.Release)
			{
				thisFootSteps = true;
				break;
			}
		}

		if (!thisFootSteps)
			return false;

		// If the other foot performs an action this is not a step with this foot.
		var otherFoot = OtherFoot(foot);
		for (var p = 0; p < NumFootPortions; p++)
		{
			if (Links[otherFoot, p].Valid)
			{
				return false;
			}
		}

		// This foot steps and the other foot does perform an action.
		return true;
	}

	/// <summary>
	/// Whether or not this link is a single, non-bracket step with only one foot.
	/// </summary>
	/// <param name="stepType">
	/// The StepType of the single step, to be set if this is a single step.
	/// </param>
	/// <param name="foot">
	/// The foot performing the single step, to be set if this is a single step.
	/// </param>
	/// <returns>
	/// True if this link is a single, non-bracket step with only one foot and
	/// false otherwise.
	/// </returns>
	public bool IsSingleStep(out StepType stepType, out int foot)
	{
		var foundStep = false;
		stepType = StepType.NewArrow;
		foot = InvalidFoot;
		for (var f = 0; f < NumFeet; f++)
		{
			for (var p = 0; p < NumFootPortions; p++)
			{
				if (Links[f, p].Valid && Links[f, p].Action != FootAction.Release)
				{
					if (foundStep)
						return false;
					foundStep = true;
					foot = f;
					stepType = Links[f, p].Step;
				}
			}
		}

		return foundStep;
	}

	/// <summary>
	/// Whether or not this link involves a step with the given foot, regardless of the actions
	/// of the other foot.
	/// </summary>
	/// <returns>True if this link involves a step with the given foot and false otherwise.</returns>
	public bool InvolvesStepWithFoot(int foot)
	{
		for (var p = 0; p < NumFootPortions; p++)
		{
			if (Links[foot, p].Valid && Links[foot, p].Action != FootAction.Release)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Whether or not this link represents a bracket step with exactly one foot.
	/// </summary>
	/// <returns>
	/// True if this link is a bracket step with exactly one foot and false otherwise.
	/// </returns>
	public bool IsBracketStep()
	{
		var numFeetWithSteps = 0;
		var bracketFound = false;
		for (var f = 0; f < NumFeet; f++)
		{
			var allPortionsStep = true;
			var anyValid = false;
			for (var p = 0; p < NumFootPortions; p++)
			{
				if (Links[f, p].Valid)
					anyValid = true;
				if (!Links[f, p].Valid || Links[f, p].Action == FootAction.Release)
					allPortionsStep = false;
			}

			if (allPortionsStep)
				bracketFound = true;
			if (anyValid)
				numFeetWithSteps++;
		}

		return numFeetWithSteps == 1 && bracketFound;
	}

	/// <summary>
	/// Whether or not this link represents a footswap.
	/// This includes a bracket that is also a footswap.
	/// </summary>
	/// <param name="foot">
	/// Out param to store the foot which performed the swap, if this is a foot swap.
	/// </param>
	/// <param name="portion">
	/// Out param to store the portion of the foot which performed the swap, if this is a foot swap.
	/// </param>
	/// <returns>True if this link is a footswap and false otherwise.</returns>
	public bool IsFootSwap(out int foot, out int portion)
	{
		foot = InvalidFoot;
		portion = DefaultFootPortion;
		for (var f = 0; f < NumFeet; f++)
		{
			for (var p = 0; p < NumFootPortions; p++)
			{
				if (Links[f, p].Valid && StepData.Steps[(int)Links[f, p].Step].IsFootSwap[p])
				{
					foot = f;
					portion = p;
					return true;
				}
			}
		}

		return false;
	}

	/// <summary>
	/// Gets wither this link involves any brackets.
	/// Includes only multiple arrow steps.
	/// </summary>
	/// <returns>True if this link involves any brackets and false otherwise.</returns>
	public bool InvolvesBracket()
	{
		for (var f = 0; f < NumFeet; f++)
		{
			for (var p = 0; p < NumFootPortions; p++)
			{
				if (Links[f, p].Valid && Links[f, p].Action != FootAction.Release &&
				    StepData.Steps[(int)Links[f, p].Step].IsBracket)
					return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Gets wither this link involves any brackets.
	/// Includes both single arrow and multiple arrow steps.
	/// </summary>
	/// <returns>True if this link involves any brackets and false otherwise.</returns>
	public bool InvolvesBracketOrSingleArrowBracket()
	{
		for (var f = 0; f < NumFeet; f++)
		{
			for (var p = 0; p < NumFootPortions; p++)
			{
				var sd = StepData.Steps[(int)Links[f, p].Step];
				if (Links[f, p].Valid && Links[f, p].Action != FootAction.Release && (sd.IsBracket || sd.IsOneArrowBracket))
					return true;
			}
		}

		return false;
	}

	#region ToString

	/// <summary>
	/// String representation.
	/// Overridden for debugger support.
	/// </summary>
	public override string ToString()
	{
		var hasLeft = false;
		var hasRight = false;
		var isRelease = false;

		for (var f = 0; f < NumFeet; f++)
		{
			for (var p = 0; p < NumFootPortions; p++)
			{
				if (Links[f, p].Valid)
				{
					if (Links[f, p].Action == FootAction.Release)
						isRelease = true;
					if (f == L)
						hasLeft = true;
					else
						hasRight = true;
				}
			}
		}

		var sb = new StringBuilder();

		if (isRelease)
			sb.Append("Release ");
		else if (hasLeft && hasRight)
			sb.Append("Jump ");

		if (hasLeft)
		{
			sb.Append("L: ");
			for (var p = 0; p < NumFootPortions; p++)
			{
				if (Links[L, p].Valid)
				{
					sb.Append(GetSimpleString(Links[L, 0].Step));
					break;
				}
			}

			if (hasRight)
				sb.Append(" ");
		}

		if (hasRight)
		{
			sb.Append("R: ");
			for (var p = 0; p < NumFootPortions; p++)
			{
				if (Links[R, p].Valid)
				{
					sb.Append(GetSimpleString(Links[R, 0].Step));
					break;
				}
			}
		}

		if (!hasLeft && !hasRight)
			sb.Append("Nothing");

		return sb.ToString();
	}

	/// <summary>
	/// ToString helper.
	/// </summary>
	private static string GetSimpleString(StepType stepType)
	{
		switch (stepType)
		{
			case StepType.SameArrow:
				return "Same";
			case StepType.NewArrow:
				return "New";
			case StepType.CrossoverFront:
			case StepType.CrossoverBehind:
				return "Crossover";
			case StepType.InvertFront:
			case StepType.InvertBehind:
				return "Invert";
			case StepType.FootSwap:
				return "Swap";
			case StepType.Swing:
				return "Swing";

			case StepType.NewArrowStretch:
				return "Stretch";
			case StepType.CrossoverFrontStretch:
			case StepType.CrossoverBehindStretch:
				return "Stretch Crossover";
			case StepType.InvertFrontStretch:
			case StepType.InvertBehindStretch:
				return "Stretch Invert";

			case StepType.FootSwapCrossoverFront:
			case StepType.FootSwapCrossoverBehind:
				return "Swap Crossover";
			case StepType.FootSwapInvertFront:
			case StepType.FootSwapInvertBehind:
				return "Swap Invert";

			case StepType.BracketHeelNewToeNew:
			case StepType.BracketHeelNewToeSame:
			case StepType.BracketHeelSameToeNew:
			case StepType.BracketHeelSameToeSame:
			case StepType.BracketHeelSameToeSwap:
			case StepType.BracketHeelNewToeSwap:
			case StepType.BracketHeelSwapToeSame:
			case StepType.BracketHeelSwapToeNew:
			case StepType.BracketHeelSwapToeSwap:
				return "Bracket";

			case StepType.BracketCrossoverFrontHeelNewToeNew:
			case StepType.BracketCrossoverFrontHeelNewToeSame:
			case StepType.BracketCrossoverFrontHeelSameToeNew:
			case StepType.BracketCrossoverBehindHeelNewToeNew:
			case StepType.BracketCrossoverBehindHeelNewToeSame:
			case StepType.BracketCrossoverBehindHeelSameToeNew:
				return "Crossover Bracket";

			case StepType.BracketInvertFrontHeelNewToeNew:
			case StepType.BracketInvertFrontHeelNewToeSame:
			case StepType.BracketInvertFrontHeelSameToeNew:
			case StepType.BracketInvertBehindHeelNewToeNew:
			case StepType.BracketInvertBehindHeelNewToeSame:
			case StepType.BracketInvertBehindHeelSameToeNew:
				return "Invert Bracket";

			case StepType.BracketStretchHeelNewToeNew:
			case StepType.BracketStretchHeelNewToeSame:
			case StepType.BracketStretchHeelSameToeNew:
				return "Stretch Bracket";

			case StepType.BracketOneArrowHeelSame:
			case StepType.BracketOneArrowHeelNew:
			case StepType.BracketOneArrowHeelSwap:
			case StepType.BracketOneArrowToeSame:
			case StepType.BracketOneArrowToeNew:
			case StepType.BracketOneArrowToeSwap:
				return "Single Bracket";

			case StepType.BracketCrossoverFrontOneArrowHeelNew:
			case StepType.BracketCrossoverFrontOneArrowToeNew:
			case StepType.BracketCrossoverBehindOneArrowHeelNew:
			case StepType.BracketCrossoverBehindOneArrowToeNew:
				return "Single Crossover Bracket";

			case StepType.BracketInvertFrontOneArrowHeelNew:
			case StepType.BracketInvertFrontOneArrowToeNew:
			case StepType.BracketInvertBehindOneArrowHeelNew:
			case StepType.BracketInvertBehindOneArrowToeNew:
				return "Single Invert Bracket";

			case StepType.BracketStretchOneArrowHeelNew:
			case StepType.BracketStretchOneArrowToeNew:
				return "Single Stretch Bracket";

			default:
				Debug.Assert(false);
				return "Unknown";
		}
	}

	#endregion

	#region IEquatable Implementation

	public bool Equals(GraphLink other)
	{
		if (other == null)
			return false;
		for (var f = 0; f < NumFeet; f++)
			for (var p = 0; p < NumFootPortions; p++)
				if (!Links[f, p].Equals(other.Links[f, p]))
					return false;
		return true;
	}

	public override bool Equals(object obj)
	{
		if (!(obj is GraphLink g))
			return false;
		return Equals(g);
	}

	public override int GetHashCode()
	{
		var hash = 17;
		for (var f = 0; f < NumFeet; f++)
			for (var p = 0; p < NumFootPortions; p++)
				hash = unchecked(hash * 31 + Links[f, p].GetHashCode());
		return hash;
	}

	#endregion
}

/// <summary>
/// Class for attaching extra data to a GraphLink when it is used for ExpressedCharts
/// and PerformedCharts. This allows GraphLink to remain slim, while supporting extra
/// information at runtime, like what type of hold (hold or roll) is used.
/// </summary>
[DebuggerDisplay("{ToString()}")]
public class GraphLinkInstance : IEquatable<GraphLinkInstance>
{
	/// <summary>
	/// Underlying GraphLink.
	/// </summary>
	public readonly GraphLink GraphLink;

	/// <summary>
	/// Per foot and portion, any special instance types for the GraphLink.
	/// </summary>
	public readonly InstanceStepType[,] InstanceTypes;

	public GraphLinkInstance()
	{
		GraphLink = new GraphLink();
		InstanceTypes = new InstanceStepType[NumFeet, NumFootPortions];
	}

	public GraphLinkInstance(GraphLink graphLink)
	{
		GraphLink = graphLink;
		InstanceTypes = new InstanceStepType[NumFeet, NumFootPortions];
	}

	public GraphLinkInstance(GraphLink graphLink, InstanceStepType[,] instanceTypes)
	{
		GraphLink = graphLink;
		InstanceTypes = instanceTypes;
		Debug.Assert(InstanceTypes.GetLength(0) == NumFeet);
		Debug.Assert(InstanceTypes.GetLength(1) == NumFootPortions);
	}

	public override string ToString()
	{
		return GraphLink.ToString();
	}

	#region IEquatable Implementation

	public bool Equals(GraphLinkInstance other)
	{
		if (other == null)
			return false;
		if (!GraphLink.Equals(other.GraphLink))
			return false;
		for (var f = 0; f < NumFeet; f++)
			for (var p = 0; p < NumFootPortions; p++)
				if (!InstanceTypes[f, p].Equals(other.InstanceTypes[f, p]))
					return false;
		return true;
	}

	public override bool Equals(object obj)
	{
		if (obj == null)
			return false;
		if (!(obj is GraphLinkInstance g))
			return false;
		return Equals(g);
	}

	public override int GetHashCode()
	{
		var hash = GraphLink.GetHashCode();
		for (var f = 0; f < NumFeet; f++)
			for (var p = 0; p < NumFootPortions; p++)
				hash = unchecked(hash * 31 + InstanceTypes[f, p].GetHashCode());
		return hash;
	}

	#endregion
}
