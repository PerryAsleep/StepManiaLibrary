using System.Diagnostics;
using System.Globalization;
using System.Text;
using Fumen;
using Fumen.ChartDefinition;
using Fumen.Converters;
using static StepManiaLibrary.TempoDetector;
using Path = System.IO.Path;
using Thread = System.Threading.Thread;

/// <summary>
/// Application for testing tempo detection on many songs.
/// </summary>
public class Program
{
	private const string LogTag = "Main";
	private const string SongPath = "C:\\Games\\StepMania 5\\Songs\\TempoTest";
	private const int NumTemposPerSongToFind = 5;
	private static readonly string ExecutionTimeString = DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss");

	private static readonly List<FileFormatType> SupportedFileFormats = new()
		{ FileFormatType.SM, FileFormatType.SSC };

	private static SoundManager SoundManager;

	/// <summary>
	/// Class for holding arguments for an individual song being processed and for
	/// managing the task to convert that song's charts.
	/// </summary>
	private class SongTaskData
	{
		/// <summary>
		/// FileInfo for the Song file.
		/// </summary>
		public readonly FileInfo FileInfo;

		/// <summary>
		/// String path of directory containing the Song file.
		/// </summary>
		public readonly string SongDir;

		public readonly string PackDir;

		/// <summary>
		/// String path to the Song file relative to the SongPath.
		/// </summary>
		public readonly string RelativePath;

		/// <summary>
		/// Function for declaring without starting an async task to process the song.
		/// </summary>
		private readonly Func<Task> TaskFunc;

		/// <summary>
		/// Async task for processing the song.
		/// </summary>
		private Task Task;

		private double ExpectedTempo;

		private IResults Results;

		private bool SongHasSingleTempo;

		public SongTaskData(FileInfo fileInfo, string songDir, string relativePath)
		{
			FileInfo = fileInfo;
			SongDir = new DirectoryInfo(songDir).Name;
			PackDir = Directory.GetParent(songDir).Name;
			RelativePath = relativePath;
			TaskFunc = async () => await ProcessSong(this);
		}

		public void Start()
		{
			Task = TaskFunc();
		}

		public bool IsDone()
		{
			return Task?.IsCompleted ?? false;
		}

		public void SetExpectedTempo(double expectedTempo)
		{
			SongHasSingleTempo = true;
			ExpectedTempo = expectedTempo;
		}

		public void SetResults(IResults results)
		{
			Results = results;
		}

		public bool DoesSongHaveSingleTempo()
		{
			return SongHasSingleTempo;
		}

		public double GetExpectedTempo()
		{
			return ExpectedTempo;
		}

		public IResults GetResults()
		{
			return Results;
		}
	}

	private class ResultComparer : IComparer<SongTaskData>
	{
		int IComparer<SongTaskData>.Compare(SongTaskData r1, SongTaskData r2)
		{
			var comparison = r1.PackDir.CompareTo(r2.PackDir);
			if (comparison != 0)
				return comparison;

			comparison = r1.SongDir.CompareTo(r2.SongDir);
			if (comparison != 0)
				return comparison;

			return 0;
		}
	}

	/// <summary>
	/// Main entry point into the program.
	/// </summary>
	/// <remarks>See Config for configuration.</remarks>
	private static void Main()
	{
		// Default the application culture to the invariant culture to ensure consistent parsing in all file I/O.
		CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
		CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

		SoundManager = new SoundManager(512, 4);

		CreateLogger();
		FindAndProcessSongs();
	}

	private static void CreateLogger()
	{
		var assembly = System.Reflection.Assembly.GetEntryAssembly();
		var programPath = assembly.Location;
		var programDir = Path.GetDirectoryName(programPath);
		var logDir = Path.Combine(programDir, "logs");
		Directory.CreateDirectory(logDir);
		var logFile = Path.Combine(logDir, $"{ExecutionTimeString}.log");
		Logger.StartUp(new Logger.Config
		{
			WriteToConsole = true,
			WriteToFile = true,
			Level = LogLevel.Info,
			LogFileFlushIntervalSeconds = 20,
			LogFileBufferSizeBytes = 10240,
			LogFilePath = logFile,
		});
	}

