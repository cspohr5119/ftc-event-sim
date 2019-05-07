using System;
using System.Collections.Generic;
using FTCData.Models;
using System.Linq;
using Matching;

namespace FTCData
{
    public class Scheduler
    {
        private readonly Random _rnd = new Random();
        private const int MAX_TRIES = 100000;

        private readonly TeamRepository _teamRepo;
        private readonly Options _options;
        private readonly MwMatch _matching;

        public Scheduler() : this(new TeamRepository())
        {
        }

        public Scheduler(TeamRepository teamRepo) : this(teamRepo, new Options())
        {
        }

        public Scheduler(TeamRepository teamRepo, Options options)
        {
            _teamRepo = teamRepo;
            _options = options;
            _matching = new MwMatch();
        }

        public int GetNextRoundNumber(IDictionary<int, Match> matches)
        {
            int round = 1;
            if (matches.Count > 0)
                round = matches.Values.Max(m => m.Round) + 1;
            return round;
        }

        public int GetNextMatchNumber(IDictionary<int, Match> matches)
        {
            int matchNumber = 1;
            if (matches.Count > 0)
                matchNumber = matches.Values.Max(m => matchNumber) + 1;
            return matchNumber;
        }

        public int AddNextRoundMatchesRandom(int roundsToAdd, IDictionary<int, Match> existingMatches, IDictionary<int, Team> teams)
        {
            // returns number of last round
            var matches = new Dictionary<int, Match>(existingMatches);
            int nextRound = GetNextRoundNumber(matches);
            int nextMatchNumber = GetNextMatchNumber(matches);
            int lastRound = nextRound + roundsToAdd - 1;
            int matchNumber = 1;
            int tries = 0;

            if (nextRound == 1)
                _teamRepo.ClearTeamStats(teams);

            for (int currentRound = nextRound; currentRound <= lastRound; currentRound++)
            {
                var teamNumbersToSchedule = teams.Keys.ToList<int>();
                int matchesPerRound = teams.Count / 4;

                // build random matchups
                var red1s = GetRandomTeams(matchesPerRound, teams, teamNumbersToSchedule);
                Team red2;
                Team blue1;
                Team blue2;

                foreach (Team red1 in red1s)
                {
                    // pick red2
                    while (true)
                    {
                        red2 = teams[teamNumbersToSchedule[_rnd.Next(teamNumbersToSchedule.Count)]];
                        if (tries++ > MAX_TRIES)
                            break;
                        if (red2.Scheduled >= currentRound)
                            continue;
                        if (red2 == red1)
                            continue;
                        break;
                    }

                    // pick blue1
                    while (true)
                    {
                        blue1 = teams[teamNumbersToSchedule[_rnd.Next(teamNumbersToSchedule.Count)]];
                        if (tries++ > MAX_TRIES)
                            break;
                        if (blue1.Scheduled >= currentRound)
                            continue;
                        if (blue1 == red1 || blue1 == red2)
                            continue;
                        break;
                    }

                    // pick blue2
                    while (true)
                    {
                        blue2 = teams[teamNumbersToSchedule[_rnd.Next(teamNumbersToSchedule.Count)]];
                        if (tries++ > MAX_TRIES)
                            break;
                        if (blue2.Scheduled >= currentRound)
                            continue;
                        if (blue2 == blue1 || blue2 == red1 || blue2 == red2)
                            continue;
                        break;
                    }

                    if (tries >= MAX_TRIES)
                        break;

                    // create and add the match
                    matches.Add(matchNumber, new Match(matchNumber, red1, red2, blue1, blue2, currentRound));
                    matchNumber++;
                }
                if (tries >= MAX_TRIES)
                    break;
            }

            if (tries >= MAX_TRIES)
            {
                // Try again from the beginning
                return AddNextRoundMatchesRandom(roundsToAdd, existingMatches, teams);
            }
            else
            {
                foreach(var match in matches.Values)
                {
                    existingMatches.Add(match.MatchNumber, match);
                }
                return lastRound;
            }
        }

