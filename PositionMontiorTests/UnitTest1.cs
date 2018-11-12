using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PositionMonitorLib;
using Gargoyle.Common;
using Gargoyle.Utils.DBAccess;
using LoggingUtilitiesLib;

namespace PositionMonitorTests
{
    [TestClass]
    public class UnitTest1
    {
        PositionMonitorUtilities m_utilities = new PositionMonitorUtilities();

        [TestInitialize]
        public void SetupTests()
        {
            m_utilities.OnError += utilities_OnError;
            m_utilities.OnInfo += utilities_OnInfo;
            LoggingUtilities.OnError += utilities_OnError;
            LoggingUtilities.OnInfo += utilities_OnInfo;

            // get Hugo connection
            DBAccess dbAccess = DBAccess.GetDBAccessOfTheCurrentUser("Reconciliation");

            m_utilities.Init(dbAccess.GetConnection("Hugo"));
            m_utilities.StartMonitor();
        }

        void utilities_OnInfo(object sender, LoggingEventArgs e)
        {
            System.Diagnostics.Trace.WriteLine(e.Message);
        }

        void utilities_OnError(object sender, LoggingEventArgs e)
        {
            System.Diagnostics.Trace.WriteLine(e.Message + "=>" + e.Exception.Message);
        }

        [TestMethod]
        public void GetAccountTest()
        {

            AccountPortfolio account = m_utilities.GetAccountPortfolio("Adar");
            Assert.IsNotNull(account, "Get account failed");
        }
    }
}
