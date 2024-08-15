using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Fumen;
using MathNet.Numerics.IntegralTransforms;

namespace StepManiaLibrary;

/// <summary>
/// Class for asynchronously detecting the tempo of music.
/// TODO: Testing and clean-up.
/// </summary>
public sealed class TempoDetector
{
	#region Settings

	public enum LocationType
	{
		RelativeToStart,
		RelativeToEnd,
		Percentage,
	}

	public sealed class Location : IEquatable<Location>
	{
		public Location()
		{
		}

		public Location(LocationType type, double time, double percentage)
		{
			Type = type;
			Time = time;
			Percentage = percentage;
		}

		public Location(Location other)
		{
			Type = other.Type;
			Time = other.Time;
			Percentage = other.Percentage;
		}

		[JsonInclude] public LocationType Type;
		[JsonInclude] public double Time;
		[JsonInclude] public double Percentage;

		public bool Equals(Location other)
		{
			if (ReferenceEquals(null, other))
				return false;
			if (ReferenceEquals(this, other))
				return true;
			return Type == other.Type && Time.DoubleEquals(other.Time) && Percentage.DoubleEquals(other.Percentage);
		}

		public override bool Equals(object obj)
		{
			return ReferenceEquals(this, obj) || (obj is Location other && Equals(other));
		}

		public override int GetHashCode()
		{
			return HashCode.Combine((int)Type, Time, Percentage);
		}
	}

	public sealed class FrequencyBand : IEquatable<FrequencyBand>
	{
		public FrequencyBand()
		{
		}

		public FrequencyBand(int low, int high, int weight)
		{
			Low = low;
			High = high;
			Weight = weight;
		}

		public FrequencyBand(FrequencyBand other)
		{
			Low = other.Low;
			High = other.High;
			Weight = other.Weight;
		}

		[JsonInclude] public int Low;
		[JsonInclude] public int High;
		[JsonInclude] public int Weight;

		public bool Equals(FrequencyBand other)
		{
			if (ReferenceEquals(null, other))
				return false;
			if (ReferenceEquals(this, other))
				return true;
			return Low == other.Low && High == other.High && Weight == other.Weight;
		}

		public override bool Equals(object obj)
		{
			return ReferenceEquals(this, obj) || (obj is FrequencyBand other && Equals(other));
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(Low, High, Weight);
		}
	}

	public sealed class Settings
	{
		public double MinTempo = 60.0;
		public double MaxTempo = 240.0;
		public int NumTemposToFind = 5;
		public double MinSeparationBetweenBestTempos = 5.0;
		public double WindowTime = 20.0;
		public double CombFilterResolution = 0.1;
		public double CombFilterBeats = 4;
		public int SampleRate;
		public int NumChannels;
		public bool WriteDebugWavFiles;
		public string WavFileFolder;
		public FrequencyBand[] FrequencyBands;
		public float[] FrequencyBandWeights;
		public Location[] Locations;

		public void SetFrequencyBands(List<FrequencyBand> bands)
		{
			FrequencyBands = new FrequencyBand[bands.Count];
			FrequencyBandWeights = new float[bands.Count];

			var totalWeight = 0;
			foreach (var band in bands)
			{
				totalWeight += band.Weight;
			}

			var i = 0;
			foreach (var band in bands)
			{
				FrequencyBands[i] = new FrequencyBand(band);
				FrequencyBandWeights[i] = (float)band.Weight / totalWeight;
				i++;
			}
		}

		public void SetLocations(List<Location> locations)
		{
			Locations = new Location[locations.Count];
			var i = 0;
			foreach (var location in locations)
			{
				Locations[i] = new Location(location);
				i++;
			}
		}

		public void SetWriteDebugWavFiles(string folder)
		{
			WriteDebugWavFiles = true;
			WavFileFolder = folder;
		}
	}

	#endregion Settings

	#region Public Results Interfaces

	public interface IResults
	{
		public double GetBestTempo();
		public double GetBestTempoAdjusted();
		public void SelectBestTempoIndex(int index);
		public int GetBestTempoIndex();
		public IReadOnlyList<IReadOnlyLocationResults> GetResultsByLocation();
		public IReadOnlyList<IReadOnlyTempoResult> GetBestTempos();
	}

