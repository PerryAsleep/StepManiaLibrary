using System;
using System.Collections.Generic;
using System.Linq;
using Fumen;
using Fumen.ChartDefinition;
using Fumen.Converters;
using static StepManiaLibrary.Constants;

namespace StepManiaLibrary.PerformedChart;

/// <summary>
/// A PerformedChart is a series of events which describe how a Chart is played.
/// This includes specifics about which feet hit which arrows in what ways.
/// An ExpressedChart can be turned into a PerformedChart for a StepGraph.
/// A PerformedChart can be used to generate an SM Chart.
/// A PerformedChart's representation comes from GraphLinkInstances and
/// GraphNodeInstances.
/// </summary>
public partial class PerformedChart
{
	private const string LogTag = "Performed Chart";

	/// <summary>
	/// StepType cost for falling back to a completely blank link, dropping all steps for all portions of all feet.
	/// </summary>
	private const double BlankStepCost = 1000.0;

	/// <summary>
	/// StepType cost for falling back to a link with at least one foot having all of its steps dropped.
	/// </summary>
	private const double BlankSingleStepCost = 900.0;

	/// <summary>
	/// StepType cost for falling back to a link with dropped steps.
	/// </summary>
	private const double IndividualDroppedArrowStepCost = 100.0;

	/// <summary>
	/// Enumeration of states each arrow can be in at each position.
	/// Used to assist with translating a PerformedChart into an SM Chart.
	/// </summary>
	public enum PerformanceFootAction
	{
		None,
		Tap,
		Fake,
		Lift,
		Hold,
		Roll,
		Release,
	}

	/// <summary>
	/// Cache of fallback / replacement GraphLinkInstances.
	/// </summary>
	private static readonly GraphLinkInstanceCache LinkCache = new();

