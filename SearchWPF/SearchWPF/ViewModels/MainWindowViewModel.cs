using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using SearchFiles;



namespace SearchWPF
{
    public class MainWindowViewModel : INotifyPropertyChanged, IDisposable
    {
        //private readonly Dictionary<string, ISearch> typesMultiThreadingDict = new Dictionary<string, ISearch>();
        private readonly string[] typesMultiThreadingStrings = new string[3];
        public readonly Stopwatch stopwatch = new Stopwatch();
        private readonly object lockerCollection = new object();

        //private readonly System.Threading.Timer timer = null;
        //private int periodTimer = 1000;

        //void TimerCallback(object state)
        //{
        //    Application.Current.Dispatcher.Invoke(() =>
        //    {
        //        int count = Swatches.Count;
        //        SelectedSwatchPrimaryColor = Swatches[new Random().Next(0, count)];
        //    });
            
        //}



        public MainWindowViewModel()
        {
            //timer = new System.Threading.Timer(new TimerCallback(TimerCallback), null, Timeout.Infinite, periodTimer);

            OpenFileCommand                     = new ActionCommand(OpenFileCommandExecute, OpenFileCommandCanExecute);
            OpenFolderCommand                   = new ActionCommand(OpenFolderCommandExecute, OpenFolderCommandCanExecute);
            DropDownDisksOpenedCommand          = new ActionCommand(DropDownDisksOpenedCommandExecute);
            StartSearchCommand                  = new ActionCommand(StartSearchCommandExecute, StartSearchCommandCanExecute);
            StopSearchCommand                   = new ActionCommand(StopSearchCommandExecute, StopSearchCommandCanExecute);
            ShowHelpTypeMultithreadingCommand   = new ActionCommand(ShowHelpTypeMultithreadingCommandExecute);
            ClosingWindowCommand                = new ActionCommand(ClosingWindowCommandExecute);

            //разрешает доступ к коллекции из разных потоков
            BindingOperations.EnableCollectionSynchronization(FilesCollection, lockerCollection);            

            TypesMultiThreading.Add(new SearchManagerThread());
            TypesMultiThreading.Add(new SearchManagerThreadPool());
            TypesMultiThreading.Add(new SearchManagerTask());

            for (var i = 0; i < TypesMultiThreading.Count; ++i)
            {
                var s = TypesMultiThreading[i];

                s.eventNewFileFinded += SearchTechnology_eventNewFileFinded;
                s.eventWorkingThreadsChanged += SearchTechnology_eventWorkingThreadsChanged;
                typesMultiThreadingStrings[i] = s.Name;
            }
            SearchTechnology = TypesMultiThreading.Last();

            MaxCountThreadsStr = "100";
            CurrentState = "Настройте поиск и нажмите Найти";
            CountWorkingThreads = 0;
            


            //определение расширений разных типов файлов
            FileExtensionAssociation textFiles          = new FileExtensionAssociation()
            {
                Name = "Текстовые файлы",
                Extensions = ".txt .docx .doc .rtf .pdf .cpp .h .cs .log .ini .cfg .lst .xml .html .text .yml .utf8"
            };
            FileExtensionAssociation audioFiles         = new FileExtensionAssociation()
            {
                Name = "Аудиофайлы",
                Extensions = ".mp3 .wav .aac .ogg .mp2 .mp1 "
            };
            FileExtensionAssociation videoFiles         = new FileExtensionAssociation()
            {
                Name = "Видеофайлы",
                Extensions = ".mp4 .mp2 .mpg .wmv .mpeg .avi .mpeg4 .mkv .flv .mov .webm .mpeg1 .3gp .3gpp .3gpp2 .h264 .swf"
            };
            FileExtensionAssociation pictureFiles       = new FileExtensionAssociation()
            {
                Name = "Изображения",
                Extensions = ".jpg .jpeg .png .gif .bmp .tiff .webp .psd .nrw"
            };
            FileExtensionAssociation applicationFiles   = new FileExtensionAssociation()
            {
                Name = "Приложения",
                Extensions = ".exe .msi .bat .cmd .ps1 .vbe .vbs"
            };
            FileExtensionAssociation archiveFiles       = new FileExtensionAssociation()
            {
                Name = "Архивы",
                Extensions = ".rar .zip .7z .zip .gzip .tar .tgz .xz .bz2 .z"
            };
            //добавление расширений в коллекцию
            FileExtensionAssociations.Add(textFiles);
            FileExtensionAssociations.Add(audioFiles);
            FileExtensionAssociations.Add(videoFiles);
            FileExtensionAssociations.Add(pictureFiles);
            FileExtensionAssociations.Add(applicationFiles);
            FileExtensionAssociations.Add(archiveFiles);


            //получение и добавление разделов дисков
            foreach (var disk in DriveInfo.GetDrives())
                DriveInfos.Add(disk);
            if (DriveInfos.Count > 0)
                SelectedDrive = DriveInfos[0];


            Swatches = new ObservableCollection<Swatch>(new SwatchesProvider().Swatches);

            LoadOptions();


            //SelectedSwatchPrimaryColor = Swatches.Where(s => s.Name == "blue").Single();

        }

