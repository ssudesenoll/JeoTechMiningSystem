using System.Collections.Generic;
using JeoTechMiningSystem1.Models;
using JeoTechMiningSystem1.Algorithms;

namespace JeoTechMiningSystem1.Services
{
    public class RouteService
    {
        private MineGraph _graph;

        public RouteService(MineGraph graph)
        {
            _graph = graph;
        }

        public RouteResult CalculateBestEvacuationRoute(string helmetNodeId)
        {
            var result = new RouteResult();

            var mainRoute = DijkstraAlgorithm.FindShortestSafePath(_graph, helmetNodeId, "MAIN_EXIT");
            var altRoute = DijkstraAlgorithm.FindShortestSafePath(_graph, helmetNodeId, "ALT_EXIT");

            bool mainSafe = mainRoute.Route != null && !_graph.Nodes["MAIN_EXIT"].IsDangerous;
            bool altSafe = altRoute.Route != null && !_graph.Nodes["ALT_EXIT"].IsDangerous;

            if (mainSafe && altSafe)
            {
                if (mainRoute.Distance <= altRoute.Distance)
                {
                    result.Success = true;
                    result.Route = mainRoute.Route;
                    result.Distance = mainRoute.Distance;
                    result.Destination = _graph.Nodes["MAIN_EXIT"];
                    result.Reason = "ANA VE ALTERNATİF ÇIKIŞ GÜVENLİ. EN KISA MESAFE SEÇİLDİ.";
                }
                else
                {
                    result.Success = true;
                    result.Route = altRoute.Route;
                    result.Distance = altRoute.Distance;
                    result.Destination = _graph.Nodes["ALT_EXIT"];
                    result.Reason = "ANA VE ALTERNATİF ÇIKIŞ GÜVENLİ. ALTERNATİF ÇIKIŞ DAHA YAKIN OLDUĞU İÇİN SEÇİLDİ.";
                }
            }
            else if (mainSafe)
            {
                result.Success = true;
                result.Route = mainRoute.Route;
                result.Distance = mainRoute.Distance;
                result.Destination = _graph.Nodes["MAIN_EXIT"];
                result.Reason = "ALTERNATİF ÇIKIŞ YOLU TEHLİKELİ. ANA ÇIKIŞA YÖNLENDİRİLDİ.";
            }
            else if (altSafe)
            {
                result.Success = true;
                result.Route = altRoute.Route;
                result.Distance = altRoute.Distance;
                result.Destination = _graph.Nodes["ALT_EXIT"];
                result.Reason = "ANA ÇIKIŞ YOLU KAPANMIŞ VEYA TEHLİKELİ. ALTERNATİF ÇIKIŞA YÖNLENDİRİLDİ.";
            }
            else
            {
                // Both exits blocked, try shelter
                var shelterRoute = DijkstraAlgorithm.FindShortestSafePath(_graph, helmetNodeId, "SHELTER");
                if (shelterRoute.Route != null && !_graph.Nodes["SHELTER"].IsDangerous)
                {
                    result.Success = true;
                    result.Route = shelterRoute.Route;
                    result.Distance = shelterRoute.Distance;
                    result.Destination = _graph.Nodes["SHELTER"];
                    result.Reason = "İKİ ÇIKIŞ DA KAPALI! ACİL OLARAK SIĞINMA ODASINA YÖNLENDİRİLİYOR.";
                }
                else
                {
                    result.Success = false;
                    result.Reason = "GÜVENLİ TAHLİYE ROTASI BULUNAMADI! BÜTÜN ÇIKIŞLAR VE SIĞINAK BAĞLANTISI KESİK.";
                }
            }

            // Simple pseudo-direction for next step
            if (result.Success && result.Route.Count > 1)
            {
                result.SuggestionDirection = $"SONRAKİ YÖN: {result.Route[1].Name} YÖNÜNE İLERLEYİN.";
            }

            return result;
        }
    }
}
