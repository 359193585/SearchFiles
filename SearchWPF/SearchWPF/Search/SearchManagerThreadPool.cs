using EPocalipse.IFilter;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace SearchFiles
{


    class SearchManagerThreadPool : ISearch
    {
        private readonly List<string> textFormats = new List<string>() { ".txt", ".docx", ".doc", ".cpp", ".h", ".cs", ".log", ".ini", ".cfg", ".lst", ".xml", ".html" };
        public string Name { get; private set; } = "ThreadPool";
        readonly ManualResetEvent ResetEvent = new ManualResetEvent(true);
        readonly object lockerFileFinded = new object();


        #region Constructors
        public SearchManagerThreadPool()
        {

        }
        public SearchManagerThreadPool(string mask, DirectoryInfo directorySearch)
            
        {
            Mask = mask;
            DirectorySearch = directorySearch;
        }
        public SearchManagerThreadPool(string mask, DirectoryInfo directorySearch, bool resourciseSearch)
            : this(mask, directorySearch)
        {
            ResourciseSearch = resourciseSearch;
        }
        public SearchManagerThreadPool(string wordInTextFiles, string mask, DirectoryInfo directorySearch)
        {
            Mask = mask;
            DirectorySearch = directorySearch;
            WordInTextFiles = wordInTextFiles;
        }
        public SearchManagerThreadPool(string wordInTextFiles, string mask, DirectoryInfo directorySearch, bool resourciseSearch)
            : this(wordInTextFiles, mask, directorySearch)
        {
            ResourciseSearch = resourciseSearch;
        }
        #endregion

        public string WordInTextFiles { get; set; }
        private string _mask = string.Empty;
        public string Mask
        {
            get => _mask;
            set
            {
                _mask = value;
                Extensions.Clear();
                Extensions.AddRange(value.Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries));
            }
        }

        private List<string> Extensions { get; set; } = new List<string>();

        public DirectoryInfo DirectorySearch { get; set; }
        public bool ResourciseSearch { get; set; } = true;
        public bool SearchInTextFiles { get; set; } = true;

        public event EventHandler<FileInfo> eventNewFileFinded;
        public event EventHandler eventWorkingThreadsChanged;

        public int MaxCountThreads
        {
            get
            {
                ThreadPool.GetMaxThreads(out int workerThreads, out int completionPortThreads);
                return workerThreads;
            }
            set
            {
                throw new InvalidOperationException("Для пула потоков нельзя задать максимальное количество потоков. Этим управляет виртуальный процессор платформы .NET");
            }
        }

        private int _countWorkingThreads = 0;
        public int CountWorkingThreads
        {
            get => _countWorkingThreads;
            private set
            {
                if (!ResetEvent.WaitOne(0) || _countWorkingThreads == 0)
                {
                    IsReady = true;                    
                }
                else
                {
                    IsReady = false;
                }
                eventWorkingThreadsChanged?.Invoke(null, EventArgs.Empty);
            }
        }
        public bool IsReady { get; private set; } = false;






        public void StartSearch()
        {
            if (DirectorySearch == null || !DirectorySearch.Exists)
                throw new ArgumentException("Не указана директоря для поиска или такая директория не существует");
            //if (string.IsNullOrWhiteSpace(Mask))
            //    throw new ArgumentException("Не указано расширение файла");

            IsReady = false;
            ResetEvent.Set();
            ThreadPool.QueueUserWorkItem(new WaitCallback(SearchInDirectory), DirectorySearch);
        }

        public void StopSearch()
        {
            ResetEvent.Reset();
        }


        private void SearchInDirectory(object obj)
        {
            if (!ResetEvent.WaitOne(0))
                return;


            Interlocked.Increment(ref _countWorkingThreads);
            CountWorkingThreads = _countWorkingThreads;
            eventWorkingThreadsChanged?.Invoke(null, EventArgs.Empty);

            //if (!ResetEvent.WaitOne(0))
            //{
            //    Interlocked.Decrement(ref _countWorkingThreads);
            //    eventWorkingThreadsChanged?.Invoke(null, EventArgs.Empty);
            //    return;
            //}

            

            DirectoryInfo dir = obj as DirectoryInfo;
            if (dir == null)
            {
                Interlocked.Decrement(ref _countWorkingThreads);
                CountWorkingThreads = _countWorkingThreads;
                eventWorkingThreadsChanged?.Invoke(null, EventArgs.Empty);
                return;
            }
            
            if (!dir.Exists)
            {
                Interlocked.Decrement(ref _countWorkingThreads);
                CountWorkingThreads = _countWorkingThreads;
                eventWorkingThreadsChanged?.Invoke(null, EventArgs.Empty);
                return;
            }

            string[] filesStr;
            try
            {
                filesStr = Directory.GetFiles(dir.FullName);
            }
            catch (UnauthorizedAccessException)
            {
                Interlocked.Decrement(ref _countWorkingThreads);
                CountWorkingThreads = _countWorkingThreads;
                eventWorkingThreadsChanged?.Invoke(null, EventArgs.Empty);
                return;
            }
            if (!ResetEvent.WaitOne(0))
            {
                Interlocked.Decrement(ref _countWorkingThreads);
                CountWorkingThreads = _countWorkingThreads;
                eventWorkingThreadsChanged?.Invoke(null, EventArgs.Empty);
                return;
            }

            if (filesStr.Length > 0)
            {
                List<FileInfo> files = new List<FileInfo>(filesStr.Length);

                for (int i = 0; i < filesStr.Length; i++)
                {
                    if (!ResetEvent.WaitOne(0))
                    {
                        Interlocked.Decrement(ref _countWorkingThreads);
                        CountWorkingThreads = _countWorkingThreads;
                        eventWorkingThreadsChanged?.Invoke(null, EventArgs.Empty);
                        return;
                    }
                    try
                    {
                        FileInfo fileInfo = new FileInfo(filesStr[i]);

                        //если введено расширение файла
                        if (Mask.Length > 0)
                        {
                            if (fileInfo.Extension == "")
                                continue;
                            //если такое расширение не найдено, пропускаем файл
                            if (Extensions.IndexOf(fileInfo.Extension) == -1)
                                continue;
                            //иначе
                            else
                            {
                                //если расширения совпали и слово не введено, файл подходит
                                if (string.IsNullOrEmpty(WordInTextFiles))
                                {
                                    lock (lockerFileFinded)
                                    {
                                        eventNewFileFinded?.Invoke(null, fileInfo);
                                    }
                                    continue;
                                }
                            }
                        }

                        //если это выражение есть в названии файла, зачитываем
                        if (fileInfo.Name.Replace(fileInfo.Extension, "").IndexOf(WordInTextFiles, StringComparison.CurrentCultureIgnoreCase) != -1)
                        {
                            lock (lockerFileFinded)
                            {
                                eventNewFileFinded?.Invoke(null, fileInfo);
                            }
                            continue;
                        }

                        if (SearchInTextFiles)
                        {
                            if (SearchInTextFile(fileInfo))
                                lock (lockerFileFinded)
                                {
                                    eventNewFileFinded?.Invoke(null, fileInfo);
                                }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Interlocked.Decrement(ref _countWorkingThreads);
                        CountWorkingThreads = _countWorkingThreads;
                        eventWorkingThreadsChanged?.Invoke(null, EventArgs.Empty);
                        return;
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }
            }
            if (!ResetEvent.WaitOne(0))
            {
                Interlocked.Decrement(ref _countWorkingThreads);
                CountWorkingThreads = _countWorkingThreads;
                eventWorkingThreadsChanged?.Invoke(null, EventArgs.Empty);
                return;
            }
            //поиск в подпапках
            if (ResourciseSearch == true)
            {
                DirectoryInfo[] dirs = dir.GetDirectories();
                for (var i = 0; i < dirs.Length; ++i)
                {
                    if (!ResetEvent.WaitOne(0))
                    {
                        Interlocked.Decrement(ref _countWorkingThreads);
                        CountWorkingThreads = _countWorkingThreads;
                        eventWorkingThreadsChanged?.Invoke(null, EventArgs.Empty);
                        return;
                    }
                    ThreadPool.QueueUserWorkItem(new WaitCallback(SearchInDirectory), dirs[i]);
                }
            }
            Interlocked.Decrement(ref _countWorkingThreads);
            CountWorkingThreads = _countWorkingThreads;
            eventWorkingThreadsChanged?.Invoke(null, EventArgs.Empty);
        }

        private bool SearchInTextFile(FileInfo file)
        {
            if (!ResetEvent.WaitOne(0))
            {
                throw new OperationCanceledException("Операция отменена пользователем");
            }            

            //если это не текстовый формат
            if (textFormats.IndexOf(file.Extension) == -1)
                return false;

            switch (file.Extension)
            {
                case ".docx":
                case ".doc":
                    return SearchInMsWordFiles(file);
                default:
                    using (StreamReader reader = new StreamReader(file.FullName, Encoding.Default))
                    {
                        string str;
                        while ((str = reader.ReadLine()) != null)
                        {
                            if (str.IndexOf(WordInTextFiles, StringComparison.CurrentCultureIgnoreCase) != -1)
                                return true;
                            if (!ResetEvent.WaitOne(0))
                            {
                                throw new OperationCanceledException("Операция отменена пользователем");
                            }
                        }
                    }
                    break;
            }
            return false;
        }


        private bool SearchInMsWordFiles(FileInfo file)
        {
            try
            {
                using (var reader = new FilterReader(file.FullName))
                {

                    string str = reader.ReadToEnd();
                    if (str.IndexOf(WordInTextFiles, StringComparison.CurrentCultureIgnoreCase) != -1)
                        return true;
                }
            }
            catch (Exception) { }
            return false;
        }

        public void Dispose()
        {
            ResetEvent.Dispose();
        }
    }
}