        #region region TypesMultiThreading
        public ObservableCollection<ISearch> TypesMultiThreading { get; } = new ObservableCollection<ISearch>();
        private ISearch _searchTech = null;
        public ISearch SearchTechnology
        {
            get => _searchTech;
            set
            {
                _searchTech = value;
                RaisePropertyChanged(nameof(SearchTechnology));
                IsEnabledMaxCountThreads = _searchTech.Name == "Класс Thread";
            }
        }
        #endregion

        #region region IsEnabled

        private bool _isEnabledMaxCountThreads = false;
        public bool IsEnabledMaxCountThreads
        {
            get => _isEnabledMaxCountThreads;
            set
            {
                _isEnabledMaxCountThreads = value;
                RaisePropertyChanged(nameof(IsEnabledMaxCountThreads));
            }
        }
        #endregion


        #region region Input Data


        //максимальное кол-во потоков
        private int _maxCountThreadsInt = 0;
        private string _maxCountThreads = string.Empty;
        public string MaxCountThreadsStr
        {
            get => _maxCountThreads;
            set
            {
                if (int.TryParse(value, out int result))
                {
                    if (result < 201)
                    {
                        _maxCountThreads = value;
                        _maxCountThreadsInt = result;
                        RaisePropertyChanged(nameof(MaxCountThreadsStr));
                        RaisePropertyChanged(nameof(MaxCountThreadsInt));
                    }
                    else
                    {
                        _maxCountThreads = "200";
                        _maxCountThreadsInt = 200;
                        RaisePropertyChanged(nameof(MaxCountThreadsStr));
                        RaisePropertyChanged(nameof(MaxCountThreadsInt));
                    }
                }
                
            }
        }
        private int MaxCountThreadsInt
        {
            get => _maxCountThreadsInt;
            set
            {
                _maxCountThreadsInt = value;
                _maxCountThreads = value.ToString();
                RaisePropertyChanged(nameof(MaxCountThreadsStr));
                RaisePropertyChanged(nameof(MaxCountThreadsInt));
            }
        }

        //расширение файла
        private string _extensionFile = string.Empty;
        public string ExtensionFile
        {
            get => _extensionFile;
            set
            {
                _extensionFile = value;
                RaisePropertyChanged(nameof(ExtensionFile));
            }
        }
        //заготовки
        public ObservableCollection<FileExtensionAssociation> FileExtensionAssociations { get; } = new ObservableCollection<FileExtensionAssociation>();
        public FileExtensionAssociation SelectedFileExtensionAssociation { get; set; } = null;


        //слово или фраза в файле
        private string _wordInFile = string.Empty;
        public string WordInFile
        {
            get => _wordInFile;
            set
            {
                _wordInFile = value;
                RaisePropertyChanged(WordInFile);
            }
        }


        //диски
        public ObservableCollection<DriveInfo> DriveInfos { get; } = new ObservableCollection<DriveInfo>();

        private DriveInfo _selectedDrive = null;
        public DriveInfo SelectedDrive
        {
            get => _selectedDrive;
            set
            {
                _selectedDrive = value;
                RaisePropertyChanged(nameof(SelectedDrive));
            }
        }        


