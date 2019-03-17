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

        PitchCalculator pitchCalculator = new PitchCalculator();

        [TestMethod]
        public void FundamentalMultiplierTest()
        {
            for (int i = 0; i < pitchList.Length; i++)
            {
                Assert.AreEqual(pitchList[i], pitchCalculator.FindPitch(i, 0, 0x100));
            }
        }

        [TestMethod]
        public void NoteFindingTest()
        {
            for (int i = 0; i < pitchList.Length; i++)
            {
                var result = pitchCalculator.FindNote(pitchList[i], 0x100);
                Assert.AreEqual(i, result[0]);
                Assert.AreEqual(0, result[1]);
                Assert.AreEqual(0, result[2]);
                Assert.AreEqual(pitchList[i], result[3]);
            }
        }
    }
}
