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

        public Engine(FTCData.Models.Options options) : this(options, new ConsoleOutput(options), new MatchRepository(options), new TeamRepository(), new Scheduler())
        {
        }

        public IList<EventStats> RunTrials(int trials)
        {
            _output.WriteStatus("Running " + trials + " trial(s)");

            var eventStatsList = new List<EventStats>();
            var teams = PrepareTeamsFromFile(_options.DataFilesFolder, _options.EventKey);
            var matches = PrepareMatchesFromFile(_options.DataFilesFolder, _options.EventKey, teams, true);
            SetTeamsOPR(teams, matches);

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

        public EventStats RunSimulation(IDictionary<int, Team> teams, IDictionary<int, Match> matches)
        {
            var eventStats = new EventStats();

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
            _matchRepo.SetMatchResultsFromOpr(matches);

            _output.WriteStatus("Generating ranking");
            _matchRepo.SetRankings(matches, teams, _options.TBPMethod);
            WriteRankings(teams, _options.Output.FinalRankings);

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

            _output.WriteStatus("Generating event stats");
            var eventStats = _matchRepo.GetEventStats(teams, matches, _options.Output.TopXStats);
            WriteEventStats(eventStats);

            return eventStats;
        }

        public EventStats RunSwiss(IDictionary<int, Team> teams)
        {
            _output.WriteStatus("Running event with Swiss scheduling");

            if (_options.SwissScheduling.SeedFirstRoundsOPR)
                throw new NotImplementedException("SeedFirstRoundOPR is not yet implemented.");

            if (_options.SwissScheduling.SchduleAtBreaks)
                throw new NotImplementedException("SchduleAtBreaks is not yet implemented.");

            var matches = new Dictionary<int, Match>();

            _output.WriteStatus("Scheduling " + _options.SwissScheduling.RoundsToScheduleAtStart + " round(s) randomly");
            for (int round = 1; round <= _options.SwissScheduling.RoundsToScheduleAtStart; round++)
            {
                _output.WriteStatus("Scheduling round " + round + " randomly");
                _scheduler.AddNextRoundMatchesRandom(1, matches, teams);
                WriteMatchups(matches, round);

                _output.WriteStatus("Setting match results for round " + round);
                _matchRepo.SetMatchResultsFromOpr(matches);
                _matchRepo.SetRankings(matches, teams, _options.TBPMethod);
            }


            int currentRound = _options.SwissScheduling.RoundsToScheduleAtStart + 1;
            for (int round = currentRound; round <= _options.Rounds; round++)
            {
                _output.WriteStatus("Scheduling round " + round + " with Swiss algorithm");
                _scheduler.AddNextRoundMatchesSwiss(matches, round, teams);

                _output.WriteStatus("Setting match results for round " + round);
                _matchRepo.SetMatchResultsFromOpr(matches);
                WriteMatchups(matches, round);

                _output.WriteStatus("Generating Rankings");
                _matchRepo.SetRankings(matches, teams, _options.TBPMethod);
                WriteRankings(teams, _options.Output.RankingsAfterEachRound);

            }

            WriteRankings(teams, _options.Output.FinalRankings);

            _output.WriteStatus("Generating event stats");
            var eventStats = _matchRepo.GetEventStats(teams, matches, _options.Output.TopXStats);
            WriteEventStats(eventStats);

            return eventStats;
        }

        public IDictionary<int, Match> PrepareFirstRoundMatches(IDictionary<int, Team> teams)
        {
            // prepare 1st round matches accoring to options
            var matches = new Dictionary<int, Match>();
            
            if (_options.SchedulingModel == "RandomScheduling")
            {
                if (_options.RandomScheduling.UseFTCSchdule)
                {
                    // load all matches from FTC event file.  No matches will be created algorithmically
                    _output.WriteStatus("Using actual FTC event schedule");
                    matches = (Dictionary<int, Match>) PrepareMatchesFromFile(_options.DataFilesFolder, _options.EventKey, teams);
                }
                else
                {
                    _output.WriteStatus("Scheduling round 1 randomly");
                    matches = new Dictionary<int, Match>();
                    _scheduler.AddNextRoundMatchesRandom(1, matches, teams);
                }
            }
            else
            {
                // We'll be using swiss model.  How are we generating first round?
                if (_options.SwissScheduling.SeedFirstRoundsOPR)
                {
                    // TODO: Add OPR first-round seeding
                    throw new NotImplementedException("SeedFirstRoundsOPR not yet supported.");
                }
                else
                {
                    _output.WriteStatus("Scheduling round 1 randomly");
                    matches = new Dictionary<int, Match>();
                    _scheduler.AddNextRoundMatchesRandom(1, matches, teams);
                }
            }

            return matches;
        }

        public IDictionary<int, Team> PrepareTeamsFromFile(string folder, string eventKey)
        {
            _output.WriteStatus("Loading teams from " + eventKey);
            var teams = _teamRepo.GetTeamsFromTOAFile(folder, eventKey);
            _output.WriteStatus(string.Format("{0} teams loaded", teams.Count));
            return teams;
        }

        public void SetTeamsOPR(IDictionary<int, Team> teams, IDictionary<int, Match> matches)
        {
            _output.WriteStatus("Caculating team OPR from results file");
            var oprCalculator = new OPRHelper(_options);
            oprCalculator.SetTeamsOPR(teams, matches);
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
            _output.WriteHeading("Rank\tNumber\tRP\tTBP\tOPR\tOPRRank\tVariance");
            foreach (var team in teams.Values.OrderBy(t => t.Rank))
            {
                _output.WriteLine(string.Format("{0}\t{1}\t{2}\t{3}\t{4:0.0}\t{5}\t{6}", team.Rank, team.Number, team.RP, team.TBP, team.OPR, team.OPRRank, team.RankVariance), true);
            }
        }

        private void WriteEventStats(EventStats eventStats)
        {
            if (!_options.Output.TrialStats)
                return;

            _output.WriteHeading("Teams\tMatches\tHigh\tLow\tAvg\tAvgVar\tTopX\tTopXVar\tInTopX");
            _output.WriteLine(String.Format(
                "{0}\t{1}\t{2}\t{3}\t{4:0.00}\t{5:0.00}\t{6}\t{7:0.00}\t{8}",
                eventStats.TeamCount,
                eventStats.MatchCount,
                eventStats.HighScore,
                eventStats.LowScore,
                eventStats.AvgScore,
                eventStats.AvgVariance,
                eventStats.TopX,
                eventStats.AvgTopXVariance,
                eventStats.TopOprInTopRank
                ), true);
        }

        private void WriteBatchStats(BatchStats eventStats)
        {
            if (!_options.Output.BatchStats)
                return;

            _output.WriteHeading("Teams\tMatches\tHigh\tLow\tAvg\tAvgVar\tTopX\tTopXVar\tInTopX\tTrials");
            _output.WriteLine(String.Format(
                "{0}\t{1}\t{2}\t{3}\t{4:0.00}\t{5:0.00}\t{6}\t{7:0.00}\t{8:0.00}\t{9}",
                eventStats.TeamCount,
                eventStats.MatchCount,
                eventStats.HighScore,
                eventStats.LowScore,
                eventStats.AvgScore,
                eventStats.AvgVariance,
                eventStats.TopX,
                eventStats.AvgTopXVariance,
                eventStats.AvgTopOprInTopRank,
                eventStats.EventCount
                ), true);
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
