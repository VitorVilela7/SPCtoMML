using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace SPCtoMML
{
	public partial class Form2 : Form
	{
		/// <summary>
		/// Callback for updating progress
		/// </summary>
		private Func<double> updateProgressHandler;

		/// <summary>
		/// Initializes the form.
		/// </summary>
		/// <param name="updateProgress">The callback for updating progress bar ratio.</param>
		public Form2(Func<double> updateProgress)
		{
			InitializeComponent();
			this.Text = Program.GetProgramName();
			this.updateProgressHandler = updateProgress;
		}

		public void UpdateHandler(Func<double> updateProgress)
		{
			this.updateProgressHandler = updateProgress;
		}

		/// <summary>
		/// Updates the progress dialog status.
		/// </summary>
		/// <param name="status">The new status to use.</param>
		public void UpdateStatus(string status)
		{
			this.label1.Text = status;
		}

		/// <summary>
		/// Updates the progress bar.
		/// </summary>
		/// <param name="ratio">The progress ratio.</param>
		public void UpdateProgress(double ratio)
		{
			try
			{
				this.progressBar1.Value = (int)Math.Round(ratio * 1000);
			}
			catch
			{
				this.progressBar1.Value = 0;
			}
		}

		private void Form2_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (e.CloseReason == CloseReason.UserClosing)
			{
				e.Cancel = true;
			}
		}

		private void timer1_Tick(object sender, EventArgs e)
		{
			if (updateProgressHandler != null)
			{
				UpdateProgress(updateProgressHandler());
			}
		}
	}
}