	private static void FindAndProcessSongs()
	{
		if (!Directory.Exists(SongPath))
		{
			LogError($"Could not find song path \"{SongPath}\".");
			return;
		}

		var songTasks = new List<SongTaskData>();

		var pathSep = Path.DirectorySeparatorChar.ToString();

		LogInfo($"Searching for songs in \"{SongPath}\"...");

		// Search through the the SongPath and all subdirectories.
		var dirs = new Stack<string>();
		dirs.Push(SongPath);
		while (dirs.Count > 0)
		{
			// Get the directory to process.
			var currentDir = dirs.Pop();

			// Get sub directories for the next loop.
			try
			{
				var subDirs = Directory.GetDirectories(currentDir);
				// Reverse sort the subdirectories since we use a queue to pop.
				// Sorting helps the user get a rough idea of progress, and makes it easier to tell if a song pack is complete.
				Array.Sort(subDirs, (a, b) => string.Compare(b, a, StringComparison.CurrentCultureIgnoreCase));
				foreach (var str in subDirs)
					dirs.Push(str);
			}
			catch (Exception e)
			{
				LogWarn($"Could not get directories in \"{currentDir}\". {e}");
				continue;
			}

			// Get all files in this directory.
			string[] files;
			try
			{
				files = Directory.GetFiles(currentDir);
			}
			catch (Exception e)
			{
				LogWarn($"Could not get files in \"{currentDir}\". {e}");
				continue;
			}

			// Cache some paths needed for processing the song.
			var relativePath = currentDir.Substring(SongPath.Length, currentDir.Length - SongPath.Length);
			if (relativePath.StartsWith(pathSep))
				relativePath = relativePath.Substring(1, relativePath.Length - 1);
			if (!relativePath.EndsWith(pathSep))
				relativePath += pathSep;

			// Check each file.
			foreach (var file in files)
			{
				// Get the FileInfo for this file so we can check its name.
				FileInfo fi;
				try
				{
					fi = new FileInfo(file);
				}
				catch (Exception e)
				{
					LogWarn($"Could not get file info for \"{file}\". {e}");
					continue;
				}

				// Check that this is a supported file format.
				var fileFormat = FileFormat.GetFileFormatByExtension(fi.Extension);
				if (fileFormat == null || !SupportedFileFormats.Contains(fileFormat.Type))
					continue;

				// Create a task for processing this song, but do not start it.
				var taskData = new SongTaskData(fi, currentDir, relativePath);
				songTasks.Add(taskData);
			}
		}

		var totalSongCount = songTasks.Count;
		var lastKnownRemainingCount = totalSongCount;
		var inProgressTasks = new List<SongTaskData>();
		var concurrentSongCount = Environment.ProcessorCount;
		var completedTasks = new List<SongTaskData>();

		// Process all song tasks.
		var stopWatch = new Stopwatch();
		stopWatch.Start();
		LogInfo($"Found {totalSongCount} songs to process.");
		while (songTasks.Count > 0 || inProgressTasks.Count > 0)
		{
			// See if any in progress tasks are now done.
			var numTasksToAdd = concurrentSongCount;
			var tasksToRemove = new List<SongTaskData>();
			foreach (var inProgressTask in inProgressTasks)
			{
				if (inProgressTask.IsDone())
				{
					tasksToRemove.Add(inProgressTask);
					completedTasks.Add(inProgressTask);
				}
				else
				{
					numTasksToAdd--;
				}
			}

			// Remove completed tasks.
			foreach (var taskToRemove in tasksToRemove)
				inProgressTasks.Remove(taskToRemove);
			tasksToRemove.Clear();

			// Add more tasks.
			var numTasksStarted = 0;
			while (numTasksToAdd > 0 && songTasks.Count > 0)
			{
				var taskToStart = songTasks[0];
				inProgressTasks.Add(taskToStart);
				songTasks.RemoveAt(0);
				taskToStart.Start();
				numTasksToAdd--;
				numTasksStarted++;
			}

			// If we have completed more tasks this loop, log a progress update.
			if (lastKnownRemainingCount != songTasks.Count + inProgressTasks.Count)
			{
				lastKnownRemainingCount = songTasks.Count + inProgressTasks.Count;
				var processedCount = totalSongCount - lastKnownRemainingCount;
				var songPercent = 100.0 * ((double)processedCount / totalSongCount);
				LogInfo(
					$"Progress: {processedCount}/{totalSongCount} songs ({songPercent:F2}%).");
			}

			// If we added a lot of tasks then it means we are processing quickly and can speed up.
			// If we added a small number of tasks then it means we are processing slowly and can wait.
			var sleepTime = (int)Interpolation.Lerp(100, 10, 0, concurrentSongCount, numTasksStarted);
			Thread.Sleep(sleepTime);
		}

		stopWatch.Stop();
		LogInfo($"Processed {totalSongCount} songs in {stopWatch.Elapsed}.");

		WriteResultsToFile(completedTasks);
	}

