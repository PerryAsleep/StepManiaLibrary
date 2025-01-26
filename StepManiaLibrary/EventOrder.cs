using System.Collections.Generic;
using Fumen.ChartDefinition;
using Fumen.Converters;

namespace StepManiaLibrary;

/// <summary>
/// Class for providing an order to use for comparing Events with an SMEventComparer.
/// Includes Fumen Event types, and also StepManiaLibrary custom types, like Pattern.
/// </summary>
public class EventOrder
{
	/// <summary>
	/// Event order including custom StepManiaLibrary types.
	/// </summary>
	public static readonly List<string> Order;

	/// <summary>
	/// Static constructor to build the custom event list.
	/// </summary>
	static EventOrder()
	{
		Order = [];
		var defaultEventOrder = SMCommon.SMEventComparer.SMEventOrderList;
		foreach (var eventString in defaultEventOrder)
		{
			Order.Add(eventString);
			if (eventString == nameof(LaneTapNote))
			{
				Order.Add(nameof(Pattern));
			}
		}
	}
}
