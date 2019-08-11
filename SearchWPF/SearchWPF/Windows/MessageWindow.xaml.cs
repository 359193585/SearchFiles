using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SearchWPF.Windows
{
    /// <summary>
    /// Логика взаимодействия для MessageWindow.xaml
    /// </summary>
    public partial class MessageWindow : Window
    {

        public MessageWindow()
        {
            InitializeComponent();
        }

        public MessageWindow(string message) : this()
        {           
            this.DataContext = new ViewModels.MessageWindowViewModel(message, string.Empty);
        }

        public MessageWindow(string message, string caption) : this()
        {
            this.DataContext = new ViewModels.MessageWindowViewModel(message, caption);
        }
    }
}
