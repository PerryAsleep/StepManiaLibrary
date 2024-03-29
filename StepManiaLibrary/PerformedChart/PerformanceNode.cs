﻿namespace StepManiaLibrary.PerformedChart;

/// <summary>
/// A PerformedChart contains a series of PerformanceNodes.
/// Abstract base class for the various types of PerformanceNodes in a PerformedChart.
/// </summary>
public abstract class PerformanceNode
{
	/// <summary>
	/// Constructor.
	/// </summary>
	/// <param name="position">Position of this node.</param>
	/// <param name="time">Time in seconds of this node.</param>
	protected PerformanceNode(int position, double time)
	{
		Position = position;
		Time = time;
	}

	/// <summary>
	/// IntegerPosition of this node in the Chart.
	/// </summary>
	public readonly int Position;

	/// <summary>
	/// Time in seconds of this node in the Chart.
	/// </summary>
	public readonly double Time;

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
	/// Constructor.
	/// </summary>
	/// <param name="position">Position of this node.</param>
	/// <param name="time">Time in seconds of this node.</param>
	public StepPerformanceNode(int position, double time)
		: base(position, time)
	{
	}

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
	/// Constructor.
	/// </summary>
	/// <param name="position">Position of this node.</param>
	/// <param name="time">Time in seconds of this node.</param>
	/// <param name="arrow">Lane or arrow this Mine occurs on.</param>
	public MinePerformanceNode(int position, double time, int arrow)
		: base(position, time)
	{
		Arrow = arrow;
	}

	/// <summary>
	/// The lane or arrow this Mine occurs on.
	/// </summary>
	public readonly int Arrow;
}
