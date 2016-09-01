using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MainApplication.Model
{
    public static class bytesEx
    {
        public static bool Contains(this byte[] source, byte[] data)
        {
            if (source == null || source.Length == 0 || data == null || data.Length == 0 || source.Length < data.Length)
                return false;
            int sourceLength = source.Length;
            int dataLength = data.Length;
            for (int n = 0; n <= sourceLength - dataLength; n++)
            {
                for (int m = 0; m < dataLength; m++)
                {
                    if (source[n + m] != data[m])
                        break;
                    if (m == dataLength - 1)
                        return true;
                }
            }
            return false;
        }
    }

    class CommandAndCallback
    {
        public byte[] TargetBytes { get; set; }
        public Action FoundCallback { get; set; }
    }

    public class ClientCore
    {
        readonly int bufferSize;
        Queue<byte> reciveBuffer;
        int reciveBufferCount = 0;
        
        /// <summary>
        /// socket读取数据后, 等待解析完毕才执行下一次读数据
        /// </summary>
        AutoResetEvent analyseComplete = new AutoResetEvent(false);

        int packErrorCount = 0;

        public ClientCore(int buffersize)
        {
            bufferSize = buffersize;
            reciveBuffer = new Queue<byte>(bufferSize);
        }

        /// <summary>
        /// 包头+命令+长度+data+校验
        /// 0x0a,0xed+0x__+0x__+data+0x__
        /// </summary>
        virtual public void analyseData()
        {
            //解析数据, 必须是从包头(0x0a,0xed)开始, 如果不是可以直接丢弃
            while (reciveBufferCount > 5)
            {
                //判断包头
                if (0x0a != reciveBuffer.ElementAt(0) && 0xed != reciveBuffer.ElementAt(1))
                {
                    reciveBuffer.Dequeue();
                    reciveBuffer.Dequeue();
                    reciveBufferCount -= 2;
                    continue;
                }
                //判断长度是否足够
                int length = reciveBuffer.ElementAt(3);
                if (reciveBufferCount < length)
                    break;
                //取出数据
                int cmd = reciveBuffer.ElementAt(2);
                for (int n = 0; n < length; n++)
                {
                    reciveBuffer.Dequeue();
                }
            }
            analyseComplete.Set();
        }

        /// <summary>
        /// 读取到
        /// </summary>
        /// <param name="data"></param>
        void addToBuffer(byte[] data)
        {
            foreach (var b in data)
            {
                reciveBuffer.Enqueue(b);
                reciveBufferCount++;
            }
            analyseData();
        }

        #region Monitor View
        public int BedNo { get; private set; }
        public string PatientName { get; private set; }
        public int HrValue { get; private set; }
        public int RrValue { get; private set; }
        public int Spo2Value { get; private set; }
        public int PrValue { get; private set; }
        public byte[] EcgWave1 { get; private set; }
        public byte[] EcgWave2 { get; private set; }
        public byte[] EcgWave3 { get; private set; }
        public int HrAlarmHigh { get; private set; }
        public int HrAlarmLow { get; private set; }
        public byte AlarmCode { get; private set; }
        #endregion

        #region Monitor Command
        public void SetPatientName(string name)
        {
            PatientName = name;
        }
        #endregion
    }
}
