using System.Net.Sockets;

namespace CommunicationsUtils.NetworkInterfaces.Adapters
{
    public class TcpListenerAdapter : ITcpListener
    {
        private TcpListener wrappedTcpListener;

        public TcpListenerAdapter(TcpListener listener)
        {
            wrappedTcpListener = listener;
        }

        public ITcpClient AcceptTcpClient()
        {
            return new TcpClientAdapter(wrappedTcpListener.AcceptTcpClient());
        }

        public void Start()
        {
            wrappedTcpListener.Start();
        }

        public void Stop()
        {
            wrappedTcpListener.Stop();
        }
    }
}
