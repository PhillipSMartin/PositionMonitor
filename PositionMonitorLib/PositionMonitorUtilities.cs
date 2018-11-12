using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;
using System.ComponentModel;
using LoggingUtilitiesLib;
using Gargoyle.Common;

namespace PositionMonitorLib
{
    public class PositionMonitorUtilities
    {
        #region Table adapters
        private HugoDataSetTableAdapters.CurrentPositionsTableAdapter m_currentPositionsTableAdapter = new HugoDataSetTableAdapters.CurrentPositionsTableAdapter();
        private HugoDataSetTableAdapters.TradesTableAdapter m_tradesTableAdapter = new HugoDataSetTableAdapters.TradesTableAdapter();
        private HugoDataSetTableAdapters.AccountDataTableAdapter m_accountDataTableAdapter = new HugoDataSetTableAdapters.AccountDataTableAdapter();
        private HugoDataSetTableAdapters.IndexWeightsTableAdapter m_indexWeightsTableAdapter = new HugoDataSetTableAdapters.IndexWeightsTableAdapter();
        private HugoDataSetTableAdapters.TradingScheduleTableAdapter m_tradingScheduleTableAdapter = new HugoDataSetTableAdapters.TradingScheduleTableAdapter();
        private HugoDataSetTableAdapters.PortfolioSnapshotsTableAdapter m_portfolioSnapshotsTableAdapter = new HugoDataSetTableAdapters.PortfolioSnapshotsTableAdapter();
        private HugoDataSetTableAdapters.PortfolioSnapshotIdsTableAdapter m_portfolioSnapshotIdsTableAdapter = new HugoDataSetTableAdapters.PortfolioSnapshotIdsTableAdapter();

        private IHugoTableAdapter[] m_tableAdapters;
         #endregion

        #region Tables
        private HugoDataSet.AccountDataDataTable m_accountData;
        private HugoDataSet.CurrentPositionsDataTable m_currentPositions;
        private HugoDataSet.TradesDataTable m_trades;
        private HugoDataSet.IndexWeightsDataTable m_indexWeights;
        private HugoDataSet.TradingScheduleDataTable m_tradingSchedule;
        private HugoDataSet.PortfolioSnapshotIdsDataTable m_snapshotIds;
        private object m_tableLock = new object();
        private object m_connectionLock = new object();
        #endregion

        private Dictionary<string, AccountPortfolio> m_accountList = new Dictionary<string, AccountPortfolio>();

        private DateTime? m_tradesUpdateTime = null;
        private DateTime? m_accountsUpdateTime = null;
        private DateTime? m_positionsUpdateTime = null;
        private DateTime? m_indexWeightsUpdateTime = null;
        private DateTime? m_tradingScheduleUpdateTime = null;
        private DateTime? m_snapshotIdsUpdateTime = null;
        private DateTime? m_endOfDay = null;

        #region Event Handlers
        private static event LoggingEventHandler s_debugEventHandler;
        private static event LoggingEventHandler s_infoEventHandler;
        private static event LoggingEventHandler s_errorEventHandler;
        private event ServiceStoppedEventHandler m_monitorStoppedEventHandler;
        private event EventHandler<RefreshEventArgs> m_refreshEventHandler;

        // event fired when an exception occurs
        public event LoggingEventHandler OnError
        {
            add { s_errorEventHandler += value; }
            remove { s_errorEventHandler -= value; }
        }
        // event fired for logging
        public event LoggingEventHandler OnDebug
        {
            add { s_debugEventHandler += value; }
            remove { s_debugEventHandler -= value; }
        }
        public event LoggingEventHandler OnInfo
        {
            add { s_infoEventHandler += value; }
            remove { s_infoEventHandler -= value; }
        }
        // event fired when position monitor stops
        public event ServiceStoppedEventHandler OnMonitorStopped
        {
            add { m_monitorStoppedEventHandler += value; }
            remove { m_monitorStoppedEventHandler -= value; }
        }
        // event fired when tables are refreshed - subscribed to by Account
        internal event EventHandler<RefreshEventArgs> OnRefresh
        {
            add { m_refreshEventHandler += value; }
            remove { m_refreshEventHandler -= value; }
        }
        #endregion

