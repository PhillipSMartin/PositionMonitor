using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PositionMonitorLib
{
    public interface ISnapshotProvider
    {
        DateTime EndOfDay { get; }
        void TakeSnapshot(string acctName, string snapshotType);
    }
}
