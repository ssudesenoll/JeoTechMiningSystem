using System.Collections.Generic;

namespace JeoTechMiningSystem1.Models
{
    public class RouteResult
    {
        public bool Success { get; set; }
        public MineNode Destination { get; set; }
        public List<MineNode> Route { get; set; }
        public double Distance { get; set; }
        public string Reason { get; set; }
        public string SuggestionDirection { get; set; }
    }
}
