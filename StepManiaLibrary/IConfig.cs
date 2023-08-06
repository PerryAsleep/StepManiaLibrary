namespace StepManiaLibrary;

/// <summary>
/// Interface for StepManiaLibrary configuration data.
/// Expected Usage:
///  Deserialize from json or instantiate as needed.
///  Call Init to perform needed initialization after loading and after SetAsOverrideOf.
///  Call Validate after Init to perform validation.
/// </summary>
public interface IConfig<out T>
{
	/// <summary>
	/// Perform any post-load initialization on this config object.
	/// </summary>
	public void Init();

	/// <summary>
	/// Log errors if any values are not valid and return whether or not there are errors.
	/// </summary>
	/// <param name="logId">Identifier for logging.</param>
	/// <returns>True if no errors were found and false otherwise.</returns>
	public bool Validate(string logId = null);

	/// <summary>
	/// Returns a new config that is a clone of this config.
	/// </summary>
	public T Clone();
}
