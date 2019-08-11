using EPocalipse.IFilter;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace SearchFiles
{
    class SearchManagerThread : ISearch
    {
        private readonly List<string> textFormats = new List<string>() { ".txt", ".docx", ".doc", ".cpp", ".h", ".cs", ".log", ".ini", ".cfg", ".lst", ".xml", ".html" };
        public string Name { get; private set; } = "Класс Thread";
        readonly ManualResetEvent ResetEvent = new ManualResetEvent(true);
        readonly object lockerFileFinded = new object();

        Semaphore semaphore = null;
        
        //по умолчанию 100
        private int _maxCountThreads = 100;
        public int MaxCountThreads
        {
            get => _maxCountThreads;
            set
            {
                _maxCountThreads = value;
                semaphore?.Dispose();
                semaphore = null;
                semaphore = new Semaphore(_maxCountThreads, _maxCountThreads);
            }
        }

        
        private int _countWorkingThreads = 0;

        public int CountWorkingThreads
        {
            get => _countWorkingThreads;
            private set
            {
                if (_countWorkingThreads == 0)
                {
                    IsReady = true;
                    eventWorkingThreadsChanged?.Invoke(null, EventArgs.Empty);
                }
            }
        }

        public bool IsReady { get; private set; } = false;

        public event EventHandler<FileInfo> eventNewFileFinded;
        public event EventHandler eventWorkingThreadsChanged;

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



        //public List<FileInfo> ResultList { get; set; } = new List<FileInfo>();

        #region Constructors
        public SearchManagerThread()
        {
            semaphore = new Semaphore(MaxCountThreads, MaxCountThreads);
        }
        public SearchManagerThread(int maxCountThreads)
        {
            MaxCountThreads = maxCountThreads;
        }


        
        public SearchManagerThread(string mask, DirectoryInfo directorySearch, int maxCountThreads) 
            : this(maxCountThreads)
        {
            Mask = mask;
            DirectorySearch = directorySearch;
        }
        public SearchManagerThread(string mask, DirectoryInfo directorySearch, bool resourciseSearch, int maxCountThreads) 
            : this(mask, directorySearch, maxCountThreads)
        {
            ResourciseSearch = resourciseSearch;
        }
        public SearchManagerThread(string wordInTextFiles, string mask, DirectoryInfo directorySearch, int maxCountThreads) 
            : this(maxCountThreads)
        {
            Mask = mask;
            DirectorySearch = directorySearch;
            WordInTextFiles = wordInTextFiles;
        }
        public SearchManagerThread(string wordInTextFiles, string mask, DirectoryInfo directorySearch, bool resourciseSearch, int maxCountThreads) 
            : this(wordInTextFiles, mask, directorySearch, maxCountThreads)
        {
            ResourciseSearch = resourciseSearch;
        }
        #endregion


        public void StartSearch()
        {
            if (DirectorySearch == null || !DirectorySearch.Exists)
                throw new ArgumentException("Не указана директоря для поиска или такая директория не существует");
            IsReady = false;
            ResetEvent.Set();
            Thread thread = new Thread(new ParameterizedThreadStart(SearchInDirectory)) {  IsBackground = true, Name = DirectorySearch.FullName, Priority = ThreadPriority.BelowNormal };
            thread.Start(ResourciseSearch);
        }
        public void StopSearch()
        {
            ResetEvent.Reset();
        }

        private void SearchInDirectory(object obj)
        {
            if (!ResetEvent.WaitOne(0))
            {
                return;
            }

            semaphore.WaitOne(Timeout.Infinite);
            Interlocked.Increment(ref _countWorkingThreads);
            CountWorkingThreads = _countWorkingThreads;
            eventWorkingThreadsChanged?.Invoke(null, EventArgs.Empty);

            if (!ResetEvent.WaitOne(0))
            {
                Interlocked.Decrement(ref _countWorkingThreads);
                CountWorkingThreads = _countWorkingThreads;
                eventWorkingThreadsChanged?.Invoke(null, EventArgs.Empty);
                semaphore.Release();
                return;
            }

            bool resourciseSearch;
            try
            {
                resourciseSearch = (bool)obj;
            }
            catch(InvalidCastException)
            {
                resourciseSearch = true;
            }
            
            //CurrentThread.Name - полный путь к папке в которой работает поток
            DirectoryInfo dir = null;
            try
            {
                dir = new DirectoryInfo(Thread.CurrentThread.Name);
            }
            catch (Exception)
            {
                Interlocked.Decrement(ref _countWorkingThreads);
                CountWorkingThreads = _countWorkingThreads;
                eventWorkingThreadsChanged?.Invoke(null, EventArgs.Empty);
                semaphore.Release();
                return;
            }
            if (!dir.Exists)
            {
                Interlocked.Decrement(ref _countWorkingThreads);
                CountWorkingThreads = _countWorkingThreads;
                eventWorkingThreadsChanged?.Invoke(null, EventArgs.Empty);
                semaphore.Release();
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
                semaphore.Release();
                return;
            }
            if (!ResetEvent.WaitOne(0))
            {
                Interlocked.Decrement(ref _countWorkingThreads);
                CountWorkingThreads = _countWorkingThreads;
                eventWorkingThreadsChanged?.Invoke(null, EventArgs.Empty);
                semaphore.Release();
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
                        semaphore.Release();
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
                        semaphore.Release();
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
                semaphore.Release();
                return;
            }
            //поиск в подпапках
            if (resourciseSearch == true)
            {
                DirectoryInfo[] dirs = dir.GetDirectories();
                for (var i = 0; i < dirs.Length; ++i)
                {
                    if (!ResetEvent.WaitOne(0))
                    {
                        Interlocked.Decrement(ref _countWorkingThreads);
                        CountWorkingThreads = _countWorkingThreads;
                        eventWorkingThreadsChanged?.Invoke(null, EventArgs.Empty);
                        semaphore.Release();
                        return;
                    }
                    Thread th = new Thread(new ParameterizedThreadStart(SearchInDirectory)) { IsBackground = true, Name = dirs[i].FullName, Priority = ThreadPriority.Lowest };
                    th.Start(resourciseSearch);

                }
            }
            Interlocked.Decrement(ref _countWorkingThreads);
            CountWorkingThreads = _countWorkingThreads;
            eventWorkingThreadsChanged?.Invoke(null, EventArgs.Empty);
            semaphore.Release();
        }

        //поиск внутри текстового файла
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
            semaphore?.Dispose();
            ResetEvent?.Dispose();
        }
    }
}
