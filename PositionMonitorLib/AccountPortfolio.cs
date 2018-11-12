using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GargoyleMessageLib;
using Gargoyle.Common;
using Gargoyle.Messaging.Common;
using System.Threading;

namespace PositionMonitorLib
{
    public class AccountPortfolio : IDisposable
    {
        private PositionMonitorUtilities m_monitorUtilities;
        private GargoyleMessageUtilities m_messageUtilities1 = new GargoyleMessageUtilities();
        private GargoyleMessageUtilities m_messageUtilities2 = new GargoyleMessageUtilities();

        private HugoDataSet.PortfolioDataTable m_portfolio = new HugoDataSet.PortfolioDataTable();
        private HugoDataSet.IndicesDataTable m_indices = new HugoDataSet.IndicesDataTable();
        private HugoDataSet.IndexWeightsRow[] m_indexWeights = new HugoDataSet.IndexWeightsRow[] { };
        private HugoDataSet.TradesRow[] m_trades = new HugoDataSet.TradesRow[] { };
        private HugoDataSet.PortfolioSnapshotIdsRow[] m_snapshotIds = new HugoDataSet.PortfolioSnapshotIdsRow[] { };
        private HugoDataSet.AccountDataRow m_accountData;
        private object m_portfolioLock = new object();
 
        private object m_quoteServerLock = new object();

        // event fired when tables are refreshed
        private event EventHandler m_refreshEventHandler;
        public event EventHandler OnRefresh
        {
            add { m_refreshEventHandler += value; }
            remove { m_refreshEventHandler -= value; }
        }

        public AccountPortfolio(PositionMonitorUtilities utilities, HugoDataSet.AccountDataRow accountDataRow)
        {
            m_monitorUtilities = utilities;
            m_accountData = accountDataRow;
            AccountName = accountDataRow.AcctName;
            UpdateBenchmark(BenchmarkSymbol, accountDataRow.IndexFlag ? QuoteType.Index : QuoteType.Stock);
  
            IsStarted = false;
            QuoteServiceStoppedTime = DateTime.Now.TimeOfDay;

            m_messageUtilities1.OnInfo += m_messageUtilities_OnInfo;
            m_messageUtilities1.OnDebug += m_messageUtilities_OnDebug;
            m_messageUtilities1.OnError += m_messageUtilities_OnError;
            m_messageUtilities1.OnQuote += m_messageUtilities_OnQuote;
            m_messageUtilities1.OnSubscriberStopped += m_messageUtilities_OnReaderStopped;

            m_messageUtilities2.OnInfo += m_messageUtilities_OnInfo;
            m_messageUtilities2.OnDebug += m_messageUtilities_OnDebug;
            m_messageUtilities2.OnError += m_messageUtilities_OnError;
            m_messageUtilities2.OnQuote += m_messageUtilities_OnQuote;
            m_messageUtilities2.OnSubscriberStopped += m_messageUtilities_OnReaderStopped;
        }

        #region Public Properties
        public string AccountName { get; private set; }
        public double DividendsReceived { get; set; }
        public string Name { get { return String.Format("AccountPortfolio for {0}", AccountName); } }
        public bool IsStarted { get; private set; }

        public TimeSpan? QuoteServiceStoppedTime { get; private set; }
        public TimeSpan? LastQuoteTime { get; private set; }
        public bool IsSubscribed { get; private set; }

        public HugoDataSet.PortfolioDataTable Portfolio
        {
            get
            {
                lock (m_portfolioLock)
                {
                    return m_portfolio.Copy() as HugoDataSet.PortfolioDataTable;
                }
            }
        }

        public HugoDataSet.IndicesRow[] Indices
        {
            get
            {
                List<HugoDataSet.IndicesRow> indicesList = new List<HugoDataSet.IndicesRow>();
                lock (m_portfolioLock)
                {
                    foreach (var index in m_indexWeights)
                    {
                        indicesList.Add( m_indices.Rows.Find(new string[] { AccountName, index.Symbol }) as HugoDataSet.IndicesRow );
                    }
                }

                return indicesList.ToArray();
            }
        }

