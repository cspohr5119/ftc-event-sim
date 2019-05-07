using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using System;
using System.Collections.Generic;

namespace OPR
{
    public class OPRCalculator
    {
        /**
         * Direct port of William Gardner's OPR.java class found here: https://github.com/cheer4ftc/opr
         * 
         * Computes Offensive Power Rating (OPR) using the MMSE method from FRC or FTC match data
         * <p>
         * The MMSE method is always stable (unlike the traditional least-squares OPR calculation)
         * and does a better job of predicting future unknown match scores.
         * As the number of matches at an event gets large, the OPR values computed for the MMSE method
         * converge to those computed using the traditional method.
         * The MMSE parameter is a function of how random the scores are in each game.
         * If an alliance scores virtually the same amount every time they play, the MMSE parameter should
         * be close to 0.
         * If an alliance's score varies substantially from match to match due to randomness in their
         * ability, their opponent's ability, or random game factors, then the MMSE parameter should be
         * larger.
         * For a typical game, an MMSE parameter of 1-3 is recommended.
         * Using an MMSE parameter of exactly 0 causes the computed OPR values to be identical to the
         * traditional OPR values.
         *
         * @param mmse             input MMSE adjustment parameter (0=normal OPR, 1-3 recommended)
         * @param teamList         input Array of team numbers, sorted
         * @param teamsPerAlliance input number of teams per alliance (usually 3 for FRC, 2 for FTC)
         * @param teamPlaying      input 3D Array of team numbers for alliances playing in each match [match#][alliance 0=R, 1=B][0-1=FTC, 0-2=FRC]
         * @param score            input 2D Array of scores for each match (-1 if not scored yet) [match#][alliance 0=R, 1=B]
         * @return opr      Array of OPR values matching the order of teamList, null if failed
         */

        public double[] computeMMSE(double mmse, int[] teamList, int teamsPerAlliance, int[][][] teamPlaying, int[][] score)
        {
            var teamAL = new List<int>();
            int numTeams = teamList.Length;
            for (int i = 0; i < numTeams; i++)
            {
                teamAL.Add(teamList[i]);
            }

            // count # of scored matches
            int numScoredMatches = 0;
            foreach (int[] aScore in score)
            {
                if (aScore[0] >= 0)
                {
                    numScoredMatches++;
                }
            }

            // setup matrices and vectors
            Matrix Ar = new DenseMatrix(numScoredMatches, numTeams);
            Matrix Ab = new DenseMatrix(numScoredMatches, numTeams);
            Matrix Mr = new DenseMatrix(numScoredMatches, 1);
            Matrix Mb = new DenseMatrix(numScoredMatches, 1);

            Matrix Ao = new DenseMatrix(2 * numScoredMatches, numTeams);
            Matrix Mo = new DenseMatrix(2 * numScoredMatches, 1);

            // populate matrices and vectors
            int match = 0;
            double totalScore = 0;
            for (int i = 0; i < score.Length; i++)
            {
                if (score[i][0] >= 0)
                { // score match

                    for (int j = 0; j < teamsPerAlliance; j++)
                    {
                        Ar[match, teamAL.IndexOf(teamPlaying[i][0][j])] = 1.0;
                        Ab[match, teamAL.IndexOf(teamPlaying[i][1][j])] = 1.0;
                    }
                    Mr[match, 0] = score[i][0];
                    Mb[match, 0] = score[i][1];

                    totalScore += score[i][0];
                    totalScore += score[i][1];

                    match++;
                }
            }
            Ao.SetSubMatrix(0, 0, numScoredMatches, 0, 0, numTeams, Ar);
            Ao.SetSubMatrix(numScoredMatches, 0, numScoredMatches, 0, 0, numTeams, Ab);

            double meanTeamOffense = totalScore / (numScoredMatches * 2 * teamsPerAlliance); // 2=alliancesPerMatch
            for (int i = 0; i < numScoredMatches; i++)
            {
                Mr[i, 0] = Mr[i, 0] - 2.0 * meanTeamOffense;
                Mb[i, 0] = Mb[i, 0] - 2.0 * meanTeamOffense;
            }
            Mo.SetSubMatrix(0, 0, numScoredMatches, 0, 0, 1, Mr);
            Mo.SetSubMatrix(numScoredMatches, 0, numScoredMatches, 0, 0, 1, Mb);

            // compute inverse of match matrix (Ao' Ao + mmse*I)
            var M = Matrix<double>.Build;
            Matrix<double> matchMatrixInv;
            try
            {
                matchMatrixInv = Ao.Transpose().Multiply(Ao).Add(M.DenseIdentity(numTeams, numTeams).Multiply(mmse)).Inverse();
            }
            catch (Exception)
            {
                return null; // matrix not invertible
            }

            // compute OPRs
            double[] opr = new double[teamList.Length];
            Matrix<double> Oprm = matchMatrixInv.Multiply(Ao.Transpose().Multiply(Mo));
            for (int i = 0; i < numTeams; i++)
            {
                Oprm[i, 0] = Oprm[i, 0] + meanTeamOffense;

                opr[i] = Oprm[i, 0];
            }

            return opr;
        }
    }
}
