using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PositionMonitorLib
{
    internal class RefreshEventArgs
    {
        public RefreshEventArgs(bool updated = false)
        {
            TradesUpdated = updated;
            AccountsUpdated = updated;
            PositionsUpdated = updated;
            IndexWeightsUpdated = updated;
            TradingScheduleUpdated = updated;
            SnapshotIdsUpdated = updated;
        }

        public bool TradesUpdated { get; set; }
        public bool AccountsUpdated { get; set; }
        public bool PositionsUpdated { get; set; }
        public bool IndexWeightsUpdated { get; set; }
        public bool TradingScheduleUpdated { get; set; }
        public bool SnapshotIdsUpdated { get; set; }

        public bool TablesUpdated
        {
            get
            {
                return TradesUpdated || AccountsUpdated || PositionsUpdated || IndexWeightsUpdated || TradingScheduleUpdated || SnapshotIdsUpdated;
            }
        }
    }
}
