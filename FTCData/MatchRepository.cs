using System;
using System.Collections.Generic;
using FTCData.Models;
using System.Linq;
using System.Net;
using System.IO;
using System.Web.Script.Serialization;
using System.Configuration;
using NCalc;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.Statistics;
namespace FTCData
{
    public class MatchRepository
    {
        private readonly Options _options;
        private readonly Laplace _laplace;
        private Expression _tbpExpr = null;
        private Expression _rpExpr = null;


        public MatchRepository(Options options)
        {
            _options = options;
            _laplace = new Laplace(0.0, (double) _options.RandomTightness);
        }

        public IDictionary<int, Match> GetMatchesFromTOAFile(IDictionary<int, Team> teams, string folder, string eventKey, bool includeActualScores = false)
        {
            var matches = new Dictionary<int, Match>();
            var path = GetTOAMatchFilePath(folder, eventKey);
            string json;

            if (!File.Exists(path))
                json = DownloadMatchesFromTOA(eventKey, path);
            else
                json = File.ReadAllText(path);

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            dynamic toaMatchDictionary = serializer.Deserialize<object>(json);

            int matchNumber = 1;

            foreach(var toaMatch in toaMatchDictionary)
            {
                if (toaMatch["tournament_level"] == 1)
                {

                int red1 = int.Parse(toaMatch["participants"][0]["team_key"]);
                int red2 = int.Parse(toaMatch["participants"][1]["team_key"]);
                int blue1 = int.Parse(toaMatch["participants"][2]["team_key"]);
                int blue2 = int.Parse(toaMatch["participants"][3]["team_key"]);

                    var match = new Match(matchNumber, teams[red1],teams[red2], teams[blue1],teams[blue2], 1);

                    if (includeActualScores)
                    {
                        match.Played = true;
                        match.RedScore = toaMatch["red_score"];
                        match.RedPenaltyBonus = toaMatch["red_penalty"];
                        match.BlueScore = toaMatch["blue_score"];
                        match.BluePenaltyBonus = toaMatch["blue_penalty"];
                    }

                    matches.Add(matchNumber, match);
                    matchNumber++;
                }
            }

            int matchesPerRound = matches.Count / _options.Rounds;
            foreach (Match match in matches.Values)
            {
                match.Round = (match.MatchNumber - 1) / matchesPerRound + 1;
            }

            return matches;
        }

        public string GetTOAMatchFilePath(string folder, string eventKey)
        {
            return Path.Combine(folder, eventKey + "Matches.json");
        }

        public string DownloadMatchesFromTOA(string eventKey, string path)
        {
            var appSettings = ConfigurationManager.AppSettings;
            string apiKey = appSettings["TOA_API_KEY"];
            string matchURL = appSettings["TOAMatchesURL"];

            var client = new WebClient();

            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            client.Headers.Add("Content-Type", "application/json");
            client.Headers.Add("X-TOA-Key", apiKey);
            client.Headers.Add("X-Application-Origin", "EventSim");

            string url = string.Format(matchURL, eventKey);
            var response = client.DownloadString(url);

            File.WriteAllText(path, response);
            return response;
        }

        public void SetMatchResultsFromPPM(IDictionary<int, Match> matches)
        {
            foreach (var match in matches.Values.Where(m => m.Played == false))
            {
                match.Played = true;
                match.RedScore = CalculateAllianceScore(match.Red1, match.Red2);
                match.BlueScore = CalculateAllianceScore(match.Blue1, match.Blue2);
            }
        }

        public void SetMatchResultsFromPPM(IDictionary<int, Match> matches, int round)
        {
            foreach (var match in matches.Values.Where(m => m.Played == false && m.Round == round))
            {
                match.Played = true;
                match.RedScore = CalculateAllianceScore(match.Red1, match.Red2);
                match.BlueScore = CalculateAllianceScore(match.Blue1, match.Blue2);
            }
        }

