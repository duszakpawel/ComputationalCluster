﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunicationsUtils.Messages;
using UCCTaskSolver;

namespace AlgorithmSolvers.DVRPEssentials
{
    /// <summary>
    /// Task solver implementation for DVRP
    /// </summary>
    public class DVRPTaskSolver : TaskSolver
    {
        public DVRPTaskSolver(byte[] problemData) : base(problemData)
        {
        }

        ///
        ///
        ///SOLVE PROBLEM
        /// 
        /// 
        public override byte[] Solve(byte[] partialData, TimeSpan timeout)
        {
            if (partialData == null || partialData.Length == 0)
                return null;
            //przerób bajty na PartialProblemInstance
            var converter = new ProblemToBytesConverter();
            var partialInstance = (DVRPPartialProblemInstance)converter.FromBytesArray(partialData);
            var instance = (DVRPProblemInstance)converter.FromBytesArray(_problemData);

            //dla każdego samochodu (listy klientów mu przypisanej) sprawdz poprawność kombinacji 
            //(w sensie żądań)
            if (!demandsValid(instance))
                return converter.ToByteArray(solutionImpossible());

            var n = instance.Visits.Count;
            var s = instance.VehicleNumber;
            var i = partialInstance.MinimalSetCount;

            if (s * i > n)
                return converter.ToByteArray(solutionImpossible());

            partialInstance.PartialResult = double.MaxValue;

            solveInParallel(instance, ref partialInstance);

            return converter.ToByteArray(partialInstance);
        }

        private void solveInParallel(DVRPProblemInstance instance, ref DVRPPartialProblemInstance finalSolution)
        {
            var solQueue = new ConcurrentQueue<DVRPPartialProblemInstance>();
            //dwa wątki - lub jeden
            List<Thread> threads = new List<Thread>();
            foreach (var number in finalSolution.IgnoredSets)
            {
                var currSolution = new DVRPPartialProblemInstance();
                int i = finalSolution.MinimalSetCount;
                solutionCopyTo(finalSolution, ref currSolution);
                currSolution.IgnoredSets = new[] {number};
                var thread =
                    new Thread(t =>
                    {
                        if (i == 0)
                        {
                        //different behaviour (generate sets in loop)
                            solveForZero(instance, ref currSolution, number);
                        }
                        else if (i*instance.VehicleNumber == instance.Visits.Count)
                        {
                            instantMinimization(instance, ref currSolution);
                        } //nothing to do, but generate sets:
                        else
                            generateIgnoredSets(instance, ref currSolution, i, number);

                        solQueue.Enqueue(currSolution);
                    });

                threads.Add(thread);
                thread.Start();
            }
            foreach (var thread in threads)
            {
                thread.Join();
            }
            int k = 0;
            int min = 0;
            if (solQueue.IsEmpty)
            { 
                var sol2 = solutionImpossible();
                solutionCopyTo(sol2, ref finalSolution);
                return;
            }
            foreach (var sol in solQueue)
            {
                if (sol.PartialResult < finalSolution.PartialResult)
                {
                    min = k;
                }
                k++;
            }
            solutionCopyTo(solQueue.ElementAt(min), ref finalSolution);
        }

        private void solveForZero(DVRPProblemInstance instance, ref DVRPPartialProblemInstance currSolution, 
            int number)
        {
            var clast = instance.Visits.Count;

            var currVisits = new List<int>[instance.VehicleNumber];
            for (int e = 0; e < instance.VehicleNumber; e++)
            {
                currVisits[e] = new List<int>();
            }

            var lastIds = new int[instance.Visits.Count];
            for (var s = 0; s < instance.Visits.Count; s++)
            {
                    lastIds[s] = instance.Visits[s].Id;
            }

            solveForZeroRec(instance, ref currSolution, currVisits, lastIds, currSolution.MinimalSetCount, 
                number, clast, number, 0);
        }

