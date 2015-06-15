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
			this.Text = Program.GetProgramName();
			this.player = new TracePlayer();
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

				if (progressBar != null)
				{
					progressBar.UpdateStatus(statusText);
				}
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

			dspTrace = null;

			progressBar = new Form2(delegate()
			{
				return dspTrace == null ? 0 : dspTrace.CurrentProgress;
			});
			progressBar.Owner = this;

			thread = new Thread(handler);
			thread.Priority = ThreadPriority.BelowNormal;
			thread.Start();

			progressBar.ShowDialog();
			executing = false;
		}

		Thread thread;
		bool executing;
		Form2 progressBar;

		private void loadSPCInfo()
		{
			//A9h - seconds - 3 bytes
			uint seconds = 120;
			UInt32.TryParse(ASCIIEncoding.ASCII.GetString(spcData, 0xA9, 3), out seconds);
			seconds = (uint)Math.Ceiling(seconds / 2.0);

			if (seconds == 0)
			{
				seconds = 60;
			}

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

			progressBar.Invoke((ThreadStart)delegate()
			{
				progressBar.Close();
				progressBar.Dispose();
			});
		}

		private void handler3()
		{
			resetLog();

			if (dspTrace == null)
			{
				appendLine("Error: You must click \"Analyse SPC\" before exporting MML.");
				goto end;
			}

			appendLine("Converting traces...");

			int tempo = 100;
			Int32.TryParse(textBox3.Text, out tempo);

			NoteDumper noteDumper = new NoteDumper(dspTrace.TraceResult);
			MMLDumper mmlDumper = new MMLDumper(noteDumper.OutputNoteData(), tempo);
			progressBar.UpdateHandler(delegate() { return mmlDumper.CurrentRatio; });

			bool truncate = checkBox1.Checked;

			if (radioButton2.Checked)
			{
				mmlDumper.SetupStaccato(true, true, truncate);
			}
			else
			{
				mmlDumper.SetupStaccato(radioButton1.Checked, false, truncate);
			}

			mmlDumper.SetupVolume(checkBox2.Checked);
			mmlDumper.SetupPitch(checkBox3.Checked, checkBox4.Checked);
			mmlDumper.SetupPathSamples(textBox6.Text);

			appendLine("Tuning samples...");
			mmlDumper.SetUpSampleMultiplier();

			appendLine("Scanning staccato...");
			mmlDumper.CreateStaccatoMap();

			if (radioButton4.Checked)
			{
				append("Calculating Tempo... ");
				appendLine("t{0}", mmlDumper.CalculateTempo());
			}

			appendLine("Generating MML data...");

			File.WriteAllText(textBox4.Text, mmlDumper.OutputMML());

			appendLine("Generating samples...");

			SampleDumper brrDumper = new SampleDumper(dspTrace.TraceResult);

			string dir = textBox5.Text + "/";
			brrDumper.ExportBRRSamples(dir);

			appendLine("All done.");

		end:
			progressBar.Invoke((ThreadStart)delegate()
			{
				progressBar.Close();
				progressBar.Dispose();
			});
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
			if (textBox4.Text == "")
			{
				button4.PerformClick();
			}
			if (textBox5.Text == "")
			{
				button7.PerformClick();
			}

			if (executing)
			{
				return;
			}
			executing = true;

			if (progressBar != null)
			{
				progressBar.Dispose();
				progressBar = null;
			}
			thread = new Thread(handler3);
			thread.Priority = ThreadPriority.BelowNormal;

			progressBar = new Form2(null);
			progressBar.Owner = this;
			thread.Start();
			progressBar.ShowDialog();
			progressBar = null;

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

		private void button3_Click(object sender, EventArgs e)
		{
			if (textBox5.Text == "")
			{
				button7.PerformClick();
			}

			resetLog();

			if (dspTrace == null)
			{
				appendLine("Error: You must click \"Analyse SPC\" before exporting BRR samples.");
			}
			else
			{
				try
				{
					SampleDumper brrDumper = new SampleDumper(dspTrace.TraceResult);
					brrDumper.ExportBRRSamples(folderBrowserDialog1.SelectedPath);
					appendLine("BRR samples successfully exported.");
				}
				catch (Exception ex)
				{
					appendLine("An error occured while exporting BRR samples:");
					appendLine(ex.Message);
				}
			}
		}

		private void button4_Click(object sender, EventArgs e)
		{
			if (saveFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				textBox4.Text = saveFileDialog1.FileName;
			}
		}

		private void button7_Click(object sender, EventArgs e)
		{
			if (folderBrowserDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				textBox5.Text = folderBrowserDialog1.SelectedPath;
				textBox6.Text = new FileInfo(folderBrowserDialog1.SelectedPath + "/").Directory.Name;

				if (textBox6.Text.ToLower() == "samples")
				{
					textBox6.Text = "";
				}
			}
		}
	}
}
