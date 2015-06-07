using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace SPCtoMML
{
	static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.Run(new Form1());
		}

		/// <summary>
		/// Obtains the program name.
		/// </summary>
		/// <returns>Returns the program name.</returns>
		public static string GetProgramName()
		{
			return Application.ProductName + " " + Application.ProductVersion.Replace(".", "");
		}
	}
}
