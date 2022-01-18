using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Menelaus.Cognition.DataType
{
    internal class TokenTree
    {
        private readonly TokenTreeNode _root;

        internal TokenTree()
        {
            this._root = new TokenTreeNode(string.Empty, null);
        }

        internal void InsertTemplate(LogTemplate template)
        {
            var tokens = template.Tokens.ToArray();
            InsertTemplate(_root, tokens, 0, template.TemplateId, template.Length);
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
            var bfs = new List<Tuple<TokenTreeNode, int>>() { new Tuple<TokenTreeNode, int>(_root, -1) };
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
                        if (node.Value.MaxLength >= words.Count)
                        {
                            var index = words.FindIndex(bfs[pointer].Item2 + 1, x => node.Key == x);
                            if (index > -1)
                            {
                                bfs.Add(new Tuple<TokenTreeNode, int>(node.Value, index));
                            }
                        }
                    }
                }

                pointer++;
            }

            result.Reverse();
            return result.ToList();
        }

        internal void PrintLcsTree()
        {
            var list = new List<TokenTreeNode> { this._root };

            while (list.Any())
            {
                var node = list.First();
                list = list.Concat(node.Children?.Values.ToArray() ?? new TokenTreeNode[] { }).ToList();
                Console.WriteLine(node.Token + ":" + string.Join("-", node.Children?.Values.Select(x => x.Token) ?? new string[] { }));
                list.RemoveAt(0);
            }
        }

        private void InsertTemplate(TokenTreeNode node, IList<string> tokens, int index, int templateId, int length)
        {
            node.MaxLength = Math.Max(node.MaxLength, length);

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
                    node.Children = new Dictionary<string, TokenTreeNode>
                    {
                        { tokens[index], new TokenTreeNode(tokens[index], node) },
                    };
                }
                else if (!node.Children.ContainsKey(tokens[index]))
                {
                    node.Children.Add(tokens[index], new TokenTreeNode(tokens[index], node));
                }

                InsertTemplate(node.Children[tokens[index]], tokens, index + 1, templateId, length);
            }
        }

        private class TokenTreeNode
        {
            internal string Token { get; private set; }

            internal int MaxLength { get; set; }

            internal HashSet<int> TemplateIds { get; set; }

            internal TokenTreeNode Parent { get; set; }

            internal Dictionary<string, TokenTreeNode> Children { get; set; }

            internal TokenTreeNode(string token, TokenTreeNode parent)
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
