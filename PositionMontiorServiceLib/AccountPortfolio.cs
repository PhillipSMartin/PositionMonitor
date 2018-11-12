using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gargoyle.Common;
using Gargoyle.Messaging.Common;
using GargoyleMessageLib;

namespace PositionMonitorServiceLib
{
    public class AccountPortfolio : IDisposable
    {
        private GargoyleMessageUtilities m_messageUtilities = new GargoyleMessageUtilities();

        private HugoDataSet.PortfolioDataTable m_portfolio = new HugoDataSet.PortfolioDataTable();
        private object m_portfolioLock = new object();


        // event fired when tables are refreshed
        private event EventHandler m_refreshEventHandler;
        public event EventHandler OnRefresh
        {
            add { m_refreshEventHandler += value; }
            remove { m_refreshEventHandler -= value; }
        }

        public AccountPortfolio(string acctName)
        {
            AccountName = acctName;
            IsStarted = false;

            m_messageUtilities.OnInfo += m_messageUtilities_OnInfo;
            m_messageUtilities.OnDebug += m_messageUtilities_OnDebug;
            m_messageUtilities.OnError += m_messageUtilities_OnError;
            m_messageUtilities.OnQuote += m_messageUtilities_OnQuote;
            m_messageUtilities.OnSubscriberStopped += m_messageUtilities_OnReaderStopped;
        }

        #region Public Properties
        public string AccountName { get; private set; }
        public string Name { get { return String.Format("AccountPortfolio for {0}", AccountName); } }
        public bool IsStarted { get; private set; }
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

        public HugoDataSet.TradesDataTable Trades
        {
            get
            {
                HugoDataSet.TradesDataTable trades = new HugoDataSet.TradesDataTable();
                foreach (HugoDataSet.TradesRow row in PositionMonitorUtilities.GetTradesForAccount(AccountName))
                {
                    trades.AddTradesRow(row);
                }
                return trades;
            }
        }
        public HugoDataSet.AccountDataRow AccountData
        {
            get
            {
                return PositionMonitorUtilities.GetDataForAccount(AccountName);
            }
        }
        #endregion

