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

        public void Init()
        {
            bufferManager.InitBuffer();

            //preallocate pool of SocketAsyncEventArgs objects
            for (int n = 0; n < maxConnections; n++)
            {
                SocketAsyncEventArgs readWriteEventArg = new SocketAsyncEventArgs();
                readWriteEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(io_Completed);
                readWriteEventArg.UserToken = new AsyncUserToken();

                //assign a byte buffer from the buffer pool to the SocketAsyncEventArg object
                bufferManager.SetBuffer(readWriteEventArg);

                //add SocketAsyncEventArg to the pool
                readWritePool.Push(readWriteEventArg);
            }
        }


        
        static CancellationTokenSource endTaks = new CancellationTokenSource();


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
                acceptEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(accept_Completed);
            }
            else
            {
                // socket must be cleared since the context object is being reused
                acceptEventArg.AcceptSocket = null;
            }

            maxAcceptedClients.WaitOne();
            bool willRaiseEvent = listener.AcceptAsync(acceptEventArg);
            if (!willRaiseEvent)
            {
                ProcessAccept(acceptEventArg);
            }
        }

        void io_Completed(object sender, SocketAsyncEventArgs e)
        {
            // determine which type of operation just completed and call the associated handler
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Receive:
                    //  ProcessReceive(e);
                    break;
                case SocketAsyncOperation.Send:
                    //   ProcessSend(e);
                    break;
                default:
                    break;
                    //throw new ArgumentException("The last operation completed on the socket was not a receive or send");
            }

        }

        /// <summary>
        /// This method is the callback method associated with Socket.AcceptAsync operations and is invoked when an accept operation is complete
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void accept_Completed(object sender, SocketAsyncEventArgs e)
        {

        }

        public static void CloseServer()
        {
            endTaks.Cancel();
        }
    }
}