        private void solveForZeroRec(DVRPProblemInstance instance, ref DVRPPartialProblemInstance solution,
          List<int>[] currVisits, int[] lastIds, int i, int number, int visitsToAssign, int setsToAssign, 
          int minSet)
        {
            if (setsToAssign > visitsToAssign)
                return;
            if (visitsToAssign == 0)
            {
                minimizeSolution(instance, ref solution, currVisits);
                return;
            }

            for (var k = minSet; k < number; k++)
            {
                if (k > 0 && currVisits[k - 1].Count == i)
                    return;
                if (setsToAssign == visitsToAssign && currVisits[k].Count != i)
                    continue;

                for (var t = 0; t < lastIds.Length; t++)
                {
                    if (lastIds[t] == -1)
                        continue;
                    if (currVisits[k].Count > i && currVisits[k].Last() > lastIds[t] )
                        continue;
                    if (k > 0 && currVisits[k - 1][0] > lastIds[t])
                        break;
                    var add = currVisits[k].Count == i ? (byte)1 : (byte)0;
                    currVisits[k].Add(lastIds[t]);
                    var tmp = lastIds[t];
                    lastIds[t] = -1;
                    solveForZeroRec(instance, ref solution, currVisits, lastIds, i,
                        number, visitsToAssign - 1, setsToAssign - add, k);
                    lastIds[t] = tmp;
                    currVisits[k].Remove(lastIds[t]);
                }
            }
        }

        private void solutionCopyTo(DVRPPartialProblemInstance a, ref DVRPPartialProblemInstance b)
        {
            if (a.IgnoredSets != null)
            {
                b.IgnoredSets = new int[a.IgnoredSets.Length];
                a.IgnoredSets.CopyTo(b.IgnoredSets, 0);
            }
            b.MinimalSetCount = a.MinimalSetCount;
            b.SolutionResult = a.SolutionResult;
            b.PartialResult = a.PartialResult;
            if (a.VisitIds == null)
                return;
            b.VisitIds = new int[a.VisitIds.GetLength(0)][];
            for (int i = 0; i < a.VisitIds.GetLength(0); i++)
            {
                b.VisitIds[i] = new int[a.VisitIds[i].Length];
                a.VisitIds[i].CopyTo(b.VisitIds[i], 0);
            }
        }

        private void generateIgnoredSets(DVRPProblemInstance instance, ref DVRPPartialProblemInstance solution,
            int i, int ignoredCount)
        {
            int[] ignoredSets = Enumerable.Repeat(-1, instance.VehicleNumber).ToArray();
            generateIgnoredSetsRec(instance, ref solution, i, ignoredCount, ignoredSets, 0);
        }

