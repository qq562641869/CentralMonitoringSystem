using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SocketClient.Model
{
    public class AsyncClientCore
    {
        public AsyncClientCore() { }

        static ManualResetEvent connectDone = new ManualResetEvent(false);
        static Socket client;

        public static void ConnectServer(IPAddress address)
        {
            // Establish the remote endpoint for the socket
            IPEndPoint remoteEP = new IPEndPoint(address, 11000);

            // Create a TCP/IP socket
            Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            client.BeginConnect(remoteEP, new AsyncCallback(connectCallback), client);
            connectDone.WaitOne();
        }

        static void connectCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object
                Socket client = (Socket)ar.AsyncState;

                // Complete the connection
                client.EndConnect(ar);

                // Signal that the connection has been made
                connectDone.Set();
            }
            catch(Exception e)
            {
                ;
            }
        }

        public static void ShutDown()
        {
            client?.Shutdown(SocketShutdown.Both);
        }
    }
}
