using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;

namespace PositionMonitorService
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the interface name "IService1" in both code and config file together.
    [ServiceContract]
    public interface IPositionMonitor
    {

        [OperationContract]
        AccountSummary GetAccountSummary(string acctName);
    }


    // Use a data contract as illustrated in the sample below to add composite types to service operations.
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

        internal AccountSummary(string acctName)
        {
            AccountName = acctName;
        }

        //internal AccountSummary(AccountPortfolio portfolio)
        //{
        //    if (portfolio != null)
        //    {
        //        AccountName = portfolio.AccountName;
        //        Portfolio = portfolio.Portfolio;
        //        Trades = portfolio.Trades;
        //        AccountData = portfolio.AccountData;
        //    }
        //}
    }
}
