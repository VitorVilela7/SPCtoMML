/***************************************************************************************************
* Program:    SNES Audio Processing Unit (APU) Emulator                                            *
* Platform:   Intel 80386                                                                          *
* Programmer: Anti Resonance (Alpha-II Productions), sunburst (degrade-factory)                    *
*                                                                                                  *
* "SNES" and "Super Nintendo Entertainment System" are trademarks of Nintendo Co., Limited and its *
* subsidiary companies.                                                                            *
*                                                                                                  *
* This program is free software; you can redistribute it and/or modify it under the terms of the   *
* GNU General Public License as published by the Free Software Foundation; either version 2 of     *
* the License, or (at your option) any later version.                                              *
*                                                                                                  *
* This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;        *
* without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.        *
* See the GNU General Public License for more details.                                             *
*                                                                                                  *
* You should have received a copy of the GNU General Public License along with this program;       *
* if not, write to the Free Software Foundation, Inc.                                              *
* 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.                                        *
*                                                                                                  *
*                                                 Copyright (C) 2003-2006 Alpha-II Productions     *
*                                                 Copyright (C) 2003-2008 degrade-factory          *
***************************************************************************************************/

using System.Runtime.InteropServices;
using b8 = System.Boolean;
using u16 = System.UInt16;
using u32 = System.UInt32;
using u8 = System.Byte;
using IntPtr = System.IntPtr;
using System;

namespace AM4Play.SNESAPU
{
	unsafe static class APU
	{
		public static byte* dsp;
		public static byte* xram;
		public static byte* ram;
		public static byte* outPort;
		public static Voice* voice;
        public static uint* VMAXL;
        public static uint* VMAXR;
        public static uint* T64COUNTER;

		public static void GetPointers()
		{
			fixed (u8** dspPtr = &dsp)
			{
				fixed (u8** xramPtr = &xram)
				{
					fixed (u8** ramPtr = &ram)
					{
						fixed (u8** portPtr = &outPort)
						{
                            fixed (Voice** ptr = &voice)
                            {
                                fixed (uint** VMAXL_PTR = &VMAXL)
                                {
                                    fixed (uint** VMAXR_PTR = &VMAXR)
                                    {
                                        fixed (uint** T64_PTR = &T64COUNTER)
                                        {
                                            GetAPUData(ramPtr, xramPtr, portPtr, T64_PTR, dspPtr, ptr, VMAXL_PTR, VMAXR_PTR);
                                        }
                                    }
                                }
                            }
						}
					}
				}
			}
		}

		/// <summary>
		/// Get SNESAPU.DLL Version Information
		/// </summary>
		/// <param name="pVer">SNESAPU.DLL version (32bit)</param>
		/// <param name="pMin">SNESAPU.DLL compatible version (32bit)</param>
		/// <param name="pOpt">SNESAPU.DLL option flags</param>
		[DllImport("SNESAPU")]
		public static extern void SNESAPUInfo(u32* pVer, u32* pMin, u32* pOpt);

		/// <summary>
		/// Get SNESAPU Data Pointers
		/// </summary>
		/// <param name="ppRAM">64KB Sound RAM</param>
		/// <param name="ppXRAM">128byte extra RAM</param>
		/// <param name="ppOutPort">APU 4 ports of output</param>
		/// <param name="ppT64Cnt">64kHz timer counter</param>
		/// <param name="ppDSP">128byte DSPRAM structure (see DSP.inc)</param>
		/// <param name="ppVoice">VoiceMix structures of 8 voices (see DSP.inc)</param>
		/// <param name="ppVMMaxL">Max master volume (left)</param>
		/// <param name="ppVMMaxR">Max master volume (right)</param>
		[DllImport("SNESAPU")]
		public static extern void GetAPUData(u8** ppRAM, u8** ppXRAM, u8** ppOutPort, u32** ppT64Cnt, u8** ppDSP, Voice** ppVoice, u32** ppVMMaxL, u32** ppVMMaxR);