        public int CalculateAllianceScore(Team team1, Team team2)
        {
            decimal score = team1.PPM + team2.PPM;
            if (_options.ScoreRandomness > 0.0m)
            {
                return RandomLaplace((double) score);
            }

            return (int) Math.Round(score);
        }

        public int RandomLaplace(double score)
        {
            double scoreMin = score - (score * (double)_options.ScoreRandomness);
            double scoreMax = score + (score * (double)_options.ScoreRandomness * .79); // range max is about 79% of range min

            // Get a random value with a gamma distribution
            double rndLaplace;

            // Generate a Laplace-distributed random number between 0 and 1
            double rndNormalized = 0.0;
            while (rndNormalized < 0.001 || rndNormalized > 0.999)  // prevent occasional wacky numbers
            {
                rndLaplace = _laplace.Sample();  // Random number number between -10 and about 10, with a distribution peak at 0.
                rndNormalized = (rndLaplace + 10.0) / 20.0;
            }

            // Apply the skew by raising to the skew value power
            double rndSkewed = Math.Pow(rndNormalized, 0.8);

            // Scale the number to the range we're looking for
            double rndScaled = rndSkewed * (scoreMax - scoreMin);

            // calculate the new score!
            score = rndScaled + scoreMin; 

            // Just in case, score can't be < 0;
            if (score < 0)
                score = 0;

            return (int)Math.Round(score, 0);
        }

        public void SetRankings(IDictionary<int, Match> matches, IDictionary<int, Team> teams, string tbpMethod, int round = -1)
        {
            var oprCalculater = new OPRHelper(_options);

            // clear team stats
            foreach (var item in teams)
            {
                ClearTeamStats(item.Value);
            }

            foreach (var match in matches.Values.Where(m => m.Played == true && m.Round <= round))
            {
                match.Red1.RP += CalculateRP(match.Red1, match);
                match.Red2.RP += CalculateRP(match.Red2, match);
                match.Blue1.RP += CalculateRP(match.Blue1, match);
                match.Blue2.RP += CalculateRP(match.Blue2, match);

                // add tie breaker points
                match.Red1.TBP = CalculateTBP(match.Red1, match, matches);
                match.Red2.TBP = CalculateTBP(match.Red2, match, matches);
                match.Blue1.TBP = CalculateTBP(match.Blue1, match, matches);
                match.Blue2.TBP = CalculateTBP(match.Blue2, match, matches);

                // increment play counts
                match.Red1.Played++;
                match.Red2.Played++;
                match.Blue1.Played++;
                match.Blue2.Played++;

                // set match difficulty
                match.Red1.ScheduleDifficulty += match.Blue1.PPM + match.Blue2.PPM - match.Red2.PPM;
                match.Red2.ScheduleDifficulty += match.Blue1.PPM + match.Blue2.PPM - match.Red1.PPM;
                match.Blue1.ScheduleDifficulty += match.Red1.PPM + match.Red2.PPM - match.Blue2.PPM;
                match.Blue2.ScheduleDifficulty += match.Red1.PPM + match.Red2.PPM - match.Blue1.PPM;
            }

            // Set Rank values
            int rank = 1;
            foreach (var team in teams.Values.OrderByDescending(t => t.RP).ThenByDescending(t => t.TBP))
            {
                team.Rank = rank;
                if (round > -1)
                    team.RankProgression[round] = rank;
                rank++;
            }

            // Set CurrentOPR
            var _oprHelper = new OPRHelper(_options);
            Dictionary<int, Match> roundMatches = matches.Where(m => m.Value.Round <= round).ToDictionary(m => m.Key, m => m.Value);
            _oprHelper.SetTeamsOPR(teams, "CurrentOPR", matches);

            // Set PPM Rank and Variance
            rank = 1;
            foreach (var item in teams.OrderByDescending(t => t.Value.PPM))
            {
                item.Value.PPMRank = rank++;
                item.Value.PPMRankDifference = item.Value.PPMRank - item.Value.Rank;
            }

            // Set CurrentOPR Rank and Variance
            rank = 1;
            foreach (var item in teams.OrderByDescending(t => t.Value.CurrentOPR))
            {
                item.Value.OPRRank = rank++;
                item.Value.OPRRankDifference = item.Value.OPRRank - item.Value.Rank;
            }
        }

