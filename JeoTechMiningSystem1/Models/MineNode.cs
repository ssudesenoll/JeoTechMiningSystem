using System.Collections.Generic;

namespace JeoTechMiningSystem1.Models
{
    public class MineNode
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public bool IsDangerous { get; set; }
        public NodeType Type { get; set; }
        public List<MineNode> Neighbors { get; set; }

        public MineNode(string id, string name, int x, int y, NodeType type)
        {
            Id = id;
            Name = name;
            X = x;
            Y = y;
            Type = type;
            IsDangerous = false;
            Neighbors = new List<MineNode>();
        }

        public void AddNeighbor(MineNode node)
        {
            if (!Neighbors.Contains(node))
            {
                Neighbors.Add(node);
                node.Neighbors.Add(this); // Undirected graph
            }
        }
    }
}