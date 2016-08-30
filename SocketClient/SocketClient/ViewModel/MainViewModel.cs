using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using System.Windows.Input;
using System.Net;
using System.Linq;
using System.Net.Sockets;
using System.Collections.Generic;
using SocketClient.Model;

namespace SocketClient.ViewModel
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
        /// <summary>
        /// Initializes a new instance of the MainViewModel class.
        /// </summary>
        public MainViewModel()
        {
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

        public ICommand ConnectServerCmd
        {
            get
            {
                return new RelayCommand(() =>
                {
                    AsyncClientCore.ConnectServer(IPAddress.Parse(SelectedAddress));
                });
            }
        }

        public override void Cleanup()
        {
            AsyncClientCore.ShutDown();
            base.Cleanup();
        }
    }
}