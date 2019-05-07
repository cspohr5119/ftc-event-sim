namespace FTCData.Models
{
    public class Node
    {
        private readonly int _id;
        private readonly Team _team;
        private readonly Team _team2;

        public Node(int id, Team team) : this(id, team, null)
        {
        }

        public Node(int id, Team team, Team team2)
        {
            _id = id;
            _team = team;
            _team2 = team2;
        }

        public int Id => _id;

        public Team Team => _team;

        public Team Team2 => _team2;
    }
}
