using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CommunicationsUtils.NetworkInterfaces.Adapters
{
    public class NetworkStreamAdapter : INetworkStream
    {
        private static readonly ILog Log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private NetworkStream wrappedStream;

        public NetworkStreamAdapter(TcpClient client)
        {
            Client = client;
            wrappedStream = client.GetStream();
        }

        public void Close()
        {
            wrappedStream.Close();
        }

        public TcpClient Client { get; set; }

        public void Connect(string hostname, int port)
        {
            Client.Connect(hostname, port);
        }

        //TO DO
        public void Dispose()
        {
            return;
        }

        public int Read(byte[] buf, int count, bool flag)
        {
            int i = 0;
            int l = 0;
            int len = 0;
            try
            {
                l = wrappedStream.Read(buf, len, count - len);
                len += l;

                while (i < 6)
                {
                    if (wrappedStream.DataAvailable)
                    {
                        l = wrappedStream.Read(buf, len, count - len);
                        len += l;
                    }
                    Thread.Sleep(20);
                    i++;
                } 
            }
            catch (Exception)
            {
                Log.Debug("Something");
            }

            Log.DebugFormat("Read from connection: {0}", len);
            return len;
        }

        public void Write(byte[] buf, int count)
        {
            Log.DebugFormat("bytes written: {0}", count);
            wrappedStream.Write(buf, 0, count);
            wrappedStream.Flush();
        }
    }
}