	/// <summary>
	/// List of PerformanceNodes representing the roots of each section of the PerformedChart.
	/// </summary>
	private readonly PerformanceNode Root;

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
		Root = root;
		LogIdentifier = logIdentifier;
	}

	public PerformanceNode GetRootNode()
	{
		return Root;
	}

	#region Creation From ExpressedChart

	/// <summary>
	/// Creates a PerformedChart by iteratively searching for a series of GraphNodes that satisfy
	/// the given ExpressedChart's StepEvents.
	/// </summary>
	/// <param name="stepGraph">
	/// StepGraph representing all possible states that can be traversed.
	/// </param>
	/// <param name="config">Config to use.</param>
	/// <param name="rootNodes">
	/// Tiers of root GraphNodes to try as the root.
	/// Outer list expected to be sorted by how desirable the GraphNodes are with the
	/// first List being the most desirable GraphNodes and the last List being the least
	/// desirable GraphNodes. Inner Lists expected to contain GraphNodes of equal preference.
	/// </param>
	/// <param name="fallbacks">StepTypeFallbacks to use.</param>
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
		Config config,
		List<List<GraphNode>> rootNodes,
		StepTypeFallbacks fallbacks,
		ExpressedChart.ExpressedChart expressedChart,
		int randomSeed,
		string logIdentifier)
	{
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
			var furthestPosition = 0;
			foreach (var currentTierOfRootNodes in rootNodes)
			{
				tier++;

				// Order the root nodes at this tier randomly since they are weighted evenly.
				var roots = currentTierOfRootNodes.OrderBy(_ => random.Next()).ToList();

				// Try each root node.
				foreach (var rootNode in roots)
				{
					var depth = 0;

					// Set up a root search node at the root GraphNode.
					rootSearchNode = new SearchNode(
						rootNode,
						LinkCache.GetGraphLinks(expressedChart.StepEvents[0].LinkInstance, fallbacks),
						null,
						0.0,
						0.0,
						depth,
						null,
						new PerformanceFootAction[stepGraph.NumArrows],
						stepGraph,
						nps,
						random.NextDouble(),
						config,
						null,
						null);
					var currentSearchNodes = new HashSet<SearchNode> { rootSearchNode };

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

							rootGraphNodeToUse = rootNode;
							break;
						}

						// Failed to find a path. Break out and try the next root.
						if (currentSearchNodes.Count == 0)
						{
							furthestPosition = Math.Max(furthestPosition, expressedChart.StepEvents[depth].Position);
							break;
						}

						// Accumulate the next level of SearchNodes by looping over each SearchNode
						// in the current set.
						var nextDepth = depth + 1;
						var nextSearchNodes = new HashSet<SearchNode>();

						foreach (var searchNode in currentSearchNodes)
						{
							var deadEnd = true;
							var numLinks = searchNode.GraphLinks.Count;
							for (var l = 0; l < numLinks; l++)
							{
								var graphLink = searchNode.GraphLinks[l];
								var stepTypeCost = GetStepTypeCost(searchNode, l);

								// Special case handling for a blank link for a skipped step.
								if (graphLink.GraphLink.IsBlank())
								{
									// This next node is the same as the previous node since we skipped a step.
									var nextGraphNode = searchNode.GraphNode;
									// Similarly, the links out of this node haven't changed.
									var graphLinksToNextNode = searchNode.GraphLinks;
									// Blank steps involve no actions.
									var actions = new PerformanceFootAction[stepGraph.NumArrows];
									for (var a = 0; a < stepGraph.NumArrows; a++)
										actions[a] = PerformanceFootAction.None;

									// Set up the new SearchNode.
									var nextSearchNode = new SearchNode(
										nextGraphNode,
										graphLinksToNextNode,
										graphLink,
										stepTypeCost,
										expressedChart.StepEvents[depth].Time,
										nextDepth,
										searchNode,
										actions,
										stepGraph,
										nps,
										random.NextDouble(),
										config,
										null,
										null);

									// Hook up the new SearchNode and store it in the nextSearchNodes for pruning.
									if (!AddChildNode(searchNode, nextSearchNode, graphLink, nextSearchNodes, stepGraph,
										    expressedChart))
										continue;
									deadEnd = false;
								}
								else
								{
									// The GraphNode may not actually have this GraphLink due to
									// the StepTypeReplacements.
									if (!searchNode.GraphNode.Links.ContainsKey(graphLink.GraphLink))
										continue;

									// Check every GraphNode linked to by this GraphLink.
									var nextNodes = searchNode.GraphNode.Links[graphLink.GraphLink];
									for (var n = 0; n < nextNodes.Count; n++)
									{
										var nextGraphNode = nextNodes[n];

										// Determine new step information.
										var actions = GetActionsForNode(nextGraphNode, graphLink.GraphLink,
											stepGraph.NumArrows);

										// Set up the graph links leading out of this node to its next nodes.
										List<GraphLinkInstance> graphLinksToNextNode;
										if (nextDepth < expressedChart.StepEvents.Count)
										{
											var sourceLinkKey = expressedChart.StepEvents[nextDepth].LinkInstance;
											graphLinksToNextNode = LinkCache.GetGraphLinks(sourceLinkKey, fallbacks);
										}
										else
										{
											graphLinksToNextNode = new List<GraphLinkInstance>();
										}

										// Set up a new SearchNode.
										var nextSearchNode = new SearchNode(
											nextGraphNode,
											graphLinksToNextNode,
											graphLink,
											stepTypeCost,
											expressedChart.StepEvents[depth].Time,
											nextDepth,
											searchNode,
											actions,
											stepGraph,
											nps,
											random.NextDouble(),
											config,
											null,
											null);

										// Hook up the new SearchNode and store it in the nextSearchNodes for pruning.
										if (!AddChildNode(searchNode, nextSearchNode, graphLink, nextSearchNodes, stepGraph,
											    expressedChart))
											continue;
										deadEnd = false;
									}
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
				LogError($"Unable to create performance. Furthest position: {furthestPosition}", logIdentifier);
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
				GraphNodeInstance = new GraphNodeInstance { Node = rootGraphNodeToUse ?? rootNodes[0][0] },
			},
			logIdentifier);

		// Add the StepPerformanceNodes to the PerformedChart
		var currentPerformanceNode = performedChart.Root;
		var currentSearchNode = rootSearchNode;
		currentSearchNode = currentSearchNode?.GetNextNode();
		while (currentSearchNode != null)
		{
			if (currentSearchNode.IsBlank())
			{
				currentSearchNode = currentSearchNode.GetNextNode();
				continue;
			}

			// Create GraphNodeInstance.
			var graphLinkInstance = currentSearchNode.GraphLinkFromPreviousNode;
			var stepEventIndex = currentSearchNode.Depth - 1;
			var graphNodeInstance = new GraphNodeInstance { Node = currentSearchNode.GraphNode };
			for (var f = 0; f < NumFeet; f++)
				for (var p = 0; p < NumFootPortions; p++)
					graphNodeInstance.InstanceTypes[f, p] = graphLinkInstance.InstanceTypes[f, p];

			// Add new StepPerformanceNode and advance.
			var newNode = new StepPerformanceNode
			{
				Position = expressedChart.StepEvents[stepEventIndex].Position,
				GraphLinkInstance = graphLinkInstance,
				GraphNodeInstance = graphNodeInstance,
				Prev = currentPerformanceNode,
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

	/// <summary>
	/// Helper function when searching to add a new child SearchNode.
	/// Checks for whether this node would be invalid due to stepping on a previous release at the same position.
	/// Updates the parentNode's NextNodes links to include the new childNode.
	/// Updates nextSearchNodes to include the new child SearchNode for later pruning.
	/// </summary>
	private static bool AddChildNode(
		SearchNode parentNode,
		SearchNode childNode,
		GraphLinkInstance graphLink,
		HashSet<SearchNode> nextSearchNodes,
		StepGraph stepGraph,
		ExpressedChart.ExpressedChart expressedChart)
	{
		// Do not consider this next SearchNode if it results in an invalid state.
		if (DoesNodeStepOnReleaseAtSamePosition(childNode, expressedChart, stepGraph.NumArrows))
			return false;

		// Update the previous SearchNode's NextNodes to include the new SearchNode.
		if (!parentNode.NextNodes.ContainsKey(graphLink))
			parentNode.NextNodes[graphLink] = new HashSet<SearchNode>();
		parentNode.NextNodes[graphLink].Add(childNode);

		// Add this node to the set of next SearchNodes to be pruned after they are all found.
		nextSearchNodes.Add(childNode);
		return true;
	}

	/// <summary>
	/// Helper function when searching to get a child SearchNode's step
	/// </summary>
	/// <param name="parentNode"></param>
	/// <param name="graphLinkIndexToChild"></param>
	/// <returns></returns>
	private static double GetStepTypeCost(
		SearchNode parentNode,
		int graphLinkIndexToChild)
	{
		var numLinks = parentNode.GraphLinks.Count;
		var graphLinkToChild = parentNode.GraphLinks[graphLinkIndexToChild];

		if (graphLinkToChild.GraphLink.IsBlank())
			return BlankStepCost;

		// Determine the step type cost for this node.
		// Assumption that the first GraphLink is the source from which the fallbacks were derived.
		var sourceLink = parentNode.GraphLinks[0];
		var stepRemovalCost = 0.0;
		if (LinkCache.ContainsBlankLink(sourceLink, graphLinkToChild))
		{
			return BlankSingleStepCost;
		}

		// The first link out of this search node is the most preferred node. The
		// links at higher indexes are less preferred fallbacks that should cost more.
		if (numLinks == 1)
			return 0.0;

		var numStepsRemoved = LinkCache.GetNumStepsRemoved(parentNode.GraphLinks[0], graphLinkToChild);
		if (numStepsRemoved > 0)
		{
			stepRemovalCost += numStepsRemoved * IndividualDroppedArrowStepCost;
		}

		return stepRemovalCost + (double)graphLinkIndexToChild / (numLinks - 1);
	}

	/// <summary>
	/// Finds the notes per second of the entire Chart represented by the given ExpressedChart.
	/// </summary>
	/// <param name="expressedChart">ExpressedChart to find the notes per second of.</param>
	/// <returns>Notes per second of the Chart represented by the given ExpressedChart.</returns>
	private static double FindNPS(ExpressedChart.ExpressedChart expressedChart)
	{
		var nps = 0.0;
		var startTime = double.MaxValue;
		var endTime = 0.0;
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
						if (stepEvent.Time < startTime)
							startTime = stepEvent.Time;
						numSteps++;
						endTime = stepEvent.Time;
					}
				}
			}
		}

		if (endTime > startTime)
		{
			nps = numSteps / (endTime - startTime);
		}

		return nps;
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
	/// <param name="random">Random to use when needing to select a random lane.</param>
	private static void AddMinesToPerformedChart(
		PerformedChart performedChart,
		StepGraph stepGraph,
		ExpressedChart.ExpressedChart expressedChart,
		PerformanceNode lastPerformanceNode,
		Random random)
	{
		// Record which lanes have arrows in them.
		var numLanesWithArrows = 0;
		var lanesWithNoArrows = new bool[stepGraph.NumArrows];
		for (var a = 0; a < stepGraph.NumArrows; a++)
			lanesWithNoArrows[a] = true;

		var currentPerformanceNode = performedChart.Root;
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
		currentPerformanceNode = performedChart.Root;
		while (currentPerformanceNode != null)
		{
			if (currentPerformanceNode is StepPerformanceNode stepNode)
				stepEvents.Add(stepNode);
			currentPerformanceNode = currentPerformanceNode.Next;
		}

		var (releases, steps) = MineUtils.GetReleasesAndSteps(stepEvents, stepGraph.NumArrows);

		// Add the MinePerformanceNodes to the PerformedChart.
		// For simplicity add all the nodes to the end. They will be sorted later.
		var stepIndex = 0;
		var releaseIndex = 0;
		var previousMinePosition = -1;
		var arrowsOccupiedByMines = new bool[stepGraph.NumArrows];
		var randomLaneOrder = Enumerable.Range(0, stepGraph.NumArrows).OrderBy(_ => random.Next()).ToArray();
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
							Prev = lastPerformanceNode,
						};
						lastPerformanceNode.Next = newNode;
						lastPerformanceNode = newNode;

						arrowsOccupiedByMines[bestArrow] = true;
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
							Prev = lastPerformanceNode,
						};
						lastPerformanceNode.Next = newNode;
						lastPerformanceNode = newNode;

						arrowsOccupiedByMines[firstLaneWithNoArrow] = true;
					}

					break;
				}
			}
		}
	}

	#endregion Creation From ExpressedChart

	#region Creation From PatternConfig

	public static PerformedChart CreateWithPattern(
		StepGraph stepGraph,
		PatternConfig patternConfig,
		Config config,
		int startPosition,
		int endPosition,
		bool endPositionInclusive,
		int randomSeed,
		int previousStepFoot,
		double previousStepTime,
		int[] previousFooting,
		int[] followingFooting,
		int[] currentLaneCounts,
		IReadOnlyList<Event> currentEvents,
		string logIdentifier)
	{
		var random = new Random(randomSeed);

		const double stepTypeFallbackCost = 0.0;

		// Determine the times and positions of all events to generate.
		// This depends on the currently present rate altering events.
		// Here we intentionally pad the end position out to generate two extra steps.
		// This allows us to easily ensure that the pattern ends at a position that can step
		// to the specified following footing using normal tightening rules.
		var extendedEndPosition = endPosition + SMCommon.MaxValidDenominator / patternConfig.BeatSubDivision * NumFeet;
		var timingData = DeterminePatternTiming(patternConfig, currentEvents, startPosition, extendedEndPosition,
			endPositionInclusive);
		if (timingData == null || timingData.Length <= NumFeet)
		{
			var endInclusiveExclusiveString = endPositionInclusive ? "inclusive" : "exclusive";
			LogError(
				$"Range from {startPosition} to {endPosition} ({endInclusiveExclusiveString}) is not large enough to generate steps.",
				logIdentifier);
			return null;
		}

		// Determine the NPS now that we know the timing data.
		var nps = FindNPS(currentEvents, timingData);

		// Get the starting position.
		var rootLeft = patternConfig.LeftFootStartLaneSpecified;
		var rootRight = patternConfig.RightFootStartLaneSpecified;
		var firstStepTypeLeft = StepType.SameArrow;
		var firstStepTypeRight = StepType.SameArrow;
		switch (patternConfig.LeftFootStartChoice)
		{
			case PatternConfigStartFootChoice.SpecifiedLane:
				// If the specified foot is not valid, fall back to the previous footing.
				if (rootLeft < 0 || (rootLeft >= stepGraph.NumArrows
				                     && previousFooting[L] >= 0 && previousFooting[L] < stepGraph.NumArrows))
				{
					rootLeft = previousFooting[L];
				}

				break;
			case PatternConfigStartFootChoice.AutomaticSameLane:
				rootLeft = previousFooting[L];
				break;
			case PatternConfigStartFootChoice.AutomaticNewLane:
				rootLeft = previousFooting[L];
				firstStepTypeLeft = StepType.NewArrow;
				break;
		}

		switch (patternConfig.RightFootStartChoice)
		{
			case PatternConfigStartFootChoice.SpecifiedLane:
				// If the specified foot is not valid, fall back to the previous footing.
				if (rootRight < 0 || (rootRight >= stepGraph.NumArrows
				                      && previousFooting[R] >= 0 && previousFooting[R] < stepGraph.NumArrows))
				{
					rootRight = previousFooting[R];
				}

				break;
			case PatternConfigStartFootChoice.AutomaticSameLane:
				rootRight = previousFooting[R];
				break;
			case PatternConfigStartFootChoice.AutomaticNewLane:
				rootRight = previousFooting[R];
				firstStepTypeRight = StepType.NewArrow;
				break;
		}

		var rootGraphNode = stepGraph.FindGraphNode(rootLeft, GraphArrowState.Resting, rootRight, GraphArrowState.Resting);
		if (rootGraphNode == null)
		{
			LogError($"Could not find starting node for left foot on {rootLeft} and right foot on {rootRight}.", logIdentifier);
			return null;
		}

		var root = new StepPerformanceNode
		{
			Position = 0,
			GraphNodeInstance = new GraphNodeInstance { Node = rootGraphNode },
		};
		var performedChart = new PerformedChart(stepGraph.NumArrows, root, logIdentifier);

		// Get the starting foot to start on.
		var foot = patternConfig.StartingFootSpecified;
		switch (patternConfig.StartingFootChoice)
		{
			case PatternConfigStartingFootChoice.Random:
			{
				foot = random.NextDouble() < 0.5 ? L : R;
				break;
			}
			case PatternConfigStartingFootChoice.Automatic:
			{
				foot = OtherFoot(previousStepFoot);
				break;
			}
		}

		var firstStepType = foot == L ? firstStepTypeLeft : firstStepTypeRight;
		var secondStepType = foot == L ? firstStepTypeRight : firstStepTypeLeft;

		var depth = 0;

		// Set up a root search node at the root GraphNode.
		var possibleGraphLinksToNextNode = new List<GraphLinkInstance>();
		var rootGraphLink = new GraphLink
		{
			Links =
			{
				[foot, DefaultFootPortion] = new GraphLink.FootArrowState(firstStepType, FootAction.Tap),
			},
		};
		possibleGraphLinksToNextNode.Add(new GraphLinkInstance(rootGraphLink));

		var rootSearchNode = new SearchNode(
			rootGraphNode,
			possibleGraphLinksToNextNode,
			null,
			stepTypeFallbackCost,
			previousStepTime,
			depth,
			null,
			new PerformanceFootAction[stepGraph.NumArrows],
			stepGraph,
			nps,
			random.NextDouble(),
			config,
			patternConfig,
			currentLaneCounts);

		var currentSearchNodes = new HashSet<SearchNode> { rootSearchNode };
		var numSameArrowSteps = new int[NumFeet];

		foreach (var timingInfo in timingData)
		{
			var timeSeconds = timingInfo.Item1;

			// Failed to find a path.
			if (currentSearchNodes.Count == 0)
			{
				performedChart.LogError("Failed to find path.");
				break;
			}

			// Accumulate the next level of SearchNodes by looping over each SearchNode
			// in the current set.
			var nextDepth = depth + 1;
			foot = OtherFoot(foot);
			var nextSearchNodes = new HashSet<SearchNode>();

			// Determine the StepType to use.
			var stepType = secondStepType;
			if (depth > 1)
			{
				// Check to see if this step is one of the final steps which intentionally
				// goes beyond the last step in the pattern to aid with ensuring the steps
				// end in a way which can step to the following footing.
				if (depth == timingData.Length - 3 || depth == timingData.Length - 2)
				{
					if (foot == L)
					{
						switch (patternConfig.LeftFootEndChoice)
						{
							case PatternConfigEndFootChoice.AutomaticNewLaneToFollowing:
								stepType = StepType.NewArrow;
								break;
							case PatternConfigEndFootChoice.AutomaticSameLaneToFollowing:
								stepType = StepType.SameArrow;
								break;
							case PatternConfigEndFootChoice.AutomaticIgnoreFollowingSteps:
							case PatternConfigEndFootChoice.AutomaticSameOrNewLaneAsFollowing:
								stepType = random.NextDouble() > patternConfig.NewArrowStepWeightNormalized
									? StepType.SameArrow
									: StepType.NewArrow;
								break;
						}
					}
					else
					{
						switch (patternConfig.RightFootEndChoice)
						{
							case PatternConfigEndFootChoice.AutomaticNewLaneToFollowing:
								stepType = StepType.NewArrow;
								break;
							case PatternConfigEndFootChoice.AutomaticSameLaneToFollowing:
								stepType = StepType.SameArrow;
								break;
							case PatternConfigEndFootChoice.AutomaticIgnoreFollowingSteps:
							case PatternConfigEndFootChoice.AutomaticSameOrNewLaneAsFollowing:
								stepType = random.NextDouble() > patternConfig.NewArrowStepWeightNormalized
									? StepType.SameArrow
									: StepType.NewArrow;
								break;
						}
					}
				}
				else
				{
					stepType = random.NextDouble() > patternConfig.NewArrowStepWeightNormalized
						? StepType.SameArrow
						: StepType.NewArrow;

					if (stepType == StepType.SameArrow)
					{
						numSameArrowSteps[foot]++;
						if (numSameArrowSteps[foot] >= patternConfig.MaxSameArrowsInARowPerFoot)
						{
							stepType = StepType.NewArrow;
							numSameArrowSteps[foot] = 0;
						}
					}
				}
			}

			// Set up the graph links leading out of this node to its next nodes.
			foreach (var searchNode in currentSearchNodes)
			{
				// Check every GraphLink out of the SearchNode.
				var deadEnd = true;
				for (var l = 0; l < searchNode.GraphLinks.Count; l++)
				{
					var graphLink = searchNode.GraphLinks[l];
					// The GraphNode may not actually have this GraphLink due to
					// the StepTypeReplacements.
					if (!searchNode.GraphNode.Links.ContainsKey(graphLink.GraphLink))
						continue;

					// Check every GraphNode linked to by this GraphLink.
					var nextNodes = searchNode.GraphNode.Links[graphLink.GraphLink];
					for (var n = 0; n < nextNodes.Count; n++)
					{
						var nextGraphNode = nextNodes[n];

						// Determine new step information.
						var actions = GetActionsForNode(nextGraphNode, graphLink.GraphLink,
							stepGraph.NumArrows);

						// Set up links to the next node.
						possibleGraphLinksToNextNode = new List<GraphLinkInstance>();
						var link = new GraphLink
						{
							Links =
							{
								[foot, DefaultFootPortion] = new GraphLink.FootArrowState(stepType, FootAction.Tap),
							},
						};
						possibleGraphLinksToNextNode.Add(new GraphLinkInstance(link));

						// Set up a new SearchNode.
						var nextSearchNode = new SearchNode(
							nextGraphNode,
							possibleGraphLinksToNextNode,
							graphLink,
							stepTypeFallbackCost,
							timeSeconds,
							nextDepth,
							searchNode,
							actions,
							stepGraph,
							nps,
							random.NextDouble(),
							config,
							patternConfig,
							null
						);

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

		// Finished
		{
			// Filter the set of current nodes to ones which end at acceptable positions.
			RemoveNodesEndingInUnwantedLocations(stepGraph, ref currentSearchNodes, patternConfig, followingFooting);
			if (currentSearchNodes.Count == 0)
			{
				performedChart.LogError("Failed to find path ending at desired location.");
				return null;
			}

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

			// Remove the last two nodes which were placeholders to ensure the pattern ends at
			// the correct location.
			var nodesToRemove = NumFeet;
			while (nodesToRemove > 0)
			{
				if (bestNode.PreviousNode == null)
					break;
				bestNode.RemoveFromPreviousNode();
				bestNode = bestNode.PreviousNode;
				nodesToRemove--;
			}

			//previousSectionEnd = endPosition;
			//previousSectionLastL = bestNode.GraphNode.State[L, DefaultFootPortion].Arrow;
			//previousSectionLastR = bestNode.GraphNode.State[R, DefaultFootPortion].Arrow;
			//previousSectionLastFoot = OtherFoot(foot);
		}

		// Add the StepPerformanceNodes to the PerformedChart
		var currentPerformanceNode = root;
		var currentSearchNode = rootSearchNode;
		currentSearchNode = currentSearchNode?.GetNextNode();
		var index = 0;
		while (currentSearchNode != null)
		{
			// Create GraphNodeInstance.
			var graphNodeInstance = new GraphNodeInstance { Node = currentSearchNode.GraphNode };
			var graphLinkInstance = currentSearchNode.GraphLinkFromPreviousNode;
			for (var f = 0; f < NumFeet; f++)
				for (var p = 0; p < NumFootPortions; p++)
					graphNodeInstance.InstanceTypes[f, p] = graphLinkInstance.InstanceTypes[f, p];

			// Add new StepPerformanceNode and advance.
			var newNode = new StepPerformanceNode
			{
				Position = timingData[index].Item2,
				GraphLinkInstance = graphLinkInstance,
				GraphNodeInstance = graphNodeInstance,
				Prev = currentPerformanceNode,
			};
			currentPerformanceNode.Next = newNode;
			currentPerformanceNode = newNode;
			currentSearchNode = currentSearchNode.GetNextNode();
			index++;
		}

		return performedChart;
	}

	/// <summary>
	/// Given a set of possible final nodes for pattern generation, remove nodes which end
	/// in unwanted ending locations based on the rules in the given PatternConfig.
	/// </summary>
	/// <param name="stepGraph">StepGraph to use.</param>
	/// <param name="currentSearchNodes">Set of possible ending SearchNodes to remove nodes from.</param>
	/// <param name="patternConfig">PatternConfig with rules for how the pattern should end.</param>
	/// <param name="followingFooting">Array of lanes per foot representing the footing of steps following this pattern.</param>
	private static void RemoveNodesEndingInUnwantedLocations(
		StepGraph stepGraph,
		ref HashSet<SearchNode> currentSearchNodes,
		PatternConfig patternConfig,
		int[] followingFooting)
	{
		var leftFinalStep = followingFooting[L];
		if (patternConfig.LeftFootEndChoice == PatternConfigEndFootChoice.SpecifiedLane)
			leftFinalStep = patternConfig.LeftFootEndLaneSpecified;
		var rightFinalStep = followingFooting[R];
		if (patternConfig.RightFootEndChoice == PatternConfigEndFootChoice.SpecifiedLane)
			rightFinalStep = patternConfig.RightFootEndLaneSpecified;

		// Filter the currentSearchNodes if the end choice is set to end on a specified lane.
		for (var f = 0; f < NumFeet; f++)
		{
			var lane = f == L ? leftFinalStep : rightFinalStep;
			if (lane < 0 || lane >= stepGraph.NumArrows)
				continue;
			var specifiedRemainingNodes = new HashSet<SearchNode>();
			foreach (var node in currentSearchNodes)
			{
				if (node.GraphNode.State[f, DefaultFootPortion].Arrow == lane)
				{
					specifiedRemainingNodes.Add(node);
					continue;
				}

				Prune(node);
			}

			currentSearchNodes = specifiedRemainingNodes;
		}
	}

	/// <summary>
	/// Determines the time in seconds and integer position of all events to generate for a PatternConfig.
	/// </summary>
	/// <param name="patternConfig">PatternConfig for generating steps.</param>
	/// <param name="chartEvents">All Events currently in the Chart.</param>
	/// <param name="startPosition">Starting IntegerPosition of pattern to generate.</param>
	/// <param name="endPosition">Ending IntegerPosition of pattern to generate.</param>
	/// <param name="endPositionInclusive">Whether or not the endPosition is inclusive.</param>
	/// <returns>Array of Tuples of times in seconds and integer positions of all steps to generate.</returns>
	private static Tuple<double, int>[] DeterminePatternTiming(
		PatternConfig patternConfig,
		IReadOnlyList<Event> chartEvents,
		int startPosition,
		int endPosition,
		bool endPositionInclusive)
	{
		var patternEvents = new List<Event>();

		// Add PatternEvents for the steps to be added with correct IntegerPositions.
		var numEvents = 0;
		var pos = startPosition;
		while (endPositionInclusive ? pos <= endPosition : pos < endPosition)
		{
			numEvents++;
			patternEvents.Add(new Pattern
			{
				IntegerPosition = pos,
			});
			pos += SMCommon.MaxValidDenominator / patternConfig.BeatSubDivision;
		}

		if (numEvents == 0)
			return null;

		// Clone all events which affect timing.
		foreach (var existingEvent in chartEvents)
		{
			if (SMCommon.DoesEventAffectTiming(existingEvent))
				patternEvents.Add(existingEvent.Clone());
		}

		// Sort all events.
		patternEvents.Sort(new SMCommon.SMEventComparer(EventOrder.Order));

		// Set the time on the pattern events.
		SMCommon.SetEventTimeAndMetricPositionsFromRows(patternEvents);

		// Now that the times are set, copy them to the data to return.
		var timingData = new Tuple<double, int>[numEvents];
		var index = 0;
		foreach (var patternEvent in patternEvents)
		{
			if (patternEvent is not Pattern)
				continue;
			timingData[index] = new Tuple<double, int>(patternEvent.TimeSeconds, patternEvent.IntegerPosition);
			index++;
		}

		return timingData;
	}

	/// <summary>
	/// Finds the notes per second of the entire Chart represented by the given List of Events and the
	/// timing data to be used to generate a pattern.
	/// </summary>
	/// <param name="currentEvents">Current Events.</param>
	/// <param name="timingData">Timing data for new events to generate.</param>
	/// <returns>Notes per second of the steps represented by the given parameters.</returns>
	private static double FindNPS(IReadOnlyList<Event> currentEvents, Tuple<double, int>[] timingData)
	{
		var nps = 0.0;
		var startTime = double.MaxValue;
		var endTime = 0.0;
		var numSteps = 0;

		// Consider steps from the current Events.
		foreach (var chartEvent in currentEvents)
		{
			switch (chartEvent)
			{
				case LaneTapNote when chartEvent.SourceType == SMCommon.NoteChars[(int)SMCommon.NoteType.Mine].ToString():
					continue;
				case LaneTapNote:
				case LaneHoldStartNote:
					break;
				default:
					continue;
			}

			startTime = Math.Min(startTime, chartEvent.TimeSeconds);
			endTime = Math.Max(endTime, chartEvent.TimeSeconds);
			numSteps++;
		}

		// Consider steps from the timing data.
		if (timingData.Length > 0)
		{
			numSteps += timingData.Length;
			endTime = Math.Max(endTime, timingData[^1].Item1);
		}

		if (endTime > startTime)
		{
			nps = numSteps / (endTime - startTime);
		}

		return nps;
	}

	#endregion Creation From PatternConfig

	#region Pruning

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
			node.RemoveFromPreviousNode();
			if (node.PreviousNode.NextNodes.Count != 0)
				break;
			node = node.PreviousNode;
		}
	}

	#endregion Pruning

	#region Helpers

	/// <summary>
	/// Returns whether or not the given PerformanceFootAction represents a step.
	/// </summary>
	/// <param name="footAction">PerformanceFootAction to check.</param>
	/// <returns>True if footAction represents a step and false otherwise.</returns>
	public static bool IsStep(PerformanceFootAction footAction)
	{
		return footAction is PerformanceFootAction.Tap or PerformanceFootAction.Hold or PerformanceFootAction.Roll;
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
	private static bool DoesNodeStepOnReleaseAtSamePosition(SearchNode node, ExpressedChart.ExpressedChart expressedChart,
		int numArrows)
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

	#endregion Helpers

	#region StepMania Event Generation

	/// <summary>
	/// Creates a List of Events representing the Events of an SM Chart.
	/// </summary>
	/// <returns>List of Events that represent the Events of a SM Chart.</returns>
	public List<Event> CreateSMChartEvents()
	{
		var events = new List<Event>();

		var currentNode = Root;
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
						{
							events.Add(new LaneHoldEndNote
							{
								IntegerPosition = stepNode.Position,
								Lane = arrow,
								Player = 0,
								SourceType = SMCommon.NoteChars[(int)SMCommon.NoteType.HoldEnd].ToString(),
							});
							break;
						}
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
								SourceType = SMCommon.NoteChars[(int)instanceAction].ToString(),
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
								SourceType = SMCommon.NoteChars[(int)holdRollType].ToString(),
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
					SourceType = SMCommon.NoteChars[(int)SMCommon.NoteType.Mine].ToString(),
				});
			}

			// Advance
			currentNode = currentNode.Next;
		}

		return events;
	}

	#endregion StepMania Event Generation

	#region Logging

	// ReSharper disable UnusedMember.Local
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
	// ReSharper restore UnusedMember.Local

	#endregion Logging
}
