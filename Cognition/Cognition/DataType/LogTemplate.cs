using Menelaus.Cognition.DataType;

namespace Menelaus.Cognition
{
    internal class LogTemplate
    {
        internal string[] Sequence { get; private set; }

        internal int TemplateId { get; private set; }

        internal string[] Tokens { get; private set; }

        internal int Length => Sequence.Length;

        internal string TokensHash { get; private set; }

        internal List<int> LogIds { get; set; }

        private string SequenceString { get; set; }

        public LogTemplate(IList<string> sequence)
        {
            Sequence = sequence.ToArray();
            TemplateId = GetTemplateHashCode(sequence);
            Tokens = sequence.Where(x => x != Constants.VariablePlaceholder).ToArray();
            TokensHash = GetLcsTokenHash(Tokens);
            SequenceString = string.Join(" ", Sequence);
        }

        public override string ToString() => SequenceString;

        internal string this[int index] => Sequence[index];

        internal bool TryMatch(Log log)
        {
            if (log.Words.Length > Sequence.Length)
            {
                return false;
            }

            var matrix = new int[Sequence.Length + 1, log.Words.Length + 1];
            for (int i = 0; i < Sequence.Length; i++)
            {
                for (int j = 0; j < log.Words.Length; j++)
                {
                    if (Sequence[i] == log.Words[j] || Sequence[i] == Constants.VariablePlaceholder)
                    {
                        matrix[i + 1, j + 1] = matrix[i, j] + 1;
                    }
                    else
                    {
                        matrix[i + 1, j + 1] = Math.Max(matrix[i + 1, j], matrix[i, j + 1]);
                    }
                }
            }

            return matrix[Sequence.Length, log.Words.Length] >= log.Words.Length;
        }

        public string[] Match(Log log)
        {
            if (log.Words.Length > Sequence.Length)
            {
                throw new NotSupportedException("Template not match");
            }

            var clone = this.Tokens.ToList();
            return log.Words.Where(x => !clone.Remove(x)).ToArray();
        }

        private static int GetTemplateHashCode(IList<string> sequence)
        {
            var hash = 0x2217;
            foreach (var item in sequence)
            {
                hash += item.GetHashCode();
                hash *= 31;
            }
            return hash;
        }

        private static string GetLcsTokenHash(IList<string> tokens)
        {
            return string.Join(Constants.LcsSeparator, tokens);
        }

    }
}
