using System;
using System.Runtime.InteropServices;
using b8 = System.Boolean;
using f32 = System.Single;
using s16 = System.Int16;
using s32 = System.Int32;
using u16 = System.UInt16;
using u32 = System.UInt32;
using u8 = System.Byte;

/***************************************************************************************************
* Program:    SNES Digital Signal Processor (DSP) Emulator                                         *
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
*                                                 Copyright (C) 1999-2006 Alpha-II Productions     *
*                                                 Copyright (C) 2003-2010 degrade-factory          *
***************************************************************************************************/

namespace AM4Play.SNESAPU
{
	//Interpolation routines -----------------------
	//#define	INT_NONE	0							//None
	//#define	INT_LINEAR	1							//Linear
	//#define	INT_CUBIC	2							//Cubic Spline
	//#define	INT_GAUSS	3							//SNES Gaussian
	// ----- degrade-factory code [2004/08/16] -----
	//#define	INT_SINC	4							//8-point Sinc
	//#define	INT_GAUSS4	7							//4-point Gaussian
	// ----- degrade-factory code [END] -----

	enum Interpolation : uint
	{
		/// <summary>
		/// None
		/// </summary>
		INT_NONE = 0,
		/// <summary>
		/// Linear
		/// </summary>
		INT_LINEAR = 1,
		/// <summary>
		/// Cubic Spline
		/// </summary>
		INT_CUBIC = 2,
		/// <summary>
		/// SNES Gaussian
		/// </summary>
		INT_GAUSS = 3,
		/// <summary>
		/// 8-point Sinc
		/// </summary>
		INT_SINC = 4,
		/// <summary>
		/// 4-point Gaussian
		/// </summary>
		INT_GAUSS4 = 7,
	}

	//DSP options ----------------------------------
	//#define	DSP_ANALOG	0x01						//Simulate analog anomalies (low-pass filter)
	//#define	DSP_OLDSMP	0x02						//Old ADPCM sample decompression routine
	//#define	DSP_SURND	0x04						//Surround sound
	//#define	DSP_REVERSE	0x08						//Reverse stereo samples
	//#define	DSP_NOECHO	0x10						//Disable echo
	// ----- degrade-factory code [2008/09/09] -----
	//#define	DSP_NOPMOD	0x20						//Disable pitch modulation
	//#define	DSP_NOPREAD	0x40						//Disable pitch read
	//#define	DSP_NOFIR	0x80						//Disable FIR filter
	//#define	DSP_BASS	0x100						//BASS BOOST (low-pass filter)
	//#define	DSP_NOENV	0x200						//Disable envelope
	//#define	DSP_NONOISE	0x400						//Disable noise
	//#define	DSP_ECHOMEM	0x800						//Write DSP echo memory map
	//#define	DSP_NOSURND	0x1000						//Disable surround sound
	//#define	DSP_FLOAT	0x40000000					//32bit floating-point volume output
	//#define	DSP_NOSAFE	0x80000000					//Disable volume safe
	// ----- degrade-factory code [END] -----

	[Flags]
	enum DSPOpts : uint
	{
		/// <summary>
		/// Simulate analog anomalies (low-pass filter)
		/// </summary>
		DSP_ANALOG = 0x01,
		/// <summary>
		/// Old ADPCM sample decompression routine
		/// </summary>
		DSP_OLDSMP = 0x02,
		/// <summary>
		/// Surround sound
		/// </summary>
		DSP_SURND = 0x04,
		/// <summary>
		/// Reverse stereo samples
		/// </summary>
		DSP_REVERSE = 0x08,
		/// <summary>
		/// Disable echo
		/// </summary>
		DSP_NOECHO = 0x10,
		/// <summary>
		/// Disable pitch modulation
		/// </summary>
		DSP_NOPMOD = 0x20,
		/// <summary>
		/// Disable pitch read
		/// </summary>
		DSP_NOPREAD = 0x40,
		/// <summary>
		/// Disable FIR filter
		/// </summary>
		DSP_NOFIR = 0x80,
		/// <summary>
		/// BASS BOOST (low-pass filter)
		/// </summary>
		DSP_BASS = 0x100,
		/// <summary>
		/// Disable envelope
		/// </summary>
		DSP_NOENV = 0x200,
		/// <summary>
		/// Disable noise
		/// </summary>
		DSP_NONOISE = 0x400,
		/// <summary>
		/// Write DSP echo memory map
		/// </summary>
		DSP_ECHOMEM = 0x800,
		/// <summary>
		/// Disable surround sound
		/// </summary>
		DSP_NOSURND = 0x1000,
		/// <summary>
		/// 32bit floating-point volume output
		/// </summary>
		DSP_FLOAT = 0x40000000,
		/// <summary>
		/// Disable volume safe
		/// </summary>
		DSP_NOSAFE = 0x80000000,
	}

