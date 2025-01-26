using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Fumen;
using static Fumen.Converters.SMCommon;

namespace StepManiaLibrary.PerformedChart;

/// <summary>
/// Configuration data for PerformedChart behavior.
/// Expected Usage:
///  Deserialize from json or instantiate as needed.
///  For overrides, call SetAsOverrideOf before Init and Validate.
///  Call Init to perform needed initialization after loading and after SetAsOverrideOf.
///  Call Validate after Init to perform validation.
/// </summary>
public class Config : StepManiaLibrary.Config,
	IEquatable<Config>,
	Fumen.IObserver<StepManiaLibrary.Config>
{
	/// <summary>
	/// Tag for logging messages.
	/// </summary>
	private const string LogTag = "PerformedChartConfig";

	/// <summary>
	/// TransitionConfig.
	/// </summary>
	[JsonInclude]
	public TransitionConfig Transitions
	{
		get => TransitionConfigInternal;
		set
		{
			if (ReferenceEquals(TransitionConfigInternal, value))
				return;
			TransitionConfigInternal?.RemoveObserver(this);
			TransitionConfigInternal = value;
			TransitionConfigInternal?.AddObserver(this);
			Notify(NotificationConfigChanged, this);
		}
	}

	private TransitionConfig TransitionConfigInternal = new();

	/// <summary>
	/// FacingConfig.
	/// </summary>
	[JsonInclude]
	public FacingConfig Facing
	{
		get => FacingInternal;
		set
		{
			if (ReferenceEquals(FacingInternal, value))
				return;
			FacingInternal?.RemoveObserver(this);
			FacingInternal = value;
			FacingInternal?.AddObserver(this);
			Notify(NotificationConfigChanged, this);
		}
	}

	private FacingConfig FacingInternal = new();

	/// <summary>
	/// LateralTighteningConfig.
	/// </summary>
	[JsonInclude]
	public LateralTighteningConfig LateralTightening
	{
		get => LateralTighteningInternal;
		set
		{
			if (ReferenceEquals(LateralTighteningInternal, value))
				return;
			LateralTighteningInternal?.RemoveObserver(this);
			LateralTighteningInternal = value;
			LateralTighteningInternal?.AddObserver(this);
			Notify(NotificationConfigChanged, this);
		}
	}

	private LateralTighteningConfig LateralTighteningInternal = new();

	/// <summary>
	/// StepTighteningConfig.
	/// </summary>
	[JsonInclude]
	public StepTighteningConfig StepTightening
	{
		get => StepTighteningInternal;
		set
		{
			if (ReferenceEquals(StepTighteningInternal, value))
				return;
			StepTighteningInternal?.RemoveObserver(this);
			StepTighteningInternal = value;
			StepTighteningInternal?.AddObserver(this);
			Notify(NotificationConfigChanged, this);
		}
	}

	private StepTighteningConfig StepTighteningInternal = new();

	/// <summary>
	/// Dictionary of StepMania StepsType to a List of integers representing weights
	/// for each lane. When generating a PerformedChart we should try to match these weights
	/// for distributing arrows.
	/// </summary>
	[JsonInclude]
	public Dictionary<string, List<int>> ArrowWeights
	{
		get => ArrowWeightsInternal;
		set
		{
			ArrowWeightsInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private Dictionary<string, List<int>> ArrowWeightsInternal = new();

	/// <summary>
	/// Normalized ArrowWeights.
	/// Values sum to 1.0.
	/// </summary>
	[JsonIgnore] private Dictionary<string, List<double>> ArrowWeightsNormalized;

	/// <summary>
	/// Constructor.
	/// </summary>
	public Config()
	{
		Transitions.AddObserver(this);
		Facing.AddObserver(this);
		LateralTightening.AddObserver(this);
		StepTightening.AddObserver(this);
	}

	/// <summary>
	/// Sets this Config to be an override of the given other Config.
	/// Any values in this Config which are at their default, invalid values will
	/// be replaced with the corresponding values in the given other Config.
	/// Called before Init and Validate.
	/// </summary>
	/// <param name="other">Other Config to use as a base.</param>
	public void SetAsOverrideOf(Config other)
	{
		LateralTightening.SetAsOverrideOf(other.LateralTightening);
		StepTightening.SetAsOverrideOf(other.StepTightening);
		Facing.SetAsOverrideOf(other.Facing);

		foreach (var kvp in other.ArrowWeights)
		{
			if (!ArrowWeights.ContainsKey(kvp.Key))
			{
				ArrowWeights.Add(kvp.Key, [..kvp.Value]);
			}
		}

		Notify(NotificationConfigChanged, this);
	}

	#region Config

	/// <summary>
	/// Returns a new Config that is a clone of this Config.
	/// </summary>
	public override Config Clone()
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
	public override void Init()
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
	public override bool Validate(string logId = null)
	{
		var errors = !LateralTightening.Validate(logId);
		errors = !StepTightening.Validate(logId) || errors;
		errors = !Facing.Validate(logId) || errors;
		errors = !Transitions.Validate(logId) || errors;
		return !errors;
	}

	#endregion Config

	/// <summary>
	/// Sets the arrow weight for a given lane of a given ChartType.
	/// Will update normalized weights.
	/// </summary>
	/// <param name="chartType">ChartType to set the arrow weight for.</param>
	/// <param name="laneIndex">Lane index to set the arrow weight for.</param>
	/// <param name="weight">New weight</param>
	public void SetArrowWeight(ChartType chartType, int laneIndex, int weight)
	{
		var chartTypeString = ChartTypeString(chartType);
		if (!ArrowWeights.TryGetValue(chartTypeString, out var weights))
			return;
		if (laneIndex < 0 || laneIndex >= weights.Count)
			return;
		if (weights[laneIndex] == weight)
			return;
		weights[laneIndex] = weight;
		RefreshArrowWeightsNormalized(chartTypeString);
		Notify(NotificationConfigChanged, this);
	}

	/// <summary>
	/// Gets the desired arrow weights for the given chart type.
	/// Values normalized to sum to 1.0.
	/// </summary>
	/// <returns>List of normalized weights.</returns>
	public IReadOnlyList<double> GetArrowWeightsNormalized(string chartType)
	{
		if (ArrowWeightsNormalized.TryGetValue(chartType, out var weights))
			return weights;
		return new List<double>();
	}

	/// <summary>
	/// Refreshes the normalized arrow weights from their non-normalized values.
	/// </summary>
	private void RefreshArrowWeightsNormalized(string chartTypeString)
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
		ChartType smChartType,
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
			var expectedNumArrows = GetChartProperties(smChartType).GetNumInputs();
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

	#region IObserver

	/// <summary>
	/// Notification handler for the sub-object Configs of this Config.
	/// </summary>
	public void OnNotify(string eventId, StepManiaLibrary.Config notifier, object payload)
	{
		// Bubble up the notification to this Config's Observers.
		Notify(NotificationConfigChanged, this);
	}

	#endregion IObserver

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
			if (!other.ArrowWeights.TryGetValue(key, out var otherWeights))
				return false;
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
			if (!other.ArrowWeightsNormalized.TryGetValue(key, out var otherWeights))
				return false;
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
