﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CommunicationsUtils.Messages;
using CommunicationsUtils.NetworkInterfaces;
using log4net;
using Server.Data;
using Server.Interfaces;

namespace Server.MessageProcessing
{
    //TODO: if methods of primary and backup are the same, let them stay here
    //TODO: some of methods must be redefined in BackupMessageProcessor
    //TODO: some are unique for PrimaryServer
    /// <summary>
    /// Message processor for component.
    /// Contains implementations for handling different messages that occur in component.
    /// </summary>
    public abstract class MessageProcessor : IMessageProcessor
    {

        protected ConcurrentQueue<Message> SynchronizationQueue;

        private bool _doStatusWork = false;

        protected static readonly ILog Log =
            LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public List<Thread> StatusThreads { get; protected set; }

        protected IClusterListener ClusterListener;

        protected MessageProcessor(IClusterListener clusterListener, 
            ConcurrentQueue<Message> synchronizationQueue,
            IDictionary<int, ProblemDataSet> dataSets,
            IDictionary<int, ActiveComponent> activeComponents )
        {
            ClusterListener = clusterListener;
            SynchronizationQueue = synchronizationQueue;
            StatusThreads = new List<Thread>();
            //run all status threads (needed if backup is getting primary status)
            if (activeComponents != null)
            {
                foreach (var activeComponent in activeComponents)
                {
                    activeComponent.Value.StatusWatch.Start();
                    RunStatusThread(activeComponent.Key, activeComponents, dataSets);
                }
            }
        }

        public void Stop()
        {
            _doStatusWork = false;
        }

        private void StatusThreadWork(int who,
            IDictionary<int, ActiveComponent> activeComponents, IDictionary<int, ProblemDataSet> dataSets)
        {
            while (_doStatusWork)
            {
                var elapsed = activeComponents[who].StatusWatch.ElapsedMilliseconds;
                if (elapsed > 2*Properties.Settings.Default.Timeout)
                {
                    //backup timeout
                    if (activeComponents[who].ComponentType == ComponentType.CommunicationServer)
                    {
                        continue;
                    }
                    Message deregister = new Register()
                    {
                        Deregister = true,
                        DeregisterSpecified = true,
                        Id = (ulong)who,
                        IdSpecified = true
                    };
                    SynchronizationQueue.Enqueue(deregister);
                    Log.DebugFormat("TIMEOUT of {0}. Deregistering.", activeComponents[who].ComponentType);
                    DataSetOps.HandleClientMalfunction(activeComponents, who, dataSets);
                    activeComponents.Remove(who);
                    return;
                }
                Thread.Sleep((int)(2.5f * Properties.Settings.Default.Timeout));
            }
        }

        protected void RunStatusThread(int who,
            IDictionary<int, ActiveComponent> activeComponents, IDictionary<int, ProblemDataSet> dataSets)
        {
            //we assume that _noOperationBackupsCommunication are malfunction-free (specification issue)
            if (activeComponents[who].ComponentType == ComponentType.CommunicationServer)
                return;
            if (!_doStatusWork) _doStatusWork = true;
            var t = new Thread(() => StatusThreadWork(who, activeComponents, dataSets));
            t.Start();
            StatusThreads.Add(t);
        }

        /// <summary>
        /// Processes message.
        /// </summary>
        /// <param name="message">Instance of message to process</param>
        /// <param name="dataSets">Dictionary of problem data sets (maybe to update one of these or maybe not)</param>
        /// <param name="activeComponents">Dictionary of active components (maybe to update one of these or maybe not)</param>
        public virtual void ProcessMessage(Message message, IDictionary<int, ProblemDataSet> dataSets,
            IDictionary<int, ActiveComponent> activeComponents)
        {
            switch (message.MessageType)
            {
                case MessageType.DivideProblemMessage:
                    ProcessDivideProblemMessage(message.Cast<DivideProblem>(), dataSets, activeComponents);
                    break;
                case MessageType.NoOperationMessage:
                    ProcessNoOperationMessage(message.Cast<NoOperation>(), dataSets, activeComponents);
                    break;
                case MessageType.SolvePartialProblemsMessage:
                    ProcessSolvePartialProblemMessage(message.Cast<SolvePartialProblems>(), dataSets, activeComponents);
                    break;
                case MessageType.RegisterMessage:
                    ProcessRegisterMessage(message.Cast<Register>(), dataSets, activeComponents);
                    break;
                case MessageType.SolutionsMessage:
                    ProcessSolutionsMessage(message.Cast<Solutions>(), dataSets, activeComponents);
                    break;
                case MessageType.SolveRequestMessage:
                    ProcessSolveRequestMessage(message.Cast<SolveRequest>(), dataSets, activeComponents);
                    break;
                default:
                    return;
            }
        }

