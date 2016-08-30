using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MainApplication.Model
{
    /// <summary>
    /// This class creates a single large buffer which can be divided up 
    /// and assigned to SocketAsyncEventArgs objects for use with each 
    /// socket I/O operation.  
    /// This enables bufffers to be easily reused and guards against 
    /// fragmenting heap memory.
    /// </summary>
    class BufferManager
    {
        /// <summary>
        /// the total number of bytes controlled by the buffer pool
        /// </summary>
        readonly int totalBytes;

        /// <summary>
        /// the total number of bytes for each SocketAsyncEventArgs
        /// </summary>
        readonly int bufferSize;

        /// <summary>
        /// the underlying byte array maintained by the Buffer Manager
        /// </summary>
        byte[] buffer;

        Stack<int> m_freeIndexPool;
        int m_currentIndex;
        
        public BufferManager(int m_totalBytes, int m_bufferSize)
        {
            totalBytes = m_totalBytes;
            m_currentIndex = 0;
            bufferSize = m_bufferSize;
            m_freeIndexPool = new Stack<int>();
        }

        /// <summary>
        /// Assigns a buffer from the buffer pool to the 
        /// specified SocketAsyncEventArgs object
        /// </summary>
        /// <param name="args"></param>
        /// <returns>true if the buffer was successfully set</returns>
        public bool SetBuffer(SocketAsyncEventArgs args)
        {
            if (m_freeIndexPool.Count > 0)
            {
                args.SetBuffer(buffer, m_freeIndexPool.Pop(), bufferSize);
            }
            else
            {
                if ((totalBytes - bufferSize) < m_currentIndex)
                {
                    return false;
                }
                args.SetBuffer(buffer, m_currentIndex, bufferSize);
                m_currentIndex += bufferSize;
            }
            return true;
        }

        public void InitBuffer()
        {
            buffer = new byte[totalBytes];
        }

        public void Count()
        {
            int current = m_currentIndex;
            int max = totalBytes;
        }

        /// <summary>
        /// Removes the buffer from a SocketAsyncEventArg object.  
        /// This frees the buffer back to the buffer pool
        /// </summary>
        /// <param name="args"></param>
        public void FreeBuffer(SocketAsyncEventArgs args)
        {
            //get SocketAsyncEventArgs object's offset, for next use
            m_freeIndexPool.Push(args.Offset);
            args.SetBuffer(null, 0, 0);
        }
    }

    /// <summary>
    /// Represents a collection of reusable SocketAsyncEventArgs objects
    /// </summary>
    class SocketAsyncEventArgsPool
    {
        Stack<SocketAsyncEventArgs> m_pool;

        public SocketAsyncEventArgsPool(int capacity)
        {
            m_pool = new Stack<SocketAsyncEventArgs>(capacity);
        }

        public void Push(SocketAsyncEventArgs item)
        {
            if (item == null)
                return;
            lock (m_pool)
            {
                m_pool.Push(item);
            }
        }

        /// <summary>
        /// Removes a SocketAsyncEventArgs instance from the pool
        /// and returns the object removed from the pool
        /// </summary>
        /// <returns></returns>
        public SocketAsyncEventArgs Pop()
        {
            lock (m_pool)
            {
                return m_pool.Pop();
            }
        }
        
        /// <summary>
        /// The number of SocketAsyncEventArgs instances in the pool
        /// </summary>
        public int Count
        {
            get { return m_pool.Count; }
        }
    }

    class AsyncUserToken
    {
        public Socket Socket { get; set; }
        public DateTime LastActive { get; set; }
        public AsyncUserToken() { }
    }

    public class AsyncServerCore
    {
        /// <summary>
        /// all begin with it
        /// </summary>
        Socket listener;

        /// <summary>
        /// the maximum number of connections the sample is designed to handle simultaneously 
        /// </summary>
        readonly int maxConnections;

        /// <summary>
        /// buffer size to use for each socket I/O operation 
        /// </summary>
        readonly int reciveBufferSize;

        /// <summary>
        /// represents a large reusable set of buffers for all socket operations
        /// </summary>
        BufferManager bufferManager;

        /// <summary>
        /// pool of reusable SocketAsyncEventArgs objects for write, read and accept socket operations
        /// </summary>
        SocketAsyncEventArgsPool readWritePool;

        Semaphore maxAcceptedClients;

        int totalBytesRead;
        int totalByteWrite;
        int connectedSockets;

        public AsyncServerCore(int n_maxConnection = 128, int n_reciveBufferSize = 1024)
        {
            maxConnections = n_maxConnection;
            reciveBufferSize = n_reciveBufferSize;

            //for read and write 
            bufferManager = new BufferManager(maxConnections * reciveBufferSize * 2, reciveBufferSize);

            readWritePool = new SocketAsyncEventArgsPool(maxConnections);

            maxAcceptedClients = new Semaphore(maxConnections / 2, maxConnections);
        }

        /// <summary>
        /// Initializes the server by preallocating reusable buffers and 
        /// context objects.  These objects do not need to be preallocated 
        /// or reused, but it is done this way to illustrate how the API can 
        /// easily be used to create reusable objects to increase server performance.
        /// </summary>
        public void Init()
        {
            bufferManager.InitBuffer();

            //preallocate pool of SocketAsyncEventArgs objects
            for (int n = 0; n < maxConnections; n++)
            {
                SocketAsyncEventArgs readWriteEventArg = new SocketAsyncEventArgs();
                readWriteEventArg.Completed += (s, e) =>
                {
                    // determine which type of operation just completed and call the associated handler
                    switch (e.LastOperation)
                    {
                        case SocketAsyncOperation.Receive:
                            processReceive(e);
                            break;
                        case SocketAsyncOperation.Send:
                            processSend(e);
                            break;
                        default:
                            break;
                            throw new ArgumentException("The last operation completed on the socket was not a receive or send");
                    }
                };
                readWriteEventArg.UserToken = new AsyncUserToken();

                //assign a byte buffer from the buffer pool to the SocketAsyncEventArg object
                bufferManager.SetBuffer(readWriteEventArg);

                //add SocketAsyncEventArg to the pool
                readWritePool.Push(readWriteEventArg);
            }
            bufferManager.Count();
        }

        public void StartListening(IPEndPoint endpoint)
        {
            // Create a TCP/IP socket
            listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // Bind the socket to the local endpoint and listen for incoming connections
            try
            {
                listener.Bind(endpoint);
                //start the server with a listen backlog of 100 connections
                listener.Listen(100);

                startAccept(null);
            }
            catch (Exception e)
            {
                ;
            }
        }

        /// <summary>
        /// Begins an operation to accept a connection request from the client 
        /// </summary>
        /// <param name="acceptEventArg">The context object to use when issuing the accept operation on the server's listening socket</param>
        void startAccept(SocketAsyncEventArgs acceptEventArg)
        {
            if (acceptEventArg == null)
            {
                acceptEventArg = new SocketAsyncEventArgs();
                acceptEventArg.Completed += (s,e) => processAccept(e);
            }
            else
            {
                // socket must be cleared since the context object is being reused
                acceptEventArg.AcceptSocket = null;
            }

            //wait if more that maxAcceptedClients
            maxAcceptedClients.WaitOne();

            bool manualRaiseEvent = listener.AcceptAsync(acceptEventArg);
            //Returns false if the I/O operation completed synchronously
            //and the SocketAsyncEventArgs.Completed event on the e parameter will not be raised
            if (!manualRaiseEvent)
            {
                processAccept(acceptEventArg);
            }
        }
        
        private void processAccept(SocketAsyncEventArgs e)
        {
            Interlocked.Increment(ref connectedSockets);
            /*
                        // Get the socket for the accepted client connection and put it into the ReadEventArg object user token
                        SocketAsyncEventArgs readEventArgs = readWritePool.Pop();
                        AsyncUserToken token = readEventArgs.UserToken as AsyncUserToken;
                        token.Socket = e.AcceptSocket;

                                    // As soon as the client is connected, post a receive to the connection
                                    bool manualRaiseEvent = token.Socket.ReceiveAsync(readEventArgs);
                                    if (!manualRaiseEvent)
                                    {
                                        processReceive(readEventArgs);
                                    }
                        */
            SocketAsyncEventArgs sendEventArgs = readWritePool.Pop();
            AsyncUserToken token = sendEventArgs.UserToken as AsyncUserToken;
            token.Socket = e.AcceptSocket;
            bool manualRaiseEvent = token.Socket.SendAsync(sendEventArgs);
            if (!manualRaiseEvent)
            {
                processReceive(sendEventArgs);
            }

            // Accept the next connection request
            startAccept(e);
        }
        
        private void processReceive(SocketAsyncEventArgs e)
        {
            // check if the remote host closed the connection
            AsyncUserToken token = e.UserToken as AsyncUserToken;
            if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
            {
                //increment the count of the total bytes receive by the server
                Interlocked.Add(ref totalBytesRead, e.BytesTransferred);

                //填充buffer数据后, 设置数据长度
                //...
                string clientMsg = Encoding.ASCII.GetString(e.Buffer, e.Offset, e.BytesTransferred);
                //echo the data received back to the client
                byte[] buf = Encoding.ASCII.GetBytes(DateTime.Now + ": This is from Server. How You Doing?");
                int n = e.Offset;
                foreach(var b in buf)
                {
                    e.Buffer[n++] = b;
                }
                e.SetBuffer(e.Offset, buf.Length);
                //socket的send方法, 拷贝数据到基础系统的发送缓冲区, 然后由基础系统将缓冲区数据发生到另一端口
                //异步发送消息的拷贝, 将socket自带的buffer空间所有数据, 拷贝到基础系统发送缓冲区
                //执行线程无需IO等待, 立即返回
                bool manualRaiseEvent = token.Socket.SendAsync(e);
                if (!manualRaiseEvent)
                {
                    processSend(e);
                }
            }
            else
            {
                closeClientSocket(e);
            }
        }
        
        private void processSend(SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                /*
                SocketAsyncEventArgs sendEventArgs = readWritePool.Pop();
                AsyncUserToken token = sendEventArgs.UserToken as AsyncUserToken;
                token.Socket = e.AcceptSocket;
                */

                Interlocked.Add(ref totalByteWrite, e.BytesTransferred);
/*
                // done echoing data back to the client
                AsyncUserToken token = e.UserToken as AsyncUserToken;
                // read the next block of data send from the client
                bool manualRaiseEvent = token.Socket.ReceiveAsync(e);
                if (!manualRaiseEvent)
                {
                    processReceive(e);
                }
                */
            }
            else
            {
                closeClientSocket(e);
            }
        }

        private void closeClientSocket(SocketAsyncEventArgs e)
        {
            AsyncUserToken token = e.UserToken as AsyncUserToken;

            // close the socket associated with the client
            try
            {
                token.Socket.Shutdown(SocketShutdown.Send);
            }
            // throws if client process has already closed
            catch (Exception) { }
            token.Socket.Close();

            // decrement the counter keeping track of the total number of clients connected to the server
            Interlocked.Decrement(ref connectedSockets);
            maxAcceptedClients.Release();
            
            // Free the SocketAsyncEventArg so they can be reused by another client
            readWritePool.Push(e);
        }
    }
}