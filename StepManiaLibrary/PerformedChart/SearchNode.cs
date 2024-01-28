using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Fumen;
using static StepManiaLibrary.Constants;

namespace StepManiaLibrary.PerformedChart;

public partial class PerformedChart
{
	/// <summary>
	/// Search node for performing a search to find the best PerformedChart.
	/// When searching each SearchNode has at most one previous SearchNode and potentially
	/// many next SearchNode, one for each valid GraphNode reachable from each valid
	/// GraphLink out of this node.
	/// When the search is complete each SearchNode will have at most one previous
	/// SearchNode and at most one next SearchNode.
	/// Each SearchNode has a unique Id even if it represents the same GraphNode
	/// and the position, so that all nodes can be stored and compared without
	/// conflicting.
	/// </summary>
	private class SearchNode : IEquatable<SearchNode>, IComparable<SearchNode>
	{
		/// <summary>
		/// Lateral movement direction for determining lateral tightening cost.
		/// </summary>
		private enum LateralMovementDirection
		{
			Left,
			Right,
			None,
		};

		private static long IdCounter;

		/// <summary>
		/// Unique identifier for preventing conflicts when storing SearchNodes in
		/// HashSets or other data structures that rely on the IEquatable interface.
		/// </summary>
		private readonly long Id;

		/// <summary>
		/// The GraphNode at this SearchNode.
		/// </summary>
		public readonly GraphNode GraphNode;

		/// <summary>
		/// The GraphLink from the Previous SearchNode that links to this SearchNode.
		/// </summary>
		public readonly GraphLinkInstance GraphLinkFromPreviousNode;

		/// <summary>
		/// The depth of this SearchNode.
		/// This depth can also index the ExpressedChart StepEvents for accessing the StepType.
		/// Depth is the index of this SearchNode in the sequence of nodes that make up the chart.
		/// Note that while there are N SearchNodes in a complete chart, there are N-1 StepEvents in
		/// the corresponding ExpressedChart since the StepEvents represent actions between nodes.
		/// The GraphLink out of SearchNode N is StepEvent N's LinkInstance.
		/// The GraphLink into SearchNode N is StepEvent N-1's LinkInstance.
		/// </summary>
		public readonly int Depth;

		/// <summary>
		/// The previous SearchNode.
		/// Used for backing up when hitting a dead end in a search.
		/// </summary>
		public readonly SearchNode PreviousNode;

		/// <summary>
		/// All the GraphLinks which are valid for linking out of this SearchNode and into the next SearchNodes.
		/// This is a List and not just one GraphLink due to configurable StepType replacements.
		/// See Config.StepTypeReplacements.
		/// </summary>
		public readonly List<GraphLinkInstance> GraphLinks;

		/// <summary>
		/// All the valid NextNodes out of this SearchNode.
		/// These are added during the search and pruned so at the end there is at most one next SearchNode.
		/// </summary>
		public readonly Dictionary<GraphLinkInstance, HashSet<SearchNode>> NextNodes = new();

		/// <summary>
		/// This SearchNode's cost for fast steps.
		/// Higher values are worse.
		/// </summary>
		private readonly double TotalIndividualStepTravelSpeedCost;

		/// <summary>
		/// This SearchNode's cost for wide steps.
		/// Higher values are worse.
		/// </summary>
		private readonly double TotalIndividualStepTravelDistanceCost;

		/// <summary>
		/// This SearchNode's cost for using unwanted fast lateral movement.
		/// Higher values are worse.
		/// </summary>
		private readonly double TotalLateralMovementSpeedCost;

		/// <summary>
		/// Total cost for transitioning too early.
		/// </summary>
		private readonly int TotalEarlyTransitionCost;

		/// <summary>
		/// Total cost for transitioning too late.
		/// </summary>
		private readonly int TotalLateTransitionCost;

		/// <summary>
		/// If this SearchNode represents a transition, whether that transition moved left or right.
		/// Null if this SearchNode does not represent a transition.
		/// </summary>
		private readonly bool? TransitionedLeft;

		/// <summary>
		/// The most recent previous SearchNode which represented a transition.
		/// </summary>
		private readonly SearchNode LastTransitionStepNode;

		/// <summary>
		/// This SearchNode's cost for deviating from the configured DesiredArrowWeights.
		/// Higher values are worse.
		/// </summary>
		private readonly float TotalDistributionCost;

		/// <summary>
		/// This SearchNode's cost for stretch moves.
		/// Higher values are worse.
		/// </summary>
		private readonly double TotalStretchCost;

		/// <summary>
		/// This SearchNode's cost for using fallback StepTypes.
		/// Higher values are worse.
		/// </summary>
		private readonly double TotalFallbackStepCost;

		/// <summary>
		/// This note's total cost for orientations which exceed unwanted inward or outward
		/// facing cutoffs.
		/// </summary>
		private readonly double TotalFacingCost;

		/// <summary>
		/// Total number of steps in the chart up to and including this SearchNode.
		/// </summary>
		private readonly int TotalSteps;

		/// <summary>
		/// Total number of steps within the relevant pattern up to and including this SearchNode.
		/// When generating an entire chart, this will match TotalSteps.
		/// </summary>
		private readonly int TotalStepsInPattern;

		/// <summary>
		/// Total number of steps in an inward facing orientation up to and including this SearchNode.
		/// </summary>
		private readonly int TotalNumInwardSteps;

		/// <summary>
		/// Total number of steps in an outward facing orientation up to and including this SearchNode.
		/// </summary>
		private readonly int TotalNumOutwardSteps;

		/// <summary>
		/// Cost when generating patterns for deviating from desired step type weights.
		/// </summary>
		private float PatternGenerationStepTypeCost;

		/// <summary>
		/// Total number of NewArrow steps up to and including this SearchNode.
		/// </summary>
		private readonly int TotalNumNewArrowSteps;

		/// <summary>
		/// Total number of SameArrow steps up to and including this SearchNode.
		/// </summary>
		private readonly int TotalNumSameArrowSteps;

		/// <summary>
		/// Total number of SameArrow steps in a row beyond the maximum specified for generating patters.
		/// </summary>
		private readonly int TotalNumSameArrowStepsInARowPerFootOverMax;

		/// <summary>
		/// This SearchNode's random weight for when all costs are equal.
		/// Higher values are worse.
		/// </summary>
		private readonly double RandomWeight;

		/// <summary>
		/// The total number of misleading steps for the path up to and including this SearchNode.
		/// Misleading steps are steps which any reasonable player would interpret differently
		/// than intended. For example if the intent is a NewArrow NewArrow jump but that is
		/// represented as a jump from LU to UD players would keep their right foot on U as a
		/// SameArrow step while moving only their left foot, leaving them in an unexpected
		/// orientation that they will likely need to double-step to correct from.
		/// </summary>
		private readonly int MisleadingStepCount;

		/// <summary>
		/// The total number of ambiguous steps for the path to to and including this SearchNode.
		/// Ambiguous steps are steps which any reasonably player would interpret as having more
		/// than one equally viable option for performing. For example if the player is on LR and
		/// the next step is D, that could be done with either the left foot or the right foot.
		/// </summary>
		private readonly int AmbiguousStepCount;

		/// <summary>
		/// The time in seconds of the Events represented by this SearchNode.
		/// </summary>
		private readonly double Time;

