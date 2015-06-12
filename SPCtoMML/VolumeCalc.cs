using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SPCtoMML
{
    static class VolumeCalc
    {
        //1280: db $00,$01,$03,$07,$0d,$15,$1e,$29,$34,$42,$51,$5e,$67,$6e,$73,$77
        //1290: db $7a,$7c,$7d,$7e,$7f

        static int[] PanTable = {
                                    0x00, 0x01, 0x03, 0x07, 0x0D, 0x15, 0x1E,
                                    0x29, 0x34, 0x42, 0x51, 0x5E, 0x67, 0x6E,
                                    0x73, 0x77, 0x7A, 0x7C, 0x7D, 0x7E, 0x7F,
                                };

        static int[][] QTable = new int[][] {
            new[]{0x08,0x12,0x1b,0x24,0x2c,0x35,0x3e,0x47,0x51,0x5a,0x62,0x6b,0x7d,0x8f,0xa1,0xb3},
            new[]{0x19,0x33,0x4C,0x66,0x72,0x7F,0x8C,0x99,0xA5,0xB2,0xBf,0xCC,0xD8,0xE5,0xF2,0xFC},
        };

		private static int signedByteToInt(int value)
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

		private static byte intToSignedByte(int value)
		{
			if (value < 0)
			{
				return (byte)((Math.Abs(value) ^ 0xFF) + 1);
			}
			else
			{
				return (byte)value;
			}
		}

		/// <summary>
		/// Returns: panning found, angle distance
		/// </summary>
		/// <param name="volume"></param>
		/// <param name="index"></param>
		/// <returns></returns>
		public static int FindPanning(int[] volume, out double distance)
		{
			volume[0] = Math.Abs(signedByteToInt(volume[0]));
			volume[1] = Math.Abs(signedByteToInt(volume[1]));

			var angle = Math.Atan2(volume[0], volume[1]);
			int panFound = 10;
			distance = int.MaxValue;

			for (int y = 0; y < 21; ++y)
			{
				int[] currentVolume = CalcPanning(256, y, 0, new[] { false, false });
				var currentAngle = Math.Atan2(currentVolume[0], currentVolume[1]);
				var currentDistance = Math.Abs(currentAngle - angle);

				if (currentDistance < distance)
				{
					distance = currentDistance;
					panFound = y;

					if (distance == 0)
					{
						break;
					}
				}
			}

			return panFound;
		}

		/// <summary>
		/// Return qXY, pan, volume, amplify, distance, vol1, vol2, left surround, right surround
		/// </summary>
		/// <param name="volume"></param>
		/// <param name="index"></param>
		/// <returns></returns>
		public static int[] FindVolume2(int[] volume, int index, bool amplify)
		{
			bool[] surround = new[] { false, false };

			for (int i = 0; i < 2; ++i)
			{
				if (volume[i] > 0x7F)
				{
					surround[i] = true;
				}
			}

			int distance = int.MaxValue;
			int[] close = new[] { 0, 0 };
			int qClose = 0;
			int vClose = 0;
			int mClose = 0;

			double yDistance;
			int y = FindPanning((int[])volume.Clone(), out yDistance);

			int amplifyMaximum = amplify ? 256 : 1;

			for (int m = 0; m < amplifyMaximum; ++m)
			{
				for (int q = 15; q >= 0; --q)
				{
					int cQ = CalcQ(q, index);

					for (int v = 255; v >= 0; --v)
					{
						int[] vol = CalcPanning(VolumeBase(255, v, cQ), y, m, surround);

						int current = (int)Math.Sqrt(Math.Pow(Math.Abs(vol[0] - volume[0]) + 1, 2)
							* Math.Pow(Math.Abs(vol[1] - volume[1]) + 1, 2));

						if (current < distance)
						{
							close = vol;
							distance = current;

							qClose = q;
							vClose = v;
							mClose = m;

							if (distance == 1)
							{
								goto breakAll;
							}
						}
						else if (vol[0] < volume[0] && vol[1] < volume[1])
						{
							break;
						}
					}
				}
			}


		breakAll:
			return new[] { qClose, y, vClose, mClose, distance - 1,
                close[0], close[1], surround[0] ? 1 : 0, surround[1] ? 1 : 0 };
		}

		/// <summary>
		/// Return qXY, pan, volume, amplify, distance, vol1, vol2, left surround, right surround
		/// </summary>
		/// <param name="volume"></param>
		/// <param name="index"></param>
		/// <returns></returns>
        public static int[] FindVolume(int[] volume, int index)
        {
            bool[] surround = new[] { false, false };

            for (int i = 0; i < 2; ++i)
            {
                if (volume[i] > 0x7F)
                {
                    surround[i] = true;
                }
            }

            int distance = int.MaxValue;
            int[] close = new[] { 0, 0 };
            int qClose = 0;
            int yClose = 0;
            int vClose = 0;
            int mClose = 0;

            for (int m = 0; m < 256; ++m)
            {
                for (int q = 15; q >= 0; --q)
                {
                    int cQ = CalcQ(q, index);

                    for (int y = 0; y <= 20; ++y)
                    {
                        for (int v = 255; v >= 0; --v)
                        {
                            int[] vol = CalcPanning(VolumeBase(255, v, cQ), y, m, surround);

                            int current = (int)Math.Sqrt(Math.Pow(Math.Abs(vol[0] - volume[0]) + 1, 2)
                                * Math.Pow(Math.Abs(vol[1] - volume[1]) + 1, 2));
                            
                            if (current < distance)
                            {
                                close = vol;
                                distance = current;

                                qClose = q;
                                yClose = y;
                                vClose = v;
                                mClose = m;

								if (distance == 1)
								{
									goto breakAll;
								}
                            }
							else if (vol[0] < volume[0] && vol[1] < volume[1])
							{
								break;
							}
                        }
                    }
                }
            }

        breakAll:
            return new[] { qClose, yClose, vClose, mClose, distance - 1,
                close[0], close[1], surround[0] ? 1 : 0, surround[1] ? 1 : 0 };
        }

        public static int[] CalculateVolume(int[] values, bool loud)
        {
            //return new[] { master, volume, pan, q };
            values[0] &= 255;
            values[1] &= 255;
            values[2] &= 255;
            values[3] &= 127;

            int Base = VolumeBase(values[0], values[1], CalcQ(values[3], loud ? 1 : 0));
            int[] VOL = CalcPanning(Base, values[2] & 0x1f, values.Length == 5 ? values[4] : 0,
                new[] { (values[2] & 0x80) != 0, (values[2] & 0x40) != 0 });

            return VOL;
        }

        public static int[] CalculateVolume(string parse)
        {
            bool loud;
            return CalculateVolume(ParseStr(parse, out loud), loud);
        }

        static int[] ParseStr(string parse, out bool loud)
        {
            parse = parse.ToLower();
            parse = parse + "\x00\x00\x00\x00\x00\x00\x00\x00";

            int index = 0, size = parse.Length;
            int pan = 10;
            int volume = 255;
            int master = 255;
            int q = 0x7F;
            int multiply = 0;

            bool pass = false;
            bool pass2 = false;

            loud = false;

            while (index < size)
            {
                switch (parse[index++])
                {
                    case '#': try
                        {
                            if (parse[index] == 'l' && parse[index + 1] == 'o' &&
                                parse[index + 2] == 'u' && parse[index + 3] == 'd' &&
                                parse[index + 4] == 'e' && parse[index + 5] == 'r')
                            {
                                index += 6;
                                loud = true;
                            }
                            else { throw new Exception(); }
                        }
                        catch
                        {
                            throw new Exception("Invalid #louder command.");
                        }
                        break;

                    // $FA $03 $XX
                    case '$':
                        if (parse[index] == 'f' && parse[index + 1] == 'a')
                        {
                            pass = true;
                        }
                        else if (pass && parse[index] == '0' && parse[index + 1] == '3')
                        {
                            pass = false;
                            pass2 = true;
                        }
                        else if (pass2)
                        {
                            multiply = 0;

                            while (Char.IsNumber(parse[index]) || (parse[index] >= 'a' && parse[index] <= 'f'))
                            {
                                if (Char.IsNumber(parse[index]))
                                {
                                    multiply |= parse[index++] - '0';
                                }
                                else
                                {
                                    multiply |= parse[index++] - 'a' + 10;
                                }
                                multiply <<= 4;
                            }

                            multiply >>= 4;

                            if (multiply < 0 || multiply > 0xff)
                            {
                                throw new Exception("Invalid value in '$' command.");
                            }
                        }
                        else
                        {
                            throw new Exception("The only allowed hex command is $FA $03 $XX.");
                        }
                        break;

                    case 'q':
                        q = 0;

                        while (Char.IsNumber(parse[index]) || (parse[index] >= 'a' && parse[index] <= 'f'))
                        {
                            if (Char.IsNumber(parse[index]))
                            {
                                q |= parse[index++] - '0';
                            }
                            else
                            {
                                q |= parse[index++] - 'a' + 10;
                            }
                            q <<= 4;
                        }

                        q >>= 4;

                        if (q < 0 || q > 0x7F)
                        {
                            throw new Exception("Invalid value in 'q' command.");
                        }

                        pass = pass2 = false;
                        break;

                    case 'v':
                        volume = 0;
                        
                        while (Char.IsNumber(parse[index]))
                        {
                            volume += (int)(parse[index++] - '0');
                            volume *= 10;
                        }

                        volume /= 10;

                        if (volume < 0 || volume > 255)
                        {
                            throw new Exception("Invalid value in 'v' command.");
                        }

                        pass = pass2 = false;
                        break;

                    case 'w':
                        master = 0;

                        while (Char.IsNumber(parse[index]))
                        {
                            master += (int)(parse[index++] - '0');
                            master *= 10;
                        }

                        master /= 10;

                        if (master < 0 || master > 255)
                        {
                            throw new Exception("Invalid value in 'w' command.");
                        }

                        pass = pass2 = false;
                        break;

                    case 'y':
                        pan = 0;

                        while (Char.IsNumber(parse[index]))
                        {
                            pan += (int)(parse[index++] - '0');
                            pan *= 10;
                        }

                        pan /= 10;

                        if (pan < 0 || pan > 20)
                        {
                            throw new Exception("Invalid value in 'y' command.");
                        }

                        if (parse[index++] == ',')
                        {
                            int option = parse[index++] - '0';
                            int option2 = parse[++index] - '0';

                            if (option<0|| option > 1 || option2 > 1||option2<0)
                            {
                                throw new Exception("Invalid value in 'y' command.");
                            }

                            pan |= option << 7;
                            pan |= option2 << 6;
                        }

                        pass = pass2 = false;
                        break;
                }
            }

            return new[] { master, volume, pan, q, multiply };
        }

        static int[] CalcPanning(int volume, int pan, int multiply, bool[] surround)
        {
            int[] result = new int[2];

            for (int i = 0; i < 2; ++i)
            {
                result[i] = volume * PanTable[pan] >> 8;
                result[i] += result[i] * multiply >> 8;

                if (surround[i])
                {
					result[i] = ((result[i] ^ 255) + 1) & 255;
                }

				pan = Math.Abs(pan - 0x14); // make positive.

                //1036: 7d        mov   a,x
                //1037: 9f        xcn   a
                //1038: 5c        lsr   a
                //1039: c4 12     mov   $12,a             ; $12 = voice X volume DSP reg
                //103b: eb 11     mov   y,$11
                //103d: f6 81 12  mov   a,$1281+y         ; next pan val from table
                //1040: 80        setc
                //1041: b6 80 12  sbc   a,$1280+y         ; pan val
                //1044: eb 10     mov   y,$10
                //1046: cf        mul   ya
                //1047: dd        mov   a,y
                //1048: eb 11     mov   y,$11
                //104a: 60        clrc
                //104b: 96 80 12  adc   a,$1280+y         ; add integer part to pan val
                //104e: fd        mov   y,a
                //104f: f5 71 03  mov   a,$0371+x         ; volume
                //1052: cf        mul   ya
                //1053: f5 a1 02  mov   a,$02a1+x         ; bits 7/6 will negate volume L/R
                //1056: 13 12 01  bbc0  $12,$105a
                //1059: 1c        asl   a
                //105a: 10 05     bpl   $1061
                //105c: dd        mov   a,y
                //105d: 48 ff     eor   a,#$ff
                //105f: bc        inc   a
                //1060: fd        mov   y,a
                //1061: dd        mov   a,y
                //1062: eb 12     mov   y,$12
                //1064: 3f 8f 06  call  $068f             ; set DSP vol if vbit 1D clear
            }

            return result;
        }

        static int CalcQ(int q, int index)
        {
            return QTable[index][q & 15];
        }

        static int VolumeBase(int master, int vol, int q)
        {
            /*; set voice volume from master/base/A
            124e: f5 41 02  mov   a,$0241+x
            1251: cf        mul   ya
            1252: e4 57     mov   a,$57             ; master volume
            1254: cf        mul   ya
            1255: dd        mov   a,y
            1256: cf        mul   ya
            1257: dd        mov   a,y
            1258: d5 71 03  mov   $0371+x,a         ; voice volume
            125b: 6f        ret*/

            return (int)Math.Pow((q * vol >> 8) * master >> 8, 2) >> 8;
        }
    }
}
