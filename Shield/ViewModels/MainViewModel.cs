using System.ComponentModel;
using Shield.Services;

namespace Shield.ViewModels
{
    public sealed class MainViewModel : INotifyPropertyChanged, IMainViewModel
    {
        private Sensors sensors;

        public event PropertyChangedEventHandler PropertyChanged;

        public MainViewModel()
        {
            
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public Sensors Sensors
        {
            get { return sensors; }
            set
            {
                sensors = value;
                OnPropertyChanged("Sensors");
            }
        }
    }

    public interface IMainViewModel
    {
        Sensors Sensors { get; set; }
    }
}