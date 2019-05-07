namespace FTCData.Models
{
    public class Match
    {
        private readonly int _matchNumber;
        private readonly Team _red1;
        private readonly Team _red2;
        private readonly Team _blue1;
        private readonly Team _blue2;

        public Match(int matchNumber, Team red1, Team red2, Team blue1, Team blue2, int round = 1)
        {
            _matchNumber = matchNumber;
            _red1 = red1;
            _red2 = red2;
            _blue1 = blue1;
            _blue2 = blue2;

            _red1.HasAlignedWith[_red2.Number] = _red2;
            _red1.HasOpposed[_blue1.Number] = _blue1;
            _red1.HasOpposed[_blue2.Number] = _blue2;
            _red1.Scheduled++;

            _red2.HasAlignedWith[_red1.Number] = _red1;
            _red2.HasOpposed[_blue1.Number] = _blue1;
            _red2.HasOpposed[_blue2.Number] = _blue2;
            _red2.Scheduled++;

            _blue1.HasAlignedWith[_blue2.Number] = _blue2;
            _blue1.HasOpposed[_red1.Number] = _red1;
            _blue1.HasOpposed[_red2.Number] = _red2;
            _blue1.Scheduled++;

            _blue2.HasAlignedWith[_blue1.Number] = _blue1;
            _blue2.HasOpposed[_red1.Number] = _red1;
            _blue2.HasOpposed[_red2.Number] = _red2;
            _blue2.Scheduled++;

            Round = round;
        }
        public int MatchNumber => _matchNumber;

        public Team Red1 => _red1;

        public Team Red2 => _red2;

        public Team Blue1 => _blue1;

        public Team Blue2 => _blue2;

        public bool Played { get; set; } = false; 
        public int RedScore { get; set; }
        public int BlueScore { get; set; }
        public int RedPenaltyBonus { get; set; }
        public int BluePenaltyBonus { get; set; }
        public int Round { get; set; }
    }
}
