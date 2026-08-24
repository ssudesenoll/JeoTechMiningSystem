using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using JeoTechMiningSystem1.Models;
using JeoTechMiningSystem1.Algorithms;

namespace JeoTechMiningSystem1.UI
{
    public class MapHelmet
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string NodeId { get; set; }
        public string TargetNodeId { get; set; } = "";
        public float Progress { get; set; }
        public RouteResult Route { get; set; }

        public bool IsFallen { get; set; }
        public bool IsTrapped { get; set; }
        public RouteResult RescueRoute { get; set; }
    }

    public class MineMapControl : Control
    {
        public MineGraph Graph { get; set; }
        public List<MapHelmet> ActiveHelmets { get; set; } = new List<MapHelmet>();

        public MineMapControl()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(10, 15, 25);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (Pen gridPen = new Pen(Color.FromArgb(25, 35, 50), 1))
            {
                for (int i = 0; i < this.Width; i += 30) g.DrawLine(gridPen, i, 0, i, this.Height);
                for (int i = 0; i < this.Height; i += 30) g.DrawLine(gridPen, 0, i, this.Width, i);
            }

            if (Graph == null || Graph.Nodes.Count == 0) return;

            float minX = Graph.Nodes.Values.Min(n => n.X);
            float maxX = Graph.Nodes.Values.Max(n => n.X);
            float minY = Graph.Nodes.Values.Min(n => n.Y);
            float maxY = Graph.Nodes.Values.Max(n => n.Y);

            float mapWidth = Math.Max(1f, maxX - minX);
            float mapHeight = Math.Max(1f, maxY - minY);

            float paddingX = 120f;
            float paddingY = 80f;
            float scaleX = (this.Width - (paddingX * 2)) / mapWidth;
            float scaleY = (this.Height - (paddingY * 2)) / mapHeight;
            float scale = Math.Min(scaleX, scaleY);

            float offsetX = (this.Width - (mapWidth * scale)) / 2f;
            float offsetY = (this.Height - (mapHeight * scale)) / 2f;

            PointF GetPos(MineNode n) => new PointF(offsetX + (n.X - minX) * scale, offsetY + (n.Y - minY) * scale);

            Pen tunnelPen = new Pen(Color.FromArgb(40, 50, 70), 16);
            Pen dangerTunnelPen = new Pen(Color.DarkRed, 16) { DashStyle = DashStyle.Dash };

            Pen routeOuter = new Pen(Color.FromArgb(0, 200, 100), 8);
            Pen routeInner = new Pen(Color.White, 2);

            Pen rescueOuter = new Pen(Color.Magenta, 8) { DashPattern = new float[] { 4, 2 } };
            Pen rescueInner = new Pen(Color.White, 2) { DashPattern = new float[] { 16, 8 } };

            Font distFont = new Font("Arial", 8, FontStyle.Bold);
            Font nodeFont = new Font("Segoe UI", 9, FontStyle.Bold);

            var drawnEdges = new HashSet<string>();

            // 1. TÜNELLER VE MESAFELER
            foreach (var node in Graph.Nodes.Values)
            {
                foreach (var neighbor in node.Neighbors)
                {
                    string edgeHash = string.Compare(node.Id, neighbor.Id) < 0 ? $"{node.Id}-{neighbor.Id}" : $"{neighbor.Id}-{node.Id}";
                    if (!drawnEdges.Contains(edgeHash))
                    {
                        drawnEdges.Add(edgeHash);
                        PointF p1 = GetPos(node);
                        PointF p2 = GetPos(neighbor);

                        g.DrawLine((node.IsDangerous || neighbor.IsDangerous) ? dangerTunnelPen : tunnelPen, p1.X, p1.Y, p2.X, p2.Y);

                        float midX = (p1.X + p2.X) / 2f;
                        float midY = (p1.Y + p2.Y) / 2f;
                        double dist = Math.Round(Math.Sqrt(Math.Pow(node.X - neighbor.X, 2) + Math.Pow(node.Y - neighbor.Y, 2)));

                        SizeF tSize = g.MeasureString(dist.ToString() + "m", distFont);
                        g.DrawString(dist.ToString() + "m", distFont, Brushes.LightGray, midX - (tSize.Width / 2), midY - 15);
                    }
                }
            }

            // 2. GÜVENLİ ROTALAR (Önce Yeşil Hat Çizilir)
            if (ActiveHelmets != null)
            {
                foreach (var h in ActiveHelmets)
                {
                    if (!h.IsTrapped && !h.IsFallen && h.Route != null && h.Route.Success)
                    {
                        for (int i = 0; i < h.Route.Route.Count - 1; i++)
                        {
                            PointF p1 = GetPos(h.Route.Route[i]);
                            PointF p2 = GetPos(h.Route.Route[i + 1]);
                            g.DrawLine(routeOuter, p1.X, p1.Y, p2.X, p2.Y);
                            g.DrawLine(routeInner, p1.X, p1.Y, p2.X, p2.Y);
                        }
                    }
                }

                // KURTARMA ROTALARI (Mor hat her zaman yeşilin ÜZERİNE çizilsin ki yutulmasın!)
                foreach (var h in ActiveHelmets)
                {
                    if ((h.IsTrapped || h.IsFallen) && h.RescueRoute != null && h.RescueRoute.Success)
                    {
                        for (int i = 0; i < h.RescueRoute.Route.Count - 1; i++)
                        {
                            PointF p1 = GetPos(h.RescueRoute.Route[i]);
                            PointF p2 = GetPos(h.RescueRoute.Route[i + 1]);
                            g.DrawLine(rescueOuter, p1.X, p1.Y, p2.X, p2.Y);
                            g.DrawLine(rescueInner, p1.X, p1.Y, p2.X, p2.Y);
                        }
                    }
                }
            }

            // 3. DÜĞÜMLER
            foreach (var node in Graph.Nodes.Values)
            {
                PointF pos = GetPos(node);
                Color nodeColor = Color.FromArgb(40, 50, 70);
                Color borderColor = Color.Gray;
                int size = 26;

                if (node.Type == NodeType.MainExit || node.Type == NodeType.AlternativeExit)
                {
                    nodeColor = Color.FromArgb(0, 100, 50);
                    borderColor = Color.Cyan;
                    size = 40;
                }
                else if (node.Type == NodeType.IoTModule)
                {
                    nodeColor = Color.FromArgb(0, 80, 40);
                    borderColor = Color.LimeGreen;
                    size = 38;
                }
                else if (node.Type == NodeType.Shelter)
                {
                    nodeColor = Color.RoyalBlue;
                    borderColor = Color.DeepSkyBlue;
                    size = 38;
                }

                if (node.IsDangerous)
                {
                    nodeColor = Color.DarkRed;
                    borderColor = Color.Red;
                }

                g.FillEllipse(new SolidBrush(nodeColor), pos.X - size / 2, pos.Y - size / 2, size, size);
                g.DrawEllipse(new Pen(borderColor, 2), pos.X - size / 2, pos.Y - size / 2, size, size);

                if (node.Type == NodeType.IoTModule)
                {
                    Pen iconPen = new Pen(Color.LimeGreen, 2);
                    if (node.IsDangerous) iconPen.Color = Color.White;
                    g.DrawLine(iconPen, pos.X, pos.Y - 2, pos.X, pos.Y + 8);
                    g.DrawArc(iconPen, pos.X - 6, pos.Y - 10, 12, 12, 180, 180);
                    g.DrawArc(iconPen, pos.X - 3, pos.Y - 5, 6, 6, 180, 180);
                }
                else if (node.Type == NodeType.MainExit || node.Type == NodeType.AlternativeExit)
                {
                    g.DrawRectangle(Pens.White, pos.X - 8, pos.Y - 8, 8, 16);
                    g.DrawLine(Pens.White, pos.X + 2, pos.Y, pos.X + 10, pos.Y);
                    g.DrawLine(Pens.White, pos.X + 7, pos.Y - 3, pos.X + 10, pos.Y);
                    g.DrawLine(Pens.White, pos.X + 7, pos.Y + 3, pos.X + 10, pos.Y);
                }
                else if (node.Type == NodeType.Shelter)
                {
                    g.DrawLine(new Pen(Color.White, 2), pos.X - 8, pos.Y + 2, pos.X, pos.Y - 6);
                    g.DrawLine(new Pen(Color.White, 2), pos.X, pos.Y - 6, pos.X + 8, pos.Y + 2);
                    g.DrawLine(new Pen(Color.White, 2), pos.X - 6, pos.Y + 2, pos.X - 6, pos.Y + 8);
                    g.DrawLine(new Pen(Color.White, 2), pos.X + 6, pos.Y + 2, pos.X + 6, pos.Y + 8);
                    g.DrawLine(new Pen(Color.White, 2), pos.X - 6, pos.Y + 8, pos.X + 6, pos.Y + 8);
                }

                string shortName = node.Name.Contains("[") ? node.Name : node.Name.Split('(')[0].Trim();
                SizeF nSize = g.MeasureString(shortName, nodeFont);
                float lblY = (node.Type == NodeType.MainExit || node.Type == NodeType.AlternativeExit || node.Type == NodeType.Shelter) ? pos.Y + size / 2 + 5 : pos.Y - size / 2 - 20;

                g.FillRectangle(new SolidBrush(Color.FromArgb(15, 22, 38)), pos.X - nSize.Width / 2, lblY, nSize.Width, nSize.Height);
                g.DrawRectangle(Pens.Gray, pos.X - nSize.Width / 2, lblY, nSize.Width, nSize.Height);
                g.DrawString(shortName, nodeFont, Brushes.White, pos.X - nSize.Width / 2, lblY);
            }

            // 4. BARETLER VE HAREKETLİ SİMGELER
            if (ActiveHelmets != null)
            {
                Dictionary<string, int> nodeHelmetCount = new Dictionary<string, int>();

                foreach (var helmet in ActiveHelmets)
                {
                    if (!string.IsNullOrEmpty(helmet.NodeId) && Graph.Nodes.ContainsKey(helmet.NodeId))
                    {
                        PointF hPos = GetPos(Graph.Nodes[helmet.NodeId]);
                        PointF arrowTarget = hPos;

                        if (!string.IsNullOrEmpty(helmet.TargetNodeId) && Graph.Nodes.ContainsKey(helmet.TargetNodeId))
                        {
                            PointF nextPos = GetPos(Graph.Nodes[helmet.TargetNodeId]);
                            hPos.X = hPos.X + (nextPos.X - hPos.X) * helmet.Progress;
                            hPos.Y = hPos.Y + (nextPos.Y - hPos.Y) * helmet.Progress;
                            arrowTarget = nextPos;
                        }
                        else
                        {
                            int count = nodeHelmetCount.ContainsKey(helmet.NodeId) ? nodeHelmetCount[helmet.NodeId] : 0;
                            nodeHelmetCount[helmet.NodeId] = count + 1;
                            hPos.X += (count * 20) - 10;
                            hPos.Y -= (count * 10);

                            if (helmet.Route != null && helmet.Route.Success && helmet.Route.Route.Count > 1)
                                arrowTarget = GetPos(helmet.Route.Route[1]);
                        }

                        Brush helmetBrush = helmet.IsFallen ? Brushes.Red : (helmet.IsTrapped ? Brushes.DarkOrange : Brushes.Gold);
                        Pen helmetPen = helmet.IsFallen ? new Pen(Color.Red, 3) : (helmet.IsTrapped ? new Pen(Color.DarkOrange, 3) : new Pen(Color.Gold, 3));
                        Color labelBg = helmet.IsFallen || helmet.IsTrapped ? Color.DarkRed : Color.FromArgb(15, 22, 38);

                        g.FillPie(helmetBrush, hPos.X - 15, hPos.Y - 15, 30, 30, 180, 180);
                        g.DrawLine(helmetPen, hPos.X - 18, hPos.Y, hPos.X + 18, hPos.Y);

                        if (helmet.IsTrapped || helmet.IsFallen)
                        {
                            g.FillRectangle(Brushes.Red, hPos.X + 15, hPos.Y - 10, 26, 14);
                            g.DrawString("SOS", new Font("Segoe UI", 8, FontStyle.Bold), Brushes.White, hPos.X + 16, hPos.Y - 11);
                        }
                        else if (arrowTarget != hPos)
                        {
                            float dx = arrowTarget.X - hPos.X;
                            float dy = arrowTarget.Y - hPos.Y;
                            float angle = (float)(Math.Atan2(dy, dx) * 180 / Math.PI);

                            using (Matrix matrix = new Matrix())
                            {
                                matrix.RotateAt(angle, hPos);
                                g.Transform = matrix;
                                g.FillPolygon(Brushes.Cyan, new PointF[] { new PointF(hPos.X + 22, hPos.Y - 6), new PointF(hPos.X + 34, hPos.Y), new PointF(hPos.X + 22, hPos.Y + 6) });
                                g.ResetTransform();
                            }
                        }

                        string baretLabel = $"{helmet.Name} ({helmet.Id})";
                        SizeF txtSize = g.MeasureString(baretLabel, nodeFont);
                        RectangleF rect = new RectangleF(hPos.X - txtSize.Width / 2, hPos.Y + 8, txtSize.Width + 4, txtSize.Height + 2);
                        g.FillRectangle(new SolidBrush(labelBg), rect);
                        g.DrawRectangle(helmetPen, rect.X, rect.Y, rect.Width, rect.Height);
                        g.DrawString(baretLabel, nodeFont, Brushes.White, rect.X + 2, rect.Y + 1);
                    }
                }
            }

            // =========================================================
            // ZARİF VE KÜÇÜLTÜLMÜŞ LEJANT (HARİTA GÖSTERGELERİ)
            // =========================================================
            int legX = 10;
            int legY = this.Height - 75;
            int legW = 560;
            int legH = 65;

            g.FillRectangle(new SolidBrush(Color.FromArgb(20, 30, 45)), legX, legY, legW, legH);
            g.DrawRectangle(Pens.Gray, legX, legY, legW, legH);

            g.DrawString("LEJANT / HARİTA GÖSTERGELERİ", new Font("Segoe UI", 8, FontStyle.Bold), Brushes.Gold, legX + 5, legY + 5);

            // 1. SÜTUN
            g.DrawLine(routeOuter, legX + 15, legY + 25, legX + 35, legY + 25);
            g.DrawLine(routeInner, legX + 15, legY + 25, legX + 35, legY + 25);
            g.DrawString("Güvenli Rota", new Font("Segoe UI", 8), Brushes.White, legX + 40, legY + 20);

            g.DrawLine(rescueOuter, legX + 15, legY + 45, legX + 35, legY + 45);
            g.DrawLine(rescueInner, legX + 15, legY + 45, legX + 35, legY + 45);
            g.DrawString("Kurtarma (Tahlisiye)", new Font("Segoe UI", 8), Brushes.White, legX + 40, legY + 40);

            // 2. SÜTUN
            g.FillEllipse(Brushes.DarkRed, legX + 170, legY + 20, 12, 12);
            g.DrawEllipse(Pens.Red, legX + 170, legY + 20, 12, 12);
            g.DrawString("Tehlikeli Bölge", new Font("Segoe UI", 8), Brushes.White, legX + 185, legY + 20);

            g.FillEllipse(new SolidBrush(Color.FromArgb(0, 100, 50)), legX + 170, legY + 40, 12, 12);
            g.DrawEllipse(Pens.Cyan, legX + 170, legY + 40, 12, 12);
            g.DrawString("Ana / Alternatif Çıkış", new Font("Segoe UI", 8), Brushes.White, legX + 185, legY + 40);

            // 3. SÜTUN
            g.FillEllipse(Brushes.RoyalBlue, legX + 315, legY + 20, 12, 12);
            g.DrawEllipse(Pens.DeepSkyBlue, legX + 315, legY + 20, 12, 12);
            g.DrawString("Sığınma Odası", new Font("Segoe UI", 8), Brushes.White, legX + 330, legY + 20);

            g.FillEllipse(new SolidBrush(Color.FromArgb(40, 50, 70)), legX + 315, legY + 40, 12, 12);
            g.DrawEllipse(Pens.Gray, legX + 315, legY + 40, 12, 12);
            g.DrawString("Kavşak Noktası", new Font("Segoe UI", 8), Brushes.White, legX + 330, legY + 40);

            // 4. SÜTUN
            g.FillPie(Brushes.Gold, legX + 435, legY + 20, 14, 14, 180, 180);
            g.DrawLine(new Pen(Color.Gold, 2), legX + 433, legY + 27, legX + 451, legY + 27);
            g.DrawString("Akıllı Baret", new Font("Segoe UI", 8), Brushes.White, legX + 455, legY + 20);

            g.FillRectangle(Brushes.Red, legX + 435, legY + 40, 18, 12);
            g.DrawString("SOS", new Font("Segoe UI", 6, FontStyle.Bold), Brushes.White, legX + 436, legY + 41);
            g.DrawString("Mahsur/Yaralı", new Font("Segoe UI", 8), Brushes.White, legX + 455, legY + 40);
        }
    }
}