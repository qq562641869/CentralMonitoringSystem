using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using MainApplication.Model;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Windows.Data;
using System.Windows.Input;
using System;
using System.ComponentModel;
using System.Globalization;

namespace MainApplication.ViewModel
{
    public class BedNoConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((int)value > 0)
                return value.ToString();
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class OneMonitor : INotifyPropertyChanged
    {
        ClientCore clientCore;

        public OneMonitor(ClientCore client)
        {
            clientCore = client;
        }

        public int BedNo { get { return clientCore.BedNo; } }
        public string PatientName { get { return clientCore.PatientName; } }

        public event PropertyChangedEventHandler PropertyChanged;

        public void SayChanged()
        {
            PropertyChanged(this, new PropertyChangedEventArgs("PatientName"));
        }
    }

    public class MonitorViewModel : ViewModelBase
    {
        List<ClientCore> clientList;
        ObservableCollection<OneMonitor> monitorList;

        public MonitorViewModel()
        {
            clientList = new List<ClientCore>(128);
            monitorList = new ObservableCollection<OneMonitor>();

            for (int n = 0; n < 128; n++)
            {
                ClientCore client = new ClientCore(128);
                //set buffer...
                client.SetPatientName("Patient " + (n + 1));
                OneMonitor monitor = new OneMonitor(client);
                clientList.Add(client);
                monitorList.Add(monitor);
            }
        }

        #region Display Value
        public OneMonitor Bed1
        {
            get { return monitorList[0]; }
        }
        public OneMonitor Bed2
        {
            get { return monitorList[1]; }
        }
        public OneMonitor Bed3
        {
            get { return monitorList[2]; }
        }
        public OneMonitor Bed4
        {
            get { return monitorList[3]; }
        }
        #endregion

        #region Client Command
        public ICommand SetPatientCmd
        {
            get
            {
                return new RelayCommand(() =>
                {
                    clientList[0].SetPatientName("from View Model");
                    monitorList[0].SayChanged();
                });
            }
        }
        #endregion
    }
}
