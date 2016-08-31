using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace MainApplication.Model
{
    public enum ClientStatus : byte { Closed, Opened, Opening, Closing }

    public class CustomClientStatusEventArgs : EventArgs
    {
        public ClientStatus Status { get; set; }
    }

    public class CustomClientReciveEventArgs : EventArgs
    {
        public byte[] Data { get; set; }
        public string Messsage { get; set; }
    }

    class ServerClientCmdHelper
    {
        #region Server To Client
        /// <summary>
        /// prototype: _ea__bea_ aka heartbeat
        /// </summary>
        public byte[] HeartBeat()
        {
            byte[] heartbeatHeader = new byte[] { 0x0e, 0xa0, 0x0b, 0xea, 0x00 };
            return packData(heartbeatHeader);
        }

        /// <summary>
        /// prototype: 5e__e_71_e aka servertime
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public byte[] ServerTime(DateTime time)
        {
            byte[] servertimeHeader = new byte[] { 0x5e, 0x00, 0xe0, 0x71, 0x0e };
            byte[] servertimeData = Encoding.ASCII.GetBytes(time.ToString("yyyy-MM-dd HH:mm:ss"));
            byte[] buf = servertimeHeader.Concat(servertimeData).ToArray();
            return packData(buf);
        }
        #endregion

        byte[] packData(byte[] orgin)
        {
            int length = orgin.Length;
            if (orgin == null || length < 1)
                return null;
            byte[] packed = new byte[length + 1];
            int index = 0;
            int sum = 0;
            foreach (var b in orgin)
            {
                packed[index++] = b;
                sum += (sum | b);
            }
            packed[index] = (byte)(sum % 256);
            return packed;
        }
    }

    public class ClientBase
    {
        IPEndPoint endPoint;

        string clientName = string.Empty;

        DateTime lastActive;

        readonly int readBufferSize;

        readonly int sendBufferSize;

        /// <summary>
        /// 写 由SocketAsyncEventArgsPool分配 buffer由BufferManager分配
        /// </summary>
        public SocketAsyncEventArgs ReadArgs;

        /// <summary>
        /// 读 由SocketAsyncEventArgsPool分配 buffer由BufferManager分配
        /// </summary>
        public SocketAsyncEventArgs SendArgs;

        EventHandler<SocketAsyncEventArgs> sendCompleteHandler;
        ManualResetEvent waitSend = new ManualResetEvent(false);

        EventHandler<SocketAsyncEventArgs> readCompleteHandler;
        ManualResetEvent waitRead = new ManualResetEvent(false);

        public event EventHandler<CustomClientReciveEventArgs> ClientStatusChanged;

        CancellationTokenSource cancelClient = new CancellationTokenSource();

        Queue<byte> reciveBuffer;

        public ClientBase(int readSize, int sendSize)
        {
            readBufferSize = readSize;
            sendBufferSize = sendSize;

            sendCompleteHandler = new EventHandler<SocketAsyncEventArgs>((s, e) =>
            {
                //执行close()后, 即使有数据, 也不处理
                if (Status != ClientStatus.Opened)
                    return;
                lastActive = DateTime.Now;
                if (e.LastOperation == SocketAsyncOperation.Send && e.SocketError == SocketError.Success)
                {
                }
                waitSend.Set();
            });
            readCompleteHandler = new EventHandler<SocketAsyncEventArgs>((s, e) =>
            {
                //执行close()后, 即使有数据, 也不处理
                if (Status != ClientStatus.Opened)
                    return;
                lastActive = DateTime.Now;
                if (e.LastOperation == SocketAsyncOperation.Receive && e.SocketError == SocketError.Success)
                {
                    int length = e.BytesTransferred;
                    if (length > 0)
                    {
                        int offset = e.Offset;
                        for (int n = 0; n < length; n++)
                        {
                            reciveBuffer.Enqueue(e.Buffer[offset + n]);
                        }
                        analyseData();
                    }
                }
                waitRead.Set();
            });

            ReadArgs = new SocketAsyncEventArgs();
            ReadArgs.Completed += readCompleteHandler;
            SendArgs = new SocketAsyncEventArgs();
            SendArgs.Completed += sendCompleteHandler;

            reciveBuffer = new Queue<byte>(readBufferSize * 4);
        }

        public string IPAddress
        {
            get
            {
                return endPoint?.Address.ToString();
            }
        }

        public string ClientName { get { return clientName; } }

        public ClientStatus Status { get; set; }

        public DateTime LastActive { get { return lastActive; } }
        
        byte[] heartBeat = new byte[] { 0x0e, 0xa0, 0x0b, 0xea, 0x00 };
        /// <summary>
        /// 发送心跳包, 不用等待执行完成
        /// </summary>
        /// <returns></returns>
        public Task SendHeartBeat()
        {
            return SendData(heartBeat);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        /// <returns>0:无数据 -1:取消 -2:超时 n:写长度</returns>
        public Task<int> SendData(byte[] data)
        {
            return Task.Run(() =>
            {
                if (data == null || data.Length == 0 || data.Length > sendBufferSize)
                    return 0;
                //将数据写入到缓存中
                int index = SendArgs.Offset;
                foreach (var b in data)
                {
                    SendArgs.Buffer[index++] = b;
                }
                SendArgs.SetBuffer(SendArgs.Offset, data.Length);
                //复位写入操作信号量
                waitSend.Reset();
                //执行写入前判断是否已经取消
                if (cancelClient.IsCancellationRequested)
                    return -1;
                //执行写入操作, 如果同步完成, 手动触发sendComplete事件
                if (!SendArgs.AcceptSocket.SendAsync(SendArgs))
                {
                    sendCompleteHandler(this, SendArgs);
                }
                //等待sendComplete完成
                if (!waitSend.WaitOne(10000))
                    return -2;
                return data.Length;
            }, cancelClient.Token);
        }
        
        public Task ReadData()
        {
            return Task.Run(() =>
            {
                while(true)
                {
                    //复位读取操作信号量
                    waitRead.Reset();
                    if (cancelClient.IsCancellationRequested)
                        break;
                    //ReadArgs.SetBuffer(ReadArgs.Offset, length);
                    if(!ReadArgs.AcceptSocket.ReceiveAsync(ReadArgs))
                    {
                        readCompleteHandler(this, ReadArgs);
                    }
                    waitRead.WaitOne();
                }
            }, cancelClient.Token);
        }

        /// <summary>
        /// 数据解析在此执行
        /// </summary>
        public virtual void analyseData()
        {
            int count = reciveBuffer.Count;
            var buf = new byte[count];
            for (int n = 0; n < count; n++)
            {
                buf[n] = reciveBuffer.Dequeue();
            }
            var msg = Encoding.ASCII.GetString(buf);
            ClientStatusChanged?.Invoke(this, new CustomClientReciveEventArgs() { Messsage = msg });
        }

        public virtual void Close()
        {
            cancelClient.Cancel();
            Status = ClientStatus.Closing;
            //
            //
            Status = ClientStatus.Closed;
        }
    }

    class ClientBufferManager
    {
        readonly int maxClientCount, readBufferLength, writeBufferLength, totalBufferLength;
        byte[] theBuffer;
        /// <summary>
        /// 当前读写位置
        /// </summary>
        int currentIndex;
        /// <summary>
        /// theBuffer中可用的数据起始位置
        /// </summary>
        Stack<int> freeIndexPool;

        public ClientBufferManager(int client, int readbuffer, int writebuffer)
        {
            maxClientCount = client;
            readBufferLength = readbuffer;
            writeBufferLength = writebuffer;
            totalBufferLength = maxClientCount * (readBufferLength + writeBufferLength);

            freeIndexPool = new Stack<int>(maxClientCount);
            theBuffer = new byte[totalBufferLength];
        }

        public bool SetBuffer(SocketAsyncEventArgs readArgs, SocketAsyncEventArgs writeArgs)
        {
            if (freeIndexPool.Count > 0)
            {
                int index = freeIndexPool.Pop();
                readArgs.SetBuffer(theBuffer, index, readBufferLength);
                writeArgs.SetBuffer(theBuffer, index + readBufferLength, writeBufferLength);
            }
            else
            {
                if (currentIndex + readBufferLength + writeBufferLength > totalBufferLength)
                    return false;
                readArgs.SetBuffer(theBuffer, currentIndex, readBufferLength);
                writeArgs.SetBuffer(theBuffer, currentIndex + readBufferLength, writeBufferLength);
                currentIndex = (readBufferLength + writeBufferLength);
            }
            return true;
        }

        public void ClearBuffer(SocketAsyncEventArgs readArgs, SocketAsyncEventArgs writeArgs)
        {
            //readbuffer和writebuffer必须是连在一起的
            if (readArgs.Offset + readBufferLength != writeArgs.Offset)
                throw new Exception("SetBuffer Error, Result FreeBuffer Error");
            freeIndexPool.Push(readArgs.Offset);
            readArgs.SetBuffer(null, 0, 0);
            writeArgs.SetBuffer(null, 0, 0);
        }
    }

    /// <summary>
    /// Represents a collection of reusable ClientBase objects
    /// </summary>
    class ClientPool
    {
        readonly int maxClientCount;
        Stack<ClientBase> freeClient;
        ClientBufferManager bufferManager;

        public ClientPool(int capacity)
        {
            maxClientCount = capacity;
            freeClient = new Stack<ClientBase>(maxClientCount);
            bufferManager = new ClientBufferManager(maxClientCount, 1024, 256);
            for (int n = 0; n < maxClientCount; n++)
            {
                freeClient.Push(new ClientBase(1024, 256));
            }
        }

        /// <summary>
        /// Get free client from pool, and then set the buffer
        /// </summary>
        /// <returns></returns>
        public ClientBase Pop()
        {
            lock (freeClient)
            {
                var client = freeClient.Pop();
                if (client != null)
                {
                    if (bufferManager.SetBuffer(client.ReadArgs, client.SendArgs))
                    {
                        client.ReadArgs.AcceptSocket = null;
                        client.SendArgs.AcceptSocket = null;
                        return client;
                    }
                    freeClient.Push(client);
                }
                return client;
            }
        }
    
        /// <summary>
        /// put used client back to pool, and the clear the buffer
        /// </summary>
        /// <param name="client"></param>
        public void Push(ClientBase client)
        {
            if (client == null)
                return;
            bufferManager.ClearBuffer(client.ReadArgs, client.SendArgs);
            freeClient.Push(client);
        }
    }

    /// <summary>
    /// 对AsyncServerCore的封装
    /// </summary>
    sealed class CustomServer
    {
        IPEndPoint localEndPoint;
        Socket serverCore;

        readonly int maxClientCount;

        Semaphore maxAcceptedClients;

        ClientPool clientPool;
        List<ClientBase> onlineClient;

        EventHandler<SocketAsyncEventArgs> acceptCompleteHandler;

        public event EventHandler<CustomClientReciveEventArgs> ClientStatusChanged;

        public CustomServer(int capacity = 128)
        {
            try
            {
                maxClientCount = capacity;

                serverCore = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                clientPool = new ClientPool(maxClientCount);
                onlineClient = new List<ClientBase>(maxClientCount);

                maxAcceptedClients = new Semaphore(maxClientCount, maxClientCount);

                acceptCompleteHandler = new EventHandler<SocketAsyncEventArgs>((s, e) =>
                {
                    if (e.SocketError == SocketError.Success)
                    {
                        var client = clientPool.Pop();
                        client.ReadArgs.AcceptSocket = e.AcceptSocket;
                        client.SendArgs.AcceptSocket = e.AcceptSocket;
                        client.Status = ClientStatus.Opened;
                        client.ClientStatusChanged += (cs, ce) =>
                        {
                            ClientStatusChanged?.Invoke(this, ce);
                        };
                        lock (onlineClient)
                        {
                            onlineClient.Add(client);
                        }
                        client.ReadData();
                    }
                    startAccept(e);
                });
            }
            catch (Exception e)
            {
                ;
            }
        }

        /// <summary>
        /// start listen and start accept
        /// </summary>
        public void StartServer(IPEndPoint endPoint)
        {
            localEndPoint = endPoint;
            serverCore.Bind(localEndPoint);
            //start the server with a listen backlog of 100 connections
            serverCore.Listen(100);

            startAccept(null);
           // startWatcher();
        }

        CancellationTokenSource cancelWatcher = new CancellationTokenSource();

        void startWatcher()
        {
            Task.Run(async () =>
            {
                DateTime deadLine = DateTime.Now;
                while (true)
                {
                    if (cancelWatcher.IsCancellationRequested)
                        break;

                    //超过8秒, 判定为已断开
                    deadLine = DateTime.Now.AddSeconds(-8);
                    var tooLong = from client in onlineClient
                                  where client.Status == ClientStatus.Opened && client.LastActive < deadLine
                                  select client;
                    if (tooLong != null && tooLong.Any())
                    {
                        Parallel.ForEach(tooLong, (client) =>
                        {
                            //执行close()时, 将clientBase.Status设置为Closing和Closed, 不会重复close
                            client.Close();
                            lock (onlineClient)
                            {
                                onlineClient.Remove(client);
                            }
                        });
                    }

                    //超过4秒, 发送心跳包
                    deadLine = DateTime.Now.AddSeconds(-4);
                    var littleLong = from client in onlineClient
                                     where client.LastActive < deadLine
                                     select client;
                    if (littleLong != null && littleLong.Any())
                    {
                        Parallel.ForEach(littleLong, (client) =>
                        {
                            client.SendHeartBeat();
                        });
                    }
                    await Task.Delay(2000);
                }
            }, cancelWatcher.Token);
        }

        void startAccept(SocketAsyncEventArgs args)
        {
            maxAcceptedClients.WaitOne();

            if (args == null)
            {
                args = new SocketAsyncEventArgs();
                args.Completed += acceptCompleteHandler;
            }
            else
            {
                //clear old socket
                args.AcceptSocket = null;
            }
            //if complete in sync mode
            if (!serverCore.AcceptAsync(args))
            {
                acceptCompleteHandler(null, args);
            }
        }
    }
}