	//PackWave options -----------------------------
	//#define	BRR_LINEAR	0x01						//Use linear compression for all blocks
	//#define	BRR_LOOP	0x02						//Set loop flag in block header
	//#define	BRR_NOINIT	0x04						//Don't create an initial block of silence
	//#define	BRR_8BIT	0x10						//Input samples are 8-bit

	[Flags]
	enum PackWaveOpts
	{
		/// <summary>
		/// Use linear compression for all blocks
		/// </summary>
		BRR_LINEAR = 0x01,
		/// <summary>
		/// Set loop flag in block header
		/// </summary>
		BRR_LOOP = 0x02,
		/// <summary>
		/// Don't create an initial block of silence
		/// </summary>
		BRR_NOINIT = 0x04,
		/// <summary>
		/// Input samples are 8-bit
		/// </summary>
		BRR_8BIT = 0x10,
	}

	//Mixing flags ---------------------------------
	//#define	MFLG_MUTE	0x01						//Voice is muted (set by user)
	//#define	MFLG_NOISE	0x02						//Voice is noise (set by user)
	//#define	MFLG_USER	0x03						//Flags by user
	//#define	MFLG_KOFF	0x04						//Voice is in the process of keying off
	//#define	MFLG_OFF	0x08						//Voice is currently inactive
	//#define	MFLG_END	0x10						//End block was just played

	[Flags]
	enum MixFlags
	{
		/// <summary>
		/// Voice is muted (set by user)
		/// </summary>
		MFLG_MUTE = 1,
		/// <summary>
		/// Voice is noise (set by user)
		/// </summary>
		MFLG_NOISE = 2,
		/// <summary>
		/// Flags by user
		/// </summary>
		MFLG_USER = 3, // sets both mute and noise.
		/// <summary>
		/// Voice is in the process of keying off
		/// </summary>
		MFLG_KOFF = 4,
		/// <summary>
		/// Voice is currently inactive
		/// </summary>
		MFLG_OFF = 8,
		/// <summary>
		/// End block was just played
		/// </summary>
		MFLG_END = 16,
	}

	//Script700 DSP flags
	//#define	S700_MUTE	0x01						//Mute voice
	//#define	S700_CHANGE	0x02						//Change sound source (note change)
	//#define	S700_DETUNE	0x04						//Detune sound pitch rate
	//#define	S700_VOLUME	0x08						//Change sound volume

	[Flags]
	enum DSPFlags
	{
		/// <summary>
		/// Mute voice
		/// </summary>
		S700_MUTE = 1,
		/// <summary>
		/// Change sound source (note change)
		/// </summary>
		S700_CHANGE = 2,
		/// <summary>
		/// Detune sound pitch rate
		/// </summary>
		S700_DETUNE = 4,
		/// <summary>
		/// Change sound volume
		/// </summary>
		S700_VOLUME = 8,
	}

	//Script700 DSP master parameters
	//#define	S700_MVOL_L	0x00						//Master volume (left)
	//#define	S700_MVOL_R	0x01						//Master volume (right)
	//#define	S700_ECHO_L	0x02						//Echo volume (left)
	//#define	S700_ECHO_R	0x03						//Echo volume (right)

	enum S700_PARAM
	{
		/// <summary>
		/// Master volume (left)
		/// </summary>
		S700_MVOL_L = 0,
		/// <summary>
		/// Master volume (right)
		/// </summary>
		S700_MVOL_R = 1,
		/// <summary>
		/// Echo volume (left)
		/// </summary>
		S700_ECHO_L = 2,
		/// <summary>
		/// Echo volume (right)
		/// </summary>
		S700_ECHO_R = 3,
	}

	// ----- degrade-factory code [2009/03/11] -----
	[StructLayout(LayoutKind.Explicit)]
	unsafe struct Voice
	{
		//Voice -----------08
		[FieldOffset(0)]
		public u16 vAdsr;								//ADSR parameters when KON was written
		[FieldOffset(2)]
		public u8 vGain;								//Gain parameters when KON was written
		[FieldOffset(3)]
		public u8 vRsv;								//Changed ADSR/Gain parameters flag
		[FieldOffset(4)]
		public s16* sIdx;								//-> current sample in sBuf
		
		//Waveform --------06
		[FieldOffset(8)]
		public void* bCur;								//-> current block
		[FieldOffset(12)]
		public u8 bHdr;								//Block Header for current block
		[FieldOffset(13)]
		public MixFlags mFlg;								//Mixing flags (see MixF)
		
