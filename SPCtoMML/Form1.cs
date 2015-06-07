using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using AM4Play.SNESAPU;
using System.IO;
using System.Threading;
using System.Diagnostics;

namespace SPCtoMML
{
	public partial class Form1 : Form
	{
		private byte[] spcData;
		private DSPTrace dspTrace;
		private TracePlayer player;
		private bool playingState;

		public Form1()
		{
			InitializeComponent();
			player = new TracePlayer();
		}

		private void startPlayer()
		{
			if (player != null)
			{
				player.Play();
				playingState = true;
			}

			updatePlayerButton();
		}

		private void stopPlayer()
		{
			playingState = false;

			if (player != null)
			{
				player.Pause();
			}

			updatePlayerButton();
		}

		private void updatePlayerButton()
		{
			button5.Text = playingState ? "Pause" : "Play Analysis";
		}

		private void reloadSong()
		{
			APU.ResetAPU(0xFFFFFFFF);
			APU.LoadSPCFile(spcData);
		}

		private void resetLog()
		{
			textBox1.Invoke((ThreadStart)delegate()
			{
				textBox1.Text = "";
			});
		}

		private void appendLine(string s, params object[] ss)
		{
			append(s + Environment.NewLine, ss);
		}

		private void append(string s, params object[] ss)
		{
			textBox1.Invoke((ThreadStart)delegate()
			{
				string textToAppend = String.Format(s, ss);
				textBox1.Text += textToAppend;
				textBox1.SelectionStart = textBox1.Text.Length - 1;
				textBox1.ScrollToCaret();

				int index = Math.Max(0, textBox1.Text.LastIndexOf(Environment.NewLine) - 1);
				index = Math.Max(0, textBox1.Text.LastIndexOf(Environment.NewLine, index));

				string statusText = textBox1.Text.Substring(index, textBox1.Text.Length - index);
				statusText = statusText.Replace(Environment.NewLine, "");

				toolStripStatusLabel1.Text = statusText;
			});
		}

		private void button1_Click(object sender, EventArgs e)
		{
			if (openFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				stopPlayer();
				spcData = File.ReadAllBytes(openFileDialog1.FileName);
				reloadSong();
				resetLog();
				loadSPCInfo();
				appendLine("Ready to analyze SPC file.");
			}
		}

		private void button2_Click(object sender, EventArgs e)
		{
			if (executing)
			{
				return;
			}
			executing = true;
			stopPlayer();

			reloadSong();
			resetLog();
			appendLine("Started thread...");
			appendLine("");

			thread = new Thread(handler);
			thread.Priority = ThreadPriority.BelowNormal;
			thread.Start();

			executing = false;
		}

		Thread thread;
		bool executing;

		private void loadSPCInfo()
		{
			//A9h - seconds - 3 bytes
			uint seconds = 120;
			UInt32.TryParse(ASCIIEncoding.ASCII.GetString(spcData, 0xA9, 3), out seconds);
			seconds += seconds & 1;
			seconds /= 2;
			textBox2.Text = seconds.ToString();
		}

		private void handler()
		{
			append("Tracking DSP instructions... ");

			dspTrace = new DSPTrace();
			dspTrace.Trace(Convert.ToInt32(textBox2.Text));
			player.Init(dspTrace.TraceResult);

			appendLine("Done.\r\nTotal DSP traces = {0}", dspTrace.TotalTraces);
			appendLine("");

		}

		private void handler3()
		{
			appendLine("Converting traces...");

			NoteDumper noteDumper = new NoteDumper(dspTrace.TraceResult);
			MMLDumper mmlDumper = new MMLDumper(noteDumper.OutputNoteData());

			appendLine("Tuning samples...");
			mmlDumper.SetUpSampleMultiplier();

			appendLine("Scanning staccato...");
			mmlDumper.CreateStaccatoMap();

			//appendLine("Creating loop data...");
			//mmlDumper.CreateLoopMap();

			append("Calculating Tempo... ");
			appendLine("t{0}", mmlDumper.CalculateTempo());

			appendLine("Generating MML data...");

			File.WriteAllText("C:/Users/Vitor/Desktop/AMK/music/test.txt", mmlDumper.OutputMML());

			appendLine("Generating samples...");

			SampleDumper brrDumper = new SampleDumper(dspTrace.TraceResult);

			string dir = "C:/Users/Vitor/Desktop/AMK/samples/test";

			if (Directory.Exists(dir))
			{
				Directory.Delete(dir, true);
			}
			Directory.CreateDirectory(dir);

			brrDumper.ExportBRRSamples(dir);

			appendLine("All done.");
		}

		private void button5_Click(object sender, EventArgs e)
		{
			if (playingState)
			{
				stopPlayer();
			}
			else if (player != null)
			{
				startPlayer();
			}
		}

		private void button6_Click(object sender, EventArgs e)
		{
			if (executing)
			{
				return;
			}
			executing = true;

			thread = new Thread(handler3);
			thread.Priority = ThreadPriority.BelowNormal;
			thread.Start();

			executing = false;
		}

		private void Form1_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (player != null)
			{
				player.Stop();
				player = null;
			}
		}
	}
}
