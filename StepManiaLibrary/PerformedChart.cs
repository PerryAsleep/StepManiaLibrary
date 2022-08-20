﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Fumen;
using Fumen.ChartDefinition;
using Fumen.Converters;
using static StepManiaLibrary.Constants;
using static Fumen.Utils;

namespace StepManiaLibrary
{
	/// <summary>
	/// A PerformedChart is a series of events which describe how a Chart is played.
	/// This includes specifics about which feet hit which arrows in what ways.
	/// An ExpressedChart can be turned into a PerformedChart for a StepGraph.
	/// A PerformedChart can be used to generate an SM Chart.
	/// A PerformedChart's representation comes from GraphLinkInstances and
	/// GraphNodeInstances.
	/// TODO: Consider consolidating search logic with ExpressedChart?
	/// </summary>
	public class PerformedChart
	{
		private const string LogTag = "Performed Chart";

		/// <summary>
		/// Enumeration of states each arrow can be in at each position.
		/// Used to assist with translating a PerformedChart into an SM Chart.
		/// </summary>
		private enum PerformanceFootAction
		{
			None,
			Tap,
			Fake,
			Lift,
			Hold,
			Roll,
			Release
		}

		/// <summary>
		/// A PerformedChart contains a series of PerformanceNodes.
		/// Abstract base class for the various types of PerformanceNodes in a PerformedChart.
		/// </summary>
		public abstract class PerformanceNode
		{
			/// <summary>
			/// IntegerPosition of this node in the Chart.
			/// </summary>
			public int Position;

			/// <summary>
			/// Next PerformanceNode in the series.
			/// </summary>
			public PerformanceNode Next;

			/// <summary>
			/// Previous PerformanceNode in the series.
			/// </summary>
			public PerformanceNode Prev;
		}

		/// <summary>
		/// PerformanceNode representing a normal step or release.
		/// </summary>
		public class StepPerformanceNode : PerformanceNode, MineUtils.IChartNode
		{
			/// <summary>
			/// GraphNodeInstance representing the state at this PerformanceNode.
			/// </summary>
			public GraphNodeInstance GraphNodeInstance;

			/// <summary>
			/// GraphLinkInstance to the GraphNodeInstance at this PerformanceNode.
			/// </summary>
			public GraphLinkInstance GraphLinkInstance;

			#region MineUtils.IChartNode Implementation

			public GraphNode GetGraphNode()
			{
				return GraphNodeInstance?.Node;
			}

			public GraphLink GetGraphLinkToNode()
			{
				return GraphLinkInstance?.GraphLink;
			}

			public int GetPosition()
			{
				return Position;
			}

			#endregion
		}

		/// <summary>
		/// PerformanceNode representing a mine.
		/// </summary>
		public class MinePerformanceNode : PerformanceNode
		{
			/// <summary>
			/// The lane or arrow this Mine occurs on.
			/// </summary>
			public int Arrow;
		}

		/// <summary>
		/// Search node for performing a search through an ExpressedChart to find the best PerformedChart.
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
			public readonly GraphLink GraphLinkFromPreviousNode;

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
			public readonly List<GraphLink> GraphLinks;

			/// <summary>
			/// All the valid NextNodes out of this SearchNode.
			/// These are added during the search and pruned so at the end there is at most one next SearchNode.
			/// </summary>
			public readonly Dictionary<GraphLink, HashSet<SearchNode>> NextNodes =
				new Dictionary<GraphLink, HashSet<SearchNode>>();

			/// <summary>
			/// This SearchNode's cost for using unwanted individual steps.
			/// Higher values are worse.
			/// </summary>
			private readonly double TotalIndividualStepCost;

			private readonly double IndividualStepCost;

			/// <summary>
			/// This SearchNode's cost for using unwanted faster lateral movement.
			/// Higher values are worse.
			/// </summary>
			private readonly double TotalLateralMovementSpeedCost;

			/// <summary>
			/// This SearchNode's cost for deviating from the configured DesiredArrowWeights.
			/// Higher values are worse.
			/// </summary>
			private readonly double DistributionCost;

			private readonly double SectionStepTypeCost;

			private readonly int TotalNumSameArrowSteps;
			private readonly int TotalNumSameArrowStepsInARowPerFootOverMax;
			private readonly int TotalNumNewArrowSteps;
			private readonly int TotalNumBracketableNewArrowSteps;


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
			/// Whether or not this SearchNode represents a step with either foot.
			/// </summary>
			private bool Stepped;

			/// <summary>
			/// The time in microseconds of the Events represented by this SearchNode.
			/// </summary>
			private readonly long TimeMicros;

			/// <summary>
			/// Lateral position of the body on the pads at this SearchNode.
			/// Units are in arrows.
			/// </summary>
			private double LateralBodyPosition;

			/// <summary>
			/// For each foot, the last time in microseconds that it was stepped on.
			/// During construction, these values will be updated to this SearchNode's TimeMicros
			/// if this SearchNode represents steps on any arrows.
			/// </summary>
			private readonly long[] LastTimeFootStepped;

