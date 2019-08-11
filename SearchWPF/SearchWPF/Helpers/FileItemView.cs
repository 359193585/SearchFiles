using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SearchWPF
{
    public class FileItemView : INotifyPropertyChanged
    {

        //private Icon img = null;
        private string _name = string.Empty;
        private string _size = string.Empty;
        private string _creationTime = string.Empty;
        private string _modifyTime = string.Empty;
        private string _attributes = string.Empty;
        private string _fullPath = string.Empty;

        //public Icon ImageItem
        //{
        //    get => img;
        //    set
        //    {
        //        img = value;
        //        RaisePropertyChanged(nameof(ImageItem));
        //    }
        //}
                
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                RaisePropertyChanged(nameof(Name));
            }
        }
        
        public string Size
        {
            get => _size;
            set
            {
                _size = value;
                RaisePropertyChanged(nameof(Size));
            }
        }
        
        public string CreationTime
        {
            get => _creationTime;
            set
            {
                _creationTime = value;
                RaisePropertyChanged(nameof(CreationTime));
            }
        }
        
        public string ModifyTime
        {
            get => _modifyTime;
            set
            {
                _modifyTime = value;
                RaisePropertyChanged(nameof(ModifyTime));
            }
        }
        
        public string Attributes
        {
            get => _attributes;
            set
            {
                _attributes = value;
                RaisePropertyChanged(nameof(Attributes));
            }
        }
        
        public string FullPath
        {
            get => _fullPath;
            set
            {
                _fullPath = value;
                RaisePropertyChanged(nameof(FullPath));
            }
        }



        public void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
