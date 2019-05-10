using System;
using System.Collections.Generic;
using FTCData.Models;
using System.Linq;
using System.Net;
using System.IO;
using System.Web.Script.Serialization;
using System.Configuration;

namespace FTCData
{
    public class MatchRepository
    {
        private readonly Options _options;
        private readonly Random _rnd = new Random();

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

        public void SetMatchResultsFromOpr(IDictionary<int, Match> matches)
        {
            foreach (var match in matches.Values.Where(m => m.Played == false))
            {
                match.Played = true;
                match.RedScore = CalculateAllianceScore(match.Red1, match.Red2);
                match.BlueScore = CalculateAllianceScore(match.Blue1, match.Blue2);
            }
        }

        public int CalculateAllianceScore(Team team1, Team team2)
        {
            int score = (int)Math.Round(team1.OPR + team2.OPR, 0);
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

            // Set OPR Rank and Variance
            rank = 1;
            foreach (var item in teams.OrderByDescending(t => t.Value.OPR))
            {
                item.Value.OPRRank = rank++;
                item.Value.RankVariance = item.Value.OPRRank - item.Value.Rank;
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

            switch (_options.TBPMethod)
            {
                case "LosingScore":
                    return losingPPScore;

                case "WinningScore":
                    return winningPPScore;

                case "OwnScore":
                    if (isRed)
                        return redPPScore;
                    else
                        return bluePPScore;

                case "TotalScore":
                    return redPPScore + bluePPScore;

                default:
                    throw new NotImplementedException(_options.TBPMethod + " is not a supported TBPMethod");
            }
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
                AvgScore = (decimal)matches.Values.Average(m => (m.RedScore + m.BlueScore / 2)),
                AvgVariance = (decimal)sortedTeams.Average(t => Math.Abs(t.RankVariance)),
                TopX = topX,
                TopOprInTopRank = sortedTeams.Count(t => t.Rank <= topX && t.OPRRank <= topX),
                AvgTopXVariance = (decimal)sortedTeams.Where(t => t.Rank <= topX).Average(t => Math.Abs(t.RankVariance))
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
                AvgScore = (decimal)eventStatsList.Average(e => (e.AvgScore)),
                AvgVariance = (decimal)eventStatsList.Average(e => e.AvgVariance),
                TopX = eventStatsList[0].TopX,
                AvgTopOprInTopRank = (decimal)eventStatsList.Average(e => e.TopOprInTopRank),
                AvgTopXVariance = (decimal)eventStatsList.Average(e => e.AvgTopXVariance)
            };

            return batchStats;
        }
    }
}
