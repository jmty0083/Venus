﻿using Cognition;
using Menelaus.Cognition.DataType;
using Menelaus.Cognition.Model;
using Menelaus.Library.DataStructures.Tables.Csv;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Menelaus.Cognition
{
    public class LogParser
    {
        private Regex LogFormatRegex { get; set; }

        private CognitionModel Model { get; set; }

        private Regex[] ContentSubtitues { get; set; }

        private string[] Ignorance { get; set; }

        private HashSet<string> LogFormatHeader { get; set; }

        public LogParser(ILogParserConfigure logParserConfigure)
        {
            this.Model = new CognitionModel(logParserConfigure.LcsThreshold, logParserConfigure.LcsTokensOnly);

            var list = Regex.Split(logParserConfigure.LogFormat, @"(<[^<>]+>)");
            this.LogFormatRegex = new Regex("^" + string.Join("", list.Select((reg, k) => k % 2 == 0 ? Regex.Replace(reg, " +", @"\s+") : string.Format(@"(?<{0}>.*?)", reg.Trim('<', '>')))) + "$");
            this.LogFormatHeader = list.Where((header, i) => i % 2 != 0).Select(x => x.Trim('<', '>')).ToHashSet();

            this.ContentSubtitues = logParserConfigure.SubtitutionRegex.Select(x => new Regex(x)).ToArray();
            this.Ignorance = logParserConfigure.Ignorance.ToArray();
        }

        public void ParseFile(string inputpath)
        {
            int i = 0;
            foreach (var line in File.ReadLines(inputpath))
            {
                this.ParseLineNoLookup(line, ++i);
            }
            GC.Collect();
        }

        public void ParseFile(string inputpath, string outputpath)
        {
            var logdata = File.ReadLines(inputpath)
                .Select((x, i) => this.ParseLineNoLookup(x, i + 1))
                .Where(x => !(x is null))
                .ToList();

            this.OutputToFile(logdata, outputpath);

            GC.Collect();
        }

        public Log Lookup(Log log)
        {
            var t = this.Model.Lookup(log.LogId);
            log.Template = t.ToString();
            log.Parameters = t.Match(log);
            return log;
        }

        public bool TryLookup(ref Log log)
        {
            var t = this.Model.Lookup(log.LogId);
            log.Template = t.ToString();
            try
            {
                log.Parameters = t.Match(log);
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        private Log ParseLineNoLookup(string line, int lineNumber)
        {
            //Console.WriteLine("Parsing: {0}", line);

            if (this.Preprocess(line, lineNumber, out Log log))
            {
                this.Model.ParseLog(log);
                return log;
            }
            else
            {
                return null;
            }
        }

        private void OutputToFile(IList<Log> logdata, string filepath)
        {
            var csv = new CsvTable();
            var header = new List<string>();
            header.Add("LineId");
            header = header.Concat(this.LogFormatHeader).ToList();
            header.Remove("Content");
            header.Add("Content");
            header.Add("EventId");
            header.Add("EventTemplate");
            header.Add("ParameterList");
            csv.AppendLine(header);

            foreach (var log in logdata)
            {
                var line = new List<object>();
                var template = this.Model.Lookup(log.LogId);
                //template.ApplyToLogData(log);

                line.Add(log.LogId);
                line = line.Concat(log.FormatLog.Where(x => x.Key != "Content").Select(x => "\"" + x.Value + "\"").ToArray()).ToList();
                line.Add("\"" + log.FormatLog["Content"].Replace("\"", "\'") + "\"");
                line.Add(template.TemplateId);
                line.Add("\"" + template.ToString().Replace("\"", "\'") + "\"");
                line.Add("\"" + string.Join(",", template.Match(log)).Replace("\"", "\'") + "\"");
                csv.AppendLine(line);
            }

            csv.SaveFile(filepath);
        }

        private bool Preprocess(string line, int lineNumber, out Log log)
        {
            var match = this.LogFormatRegex.Match(line);
            if (match.Success)
            {
                var info = this.LogFormatHeader.ToDictionary(x => x, x => match.Groups[x].Value);
                var content = info["Content"];
                // info.Remove("Content");

                foreach (var ignore in this.Ignorance)
                {
                    content = content.Replace(ignore, " ");
                }

                foreach (var regex in this.ContentSubtitues)
                {
                    content = regex.Replace(content, Constants.VariablePlaceholder);
                }

                log = new Log(lineNumber, line)
                {
                    FormatLog = info,
                    Words = content.Split(' ').Where(x => x.Any()).ToArray(),
                };

                return true;
            }
            else
            {
                //throw new InvalidOperationException(string.Format("Preprocess via regex {1} cannot match line {0}", line, this.LogFormatRegex));
                log = null;
                return false;
            }
        }
    }
}
