using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SearchFiles
{
    public interface ISearch : IDisposable
    {
        string Name { get; }
        string WordInTextFiles { get; set; }
        string Mask { get; set; }
        DirectoryInfo DirectorySearch { get; set; }
        bool ResourciseSearch { get; set; }
        bool SearchInTextFiles { get; set; }

        bool IsReady { get; }

        event EventHandler<FileInfo> eventNewFileFinded;
        event EventHandler eventWorkingThreadsChanged;

        //List<FileInfo> ResultList { get; set; }
        int MaxCountThreads { get; set; }
        int CountWorkingThreads { get; }
        //bool IsReady { get; set; }

        void StopSearch();
        void StartSearch();
    }
}