		/// <summary>
		/// Lateral position of the body on the pads at this SearchNode.
		/// Units are in arrows.
		/// </summary>
		private readonly float LateralBodyPosition;

		/// <summary>
		/// Lateral movement direction of this node from the previous node.
		/// </summary>
		private readonly LateralMovementDirection MovementDir = LateralMovementDirection.None;

		/// <summary>
		/// Time that we started moving in the current lateral direction.
		/// </summary>
		private readonly double LastLateralMovementStartTime;

		/// <summary>
		/// Position from which we started moving in the current lateral direction.
		/// </summary>
		private readonly float LastLateralMovementStartPosition;

		/// <summary>
		/// Number of steps in the current lateral move.
		/// </summary>
		private readonly int LateralMovementNumSteps;

		/// <summary>
		/// For each foot, the last time in seconds that it was stepped on.
		/// During construction, these values will be updated to this SearchNode's Time
		/// if this SearchNode represents steps on any arrows.
		/// </summary>
		private readonly double[] LastTimeFootStepped;

		/// <summary>
		/// For each foot, the last time in seconds that it was released.
		/// During construction, these values will be updated to this SearchNode's Time
		/// if this SearchNode represents releases on any arrows.
		/// </summary>
		private readonly double[] LastTimeFootReleased;

		/// <summary>
		/// For each Foot and FootPortion, the last arrows that were stepped on by it.
		/// During construction, these values will be updated based on this SearchNode's
		/// steps.
		/// </summary>
		private readonly int[][] LastArrowsSteppedOnByFoot;

		/// <summary>
		/// The number of steps on each arrow up to and including this SearchNode.
		/// Used to determine Cost.
		/// </summary>
		private readonly int[] StepCounts;

		/// <summary>
		/// The PerformanceFootActions performed by this SearchNode. Index is arrow/lane.
		/// </summary>
		public readonly PerformanceFootAction[] Actions;

		/// <summary>
		/// Factory method to create a SearchNode to use as for transition comparisons.
		/// When determining when transitions occur we need to compare to a past node
		/// which transitioned to determine the cost for transitioning too early or late.
		/// When generating patterns that are only a small portion of the chart, that previous
		/// transition SearchNode node can be created through this constructor.
		/// </summary>
		/// <param name="left">
		/// Whether the node transition was to the left or right.
		/// </param>	
		/// <param name="totalSteps">
		/// The total step count at the time of the transition.
		/// </param>
		public static SearchNode MakeTransitionNode(bool? left, int totalSteps)
		{
			return new SearchNode(left, totalSteps);
		}

