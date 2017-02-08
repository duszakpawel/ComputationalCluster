using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using CommunicationsUtils.Messages;
using CommunicationsUtils.Serialization;
using CommunicationsUtils.NetworkInterfaces.Adapters;

namespace CommunicationsUtils.NetworkInterfaces
{
    /// <summary>
    /// Computational Cluster Listener component (server-side)
    /// Provides configuration, sending and retrieving messages
    /// </summary>
    public class ClusterListener : IClusterListener
    {
        private ITcpListener tcpListener;
        private MessageToBytesConverter _converter = new MessageToBytesConverter();

        public ClusterListener(ITcpListener listener)
        {
            tcpListener = listener;
        }

        public void Start ()
        {
            tcpListener.Start();
        }

        public ITcpClient AcceptConnection()
        {
            ITcpClient currClient;
            try
            {
                currClient = tcpListener.AcceptTcpClient();
            }
            catch (Exception)
            {
                tcpListener.Start();
                currClient = tcpListener.AcceptTcpClient();
            }

            return currClient;
        }

        public Message[] GetRequests(ITcpClient client)
        {
            byte[] responseBytes;
            int len;

            var stream = client.GetStream();
            responseBytes = new byte[Properties.Settings.Default.MaxBufferSize];
            len = stream.Read(responseBytes, Properties.Settings.Default.MaxBufferSize, false);

            return _converter.BytesToMessages(responseBytes, len);
        }

        public void WriteResponses(ITcpClient client, Message[] responses)
        {
            byte[] requestsBytes;
            var count = _converter.MessagesToBytes(out requestsBytes, responses);

            var stream = client.GetStream();
            stream.Write(requestsBytes, count);
        }

        public void Stop ()
        {
            tcpListener.Stop();
        }
    }
}