	private static async Task ProcessSong(SongTaskData songData)
	{
		LogInfo("Loading Song.", songData.FileInfo, songData.RelativePath);

		// Load the song.
		Song song;
		try
		{
			var reader = Reader.CreateReader(songData.FileInfo);
			if (reader == null)
			{
				LogError("Unsupported file format. Cannot parse.", songData.FileInfo, songData.RelativePath);
				return;
			}

			song = await reader.LoadAsync(CancellationToken.None);
		}
		catch (Exception e)
		{
			LogError($"Failed to load file. {e}", songData.FileInfo, songData.RelativePath);
			return;
		}

		// Get the expected tempo.
		if (!GetSongTempo(songData, song, out var musicFile, out var expectedTempo))
			return;

		// Don't consider songs with tempos like 99.999.
		var diffFromWhole = Math.Abs(expectedTempo - (int)expectedTempo);
		if (diffFromWhole < 0.01 && diffFromWhole > 0.00001)
		{
			LogInfo($"Expected tempo {expectedTempo} is not a whole number. Ignoring.");
			return;
		}

		songData.SetExpectedTempo(expectedTempo);

		// Load the music into a sample buffer.
		try
		{
			var cancellationTokenSource = new CancellationTokenSource();
			var sound = await SoundManager.LoadAsync(musicFile);
			if (!sound.hasHandle())
			{
				throw new Exception($"Failed to load {musicFile}");
			}

			SoundManager.AllocateSampleBuffer(sound, SoundManager.GetSampleRate(), out var samples, out var numChannels);
			await SoundManager.FillSamplesAsync(sound, SoundManager.GetSampleRate(), samples, numChannels,
				cancellationTokenSource.Token);
			if (samples == null || numChannels < 1)
			{
				throw new Exception($"Failed to fill samples for {musicFile}");
			}

			// Detect the tempo.
			var results = await DetectTempo(samples, CreateSettings(numChannels, songData.SongDir),
				cancellationTokenSource.Token);
			songData.SetResults(results);
		}
		catch (Exception e)
		{
			LogError($"Failed to load music and parse tempo. {e}", songData.FileInfo, songData.RelativePath);
		}
	}

	private static bool GetSongTempo(SongTaskData songData, Song song, out string musicFile, out double tempo)
	{
		tempo = 0.0;
		musicFile = null;

		var foundTempo = false;
		var allTemposMatch = true;
		var allMusicMatches = true;
		foreach (var chart in song.Charts)
		{
			if (chart.Layers.Count != 1)
				continue;

			if (musicFile == null)
			{
				musicFile = chart.MusicFile;
			}
			else if (musicFile != chart.MusicFile)
			{
				allMusicMatches = false;
				break;
			}

			foreach (var chartEvent in chart.Layers[0].Events)
			{
				if (chartEvent is Tempo tempoEvent)
				{
					if (!foundTempo)
					{
						tempo = tempoEvent.TempoBPM;
					}
					else
					{
						if (!tempoEvent.TempoBPM.DoubleEquals(tempo))
						{
							allTemposMatch = false;
							break;
						}
					}

					foundTempo = true;
				}
			}
		}

		if (!foundTempo)
		{
			LogInfo("Could not find tempo for song.", songData.FileInfo, songData.RelativePath, song);
			return false;
		}

		if (!allTemposMatch)
		{
			LogInfo("Charts have multiple tempos. Ignoring.", songData.FileInfo, songData.RelativePath, song);
			return false;
		}

		if (musicFile == null)
		{
			LogInfo("No music file specified for song.", songData.FileInfo, songData.RelativePath, song);
			return false;
		}

		if (!allMusicMatches)
		{
			LogInfo("Charts have different music. Ignoring.", songData.FileInfo, songData.RelativePath, song);
			return false;
		}

		// Convert the music file to an absolute path.
		var songFilePath = Path.Combine(SongPath, songData.RelativePath);
		musicFile = Path.Combine(songFilePath, musicFile);

		return true;
	}

	private static Settings CreateSettings(int numChannels, string logTag)
	{
		var settings = new Settings()
		{
			MinTempo = 60.0,
			MaxTempo = 240.0,
			NumTemposToFind = NumTemposPerSongToFind,
			MinSeparationBetweenBestTempos = 5.0,
			WindowTime = 20.0,
			CombFilterResolution = 0.25,
			CombFilterBeats = 4.0,
			SampleRate = (int)SoundManager.GetSampleRate(),
			NumChannels = numChannels,
		};
		settings.SetFrequencyBands(new List<FrequencyBand>
		{
			new(0, 200, 100),
			new(200, 400, 100),
			new(400, 800, 50),
			new(800, 1600, 50),
			new(1600, 3200, 50),
			new(3200, 20000, 100),
		});
		settings.SetLocations(new List<Location>
		{
			new(LocationType.RelativeToStart, 20.0, 0.5),
			new(LocationType.Percentage, 0.0, 0.5),
			new(LocationType.RelativeToEnd, 20.0, 0.5),
		});
		settings.SetShouldLog(logTag);
		return settings;
	}