        public HugoDataSet.IndicesRow Benchmark
        {
            get
            {
                if (BenchmarkSymbol != null)
                {
                    lock (m_portfolioLock)
                    {
                        return m_indices.Rows.Find(new string[] { AccountName, BenchmarkSymbol }) as HugoDataSet.IndicesRow;
                    }
                }

                return null;
            }
        }

        public HugoDataSet.AccountDataRow Data
        {
            get
            {
                return m_accountData;
            }
        }

        public HugoDataSet.TradesRow[] Trades
        {
            get
            {
                return m_trades;
            }
        }
        public HugoDataSet.PortfolioSnapshotIdsRow[] SnapshotIds
        {
            get
            {
                return m_snapshotIds;
            }
        }
        #endregion

        #region Public Methods

        // should call StartSubsriber() after calling Start()
        public bool Start()
        {
            try
            {
                if (!IsStarted)
                {
                    if (!m_monitorUtilities.IsMonitoring)
                    {
                        PositionMonitorUtilities.Info(String.Format("Cannot start {0} before starting monitor", Name));
                    }
                    else
                    {
                        RefreshEventArgs args = new RefreshEventArgs(true);
                        UpdateTables(args);
                        m_monitorUtilities.OnRefresh += utilities_OnRefresh;
                        IsStarted = true;
                    }
                }
            }
            catch (Exception ex)
            {
                PositionMonitorUtilities.Error("Unable to start " + Name, ex);
                IsStarted = false;
            }

            return IsStarted;
        }

        public bool Stop()
        {
            try
            {
                if (IsStarted)
                {
                     IsStarted = false;
                     m_monitorUtilities.OnRefresh -= utilities_OnRefresh;
                }

                StopSubscriber();
            }
            catch (Exception ex)
            {
                PositionMonitorUtilities.Error("Unable to stop " + Name, ex);
                IsStarted = false;
            }

            return !IsStarted;
        }

        public bool StartSubscriber()
        {
            try
            {
                lock (m_quoteServerLock)
                {
                    if (!IsSubscribed)
                    {
                        // start two subscribers - one for positions and one for indices (necessary in case there is an overlap)
                        IsSubscribed = m_messageUtilities1.StartSubscriber(m_monitorUtilities.QuoteServerHost, m_monitorUtilities.QuoteServerPort)
                            && m_messageUtilities2.StartSubscriber(m_monitorUtilities.QuoteServerHost, m_monitorUtilities.QuoteServerPort);
                        if (IsSubscribed)
                        {
                            QuoteServiceStoppedTime = null;

                            int rowCount;
                            lock (m_portfolioLock)
                            {
                                rowCount = 0;
                                foreach (HugoDataSet.PortfolioRow row in m_portfolio.Rows)
                                {
                                    if (!row.IsCurrent_PositionNull())
                                    {
                                        int sodPosition = row.IsSOD_PositionNull() ? 0 : row.SOD_Position;
                                        if ((row.Current_Position != 0) || (sodPosition != 0) || (row.IsStock == 1))
                                        {
                                            row.SubscriptionStatus = SubscriptionStatus.Subscribed.ToString();
                                            m_messageUtilities1.Subscribe(row.Symbol, row.QuoteType, row);
                                            rowCount++;
                                        }
                                    }
                                }
                                PositionMonitorUtilities.Info(String.Format("{0} subscribed to {1} position symbols, host={2}", Name, rowCount, m_monitorUtilities.QuoteServerHost));

                                rowCount = 0;
                                foreach (HugoDataSet.IndicesRow row in m_indices.Rows)
                                {
                                    QuoteType quoteType = row.IndexFlag ? QuoteType.Index : QuoteType.Stock;
                                    row.SubscriptionStatus = SubscriptionStatus.Subscribed.ToString();
                                    m_messageUtilities2.Subscribe(row.Symbol, quoteType, row);
                                    rowCount++;
                                }
                                PositionMonitorUtilities.Info(String.Format("{0} subscribed to {1} index symbols, host={2}", Name, rowCount, m_monitorUtilities.QuoteServerHost));
                            }
                          }
                    }
                }
            }
            catch (Exception ex)
            {
                PositionMonitorUtilities.Error(Name + " unable to subscribe to market data", ex);
                StopSubscriber();
            }

            return IsSubscribed;
        }