        public int CalculateRP(Team team, Match match)
        {
            bool isRed = (team == match.Red1 || team == match.Red2);

            int redPPScore = match.RedScore - match.RedPenaltyBonus;
            int bluePPScore = match.BlueScore - match.BluePenaltyBonus;

            int winningPPScore;
            int losingPPScore;

            int win = 0;
            int tie = 0;

            if (match.RedScore > match.BlueScore)
            {
                // red wins
                winningPPScore = redPPScore;
                losingPPScore = bluePPScore;
                if (isRed)
                    win = 1;
            }
            else if (match.RedScore < match.BlueScore)
            {
                // blue wins
                winningPPScore = bluePPScore;
                losingPPScore = redPPScore;
                if (!isRed)
                    win = 1;
            }
            else //tie
            {
                winningPPScore = Math.Max(redPPScore, bluePPScore);
                losingPPScore = Math.Min(redPPScore, bluePPScore);
                tie = 1;
            }

            int ownPPScore = 0;
            if (isRed)
                ownPPScore = redPPScore;
            else
                ownPPScore = bluePPScore;

            return EvaluateRP(_options.RPExpression, winningPPScore, losingPPScore, ownPPScore, win, tie);
        }

        public int EvaluateRP(string rpExpression, int winningScore, int losingScore, int ownScore, int win, int tie)
        {
            var ownScores = new List<int>();
            // Cache the expression for performance
            if (_rpExpr == null)
            {
                _rpExpr = new Expression(rpExpression);
            }

            _rpExpr.Parameters["WinningScore"] = winningScore;
            _rpExpr.Parameters["LosingScore"] = losingScore;
            _rpExpr.Parameters["TotalScore"] = winningScore + losingScore;
            _rpExpr.Parameters["OwnScore"] = ownScore;
            _rpExpr.Parameters["Win"] = win;
            _rpExpr.Parameters["Tie"] = tie;

            return (int)Math.Round(Convert.ToDouble(_rpExpr.Evaluate()));
        }

        public int CalculateTBP(Team team, Match match, IDictionary<int, Match> matches)
        {
            bool isRed = (team == match.Red1 || team == match.Red2);

            int redPPScore = match.RedScore - match.RedPenaltyBonus;
            int bluePPScore = match.BlueScore - match.BluePenaltyBonus;

            int winningPPScore;
            int losingPPScore;

            if (match.RedScore > match.BlueScore)
            {
                winningPPScore = redPPScore;
                losingPPScore = bluePPScore;
            }
            else if (match.RedScore < match.BlueScore)
            {
                winningPPScore = bluePPScore;
                losingPPScore = redPPScore;
            }
            else //tie
            {
                winningPPScore = Math.Max(redPPScore, bluePPScore);
                losingPPScore = Math.Min(redPPScore, bluePPScore);
            }

            int ownPPScore = 0;
            if (isRed)
                ownPPScore = redPPScore;
            else
                ownPPScore = bluePPScore;

            switch (_options.TBPMethod)
            {
                case "LosingScore":
                    return team.TBP + losingPPScore;

                case "WinningScore":
                    return team.TBP + winningPPScore;

                case "OwnScore":
                    return team.TBP + ownPPScore;

                case "TotalScore":
                    return team.TBP + redPPScore + bluePPScore;

                case "Expression":
                    return EvaluateTBP(_options.TBPExpression, winningPPScore, losingPPScore, ownPPScore, matches, team);

                default:
                    throw new NotImplementedException(_options.TBPMethod + " is not a supported TBPMethod");
            }
        }

