using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Menelaus.Cognition
{
    internal class LcsTreeNaive
    {
        private readonly LcsTreeNode _root;

        internal LcsTreeNaive()
        {
            this._root = new LcsTreeNode(string.Empty, null);
        }

        internal void InsertTemplate(LogTemplate template)
        {
            var tokens = template.Tokens.ToArray();
            InsertTemplate(_root, tokens, 0, template.TemplateId);
        }

        internal void RemoveTemplate(LogTemplate template)
        {
            var node = _root;
            foreach (var item in template.Tokens)
            {
                node = node.Children[item];
            }
            node.TemplateIds.Remove(template.TemplateId);

            if (node.IsEmpty())
            {
                node.Parent.Children.Remove(node.Token);
            }
        }

        internal List<int> FindTemplates(List<string> words)
        {
            IEnumerable<int> result = new List<int>();
            var bfs = new List<Tuple<LcsTreeNode, int>>() { new Tuple<LcsTreeNode, int>(_root, -1) };
            var currentLcs = new List<string>();
            var pointer = 0;
            while (pointer < bfs.Count)
            {
                if (bfs[pointer].Item1.TemplateIds != null)
                {
                    result = result.Concat(bfs[pointer].Item1.TemplateIds);
                }

                if (bfs[pointer].Item1.Children != null)
                {
                    foreach (var node in bfs[pointer].Item1.Children)
                    {
                        var index = words.FindIndex(bfs[pointer].Item2 + 1, x => node.Key == x);
                        if (index > -1)
                        {
                            bfs.Add(new Tuple<LcsTreeNode, int>(node.Value, index));
                        }
                    }
                }

                pointer++;
            }

            ((List<int>)result).Reverse();
            return result.ToList();
        }

        internal void PrintLcsTree()
        {
            var list = new List<LcsTreeNode> { this._root };

            while (list.Any())
            {
                var node = list.First();
                list = list.Concat(node.Children?.Values.ToArray() ?? new LcsTreeNode[] { }).ToList();
                Console.WriteLine(node.Token + ":" + string.Join("-", node.Children?.Values.Select(x => x.Token) ?? new string[] { }));
                list.RemoveAt(0);
            }
        }

        private void InsertTemplate(LcsTreeNode node, IList<string> tokens, int index, int templateId)
        {
            if (index >= tokens.Count)
            {
                if (node.TemplateIds is null)
                {
                    node.TemplateIds = new HashSet<int> { templateId };
                }
                else
                {
                    node.TemplateIds.Add(templateId);
                }
            }
            else
            {
                if (node.Children == null)
                {
                    node.Children = new Dictionary<string, LcsTreeNode>
                    {
                        { tokens[index], new LcsTreeNode(tokens[index], node) },
                    };
                }
                else if (!node.Children.ContainsKey(tokens[index]))
                {
                    node.Children.Add(tokens[index], new LcsTreeNode(tokens[index], node));
                }

                InsertTemplate(node.Children[tokens[index]], tokens, index + 1, templateId);
            }
        }

        private class LcsTreeNode
        {
            internal string Token { get; private set; }

            internal HashSet<int> TemplateIds { get; set; }

            internal LcsTreeNode Parent { get; set; }

            internal Dictionary<string, LcsTreeNode> Children { get; set; }

            internal LcsTreeNode(string token, LcsTreeNode parent)
            {
                this.Token = token;
                this.Parent = parent;
            }

            internal bool IsEmpty()
            {
                return (!TemplateIds?.Any() ?? true) && (!Children?.Any() ?? true);
            }
        }
    }
}
