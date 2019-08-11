using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SearchWPF.ViewModels
{
    class MessageWindowViewModel : INotifyPropertyChanged
    {

        public MessageWindowViewModel(string message, string caption)
        {
            Message = message;
            Caption = caption;

            MoveCommand = new ActionCommand(MoveCommandExecute, MoveCommandCanExecute);
            ButtonRightCommand = new ActionCommand(ButtonRightCommandExecute);
        }
        

    
        private string _message = string.Empty;
        public string Message
        {
            get => _message;
            set
            {
                _message = value;
                RaisePropertyChanged(nameof(Message));
            }
        }

        private string _caption = string.Empty;
        public string Caption
        {
            get => _caption;
            set
            {
                _caption = value;
                RaisePropertyChanged(nameof(Caption));
            }
        }

        public ActionCommand ButtonRightCommand { get; set; }
        void ButtonRightCommandExecute (object state)
        {
            (state as Windows.MessageWindow)?.Close();
        }


        #region region MoveCommand
        public ActionCommand MoveCommand { get; set; }
        void MoveCommandExecute(object state)
        {
            System.Windows.Window wnd = state as System.Windows.Window;
            if (wnd == null)
                return;
            try
            {
                wnd.DragMove();
            }
            catch (InvalidOperationException) { }
        }
        bool MoveCommandCanExecute(object state)
        {
            return true;
        }
        #endregion


        public void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
