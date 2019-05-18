using System;
using System.Collections.Generic;
using System.Linq;
using FTCData;
using FTCData.Models;
using OPR;

namespace EventSim
{
    public class Engine
    {
        private readonly Options _options;
        private readonly ConsoleOutput _output;
        private readonly MatchRepository _matchRepo;
        private readonly TeamRepository _teamRepo;
        private readonly Scheduler _scheduler;
        public ConsoleOutput Output => _output;

        public Engine(Options options, ConsoleOutput output, MatchRepository matchRepo, TeamRepository teamRepo, Scheduler scheduler)
        {
            _options = options;
            _output = output;
            _matchRepo = matchRepo;
            _teamRepo = teamRepo;
            _scheduler = scheduler;
        }

        public Engine(FTCData.Models.Options options) : 
            this(options, new ConsoleOutput(options), new MatchRepository(options), new TeamRepository(), new Scheduler(options))
        {
        }

        public IList<EventStats> RunTrials(int trials)
        {
            _output.WriteStatus("Running " + trials + " trial(s)");

            IDictionary<int, Team> teams = null;
            IDictionary<int, Match> matches = null;

            // Set things up depending on whether we're using an EventKey or TeamPPMFile
            var eventStatsList = new List<EventStats>();
            if (!string.IsNullOrEmpty(_options.EventKey))
            {
                teams = PrepareTeamsFromEventKey(_options.DataFilesFolder, _options.EventKey);
                matches = PrepareMatchesFromFile(_options.DataFilesFolder, _options.EventKey, teams, true);
                SetTeamsPPM(teams, matches);
            }
            else if (!string.IsNullOrEmpty(_options.TeamPPMFile))
            {
                // override options if using TeamPPMFile
                if (string.IsNullOrEmpty(_options.EventKey))
                {
                    _options.RandomScheduling.UseFTCResults = false;
                    _options.RandomScheduling.UseFTCSchedule = false;
                }

                teams = PrepareTeamsFromPPMFile(_options.TeamPPMFile);
            }
            else
            {
                throw new InvalidOperationException("EventKey or TeamPPMFile must be provided in options");
            }

            for (int trial = 1; trial <= trials; trial++)
            {
                _output.WriteStatus("Trial " + trial);
                var eventStats = RunSimulation(teams, matches);
                eventStatsList.Add(eventStats);
            }

            var batchStats = _matchRepo.GetBatchStats(eventStatsList);
            WriteBatchStats(batchStats);

            return eventStatsList;
        }

        public EventStats RunSimulation(IDictionary<int, Team> teams, IDictionary<int, Match> matches = null)
        {
            var eventStats = new EventStats();

            // Check if we have matches if required
            if (_options.RandomScheduling.UseFTCResults && matches == null)
                throw new InvalidOperationException("Matches must be provided if using RandomScheduling.UseFTCResults");
            
            // run scheduling-specific simulation
            switch (_options.SchedulingModel)
            {
                case "RandomScheduling":
                    if (_options.RandomScheduling.UseFTCResults)
                        return RunActual(teams, matches);
                    else
                        return RunRandom(teams);

                case "SwissScheduling":
                    return RunSwiss(teams);

                default:
                    throw new NotImplementedException(_options.SchedulingModel + " is not a supported SchedulingModel. Must be RandomScheduling or SwissScheduling");
            }
        }

        public EventStats RunRandom(IDictionary<int, Team> teams)
        {
            var matches = new Dictionary<int, Match>();

            _output.WriteStatus("Scheduling " + _options.Rounds + " random rounds");
            _scheduler.AddNextRoundMatchesRandom(_options.Rounds, matches, teams);

            _output.WriteStatus("Setting match results for all rounds");
            _matchRepo.SetMatchResultsFromPPM(matches);

            _output.WriteStatus("Generating ranking");
            _matchRepo.SetRankings(matches, teams, _options.TBPMethod);
            WriteRankings(teams, _options.Output.FinalRankings);

            WriteMatchScores(matches);

            _output.WriteStatus("Generating event stats");
            var eventStats = _matchRepo.GetEventStats(teams, matches, _options.Output.TopXStats);
            WriteEventStats(eventStats);

            return eventStats;
        }