			/// <summary>
			/// For each foot, the last time in microseconds that it was released.
			/// During construction, these values will be updated to this SearchNode's TimeMicros
			/// if this SearchNode represents releases on any arrows.
			/// </summary>
			private readonly long[] LastTimeFootReleased;

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
			/// Constructor.
			/// </summary>
			/// <param name="graphNode">
			/// GraphNode representing the state of this SearchNode.
			/// </param>
			/// <param name="originalGraphLinkToNextNode">
			/// The original GraphLink to the next GraphNode.
			/// This will be used to determine which replacement GraphLinks are acceptable from
			/// this SearchNode.
			/// </param>
			/// <param name="graphLinkFromPreviousNode">
			/// The GraphLink to this SearchNode from the previous SearchNode.
			/// </param>
			/// <param name="timeMicros">
			/// Time of the corresponding ExpressedChart event in microseconds.
			/// </param>
			/// <param name="depth"> The 0-based depth of this SearchNode. </param>
			/// <param name="previousNode"> The previous SearchNode. </param>
			/// <param name="actions">
			/// For each arrow, the PerformanceFootAction to take for this SearchNode.
			/// </param>
			/// <param name="stepGraph">StepGraph for the PerformedChart.</param>
			/// <param name="nps">Average notes per second of the Chart.</param>
			/// <param name="randomWeight">
			/// Random weight to use as a fallback for comparing SearchNodes with equal costs.
			/// </param>
			public SearchNode(
				GraphNode graphNode,
				List<GraphLink> possibleGraphLinksToNextNode,
				GraphLink graphLinkFromPreviousNode,
				long timeMicros,
				int depth,
				SearchNode previousNode,
				PerformanceFootAction[] actions,
				StepGraph stepGraph,
				double nps,
				double randomWeight,
				PerformedChartConfig config,
				FillConfig fillSectionConfig)
			{
				Id = Interlocked.Increment(ref IdCounter);
				GraphNode = graphNode;
				GraphLinks = possibleGraphLinksToNextNode;
				GraphLinkFromPreviousNode = graphLinkFromPreviousNode;
				Depth = depth;
				PreviousNode = previousNode;
				TimeMicros = timeMicros;
				RandomWeight = randomWeight;
				Stepped = false;
				Actions = actions;

				// Copy the previous SearchNode's ambiguous and misleading step counts.
				// We will update them later after determining if this SearchNode represents
				// an ambiguous or misleading step.
				AmbiguousStepCount = previousNode?.AmbiguousStepCount ?? 0;
				MisleadingStepCount = previousNode?.MisleadingStepCount ?? 0;

				// Copy the previous SearchNode's StepCounts and update them.
				StepCounts = new int[Actions.Length];
				for (var a = 0; a < Actions.Length; a++)
				{
					StepCounts[a] = (previousNode?.StepCounts[a] ?? 0)
					                + (Actions[a] == PerformanceFootAction.Tap || Actions[a] == PerformanceFootAction.Hold ? 1 : 0);
				}

				// Copy the previous SearchNode's last step times to this nodes last step times.
				// We will update them later if this SearchNode represents a step.
				LastTimeFootStepped = new long[NumFeet];
				for (var f = 0; f < NumFeet; f++)
					LastTimeFootStepped[f] = previousNode?.LastTimeFootStepped[f] ?? 0L;
				LastTimeFootReleased = new long[NumFeet];
				for (var f = 0; f < NumFeet; f++)
					LastTimeFootReleased[f] = previousNode?.LastTimeFootReleased[f] ?? 0L;

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

				double lateralMovementSpeedCost;
				(DistributionCost, IndividualStepCost, lateralMovementSpeedCost) =
					DetermineCostsAndUpdateStepTracking(stepGraph, nps, config);

				TotalIndividualStepCost = (PreviousNode?.TotalIndividualStepCost ?? 0.0) + IndividualStepCost;
				TotalLateralMovementSpeedCost = (PreviousNode?.TotalLateralMovementSpeedCost ?? 0.0) + lateralMovementSpeedCost;

				TotalNumSameArrowSteps = PreviousNode?.TotalNumSameArrowSteps ?? 0;
				TotalNumSameArrowStepsInARowPerFootOverMax = PreviousNode?.TotalNumSameArrowStepsInARowPerFootOverMax ?? 0;
				TotalNumNewArrowSteps = PreviousNode?.TotalNumNewArrowSteps ?? 0;
				TotalNumBracketableNewArrowSteps = PreviousNode?.TotalNumBracketableNewArrowSteps ?? 0;
				if (graphLinkFromPreviousNode != null)
				{
					if (graphLinkFromPreviousNode.IsSingleStep(out var stepType, out var footPerformingStep))
					{
						switch (stepType)
						{
							case StepType.NewArrow:
							{
								TotalNumNewArrowSteps++;

								if (PreviousNode != null)
								{
									var steppedFromArrow =
										PreviousNode.LastArrowsSteppedOnByFoot[footPerformingStep][DefaultFootPortion];
									var steppedToArrow = GraphNode.State[footPerformingStep, DefaultFootPortion].Arrow;
									if (steppedFromArrow != InvalidArrowIndex && steppedToArrow != InvalidArrowIndex)
									{
										for (var f = 0; f < NumFeet; f++)
										{
											if (stepGraph.PadData.ArrowData[steppedFromArrow]
												    .BracketablePairingsOtherHeel[f][steppedToArrow]
											    || stepGraph.PadData.ArrowData[steppedFromArrow]
												    .BracketablePairingsOtherToe[f][steppedToArrow])
											{
												TotalNumBracketableNewArrowSteps++;
												break;
											}
										}
									}
								}
								break;
							}
							case StepType.SameArrow:
								TotalNumSameArrowSteps++;
								if (fillSectionConfig != null && fillSectionConfig.MaxSameArrowsInARowPerFoot > 0)
								{
									// Two because the previous step was the same arrow.
									// One SameArrow step looks like steps in a row with the same foot.
									var numStepsInARow = 2;
									var previousNodeToCheck = PreviousNode;
									while (previousNodeToCheck != null && previousNodeToCheck.GraphLinkFromPreviousNode != null)
									{
										if (previousNodeToCheck.GraphLinkFromPreviousNode.IsSingleStep(out var previousStepType, out var previousFoot)
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

										if (numStepsInARow > fillSectionConfig.MaxSameArrowsInARowPerFoot)
										{
											break;
										}
										previousNodeToCheck = previousNodeToCheck.PreviousNode;
									}

									if (numStepsInARow > fillSectionConfig.MaxSameArrowsInARowPerFoot)
									{
										TotalNumSameArrowStepsInARowPerFootOverMax++;
									}
								}
								break;
						}
					}
				}

				SectionStepTypeCost = DetermineSectionStepCost(fillSectionConfig);

				var (ambiguous, misleading) = DetermineAmbiguity(stepGraph);
				if (ambiguous)
					AmbiguousStepCount++;
				if (misleading)
					MisleadingStepCount++;
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

			private double DetermineSectionStepCost(FillConfig config)
			{
				if (config == null)
					return 0.0;

				var totalSteps = TotalNumSameArrowSteps + TotalNumNewArrowSteps;
				if (totalSteps == 0)
					return 0.0;

				var cost = 0.0;

				var sameArrowNormalized = TotalNumSameArrowSteps / (double)totalSteps;
				cost += Math.Abs(sameArrowNormalized - config.SameArrowStepWeightNormalized);
				var newArrowNormalized = TotalNumNewArrowSteps / (double)totalSteps;
				cost += Math.Abs(newArrowNormalized - config.NewArrowStepWeightNormalized);

				if (TotalNumNewArrowSteps > 0)
				{
					var bracketableNormalized = TotalNumBracketableNewArrowSteps / TotalNumNewArrowSteps;
					cost += Math.Abs(bracketableNormalized - config.NewArrowBracketableWeightNormalized);
				}

				return cost;
			}

			/// <summary>
			/// Determines the costs of this SearchNode.
			/// The three costs returned are the distribution cost, the individual step cost, and the
			/// lateral movement speed cost.
			/// Higher values are worse.
			/// Also updates LastArrowsSteppedOnByFoot, LastTimeFootStepped, and LateralBodyPosition.
			/// Expects that LastArrowsSteppedOnByFoot and LastTimeFootStepped represent values from the
			/// previous SearchNode at the time of calling.
			/// </summary>
			/// <param name="averageNps">Average notes per second of the Chart.</param>
			/// <param name="stepGraph">StepGraph for the PerformedChart.</param>
			/// <returns>
			/// Costs to use for this SearchNode.
			/// Value 1: distribution cost
			/// Value 2: individual step cost
			/// Value 3: lateral movement speed cost
			/// </returns>
			private (double, double, double) DetermineCostsAndUpdateStepTracking(
				StepGraph stepGraph,
				double averageNps,
				PerformedChartConfig config)
			{
				// Record the previous step time.
				// This is currently stored in LastTimeFootStepped from initialization.
				// We will update LastTimeFootStepped for this step later.
				var previousStepTime = 0L;
				for (var f = 0; f < NumFeet; f++)
				{
					if (LastTimeFootStepped[f] > previousStepTime)
						previousStepTime = LastTimeFootStepped[f];
				}

				// Determine distribution cost by calculating how far off the chart up to this point
				// is from the desired distribution of arrows.
				var distributionCost = 0.0;
				var weights = config.GetDesiredArrowWeightsNormalized(stepGraph.PadData.StepsType);
				var totalSteps = 0;
				for (var a = 0; a < StepCounts.Length; a++)
					totalSteps += StepCounts[a];
				if (totalSteps > 0)
				{
					var totalDifferenceFromDesiredLanePercentage = 0.0;
					for (var a = 0; a < StepCounts.Length; a++)
						totalDifferenceFromDesiredLanePercentage += Math.Abs((double) StepCounts[a] / totalSteps - weights[a]);
					distributionCost = totalDifferenceFromDesiredLanePercentage / StepCounts.Length;
				}

				// Determine how the feet step at this SearchNode.
				// While checking each foot, 
				var individualStepCost = 0.0;
				if (GraphLinkFromPreviousNode != null)
				{
					for (var f = 0; f < NumFeet; f++)
					{
						var steppedWithThisFoot = false;
						var bracketedWithThisFoot = false;
						var steppedWithOtherFoot = false;
						var thisFootPortionSteppingWith = InvalidFootPortion;
						var otherFoot = OtherFoot(f);
						for (var p = 0; p < NumFootPortions; p++)
						{
							if (GraphLinkFromPreviousNode.Links[f, p].Valid)
							{
								if (GraphLinkFromPreviousNode.Links[f, p].Action == FootAction.Release
								    || GraphLinkFromPreviousNode.Links[f, p].Action == FootAction.Tap)
								{
									LastTimeFootReleased[f] = TimeMicros;
								}

								if (GraphLinkFromPreviousNode.Links[f, p].Action != FootAction.Release)
								{
									bracketedWithThisFoot = steppedWithThisFoot;
									steppedWithThisFoot = true;
									Stepped = true;
									thisFootPortionSteppingWith = p;
								}
							}

							if (GraphLinkFromPreviousNode.Links[otherFoot, p].Valid)
								steppedWithOtherFoot = true;
						}

						// Check for updating this SearchNode's individual step cost if the Config is
						// configured to use individual step tightening.
						if (steppedWithThisFoot
						    && !bracketedWithThisFoot
						    && !steppedWithOtherFoot
						    && config.IndividualStepTighteningMinTimeSeconds > 0.0)
						{
							var arrowBeingSteppedTo = GraphNode.State[f, thisFootPortionSteppingWith].Arrow;
							var timeBetweenStepsSeconds = ToSeconds(TimeMicros - LastTimeFootStepped[f]);

							for (var p = 0; p < NumFootPortions; p++)
							{
								if (LastArrowsSteppedOnByFoot[f][p] == InvalidArrowIndex)
									continue;

								var arrowBeingSteppedFrom = LastArrowsSteppedOnByFoot[f][p];
								var travelDistance = stepGraph.PadData.ArrowData[arrowBeingSteppedTo]
									.TravelDistanceWithArrow[arrowBeingSteppedFrom];

								// Determine the normalized speed penalty
								double speedPenalty;

								// The configure min and max speeds are a range.
								if (config.IndividualStepTighteningMinTimeSeconds <
								    config.IndividualStepTighteningMaxTimeSeconds)
								{
									// Clamp to a normalized value.
									// Invert since lower times represent faster movements, which are worse.
									speedPenalty = Math.Min(1.0, Math.Max(0.0,
										1.0 - (timeBetweenStepsSeconds - config.IndividualStepTighteningMinTimeSeconds) 
										/ (config.IndividualStepTighteningMaxTimeSeconds - config.IndividualStepTighteningMinTimeSeconds)));
								}

								// The configured min and max speeds are the same, and are non-zero.
								else
								{
									// If the actual speed is faster than the configured speed then use the full speed penalty
									// of 1.0. Otherwise use no speed penalty of 0.0;
									speedPenalty = timeBetweenStepsSeconds < config.IndividualStepTighteningMinTimeSeconds ? 1.0 : 0.0;
								}

								individualStepCost = Math.Max(individualStepCost, speedPenalty * travelDistance);
							}
						}

						// Update our values for tracking the last steps.
						if (steppedWithThisFoot)
						{
							LastTimeFootStepped[f] = TimeMicros;
							for (var p = 0; p < NumFootPortions; p++)
							{
								if (GraphLinkFromPreviousNode.Links[f, p].Valid &&
								    GraphLinkFromPreviousNode.Links[f, p].Action != FootAction.Release)
								{
									LastArrowsSteppedOnByFoot[f][p] = GraphNode.State[f, p].Arrow;
								}
								else
								{
									LastArrowsSteppedOnByFoot[f][p] = InvalidArrowIndex;
								}
							}
						}
					}
				}

				// Now that we have updated the LastTimeFootStepped and LastArrowsSteppedOnByFoot values,
				// calculate the lateral position at this node so we can check for lateral speed cost.
				LateralBodyPosition = GetLateralBodyPosition(stepGraph);

				// Determine the lateral body movement cost.
				// When notes are more dense the body should move side to side less.
				var lateralMovementSpeedCost = 0.0;
				if (config.LateralTighteningPatternLength > 0)
				{
					// Scan backwards over the previous LateralTighteningPatternLength steps.
					var stepCounter = config.LateralTighteningPatternLength;
					var node = PreviousNode;
					bool? goingLeft = null;
					var previousPosition = LateralBodyPosition;
					var previousTime = TimeMicros;
					while (stepCounter > 0 && node != null)
					{
						// Skip SearchNodes which aren't steps.
						if (!node.Stepped)
						{
							node = node.PreviousNode;
							continue;
						}

						// If we have already tracked lateral movement in one direction, make sure we do
						// not start moving in the other direction.
						if (goingLeft != null)
						{
							// If we were going left and are now going right, stop searching.
							if (goingLeft.Value)
							{
								if (node.LateralBodyPosition > previousPosition)
								{
									break;
								}
							}
							// If we were going right and are now going left, stop searching.
							else
							{
								if (node.LateralBodyPosition < previousPosition)
								{
									break;
								}
							}
						}

						// If the body moved without changing directions, update the tracking
						// variables and continue.
						if (Math.Abs(previousPosition - node.LateralBodyPosition) > 0.0001)
						{
							// If we do not know yet whether we are moving right or left, set that now.
							if (goingLeft == null)
								goingLeft = node.LateralBodyPosition < previousPosition;

							previousPosition = node.LateralBodyPosition;
							previousTime = node.TimeMicros;
							stepCounter--;
						}

						node = node.PreviousNode;
					}

					// If we scanned backwards the full amount and found an uninterrupted period of movement
					// in one direction, then perform the nps and speed checks.
					if (stepCounter == 0)
					{
						var nps = config.LateralTighteningPatternLength / ToSeconds(TimeMicros - previousTime);
						var speed = Math.Abs(LateralBodyPosition - previousPosition) / ToSeconds(TimeMicros - previousTime);
						if (((averageNps > 0.0 && nps > averageNps * config.LateralTighteningRelativeNPS)
						     || nps > config.LateralTighteningAbsoluteNPS)
						    && speed > config.LateralTighteningSpeed)
						{
							lateralMovementSpeedCost = speed - config.LateralTighteningSpeed;
						}
					}
				}

				return (distributionCost, individualStepCost, lateralMovementSpeedCost);
			}

			/// <summary>
			/// Gets the lateral position of the body for this SearchNode.
			/// </summary>
			/// <param name="stepGraph">StepGraph for the PerformedChart.</param>
			/// <returns>Lateral position of the body for this SearchNode.</returns>
			private double GetLateralBodyPosition(StepGraph stepGraph)
			{
				var numPoints = 0;
				var x = 0.0;
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
							if (GraphLinkFromPreviousNode.Links[f, p].Action == FootAction.Release)
								return (false, false);
							if (!GraphLinkFromPreviousNode.Links[f, p].Valid)
								isJump = false;
							else if (GraphLinkFromPreviousNode.Links[f, p].Step != StepType.NewArrow
							         && GraphLinkFromPreviousNode.Links[f, p].Step != StepType.SameArrow)
								return (false, false);
							if (GraphLinkFromPreviousNode.Links[f, p].Valid)
							{
								involvesSameArrowStep |= GraphLinkFromPreviousNode.Links[f, p].Step == StepType.SameArrow;
								involvesNewArrowStep |= GraphLinkFromPreviousNode.Links[f, p].Step == StepType.NewArrow;
							}
						}
						// We only care about single steps and jumps, not brackets.
						else if (GraphLinkFromPreviousNode.Links[f, p].Valid)
							return (false, false);
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
					var followsBracket = PreviousNode.GraphLinkFromPreviousNode?.IsBracketStep() ?? false;

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
					if (PreviousNode.LastTimeFootReleased[L] != PreviousNode.LastTimeFootReleased[R])
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
								stepGraph.PadData.ArrowData[arrowBeingSteppedOn].BracketablePairingsOtherHeel[L][leftFrom]
								|| stepGraph.PadData.ArrowData[arrowBeingSteppedOn].BracketablePairingsOtherToe[L][leftFrom];
						}

						if (!rightBracketable && PreviousNode.LastArrowsSteppedOnByFoot[R][p] != InvalidArrowIndex)
						{
							var rightFrom = PreviousNode.LastArrowsSteppedOnByFoot[R][p];
							rightBracketable =
								stepGraph.PadData.ArrowData[arrowBeingSteppedOn].BracketablePairingsOtherHeel[R][rightFrom]
								|| stepGraph.PadData.ArrowData[arrowBeingSteppedOn].BracketablePairingsOtherToe[R][rightFrom];
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
							otherGraphLink.Links[OtherFoot(f), p] = GraphLinkFromPreviousNode.Links[f, p];

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
					var leftSameLink = new GraphLink();
					leftSameLink.Links[L, DefaultFootPortion] = new GraphLink.FootArrowState(StepType.SameArrow, FootAction.Tap);
					leftSameLink.Links[R, DefaultFootPortion] = new GraphLink.FootArrowState(StepType.NewArrow, FootAction.Tap);
					misleading = DoesAnySiblingNodeFromLinkMatchActions(leftSameLink, stepGraph);
					if (misleading)
						return (ambiguous, misleading);

					// Check right foot SameArrow left foot NewArrow.
					var rightSameLink = new GraphLink();
					rightSameLink.Links[L, DefaultFootPortion] = new GraphLink.FootArrowState(StepType.NewArrow, FootAction.Tap);
					rightSameLink.Links[R, DefaultFootPortion] = new GraphLink.FootArrowState(StepType.SameArrow, FootAction.Tap);
					misleading = DoesAnySiblingNodeFromLinkMatchActions(rightSameLink, stepGraph);
					if (misleading)
						return (ambiguous, misleading);

					// Check SameArrow SameArrow.
					var bothSameLink = new GraphLink();
					bothSameLink.Links[L, DefaultFootPortion] = new GraphLink.FootArrowState(StepType.SameArrow, FootAction.Tap);
					bothSameLink.Links[R, DefaultFootPortion] = new GraphLink.FootArrowState(StepType.SameArrow, FootAction.Tap);
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
					// At this point in the search only Tap and Hold are in use for steps.
					if ((actions[a] == PerformanceFootAction.Tap || actions[a] == PerformanceFootAction.Hold)
					    != (otherActions[a] == PerformanceFootAction.Tap || otherActions[a] == PerformanceFootAction.Hold))
					{
						return false;
					}
				}

				return true;
			}

