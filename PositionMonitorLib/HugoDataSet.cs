using System;
using System.Data.SqlClient;
using Gargoyle.Common;
using Gargoyle.Messaging.Common;

namespace PositionMonitorLib
{
    
    public partial class HugoDataSet {
        partial class PortfolioDataTable
        {
        }
    
        public partial class PortfolioRow : IQuoteRow
        {
            public QuoteType QuoteType
            {
                get
                {
                    if (IsStock > 0)
                        return QuoteType.Stock;
                    else if (IsOption > 0)
                        return QuoteType.Option;
                    else if (IsFuture > 0)
                        return QuoteType.Future;
                    else
                        throw new System.Data.StrongTypingException("QuoteType is invalid");
                }

            }

            public double CurrentPrice
            {
                get
                {
                    // if we are closed and have a closing price, use that as current price
                    if (Closed)
                    {
                        if (!IsClosingPriceNull())
                            return ClosingPrice;
                    }

                    // otherwise, use LastPrice for stocks and futures if available
                    if ((IsStock > 0) || (IsFuture > 0))
                    {
                        if (!IsLastPriceNull())
                            return LastPrice;
                    }

                    // otherwise use Last if within the current market or Bid or Ask if Last is outside the current market
                    if (!IsAskNull())
                    {
                        double bid = 0;
                        if (!IsBidNull())
                            bid = Bid;

                        if (!IsLastPriceNull())
                        {
                            if (LastPrice <= bid)
                                return bid;
                            else if (LastPrice >= Ask)
                                return Ask;
                            else
                                return LastPrice;
                        }
                        else if (!IsPrevCloseNull())
                        {
                            if (PrevClose <= bid)
                                return bid;
                            else if (PrevClose >= Ask)
                                return Ask;
                            else
                                return PrevClose;
                        }
                        else
                        {
                            return (Ask + bid) / 2.0;
                        }
                    }

                    // otherwise use Previous Close
                    if (!IsPrevCloseNull())
                    {
                        return PrevClose;
                    }

                    // otherwise, use yesterday's closing price according to custodian
                    if (!IsSOD_PriceNull())
                        return SOD_Price;

                    // as a last restort, return 0;
                    return 0;
                }
            }
        }

        public partial class AccountDataRow
        {
            public double AdjustedMinDelta
            {
                get
                {
                    TimeSpan timeSinceOpening = DateTime.Now.TimeOfDay - StartOfDay.TimeOfDay;
                    return MinDelta + LowerAdjustmentPerMinute * Math.Min(timeSinceOpening.TotalMinutes, MinutesInDay);
                }
            }
            public double AdjustedMaxDelta
            {
                get
                {
                    TimeSpan timeSinceOpening = DateTime.Now.TimeOfDay - StartOfDay.TimeOfDay;
                    return MaxDelta - UpperAdjustmentPerMinute * Math.Min(timeSinceOpening.TotalMinutes, MinutesInDay);;
                }
            }
        }

        public partial class IndicesRow : IQuoteRow
        {
            #region IQuoteRow Members


            public double Bid
            {
                get
                {
                    return 0;
                }
                set
                {
                }
            }

            public double Ask
            {
                get
                {
                    return 0;
                }
                set
                {
                }
            }

            public double Delta
            {
                get
                {
                    return 0;
                }
                set
                {
                }
            }

            public double Gamma
            {
                get
                {
                    return 0;
                }
                set
                {
                }
            }

            public double Theta
            {
                get
                {
                    return 0;
                }
                set
                {
                }
            }

            public double Vega
            {
                get
                {
                    return 0;
                }
                set
                {
                }
            }

            public double ImpliedVol
            {
                get
                {
                    return 0;
                }
                set
                {
                }
            }

            public double CurrentPrice
            {
                get
                {
                    // if we are closed and have a closing price, use that as current price
                    if (Closed)
                    {
                        if (!IsClosingPriceNull())
                            return ClosingPrice;
                    }

                    // otherise, use LastPrice
                    if (!IsLastPriceNull())
                        return LastPrice;

                    return 0;
                }
            }

            #endregion
        }
    }
}
namespace PositionMonitorLib.HugoDataSetTableAdapters
{

    public partial class PortfolioSnapshotIdsTableAdapter : IHugoTableAdapter
    {
        public void SetAllCommandTimeouts(int timeOut)
        {
            foreach (System.Data.IDbCommand cmd in CommandCollection)
            {
                cmd.CommandTimeout = timeOut;
            }
        }
        public void SetAllConnections(SqlConnection sqlConnection)
        {
            foreach (System.Data.IDbCommand cmd in CommandCollection)
            {
                cmd.Connection = sqlConnection;
            }
        }
        public void LogCommand(string commandText)
        {
            PositionMonitorUtilities.LogSqlCommand(CommandCollection, commandText);
        }