        public EventStats RunActual(IDictionary<int, Team> teams, IDictionary<int, Match> matches)
        {
            _output.WriteHeading("Running actual FTC event from matches and results");
 
            _output.WriteStatus("Generating Ranking");
            _matchRepo.SetRankings(matches, teams, _options.TBPMethod);
            WriteRankings(teams, _options.Output.FinalRankings);

            WriteMatchScores(matches);

            _output.WriteStatus("Generating event stats");
            var eventStats = _matchRepo.GetEventStats(teams, matches, _options.Output.TopXStats);
            WriteEventStats(eventStats);

            return eventStats;
        }

        public EventStats RunSwiss(IDictionary<int, Team> teams)
        {
            _output.WriteStatus("Running event with Swiss scheduling");

            var matches = new Dictionary<int, Match>();
            int schedulingRound = 1;
            int playingRound = 1;
            List<int> breaksAfter;

            // Set initial ranks for seeded start (will not be used if Random start)
            SetRankingByPPM(teams);

            // Get list of last match for each day
            _output.WriteStatus("Breaks after " + _options.SwissScheduling.BreaksAfter);
            if (_options.SwissScheduling.ScheduleAtBreaks)
                breaksAfter = _options.SwissScheduling.BreaksAfter.Split(',').Select(b => int.Parse(b)).OrderBy(b => b).ToList<int>();
            else
                breaksAfter = new List<int>();

            // Add a break for the last match of the last day
            breaksAfter.Add(_options.Rounds);

            // Run each day, creating the first rounds of the day's schedule at the start of each day
            for (int day = 1; day <= breaksAfter.Count; day++)
            {
                // Start of day.  Schedule the first round(s)

                int dayStartingRound = playingRound;
                int scheduleTo = breaksAfter[day - 1];

                // Figure out how many rounds to schedule at the start of day.  Consider the first and last day may be shorter
                int startingRoundsToSchedule = Math.Min(_options.SwissScheduling.RoundsToScheduleAtStart, scheduleTo - schedulingRound + 1);

                if (_options.SwissScheduling.SeedFirstRoundsOPR == false && day == 1)
                {
                    // It's day 1 and we're not seeding the swiss schedule.  Schedule first round(s) randomly.
                    _output.WriteStatus("Scheduling " + startingRoundsToSchedule + " round(s) randomly - Day " + day);
                    for (schedulingRound = 1; schedulingRound <= startingRoundsToSchedule; schedulingRound++)
                    {
                        _output.WriteStatus("Scheduling round " + schedulingRound + " randomly");
                        _scheduler.AddNextRoundMatchesRandom(1, matches, teams);
                        WriteMatchups(matches, schedulingRound);
                    }
                }
                else
                {
                    // Use team rank to seed Swiss schedule for day 2 and beyond, and day 1 if using seeding on day 1
                    _output.WriteStatus("Scheduling " + startingRoundsToSchedule + " round(s) Swiss  - Day " + day);

                    for (schedulingRound = dayStartingRound; schedulingRound < dayStartingRound + startingRoundsToSchedule; schedulingRound++)
                    {
                        // Set opponent pairing method.  May be different for opening matches.
                        string pairingMethod;
                        if (day == 1)
                            pairingMethod = _options.SwissScheduling.StartingRoundsOpponentPairingMethod;
                        else
                            pairingMethod = _options.SwissScheduling.OpponentPairingMethod;

                        _output.WriteStatus("Scheduling round " + schedulingRound + " Swiss " + pairingMethod);
                        _scheduler.AddNextRoundMatchesSwiss(matches, schedulingRound, teams, pairingMethod);
                        WriteMatchups(matches, schedulingRound);
                    }
                }

                // Work through each day.  We should already have a schedule set for at least the next round
                for (playingRound = dayStartingRound; playingRound <= breaksAfter[day - 1]; playingRound++)
                {
                    // Set the results for the current round of matches
                    _output.WriteStatus("Setting match results for round " + playingRound);
                    _matchRepo.SetMatchResultsFromPPM(matches, playingRound);

                    // Set the rankings
                    _matchRepo.SetRankings(matches, teams, _options.TBPMethod);
                    _output.WriteStatus("Generating Rankings");
                    WriteRankings(teams, _options.Output.RankingsAfterEachRound);

                    // Schedule upcoming matches...

                    // Calculate range of upcomming rounds to schedule for this day
                    int startSchedulingRound = schedulingRound;
                    int endSchedulingRound = playingRound + _options.SwissScheduling.ScheduleRoundsAhead;

                    // Limit the work-ahead to the last match of the day
                    if (endSchedulingRound > scheduleTo)
                        endSchedulingRound = scheduleTo;

                    // Schedule the next batch of rounds
                    for (schedulingRound = startSchedulingRound; schedulingRound <= endSchedulingRound; schedulingRound++)
                    {
                        _output.WriteStatus("Scheduling round " + schedulingRound + " Swiss " + _options.SwissScheduling.OpponentPairingMethod);
                        _scheduler.AddNextRoundMatchesSwiss(matches, schedulingRound, teams, _options.SwissScheduling.OpponentPairingMethod);
                        WriteMatchups(matches, schedulingRound);
                    }
                    // round has been played and future matches scheduled.  Repeat for each match of the day 
                }
                // day is complete.  Repeat for next day
            }

            // All rounds complete.  Write the final rankings
            WriteRankings(teams, _options.Output.FinalRankings);

            WriteMatchScores(matches);

            // Populate the event stats
            _output.WriteStatus("Generating event stats");
            var eventStats = _matchRepo.GetEventStats(teams, matches, _options.Output.TopXStats);
            WriteEventStats(eventStats);

            return eventStats;
        }