        public IList<Team> GetRandomTeams(int numberOfTeams, IDictionary<int, Team> teams, IList<int> teamNumbersToSchedule)
        {
            var randomTeams = new List<Team>();
            for(int i = 0; i < numberOfTeams; i++)
            {
                var teamNumberToSchedule = teamNumbersToSchedule[_rnd.Next(teamNumbersToSchedule.Count)];
                teamNumbersToSchedule.Remove(teamNumberToSchedule);
                randomTeams.Add(teams[teamNumberToSchedule]);
            }

            return randomTeams;
        }

        public int AddNextRoundMatchesSwiss(Dictionary<int, Match> matches, int round, IDictionary<int, Team> teams)
        {
            // create a node for each team
            var nodeList = CreateNodesFromRankings(teams);

            // create groups based on rankings
            var groups = CreateGroups(nodeList);

            // create edges for each group
            var edges = new List<Edge>();
            foreach (var group in groups)
            {
                edges.AddRange(CreateEdgesForGroup(group));
            }

            // create edges across groups with increased cost
            int crossGroupAdder = teams.Count + 10;
            edges.AddRange(CreateEdgesBetweenNeighboringGroups(groups, crossGroupAdder));

            // add cost for teams that have opposed each other already
            int oppositionAdder = teams.Count * 100;
            AddCostForPriorOpponenets(edges, oppositionAdder);

            // get optimal 1v1 matchups
            var pairs = Get1v1Matchups(nodeList, edges);

            // now that we have 1v1 matchups, pair them optimally to create 2v2 matchups

            // create a node for each 1v1
            nodeList = CreateNodesFromPairs(pairs);

            // create groups based on rankings of top team
            groups = CreateGroups(nodeList);

            // create edges between pairs, by group
            edges = new List<Edge>();
            foreach (var group in groups)
            {
                edges.AddRange(CreateEdgesForGroup(group));
            }

            // create edges across groups with increased cost
            crossGroupAdder = teams.Count + 10;
            edges.AddRange(CreateEdgesBetweenNeighboringGroups(groups, crossGroupAdder));

            // add cost for teams that have aligned together already
            int alignmentAdder = teams.Count * 10;
            AddCostForPriorAlignment(edges, alignmentAdder);

            // add cost for teams that have opposed each other already
            AddCostForPriorOpponenets(edges, oppositionAdder);

            // Get paired matchup
            var pairMatchups = GetPairMatchups(nodeList, edges); // return Tuple<Tuple<Team, Team>, Tuple<Team, Team>>

            // Build and add matches to schedule
            Console.WriteLine("Matchups for Round " + round);

            int matchesPerRound = teams.Count / 4;
            int matchNumber = matches.Count + 2;

            foreach (var pairMatchup in pairMatchups)
            {
                var pair1 = pairMatchup.Item1;
                var pair2 = pairMatchup.Item2;

                var red1 = pair1.Item1;
                var red2 = pair2.Item1;

                var blue1 = pair1.Item2;
                var blue2 = pair2.Item2;

                var match = new Match(matchNumber, red1, red2, blue1, blue2, round);
                matches.Add(matchNumber, match);

                matchNumber++;
            }

            return round;
        }

        public IList<Node> CreateNodesFromRankings(IDictionary<int, Team> teams)
        {
            var nodes = new List<Node>();
            var rankings = teams.Values.OrderBy(t => t.Rank).ToList();
            foreach (Team team in rankings)
            {
                nodes.Add(new Node(team.Rank - 1, team));
            }
            return nodes;
        }
        public IList<Node> CreateNodesFromPairs(IList<Tuple<Team, Team>> pairs)
        {
            var nodes = new List<Node>();
            var orderedPairs = pairs.OrderBy(p => p.Item1.Rank).ToList();
            int id = 0;
            foreach (var pair in orderedPairs)
            {
                nodes.Add(new Node(id++, pair.Item1, pair.Item2));
            }
            return nodes;
        }


        public IList<List<Node>> CreateGroups(IList<Node> nodes)
        {
            var groups = new List<List<Node>>();
            int lastRP = 0;
            int currentRP = 0;
            var group = new List<Node>();

            foreach (var node in nodes.OrderBy(n => n.Team.Rank))
            {
                currentRP = node.Team.RP;
                if (node.Team.RP == lastRP)
                {
                    group.Add(node);
                }
                else
                {
                    // new rank encountered.  only start a new group if old group has an even number of items
                    if (group.Count % 2 == 0)
                    {
                        group = new List<Node>();
                        groups.Add(group);
                        lastRP = currentRP;
                    }
                    // if odd number, just add the item and continue. Will be even next time.
                    group.Add(node);
                }
            }

            return groups;
        }

