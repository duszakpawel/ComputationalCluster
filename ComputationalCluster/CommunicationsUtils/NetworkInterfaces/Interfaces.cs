using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CommunicationsUtils.NetworkInterfaces
{ 
    public interface ITcpClient
    {
        INetworkStream GetStream();
        void Connect(string hostname, int port);
        void Close();
        string GetAddress();
    }

    public interface INetworkStream : IDisposable
    {
        void Write(byte[] buf, int count);
        int Read(byte[] buf, int count, bool flag);
        void Close();
        TcpClient Client { get; set; }
        void Connect(string hostname, int port);
    }

    public interface ITcpListener
    {
        void Start();
        ITcpClient AcceptTcpClient();
        void Stop();
    }

    public interface ISocket
    {
        int Receive(byte[] requestBytes, int count);
        void Close();
        void Send(byte[] v, int count);
        string ExtractSocketAddress();
        void KillSocket();
    }
}