        public int EvaluateTBP(string tbpExpression, int winningScore, int losingScore, int ownScore, IDictionary<int, Match> matches, Team team)
        {
            var ownScores = new List<int>();
            if (tbpExpression.Contains("OwnScores"))
            {
                ownScores = GetTeamOwnScores(matches, team.Number);
            }
            
            // Cache the expression for performance
            if (_tbpExpr == null)
            { 
                _tbpExpr = new Expression(tbpExpression);
                _tbpExpr.EvaluateFunction += delegate (string name, FunctionArgs args)
                {
                    if (name == "Top")
                    {
                        var scoresToUse = (List<int>) args.Parameters[1].Evaluate();
                        int topScoreSum = scoresToUse.OrderByDescending(s => s).Take((int) args.Parameters[0].Evaluate()).Sum();
                        args.Result = topScoreSum;
                    }
                };
            }

            _tbpExpr.Parameters["WinningScore"] = winningScore;
            _tbpExpr.Parameters["LosingScore"] = losingScore;
            _tbpExpr.Parameters["TotalScore"] = winningScore + losingScore;
            _tbpExpr.Parameters["OwnScore"] = ownScore;
            _tbpExpr.Parameters["OwnScores"] = ownScores;

            if (tbpExpression.Contains("Top("))
            {
                // Top returns a total TBP, so don't added it to the existing TBP.  Just return the value returned;
                return (int)Math.Round(Convert.ToDouble(_tbpExpr.Evaluate()));
            }
            else
            {
                // All other expressions return incremental TBP, so add it to existing TBP
                return team.TBP + (int)Math.Round(Convert.ToDouble(_tbpExpr.Evaluate()));
            }       
        }

        public List<int> GetTeamOwnScores(IDictionary<int, Match> matches, int teamNumber)
        {
            var ownScores = matches.Values.Where(m => m.Red1.Number == teamNumber || m.Red2.Number == teamNumber)
                .Select(m => m.RedScore - m.RedPenaltyBonus).ToList();

            ownScores.AddRange(matches.Values.Where(m => m.Blue1.Number == teamNumber || m.Blue2.Number == teamNumber)
                .Select(m => m.BlueScore - m.BluePenaltyBonus).ToList());

            return ownScores;
        }

        public void ClearTeamStats(Team team)
        {
            team.Played = 0;
            team.RP = 0;
            team.TBP = 0;
            team.Rank = 0;
            team.ScheduleDifficulty = 0;
        }

