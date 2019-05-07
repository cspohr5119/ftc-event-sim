using System;
using System.Collections.Generic;
using System.Linq;
using FTCData.Models;
using OPR;

namespace FTCData
{
    public class OPRHelper
    {

        OPRCalculator _oprCalculator = new OPRCalculator();

        public void SetTeamsOPR(IDictionary<int, Team> teams, IDictionary<int, Match> matches)
        {
            double mmse = 0.0d;
            int[] teamList = teams.Values.Select(t => t.Number).ToArray<int>();
            int teamsPerAlliance = 2;
            int[][][] teamsPlaying = CreateTeamsPlayingArray(matches);
            int[][] score = CreateScoreArray(matches);

            double[] oprArray = _oprCalculator.computeMMSE(mmse, teamList, teamsPerAlliance, teamsPlaying, score);

            for (int i = 0; i < teamList.Length; i++)
            {
                int teamNumber = teamList[i];
                var team = teams[teamNumber];
                team.OPR = (decimal) oprArray[i];
            }
        }

        public int[][][] CreateTeamsPlayingArray(IDictionary<int, Match> matches)
        {
            // int[matches.Count][2][2] 
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
                    matchScores[0] = match.RedScore; // - match.RedPenaltyBonus;  (removed these to match TOA's OPR calculation!)
                    matchScores[1] = match.BlueScore; // - match.BluePenaltyBonus;
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