			#region IComparable Implementation

			public int CompareTo(SearchNode other)
			{
				// First, consider misleading steps. These are steps which a player would
				// never interpret as intended.
				if (MisleadingStepCount != other.MisleadingStepCount)
					return MisleadingStepCount < other.MisleadingStepCount ? -1 : 1;

				// Next consider consider ambiguous steps. These are steps which the player
				// would recognize as having multiple options could result in the wrong footing.
				if (AmbiguousStepCount != other.AmbiguousStepCount)
					return AmbiguousStepCount < other.AmbiguousStepCount ? -1 : 1;

				//if (GraphLinkFromPreviousNode != null
				//    && other.GraphLinkFromPreviousNode != null
				//    && GraphLinkFromPreviousNode.Equals(other.GraphLinkFromPreviousNode))
				//{
				//	if (Math.Abs(IndividualStepCost - other.IndividualStepCost) > 0.00001)
				//		return IndividualStepCost < other.IndividualStepCost ? -1 : 1;
				//}

				if (TotalNumSameArrowStepsInARowPerFootOverMax != other.TotalNumSameArrowStepsInARowPerFootOverMax)
					return TotalNumSameArrowStepsInARowPerFootOverMax < other.TotalNumSameArrowStepsInARowPerFootOverMax ? -1 : 1;

				// Next consider individual step cost. This is a measure of how uncomfortably energetic
				// the individual steps are.
				if (Math.Abs(TotalIndividualStepCost - other.TotalIndividualStepCost) > 0.00001)
					return TotalIndividualStepCost < other.TotalIndividualStepCost ? -1 : 1;

				if (Math.Abs(SectionStepTypeCost - other.SectionStepTypeCost) > 0.00001)
					return SectionStepTypeCost < other.SectionStepTypeCost ? -1 : 1;

				// Next consider lateral movement speed. We want to avoid moving on bursts.
				if (Math.Abs(TotalLateralMovementSpeedCost - other.TotalLateralMovementSpeedCost) > 0.00001)
					return TotalLateralMovementSpeedCost < other.TotalLateralMovementSpeedCost ? -1 : 1;

				// If the individual steps and body movement are good, try to match a good distribution next.
				if (Math.Abs(DistributionCost - other.DistributionCost) > 0.00001)
					return DistributionCost < other.DistributionCost ? -1 : 1;

				// Finally, use a random weight. This is helpful to break up patterns.
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
				return (int) Id;
			}

			#endregion IEquatable Implementation
		}

		/// <summary>
		/// Cache of GraphLink to all replacement GraphLink which can be used in a PerformedChart
		/// based on the StepTypeReplacements in Config and the available GraphLinks in the
		/// StepGraph for the OutputChartType.
		/// This is cached as a performance optimization so we do not need to construct the list for
		/// each node of a search.
		/// It is expected that this 
		/// </summary>
		private static readonly Dictionary<GraphLink, List<GraphLink>> GraphLinkReplacementCache =
			new Dictionary<GraphLink, List<GraphLink>>();

		/// <summary>
		/// List of PerformanceNodes representing the roots of each section of the PerformedChart.
		/// </summary>
		private readonly List<PerformanceNode> SectionRoots = new List<PerformanceNode>();

		/// <summary>
		/// Number of arrows in the Chart.
		/// </summary>
		private readonly int NumArrows;

