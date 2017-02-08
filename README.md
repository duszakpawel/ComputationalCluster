# ComputationalCluster

Computational cluster dedicated to solve NP-Problems.

Nodes of this network communicate with the server(s) by TCP/IP protocol.

The solution contains all components of the system:

* communication server

and possible multiple components:

* task manager
* computational node
* backup server
* client node

To solve any problem you will need to provide specific assembly (.dll) under External libraries folder. It must contain class inherited from the TaskSolver class providing common interface of the problem (solving partial problem, merging partial solution, choosing final solution etc). As an example there is Dynamic Vehicle Routing Problem (DVRP) task solver provided by AlgorithmsSolver project.

To start computations, all the components must be in the same local network. Also you will need to setup IP address and PORT number of Communication Server for other components (Resources of projects). There is need of at least one instance of: communication server, task manager, computational node and client node to perform the process. Also you will need to provide specific class of client to provide the problem to the server. As an example there is provided client for DVRP problem which performs parsing the problem description from file to bytes. If the client is connected to the network, it can request to start computations of specific problem that he provide.

Any components can (or must if you dont provide correct parameters in resources for each of project) be started with initial parameters (check the ArgumentParserExtesions class for specific project).

```
projectname -parameter1 value1 -parameter2 -value2
```

There is also possibility to join some backup servers to the network; if the main server crashes or something then there is a chance that computations may continue and the main server will be switched to another one.
