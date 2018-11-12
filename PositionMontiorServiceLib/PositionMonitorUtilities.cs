using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LoggingUtilitiesLib;
using Gargoyle.Common;
using System.Data.SqlClient;
using System.Data;
using System.ComponentModel;

namespace PositionMonitorServiceLib
{
    public class PositionMonitorUtilities
    {
        #region Table adapters
        private static HugoDataSetTableAdapters.CurrentPositionsTableAdapter s_currentPositionsTableAdapter = new HugoDataSetTableAdapters.CurrentPositionsTableAdapter();
        private static HugoDataSetTableAdapters.TradesTableAdapter s_tradesTableAdapter = new HugoDataSetTableAdapters.TradesTableAdapter();
        private static HugoDataSetTableAdapters.AccountDataTableAdapter s_accountDataTableAdapter = new HugoDataSetTableAdapters.AccountDataTableAdapter();

        private static IHugoTableAdapter[] s_tableAdapters;
        #endregion

        #region Tables
        private static HugoDataSet.AccountDataDataTable s_accountData;
        private static HugoDataSet.CurrentPositionsDataTable s_currentPositions;
        private static HugoDataSet.TradesDataTable s_trades;
        private static object s_tableLock = new object();
        #endregion

        private static Dictionary<string, AccountPortfolio> s_accountList = new Dictionary<string, AccountPortfolio>();

        private static DateTime? s_tradesUpdateTime = null;
        private static DateTime? s_accountsUpdateTime = null;
        private static DateTime? s_positionsUpdateTime = null;

        #region Event Handlers
        private static event LoggingEventHandler s_debugEventHandler;
        private static event LoggingEventHandler s_infoEventHandler;
        private static event LoggingEventHandler s_errorEventHandler;
        private static event ServiceStoppedEventHandler s_monitorStoppedEventHandler;
        private static event EventHandler s_refreshEventHandler;

        // event fired when an exception occurs
        public static event LoggingEventHandler OnError
        {
            add { s_errorEventHandler += value; }
            remove { s_errorEventHandler -= value; }
        }
        // event fired for logging
        public static event LoggingEventHandler OnDebug
        {
            add { s_debugEventHandler += value; }
            remove { s_debugEventHandler -= value; }
        }
        public static event LoggingEventHandler OnInfo
        {
            add { s_infoEventHandler += value; }
            remove { s_infoEventHandler -= value; }
        }
        // event fired when position monitor stops
        public static event ServiceStoppedEventHandler OnMonitorStopped
        {
            add { s_monitorStoppedEventHandler += value; }
            remove { s_monitorStoppedEventHandler -= value; }
        }
        // event fired when tables are refreshed - subscribed to by Account
        internal static event EventHandler OnRefresh
        {
            add { s_refreshEventHandler += value; }
            remove { s_refreshEventHandler -= value; }
        }
        #endregion

        static PositionMonitorUtilities()
        {
            s_tableAdapters = new IHugoTableAdapter[]
            {
                s_currentPositionsTableAdapter,
                s_tradesTableAdapter,
                s_accountDataTableAdapter
            };
        }


        #region Public Properties
        public static bool IsInitialized { get; private set; }
        public static bool IsMonitoring { get; private set; }

        private static bool s_hadError;
        public static bool HadError { get { return s_hadError; } private set { s_hadError = value; } }

        private static string s_lastErrorMessage;
        public static string LastErrorMessage { get { return s_lastErrorMessage; } private set { s_lastErrorMessage = value; } }
        #endregion

        #region Public Methods
        public static void Dispose()
        {
            Dispose(true);
        }
        public static bool Init(SqlConnection sqlConnection, int timeOut = 10000)
        {
            if (!IsInitialized)
            {
                try
                {
                    if (sqlConnection == null)
                        throw new ArgumentNullException("sqlConnection");

                    foreach (IHugoTableAdapter tableAdapter in s_tableAdapters)
                    {
                        tableAdapter.SetAllConnections(sqlConnection);
                        tableAdapter.SetAllCommandTimeouts(timeOut);
                    }

                    Info("PositionMonitorUtilities SQL connection = " + sqlConnection.ConnectionString);

                    IsInitialized = true;
                    Info("PositionMonitorUtilities initialized");
                }
                catch (Exception ex)
                {
                    Error("Unable to initialize PositionMonitorUtilities", ex);
                }
            }
            return IsInitialized;
        }

