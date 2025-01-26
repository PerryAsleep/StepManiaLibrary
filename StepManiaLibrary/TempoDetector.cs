using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Fumen;
using MathNet.Numerics.IntegralTransforms;

// ReSharper disable All 

namespace StepManiaLibrary;

/// <summary>
/// Class for asynchronously detecting the tempo of music.
/// TODO: Testing and clean-up.
/// I am probably going to re-evaluate this entire approach to use onset detection since it is simpler and will
/// better enable auto-syncing.
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
		public bool ShouldLog;
		public string LogTag;
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

		public void SetShouldLog(string logTag)
		{
			ShouldLog = true;
			LogTag = logTag;
		}
	}

	#endregion Settings

	#region Public Results Interfaces

	public interface IResults
	{
		public double GetBestTempo();
		public void SelectBestTempoIndex(int index);
		public int GetBestTempoIndex();
		public IReadOnlyList<IReadOnlyLocationResults> GetResultsByLocation();
		public IReadOnlyList<IReadOnlyTempoResult> GetBestTempos();
	}

	public interface IReadOnlyLocationResults
	{
		public float GetAverageCorrelation();
		public float[] GetCorrelations();
		public float[] GetNormalizedCorrelations();
		public Location GetLocation();
	}

	public interface IReadOnlyTempoResult
	{
		public double GetTempo();
		public double GetCorrelation();
	}

	#endregion Public Results Interfaces

	#region Private Results Classes

	private sealed class LocationResults : IReadOnlyLocationResults
	{
		private float AverageCorrelation;
		private float[] Correlations;
		private float[] NormalizedCorrelations;
		private readonly Location Location;

		public LocationResults(Location location)
		{
			Location = location;
		}

		public void SetResults(float averageCorrelation, float[] correlations)
		{
			AverageCorrelation = averageCorrelation;
			Correlations = correlations;
		}

		public void SetNormalizedCorrelations(float[] normalizedCorrelations)
		{
			NormalizedCorrelations = normalizedCorrelations;
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

	private class CorrelatedTempo
	{
		public readonly double Tempo;
		public double Correlation = double.MinValue;

		public CorrelatedTempo(double tempo)
		{
			Tempo = tempo;
		}

		public CorrelatedTempo(double tempo, double correlation)
		{
			Tempo = tempo;
			Correlation = correlation;
		}
	}

	private sealed class DivisibleTempo : IReadOnlyTempoResult
	{
		private readonly List<CorrelatedTempo> AllTempos = [];
		private readonly List<CorrelatedTempo> CorrelatedTempos = [];
		private int BestTempoIndex = 0;

		public DivisibleTempo(double tempo, double correlation, double min, double max)
		{
			var t = tempo;
			AllTempos.Add(new CorrelatedTempo(t, correlation));
			t *= 0.5;
			while (t >= min)
			{
				AllTempos.Add(new CorrelatedTempo(t));
				t *= 0.5;
			}

			AllTempos.Reverse();

			t = tempo * 2.0;
			while (t <= max)
			{
				AllTempos.Add(new CorrelatedTempo(t));
				t *= 2.0;
			}

			RefreshCorrelatedTempos();
		}

		public bool ContainsTempo(double tempo)
		{
			foreach (var t in AllTempos)
			{
				if (Math.Abs(t.Tempo - tempo) < 0.001)
					return true;
			}

			return false;
		}

		public bool IsGivenTempoNearAnyCorrelatedTempo(double tempo, double minSeparation)
		{
			foreach (var t in AllTempos)
			{
				var diff = Math.Abs(t.Tempo - tempo);
				if (diff > 0.0 && diff < minSeparation)
					return true;
			}

			return false;
		}

		public void UpdateCorrelationForTempo(double tempo, double correlation)
		{
			foreach (var t in AllTempos)
			{
				if (Math.Abs(t.Tempo - tempo) < 0.001)
				{
					t.Correlation = Math.Max(t.Correlation, correlation);
					RefreshCorrelatedTempos();
					return;
				}
			}
		}

		public double GetBestCorrelation()
		{
			var bestCorrelation = double.MinValue;
			foreach (var t in AllTempos)
			{
				bestCorrelation = Math.Max(t.Correlation, bestCorrelation);
			}

			return bestCorrelation;
		}

		public double GetMostCloselyCorrelatedTempo()
		{
			var bestCorrelation = double.MinValue;
			var bestTempo = 0.0;
			foreach (var t in AllTempos)
			{
				if (t.Correlation > bestCorrelation)
				{
					bestCorrelation = t.Correlation;
					bestTempo = t.Tempo;
				}
			}

			return bestTempo;
		}

		public IReadOnlyList<CorrelatedTempo> GetCorrelatedTempos()
		{
			return CorrelatedTempos;
		}

		public void SelectBestDivisibleTempo(double min, double max)
		{
			var averageTempo = min + (max - min) * 0.5;

			// Under normal circumstances the best tempo is the most strongly correlated tempo.
			var bestCorrelation = double.MinValue;
			BestTempoIndex = 0;
			var index = 0;
			foreach (var t in AllTempos)
			{
				if (t.Correlation > bestCorrelation)
				{
					bestCorrelation = t.Correlation;
					BestTempoIndex = index;
				}

				index++;
			}

			// If the best tempo is at a half bpm and doubling it is within range, then double it.
			// Non-integer tempos are very unlikely to be intended.
			if (Math.Abs(AllTempos[BestTempoIndex].Tempo - (int)AllTempos[BestTempoIndex].Tempo - 0.25) < 0.0001
			    && BestTempoIndex + 2 < AllTempos.Count)
			{
				BestTempoIndex += 2;
			}

			if (Math.Abs(AllTempos[BestTempoIndex].Tempo - (int)AllTempos[BestTempoIndex].Tempo - 0.5) < 0.0001
			    && BestTempoIndex + 1 < AllTempos.Count)
			{
				BestTempoIndex++;
			}

			// If halving or doubling puts us closer to the average tempo and the following conditions are met,
			// then halve or double as necessary.
			// 1) That new value is an integer tempo
			// 2) That new value is within the valid tempo range
			// 3) That new value doesn't have a poorly correlated tempo
			while (true)
			{
				var nextIndex = BestTempoIndex - 1;
				if (nextIndex < 0)
					break;
				if (!(Math.Abs(AllTempos[nextIndex].Tempo - (int)AllTempos[nextIndex].Tempo) < 0.0001))
					break;
				if (AllTempos[BestTempoIndex].Correlation - AllTempos[nextIndex].Correlation > 0.1)
					break;
				if (Math.Abs(AllTempos[BestTempoIndex].Tempo - averageTempo) <
				    Math.Abs(AllTempos[nextIndex].Tempo - averageTempo))
					break;
				BestTempoIndex = nextIndex;
			}

			while (true)
			{
				var nextIndex = BestTempoIndex + 1;
				if (nextIndex >= AllTempos.Count)
					break;
				if (!(Math.Abs(AllTempos[nextIndex].Tempo - (int)AllTempos[nextIndex].Tempo) < 0.0001))
					break;
				if (AllTempos[BestTempoIndex].Correlation - AllTempos[nextIndex].Correlation > 0.1)
					break;
				if (Math.Abs(AllTempos[BestTempoIndex].Tempo - averageTempo) <
				    Math.Abs(AllTempos[nextIndex].Tempo - averageTempo))
					break;
				BestTempoIndex = nextIndex;
			}
		}

		private void RefreshCorrelatedTempos()
		{
			CorrelatedTempos.Clear();
			foreach (var t in AllTempos)
			{
				if (t.Correlation > double.MinValue)
				{
					CorrelatedTempos.Add(t);
				}
			}
		}

		public double GetTempo()
		{
			return AllTempos[BestTempoIndex].Tempo;
		}

		public double GetCorrelation()
		{
			return AllTempos[BestTempoIndex].Correlation;
		}
	}

	private sealed class Results : IResults
	{
		private readonly List<LocationResults> ResultsByLocation;
		private List<DivisibleTempo> BestTempos = [];
		private int BestTempoIndex = 0;

		public Results(List<LocationResults> resultsByLocation)
		{
			ResultsByLocation = resultsByLocation;
		}

		public List<LocationResults> GetResultsByLocationMutable()
		{
			return ResultsByLocation;
		}

		public void SetBestTempos(List<DivisibleTempo> bestTempos)
		{
			BestTempos = bestTempos;
		}

		public double GetBestTempo()
		{
			return BestTempos[BestTempoIndex].GetTempo();
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

	private class TempoResultComparer : IComparer<DivisibleTempo>
	{
		public int Compare(DivisibleTempo r1, DivisibleTempo r2)
		{
			if (ReferenceEquals(r1, r2))
				return 0;
			var comparison = r2.GetBestCorrelation().CompareTo(r1.GetBestCorrelation());
			if (comparison != 0)
				return comparison;
			return r2.GetMostCloselyCorrelatedTempo().CompareTo(r1.GetMostCloselyCorrelatedTempo());
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

		// Detect onset envelope for each band.
		var numBands = settings.FrequencyBands.Length;
		var bandCorrelations = new float[numBands][];
		var bandAverages = new float[numBands];
		var bandDeviations = new float[numBands];
		var bandWithGreatestDeviation = 0;
		var greatestDeviation = 0.0f;
		for (var band = 0; band < numBands; band++)
		{
			FullWaveRectify(bandBuffers[band], settings, band, results);
			token.ThrowIfCancellationRequested();
			EnvelopePeaks(bandBuffers[band], settings, band, results);
			token.ThrowIfCancellationRequested();
			DetectTempoWithCombFilter(bandBuffers[band], settings, out var correlations, out var average, out var deviation);

			bandCorrelations[band] = correlations;
			bandAverages[band] = average;
			bandDeviations[band] = deviation;
			if (deviation > greatestDeviation)
			{
				greatestDeviation = deviation;
				bandWithGreatestDeviation = band;
			}

			token.ThrowIfCancellationRequested();
		}

		if (settings.ShouldLog)
		{
			var deviationsStringBuilder = new StringBuilder();
			var averagesStringBuilder = new StringBuilder();
			deviationsStringBuilder.Append('[');
			averagesStringBuilder.Append('[');
			for (var i = 0; i < numBands; i++)
			{
				deviationsStringBuilder.Append($"{bandDeviations[i]:N2}");
				averagesStringBuilder.Append($"{bandAverages[i]:N2}");
				if (i < numBands - 1)
				{
					deviationsStringBuilder.Append(',');
					averagesStringBuilder.Append(',');
				}
			}

			deviationsStringBuilder.Append(']');
			averagesStringBuilder.Append(']');

			var deviationsString = deviationsStringBuilder.ToString();
			var averagesString = averagesStringBuilder.ToString();

			var bandId =
				$"Band {bandWithGreatestDeviation} ([{settings.FrequencyBands[bandWithGreatestDeviation].Low}Hz-{settings.FrequencyBands[bandWithGreatestDeviation].High}Hz])";

			LogInfo(settings, results.GetLocation(),
				$"{bandId} has greatest deviation. Deviations: {deviationsString}. Averages: {averagesString}");
		}

		// Use the band with the greatest deviation.
		results.SetResults(bandAverages[bandWithGreatestDeviation], bandCorrelations[bandWithGreatestDeviation]);

		//// Combine onset envelopes
		//var combinedOnsetEnvelope = CombineOnsetEnvelopes(bandBuffers, settings, results);
		//token.ThrowIfCancellationRequested();

		//// Detect tempo using comb filter
		//DetectTempoWithCombFilter(combinedOnsetEnvelope, settings, out var correlations, out var average, out var deviation);
		//results.SetResults(average, correlations);
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
			if (highBin > 0)
			{
				for (var bin = lowBin; bin < highBin && bin < fftSize / 2; bin++)
				{
					bandComplex[bin] = complexSignal[bin];
					bandComplex[fftSize - bin - 1] = complexSignal[fftSize - bin - 1];
				}
			}
			else
			{
				for (var bin = lowBin; bin < fftSize / 2; bin++)
				{
					bandComplex[bin] = complexSignal[bin];
					bandComplex[fftSize - bin - 1] = complexSignal[fftSize - bin - 1];
				}
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

	private static void DetectTempoWithCombFilter(float[] signal, Settings settings, out float[] correlations, out float average,
		out float deviation)
	{
		var maxCorrelation = float.MinValue;
		var minCorrelation = float.MaxValue;
		average = 0.0f;
		deviation = 0.0f;
		var numCorrelations = (int)((settings.MaxTempo - settings.MinTempo) / settings.CombFilterResolution + 1);
		if (numCorrelations <= 0)
		{
			correlations = null;
			return;
		}

		correlations = new float[numCorrelations];

		for (var i = 0; i < numCorrelations; i++)
		{
			var tempo = settings.MinTempo + i * settings.CombFilterResolution;
			var delay = (int)(60.0 * settings.CombFilterBeats / tempo * settings.SampleRate);
			var correlation = (float)CombFilterCorrelation(signal, delay);
			correlations[i] = correlation;
			average += correlation;
			if (correlation > maxCorrelation)
				maxCorrelation = correlation;
			if (correlation < minCorrelation)
				minCorrelation = correlation;
		}

		average /= numCorrelations;
		var range = maxCorrelation - minCorrelation;
		for (var i = 0; i < numCorrelations; i++)
		{
			var scaledCorrelation = (correlations[i] - minCorrelation) * range;
			deviation += Math.Abs(scaledCorrelation - average);
		}

		deviation /= numCorrelations;
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
		var bestDivisibleTempos = new SortedSet<DivisibleTempo>(new TempoResultComparer());
		foreach (var locationResult in results.GetResultsByLocation())
		{
			var i = 0;
			var averageCorrelation = locationResult.GetAverageCorrelation();
			foreach (var correlation in locationResult.GetCorrelations())
			{
				var relativeCorrelation = correlation - averageCorrelation;
				CheckAndAddToBestTempos(settings.MinTempo + i * settings.CombFilterResolution, relativeCorrelation,
					settings, bestDivisibleTempos);
				i++;
			}
		}

		// For each of the best commonly divisible tempos, determine the best divisible tempo.
		foreach (var divisibleTempo in bestDivisibleTempos)
		{
			divisibleTempo.SelectBestDivisibleTempo(settings.MinTempo, settings.MaxTempo);
		}

		// Add the best divisible tempos to the results.
		var bestTemposList = new List<DivisibleTempo>();
		foreach (var bestTempo in bestDivisibleTempos)
			bestTemposList.Add(bestTempo);
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
				if (bestCorrelation - bestTemposList[i].GetCorrelation() > 0.02)
					break;

				var tempoToCheck = bestTemposList[i].GetTempo();

				var factor = bestTempo / tempoToCheck;

				// Don't try to replace a tempo with a fractional tempo.
				if (Math.Abs(tempoToCheck - (int)tempoToCheck) > 0.01)
					continue;

				// If the best tempo we found is one of the following ratios
				var replacedBestTempo = false;
				for (var j = 1; j < settings.CombFilterBeats * 4; j++)
				{
					var r = settings.CombFilterBeats > j ? settings.CombFilterBeats / j : j / settings.CombFilterBeats;
					var evenlyDivides = Math.Abs(r - (int)r) < 0.001;
					if (evenlyDivides)
						continue;

					// TODO: This check should be: is tempo - 1 interval <= badfactor && is tempo + 1 inverval >= badfactor

					var badFactor = settings.CombFilterBeats / j;
					if (Math.Abs(factor - badFactor) < 0.001)
					{
						LogInfo(settings,
							$"Replacing {bestTempo}bpm with {tempoToCheck}bpm because {bestTempo} is {settings.CombFilterBeats}/{j} of {tempoToCheck}.");

						results.SelectBestTempoIndex(i);
						replacedBestTempo = true;
						break;
					}
				}

				if (replacedBestTempo)
					break;
			}
		}
	}

	private static void CheckAndAddToBestTempos(double tempo, float correlation, Settings settings,
		SortedSet<DivisibleTempo> bestTempos)
	{
		// See if one of the current best tempos is divisible with the given tempo. If it is, update the correlation.
		DivisibleTempo matchingTempo = null;
		foreach (var bestTempo in bestTempos.Reverse())
		{
			if (bestTempo.ContainsTempo(tempo))
			{
				matchingTempo = bestTempo;
				bestTempos.Remove(matchingTempo);
				matchingTempo.UpdateCorrelationForTempo(tempo, correlation);
				bestTempos.Add(matchingTempo);
				break;
			}
		}

		// If this tempo isn't in the best, check for adding it.
		if (matchingTempo == null)
		{
			// Don't add this tempo if it is close to another tempo with a better correlation.
			foreach (var bestTempo in bestTempos.Reverse())
			{
				foreach (var correlatedTempo in bestTempo.GetCorrelatedTempos())
				{
					var diff = Math.Abs(correlatedTempo.Tempo - tempo);
					if (diff < settings.MinSeparationBetweenBestTempos)
					{
						if (bestTempo.GetBestCorrelation() >= correlation)
							return;
					}
				}
			}

			// Add this tempo if it has a better correlation than any of the best tempos so far.
			var addedTempo = false;
			foreach (var bestTempo in bestTempos)
			{
				if (correlation > bestTempo.GetBestCorrelation())
				{
					bestTempos.Add(new DivisibleTempo(tempo, correlation, settings.MinTempo, settings.MaxTempo));
					addedTempo = true;
					break;
				}
			}

			// Also add the tempo if we still have room for best tempos.
			if (!addedTempo && bestTempos.Count < settings.NumTemposToFind)
			{
				bestTempos.Add(new DivisibleTempo(tempo, correlation, settings.MinTempo, settings.MaxTempo));
				addedTempo = true;
			}

			// If we didn't meet the criteria above we are done.
			if (!addedTempo)
				return;
		}

		// Remove tempos which are close to the added tempo and have worse correlations.
		var shouldCheckForRemoving = true;
		while (shouldCheckForRemoving)
		{
			shouldCheckForRemoving = false;
			foreach (var bestTempo in bestTempos.Reverse())
			{
				if (bestTempo.GetBestCorrelation() >= correlation)
					continue;
				if (bestTempo.IsGivenTempoNearAnyCorrelatedTempo(tempo, settings.MinSeparationBetweenBestTempos))
				{
					bestTempos.Remove(bestTempo);
					shouldCheckForRemoving = true;
					break;
				}
			}
		}

		// Cap the size, removing the worst correlated tempo.
		while (bestTempos.Count > settings.NumTemposToFind)
		{
			bestTempos.Remove(bestTempos.Max);
		}
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
			var wavDir = Path.Combine([programDir, "tempo-detector-wavs", settings.WavFileFolder]);
			var wavFile = Path.Combine(wavDir, $"{fileName}.wav");
			System.IO.Directory.CreateDirectory(wavDir);
			WavWriter.WriteWavFile(wavFile, sampleData, settings.SampleRate, 1);
		}
		catch (Exception e)
		{
			LogError(settings, $"Failed to write wav file {fileName}: {e}");
		}
	}

	#region Logging

	private static string GetLogIdentifier(Settings settings)
	{
		if (!string.IsNullOrEmpty(settings.LogTag))
			return $"[TempoDetection {settings.LogTag}]";
		return "[TempoDetection]";
	}

	private static string GetLogIdentifier(Settings settings, Location location)
	{
		if (!string.IsNullOrEmpty(settings.LogTag))
			return $"[TempoDetection {settings.LogTag}] [{GetLocationString(location)}]";
		return $"[TempoDetection] [{GetLocationString(location)}]";
	}

	private static void LogError(Settings settings, Location location, string message)
	{
		if (!settings.ShouldLog)
			return;
		Logger.Error($"{GetLogIdentifier(settings, location)} {message}");
	}

	private static void LogWarn(Settings settings, Location location, string message)
	{
		if (!settings.ShouldLog)
			return;
		Logger.Warn($"{GetLogIdentifier(settings, location)} {message}");
	}

	private static void LogInfo(Settings settings, Location location, string message)
	{
		if (!settings.ShouldLog)
			return;
		Logger.Info($"{GetLogIdentifier(settings, location)} {message}");
	}

	private static void LogError(Settings settings, string message)
	{
		if (!settings.ShouldLog)
			return;
		Logger.Error($"{GetLogIdentifier(settings)} {message}");
	}

	private static void LogWarn(Settings settings, string message)
	{
		if (!settings.ShouldLog)
			return;
		Logger.Warn($"{GetLogIdentifier(settings)} {message}");
	}

	private static void LogInfo(Settings settings, string message)
	{
		if (!settings.ShouldLog)
			return;
		Logger.Info($"{GetLogIdentifier(settings)} {message}");
	}

	#endregion Logging
}

// ReSharper enable All 