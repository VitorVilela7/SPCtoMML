using System.Runtime.InteropServices;
using s32 = System.Int32;
using u16 = System.UInt16;
using u32 = System.UInt32;
using u8 = System.Byte;

namespace AM4Play.SNESAPU
{
	unsafe static class SPC700
	{
		/// <summary>
		/// This function is a remnant from the 16-bit assembly when dynamic code reallocation was used.
		/// Now it just initializes internal pointers.<br />
		///<br />
		/// Note:<br />
		///    Callers should use InitAPU instead<br />
		/// <br />
		/// Destroys:<br />
		///    EAX
		/// </summary>
		[DllImport("SNESAPU")]
		public static extern void InitSPC();

		/// <summary>
		/// Reset SPC700<br />
		///<br />
		/// Clears all memory, resets the function registers, T64Cnt, and halt flag, and copies ROM into the
		/// IPL region.<br />
		///<br />
		/// Note:<br />
		///    Callers should use ResetAPU instead<br />
		///<br />
		/// Destroys:<br />
		///    EAX
		/// </summary>
		[DllImport("SNESAPU")]
		public static extern void ResetSPC();

		/// <summary>
		/// Fix SPC700 After Loading SPC File<br/>
		///<br/>
		/// Loads timer steps with the values in the timer registers, resets the counters, sets up the in/out
		/// ports, and stores the registers.<br/>
		///<br/>
		/// Note:<br/>
		///    Callers should use FixAPU instead<br/>
		///<br/>
		/// </summary>
		/// <param name="pc">SPC internal registers</param>
		/// <param name="a">SPC internal registers</param>
		/// <param name="y">SPC internal registers</param>
		/// <param name="x">SPC internal registers</param>
		/// <param name="psw">SPC internal registers</param>
		/// <param name="sp">SPC internal registers</param>
		[DllImport("SNESAPU")]
		public static extern void FixSPC(u16 pc, u8 a, u8 y, u8 x, u8 psw, u8 sp);

		/// <summary>
		/// Get SPC700 Registers<Br/>
		///<Br/>
		/// Returns the registers stored in the CPU
		/// </summary>
		/// <param name="pPC">Vars to store SPC internal registers</param>
		/// <param name="pA">Vars to store SPC internal registers</param>
		/// <param name="pY">Vars to store SPC internal registers</param>
		/// <param name="pX">Vars to store SPC internal registers</param>
		/// <param name="pPSW">Vars to store SPC internal registers</param>
		/// <param name="pSP">Vars to store SPC internal registers</param>
		[DllImport("SNESAPU")]
		public static extern void GetSPCRegs(u16* pPC, u8* pA, u8* pY, u8* pX, u8* pPSW, u8* pSP);

		/// <summary>
		/// Write to APU RAM<br/>
		///<br/>
		/// Writes a value to APU RAM.  Use this instead of writing to RAM directly so any necessary internal
		/// changes can be made.
		/// </summary>
		/// <param name="addr"></param>
		/// <param name="val"></param>
		[DllImport("SNESAPU")]
		public static extern void SetAPURAM(u32 addr, u8 val);

		/// <summary>
		/// Write to SPC700 Port<br/>
		///<br/>
		/// Writes a value to the SPC700 via the in ports.  Use this instead of writing to RAM directly.
		/// </summary>
		/// <param name="port">Port on which to write (0-3)</param>
		/// <param name="val">Value to write</param>
		[DllImport("SNESAPU")]
		public static extern void InPort(u8 port, u8 val);

		/// <summary>
		/// Emulate SPC700<Br/>
		///<Br/>
		/// Emulates the SPC700 for the number of clock cycles specified, or if the counter break option is
		/// enabled, until a counter is increased, whichever happens first.<Br/>
		///<Br/>
		/// Note:<Br/>
		///    Callers should use EmuAPU instead<Br/>
		///    Passing values menor 0 will cause undeterminable results <Br/>
		/// </summary>
		/// <param name="cyc">Number of 24.576MHz clock cycles to execute (must be > 0)</param>
		/// <returns>Clock cycles left to execute (negative if more cycles than specified were emulated)</returns>
		[DllImport("SNESAPU")]
		public static extern s32 EmuSPC(s32 cyc);
	}
}
