using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Menelaus.Cognition.DataType
{
    public class Log
    {
        internal Log(int lineId, string log)
        {
            LogId = lineId;
            RawLog = log;
        }

        internal int LogId { get; private set; }

        public string RawLog { get; private set; }

        public Dictionary<string, string> FormatLog { get; internal set; }

        public string[] Parameters { get; internal set; }

        public string Template { get; internal set; }

        internal string[] Words { get; set; }

        public override string ToString() => FormatLog?["Content"] ?? RawLog;
    }
}
