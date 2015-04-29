using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shield.Communication
{
    public interface IDestination
    {
        string PREFIX { get; }

        string ParseAddressForFileName(string address);

        bool CheckPrefix(string address);

        Task<string> Send(MemoryStream memStream, string address);
    }
}
