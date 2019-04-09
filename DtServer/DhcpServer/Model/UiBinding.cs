using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DhcpServer.Model
{
    public class UiBinding
    {
        public string Score { get; set; }
                
        public string ScoreLine
        {
            get
            {
                return Score;
            }
        }
    }
    public class UiBindingViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged = delegate { };

        private UiBinding defaultUiBinding = new UiBinding();
        public UiBinding DefaultUiBinding { get { return this.defaultUiBinding; } }
        private ObservableCollection<UiBinding> uiBindings = new ObservableCollection<UiBinding>();
        public ObservableCollection<UiBinding> UiBindings { get { return this.uiBindings; } }
        
        public string Action
        {
            set
            {
                this.uiBindings.Add(new UiBinding()
                {
                    Score = value
                });
                this.OnPropertyChanged();
            }
        }

        public void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            // Raise the PropertyChanged event, passing the name of the property whose value has changed.
            this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