	public interface IReadOnlyLocationResults
	{
		public double GetBestTempo();
		public double GetBestTempoAdjusted();
		public float GetAverageCorrelation();
		public float[] GetCorrelations();
		public float[] GetNormalizedCorrelations();
		public Location GetLocation();
	}

	public interface IReadOnlyTempoResult
	{
		public double GetTempo();
		public double GetTempoAdjusted();
		public double GetCorrelation();
		public double GetCorrelationDifferenceFromAverage();
	}

	#endregion Public Results Interfaces

	#region Private Results Classes

	private sealed class LocationResults : IReadOnlyLocationResults
	{
		private double BestTempo;
		private double BestTempoAdjusted;
		private float AverageCorrelation;
		private float[] Correlations;
		private float[] NormalizedCorrelations;
		private readonly Location Location;

		public LocationResults(Location location)
		{
			Location = location;
		}

		public void SetResults(double bestTempo, double bestTempoAdjusted, float averageCorrelation, float[] correlations)
		{
			BestTempo = bestTempo;
			BestTempoAdjusted = bestTempoAdjusted;
			AverageCorrelation = averageCorrelation;
			Correlations = correlations;
		}

		public void SetNormalizedCorrelations(float[] normalizedCorrelations)
		{
			NormalizedCorrelations = normalizedCorrelations;
		}

		public double GetBestTempo()
		{
			return BestTempo;
		}

		public double GetBestTempoAdjusted()
		{
			return BestTempoAdjusted;
		}

		public float GetAverageCorrelation()
		{
			return AverageCorrelation;
		}

		public float[] GetCorrelations()
		{
			return Correlations;
		}

		public float[] GetNormalizedCorrelations()
		{
			return NormalizedCorrelations;
		}

		public Location GetLocation()
		{
			return Location;
		}
	}

	private sealed class TempoResult : IReadOnlyTempoResult
	{
		private readonly double Tempo;
		private readonly double TempoAdjusted;
		private readonly double Correlation;
		private readonly double CorrelationDifferenceFromAverage;

		public TempoResult(double tempo, double tempoAdjusted, double correlation, double correlationDifferenceFromAverage)
		{
			Tempo = tempo;
			TempoAdjusted = tempoAdjusted;
			Correlation = correlation;
			CorrelationDifferenceFromAverage = correlationDifferenceFromAverage;
		}

		public double GetTempo()
		{
			return Tempo;
		}

		public double GetTempoAdjusted()
		{
			return TempoAdjusted;
		}

		public double GetCorrelation()
		{
			return Correlation;
		}

		public double GetCorrelationDifferenceFromAverage()
		{
			return CorrelationDifferenceFromAverage;
		}
	}

	private sealed class Results : IResults
	{
		private readonly List<LocationResults> ResultsByLocation;
		private List<TempoResult> BestTempos = new();
		private int BestTempoIndex = 0;

		public Results(List<LocationResults> resultsByLocation)
		{
			ResultsByLocation = resultsByLocation;
		}

		public List<LocationResults> GetResultsByLocationMutable()
		{
			return ResultsByLocation;
		}

		public void SetBestTempos(List<TempoResult> bestTempos)
		{
			BestTempos = bestTempos;
		}

		public double GetBestTempo()
		{
			return BestTempos[BestTempoIndex].GetTempo();
		}

		public double GetBestTempoAdjusted()
		{
			return BestTempos[BestTempoIndex].GetTempoAdjusted();
		}

		public int GetBestTempoIndex()
		{
			return BestTempoIndex;
		}

		public void SelectBestTempoIndex(int index)
		{
			BestTempoIndex = Math.Clamp(index, 0, BestTempos.Count);
		}

		public IReadOnlyList<IReadOnlyLocationResults> GetResultsByLocation()
		{
			return ResultsByLocation;
		}

		public IReadOnlyList<IReadOnlyTempoResult> GetBestTempos()
		{
			return BestTempos;
		}
	}

	#endregion Private Results Classes

	#region Internal Classes

	private struct Peak
	{
		public int Index;
		public float Value;

		public Peak(int index, float value)
		{
			Index = index;
			Value = value;
		}
	}

	#endregion Internal Classes

