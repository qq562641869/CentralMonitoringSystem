using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using System.Windows.Input;
using MainApplication.Model;
using System.Net;
using System.Linq;
using System.Net.Sockets;
using System.Collections.Generic;

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
        AsyncServerCore server;

        public MainViewModel()
        {
            server = new AsyncServerCore();
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
                    IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(SelectedAddress), 11000);
                    server.Init();
                    server.StartListening(endPoint);
                });
            }
        }

        public override void Cleanup()
        {
            base.Cleanup();
        }
    }
}