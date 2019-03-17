using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SPCtoMML
{
    public class BeatCalculator
    {
        public BeatCalculator()
        {
            scoreMap = new double[193];
            
            ScoreTicks(2, 0, 1.00, false);
            ScoreTicks(2, 0, 1.00, true);

            // bad
            scoreMap[0] = scoreMap[1] = scoreMap[2] = scoreMap[3] = 0;
        }

        public int FindTempo(NoteLength[] noteLengths, int acceptedError)
        {
            double best = 0;
            int bestTempo = 0;

            for (int i = 1; i <= 255; i++)
            {
                var score = ScoreTempo(noteLengths, i, acceptedError);
                
                if (score > best)
                {
                    best = score;
                    bestTempo = i;
                }
            }

            return bestTempo;
        }

        public double ScoreTempo(NoteLength[] noteLengths, int tempo, int acceptedError)
        {
            // TOTAL TIME = 512 * TICKS / TEMPO
            // TICKS = TOTAL TIME * TEMPO / 512

            double error = acceptedError * tempo / 512.0;
            double score = 0;

            for (int i = 0; i < noteLengths.Length; i++)
            {
                score += RateDurationRange(noteLengths[i].Length, error, tempo);
                score += RateStaccato(noteLengths[i].Staccato, error, tempo);
            }

            score /= Math.Exp(tempo / 32);
            return score;
        }

        public double RateDuration(int ticks)
        {
            if (ticks <= 1)
            {
                return 0;
            }
            else
            {
                return ScoreDuration(ticks, ticks);
            }
        }

        public double RateStaccato(int staccato, double error, int tempo)
        {
            // TO DO?
            return 0;
        }

        public double RateDurationRange(int length, double error, int tempo)
        {
            double ticks = length * tempo / 512.0;
            int minTicks = Math.Max(1, (int)Math.Round(ticks - error));
            int maxTicks = Math.Max(1, (int)Math.Round(ticks + error));

            return ScoreDuration(minTicks, maxTicks);
        }

        private double ScoreDuration(int minTicks, int maxTicks)
        {
            double score = 0;

            for (; minTicks <= maxTicks; minTicks++)
            {
                score = Math.Max(score, scoreMap[minTicks % 192]);
                
                if (minTicks >= 192 && minTicks % 192 == 0)
                {
                    score = 1; // 1 is the maximum score.
                }
            }

            return score;
        }

        private void ScoreTicks(int i, int baseTicks, double baseScore, bool triplet)
        {
            while (i <= 32)
            {
                // c2, c4, c8, c16, c32
                int noteValue = i * (triplet ? 3 : 2) / 2;
                int currentTicks = baseTicks + 192 / noteValue;

                i <<= 1;

                if (currentTicks < 192 && currentTicks > 0)
                {
                    scoreMap[currentTicks] = Math.Max(scoreMap[currentTicks], baseScore);
                    
                    // simulate ties or dots
                    ScoreTicks(i, currentTicks, baseScore * baseScore * 0.80, triplet);

                    // with triplets swapped.
                    ScoreTicks(i, currentTicks, baseScore * baseScore * 0.70, !triplet);
                }
            }
        }

        private double[] scoreMap;
    }
}
