using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SPCtoMML
{
    /// <summary>
    /// Specialized module in processing and outputting echo effects.
    /// </summary>
    class EchoModule
    {
        private int tempo;
        private int[] masterVolume;

        private int echoDelay;
        private int echoEnable;
        private int echoFeedback;
        private int echoLeftVolume;
        private int echoRightVolume;
        private int echoLeftVolumeSlide;
        private int echoRightVolumeSlide;
        private int echoSlideLength;

        private int[] firFilter;

        private bool? allowEcho;
        private bool allowEchoUpdate;

        private bool firUpdate;
        private bool echoEnableUpdate;
        private bool echoDelayUpdate;
        private bool echoFeedbackUpdate;
        private bool echoVolumeUpdate;
        private bool echoSlideUpdate;

        private bool echoSync;

        public void EchoEnableEvent()
        {
            if (allowEcho != null && !(bool)allowEcho)
            {
                allowEchoUpdate = true;
                echoDelayUpdate = true;
                echoEnableUpdate = true;
                echoVolumeUpdate = true;
                echoFeedbackUpdate = true;
                firUpdate = true;
            }
            allowEcho = true;
        }

        public void EchoDisableEvent()
        {
            if (allowEcho != false)
            {
                allowEchoUpdate = true;
                allowEcho = false;
            }
        }

        public void EchoDelayUpdate(int newDelay)
        {
            echoDelay = newDelay;
            if (!echoSync) echoDelayUpdate = true;
        }

        public void EchoEnableUpdate(int flags)
        {
            echoEnable = flags;
            if (!echoSync) echoDelayUpdate = true;
        }

        public void EchoFeedbackUpdate(int feedback)
        {
            echoFeedback = feedback;
            if (!echoSync)
            {
                echoFeedbackUpdate = true;
            }
        }

        public void EchoFirUpdate(int[] fir)
        {
            firFilter = fir;
            if (!echoSync) firUpdate = true;
        }

        public void VolumeUpdate(int[] eventData)
        {
            if (eventData.Length <= 4)
            {
                echoLeftVolume = DspUtils.ToSigned((byte)(eventData[eventData.Length - 2] & 255));
                echoRightVolume = DspUtils.ToSigned((byte)(eventData[eventData.Length - 2] >> 8));
                if (!echoSync) echoVolumeUpdate = true;
                if (!echoSync) echoSlideUpdate = false;
            }
            else
            {
                int length = eventData.Length;
                int index = 2;
                int mode = eventData[index - 2] > eventData[index] ? 1 : 0;
                bool okSlide = true;

                for (index = 2; index < length; index += 2)
                {
                    if (mode != (eventData[index - 2] > eventData[index] ? 1 : 0))
                    {
                        //okSlide = false;
                        break;
                    }
                }

                if (index == 2)
                {
                    okSlide = false;
                }

                if (!echoSync) echoVolumeUpdate = true;

                if (okSlide)
                {
                    echoLeftVolume = DspUtils.ToSigned((byte)(eventData[0] & 255));
                    echoRightVolume = DspUtils.ToSigned((byte)(eventData[0] >> 8));
                    echoLeftVolumeSlide = DspUtils.ToSigned((byte)(eventData[index - 2] & 255));
                    echoRightVolumeSlide = DspUtils.ToSigned((byte)(eventData[index - 2] >> 8));
                    echoSlideLength = timeToTicks(eventData[index - 1]);
                    if (echoSlideLength > 0xFF) echoSlideLength = 0xFF;
                    if (!echoSync) echoSlideUpdate = true;
                }
                else
                {
                    echoLeftVolume = DspUtils.ToSigned((byte)(eventData[index - 2] & 255));
                    echoRightVolume = DspUtils.ToSigned((byte)(eventData[index - 2] >> 8));
                    echoLeftVolumeSlide = 0;
                    echoRightVolumeSlide = 0;
                    echoSlideLength = 0;
                    if (!echoSync) echoSlideUpdate = false;
                }
            }
        }

        public void InitEchoChannel()
        {
            allowEchoUpdate = false;
            allowEcho = null;

            echoVolumeUpdate = false;
            echoEnableUpdate = false;
            echoDelayUpdate = false;
            echoSlideUpdate = false;
            firUpdate = false;

            echoDelay = 0;
            echoEnable = 0;
            echoFeedback = 0;
            echoLeftVolume = 0;
            echoRightVolume = 0;
            echoLeftVolumeSlide = 0;
            echoRightVolumeSlide = 0;
            echoSlideLength = 0;

            firFilter = new int[8];
            masterVolume = new int[2] { 0x7F, 0x7F };
        }

        public void ResetSync()
        {
            echoSync = false;
        }

        public void EnableSync()
        {
            echoSync = true;
        }

        private void GetVolumeChange(StringBuilder output)
        {
            if (!echoSlideUpdate && !echoEnableUpdate && !echoVolumeUpdate)
            {
                return;
            }

            int leftVolume = DspUtils.ToByte(echoLeftVolume * 0x7F / (double)masterVolume[0]);
            int rightVolume = DspUtils.ToByte(echoRightVolume * 0x7F / (double)masterVolume[1]);

            int leftVolumeS = DspUtils.ToByte(echoLeftVolumeSlide * 0x7F / (double)masterVolume[0]);
            int rightVolumeS = DspUtils.ToByte(echoRightVolumeSlide * 0x7F / (double)masterVolume[1]);

            //[11:24:37] <AlcaRobot> $EF $FF $XX $YY (command, channels, left vol, right vol)
            output.AppendLine($"$EF ${echoEnable:X2} ${leftVolume:X2} ${rightVolume:X2}");

            if (echoSlideUpdate)
            {
                output.AppendLine($"$F2 ${echoSlideLength:X2} ${leftVolumeS:X2} ${rightVolumeS:X2}");
            }

            echoVolumeUpdate = false;
            echoSlideUpdate = false;
            echoEnableUpdate = false;
        }

        public string GetEchoChanges()
        {
            StringBuilder output = new StringBuilder();

            if (allowEchoUpdate && !(bool)allowEcho)
            {
                allowEchoUpdate = false;
                return "$F0";
            }

            if (allowEcho != null && !(bool)allowEcho)
            {
                return "";
            }

            allowEchoUpdate = false;

            GetVolumeChange(output);

            if (echoDelayUpdate || echoFeedbackUpdate)
            {
                echoDelayUpdate = echoFeedbackUpdate = false;

                output.AppendLine($"$F1 ${(echoDelay & 15):X2} ${echoFeedback:X2} $01");
                firUpdate = true;
            }

            if (firUpdate)
            {
                firUpdate = false;

                int firTest = 0;

                for (int i = 0; i < 8; ++i)
                {
                    if (i == 0 && firFilter[i] == 0x7F)
                    {
                        continue;
                    }

                    firTest |= firFilter[i];
                }

                if (firTest != 0)
                {
                    output.Append("$F5");

                    for (int i = 0; i < 8; ++i)
                    {
                        output.Append($" ${firFilter[i]:X2}");
                    }

                    output.AppendLine();
                }
            }

            return output.ToString();
        }
        
        private int timeToTicks(double millisecond)
        {
            return (int)Math.Ceiling(millisecond * tempo / 512.0);
        }

        public void TempoUpdate(int newTempo)
        {
            // TO DO: update echo parameters if needed
            tempo = newTempo;
        }

        public void MasterVolumeUpdate(int[] newMasterVolume)
        {
            masterVolume = newMasterVolume;
        }
    }
}
