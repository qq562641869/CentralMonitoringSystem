using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using MainApplication.Model;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Input;

namespace MainApplication.ViewModel
{
    /// <summary>
    /// This class contains properties that the main View can data bind to.
    /// <para>
    /// Use the <strong>mvvminpc</strong> snippet to add bindable properties to this ViewModel.
    /// </para>
    /// <para>
    /// You can also use Blend to data bind with the tool's support.
    /// </para>
    /// <para>
    /// See http://www.galasoft.ch/mvvm
    /// </para>
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        CustomServer server;

        public MainViewModel()
        {
            server = new CustomServer();
        }

        public IEnumerable<string> IPAddressList
        {
            get
            {
                IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
                var buf = from add in ipHostInfo.AddressList
                          where add.AddressFamily == AddressFamily.InterNetwork
                          select add.ToString();
                return buf;
            }
        }

        public string SelectedAddress { get; set; }

        public ICommand StartServerCmd
        {
            get
            {
                return new RelayCommand(() =>
                {
                    ClientCore core = new ClientCore(100);
                });
            }
        }

        ObservableCollection<string> messageList;
        public ObservableCollection<string> MessageList
        {
            get
            {
                if (messageList == null)
                    messageList = new ObservableCollection<string>();
                return messageList;
            }
        }

        public override void Cleanup()
        {
            base.Cleanup();
        }
    }
}