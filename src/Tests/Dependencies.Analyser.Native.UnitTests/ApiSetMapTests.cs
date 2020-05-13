using System;
using Dependencies.ApiSetMapInterop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dependencies.Analyser.Native.UnitTests
{
    [TestClass]
    public class ApiSetMapTest
    {
        [TestMethod]
        public void GetApiSetMap()
        {
            var apiMapProvider = new ApiSetMapProviderInterop();
            var baseMap = apiMapProvider.GetApiSetMap();

            Assert.AreNotEqual(0, baseMap.Count);
        }

    }
}
