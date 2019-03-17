using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace SPCtoMML
{
	class MMLDumper
    {
        const bool allowDots = true;

        private Staccato staccatoSystem;
        private BeatCalculator beatCalculator;
        private Pitch pitchCalculator;

        private static readonly string[] notes = { "c", "c+", "d", "d+", "e", "f", "f+", "g", "g+", "a", "a+", "b" };
		private static Dictionary<int, int[]> findVolumeCache = new Dictionary<int, int[]>();

		private int ticksToBreakSection = 48;
		private StringBuilder currentOutput;

		// settings
		private bool allowVolumeAmplify;
		private bool allowStaccato;
		private bool allowAdvancedStaccato;
		private bool truncateSmallRests;
		private bool allowTuningCommand;
		private bool allowPitchVibrato;
		private string samplesFolder;

		// sample memory
		private List<int[]> sampleList = new List<int[]>();
		private int[] sampleMultipliers = new int[256];

		// remote commands
		private List<string> remoteCommands = new List<string>();

		// currently being used
		private int currentKeyOnRemoteCommand;
		private int currentKeyOffRemoteCommand;

		private int currentSample;
		private int currentOctave;
		private int currentTuning;
		private int currentVolume;
		private int currentAmplify;
		private int currentPanning;
		private int currentVolumeQ;
		private int currentEnvelope;
		private bool noiseEnable;
		private int leftSurround;
		private int rightSurround;
		private int currentVibratoAmplitude;
		private int currentVibratoDelay;
		private int currentVibratoTime;

		private int noiseClock;
		private bool noiseClockRefresh;

		private int echoDelay;
		private int echoEnable;
		private int echoFeedback;
		private int echoLeftVolume;
		private int echoRightVolume;
		private int echoLeftVolumeSlide;
		private int echoRightVolumeSlide;
		private int echoSlideLength;

		private int[] firFilter;

		private bool? allowEcho;
		private bool allowEchoUpdate;

		private bool firUpdate;
		private bool echoEnableUpdate;
		private bool echoDelayUpdate;
		private bool echoFeedbackUpdate;
		private bool echoVolumeUpdate;
		private bool echoSlideUpdate;

		// handlers ticks, tick compensation
		private double tickSync;
		private int totalTicks;
		private int lastNoteDur;

		private bool updateQ;

		private int[] masterVolume = new int[2];
		
		private int tempo;
		private bool insertTempo;

		private Note[][] noteData;
		private int[][] staccato;

		/// <summary>
		/// Current progress ratio.
		/// </summary>
		public double CurrentRatio { get; private set; }

		public MMLDumper(Note[][] data, int defaultTempo)
		{
			noteData = data;
			tempo = defaultTempo;
			insertTempo = true;
		}

		public void SetupPitch(bool tuning, bool vibrato)
		{
			this.allowTuningCommand = tuning;
			this.allowPitchVibrato = vibrato;
		}

		public void SetupVolume(bool amplify)
		{
			this.allowVolumeAmplify = amplify;
		}

		public void SetupPathSamples(string name)
		{
			this.samplesFolder = name;
		}

		public void SetupStaccato(bool enable, bool enableAdvanced, bool truncate)
		{
			this.allowStaccato = enable;
			this.allowAdvancedStaccato = enableAdvanced;
			this.truncateSmallRests = truncate;
		}

		public void CreateStaccatoMap()
		{
			staccato = new int[noteData.Length][];
            staccatoSystem = new Staccato();

			for (int ch = 0; ch < noteData.Length; ++ch)
			{
                // Update progress bar
                CurrentRatio = ch / (double)noteData.Length;

                if (noteData[ch] == null)
				{
					continue;
				}

				staccato[ch] = new int[noteData[ch].Length];
				bool rest = noteData[ch][0].IsRest;

				for (int n = 1; n < staccato[ch].Length; ++n)
				{
					if (noteData[ch][n].IsRest && !rest)
					{
						staccato[ch][n - 1] = noteData[ch][n].NoteLength;
					}

					rest = noteData[ch][n].IsRest;
				}
			}
		}

		public int CalculateTempo()
        {
            beatCalculator = new BeatCalculator();

            var tempList = new List<int>();

            for (int c = 0; c < 8; c++)
            {
                if (noteData[c] == null)
                {
                    continue;
                }

                int current = 0;
                int noteCount = 0;

                for (int i = 0; i < noteData[c].Length; i++)
                {
                    if (noteData[c][i].IsRest)
                    {
                        if (noteCount > 1)
                        {
                            tempList.Add(current + noteData[c][i].NoteLength);
                        }

                        current = 0;
                    }
                    else
                    {
                        noteCount++;
                        current = noteData[c][i].NoteLength;
                    }
                }

                if (current != 0 && noteCount > 1)
                {
                    tempList.Add(current);
                }
            }
            
            int foundTempo = beatCalculator.FindTempo(tempList.ToArray(), 2);

            tempo = foundTempo;
            insertTempo = true;
            return foundTempo;
        }
        
		public string OutputMML()
		{
			StringBuilder finalOutput = new StringBuilder();

			initVariables();

			for (int ch = 0; ch < 8; ++ch)
			{
				if (noteData[ch] == null)
				{
					continue;
				}

				initChannel();
				finalOutput.AppendFormat("#{0} ", ch);

				for (int l = 0; l < noteData[ch].Length; ++l)
				{
					CurrentRatio = (l / (double)noteData[ch].Length / 8.0) + (ch / 8.0);

					Note note2 = noteData[ch][l];
					int stac = staccato[ch][l];

					if (note2.IsRest && l == 0)
					{
						finalOutput.Append(processRest(note2));
					}
					else if (!note2.IsRest)
					{
						finalOutput.Append(processNote(note2, stac));
					}
				}

				finalOutput.AppendLine();
				finalOutput.AppendLine();
			}

			// insert sample header
			finalOutput.Insert(0, buildSampleList());

			// insert AMK header
			finalOutput.Insert(0, buildHeader());

			return finalOutput.ToString();
		}

		public void SetUpSampleMultiplier()
		{
            pitchCalculator = new Pitch();

			Dictionary<int, List<int>> pitchesPerSample = new Dictionary<int, List<int>>();

			foreach (var ch in noteData.Where(n => n != null))
			{
				foreach (var note in ch.Where(n => !n.IsRest && n.PitchCache.Length > 0))
				{
					if (!pitchesPerSample.ContainsKey(note.Sample))
					{
						pitchesPerSample.Add(note.Sample, new List<int>());
					}

					if (!pitchesPerSample[note.Sample].Contains(note.PitchCache[0]))
					{
						pitchesPerSample[note.Sample].Add(note.PitchCache[0]);
					}
				}
			}

			var array = pitchesPerSample.ToArray();

			for (int i = 0; i < array.Length; ++i)
			{
				CurrentRatio = 0.1 + i / (double)array.Length * 9.0 / 10.0;
				sampleMultipliers[array[i].Key] = pitchCalculator.FindPitchMultiplier(array[i].Value.ToArray(), allowTuningCommand);
			}
		}

		private string processNote(Note note, int staccato)
		{
			int ticks = timeToTicks(note.NoteLength);
			int stacTicks = timeToTicks(staccato, 1);

			if (ticks < 1)
			{
				if (stacTicks > 0)
				{
					return processRest(stacTicks);
				}
				else
				{
					return "";
				}
			}

			currentOutput = new StringBuilder();
			processNoteEvents(note.Events);

			mmlTempoUpdate();
			mmlEchoUpdate();
			mmlSampleUpdate(note.Sample, note.GainCache[0]);
			mmlNoiseUpdate(note.UseNoise, note.Sample, note.GainCache[0]);
			//mmlEnvelopeUpdate(note.GainCache, ref ticks, ref stacTicks);
			mmlVolumeUpdate(note.VolumeCache[0]);

			int[] pitchData = parsePitchCachePass2(note.PitchCache);
			int[] noteData = pitchCalculator.FindNote(pitchData[3], sampleMultipliers[note.Sample], allowTuningCommand ? -1 : 0);

			if (pitchData.Length > 4 && pitchData[4] == 3)
			{
				int[] noteData2 = pitchCalculator.FindNote(pitchData[7], sampleMultipliers[note.Sample]);
				int difference = Math.Abs(noteData2[1] - noteData[1] + (noteData2[0] - noteData[0]) * 256);

				int delay = Math.Min(255, timeToTicks(pitchData[5], 2));
				int time = timeToTicks(pitchData[6], 2);
				int amplitude;

				if (time != 0)
				{
					time = (int)Math.Max(1, Math.Round(256 / (double)time));
				}
				else
				{
					time = 0xFF;
				}

				if (time > 0xFF)
				{
					time = 0xFF;
				}

				if (difference >= 237)
				{
					amplitude = ((difference / 0xFC) & 15) + 0xF0;
				}
				else
				{
					amplitude = (int)Math.Ceiling(difference * 256 / (double)0xFC);
				}

				int limit = 4;

				if (Math.Abs(currentVibratoAmplitude - amplitude) > limit ||
					Math.Abs(currentVibratoDelay - delay) > limit ||
					Math.Abs(currentVibratoTime - time) > limit)
				{
					currentVibratoAmplitude = amplitude;
					currentVibratoDelay = delay;
					currentVibratoTime = time;
					currentOutput.AppendFormat("$DE ${0:X2} ${1:X2} ${2:X2} ", delay, time, amplitude);
				}
			}
			else
			{
				if (currentVibratoDelay < ticks && currentVibratoAmplitude != 0)
				{
					currentVibratoAmplitude = 0;
					currentOutput.AppendFormat("$DF ");
				}
			}

			mmlOctaveUpdate(noteData[0] / 12);
			mmlTuningUpdate(noteData[1]);

			handleStaccato(ref ticks, ref stacTicks, pitchData.Length <= 4);
			currentOutput.Append(notes[noteData[0] % 12]);
			int firstLengthPosition = currentOutput.Length;

			int currentTicks = ticks;
			bool insertedDelay = false;
			bool insertedSlide = false;
			int lastNote = noteData[0];
			int lastDelay = pitchData[1];
			int lastTime = 0;

			for (int i = pitchData.Length > 4 && pitchData[4] == 3 ? 8 : 4; i < pitchData.Length; i += 4)
			{
				int delay = timeToTicks(pitchData[i + 1] - lastDelay, 2);
				int time = Math.Min(255, timeToTicks(pitchData[i + 2] - pitchData[i + 1], 2));

				int[] slideData = pitchCalculator.FindNote(pitchData[i + 3], sampleMultipliers[currentSample], currentTuning);

				if (lastNote == slideData[0] || time < 1)
				{
					continue;
				}
				else if (delay >= currentTicks)
				{
					break;
				}

				if (delay < 2)
				{
					delay = 0;
				}
				if (insertedSlide)
				{
					delay = Math.Max(lastTime + 1, delay);
				}

				if (delay > 0)
				{
					currentOutput.Append(getNoteLength(delay, true));

					if (!insertedSlide)
					{
						if (!insertedDelay)
						{
							firstLengthPosition = currentOutput.Length;
						}
						insertedDelay = true;
					}
				}

				lastNote = slideData[0];
				lastDelay = pitchData[i + 1];
				lastTime = time;

				string noteString = notes[slideData[0] % 12];
				if (currentOctave != slideData[0] / 12)
				{
					//currentOctave = slideData[0] / 12;
					noteString = "o" + (slideData[0] + 1) + noteString;
				}

				currentOutput.AppendFormat(" $DD ${0:X2} ${1:X2} ${3:X2} ", 0, time, noteString, slideData[0] | 0x80);
				currentOutput.Append("^");

				currentTicks -= delay;
				insertedSlide = true;
			}

			// insertedDelay x insertedSlide truth table
			// true   true => true
			// false  true => false
			// false false => true
			// true  false => you're doing something wrong VV (true) (== will give false)

			while (currentTicks < 2)
			{
				currentTicks++;
				tickSync--;
			}

			var lengthData = getNoteLength(currentTicks, insertedSlide).Split('^');
			if (lengthData.Length > 1 && !insertedSlide)
				currentOutput.Insert(firstLengthPosition, insertedDelay == insertedSlide ? "^" : "");
			currentOutput.Insert(firstLengthPosition, lengthData[0]);
			if (insertedSlide)
				currentOutput.Insert(firstLengthPosition, insertedDelay == insertedSlide ? "^" : "");
			currentOutput.Append(String.Join("^", lengthData, 1, lengthData.Length - 1));

			removeLastChar('^');
			removeLastChar(' ');

			breakSection(ticks);

			if (stacTicks > 0)
			{
				string copy = currentOutput.ToString();
				processRest(stacTicks);
				currentOutput.Insert(0, copy);
			}

			return currentOutput.ToString();
		}

		private void removeLastChar(char c)
		{
			if (currentOutput[currentOutput.Length - 1] == c)
			{
				currentOutput.Remove(currentOutput.Length - 1, 1);
			}
		}

		private void handleStaccato(ref int length, ref int staccato, bool allowQuant)
		{
			if (!allowStaccato)
			{
				// update volume if needed and return
				if (updateQ)
				{
					updateQ = false;
					currentOutput.AppendFormat("q7{0:X} ", currentVolumeQ);
				}
				return;
			}

			allowQuant &= allowAdvancedStaccato;

            // TO DO...
            int noteDur = 0;
            int bestDiff = int.MaxValue;

            Debug.WriteLine($"Before stac: {staccato} {length}");

            var list = staccatoSystem.FindStaccatoGivenDuration(length);
            Staccato.StaccatoPointer pickedStaccato = null;

            foreach (var item in list)
            {
                if (staccato - item.Staccato < bestDiff && item.Staccato <= staccato)
                {
                    bestDiff = staccato - item.Staccato;
                    pickedStaccato = item;
                }
                else if (staccato - item.Staccato == bestDiff && item.Index == lastNoteDur)
                {
                    pickedStaccato = item;
                }
            }

            if (pickedStaccato == null)
            {
                foreach (var item in list)
                {
                    if (Math.Abs(staccato - item.Staccato) < bestDiff)
                    {
                        bestDiff = Math.Abs(staccato - item.Staccato);
                        pickedStaccato = item;
                    }
                    else if (Math.Abs(staccato - item.Staccato) == bestDiff && item.Index == lastNoteDur)
                    {
                        pickedStaccato = item;
                    }
                }
            }

            staccato -= pickedStaccato.Staccato;
            length += pickedStaccato.Staccato;
            noteDur = pickedStaccato.Index;

            Debug.WriteLine($"After stac: {staccato} {length}");
            Debug.WriteLine($"Verify: {staccatoSystem.FindStaccatoTicks(length, noteDur)} == {pickedStaccato.Staccato}");

            if (updateQ || lastNoteDur != noteDur)
			{
				updateQ = false;
				lastNoteDur = noteDur;
				currentOutput.AppendFormat("q{0}{1:X} ", noteDur, currentVolumeQ);
			}
		}

		private string processRest(Note note)
		{
			int ticks = timeToTicks(note.NoteLength, 1);

			if (ticks > 0)
			{
				return processRest(ticks);
			}
			else
			{
				return "";
			}
		}

		private string processRest(int ticks)
		{
			currentOutput = new StringBuilder();

			mmlTempoUpdate();
			mmlEchoUpdate();

			currentOutput.AppendFormat("r{0}", getNoteLength(ticks));
			breakSection(ticks);

			return currentOutput.ToString();
		}

		private void processNoteEvents(NoteEvent[] events)
		{
			if (events == null)
			{
				return;
			}

			bool echoSync = false;

			foreach (NoteEvent e in events)
			{
				// special events should get updated even if the previous
				// value says the otherwise.
				switch (e.EventType)
				{
					case NoteEventType.EnableEcho:
						if (allowEcho != null && !(bool)allowEcho)
						{
							allowEchoUpdate = true;
							echoDelayUpdate = true;
							echoEnableUpdate = true;
							echoVolumeUpdate = true;
							echoFeedbackUpdate = true;
							firUpdate = true;
						}
						allowEcho = true;
						break;

					case NoteEventType.DisableEcho:
						if (allowEcho != false)
						{
							allowEchoUpdate = true;
							allowEcho = false;
						}
						break;

					case NoteEventType.PitchModulationUpdate:
						mmlPitchModulationUpdate(e.EventData[0]);
						break;

					case NoteEventType.MasterVolumeUpdate:
						masterVolume = e.EventData;
						for (int i = 0; i < 2; ++i)
						{
							masterVolume[i] = signedByteToInt((byte)masterVolume[i]);
						}
						break;

					case NoteEventType.NoiseUpdate:
						noiseClock = e.EventData[1];
						noiseClockRefresh = true;
						// noise enable is ignored, since the engine
						// uses .UseNoise
						break;

					case NoteEventType.EchoDelayUpdate:
						echoDelay = e.EventData[0];
						if (!echoSync) echoDelayUpdate = true;
						break;

					case NoteEventType.EchoEnableUpdate:
						echoEnable = e.EventData[0];
						if (!echoSync) echoEnableUpdate = true;
						break;

					case NoteEventType.EchoFeedbackUpdate:
						echoFeedback = e.EventData[e.EventData.Length - 2];
						if (!echoSync)
						{
							echoFeedbackUpdate = true;
						}
						break;

					case NoteEventType.EchoFirFilterUpdate:
						firFilter = e.EventData;
						if (!echoSync) firUpdate = true;
						break;

					case NoteEventType.EchoVolumeUpdate:
						if (e.EventData.Length <= 4)
						{
							echoLeftVolume = signedByteToInt((byte)(e.EventData[e.EventData.Length - 2] & 255));
							echoRightVolume = signedByteToInt((byte)(e.EventData[e.EventData.Length - 2] >> 8));
							if (!echoSync) echoVolumeUpdate = true;
							if (!echoSync) echoSlideUpdate = false;
						}
						else
						{
							int length = e.EventData.Length;
							int index = 2;
							int mode = e.EventData[index - 2] > e.EventData[index] ? 1 : 0;
							bool okSlide = true;

							for (index = 2; index < length; index += 2)
							{
								if (mode != (e.EventData[index - 2] > e.EventData[index] ? 1 : 0))
								{
									//okSlide = false;
									break;
								}
							}

							if (index == 2)
							{
								okSlide = false;
							}

							if (!echoSync) echoVolumeUpdate = true;

							if (okSlide)
							{
								echoLeftVolume = signedByteToInt((byte)(e.EventData[0] & 255));
								echoRightVolume = signedByteToInt((byte)(e.EventData[0] >> 8));
								echoLeftVolumeSlide = signedByteToInt((byte)(e.EventData[index - 2] & 255));
								echoRightVolumeSlide = signedByteToInt((byte)(e.EventData[index - 2] >> 8));
								echoSlideLength = timeToTicks(e.EventData[index - 1], 2);
								if (echoSlideLength > 0xFF) echoSlideLength = 0xFF;
								if (!echoSync) echoSlideUpdate = true;
							}
							else
							{
								echoLeftVolume = signedByteToInt((byte)(e.EventData[index - 2] & 255));
								echoRightVolume = signedByteToInt((byte)(e.EventData[index - 2] >> 8));
								echoLeftVolumeSlide = 0;
								echoRightVolumeSlide = 0;
								echoSlideLength = 0;
								if (!echoSync) echoSlideUpdate = false;
							}
						}
						break;

					case NoteEventType.EchoSync:
						echoSync = true;
						break;

					default:
						throw new InvalidOperationException();
				}
			}
		}

		private int[] parsePitchCachePass2(int[] pitchCache)
		{
			int[] cache = parsePitchCachePass1(pitchCache);

			if (!allowPitchVibrato)
			{
				return cache;
			}

			Dictionary<int, List<int>> pitchDelta = new Dictionary<int, List<int>>();

			SortedDictionary<int, int> pitchMap = new SortedDictionary<int, int>();
			SortedDictionary<int, List<int>> pitchHeat = new SortedDictionary<int, List<int>>();

			int last = cache[3];

			for (int i = 4; i < cache.Length; i += 4)
			{
				int pitch = cache[i + 3];
				int timer = cache[i + 2];

				if (!pitchMap.ContainsKey(pitch))
				{
					pitchMap[pitch] = 0;
					pitchHeat[pitch] = new List<int>();
				}

				pitchMap[pitch]++;
				pitchHeat[pitch].Add(timer);

				int delta = Math.Abs(cache[i + 3] - last);
				last = cache[i + 3];

				if (pitchDelta.ContainsKey(delta))
				{
					pitchDelta[delta][0]++;
				}
				else
				{
					pitchDelta[delta] = new List<int>(new[] { 1 });
				}

				pitchDelta[delta].Add(i);
			}

			var map2 = pitchMap.Where(x => x.Value > 1).OrderBy(x => x.Key).ToArray();

			if (map2.Length == 0)
			{
				return cache;
			}
			else if (map2.Length == 1)
			{
				map2 = pitchMap.OrderBy(x => x.Key).ToArray();
			}

			int lowestPitch = map2.First().Key;
			int highestPitch = map2.Last().Key;
			int frequency;

			if (pitchHeat[lowestPitch].Count > 1 && pitchHeat[highestPitch].Count > 1)
			{
				var low = pitchHeat[lowestPitch];
				var high = pitchHeat[highestPitch];

				int count = 0;
				frequency = 0;

				for (int i = 1; i < low.Count; ++i, ++count)
				{
					frequency += Math.Abs(low[i - 1] - low[i]);
				}
				for (int i = 1; i < high.Count; ++i, ++count)
				{
					frequency += Math.Abs(high[i - 1] - high[i]);
				}

				if (count != 0)
				{
					frequency /= count;
				}
			}
			else
			{
				frequency = Math.Abs(pitchHeat[lowestPitch][0] - pitchHeat[highestPitch][0]) * 2;
			}

			if (pitchDelta.Count > 0)
			{
				var topList = pitchDelta.OrderBy(x => x.Value[0]).ToArray();
				var top = topList.Last();

				if (top.Value[0] > 1)
				{
					List<int> cache2 = new List<int>();

					for (int i = 0; i < 4; ++i)
					{
						cache2.Add(cache[i]);
					}

					int delay = cache[5];
					int totalTime = cache[cache.Length - 2];
					int occurences = (cache.Length / 4 + 1);

					if (delay + frequency * occurences >= totalTime)
					{
						cache2.Add(3);
						cache2.Add(delay);
						cache2.Add(frequency);
						cache2.Add(cache[3] + (highestPitch - lowestPitch) / 2);
						return cache2.ToArray();
					}
				}
			}

			return cache;
		}

		private int[] parsePitchCachePass1(int[] pitchCache)
		{
			// parse and compact pitch cache into actual actions.
			// output format: <TYPE> <DELAY> <TIME> <PITCH>
			// <TYPE>: 00 = direct change, 01 = slide up, 02 = slide down, 03 = vibrato

			if (pitchCache.Length < 2)
			{
				// this should never happen, but who knows.
				return new[] { 0, 0, 0, 0 };
			}

			int mode = 0;
			int index = 2;
			int count = 1;
			int delay = 0;

			List<int> cache = new List<int>();

			while (index != pitchCache.Length)
			{
				int currentMode = pitchCache[index] > pitchCache[index - 2] ? 1 : 2;

				if (currentMode != mode)
				{
					cache.Add(count == 1 ? 0 : mode);
					cache.Add(delay);
					cache.Add(pitchCache[index + 1]); // yes + 1 not - 1
					cache.Add(pitchCache[index - 2]);

					delay = pitchCache[index + 1];
					count = 0;
				}

				mode = currentMode;
				index += 2;
				count++;
			}

			cache.Add(count == 1 ? 0 : mode);
			cache.Add(delay);
			cache.Add(pitchCache[index - 1]);
			cache.Add(pitchCache[index - 2]);
			return cache.ToArray();
		}

		private void initVariables()
		{
			sampleList.Clear();
			remoteCommands.Clear();

			for (int d = 0; d < 2; ++d)
			{
				masterVolume[d] = 0x7F;
			}
		}

		private void initChannel()
		{
			allowEchoUpdate = false;
			allowEcho = null;

			echoVolumeUpdate = false;
			echoEnableUpdate = false;
			echoDelayUpdate = false;
			echoSlideUpdate = false;
			firUpdate = false;

			echoDelay = 0;
			echoEnable = 0;
			echoFeedback = 0;
			echoLeftVolume = 0;
			echoRightVolume = 0;
			echoLeftVolumeSlide = 0;
			echoRightVolumeSlide = 0;
			echoSlideLength = 0;

			firFilter = new int[8];

			noiseEnable = false;
			noiseClockRefresh = false;
			noiseClock = 0x00;

			currentSample = -1;
			currentOctave = -1;
			currentEnvelope = -1;
			currentTuning = 0;

			currentVolume = 255;
			currentAmplify = 0;
			currentPanning = 10;
			currentVolumeQ = 15;
			leftSurround = 0;
			rightSurround = 1;

			tickSync = 0;
			totalTicks = 0;
			lastNoteDur = 7;
			updateQ = false;

			currentKeyOffRemoteCommand = -1;
			currentKeyOnRemoteCommand = -1;

			currentVibratoAmplitude = 0;
			currentVibratoDelay = 0;
			currentVibratoTime = 0;
		}

		private string buildHeader()
		{
			StringBuilder output = new StringBuilder();
			output.AppendLine("#amk 2");
			output.AppendLine();

			for (int i = 0; i < remoteCommands.Count; ++i)
			{
				output.AppendFormat("(!{0})[{1}]", i + 1, remoteCommands[i]);
				output.AppendLine();
			}
			if (remoteCommands.Count != 0)
			{
				output.AppendLine();
			}

			return output.ToString();
		}

		private string buildSampleList()
		{
			StringBuilder output = new StringBuilder();

			output.AppendFormat("#path \"{0}\"", samplesFolder);
			output.AppendLine();
			output.AppendLine();

			output.AppendLine("#samples");
			output.AppendLine("{");

			for (int i = 0; i < sampleList.Count; ++i)
			{
				output.AppendFormat("\t\"sample_{0:X2}.brr\"", sampleList[i][0]);
				output.AppendLine();
			}

			output.AppendLine("}");
			output.AppendLine();

			output.AppendLine("#instruments");
			output.AppendLine("{");

			for (int i = 0; i < sampleList.Count; ++i)
			{
				output.AppendFormat("\t\"sample_{0:X2}.brr\"\t${2:X2} ${3:X2} ${4:X2} ${5:X2} ${6:X2}\t; @{1}",
					sampleList[i][0], i + 30, sampleList[i][1], sampleList[i][2],
					sampleList[i][3], sampleList[i][4], sampleList[i][5]);
				output.AppendLine();
			}

			output.AppendLine("}");
			output.AppendLine();

			return output.ToString();
		}

		private void breakSection(int ticks)
		{
			int sectionCount1 = totalTicks / ticksToBreakSection;
			int sectionCount2 = (totalTicks += ticks) / ticksToBreakSection;
			int breaks = sectionCount2 - sectionCount1;

			if (breaks > 0)
			{
				do
				{
					currentOutput.AppendLine();
				} while (--breaks > 0);
			}
			else
			{
				currentOutput.Append(" ");
			}
		}

		private void mmlTempoUpdate()
		{
			if (insertTempo)
			{
				insertTempo = false;
				currentOutput.AppendFormat("w255 t{0} ", tempo);
			}
		}

		private void mmlPitchModulationUpdate(int enableBits)
		{
			// update regardless of state
			currentOutput.AppendFormat("$FA $00 ${0:X2} ", enableBits & 0xFE);
		}

		private void mmlNoiseUpdate(bool enable, int sample, int envelope)
		{
			if (noiseEnable != enable || noiseClockRefresh)
			{
				noiseEnable = enable;

				if (enable)
				{
					noiseClockRefresh = false;
					currentOutput.AppendFormat("n{0:X2} ", noiseClock);
				}
				else if (!noiseClockRefresh)
				{
					// reload current sample
					currentSample = -1;
					mmlSampleUpdate(sample, envelope);
				}
			}
		}

		private void mmlEchoUpdate()
		{
			if (allowEchoUpdate && !(bool)allowEcho)
			{
				currentOutput.Append("$F0 ");
				allowEchoUpdate = false;
				return;
			}

			if (allowEcho != null && !(bool)allowEcho)
			{
				return;
			}

			allowEchoUpdate = false;
			mmlEchoVolumeUpdate();

			if (echoDelayUpdate || echoFeedbackUpdate)
			{
				echoDelayUpdate = echoFeedbackUpdate = false;

				currentOutput.AppendFormat("$F1 ${0:X2} ${1:X2} ${2:X2} ", echoDelay & 15, echoFeedback, 0x01);
				firUpdate = true;
			}

			if (firUpdate)
			{
				firUpdate = false;
				int firTest = 0;
				for (int i = 0; i < 8; ++i)
				{
					if (i == 0 && firFilter[i] == 0x7F)
					{
						continue;
					}
					firTest |= firFilter[i];
				}

				if (firTest != 0)
				{
					currentOutput.Append("$F5 ");
					for (int i = 0; i < 8; ++i)
					{
						currentOutput.AppendFormat("${0:X2} ", firFilter[i]);
					}
				}
			}
		}

		private void mmlEchoVolumeUpdate()
		{
			if (!echoSlideUpdate && !echoEnableUpdate && !echoVolumeUpdate)
			{
				return;
			}

			int leftVolume = intToSignedByte((int)Math.Max(-128, Math.Min(127,
				Math.Round(echoLeftVolume * 0x7F / (double)masterVolume[0]))));
			int rightVolume = intToSignedByte((int)Math.Max(-128, Math.Min(127,
				Math.Round(echoRightVolume * 0x7F / (double)masterVolume[1]))));

			int leftVolumeS = intToSignedByte((int)Math.Max(-128, Math.Min(127,
				Math.Round(echoLeftVolumeSlide * 0x7F / (double)masterVolume[0]))));
			int rightVolumeS = intToSignedByte((int)Math.Max(-128, Math.Min(127,
				Math.Round(echoRightVolumeSlide * 0x7F / (double)masterVolume[1]))));

			//[11:24:37] <AlcaRobot> $EF $FF $XX $YY (command, channels, left vol, right vol)
			currentOutput.AppendFormat("$EF ${0:X2} ${1:X2} ${2:X2} ", echoEnable, leftVolume, rightVolume);

			if (echoSlideUpdate)
			{
				currentOutput.AppendFormat("$F2 ${0:X2} ${1:X2} ${2:X2} ", echoSlideLength, leftVolumeS, rightVolumeS);
			}

			echoVolumeUpdate = false;
			echoSlideUpdate = false;
			echoEnableUpdate = false;
		}

		private void mmlSampleUpdate(int sample, int envelope)
		{
			if (currentSample != sample)
			{
				noiseEnable = false;
				currentSample = sample;

				int instrument = sampleList.FindIndex(x => x[0] == sample) + 30;

				if (instrument == 29)
				{
					instrument += sampleList.Count + 1;
					sampleList.Add(new[] { sample, envelope & 255, envelope >> 8 & 255, envelope >> 16 & 255,
						sampleMultipliers[sample] >> 8, sampleMultipliers[sample] & 255 });
				}

				currentOutput.AppendFormat("@{0} ", instrument);

				currentEnvelope = sampleList[instrument - 30][1] |
					(sampleList[instrument - 30][2] << 8) | (sampleList[instrument - 30][3] << 16);
				disableRemoteCommand();
			}
		}

		private void mmlEnvelopeUpdate(int[] gainCache, ref int length, ref int staccato)
		{
			int envelope = gainCache[0];

			if (gainCache.Length > 2)
			{
				List<int> envelopes = new List<int>();
				List<int> triggers = new List<int>();

				for (int i = 1; i < gainCache.Length; i += 2)
				{
					int temp = timeToTicks(gainCache[i], 2);

					if (temp < length)
					{
						triggers.Add(temp);
						envelopes.Add(gainCache[i - 1]);
					}
				}

				if (triggers.Count > 0)
				{
					int trigger = triggers.Last();
					int envelope2 = envelopes.Last();

					// some inside songs can change more than one envelope at once.
					// regardless of what happen, the last envelope change is used.

					insertRemoteCommand(convertEnvelope(envelope), false);
					insertRemoteCommand(convertEnvelope(envelope2), true);
					staccato += length - trigger;
					length = trigger;
				}

			}
			else if (currentEnvelope != envelope)
			{
				disableRemoteCommand();
				currentEnvelope = envelope;
				currentOutput.Append(convertEnvelope(currentEnvelope));
				currentOutput.Append(' ');
			}
		}

		/// <summary>
		/// Inserts a remote command on current channel.
		/// </summary>
		/// <param name="command">The command to insert at.</param>
		/// <param name="fadeOut">True to apply it on key off, false for key on.</param>
		private void insertRemoteCommand(string command, bool fadeOut)
		{
			int index = addRemoteCommand(command);
			if (fadeOut && currentKeyOffRemoteCommand != index)
			{
				currentKeyOffRemoteCommand = index;
				currentOutput.AppendFormat("(!{0},3) ", index);
			}
			else if (!fadeOut && currentKeyOnRemoteCommand != index)
			{
				currentKeyOnRemoteCommand = index;
				currentOutput.AppendFormat("(!{0},-1) ", index);
			}
		}

		/// <summary>
		/// Disables current remote command.
		/// </summary>
		private void disableRemoteCommand()
		{
			if (currentKeyOnRemoteCommand != -1 || currentKeyOffRemoteCommand != -1)
			{
				currentKeyOffRemoteCommand = currentKeyOnRemoteCommand = -1;
				currentOutput.Append("(!0,0) ");
			}
		}

		/// <summary>
		/// Adds a remote command to the table list.
		/// </summary>
		/// <param name="command">The remote command contents.</param>
		/// <returns>The remote command number inserted on table.</returns>
		private int addRemoteCommand(string command)
		{
			int index = remoteCommands.IndexOf(command);
			if (index == -1)
			{
				index = remoteCommands.Count;
				remoteCommands.Add(command);
			}
			return index + 1;
		}

		/// <summary>
		/// Converts the current envelope into a AMK command.
		/// </summary>
		/// <param name="envelope">The envelope in 0xZZYYXX format where in order (XX->ZZ) is ADSR1,
		/// ADSR2 and GAIN.</param>
		/// <returns>The command.</returns>
		private string convertEnvelope(int envelope)
		{
			int adsr1 = envelope & 255;
			int adsr2 = envelope >> 8 & 255;
			int gain = envelope >> 16 & 255;

			int inst = sampleList.FindIndex(x => x[0] == currentSample);

			if (adsr1 == sampleList[inst][1] && adsr2 == sampleList[inst][2] && gain == sampleList[inst][3])
			{
				return "$F4 $09";
			}

			if ((adsr1 & 0x80) != 0)
			{
				return String.Format("$ED ${0:X2} ${1:X2}", adsr1 & 0x7f, adsr2);
			}
			else
			{
				return String.Format("$ED $80 ${0:X2}", gain);
			}
		}

		private void mmlOctaveUpdate(int octave)
		{
			if (currentOctave != octave)
			{
				if (currentOctave == -1)
				{
					currentOutput.AppendFormat("o{0} ", octave + 1);
				}
				else
				{
					while (octave - currentOctave != 0)
					{
						if (octave - currentOctave > 0)
						{
							currentOutput.Append('>');
							currentOctave++;
						}
						else
						{
							currentOutput.Append('<');
							currentOctave--;
						}
					}

					currentOutput.Append(' ');
				}
				currentOctave = octave;
			}
		}

		private void mmlTuningUpdate(int newTuning)
		{
			if (currentTuning != newTuning && allowTuningCommand)
			{
				currentTuning = newTuning;
				currentOutput.AppendFormat("$EE ${0:X2} ", currentTuning);
			}
		}

		private void mmlVolumeUpdate(int mixedVolume)
		{
			int leftVolume = (int)Math.Ceiling(signedByteToInt((byte)(mixedVolume & 0xFF)) * masterVolume[0] / 127.0);
			int rightVolume = (int)Math.Ceiling(signedByteToInt((byte)(mixedVolume >> 8)) * masterVolume[1] / 127.0);

			leftVolume = Math.Max(-128, Math.Min(127, leftVolume));
			rightVolume = Math.Max(-128, Math.Min(127, rightVolume));

			leftVolume = intToSignedByte(leftVolume);
			rightVolume = intToSignedByte(rightVolume);

			mixedVolume = leftVolume | (rightVolume << 8);

			int[] volData;

			int volumeHash = mixedVolume | ((allowVolumeAmplify ? 1 : 0) << 16);

			if (!findVolumeCache.TryGetValue(volumeHash, out volData))
			{
				volData = VolumeCalc.FindVolume2(new[] { leftVolume, rightVolume }, 1, allowVolumeAmplify);
				findVolumeCache[volumeHash] = volData;
			}

			if (currentVolumeQ != volData[0])
			{
				currentVolumeQ = volData[0];
				updateQ = true;
			}

			if (currentVolume != volData[2])
			{
				currentVolume = volData[2];
				currentOutput.AppendFormat("v{0} ", currentVolume);
			}

			if (currentPanning != volData[1] || leftSurround != volData[7] || rightSurround != volData[8])
			{
				currentPanning = volData[1];
				leftSurround = volData[7];
				rightSurround = volData[8];

				if ((leftSurround | rightSurround) != 0)
				{
					currentOutput.AppendFormat("y{0},{1},{2} ", currentPanning, leftSurround, rightSurround);
				}
				else
				{
					currentOutput.AppendFormat("y{0} ", currentPanning);
				}
			}

			if (currentAmplify != volData[3])
			{
				currentAmplify = volData[3];
				currentOutput.AppendFormat("$FA $03 ${0:X2} ", currentAmplify);
			}

			// TO DO: amplify, surround, slide, etc.
		}

		private string getNoteLengthStep2(int ticks)
		{
			int dotCandidate = 1;
			int dotCount = 0;

			if (192 % ticks == 0)
			{
				return (192 / ticks).ToString();
			}

			while (dotCandidate < 192 && dotCount == 0 && allowDots)
			{
				// should return 2, 3, 4, 6, 8, 12, 16, etc.
				while ((192 / ++dotCandidate > ticks || 192 % dotCandidate != 0)) ;

				for (int dots = 0; dots <= 10; ++dots)
				{
					int c = dotCandidate;
					int length = 0;

					for (int d = 0; d <= dots && c < 192; ++d, c <<= 1)
					{
						length += 192 / c;
					}

					if (length == ticks)
					{
						dotCount = dots;
						break;
					}
					else if (length > ticks)
					{
						break;
					}
				}
			}

			if (dotCount == 0)
			{
				return "=" + ticks;
			}
			else
			{
				return String.Format("{0}{1}", dotCandidate, String.Join("", Enumerable.Repeat(".", dotCount)));
			}
		}

		private string getNoteLength(int ticks, bool disallowWhole = false)
		{
			StringBuilder output = new StringBuilder();
			int key1 = 192;
			int key2 = 1;
			bool first = true;

			if (192 % ticks == 0 && !(disallowWhole && ticks == 192))
			{
				return (192 / ticks).ToString();
			}
			else if (ticks < 128)
			{
				return getNoteLengthStep2(ticks);
			}
			else if (disallowWhole)
			{
				key1 >>= 1;
				key2 <<= 1;
			}

			while (key1 > 0 && key2 <= 192 && ticks > 128)
			{
				while (ticks >= key1)
				{
					if (!first)
					{
						output.Append("^");
					}
					output.Append(key2);
					first = false;
					ticks -= key1;
				}
				key1 >>= 1;
				key2 <<= 1;
			}

			if (ticks != 0)
			{
				if (!first)
				{
					output.Append("^");
				}

				output.Append(getNoteLengthStep2(ticks));
			}

			return output.ToString();
		}

		/// <summary>
		/// converts time to note ticks.
		/// </summary>
		/// <param name="millisecond">the time in milliseconds</param>
		/// <param name="mode">0 -> tick compensation, 1 -> only store compensation, 2 -> no compensation</param>
		/// <returns></returns>
		private int timeToTicks(double millisecond, int mode = 0)
		{
			double ticks = millisecond * tempo / 512.0;

			if (mode == 2)
			{
				return (int)Math.Ceiling(ticks);
			}
			else if (mode == 1)
			{
				int intTicks = (int)Math.Floor(ticks);
				tickSync += ticks - intTicks;

				return intTicks;
			}
			else
			{
				int intTicks = (int)Math.Round(ticks);
				tickSync += ticks - intTicks;

				int change = (int)tickSync;

                double score1 = beatCalculator.RateDuration(intTicks);
                double score2 = beatCalculator.RateDuration(intTicks + change);

                if (score2 > score1)
                {
                    tickSync -= change;
                    return intTicks + change;
                }
                else
                {
                    return intTicks;
                }
			}
		}

		private int signedByteToInt(byte value)
		{
			if (value >= 0x80)
			{
				return -((value ^ 0xFF) + 1);
			}
			else
			{
				return value;
			}
		}

		private byte intToSignedByte(int value)
		{
			if (value < 0)
			{
				return (byte)((Math.Abs(value) ^ 0xFF) + 1);
			}
			else
			{
				return (byte)value;
			}
		}
	}
}
