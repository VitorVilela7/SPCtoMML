using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AM4Play.SNESAPU;

namespace SPCtoMML
{
	/// <summary>
	/// Used for tracking DSP data from the current SPC file.
	/// </summary>
	unsafe class DSPTrace
	{
		/// <summary>
		/// Holds the current DSP changes.
		/// </summary>
		private List<byte> dspChanges;
		/// <summary>
		/// Holds the (unmanaged) pointer for DSP change callback.
		/// </summary>
		private APU.CBFUNC callbackPointer;
		/// <summary>
		/// Holds the last 64000Hz SPC clock value.
		/// </summary>
		private uint lastTimer;
		/// <summary>
		/// Holds the last DSP values before callback.
		/// </summary>
		private byte[] ldsp;
		/// <summary>
		/// Holds the pointer to the DSP RAM.
		/// </summary>
		private byte* dsp;
		/// <summary>
		/// Holds the sample addresses for each channel.
		/// </summary>
		private int[] sampleAddr;
		/// <summary>
		/// Holds the sample loop addresses for each channel.
		/// </summary>
		private int[] sampleLoop;
		/// <summary>
		/// Total seconds to trace.
		/// </summary>
		private int totalSeconds;
		/// <summary>
		/// Current amount of seconds.
		/// </summary>
		private int currentSeconds;

		/// <summary>
		/// Obtains the current tracking progress.
		/// </summary>
		public double CurrentProgress
		{
			get
			{
				return currentSeconds / (double)totalSeconds;
			}
		}
		/// <summary>
		/// Gets the tracing results.
		/// </summary>
		public byte[] TraceResult
		{
			get
			{
				if (dspChanges == null)
				{
					return null;
				}
				else
				{
					return dspChanges.ToArray();
				}
			}
		}
		/// <summary>
		/// Gets the total amount of traces, discounting the ARAM copy and timer changes.
		/// </summary>
		public int TotalTraces
		{
			get
			{
				int total = 0;
				for (int i = 0x10000; i < dspChanges.Count; i += 2)
				{
					if (dspChanges[i] != 0xFF)
					{
						total++;
					}
				}
				return total;
			}
		}
		/// <summary>
		/// Gets the size of DSP traces.
		/// </summary>
		public int TraceLength
		{
			get
			{
				return dspChanges.Count;
			}
		}
		/// <summary>
		/// Initializes the pointers and prepares SNES APU for tracking instructions.
		/// </summary>
		public DSPTrace()
		{
			APU.GetPointers();
			this.dsp = APU.dsp;
			this.ldsp = new byte[128];
			this.sampleAddr = new int[8];
			this.sampleLoop = new int[8];
			this.lastTimer = *APU.T64COUNTER;
			this.callbackPointer = new APU.CBFUNC(CallBackAPU);
			APU.SNESAPUCallback(callbackPointer, APU.CallbackEffect.CBE_DSPREG);
			dspChanges = new List<byte>();
		}
		/// <summary>
		/// Traces the current loaded SPC on SNES APU for the specified number of seconds.
		/// </summary>
		/// <param name="seconds">Amount of seconds to trace.</param>
		public void Trace(int seconds)
		{
			initializeDSP();
			lastTimer = 0;
			totalSeconds = seconds;
			currentSeconds = 0;

			while (++currentSeconds < totalSeconds)
			{
				APU.EmuAPU(IntPtr.Zero, 24576000, 0);
			}
		}

