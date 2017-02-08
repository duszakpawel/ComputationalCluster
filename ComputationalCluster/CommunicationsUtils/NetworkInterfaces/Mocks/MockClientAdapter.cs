﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommunicationsUtils.NetworkInterfaces.Mocks
{
    public class MockClientAdapter : ITcpClient
    {
        public void Close()
        {
        }

        public string GetAddress()
        {
            return null;
        }

        public void Connect(string hostname, int port)
        {
        }

        public INetworkStream GetStream()
        {
            return new MockStreamAdapter();
        }
    }
}
