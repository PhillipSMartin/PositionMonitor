using System.Data.SqlClient;
using Gargoyle.Messaging.Common;

namespace PositionMonitorServiceLib
{

    public partial class HugoDataSet
    {
        partial class PortfolioDataTable
        {
        }

        public partial class PortfolioRow
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
        }
    }
}
namespace PositionMonitorServiceLib.HugoDataSetTableAdapters
{


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
}
