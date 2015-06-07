using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace SPCtoMML
{
	class SampleDumper
	{
		byte[] traceData;

		public SampleDumper(byte[] traceData)
		{
			this.traceData = traceData;
		}

		public void ExportBRRSamples(string directory)
		{
			List<byte> stack = new List<byte>();
			List<int> sampleAddress = new List<int>();
			List<int> sampleLoopAddress = new List<int>();

			int currentAddress = 0;
			int currentLoop = 0;

			for (int x = 0x10000; x < traceData.Length; x += 2)
			{
				if (traceData[x] >= 0x80 && traceData[x] < 0x88)
				{
					currentAddress = traceData[x + 1];
				}
				else if (traceData[x] >= 0x88 && traceData[x] < 0x90)
				{
					currentAddress |= traceData[x + 1] << 8;
				}
				else if (traceData[x] >= 0x90 && traceData[x] < 0x98)
				{
					currentLoop = traceData[x + 1];
				}
				else if (traceData[x] >= 0x98 && traceData[x] < 0xA0)
				{
					currentLoop |= traceData[x + 1] << 8;
					if (!sampleAddress.Contains(currentAddress) || !sampleLoopAddress.Contains(currentLoop))
					{
						sampleAddress.Add(currentAddress);
						sampleLoopAddress.Add(currentLoop);
					}
				}
			}

			// hardest timer: push BRR files and put into .bnk
			for (int s = 0; s < sampleLoopAddress.Count; ++s)
			{
				int BRR_OFFSET = sampleAddress[s];
				int LOOP_OFFSET = sampleLoopAddress[s];
				LOOP_OFFSET -= BRR_OFFSET;

				stack.Clear();

				while (true)
				{
					for (int i = 0; i < 9; ++i, ++BRR_OFFSET, BRR_OFFSET &= 0xFFFF)
					{
						stack.Add(traceData[BRR_OFFSET]);
					}

					if ((traceData[BRR_OFFSET - 9 & 0xFFFF] & 1) == 1)
					{
						break;
					}
				}

				stack.Insert(0, (byte)(LOOP_OFFSET >> 8));
				stack.Insert(0, (byte)(LOOP_OFFSET));

				File.WriteAllBytes(String.Format("{0}/sample_{1:X2}.brr", directory, s), stack.ToArray());
			}
		}
	}
}
