using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;

namespace PositionMonitorServiceLib
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the class name "Service1" in both code and config file together.
    public class PositionMonitor : IPositionMonitor
    {
        #region IPositionMonitor Members

        //public AccountSummary[] GetAllAccountSummaries()
        //{
        //    AccountPortfolio[] portfolios = PositionMonitorUtilities.GetAllAccountPortfolios();
        //    if (portfolios == null)
        //    {
        //        return null;
        //    }
        //    else
        //    {
        //        List<AccountSummary> summaries = new List<AccountSummary>();
        //        foreach (AccountPortfolio portfolio in portfolios)
        //        {
        //            summaries.Add(new AccountSummary(portfolio));
        //        }
        //        return summaries.ToArray();
        //    }
        //}

        public AccountSummary GetAccountSummary(string acctName)
        {
            AccountPortfolio portfolio = PositionMonitorUtilities.GetAccountPortfolio(acctName);
            if (portfolio != null)
                return new AccountSummary(portfolio);
            else
                return null;
        }

        #endregion
    }
}