        private bool _recourciveSearch = true;
        public bool RecourciveSearch
        {
            get => _recourciveSearch;
            set
            {
                _recourciveSearch = value;
                RaisePropertyChanged(nameof(RecourciveSearch));
            }
        }

        private bool _searchInTextFile = true;
        public bool SearchInTextFile
        {
            get => _searchInTextFile;
            set
            {
                _searchInTextFile = value;
                RaisePropertyChanged(nameof(SearchInTextFile));
            }
        }


        #endregion

        #region region Output Data


        //результат поиска        
        public ObservableCollection<FileItemView> FilesCollection { get; } = new ObservableCollection<FileItemView>();


        private FileItemView _selectedItem = null;
        public FileItemView SelectedItemResult
        {
            get => _selectedItem;
            set
            {
                _selectedItem = value;
                RaisePropertyChanged(nameof(SelectedItemResult));
            }
        }

        //текущее состояние
        private string _currentState = string.Empty;
        public string CurrentState
        {
            get => _currentState;
            private set
            {
                _currentState = value;
                RaisePropertyChanged(nameof(CurrentState));
            }
        }


        //работающие потоки        
        private int _countWorkingThreads = 0;
        public int CountWorkingThreads
        {
            get => _countWorkingThreads;
            set
            {
                _countWorkingThreads = value;
                RaisePropertyChanged(nameof(CountWorkingThreads));
            }
        }


        //время поиска      
        private string _workingTime = string.Empty;
        public string WorkingTime
        {
            get => _workingTime;
            private set
            {
                _workingTime = value;
                RaisePropertyChanged(nameof(WorkingTime));
            }
        }

        private bool _processSearch = false;
        public bool ProcessSearch
        {
            get => _processSearch;
            set
            {
                _processSearch = value;
                RaisePropertyChanged(nameof(ProcessSearch));
            }
        }

        #endregion

        #region IsEnabled


        private bool _typeMultiThreadingIsEnabled = true;
        public bool TypeMultiThreadingIsEnabled
        {
            get => _typeMultiThreadingIsEnabled;
            set
            {
                _typeMultiThreadingIsEnabled = value;
                RaisePropertyChanged(nameof(TypeMultiThreadingIsEnabled));
            }
        }


        //расширение файла
        private bool _fileExtensionIsEnabled = true;
        public bool FileExtensionIsEnabled
        {
            get => _fileExtensionIsEnabled;
            set
            {
                _fileExtensionIsEnabled = value;
                RaisePropertyChanged(nameof(FileExtensionIsEnabled));
            }
        }


        //слово или фраза в файле
        private bool _wordInFileIsEnabled = true;
        public bool WordInFileIsEnabled
        {
            get => _wordInFileIsEnabled;
            set
            {
                _wordInFileIsEnabled = value;
                RaisePropertyChanged(nameof(WordInFileIsEnabled));
            }
        }

        //диски
        private bool _disksComboBoxIsEnabled = true;
        public bool DisksComboBoxIsEnabled
        {
            get => _disksComboBoxIsEnabled;
            set
            {
                _disksComboBoxIsEnabled = value;
                RaisePropertyChanged(nameof(DisksComboBoxIsEnabled));
            }
                
        }


        //Старт
        private bool _startSearchIsEnabled = true;
        public bool StartSearchIsEnabled
        {
            get => _startSearchIsEnabled;
            set
            {
                _startSearchIsEnabled = value;
                RaisePropertyChanged(nameof(StartSearchIsEnabled));
            }
        }

        //Стоп
        private bool _stopSearchIsEnabled = false;
        public bool StopSearchIsEnabled
        {
            get => _stopSearchIsEnabled;
            set
            {
                _stopSearchIsEnabled = value;
                RaisePropertyChanged(nameof(StopSearchIsEnabled));
            }
        }


        #endregion


        //#region region ExitCommand
        //public ActionCommand ExitCommand { get; private set; }
        //void ExitCommandExecute (object state)
        //{
        //    //if (MessageBox.Show(
        //    //    "Выйти из программы?", 
        //    //    "Выход", 
        //    //    MessageBoxButton.YesNo, 
        //    //    MessageBoxImage.Question) == MessageBoxResult.Yes)
        //    //(state as MainWindow)?.Close();
        //}
        //bool ExitCommandCanExecute(object state)
        //{
        //    return true;
        //}
        //#endregion