		/// <summary>
		/// Private constructor for making a SearchNode used for transition comparisons.
		/// When determining when transitions occur we need to compare to a past node
		/// which transitioned to determine the cost for transitioning too early or late.
		/// When generating patterns that are only a small portion of the chart, that previous
		/// transition SearchNode node can be created through this constructor.
		/// </summary>
		/// <param name="left">
		/// Whether the node transition was to the left or right.
		/// </param>
		/// <param name="totalSteps">
		/// The total step count at the time of the transition.
		/// </param>
		private SearchNode(bool? left, int totalSteps)
		{
			TransitionedLeft = left;
			TotalSteps = totalSteps;
		}

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="graphNode">
		/// GraphNode representing the state of this SearchNode.
		/// </param>
		/// <param name="possibleGraphLinksToNextNode">
		/// All the possible GraphLinkInstances out of this SearchNode to following nodes.
		/// </param>
		/// <param name="graphLinkFromPreviousNode">
		/// The GraphLink to this SearchNode from the previous SearchNode.
		/// </param>
		/// <param name="stepTypeFallbackCost">
		/// The cost to use this SearchNode compared to its siblings based on the StepTypes used
		/// to reach this node from its parent and how preferable those StepTypes are.
		/// </param>
		/// <param name="time">
		/// Time of the corresponding ExpressedChart event in seconds.
		/// </param>
		/// <param name="depth"> The 0-based depth of this SearchNode.</param>
		/// <param name="previousNode"> The previous SearchNode.</param>
		/// <param name="actions">
		/// For each arrow, the PerformanceFootAction to take for this SearchNode.
		/// </param>
		/// <param name="stepGraph">StepGraph for the PerformedChart.</param>
		/// <param name="nps">Average notes per second of the Chart.</param>
		/// <param name="randomWeight">
		/// Random weight to use as a fallback for comparing SearchNodes with equal costs.
		/// </param>
		/// <param name="config">Config to use.</param>
		/// <param name="patternConfig">
		/// Optional PatternConfig to use.
		/// This is only intended to be used for generating patterns in existing Charts.
		/// </param>
		/// <param name="specifiedStepCounts">
		/// Optional counts to use current steps per lane.
		/// If omitted, the previous SearchNode's counts will be used.
		/// This is intended to be used for generating patterns in existing Charts.
		/// </param>
		/// <param name="specifiedStepTimes">
		/// Optional times to use per foot of the last time this foot both stepped and released.
		/// If omitted, the previous SearchNode's values will be used.
		/// This is intended to be used for generating patterns in existing Charts.
		/// </param>
		/// <param name="specifiedTotalSteps">
		/// Optional value to use for the total number of steps up to this node.
		/// If omitted, the previous SearchNode's values will be used.
		/// This is intended to be used for generating patterns in existing Charts.
		/// </param>
		/// <param name="specifiedLastTransitionNode">
		/// Optional SearchNode to use as the previous SearchNode which resulted in a transition.
		/// If omitted, the previous SearchNode's values will be used.
		/// This is intended to be used for generating patterns in existing Charts.
		/// </param>
		public SearchNode(
			GraphNode graphNode,
			List<GraphLinkInstance> possibleGraphLinksToNextNode,
			GraphLinkInstance graphLinkFromPreviousNode,
			double stepTypeFallbackCost,
			double time,
			int depth,
			SearchNode previousNode,
			PerformanceFootAction[] actions,
			StepGraph stepGraph,
			double nps,
			double randomWeight,
			Config config,
			PatternConfig patternConfig = null,
			int[] specifiedStepCounts = null,
			double[] specifiedStepTimes = null,
			int? specifiedTotalSteps = null,
			SearchNode specifiedLastTransitionNode = null)
		{
			Id = Interlocked.Increment(ref IdCounter);
			GraphNode = graphNode;
			GraphLinks = possibleGraphLinksToNextNode;
			GraphLinkFromPreviousNode = graphLinkFromPreviousNode;
			Depth = depth;
			PreviousNode = previousNode;
			Time = time;
			RandomWeight = randomWeight;
			Actions = actions;

			// Store the SearchNode for the last transition.
			// This normally comes from the PreviousNode's value, but when generating patterns
			// it may be specified explicitly.
			LastTransitionStepNode = specifiedLastTransitionNode ?? PreviousNode?.LastTransitionStepNode;

			var isRelease = graphLinkFromPreviousNode?.GraphLink?.IsRelease() ?? false;
			if (specifiedTotalSteps != null)
				TotalSteps = specifiedTotalSteps.Value + (isRelease ? 0 : 1);
			else
				TotalSteps = (PreviousNode?.TotalSteps ?? 0) + (isRelease ? 0 : 1);
			TotalStepsInPattern = (PreviousNode?.TotalStepsInPattern ?? 0) + (isRelease ? 0 : 1);

			// Copy the previous SearchNode's ambiguous and misleading step counts.
			// We will update them later after determining if this SearchNode represents
			// an ambiguous or misleading step.
			AmbiguousStepCount = previousNode?.AmbiguousStepCount ?? 0;
			MisleadingStepCount = previousNode?.MisleadingStepCount ?? 0;
			TotalFallbackStepCost = stepTypeFallbackCost + (previousNode?.TotalFallbackStepCost ?? 0.0);
			TotalFacingCost = previousNode?.TotalFacingCost ?? 0;

			// Set StepCounts either from specified counts or the previous node's StepCounts.
			StepCounts = new int[Actions.Length];
			if (specifiedStepCounts != null)
			{
				for (var a = 0; a < specifiedStepCounts.Length; a++)
				{
					StepCounts[a] = specifiedStepCounts[a] + (IsStep(Actions[a]) ? 1 : 0);
				}
			}
			else
			{
				// Copy the previous SearchNode's StepCounts and update them.
				for (var a = 0; a < Actions.Length; a++)
				{
					StepCounts[a] = (previousNode?.StepCounts[a] ?? 0) + (IsStep(Actions[a]) ? 1 : 0);
				}
			}

			// Set the last step times either from specified values or the previous node.
			LastTimeFootStepped = new double[NumFeet];
			LastTimeFootReleased = new double[NumFeet];
			if (specifiedStepTimes != null)
			{
				for (var f = 0; f < NumFeet; f++)
					LastTimeFootStepped[f] = specifiedStepTimes[f];
				for (var f = 0; f < NumFeet; f++)
					LastTimeFootReleased[f] = specifiedStepTimes[f];
			}
			else
			{
				// Copy the previous SearchNode's last step times to this nodes last step times.
				// We will update them later if this SearchNode represents a step.
				for (var f = 0; f < NumFeet; f++)
					LastTimeFootStepped[f] = previousNode?.LastTimeFootStepped[f] ?? 0.0;
				for (var f = 0; f < NumFeet; f++)
					LastTimeFootReleased[f] = previousNode?.LastTimeFootReleased[f] ?? 0.0;
			}

			// Copy the previous SearchNode's LastArrowsSteppedOnByFoot values.
			// We will update them later if this SearchNode represents a step.
			LastArrowsSteppedOnByFoot = new int[NumFeet][];
			for (var f = 0; f < NumFeet; f++)
			{
				LastArrowsSteppedOnByFoot[f] = new int[NumFootPortions];
				for (var p = 0; p < NumFootPortions; p++)
				{
					LastArrowsSteppedOnByFoot[f][p] = previousNode?.LastArrowsSteppedOnByFoot[f][p]
					                                  ?? GraphNode.State[f, p].Arrow;
				}
			}

			// Update this node's facing costs.
			UpdateFacingCost(stepGraph, config, isRelease, out TotalNumInwardSteps, out TotalNumOutwardSteps,
				out TotalFacingCost);

			// Update this node's transition costs.
			TotalEarlyTransitionCost = PreviousNode?.TotalEarlyTransitionCost ?? 0;
			TotalLateTransitionCost = PreviousNode?.TotalLateTransitionCost ?? 0;
			UpdateTransitionCost(
				stepGraph,
				config.Transitions,
				out var earlyTransitionCost,
				out var lateTransitionCost,
				out TransitionedLeft,
				ref LastTransitionStepNode);
			TotalEarlyTransitionCost += earlyTransitionCost;
			TotalLateTransitionCost += lateTransitionCost;

			// Update this node's individual step travel costs.
			var (travelSpeedCost, travelDistanceCost) =
				GetStepTravelCostsAndUpdateStepTracking(stepGraph, config.StepTightening, out var isStep);
			TotalIndividualStepTravelSpeedCost = (PreviousNode?.TotalIndividualStepTravelSpeedCost ?? 0.0) + travelSpeedCost;
			TotalIndividualStepTravelDistanceCost =
				(PreviousNode?.TotalIndividualStepTravelDistanceCost ?? 0.0) + travelDistanceCost;

			// Update this node's lane distribution cost.
			TotalDistributionCost = GetDistributionCost(stepGraph, config);

			// Update this node's stretch cost.
			TotalStretchCost = (PreviousNode?.TotalStretchCost ?? 0.0) + GetStretchCost(stepGraph, config.StepTightening);

			// Update this node's lateral movement cost.
			LateralBodyPosition = GetLateralBodyPosition(stepGraph);
			UpdateLateralTracking(isStep, ref LateralMovementNumSteps, ref LastLateralMovementStartTime,
				ref LastLateralMovementStartPosition, ref MovementDir);
			TotalLateralMovementSpeedCost =
				(PreviousNode?.TotalLateralMovementSpeedCost ?? 0.0) + GetLateralMovementCost(config.LateralTightening, nps);

			// Update this node's cost for repeated SameArrow steps when generating patterns.
			UpdateStepCounts(
				patternConfig,
				out TotalNumSameArrowSteps,
				out TotalNumSameArrowStepsInARowPerFootOverMax,
				out TotalNumNewArrowSteps);
			DeterminePatternGenerationStepCost(patternConfig);

			// Update this nodes ambiguous and misleading step costs.
			var (ambiguous, misleading) = DetermineAmbiguity(stepGraph);
			if (ambiguous)
				AmbiguousStepCount++;
			if (misleading)
				MisleadingStepCount++;
		}

		/// <summary>
		/// Removes this node from its PreviousNode's NextNodes.
		/// </summary>
		public void RemoveFromPreviousNode()
		{
			PreviousNode.NextNodes[GraphLinkFromPreviousNode].Remove(this);
			if (PreviousNode.NextNodes[GraphLinkFromPreviousNode].Count == 0)
				PreviousNode.NextNodes.Remove(GraphLinkFromPreviousNode);
		}

		/// <summary>
		/// Returns whether or not this SearchNode represents a completely blank step.
		/// </summary>
		public bool IsBlank()
		{
			return GraphLinkFromPreviousNode.GraphLink.IsBlank();
		}

		/// <summary>
		/// Gets the next ChartSearchNode.
		/// Assumes that the search is complete and there is at most one next SearchNode.
		/// </summary>
		/// <returns>The next SearchNode or null if none exists.</returns>
		public SearchNode GetNextNode()
		{
			if (NextNodes.Count == 0 || NextNodes.First().Value.Count == 0)
				return null;
			return NextNodes.First().Value.First();
		}