        private void generateIgnoredSetsRec(DVRPProblemInstance instance, 
            ref DVRPPartialProblemInstance solution, int i, int ignoredCount, int[] ignoredSets, int howmany)
        {
            if (howmany == ignoredCount)
            {
                generateSets(instance, i, ref solution, ignoredSets);
            }
            else
            {
                for (var j = 0; j < instance.VehicleNumber; j++)
                {
                    if (howmany > 0 && ignoredSets[howmany - 1] > j)
                        return;
                    ignoredSets[howmany] = j;
                    generateIgnoredSetsRec(instance, ref solution, i, ignoredCount, ignoredSets, howmany+1);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="instance">instance</param>
        /// <param name="i">minimal set count</param>
        /// <param name="solution">reference with visit ids permuted</param>
        /// <param name="ignoredCount">number of sets to ignore</param>
        private void generateSets(DVRPProblemInstance instance, int i, ref DVRPPartialProblemInstance solution,
            int[] ignoredSets)
        {
            //dodawaj klientów z nawrotami do zbiorów currVisits takich, że nie należą do y-greków
            //uważaj na powtórzenia
            var clast = instance.Visits.Count - i * instance.VehicleNumber;
            var cval = 0;
            var unignoredSets = Enumerable.Range(0, instance.VehicleNumber)
                .Where(x => !ignoredSets.Contains(x)).ToArray();
            var currVisits = new List<int>[instance.VehicleNumber];
            for (int e = 0; e < instance.VehicleNumber; e++)
            {
                currVisits[e] = new List<int>();
                for (int f = 0; f < solution.VisitIds[e].Length; f++)
                    currVisits[e].Add(solution.VisitIds[e][f]);
            }
            var lastIds = new int[instance.Visits.Count];
            for (var s = 0; s < instance.Visits.Count; s++)
            {
                if (currVisits.Any(x => x.Contains(instance.Visits[s].Id)))
                    lastIds[s] = -1;
                else
                    lastIds[s] = instance.Visits[s].Id;
            }

            generateSetsRec(instance, ref solution, i, currVisits, clast, cval, unignoredSets, 
                lastIds, 0, unignoredSets.Count());
        }

        private void generateSetsRec(DVRPProblemInstance instance, ref DVRPPartialProblemInstance solution,
            int i, List<int>[] currVisits, int clast, int cunign, int[] unignoredSets,  
            int[] lastIds, int minSet, int unignoredCount)
        {
            if (clast < unignoredCount - cunign)
                return;
            if (clast == 0 &&  cunign < unignoredCount)
                return;
            if (clast == 0)
            {
                minimizeSolution(instance, ref solution, currVisits);
                return;
            }

            for (var k = minSet; k < unignoredSets.Length; k++)
            {
                if (currVisits[unignoredSets[k]].Count == i && cunign == unignoredCount)
                    continue;
                if (k > 0 && currVisits[unignoredSets[k-1]].Count == i)
                    return;

                for (var t = 0; t < lastIds.Length; t++)
                {
                    if (lastIds[t] == -1)
                        continue;
                    if (currVisits[unignoredSets[k]].Count > i && currVisits[unignoredSets[k]].Last() > lastIds[t])
                        continue;
                    if (k > 0 && currVisits[unignoredSets[k-1]][0] > lastIds[t])
                        break;
                    var add = currVisits[unignoredSets[k]].Count == i ? (byte)1 : (byte)0;
                    currVisits[unignoredSets[k]].Add(lastIds[t]);
                    var tmp = lastIds[t];
                    lastIds[t] = -1;
                    generateSetsRec(instance, ref solution, i, currVisits, 
                        clast - 1, cunign + add, unignoredSets, lastIds, k, unignoredCount);
                    lastIds[t] = tmp;
                    currVisits[unignoredSets[k]].Remove(lastIds[t]);
                }
            }
        }

        private void minimizeSolution(DVRPProblemInstance instance, ref DVRPPartialProblemInstance solution,
            List<int>[] currVisits)
        {
            double newCost = 0f;
            var newSolution = new List<int>[instance.VehicleNumber];
            for (var j = 0; j < instance.VehicleNumber; j++)
            {
                var cvref = new List<int>();
                for (int e = 0; e < currVisits[j].Count; e++)
                    cvref.Add(currVisits[j][e]);

                var currCost = minimizePermutation(instance, ref cvref);
                if (currCost < 0f)
                {
                    return;
                }
                newCost += currCost;
                newSolution[j] = cvref;
            }
            if (newCost < solution.PartialResult)
            {
                solution.VisitIds = new int[instance.VehicleNumber][];
                for (int u = 0; u < instance.VehicleNumber; u++)
                    solution.VisitIds[u] = newSolution[u].ToArray();
                solution.PartialResult = newCost;
                solution.SolutionResult = SolutionResult.Successful;
            }
        }

        private void instantMinimization(DVRPProblemInstance instance, ref DVRPPartialProblemInstance solution)
        {
            double newCost = 0f;
            for (var j = 0; j < instance.VehicleNumber; j++)
            {
                var cvref = solution.VisitIds[j].ToList();
                var currCost = minimizePermutation(instance, ref cvref);
                if (currCost < 0f)
                {
                    var s = solutionImpossible();
                    solutionCopyTo(s, ref solution);
                    return;
                }
                newCost += currCost;
            }
            solution.PartialResult = newCost;
            solution.SolutionResult = SolutionResult.Successful;
        }

        /// <summary>
        /// minimalizacja kosztu
        /// </summary>
        /// <param name="instance">dane całego problemu</param>
        /// <param name="carVisits">permutacje dla samochodu - do minimalizacji kosztu</param>
        /// <returns>koszt minimalnej permutacji, -1 w przypadku nieistniejacej permutacji (dla warunkow czasowych)
        /// </returns>
        private double minimizePermutation(DVRPProblemInstance instance, ref List<int> carVisits)
        {
            if (carVisits.Count == 0)
                return 0f;
            //generacja wszystkich permutacji i sprawdzanie kosztu (zlozonosc n!)
            //permutacja generowana w rekursji
            var newVisits = new List<int>();
            var cost = double.MaxValue;
            minimizePermutationRec(instance, ref carVisits, 0,
                instance.VehicleCapacity, ref cost, newVisits);
            return cost == Double.MaxValue ? Double.MinValue : cost;
        }

        /// <summary>
        /// algorytm z nawrotami
        /// </summary>
        /// <param name="instance">instancja problemu</param>
        /// <param name="carVisits">minimalna permutacja klientów dla samochodu</param>
        /// <param name="currCost">koszt obecnie budowanej permutacji (odległość)</param>
        /// <param name="currCapacity">ile autku zostało w bagażniku towaru</param>
        /// <param name="minCost">koszt carVisits (tzn. minimalny)</param>
        /// <param name="newVisits">aktualnie budowana permutacja</param>
        private void minimizePermutationRec(DVRPProblemInstance instance, ref List<int> carVisits,
            double currCost, int currCapacity, ref double minCost, List<int> newVisits)
        {
            //zbudowalismy pewna permutacje, sprawdzenie czy jest dobra i ew. aktualizacja refów
            if (newVisits.Count == carVisits.Count)
            {
                if (routeImpossible(instance, newVisits))
                    return;
                //dodatkowo dodany koszt drogi powrotnej do depotu:
                double realCost = currCost + getDistanceCost(instance.Depots.Single().Location,
                    instance.Visits.Single(x => x.Id == newVisits.Last()).Location);

                if (realCost < minCost)
                {
                    minCost = realCost;
                    //deep copy
                    carVisits = new List<int>();
                    for (int i = 0; i < newVisits.Count; i++)
                        carVisits.Add(newVisits[i]);
                }
                return;
            }

            //rekursywna generacja permutacji
            for (var i = 0; i < carVisits.Count; i++)
            {
                if (currCapacity < 0)
                    return;

                var visitId = carVisits[i];

                if (newVisits.Contains(visitId)) continue;
                //if (newVisits.Count == 0 && i > (carVisits.Length + 1)/2)
                //    return;
                //if (newVisits.Count == 1 && i > (carVisits.Length + 1)/2)
                //    return;

                //ogólnie to paskudnie wygląda (bo visits to tylko inty do visit.Id)
                var visit = instance.Visits.Single(x => x.Id == visitId);
                var depot = instance.Depots.Single();

                var from = newVisits.Count == 0
                    ? depot.Location
                    : instance.Visits.Single(x => x.Id == newVisits.Last()).Location;

                var to = visit.Location;
                //dodawanie kosztu (w sensie dystansu)
                //autko nie ma towaru dla tego klienta, trzeba nawrócić do depota

                var lengthCost = getDistanceCost(from, to);
                newVisits.Add(visitId);
                var nextvToDepot = getDistanceCost(depot.Location, to);
                var curvToDepot = getDistanceCost(from, depot.Location);
                //uwzględnianie powrotu do depota

                if (from != depot.Location && currCapacity + visit.Demand <= 0)
                {
                    if (currCost +
                        curvToDepot +
                        2*nextvToDepot > minCost)
                    {
                        newVisits.Remove(visitId);
                        return;
                    }

                    minimizePermutationRec(instance, ref carVisits, currCost +
                        curvToDepot +
                        nextvToDepot,
                        instance.VehicleCapacity - Math.Abs(visit.Demand), ref minCost, newVisits);
                }
                else
                {
                    if (currCost + lengthCost + nextvToDepot > minCost)
                    {
                        newVisits.Remove(visitId);
                        return;
                    }
                    minimizePermutationRec(instance, ref carVisits, currCost + lengthCost,
                    currCapacity - Math.Abs(visit.Demand), ref minCost, newVisits);
                }

                newVisits.Remove(visitId);
            }
        }

        /// <summary>
        /// sprawdza, czy droga ma sens pod względem wymagań czasowych
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="newVisits"></param>
        /// <returns>true jeżeli droga jest chujowa</returns>
        private bool routeImpossible(DVRPProblemInstance instance, IReadOnlyList<int> newVisits)
        {
            //sprawdzenie czy sie zdazy dojechac z depotu do pierwszej wizyty
            var depot = instance.Depots.Single();
            var firstVisit = instance.Visits.Single(x => x.Id == newVisits[0]);
            var currTime = (double)depot.EarliestDepartureTime +
                getTimeCost(instance, depot.Location, firstVisit.Location);

            //przyjechaliśmy przed otwarciem depota. trzeba zaczekać na otwarcie
            //interesuje nas nie minimalizacja czasu, ale drogi, więc może tak być:
            if (currTime < firstVisit.AvailabilityTime)
                currTime = (double)firstVisit.AvailabilityTime;
            //if (currTime + getTimeCost(instance, firstVisit.Location, depot.Location) >= depot.LatestReturnTime)
            //    return true;

            var currCapacity = instance.VehicleCapacity;

            //sprawdzenie w pętli czy da się dojechać z i-1 wizyty do i-tej wizyty w dobrym czasie
            for (var i = 0; i < newVisits.Count - 1; i++)
            {
                var visit = instance.Visits.Single(x => x.Id == newVisits[i]);
                currTime += visit.Duration;
                var nextVisit = instance.Visits.Single(x => x.Id == newVisits[i + 1]);
                //if (currTime + getTimeCost(instance, visit.Location, depot.Location) >= depot.LatestReturnTime)
                //    return true;

                //autko nie ma już towaru, trzeba wrócić do depot:
                if (currCapacity + nextVisit?.Demand < 0)
                {
                    currTime += getTimeCost(instance, depot.Location, visit.Location);
                    currTime += getTimeCost(instance, depot.Location, nextVisit.Location);
                    currCapacity = instance.VehicleCapacity;
                }
                else
                {
                    currTime += getTimeCost(instance, visit.Location, nextVisit.Location);
                }
                //podobnie jak wyżej, poczekanie na otwarcie klienta
                if (currTime < nextVisit.AvailabilityTime)
                    currTime = (double)nextVisit.AvailabilityTime;
                //if (currTime + getTimeCost(instance, nextVisit.Location, depot.Location) >= depot.LatestReturnTime)
                //    return true;
            }

            //sprawdzenie, czy sie zdazy dojechac z ostatniej wizyty do depotu
            var lastVisit = instance.Visits.Single(x => x.Id == newVisits.Last());
            currTime += lastVisit.Duration + getTimeCost(instance, lastVisit.Location, depot.Location);
            return currTime > depot.LatestReturnTime;
        }

        /// <summary>
        /// wylicza koszt czasowy (t = s/V)
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        private double getTimeCost(DVRPProblemInstance instance, Location from, Location to)
        {
            return (getDistanceCost(from, to) / (double)instance.VehicleSpeed);
        }

        /// <summary>
        /// wylicza koszt odległościowy z from do to (euklidesowo)
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        private double getDistanceCost(Location from, Location to)
        {
            return Math.Sqrt(Math.Pow(from.X - to.X, 2) + Math.Pow(from.Y - to.Y, 2));
        }

        /// <summary>
        /// templatka do zwracania enuma z Impossible
        /// </summary>
        /// <returns></returns>
        private DVRPPartialProblemInstance solutionImpossible()
        {
            var converter = new ProblemToBytesConverter();
            return new DVRPPartialProblemInstance()
            {
                PartialResult = double.MaxValue,
                SolutionResult = SolutionResult.Impossible,
                VisitIds = null
            };
        }

        /// <summary>
        /// sprawdza czy klient nie ma zamówienia > vehicleCapacity (lepiej byłoby zrobić to w
        /// divide, ale to jest zbyt trudne)
        /// </summary>
        /// <param name="instance"></param>
        /// <returns>true jeżeli warunki są ok</returns>
        private bool demandsValid(DVRPProblemInstance instance)
        {
            return !instance.Visits.Any
                (x => x.Demand + instance.VehicleCapacity < 0);
        }

        ///
        ///
        ///DIVIDE PROBLEM
        /// 
        /// 
        public override byte[][] DivideProblem(int threadCount)
        {
            if (_problemData == null || _problemData.Length == 0)
                return null;

            var converter = new ProblemToBytesConverter();
            var instance = (DVRPProblemInstance)converter.FromBytesArray(_problemData);
            var problems = divideProblem(instance);
            return problems.Select(x => converter.ToByteArray(x)).ToArray();
        }

        private DVRPPartialProblemInstance[] divideProblem(DVRPProblemInstance instance)
        {
            var partialProblemInstances = new List<DVRPPartialProblemInstance>();

            for (var i = 0; i <= instance.Visits.Count / instance.VehicleNumber; i++)
            {
                //generacja permutacji dla i
                //generacja partial problemów - minimalingored, maximumignored
                generatePartialProblems(instance, i, ref partialProblemInstances);
            }

            return partialProblemInstances.ToArray();
        }

        private void generatePartialProblems(DVRPProblemInstance instance, int i, 
            ref List<DVRPPartialProblemInstance> partialProblemInstances)
        {
            if (i == 0)
            {
                var schema = new DVRPPartialProblemInstance()
                {
                    MinimalSetCount = 0,
                    PartialResult = double.MaxValue,
                    SolutionResult = SolutionResult.Impossible
                };
                appendProblems(instance, i, schema, ref partialProblemInstances);
                return;
            }
            var schemas = solvePossibleMinCountSets(instance, i);
            foreach (var schema in schemas)
            {
                if (instance.VehicleNumber*i == instance.Visits.Count)
                {
                    var partial = new DVRPPartialProblemInstance();
                    solutionCopyTo(schema, ref partial);
                    partial.IgnoredSets = new[] {instance.VehicleNumber};
                    partialProblemInstances.Add(partial);
                }
                else
                    appendProblems(instance, i, schema, ref partialProblemInstances);
            }
        }

        private void appendProblems(DVRPProblemInstance instance, int i, DVRPPartialProblemInstance schema,
            ref List<DVRPPartialProblemInstance> partialProblemInstances)
        {
            int k = Math.Max(1, instance.VehicleNumber*(i+1) - instance.Visits.Count);
            int e = instance.VehicleNumber - 1;
            while (k<=e)
            {
                var ignoredList = new List<int> {k};

                if (k!=e)
                    ignoredList.Add(e);

                var newProblem = new DVRPPartialProblemInstance();
                solutionCopyTo(schema, ref newProblem);
                newProblem.IgnoredSets = ignoredList.ToArray();
                partialProblemInstances.Add(newProblem);
                k++;
                e--;
            }
        }

        /// <summary>
        /// wyznacz wszystkie możliwe przydziały i klientów do wszystkich s samochodów(rekurencja)
        /// </summary>
        /// <param name="instance">problem instance</param>
        /// <param name="i">minimal set count</param>
        /// <returns></returns>
        private List<DVRPPartialProblemInstance> solvePossibleMinCountSets(DVRPProblemInstance instance, int i)
        {
            var solution = new List<DVRPPartialProblemInstance>();

            var currVisits = new List<int>[instance.VehicleNumber];
            for (var j = 0; j < instance.VehicleNumber; j++)
            {
                currVisits[j] = new List<int>();
            }
            var lastIds = new int[instance.Visits.Count];
            int ind = 0;

            foreach (var v in instance.Visits)
                lastIds[ind++] = v.Id;
            solvePossibleMinCountSetsRec(instance, i, 0, 0, ref solution, currVisits, lastIds);
            return solution;
        }

        private void solvePossibleMinCountSetsRec(DVRPProblemInstance instance, int i, int set, int j,
            ref List<DVRPPartialProblemInstance> solutions, List<int>[] currVisits, int[] lastIds)
        {
            if (j > i)
                throw new Exception("Something went wrong");
            if (j == i)
            {
                if (set < instance.VehicleNumber - 1)
                {
                    solvePossibleMinCountSetsRec(instance, i, set + 1, 0, ref solutions, currVisits, lastIds);
                    return;
                }
                else
                {
                    var newSol = new DVRPPartialProblemInstance();
                    newSol.PartialResult = double.MaxValue;
                    newSol.SolutionResult = SolutionResult.Impossible;
                    newSol.MinimalSetCount = i;
                    //uzyskaliśmy podział, przepisanie do szkieletu zadania
                    newSol.VisitIds = new int[currVisits.Length][];
                    for (var k = 0; k < currVisits.Length; k++)
                    {
                        newSol.VisitIds[k] = new int[currVisits[k].Count];
                        for (var s = 0; s < currVisits[k].Count; s++)
                            newSol.VisitIds[k][s] = currVisits[k][s];
                    }
                    solutions.Add(newSol);
                    return;
                }
            }

            for (var k = 0; k < lastIds.Length; k++)
            {
                if (lastIds[k] != -1)
                {
                    if ((set>0 && j==0 && currVisits[set-1].Last() > lastIds[k]) || 
                        (!(set == 0 && j == 0) && (instance.Visits[k].Id < currVisits[0][0]
                        || (currVisits[set].Count > 0 && currVisits[set].Last() > instance.Visits[k].Id)
                        || (currVisits[0].Count > 0 && currVisits[0][0] > instance.Visits[k].Id))))
                        continue;
                    var id = lastIds[k];
                    currVisits[set].Add(id);
                    lastIds[k] = -1;
                    solvePossibleMinCountSetsRec(instance, i, set, j + 1, ref solutions, currVisits, lastIds);
                    lastIds[k] = id;
                    currVisits[set].Remove(id);
                }
            }
        }

        ///
        ///
        ///MERGE SOLUTION
        /// 
        /// 
        public override byte[] MergeSolution(byte[][] solutions)
        {
            if (solutions == null || solutions.GetLength(0) == 0)
                return null;
            var converter = new ProblemToBytesConverter();
            var partialSolutions = solutions.Select
                (solution => (DVRPPartialProblemInstance)converter.FromBytesArray(solution)).ToList();
            var imin = 0;
            var minCost = double.MaxValue;
            for (var i = 0; i < solutions.GetLength(0); i++)
            {
                if (partialSolutions[i].PartialResult >= minCost) continue;
                imin = i;
                minCost = partialSolutions[i].PartialResult;
            }

            return solutions[imin];
        }

        public override string Name { get; }
    }
}
