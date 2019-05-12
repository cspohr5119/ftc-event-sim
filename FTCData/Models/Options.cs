using System.Xml.Serialization;

namespace FTCData.Models
{
    [XmlRoot("Options")]
    public class Options
    {
        public Options()
        {
            RandomScheduling = new RandomSchedulingOptions();
            SwissScheduling = new SwissSchedulingOptions();
            Output = new OutputOptions();
        }

        [XmlAttribute]
        public string Title = "Internal defaults";
        [XmlAttribute]
        public string EventKey = "1819-CMP-DET1";
        [XmlAttribute]
        public string TeamPPMFile = "";
        [XmlAttribute]
        public string DataFilesFolder = "DataFiles";
        [XmlAttribute]
        public string SchedulingModel = "SwissScheduling";
        [XmlAttribute]
        public string TBPMethod = "LosingScore";
        [XmlAttribute]
        public string TBPExpression = "[OwnScore] + [LosingScore]";
        [XmlAttribute]
        public decimal ScoreRandomness = 0;
        [XmlAttribute]
        public int Rounds = 0;
        [XmlAttribute]
        public int Trials = 1;
        [XmlAttribute]
        public bool OPRExcludesPenaltyPoints = true;
        [XmlAttribute]
        public decimal OPRmmse = 1;

        [XmlElement("RandomScheduling")]
        public RandomSchedulingOptions RandomScheduling;
        [XmlElement("SwissScheduling")]
        public SwissSchedulingOptions SwissScheduling;
        [XmlElement("Output")]
        public OutputOptions Output;

        [XmlRoot("RandomScheduling")]
        public class RandomSchedulingOptions
        {
            [XmlAttribute]
            public bool UseFTCSchedule = true;
            [XmlAttribute]
            public bool UseFTCResults = false;
        }

        [XmlRoot("SwissScheduling")]
        public class SwissSchedulingOptions
        {
            [XmlAttribute]
            public bool SeedFirstRoundsOPR = false;
            [XmlAttribute]
            public int RoundsToScheduleAtStart = 1;
            [XmlAttribute]
            public string StartingRoundsOpponentPairingMethod = "Slide";
            [XmlAttribute]
            public bool ScheduleAtBreaks = false;
            [XmlAttribute]
            public string BreaksAfter = "2,7";
            [XmlAttribute]
            public string OpponentPairingMethod = "Fold";
            [XmlAttribute]
            public string AlliancePairingMethod = "Slide";
            [XmlAttribute]
            public int CostForPreviousOpponent = 100;
            [XmlAttribute]
            public int CostForPreviousAlignment = 10;
            [XmlAttribute]
            public int CostForCrossingGroups = 10;
        }

        [XmlRoot("Output")]
        public class OutputOptions
        {
            [XmlAttribute]
            public bool Title = true;
            [XmlAttribute]
            public bool Status = true;
            [XmlAttribute]
            public bool Headings = true;
            [XmlAttribute]
            public bool Matchups = true;
            [XmlAttribute]
            public bool IncludeCurrentRank = true;
            [XmlAttribute]
            public bool RankingsAfterEachRound = false;
            [XmlAttribute]
            public bool FinalRankings = true;
            [XmlAttribute]
            public int TopXStats = 6;
            [XmlAttribute]
            public bool TrialStats = true;
            [XmlAttribute]
            public bool BatchStats = true;
        }
    }
}