		//Envelope --------22
		[FieldOffset(14)]
		public u8 eMode;								//[3-0] Current mode (see EnvM)
		//[6-4] ADSR mode to switch into from Gain
		//[7]   Envelope is idle
		[FieldOffset(15)]
		public u8 eRIdx;								//Index in RateTab (0-31)
		[FieldOffset(16)]
		public u32 eRate;								//Rate of envelope adjustment (16.16)
		[FieldOffset(20)]
		public u32 eCnt;								//Sample counter (16.16)
		[FieldOffset(24)]
		public u32 eVal;								//Current envelope value
		[FieldOffset(28)]
		public s32 eAdj;								//Amount to adjust envelope height
		[FieldOffset(32)]
		public u32 eDest;								//Envelope Destination
		//Visualization ---08
		[FieldOffset(36)]
		public s32 vMaxL;								//Maximum absolute sample output
		[FieldOffset(40)]
		public s32 vMaxR;
		//Samples ---------52
		[FieldOffset(44)]
		public s16 sP1;								//Last sample decompressed (prev1)
		[FieldOffset(46)]
		public s16 sP2;								//Second to last sample (prev2)

		[FieldOffset(48)]
		fixed s16 sBufP[8];							//Last 8 samples from previous block (needed for inter.)
		[FieldOffset(64)]
		fixed s16 sBuf[16];							//32 bytes for decompressed sample blocks
		//Mixing ----------32
		[FieldOffset(96)]
		public f32 mTgtL;								//Target volume (floating-point routine only)
		[FieldOffset(100)]
		public f32 mTgtR;								// "  "
		[FieldOffset(104)]
		public s32 mChnL;								//Channel Volume (-24.7)
		[FieldOffset(108)]
		public s32 mChnR;								// "  "
		[FieldOffset(112)]
		public u32 mRate;								//Pitch Rate after modulation (16.16)
		[FieldOffset(116)]
		public u16 mDec;								//Pitch Decimal (.16) (used as delta for interpolation)
		[FieldOffset(118)]
		public u8 mSrc;								//Current source number
		[FieldOffset(119)]
		public u8 mKOn;								//Delay time from writing KON to output
		[FieldOffset(120)]
		public u32 mOrgP;								//Original pitch rate converted from the DSP (16.16)
		[FieldOffset(124)]
		public s32 mOut;								//Last sample output before chn vol (used for pitch mod)
	}

	unsafe static class DSP
	{
		public const int EXT_CBEVENT = 0x10010;
		public const int CBE_DSPREG = 0;

		/// <summary>
		/// Writes a value to a specified DSP register and alters the DSP accordingly.  If the register write
		/// affects the output generated by the DSP, this function returns true.
		/// </summary>
		/// <remarks>SetDSPReg does not call the debugging vector</remarks>
		/// <param name="reg">DSP Address</param>
		/// <param name="val">DSP Data</param>
		/// <returns>true, if the DSP state was affected</returns>
		[DllImport("SNESAPU")]
		public static extern b8 SetDSPReg(u8 reg, u8 val);

		/// <summary>
		/// Emulates the DSP of the SNES
		///
		/// Notes:
		///    If 'pBuf' is NULL, the routine MIX_NONE will be used
		///    Range checking is performed on 'size'
		///
		///    Callers should use EmuAPU instead
		/// </summary>
		/// <param name="pBuf">Buffer to store output</param>
		/// <param name="size">Length of buffer (in samples, can be 0)</param>
		/// <returns>End of buffer</returns>
		[DllImport("SNESAPU")]
		public static extern void* EmuDSP(void* pBuf, s32 size);

		/// <summary>
		/// Initialize DSP
		///
		/// Creates the lookup tables for interpolation, and sets the default mixing settings:
		///
		///    mixType = 1
		///    numChn  = 2
		///    bits    = 16
		///    rate    = 32000
		///    inter   = INT_GAUSS
		///    opts    = 0
		///
		/// Note:
		///    Callers should use InitAPU instead
		/// </summary>
		[DllImport("SNESAPU")]
		public static extern void InitDSP();

		/// <summary>
		/// Reset DSP
		///
		/// Resets the DSP registers, erases internal variables, and resets the volume
		///
		/// Note:
		///    Callers should use ResetAPU instead
		/// </summary>
		[DllImport("SNESAPU")]
		public static extern void ResetDSP();

