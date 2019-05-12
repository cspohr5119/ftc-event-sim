using System.Collections.Generic;

namespace FTCData.Models
{
    public class Team
    {
        private readonly int _number;
        private readonly string _name;
        private readonly Dictionary<int, Team> _hasAlignedWith;
        private readonly Dictionary<int, Team> _hasOpposed;

        public Team(int Number, string Name)
        {
            _number = Number;
            _name = Name;
            _hasAlignedWith = new Dictionary<int, Team>();
            _hasOpposed = new Dictionary<int, Team>();
        }

        public int Number => _number;
        public string Name => _name;
        public Dictionary<int, Team> HasAlignedWith => _hasAlignedWith;
        public Dictionary<int, Team> HasOpposed => _hasOpposed;
        public int RP { get; set; }
        public int TBP { get; set; }
        public int Scheduled{ get; set; }
        public int Played { get; set; }
        public int Rank { get; set; }
        public decimal PPM { get; set; }
        public decimal CurrentOPR { get; set; }
        public int PPMRank { get; set; }
        public int OPRRank { get; set; }
        public int OPRRankDifference { get; set; }
        public int PPMRankDifference { get; set; }
    }
}
