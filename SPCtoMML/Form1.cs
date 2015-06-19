using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using AM4Play.SNESAPU;

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
			this.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
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
			if (spcData == null)
			{
				throw new Exception("You must load a SPC file before proceeding.");
			}

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
				resetLog();

				try
				{
					stopPlayer();
					spcData = loadSPC(openFileDialog1.FileName);
					reloadSong();
					loadSPCInfo();
					appendLine("Ready to analyze SPC file.");
				}
				catch(Exception ex)
				{
					spcData = null;
					appendLine("Can't load SPC file: {0} ", ex.Message);
				}
			}
		}

		private byte[] loadSPC(string path)
		{
			byte[] data = File.ReadAllBytes(path);

			byte[] moreBin1 = {
				0x2F, 0x04, 0x5B, 0x04, 0x68, 0x04, 0x9A, 0x04, 0x3F, 0x5C, 0x12, 0x30,
				0x1B, 0xC4, 0x10, 0xE4, 0x46, 0x9F, 0x5C, 0x08, 0x06, 0x2D, 0x9C, 0x2D,
			};

			byte[] moreBin2 = {
				0x47, 0x0D, 0x8E, 0x0D, 0xA5, 0x0D, 0x00, 0x00, 0xC1, 0x0D, 0xD5, 0x0D,
				0xF0, 0x0D, 0xFC, 0x0D, 0x11, 0x0E, 0x1D, 0x0E, 0x35, 0x0E, 0x3B, 0x0E,
			};
			
			byte[] moreBin3 = {
				0x20, 0xCD, 0xCF, 0xBD, 0xE8, 0x00, 0x8D, 0x00, 0xD6, 0x00, 0x01, 0xFE,
				0xFB, 0xD6, 0x00, 0x02, 0xFE, 0xFB, 0xD6, 0x00, 0x03, 0xFE, 0xFB, 0xDA,
			};

			byte[] moreBin4 = {
				0x8F, 0x5F, 0xE3, 0xF8, 0x46, 0xF4, 0xC1, 0x30, 0x08, 0x8D, 0x05, 0x9C,
				0x8F, 0x46, 0xE2, 0x2F, 0x08, 0x8F, 0xA5, 0xE2, 0x8D, 0x06, 0x80, 0xA8,
			};

			verifyData(ref moreBin1, ref data, 0x500);
			verifyData(ref moreBin2, ref data, 0x500);
			verifyData(ref moreBin3, ref data, 0x500);
			verifyData(ref moreBin4, ref data, 0x500);
			return data;
		}

		private void verifyData(ref byte[] binData, ref byte[] data, int offset)
		{
			for (int i = 0; i < binData.Length; ++i)
			{
				if (data[i + offset] != binData[i])
				{
					return;
				}
			}

			throw new Exception("Not supported file format.");
		}

		private void button2_Click(object sender, EventArgs e)
		{
			if (executing)
			{
				return;
			}
			executing = true;
			stopPlayer();
			resetLog();

			try
			{
				reloadSong();
			}
			catch(Exception ex)
			{
				appendLine("Error: {0}", ex.Message);
				executing = false;
				return;
			}

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
			if (dspTrace == null)
			{
				resetLog();
				appendLine("Error: You must analyze SPC before proceeding.");
				return;
			}

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
				appendLine("Error: You must click \"Analyze SPC\" before exporting BRR samples.");
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
