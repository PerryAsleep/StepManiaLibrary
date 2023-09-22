namespace Fumen.ChartDefinition;

/// <summary>
/// Event representing a Pattern to use for sorting.
/// </summary>
public class Pattern : Event
{
	public Pattern()
	{
	}

	public Pattern(Pattern other)
		: base(other)
	{
	}

	public override Pattern Clone()
	{
		return new Pattern(this);
	}
}
