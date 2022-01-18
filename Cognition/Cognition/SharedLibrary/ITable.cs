using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Menelaus.Library.DataStructures.Tables
{
    public interface ITable
    {
        IEnumerable<object[]> GetData();

        object[] GetLine(int line);

        void AppendLine(IEnumerable<object> line);

        void AppendLine(ITableLine line);

        void AppendLines(IEnumerable<IEnumerable<object>> lines);

        void AppendLines(IEnumerable<ITableLine> lines);

        void SaveFile(string filename);
    }
}