		/// <summary>
		/// Updates variables for tracking the total number of relevant StepTypes for pattern generation.
		/// </summary>
		/// <param name="patternConfig">PatternConfig to use.</param>
		/// <param name="totalNumSameArrowSteps">Total number of SameArrow steps to set.</param>
		/// <param name="totalNumSameArrowStepsInARowPerFootOverMax">
		/// Total number of SameArrow steps in a row per foot over the specified maximum from the PatternConfig.
		/// </param>
		/// <param name="totalNumNewArrowSteps">Total number of NewArrow steps to set.</param>
		private void UpdateStepCounts(
			PatternConfig patternConfig,
			out int totalNumSameArrowSteps,
			out int totalNumSameArrowStepsInARowPerFootOverMax,
			out int totalNumNewArrowSteps)
		{
			totalNumSameArrowSteps = PreviousNode?.TotalNumSameArrowSteps ?? 0;
			totalNumSameArrowStepsInARowPerFootOverMax = PreviousNode?.TotalNumSameArrowStepsInARowPerFootOverMax ?? 0;
			totalNumNewArrowSteps = PreviousNode?.TotalNumNewArrowSteps ?? 0;

			if (GraphLinkFromPreviousNode == null)
				return;

			if (GraphLinkFromPreviousNode.GraphLink.IsSingleStep(out var stepType, out var footPerformingStep))
			{
				switch (stepType)
				{
					case StepType.NewArrow:
					{
						totalNumNewArrowSteps++;
						break;
					}
					case StepType.SameArrow:
					{
						totalNumSameArrowSteps++;
						if (patternConfig?.LimitSameArrowsInARowPerFoot == true && patternConfig.MaxSameArrowsInARowPerFoot > 0)
						{
							// Two because the previous step was the same arrow.
							// One SameArrow step looks like steps in a row with the same foot.
							var numStepsInARow = 2;
							var previousNodeToCheck = PreviousNode;
							while (previousNodeToCheck?.GraphLinkFromPreviousNode != null)
							{
								if (previousNodeToCheck.GraphLinkFromPreviousNode.GraphLink.IsSingleStep(
									    out var previousStepType, out var previousFoot)
								    && previousFoot == footPerformingStep)
								{
									if (previousStepType == StepType.SameArrow)
									{
										numStepsInARow++;
									}
									else
									{
										break;
									}
								}

								if (numStepsInARow > patternConfig.MaxSameArrowsInARowPerFoot)
								{
									break;
								}

								previousNodeToCheck = previousNodeToCheck.PreviousNode;
							}

							if (numStepsInARow > patternConfig.MaxSameArrowsInARowPerFoot)
							{
								totalNumSameArrowStepsInARowPerFootOverMax++;
							}
						}

						break;
					}
				}
			}
		}

		/// <summary>
		/// Updates the cost to use for deviating from desired StepTypes when generating patterns.
		/// </summary>
		/// <param name="patternConfig">PatternConfig to use.</param>
		private void DeterminePatternGenerationStepCost(PatternConfig patternConfig)
		{
			if (patternConfig == null)
			{
				PatternGenerationStepTypeCost = 0.0f;
				return;
			}

			// We only want to update the cost periodically. Updating it every step results in patterns which are too
			// repetitive. If we aren't at a depth where we should be updating the cost, return the previous cost.
			if (patternConfig.StepTypeCheckPeriod > 1 && Depth > 0 && patternConfig.StepTypeCheckPeriod % Depth != 0)
				PatternGenerationStepTypeCost = PreviousNode.PatternGenerationStepTypeCost;

			// We are at a depth where we should calculate a new cost for deviating from the desired distribution.
			UpdatePatternGenerationStepTypeCostFromStepTypeWeights(patternConfig);
		}

		/// <summary>
		/// Allows this SearchNode to perform any final adjustments to cost based on it being the final
		/// note in a pattern.
		/// </summary>
		/// <param name="patternConfig">PatternConfig to use.</param>
		public void SetIsFinalNodeInPattern(PatternConfig patternConfig)
		{
			// Final nodes should always update their cost for deviating from desired StepTypes.
			// This normally runs periodically.
			UpdatePatternGenerationStepTypeCostFromStepTypeWeights(patternConfig);
		}

		/// <summary>
		/// Updates the cost to use for deviating from desired StepTypes when generating patterns by comparing
		/// the current distribution of StepTypes to the desired distribution.
		/// </summary>
		/// <param name="patternConfig">PatternConfig to use.</param>
		private void UpdatePatternGenerationStepTypeCostFromStepTypeWeights(PatternConfig patternConfig)
		{
			PatternGenerationStepTypeCost = 0.0f;

			if (patternConfig == null)
				return;

			var totalSteps = TotalNumSameArrowSteps + TotalNumNewArrowSteps;
			if (totalSteps == 0)
				return;

			var sameArrowNormalized = TotalNumSameArrowSteps / (float)totalSteps;
			PatternGenerationStepTypeCost += Math.Abs(sameArrowNormalized - (float)patternConfig.SameArrowStepWeightNormalized);
			var newArrowNormalized = TotalNumNewArrowSteps / (float)totalSteps;
			PatternGenerationStepTypeCost += Math.Abs(newArrowNormalized - (float)patternConfig.NewArrowStepWeightNormalized);
		}

		/// <summary>
		/// Determines the step travel costs of this SearchNode.
		/// Higher values are worse.
		/// Also updates LastArrowsSteppedOnByFoot, LastTimeFootStepped, and LateralBodyPosition.
		/// Expects that LastArrowsSteppedOnByFoot and LastTimeFootStepped represent values from the
		/// previous SearchNode at the time of calling.
		/// </summary>
		/// <returns>
		/// Value 1: Travel speed cost
		/// Value 2: Travel distance cost
		/// </returns>
		private (double, double) GetStepTravelCostsAndUpdateStepTracking(StepGraph stepGraph, Config.StepTighteningConfig config,
			out bool isStep)
		{
			var speedCost = 0.0;
			var distanceCost = 0.0;
			isStep = false;

			// Determine how the feet step at this SearchNode.
			// While checking each foot, 
			if (PreviousNode?.GraphNode != null)
			{
				var totalFootDistance = 0.0;
				var prevLinks = GraphLinkFromPreviousNode.GraphLink.Links;
				for (var f = 0; f < NumFeet; f++)
				{
					var footSpeedCost = 0.0;
					var steppedWithThisFoot = false;
					for (var p = 0; p < NumFootPortions; p++)
					{
						if (prevLinks[f, p].Valid)
						{
							if (prevLinks[f, p].Action == FootAction.Release
							    || prevLinks[f, p].Action == FootAction.Tap)
							{
								LastTimeFootReleased[f] = Time;
							}

							if (prevLinks[f, p].Action != FootAction.Release)
							{
								steppedWithThisFoot = true;
								isStep = true;
							}
						}
					}

					// Check for updating this SearchNode's individual step cost if the Config is
					// configured to use individual step tightening.
					if (steppedWithThisFoot)
					{
						var time = Time - LastTimeFootStepped[f];

						var distance = GetDistanceUsingMinMovement(stepGraph, GraphNode, f, config);

						totalFootDistance += distance;

						// Determine the speed cost for this foot.
						if (config.IsSpeedTighteningEnabled() && distance >= config.SpeedTighteningMinDistance)
						{
							// Determine the normalized speed penalty
							double speedPenalty;

							// The configured min and max speeds are a range.
							if (config.SpeedMinTimeSeconds < config.SpeedMaxTimeSeconds)
							{
								// Clamp to a normalized value.
								// Invert since lower times represent faster movements, which are worse.
								speedPenalty = Math.Min(1.0, Math.Max(0.0,
									1.0 - (time - config.SpeedMinTimeSeconds)
									/ (config.SpeedMaxTimeSeconds - config.SpeedMinTimeSeconds)));
							}

							// The configured min and max speeds are the same, and are non-zero.
							else
							{
								// If the actual speed is faster than the configured speed then use the full speed penalty
								// of 1.0. Otherwise use no speed penalty of 0.0;
								speedPenalty = time < config.SpeedMinTimeSeconds ? 1.0 : 0.0;
							}

							footSpeedCost = speedPenalty * distance;
						}

						// Update our values for tracking the last steps.
						LastTimeFootStepped[f] = Time;
						for (var p = 0; p < NumFootPortions; p++)
						{
							if (prevLinks[f, p].Valid && prevLinks[f, p].Action != FootAction.Release)
							{
								LastArrowsSteppedOnByFoot[f][p] = GraphNode.State[f, p].Arrow;
							}
							else
							{
								LastArrowsSteppedOnByFoot[f][p] = InvalidArrowIndex;
							}
						}
					}

					speedCost += footSpeedCost;
				}

				// Determine the distance cost for both feet.
				if (config.IsDistanceTighteningEnabled())
				{
					distanceCost = 1.0;
					var distanceRange = config.DistanceMax - config.DistanceMin;
					if (distanceRange > 0.0)
					{
						distanceCost = Math.Min(1.0, Math.Max(0.0,
							(totalFootDistance - config.DistanceMin) / distanceRange));
					}
				}
			}

			return (speedCost, distanceCost);
		}

