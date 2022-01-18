using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Menelaus.Library.DataStructures.Tables
{
    public interface ITableLine
    {
        object[] GetLine();

        void SetLine(object[] line);
    }
}