		/// <summary>
		/// Recalculates tables, changes the output sample rate, and sets up the mixing routine
		///
		/// Notes:
		///    Range checking is performed on all parameters.  If a parameter does not match the required
		///     range of values, the default value will be assumed.
		///
		///    -1 can be used for any paramater that should remain unchanged.
		///
		///    Callers should use SetAPUOpt instead
		/// </summary>
		/// <param name="mix">Mixing routine (default 1)</param>
		/// <param name="chn">Number of channels (1 or 2, default 2)</param>
		/// <param name="bits">Sample size (8, 16, 24, 32, or -32 [IEEE 754], default 16)</param>
		/// <param name="rate">Sample rate (8000-192000, default 32000)</param>
		/// <param name="inter">Interpolation type (default INT_GAUSS)</param>
		/// <param name="opts">See 'DSP options' in the Defines section</param>
		[DllImport("SNESAPU")]
		public static extern void SetDSPOpt(u32 mix, u32 chn, u32 bits, u32 rate, u32 inter, u32 opts);

		/// <summary>
		/// Initializes the internal mixer variables
		/// </summary>
		[DllImport("SNESAPU")]
		public static extern void FixDSP();

		/// <summary>
		/// Puts all DSP voices in a key off state and erases echo region.
		/// </summary>
		/// <param name="reset">True  = Reset all voices; False = Only erase memory</param>
		[DllImport("SNESAPU")]
		public static extern void FixSeek(u8 reset);

		/// <summary>
		/// Adjusts the pitch of the DSP
		/// </summary>
		/// <param name="Base">Base sample rate (32000 = Normal pitch, 32458 = Old SB cards, 32768 = Old ZSNES)</param>
		[DllImport("SNESAPU")]
		public static extern void SetDSPPitch(u32 Base);

		/// <summary>
		/// Set DSP Amplification
		/// 
		/// This value is applied to the output with the main volumes
		/// </summary>
		/// <param name="amp">Amplification level [-15.16] (1.0 = SNES, negative values act as 0)</param>
		[DllImport("SNESAPU")]
		public static extern void SetDSPAmp(u32 amp);

		/// <summary>
		/// Set DSP Volume
		///
		/// This value attenuates the output and was implemented to allow songs to be faded out.
		///
		/// Notes:
		///    ResetDSP sets this value to 65536 (no attenuation).
		///    This function is called internally by EmuAPU and SetAPULength, and should not be called by
		///     the user.
		///
		/// </summary>
		/// <param name="vol">Volume [-1.16] (0.0 to 1.0, negative values act as 0)</param>
		[DllImport("SNESAPU")]
		public static extern void SetDSPVol(u32 vol);

		/// <summary>
		/// Set Voice Stereo Separation
		///
		/// Sets the amount to adjust the panning position of each voice
		/// </summary>
		/// <param name="sep">Separation [1.16]
		///         1.0 - full separation (output is either left, center, or right)
		///         0.5 - normal separation (output is unchanged)
		///           0 - no separation (output is completely monaural)</param>
		[DllImport("SNESAPU")]
		public static extern void SetDSPStereo(u32 sep);

		/// <summary>
		/// Set Echo Feedback Crosstalk
		///
		/// Sets the amount of crosstalk between the left and right channel during echo feedback
		/// </summary>
		/// <param name="leak">Crosstalk amount [-1.15]
		///           1.0 - no crosstalk (SNES)
		///             0 - full crosstalk (mono/center)
		///          -1.0 - inverse crosstalk (L/R swapped)</param>
		[DllImport("SNESAPU")]
		public static extern void SetDSPEFBCT(s32 leak);

	}
}

/*

//Internal mixing data -------------------------
// ----- degrade-factory code [2009/07/11] -----
typedef struct MixF
{
	b8	mute:1;									//Voice is muted (set by user)
	u8	noise:1;								//Voice is noise (set by user)
	b8	keyOff:1;								//Voice is in key off mode
	b8	inactive:1;								//Voice is inactive, no samples are being played
	b8	keyEnd:1;								//End block was just played
	u8	__r2:3;
} MixF;
// ----- degrade-factory code [END] -----

typedef enum EnvM
{
	ENV_DEC,									//Linear decrease
	ENV_EXP,									//Exponential decrease
	ENV_INC,									//Linear increase
	ENV_BENT = 6,								//Bent line increase
	ENV_DIR,									//Direct setting
	ENV_REL,									//Release mode (key off)
	ENV_SUST,									//Sustain mode
	ENV_ATTACK,									//Attack mode
	ENV_DECAY = 13,								//Decay mode
} EnvM;

#define	ENVM_IDLE	0x80						//Envelope is marked as idle, or not changing
#define	ENVM_MODE	0xF							//Envelope mode is stored in lower four bits

*/