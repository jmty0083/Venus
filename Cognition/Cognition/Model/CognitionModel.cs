﻿using Menelaus.Cognition.DataType;
using System.Diagnostics;

namespace Menelaus.Cognition.Model
{
    internal class CognitionModel
    {
        private double LcsThreshold { get; set; }

        private bool LcsTokensOnly { get; set; }

        // Template Dictionary <TemplateId, Template>
        private Dictionary<int, LogTemplate> Templates { get; set; }

        // Index I: length <length, TemplateId>
        private Dictionary<int, HashSet<int>> LengthIndexer { get; set; }

        // Index II: tokens <tokenHash, TemplateId>
        private Dictionary<string, HashSet<int>> TokenIndexer { get; set; }

        // Index III: LCS prefix tree
        private TokenTree PrefixTree { get; set; }

        // Unmatched log pool <logLength, LogList>
        private Dictionary<int, List<Log>> UnmatchedLogPool { get; set; }

        // LogId to TemplateId table
        private Dictionary<int, int> LogTemplateLookupTable { get; set; }

        internal CognitionModel(double threshold, bool useTokenOnly)
        {
            this.LcsThreshold = threshold;
            this.LcsTokensOnly = useTokenOnly;
            this.Templates = new Dictionary<int, LogTemplate>();
            this.LengthIndexer = new Dictionary<int, HashSet<int>>();
            this.TokenIndexer = new Dictionary<string, HashSet<int>>();
            this.PrefixTree = new TokenTree();
            this.UnmatchedLogPool = new Dictionary<int, List<Log>>();
            this.LogTemplateLookupTable = new Dictionary<int, int>();

        }

        internal LogTemplate Lookup(int logId)
        {
            if (this.LogTemplateLookupTable.ContainsKey(logId))
            {
                return this.Templates[this.LogTemplateLookupTable[logId]];
            }
            else
            {
                return new LogTemplate(this.UnmatchedLogPool.Values.SelectMany(x => x).First(x => x.LogId == logId).Words);
            }
        }

        internal void ParseLog(Log log)
        {
            if (this.TryMatchTemplate(log))
            {
                return;
            }

            if (this.TryMergeLogData(log, out LogTemplate template))
            {
                while (this.TryMergeTemplate(template, out LogTemplate mergedTemplate))
                {
                    template = mergedTemplate;
                }

                while (this.TryAbsorbTemplate(template, out LogTemplate absorbedTemplate))
                {
                    template = absorbedTemplate;
                }

                this.BuildIndex(template);
            }
        }

        internal List<LogTemplate> GetAllTemplates()
        {
            return this.Templates.Values.ToList();
        }


        private bool TryMatchTemplate(Log log)
        {
            var list = this.PrefixTree.FindTemplates(log.Words.ToList());
            foreach (var templateId in list)
            {
                if (this.Templates[templateId].TryMatch(log))
                {
                    this.Templates[templateId].LogIds.Add(log.LogId);
                    this.LogTemplateLookupTable.Add(log.LogId, templateId);

                    return true;
                }
            }
            return false;
        }

        private bool TryMergeLogData(Log log, out LogTemplate result)
        {
            result = null;
            if (this.UnmatchedLogPool.ContainsKey(log.Words.Length))
            {
                var pool = this.UnmatchedLogPool[log.Words.Length];
                foreach (var unmatchedLog in pool)
                {
                    if (TemplateCorrection.TryMerge(log.Words, unmatchedLog.Words, this.LcsThreshold, this.LcsTokensOnly, out result))
                    {
                        pool.Remove(unmatchedLog);
                        result.LogIds = new List<int> { log.LogId, unmatchedLog.LogId };

                        return true;
                    }
                }
            }
            else
            {
                this.UnmatchedLogPool.Add(log.Words.Length, new List<Log>());
            }

            this.UnmatchedLogPool[log.Words.Length].Add(log);
            return false;
        }

        private bool TryMergeTemplate(LogTemplate template, out LogTemplate result)
        {
            result = null;
            if (this.LengthIndexer.ContainsKey(template.Length))
            {
                var list = this.LengthIndexer[template.Length];
                foreach (var tryTemplateId in list)
                {
                    if (TemplateCorrection.TryMerge(template.Sequence, this.Templates[tryTemplateId].Sequence, this.LcsThreshold, this.LcsTokensOnly, out result))
                    {
                        result.LogIds = template.LogIds
                            .Concat(this.Templates[tryTemplateId].LogIds)
                            .ToList();

                        //this.Templates[templateId].EmergedId = result.TemplateId;
                        this.RemoveTemplate(tryTemplateId);

                        return true;
                    }
                }
            }
            return false;
        }

        private bool TryAbsorbTemplate(LogTemplate template, out LogTemplate result)
        {
            result = null;
            if (this.TokenIndexer.ContainsKey(template.TokensHash))
            {
                var list = this.TokenIndexer[template.TokensHash];
                foreach (var tryTemplateId in list)
                {
                    if (TemplateCorrection.TryAbsorb(template, this.Templates[tryTemplateId], out result))
                    {
                        result.LogIds = template.LogIds
                            .Concat(this.Templates[tryTemplateId].LogIds)
                            .ToList();

                        this.RemoveTemplate(tryTemplateId);
                        return true;
                    }
                }
            }

            return false;
        }

        private void BuildIndex(LogTemplate template)
        {
            this.Templates.Add(template.TemplateId, template);

            if (this.LengthIndexer.ContainsKey(template.Length))
            {
                this.LengthIndexer[template.Length].Add(template.TemplateId);
            }
            else
            {
                this.LengthIndexer.Add(template.Length, new HashSet<int> { template.TemplateId });
            }

            if (this.TokenIndexer.ContainsKey(template.TokensHash))
            {
                this.TokenIndexer[template.TokensHash].Add(template.TemplateId);
            }
            else
            {
                this.TokenIndexer.Add(template.TokensHash, new HashSet<int> { template.TemplateId });
            }

            foreach (var logId in template.LogIds)
            {
                if (this.LogTemplateLookupTable.ContainsKey(logId))
                {
                    this.LogTemplateLookupTable[logId] = template.TemplateId;
                }
                else
                {
                    this.LogTemplateLookupTable.Add(logId, template.TemplateId);
                }
            }

            this.PrefixTree.InsertTemplate(template);
        }

        private void RemoveTemplate(int templateId)
        {
            var template = this.Templates[templateId];
            this.LengthIndexer[template.Length].Remove(templateId);
            this.TokenIndexer[template.TokensHash].Remove(templateId);
            this.PrefixTree.RemoveTemplate(template);
            this.Templates.Remove(templateId);
        }
    }
}