	public static async Task<IResults> DetectTempo(float[] music, Settings settings, CancellationToken token)
	{
		// Set up Results.
		var locationResults = new List<LocationResults>(settings.Locations.Length);
		foreach (var location in settings.Locations)
			locationResults.Add(new LocationResults(new Location(location)));
		var results = new Results(locationResults);

		// Set up a task for each sample window.
		var tasks = new Task[locationResults.Count];
		for (var i = 0; i < locationResults.Count; i++)
		{
			var locationResult = locationResults[i];
			tasks[i] = Task.Run(() =>
			{
				var location = locationResult.GetLocation();
				var window = GetMonoSampleWindow(music, settings, location);
				token.ThrowIfCancellationRequested();
				DetectTempo(window, settings, locationResult, token);
			}, token);
		}

		// Run the tasks.
		await Task.WhenAll(tasks);
		token.ThrowIfCancellationRequested();

		// Normalize results.
		NormalizeCorrelations(results);

		// Aggregate results.
		SelectBestTemposAcrossAllWindows(results, settings);

		return results;
	}

	private static float[] GetMonoSampleWindow(float[] music, Settings settings, Location location)
	{
		// Determine window to sample.
		var windowStartSample = 0;
		var desiredWindowTime = settings.WindowTime;
		var desiredWindowSamples = (int)(desiredWindowTime * settings.SampleRate);
		var numSamples = music.Length / settings.NumChannels;
		switch (location.Type)
		{
			case LocationType.RelativeToStart:
				windowStartSample = (int)(location.Time * settings.SampleRate);
				break;
			case LocationType.RelativeToEnd:
				windowStartSample = numSamples - (int)((location.Time + desiredWindowTime) * settings.SampleRate);
				break;
			case LocationType.Percentage:
				windowStartSample = (int)((numSamples - desiredWindowSamples) * location.Percentage);
				break;
		}

		windowStartSample = Math.Clamp(windowStartSample, 0, numSamples - 1);
		var windowEndSample = Math.Clamp(windowStartSample + desiredWindowSamples, 0, numSamples - 1);
		var windowLen = windowEndSample - windowStartSample + 1;
		if (windowLen <= 0)
			return null;

		// Copy from the music data into a mono window.
		var window = new float[windowLen];
		for (var s = 0; s < window.Length; s++)
		{
			float sum = 0;
			for (var c = 0; c < settings.NumChannels; c++)
				sum += music[(windowStartSample + s) * settings.NumChannels + c];
			window[s] = sum / settings.NumChannels;
		}

		if (settings.WriteDebugWavFiles)
			WriteWav($"{GetLocationString(location)}-mono", window, settings);

		return window;
	}

	private static void DetectTempo(float[] window, Settings settings, LocationResults results, CancellationToken token)
	{
		if (window == null)
			return;

		// Generate time domain buffers for each frequency band.
		var bandBuffers = GenerateBandBuffers(window, settings, results);
		token.ThrowIfCancellationRequested();

		// Detect onset envelope for each band
		var numBands = settings.FrequencyBands.Length;
		//var onsetEnvelopes = new float[numBands][];
		for (var band = 0; band < numBands; band++)
		{
			FullWaveRectify(bandBuffers[band], settings, band, results);
			token.ThrowIfCancellationRequested();
			EnvelopePeaks(bandBuffers[band], settings, band, results);

			//onsetEnvelopes[band] = DetectOnsetEnvelope(bandBuffers[band], settings, band, results);
			token.ThrowIfCancellationRequested();
		}

		// Combine onset envelopes
		var combinedOnsetEnvelope = CombineOnsetEnvelopes(bandBuffers, settings, results);
		token.ThrowIfCancellationRequested();

		// Detect tempo using comb filter
		DetectTempoWithCombFilter(combinedOnsetEnvelope, settings, results);
	}