        public void SetRankingByPPM(IDictionary<int, Team> teams)
        {
            _teamRepo.ClearTeamStats(teams);

            var sortedTeams = teams.Values.OrderByDescending(t => t.PPM);
            int rank = 1;
            foreach (var team in sortedTeams)
            {
                team.Rank = rank;
                team.PPMRank = rank;
                rank++;
            }
        }

        public IDictionary<int, Team> PrepareTeamsFromEventKey(string folder, string eventKey)
        {
            _output.WriteStatus("Loading teams from " + eventKey);
            var teams = _teamRepo.GetTeamsFromTOAFile(folder, eventKey);
            _output.WriteStatus(string.Format("{0} teams loaded", teams.Count));
            return teams;
        }

        public IDictionary<int, Team> PrepareTeamsFromPPMFile(string teamPPMFile)
        {
            _output.WriteStatus("Loading teams from " + teamPPMFile);
            var teams = _teamRepo.GetTeamsFromPPMFile(teamPPMFile);
            _output.WriteStatus(string.Format("{0} teams loaded", teams.Count));
            return teams;
        }

        public void SetTeamsPPM(IDictionary<int, Team> teams, IDictionary<int, Match> matches)
        {
            _output.WriteStatus("Caculating team PPM as OPR from results file");
            var oprCalculator = new OPRHelper(_options);
            oprCalculator.SetTeamsOPR(teams, "PPM", matches);
        }

        public IDictionary<int, Match> PrepareMatchesFromFile(string folder, string eventKey, IDictionary<int, Team> teams, bool useActualScores = false)
        {
            _output.WriteStatus("Creating match schedule from " + eventKey);
            var matches = _matchRepo.GetMatchesFromTOAFile(teams, folder, eventKey, useActualScores);
            _output.WriteStatus(string.Format("{0} matches loaded", matches.Count));

            return matches;
        }

        private void WriteRankings(IDictionary<int, Team> teams, bool writeIt)
        {
            if (!writeIt)
                return;

            _output.WriteHeading("Results with TBP = " + _options.TBPMethod);

            _output.WriteHeading("Rank\tNumber\tPPM\tRP\tTBP\tOPR\tOPRRank\tOPRDif\tPPMRank\tPPMDif");
            foreach (var team in teams.Values.OrderBy(t => t.Rank))
            {
                _output.WriteLine(string.Format("{0}\t{1}\t{2:0.0}\t{3}\t{4}\t{5:0.0}\t{6}\t{7}\t{8}\t{9}", 
                    team.Rank, 
                    team.Number, 
                    team.PPM, 
                    team.RP, 
                    team.TBP, 
                    team.CurrentOPR, 
                    team.OPRRank, 
                    team.OPRRankDifference, 
                    team.PPMRank,
                    team.PPMRankDifference), true);
            }
        }

