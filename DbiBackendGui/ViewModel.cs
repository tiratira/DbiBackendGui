using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbiBackendGui
{
    public class ViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<NspFile> NspFiles { get; set; } = new ObservableCollection<NspFile>();
    }

    public class NspFile : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public string FileName { get; set; } = "";
        public string FullPath { get; set; } = "";
    }


}
