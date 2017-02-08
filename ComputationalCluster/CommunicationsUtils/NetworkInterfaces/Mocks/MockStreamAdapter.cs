using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CommunicationsUtils.NetworkInterfaces.Mocks
{
    public class MockStreamAdapter : INetworkStream
    {
        public void Close()
        {
            return;
        }

        public TcpClient Client { get; set; }
        
        public void Connect(string hostname, int port)
        {
            return;
        }

        public void Dispose()
        {
            return;
        }

        public int Read(byte[] buf, int count, bool flag)
        {
            return 0;
        }

        public void Write(byte[] buf, int count)
        {
            return;
        }
    }
}