		/// <summary>
		/// Identifier to use when logging messages about this PerformedChart.
		/// </summary>
		private readonly string LogIdentifier;

		/// <summary>
		/// Private constructor.
		/// </summary>
		/// <param name="numArrows">Number of arrows in the Chart.</param>
		/// <param name="root">
		/// Root PerformanceNode of the PerformedChart. Added as the root of the first section.
		/// </param>
		/// <param name="logIdentifier">
		/// Identifier to use when logging messages about this PerformedChart.
		/// </param>
		private PerformedChart(int numArrows, PerformanceNode root, string logIdentifier)
		{
			NumArrows = numArrows;
			SectionRoots.Add(root);
			LogIdentifier = logIdentifier;
		}

		private PerformedChart(int numArrows, string logIdentifier)
		{
			NumArrows = numArrows;
			LogIdentifier = logIdentifier;
		}

		public List<PerformanceNode> GetRootNodes()
		{
			return SectionRoots;
		}

		/// <summary>
		/// Creates a PerformedChart by iteratively searching for a series of GraphNodes that satisfy
		/// the given ExpressedChart's StepEvents.
		/// </summary>
		/// <param name="stepGraph">
		/// StepGraph representing all possible states that can be traversed.
		/// </param>
		/// <param name="rootNodes">
		/// Tiers of root GraphNodes to try as the root.
		/// Outer list expected to be sorted by how desirable the GraphNodes are with the
		/// first List being the most desirable GraphNodes and the last List being the least
		/// desirable GraphNodes. Inner Lists expected to contain GraphNodes of equal preference.
		/// </param>
		/// <param name="expressedChart">ExpressedChart to search.</param>
		/// <param name="randomSeed">
		/// Random seed to use when needing to make random choices when creating the PerformedChart.
		/// </param>
		/// <param name="logIdentifier">
		/// Identifier to use when logging messages about this PerformedChart.
		/// </param>
		/// <returns>
		/// PerformedChart satisfying the given ExpressedChart for the given StepGraph.
		/// </returns>
		public static PerformedChart CreateFromExpressedChart(
			StepGraph stepGraph,
			PerformedChartConfig config,
			List<List<GraphNode>> rootNodes,
			ExpressedChart expressedChart,
			int randomSeed,
			string logIdentifier)
		{
			if (GraphLinkReplacementCache.Count == 0)
			{
				LogError("Programmer Error. No cached GraphLink replacements. See CacheGraphLinks.", logIdentifier);
				return null;
			}

			if (rootNodes == null || rootNodes.Count < 1 || rootNodes[0] == null || rootNodes[0].Count < 1)
				return null;

			SearchNode rootSearchNode = null;
			GraphNode rootGraphNodeToUse = null;
			var nps = FindNPS(expressedChart);
			var random = new Random(randomSeed);

			// Find a path of SearchNodes through the ExpressedChart.
			if (expressedChart.StepEvents.Count > 0)
			{
				// Try each tier of root nodes in order until we find a chart.
				var tier = -1;
				foreach (var currentTierOfRootNodes in rootNodes)
				{
					tier++;

					// Order the root nodes at this tier randomly since they are weighted evenly.
					var roots = currentTierOfRootNodes.OrderBy(a => random.Next()).ToList();

					// Try each root node.
					foreach (var rootGraphNode in roots)
					{
						var depth = 0;

						// Set up a root search node at the root GraphNode.
						rootSearchNode = new SearchNode(
							rootGraphNode,
							GraphLinkReplacementCache[expressedChart.StepEvents[0].LinkInstance.GraphLink],
							null,
							0L,
							depth,
							null,
							new PerformanceFootAction[stepGraph.NumArrows],
							stepGraph,
							nps,
							random.NextDouble(),
							config,
							null);
						var currentSearchNodes = new HashSet<SearchNode>();
						currentSearchNodes.Add(rootSearchNode);

						while (true)
						{
							// Finished
							if (depth >= expressedChart.StepEvents.Count)
							{
								// Choose path with lowest cost.
								SearchNode bestNode = null;
								foreach (var node in currentSearchNodes)
									if (bestNode == null || node.CompareTo(bestNode) < 0)
										bestNode = node;

								// Remove any nodes that are not chosen so there is only one path through the chart.
								foreach (var node in currentSearchNodes)
								{
									if (node.Equals(bestNode))
										continue;
									Prune(node);
								}

								rootGraphNodeToUse = rootGraphNode;
								break;
							}

							// Failed to find a path. Break out and try the next root.
							if (currentSearchNodes.Count == 0)
								break;

							// Accumulate the next level of SearchNodes by looping over each SearchNode
							// in the current set.
							var nextDepth = depth + 1;
							var nextSearchNodes = new HashSet<SearchNode>();
							foreach (var searchNode in currentSearchNodes)
							{
								// Check every GraphLink out of the SearchNode.
								var deadEnd = true;
								for (var l = 0; l < searchNode.GraphLinks.Count; l++)
								{
									var graphLink = searchNode.GraphLinks[l];
									// The GraphNode may not actually have this GraphLink due to
									// the StepTypeReplacements.
									if (!searchNode.GraphNode.Links.ContainsKey(graphLink))
										continue;

									// Check every GraphNode linked to by this GraphLink.
									var nextNodes = searchNode.GraphNode.Links[graphLink];
									for (var n = 0; n < nextNodes.Count; n++)
									{
										var nextGraphNode = nextNodes[n];
										// Determine new step information.
										var actions = GetActionsForNode(nextGraphNode, graphLink, stepGraph.NumArrows);

										var graphLinksToNextNode = new List<GraphLink>();
										if (nextDepth < expressedChart.StepEvents.Count)
											graphLinksToNextNode = GraphLinkReplacementCache[expressedChart.StepEvents[nextDepth].LinkInstance.GraphLink];

										// Set up a new SearchNode.
										var nextSearchNode = new SearchNode(
											nextGraphNode,
											graphLinksToNextNode,
											graphLink,
											expressedChart.StepEvents[depth].TimeMicros,
											nextDepth,
											searchNode,
											actions,
											stepGraph,
											nps,
											random.NextDouble(),
											config,
											null
										);

										// Do not consider this next SearchNode if it results in an invalid state.
										if (DoesNodeStepOnReleaseAtSamePosition(nextSearchNode, expressedChart, stepGraph.NumArrows))
											continue;

										// Update the previous SearchNode's NextNodes to include the new SearchNode.
										if (!searchNode.NextNodes.ContainsKey(graphLink))
											searchNode.NextNodes[graphLink] = new HashSet<SearchNode>();
										searchNode.NextNodes[graphLink].Add(nextSearchNode);

										// Add this node to the set of next SearchNodes to be pruned after they are all found.
										nextSearchNodes.Add(nextSearchNode);
										deadEnd = false;
									}
								}

								// This SearchNode has no valid children. Prune it.
								if (deadEnd)
									Prune(searchNode);
							}

							// Prune all the next SearchNodes, store them in currentSearchNodes, and advance.
							currentSearchNodes = Prune(nextSearchNodes);
							depth = nextDepth;
						}

						// If we found a path from a root GraphNode, then the search is complete.
						if (rootGraphNodeToUse != null)
							break;
					}

					// If we found a path from a root GraphNode, then the search is complete.
					if (rootGraphNodeToUse != null)
						break;
				}

				// If we exhausted all valid root GraphNodes and did not find a path, log an error
				// and return a null PerformedChart.
				if (rootGraphNodeToUse == null)
				{
					LogError("Unable to find performance.", logIdentifier);
					return null;
				}

				// Log a warning if we had to fall back to a worse tier of root GraphNodes.
				if (tier > 0)
				{
					LogInfo($"Using fallback root at tier {tier}.", logIdentifier);
				}
			}

			// Set up a new PerformedChart
			var performedChart = new PerformedChart(
				stepGraph.NumArrows,
				new StepPerformanceNode
				{
					Position = 0,
					GraphNodeInstance = new GraphNodeInstance {Node = rootGraphNodeToUse ?? rootNodes[0][0]},
				},
				logIdentifier);

			// Add the StepPerformanceNodes to the PerformedChart
			var currentPerformanceNode = performedChart.SectionRoots[0];
			var currentSearchNode = rootSearchNode;
			currentSearchNode = currentSearchNode?.GetNextNode();
			while (currentSearchNode != null)
			{
				// Create GraphNodeInstance.
				var stepEventIndex = currentSearchNode.Depth - 1;
				var graphNodeInstance = new GraphNodeInstance {Node = currentSearchNode.GraphNode};
				for (var f = 0; f < NumFeet; f++)
					for (var p = 0; p < NumFootPortions; p++)
						graphNodeInstance.InstanceTypes[f, p] =
							expressedChart.StepEvents[stepEventIndex].LinkInstance.InstanceTypes[f, p];

				// Create GraphLinkInstance.
				var graphLink = currentSearchNode.GraphLinkFromPreviousNode;
				GraphLinkInstance graphLinkInstance = new GraphLinkInstance {GraphLink = graphLink};
				for (var f = 0; f < NumFeet; f++)
				{
					for (var p = 0; p < NumFootPortions; p++)
					{
						graphLinkInstance.InstanceTypes[f, p] =
							expressedChart.StepEvents[stepEventIndex].LinkInstance.InstanceTypes[f, p];
					}
				}

				// Add new StepPerformanceNode and advance.
				var newNode = new StepPerformanceNode
				{
					Position = expressedChart.StepEvents[stepEventIndex].Position,
					GraphLinkInstance = graphLinkInstance,
					GraphNodeInstance = graphNodeInstance,
					Prev = currentPerformanceNode
				};
				currentPerformanceNode.Next = newNode;
				currentPerformanceNode = newNode;
				currentSearchNode = currentSearchNode.GetNextNode();
			}

			var lastPerformanceNode = currentPerformanceNode;

			// Add Mines
			AddMinesToPerformedChart(performedChart, stepGraph, expressedChart, lastPerformanceNode, random);

			return performedChart;
		}