		/// <summary>
		/// Get Script700 Data Pointers
		/// </summary>
		/// <param name="pDLLVer">SNESAPU version (32byte string)</param>
		/// <param name="ppSPCReg">Pointer of SPC700 register</param>
		/// <param name="ppScript700">Pointer of Script700 work memory</param>
		[DllImport("SNESAPU")]
		public static extern void GetScript700Data(char* pDLLVer, u32** ppSPCReg, u8** ppScript700);

		/// <summary>
		/// Clears all memory, sets registers to default values, and sets the amplification level.
		/// </summary>
		/// <param name="amp">Amplification (-1 = keep current amp level, see SetDSPAmp for more information)</param>
		[DllImport("SNESAPU")]
		public static extern void ResetAPU(u32 amp);

		/// <summary>
		/// Prepares the sound processor for emulation after an .SPC/.ZST is loaded.
		/// </summary>
		/// <param name="pc">SPC700 internal registers</param>
		/// <param name="a">SPC700 internal registers</param>
		/// <param name="y">SPC700 internal registers</param>
		/// <param name="x">SPC700 internal registers</param>
		/// <param name="psw">SPC700 internal registers</param>
		/// <param name="sp">SPC700 internal registers</param>
		[DllImport("SNESAPU")]
		public static extern void FixAPU(u16 pc, u8 a, u8 y, u8 x, u8 psw, u8 sp);

		/// <summary>
		/// Restores the APU state from an SPC file.  This eliminates the need to call ResetAPU, copy memory,
		/// and call FixAPU.
		/// </summary>
		/// <param name="pFile">66048 byte SPC file</param>
		[DllImport("SNESAPU")]
		public static extern void LoadSPCFile(void* pFile);

		/// <summary>
		/// Restores the APU state from an SPC file.  This eliminates the need to call ResetAPU, copy memory,
		/// and call FixAPU.
		/// </summary>
		/// <param name="pFile">66048 byte SPC file</param>
		public static void LoadSPCFile(byte[] pFile)
		{
			fixed (byte* ptr = pFile)
			{
				LoadSPCFile(ptr);
			}
		}

		/// <summary>
		/// Configures the sound processor emulator.  Range checking is performed on all parameters.
		/// -1 can be passed for any parameter you want to remain unchanged
		/// see SetDSPOpt() in DSP.h for a more detailed explantion of the options
		/// </summary>
		/// <param name="mix">Mixing routine (default 1)</param>
		/// <param name="chn">Number of channels (1 or 2, default 2)</param>
		/// <param name="bits">Sample size (8, 16, 24, 32, or -32 [IEEE 754], default 16)</param>
		/// <param name="rate">Sample rate (8000-192000, default 32000)</param>
		/// <param name="inter">Interpolation type (default INT_GAUSS)</param>
		/// <param name="opts">See 'DSP options' in the Defines section of DSP.h</param>
		[DllImport("SNESAPU")]
		public static extern void SetAPUOpt(u32 mix, u32 chn, u32 bits, u32 rate, Interpolation inter, u32 opts);

		/// <summary>
		/// Calculates the ratio of emulated clock cycles to sample output.  Used to speed up or slow down a
		/// song without affecting the pitch.
		/// </summary>
		/// <param name="speed">Multiplier [16.16] (1/2x to 16x)</param>
		[DllImport("SNESAPU")]
		public static extern void SetAPUSmpClk(u32 speed);

		/// <summary>
		/// Set Audio Processor Song Length
		/// Sets the length of the song and fade
		/// Notes:  If a song is not playing, you must call ResetAPU or set T64Cnt to 0 before calling this.
		/// To set a song with no length, pass -1 and 0 for the song and fade.
		/// </summary>
		/// <param name="song">Length of song (in 1/64000ths second)</param>
		/// <param name="fade">Length of fade (in 1/64000ths second)</param>
		/// <returns>Total length</returns>
		[DllImport("SNESAPU")]
		public static extern u32 SetAPULength(u32 song, u32 fade);