        public IList<Edge> CreateEdgesForGroup(IList<Node> nodeList)
        {
            // expect nodeList to already be sorted in rank order
            var group = nodeList.ToList();
            var edges = new List<Edge>();

            /* The following algorithm creates edges between nodes, assigning costs as it does.
             * Later, the graph will be solved for minimum cost, so that nodes will be
             * matched optimally.  If all nodes can be matched at a cost of 1 each, they will.
             * Additional edges with higher costs will also be added between ranking groups to in case the
             * current group has no good options.
             * Later, costs will be increased later for teams who have played each other before in order
             * to avoid teams opposing each other twice in a tournament.
             *
             * Represent the list of nodes as a matrix to make it easier to visualize the edges (connections).
             * Each node connects to another node with a cost.
             * Ideal is the column index of the the most prefered (least cost).
             * Everything else is the distance from Ideal.  Ideal can be greater than max to make the math easier.
             * Don't create matches from a node to itself.
             *
             * Example Folding Matrix of 8 nodes
             *node1  node 2
             *       0 1 2 3 4 5 6 7 Ideal 
             *     +----------------------
             *   0 |   7 6 5 4 3 2 1  7
             *   1 |     5 4 3 2 1 2  6
             *   2 |       3 2 1 2 3  5
             *   3 |         1 2 3 4  4
             *   4 |           3 4 5  3
             *   5 |             5 6  2
             *   6 |               7  1
             *   7 |
             */

            /* Example Sliding Matrix of 8 nodes
             *node1  node 1
             *       0 1 2 3 4 5 6 7 Ideal
             *     +----------------------
             *   0 |   4 3 2 1 2 3 4  4
             *   1 |     4 3 2 1 2 3  5
             *   2 |       4 3 2 1 2  6
             *   3 |         4 3 2 1  7
             *   4 |           4 3 2  8
             *   5 |             4 3  9
             *   6 |               4  10
             *   7 |
             */

            int groupSize = group.Count;

            // Work each row
            for (int node1Idx = 0; node1Idx<groupSize - 1; node1Idx++)
            {
                // first, figure out where the ideal column is.  (may be greater than the group size)
                int ideal;  
                if (_options.SwissScheduling.OpponentPairingMethod == "Fold")
                    ideal = groupSize - node1Idx - 1;
                else // slide
                    ideal = (groupSize / 2)  + node1Idx;

                // Work the columns in the current row
                // Calculate each cost as the distance away from ideal, plus 1
                for (int node2Idx = node1Idx + 1; node2Idx<groupSize; node2Idx++)
                {
                    int cost = Math.Abs(node2Idx - ideal) + 1;
                    edges.Add(new Edge(group[node1Idx], group[node2Idx], cost));
                }
            }

            return edges;
        }

        public IList<Edge> CreateEdgesBetweenNeighboringGroups(IList<List<Node>> groups, int costAdder)
        {
            // connect nodes between groups with cost = costAdder + distance between nodes
            var edges = new List<Edge>();

            for (int i = 0; i < groups.Count - 1; i++)
            {
                var sourceGroup = groups[i];
                var targetGroup = groups[i + 1];

                foreach(var sourceNode in sourceGroup)
                {
                    int cost = costAdder;
                    foreach (var targetNode in targetGroup)
                    {
                        cost++;
                        edges.Add(new Edge(sourceNode, targetNode, cost));
                    }
                }
            }

            return edges;
        }

        public void AddCostForPriorOpponenets(IList<Edge> edges, int costAdder)
        {
            // Check existing edges and increase the cost if two teams have opposed each other before.
            // Note:  
            //  Each node is a 1v1 matchup, therefore opponents.  This means:
            //   Node1.Team is opposing both Node1.Team2 and Node2.Team2 amd
            //   Node2.Team is opposing both Node1.Team and Node2.Team.
            //  We only need to check for opponents between nodes, though, because we
            //  already optimized for intra-node opponents during 1v1 pairing and that
            //  has no bearing when trying to match 1v1 nodes to other 1v1 nodes for a match lineup.

            foreach (var edge in edges)
            {
                if (edge.Node1.Team.HasOpposed.ContainsKey(edge.Node2.Team.Number))
                {
                    edge.Cost += costAdder;
                }

                if (edge.Node1.Team2 != null) // this is a node of pairs  (doesn't apply for 1v1 pairing)
                {
                    if (edge.Node1.Team.HasOpposed.ContainsKey(edge.Node2.Team2.Number))
                    {
                        edge.Cost += costAdder;
                    }
                    if (edge.Node2.Team.HasOpposed.ContainsKey(edge.Node1.Team2.Number))
                    {
                        edge.Cost += costAdder;
                    }
                }
            }
        }

