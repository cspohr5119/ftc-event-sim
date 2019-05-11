using System.Xml.Serialization;

namespace FTCData.Models
{
    public class Options
    {
        [XmlAttribute]
        public string Title = @"Internal Default Configuration"; // Descriptive title for this configuration.  
        [XmlAttribute]
        public string EventKey = @"1819-CMP-DET1"; // EventKey from theorangealliance.org
        [XmlAttribute]
        public string DataFilesFolder = @"DataFiles"; // Relative path to the local folder where TOA files are.
        [XmlAttribute]
        public string SchedulingModel = "SwissSchduling";  // "RandomScheduling" or "SwissScheduling"
        [XmlAttribute]
        public string TBPMethod = "LosingScore";          // "LosingScore", "WinningScore", "OwnScore", "TotalScore", "Expression"
        [XmlAttribute]
        public string TBPExpression = "[OwnScore] + [LosingScore]"; // If TBPMethod is "Expresion", caluclate TBP with mathematical expresion 
                                                                        // containing [LosingSocore], [WinningScore], and/or [OwnScore]
        [XmlAttribute]
        public decimal ScoreRandomness = 0;        // 0.0 - 1.0.  Alliance Score = OPR +/- (ScoreRandomness * OPR)
        [XmlAttribute]
        public int Rounds = 9;  // How many rounds to schedule in a tournament.
        [XmlAttribute]
        public int Trials = 1;  // How many tournaments to run as a batch.  Good for getting long-term averages.
        [XmlAttribute]
        public bool OPRExcludesPenaltyPoints = true;  // Removes penalty points from OPR calculation.
        [XmlAttribute]
        public decimal OPRmmse = 1;  // Minimum Mean Square Error: 1 - 3 recommended, 0 for traditional OPR values.

        public RandomSchedulingOptions RandomScheduling = new RandomSchedulingOptions();
        public SwissSchedulingOptions SwissScheduling = new SwissSchedulingOptions();
        public OutputOptions Output = new OutputOptions();

        public class RandomSchedulingOptions
        {
            [XmlAttribute]
            public bool UseFTCSchdule = true;   // Schedule matches as specified in EventFIle
            [XmlAttribute]
            public bool UseFTCResults = false;  // Determine match winners from EventFile.  Specify false to use OPR.
        }

        public class SwissSchedulingOptions
        {
            [XmlAttribute]
            public bool SeedFirstRoundsOPR = false;         // Set first round pairings from OPR, Random if false
            [XmlAttribute]
            public int RoundsToScheduleAtStart = 1;         // How many rounds to schedule before play begins
            [XmlAttribute]
            public bool SchduleAtBreaks = false;            // Schedule rounds after each break for multi-day tournaments
            [XmlAttribute]
            public string BreaksAfter = "2,7";              // Play stops after these rounds
            [XmlAttribute]
            public string OpponentPairingMethod = "Fold";   // "Fold" or "Slide"
            [XmlAttribute]
            public string AlliancePairingMethod = "Slide";  // "Fold" or "Slide"
            [XmlAttribute]
            public int CostForPreviousOppoent = 100;        // All costs are multiplied by number of teams
            [XmlAttribute]
            public int CostForPreviousAlignment = 10;       // 
            [XmlAttribute]
            public int CostForCrossingGroups = 10;          // Cost to pair teams or pairs across ranking groups
        }

        public class OutputOptions
        {
            [XmlAttribute]
            public bool Title = true;                  // Write what is currently happening
            [XmlAttribute]
            public bool Status = true;                  // Write what is currently happening
            [XmlAttribute]
            public bool Headings = true;                // Write heading for each section of output
            [XmlAttribute]
            public bool Matchups = true;                // Write Scheduled matchups whenever generated
            [XmlAttribute]
            public bool IncludeCurrentRank = true;      // Include team rank with team number such as 12345(5)
            [XmlAttribute]
            public bool RankingsAfterEachRound = false; // Write rankings after each round played
            [XmlAttribute]
            public bool FinalRankings = true;           // Write Final Rankings
            [XmlAttribute]
            public int TopXStats = 6;                   // Write extra stats about Top X teams.  0 to supress
            [XmlAttribute]
            public bool TrialStats = true;              // Write stats for every trial in a batch.
            [XmlAttribute]
            public bool BatchStats = true;              // Write aggregated stats after all trials.
        }
    }
}
