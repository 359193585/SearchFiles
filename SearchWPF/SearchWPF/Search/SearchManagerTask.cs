using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EPocalipse.IFilter;


namespace SearchFiles
{
    class SearchManagerTask : ISearch
    {
        private readonly List<string> textFormats = new List<string>() { ".txt", ".docx", ".doc", ".cpp", ".h", ".cs", ".log", ".ini", ".cfg", ".lst", ".xml", ".html" };
        public string Name { get; private set; } = "Класс Task";

        private CancellationTokenSource cancellationTokenSource = null;
        private CancellationToken token;
        private readonly List<Task> tasks = new List<Task>();

        readonly Timer timer = null;
        readonly int periodTimer = 100;

        readonly object lockerFileFinded = new object();
        readonly object lockerWordOpen = new object();

        #region Constructors
        public SearchManagerTask()
        {
            timer = new Timer(new TimerCallback(TimerCallback), null, Timeout.Infinite, periodTimer);
        }
        public SearchManagerTask(string mask, DirectoryInfo directorySearch)
            : this()
        {
            Mask = mask;
            DirectorySearch = directorySearch;
        }
        public SearchManagerTask(string mask, DirectoryInfo directorySearch, bool resourciseSearch)
            : this(mask, directorySearch)
        {
            ResourciseSearch = resourciseSearch;
        }
        public SearchManagerTask(string wordInTextFiles, string mask, DirectoryInfo directorySearch)
            : this()
        {
            Mask = mask;
            DirectorySearch = directorySearch;
            WordInTextFiles = wordInTextFiles;
        }
        public SearchManagerTask(string wordInTextFiles, string mask, DirectoryInfo directorySearch, bool resourciseSearch)
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
        public bool SearchInTextFiles { get; set; }

        public bool IsReady { get; private set; } = false;

        public event EventHandler<FileInfo> eventNewFileFinded;
        public event EventHandler eventWorkingThreadsChanged;

        public int MaxCountThreads
        {
            get
            {
                return -1;
            }
            set
            {
                throw new InvalidOperationException("Для тасков нельзя задать максимальное количество потоков. Этим управляет виртуальный процессор платформы .NET");
            }
        }

        private void TimerCallback(object state)
        {
            eventWorkingThreadsChanged?.Invoke(null, EventArgs.Empty);
        }


        private int _countWorkingThreads = 0;
        public int CountWorkingThreads
        {
            get => _countWorkingThreads;
            private set
            {
                if (token.IsCancellationRequested || _countWorkingThreads == 0)
                {
                    IsReady = true;
                    //Thread.Sleep(1000);
                    //eventWorkingThreadsChanged?.Invoke(null, EventArgs.Empty);
                    //for (var i = 0; i < tasks.Count; ++i)
                    //    tasks[i].Dispose();
                    //tasks.Clear();
                }
                else
                    IsReady = false;

                //if (_maxCount - value == _maxCount)
                //{
                //    IsReady = true;
                //    eventWorkingThreadsChanged?.Invoke(null, EventArgs.Empty);
                //    for (var i = 0; i < tasks.Count; ++i)
                //        tasks[i].Dispose();
                //    tasks.Clear();
                //}

            }
        }

        public void StartSearch()
        {
            if (DirectorySearch == null || !DirectorySearch.Exists)
                throw new ArgumentException("Не указана директоря для поиска или такая директория не существует");

            IsReady = false;

            timer.Change(0, periodTimer);
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = new CancellationTokenSource();
            token = cancellationTokenSource.Token;

            Task task = new Task(new Action<object>(SearchInDirectory), DirectorySearch);
            tasks.Add(task);
            task.Start();
        }

        public void StopSearch()
        {
            cancellationTokenSource?.Cancel(false);
            timer.Change(0, 500);
        }





        private void SearchInDirectory(object obj)
        {
            if (token.IsCancellationRequested)
                return;
            

            Interlocked.Increment(ref _countWorkingThreads);
            CountWorkingThreads = _countWorkingThreads;
            //eventWorkingThreadsChanged?.Invoke(null, EventArgs.Empty);

            //if (token.IsCancellationRequested)
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
                //eventWorkingThreadsChanged?.Invoke(null, EventArgs.Empty);
                return;
            }

            if (!dir.Exists)
            {
                Interlocked.Decrement(ref _countWorkingThreads);
                CountWorkingThreads = _countWorkingThreads;
                //eventWorkingThreadsChanged?.Invoke(null, EventArgs.Empty);
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
                //eventWorkingThreadsChanged?.Invoke(null, EventArgs.Empty);
                return;
            }
            if (token.IsCancellationRequested)
            {
                Interlocked.Decrement(ref _countWorkingThreads);
                CountWorkingThreads = _countWorkingThreads;
                //eventWorkingThreadsChanged?.Invoke(null, EventArgs.Empty);
                return;
            }

            if (filesStr.Length > 0)
            {
                List<FileInfo> files = new List<FileInfo>(filesStr.Length);

                for (int i = 0; i < filesStr.Length; i++)
                {
                    if (token.IsCancellationRequested)
                    {
                        Interlocked.Decrement(ref _countWorkingThreads);
                        CountWorkingThreads = _countWorkingThreads;
                        //eventWorkingThreadsChanged?.Invoke(null, EventArgs.Empty);
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

                        
                        //fileInfo.Name.Split('.')[0]
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
                        //eventWorkingThreadsChanged?.Invoke(null, EventArgs.Empty);
                        return;
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }
            }
            if (token.IsCancellationRequested)
            {
                Interlocked.Decrement(ref _countWorkingThreads);
                CountWorkingThreads = _countWorkingThreads;
                //eventWorkingThreadsChanged?.Invoke(null, EventArgs.Empty);
                return;
            }
            //поиск в подпапках
            if (ResourciseSearch == true)
            {
                DirectoryInfo[] dirs = dir.GetDirectories();
                for (var i = 0; i < dirs.Length; ++i)
                {
                    if (token.IsCancellationRequested)
                    {
                        Interlocked.Decrement(ref _countWorkingThreads);
                        CountWorkingThreads = _countWorkingThreads;
                        //eventWorkingThreadsChanged?.Invoke(null, EventArgs.Empty);
                        return;
                    }

                    Task task = new Task(new Action<object>(SearchInDirectory), dirs[i]);
                    tasks.Add(task);
                    task.Start();
                }
            }
            Interlocked.Decrement(ref _countWorkingThreads);
            CountWorkingThreads = _countWorkingThreads;
            //eventWorkingThreadsChanged?.Invoke(null, EventArgs.Empty);
        }

        private bool SearchInTextFile(FileInfo file)
        {
            //если это не текстовый формат
            if (textFormats.IndexOf(file.Extension) == -1)
                return false;


            if (token.IsCancellationRequested)
            {
                throw new OperationCanceledException("Операция отменена пользователем");
            }


            switch(file.Extension)
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
                            if (token.IsCancellationRequested)
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
            cancellationTokenSource?.Dispose();
            timer?.Dispose();
            if (tasks.Count > 0)
            {
                for (var i = 0; i < tasks.Count; ++i)
                    tasks[i].Dispose();
                tasks.Clear();
            }
        }
    }
}