        public void StopSubscriber()
        {
            try
            {
                lock (m_quoteServerLock)
                {
                    if (IsSubscribed)
                    {
                        IsSubscribed = false;
                        QuoteServiceStoppedTime = DateTime.Now.TimeOfDay;
                        m_messageUtilities1.StopSubscriber();
                        m_messageUtilities2.StopSubscriber();

                        lock (m_portfolioLock)
                        {
                            foreach (HugoDataSet.PortfolioRow row in m_portfolio.Rows)
                            {
                                row.SubscriptionStatus = SubscriptionStatus.Unsubscribed.ToString();
                                row.UpdateTime = DateTime.Now - DateTime.Today;
                            }

                            foreach (HugoDataSet.IndicesRow row in m_indices.Rows)
                            {
                                row.SubscriptionStatus = SubscriptionStatus.Unsubscribed.ToString();
                                row.UpdateTime = DateTime.Now - DateTime.Today;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                PositionMonitorUtilities.Error(Name + " enountered an error stopping subscriber", ex);
            }
      }

        public int NumberOfTrades
        {
            get
            {
                if (m_trades == null)
                    return 0;
                else
                    return m_trades.Length;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        #region Private Methods
        #region Event handlers
        private void m_messageUtilities_OnInfo(object sender, LoggingEventArgs e)
        {
            PositionMonitorUtilities.Info(e.Message);
        }
        private void m_messageUtilities_OnDebug(object sender, LoggingEventArgs e)
        {
            PositionMonitorUtilities.Debug(e.Message);
        }
        private void m_messageUtilities_OnError(object sender, LoggingEventArgs e)
        {
            if (e.Exception.GetType() == typeof(System.Net.Sockets.SocketException))
            {
                // don't bother reporting socket exception unless we are already connected
                if (!IsSubscribed)
                   return;
            }
            PositionMonitorUtilities.Error(e.Message, e.Exception);
        }

         private void m_messageUtilities_OnQuote(object sender, QuoteEventArgs e)
        {
            string ticker = null;
            try
            {
                if (IsSubscribed)
                {
                    ticker = e.Quote.Ticker;

                    IQuoteRow row = e.ClientObject as IQuoteRow;

                    lock (m_portfolioLock)
                    {
                        bool bUpdated = false;
                        if (e.Quote.SubscriptionStatus != SubscriptionStatus.Unchanged)
                        {
                            row.SubscriptionStatus = e.Quote.SubscriptionStatus.ToString();
                            bUpdated = true;
                        }

                        if (e.Quote.OpenStatus == OpenStatus.Closed)
                        {
                            row.Closed = true;
                            bUpdated = true;
                        }
                        else if (e.Quote.OpenStatus == OpenStatus.Open)
                        {
                            row.Closed = false;
                            bUpdated = true;
                        }

                        if (e.Quote.HasOpen)
                        {
                            row.Open = e.Quote.Open;
                            bUpdated = true;
                        }

                        if (e.Quote.HasPrevClose)
                        {
                            row.PrevClose = e.Quote.PrevClose;
                            bUpdated = true;
                        }

                        if (!row.Closed)
                        {
                            if (e.Quote.HasLast)
                            {
                                row.LastPrice = e.Quote.Last;
                                bUpdated = true;
                            }

                            if (e.Quote.HasBid)
                            {
                                row.Bid = e.Quote.Bid;
                                bUpdated = true;
                            }
                            if (e.Quote.HasAsk)
                            {
                                row.Ask = e.Quote.Ask;
                                bUpdated = true;
                            }
                            if (e.Quote.HasDelta)
                            {
                                row.Delta = e.Quote.Delta;
                                bUpdated = true;
                            }
                            if (e.Quote.HasGamma)
                            {
                                row.Gamma = e.Quote.Gamma;
                                bUpdated = true;
                            }
                            if (e.Quote.HasTheta)
                            {
                                row.Theta = e.Quote.Theta;
                                bUpdated = true;
                            }
                            if (e.Quote.HasVega)
                            {
                                row.Vega = e.Quote.Vega;
                                bUpdated = true;
                            }
                            if (e.Quote.HasImpliedVol)
                            {
                                row.ImpliedVol = e.Quote.ImpliedVol;
                                bUpdated = true;
                            }
                        }

                        else // i.e., if row.Closed
                        {
                            if (e.Quote.HasClose)
                            {
                                row.ClosingPrice = e.Quote.Close;
                                bUpdated = true;
                            }
                        }

                        if (bUpdated)
                        {
                            LastQuoteTime = row.UpdateTime = DateTime.Now.TimeOfDay;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                PositionMonitorUtilities.Error(Name + " unable to process quote for " + (ticker ?? "<NULL>"), ex);
            }
        }

        private void m_messageUtilities_OnReaderStopped(object sender, ServiceStoppedEventArgs e)
        {
 
            if (e.Exception == null)
                PositionMonitorUtilities.Info(e.Message);
            else
                PositionMonitorUtilities.Error(e.Message, e.Exception);

            StopSubscriber();

            if (m_refreshEventHandler != null)
                m_refreshEventHandler(this, new EventArgs());
        }

        private void utilities_OnRefresh(object sender, RefreshEventArgs e)
        {
            if (IsStarted)
                UpdateTables(e);
        }
        #endregion

        private void UpdateTables(RefreshEventArgs e)
        {
            string msg = String.Format("{0} refreshed portfolio:", Name);

            if (e.PositionsUpdated)
            {
                UpdatePositions(m_monitorUtilities.GetCurrentPositionsForAccount(AccountName));
                msg += String.Format(" {0} positions,", m_portfolio.Count);
            }

            if (e.IndexWeightsUpdated)
            {
                UpdateIndices(m_monitorUtilities.GetIndexWeightsForAccount(AccountName));
                msg += " index weights,";
            }

            if (e.AccountsUpdated)
            {
                m_accountData = m_monitorUtilities.GetDataForAccount(AccountName);
                if (m_accountData != null)
                {
                    UpdateBenchmark(BenchmarkSymbol, m_accountData.IndexFlag ? QuoteType.Index : QuoteType.Stock);
                    msg += " account info,";
                }
            }

            if (e.TradesUpdated)
            {
                m_trades = m_monitorUtilities.GetTradesForAccount(AccountName);
                DividendsReceived = - m_trades.Where(x => x.TradeType == "RecDiv").Sum(x => x.Change_in_Cost);
                msg += String.Format(" {0} trades,", NumberOfTrades);
            }

            if (e.SnapshotIdsUpdated)
            {
                m_snapshotIds = m_monitorUtilities.GetSnapshotsForAccount(AccountName);
                msg += " snapshots";
            }

            PositionMonitorUtilities.Debug(msg);
            if (m_refreshEventHandler != null)
                m_refreshEventHandler(this, new EventArgs());
        }

        private bool UpdatePositions(HugoDataSet.CurrentPositionsRow[] rows)
        {
            try
            {
                List<HugoDataSet.PortfolioRow> subscribeList = new List<HugoDataSet.PortfolioRow>();
                List<string> unsubscribeList = new List<string>();
                List<string> indicesForNetting = new List<string>();

                int updateCounter = 1;
                lock (m_portfolioLock)
                {
                    if (m_portfolio.Rows.Count > 0)
                    {
                        updateCounter = m_portfolio[0].UpdateCounter + 1;
                    }
                    foreach (HugoDataSet.CurrentPositionsRow positionRow in rows)
                    {
                        if (!BuildPortfolioRow(subscribeList, indicesForNetting, positionRow, updateCounter))
                            return false;
                    }

                    // remove any rows which no longer exists in the query result set 
                    // (Expired options may fit this criterion. They may have been included in an early query result before they were removed from Hugo)
                    foreach (HugoDataSet.PortfolioRow portfolioRow in m_portfolio.Rows)
                    {
                        if (portfolioRow.UpdateCounter < updateCounter)
                        {
                            //if (portfolioRow.SubscriptionStatus == SubscriptionStatus.Subscribed.ToString())
                            //{
                            //    unsubscribeList.Add(portfolioRow.Symbol);
                            //}
                            //m_portfolio.RemovePortfolioRow(portfolioRow);

                            // don't remove the row but make sure position is zero
                            portfolioRow.UpdateCounter = updateCounter;
                            if (portfolioRow.Current_Position != 0)
                            {
                                portfolioRow.Change_in_Position -= portfolioRow.Current_Position;
                                portfolioRow.Current_Position = 0;
                                portfolioRow.Current_Cost = 0;
                                if ((portfolioRow.IsOption == 1) || (portfolioRow.IsFuture == 1))
                                {
                                    if (!indicesForNetting.Contains(portfolioRow.UnderlyingSymbol))
                                        indicesForNetting.Add(portfolioRow.UnderlyingSymbol);
                                }
                            }
                        }
                    }

                    // don't abort simply because netting fails
                    PerformNettingForAccount(indicesForNetting);
                }

                return ManageSubscriptions(subscribeList, unsubscribeList);
            }
            catch (Exception ex)
            {
                PositionMonitorUtilities.Error(string.Format("{0} unable to fill current position table", Name), ex);
                return false;
            }
        }

        private bool PerformNettingForAccount(List<string> indicesForNetting)
        {
            bool worked = true;
            try
            {
                foreach (string index in indicesForNetting)
                {
                    worked &= PerformNettingForIndex(index);
                }
            }
            catch (Exception ex)
            {
                PositionMonitorUtilities.Error(String.Format("{0} unable to perform netting", Name), ex);
                return false;
            }

            return worked;
        }

        private bool PerformNettingForIndex(string index)
        {
            HugoDataSet.PortfolioRow[] allPositionsForIndex = new HugoDataSet.PortfolioRow[0];

            try
            {
                allPositionsForIndex = m_portfolio.Select(String.Format("UnderlyingSymbol = '{0}'", index)) as HugoDataSet.PortfolioRow[];

                // 1.	Set the netted positions in all futures, calls, and puts equal to the actual positions.
                foreach (HugoDataSet.PortfolioRow row in allPositionsForIndex)
                {
                    row.Netting_Adjustment = 0;
                }

                // 2.	Select a future which we have not processed yet. 
                var futurePositions = m_portfolio.Select(
                    String.Format("UnderlyingSymbol = '{0}' AND IsFuture=1", index), "ExpirationDate ASC") as HugoDataSet.PortfolioRow[];
                foreach (HugoDataSet.PortfolioRow futurePosition in futurePositions)
                {
                    // 3.	Select the next expiration in which we have both a netted position greater than zero in the selected future and a netted position greater than zero in some put. 
                    if ((futurePosition.Current_Position + futurePosition.Netting_Adjustment) > 0)
                    {

                        // 4.	Select the put with the lowest strike price for which we have a netted position greater than zero.
                        HugoDataSet.PortfolioRow[] puts = m_portfolio.Select(String.Format("UnderlyingSymbol = '{0}' AND OptionType='Put' AND ExpirationDate = '{1:d}' AND (([Current Position] + [Netting Adjustment]) > 0)", index, futurePosition.ExpirationDate), "StrikePrice ASC") as HugoDataSet.PortfolioRow[];
                        foreach (HugoDataSet.PortfolioRow put in puts)
                        {
                            // 5.	Calculate the number F as the minimum of
                            // a.	The netted position in the future times the multiplier (usually 50).
                            // b.	The netted position in the selected put times the shares per contract (usually 100).
                            // c.	Minus one times he sum of netted positions of all short calls (i.e., ignoring any calls we are long) times the shares per contract (usually 100)  in strikes equal to or below the strike of the selected put
                            int F = Math.Min((futurePosition.Current_Position + futurePosition.Netting_Adjustment) * futurePosition.Multiplier,
                                (put.Current_Position + put.Netting_Adjustment) * put.Multiplier);

                            if (F > 0)
                            {
                                HugoDataSet.PortfolioRow[] calls = m_portfolio.Select(String.Format("UnderlyingSymbol = '{0}' AND OptionType='Call' AND ExpirationDate = '{1:d}' AND (([Current Position] + [Netting Adjustment]) < 0) AND StrikePrice <= {2}", index, futurePosition.ExpirationDate, put.StrikePrice), "StrikePrice DESC") as HugoDataSet.PortfolioRow[];
                                int callSum = 0;
                                foreach (HugoDataSet.PortfolioRow call in calls)
                                {
                                    callSum -= (call.Current_Position + call.Netting_Adjustment) * call.Multiplier;
                                }
                                F = Math.Min(F, callSum);

                                //6.	If F is greater than zero, change netted positions as follows, otherwise move on to the next step. 
                                if (F > 0)
                                {
                                    // a.	Reduce the netted position of the selected future by F divided by the multiplier.
                                    futurePosition.Netting_Adjustment -= F / futurePosition.Multiplier;

                                    // b.	Reduce the netted position of the selected put by F divided by the shares per contract.
                                    put.Netting_Adjustment -= F / put.Multiplier;

                                    // c.	Select with a call for which we have a negative netted position on a strike equal to or less than the strike 
                                    //      of the selected put. Select the call with the highest strike if there is more than one such call.
                                    foreach (HugoDataSet.PortfolioRow call in calls)
                                    {
                                        //d.	Define f as the minimum of F and -1 times the netted position of the selected call times the shares per contract
                                        int f = Math.Min(F, -(call.Current_Position + call.Netting_Adjustment) * call.Multiplier);

                                        // e.	Reduce F by f. Increase the netted position of the selected call by f divided by the shares per contract
                                        F -= f;
                                        call.Netting_Adjustment += f / call.Multiplier;

                                        //f.	If F is still greater than zero, select the next short call position (the next lower strike) 
                                        //      and go back to step d. If F is zero or if there is no such call, go on to the next step.
                                        if (F <= 0)
                                            break;
                                    }
                                }
                            }

                            // 7.	If the netted position in the future is still greater than zero, select the next put (moving to higher strike prices) 
                            //      in which we have a netted position greater than zero, and go back to step 5. 
                            //      If the netted position in the future is zero or if there is no such put, go back to step 3.
                            if ((futurePosition.Current_Position + futurePosition.Netting_Adjustment) <= 0)
                                break;
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                PositionMonitorUtilities.Error(String.Format("{0} unable to perform netting for {1}", Name, index), ex);

                // undo any calculations
                foreach (HugoDataSet.PortfolioRow row in allPositionsForIndex)
                {
                    row.Netting_Adjustment = 0;
                }
                return false;
            }
            return true;
        }

        private bool ManageSubscriptions(List<HugoDataSet.PortfolioRow> subscribeList, List<string> unsubscribeList)
        {
            try
            {
                if (IsSubscribed)
                {
                    if (subscribeList.Count > 0)
                    {
                        foreach (HugoDataSet.PortfolioRow row in subscribeList)
                        {
                            m_messageUtilities1.Subscribe(row.Symbol, row.QuoteType, row);
                            PositionMonitorUtilities.Info(String.Format("{0} now subscribed to {1}", Name, row.Symbol));
                        }
                        PositionMonitorUtilities.Info(String.Format("{0} subscribed to {1} new symbols", Name, subscribeList.Count));
                    }
                    if (unsubscribeList.Count > 0)
                    {
                        foreach (string ticker in unsubscribeList)
                        {
                            m_messageUtilities1.Unsubscribe(ticker);
                            PositionMonitorUtilities.Info(String.Format("{0} now unsubscribed from {1}", Name, ticker));
                        }
                        PositionMonitorUtilities.Info(String.Format("{0} unsubscribed from {1} symbols", Name, unsubscribeList.Count));
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                PositionMonitorUtilities.Error(Name + " unable to manage subscriptions", ex);
                return false;
            }
        }

        private bool BuildPortfolioRow(List<HugoDataSet.PortfolioRow> subscribeList, List<string> indicesForNetting, HugoDataSet.CurrentPositionsRow positionRow, int updateCounter)
        {
            try
            {
                bool bCheckForNetting;
                bCheckForNetting = false;

                HugoDataSet.PortfolioRow portfolioRow = m_portfolio.Rows.Find(new string[] { positionRow.AcctName, positionRow.Symbol }) as HugoDataSet.PortfolioRow;
                if (portfolioRow == null)
                {
                    bCheckForNetting = true;

                    portfolioRow = m_portfolio.NewPortfolioRow();
                    portfolioRow.AcctName = positionRow.AcctName;
                    portfolioRow.Symbol = positionRow.Symbol;

                    if (!positionRow.IsExpirationDateNull())
                        portfolioRow.ExpirationDate = positionRow.ExpirationDate;
                    if (!positionRow.IsStrikePriceNull())
                        portfolioRow.StrikePrice = positionRow.StrikePrice;
                    if (!positionRow.IsOptionTypeNull())
                        portfolioRow.OptionType = positionRow.OptionType;
                    if (!positionRow.IsUnderlyingSymbolNull())
                        portfolioRow.UnderlyingSymbol = positionRow.UnderlyingSymbol;

                    // Two multipliers are necessary
                    //  Multiplier is used to determine the deltas on the associated index
                    //  PriceMultiplier is used to determine the market value fo the option itself
                    portfolioRow.Multiplier = (short)Math.Round(positionRow.Multiplier * positionRow.AssociatedIndexMultiplier, 0);
                    portfolioRow.PriceMultiplier = positionRow.Multiplier;
                    portfolioRow.IsStock = positionRow.IsStock;
                    portfolioRow.IsOption = positionRow.IsOption;
                    portfolioRow.IsFuture = positionRow.IsFuture;
                    m_portfolio.Rows.Add(portfolioRow);
                }
                else
                {
                    // if current position has changed, we must perform netting
                    if (portfolioRow.Current_Position != positionRow.Current_Position)
                    {
                        bCheckForNetting = true;
                    }
                }
                portfolioRow.Current_Position = positionRow.IsCurrent_PositionNull() ? 0 : positionRow.Current_Position;
                portfolioRow.SOD_Position = positionRow.IsSOD_PositionNull() ? 0 : positionRow.SOD_Position;
                portfolioRow.SOD_Price = positionRow.SOD_Price;
                portfolioRow.SOD_Market_Value = positionRow.SOD_Market_Value;
                portfolioRow.Change_in_Position = positionRow.Change_in_Position;
                portfolioRow.Change_in_Cost = positionRow.Change_in_Cost;
                portfolioRow.Current_Cost = positionRow.Current_Cost;
                portfolioRow.UpdateCounter = updateCounter;

                if (IsSubscribed && (portfolioRow.SubscriptionStatus != SubscriptionStatus.Subscribed.ToString()))
                {
                    // must subscribe to stocks with 0 positions in case we have associated options (so we can calculated dollar deltas)
                    if ((portfolioRow.Current_Position != 0) || (portfolioRow.SOD_Position != 0) || (portfolioRow.IsStock == 1))
                    {
                        portfolioRow.SubscriptionStatus = SubscriptionStatus.Subscribed.ToString();
                        subscribeList.Add(portfolioRow);
                    }
                }

                if (bCheckForNetting)
                {
                    if ((portfolioRow.IsOption == 1) || (portfolioRow.IsFuture == 1))
                    {
                        if (!indicesForNetting.Contains(portfolioRow.UnderlyingSymbol))
                            indicesForNetting.Add(portfolioRow.UnderlyingSymbol);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                PositionMonitorUtilities.Error(Name + " unable to build portfolio row", ex);
                return false;
            }
        }

        private bool UpdateBenchmark(string symbol, QuoteType quoteType)
        {
            try
            {
                if (!String.IsNullOrEmpty(symbol))
                {
                    if (symbol == "SPXT")
                        symbol = "SPX";

                    bool subscribe = false;
                    HugoDataSet.IndicesRow benchmarkRow = null;
                    lock (m_portfolioLock)
                    {
                        benchmarkRow = m_indices.Rows.Find(new string[] { AccountName, symbol }) as HugoDataSet.IndicesRow;
                        if (benchmarkRow == null)
                        {
                            benchmarkRow = m_indices.NewIndicesRow();
                            benchmarkRow.AcctName = AccountName;
                            benchmarkRow.Symbol = symbol;
                            benchmarkRow.IndexFlag = (quoteType == QuoteType.Index);
                            m_indices.Rows.Add(benchmarkRow);
                        }
                        if (IsSubscribed && (benchmarkRow.SubscriptionStatus != SubscriptionStatus.Subscribed.ToString()))
                        {
                            benchmarkRow.SubscriptionStatus = SubscriptionStatus.Subscribed.ToString();
                            subscribe = true;
                        }
                    }

                    if (subscribe)
                    {
                        m_messageUtilities1.Subscribe(benchmarkRow.Symbol, quoteType, benchmarkRow);
                        PositionMonitorUtilities.Info(String.Format("{0} now subscribed to {1} as benchmark", Name, benchmarkRow.Symbol));
                    }
                }
            }
            catch (Exception ex)
            {
                PositionMonitorUtilities.Error(String.Format("{0} unable to subscribe to benchmark {1}", Name, symbol), ex);
                return false;
            }
            return true;
        }

        private bool UpdateIndices(HugoDataSet.IndexWeightsRow[] rows)
        {
            try
            {
                List<HugoDataSet.IndicesRow> subscribeList = new List<HugoDataSet.IndicesRow>();

                lock (m_portfolioLock)
                {
                    m_indexWeights = rows;
                    foreach (HugoDataSet.IndexWeightsRow indexWeightsRow in rows)
                    {
                        HugoDataSet.IndicesRow indicesRow = m_indices.Rows.Find(new string[] { indexWeightsRow.AcctName, indexWeightsRow.Symbol }) as HugoDataSet.IndicesRow;
                        if (indicesRow == null)
                        {
                            indicesRow = m_indices.NewIndicesRow();
                            indicesRow.AcctName = indexWeightsRow.AcctName;
                            indicesRow.Symbol = indexWeightsRow.Symbol;
                            m_indices.Rows.Add(indicesRow);
                        }

                        indicesRow.Weight = indexWeightsRow.Weight;
                        indicesRow.IndexFlag = indexWeightsRow.IndexFlag;
 
                        if (IsSubscribed && (indicesRow.SubscriptionStatus != SubscriptionStatus.Subscribed.ToString()))
                        {
                            indicesRow.SubscriptionStatus = SubscriptionStatus.Subscribed.ToString();
                            subscribeList.Add(indicesRow);
                        }
                    }
                }

                if (IsSubscribed)
                {
                    if (subscribeList.Count > 0)
                    {
                        foreach (HugoDataSet.IndicesRow row in subscribeList)
                        {
                            QuoteType quoteType = row.IndexFlag ? QuoteType.Index : QuoteType.Stock;
                            m_messageUtilities2.Subscribe(row.Symbol, quoteType, row);
                            PositionMonitorUtilities.Info(String.Format("{0} now subscribed to {1} as {2}", Name, row.Symbol, quoteType.ToString()));
                        }
                        PositionMonitorUtilities.Info(String.Format("{0} subscribed to {1} new indices", Name, subscribeList.Count));
                    }
                }
            }
            catch (Exception ex)
            {
                PositionMonitorUtilities.Error(String.Format("{0} unable to fill indices table", Name), ex);
                return false;
            }
            return true;
        }

        private string BenchmarkSymbol
        {
            get
            {
                if (!m_accountData.IsBenchmarkNull())
                {
                    if (m_accountData.Benchmark == "SPXT")
                        return "SPX";
                    else
                        return m_accountData.Benchmark;
                }

                return null;
            }
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (m_portfolio != null)
                {
                    m_portfolio.Dispose();
                    m_portfolio = null;
                }
                if (m_indices != null)
                {
                    m_indices.Dispose();
                    m_indices = null;
                }
                if (m_messageUtilities1 != null)
                {
                    m_messageUtilities1.Dispose();
                    m_messageUtilities1 = null;
                }
                if (m_messageUtilities2 != null)
                {
                    m_messageUtilities2.Dispose();
                    m_messageUtilities2 = null;
                }
                PositionMonitorUtilities.Info(Name + " disposed");
            }
        }
        #endregion
    }
}