		/// <summary>
		/// Gets the stretch cost of this SearchNode.
		/// The stretch cost represents how much this node stretches beyond desired limits from
		/// the Config.
		/// </summary>
		/// <returns>Stretch cost.</returns>
		private double GetStretchCost(StepGraph stepGraph, Config.StepTighteningConfig config)
		{
			if (!config.IsStretchTighteningEnabled())
				return 0.0;

			var stretchCost = 0.0;

			// Determine the stretch distance.
			var lIsBracket = stepGraph.GetFootPositionAndIsBracket(GraphNode, L, out var lX, out var lY);
			var rIsBracket = stepGraph.GetFootPositionAndIsBracket(GraphNode, R, out var rX, out var rY);
			var stretchDistance = GetCompensatedDistance(config, lIsBracket, lX, lY, rIsBracket, rX, rY);

			// Determine the cost based on the stretch distance.
			if (stretchDistance >= config.StretchDistanceMin)
			{
				stretchCost = 1.0;
				var stretchRange = config.StretchDistanceMax - config.StretchDistanceMin;
				if (stretchRange > 0.0)
				{
					stretchCost = Math.Min(1.0, Math.Max(0.0,
						(stretchDistance - config.StretchDistanceMin) / stretchRange));
				}
			}

			return stretchCost;
		}

		/// <summary>
		/// Gets the distance for the given foot to move to its position in the given GraphNode taking into
		/// account the given StepTighteningConfig's lateral and longitudinal minimum panel distances. The
		/// returned result is the minimum movement required to move to the new position assuming the foot
		/// moves from the nearest points on the panels.
		/// </summary>
		/// <returns>Minimum distance from previous foot position to new foot position.</returns>
		private double GetDistanceUsingMinMovement(
			StepGraph stepGraph,
			GraphNode node,
			int foot,
			Config.StepTighteningConfig config)
		{
			var previousWasBracket = stepGraph.GetFootPositionAndIsBracket(
				LastArrowsSteppedOnByFoot[foot], out var previousX, out var previousY);
			var currentIsBracket = stepGraph.GetFootPositionAndIsBracket(
				node, foot, out var currentX, out var currentY);
			return GetCompensatedDistance(config, previousWasBracket, previousX, previousY, currentIsBracket, currentX, currentY);
		}

		/// <summary>
		/// Gets the distance required to move between panels taking into account where
		/// the foot may rest on the panels to achieve the minimum distance. Expects two points,
		/// A and B, representing the points whose distance to measure. These may be the
		/// points representing before and after movement for one foot, or one point per foot.
		/// The points are the centered, average points on the panels. For a normal step this
		/// is the center of one panel. For a bracket, it the average of both panels.
		/// </summary>
		/// <param name="config">
		/// StepTighteningConfig from which to get lateral and longitudinal panel distance compensation values.
		/// </param>
		/// <param name="aIsBracket">Whether or not position A is the result of a bracket.</param>
		/// <param name="aX">Position A X value.</param>
		/// <param name="aY">Position A Y value.</param>
		/// <param name="bIsBracket">Whether or not position B is the result of a bracket.</param>
		/// <param name="bX">Position B X value.</param>
		/// <param name="bY">Position B Y value.</param>
		/// <returns>Minimum distance between points A and B.</returns>
		private double GetCompensatedDistance(
			Config.StepTighteningConfig config,
			bool aIsBracket, double aX, double aY,
			bool bIsBracket, double bX, double bY)
		{
			// The maximum amount x positions for non-bracket steps can be moved outward.
			var xComp = ArrowData.HalfPanelWidth - config.LateralMinPanelDistance;
			// The maximum amount y positions for non-bracket steps can be moved outward.
			var yComp = ArrowData.HalfPanelHeight - config.LongitudinalMinPanelDistance;

			var dx = 0.0;
			var dy = 0.0;

			// Neither step is a bracket. Both positions can be shifted from their centers to compensate for
			// minimal movements.
			if (!aIsBracket && !bIsBracket)
			{
				if (bX > aX)
				{
					dx = Math.Max(0.0, bX - xComp - (aX + xComp));
				}
				else if (bX < aX)
				{
					dx = Math.Max(0.0, aX - xComp - (bX + xComp));
				}

				if (bY > aY)
				{
					dy = Math.Max(0.0, bY - yComp - (aY + yComp));
				}
				else if (bY < aY)
				{
					dy = Math.Max(0.0, aY - yComp - (bY + yComp));
				}
			}
			// Only the A was a bracket. B can be shifted to compensate for minimal movements.
			else if (aIsBracket && !bIsBracket)
			{
				if (bX > aX)
				{
					dx = Math.Max(0.0, bX - xComp - aX);
				}
				else if (bX < aX)
				{
					dx = Math.Max(0.0, aX - (bX + xComp));
				}

				if (bY > aY)
				{
					dy = Math.Max(0.0, bY - yComp - aY);
				}
				else if (bY < aY)
				{
					dy = Math.Max(0.0, aY - (bY + yComp));
				}
			}
			// Only the B was a bracket. A can be shifted to compensate for minimal movements.
			else if (!aIsBracket)
			{
				if (bX > aX)
				{
					dx = Math.Max(0.0, bX - (aX + xComp));
				}
				else if (bX < aX)
				{
					dx = Math.Max(0.0, aX - xComp - bX);
				}

				if (bY > aY)
				{
					dy = Math.Max(0.0, bY - (aY + yComp));
				}
				else if (bY < aY)
				{
					dy = Math.Max(0.0, aY - yComp - bY);
				}
			}
			// Both steps are brackets. No compensation should occur.
			else
			{
				dx = bX - aX;
				dy = bY - aY;
			}

			return Math.Sqrt(dx * dx + dy * dy);
		}

