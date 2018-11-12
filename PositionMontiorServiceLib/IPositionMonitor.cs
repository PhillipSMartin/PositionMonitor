using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;

namespace PositionMonitorServiceLib
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the interface name "IService1" in both code and config file together.
    [ServiceContract]
    public interface IPositionMonitor
    {
 //       [OperationContract]
 //       AccountSummary[] GetAllAccountSummaries();

        [OperationContract]
        AccountSummary GetAccountSummary(string acctName);
    }

    [DataContract]
    public class AccountSummary
    {
        [DataMember]
        public string AccountName { get; set; }
        [DataMember]
        public DataTable Portfolio { get; set; }
        [DataMember]
        public DataTable Trades { get; set; }
        [DataMember]
        public DataRow AccountData { get; set; }

        internal AccountSummary(AccountPortfolio portfolio)
        {
            if (portfolio != null)
            {
                AccountName = portfolio.AccountName;
                Portfolio = portfolio.Portfolio;
                Trades = portfolio.Trades;
                AccountData = portfolio.AccountData;
            }
        }
    }
}