        public PositionMonitorUtilities()
        {
             m_tableAdapters = new IHugoTableAdapter[]
            {
                m_currentPositionsTableAdapter,
                m_tradesTableAdapter,
                m_accountDataTableAdapter,
                m_indexWeightsTableAdapter,
                m_tradingScheduleTableAdapter,
                m_portfolioSnapshotsTableAdapter,
                m_portfolioSnapshotIdsTableAdapter
            };
            QuoteServerHost = Properties.Settings.Default.PublisherHost;
            QuoteServerPort = Properties.Settings.Default.Port;
            RefreshMs = Properties.Settings.Default.RefreshMs;
        }

        #region Public Properties
        public bool IsInitialized { get; private set; }
        public bool IsMonitoring { get; private set; }
        public bool IsUsingQuoteFeed { get { return !String.IsNullOrEmpty(QuoteServerHost); } }
        public int AccountLimit { get; set; }

        private static bool s_hadError;
        public bool HadError { get { return s_hadError; } private set { s_hadError = value; } }

        private static string s_lastErrorMessage;
        public string LastErrorMessage { get { return s_lastErrorMessage; } private set { s_lastErrorMessage = value; } }
        public DateTime? EndOfDay { get { return m_endOfDay; } }

        public ISnapshotProvider SnapshotProvider { get; set; }

        public string QuoteServerHost { get; set; }
        public int QuoteServerPort { get; set; }
        public int RefreshMs { get; set; }
        #endregion

