using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using Gargoyle.Common;
using Gargoyle.Utils.DBAccess;
using log4net;
using PositionMonitorServiceLib;

namespace PositionMonitorHost
{
    public class Host : IDisposable
    {
        private ILog m_logger = LogManager.GetLogger(typeof(Program));
        private System.Data.SqlClient.SqlConnection m_hugoConnection;
        private ServiceHost m_monitor;

        private string m_lastErrorMessage;

        public void Run()
        {
            // initialize logging
            log4net.Config.XmlConfigurator.Configure();
            PositionMonitorUtilities.OnInfo += Utilities_OnInfo;
            PositionMonitorUtilities.OnError += Utilities_OnError;
            PositionMonitorUtilities.OnDebug += Utilities_OnInfo;
            PositionMonitorUtilities.OnMonitorStopped += Utilities_OnMonitorStopped;

            if (!GetDatabaseConnections())
            {
                Console.WriteLine("Failed to connect to Hugo");
                return;
            }

            if (!PositionMonitorUtilities.Init(m_hugoConnection))
            {
                Console.WriteLine("Failed to initialize PositionMonitorUtilities");
                return;
            }
            Console.WriteLine("PositionMonitorUtilities initiallized");

            if (!PositionMonitorUtilities.StartMonitor())
            {
                Console.WriteLine("Failed to start monitor");
                return;
            }
            Console.WriteLine("Monitor started");

            using (m_monitor = new ServiceHost(typeof(PositionMonitor)))
            {
                m_monitor.Open();
                Console.WriteLine("Host is running - press any key to stop host");

                Console.ReadKey();
                m_monitor.Close();
                Console.WriteLine("Host is stopped");

                PositionMonitorUtilities.StopMonitor();
                Console.WriteLine("Monitor stopped");
            }
            m_monitor = null;
            
        }

        #region Logging
        // events from LoggingUtilities and TaskUtilities
        private void Utilities_OnInfo(object sender, LoggingEventArgs eventArgs)
        {
            OnInfo(eventArgs.Message);
        }

        private void Utilities_OnError(object sender, LoggingEventArgs eventArgs)
        {
            OnError(eventArgs.Message, eventArgs.Exception);
        }

        private void Utilities_OnMonitorStopped(object sender, ServiceStoppedEventArgs eventArgs)
        {
            OnError(eventArgs.Message, eventArgs.Exception);
            OnInfo("Monitor stopped");
            if (m_monitor != null)
            {
                m_monitor.Close();
            }

        }

        // helper methods to write to log
        private void OnInfo(string msg, bool updateLastErrorMsg = false)
        {
            if (updateLastErrorMsg)
                m_lastErrorMessage = msg;
            if (m_logger != null)
            {
                lock (m_logger)
                {
                    m_logger.Info(msg);
                }
            }

        }
        private void OnError(string msg, Exception e, bool updateLastErrorMsg = false)
        {
            string fullMsg;
            if (e != null)
                fullMsg = msg + "->" + e.Message;
            else
                fullMsg = msg;
            if (updateLastErrorMsg)
                m_lastErrorMessage = fullMsg;

            if (m_logger != null)
            {
                lock (m_logger)
                {
                    m_logger.Error(msg, e);
                }
            }
        }
        #endregion

        #region IDisposable Members
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected void Dispose(bool disposing)
        {

 //           if (m_bTaskStarted)
  //              m_bTaskStarted = !EndTask(m_parms.TaskName, !m_bTaskFailed);

            if (disposing)
            {
            }
        }
        #endregion

        private bool GetDatabaseConnections()
        {
            DBAccess dbAccess = DBAccess.GetDBAccessOfTheCurrentUser(Properties.Settings.Default.ProgramName);
            m_hugoConnection = dbAccess.GetConnection("Hugo");
            if (m_hugoConnection == null)
            {
                OnInfo("Unable to connect to Hugo", true);
                return false;
            }
            return true;
        }
    }
}