		/// <summary>
		/// Determines the transition costs for this SearchNode.
		/// </summary>
		private void UpdateTransitionCost(
			StepGraph stepGraph,
			Config.TransitionConfig config,
			out int earlyTransitionCost,
			out int lateTransitionCost,
			out bool? transitionedLeft,
			ref SearchNode lastTransitionStepNode)
		{
			earlyTransitionCost = 0;
			lateTransitionCost = 0;
			transitionedLeft = null;

			// Early out of transition costs are not enabled.
			if (!config.IsEnabled())
				return;
			if (stepGraph.PadData.GetWidth() < config.MinimumPadWidth)
				return;

			// Determine which side of the pads, if any, the player is on.
			stepGraph.GetSide(GraphNode, config.TransitionCutoffPercentage, out var leftSide);

			// If there is no previous transition node, which may occur at the start of the chart, then
			// consider being on either side of the pads a transition, but do not penalize it with a cost.
			if (leftSide != null && lastTransitionStepNode == null)
			{
				transitionedLeft = leftSide;
				lastTransitionStepNode = this;
				return;
			}

			// When considering transition limits, we want to compare against the total number of steps
			// in the chart, not just within the pattern. We want to prevent short patterns from extending
			// the sequences which don't transition.
			var numStepsSinceLastTransition = TotalSteps - (lastTransitionStepNode?.TotalSteps ?? 0);

			// Transition occurred.
			if (lastTransitionStepNode != null && leftSide != null
			                                   && lastTransitionStepNode.TransitionedLeft != null
			                                   && lastTransitionStepNode.TransitionedLeft.Value != leftSide.Value)
			{
				// Cost for transitioning too early.
				if (numStepsSinceLastTransition < config.StepsPerTransitionMin)
				{
					earlyTransitionCost += config.StepsPerTransitionMin - numStepsSinceLastTransition;
				}

				// Record transition.
				transitionedLeft = leftSide;
				lastTransitionStepNode = this;
			}

			// No transition has occurred.
			else
			{
				// Cost for transitioning too late.
				if (numStepsSinceLastTransition > config.StepsPerTransitionMax)
				{
					lateTransitionCost += numStepsSinceLastTransition - config.StepsPerTransitionMax;
				}
			}
		}

		/// <summary>
		/// Updates the Facing cost of this SearchNode.
		/// </summary>
		private void UpdateFacingCost(
			StepGraph stepGraph,
			Config config,
			bool isRelease,
			out int totalNumInwardSteps,
			out int totalNumOutwardSteps,
			out double totalFacingCost)
		{
			totalNumInwardSteps = PreviousNode?.TotalNumInwardSteps ?? 0;
			if (!isRelease && stepGraph.IsFacingInward(GraphNode, config.Facing.InwardPercentageCutoff))
				totalNumInwardSteps++;
			totalNumOutwardSteps = PreviousNode?.TotalNumOutwardSteps ?? 0;
			if (!isRelease && stepGraph.IsFacingOutward(GraphNode, config.Facing.OutwardPercentageCutoff))
				totalNumOutwardSteps++;
			totalFacingCost = PreviousNode?.TotalFacingCost ?? 0;

			// When considering facing limits, we only want to limit within the range of the pattern.
			if (config.Facing.MaxInwardPercentage < 1.0)
			{
				if (totalNumInwardSteps / (double)TotalStepsInPattern > config.Facing.MaxInwardPercentage)
					totalFacingCost++;
			}

			if (config.Facing.MaxOutwardPercentage < 1.0)
			{
				if (totalNumOutwardSteps / (double)TotalStepsInPattern > config.Facing.MaxOutwardPercentage)
					totalFacingCost++;
			}
		}

		/// <summary>
		/// Gets the distribution cost of this SearchNode.
		/// The distribution costs represents how far off the chart up to this point does not match
		/// the desired arrow weights from the Config.
		/// </summary>
		/// <returns>Distribution cost.</returns>
		private float GetDistributionCost(StepGraph stepGraph, Config config)
		{
			var distributionCost = 0.0f;
			var weights = config.GetArrowWeightsNormalized(stepGraph.PadData.StepsType);
			var totalSteps = 0;
			for (var a = 0; a < StepCounts.Length; a++)
				totalSteps += StepCounts[a];
			if (totalSteps > 0)
			{
				var totalDifferenceFromDesiredLanePercentage = 0.0f;
				for (var a = 0; a < StepCounts.Length; a++)
					totalDifferenceFromDesiredLanePercentage +=
						Math.Abs((float)StepCounts[a] / totalSteps - (float)weights[a]);
				distributionCost = totalDifferenceFromDesiredLanePercentage / StepCounts.Length;
			}

			return distributionCost;
		}

		/// <summary>
		/// Updates tracking variables to support lateral movement cost calculation.
		/// </summary>
		private void UpdateLateralTracking(
			bool isStep,
			ref int lateralMovementNumSteps,
			ref double lastLateralMovementStartTime,
			ref float lastLateralMovementStartPosition,
			ref LateralMovementDirection movementDir)
		{
			// Ignore releases.
			if (!isStep)
			{
				if (PreviousNode != null)
				{
					lateralMovementNumSteps = PreviousNode.LateralMovementNumSteps;
					lastLateralMovementStartTime = PreviousNode.LastLateralMovementStartTime;
					lastLateralMovementStartPosition = PreviousNode.LastLateralMovementStartPosition;
					movementDir = PreviousNode.MovementDir;
				}

				return;
			}

			lateralMovementNumSteps = 0;
			if (PreviousNode != null)
			{
				lastLateralMovementStartTime = PreviousNode.LastLateralMovementStartTime;
				lastLateralMovementStartPosition = PreviousNode.LastLateralMovementStartPosition;

				var d = LateralBodyPosition - PreviousNode.LateralBodyPosition;
				if (Math.Abs(d) > 0.0001)
				{
					movementDir = d < 0 ? LateralMovementDirection.Left : LateralMovementDirection.Right;
					if (PreviousNode.MovementDir != MovementDir)
					{
						lastLateralMovementStartTime = Time;
						lastLateralMovementStartPosition = LateralBodyPosition;
						lateralMovementNumSteps = 1;
					}
					else
					{
						lateralMovementNumSteps = PreviousNode.LateralMovementNumSteps + 1;
					}
				}
			}
		}

		/// <summary>
		/// Gets the lateral movement cost of this SearchNode.
		/// </summary>
		private double GetLateralMovementCost(Config.LateralTighteningConfig config, double averageNps)
		{
			if (!config.IsEnabled())
				return 0.0;

			var lateralMovementSpeedCost = 0.0;

			var t = Time - LastLateralMovementStartTime;
			if (MovementDir != LateralMovementDirection.None && t > 0.0)
			{
				var nps = (LateralMovementNumSteps - 1) / t;
				var speed = Math.Abs(LateralBodyPosition - LastLateralMovementStartPosition) / t;
				if (((averageNps > 0.0 && nps > averageNps * config.RelativeNPS) || nps > config.AbsoluteNPS)
				    && speed > config.Speed)
				{
					lateralMovementSpeedCost = speed - config.Speed;
				}
			}

			return lateralMovementSpeedCost;
		}

		/// <summary>
		/// Gets the lateral position of the body for this SearchNode.
		/// </summary>
		/// <param name="stepGraph">StepGraph for the PerformedChart.</param>
		/// <returns>Lateral position of the body for this SearchNode.</returns>
		private float GetLateralBodyPosition(StepGraph stepGraph)
		{
			var numPoints = 0;
			var x = 0.0f;
			for (var f = 0; f < NumFeet; f++)
			{
				for (var p = 0; p < NumFootPortions; p++)
				{
					if (LastArrowsSteppedOnByFoot[f][p] == InvalidArrowIndex)
						continue;

					numPoints++;
					x += stepGraph.PadData.ArrowData[LastArrowsSteppedOnByFoot[f][p]].X;
				}
			}

			if (numPoints > 0)
				x /= numPoints;

			return x;
		}