        #region Public Methods
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        public bool Init(SqlConnection sqlConnection, int timeOut = 10000)
        {
            if (!IsInitialized)
            {
                try
                {
                    if (sqlConnection == null)
                        throw new ArgumentNullException("sqlConnection");

                    foreach (IHugoTableAdapter tableAdapter in m_tableAdapters)
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

        public bool StartMonitor()
        {
            if (!IsMonitoring)
            {
                try
                {
                    if (IsInitialized)
                    {
                        Info("Starting monitor");
                        RefreshEventArgs args = RefreshTables();
                        if (args.TablesUpdated)
                        {
                            if (BuildAccountPortfolios())
                            {
                                System.Threading.Thread.Sleep(RefreshMs); // wait one cycle to give quotes a chance to come in
                                IsMonitoring = true;
                                BackgroundWorker worker = new BackgroundWorker();
                                worker.DoWork += new DoWorkEventHandler(AsyncRefresh);
                                worker.RunWorkerAsync(args);
                                Info("Monitor started");
                            }
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

        public bool StopMonitor(Exception ex=null)
        {
            if (IsMonitoring)
            {
                try
                {
                    IsMonitoring = false;
                    if (m_monitorStoppedEventHandler != null)
                        m_monitorStoppedEventHandler(null, new ServiceStoppedEventArgs("PositionMonitor", "Monitor stopped", ex));
                    else
                        Info("Monitor stopped");

                    foreach (AccountPortfolio account in m_accountList.Values)
                    {
                        account.Stop();
                    }
                    m_accountList.Clear();
                }
                catch (Exception e)
                {
                    Error("Error in OnMonitorStopped handler", e);
                }
            }
            return true;
        }

        public void StopQuoteFeed()
        {
            if (IsMonitoring)
            {
                try
                {
                    Info("Stopping quote feed for all accounts");
                    foreach (AccountPortfolio account in m_accountList.Values)
                    {
                        account.StopSubscriber();
                    }
                 }
                catch (Exception e)
                {
                    Error("Error in OnMonitorStopped handler", e);
                }
            }
        }

        public string[] GetAllAccountNames()
        {
            try
            {
                if (IsMonitoring)
                    return m_accountList.Keys.ToArray();
                else
                {
                    DateTime? updateTime = null;
                    HugoDataSet.AccountDataDataTable accountData = GetAccountData(ref updateTime);
                    var accountNameRows = accountData.Select() as HugoDataSet.AccountDataRow[];
                    List<string> accountNames = new List<string>();
                    foreach (var accountNameRow in accountNameRows)
                    {
                        accountNames.Add(accountNameRow.AcctName);
                    }
                    return accountNames.ToArray();
                }
            }
            catch (Exception e)
            {
                Error("Error in GetAllAccountNames", e);
                return null;
            }
        }

        public double? GetCurrentPrice(string acctName, string symbol)
        {
            double? currentPrice = null;

            try
            {
                AccountPortfolio account = GetAccountPortfolio(acctName);
                if (account != null)
                {
                    var row = account.Portfolio.FindByAcctNameSymbol(acctName, symbol);
                    if (row != null)
                        currentPrice = row.CurrentPrice;
                }
            }
            catch (Exception e)
            {
                Error("Error in GetCurrentPrice", e);
            }
            return currentPrice;

        }

        public AccountPortfolio GetAccountPortfolio(string acctName)
        {
            AccountPortfolio account = null;

            try
            {
                if (IsMonitoring)
                {
                    if (!m_accountList.TryGetValue(acctName, out account))
                    {
                        Info(String.Format("Account {0] not found", acctName));
                    }
                }
                else
                {
                    Info("Error - monitor not started");
                }
            }
            catch (Exception e)
            {
                Error("Error in GetAccountPortfolio", e);
            }
            return account;
        }

        public HugoDataSet.PortfolioSnapshotsRow GetPortfolioSnapshot(short snapshotId)
        {
            HugoDataSet.PortfolioSnapshotsRow snapshot = null;
            if (IsInitialized)
            {
                    try
                    {
                        HugoDataSet.PortfolioSnapshotsDataTable table = null;
                        lock (m_connectionLock)
                        {
                            table = m_portfolioSnapshotsTableAdapter.GetData(snapshotId);
                        }

                        if (table != null)
                        {
                            if (table.Rows.Count > 0)
                            {
                                snapshot = table.Rows[0] as HugoDataSet.PortfolioSnapshotsRow;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Error("Exception getting portfolio snapshot", ex);
                    }
                    finally
                    {
                        m_portfolioSnapshotsTableAdapter.LogCommand("PositionMonitor.p_get_portfolio_snapshot2");
                    }
             }
            else
            {
                Info("Error - monitor not initialized");
            }
            return snapshot;
        }

        public AccountPortfolio[] GetAllAccountPortfolios()
        {

            try
            {
                if (IsMonitoring)
                    return m_accountList.Values.ToArray();
                else
                {
                    Info("Error - monitor not started");
                    return null;
                }
            }
            catch (Exception e)
            {
                Error("Error in GetAllAccountPortfolios", e);
                return null;
            }
        }

        public HugoDataSet.TradesRow[] GetAccountTrades(string acctName, DateTime tradeDate)
        {
            try
            {
                // if tradeDate is today, use trades table maintained by asynchronous thread
                // otherwise, read it from Hugo
                if (tradeDate == DateTime.Today)
                {
                    var portfolio = GetAccountPortfolio(acctName);
                    if (portfolio != null)
                        return portfolio.Trades;
                }
                else
                {
                    DateTime? updateTime = null;
                    var trades = GetTrades(acctName, tradeDate, ref updateTime);
                    if (trades != null)
                        return trades.Select() as HugoDataSet.TradesRow[];
                }
            }
            catch (Exception e)
            {
                Error("Error in GetAccountTrades", e);
            }

            return new HugoDataSet.TradesRow[] { };
        }

        public HugoDataSet.TradesRow[] GetAllAccountTrades(DateTime tradeDate)
        {
            try
            {
                // if tradeDate is today, use trades table maintained by asynchronous thread
                // otherwise, read it from Hugo
                if (tradeDate == DateTime.Today)
                {
                    lock (m_tableLock)
                    {
                        if (m_trades != null)
                        {
                            return m_trades.Select() as HugoDataSet.TradesRow[];
                        }
                        else
                        {
                            return new HugoDataSet.TradesRow[] { };
                        }
                    }
                }
                else
                {
                    return GetAccountTrades(null, tradeDate);
                }
            }
            catch (Exception e)
            {
                Error("Error in GetAllAccountPortfolios", e);
                return null;
            }
        }

        public HugoDataSet.TradingScheduleRow[] GetTradingScheduleRows()
        {
            try
            {
                lock (m_tableLock)
                {
                    if (m_tradingSchedule != null)
                        return m_tradingSchedule.Select() as HugoDataSet.TradingScheduleRow[];
                }
            }
            catch (Exception e)
            {
                Error("Error in GetTradingScheduleRows", e);
                return null;
            }
            return new HugoDataSet.TradingScheduleRow[] { };
        }

        public int InsertPortfolioSnapshot(string accountName, DateTime timeStamp, string snapshotType, double deltaPercent, double faceValuePutsPercent, double faceValuePutsPercentTarget, double returnOnEquity, bool tradingComplete, string xml)
        {
            short? snapshotId = null;
            if (!IsInitialized)
            {
                Info("Cannot insert snapshot data before PositionMonitorUtilities is initialized");
                return -1;
            }

            try
            {
                lock (m_connectionLock)
                {
                    m_portfolioSnapshotsTableAdapter.InsertSnaphot(accountName, timeStamp, snapshotType, deltaPercent, faceValuePutsPercent, faceValuePutsPercentTarget, returnOnEquity, tradingComplete, xml, ref snapshotId);
                    return snapshotId.HasValue ? snapshotId.Value : -1;
                }
            }
            catch (Exception ex)
            {
                Error("Exception inserting portfolio snapshot", ex);
                return -1;
            }
            finally
            {
                m_portfolioSnapshotsTableAdapter.LogCommand("PositionMonitor.p_insert_portfolio_snapshot2");
            }
        }

        public HugoDataSet.TradingScheduleRow GetNextTimeSliceForAccount(string acctName)
        {
            try
            {
                lock (m_tableLock)
                {
                    if (m_tradingSchedule != null)
                    {
                        HugoDataSet.TradingScheduleRow[] rows = m_tradingSchedule.Select(String.Format("AcctName = '{0}' AND EndTradingTime >= '{1}'", acctName, DateTime.Now), "StartTradingTime ASC") as HugoDataSet.TradingScheduleRow[];
                        if (rows.Length > 0)
                            return rows[0];
                    }
                }
            }
            catch (Exception ex)
            {
                Error("Exception in GetNextTimeSliceForAccount", ex);
            }
            return null;
        }

        public HugoDataSet.PortfolioSnapshotIdsRow[] GetSnapshotsForAccount(string acctName)
        {
            try
            {
                lock (m_tableLock)
                {
                    if (m_snapshotIds != null)
                        return m_snapshotIds.Select(String.Format("AccountName = '{0}'", acctName)) as HugoDataSet.PortfolioSnapshotIdsRow[];
                }

                // if the table hasn't been populated (possibly because this is a one-off request), get snapshotids for this account from Hugo
                DateTime? updateDate = null;
                var snapshotIds = GetSnapshotIds(acctName, ref updateDate);
                if (snapshotIds != null)
                    return snapshotIds.Select() as HugoDataSet.PortfolioSnapshotIdsRow[];
             }
            catch (Exception ex)
            {
                Error("Exception in GetSnapshotsForAccount", ex);
            }
            return new HugoDataSet.PortfolioSnapshotIdsRow[] { };
        }
         #endregion

        #region Internal Methods Used by AccountPortfolio
        internal HugoDataSet.AccountDataRow GetDataForAccount(string acctName)
        {
            try
            {
                lock (m_tableLock)
                {
                    if (m_accountData != null)
                        return m_accountData.FindByAcctName(acctName);
                }
            }
            catch (Exception ex)
            {
                Error("Exception in GetDataForAccount", ex);
            }
            return null;
        }

        internal HugoDataSet.CurrentPositionsRow[] GetCurrentPositionsForAccount(string acctName)
        {
            try
            {
                lock (m_tableLock)
                {
                    if (m_currentPositions != null)
                        return m_currentPositions.Select(String.Format("AcctName = '{0}'", acctName)) as HugoDataSet.CurrentPositionsRow[];
                }
            }
            catch (Exception ex)
            {
                Error("Exception in GetCurrentPositionsForAccount", ex);
            }
            return new HugoDataSet.CurrentPositionsRow[] { };
        }
        internal HugoDataSet.TradesRow[] GetTradesForAccount(string acctName)
        {
            try
            {
                lock (m_tableLock)
                {
                    if (m_trades != null)
                        return m_trades.Select(String.Format("AcctName = '{0}'", acctName)) as HugoDataSet.TradesRow[];
                }
            }
            catch (Exception ex)
            {
                Error("Exception in GetTradesForAccount", ex);
            }
            return new HugoDataSet.TradesRow[] { };
        }
        internal HugoDataSet.IndexWeightsRow[] GetIndexWeightsForAccount(string acctName)
        {
            try
            {
                lock (m_tableLock)
                {
                    if (m_indexWeights != null)
                        return m_indexWeights.Select(String.Format("AcctName = '{0}'", acctName)) as HugoDataSet.IndexWeightsRow[];
                }
            }
            catch (Exception ex)
            {
                Error("Exception in GetIndexWeightsForAccount", ex);
            }
            return new HugoDataSet.IndexWeightsRow[] { };
        }
        internal HugoDataSet.TradingScheduleRow[] GetStartOfTradingSnapshotsDue()
        {
            try
            {
                if (m_tradingSchedule != null)
                    return m_tradingSchedule.Select(String.Format("SnapshotNeeded = 1 AND StartOfTradingSnapshotTaken = 0 AND StartTradingTime <='{0}'", DateTime.Now)) as HugoDataSet.TradingScheduleRow[];
            }
            catch (Exception ex)
            {
                Error("Exception in GetStartOfTradingSnapshotsDue", ex);
            }
            return new HugoDataSet.TradingScheduleRow[] { };
        }
        internal HugoDataSet.TradingScheduleRow[] GetEndOfTradingSnapshotsDue()
        {
            try
            {
                if (m_tradingSchedule != null)
                    return m_tradingSchedule.Select(String.Format("SnapshotNeeded = 1 AND EndOfTradingSnapshotTaken = 0 AND EndTradingTime <='{0}'", DateTime.Now)) as HugoDataSet.TradingScheduleRow[];
            }
            catch (Exception ex)
            {
                Error("Exception in GetEndOfTradingSnapshotsDue", ex);
            }
            return new HugoDataSet.TradingScheduleRow[] { };
        }
        internal HugoDataSet.TradingScheduleRow[] GetEndOfDaySnapshotsDue()
        {
            try
            {
                if (m_tradingSchedule != null)
                    return m_tradingSchedule.Select(String.Format("SnapshotNeeded = 1 AND EndOfDaySnapshotTaken = 0")) as HugoDataSet.TradingScheduleRow[];
            }
            catch (Exception ex)
            {
                Error("Exception in GetEndOfDaySnapshotsDue", ex);
            }
            return new HugoDataSet.TradingScheduleRow[] { };
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

        internal void ClearErrorState()
        {
            HadError = false;
            LastErrorMessage = null;
        }

          #endregion

        #region Private Methods
        private void AsyncRefresh(object o, DoWorkEventArgs e)
        {
            try
            {
                RefreshEventArgs args = (RefreshEventArgs)e.Argument;
                while (IsMonitoring)
                {

                    if ((m_refreshEventHandler != null) && args.TablesUpdated)
                    {
                        Debug("Tables refreshed");
                        m_refreshEventHandler(null, args);
                    }
                    else
                    {
                        Debug("No updates to tables");
                    }

                    System.Threading.Thread.Sleep(RefreshMs);
                    if (IsMonitoring)
                    {
                        args = RefreshTables();
                    }

                    // make sure quote subscriber is running for each account
                    bool quoteServerIsUp = IsUsingQuoteFeed;
                    foreach (AccountPortfolio account in GetAllAccountPortfolios())
                    {
                        if (account.IsStarted && quoteServerIsUp)
                            quoteServerIsUp = account.StartSubscriber();
                    }
                }
                StopMonitor();
            }
            catch (Exception ex)
            {
                Error("Exception in refresh thread", ex);
                StopMonitor(ex);
            }
        }

        private bool BuildAccountPortfolios()
        {
            try
            {
                // make sure we have no legacies
                foreach (AccountPortfolio account in m_accountList.Values)
                {
                    account.Stop();
                }
                m_accountList.Clear();

                // build a portfolio for each account in AccountData table
                foreach (HugoDataSet.AccountDataRow row in m_accountData)
                {
                    Info("Building portfolio for account " + row.AcctName);
                    AccountPortfolio account = new AccountPortfolio(this, row);
                    m_accountList.Add(row.AcctName, account);
                }
            }
            catch (Exception ex)
            {
                Error("Error building account portfolios", ex);
                return false;
            }
            return true;
        }

        #region Read tables from Hugo
        private RefreshEventArgs RefreshTables(bool initializing = false)
        {
            RefreshEventArgs args = new RefreshEventArgs();
            try
            {
                RefreshCriticalTables(args);

                // skip non-critical tables if initializing, so we will start up sooner
                if (!initializing)
                {
#if !DEBUG
                    TakeRequiredSnapshots();
#endif
                    RefreshNoncriticalTables(args);

                    // try again before sleeping, in case above Refresh took a long time
#if !DEBUG
                   TakeRequiredSnapshots();
#endif
                }
             }
            catch (Exception ex)
            {
                Error("Unable to refresh data tables", ex);
            }
            return args;
        }

        private void RefreshNoncriticalTables(RefreshEventArgs args)
        {
            try
            {
                HugoDataSet.PortfolioSnapshotIdsDataTable snapshotIds = GetSnapshotIds(null, ref m_snapshotIdsUpdateTime);
                HugoDataSet.TradesDataTable trades = GetTrades(null, null, ref m_tradesUpdateTime);

                lock (m_tableLock)
                {
                    // if there are no changes, an empty table is returned by the above methods
                    if ((snapshotIds.Count > 0) || (m_snapshotIds == null))
                    {
                        m_snapshotIds = snapshotIds;
                        args.SnapshotIdsUpdated = true;
                    }
                    if ((trades.Count > 0) || (m_trades == null))
                    {
                        m_trades = trades;
                        args.TradesUpdated = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Error("Unable to refresh non-critical data tables", ex);
            }
        }

        private void RefreshCriticalTables(RefreshEventArgs args)
        {
            try
            {
                HugoDataSet.AccountDataDataTable accountData = GetAccountData(ref m_accountsUpdateTime);
                HugoDataSet.CurrentPositionsDataTable currentPositions = GetCurrentPositions(); ;
                HugoDataSet.IndexWeightsDataTable indexWeights = GetIndexWeights();
                HugoDataSet.TradingScheduleDataTable tradingSchedule = GetTradingSchedule();

                lock (m_tableLock)
                {
                    // if there are no changes, an empty table is returned by the above methods
                    if (accountData.Count > 0)
                    {
                        if (AccountLimit > 0)
                        {
                            for (int i = accountData.Count - 1; i >= AccountLimit; i--)
                            {
                                accountData.Rows.RemoveAt(i);
                            }
                        }
                        m_accountData = accountData;
                        args.AccountsUpdated = true;
                    }
                    if (currentPositions.Count > 0)
                    {
                        m_currentPositions = currentPositions;
                        args.PositionsUpdated = true;
                    }
                    if ((indexWeights.Count > 0) || (m_indexWeights == null))
                    {
                        m_indexWeights = indexWeights;
                        args.IndexWeightsUpdated = true;
                    }
                    if ((tradingSchedule.Count > 0) || (m_tradingSchedule == null))
                    {
                        m_tradingSchedule = tradingSchedule;
                        args.TradingScheduleUpdated = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Error("Unable to refresh critical data tables", ex);
            }
        }

        private void TakeRequiredSnapshots()
        {
            try
            {
                if (SnapshotProvider != null)
                {
                    if (SnapshotProvider.EndOfDay.Date == DateTime.Today)   // don't take snapshots on a non-trading day
                    {
                        var snapshotRows = GetStartOfTradingSnapshotsDue();
                        foreach (var row in snapshotRows)
                        {
                            row.StartOfTradingSnapshotTaken = 1;
                            SnapshotProvider.TakeSnapshot(row.AcctName, "StartOfTrading");
                        }

                        snapshotRows = GetEndOfTradingSnapshotsDue();
                        foreach (var row in snapshotRows)
                        {
                            row.EndOfTradingSnapshotTaken = 1;
                            SnapshotProvider.TakeSnapshot(row.AcctName, "EndOfTrading");
                        }

                        if (DateTime.Now > SnapshotProvider.EndOfDay)
                        {
                            snapshotRows = GetEndOfDaySnapshotsDue();
                            foreach (var row in snapshotRows)
                            {
                                row.EndOfDaySnapshotTaken = 1;
                                SnapshotProvider.TakeSnapshot(row.AcctName, "EndOfDay");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Error("Exception in TakeRequiredSnapshots method", ex);
            }
       }

        private HugoDataSet.AccountDataDataTable GetAccountData(ref DateTime? accountsUpdateTime)
        {
            try
            {
                lock (m_connectionLock)
                {
                    return m_accountDataTableAdapter.GetData(ref accountsUpdateTime, true);
                }
            }
            finally
            {
                m_accountDataTableAdapter.LogCommand("PositionMonitor.p_get_account_data3");
            }
        }
        private HugoDataSet.CurrentPositionsDataTable GetCurrentPositions()
        {
            if (!IsInitialized)
            {
                Info("Cannot get Current positions before PositionMonitorUtilities is initialized");
                return null;
            }

            try
            {
                lock (m_connectionLock)
                {
                    return m_currentPositionsTableAdapter.GetData(null, null, ref m_positionsUpdateTime);
                }
            }
            finally
            {
                m_currentPositionsTableAdapter.LogCommand("PositionMonitor.p_get_CurrentPositions");
            }
        }
        private HugoDataSet.TradesDataTable GetTrades(string acctName, DateTime? tradeDate, ref DateTime? updateTime)
        {
            if (!IsInitialized)
            {
                Info("Cannot get trades before PositionMonitorUtilities is initialized");
                return null;
            }

            try
            {
                lock (m_connectionLock)
                {
                    return m_tradesTableAdapter.GetData(acctName, tradeDate, ref updateTime);
                }
            }
            finally
            {
                m_tradesTableAdapter.LogCommand("PositionMonitor.p_get_Trades");
            }
        }
        private HugoDataSet.IndexWeightsDataTable GetIndexWeights()
        {
            if (!IsInitialized)
            {
                Info("Cannot get index weights before PositionMonitorUtilities is initialized");
                return null;
            }

            try
            {
                lock (m_connectionLock)
                {
                    return m_indexWeightsTableAdapter.GetData(null, ref m_indexWeightsUpdateTime);
                }
            }
            finally
            {
                m_indexWeightsTableAdapter.LogCommand("PositionMonitor.p_get_CurrentIndexWeights");
            }
        }
        private HugoDataSet.TradingScheduleDataTable GetTradingSchedule()
        {
            if (!IsInitialized)
            {
                Info("Cannot get trading schedule before PositionMonitorUtilities is initialized");
                return null;
            }

            try
            {
                lock (m_connectionLock)
                {
                    return m_tradingScheduleTableAdapter.GetData(null, ref m_tradingScheduleUpdateTime, ref m_endOfDay);
                }
            }
            finally
            {
                m_tradingScheduleTableAdapter.LogCommand("PositionMonitor.p_get_TradingSchedule");
            }
        }
        private HugoDataSet.PortfolioSnapshotIdsDataTable GetSnapshotIds(string acctName, ref DateTime? snapshotIdsUpdateTime)
        {
            if (!IsInitialized)
            {
                Info("Cannot get snapshot ids before PositionMonitorUtilities is initialized");
                return null;
            }

            try
            {
                lock (m_connectionLock)
                {
                    return m_portfolioSnapshotIdsTableAdapter.GetData(acctName, ref snapshotIdsUpdateTime);
                }
            }
            finally
            {
                m_portfolioSnapshotIdsTableAdapter.LogCommand("PositionMonitor.p_get_portfolio_snapshotids2");
            }
        }
        #endregion

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopMonitor();

                if (m_tableAdapters != null)
                {
                    foreach (IHugoTableAdapter tableAdapter in m_tableAdapters)
                    {
                        if (tableAdapter != null)
                        {
                            tableAdapter.Dispose();
                        }
                    }
                    m_tableAdapters = null;
                }

                if (m_currentPositions != null)
                {
                    m_currentPositions.Dispose();
                    m_currentPositions = null;
                }
                if (m_trades != null)
                {
                    m_trades.Dispose();
                    m_trades = null;
                }
                if (m_accountData != null)
                {
                    m_accountData.Dispose();
                    m_accountData = null;
                }
                if (m_indexWeights != null)
                {
                    m_indexWeights.Dispose();
                    m_indexWeights = null;
                }
                if (m_tradingSchedule != null)
                {
                    m_tradingSchedule.Dispose();
                    m_tradingSchedule = null;
                }
                Info("PositionMonitorUtilities disposed");
            }
        }
         #endregion
    }
}