        public static bool StartMonitor()
        {
            if (!IsMonitoring)
            {
                try
                {
                    if (IsInitialized)
                    {
                        Info("Starting monitor");
                        RefreshTables();
                        if (BuildAccountPortfolios())
                        {
                            IsMonitoring = true;
                            BackgroundWorker worker = new BackgroundWorker();
                            worker.DoWork += new DoWorkEventHandler(AsyncRefresh);
                            worker.RunWorkerAsync();
                            Info("Monitor started");
                        }
                    }
                    else
                    {
                        Info("Cannot start monitor before it is initialized");
                    }
                }
                catch (Exception ex)
                {
                    Error("Error trying to start Monitor", ex);
                    IsMonitoring = false;
                }
            }
            return IsMonitoring;
        }

        public static bool StopMonitor()
        {
            if (IsMonitoring)
            {
                try
                {
                    IsMonitoring = false;
                    if (s_monitorStoppedEventHandler != null)
                        s_monitorStoppedEventHandler(null, new ServiceStoppedEventArgs("PositionMonitor", "Monitor stopped"));
                    else
                        Info("Monitor stopped");

                    foreach (AccountPortfolio account in s_accountList.Values)
                    {
                        account.Stop();
                    }
                    s_accountList.Clear();
                }
                catch (Exception ex)
                {
                    Error("Error in OnMonitorStopped handler", ex);
                }
            }
            return true;
        }

        public static AccountPortfolio[] GetAllAccountPortfolios()
        {
            if (IsMonitoring)
                return s_accountList.Values.ToArray();
            else
            {
                Info("Error - monitor not started");
                return null;
            }
        }

        public static AccountPortfolio GetAccountPortfolio(string acctName)
        {
            AccountPortfolio account = null;
            if (IsMonitoring)
            {
                if (!s_accountList.TryGetValue(acctName, out account))
                {
                    Info(String.Format("Account {0] not found", acctName));
                }
            }
            else
            {
                Info("Error - monitor not started");
            }
            return account;
        }

        #endregion

        #region Internal Methods Used by AccountPortfolio
        internal static HugoDataSet.AccountDataRow GetDataForAccount(string acctName)
        {
            lock (s_tableLock)
            {
                if (s_accountData != null)
                    return s_accountData.FindByAcctName(acctName);
                else
                    return null;
            }
        }
        internal static HugoDataSet.CurrentPositionsRow[] GetCurrentPositionsForAccount(string acctName)
        {
            lock (s_tableLock)
            {
                if (s_currentPositions != null)
                    return s_currentPositions.Select(String.Format("AcctName = '{0}'", acctName)) as HugoDataSet.CurrentPositionsRow[];
                else
                    return new HugoDataSet.CurrentPositionsRow[] { };
            }
        }
        internal static HugoDataSet.TradesRow[] GetTradesForAccount(string acctName)
        {
            lock (s_tableLock)
            {
                if (s_trades != null)
                    return s_trades.Select(String.Format("AcctName = '{0}'", acctName)) as HugoDataSet.TradesRow[];
                else
                    return new HugoDataSet.TradesRow[] { };
            }
        }

        internal static void LogSqlCommand(IDbCommand[] commandCollection, string commandText)
        {
            try
            {
                LoggingUtilities.LogSqlCommand("SqlLog", commandCollection, commandText);
            }
            catch (LoggingUtilitiesCommandNotFoundException e)
            {
                Info("Logging error: " + e.Message);
            }
            catch (Exception)
            {
                throw;
            }
        }
        internal static int GetReturnCode(IDbCommand[] commandCollection, string commandText)
        {
            try
            {
                return LoggingUtilities.GetReturnCode(commandCollection, commandText);
            }
            catch (LoggingUtilitiesCommandNotFoundException e)
            {
                Info("Logging error: " + e.Message);
                return 8;
            }
            catch (Exception)
            {
                throw;
            }
        }

        internal static void Debug(string msg)
        {
            try
            {
                if (s_debugEventHandler != null)
                {
                    s_debugEventHandler(null, new LoggingEventArgs("PositionMonitorLib", msg));
                }
            }
            catch
            {
            }
        }

        internal static void Info(string msg)
        {
            try
            {
                if (s_infoEventHandler != null)
                {
                    s_infoEventHandler(null, new LoggingEventArgs("PositionMonitorLib", msg));
                }
            }
            catch
            {
            }
        }

        internal static void Error(string msg, Exception e)
        {
            try
            {
                s_hadError = true;
                s_lastErrorMessage = e.Message;
                if (s_errorEventHandler != null)
                    s_errorEventHandler(null, new LoggingEventArgs("PositionMonitorLib", msg, e));
            }
            catch
            {
            }
        }

