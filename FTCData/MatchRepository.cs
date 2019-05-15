using System;
using System.Collections.Generic;
using FTCData.Models;
using System.Linq;
using System.Net;
using System.IO;
using System.Web.Script.Serialization;
using System.Configuration;
using NCalc;

namespace FTCData
{
    public class MatchRepository
    {
        private readonly Options _options;
        private readonly Random _rnd = new Random();
        private Expression _expr = null;

        public MatchRepository(Options options)
        {
            _options = options;
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
            int score = (int)Math.Round(team1.PPM + team2.PPM, 0);
            if (_options.ScoreRandomness > 0.0m)
            { 
                int scoreMin = score - (int)(score * _options.ScoreRandomness);
                int scoreMax = score + (int)(score * _options.ScoreRandomness) + 1;
                score = _rnd.Next(scoreMin, scoreMax);
            }

            return score;
        }

        public void SetRankings(IDictionary<int, Match> matches, IDictionary<int, Team> teams, string tbpMethod)
        {
            var oprCalculater = new OPRHelper(_options);

            // clear team stats
            foreach (var item in teams)
            {
                ClearTeamStats(item.Value);
            }

            foreach (var item in matches.Where(m => m.Value.Played == true))
            {
                var match = item.Value;

                if (match.RedScore > match.BlueScore)
                {
                    // red wins
                    match.Red1.RP += 2;
                    match.Red2.RP += 2;
                }
                else if (match.RedScore < match.BlueScore)
                {
                    // blue wins
                    match.Blue1.RP += 2;
                    match.Blue2.RP += 2;
                }
                else
                {
                    // tie
                    match.Red1.RP++;
                    match.Red2.RP++;
                    match.Blue1.RP++;
                    match.Blue2.RP++;
                }

                // add tie breaker points
                match.Red1.TBP += CalculateTBP(match.Red1, match);
                match.Red2.TBP += CalculateTBP(match.Red2, match);
                match.Blue1.TBP += CalculateTBP(match.Blue1, match);
                match.Blue2.TBP += CalculateTBP(match.Blue2, match);

                // increment play counts
                match.Red1.Played++;
                match.Red2.Played++;
                match.Blue1.Played++;
                match.Blue2.Played++;
            }

            // Set Rank values
            int rank = 1;
            foreach (var item in teams.OrderByDescending(t => t.Value.RP).ThenByDescending(t => t.Value.TBP))
            {
                item.Value.Rank = rank++;
            }

            // Set CurrentOPR
            var _oprHelper = new OPRHelper(_options);
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

        public int CalculateTBP(Team team, Match match)
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
                    return losingPPScore;

                case "WinningScore":
                    return winningPPScore;

                case "OwnScore":
                    return ownPPScore;

                case "TotalScore":
                    return redPPScore + bluePPScore;

                case "Expression":
                    return EvaluateTBP(_options.TBPExpression, winningPPScore, losingPPScore, ownPPScore);

                default:
                    throw new NotImplementedException(_options.TBPMethod + " is not a supported TBPMethod");
            }
        }

        public int EvaluateTBP(string tbpExpression, int winningScore, int losingScore, int ownScore)
        {
            // Cache the expression for performance
            if (_expr == null)
                _expr = new Expression(tbpExpression);

            _expr.Parameters["WinningScore"] = winningScore;
            _expr.Parameters["LosingScore"] = losingScore;
            _expr.Parameters["OwnScore"] = ownScore;

            return (int) Math.Round(Convert.ToDouble(_expr.Evaluate()));
        }

        public void ClearTeamStats(Team team)
        {
            team.Played = 0;
            team.RP = 0;
            team.TBP = 0;
            team.Rank = 0;
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
                AvgTopXOPRRankDifference = (decimal) sortedTeams.Where(t => t.Rank <= topX).Average(t => Math.Abs(t.OPRRankDifference)),
                AvgTopXPPMRankDifference = (decimal) sortedTeams.Where(t => t.Rank <= topX).Average(t => Math.Abs(t.PPMRankDifference))
            };

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
                AvgTopPPMInTopRank = (decimal) eventStatsList.Average(e => e.TopPPMInTopRank)
            };

            return batchStats;
        }
    }
}