        //#region region MaximizeCommand
        //public ActionCommand MaximizeCommand { get; private set; }
        //void MaximizeCommandExecute(object state)
        //{
        //    //MainWindow wnd = state as MainWindow;
        //    //if (wnd == null)
        //    //    return;

        //    //if (wnd.WindowState == WindowState.Maximized)
        //    //{
        //    //    wnd.WindowState = WindowState.Normal;
        //    //    ButtonIconName = "WindowMaximize";
        //    //}
        //    //else
        //    //{
        //    //    wnd.WindowState = WindowState.Maximized;
        //    //    ButtonIconName = "WindowRestore";
        //    //}        
        //}
        //bool MaximizeCommandCanExecute(object state)
        //{
        //    return true;
        //}

        //private string _buttonIconName = "WindowMaximize";
        //public string ButtonIconName
        //{
        //    get => _buttonIconName;
        //    set
        //    {
        //        _buttonIconName = value;
        //        RaisePropertyChanged(nameof(ButtonIconName));
        //    }
        //}
        //#endregion

        //#region region MinimizeCommand
        //public ActionCommand MinimizeCommand { get; private set; }
        //void MinimizeCommandExecute(object state)
        //{
        //    //MainWindow wnd = state as MainWindow;
        //    //if (wnd == null)
        //    //    return;
        //    //wnd.WindowState = wnd.WindowState == WindowState.Minimized ? WindowState.Normal : WindowState.Minimized;
        //}
        //bool MinimizeCommandCanExecute(object state)
        //{
        //    return true;
        //}
        //#endregion

        //#region region MoveCommand
        //public ActionCommand MoveCommand { get; set; }
        //void MoveCommandExecute (object state)
        //{
        //    //System.Windows.Window wnd = state as System.Windows.Window;
        //    //if (wnd == null)
        //    //    return;
        //    //try
        //    //{
        //    //    wnd.DragMove();
        //    //}
        //    //catch (InvalidOperationException) { }
        //}
        //bool MoveCommandCanExecute (object state)
        //{
        //    return true;
        //}
        //#endregion

        #region DropDownDisksOpened

        public ActionCommand DropDownDisksOpenedCommand { get; private set; }

        void DropDownDisksOpenedCommandExecute(object state)
        {
            DriveInfos.Clear();
            //получение и добавление разделов дисков
            foreach (var disk in DriveInfo.GetDrives().Where(d => d.IsReady))
                DriveInfos.Add(disk);
            if (DriveInfos.Count > 0)
                SelectedDrive = DriveInfos[0];
        }

        #endregion

        #region region StartSearchCommand

        //Начать поиск
        public ActionCommand StartSearchCommand { get; private set; }
        void StartSearchCommandExecute(object state)
        {
            
            if (SelectedDrive == null)
            {
                CurrentState = "Не выбран путь/диск для поиска";
                return;
            }
            else if (!SelectedDrive.IsReady)
            {
                CurrentState = "Выбранный диск не готов";
                return;
            }

            if (SearchTechnology == null)
            {
                CurrentState = "Некорректно выбран тип реализации многопоточности";
                return;
            }

            FilesCollection.Clear();

            SearchTechnology.DirectorySearch = new DirectoryInfo(SelectedDrive.Name);
            SearchTechnology.Mask = SelectedFileExtensionAssociation?.Extensions ?? ExtensionFile;
            SearchTechnology.ResourciseSearch = RecourciveSearch;
            SearchTechnology.WordInTextFiles = WordInFile;
            SearchTechnology.SearchInTextFiles = SearchInTextFile;
            try
            {
                SearchTechnology.MaxCountThreads = MaxCountThreadsInt;
            }
            catch (InvalidOperationException) { }

            stopwatch.Reset();
            

            try
            {
                SearchTechnology.StartSearch();
                
                stopwatch.Start();
                CurrentState = "Идет поиск файлов...";

                //timer.Change(0, periodTimer);

                ChangeState(true);
            }
            catch (Exception ex)
            {
                CurrentState = "Поиск не начался. " + ex.Message;
                ChangeState(false);
            }
        }
        bool StartSearchCommandCanExecute(object state)
        {
            return true;
        }
        #endregion

