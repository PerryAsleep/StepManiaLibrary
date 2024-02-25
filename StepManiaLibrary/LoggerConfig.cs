using System.Text.Json.Serialization;
using Fumen;

namespace StepManiaLibrary;

public class LoggerConfig
{
	/// <summary>
	/// Tag for logging messages.
	/// </summary>
	private const string LogTag = "LoggerConfig";

	/// <summary>
	/// Logging log level.
	/// </summary>
	[JsonInclude] public LogLevel LogLevel = LogLevel.Info;

	/// <summary>
	/// Whether or not log to a file.
	/// </summary>
	[JsonInclude] public bool LogToFile;

	/// <summary>
	/// Directory to use for writing the log file.
	/// </summary>
	[JsonInclude] public string LogDirectory;

	/// <summary>
	/// Interval in seconds after which to flush the log file to disk.
	/// </summary>
	[JsonInclude] public int LogFlushIntervalSeconds;

	/// <summary>
	/// Log buffer size in bytes. When full the buffer log will flush to disk.
	/// </summary>
	[JsonInclude] public int LogBufferSizeBytes;

	/// <summary>
	/// Whether or not to log to the console.
	/// </summary>
	[JsonInclude] public bool LogToConsole;

	public bool Validate()
	{
		var errors = false;

		if (LogToFile)
		{
			if (string.IsNullOrEmpty(LogDirectory))
			{
				LogError("LogToFile is true, but no LogDirectory specified.");
				errors = true;
			}

			if (LogBufferSizeBytes <= 0)
			{
				LogError("Expected a non-negative LogBufferSizeBytes.");
				errors = true;
			}
		}

		return !errors;
	}

	#region Logging

	private static void LogError(string message)
	{
		Logger.Error($"[{LogTag}] {message}");
	}

	#endregion Logging
}
