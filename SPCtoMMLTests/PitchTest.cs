using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SPCtoMML;

namespace SPCtoMMLTests
{
    [TestClass]
    public class PitchTest
    {
        int[] pitchList = {
            66, 70, 75, 79, 84, 89, 94, 100, 106, 112, 119, 126, // o1
            133, 141, 150, 159, 168, 178, 189, 200, 212, 225, 238, 252, // o2
            267, 283, 300, 318, 337, 357, 378, 401, 425, 450, 477, 505, // o3
            535, 567, 601, 637, 675, 715, 757, 802, 850, 901, 954, 1011, // o4
            1071, 1135, 1202, 1274, 1350, 1430, 1515, 1605, 1701, 1802, 1909, 2022, // o5
            2143, 2270, 2405, 2548, 2700, 2860, 3030, 3211, 3402, 3604 // o6
        };

        [TestMethod]
        public void FundamentalMultiplierTest()
        {
            var pitch = new Pitch();
            
            for (int i = 0; i < pitchList.Length; i++)
            {
                Assert.AreEqual(pitchList[i], pitch.FindPitch(i, 0, 0x100));
            }
        }
    }
}
