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
    public class StateObject
    {
        // Client socket.
        public Socket workSocket = null;
        // Size of receive buffer.
        public const int BufferSize = 256;
        // Receive buffer.
        public byte[] buffer = new byte[BufferSize];
        // Received data string.
        public StringBuilder sb = new StringBuilder();
    }

    public class AsyncClientCore
    {
        public AsyncClientCore() { }

        static ManualResetEvent connectDone = new ManualResetEvent(false);
        private static ManualResetEvent sendDone = new ManualResetEvent(false);
        private static ManualResetEvent recvDone = new ManualResetEvent(false);

        static Socket client;

        public static void ConnectServer(IPAddress address)
        {
            // Establish the remote endpoint for the socket
            IPEndPoint remoteEP = new IPEndPoint(address, 11000);

            // Create a TCP/IP socket
            client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            client.BeginConnect(remoteEP, new AsyncCallback(connectCallback), client);
            connectDone.WaitOne();

            Task.Run(async () =>
            {
                while (true)
                {
                    //send data
                    sendDone.Reset();

                    
                    StateObject obj = new StateObject();
                    obj.workSocket = client;
                    client.BeginReceive(obj.buffer, 0, StateObject.BufferSize,0, new AsyncCallback(recvCallback), obj);

                    byte[] buf = Encoding.ASCII.GetBytes(DateTime.Now + ": This is from client.");
                    client.BeginSend(buf, 0, buf.Length, 0, new AsyncCallback(sendCallback), client);
                    sendDone.WaitOne();

                    await Task.Delay(1000);
                }
            });
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

        static void sendCallback(IAsyncResult ar)
        {
            // Retrieve the socket from the state object.
            Socket client = (Socket)ar.AsyncState;

            // Complete sending the data to the remote device.
            int bytesSent = client.EndSend(ar);

            sendDone.Set();
        }


        static void recvCallback(IAsyncResult ar)
        {
            // Retrieve the socket from the state object.
            StateObject obj = (StateObject)ar.AsyncState;

            // Complete sending the data to the remote device.
            int bytesRecv = obj.workSocket.EndReceive(ar);

            string serverMsg = Encoding.ASCII.GetString(obj.buffer, 0, bytesRecv);

            sendDone.Set();
        }

        public static void ShutDown()
        {
            client?.Shutdown(SocketShutdown.Both);
        }
    }
}
