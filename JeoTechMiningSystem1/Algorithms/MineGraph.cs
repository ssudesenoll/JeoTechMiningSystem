using System.Collections.Generic;
using JeoTechMiningSystem1.Models;

namespace JeoTechMiningSystem1.Algorithms
{
    public class MineGraph
    {
        public Dictionary<string, MineNode> Nodes { get; private set; }

        public MineGraph()
        {
            Nodes = new Dictionary<string, MineNode>();
            InitializeGraph();
        }

        private void InitializeGraph()
        {
            // Fotoğraftaki grid yapısına uygun milimetrik koordinatlar
            AddNode(new MineNode("N1", "ANA ÇIKIŞ (N1)", 100, 150, NodeType.MainExit));
            AddNode(new MineNode("N2", "Kavşak (N2)", 280, 150, NodeType.Junction));

            AddNode(new MineNode("IoT-01", "N3 [IoT-01]", 280, 310, NodeType.IoTModule));
            AddNode(new MineNode("N4", "Kavşak (N4)", 450, 310, NodeType.Junction));
            AddNode(new MineNode("IoT-03", "N8 [IoT-03]", 630, 310, NodeType.IoTModule));
            AddNode(new MineNode("N11", "Kavşak (N11)", 810, 310, NodeType.Junction));

            AddNode(new MineNode("N5", "Kavşak (N5)", 280, 470, NodeType.Junction));
            AddNode(new MineNode("IoT-02", "N6 [IoT-02]", 450, 470, NodeType.IoTModule));
            AddNode(new MineNode("N7", "Kavşak (N7)", 630, 470, NodeType.Junction));
            AddNode(new MineNode("IoT-04", "N10 [IoT-04]", 810, 470, NodeType.IoTModule));

            // BURASI GÜNCELLENDİ: "SIĞINMA" yerine "SIĞINMA ODASI" yazıldı
            AddNode(new MineNode("SHELTER", "SIĞINMA ODASI (N9)", 630, 620, NodeType.Shelter));
            AddNode(new MineNode("ALT_EXIT", "ALTERNATİF ÇIKIŞ (N12)", 950, 180, NodeType.AlternativeExit));

            // Fotoğraftaki tüm yatay, dikey ve çapraz tünel bağlantıları
            Connect("N1", "N2");

            Connect("N2", "IoT-01"); // N2 - N3
            Connect("N2", "IoT-03"); // N2 - N8 (Çapraz 410m)
            Connect("N2", "N11");    // N2 - N11 (Çapraz 530m)

            Connect("IoT-01", "N4"); // N3 - N4
            Connect("IoT-01", "N5"); // N3 - N5

            Connect("N4", "IoT-02"); // N4 - N6
            Connect("N4", "IoT-03"); // N4 - N8

            Connect("N5", "IoT-02"); // N5 - N6

            Connect("IoT-02", "N7"); // N6 - N7

            Connect("IoT-03", "N7"); // N8 - N7
            Connect("IoT-03", "N11"); // N8 - N11

            Connect("N7", "SHELTER"); // N7 - N9
            Connect("N7", "IoT-04");  // N7 - N10

            Connect("N11", "IoT-04"); // N11 - N10
            Connect("N11", "ALT_EXIT"); // N11 - N12
        }

        private void AddNode(MineNode node)
        {
            Nodes.Add(node.Id, node);
        }

        private void Connect(string id1, string id2)
        {
            if (Nodes.ContainsKey(id1) && Nodes.ContainsKey(id2))
            {
                Nodes[id1].AddNeighbor(Nodes[id2]);
            }
        }

        public void ResetDangers()
        {
            foreach (var node in Nodes.Values) node.IsDangerous = false;
        }
    }
}