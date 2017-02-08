using CommunicationsUtils.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommunicationsUtils.NetworkInterfaces
{
    public interface IClusterListener
    {
        void Start();
        ITcpClient AcceptConnection();
        Message[] GetRequests(ITcpClient client);
        void WriteResponses(ITcpClient client, Message[] responses);
        void Stop();
    }
}
