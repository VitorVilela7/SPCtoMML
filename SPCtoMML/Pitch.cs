using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SPCtoMML
{
	static class Pitch
	{
		static int[] pitchTable = new int[] {
                0x085f, 0x08de, 0x0965, 0x09f4,
                0x0a8c, 0x0b2c, 0x0bd6, 0x0c8b,
                0x0d4a, 0x0e14, 0x0eea, 0x0fcd,
                0x10be,
        };

		/// <summary>
		/// Returns the pitch specified.
		/// </summary>
		/// <param name="note">The note value</param>
		/// <param name="tuning">The tuning ($EE)</param>
		/// <param name="multiplier8x8">The pitch multiplier in 8.8 fixed point</param>
		/// <returns></returns>
		public static int FindPitch(int note, int tuning, int multiplier8x8)
		{
			if (note >= 0x34)
			{
				int i = note - 0x34 + tuning + note * 256;
				tuning = i % 256;
				note = i / 256;
			}
			else if (note < 0x13)
			{
				int i = (note - 0x13) * 2 + tuning + note * 256;
				tuning = i % 256 & 255;
				note = i / 256;
			}

			int pitch1 = pitchTable[note % 12];
			int pitch2 = pitchTable[note % 12 + 1];

			int pitch = pitch1 + (pitch2 - pitch1) * tuning / 256;
			pitch <<= 1;
			pitch >>= 6 - note / 12;

			return pitch * multiplier8x8 / 256;
		}

		/// <summary>
		/// Returns the pitch multiplier that gives the best note accuracy.
		/// </summary>
		/// <param name="pitches"></param>
		/// <returns></returns>
		public static int FindPitchMultiplier(int[] pitches, bool allowTuning)
		{
			int maximumPitch = FindPitch(0x45, 0, 0x0100);
			int minMultiplier = 0x0100;
			int minPitch = int.MaxValue;
			int defaultTuning = allowTuning ? -1 : 0;

			foreach (int pitch in pitches)
			{
				minMultiplier = Math.Max(minMultiplier, pitch * 0x0100 / maximumPitch);
				minPitch = Math.Min(minPitch, pitch);
			}

			int accuracyScore = int.MaxValue;
			int tuningScore = int.MaxValue;
			int multiplier = minMultiplier;
			
			for (int m = minMultiplier; minPitch >= FindPitch(0, 0, m); ++m)
			{
				int currentAccuracyScore = 0;
				int currentTuningScore = 0;
				int currentTuning = 0;

				var pitchData = pitches.Select(x => FindNote(x, m, defaultTuning));

				foreach (int[] data in pitchData)
				{
					if ((currentAccuracyScore += data[2]) > accuracyScore)
					{
						goto stop;
					}
				}

				foreach (int[] data in pitchData)
				{
					if (data[1] != currentTuning)
					{
						currentTuning = data[1];

						if (++currentTuningScore > tuningScore)
						{
							goto stop;
						}
					}
				}

				accuracyScore = currentAccuracyScore;
				tuningScore = currentTuningScore;
				multiplier = m;

				if (accuracyScore == 0 && tuningScore == 0)
				{
					break;
				}

			stop:
				continue;
			}

			return multiplier;
		}

		/// <summary>
		/// Returns note, tuning, accuracy/delta and input input
		/// </summary>
		/// <param name="pitch"></param>
		/// <param name="multiplier8x8"></param>
		/// <returns></returns>
		public static int[] FindNote(int pitch, int multiplier8x8, int defaultTuning = -1)
		{
			int note, tuning;
			int distance = pitch;

			bool lockTuning = defaultTuning != -1;
			defaultTuning = lockTuning ? defaultTuning : 0;

			for (note = 0; note < 70; ++note)
			{
				int currrentDistance = pitch - FindPitch(note, defaultTuning, multiplier8x8);

				if (currrentDistance == 0)
				{
					return new[] { note, defaultTuning, 0, pitch };
				}
				else if (!lockTuning && (currrentDistance > distance || (currrentDistance < 0)))
				{
					break;
				}
				else if ((currrentDistance = Math.Abs(currrentDistance)) > distance)
				{
					break;
				}
				
				distance = currrentDistance;
			}

			--note;

			if (lockTuning)
			{
				return new[] { Math.Max(0, note), defaultTuning, distance, pitch };
			}

			for (tuning = 1; tuning < 256; ++tuning)
			{
				int currrentDistance = Math.Abs(pitch - FindPitch(note, tuning, multiplier8x8));

				if (currrentDistance == 0)
				{
					return new[] { note, tuning, 0, pitch };
				}
				else if (currrentDistance > distance)
				{
					break;
				}

				distance = currrentDistance;
			}

			return new[] { note, tuning - 1, distance, pitch };
		}
	}
}
