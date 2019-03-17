using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SPCtoMML
{
    // given real duration of X ticks, return staccato.
    public class Staccato
    {
        private static readonly byte[] noteDurations = { 0x33, 0x66, 0x80, 0x99, 0xB3, 0xCC, 0xE6, 0xFF };
        private Dictionary<int, List<StaccatoPointer>> durationMap;
        private StaccatoPointer[] maxStaccato;

        public Staccato()
        {
            durationMap = new Dictionary<int, List<StaccatoPointer>>();
            var maxStaccatoList = new List<StaccatoPointer>();

            for (int d = 0; d < 8; d++)
            {
                for (int i = 1; i <= 0x80; i++)
                {
                    int staccato = FindStaccatoTicks(i, d);
                    int real = i - staccato;

                    if (!durationMap.ContainsKey(real))
                    {
                        durationMap[real] = new List<StaccatoPointer>();
                    }

                    var pointer = new StaccatoPointer { Duration = real, Staccato = staccato, Index = d };

                    if (i == 0x80)
                    {
                        maxStaccatoList.Add(pointer);
                    }
                    else
                    {
                        durationMap[real].Add(pointer);
                    }
                }
            }

            maxStaccatoList.Sort((x, y) => x.Duration.CompareTo(y.Duration));
            maxStaccato = maxStaccatoList.ToArray();
        }

        public StaccatoPointer[] FindStaccatoGivenDuration(int realDuration)
        {
            var output = new List<StaccatoPointer>();

            if (durationMap.ContainsKey(realDuration))
            {
                output.AddRange(durationMap[realDuration]);
            }

            foreach (var item in maxStaccato)
            {
                if (item.Duration <= realDuration)
                {
                    output.Add(item);
                }
                else
                {
                    break;
                }
            }
            
            return output.ToArray();
        }

        public int FindStaccatoTicks(int noteTicks, int durIndex)
        {
            // separate note and tie ticks since staccato is relative to the
            // first note ticks.
            int tieTicks = 0;

            if (noteTicks >= 0x80)
            {
                // this is based on the AddmusicK and older behavior.
                tieTicks = noteTicks - 0x60;
                noteTicks = 0x60;
            }

            int realTicks = Math.Max(1, noteTicks * noteDurations[durIndex] >> 8) + tieTicks - 1;
            int stacTicks = (noteTicks + tieTicks) - realTicks;

            if (realTicks == 0)
            {
                realTicks += 1;
                stacTicks -= 1;
            }

            return stacTicks;
        }

        public class StaccatoPointer
        {
            public int Duration { get; set; }
            public int Staccato { get; set; }
            public int Index { get; set; }
        }
    }
}
