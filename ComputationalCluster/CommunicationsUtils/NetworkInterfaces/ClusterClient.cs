﻿using CommunicationsUtils.Messages;
using CommunicationsUtils.Serialization;
using CommunicationsUtils.NetworkInterfaces.Adapters;
using System;
using System.Threading;
using CommunicationsUtils.NetworkInterfaces.Factories;

namespace CommunicationsUtils.NetworkInterfaces
{
    /// <summary>
    /// Computational Cluster Client component.
    /// Sends messages and retrieves response messages.
    /// </summary>
    public class ClusterClient : IClusterClient
    {
        IClientAdapterFactory _tcpFactory;
        private readonly MessageToBytesConverter _converter = new MessageToBytesConverter();
        private string _address;
        private int _port;

        public string Address
        {
            get
            {
                return _address;
            }

            set
            {
                _address = value;
            }
        }

        public int Port
        {
            get
            {
                return _port;
            }

            set
            {
                _port = value;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="address">Address of the server</param>
        /// <param name="port">Port of the server</param>
        public ClusterClient(string address, int port, IClientAdapterFactory tcpFactory)
        {
            _address = address;
            _port = port;
            _tcpFactory = tcpFactory;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="requests">Requests to send</param>
        /// <returns>Responses from the server</returns>
        public Message[] SendRequests (Message[] requests)
        {
            byte[] requestsBytes;
            int count = _converter.MessagesToBytes(out requestsBytes, requests);

            ITcpClient _tcpClient = _tcpFactory.Create();
            _tcpClient.Connect(_address, _port);
            var networkStream = _tcpClient.GetStream();
            networkStream.Write(requestsBytes, count);
            var responseBytes = new byte[Properties.Settings.Default.MaxBufferSize];
            var len = networkStream.Read(responseBytes, Properties.Settings.Default.MaxBufferSize, true);
            return _converter.BytesToMessages(responseBytes, len);
        }

        public void ChangeListenerParameters(string address, int port)
        {
            this._address = address;
            this._port = port;
        }

    }
}