		/// <summary>
		/// Determines whether this SearchNode represents an ambiguous or a misleading step.
		/// </summary>
		/// <param name="stepGraph">StepGraph for the PerformedChart.</param>
		/// <returns>
		/// Tuple of values for representing an ambiguous or misleading step.
		/// Value 1: True if this step is ambiguous and false otherwise.
		/// Value 2: True if this step is misleading and false otherwise.
		/// </returns>
		private (bool, bool) DetermineAmbiguity(StepGraph stepGraph)
		{
			// Technically the first step can be ambiguous
			if (GraphLinkFromPreviousNode == null)
				return (false, false);
			if (IsBlank())
				return (false, false);

			var prevLinks = GraphLinkFromPreviousNode.GraphLink.Links;

			// Perform early outs while determining some information about this step.
			var isJump = true;
			var involvesSameArrowStep = false;
			var involvesNewArrowStep = false;
			for (var f = 0; f < NumFeet; f++)
			{
				for (var p = 0; p < NumFootPortions; p++)
				{
					if (p == DefaultFootPortion)
					{
						// We only care about single steps and jumps, not releases.
						if (prevLinks[f, p].Action == FootAction.Release)
							return (false, false);
						if (!prevLinks[f, p].Valid)
							isJump = false;
						else if (prevLinks[f, p].Step != StepType.NewArrow && prevLinks[f, p].Step != StepType.SameArrow)
							return (false, false);
						if (prevLinks[f, p].Valid)
						{
							involvesSameArrowStep |= prevLinks[f, p].Step == StepType.SameArrow;
							involvesNewArrowStep |= prevLinks[f, p].Step == StepType.NewArrow;
						}
					}
					// We only care about single steps and jumps, not brackets.
					else if (prevLinks[f, p].Valid)
					{
						return (false, false);
					}
				}
			}

			// Some jumps involving new arrows can be misleading.
			if (involvesSameArrowStep)
			{
				// Steps on the same arrow are not ambiguous.
				if (!isJump)
					return (false, false);

				// Jumps with same arrow.
				var matchesPreviousArrows = DoNonInstanceActionsMatch(Actions, PreviousNode.Actions);
				var followsBracket = PreviousNode.GraphLinkFromPreviousNode?.GraphLink?.IsBracketStep() ?? false;

				// Any jump involving a new arrow that matches the previous arrows is misleading.
				// Any same arrow same arrow jump following a bracket is misleading.
				if (matchesPreviousArrows && (involvesNewArrowStep || followsBracket))
					return (false, true);

				// Failing the above checks, jumps with same arrow steps are not ambiguous.
				return (false, false);
			}

			// At this point we are either dealing with a NewArrow step or a NewArrow NewArrow jump.

			// For ambiguity for a step, the step must follow a jump with a release at the same time
			// with no mines to indicate footing. The step must also be bracketable from both feet.
			if (!isJump)
			{
				// Use the previous node since this node has already updated the LastTimeFootReleased for its step.
				// If the feet were not released at the same time, we did not come from a jump, meaning this step
				// is not ambiguous
				if (!PreviousNode.LastTimeFootReleased[L].DoubleEquals(PreviousNode.LastTimeFootReleased[R]))
					return (false, false);

				// TODO: Mines

				// Determine which arrow is being stepped on so we can perform bracket checks.
				var arrowBeingSteppedOn = InvalidArrowIndex;
				for (var a = 0; a < Actions.Length; a++)
				{
					if (Actions[a] == PerformanceFootAction.Tap || Actions[a] == PerformanceFootAction.Hold)
					{
						arrowBeingSteppedOn = a;
						break;
					}
				}

				// Determine if each foot is bracketable with the arrow being stepped on.
				var leftBracketable = false;
				var rightBracketable = false;
				for (var p = 0; p < NumFootPortions; p++)
				{
					if (!leftBracketable && PreviousNode.LastArrowsSteppedOnByFoot[L][p] != InvalidArrowIndex)
					{
						var leftFrom = PreviousNode.LastArrowsSteppedOnByFoot[L][p];
						leftBracketable =
							stepGraph.PadData.ArrowData[arrowBeingSteppedOn].BracketablePairingsHeel[L][leftFrom]
							|| stepGraph.PadData.ArrowData[arrowBeingSteppedOn].BracketablePairingsToe[L][leftFrom];
					}

					if (!rightBracketable && PreviousNode.LastArrowsSteppedOnByFoot[R][p] != InvalidArrowIndex)
					{
						var rightFrom = PreviousNode.LastArrowsSteppedOnByFoot[R][p];
						rightBracketable =
							stepGraph.PadData.ArrowData[arrowBeingSteppedOn].BracketablePairingsHeel[R][rightFrom]
							|| stepGraph.PadData.ArrowData[arrowBeingSteppedOn].BracketablePairingsToe[R][rightFrom];
					}
				}

				// If one foot can bracket to this arrow and the other foot cannot, it is not ambiguous.
				if (leftBracketable != rightBracketable)
					return (false, false);
			}

			// For ambiguity there must be a GraphLink that is the same as the GraphLink
			// to this node, but the feet are flipped, which results in a different GraphNode
			// but generates the same arrows.

			// Generate a flipped GraphLink.
			var otherGraphLink = new GraphLink();
			for (var f = 0; f < NumFeet; f++)
				for (var p = 0; p < NumFootPortions; p++)
					if (p == DefaultFootPortion)
						otherGraphLink.Links[OtherFoot(f), p] = prevLinks[f, p];

			// For ambiguity the arrows generated from the steps from both nodes must be the same.
			var ambiguous = DoesAnySiblingNodeFromLinkMatchActions(otherGraphLink, stepGraph);

			// Determine if this step is misleading.
			// If this is a NewArrow NewArrow jump and there is a NewArrow SameArrow,
			// or even a SameArrow SameArrow jump that results in a matching GraphNode then
			// this step is misleading as SameArrow steps are what a player would do.
			var misleading = false;
			if (isJump)
			{
				// Assuming taps here for simplicity since for footing we just care about stepping at all.
				// DoesAnySiblingNodeFromLinkMatchActions will treat Steps and Holds equally as steps.

				// Check left foot SameArrow right foot NewArrow.
				var leftSameLink = new GraphLink
				{
					Links =
					{
						[L, DefaultFootPortion] = new GraphLink.FootArrowState(StepType.SameArrow, FootAction.Tap),
						[R, DefaultFootPortion] = new GraphLink.FootArrowState(StepType.NewArrow, FootAction.Tap),
					},
				};
				misleading = DoesAnySiblingNodeFromLinkMatchActions(leftSameLink, stepGraph);
				if (misleading)
					return (ambiguous, true);

				// Check right foot SameArrow left foot NewArrow.
				var rightSameLink = new GraphLink
				{
					Links =
					{
						[L, DefaultFootPortion] = new GraphLink.FootArrowState(StepType.NewArrow, FootAction.Tap),
						[R, DefaultFootPortion] = new GraphLink.FootArrowState(StepType.SameArrow, FootAction.Tap),
					},
				};
				misleading = DoesAnySiblingNodeFromLinkMatchActions(rightSameLink, stepGraph);
				if (misleading)
					return (ambiguous, true);

				// Check SameArrow SameArrow.
				var bothSameLink = new GraphLink
				{
					Links =
					{
						[L, DefaultFootPortion] = new GraphLink.FootArrowState(StepType.SameArrow, FootAction.Tap),
						[R, DefaultFootPortion] = new GraphLink.FootArrowState(StepType.SameArrow, FootAction.Tap),
					},
				};
				misleading = DoesAnySiblingNodeFromLinkMatchActions(bothSameLink, stepGraph);
			}

			return (ambiguous, misleading);
		}

