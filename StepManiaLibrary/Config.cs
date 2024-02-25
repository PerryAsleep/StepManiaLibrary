namespace StepManiaLibrary;

/// <summary>
/// Config object abstract base class.
/// Configs are objects deserialized from json into runtime data.
/// They are mutable at runtime.
/// They have common functionality captured by this class:
///  - They are Notifiers and notify Observers on changes.
///  - They may need to initialize additional runtime state through the Init method.
///  - They perform validation post-load through the Validate method.
///  - They are cloneable and have a deep Clone method.
/// </summary>
public abstract class Config : Fumen.Notifier<Config>
{
	public const string NotificationConfigChanged = "ConfigChanged";

	/// <summary>
	/// Perform any post-load initialization on this config object.
	/// </summary>
	public abstract void Init();

	/// <summary>
	/// Log errors if any values are not valid and return whether or not there are errors.
	/// </summary>
	/// <param name="logId">Identifier for logging.</param>
	/// <returns>True if no errors were found and false otherwise.</returns>
	public abstract bool Validate(string logId = null);

	/// <summary>
	/// Returns a new config that is a clone of this config.
	/// </summary>
	public abstract Config Clone();
}