        public EventStats GetEventStats(IDictionary<int, Team> teams, IDictionary<int, Match> matches, int topX)
        {
            var sortedTeams = teams.Values.OrderBy(t => t.Rank).ToList();

            var eventStats = new EventStats
            {
                MatchCount = matches.Count,
                TeamCount = teams.Count,
                HighScore = matches.Values.Max(m => Math.Max(m.RedScore, m.BlueScore)),
                LowScore = matches.Values.Min(m => Math.Min(m.RedScore, m.BlueScore)),
                AvgScore = (decimal) matches.Values.Average(m => (m.RedScore + m.BlueScore / 2)),
                TopX = topX,
                AvgOPRRankDifference = (decimal) sortedTeams.Average(t => Math.Abs(t.OPRRankDifference)),
                AvgPPMRankDifference = (decimal)sortedTeams.Average(t => Math.Abs(t.PPMRankDifference)),
                TopOPRInTopRank = sortedTeams.Count(t => t.Rank <= topX && t.OPRRank <= topX),
                TopPPMInTopRank = sortedTeams.Count(t => t.Rank <= topX && t.PPMRank <= topX),
                AvgTopXOPRRankDifference = 0,
                AvgTopXPPMRankDifference = 0
            };

            // Calculate OPRRankErr
            decimal oprRankErrSum = 0;
            foreach (Team team in sortedTeams)
            {
                // Find the team with the OPR rank equal to the current team's rank
                Team teamToCompare = teams.Values.Where(t => t.OPRRank == team.Rank).FirstOrDefault();
                if (teamToCompare != null)
                    oprRankErrSum += Math.Abs(teamToCompare.CurrentOPR - team.CurrentOPR);
            }
            // Average over number of teams
            eventStats.AvgOPRRankErr = oprRankErrSum / teams.Count();

            // Calculate TopXOPRRankErr
            decimal topXOprRankErrSum = 0;
            foreach (Team team in sortedTeams.Take(topX))
            {
                // Find the team with the OPR rank equal to the current team's rank
                Team teamToCompare = teams.Values.Where(t => t.OPRRank == team.Rank).FirstOrDefault();
                if (teamToCompare != null)
                    topXOprRankErrSum += Math.Abs(teamToCompare.CurrentOPR - team.CurrentOPR);
            }
            // Average over number of teams
            eventStats.AvgTopXOPRRankErr = topXOprRankErrSum / teams.Count();

            // AvgTopX calculations need at least one team in the top rank to be in topX, otherwise it breaks
            if (eventStats.TopOPRInTopRank > 0)
                eventStats.AvgTopXOPRRankDifference = (decimal)sortedTeams.Where(t => t.Rank <= topX).Average(t => Math.Abs(t.OPRRankDifference));

            if (eventStats.TopPPMInTopRank > 0)
                eventStats.AvgTopXPPMRankDifference = (decimal)sortedTeams.Where(t => t.Rank <= topX).Average(t => Math.Abs(t.PPMRankDifference));

            // Rank Correlation
            eventStats.OPRRankCorrelation = Correlation.Spearman(teams.Values
                                                                                .OrderByDescending(m => m.Rank)
                                                                                .Select(m => (double) m.CurrentOPR).ToList(),
                                                                           teams.Values
                                                                                .OrderByDescending(m => m.Rank)
                                                                                .Select(m => (double) matches.Count - m.Rank).ToList());

            eventStats.TopXOPRRankCorrelation = Correlation.Spearman(teams.Values.Where(m => m.Rank <= topX)
                                                                                .OrderByDescending(m => m.Rank)
                                                                                .Select(m => (double) m.CurrentOPR).ToList(),
                                                                           teams.Values.Where(m => m.Rank <= topX)
                                                                                .OrderByDescending(m => m.Rank)
                                                                                .Select(m => (double) matches.Count - m.Rank).ToList());

            if (double.IsNaN(eventStats.OPRRankCorrelation))
                eventStats.OPRRankCorrelation = 0d;

            if (double.IsNaN(eventStats.TopXOPRRankCorrelation))
                eventStats.TopXOPRRankCorrelation = 0d;

            return eventStats;
        }

        public BatchStats GetBatchStats(IList<EventStats> eventStatsList)
        {
            var batchStats = new BatchStats
            {
                EventCount = eventStatsList.Count,
                MatchCount = eventStatsList.Sum(e => e.MatchCount),
                TeamCount = eventStatsList.Max(e => e.TeamCount),
                HighScore = eventStatsList.Max(e => e.HighScore),
                LowScore = eventStatsList.Min(e => e.LowScore),
                AvgScore = eventStatsList.Average(e => (e.AvgScore)),
                AvgOPRRankDifference = eventStatsList.Average(e => e.AvgOPRRankDifference),
                AvgPPMRankDifference = eventStatsList.Average(e => e.AvgPPMRankDifference),
                TopX = eventStatsList[0].TopX,
                AvgTopOPRInTopRank = (decimal) eventStatsList.Average(e => e.TopOPRInTopRank),
                AvgTopXOPRRankDifference = eventStatsList.Average(e => e.AvgTopXOPRRankDifference),
                AvgTopPPMInTopRank = (decimal) eventStatsList.Average(e => e.TopPPMInTopRank),
                AvgOPRRankErr = (decimal) eventStatsList.Average(e => e.AvgOPRRankErr),
                AvgTopXOPRRankErr = (decimal) eventStatsList.Average(e => e.AvgTopXOPRRankErr),
                OPRRankCorrelation = eventStatsList.Average(e => e.OPRRankCorrelation),
                TopXOPRRankCorrelation = eventStatsList.Average(e => e.TopXOPRRankCorrelation)
            };

            return batchStats;
        }
    }
}