		// TODO: Rewrite
		//public static PerformedChart CreateByFilling(
		//	StepGraph stepGraph,
		//	List<FillSectionConfig> fillSections,
		//	Chart chart,
		//	int randomSeed,
		//	string logIdentifier)
		//{
		//	var performedChart = new PerformedChart(stepGraph.NumArrows, logIdentifier);
		//	var random = new Random(randomSeed);

		//	var validStepTypes = new[] {StepType.NewArrow, StepType.SameArrow};

		//	// up front, loop over all the events in the chart and the configs to make a mapping
		//	// with the cached timemicros and positions by index.
		//	var timingPerSection = DetermineFillSectionTiming(fillSections, chart);

		//	var sectionIndex = 0;
		//	var previousSectionLastL = InvalidArrowIndex;
		//	var previousSectionLastR = InvalidArrowIndex;
		//	var previousSectionEnd = 0;
		//	var previousSectionLastFoot = InvalidFoot;
		//	foreach (var sectionConfig in fillSections)
		//	{
		//		var sectionTiming = timingPerSection[sectionIndex];
		//		var startingWherePreviousSectionEnded = false;

		//		// Get the starting position for this section.
		//		var lStart = sectionConfig.LeftFootStartLane;
		//		var rStart = sectionConfig.RightFootStartLane;
		//		GraphNode rootGraphNode;
		//		if (lStart == InvalidArrowIndex || rStart == InvalidArrowIndex)
		//		{
		//			if (previousSectionEnd == sectionConfig.StartPosition
		//			    && previousSectionLastL != InvalidArrowIndex
		//			    && previousSectionLastR != InvalidArrowIndex)
		//			{
		//				startingWherePreviousSectionEnded = true;
		//				rootGraphNode = stepGraph.FindGraphNode(previousSectionLastL, GraphArrowState.Resting, previousSectionLastR, GraphArrowState.Resting);
		//			}
		//			else
		//			{
		//				rootGraphNode = stepGraph.Root;
		//			}
		//		}
		//		else
		//		{
		//			rootGraphNode = stepGraph.FindGraphNode(lStart, GraphArrowState.Resting, rStart, GraphArrowState.Resting);
		//			if (rootGraphNode == null)
		//			{
		//				performedChart.LogError($"Section {sectionIndex}: Could not find starting node for left foot on {lStart} and right foot on {rStart}.");
		//				return null;
		//			}
		//		}

		//		var root = new StepPerformanceNode
		//		{
		//			Position = 0,
		//			GraphNodeInstance = new GraphNodeInstance { Node = rootGraphNode },
		//		};

		//		// Get the starting foot to start on.
		//		var foot = sectionConfig.FootToStartOn;
		//		if (foot == InvalidFoot)
		//		{
		//			if (previousSectionEnd == sectionConfig.StartPosition
		//			    && previousSectionLastFoot != InvalidFoot)
		//			{
		//				foot = OtherFoot(previousSectionLastFoot);
		//			}
		//			else
		//			{
		//				foot = random.Next(NumFeet);
		//			}
		//		}

		//		var depth = 0;

		//		// Set up a root search node at the root GraphNode.
		//		var possibleGraphLinksToNextNode = new List<GraphLink>();

		//		if (startingWherePreviousSectionEnded)
		//		{
		//			foreach (var stepType in validStepTypes)
		//			{
		//				var link = new GraphLink();
		//				link.Links[foot, DefaultFootPortion] = new GraphLink.FootArrowState(stepType, FootAction.Tap);
		//				possibleGraphLinksToNextNode.Add(link);
		//			}
		//		}
		//		else
		//		{
		//			var rootGraphLink = new GraphLink();
		//			rootGraphLink.Links[foot, DefaultFootPortion] = new GraphLink.FootArrowState(StepType.SameArrow, FootAction.Tap);
		//			possibleGraphLinksToNextNode.Add(rootGraphLink);
		//		}
				
		//		var rootSearchNode = new SearchNode(
		//			rootGraphNode,
		//			possibleGraphLinksToNextNode,
		//			null,
		//			0L,
		//			depth,
		//			null,
		//			new PerformanceFootAction[stepGraph.NumArrows],
		//			stepGraph,
		//			0,
		//			random.NextDouble(),
		//			sectionConfig.PerformedChartConfig,
		//			sectionConfig);

		//		var currentSearchNodes = new HashSet<SearchNode>();
		//		currentSearchNodes.Add(rootSearchNode);

		//		foreach(var timingInfo in sectionTiming)
		//		{
		//			var timeMicros = timingInfo.Item1;
					
		//			// Failed to find a path.
		//			if (currentSearchNodes.Count == 0)
		//			{
		//				performedChart.LogError($"Section {sectionIndex}: Failed to find path.");
		//				break;
		//			}

		//			// Accumulate the next level of SearchNodes by looping over each SearchNode
		//			// in the current set.
		//			var nextDepth = depth + 1;
		//			foot = OtherFoot(foot);
		//			var nextSearchNodes = new HashSet<SearchNode>();

		//			foreach (var searchNode in currentSearchNodes)
		//			{
		//				// Check every GraphLink out of the SearchNode.
		//				var deadEnd = true;
		//				for (var l = 0; l < searchNode.GraphLinks.Count; l++)
		//				{
		//					var graphLink = searchNode.GraphLinks[l];
		//					// The GraphNode may not actually have this GraphLink due to
		//					// the StepTypeReplacements.
		//					if (!searchNode.GraphNode.Links.ContainsKey(graphLink))
		//						continue;

		//					// Check every GraphNode linked to by this GraphLink.
		//					var nextNodes = searchNode.GraphNode.Links[graphLink];
		//					for (var n = 0; n < nextNodes.Count; n++)
		//					{
		//						var nextGraphNode = nextNodes[n];
		//						// Determine new step information.
		//						var actions = GetActionsForNode(nextGraphNode, graphLink, stepGraph.NumArrows);
								
		//						possibleGraphLinksToNextNode = new List<GraphLink>();
		//						foreach (var stepType in validStepTypes)
		//						{
		//							if (depth == 1 && !startingWherePreviousSectionEnded && stepType != StepType.SameArrow)
		//								continue;
		//							var link = new GraphLink();
		//							link.Links[foot, DefaultFootPortion] = new GraphLink.FootArrowState(stepType, FootAction.Tap);
		//							possibleGraphLinksToNextNode.Add(link);
		//						}

		//						// Set up a new SearchNode.
		//						var nextSearchNode = new SearchNode(
		//							nextGraphNode,
		//							possibleGraphLinksToNextNode,
		//							graphLink,
		//							timeMicros,
		//							nextDepth,
		//							searchNode,
		//							actions,
		//							stepGraph,
		//							0,
		//							random.NextDouble(),
		//							sectionConfig.PerformedChartConfig,
		//							sectionConfig
		//						);

		//						// Update the previous SearchNode's NextNodes to include the new SearchNode.
		//						if (!searchNode.NextNodes.ContainsKey(graphLink))
		//							searchNode.NextNodes[graphLink] = new HashSet<SearchNode>();
		//						searchNode.NextNodes[graphLink].Add(nextSearchNode);

		//						// Add this node to the set of next SearchNodes to be pruned after they are all found.
		//						nextSearchNodes.Add(nextSearchNode);
		//						deadEnd = false;
		//					}
		//				}

		//				// This SearchNode has no valid children. Prune it.
		//				if (deadEnd)
		//					Prune(searchNode);
		//			}

		//			// Prune all the next SearchNodes, store them in currentSearchNodes, and advance.
		//			currentSearchNodes = Prune(nextSearchNodes);
		//			depth = nextDepth;
		//		}

		//		// Finished
		//		{
		//			// Check for ending at the correct location.
		//			if (sectionConfig.LeftFootEndLane != InvalidArrowIndex &&
		//			    sectionConfig.RightFootEndLane != InvalidArrowIndex)
		//			{
		//				var remainingNodes = new HashSet<SearchNode>();
		//				foreach (var node in currentSearchNodes)
		//				{
		//					if (node.GraphNode.State[L, DefaultFootPortion].Arrow == sectionConfig.LeftFootEndLane
		//					    && node.GraphNode.State[R, DefaultFootPortion].Arrow == sectionConfig.RightFootEndLane)
		//					{
		//						remainingNodes.Add(node);
		//						continue;
		//					}

		//					Prune(node);
		//				}

		//				currentSearchNodes = remainingNodes;
		//				if (currentSearchNodes.Count == 0)
		//				{
		//					performedChart.LogError(
		//						$"Section {sectionIndex}: Could not find path ending with left on {sectionConfig.LeftFootEndLane} and right foot on {sectionConfig.RightFootEndLane}.");
		//				}
		//			}

		//			// Choose path with lowest cost.
		//			SearchNode bestNode = null;
		//			foreach (var node in currentSearchNodes)
		//				if (bestNode == null || node.CompareTo(bestNode) < 0)
		//					bestNode = node;

		//			// Remove any nodes that are not chosen so there is only one path through the chart.
		//			foreach (var node in currentSearchNodes)
		//			{
		//				if (node.Equals(bestNode))
		//					continue;
		//				Prune(node);
		//			}

		//			previousSectionEnd = sectionConfig.EndPosition;
		//			previousSectionLastL = bestNode.GraphNode.State[L, DefaultFootPortion].Arrow;
		//			previousSectionLastR = bestNode.GraphNode.State[R, DefaultFootPortion].Arrow;
		//			previousSectionLastFoot = OtherFoot(foot);
		//		}

