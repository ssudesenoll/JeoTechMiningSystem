using JeoTechMiningSystem1.Models;
using JeoTechMiningSystem1.Algorithms;
using System;
using System.Collections.Generic;

namespace JeoTechMiningSystem1.Algorithms
{
    public static class DijkstraAlgorithm
    {
        public static (List<MineNode> Route, double Distance) FindShortestSafePath(MineGraph graph, string startId, string endId)
        {
            if (!graph.Nodes.ContainsKey(startId) || !graph.Nodes.ContainsKey(endId))
                return (null, 0);

            MineNode start = graph.Nodes[startId];
            MineNode end = graph.Nodes[endId];

            // If the start or end themselves are strictly blocked, path is impossible
            if (end.IsDangerous) return (null, 0);

            var distances = new Dictionary<string, double>();
            var previous = new Dictionary<string, MineNode>();
            var unvisited = new List<MineNode>();

            foreach (var node in graph.Nodes.Values)
            {
                distances[node.Id] = double.MaxValue;
                unvisited.Add(node);
            }
            distances[startId] = 0;

            while (unvisited.Count > 0)
            {
                unvisited.Sort((a, b) => distances[a.Id].CompareTo(distances[b.Id]));
                MineNode current = unvisited[0];
                unvisited.Remove(current);

                if (distances[current.Id] == double.MaxValue) break;
                if (current == end) break;

                foreach (var neighbor in current.Neighbors)
                {
                    // MANDATORY REQUIREMENT: CRITICAL NODES MUST BE EXCLUDED FROM ROUTING
                    if (neighbor.IsDangerous && neighbor != end && neighbor != start)
                        continue;

                    double dist = CalculateDistance(current, neighbor);
                    double alt = distances[current.Id] + dist;

                    if (alt < distances[neighbor.Id])
                    {
                        distances[neighbor.Id] = alt;
                        previous[neighbor.Id] = current;
                    }
                }
            }

            if (distances[endId] == double.MaxValue)
                return (null, 0);

            var path = new List<MineNode>();
            MineNode curr = end;
            while (curr != null)
            {
                path.Insert(0, curr);
                previous.TryGetValue(curr.Id, out curr);
            }

            return (path, distances[endId]);
        }

        private static double CalculateDistance(MineNode a, MineNode b)
        {
            // Simple Euclidean distance
            return Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
        }
    }
}