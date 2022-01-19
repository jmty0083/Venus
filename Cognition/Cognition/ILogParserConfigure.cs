using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Cognition
{
    public interface ILogParserConfigure
    {
        double LcsThreshold { get; set; }

        bool LcsTokensOnly { get; set; }

        string LogFormat { get; set; }

        IList<string> SubtitutionRegex { get; set; }

        IList<string> Ignorance { get; set; }
    }
}