		//		performedChart.SectionRoots.Add(root);
		//		sectionIndex++;

		//		// Add the StepPerformanceNodes to the PerformedChart
		//		var currentPerformanceNode = root;
		//		var currentSearchNode = rootSearchNode;
		//		currentSearchNode = currentSearchNode?.GetNextNode();
		//		var index = 0;
		//		while (currentSearchNode != null)
		//		{
		//			// Create GraphNodeInstance.
		//			var graphNodeInstance = new GraphNodeInstance { Node = currentSearchNode.GraphNode };

		//			// Create GraphLinkInstance.
		//			var graphLink = currentSearchNode.GraphLinkFromPreviousNode;
		//			GraphLinkInstance graphLinkInstance = new GraphLinkInstance { GraphLink = graphLink };

		//			// Add new StepPerformanceNode and advance.
		//			var newNode = new StepPerformanceNode
		//			{
		//				Position = sectionTiming[index].Item2,
		//				GraphLinkInstance = graphLinkInstance,
		//				GraphNodeInstance = graphNodeInstance,
		//				Prev = currentPerformanceNode
		//			};
		//			currentPerformanceNode.Next = newNode;
		//			currentPerformanceNode = newNode;
		//			currentSearchNode = currentSearchNode.GetNextNode();
		//			index++;
		//		}
		//	}

		//	return performedChart;
		//}

		//private class FillEvent
		//{
		//	public Event Event;

		//	public int SectionIndex;
		//	public int IndexWithinSection;
		//	public int Position;
		//}

		//public class FillEventComparer : IComparer<FillEvent>
		//{
		//	private SMCommon.SMEventComparer SMEventComparer = new SMCommon.SMEventComparer();

		//	int IComparer<FillEvent>.Compare(FillEvent e1, FillEvent e2)
		//	{
		//		// First, compare by position.
		//		int comparison = e1.Position.CompareTo(e2.Position);
		//		if (comparison != 0)
		//			return comparison;

		//		// If one event is for an Event from the Chart, that should come first.
		//		if ((e1.Event == null) != (e2.Event == null))
		//			return e1.Event == null ? 1 : -1;

		//		// If neither event is from the Chart, the are the same.
		//		if (e1.Event == null)
		//			return 0;

		//		// Both events are Chart Events. Compare them with the standard StepMania sort rules.
		//		return SMEventComparer.Compare(e1.Event, e2.Event);
		//	}
		//}

		// TODO: Rewrite
		//private static Tuple<long, int>[][] DetermineFillSectionTiming(
		//	List<FillSectionConfig> fillSections,
		//	Chart chart)
		//{
		//	var FillEvents = new List<FillEvent>();
		//	foreach(var chartEvent in chart.Layers[0].Events)
		//		FillEvents.Add(new FillEvent { Event = chartEvent, Position = chartEvent.IntegerPosition});

		//	var numSections = fillSections.Count;
		//	var numEventsInSection = new int[numSections];
		//	var sectionIndex = 0;
		//	foreach (var config in fillSections)
		//	{
		//		var indexWithinSection = 0;
		//		var pos = config.StartPosition;
		//		while (pos < config.EndPosition)
		//		{
		//			numEventsInSection[sectionIndex]++;
		//			FillEvents.Add(new FillEvent
		//			{
		//				SectionIndex = sectionIndex,
		//				IndexWithinSection = indexWithinSection,
		//				Position = pos
		//			});
					
		//			indexWithinSection++;
		//			pos += SMCommon.MaxValidDenominator / config.BeatSubDivisionToFill;
		//		}
		//		sectionIndex++;
		//	}

		//	FillEvents.Sort(new FillEventComparer());
			
		//	var sectionData = new Tuple<long, int>[numSections][];
		//	for (var si = 0; si < numSections; si++)
		//		sectionData[si] = new Tuple<long, int>[numEventsInSection[si]];

		//	var bpm = 0.0;
		//	var timeSignature = new Fraction(4, 4);
		//	double beatTime = 0.0;
		//	double currentTime = 0.0;

		//	var previousPosition = 0;
		//	foreach (var fillEvent in FillEvents)
		//	{
		//		if (fillEvent.Position > previousPosition)
		//		{
		//			var currentBeats =
		//				fillEvent.Position.Measure * timeSignature.Numerator
		//				+ fillEvent.Position.Beat
		//				+ (fillEvent.Position.SubDivision.Denominator == 0 ? 0 : fillEvent.Position.SubDivision.ToDouble());
		//			var previousBeats =
		//				previousPosition.Measure * timeSignature.Numerator
		//				+ previousPosition.Beat
		//				+ (previousPosition.SubDivision.Denominator == 0 ? 0 : previousPosition.SubDivision.ToDouble());
		//			currentTime += (currentBeats - previousBeats) * beatTime;
		//		}

		//		// Add the timing data.
		//		if (fillEvent.Event == null)
		//		{
		//			var timeMicros = ToMicros(currentTime);
		//			sectionData[fillEvent.SectionIndex][fillEvent.IndexWithinSection] =
		//				new Tuple<long, int>(timeMicros, fillEvent.Position);
		//		}

		//		// Process tempo changes and stops.
		//		if (fillEvent.Event != null)
		//		{
		//			var chartEvent = fillEvent.Event;
		//			var beatTimeDirty = false;
		//			if (chartEvent is Stop stop)
		//				currentTime += ToSeconds(stop.LengthMicros);
		//			else if (chartEvent is TimeSignature ts)
		//			{
		//				timeSignature = ts.Signature;
		//				beatTimeDirty = true;
		//			}
		//			else if (chartEvent is Tempo tc)
		//			{
		//				bpm = tc.TempoBPM;
		//				beatTimeDirty = true;
		//			}

		//			if (beatTimeDirty)
		//			{
		//				if (bpm == 0.0 || timeSignature.Denominator == 0.0)
		//					beatTime = 0.0;
		//				else
		//					beatTime = (60 / bpm) * (4.0 / timeSignature.Denominator);
		//			}
		//		}

		//		previousPosition = fillEvent.Position;
		//	}

		//	return sectionData;
		//}

		/// <summary>
		/// Finds the notes per second of the entire Chart represented by the given ExpressedChart.
		/// </summary>
		/// <param name="expressedChart">ExpressedChart to find the notes per second of.</param>
		/// <returns>Notes per second of the Chart represented by the given ExpressedChart.</returns>
		private static double FindNPS(ExpressedChart expressedChart)
		{
			var nps = 0.0;
			var startTime = long.MaxValue;
			var endTime = 0L;
			var numSteps = 0;
			for (var e = 0; e < expressedChart.StepEvents.Count; e++)
			{
				var stepEvent = expressedChart.StepEvents[e];
				for (var f = 0; f < NumFeet; f++)
				{
					for (var p = 0; p < NumFootPortions; p++)
					{
						if (stepEvent.LinkInstance.GraphLink.Links[f, p].Valid
						    && stepEvent.LinkInstance.GraphLink.Links[f, p].Action != FootAction.Release)
						{
							if (stepEvent.TimeMicros < startTime)
								startTime = stepEvent.TimeMicros;
							numSteps++;
							endTime = stepEvent.TimeMicros;
						}
					}
				}
			}

			if (endTime > startTime)
			{
				nps = numSteps / ToSeconds(endTime - startTime);
			}

			return nps;
		}

		/// <summary>
		/// Prunes the given HashSet of SearchNodes to a HashSet that contains
		/// only the lowest cost SearchNode per unique GraphNode.
		/// </summary>
		/// <param name="nodes">HashSet of SearchNodes to prune.</param>
		/// <returns>Pruned SearchNodes.</returns>
		private static HashSet<SearchNode> Prune(HashSet<SearchNode> nodes)
		{
			// Set up a Dictionary to track the best ChartSearchNode per GraphNode.
			var bestNodes = new Dictionary<GraphNode, SearchNode>();
			foreach (var node in nodes)
			{
				// There is already a best node for this GraphNode, compare them.
				if (bestNodes.TryGetValue(node.GraphNode, out var currentNode))
				{
					// This node is better.
					if (node.CompareTo(currentNode) < 1)
					{
						Prune(currentNode);

						// Set the currentNode to this new best node so we record it below.
						currentNode = node;
					}
					else
					{
						Prune(node);
					}
				}
				// There is not yet a best node recorded for this GraphNode. Record this node
				// as the current best.
				else
				{
					currentNode = node;
				}

				bestNodes[currentNode.GraphNode] = currentNode;
			}

			return bestNodes.Values.ToHashSet();
		}

		/// <summary>
		/// Removes the given SearchNode from the tree.
		/// Removes all parents up until the first parent with other children.
		/// </summary>
		/// <param name="node">SearchNode to prune.</param>
		private static void Prune(SearchNode node)
		{
			// Prune the node up until parent that has other children.
			while (node.PreviousNode != null)
			{
				node.PreviousNode.NextNodes[node.GraphLinkFromPreviousNode].Remove(node);
				if (node.PreviousNode.NextNodes[node.GraphLinkFromPreviousNode].Count == 0)
					node.PreviousNode.NextNodes.Remove(node.GraphLinkFromPreviousNode);
				if (node.PreviousNode.NextNodes.Count != 0)
					break;
				node = node.PreviousNode;
			}
		}