        #region region StopSearchCommand

        public ActionCommand StopSearchCommand { get; private set; }
        void StopSearchCommandExecute(object state)
        {
            SearchTechnology.StopSearch();
            CurrentState = "Поиск останавливается... Подождите...";
           
            StopSearchIsEnabled = false;
        }
        bool StopSearchCommandCanExecute(object state)
        {
            return true;
        }

        #endregion

        #region region OpenFileCommand

        public ActionCommand OpenFileCommand { get; set; }
        void OpenFileCommandExecute(object state)
        {
            if (SelectedItemResult != null)
            {
                try
                {
                    Process.Start(SelectedItemResult.FullPath);
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    MessageBox.Show("Ошибка открытия файла. " + ex.Message);
                }
            }
        }
        bool OpenFileCommandCanExecute(object state)
        {
            return SelectedItemResult != null;
        }

        #endregion

        #region region OpenFolderCommand

        public ActionCommand OpenFolderCommand { get; set; }
        void OpenFolderCommandExecute (object state)
        {
            if (SelectedItemResult == null)
                return;

            try
            {
                Process.Start("explorer.exe", " /select, " + (SelectedItemResult.FullPath));
            }
            catch (Win32Exception ex)
            {
                MessageBox.Show("Ошибка открытия файла. " + ex.Message);
            }
        }
        bool OpenFolderCommandCanExecute (object state)
        {
            return SelectedItemResult != null;
        }

        #endregion

        #region region ShowHelpTypeMultithreadingCommand

        public ActionCommand ShowHelpTypeMultithreadingCommand { get; set; }
        void ShowHelpTypeMultithreadingCommandExecute(object state)
        {
            Windows.MessageWindow messageWindow = new Windows.MessageWindow(
                "Класс Thread рекомендуется выбирать при поиске в контексте больших объемов данных и вложенности папок. Эта реализация дает возможность вручную регулировать в соответствующем пункте меню кол-во потоков доступных поиску. Следует учитывать, что большое кол-во (больше 150) может загрузить процессор и не даст Вам совершать другие действия на Вашем компьютере. На этот параметр стоит ограничение в 200 потоков. " +
                "ThreadPool не дает возможности выбрать кол-во потоков, потому что этим управляет платформа .NET, и строго не рекомендуется изменять это значение. " +
                "Класс Task ориентирован на многоядерность и его следует выбирать при большом кол-ве ядер процессора. " +
                "Если для Вас это не имеет значения просто оставьте как есть",
                "Как выбрать реализацию многопоточности");
            messageWindow.Owner = state as MainWindow;
            messageWindow.ShowDialog();
        }
        #endregion

        #region region ClosingWindowCommand

        public ActionCommand ClosingWindowCommand { get; private set; }
        void ClosingWindowCommandExecute(object state)
        {
            SaveOptions();
        }

        #endregion


        #region Style

        private bool _isDark = false;
        public bool IsDarkColor
        {
            get => _isDark;
            set
            {
                _isDark = value;
                new PaletteHelper().SetLightDark(value);
            }
        }

        public ObservableCollection<Swatch> Swatches { get; }

        private Swatch _selectedSwatchPrimaryColor = null;
        public Swatch SelectedSwatchPrimaryColor
        {
            get => _selectedSwatchPrimaryColor;
            set
            {
                _selectedSwatchPrimaryColor = value;
                RaisePropertyChanged(nameof(SelectedSwatchPrimaryColor));
                new PaletteHelper().ReplacePrimaryColor(value);
            }
        }        


        #endregion


        #region region SearchEvents
        private void SearchTechnology_eventWorkingThreadsChanged(object sender, EventArgs e)
        {
            WorkingTime = stopwatch.Elapsed.ToString();
            CountWorkingThreads = SearchTechnology.CountWorkingThreads;

            //поиск еще не начинался
            if (SearchTechnology.IsReady == false && SearchTechnology.CountWorkingThreads == 0)
                return;

            ChangeState(!SearchTechnology.IsReady);

            //поиск закончен
            if (SearchTechnology.IsReady)
            {
                CurrentState = "Поиск завершен";
                stopwatch.Stop();

                //остановка
                //timer.Change(0, 0);
                //SelectedSwatchPrimaryColor = DefaultSwatch;
            }
            else
            {
                CurrentState = "Идет поиск файлов...";
                if (!stopwatch.IsRunning)
                    stopwatch.Start();
            }           
        }

