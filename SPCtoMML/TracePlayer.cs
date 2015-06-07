using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AM4Play.SNESAPU;
using System.Threading;
using WaveLib;

namespace SPCtoMML
{
	unsafe class TracePlayer
	{
		WaveOutPlayer player;
		bool playing;

		byte[] traces;

		int tPtr;
		int wait;

		public TracePlayer()
		{
			playing = false;
		}

		public void Init(byte[] traces)
		{
			// create a fake spc file
			this.traces = traces;
			byte[] spc = new byte[66048];
			Array.Copy(traces, 0, spc, 0x100, 0x10000);
			APU.ResetAPU(0xFFFFFFFF);
			APU.LoadSPCFile(spc);
			APU.GetPointers();
			APU.SetAPUOpt(2, 2, 16, 32000, Interpolation.INT_SINC, 0);
			tPtr = 0x10000;
			wait = 0;
		}

		public void Play()
		{
			if (player == null)
			{
				player = new WaveOutPlayer(-1, new WaveFormat(32000, 16, 2), 5120, 2, Filler);
			}
			playing = true;
		}

		public void Stop()
		{
			if (player != null)
			{
				player.Dispose();
				player = null;
			}
			playing = false;
		}

		public void Pause()
		{
			playing = false;
		}

		public void Filler(IntPtr pointer, int bytes)
		{
			uint* sPtr = (uint*)pointer;

			if (!playing)
			{
				for (int i = 0; i < bytes; i += 4)
				{
					*sPtr++ = 0;
				}
				return;
			}
			
			do
			{
				// 1 sample = 1/32 seconds

				if (wait == 0)
				{
					while (traces[tPtr] != 0xFF)
					{
						if (traces[tPtr] >= 0x80)
						{
							int ch = (traces[tPtr] & 7) * 0x10;
							int mode = (traces[tPtr] & 0x18) >> 3;
							uint aram = (uint)((APU.dsp[0x5D] << 8) + (APU.dsp[ch | 4] << 2) + mode);
							SPC700.SetAPURAM(aram, traces[tPtr + 1]);
							tPtr += 2;
							continue;
						}

						DSP.SetDSPReg(traces[tPtr], traces[tPtr + 1]);
						tPtr += 2;

						if (tPtr == traces.Length)
						{
							tPtr = 0x10000;
						}
					}
					wait = traces[tPtr + 1] + 1;
					tPtr += 2;
					if (tPtr == traces.Length)
					{
						tPtr = 0x10000;
					}
				}

				APU.EmuAPU(sPtr, 32, 1);
				sPtr += 32;
				--wait;
			} while ((bytes -= 128) > 0);
		}
	}
}