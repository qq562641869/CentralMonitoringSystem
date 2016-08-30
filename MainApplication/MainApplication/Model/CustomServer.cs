using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace MainApplication.Model
{
    public enum ClientStatus : byte { OffLine, OnLine, Connecting }

    public class CustomClientStatusEventArgs : EventArgs
    {
        public ClientStatus Status { get; set; }
    }

    public class CustomClientReciveEventArgs : EventArgs
    {
        public byte[] Data { get; set; }
    }

    public class CustomClient
    {
        IPEndPoint endPoint;

        public CustomClient(IPEndPoint endpoint, string name)
        {
            endPoint = endpoint;
            Name = name;
        }

        public string IPAddress
        {
            get
            {
                return endPoint?.Address.ToString();
            }
        }

        public string Name { get; private set; }

        public ClientStatus Status { get; private set; }

        public event EventHandler<CustomClientStatusEventArgs> StatusChanged;

        public event EventHandler<CustomClientReciveEventArgs> RecivedData;

        public int WriteData(byte[] data)
        {
            return 0;
        }
    }

    /// <summary>
    /// 对AsyncServerCore的封装
    /// </summary>
    public class CustomServer
    {
        List<CustomClient> clients;
        EventHandler<CustomClientStatusEventArgs> statusChangedHandler;
        EventHandler<CustomClientReciveEventArgs> reciveDataHandler;

        public CustomServer(int capacity = 128)
        {
            statusChangedHandler = new EventHandler<CustomClientStatusEventArgs>((s, e) =>
            {
                if (e.Status == ClientStatus.OffLine)
                {
                    clients.Remove(s as CustomClient);
                }
            });
            reciveDataHandler = new EventHandler<CustomClientReciveEventArgs>((s, e) =>
            {
            });

            clients = new List<CustomClient>(capacity);
            for (int n = 0; n < capacity; n++)
            {
                CustomClient client = new CustomClient(null, "Client " + n);
                client.StatusChanged += statusChangedHandler;
                client.RecivedData += reciveDataHandler;
                clients.Add(client);
            }
        }
    }
}