		/// <summary>
		/// Checks whether the given node has a step that occurs at the same time as a release on the same arrow.
		/// Some valid expressions might otherwise cause this kind of pattern to be generated in a PerformedChart
		/// but this does not represent a valid SM Chart. This can happen when there is a jump and the foot in
		/// question is supposed to jump on the same arrow but the previous step was a bracket so there are two
		/// arrows to choose from. We could apply the SameArrow step to the arrow which just released even though
		/// that is impossible in the ExpressedChart.
		/// </summary>
		/// <param name="node">The SearchNode to check.</param>
		/// <param name="expressedChart">The ExpressedChart so we can check GraphLinks.</param>
		/// <param name="numArrows">Number of arrows in the Chart.</param>
		/// <returns>
		/// True if this SearchNode has a step that occurs at the same time as a release on the same arrow.
		/// </returns>
		private static bool DoesNodeStepOnReleaseAtSamePosition(SearchNode node, ExpressedChart expressedChart, int numArrows)
		{
			var previousNode = node.PreviousNode;
			if (previousNode == null)
				return false;
			var previousPreviousNode = previousNode.PreviousNode;
			if (previousPreviousNode == null)
				return false;

			// This node and the previous node must occur at the same time for the problem to arise.
			if (expressedChart.StepEvents[previousNode.Depth - 1].Position != expressedChart.StepEvents[node.Depth - 1].Position)
				return false;

			// Check if the previous node released on the same arrow tha the current node is stepping on.
			for (var a = 0; a < numArrows; a++)
			{
				if (previousNode.Actions[a] == PerformanceFootAction.Release &&
				    node.Actions[a] != PerformanceFootAction.None && node.Actions[a] != PerformanceFootAction.Release)
					return true;
			}

			return false;
		}

		/// <summary>
		/// Add Mines to the PerformedChart. Done after the steps are added since mine placement is
		/// relative to arrows in the chart. Mines are added to the end of the PerformanceNode list
		/// and sorted later.
		/// </summary>
		/// <param name="performedChart">PerformedChart to add mines for.</param>
		/// <param name="stepGraph">
		/// StepGraph representing all possible states that can be traversed.
		/// </param>
		/// <param name="expressedChart">ExpressedChart being used to generate the PerformedChart.</param>
		/// <param name="lastPerformanceNode">
		/// Last StepPerformanceNode in the PerformedChart. Used to append MinePerformanceNodes to
		/// the end.
		/// </param>
		private static void AddMinesToPerformedChart(
			PerformedChart performedChart,
			StepGraph stepGraph,
			ExpressedChart expressedChart,
			PerformanceNode lastPerformanceNode,
			Random random)
		{
			// Record which lanes have arrows in them.
			var numLanesWithArrows = 0;
			var lanesWithNoArrows = new bool[stepGraph.NumArrows];
			for (var a = 0; a < stepGraph.NumArrows; a++)
				lanesWithNoArrows[a] = true;
			foreach (var root in performedChart.SectionRoots)
			{
				var currentPerformanceNode = root;
				while (currentPerformanceNode != null)
				{
					if (currentPerformanceNode is StepPerformanceNode stepNode)
					{
						for (var f = 0; f < NumFeet; f++)
						{
							for (var p = 0; p < NumFootPortions; p++)
							{
								if (stepNode.GraphNodeInstance.Node.State[f, p].Arrow != InvalidArrowIndex)
								{
									if (lanesWithNoArrows[stepNode.GraphNodeInstance.Node.State[f, p].Arrow])
									{
										lanesWithNoArrows[stepNode.GraphNodeInstance.Node.State[f, p].Arrow] = false;
										numLanesWithArrows++;
									}
								}
							}
						}

						if (numLanesWithArrows == stepGraph.NumArrows)
							break;
					}

					currentPerformanceNode = currentPerformanceNode.Next;
				}
			}

			// Get the first lane with no arrow, if one exists.
			var firstLaneWithNoArrow = InvalidArrowIndex;
			for (var a = 0; a < stepGraph.NumArrows; a++)
			{
				if (!lanesWithNoArrows[a])
					continue;
				firstLaneWithNoArrow = a;
				break;
			}

			// Create sorted lists of releases.
			var stepEvents = new List<StepPerformanceNode>();
			foreach (var root in performedChart.SectionRoots)
			{
				var currentPerformanceNode = root;
				while (currentPerformanceNode != null)
				{
					if (currentPerformanceNode is StepPerformanceNode stepNode)
						stepEvents.Add(stepNode);
					currentPerformanceNode = currentPerformanceNode.Next;
				}
			}

			var (releases, steps) = MineUtils.GetReleasesAndSteps(stepEvents, stepGraph.NumArrows);

			// Add the MinePerformanceNodes to the PerformedChart.
			// For simplicity add all the nodes to the end. They will be sorted later.
			var stepIndex = 0;
			var releaseIndex = 0;
			var previousMinePosition = -1;
			var arrowsOccupiedByMines = new bool[stepGraph.NumArrows];
			var randomLaneOrder = Enumerable.Range(0, stepGraph.NumArrows).OrderBy(x => random.Next()).ToArray();
			for (var m = 0; m < expressedChart.MineEvents.Count; m++)
			{
				var mineEvent = expressedChart.MineEvents[m];
				// Advance the step and release indices to follow and precede the event respectively.
				while (stepIndex < steps.Count && steps[stepIndex].Position <= mineEvent.Position)
					stepIndex++;
				while (releaseIndex + 1 < releases.Count && releases[releaseIndex + 1].Position < mineEvent.Position)
					releaseIndex++;

				// Reset arrows occupied by mines if this mine is at a new position.
				if (previousMinePosition < 0 || previousMinePosition < mineEvent.Position)
				{
					for (var a = 0; a < stepGraph.NumArrows; a++)
						arrowsOccupiedByMines[a] = false;
				}

				previousMinePosition = mineEvent.Position;

				switch (mineEvent.Type)
				{
					case MineType.AfterArrow:
					case MineType.BeforeArrow:
					{
						var bestArrow = MineUtils.FindBestNthMostRecentArrow(
							mineEvent.Type == MineType.AfterArrow,
							mineEvent.ArrowIsNthClosest,
							mineEvent.FootAssociatedWithPairedNote,
							stepGraph.NumArrows,
							releases,
							releaseIndex,
							steps,
							stepIndex,
							arrowsOccupiedByMines,
							mineEvent.Position,
							randomLaneOrder);
						if (bestArrow != InvalidArrowIndex)
						{
							// Add mine event
							var newNode = new MinePerformanceNode
							{
								Position = mineEvent.Position,
								Arrow = bestArrow,
								Prev = lastPerformanceNode
							};
							lastPerformanceNode.Next = newNode;
							lastPerformanceNode = newNode;

							arrowsOccupiedByMines[bestArrow] = true;
						}
						else
						{
							performedChart.LogWarn(
								$"Skipping {mineEvent.Type:G} mine event at {mineEvent.Position}. Unable to determine best arrow to associate with this mine.");
						}

						break;
					}
					case MineType.NoArrow:
					{
						// If this PerformedChart has a lane with no arrows in it, use that for this mine.
						// If it doesn't then just skip the mine.
						if (firstLaneWithNoArrow >= 0)
						{
							var newNode = new MinePerformanceNode
							{
								Position = mineEvent.Position,
								Arrow = firstLaneWithNoArrow,
								Prev = lastPerformanceNode
							};
							lastPerformanceNode.Next = newNode;
							lastPerformanceNode = newNode;

							arrowsOccupiedByMines[firstLaneWithNoArrow] = true;
						}
						else
						{
							performedChart.LogWarn(
								$"Skipping {mineEvent.Type:G} mine event at {mineEvent.Position}. No empty lanes.");
						}

						break;
					}
				}
			}
		}

		/// <summary>
		/// Given a GraphNodeInstance and the GraphLinkInstance to that node, returns a
		/// representation of what actions should be performed on what arrows
		/// to arrive at the node. The actions are returned in an array indexed by arrow.
		/// This is a helper method used when generating an SM Chart.
		/// </summary>
		/// <param name="graphNode">GraphNode.</param>
		/// <param name="graphLinkToNode">GraphLink to GraphNode.</param>
		/// <param name="numArrows">Number of arrows in the Chart.</param>
		/// <returns>Array of actions.</returns>
		private static PerformanceFootAction[] GetActionsForNode(
			GraphNodeInstance graphNode,
			GraphLinkInstance graphLinkToNode,
			int numArrows)
		{
			// Initialize actions.
			var actions = new PerformanceFootAction[numArrows];
			for (var a = 0; a < numArrows; a++)
				actions[a] = PerformanceFootAction.None;

			for (var f = 0; f < NumFeet; f++)
			{
				for (var p = 0; p < NumFootPortions; p++)
				{
					if (graphLinkToNode.GraphLink.Links[f, p].Valid)
					{
						var arrow = graphNode.Node.State[f, p].Arrow;
						switch (graphLinkToNode.GraphLink.Links[f, p].Action)
						{
							case FootAction.Release:
								actions[arrow] = PerformanceFootAction.Release;
								break;
							case FootAction.Hold:
								if (graphNode.InstanceTypes[f, p] == InstanceStepType.Roll)
									actions[arrow] = PerformanceFootAction.Roll;
								else
									actions[arrow] = PerformanceFootAction.Hold;
								break;
							case FootAction.Tap:
								if (graphNode.InstanceTypes[f, p] == InstanceStepType.Fake)
									actions[arrow] = PerformanceFootAction.Fake;
								else if (graphNode.InstanceTypes[f, p] == InstanceStepType.Lift)
									actions[arrow] = PerformanceFootAction.Lift;
								else
									actions[arrow] = PerformanceFootAction.Tap;
								break;
						}
					}
				}
			}

			return actions;
		}

