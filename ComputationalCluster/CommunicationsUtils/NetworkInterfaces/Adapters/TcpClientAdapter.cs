using System;
using System.Net;
using System.Net.Sockets;

namespace CommunicationsUtils.NetworkInterfaces.Adapters
{
    public class TcpClientAdapter : ITcpClient
    {
        private Lazy<NetworkStreamAdapter> _adapter;
        private TcpClient _client;

        public TcpClientAdapter(TcpClient client)
        {
            _client = client;
            _adapter = new Lazy<NetworkStreamAdapter>(() => new NetworkStreamAdapter(_client));
        }

        public void Close()
        {
            _adapter.Value.Close();
        }

        public string GetAddress()
        {
            var endpoint = _client.Client.RemoteEndPoint;
            return (endpoint as IPEndPoint)?.Address.ToString();
        }

        public void Connect(string hostname, int port)
        {
            _client.Connect(hostname, port);
        }

        public INetworkStream GetStream()
        {
            return _adapter.Value;
        }
    }
}