	private static float[][] GenerateBandBuffers(float[] signal, Settings settings, LocationResults results)
	{
		// FFT to frequency domain.
		var fftSize = MathUtils.GetNextPowerOfTwo(signal.Length);
		var complexSignal = new Complex[fftSize];
		for (var s = 0; s < signal.Length; s++)
		{
			complexSignal[s] = new Complex(signal[s], 0);
		}

		Fourier.Forward(complexSignal, FourierOptions.AsymmetricScaling);

		var frequencyResolution = (double)settings.SampleRate / fftSize;

		// Process each specified frequency band.
		var numBands = settings.FrequencyBands.Length;
		var bandBuffers = new float[numBands][];
		for (var band = 0; band < numBands; band++)
		{
			// Aggregate frequencies according to the band.
			var bandComplex = new Complex[fftSize];
			var lowBin = (int)(settings.FrequencyBands[band].Low / frequencyResolution);
			var highBin = (int)(settings.FrequencyBands[band].High / frequencyResolution);
			for (var bin = lowBin; bin < highBin && bin < fftSize / 2; bin++)
			{
				bandComplex[bin] = complexSignal[bin];
				bandComplex[fftSize - bin - 1] = complexSignal[fftSize - bin - 1];
			}

			// Inverse FFT back to time domain.
			Fourier.Inverse(bandComplex, FourierOptions.AsymmetricScaling);
			bandBuffers[band] = new float[signal.Length];
			for (var s = 0; s < signal.Length; s++)
			{
				bandBuffers[band][s] = (float)bandComplex[s].Real;
			}

			if (settings.WriteDebugWavFiles)
				WriteWav(
					$"{GetLocationString(results.GetLocation())}-band-{band}({settings.FrequencyBands[band].Low}-{settings.FrequencyBands[band].High})",
					bandBuffers[band], settings);
		}

		return bandBuffers;
	}

	private static void FullWaveRectify(float[] signal, Settings settings, int band, LocationResults results)
	{
		for (var i = 0; i < signal.Length; i++)
		{
			signal[i] = Math.Abs(signal[i]);
		}

		if (settings.WriteDebugWavFiles)
			WriteWav(
				$"{GetLocationString(results.GetLocation())}-band-{band}({settings.FrequencyBands[band].Low}-{settings.FrequencyBands[band].High})-fullwaverect",
				signal, settings);
	}


	private static void EnvelopePeaks(float[] signal, Settings settings, int band, LocationResults results)
	{
		var peaks = FindPeaks(signal);
		InterpolatePeaks(peaks, signal);

		if (settings.WriteDebugWavFiles)
			WriteWav(
				$"{GetLocationString(results.GetLocation())}-band-{band}({settings.FrequencyBands[band].Low}-{settings.FrequencyBands[band].High})-envelope",
				signal, settings);
	}

	private static void InterpolatePeaks(List<Peak> peaks, float[] signal)
	{
		var peakIndex = 0;

		for (var i = 0; i < signal.Length; i++)
		{
			// Find the left peak index.
			while (peakIndex < peaks.Count - 1 && peaks[peakIndex + 1].Index <= i)
			{
				peakIndex++;
			}

			if (peakIndex == peaks.Count - 1)
			{
				// If we're past the last peak, use the last peak's value.
				signal[i] = peaks[peakIndex].Value;
			}
			else if (peakIndex == 0 && i < peaks[0].Index)
			{
				// If we're before the first peak, use the first peak's value.
				signal[i] = peaks[0].Value;
			}
			else
			{
				var p1 = peaks[peakIndex];
				var p2 = peaks[peakIndex + 1];
				var localT = (float)(i - p1.Index) / (p2.Index - p1.Index);
				var p0 = peakIndex > 0 ? peaks[peakIndex - 1] : p1;
				var p3 = peakIndex < peaks.Count - 2 ? peaks[peakIndex + 2] : p2;

				signal[i] = Interpolation.HermiteInterpolate(p0.Value, p1.Value, p2.Value, p3.Value, localT);
			}
		}
	}

	private static List<Peak> FindPeaks(float[] input)
	{
		var peaks = new List<Peak>();
		var i = 0;

		while (i < input.Length)
		{
			if ((i == 0 && input[i] > input[i + 1]) || (i > 0 && input[i] > input[i - 1]))
			{
				var peakValue = input[i];
				var peakIndex = i;
				var end = i;

				while (end < input.Length - 1 && input[end] <= input[end + 1])
				{
					end++;
					if (input[end] > peakValue)
					{
						peakValue = input[end];
						peakIndex = end;
					}
				}

				while (end < input.Length - 1 && input[end] >= input[end + 1])
				{
					end++;
					if (input[end] > peakValue)
					{
						peakValue = input[end];
						peakIndex = end;
					}
				}

				peaks.Add(new Peak(peakIndex, peakValue));
				i = end + 1;
			}
			else
			{
				i++;
			}
		}

		return peaks;
	}

