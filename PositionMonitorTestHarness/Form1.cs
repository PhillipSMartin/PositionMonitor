using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using PositionMonitorLib;
using Gargoyle.Common;
using Gargoyle.Utils.DBAccess;

namespace PositionMonitorTestHarness
{
    public partial class Form1 : Form
    {
        PositionMonitorUtilities m_utilities = new PositionMonitorUtilities();
        AccountPortfolio Account
        {
            get
            {
                return comboBoxAccount.SelectedItem as AccountPortfolio;
            }
        }

        // get Hugo connection
        DBAccess dbAccess = DBAccess.GetDBAccessOfTheCurrentUser("Reconciliation");


        public Form1()
        {
            InitializeComponent();

            m_utilities.OnInfo += m_utilities_OnInfo;
            m_utilities.OnError += m_utilities_OnError;
            m_utilities.OnDebug += m_utilities_OnInfo;
            m_utilities.OnMonitorStopped += m_utilities_OnMonitorStopped;
        }

        #region Logging
        void m_utilities_OnMonitorStopped(object sender, ServiceStoppedEventArgs e)
        {
            OnError(e.Message, e.Exception);
            EnableControls();
        }

        void m_utilities_OnInfo(object sender, LoggingEventArgs e)
        {
            OnInfo(e.Message);
        }
        void m_utilities_OnError(object sender, LoggingEventArgs e)
        {
            OnError(e.Message, e.Exception);
        }
        private void OnInfo(string message)
        {
            Action a = delegate
            {
                listBoxLog.Items.Add(string.Format("{0:T} {1}", DateTime.Now, message));
            };
            if (InvokeRequired)
                BeginInvoke(a);
            else
                a.Invoke();
        }

        private void OnError(string message, Exception exception)
        {
            Action a = delegate
            {
                if (exception == null)
                {
                    listBoxLog.Items.Add(string.Format("{0:T} {1}", DateTime.Now, message));
                }
                else
                {
                    listBoxLog.Items.Add(message + "=>" + exception.Message);
                }
            };
            if (InvokeRequired)
                BeginInvoke(a);
            else
                a.Invoke();
        }
        #endregion

        private void buttonStartAccount_Click(object sender, EventArgs e)
        {
            buttonStartAccount.Enabled = false;
            if (Account != null)
            {
                Account.Start();
                dataGridView1.DataSource = Account.Portfolio;

                buttonStartAccount.Enabled = !Account.IsStarted;
                buttonStopAccount.Enabled = Account.IsStarted;
            }
        }

        void Account_OnRefresh(object sender, EventArgs e)
        {
            if (checkBoxAutoRefresh.Checked)
            {
                Action a = delegate
              {
                  AccountPortfolio account = sender as AccountPortfolio;
                  if (account.Name == Account.Name)
                  {
                      dataGridView1.DataSource = account.Portfolio;
                  }
              };
                if (InvokeRequired)
                    BeginInvoke(a);
                else
                    a.Invoke();
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            m_utilities.Init(dbAccess.GetConnection("Hugo"));
            EnableControls();
         }

        private void buttonRefresh_Click(object sender, EventArgs e)
        {
            if (Account != null)
                dataGridView1.DataSource = Account.Portfolio;
            else
                dataGridView1.DataSource = null;
        }

        private void comboBoxAccount_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (Account != null)
            {
                dataGridView1.DataSource = Account.Portfolio;
            }
            else
            {
                dataGridView1.DataSource = null;
            }

            EnableControls();
        }

        private void buttonStart_Click(object sender, EventArgs e)
        {
            buttonStart.Enabled = false;

            if (m_utilities.StartMonitor())
            {
                comboBoxAccount.DataSource = m_utilities.GetAllAccountPortfolios();
                comboBoxAccount.DisplayMember = "AccountName";

                foreach (AccountPortfolio account in comboBoxAccount.Items)
                {
                    account.OnRefresh += Account_OnRefresh;
                }
            }

            EnableControls();
          }

        private void buttonStop_Click(object sender, EventArgs e)
        {
            buttonStop.Enabled = false;
            m_utilities.StopMonitor();

            foreach (AccountPortfolio account in comboBoxAccount.Items)
            {
                account.OnRefresh -= Account_OnRefresh;
            }
            comboBoxAccount.DataSource = null;

            EnableControls();
        }

        private void buttonStopAccount_Click(object sender, EventArgs e)
        {
            buttonStopAccount.Enabled = false;
            if (Account != null)
            {
                Account.Stop();
            }

            EnableControls();
        }

        private void buttonStopAll_Click(object sender, EventArgs e)
        {
            foreach (AccountPortfolio account in comboBoxAccount.Items)
            {
                account.Stop();
            }

            EnableControls();
        }

        private void buttonStartAll_Click(object sender, EventArgs e)
        {
            foreach (AccountPortfolio account in comboBoxAccount.Items)
            {
                account.Start();
            }

            if (Account != null)
                dataGridView1.DataSource = Account.Portfolio;

            EnableControls();
        }

        private void EnableControls()
        {
            if (Account == null)
            {
                buttonStartAccount.Enabled = false;
                buttonStopAccount.Enabled = false;
                buttonStartAll.Enabled = false;
                buttonStopAll.Enabled = false;
                buttonRefresh.Enabled = false;
            }
            else
            {
                buttonStartAccount.Enabled = !Account.IsStarted;
                buttonStopAccount.Enabled = Account.IsStarted;
                buttonStartAll.Enabled = true;
                buttonStopAll.Enabled = true;
                buttonRefresh.Enabled = true;
            }

            if (m_utilities.IsMonitoring)
            {
                buttonStart.Enabled = false;
                buttonStop.Enabled = true;
            }
            else
            {
                buttonStart.Enabled = m_utilities.IsInitialized;
                buttonStop.Enabled = false;
            }

            comboBoxAccount.Enabled = (comboBoxAccount.Items.Count > 0);
        }
    }
}
