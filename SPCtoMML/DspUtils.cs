using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SPCtoMML
{
    static class DspUtils
    {
        public static int ToSigned(byte value)
        {
            if (value >= 0x80)
            {
                return -((value ^ 0xFF) + 1);
            }
            else
            {
                return value;
            }
        }

        public static byte ToByte(double value)
        {
            return ToByte((int)Math.Round(value));
        }

        public static byte ToByte(int value)
        {
            // clip to 8-bit signed.
            value = Math.Max(-128, Math.Min(127, value));

            if (value < 0)
            {
                return (byte)((Math.Abs(value) ^ 0xFF) + 1);
            }
            else
            {
                return (byte)value;
            }
        }

    }
}
