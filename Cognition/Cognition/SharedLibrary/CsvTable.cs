using Menelaus.Cognition;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Menelaus.Library.DataStructures.Tables.Csv
{
    public class CsvTable : ITable
    {
        public List<object[]> Data { get; set; } = new List<object[]>();

        public void AppendLine(IEnumerable<object> line)
        {
            this.Data.Add(line.Select(x => x.ToString()).ToArray());
        }

        public void AppendLine(ITableLine line)
        {
            this.Data.Add(line.GetLine());
        }

        public void AppendLines(IEnumerable<IEnumerable<object>> line)
        {
            foreach (var item in line)
            {
                this.AppendLine(item);
            }
        }

        public void AppendLines(IEnumerable<ITableLine> line)
        {
            foreach (var item in line)
            {
                this.AppendLine(item);
            }
        }

        public IEnumerable<object[]> GetData()
        {
            return this.Data;
        }

        public object[] GetLine(int line)
        {
            throw new NotImplementedException();
        }

        public void SaveFile(string filename)
        {
            using (StreamWriter file = new StreamWriter(filename, false, Encoding.Default))
            {
                foreach (var line in this.Data)
                {
                    file.WriteLine(string.Join(Constants.CsvSeparatorString, line));
                }
            }
        }
    }
}