		/// <summary>
		/// Emulate Audio Processing Unit
		///
		/// Emulates the APU for a specified amount of time.  DSP output is placed in a buffer to be handled
		/// by the main program.
		/// </summary>
		/// <param name="pBuf">Buffer to store output samples</param>
		/// <param name="len">Length of time to emulate (must be > 0)</param>
		/// <param name="type">0 - len is the number of APU clock cycles to emulate (APU_CLK = 1 second); 1 - len is the number of samples to generate</param>
		/// <returns>End of buffer (pointer)</returns>
		[DllImport("SNESAPU")]
		public static extern void* EmuAPU(void* pBuf, u32 len, u8 type);

		/// <summary>
		/// Emulate Audio Processing Unit
		///
		/// Emulates the APU for a specified amount of time.  DSP output is placed in a buffer to be handled
		/// by the main program.
		/// </summary>
		/// <param name="pBuf">Buffer to store output samples</param>
		/// <param name="len">Length of time to emulate (must be > 0)</param>
		/// <param name="type">0 - len is the number of APU clock cycles to emulate (APU_CLK = 1 second); 1 - len is the number of samples to generate</param>
		/// <returns>End of buffer (pointer)</returns>
		[DllImport("SNESAPU")]
		public static extern IntPtr EmuAPU(IntPtr pBuf, u32 len, u8 type);

		/// <summary>
		/// Seeks forward in the song from the current position
		/// </summary>
		/// <param name="time">1/64000ths of a second to seek forward (must be >= 0)</param>
		/// <param name="fast">Use faster seeking method (may break some songs)</param>
        [DllImport("SNESAPU")]
        public static extern void SeekAPU(u32 time, b8 fast);

        [DllImport("SNESAPU")]
        public static extern void SeekAPU(u32 time, u32* fast);

		/// <summary>
		/// Set/Reset TimerTrick Compatible Function
		/// The setting of TimerTrick is converted into Script700, and it functions as Script700.
		/// </summary>
		/// <param name="port">SPC700 port number (0 - 3 / 0xF4 - 0xF7).</param>
		/// <param name="wait">Wait time (1 - 0xFFFFFFFF).  If this parameter is 0, TimerTrick and Script700 is disabled.</param>
		[DllImport("SNESAPU")]
		public static extern void SetTimerTrick(u32 port, u32 wait);

		/// <summary>
		/// Set/Reset Script700 Compatible Function
		/// Script700 is a function to emulate the signal exchanged between 65C816 and SPC700 of SNES.
		/// Out:
		///    = Return value is a binary-converting result of the Script700 command.
		///      >=1 : Last index of array of the program memory used.  Script700 is enabled.
		///      0   : NULL was set in the pSource parameter.  Script700 is disabled.
		///      -1  : Error occurred by binary-converting Script700.  Script700 is disabled.
		/// </summary>
		/// <param name="pSource">Pointer to a null-terminated string buffer in which the Script700 command data was stored.  If this parameter is NULL, Script700 is disabled.</param>
		/// <returns></returns>
		[DllImport("SNESAPU")]
		public static extern u32 SetScript700(void* pSource);



		//**************************************************************************************************
		// Set SNESAPU.DLL Callback Function
		//
		// In:
		//    pCbFunc -> Pointer of SNESAPU callback function
		//               Callback function definition:
		//                   u32 Callback(u32 effect, u32 addr, u32 value, void *lpData)
		//               Usually, will return value of 'value' parameter.
		//    cbMask   = SNESAPU callback mask
	
		/// <summary>
		/// Set SNESAPU.DLL Callback Function
		/// </summary>
		/// <param name="pCbFunc"></param>
		/// <param name="cbMask"></param>
		/// <returns></returns>
		[DllImport("SNESAPU")]
		public static extern CBFUNC SNESAPUCallback(CBFUNC pCbFunc, CallbackEffect cbMask);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public delegate u32 CBFUNC(CallbackEffect effect, u32 addr, u32 data, IntPtr lpData);

		//SNESAPU callback effect
		[Flags]
		public enum CallbackEffect : uint
		{
			CBE_DSPREG = 0x01,
			CBE_INCS700 = 0x40000000,
			CBE_INCDATA = 0x20000000,
		}

//#define	CBE_DSPREG	0x01						//Write DSP value event
//#define	CBE_INCS700	0x40000000					//Include Script700 text file
//#define CBE_INCDATA	0x20000000					//Include Script700 binary file

	}
}