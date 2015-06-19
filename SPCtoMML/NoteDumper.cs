using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace SPCtoMML
{
	class NoteDumper
	{
		private int timer;
		private byte[] dspTraces;

		List<Note>[] output = new List<Note>[8];
		List<string> remoteCommands = new List<string>();

		List<uint> sampleAddresses = new List<uint>();

		uint[] currentSampleAddress = new uint[8];

		bool[] channelEnable = new bool[8];

		int[] currentSample = new int[8];

		int[] voiceActive = new int[8];
		int[] voiceInactive = new int[8];

		int[] currentPitch = new int[8];
		int[] currentVolume = new int[8];
		int[] currentEnvelope = new int[8];

		List<int>[] pitchCache = new List<int>[8];
		List<int>[] volumeCache = new List<int>[8];
		List<int>[] envelopeCache = new List<int>[8];

		int[] firFilter = new int[8];

		int[] masterVolume = new int[2];
		int echoVolume;

		List<int> echoVolumeCache = new List<int>();
		List<int> echoFeedbackCache = new List<int>();

		bool[] masterVolumeUpdate = new bool[8]; // important to update all chs
		bool pitchModulationUpdate;
		bool noiseUpdate;
		bool noiseClockUpdate;
		bool echoEnableUpdate;
		bool echoDelayUpdate;
		bool firFilterUpdate;

		int echoFeedback;
		int echoEnable;
		int echoDelay;

		int pitchModulationEnable;
		int noiseEnable;
		int noiseClock;

		int ch;

		bool? allowEcho;
		bool updateEcho;

		public NoteDumper(byte[] traces)
		{
			this.dspTraces = traces;
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

		private void initDump()
		{
			pitchModulationUpdate = false;
			noiseUpdate = false;
			noiseClockUpdate = false;
			echoDelayUpdate = false;
			echoEnableUpdate = false;
			firFilterUpdate = false;
			updateEcho = false;
			allowEcho = null;

			echoFeedback = 0x00;
			echoEnable = 0x00;
			echoDelay = 0x00;
			pitchModulationEnable = 0x00;
			noiseEnable = 0x00;
			noiseClock = 0x00;


			timer = 0;

			echoVolumeCache.Clear();
			echoFeedbackCache.Clear();
			echoVolume = -1;

			sampleAddresses.Clear();

			for (int i = 0; i < 2; ++i)
			{
				masterVolume[i] = 0;
			}

			for (ch = 0; ch < 8; ++ch)
			{
				output[ch] = new List<Note>();

				masterVolumeUpdate[ch] = false;

				voiceActive[ch] = -1;
				voiceInactive[ch] = 0;

				currentSample[ch] = 0x00;
				currentSampleAddress[ch] = 0x000000;

				currentPitch[ch] = 0;
				currentVolume[ch] = 0;
				currentEnvelope[ch] = 0;

				pitchCache[ch] = new List<int>();
				volumeCache[ch] = new List<int>();
				envelopeCache[ch] = new List<int>();
				
				channelEnable[ch] = false;

				// note that FIR has nothing to do with channels.
				firFilter[ch] = 0x00;
			}
		}

		private void mainLoop()
		{
			for (int i = 0x10000; i < dspTraces.Length; i += 2)
			{
				int addr = dspTraces[i + 0];
				int value = dspTraces[i + 1];

				if (addr >= 0x80 && addr <= 0xA0)
				{
					int ch = addr & 7;
					int mode = addr & 0x18;
					currentSampleAddress[ch] &= ~(0xFFU << mode);
					currentSampleAddress[ch] |= (uint)value << mode;

					if (mode == 0x18)
					{
						int index = sampleAddresses.IndexOf(currentSampleAddress[ch]);

						if (index == -1)
						{
							index = sampleAddresses.Count;
							sampleAddresses.Add(currentSampleAddress[ch]);
						}

						currentSample[ch] = index;
					}
					continue;
				}

				if ((addr & 0x0F) >= 0x0C)
				{
					// internal reg
					switch (addr)
					{
						case 0x0F:
						case 0x1F:
						case 0x2F:
						case 0x3F:
						case 0x4F:
						case 0x5F:
						case 0x6F:
						case 0x7F:
							if (firFilter[addr >> 4] != value)
							{
								firFilter[addr >> 4] = value;
								firFilterUpdate = true;
							}
							break;

						case 0x0C:
						case 0x1C:
							if (masterVolume[addr >> 4] != value)
							{
								masterVolume[addr >> 4] = value;
								for (ch = 0; ch < 8; ++ch)
								{
									masterVolumeUpdate[ch] = true;
								}
							}
							break;


						case 0x2C: // Echo L volume
							echoVolume = (echoVolume & 0xFF00) | value;
							updateEchoVolumeCache();
							break;

						case 0x3C: // Echo R volume
							echoVolume = (echoVolume & 0x00FF) | (value << 8);
							updateEchoVolumeCache();
							break;

						case 0x6C:
							if ((value & 0x1F) != noiseClock)
							{
								noiseClock = value & 0x1F;
								noiseClockUpdate = true;
							}
							bool echoBufferWrite = (value & 0x20) == 0;
							if (echoBufferWrite != allowEcho)
							{
								allowEcho = echoBufferWrite;
								updateEcho = true;
							}
							break;

						case 0x0D:
							if (echoFeedback != value)
							{
								echoFeedback = value;
								updateEchoFeedbackCache();
							}
							break;

						case 0x2D:
							if (value != pitchModulationEnable)
							{
								pitchModulationEnable = value;
								pitchModulationUpdate = true;
							}
							break;

						case 0x3D:
							if (value != noiseEnable)
							{
								noiseEnable = value;
								noiseUpdate = true;
							}
							break;

						case 0x4D:
							if (value != echoEnable)
							{
								echoEnable = value;
								echoEnableUpdate = true;
							}
							break;

						case 0x7D:
							if (value != echoDelay)
							{
								echoDelay = value;
								echoDelayUpdate = true;
							}
							break;

						case 0x4C: // key on
							{
								for (ch = 0; ch < 8; ++ch)
								{
									if (((value >> ch) & 1) == 0)
									{
										// there's no reason to process key on twice.
										// or process a inactive voice.
										continue;
									}

									finishCache();

									if (voiceActive[ch] >= 0)
									{
										// key on after key on.
										handleNote(true);
									}
									else
									{
										// key on after key off.
										handleRest();
									}

									resetNote();
								}
								break;
							}

						case 0x5C: // key off
							{
								for (ch = 0; ch < 8; ++ch)
								{
									if (voiceActive[ch] < 0 || ((value >> ch) & 1) == 0)
									{
										// there's no reason to process key off twice.
										// or process a inactive voice.
										continue;
									}

									finishCache();
									handleNote(false);
									resetNote();
								}
								break;
							}

						case 0xFF: // increase counter
							{
								timer += value + 1;
								break;
							}
					}
					continue;
				}

				ch = addr >> 4;
				int reg = addr & 15;

				switch (reg) // 0x00 ~ 0x07
				{
					case 0x00: // L volume
						currentVolume[ch] = (currentVolume[ch] & 0xFF00) | value;
						updateVolumeCache();
						break;

					case 0x01: // R volume
						currentVolume[ch] = (currentVolume[ch] & 0x00FF) | (value << 8);
						updateVolumeCache();
						break;

					case 0x02: // pitch low byte
						currentPitch[ch] = (currentPitch[ch] & 0xFF00) | value;
						updatePitchCache();
						break;

					case 0x03: // pitch high byte
						currentPitch[ch] = (currentPitch[ch] & 0x00FF) | (value << 8);
						updatePitchCache();
						break;

					//case 0x04: // sample
					//    currentSample[ch] = value;
					//    break;

					case 0x05: // ADSR 1
						currentEnvelope[ch] &= 0xFFFF00;
						currentEnvelope[ch] |= value;
						updateEnvelopeCache();
						break;

					case 0x06: // ADSR 2
						currentEnvelope[ch] &= 0xFF00FF;
						currentEnvelope[ch] |= value << 8;
						updateEnvelopeCache();
						break;

					case 0x07: // GAIN
						currentEnvelope[ch] &= 0x00FFFF;
						currentEnvelope[ch] |= value << 16;
						updateEnvelopeCache();
						break;
				}
			}
		}

		public Note[][] OutputNoteData()
		{
			// init variables
			initDump();

			// generate note Data
			mainLoop();

			// prepare output
			Note[][] finalOutput = new Note[8][];

			for (ch = 0; ch < 8; ++ch)
			{
				if (channelEnable[ch])
				{
					if (voiceInactive[ch] != -1)
					{
						finishCache();
						handleRest();
					}
					else if (voiceActive[ch] != -1)
					{
						finishCache();
						handleNote(false);
					}

					finalOutput[ch] = output[ch].ToArray();
				}
				else
				{
					finalOutput[ch] = null;
				}
			}

			// return with note output
			return finalOutput;
		}

		/// <summary>
		/// Fix cache counters.
		/// </summary>
		private void finishCache()
		{
			for (int j = 3; j < pitchCache[ch].Count; j += 2)
			{
				pitchCache[ch][j] = pitchCache[ch][j] - pitchCache[ch][1];
			}
			for (int j = 3; j < volumeCache[ch].Count; j += 2)
			{
				volumeCache[ch][j] = volumeCache[ch][j] - volumeCache[ch][1];
			}
			for (int j = 3; j < envelopeCache[ch].Count; j += 2)
			{
				envelopeCache[ch][j] = envelopeCache[ch][j] - envelopeCache[ch][1];
			}
			for (int j = 3; j < echoVolumeCache.Count; j += 2)
			{
				echoVolumeCache[j] = echoVolumeCache[j] - echoVolumeCache[1];
			}
			for (int j = 3; j < echoFeedbackCache.Count; j += 2)
			{
				echoFeedbackCache[j] = echoFeedbackCache[j] - echoFeedbackCache[1];
			}

			pitchCache[ch][1] = 0;
			volumeCache[ch][1] = 0;
			envelopeCache[ch][1] = 0;

			if (echoVolumeCache.Count > 0)
			{
				echoVolumeCache[1] = 0;
			}
			if (echoFeedbackCache.Count > 0)
			{
				echoFeedbackCache[1] = 0;
			}
		}

		/// <summary>
		/// Clear caches.
		/// </summary>
		private void resetNote()
		{
			pitchCache[ch].Clear();
			volumeCache[ch].Clear();
			envelopeCache[ch].Clear();
			//echoVolumeCache.Clear();
			//echoFeedbackCache.Clear();

			// refill cache with a initial value
			pitchCache[ch].Add(currentPitch[ch]);
			pitchCache[ch].Add(timer);

			volumeCache[ch].Add(currentVolume[ch]);
			volumeCache[ch].Add(timer);

			envelopeCache[ch].Add(currentEnvelope[ch]);
			envelopeCache[ch].Add(timer);

			//echoVolumeCache.Add(echoVolume);
			//echoVolumeCache.Add(timer);

			//echoFeedbackCache.Add(echoFeedback);
			//echoFeedbackCache.Add(timer);
		}

		private Note createNote(bool rest, int length)
		{
			Note note = new Note();
			note.IsRest = rest;
			note.NoteLength = length;
			note.PitchCache = pitchCache[ch].ToArray();
			note.VolumeCache = volumeCache[ch].ToArray();
			note.GainCache = envelopeCache[ch].ToArray();
			note.UseEcho = ((echoEnable >> ch) & 1) != 0;
			note.UsePitchModulation = ((pitchModulationEnable >> ch) & 1) != 0;
			note.UseNoise = ((noiseEnable >> ch) & 1) != 0;
			note.Sample = currentSample[ch];

			if (!note.IsRest)
			{
				int maxPitch = 0;

				for (int i = 0; i < note.PitchCache.Length; i += 2)
				{
					maxPitch = Math.Max(note.PitchCache[i], maxPitch);
				}

				if (maxPitch == 0)
				{
					note.IsRest = true;
				}
				else
				{
					note.Events = updateEvents();
				}
			}

			return note;
		}

		private NoteEvent[] updateEvents()
		{
			// check for meta events
			List<NoteEvent> events = new List<NoteEvent>();

			if (masterVolumeUpdate[ch])
			{
				events.Add(new NoteEvent(NoteEventType.MasterVolumeUpdate, masterVolume));
				masterVolumeUpdate[ch] = false;
			}
			if (pitchModulationUpdate)
			{
				events.Add(new NoteEvent(NoteEventType.PitchModulationUpdate, pitchModulationEnable));
				pitchModulationUpdate = false;
			}
			if (noiseUpdate || noiseClockUpdate)
			{
				events.Add(new NoteEvent(NoteEventType.NoiseUpdate, noiseEnable, noiseClock));
				noiseUpdate = false;
				noiseClockUpdate = false;
			}

			bool syncEcho = false;
			bool syncEchoDone = false;

		reUpdate:
			if ((updateEcho || syncEchoDone) && allowEcho != null)
			{
				if ((bool)allowEcho)
				{
					events.Add(new NoteEvent(NoteEventType.EnableEcho));
				}
				else
				{
					events.Add(new NoteEvent(NoteEventType.DisableEcho));
				}
				updateEcho = false;
			}

			if (echoVolumeCache.Count != 0)
			{
				events.Add(new NoteEvent(NoteEventType.EchoVolumeUpdate, echoVolumeCache.ToArray()));
				echoVolumeCache.Clear();
				syncEcho = true;
			}
			if (echoFeedbackCache.Count != 0)
			{
				events.Add(new NoteEvent(NoteEventType.EchoFeedbackUpdate, echoFeedbackCache.ToArray()));
				echoFeedbackCache.Clear();
				syncEcho = true;
			}
			if (echoDelayUpdate || syncEchoDone)
			{
				echoDelayUpdate = false;
				events.Add(new NoteEvent(NoteEventType.EchoDelayUpdate, echoDelay));
				syncEcho = true;
			}
			if (echoEnableUpdate || syncEchoDone)
			{
				echoEnableUpdate = false;
				events.Add(new NoteEvent(NoteEventType.EchoEnableUpdate, echoEnable));
				syncEcho = true;
			}
			if (firFilterUpdate || syncEchoDone)
			{
				firFilterUpdate = false;
				events.Add(new NoteEvent(NoteEventType.EchoFirFilterUpdate, firFilter));
				syncEcho = true;
			}

			if (syncEcho && !syncEchoDone)
			{
				events.Add(new NoteEvent(NoteEventType.EchoSync));
				events.Add(new NoteEvent(NoteEventType.EchoFeedbackUpdate, echoFeedback, 0));
				events.Add(new NoteEvent(NoteEventType.EchoVolumeUpdate, echoVolume, 0));
				syncEchoDone = true;
				syncEcho = false;
				goto reUpdate;
			}
			channelEnable[ch] = true;

			return events.ToArray();
		}

		/// <summary>
		/// Handlers a note when fully created.
		/// </summary>
		private void handleNote(bool rekey)
		{
			int total = timer - voiceActive[ch];

			if (total > 0)
			{
				output[ch].Add(createNote(false, total));
			}

			if (!rekey)
			{
				voiceActive[ch] = -1;
				voiceInactive[ch] = timer;
			}
			else
			{
				voiceActive[ch] = timer;
				voiceInactive[ch] = -1;
			}
		}

		/// <summary>
		/// Handlers when a rest is fully created.
		/// </summary>
		private void handleRest()
		{
			int total = timer - voiceInactive[ch];

			if (total > 0)
			{
				output[ch].Add(createNote(true, total));
			}

			voiceActive[ch] = timer;
			voiceInactive[ch] = -1;
		}

		private void updateEchoVolumeCache()
		{
			if (echoVolumeCache.Count >= 2 && echoVolume == echoVolumeCache[echoVolumeCache.Count - 2])
			{
				return;
			}

			if (echoVolumeCache.Count > 1 && timer - echoVolumeCache.Last() < 4)
			{
				echoVolumeCache[echoVolumeCache.Count - 2] = echoVolume;
			}
			else
			{
				echoVolumeCache.Add(echoVolume);
				echoVolumeCache.Add(timer);
			}
		}

		private void updateEchoFeedbackCache()
		{
			if (echoFeedbackCache.Count >= 2 && echoFeedback == echoFeedbackCache[echoFeedbackCache.Count - 2])
			{
				return;
			}

			if (echoFeedbackCache.Count > 1 && timer - echoFeedbackCache.Last() < 4)
			{
				echoFeedbackCache[echoFeedbackCache.Count - 2] = echoFeedback;
			}
			else
			{
				echoFeedbackCache.Add(echoFeedback);
				echoFeedbackCache.Add(timer);
			}
		}

		private void updatePitchCache()
		{
			// ignore if new pitch is same as old pitch...
			if (pitchCache[ch].Count >= 2 && currentPitch[ch] == pitchCache[ch][pitchCache[ch].Count - 2])
			{
				return;
			}

			// merge if last pitch cache is very early.
			if (pitchCache[ch].Count > 1 && timer - pitchCache[ch].Last() < 3)
			{
				pitchCache[ch][pitchCache[ch].Count - 2] = currentPitch[ch];
				pitchCache[ch][pitchCache[ch].Count - 1] = timer;
			}
			else
			{
				pitchCache[ch].Add(currentPitch[ch]);
				pitchCache[ch].Add(timer);
			}
		}

		private void updateVolumeCache()
		{
			// ignore if new volume is same as old volume...
			if (volumeCache[ch].Count >= 2 && currentVolume[ch] == volumeCache[ch][volumeCache[ch].Count - 2])
			{
				return;
			}

			// merge if last volume cache is very early.
			if (volumeCache[ch].Count > 1 && timer - volumeCache[ch].Last() < 3)
			{
				volumeCache[ch][volumeCache[ch].Count - 2] = currentVolume[ch];
				volumeCache[ch][volumeCache[ch].Count - 2] = timer;
			}
			else
			{
				volumeCache[ch].Add(currentVolume[ch]);
				volumeCache[ch].Add(timer);
			}
		}

		private void updateEnvelopeCache()
		{
			// ignore if new envelope is same as old envelope...
			if (envelopeCache[ch].Count >= 2 && currentEnvelope[ch] == envelopeCache[ch][envelopeCache[ch].Count - 2])
			{
				return;
			}

			// merge if last gain cache is very early.
			if (envelopeCache[ch].Count > 1 && timer - envelopeCache[ch].Last() < 3)
			{
				envelopeCache[ch][envelopeCache[ch].Count - 2] = currentEnvelope[ch];
				envelopeCache[ch][envelopeCache[ch].Count - 2] = timer;
			}
			else
			{
				envelopeCache[ch].Add(currentEnvelope[ch]);
				envelopeCache[ch].Add(timer);
			}
		}

	}
}