		/// <summary>
		/// Prepares the DSP changes and inserts the initial values
		/// </summary>
		private void initializeDSP()
		{
			dspChanges.Clear();
			Marshal.Copy((IntPtr)this.dsp, ldsp, 0, 128);

			for (int i = 0; i < 0x10000; ++i)
			{
				dspChanges.Add(APU.ram[i]);
			}

			for (int i = 0; i < 8; ++i)
			{
				// FIR filter
				byte addr = (byte)((i << 4) | 0xF);

				dspChanges.Add(addr);
				dspChanges.Add(dsp[addr]);
			}
			for (int i = 0; i < 8; ++i)
			{
				// 0x0D settings
				if (i != 1)
				{
					byte addr = (byte)((i << 4) | 0xD);

					dspChanges.Add(addr);
					dspChanges.Add(dsp[addr]);
				}
			}
			for (int i = 0; i < 8; ++i)
			{
				// 0x0C settings
				if (i != 7 && i != 4 && i != 5)
				{
					byte addr = (byte)((i << 4) | 0xC);

					dspChanges.Add(addr);
					dspChanges.Add(dsp[addr]);
				}
			}

			int sampleDir = dsp[0x5d] << 8;

			for (int i = 0; i < 8; ++i)
			{
				// voice settings
				for (int j = 0; j < 8; ++j)
				{
					byte addr = (byte)((i << 4) | j);

					dspChanges.Add(addr);
					dspChanges.Add(dsp[addr]);
				}

				//set sample addresses/loop
				if ((dsp[0x4C] & (1 << i)) != 0)
				{
					int aramAddress = (sampleDir + dsp[i * 0x10 + 4] * 4) & 0xfffc;
					int sampleAddr = APU.ram[aramAddress + 1] << 8 | APU.ram[aramAddress + 0];
					int sampleLoop = APU.ram[aramAddress + 3] << 8 | APU.ram[aramAddress + 2];
					this.sampleAddr[i] = sampleAddr;
					this.sampleLoop[i] = sampleLoop;

					dspChanges.Add((byte)(0x80 | i));
					dspChanges.Add((byte)(sampleAddr));
					dspChanges.Add((byte)(0x88 | i));
					dspChanges.Add((byte)(sampleAddr >> 8));
					dspChanges.Add((byte)(0x90 | i));
					dspChanges.Add((byte)(sampleLoop));
					dspChanges.Add((byte)(0x98 | i));
					dspChanges.Add((byte)(sampleLoop >> 8));
				}
			}

			dspChanges.Add(0x5C);
			dspChanges.Add(dsp[0x5C]);
			dspChanges.Add(0x4C);
			dspChanges.Add(dsp[0x4C]);
		}
		/// <summary>
		/// Function called by SNES APU when a new DSP register write is detected from S-SMP.
		/// </summary>
		/// <param name="effect">The type of called (should be CBE_DSPREG)</param>
		/// <param name="addr">Address of the DSP register</param>
		/// <param name="data">The value being write to DSP register (mask the other 8-bit) </param>
		/// <param name="lpData">Should be null</param>
		/// <returns>Should return the data itself.</returns>
		private uint CallBackAPU(APU.CallbackEffect effect, uint addr, uint data, IntPtr lpData)
		{
			switch (effect)
			{
				case APU.CallbackEffect.CBE_DSPREG:
					if (ldsp[addr & 127] != (byte)data
						|| ((addr == 0x4C || addr == 0x5C) && (byte)data != 0))
					{
						uint diff = *APU.T64COUNTER - lastTimer;

						if (addr == 0x4C)
						{
							//command 0x80-0x87,0x88-0x8F,0x90-0x97,0x98-0x9F
							//sample low,high,loop low,loop high
							int sampleDir = ldsp[0x5D] << 8;

							//this code is used to detect possible sample changes,
							//even if the sample number is still the same.
							for (int i = 0, j = 1; i < 8; ++i, j <<= 1)
							{
								if ((data & j) != 0)
								{
									int aramAddress = (sampleDir + dsp[i * 0x10 + 4] * 4) & 0xfffc;
									int sampleAddr = APU.ram[aramAddress + 1] << 8 | APU.ram[aramAddress + 0];
									int sampleLoop = APU.ram[aramAddress + 3] << 8 | APU.ram[aramAddress + 2];

									if (this.sampleAddr[i] != sampleAddr || this.sampleLoop[i] != sampleLoop)
									{
										this.sampleAddr[i] = sampleAddr;
										this.sampleLoop[i] = sampleLoop;

										dspChanges.Add((byte)(0x80 | i));
										dspChanges.Add((byte)(sampleAddr));
										dspChanges.Add((byte)(0x88 | i));
										dspChanges.Add((byte)(sampleAddr >> 8));
										dspChanges.Add((byte)(0x90 | i));
										dspChanges.Add((byte)(sampleLoop));
										dspChanges.Add((byte)(0x98 | i));
										dspChanges.Add((byte)(sampleLoop >> 8));
									}
								}
							}
                        }

                        // round value
                        if (diff % 64 >= 32)
                        {
                            diff += 32;
                        }

                        // add timer changes. FF XX
                        // where XX is the time changed in milliseconds.
                        while (diff >= 64 * 256)
						{
							dspChanges.Add(0xFF);
							dspChanges.Add(0xFF);
							diff -= 256 << 6;
							lastTimer += 64 * 256;
						}

						if (diff >= 64)
						{
							dspChanges.Add(0xFF);
							dspChanges.Add((byte)((diff >> 6) - 1));
							lastTimer += 64 * (diff >> 6);
						}

						// Update the "whats changed in dsp"
						// and add addr/data to DSP changes.
						ldsp[addr & 127] = (byte)data;
						dspChanges.Add((byte)addr);
						dspChanges.Add((byte)data);
					}
					break;
			}

			// return same value written.
			return data;
		}
	}
}