        internal static void ClearErrorState()
        {
            HadError = false;
            LastErrorMessage = null;
        }

        #endregion

        #region Private Methods
        private static void AsyncRefresh(object o, DoWorkEventArgs args)
        {
            try
            {
                bool refreshed = true;
                while (IsMonitoring)
                {
                    if ((s_refreshEventHandler != null) && refreshed)
                    {
                        s_refreshEventHandler(null, new EventArgs());
                    }

                    System.Threading.Thread.Sleep(Properties.Settings.Default.RefreshMs);
                    if (IsMonitoring)
                    {
                        refreshed = RefreshTables();
#if DEBUG
                        if (refreshed)
                            Debug("Tables refreshed");
                        else
                            Debug("No refresh necessary");
#endif
                    }
                }
            }
            catch (Exception ex)
            {
                Error("Exception in refresh thread", ex);
            }
            finally
            {
                StopMonitor();
            }
        }

        #region Read tables from Hugo
        // returns false if no changes were made to the tables
        private static bool RefreshTables()
        {
            bool changesMade = false;
            try
            {
                HugoDataSet.AccountDataDataTable accountData = GetAccountData();
                HugoDataSet.CurrentPositionsDataTable currentPositions = GetCurrentPositions(); ;
                HugoDataSet.TradesDataTable trades = GetTrades();
                lock (s_tableLock)
                {
                    // if there are no changes, an empty table is returned by the above methods
                    if (accountData.Count > 0)
                        s_accountData = accountData;
                    if (currentPositions.Count > 0)
                        s_currentPositions = currentPositions;
                    if (trades.Count > 0)
                        s_trades = trades;
                }
                changesMade = (accountData.Count + currentPositions.Count + trades.Count > 0);
            }
            catch (Exception ex)
            {
                Error("Unable to refresh data tables", ex);
                return false;
            }
            return changesMade;
        }


        private static HugoDataSet.AccountDataDataTable GetAccountData()
        {
            if (!IsInitialized)
            {
                Info("Cannot get account data before PositionMonitorUtilities is initialized");
                return null;
            }

            try
            {
                return s_accountDataTableAdapter.GetData(ref s_accountsUpdateTime);
            }
            finally
            {
                s_accountDataTableAdapter.LogCommand("PositionMonitor.p_get_account_data");
            }
        }
        private static HugoDataSet.CurrentPositionsDataTable GetCurrentPositions()
        {
            if (!IsInitialized)
            {
                Info("Cannot get Current positions before PositionMonitorUtilities is initialized");
                return null;
            }

            try
            {
                return s_currentPositionsTableAdapter.GetData(null, null, ref s_positionsUpdateTime);
            }
            finally
            {
                s_currentPositionsTableAdapter.LogCommand("PositionMonitor.p_get_CurrentPositions");
            }
        }
        private static HugoDataSet.TradesDataTable GetTrades()
        {
            if (!IsInitialized)
            {
                Info("Cannot get trades before PositionMonitorUtilities is initialized");
                return null;
            }

            try
            {
                return s_tradesTableAdapter.GetData(null, null, ref s_tradesUpdateTime);
            }
            finally
            {
                s_tradesTableAdapter.LogCommand("PositionMonitor.p_get_Trades");
            }
        }
        #endregion

        private static bool BuildAccountPortfolios()
        {
            try
            {
                // make sure we have no legacies
                foreach (AccountPortfolio account in s_accountList.Values)
                {
                    account.Stop();
                }
                s_accountList.Clear();

                // build a portfolio for each account in AccountData table
                foreach (HugoDataSet.AccountDataRow row in s_accountData)
                {
                    AccountPortfolio account = new AccountPortfolio(row.AcctName);
                    s_accountList.Add(row.AcctName, account);
                }
            }
            catch (Exception ex)
            {
                Error("Error building account portfolios", ex);
                return false;
            }
            return true;
        }

        private static void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopMonitor();

                if (s_tableAdapters != null)
                {
                    foreach (IHugoTableAdapter tableAdapter in s_tableAdapters)
                    {
                        if (tableAdapter != null)
                        {
                            tableAdapter.Dispose();
                        }
                    }
                    s_tableAdapters = null;
                }

                if (s_currentPositions != null)
                {
                    s_currentPositions.Dispose();
                    s_currentPositions = null;
                }
                if (s_trades != null)
                {
                    s_trades.Dispose();
                    s_trades = null;
                }
                Info("PositionMonitorUtilities disposed");
            }
        }
        #endregion
    }
}