	private static void WriteResultsToFile(List<SongTaskData> allResults)
	{
		allResults.Sort(new ResultComparer());

		var sb = new StringBuilder();
		sb.Append("Pack,Song,Expected Tempo,Measured Tempo,Perfect Match,Match,");
		for (var i = 0; i < NumTemposPerSongToFind; i++)
		{
			sb.Append($"Tempo {i + 1}");
			if (i < NumTemposPerSongToFind - 1)
				sb.Append(',');
		}

		sb.Append('\n');

		foreach (var result in allResults)
		{
			if (!result.DoesSongHaveSingleTempo())
				continue;
			var results = result.GetResults();
			if (results == null)
				continue;

			var bestTempos = results.GetBestTempos();
			var numBestTempos = bestTempos.Count;
			var perfectMatch = results.GetBestTempo().DoubleEquals(result.GetExpectedTempo());
			var match = false;
			foreach (var tempo in bestTempos)
			{
				if (tempo.GetTempo().DoubleEquals(result.GetExpectedTempo())
				    || tempo.GetTempo().DoubleEquals(result.GetExpectedTempo()))
				{
					match = true;
					break;
				}
			}

			sb.Append($"{CSVEscape(result.PackDir)},");
			sb.Append($"{CSVEscape(result.SongDir)},");
			sb.Append($"{result.GetExpectedTempo()},");
			sb.Append($"{results.GetBestTempo()},");
			sb.Append(perfectMatch ? "TRUE," : "FALSE,");
			sb.Append(match ? "TRUE," : "FALSE,");

			var i = 0;
			foreach (var tempo in bestTempos)
			{
				sb.Append(tempo.GetTempo());
				if (i < numBestTempos - 1)
					sb.Append(',');
				i++;
			}

			sb.Append('\n');
		}

		try
		{
			var assembly = System.Reflection.Assembly.GetEntryAssembly();
			var programPath = assembly.Location;
			var programDir = Path.GetDirectoryName(programPath);
			var resultsDir = Path.Combine(programDir, "results");
			Directory.CreateDirectory(resultsDir);
			var resultsFile = Path.Combine(resultsDir, $"results-{ExecutionTimeString}.csv");
			File.WriteAllText(resultsFile, sb.ToString());
		}
		catch (Exception e)
		{
			LogError($"Failed to write results. {e}");
		}
	}

	/// <summary>
	/// Escapes the given string for use in a csv.
	/// </summary>
	/// <param name="input">String to escape.</param>
	/// <returns>Escaped string.</returns>
	private static string CSVEscape(string input)
	{
		return $"\"{input.Replace("\"", "\"\"")}\"";
	}

	#region Logging

	private static string GetLogIdentifier(FileInfo fi, string relativePath, Song song = null, Chart chart = null)
	{
		if (chart != null && song != null)
			return $"[{relativePath}{fi.Name} \"{song.Title}\" {chart.Type} {chart.DifficultyType}]";
		if (song != null)
			return $"[{relativePath}{fi.Name} \"{song.Title}\"]";
		return $"[{relativePath}{fi.Name}]";
	}

	private static void LogError(string message)
	{
		Logger.Error($"[{LogTag}] {message}");
	}

	private static void LogWarn(string message)
	{
		Logger.Warn($"[{LogTag}] {message}");
	}

	private static void LogInfo(string message)
	{
		Logger.Info($"[{LogTag}] {message}");
	}

	private static void LogError(string message, FileInfo fi, string relativePath, Song song = null, Chart chart = null)
	{
		LogError($"{GetLogIdentifier(fi, relativePath, song, chart)} {message}");
	}

	private static void LogWarn(string message, FileInfo fi, string relativePath, Song song = null, Chart chart = null)
	{
		LogWarn($"{GetLogIdentifier(fi, relativePath, song, chart)} {message}");
	}

	private static void LogInfo(string message, FileInfo fi, string relativePath, Song song = null, Chart chart = null)
	{
		LogInfo($"{GetLogIdentifier(fi, relativePath, song, chart)} {message}");
	}

	#endregion Logging
}
