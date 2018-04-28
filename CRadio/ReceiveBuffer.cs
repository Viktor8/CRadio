using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using System.Threading;
using System.Threading.Tasks;

namespace CRadio
{

    // bufferize stream of packets and add white noise if packets stop comes
    public class ReceiveBuffer : IDisposable
    {
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private Task _waitData;

        public ReceiveBuffer()
        {
            LastSeconds = new List<KeyValuePair<DateTime, byte[]>>();
            _waitData = Task.Factory.StartNew((token) =>
            {
                CancellationToken t = (CancellationToken)token;
                DataReceivedHandler(t);
            }, _cts.Token);    
        }

        public const int MAX_BUFFRIZED_SEC = 10;

        public int BufferezationTime
        {
            get;
            set;
        }

        public List<KeyValuePair<DateTime, byte[]>> LastSeconds
        {
            get;
            private set;
        }

        public TimeSpan BufferedTime
        {
            get
            {
                return TimeSpan.FromSeconds(LastSeconds.Count);
            }
        }

        private void DataReceivedHandler(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (PlayerConnection.DataRecived.WaitOne(1000))
                {
                    LastSeconds.Add(new KeyValuePair<DateTime, byte[]>(DateTime.Now, PlayerConnection.LastDataPacket));
                    if (LastSeconds.Count > MAX_BUFFRIZED_SEC)
                    {
                        LastSeconds.RemoveRange(0, LastSeconds.Count - MAX_BUFFRIZED_SEC);
                    }
                }
                else
                { 
                    // Add white noise;
                }
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }
}