using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SPCtoMML
{
    public class NoteLength
    {
        public int Length { get; set; }
        public int Staccato { get; set; }
        public int RealLength { get { return Length - Staccato; } }
    }
}