        private void WriteEventStats(EventStats eventStats)
        {
            if (!_options.Output.TrialStats)
                return;

            _output.WriteHeading("Teams\tMatches\tHigh\tLow\tAvg\tOPRDif\tTopX\tTopXDif\tOPRTopX\tPPMTopX");
            _output.WriteLine(String.Format(
                "{0}\t{1}\t{2}\t{3}\t{4:0.00}\t{5:0.00}\t{6}\t{7:0.00}\t{8}\t{9}",
                eventStats.TeamCount,
                eventStats.MatchCount,
                eventStats.HighScore,
                eventStats.LowScore,
                eventStats.AvgScore,
                eventStats.AvgOPRRankDifference,
                eventStats.TopX,
                eventStats.AvgTopXOPRRankDifference,
                eventStats.TopOPRInTopRank,
                eventStats.TopPPMInTopRank
                ), true);
        }

        private void WriteBatchStats(BatchStats eventStats)
        {
            if (!_options.Output.BatchStats)
                return;

            _output.WriteHeading("Teams\tMatches\tHigh\tLow\tAvg\tOPRDif\tTopX\tTopXDif\tOPRTopX\tPPMTopX\tTrials");
            _output.WriteLine(String.Format(
                "{0}\t{1}\t{2}\t{3}\t{4:0.00}\t{5:0.00}\t{6}\t{7:0.00}\t{8:0.00}\t{9}\t{10}",
                eventStats.TeamCount,
                eventStats.MatchCount,
                eventStats.HighScore,
                eventStats.LowScore,
                eventStats.AvgScore,
                eventStats.AvgOPRRankDifference,
                eventStats.TopX,
                eventStats.AvgTopXOPRRankDifference,
                eventStats.AvgTopOPRInTopRank,
                eventStats.AvgTopPPMInTopRank,
                eventStats.EventCount
                ), true);
        }

        private void WriteMatchScores(IDictionary<int, Match> matches)
        {
            if (!_options.Output.MatchScores)
                return;

            _output.WriteHeading("Match\tColor\tTeam1\tTeam2\tScore\tPPM1\tPPM2\tPPMtot\tScore-PPMtot");

            foreach (var match in matches.Values.OrderBy(m => m.MatchNumber))
            {
                _output.WriteLine(
                    string.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5:0.0}\t{6:0.0}\t{7:0.0}\t{8:0.0}",
                    match.MatchNumber,
                    "Red",
                    match.Red1.Number,
                    match.Red2.Number,
                    match.RedScore,
                    match.Red1.PPM,
                    match.Red2.PPM,
                    match.Red1.PPM + match.Red2.PPM,
                    match.RedScore - match.Red1.PPM - match.Red2.PPM
                    ), true);

                _output.WriteLine(
                    string.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5:0.0}\t{6:0.0}\t{7:0.0}\t{8:0.0}",
                    match.MatchNumber,
                    "Blue",
                    match.Blue1.Number,
                    match.Blue2.Number,
                    match.BlueScore,
                    match.Blue1.PPM,
                    match.Blue2.PPM,
                    match.Blue1.PPM + match.Blue2.PPM,
                    match.BlueScore - match.Blue1.PPM - match.Blue2.PPM
                    ), true);
            }
        }

        private void WriteMatchups(IDictionary<int, Match> matches, int round)
        {
            if (!_options.Output.Matchups)
                return;

            _output.WriteHeading("Match\tRed1\tRank(RP)\tRed2\tRank(RP)\tBlue1\tRank(RP)\tBlue2\tRank(RP)");

            foreach (var m in matches.Values.Where(m => m.Round == round).OrderBy(m => m.MatchNumber))
            {
                _output.WriteLine(
                            m.MatchNumber + "\t" +
                            m.Red1.Number + "\t" + m.Red1.Rank + " (" + m.Red1.RP + ") \t" +
                            m.Red2.Number + "\t" + m.Red2.Rank + " (" + m.Red2.RP + ") \t" +
                            m.Blue1.Number + "\t" + m.Blue1.Rank + " (" + m.Blue1.RP + ") \t" +
                            m.Blue2.Number + "\t" + m.Blue2.Rank + " (" + m.Red2.RP + ")", 
                            true);
            }
        }

    }
}