        private void SearchTechnology_eventNewFileFinded(object sender, FileInfo fileInfo)
        {
            //WorkingTime = stopwatch.Elapsed.ToString();
 
            FilesCollection.Add(new FileItemView()
            {
                //ImageItem = null,
                Name = fileInfo.Name,
                Size = fileInfo.Length.ToString() + " Байт",
                CreationTime = fileInfo.CreationTime.ToString(),
                ModifyTime = fileInfo.LastWriteTime.ToString(),
                Attributes = fileInfo.Attributes.ToString(),
                FullPath = fileInfo.FullName
            });            
        }


        //true - поиск начался, false - остановился
        void ChangeState(bool state)
        {
            TypeMultiThreadingIsEnabled = !state;            
            FileExtensionIsEnabled = !state;
            WordInFileIsEnabled = !state;
            DisksComboBoxIsEnabled = !state;
            StartSearchIsEnabled = !state;

            ProcessSearch = state;
            StopSearchIsEnabled = state;
        }

        #endregion



        #region region SaveOptions

        void SaveOptions()
        {

            try
            {
                using (RegistryKey softwareKey = Registry.CurrentUser.OpenSubKey("Software", true))
                {
                    using (RegistryKey SearchKey = softwareKey.CreateSubKey("SearchWPF", true))
                    {
                        SearchKey.SetValue(nameof(SearchInTextFile), SearchInTextFile);
                        SearchKey.SetValue(nameof(ExtensionFile), ExtensionFile);
                        SearchKey.SetValue(nameof(WordInFile), WordInFile);
                        SearchKey.SetValue(nameof(RecourciveSearch), RecourciveSearch);
                        SearchKey.SetValue(nameof(IsDarkColor), IsDarkColor);
                        SearchKey.SetValue(nameof(SelectedSwatchPrimaryColor), SelectedSwatchPrimaryColor);
                    }
                }
            }
            catch
            {

            }
        }

        void LoadOptions()
        {
            try
            {
                using (RegistryKey softwareKey = Registry.CurrentUser.OpenSubKey("Software", true))
                {
                    using (RegistryKey SearchKey = softwareKey.CreateSubKey("SearchWPF", false))
                    {
                        SearchInTextFile = bool.Parse((string)SearchKey.GetValue(nameof(SearchInTextFile), "true"));

                        string ext = (string)SearchKey.GetValue(nameof(ExtensionFile), "");//получаем текст расширения файлов
                        if (ext == "")
                        {
                            ExtensionFile = "";
                        }
                        else
                        {
                            // получаем объекты FileExtensionAssociation с таким названием
                            IEnumerable<FileExtensionAssociation> findObj = FileExtensionAssociations.Where(n => n.Name == ext);
                            //если такого объекта нет, тогда это был просто текст введенный вручную
                            if (findObj.Count() == 0)
                            {
                                ExtensionFile = ext;
                            }
                            else
                            {
                                SelectedFileExtensionAssociation = findObj.Single();
                            }
                        }
                        WordInFile                 = (string)SearchKey.GetValue(nameof(WordInFile), "");
                        RecourciveSearch           = bool.Parse((string)SearchKey.GetValue(nameof(RecourciveSearch), "true"));
                        IsDarkColor                = bool.Parse((string)SearchKey.GetValue(nameof(IsDarkColor), "false"));
                        string swatchName          = (string)SearchKey.GetValue(nameof(SelectedSwatchPrimaryColor), "blue");
                        SelectedSwatchPrimaryColor = Swatches.Where(sw => sw.Name == swatchName).Single();
                        
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        #endregion


        public void Dispose()
        {
            //timer?.Dispose();
            for (int i = 0; i < TypesMultiThreading.Count; ++i)
            {
                ISearch s = TypesMultiThreading[i];
                s?.Dispose();
            }
        }
        private void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