		/// <summary>
		/// Given a GraphNode and the GraphLink to that node, returns a
		/// representation of what actions should be performed on what arrows
		/// to arrive at the node. The actions are returned in an array indexed by arrow.
		/// This is a helper method used when searching to determine which arrows were stepped on,
		/// and for determining if steps and releases occur at the same time on the same arrows.
		/// This method will not return PerformanceFootActions based on InstanceStepTypes.
		/// Specifically, it will only set None, Release, Hold, or Tap.
		/// This method is static and takes the number of arrows as a parameter because it can be used prior to
		/// instantiating the PerformedChart.
		/// </summary>
		/// <param name="graphNode">GraphNode.</param>
		/// <param name="graphLinkToNode">GraphLink to GraphNode.</param>
		/// <param name="numArrows">Number of arrows in the Chart.</param>
		/// <returns>Array of actions.</returns>
		private static PerformanceFootAction[] GetActionsForNode(
			GraphNode graphNode,
			GraphLink graphLinkToNode,
			int numArrows)
		{
			// Initialize actions.
			var actions = new PerformanceFootAction[numArrows];
			for (var a = 0; a < numArrows; a++)
				actions[a] = PerformanceFootAction.None;

			for (var f = 0; f < NumFeet; f++)
			{
				for (var p = 0; p < NumFootPortions; p++)
				{
					if (graphLinkToNode.Links[f, p].Valid)
					{
						var arrow = graphNode.State[f, p].Arrow;
						switch (graphLinkToNode.Links[f, p].Action)
						{
							case FootAction.Release:
								actions[arrow] = PerformanceFootAction.Release;
								break;
							case FootAction.Hold:
								actions[arrow] = PerformanceFootAction.Hold;
								break;
							case FootAction.Tap:
								actions[arrow] = PerformanceFootAction.Tap;
								break;
						}
					}
				}
			}

			return actions;
		}

		/// <summary>
		/// Creates a List of Events representing the Events of an SM Chart.
		/// </summary>
		/// <returns>List of Events that represent the Events of a SM Chart.</returns>
		public List<Event> CreateSMChartEvents()
		{
			var events = new List<Event>();

			foreach (var root in SectionRoots)
			{
				var currentNode = root;
				// Skip first rest position node.
				currentNode = currentNode.Next;
				while (currentNode != null)
				{
					// StepPerformanceNode
					if (currentNode is StepPerformanceNode stepNode)
					{
						var actions = GetActionsForNode(
							stepNode.GraphNodeInstance,
							stepNode.GraphLinkInstance,
							NumArrows);

						for (var arrow = 0; arrow < NumArrows; arrow++)
						{
							var action = actions[arrow];
							switch (action)
							{
								case PerformanceFootAction.Release:
									events.Add(new LaneHoldEndNote
									{
										IntegerPosition = stepNode.Position,
										Lane = arrow,
										Player = 0,
										SourceType = SMCommon.NoteChars[(int) SMCommon.NoteType.HoldEnd].ToString()
									});
									break;
								case PerformanceFootAction.Tap:
								case PerformanceFootAction.Fake:
								case PerformanceFootAction.Lift:
								{
									var instanceAction = SMCommon.NoteType.Tap;
									if (action == PerformanceFootAction.Fake)
										instanceAction = SMCommon.NoteType.Fake;
									else if (action == PerformanceFootAction.Lift)
										instanceAction = SMCommon.NoteType.Lift;
									events.Add(new LaneTapNote
									{
										IntegerPosition = stepNode.Position,
										Lane = arrow,
										Player = 0,
										SourceType = SMCommon.NoteChars[(int) instanceAction].ToString()
									});
									break;
								}
								case PerformanceFootAction.Hold:
								case PerformanceFootAction.Roll:
								{
									// Hold or Roll Start
									var holdRollType = action == PerformanceFootAction.Hold
										? SMCommon.NoteType.HoldStart
										: SMCommon.NoteType.RollStart;
									events.Add(new LaneHoldStartNote
									{
										IntegerPosition = stepNode.Position,
										Lane = arrow,
										Player = 0,
										SourceType = SMCommon.NoteChars[(int) holdRollType].ToString()
									});
									break;
								}
							}
						}
					}

					// MinePerformanceNode
					else if (currentNode is MinePerformanceNode mineNode)
					{
						events.Add(new LaneNote
						{
							IntegerPosition = mineNode.Position,
							Lane = mineNode.Arrow,
							Player = 0,
							SourceType = SMCommon.NoteChars[(int) SMCommon.NoteType.Mine].ToString()
						});
					}

					// Advance
					currentNode = currentNode.Next;
				}
			}

			return events;
		}

		#region GraphLink Cache

		/// <summary>
		/// Determines and caches all replacement GraphLinks for the given GraphLinks.
		/// Caches into GraphLinkReplacementCache.
		/// </summary>
		/// <remarks>
		/// Expected that the given GraphLinks represent the set of all GraphLinks in
		/// the output StepGraph.
		/// Expected that this method is called to populate the cache prior to calling
		/// CreateFromExpressedChart.
		/// </remarks>
		/// <param name="graphLinks">Collection of GraphLinks</param>
		public static void CacheGraphLinks(IEnumerable<GraphLink> graphLinks,
			Dictionary<StepType, HashSet<StepType>> stepTypeReplacements)
		{
			foreach (var graphLink in graphLinks)
			{
				if (!GraphLinkReplacementCache.ContainsKey(graphLink))
					GraphLinkReplacementCache.Add(graphLink, FindAllAcceptableLinks(graphLink, stepTypeReplacements));
			}
		}

		/// <summary>
		/// Given a GraphLink from an ExpressedChart, return all acceptable GraphLinks that can be used
		/// in its place in the PerformedChart.
		/// Replacements are specified in Config.StepTypeReplacements.
		/// </summary>
		/// <param name="originalGraphLinkToNextNode">Original GraphLink.</param>
		/// <returns>List of all valid GraphLink replacements.</returns>
		private static List<GraphLink> FindAllAcceptableLinks(
			GraphLink originalGraphLinkToNextNode,
			Dictionary<StepType, HashSet<StepType>> stepTypeReplacements)
		{
			var acceptableLinks = new List<GraphLink>();
			if (originalGraphLinkToNextNode == null)
				return acceptableLinks;

			var originalLinks = originalGraphLinkToNextNode.Links;

			// Accumulate states to turn into GraphLinks.
			// Loop over each foot and portion, updating tempStates with valid replacements.
			var tempStates = new List<GraphLink.FootArrowState[,]>();
			for (var f = 0; f < NumFeet; f++)
			{
				for (var p = 0; p < NumFootPortions; p++)
				{
					// Get the acceptable steps for the step at this Foot and FootPortion.
					if (!originalLinks[f, p].Valid)
						continue;
					if (!stepTypeReplacements.TryGetValue(originalLinks[f, p].Step, out var acceptableSteps))
						continue;

					// If no temp states exist yet, create new ones for this Foot and FootPortion.
					if (tempStates.Count == 0)
					{
						foreach (var stepType in acceptableSteps)
						{
							var state = new GraphLink.FootArrowState[NumFeet, NumFootPortions];
							state[f, p] = new GraphLink.FootArrowState(stepType, originalLinks[f, p].Action);
							tempStates.Add(state);
						}
					}

					// If temp states exist, loop over them and add to their states.
					else
					{
						// Accumulate new states taking the state from the current tempStates and
						// adding the new step to them.
						var newStates = new List<GraphLink.FootArrowState[,]>();
						foreach (var tempState in tempStates)
						{
							foreach (var stepType in acceptableSteps)
							{
								// Don't create invalid brackets. In a bracket, both FootPortions
								// must use the same StepType.
								if (p > 0
								    && tempState[f, 0].Valid
								    && StepData.Steps[(int) tempState[f, 0].Step].IsBracket
								    && stepType != tempState[f, 0].Step)
								{
									continue;
								}

								// Create a new state, copied from the current temp state.
								var state = new GraphLink.FootArrowState[NumFeet, NumFootPortions];
								for (var f2 = 0; f2 < NumFeet; f2++)
									for (var p2 = 0; p2 < NumFootPortions; p2++)
										state[f2, p2] = tempState[f2, p2];
								// Update the new state with the new StepType.
								state[f, p] = new GraphLink.FootArrowState(stepType, originalLinks[f, p].Action);
								newStates.Add(state);
							}
						}

						// Update the tempStates with the newStates.
						tempStates = newStates;
					}
				}
			}

			// Accumulate all the states into GraphLinks.
			foreach (var state in tempStates)
			{
				var g = new GraphLink();
				for (var f = 0; f < NumFeet; f++)
					for (var p = 0; p < NumFootPortions; p++)
						g.Links[f, p] = state[f, p];
				acceptableLinks.Add(g);
			}

			return acceptableLinks;
		}

		#endregion GraphLink Cache

		#region Logging

		private static void LogError(string message, string logIdentifier)
		{
			if (!string.IsNullOrEmpty(logIdentifier))
				Logger.Error($"[{LogTag}] {logIdentifier} {message}");
			else
				Logger.Error($"[{LogTag}] {message}");
		}

		private static void LogWarn(string message, string logIdentifier)
		{
			if (!string.IsNullOrEmpty(logIdentifier))
				Logger.Warn($"[{LogTag}] {logIdentifier} {message}");
			else
				Logger.Warn($"[{LogTag}] {message}");
		}

		private static void LogInfo(string message, string logIdentifier)
		{
			if (!string.IsNullOrEmpty(logIdentifier))
				Logger.Info($"[{LogTag}] {logIdentifier} {message}");
			else
				Logger.Info($"[{LogTag}] {message}");
		}

		private void LogError(string message)
		{
			LogError(message, LogIdentifier);
		}

		private void LogWarn(string message)
		{
			LogWarn(message, LogIdentifier);
		}

		private void LogInfo(string message)
		{
			LogInfo(message, LogIdentifier);
		}

		#endregion Logging
	}
}
