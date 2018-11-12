using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PositionMonitorLib
{
    interface IQuoteRow
    {
         double Bid { get; set; }
         double Ask { get; set; }
         double Open { get; set; }
         double PrevClose { get; set; }
         double LastPrice { get; set; }
         double ClosingPrice { get; set; }
         bool Closed { get; set; }
         double Delta { get; set; }
         double Gamma { get; set; }
         double Theta { get; set; }
         double Vega { get; set; }
         double ImpliedVol { get; set; }
         TimeSpan UpdateTime { get; set; }
         string SubscriptionStatus { get; set; }

         double CurrentPrice { get; }
    }
}