	private static float[] CombineOnsetEnvelopes(float[][] onsetEnvelopes, Settings settings, LocationResults results)
	{
		var numBands = onsetEnvelopes.Length;
		var numSamples = onsetEnvelopes[0].Length;
		var combined = new float[numSamples];

		for (var sampleIndex = 0; sampleIndex < numSamples; sampleIndex++)
		{
			var sum = 0.0f;
			for (var band = 0; band < numBands; band++)
			{
				sum += onsetEnvelopes[band][sampleIndex] * settings.FrequencyBandWeights[band];
			}

			combined[sampleIndex] = sum;
		}

		if (settings.WriteDebugWavFiles)
			WriteWav($"{GetLocationString(results.GetLocation())}-combined-envelope", combined, settings);
		return combined;
	}

	private static void DetectTempoWithCombFilter(float[] signal, Settings settings, LocationResults results)
	{
		var bestTempo = 0.0;
		var maxCorrelation = 0.0f;
		var averageCorrelation = 0.0f;
		var correlations = new List<float>();

		var i = 0;
		while (true)
		{
			var tempo = settings.MinTempo + i * settings.CombFilterResolution;
			if (tempo > settings.MaxTempo)
				break;
			var delay = (int)(60.0 * settings.CombFilterBeats / tempo * settings.SampleRate);
			var correlation = (float)CombFilterCorrelation(signal, delay);
			correlations.Add(correlation);

			averageCorrelation += correlation;
			if (correlation > maxCorrelation)
			{
				maxCorrelation = correlation;
				bestTempo = tempo;
			}

			i++;
		}

		if (i > 0)
			averageCorrelation /= i;

		results.SetResults(bestTempo, CleanBestTempo(bestTempo, settings), averageCorrelation, correlations.ToArray());
	}

	private static void NormalizeCorrelations(Results results)
	{
		var min = float.MaxValue;
		var max = float.MinValue;
		foreach (var result in results.GetResultsByLocation())
		{
			foreach (var correlation in result.GetCorrelations())
			{
				if (correlation < min)
					min = correlation;
				if (correlation > max)
					max = correlation;
			}
		}

		foreach (var result in results.GetResultsByLocationMutable())
		{
			var correlations = result.GetCorrelations();
			var normalizedCorrelations = new float[correlations.Length];
			for (var i = 0; i < correlations.Length; i++)
			{
				normalizedCorrelations[i] = (correlations[i] - min) / (max - min);
			}

			result.SetNormalizedCorrelations(normalizedCorrelations);
		}
	}

	private static void SelectBestTemposAcrossAllWindows(Results results, Settings settings)
	{
		var bestTempos = new SortedList<float, TempoResult>(new DescendingComparer<float>());
		foreach (var locationResult in results.GetResultsByLocation())
		{
			var i = 0;
			var averageCorrelation = locationResult.GetAverageCorrelation();
			foreach (var correlation in locationResult.GetCorrelations())
			{
				var relativeCorrelation = correlation - averageCorrelation;
				CheckAndAddToBestTempos(settings.MinTempo + i * settings.CombFilterResolution, correlation, relativeCorrelation,
					settings, bestTempos);
				i++;
			}
		}

		var bestTemposList = new List<TempoResult>();
		foreach (var bestTempo in bestTempos)
			bestTemposList.Add(bestTempo.Value);
		results.SetBestTempos(bestTemposList);

		// Check for best tempos which are unlikely multiples of other best tempos.
		// For example if the tempo is 100bpm and has strong repetitive beats, a 4-beat long
		// filter at 100bpm will correctly match, but so may a 4-beat long filter at 133.3bpm
		// because it covers 3 100bpm beats. In situations like this where similarly valid 
		// tempos are found and one represents 3/4 of another, that 3/4 tempo is unlikely.
		// In this case we will keep the tempos and their correlations unchanged, but just select
		// the index of the more likely tempo as the best tempo.
		if (settings.CombFilterBeats > 1 && bestTemposList.Count > 0)
		{
			var bestTempo = bestTemposList[0].GetTempo();
			var bestCorrelation = bestTemposList[0].GetCorrelation();
			for (var i = 1; i < bestTemposList.Count; i++)
			{
				// Only try to choose a different best tempo if it has a strong correlation.
				// A perfect correlation is 1.0.
				if (bestCorrelation - bestTemposList[i].GetCorrelation() > 0.1)
					break;

				var factor = bestTempo / bestTemposList[i].GetTempo();
				if (Math.Abs(factor - 1.3333) < 0.1
				    || Math.Abs(factor - 0.6667) < 0.1)
				{
					results.SelectBestTempoIndex(i);
					break;
				}
			}
		}
	}

