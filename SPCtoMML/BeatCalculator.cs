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
        }

        public int FindTempo(int[] noteLengths, int acceptedError)
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

        public double ScoreTempo(int[] noteLengths, int tempo, int acceptedError)
        {
            // TOTAL TIME = 512 * TICKS / TEMPO
            // TICKS = TOTAL TIME * TEMPO / 512

            double error = acceptedError * tempo / 512.0;
            double score = 0;

            for (int i = 0; i < noteLengths.Length; i++)
            {
                double ticks = noteLengths[i] * tempo / 512.0;
                int minTicks = Math.Max(1, (int)Math.Round(ticks - error));
                int maxTicks = Math.Max(1, (int)Math.Round(ticks + error));
                score += ScoreDuration(minTicks, maxTicks);
            }
            
            score /= Math.Exp(tempo / 32);
            return score;
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
