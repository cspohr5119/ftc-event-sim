using System;

namespace FTCData.Models
{
    public class Edge
    {
        const int WeightFactor = 1000000;
        private readonly Node _node1;
        private readonly Node _node2;
        private int _weight;
        private int _cost;
        private readonly int[] _costArray = new int[] { 0, 0, 0 };
        private readonly int[] _weightArray = new int[] { 0, 0, 0 };

        public Edge(Node node1, Node node2, int cost)
        {
            if (cost > WeightFactor || cost < 1)
                throw new ArgumentOutOfRangeException("cost", string.Format("Cost must be a positive integer less than {0}", 1000000));

            _node1 = node1;
            _node2 = node2;

            _costArray[0] = _node1.Id;
            _costArray[1] = _node2.Id;

            _weightArray[0] = _node1.Id;
            _weightArray[1] = _node2.Id;
            Cost = cost;
        }

        public Node Node1 => _node1;
        public Node Node2 => _node2;
        public int Weight => _weight;
        public int[] CostArray => _costArray;
        public int[] WeightArray => _weightArray;

        public int Cost
        {
            get
            {
                return _cost;
            }
            set
            {
                _cost = value;
                _costArray[2] = _cost;
                _weight = WeightFactor / _cost;
                _weightArray[2] = _weight;
            }
        }
    }
}