        /// <summary>
        /// Creates array of response messages for specified message.
        /// </summary>
        /// <param name="message">Instance of message to create response messages for</param>
        /// <param name="dataSets">Dictionary of problem data sets (maybe to update one of these or maybe not)</param>
        /// <param name="activeComponents">Dictionary of active components (maybe to update one of these or maybe not)</param>
        /// <param name="backups">_noOperationBackupsCommunication' list</param>
        /// <returns></returns>
        public virtual Message[] CreateResponseMessages(Message message, IDictionary<int, ProblemDataSet> dataSets,
            IDictionary<int, ActiveComponent> activeComponents, List<NoOperationBackupCommunicationServer> backups, string caddr)
        {
            switch (message.MessageType)
            {
                case MessageType.DivideProblemMessage:
                    return RespondDivideProblemMessage(message.Cast<DivideProblem>(), dataSets, activeComponents);
                case MessageType.NoOperationMessage:
                    return RespondNoOperationMessage(message.Cast<NoOperation>(), dataSets, activeComponents);
                case MessageType.SolvePartialProblemsMessage:
                    return RespondSolvePartialProblemMessage(message.Cast<SolvePartialProblems>(), dataSets,
                        activeComponents, backups);
                case MessageType.RegisterMessage:
                    return RespondRegisterMessage(message.Cast<Register>(), dataSets, activeComponents, backups, caddr);
                case MessageType.RegisterResponseMessage:
                    return RespondRegisterResponseMessage(message.Cast<RegisterResponse>(), dataSets, activeComponents);
                case MessageType.SolutionsMessage:
                    return RespondSolutionsMessage(message.Cast<Solutions>(), dataSets, activeComponents, backups);
                case MessageType.SolutionRequestMessage:
                    return RespondSolutionRequestMessage(message.Cast<SolutionRequest>(), dataSets, activeComponents,
                        backups);
                case MessageType.SolveRequestMessage:
                    return RespondSolveRequestMessage(message.Cast<SolveRequest>(), dataSets, activeComponents, backups);
                case MessageType.StatusMessage:
                    return RespondStatusMessage(message.Cast<Status>(), dataSets, activeComponents, backups);
                case MessageType.ErrorMessage:
                    return RespondErrorMessage(message.Cast<Error>(), dataSets, activeComponents);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        protected static void WriteControlInformation(Message message)
        {
            Log.DebugFormat("Message is dequeued and is being processed. Message type: " + message.MessageType);
        }

        protected static void WriteResponseMessageControlInformation(Message message, MessageType type)
        {
            Log.DebugFormat("Responding {0} message. Returning new {1} message in response.", message.MessageType, type);
        }

        protected virtual void ProcessDivideProblemMessage(DivideProblem message,
            IDictionary<int, ProblemDataSet> dataSets,
            IDictionary<int, ActiveComponent> activeComponents)
        { }

        protected virtual void ProcessNoOperationMessage(NoOperation message,
            IDictionary<int, ProblemDataSet> dataSets,
            IDictionary<int, ActiveComponent> activeComponents)
        { }

        protected virtual void ProcessSolvePartialProblemMessage(SolvePartialProblems message,
            IDictionary<int, ProblemDataSet> dataSets,
            IDictionary<int, ActiveComponent> activeComponents)
        {
            WriteControlInformation(message);
            //update dataset for given problemId
            //message from TM and only from it, so set partialSets array (it will be enough)
            if (!dataSets.ContainsKey((int) message.Id))
            {
                Log.DebugFormat("No problem ID {0} found for {1}", message.Id, message.MessageType);
                return;
            }
            var id = (int)message.Id;
            var ind = 0;
            if (dataSets[id].PartialSets == null)
                dataSets[id].PartialSets = new PartialSet[message.PartialProblems.Length];
            else
            {
                var tmp = new PartialSet[message.PartialProblems.Length + dataSets[id].PartialSets.Length];
                Array.Copy(dataSets[id].PartialSets, tmp, dataSets[id].PartialSets.Length);
                ind = dataSets[id].PartialSets.Length;
                dataSets[id].PartialSets = tmp;
            }
            for (var i = ind; i < dataSets[id].PartialSets.Length; i++)
            {
                dataSets[id].PartialSets[i] = new PartialSet()
                {
                    NodeId = 0,
                    PartialSolution = null,
                    PartialProblem = message.PartialProblems[i - ind],
                    Status = PartialSetStatus.Fresh
                };
            }
        }

        protected virtual void ProcessRegisterMessage(Register message,
            IDictionary<int, ProblemDataSet> dataSets,
            IDictionary<int, ActiveComponent> activeComponents)
        { }

        protected virtual void ProcessSolutionsMessage(Solutions message,
            IDictionary<int, ProblemDataSet> dataSets,
            IDictionary<int, ActiveComponent> activeComponents)
        {
            WriteControlInformation(message);
            //message delivered from TM or CN
            //in case of TM - it is final solution. adjust dataset for proper problemId
            //that means, just make only one partialSet with solutions as given from Solutions message
            //in case of CN - it is partial solution. adjust partialSet array element (for taskId) 
            //in proper problemId
            if (message.SolutionsList == null || message.SolutionsList.Length == 0)
                return;

            var key = (int)message.Id;
            if (!dataSets.ContainsKey(key))
                return;
            //this is from TM:
            if (message.SolutionsList.Length > 0 && message.SolutionsList[0].Type == SolutionsSolutionType.Final)
            {
                dataSets[key].PartialSets = new PartialSet[1];
                dataSets[key].PartialSets[0] = new PartialSet()
                {
                    NodeId = 0,
                    PartialProblem = null,
                    PartialSolution = message.SolutionsList[0],
                    Status = PartialSetStatus.Sent
                };
            }
            //this is from CN
            else
            {
                //in our system there could be only one solution from CN at a time
                //but at other groups, who knows?
                foreach (var solution in message.SolutionsList)
                {
                    var taskId = solution.TaskId;
                    foreach (var partialSet in dataSets[key].PartialSets.Where(partialSet => partialSet.PartialProblem.TaskId == taskId))
                    {
                        partialSet.PartialSolution = solution;
                        partialSet.Status = PartialSetStatus.Ongoing;
                        break;
                    }
                }
            }
        }

        protected virtual void ProcessSolveRequestMessage(SolveRequest message,
            IDictionary<int, ProblemDataSet> dataSets,
            IDictionary<int, ActiveComponent> activeComponents)
        { }

        protected virtual Message[] RespondDivideProblemMessage(DivideProblem message,
            IDictionary<int, ProblemDataSet> dataSets,
            IDictionary<int, ActiveComponent> activeComponents)
        {
            return null;
        }

        protected virtual Message[] RespondNoOperationMessage(NoOperation message,
            IDictionary<int, ProblemDataSet> dataSets,
            IDictionary<int, ActiveComponent> activeComponents)
        {
            return null;
        }

        protected virtual Message[] RespondSolvePartialProblemMessage(SolvePartialProblems message,
            IDictionary<int, ProblemDataSet> dataSets,
            IDictionary<int, ActiveComponent> activeComponents, List<NoOperationBackupCommunicationServer> backups)
        {
            return null;
        }

        protected virtual Message[] RespondRegisterMessage(Register message,
            IDictionary<int, ProblemDataSet> dataSets,
            IDictionary<int, ActiveComponent> activeComponents, List<NoOperationBackupCommunicationServer> backups, string caddr)
        {
            return null;
        }

        protected virtual Message[] RespondRegisterResponseMessage(RegisterResponse message,
            IDictionary<int, ProblemDataSet> dataSets,
            IDictionary<int, ActiveComponent> activeComponents)
        {
            return null;
        }

        protected virtual Message[] RespondSolutionsMessage(Solutions message,
            IDictionary<int, ProblemDataSet> dataSets,
            IDictionary<int, ActiveComponent> activeComponents, List<NoOperationBackupCommunicationServer> backups)
        {
            return null;
        }

        protected virtual Message[] RespondSolutionRequestMessage(SolutionRequest message,
            IDictionary<int, ProblemDataSet> dataSets,
            IDictionary<int, ActiveComponent> activeComponents, List<NoOperationBackupCommunicationServer> backups)
        {
            return null;
        }

        protected virtual Message[] RespondSolveRequestMessage(SolveRequest message,
            IDictionary<int, ProblemDataSet> dataSets,
            IDictionary<int, ActiveComponent> activeComponents, List<NoOperationBackupCommunicationServer> backups)
        {
            //sent by client node. create new issue in dataset with unique problemId,
            //send back NoOp + SolveRequestResponse with proper problemId
            var maxProblemId = dataSets.Count == 0 ? 1 : dataSets.Keys.Max() + 1;
            var newSet = new ProblemDataSet()
            {
                CommonData = message.Data,
                PartialSets = null,
                ProblemType = message.ProblemType,
                TaskManagerId = 0
            };
            dataSets.Add(maxProblemId, newSet);
            Log.DebugFormat("New problem, ProblemType={0}. Assigned id: {1}",
                message.ProblemType, maxProblemId);
            Log.DebugFormat("New problem, ProblemType={0}. Assigned id: {1}",
                message.ProblemType, maxProblemId);

            message.Id = (ulong)maxProblemId;
            message.IdSpecified = true;
            SynchronizationQueue.Enqueue(message);

            return new Message[]
            {
                new NoOperation()
                {
                    BackupCommunicationServers = backups.ToArray()
                },
                new SolveRequestResponse()
                {
                    Id = (ulong) maxProblemId
                }
            };
        }

        protected abstract Message[] RespondStatusMessage(Status message,
            IDictionary<int, ProblemDataSet> dataSets,
            IDictionary<int, ActiveComponent> activeComponents, List<NoOperationBackupCommunicationServer> backups);

        protected virtual Message[] RespondErrorMessage(Error message,
            IDictionary<int, ProblemDataSet> dataSets,
            IDictionary<int, ActiveComponent> activeComponents)
        {
            //practically nothing to do
            //warn logger, print something on console if verbose
            Log.DebugFormat("Error message acquired. Type={0}, Message={1}",
                message.ErrorType, message.ErrorMessage);
            Log.DebugFormat("Error message acquired. Type={0}, Message={1}",
                message.ErrorType, message.ErrorMessage);
            return null;
        }
    }
}