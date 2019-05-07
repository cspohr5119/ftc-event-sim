using System.Collections.Generic;
using FTCData.Models;
using System.IO;
using System.Web.Script.Serialization;
using System.Net;
using System.Configuration;

namespace FTCData
{
    public class TeamRepository
    {
        public IDictionary<int, Team> GetTeamsFromTOAFile(string folder, string eventKey)
        {
            var teams = new Dictionary<int, Team>();
            var path = GetTOATeamFilePath(folder, eventKey);
            string json;

            if (!File.Exists(path))
                json = DownloadTeamsFromTOA(eventKey, path);
            else
                json = File.ReadAllText(path);

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            dynamic toaTeamDictionary = serializer.Deserialize<object>(json);

            foreach (var toaTeam in toaTeamDictionary)
            {
                var team = new Team(int.Parse(toaTeam["team_key"]), toaTeam["team"]["team_name_short"]);
                team.OPR = (decimal) toaTeam["opr"];
                teams.Add(team.Number, team);
            }

            return teams;
        }

        public string GetTOATeamFilePath(string folder, string eventKey)
        {
            return Path.Combine(folder, eventKey + "Teams.json");
        }

        public string DownloadTeamsFromTOA(string eventKey, string path)
        {
            var appSettings = ConfigurationManager.AppSettings;
            string apiKey = appSettings["TOA_API_KEY"];
            string teamURL = appSettings["TOATeamsURL"];

            var client = new WebClient();

            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            client.Headers.Add("Content-Type", "application/json");
            client.Headers.Add("X-TOA-Key", apiKey);
            client.Headers.Add("X-Application-Origin", "EventSim");

            string url = string.Format(teamURL, eventKey);
            var response = client.DownloadString(url);

            File.WriteAllText(path, response);
            return response;
        }

        public void ClearTeamStats(IDictionary<int, Team> teams)
        {
            foreach(Team team in teams.Values)
            {
                team.RP = 0;
                team.TBP = 0;
                team.OPRRank = 0;
                team.Rank = 0;
                team.RankVariance = 0;
                team.HasAlignedWith.Clear();
                team.HasOpposed.Clear();
                team.Scheduled = 0;
                team.Played = 0;
            }
        }
    }
}