        #region Public Methods
        public bool Start()
        {
            try
            {
                if (!IsStarted)
                {
                    if (!PositionMonitorUtilities.IsMonitoring)
                    {
                        PositionMonitorUtilities.Info(String.Format("Cannot start {0} before starting monitor", Name));
                    }
                    else
                    {
                        Update();
                        PositionMonitorUtilities.OnRefresh += utilities_OnRefresh;
                        IsStarted = true;

                        StartSubscriber();
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
                    PositionMonitorUtilities.OnRefresh -= utilities_OnRefresh;

                    StopSubscriber();
                }
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
                if (!IsSubscribed)
                {
                    int rowCount = 0;
                    if (IsSubscribed = m_messageUtilities.StartSubscriber(Properties.Settings.Default.MarketDataHost, Properties.Settings.Default.MarketDataPort))
                    {
                        lock (m_portfolioLock)
                        {
                            foreach (HugoDataSet.PortfolioRow row in m_portfolio.Rows)
                            {
                                if (!row.IsCurrent_PositionNull())
                                {
                                    if (row.Current_Position != 0)
                                    {
                                        row.SubscriptionStatus = SubscriptionStatus.Subscribed.ToString();
                                        m_messageUtilities.Subscribe(row.Symbol, row.QuoteType, row);
                                        rowCount++;
                                    }
                                }
                            }
                        }
                    }
                    PositionMonitorUtilities.Info(String.Format("{0} subscribed to {1} symbols", Name, rowCount));
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
                if (IsSubscribed)
                {
                    IsSubscribed = false;
                    m_messageUtilities.StopSubscriber();
                }
            }
            catch (Exception ex)
            {
                PositionMonitorUtilities.Error(Name + " enountered an error stopping subscriber", ex);
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
                    HugoDataSet.PortfolioRow row = e.ClientObject as HugoDataSet.PortfolioRow;

                    lock (m_portfolioLock)
                    {
                        bool bUpdated = false;
                        if (e.Quote.SubscriptionStatus != SubscriptionStatus.Unchanged)
                        {
                            row.SubscriptionStatus = e.Quote.SubscriptionStatus.ToString();
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

                            if (e.Quote.HasClose)
                            {
                                row.ClosingPrice = e.Quote.Close;
                                bUpdated = true;
                            }

                            if (e.Quote.OpenStatus == OpenStatus.Closed)
                            {
                                row.Closed = true;
                                bUpdated = true;
                            }

                            if (e.Quote.OpenStatus == OpenStatus.Open)
                            {
                                row.Closed = false;
                                bUpdated = true;
                            }

                            if (bUpdated)
                            {
                                row.UpdateTime = DateTime.Now - DateTime.Today;
                            }
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
            IsSubscribed = false;
            if (e.Exception == null)
                PositionMonitorUtilities.Info(e.Message);
            else
                PositionMonitorUtilities.Error(e.Message, e.Exception);

            lock (m_portfolioLock)
            {
                foreach (HugoDataSet.PortfolioRow row in m_portfolio.Rows)
                {
                    row.SubscriptionStatus = SubscriptionStatus.Unsubscribed.ToString();
                    row.UpdateTime = DateTime.Now - DateTime.Today;
                }
            }

            if (m_refreshEventHandler != null)
                m_refreshEventHandler(this, new EventArgs());
        }

        private void utilities_OnRefresh(object sender, EventArgs e)
        {
            if (IsStarted)
                Update();
        }
        #endregion

        private void Update()
        {
            UpdatePortfolio(PositionMonitorUtilities.GetCurrentPositionsForAccount(AccountName));

#if DEBUG
            PositionMonitorUtilities.Debug(String.Format("{0} refreshed portfolio: {1} positions", Name, m_portfolio.Count));
#endif
            if (m_refreshEventHandler != null)
                m_refreshEventHandler(this, new EventArgs());
        }

        private bool UpdatePortfolio(HugoDataSet.CurrentPositionsRow[] rows)
        {
            try
            {
                List<HugoDataSet.PortfolioRow> m_subscribeList = new List<HugoDataSet.PortfolioRow>();
                List<string> m_unsubscribeList = new List<string>();

                lock (m_portfolioLock)
                {
                    foreach (HugoDataSet.CurrentPositionsRow positionRow in rows)
                    {
                        HugoDataSet.PortfolioRow portfolioRow = m_portfolio.Rows.Find(new string[] { positionRow.AcctName, positionRow.Symbol }) as HugoDataSet.PortfolioRow;
                        if (portfolioRow == null)
                        {
                            portfolioRow = m_portfolio.NewPortfolioRow();
                            portfolioRow.AcctName = positionRow.AcctName;
                            portfolioRow.Symbol = positionRow.Symbol;

                            if (!positionRow.IsExpirationDateNull())
                                portfolioRow.ExpirationDate = positionRow.ExpirationDate;
                            if (!positionRow.IsStrikePriceNull())
                                portfolioRow.StrikePrice = positionRow.StrikePrice;
                            if (!positionRow.IsOptionTypeNull())
                                portfolioRow.OptionType = positionRow.OptionType;

                            portfolioRow.IsStock = positionRow.IsStock;
                            portfolioRow.IsOption = positionRow.IsOption;
                            portfolioRow.IsFuture = positionRow.IsFuture;
                            m_portfolio.Rows.Add(portfolioRow);
                        }
                        portfolioRow.SOD_Position = positionRow.SOD_Position;
                        portfolioRow.SOD_Price = positionRow.SOD_Price;
                        portfolioRow.SOD_Market_Value = positionRow.SOD_Market_Value;
                        portfolioRow.Change_in_Position = positionRow.Change_in_Position;
                        portfolioRow.Change_in_Cost = positionRow.Change_in_Cost;
                        portfolioRow.Current_Position = positionRow.Current_Position;
                        portfolioRow.Current_Cost = positionRow.Current_Cost;

                        if (IsSubscribed && (portfolioRow.SubscriptionStatus != SubscriptionStatus.Subscribed.ToString()) && !portfolioRow.IsCurrent_PositionNull())
                        {
                            if (portfolioRow.Current_Position != 0)
                            {
                                portfolioRow.SubscriptionStatus = SubscriptionStatus.Subscribed.ToString();
                                m_subscribeList.Add(portfolioRow);
                            }
                        }
                    }

                    foreach (HugoDataSet.PortfolioRow portfolioRow in m_portfolio.Rows)
                    {
                        if ((portfolioRow.Current_Position == 0) && (portfolioRow.SubscriptionStatus == SubscriptionStatus.Subscribed.ToString()))
                        {
                            portfolioRow.SubscriptionStatus = SubscriptionStatus.Unsubscribed.ToString();
                            m_unsubscribeList.Add(portfolioRow.Symbol);
                        }
                    }
                }

                if (IsSubscribed)
                {
                    if (m_subscribeList.Count > 0)
                    {
                        foreach (HugoDataSet.PortfolioRow row in m_subscribeList)
                        {
                            m_messageUtilities.Subscribe(row.Symbol, row.QuoteType, row);
                        }
                        PositionMonitorUtilities.Info(String.Format("{0} subscribed to {1} new symbols", Name, m_subscribeList.Count));
                    }
                    if (m_unsubscribeList.Count > 0)
                    {
                        foreach (string ticker in m_unsubscribeList)
                        {
                            m_messageUtilities.Unsubscribe(ticker);
                        }
                        PositionMonitorUtilities.Info(String.Format("{0} unsubscribed from {1} symbols", Name, m_unsubscribeList.Count));
                    }
                }
            }
            catch (Exception ex)
            {
                PositionMonitorUtilities.Error("Unable to fill current position table " + Name, ex);
                return false;
            }
            return true;
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
                if (m_messageUtilities != null)
                {
                    m_messageUtilities.Dispose();
                    m_messageUtilities = null;
                }
                PositionMonitorUtilities.Info(Name + " disposed");
            }
        }
        #endregion
    }
}
