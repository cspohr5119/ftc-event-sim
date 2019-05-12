using System;
using System.Collections.Generic;
using System.Linq;
using FTCData.Models;
using OPR;

namespace FTCData
{
    public class OPRHelper
    {
        private readonly Options _options;
        private readonly OPRCalculator _oprCalculator = new OPRCalculator();

        public OPRHelper(Options options)
        {
            _options = options;
        }

        public void SetTeamsOPR(IDictionary<int, Team> teams, string propertyName, IDictionary<int, Match> matches)
        {
            // Set each team's OPR value from match results.

            double mmse = (double)_options.OPRmmse;
            int[] teamList = teams.Values.Select(t => t.Number).ToArray<int>();
            int teamsPerAlliance = 2;
            int[][][] teamsPlaying = CreateTeamsPlayingArray(matches);
            int[][] score = CreateScoreArray(matches);

            // If there has been only one round of matches, use the alliance score/2.  computeMMSE won't work in this case.
            if (matches.Count == teams.Count / 4)
            { 
                for (int i = 0; i < score.Length; i++)
                {
                    matches[i + 1].Red1.CurrentOPR = score[i][0] / 2;
                    matches[i + 1].Red2.CurrentOPR = score[i][0] / 2;
                    matches[i + 1].Blue1.CurrentOPR = score[i][1] / 2;
                    matches[i + 1].Blue2.CurrentOPR = score[i][1] / 2;
                }
                return;
            }

            double[] oprArray = _oprCalculator.computeMMSE(mmse, teamList, teamsPerAlliance, teamsPlaying, score);

            for (int i = 0; i < teamList.Length; i++)
            {
                int teamNumber = teamList[i];
                var team = teams[teamNumber];
                if (propertyName == "PPM")
                    team.PPM = (decimal)oprArray[i];
                else
                    try
                    {
                        // sometimes these are NaN.  Not sure why.
                        team.CurrentOPR = (decimal)oprArray[i];
                    }
                    catch
                    {
                        team.CurrentOPR = -1;
                    }
            }
        }

        public int[][][] CreateTeamsPlayingArray(IDictionary<int, Match> matches)
        {
            int[][][] teamsPlaying = new int[matches.Count][][];

            for(int i = 0; i < matches.Count; i++)
            {
                var match = matches[i + 1];

                // array of alliances
                int[][] matchArray = new int[2][];

                // arrays of teams in an alliance
                matchArray[0] = new int[2];
                matchArray[1] = new int[2];

                matchArray[0][0] = match.Red1.Number;
                matchArray[0][1] = match.Red2.Number;

                matchArray[1][0] = match.Blue1.Number;
                matchArray[1][1] = match.Blue2.Number;

                teamsPlaying[i] = matchArray;
            }
            return teamsPlaying;
        }

        public int[][] CreateScoreArray(IDictionary<int, Match> matches)
        {
            int[][] score = new int[matches.Count][];

            for(int i = 0; i< matches.Count; i++)
            {
                var match = matches[i + 1];
                int[] matchScores = new int[2];

                if (match.RedScore + match.BlueScore >= 1)
                {
                    matchScores[0] = match.RedScore;
                    matchScores[1] = match.BlueScore;
                    if (_options.OPRExcludesPenaltyPoints)
                    {
                        matchScores[0] -= match.RedPenaltyBonus;
                        matchScores[1] -= match.BluePenaltyBonus;
                    }
                }
                else
                {
                    matchScores[0] = -1;
                    matchScores[1] = -1;
                }

                score[i] = matchScores;
            }

            return score;
        }
    }
}
