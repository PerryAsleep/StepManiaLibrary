using System;
using System.Text.Json.Serialization;
using Fumen;

namespace StepManiaLibrary.PerformedChart;

/// <summary>
/// Configuration for tightening lateral body movement.
/// See PerformedChart.md for more information.
/// </summary>
public class LateralTighteningConfig : StepManiaLibrary.Config, IEquatable<LateralTighteningConfig>
{
	/// <summary>
	/// Tag for logging messages.
	/// </summary>
	private const string LogTag = "LateralTighteningConfig";

	/// <summary>
	/// Whether or not to use this LateralTighteningConfig.
	/// </summary>
	[JsonInclude]
	public bool? Enabled
	{
		get => EnabledInternal;
		set
		{
			EnabledInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private bool? EnabledInternal;

	/// <summary>
	/// The relative notes per second over which patterns should cost more.
	/// </summary>
	[JsonInclude]
	public double RelativeNPS
	{
		get => RelativeNPSInternal;
		set
		{
			RelativeNPSInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private double RelativeNPSInternal = -1.0;

	/// <summary>
	/// The absolute notes per second over which patterns should cost more.
	/// </summary>
	[JsonInclude]
	public double AbsoluteNPS
	{
		get => AbsoluteNPSInternal;
		set
		{
			AbsoluteNPSInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private double AbsoluteNPSInternal = -1.0;

	/// <summary>
	/// The lateral body speed in arrows per second over which patterns should cost more.
	/// </summary>
	[JsonInclude]
	public double Speed
	{
		get => SpeedInternal;
		set
		{
			SpeedInternal = value;
			Notify(NotificationConfigChanged, this);
		}
	}

	private double SpeedInternal = -1.0;

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

	public bool IsEnabled()
	{
		if (Enabled == null)
			return false;
		return Enabled.Value;
	}

	#region Config

	/// <summary>
	/// Returns a new LateralTighteningConfig that is a clone of this LateralTighteningConfig.
	/// </summary>
	public override LateralTighteningConfig Clone()
	{
		return new LateralTighteningConfig
		{
			Enabled = Enabled,
			RelativeNPS = RelativeNPS,
			AbsoluteNPS = AbsoluteNPS,
			Speed = Speed,
		};
	}

	public override void Init()
	{
		// No initialization required.
	}

	/// <summary>
	/// Log errors if any values are not valid and return whether or not there are errors.
	/// </summary>
	/// <param name="logId">Identifier for logging.</param>
	/// <returns>True if no errors were found and false otherwise.</returns>
	public override bool Validate(string logId = null)
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

	#endregion Config

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
