using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PositionMonitorServiceLib
{
    interface IHugoTableAdapter : IDisposable
    {
        void SetAllCommandTimeouts(int timeOut);
        void SetAllConnections(SqlConnection sqlConnection);
        void LogCommand(string commandText);
        int GetReturnCode(string commandText);
    }
}