        public int GetReturnCode(string commandText)
        {
            return PositionMonitorUtilities.GetReturnCode(CommandCollection, commandText);
        }

    }
    public partial class PortfolioSnapshotsTableAdapter : IHugoTableAdapter
    {
        public void SetAllCommandTimeouts(int timeOut)
        {
            foreach (System.Data.IDbCommand cmd in CommandCollection)
            {
                cmd.CommandTimeout = timeOut;
            }
        }
        public void SetAllConnections(SqlConnection sqlConnection)
        {
            foreach (System.Data.IDbCommand cmd in CommandCollection)
            {
                cmd.Connection = sqlConnection;
            }
        }
        public void LogCommand(string commandText)
        {
            PositionMonitorUtilities.LogSqlCommand(CommandCollection, commandText);
        }

        public int GetReturnCode(string commandText)
        {
            return PositionMonitorUtilities.GetReturnCode(CommandCollection, commandText);
        }

    }
    public partial class CurrentPositionsTableAdapter : IHugoTableAdapter
    {
        public void SetAllCommandTimeouts(int timeOut)
        {
            foreach (System.Data.IDbCommand cmd in CommandCollection)
            {
                cmd.CommandTimeout = timeOut;
            }
        }
        public void SetAllConnections(SqlConnection sqlConnection)
        {
            foreach (System.Data.IDbCommand cmd in CommandCollection)
            {
                cmd.Connection = sqlConnection;
            }
        }
        public void LogCommand(string commandText)
        {
            PositionMonitorUtilities.LogSqlCommand(CommandCollection, commandText);
        }

        public int GetReturnCode(string commandText)
        {
            return PositionMonitorUtilities.GetReturnCode(CommandCollection, commandText);
        }

    }
    public partial class TradesTableAdapter : IHugoTableAdapter
    {
        public void SetAllCommandTimeouts(int timeOut)
        {
            foreach (System.Data.IDbCommand cmd in CommandCollection)
            {
                cmd.CommandTimeout = timeOut;
            }
        }
        public void SetAllConnections(SqlConnection sqlConnection)
        {
            foreach (System.Data.IDbCommand cmd in CommandCollection)
            {
                cmd.Connection = sqlConnection;
            }
        }
        public void LogCommand(string commandText)
        {
            PositionMonitorUtilities.LogSqlCommand(CommandCollection, commandText);
        }

        public int GetReturnCode(string commandText)
        {
            return PositionMonitorUtilities.GetReturnCode(CommandCollection, commandText);
        }

    }
    public partial class AccountDataTableAdapter : IHugoTableAdapter
    {
        public void SetAllCommandTimeouts(int timeOut)
        {
            foreach (System.Data.IDbCommand cmd in CommandCollection)
            {
                cmd.CommandTimeout = timeOut;
            }
        }
        public void SetAllConnections(SqlConnection sqlConnection)
        {
            foreach (System.Data.IDbCommand cmd in CommandCollection)
            {
                cmd.Connection = sqlConnection;
            }
        }
        public void LogCommand(string commandText)
        {
            PositionMonitorUtilities.LogSqlCommand(CommandCollection, commandText);
        }

        public int GetReturnCode(string commandText)
        {
            return PositionMonitorUtilities.GetReturnCode(CommandCollection, commandText);
        }

    }
    public partial class IndexWeightsTableAdapter : IHugoTableAdapter
    {
        public void SetAllCommandTimeouts(int timeOut)
        {
            foreach (System.Data.IDbCommand cmd in CommandCollection)
            {
                cmd.CommandTimeout = timeOut;
            }
        }
        public void SetAllConnections(SqlConnection sqlConnection)
        {
            foreach (System.Data.IDbCommand cmd in CommandCollection)
            {
                cmd.Connection = sqlConnection;
            }
        }
        public void LogCommand(string commandText)
        {
            PositionMonitorUtilities.LogSqlCommand(CommandCollection, commandText);
        }

        public int GetReturnCode(string commandText)
        {
            return PositionMonitorUtilities.GetReturnCode(CommandCollection, commandText);
        }

    }
    public partial class TradingScheduleTableAdapter : IHugoTableAdapter
    {
        public void SetAllCommandTimeouts(int timeOut)
        {
            foreach (System.Data.IDbCommand cmd in CommandCollection)
            {
                cmd.CommandTimeout = timeOut;
            }
        }
        public void SetAllConnections(SqlConnection sqlConnection)
        {
            foreach (System.Data.IDbCommand cmd in CommandCollection)
            {
                cmd.Connection = sqlConnection;
            }
        }
        public void LogCommand(string commandText)
        {
            PositionMonitorUtilities.LogSqlCommand(CommandCollection, commandText);
        }

        public int GetReturnCode(string commandText)
        {
            return PositionMonitorUtilities.GetReturnCode(CommandCollection, commandText);
        }

    }
}
