using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SPCtoMML
{
    public class TickMap
    {
        /// <summary>
        /// Time in milliseconds that the map "don't care" about differences.
        /// </summary>
        public int ToleranceTime { get; set; }
        /// <summary>
        /// The loaded array.
        /// </summary>
        public int[] Values { get { return values; } }

        private int[] heatMap;
        private int[] values;
        private int lutSize;

        public TickMap(int[] valueList)
        {
            values = valueList;
            lutSize = values.Max() + 1;
            ToleranceTime = 2;
        }

        public void Load()
        {
            heatMap = new int[lutSize + ToleranceTime * 2];

            foreach (var item in values)
            {
                int score = 0;

                for (int v = 0; v < ToleranceTime; v++)
                {
                    heatMap[item + v] += ++score;
                }

                score += 2;

                for (int v = 0; v <= ToleranceTime; v++)
                {
                    heatMap[item + ToleranceTime + v] += --score;
                }
            }
        }

        public int[] Normalize()
        {
            int[] output = new int[values.Length];

            for (int i = 0; i < output.Length; i++)
            {
                int item = values[i];
                int score = 0;
                int replacement = item;

                for (int v = -ToleranceTime; v <= ToleranceTime; v++)
                {
                    if (heatMap[item + ToleranceTime + v] > score)// ||
                        //(v == 0 && heatMap[item + ToleranceTime + v] == score))
                    {
                        score = heatMap[item + ToleranceTime + v];
                        replacement = item + v;
                    }
                }

                output[i] = replacement;
            }

            return output;
        }
    }
}