		/// <summary>
		/// Determines if any sibling GraphNode to this SearchNode's GraphNode, reachable
		/// from this SearchNode's parent by the given GraphLink represents the same set
		/// of PerformanceFootActions. If it does match, then it means that this SearchNode
		/// represents an ambiguous or misleading step.
		/// </summary>
		/// <param name="siblingLink">GraphLink from the parent SearchNode to the sibling.</param>
		/// <param name="stepGraph">StepGraph for this PerformedChart.</param>
		/// <returns>
		/// Whether there exists a sibling GraphNode matching the actions of this SearchNode's GraphNode.
		/// </returns>
		private bool DoesAnySiblingNodeFromLinkMatchActions(GraphLink siblingLink, StepGraph stepGraph)
		{
			// If this link isn't a valid from the parent node, then no node from it will match.
			if (!PreviousNode.GraphNode.Links.ContainsKey(siblingLink))
				return false;

			// Check all sibling nodes for the link.
			var otherNodes = PreviousNode.GraphNode.Links[siblingLink];
			for (var n = 0; n < otherNodes.Count; n++)
			{
				var otherNode = otherNodes[n];
				// Skip this node if it is the same GraphNode from this SearchNode (not a sibling).
				if (otherNode.EqualsFooting(GraphNode))
					continue;

				// Check if the PerformanceFootActions from the sibling match this SearchNode's Actions.
				var otherActions = GetActionsForNode(otherNode, siblingLink, stepGraph.NumArrows);
				if (DoNonInstanceActionsMatch(Actions, otherActions))
					return true;
			}

			return false;
		}

		private static bool DoNonInstanceActionsMatch(PerformanceFootAction[] actions, PerformanceFootAction[] otherActions)
		{
			if (actions.Length != otherActions.Length)
				return false;
			for (var a = 0; a < actions.Length; a++)
			{
				if (IsStep(actions[a]) != IsStep(otherActions[a]))
				{
					return false;
				}
			}

			return true;
		}

		#region IComparable Implementation

		public int CompareTo(SearchNode other)
		{
			// First consider how much this path has needed to fallback to less preferred steps.
			if (Math.Abs(TotalFallbackStepCost - other.TotalFallbackStepCost) > 0.00001)
				return TotalFallbackStepCost < other.TotalFallbackStepCost ? -1 : 1;

			// Next consider misleading steps. These are steps which a player would
			// never interpret as intended.
			if (MisleadingStepCount != other.MisleadingStepCount)
				return MisleadingStepCount < other.MisleadingStepCount ? -1 : 1;

			// Next consider consider ambiguous steps. These are steps which the player
			// would recognize as having multiple options could result in the wrong footing.
			if (AmbiguousStepCount != other.AmbiguousStepCount)
				return AmbiguousStepCount < other.AmbiguousStepCount ? -1 : 1;

			// Pattern generation logic.
			// Prefer patterns which don't exceed the maximum specified number of same arrow steps in a row.
			if (TotalNumSameArrowStepsInARowPerFootOverMax != other.TotalNumSameArrowStepsInARowPerFootOverMax)
				return TotalNumSameArrowStepsInARowPerFootOverMax < other.TotalNumSameArrowStepsInARowPerFootOverMax ? -1 : 1;

			// Next consider total stretch cost. These are steps which stretch beyond desired amounts.
			if (Math.Abs(TotalStretchCost - other.TotalStretchCost) > 0.00001)
				return TotalStretchCost < other.TotalStretchCost ? -1 : 1;

			// Next consider the facing cost. These are steps which face the player in orientations
			// which are not desired.
			if (Math.Abs(TotalFacingCost - other.TotalFacingCost) > 0.00001)
				return TotalFacingCost < other.TotalFacingCost ? -1 : 1;

			// Next consider individual step travel distance cost. This is a measure of how much distance
			// individual quick steps move.
			if (Math.Abs(TotalIndividualStepTravelDistanceCost - other.TotalIndividualStepTravelDistanceCost) > 0.00001)
				return TotalIndividualStepTravelDistanceCost < other.TotalIndividualStepTravelDistanceCost ? -1 : 1;

			// Next consider individual step travel speed cost. This is a measure of how fast individual
			// quick steps move.
			if (Math.Abs(TotalIndividualStepTravelSpeedCost - other.TotalIndividualStepTravelSpeedCost) > 0.00001)
				return TotalIndividualStepTravelSpeedCost < other.TotalIndividualStepTravelSpeedCost ? -1 : 1;

			// Pattern generation logic.
			// Next consider the cost associated with the StepType distribution of generated patterns.
			if (Math.Abs(PatternGenerationStepTypeCost - other.PatternGenerationStepTypeCost) > 0.00001)
				return PatternGenerationStepTypeCost < other.PatternGenerationStepTypeCost ? -1 : 1;

			// Next consider lateral movement speed. We want to avoid moving on bursts.
			if (Math.Abs(TotalLateralMovementSpeedCost - other.TotalLateralMovementSpeedCost) > 0.00001)
				return TotalLateralMovementSpeedCost < other.TotalLateralMovementSpeedCost ? -1 : 1;

			// Next consider transitioning too early. For stamina charts it is more important
			// to penalize quick transitions than slow transitions.
			if (TotalEarlyTransitionCost != other.TotalEarlyTransitionCost)
				return TotalEarlyTransitionCost - other.TotalEarlyTransitionCost;

			// Next consider transitioning too late.
			if (TotalLateTransitionCost != other.TotalLateTransitionCost)
				return TotalLateTransitionCost - other.TotalLateTransitionCost;

			// Lastly try to match a good distribution. This cost will result in transitions.
			if (Math.Abs(TotalDistributionCost - other.TotalDistributionCost) > 0.00001)
				return TotalDistributionCost < other.TotalDistributionCost ? -1 : 1;

			// Finally, use a random weight. This is helpful to break up repetitive steps.
			// For example breaking up L U D R into L D U R as well.
			return RandomWeight.CompareTo(other.RandomWeight);
		}

		#endregion IComparable Implementation

		#region IEquatable Implementation

		public override bool Equals(object obj)
		{
			if (obj == null)
				return false;
			if (obj is SearchNode n)
				return Equals(n);
			return false;
		}

		public bool Equals(SearchNode other)
		{
			if (other == null)
				return false;
			return Id == other.Id;
		}

		public override int GetHashCode()
		{
			return (int)Id;
		}

		#endregion IEquatable Implementation
	}
}