        public void AddCostForPriorAlignment(IList<Edge> edges, int costAdder)
        {
            // Check existing edges and increase the cost if two teams have aligned with each other before.
            // Note:  
            //  Each node is a 1v1 matchup, therefore opponents.  This means:
            //   Node1.Team and Node2.Team are alliance partners and
            //   Node1.Team2 and Node2.Team2 are also partners.

            foreach (var edge in edges)
            {
                if (edge.Node1.Team.HasAlignedWith.ContainsKey(edge.Node2.Team.Number))
                {
                    edge.Cost += costAdder;
                }

                if (edge.Node1.Team2.HasAlignedWith.ContainsKey(edge.Node2.Team2.Number))
                {
                    edge.Cost += costAdder;
                }
            }
        }

        public IList<Tuple<Team, Team>> Get1v1Matchups(IList<Node> nodeList, IList<Edge> edges)
        {
            // Get the matchup array using maximum weight matching (min cost not available)
            // Costs are converted into weights in the edge object.

            // this creates an array of arrays of edges.  (array of edge WeightArrays)
            // this is what the mwmatching script expects.
            int[][] edgeWeights = edges.Select(x => x.WeightArray).ToArray();
            int[] matchups = _matching.MaxWeightMatching(edgeWeights);

            var pairs = new List<Tuple<Team, Team>>();
            for (int i = 0; i < matchups.Length; i++)
            {
                int pair1Idx = i;
                int pair2Idx = matchups[i];

                Team topTeam;
                Team bottomTeam;

                if (nodeList[pair1Idx].Team.Rank < nodeList[pair2Idx].Team.Rank)
                {
                    topTeam = nodeList[pair1Idx].Team;
                    bottomTeam = nodeList[pair2Idx].Team;
                }
                else
                {
                    topTeam = nodeList[pair2Idx].Team;
                    bottomTeam = nodeList[pair1Idx].Team;
                }

                var pair = new Tuple<Team, Team>(topTeam, bottomTeam);
                
                // avoid adding duplicate tuples
                if (!pairs.Exists(p => p.Item1 == pair.Item1))
                    pairs.Add(pair);
            }

            return pairs;
        }

        public IList<Tuple<Tuple<Team, Team>, Tuple<Team, Team>>> GetPairMatchups(IList<Node> nodeList, IList<Edge> edges)
        {
            // Get the matchup array using maximum weight matching (min cost not available)
            // Costs are converted into weights in the edge object.

            // this creates an array of arrays of edges.  (array of edge WeightArrays)
            // this is what the mwmatching script expects.
            int[][] edgeWeights = edges.Select(x => x.WeightArray).ToArray();
            int[] matchups = _matching.MaxWeightMatching(edgeWeights);

            var pairMatchups = new List<Tuple<Tuple<Team, Team>, Tuple<Team, Team>>>();
            for (int pair1 = 0; pair1 < matchups.Length; pair1++)
            {
                int pair2 = matchups[pair1];
                if (matchups[pair1] > -1 && matchups[pair2] > -1)
                {
                    var pairMatchup = new Tuple<Tuple<Team, Team>, Tuple<Team, Team>>
                        (new Tuple<Team, Team>(nodeList[pair1].Team, nodeList[pair1].Team2),
                         new Tuple<Team, Team>(nodeList[pair2].Team, nodeList[pair2].Team2));

                    // avoid adding duplicate reversed tuples
                    if (!pairMatchups.Exists(p => p.Item1.Item1.Number == pairMatchup.Item2.Item1.Number))
                        pairMatchups.Add(pairMatchup);
                }
            }

            return pairMatchups;
        }

    }
}
