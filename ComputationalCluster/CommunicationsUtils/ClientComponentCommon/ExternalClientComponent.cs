using CommunicationsUtils.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunicationsUtils.NetworkInterfaces;
using log4net;

namespace CommunicationsUtils.ClientComponentCommon
{
    //will be abstract class with table of backup serv information in future
    //provides methods for internal+external client components (TM+CN+Comp. Client)
    public abstract class ExternalClientComponent
    {
        private static readonly ILog Log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        /// <summary>
        /// main tcp client wrapper for client node
        /// </summary>
        protected IClusterClient clusterClient;
        //backup info table reference
        private NoOperationBackupCommunicationServer[] _noOperationBackupsCommunication;
        protected string currentAddress;
        protected int currentPort;

        protected ExternalClientComponent(IClusterClient _clusterClient)
        {
            clusterClient = _clusterClient;
            currentAddress = clusterClient.Address;
            currentPort = clusterClient.Port;
        }

        /// <summary>
        /// basic, very general method - called in main() of component
        /// </summary>
        public abstract void Run();

        /// <summary>
        /// update _noOperationBackupsCommunication' list
        /// </summary>
        /// <param name="msg"></param>
        public void UpdateBackups(NoOperation msg)
        {
            _noOperationBackupsCommunication = msg.BackupCommunicationServers;
        }

        /// <summary>
        /// backup-consider message sending subroutine
        /// not considering posibility of simultaneous 
        /// failure of main and backup server (too improbable for this project)
        /// </summary>
        /// <param name="client">cliet</param>
        /// <param name="requests"></param>
        /// <returns></returns>
        public virtual Message[] SendMessages(IClusterClient client, Message[] requests)
        {
            Message[] responses;
            try
            {
                responses = client.SendRequests(requests);
            }
            catch (Exception)
            {
                if (_noOperationBackupsCommunication != null && _noOperationBackupsCommunication.Length == 0)
                {
                    throw new Exception("Critical client failure. Server timeout" +
                                        " and no _noOperationBackupsCommunication specified");
                }
                try
                {
                    Log.DebugFormat("Server not responding. Changing to params {0}, {1}", _noOperationBackupsCommunication[0].address, _noOperationBackupsCommunication[0].port);
                    client.ChangeListenerParameters(_noOperationBackupsCommunication[0].address, _noOperationBackupsCommunication[0].port);
                    currentPort = _noOperationBackupsCommunication[0].port;
                    currentAddress = _noOperationBackupsCommunication[0].address;
                    Thread.Sleep(5000);
                    responses = client.SendRequests(requests);
                }
                catch (Exception)
                {
                    throw new Exception("Critical client failure. Server timeout " +
                                        "and primary backup timeout");
                }
            }
            return responses;
        }
    }
}
