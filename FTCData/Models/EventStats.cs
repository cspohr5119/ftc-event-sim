namespace FTCData.Models
{
    public class EventStats
    {
        public int TeamCount = 0;
        public int MatchCount = 0;
        public int HighScore = 0;
        public int LowScore = 0;
        public decimal AvgScore = 0m;
        public decimal AvgOPRRankDifference = 0m;
        public decimal AvgPPMRankDifference = 0;
        public int TopX = 0;
        public int TopOPRInTopRank = 0;
        public int TopPPMInTopRank = 0;
        public decimal AvgTopXOPRRankDifference = 0m;
        public decimal AvgTopXPPMRankDifference = 0m;
        public decimal AvgOPRRankErr = 0m;
        public decimal AvgTopXOPRRankErr = 0m;
        public double OPRRankCorrelation = 0d;
        public double TopXOPRRankCorrelation = 0d;
    }
}