	private static void CheckAndAddToBestTempos(double tempo, float correlation, float correlationDifferenceFromAverage,
		Settings settings, SortedList<float, TempoResult> bestTempos)
	{
		// Don't add this tempo if it is close to another tempo with a better correlation.
		for (var i = bestTempos.Count - 1; i >= 0; i--)
		{
			var diff = Math.Abs(bestTempos.GetValueAtIndex(i).GetTempo() - tempo);
			if (diff < settings.MinSeparationBetweenBestTempos)
			{
				if (bestTempos.GetKeyAtIndex(i) >= correlationDifferenceFromAverage)
					return;
			}
		}

		var cleanedTempo = CleanBestTempo(tempo, settings);

		// Add this tempo if it has a better correlation than any of the best tempos so far.
		var addedTempo = false;
		foreach (var bestTempoKvp in bestTempos)
		{
			if (correlationDifferenceFromAverage > bestTempoKvp.Key)
			{
				bestTempos.Add(correlationDifferenceFromAverage,
					new TempoResult(tempo, cleanedTempo, correlation, correlationDifferenceFromAverage));
				addedTempo = true;
				break;
			}
		}

		// Also add the tempo if we still have room for best tempos.
		if (!addedTempo && bestTempos.Count < settings.NumTemposToFind)
		{
			bestTempos.Add(correlationDifferenceFromAverage,
				new TempoResult(tempo, cleanedTempo, correlation, correlationDifferenceFromAverage));
			addedTempo = true;
		}

		// If we didn't meet the criteria above we are done.
		if (!addedTempo)
			return;

		// Remove tempos which are close to the added tempo and have worse correlations.
		for (var i = bestTempos.Count - 1; i >= 0; i--)
		{
			if (bestTempos.GetKeyAtIndex(i) >= correlationDifferenceFromAverage)
				continue;
			var diff = Math.Abs(bestTempos.GetValueAtIndex(i).GetTempo() - tempo);
			if (diff > 0.0 && diff < settings.MinSeparationBetweenBestTempos)
			{
				bestTempos.RemoveAt(i);
			}
		}

		// Cap the size, removing the worst correlated tempo.
		while (bestTempos.Count > settings.NumTemposToFind)
		{
			bestTempos.RemoveAt(bestTempos.Count - 1);
		}
	}

	private static double CleanBestTempo(double tempo, Settings settings)
	{
		// If a tempo is halfway between two integer tempos and doubling it would still be
		// within the specified range of acceptable tempos, then double it as tempos which
		// aren't integer values are exceptionally rare.
		if (Math.Abs(tempo - (int)tempo - 0.5) < 0.0001 && tempo * 2.0 <= settings.MaxTempo)
		{
			return tempo * 2.0;
		}

		return tempo;
	}

	private static double CombFilterCorrelation(float[] signal, int delay)
	{
		var sum = 0.0;
		var count = signal.Length - delay;
		for (var i = 0; i < count; i++)
			sum += 1.0 - Math.Abs(signal[i] - signal[i + delay]);
		return sum / count;
	}


	private static string GetLocationString(Location location)
	{
		switch (location.Type)
		{
			case LocationType.RelativeToStart:
				return $"{location.Time}s-from-start";
			case LocationType.RelativeToEnd:
				return $"{location.Time}s-from-end";
			case LocationType.Percentage:
				return $"{(int)(location.Percentage * 100)}pct";
		}

		return null;
	}

	private static void WriteWav(string fileName, float[] sampleData, Settings settings)
	{
		try
		{
			var assembly = System.Reflection.Assembly.GetEntryAssembly();
			var programPath = assembly.Location;
			var programDir = System.IO.Path.GetDirectoryName(programPath);
			var wavDir = Path.Combine(new[] { programDir, "tempo-detector-wavs", settings.WavFileFolder });
			var wavFile = Path.Combine(wavDir, $"{fileName}.wav");
			System.IO.Directory.CreateDirectory(wavDir);
			WavWriter.WriteWavFile(wavFile, sampleData, settings.SampleRate, 1);
		}
		catch (Exception e)
		{
			Logger.Error($"Failed to write wav file {fileName}: {e}");
		}
	}
}
