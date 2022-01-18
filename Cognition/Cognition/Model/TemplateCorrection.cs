using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Menelaus.Cognition.Model
{
    internal static class TemplateCorrection
    {
        internal static bool TryMerge(string[] t1, string[] t2, double lcsThreshold, bool lcsTokenOnly, out LogTemplate tout)
        {
            tout = null!;
            if (t1.Length != t2.Length)
            {
                return false;
            }

            //var t1t = t1.Where(x => x != Constants.VariablePlaceholder).ToArray();
            //var t2t = t2.Where(x => x != Constants.VariablePlaceholder).ToArray();

            //var lcs = QuickAlgorithms.LongestCommonSequence(t1t, t2t);


            var matrix = new int[t1.Length + 1, t2.Length + 1];
            for (int i = 0; i < t1.Length; i++)
            {
                for (int j = 0; j < t2.Length; j++)
                {
                    if (t1[i] == t2[j])
                    {
                        matrix[i + 1, j + 1] = matrix[i, j] + 1;
                    }
                    else
                    {
                        matrix[i + 1, j + 1] = Math.Max(matrix[i + 1, j], matrix[i, j + 1]);
                    }
                }
            }

            var lcs = LongestCommonSequenceWithVaPInReverse(t1, t2, matrix);

            if (lcsTokenOnly)
            {
                if (2d * lcs.Count / (t1.Count(x => x != Constants.VariablePlaceholder) + t2.Count(x => x != Constants.VariablePlaceholder)) < lcsThreshold)
                {
                    return false;
                }
            }
            else
            {
                if ((double)matrix[t1.Length, t2.Length] / t1.Length < lcsThreshold)
                {
                    return false;
                }
            }

            //lcs.Reverse();

            var result = new List<string>();
            int p1 = t1.Length, p2 = t2.Length;
            while (p1 > 0 && p2 > 0)
            {
                if (t1[p1 - 1] == t2[p2 - 1])
                {
                    if (t1[p1 - 1] == lcs.FirstOrDefault())
                    {
                        lcs.RemoveAt(0);
                    }

                    result.Add(t1[p1 - 1]);
                    p1--;
                    p2--;
                }
                else if (t1[p1 - 1] == lcs.FirstOrDefault())
                {
                    result.Add(Constants.VariablePlaceholder);
                    p2--;
                }
                else if (t2[p2 - 1] == lcs.FirstOrDefault())
                {
                    result.Add(Constants.VariablePlaceholder);
                    p1--;
                }
                else
                {
                    result.Add(Constants.VariablePlaceholder);
                    if ((matrix[p1 - 1, p2] == matrix[p1, p2]) && (matrix[p1, p2 - 1] == matrix[p1, p2]))
                    {
                        p1--;
                        p2--;
                    }
                    else if (matrix[p1 - 1, p2] == matrix[p1, p2])
                    {
                        p1--;
                    }
                    else
                    {
                        p2--;
                    }
                }
            }

            for (int i = 0; i < p1 + p2; i++)
            {
                result.Add(Constants.VariablePlaceholder);
            }

            result.Reverse();
            tout = new LogTemplate(result);
            return true;
        }

        internal static bool TryAbsorb(LogTemplate t1, LogTemplate t2, out LogTemplate tout)
        {
            tout = null!;
            if (t1.Length == t2.Length)
            {
                return false;
            }

            int p1 = 0, p2 = 0;
            bool t1l = false, t2l = false;
            while (p1 < t1.Length || p2 < t2.Length)
            {
                if (p1 < t1.Length && p2 < t2.Length && t1[p1] == t2[p2])
                {
                    p1++;
                    p2++;
                }
                else if (p1 < t1.Length && t1[p1] == Constants.VariablePlaceholder)
                {
                    p1++;
                    if (t2l)
                    {
                        return false;
                    }
                    t1l = true;
                }
                else if (p2 < t2.Length && t2[p2] == Constants.VariablePlaceholder)
                {
                    p2++;
                    if (t1l)
                    {
                        return false;
                    }
                    t2l = true;
                }
                else
                {
                    return false;
                }
            }

            tout = t1.Length > t2.Length ? t1 : t2;

            return true;
        }

        private static List<string> LongestCommonSequenceWithVaPInReverse(string[] t1, string[] t2, int[,] matrix)
        {
            var result = new List<string>();
            int p1 = t1.Length, p2 = t2.Length;
            while (p1 > 0 && p2 > 0)
            {
                if (t1[p1 - 1] == Constants.VariablePlaceholder)
                {
                    p1--;
                }
                else if (t2[p2 - 1] == Constants.VariablePlaceholder)
                {
                    p2--;
                }
                else if (t1[p1 - 1] == t2[p2 - 1])
                {
                    result.Add(t1[p1 - 1]);
                    p1--;
                    p2--;
                }
                else if (matrix[p1, p2] == matrix[p1 - 1, p2])
                {
                    p1--;
                }
                else if (matrix[p1, p2] == matrix[p1, p2 - 1])
                {
                    p2--;
                }
                else
                {
                    throw new InvalidOperationException("Algorithm Error");
                }
            }

            //result.Reverse();
            return result;
        }
    }
}
