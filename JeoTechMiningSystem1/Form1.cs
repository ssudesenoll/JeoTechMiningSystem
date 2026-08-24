using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.IO.Ports;
using JeoTechMiningSystem1.Algorithms;
using JeoTechMiningSystem1.Models;
using JeoTechMiningSystem1.Services;
using JeoTechMiningSystem1.Communication;
using JeoTechMiningSystem1.UI;

namespace JeoTechMiningSystem1
{
    public partial class Form1 : Form
    {
        private MineGraph _graph;
        private RouteService _routeService;
        private Timer _systemTimer;
        private Timer _clockTimer;
        private Timer _evacuationTimer;

        private MineMapControl _mapControl;
        private RichTextBox _rtbLog;
        private ComboBox _cmbLeftHelmets;

        private Label _lblTargetExit, _lblDistance, _lblETA, _lblOFK, _lblSafe, _lblPath;
        private Label _lblNextDirection, _lblNextNode;
        private Label _lblClock, _lblSystemStatus, _lblBPM;
        private DataGridView _dgvSensors, _dgvHelmets;
        private ComboBox _cmbTestBaret, _cmbTestSensor;
        private CheckBox _chkGas, _chkTemp, _chkCollapse;
        private ListBox _lstEvacuated;

        private Panel _pnlSimControls;

        private Dictionary<string, string> _lastPaths = new Dictionary<string, string>();
        private HashSet<string> _manualOverrides = new HashSet<string>();
        private HashSet<string> _handledSingleAnomalies = new HashSet<string>();

        private List<HelmetData> _helmetsList;

        // ==========================================
        // GERÇEK DONANIM (IoT) DEĞİŞKENLERİ
        // ==========================================
        private SerialPort _serialPort;
        private ComboBox _cmbComPorts;
        private Button _btnConnectHardware;
        private bool _isRealHardwareMode = false;

        private Dictionary<string, HashSet<string>> _anomalies = new Dictionary<string, HashSet<string>> {
            {"IoT-01", new HashSet<string>()},
            {"IoT-02", new HashSet<string>()},
            {"IoT-03", new HashSet<string>()},
            {"IoT-04", new HashSet<string>()}
        };

        private Dictionary<string, SensorData> _realSensorData = new Dictionary<string, SensorData>();

        private bool _isGlobalEmergency = false;

        private class HelmetData
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string NodeId { get; set; }
            public string TargetNodeId { get; set; } = "";
            public string ExpectedNextNodeId { get; set; } = ""; // YANLIŞ YOL TESPİTİ İÇİN EKLENDİ
            public double EdgeProgress { get; set; } = 0.0;
            public bool IsEvacuating { get; set; } = false;
            public double Battery { get; set; } = 95.0;
            public int OfkSecondsRemaining { get; set; } = 1800;
            public int HeartRate { get; set; } = 80;
            public bool IsFallen { get; set; } = false;
            public bool IsTrapped { get; set; } = false;
            public bool IsForcedToShelter { get; set; } = false;
            public RouteResult Route { get; set; }
            public RouteResult RescueRoute { get; set; }
            public bool IsEvacuated { get; set; } = false;
            public string EvacuationPoint { get; set; } = "";
            public string OFKStatus { get; set; } = "Hesaplanıyor...";
            public override string ToString() => $"{Id} ({Name})";
        }

        private class SensorData
        {
            public string ModuleId { get; set; }
            public double Gas { get; set; }
            public double Temperature { get; set; }
        }

        private Random _rnd = new Random();

        public Form1()
        {
            InitializeComponentInternal();
            InitializeSystem();
            LoadComPorts();
        }

        private void InitializeComponentInternal()
        {
            this.Text = "JEOTECH MINING SYSTEM - Biyometrik ve Titreşimli Otonom Tahliye";
            this.Size = new Size(1600, 950);
            this.BackColor = Color.FromArgb(15, 22, 38);
            this.ForeColor = Color.White;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.WindowState = FormWindowState.Maximized;

            Panel panelTop = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Color.FromArgb(20, 30, 45) };
            this.Controls.Add(panelTop);

            Label lblMainTitle = new Label { Text = "JEOTECH MINING SYSTEM", Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = Color.Cyan, Location = new Point(10, 10), AutoSize = true };
            Label lblSubTitle = new Label { Text = "Gerçek Zamanlı IoT (NRF24L01) ve Titreşim Geri Bildirimli Otonom Karar Destek Sistemi", Font = new Font("Segoe UI", 9), ForeColor = Color.LightGray, Location = new Point(15, 38), AutoSize = true };
            panelTop.Controls.Add(lblMainTitle);
            panelTop.Controls.Add(lblSubTitle);

            FlowLayoutPanel flpTopRight = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 650, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10) };
            panelTop.Controls.Add(flpTopRight);

            _lblClock = new Label { Text = "16:13:36", Font = new Font("Consolas", 14, FontStyle.Bold), ForeColor = Color.White, Margin = new Padding(15, 5, 10, 0), AutoSize = true };

            Button btnModToggle = new Button { Text = "SİSTEM: SİMÜLASYON", BackColor = Color.RoyalBlue, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10, FontStyle.Bold), Size = new Size(190, 35) };

            _btnConnectHardware = new Button { Text = "BAĞLAN", BackColor = Color.SeaGreen, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold), Size = new Size(100, 35), Visible = false };
            _cmbComPorts = new ComboBox { Width = 80, DropDownStyle = ComboBoxStyle.DropDownList, Visible = false, Margin = new Padding(5, 7, 5, 0) };

            btnModToggle.Click += (s, e) => {
                _isRealHardwareMode = !_isRealHardwareMode;

                _isGlobalEmergency = false;
                foreach (var k in _anomalies.Keys.ToList()) _anomalies[k].Clear();
                _graph.ResetDangers();
                _chkGas.Checked = false; _chkTemp.Checked = false; _chkCollapse.Checked = false;
                _manualOverrides.Clear();
                _handledSingleAnomalies.Clear();
                _evacuationTimer.Stop();

                _helmetsList.Clear();
                _cmbLeftHelmets.Items.Clear();

                if (_isRealHardwareMode)
                {
                    btnModToggle.Text = "SİSTEM: GERÇEK DONANIM";
                    btnModToggle.BackColor = Color.DarkOrange;
                    _btnConnectHardware.Visible = true;
                    _cmbComPorts.Visible = true;
                    LoadComPorts();

                    _pnlSimControls.Visible = false;

                    _helmetsList.Add(new HelmetData { Id = "B-01", Name = "Ahmet Demir (Fiziksel Baret)", NodeId = "N2", Battery = 95.0, HeartRate = 80 });
                    Log("[SİSTEM] Gerçek Donanım moduna geçildi. Manuel butonlar kapatıldı. Arduino COM Portunu seçip bağlanın.");
                }
                else
                {
                    btnModToggle.Text = "SİSTEM: SİMÜLASYON";
                    btnModToggle.BackColor = Color.RoyalBlue;
                    _btnConnectHardware.Visible = false;
                    _cmbComPorts.Visible = false;
                    DisconnectHardware();

                    _pnlSimControls.Visible = true;

                    _helmetsList.Add(new HelmetData { Id = "H-01", Name = "Sude Şenol", NodeId = "N2", Battery = 92.5, HeartRate = 78 });
                    _helmetsList.Add(new HelmetData { Id = "H-02", Name = "Sena Doğan", NodeId = "N5", Battery = 85.0, HeartRate = 82 });
                    _helmetsList.Add(new HelmetData { Id = "H-03", Name = "Kübra Sağır", NodeId = "N4", Battery = 98.0, HeartRate = 75 });
                    Log("[SİSTEM] Simülasyon moduna dönüldü.");
                }

                foreach (var h in _helmetsList) _cmbLeftHelmets.Items.Add(h);
                if (_cmbLeftHelmets.Items.Count > 0) _cmbLeftHelmets.SelectedIndex = 0;

                _lblSystemStatus.Text = "SİSTEM DURUMU: OTONOM İZLEMEDE";
                _lblSystemStatus.BackColor = Color.SeaGreen;

                UpdateEvacuatedList();
                RecalculateRoute();
                UpdateHelmetTable();
                _mapControl.Invalidate();
            };

            _btnConnectHardware.Click += (s, e) => {
                if (_serialPort != null && _serialPort.IsOpen) DisconnectHardware();
                else ConnectHardware();
            };

            flpTopRight.Controls.Add(_lblClock);
            flpTopRight.Controls.Add(_btnConnectHardware);
            flpTopRight.Controls.Add(_cmbComPorts);
            flpTopRight.Controls.Add(btnModToggle);

            Panel panelLeft = new Panel { Dock = DockStyle.Left, Width = 400, BackColor = Color.FromArgb(18, 25, 40), Padding = new Padding(10) };
            this.Controls.Add(panelLeft);

            Label lblLeftTitle = new Label { Text = "OTONOM YÖNLENDİRME PANELİ", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Color.Cyan, Location = new Point(10, 20), AutoSize = true };
            panelLeft.Controls.Add(lblLeftTitle);

            int yPos = 55;
            panelLeft.Controls.Add(new Label { Text = "Baret:", ForeColor = Color.LightGray, Font = new Font("Segoe UI", 10), Location = new Point(10, yPos), AutoSize = true });
            _cmbLeftHelmets = new ComboBox { Location = new Point(165, yPos - 3), Width = 210, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(30, 40, 55), ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            _cmbLeftHelmets.SelectedIndexChanged += (s, e) => RecalculateRoute();
            panelLeft.Controls.Add(_cmbLeftHelmets);

            yPos += 35;
            panelLeft.Controls.Add(new Label { Text = "Biyometrik Veri (Nabız):", ForeColor = Color.Gold, Font = new Font("Segoe UI", 10), Location = new Point(10, yPos), AutoSize = true });
            _lblBPM = new Label { Text = "-", ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold), Location = new Point(165, yPos), AutoSize = true };
            panelLeft.Controls.Add(_lblBPM);

            yPos += 35;
            panelLeft.Controls.Add(new Label { Text = "Mevcut Konum:", ForeColor = Color.LightGray, Font = new Font("Segoe UI", 10), Location = new Point(10, yPos), AutoSize = true });
            _lblDistance = new Label { Text = "-", ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold), Location = new Point(165, yPos), AutoSize = true };
            panelLeft.Controls.Add(_lblDistance);

            yPos += 35;
            panelLeft.Controls.Add(new Label { Text = "Hedef Çıkış:", ForeColor = Color.LightGray, Font = new Font("Segoe UI", 10), Location = new Point(10, yPos), AutoSize = true });
            _lblTargetExit = new Label { Text = "-", ForeColor = Color.LimeGreen, Font = new Font("Segoe UI", 11, FontStyle.Bold), Location = new Point(165, yPos), AutoSize = true };
            panelLeft.Controls.Add(_lblTargetExit);

            yPos += 35;
            panelLeft.Controls.Add(new Label { Text = "Rota Durumu:", ForeColor = Color.LightGray, Font = new Font("Segoe UI", 10), Location = new Point(10, yPos), AutoSize = true });
            _lblSafe = new Label { Text = "GÜVENLİ", ForeColor = Color.LimeGreen, Font = new Font("Segoe UI", 10, FontStyle.Bold), Location = new Point(165, yPos), AutoSize = true };
            panelLeft.Controls.Add(_lblSafe);

            yPos += 35;
            panelLeft.Controls.Add(new Label { Text = "Titreşim Komutu:", ForeColor = Color.Gold, Font = new Font("Segoe UI", 10), Location = new Point(10, yPos), AutoSize = true });
            _lblNextDirection = new Label { Text = "-", ForeColor = Color.Cyan, Font = new Font("Segoe UI", 12, FontStyle.Bold), Location = new Point(165, yPos), AutoSize = true };
            panelLeft.Controls.Add(_lblNextDirection);

            yPos += 35;
            panelLeft.Controls.Add(new Label { Text = "Sonraki Nokta:", ForeColor = Color.Gold, Font = new Font("Segoe UI", 10), Location = new Point(10, yPos), AutoSize = true });
            _lblNextNode = new Label { Text = "-", ForeColor = Color.White, Font = new Font("Segoe UI", 11, FontStyle.Bold), Location = new Point(165, yPos), AutoSize = true };
            panelLeft.Controls.Add(_lblNextNode);

            yPos += 35;
            panelLeft.Controls.Add(new Label { Text = "Kalan Mesafe/Süre:", ForeColor = Color.LightGray, Font = new Font("Segoe UI", 10), Location = new Point(10, yPos), AutoSize = true });
            _lblETA = new Label { Text = "-", ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold), Location = new Point(165, yPos), AutoSize = true };
            panelLeft.Controls.Add(_lblETA);

            yPos += 35;
            panelLeft.Controls.Add(new Label { Text = "OFK Maske Durumu:", ForeColor = Color.Gold, Font = new Font("Segoe UI", 10), Location = new Point(10, yPos), AutoSize = true });
            _lblOFK = new Label { Text = "-", ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold), Location = new Point(165, yPos), AutoSize = true };
            panelLeft.Controls.Add(_lblOFK);

            yPos += 35;
            panelLeft.Controls.Add(new Label { Text = "Hesaplanan Güzergah:", ForeColor = Color.LightGray, Font = new Font("Segoe UI", 10), Location = new Point(10, yPos), AutoSize = true });
            _lblPath = new Label { Text = "-", ForeColor = Color.Cyan, Font = new Font("Segoe UI", 8, FontStyle.Bold), Location = new Point(165, yPos), AutoSize = true };
            panelLeft.Controls.Add(_lblPath);

            yPos += 45;
            _lblSystemStatus = new Label
            {
                Text = "SİSTEM DURUMU: OTONOM İZLEMEDE",
                BackColor = Color.SeaGreen,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Location = new Point(15, yPos),
                Size = new Size(360, 40),
                TextAlign = ContentAlignment.MiddleCenter
            };
            panelLeft.Controls.Add(_lblSystemStatus);

            yPos += 55;
            panelLeft.Controls.Add(new Label { Text = "✅ GÜVENLİ BÖLGEYE ULAŞANLAR", ForeColor = Color.LimeGreen, Font = new Font("Segoe UI", 10, FontStyle.Bold), Location = new Point(10, yPos), AutoSize = true });

            yPos += 25;
            _lstEvacuated = new ListBox
            {
                Location = new Point(15, yPos),
                Size = new Size(360, 95),
                BackColor = Color.FromArgb(25, 35, 50),
                ForeColor = Color.LimeGreen,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BorderStyle = BorderStyle.FixedSingle
            };
            panelLeft.Controls.Add(_lstEvacuated);

            Panel panelBottom = new Panel { Dock = DockStyle.Bottom, Height = 250, BackColor = Color.FromArgb(15, 22, 38) };
            this.Controls.Add(panelBottom);

            TableLayoutPanel tlpBottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
            tlpBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22F));
            tlpBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            tlpBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            tlpBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23F));
            panelBottom.Controls.Add(tlpBottom);

            Panel pnlSensors = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5) };
            tlpBottom.Controls.Add(pnlSensors, 0, 0);
            pnlSensors.Controls.Add(new Label { Text = "IoT Çevre Sensörleri", ForeColor = Color.Gold, Font = new Font("Segoe UI", 9, FontStyle.Bold), Dock = DockStyle.Top });
            _dgvSensors = CreateGrid();
            _dgvSensors.Columns.Add("Sensör", "Sensör");
            _dgvSensors.Columns.Add("Düğüm", "Düğüm");
            _dgvSensors.Columns.Add("CH4", "MQ-4 CH4");
            _dgvSensors.Columns.Add("Sıcaklık", "LM35 Temp");
            _dgvSensors.Columns.Add("Durum", "Durum");
            pnlSensors.Controls.Add(_dgvSensors);
            _dgvSensors.BringToFront();

            Panel pnlHelmets = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5) };
            tlpBottom.Controls.Add(pnlHelmets, 1, 0);
            pnlHelmets.Controls.Add(new Label { Text = "Akıllı Baret (Personel Listesi)", ForeColor = Color.Gold, Font = new Font("Segoe UI", 9, FontStyle.Bold), Dock = DockStyle.Top });
            _dgvHelmets = CreateGrid();
            _dgvHelmets.Columns.Add("BaretID", "Baret ID");
            _dgvHelmets.Columns.Add("Personel", "Çalışan");
            _dgvHelmets.Columns.Add("Konum", "Konum");
            _dgvHelmets.Columns.Add("BPM", "Nabız");
            _dgvHelmets.Columns.Add("Ivme", "Hareket");
            _dgvHelmets.Columns.Add("Dusme", "Durum");
            _dgvHelmets.Columns.Add("Pil", "Pil %");
            _dgvHelmets.Columns.Add("OFK", "OFK Sayacı");
            pnlHelmets.Controls.Add(_dgvHelmets);
            _dgvHelmets.BringToFront();

            Panel pnlInjector = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5) };
            tlpBottom.Controls.Add(pnlInjector, 2, 0);
            pnlInjector.Controls.Add(new Label { Text = "SİSTEM KONTROL VE TEST PANELİ", ForeColor = Color.Gold, Font = new Font("Segoe UI", 9, FontStyle.Bold), Location = new Point(5, 5), AutoSize = true });

            _pnlSimControls = new Panel { Location = new Point(0, 25), Size = new Size(250, 115) };
            pnlInjector.Controls.Add(_pnlSimControls);

            _pnlSimControls.Controls.Add(new Label { Text = "Modül:", ForeColor = Color.White, Location = new Point(5, 8), AutoSize = true });
            _cmbTestSensor = new ComboBox { Location = new Point(55, 5), Width = 70, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbTestSensor.Items.AddRange(new string[] { "IoT-01", "IoT-02", "IoT-03", "IoT-04" });
            _cmbTestSensor.SelectedIndex = 0;
            _pnlSimControls.Controls.Add(_cmbTestSensor);

            Button btnNormal = new Button { Text = "Modülü Temizle", BackColor = Color.SeaGreen, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Location = new Point(135, 4), Size = new Size(110, 24) };
            btnNormal.Click += (s, e) => {
                string sel = _cmbTestSensor.SelectedItem.ToString();
                _anomalies[sel].Clear();
                _manualOverrides.Remove(sel);
                _handledSingleAnomalies.Remove(sel);
                _chkGas.Checked = false; _chkTemp.Checked = false; _chkCollapse.Checked = false;
                SystemTimer_Tick(null, null);
            };
            _pnlSimControls.Controls.Add(btnNormal);

            _chkGas = new CheckBox { Text = "Gaz Sızıntısı", ForeColor = Color.White, Location = new Point(5, 33), AutoSize = true };
            _chkTemp = new CheckBox { Text = "Aşırı Sıcaklık", ForeColor = Color.White, Location = new Point(115, 33), AutoSize = true };
            _chkCollapse = new CheckBox { Text = "Göçük/Sarsıntı", ForeColor = Color.White, Location = new Point(5, 57), AutoSize = true };

            Button btnApply = new Button { Text = "Uygula (Test Et)", BackColor = Color.DarkRed, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Location = new Point(115, 55), Size = new Size(130, 24) };
            btnApply.Click += (s, e) => {
                string selectedIot = _cmbTestSensor.SelectedItem.ToString();
                _anomalies[selectedIot].Clear();
                _handledSingleAnomalies.Remove(selectedIot);
                _manualOverrides.Remove(selectedIot);

                if (_chkGas.Checked) _anomalies[selectedIot].Add("Gas");
                if (_chkTemp.Checked) _anomalies[selectedIot].Add("Temp");
                if (_chkCollapse.Checked) _anomalies[selectedIot].Add("Collapse");

                SystemTimer_Tick(null, null);
            };

            _pnlSimControls.Controls.Add(_chkGas);
            _pnlSimControls.Controls.Add(_chkTemp);
            _pnlSimControls.Controls.Add(_chkCollapse);
            _pnlSimControls.Controls.Add(btnApply);

            Button btnFall = new Button { Text = "🚨 Düşme / Darbe Alarmı", BackColor = Color.DarkOrange, ForeColor = Color.Black, FlatStyle = FlatStyle.Flat, Location = new Point(5, 85), Size = new Size(240, 26) };
            btnFall.Click += (s, e) => {
                if (_cmbLeftHelmets.SelectedItem is HelmetData h)
                {
                    h.IsFallen = true;
                    h.IsEvacuating = false;

                    if (!_isGlobalEmergency)
                    {
                        _lblSystemStatus.Text = "⚠️ SİSTEM DURUMU: LOKAL ACİL DURUM (İLK YARDIM)";
                        _lblSystemStatus.BackColor = Color.DarkOrange;
                    }

                    Log($"[LOKAL ALARM] MPU6050 düşme/darbe algıladı! İlk yardım ekibi {h.Name} için yönlendiriliyor.");
                    _lastPaths.Clear();
                    RecalculateRoute();
                    UpdateHelmetTable();
                    _mapControl.Invalidate();
                }
            };
            _pnlSimControls.Controls.Add(btnFall);

            pnlInjector.Controls.Add(new Label { Text = "Konum (Baret):", ForeColor = Color.White, Location = new Point(5, 148), AutoSize = true });
            _cmbTestBaret = new ComboBox { Location = new Point(95, 145), Width = 50, DropDownStyle = ComboBoxStyle.DropDownList };
            pnlInjector.Controls.Add(_cmbTestBaret);

            Button btnMove = new Button { Text = "Bareti Taşı", BackColor = Color.RoyalBlue, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Location = new Point(155, 144), Size = new Size(90, 24) };
            btnMove.Click += (s, e) => {
                if (_cmbLeftHelmets.SelectedItem is HelmetData h && _cmbTestBaret.SelectedItem != null)
                {
                    string targetMoveNode = _cmbTestBaret.SelectedItem.ToString();

                    // YANLIŞ YOL (WRONG WAY) TESPİTİ!
                    if (_isGlobalEmergency && !string.IsNullOrEmpty(h.ExpectedNextNodeId) && targetMoveNode != h.ExpectedNextNodeId && targetMoveNode != h.NodeId)
                    {
                        Log($"[DİKKAT] {h.Name} hesaplanan rotadan saptı (Yanlış Yol)! Uyarı titreşimi gönderiliyor.");
                        if (_isRealHardwareMode && h.Id == "B-01") SendGuidanceToHardware("VIB:WRONG_WAY");
                    }

                    h.NodeId = targetMoveNode;
                    h.IsFallen = false;
                    h.IsTrapped = false;
                    h.TargetNodeId = "";
                    h.EdgeProgress = 0;
                    h.IsEvacuating = false;
                    h.IsForcedToShelter = false;

                    var n = _graph.Nodes[h.NodeId];
                    if (n.Type == NodeType.MainExit || n.Type == NodeType.AlternativeExit || n.Type == NodeType.Shelter)
                    {
                        h.IsEvacuated = true;
                        h.EvacuationPoint = n.Name.Contains("[") ? n.Name.Split('[')[0].Trim() : n.Name.Split('(')[0].Trim();
                    }
                    else
                    {
                        h.IsEvacuated = false;
                        h.EvacuationPoint = "";
                    }

                    UpdateEvacuatedList();
                    RecalculateRoute();
                    UpdateHelmetTable();
                    _mapControl.Invalidate();
                }
            };
            pnlInjector.Controls.Add(btnMove);

            Button btnResetAll = new Button { Text = "🔄 Sistemi Sıfırla (Normale Dön)", BackColor = Color.SeaGreen, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Location = new Point(5, 180), Size = new Size(240, 26) };
            btnResetAll.Click += (s, e) => {
                _isGlobalEmergency = false;
                foreach (var k in _anomalies.Keys.ToList()) _anomalies[k].Clear();
                _graph.ResetDangers();

                foreach (var h in _helmetsList)
                {
                    h.IsFallen = false; h.IsTrapped = false; h.IsForcedToShelter = false; h.TargetNodeId = ""; h.EdgeProgress = 0;
                    h.IsEvacuating = false; h.IsEvacuated = false; h.EvacuationPoint = "";
                    h.OfkSecondsRemaining = 1800;
                    h.HeartRate = 80;
                    h.ExpectedNextNodeId = "";
                    if (h.Id == "H-01" || h.Id == "B-01") h.Battery = 95.0; else if (h.Id == "H-02") h.Battery = 85.0; else h.Battery = 98.0;
                }

                _chkGas.Checked = false; _chkTemp.Checked = false; _chkCollapse.Checked = false;
                _manualOverrides.Clear();
                _handledSingleAnomalies.Clear();

                _evacuationTimer.Stop();
                _lblSystemStatus.Text = "SİSTEM DURUMU: OTONOM İZLEMEDE";
                _lblSystemStatus.BackColor = Color.SeaGreen;

                _lastPaths.Clear();
                UpdateEvacuatedList();
                RecalculateRoute();
                UpdateHelmetTable();
                Log("[BİLGİ] Sistem tamamen sıfırlandı.");
            };
            pnlInjector.Controls.Add(btnResetAll);

            Panel pnlLog = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5) };
            tlpBottom.Controls.Add(pnlLog, 3, 0);
            _rtbLog = new RichTextBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(10, 15, 25), ForeColor = Color.Lime, Font = new Font("Consolas", 8), ReadOnly = true, BorderStyle = BorderStyle.None };
            pnlLog.Controls.Add(_rtbLog);

            Panel panelCenter = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(18, 25, 40) };
            this.Controls.Add(panelCenter);
            panelCenter.BringToFront();

            _mapControl = new MineMapControl { Dock = DockStyle.Fill };
            panelCenter.Controls.Add(_mapControl);

            _cmbTestSensor.SelectedIndexChanged += (s, e) => {
                string sel = _cmbTestSensor.SelectedItem.ToString();
                _chkGas.Checked = _anomalies[sel].Contains("Gas");
                _chkTemp.Checked = _anomalies[sel].Contains("Temp");
                _chkCollapse.Checked = _anomalies[sel].Contains("Collapse");
            };
        }

        private DataGridView CreateGrid()
        {
            var grid = new DataGridView { Dock = DockStyle.Fill, BackgroundColor = Color.FromArgb(20, 30, 45), BorderStyle = BorderStyle.None, RowHeadersVisible = false, AllowUserToAddRows = false, ReadOnly = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, EnableHeadersVisualStyles = false };
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(40, 50, 70);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.DefaultCellStyle.BackColor = Color.FromArgb(25, 35, 50);
            grid.DefaultCellStyle.ForeColor = Color.White;
            grid.SelectionChanged += (s, e) => grid.ClearSelection();
            return grid;
        }

        private void InitializeSystem()
        {
            _graph = new MineGraph();
            _routeService = new RouteService(_graph);
            _mapControl.Graph = _graph;

            _cmbTestBaret.Items.AddRange(_graph.Nodes.Keys.ToArray());
            _cmbTestBaret.SelectedIndex = 1;

            _helmetsList = new List<HelmetData>();

            _helmetsList.Add(new HelmetData { Id = "H-01", Name = "Sude Şenol", NodeId = "N2", Battery = 92.5, HeartRate = 78 });
            _helmetsList.Add(new HelmetData { Id = "H-02", Name = "Sena Doğan", NodeId = "N5", Battery = 85.0, HeartRate = 82 });
            _helmetsList.Add(new HelmetData { Id = "H-03", Name = "Kübra Sağır", NodeId = "N4", Battery = 98.0, HeartRate = 75 });

            foreach (var h in _helmetsList) _cmbLeftHelmets.Items.Add(h);
            _cmbLeftHelmets.SelectedIndex = 0;

            _systemTimer = new Timer { Interval = 1000 };
            _systemTimer.Tick += SystemTimer_Tick;
            _systemTimer.Start();

            _clockTimer = new Timer { Interval = 1000 };
            _clockTimer.Tick += (s, e) => _lblClock.Text = DateTime.Now.ToString("HH:mm:ss");
            _clockTimer.Start();

            _evacuationTimer = new Timer { Interval = 50 };
            _evacuationTimer.Tick += EvacuationTimer_Tick;

            _realSensorData["IoT-01"] = new SensorData { ModuleId = "IoT-01", Gas = 0.16, Temperature = 22.4 };
            _realSensorData["IoT-02"] = new SensorData { ModuleId = "IoT-02", Gas = 0.16, Temperature = 22.4 };
            _realSensorData["IoT-03"] = new SensorData { ModuleId = "IoT-03", Gas = 0.16, Temperature = 22.4 };
            _realSensorData["IoT-04"] = new SensorData { ModuleId = "IoT-04", Gas = 0.16, Temperature = 22.4 };
        }

        private void LoadComPorts()
        {
            _cmbComPorts.Items.Clear();
            string[] ports = SerialPort.GetPortNames();
            if (ports.Length > 0)
            {
                _cmbComPorts.Items.AddRange(ports);
                _cmbComPorts.SelectedIndex = 0;
            }
            else
            {
                _cmbComPorts.Items.Add("PORT YOK");
                _cmbComPorts.SelectedIndex = 0;
            }
        }

        private void ConnectHardware()
        {
            if (_cmbComPorts.SelectedItem == null || _cmbComPorts.SelectedItem.ToString() == "PORT YOK") return;

            try
            {
                _serialPort = new SerialPort(_cmbComPorts.SelectedItem.ToString(), 9600);
                _serialPort.DataReceived += SerialPort_DataReceived;
                _serialPort.Open();

                _btnConnectHardware.Text = "KOPAR";
                _btnConnectHardware.BackColor = Color.DarkRed;
                Log($"[DONANIM] NRF24L01 Alıcı modülüne {_serialPort.PortName} üzerinden bağlanıldı. Veri bekleniyor...");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Bağlantı Hatası: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Log($"[HATA] Donanıma bağlanılamadı: {ex.Message}");
            }
        }

        private void DisconnectHardware()
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
                _serialPort.Dispose();
            }
            _btnConnectHardware.Text = "BAĞLAN";
            _btnConnectHardware.BackColor = Color.SeaGreen;
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string line = _serialPort.ReadLine().Trim();
                string[] parts = line.Split(',');
                if (parts.Length == 3)
                {
                    string moduleId = parts[0];
                    if (double.TryParse(parts[1].Replace('.', ','), out double gas) &&
                        double.TryParse(parts[2].Replace('.', ','), out double temp))
                    {
                        if (_realSensorData.ContainsKey(moduleId))
                        {
                            _realSensorData[moduleId].Gas = gas;
                            _realSensorData[moduleId].Temperature = temp;

                            this.Invoke(new Action(() => {
                                HardwareDataEvaluation();
                            }));
                        }
                    }
                }
            }
            catch { /* Bozuk veriyi yoksay */ }
        }

        private void HardwareDataEvaluation()
        {
            int totalAnomaliesInMine = 0;

            foreach (var kvp in _realSensorData)
            {
                string iot = kvp.Key;
                _anomalies[iot].Clear();

                if (kvp.Value.Gas >= 1.5) _anomalies[iot].Add("Gas");
                if (kvp.Value.Temperature >= 45.0) _anomalies[iot].Add("Temp");

                totalAnomaliesInMine += _anomalies[iot].Count;
            }

            bool dangerDetected = false;

            for (int i = 0; i < _dgvSensors.Rows.Count; i++)
            {
                string iot = _dgvSensors.Rows[i].Cells[0].Value.ToString();
                var anom = _anomalies[iot];

                _dgvSensors.Rows[i].Cells[2].Value = $"%{_realSensorData[iot].Gas:F2}";
                _dgvSensors.Rows[i].Cells[3].Value = $"{_realSensorData[iot].Temperature:F1}°C";

                if (anom.Count == 0)
                {
                    _dgvSensors.Rows[i].Cells[4].Value = "NORMAL";
                    _dgvSensors.Rows[i].DefaultCellStyle.BackColor = Color.FromArgb(25, 35, 50);
                    _graph.Nodes[iot].IsDangerous = false;
                }
                else
                {
                    if (totalAnomaliesInMine >= 2)
                    {
                        _handledSingleAnomalies.Remove(iot);
                        _manualOverrides.Remove(iot);

                        _dgvSensors.Rows[i].Cells[4].Value = "TEHLİKE (Çapraz Doğrulandı)";
                        _dgvSensors.Rows[i].DefaultCellStyle.BackColor = Color.DarkRed;
                        _graph.Nodes[iot].IsDangerous = true;
                        dangerDetected = true;
                    }
                    else if (totalAnomaliesInMine == 1)
                    {
                        if (!_handledSingleAnomalies.Contains(iot))
                        {
                            _handledSingleAnomalies.Add(iot);

                            string anomType = anom.First();
                            string trAnom = anomType == "Gas" ? "MQ-4 Metan Uyarısı (%1.5 Sınırı Aşıldı)" : "LM35 Sıcaklık Uyarısı (45°C Aşıldı)";

                            bool evacuate = ShowManualDecisionPopup(iot, trAnom);

                            if (evacuate)
                            {
                                _manualOverrides.Add(iot);
                            }
                            else
                            {
                                _manualOverrides.Remove(iot);
                            }
                        }

                        if (_manualOverrides.Contains(iot))
                        {
                            _dgvSensors.Rows[i].Cells[4].Value = "MANUEL TAHLİYE";
                            _dgvSensors.Rows[i].DefaultCellStyle.BackColor = Color.DarkRed;
                            _graph.Nodes[iot].IsDangerous = true;
                            dangerDetected = true;
                        }
                        else
                        {
                            _dgvSensors.Rows[i].Cells[4].Value = "UYARI (Bekliyor)";
                            _dgvSensors.Rows[i].DefaultCellStyle.BackColor = Color.DarkGoldenrod;
                            _graph.Nodes[iot].IsDangerous = false;
                        }
                    }
                }
            }

            if (dangerDetected && !_evacuationTimer.Enabled)
            {
                _isGlobalEmergency = true;
                foreach (var h in _helmetsList)
                {
                    if (!h.IsFallen && !h.IsEvacuated) h.IsEvacuating = true;
                }
                _evacuationTimer.Start();
                _lblSystemStatus.Text = "🚨 SİSTEM DURUMU: ACİL TAHLİYE DEVREDE (DONANIM TETİKLEDİ)";
                _lblSystemStatus.BackColor = Color.DarkRed;
                Log("[DONANIM TETİKLEMESİ] Gerçek sensörler tehlike algıladı! Tahliye ve titreşimler başlatıldı.");
            }

            RecalculateRoute();
            UpdateHelmetTable();
            _mapControl.Invalidate();
        }

        private void SendGuidanceToHardware(string command)
        {
            if (_isRealHardwareMode && _serialPort != null && _serialPort.IsOpen)
            {
                try
                {
                    _serialPort.WriteLine(command);
                }
                catch { }
            }
        }

        private void UpdateEvacuatedList()
        {
            _lstEvacuated.Items.Clear();
            foreach (var h in _helmetsList)
            {
                if (h.IsEvacuated) _lstEvacuated.Items.Add($"✓ {h.Name} ({h.Id}) -> {h.EvacuationPoint}");
            }
        }

        private void EvacuationTimer_Tick(object sender, EventArgs e)
        {
            bool needsRefresh = false;
            double speedMtPerSec = (50.0 / 60.0) * 15.0;
            double moveAmt = speedMtPerSec * (_evacuationTimer.Interval / 1000.0);

            foreach (var h in _helmetsList)
            {
                if (h.IsFallen || !h.IsEvacuating || h.IsEvacuated || h.IsTrapped) continue;

                var node = _graph.Nodes[h.NodeId];
                if (node.Type == NodeType.MainExit || node.Type == NodeType.AlternativeExit || node.Type == NodeType.Shelter)
                {
                    h.IsEvacuating = false;
                    h.IsEvacuated = true;
                    h.EvacuationPoint = node.Name.Contains("[") ? node.Name.Split('[')[0].Trim() : node.Name.Split('(')[0].Trim();
                    Log($"✅ [BAŞARILI] {h.Name} güvenli bölgeye ({h.EvacuationPoint}) ulaştı!");

                    if (_isRealHardwareMode && h.Id == "B-01") SendGuidanceToHardware("VIB:SUCCESS");

                    UpdateEvacuatedList();
                    UpdateHelmetTable();
                    RecalculateRoute();
                    needsRefresh = true;
                    continue;
                }

                if (string.IsNullOrEmpty(h.TargetNodeId))
                {
                    var res = FindBestRouteWithConstraints(h);
                    h.Route = res;

                    if (res.Success && res.Route.Count > 1)
                    {
                        h.TargetNodeId = res.Route[1].Id;
                        h.EdgeProgress = 0;
                    }
                    else
                    {
                        h.IsEvacuating = false;
                    }
                }

                if (!string.IsNullOrEmpty(h.TargetNodeId))
                {
                    MineNode n1 = _graph.Nodes[h.NodeId];
                    MineNode n2 = _graph.Nodes[h.TargetNodeId];
                    double edgeDist = Math.Sqrt(Math.Pow(n1.X - n2.X, 2) + Math.Pow(n1.Y - n2.Y, 2));

                    double currentDist = h.EdgeProgress * edgeDist;
                    currentDist += moveAmt;

                    if (currentDist >= edgeDist)
                    {
                        h.NodeId = h.TargetNodeId;
                        h.TargetNodeId = "";
                        h.EdgeProgress = 0;

                        var newNode = _graph.Nodes[h.NodeId];
                        if (newNode.Type == NodeType.MainExit || newNode.Type == NodeType.AlternativeExit || newNode.Type == NodeType.Shelter)
                        {
                            h.IsEvacuating = false;
                            h.IsEvacuated = true;
                            h.EvacuationPoint = newNode.Name.Contains("[") ? newNode.Name.Split('[')[0].Trim() : newNode.Name.Split('(')[0].Trim();
                            Log($"✅ [BAŞARILI] {h.Name} güvenli bölgeye ({h.EvacuationPoint}) ulaştı!");

                            if (_isRealHardwareMode && h.Id == "B-01") SendGuidanceToHardware("VIB:SUCCESS");

                            UpdateEvacuatedList();
                        }
                        RecalculateRoute();
                    }
                    else
                    {
                        h.EdgeProgress = currentDist / edgeDist;

                        HelmetData selectedLeftPanelHelmet = _cmbLeftHelmets.SelectedItem as HelmetData;
                        if (selectedLeftPanelHelmet != null && selectedLeftPanelHelmet.Id == h.Id && h.Route != null && h.Route.Success)
                        {
                            double covered = edgeDist * h.EdgeProgress;
                            double remaining = Math.Max(0, h.Route.Distance - covered);
                            double etaMins = remaining / 50.0;
                            _lblETA.Text = $"{remaining:F1}m | ETA: {etaMins:F1} dk";
                        }
                    }
                    needsRefresh = true;
                }
            }

            if (needsRefresh) _mapControl.Invalidate();
        }

        private void SystemTimer_Tick(object sender, EventArgs e)
        {
            foreach (var h in _helmetsList)
            {
                double dropRate = h.IsEvacuating ? 0.05 : 0.02;
                h.Battery -= dropRate;
                if (h.Battery < 0) h.Battery = 0;

                if (_isGlobalEmergency && !h.IsEvacuated && !h.IsFallen)
                {
                    h.OfkSecondsRemaining -= 15;
                    if (h.OfkSecondsRemaining < 0) h.OfkSecondsRemaining = 0;
                }

                if (h.IsFallen) h.HeartRate = _rnd.Next(135, 148);
                else if (h.IsTrapped) h.HeartRate = _rnd.Next(125, 138);
                else if (h.IsEvacuating) h.HeartRate = _rnd.Next(105, 118);
                else if (_isGlobalEmergency && !h.IsEvacuated) h.HeartRate = _rnd.Next(95, 110);
                else h.HeartRate = _rnd.Next(72, 85);
            }

            if (_dgvSensors.Rows.Count == 0)
            {
                _dgvSensors.Rows.Add("IoT-01", "N3", "%0,16", "22,4°C", "NORMAL");
                _dgvSensors.Rows.Add("IoT-02", "N6", "%0,16", "22,4°C", "NORMAL");
                _dgvSensors.Rows.Add("IoT-03", "N8", "%0,16", "22,4°C", "NORMAL");
                _dgvSensors.Rows.Add("IoT-04", "N10", "%0,16", "22,4°C", "NORMAL");
            }

            if (_isRealHardwareMode)
            {
                RecalculateRoute();
                UpdateHelmetTable();
                _mapControl.Invalidate();
                return;
            }

            bool dangerDetected = false;
            int totalAnomaliesInMine = _anomalies.Values.Sum(a => a.Count);

            for (int i = 0; i < _dgvSensors.Rows.Count; i++)
            {
                string iot = _dgvSensors.Rows[i].Cells[0].Value.ToString();
                var anom = _anomalies[iot];

                string ch4 = "%0,16";
                string temp = "22,4°C";

                if (anom.Contains("Gas")) ch4 = "%1,80";
                if (anom.Contains("Temp")) temp = "48,5°C";

                _dgvSensors.Rows[i].Cells[2].Value = ch4;
                _dgvSensors.Rows[i].Cells[3].Value = temp;

                if (anom.Count == 0)
                {
                    _dgvSensors.Rows[i].Cells[4].Value = "NORMAL";
                    _dgvSensors.Rows[i].DefaultCellStyle.BackColor = Color.FromArgb(25, 35, 50);
                    _graph.Nodes[iot].IsDangerous = false;
                }
                else
                {
                    if (totalAnomaliesInMine >= 2)
                    {
                        _handledSingleAnomalies.Remove(iot);
                        _manualOverrides.Remove(iot);

                        _dgvSensors.Rows[i].Cells[4].Value = "TEHLİKE (Çapraz Doğrulandı)";
                        _dgvSensors.Rows[i].DefaultCellStyle.BackColor = Color.DarkRed;
                        _graph.Nodes[iot].IsDangerous = true;
                        dangerDetected = true;
                    }
                    else if (totalAnomaliesInMine == 1)
                    {
                        if (!_handledSingleAnomalies.Contains(iot))
                        {
                            _handledSingleAnomalies.Add(iot);
                            _systemTimer.Stop();
                            string anomType = anom.First();
                            string trAnom = anomType == "Gas" ? "MQ-4 Metan Uyarısı (%1.0 Sınırı Aşıldı)" : (anomType == "Temp" ? "LM35 Sıcaklık Uyarısı (35°C Aşıldı)" : "MPU6050 Göçük/Sarsıntı");

                            bool evacuate = ShowManualDecisionPopup(iot, trAnom);

                            if (evacuate)
                            {
                                _manualOverrides.Add(iot);
                            }
                            else
                            {
                                _manualOverrides.Remove(iot);
                            }
                            _systemTimer.Start();
                        }

                        if (_manualOverrides.Contains(iot))
                        {
                            _dgvSensors.Rows[i].Cells[4].Value = "MANUEL TAHLİYE";
                            _dgvSensors.Rows[i].DefaultCellStyle.BackColor = Color.DarkRed;
                            _graph.Nodes[iot].IsDangerous = true;
                            dangerDetected = true;
                        }
                        else
                        {
                            _dgvSensors.Rows[i].Cells[4].Value = "UYARI (Bekliyor)";
                            _dgvSensors.Rows[i].DefaultCellStyle.BackColor = Color.DarkGoldenrod;
                            _graph.Nodes[iot].IsDangerous = false;
                        }
                    }
                }
            }

            if (dangerDetected && !_evacuationTimer.Enabled)
            {
                _isGlobalEmergency = true;
                foreach (var h in _helmetsList)
                {
                    if (!h.IsFallen && !h.IsEvacuated) h.IsEvacuating = true;
                }
                _evacuationTimer.Start();
                _lblSystemStatus.Text = "🚨 SİSTEM DURUMU: ACİL TAHLİYE DEVREDE";
                _lblSystemStatus.BackColor = Color.DarkRed;
                Log("[OTONOM KONTROL] Maden genelinde tehlike algılandı! Sistem tahliyeyi OTOMATİK başlattı.");
            }

            RecalculateRoute();
            UpdateHelmetTable();
            _mapControl.Invalidate();
        }

        private RouteResult CalculateOptimalRoute(string startNodeId, List<NodeType> targetTypes, bool ignoreDangers)
        {
            var distances = new Dictionary<string, double>();
            var previous = new Dictionary<string, MineNode>();
            var unvisited = new List<MineNode>();

            foreach (var node in _graph.Nodes.Values)
            {
                if (!ignoreDangers && node.IsDangerous && node.Id != startNodeId) continue;
                distances[node.Id] = double.MaxValue;
                unvisited.Add(node);
            }

            if (!distances.ContainsKey(startNodeId)) return new RouteResult { Success = false };

            distances[startNodeId] = 0;

            while (unvisited.Count > 0)
            {
                unvisited = unvisited.OrderBy(n => distances[n.Id]).ToList();
                var current = unvisited.First();
                unvisited.Remove(current);

                if (distances[current.Id] == double.MaxValue) break;

                foreach (var neighbor in current.Neighbors)
                {
                    if (!ignoreDangers && neighbor.IsDangerous) continue;
                    if (!unvisited.Contains(neighbor)) continue;

                    double dist = Math.Sqrt(Math.Pow(current.X - neighbor.X, 2) + Math.Pow(current.Y - neighbor.Y, 2));
                    double alt = distances[current.Id] + dist;

                    if (alt < distances[neighbor.Id])
                    {
                        distances[neighbor.Id] = alt;
                        previous[neighbor.Id] = current;
                    }
                }
            }

            MineNode bestTarget = null;
            double minTargetDist = double.MaxValue;

            foreach (var node in _graph.Nodes.Values)
            {
                if (targetTypes.Contains(node.Type) && distances.ContainsKey(node.Id) && distances[node.Id] < minTargetDist)
                {
                    minTargetDist = distances[node.Id];
                    bestTarget = node;
                }
            }

            if (bestTarget == null) return new RouteResult { Success = false };

            var path = new List<MineNode>();
            var curr = bestTarget;
            while (curr != null)
            {
                path.Add(curr);
                previous.TryGetValue(curr.Id, out curr);
            }
            path.Reverse();

            return new RouteResult { Success = true, Distance = minTargetDist, Destination = bestTarget, Route = path };
        }

        private RouteResult FindBestRouteWithConstraints(HelmetData h)
        {
            var exitTypes = new List<NodeType> { NodeType.MainExit, NodeType.AlternativeExit };
            var shelterTypes = new List<NodeType> { NodeType.Shelter };

            var exitRoute = CalculateOptimalRoute(h.NodeId, exitTypes, false);

            if (exitRoute.Success)
            {
                double etaMins = exitRoute.Distance / 50.0;
                double requiredOfkSecs = etaMins * 60;
                double requiredBattery = requiredOfkSecs * 0.05;

                if (h.OfkSecondsRemaining < requiredOfkSecs + 60 || h.Battery < requiredBattery + 2.0)
                {
                    var shelterRoute = CalculateOptimalRoute(h.NodeId, shelterTypes, false);
                    if (shelterRoute.Success)
                    {
                        h.IsForcedToShelter = true;
                        return shelterRoute;
                    }
                }
                h.IsForcedToShelter = false;
                return exitRoute;
            }

            var emergencyShelter = CalculateOptimalRoute(h.NodeId, shelterTypes, false);
            if (emergencyShelter.Success)
            {
                h.IsForcedToShelter = true;
                return emergencyShelter;
            }

            return new RouteResult { Success = false };
        }

        private string DetermineDirection(MineNode current, MineNode next)
        {
            if (current == null || next == null) return "BİLİNMİYOR";
            float dx = next.X - current.X;
            float dy = next.Y - current.Y;

            if (Math.Abs(dx) > Math.Abs(dy)) return dx > 0 ? "SAĞA DÖN" : "SOLA DÖN";
            else return dy > 0 ? "GERİ DÖN" : "DÜZ DEVAM ET";
        }

        private void RecalculateRoute()
        {
            var mapHelmets = new List<MapHelmet>();
            HelmetData selectedLeftPanelHelmet = _cmbLeftHelmets.SelectedItem as HelmetData;

            foreach (var h in _helmetsList)
            {
                if (h.IsEvacuated)
                {
                    mapHelmets.Add(new MapHelmet { Id = h.Id, Name = h.Name, NodeId = h.NodeId, TargetNodeId = "", Progress = 0, Route = null, IsFallen = false, IsTrapped = false });

                    if (selectedLeftPanelHelmet != null && selectedLeftPanelHelmet.Id == h.Id)
                    {
                        _lblDistance.Text = h.EvacuationPoint;
                        _lblTargetExit.Text = "TAHLİYE EDİLDİ";
                        _lblTargetExit.ForeColor = Color.LimeGreen;
                        _lblSafe.Text = "GÜVENDE";
                        _lblSafe.ForeColor = Color.LimeGreen;
                        _lblNextDirection.Text = "HAREKET YOK";
                        _lblNextNode.Text = "GÜVENLİ BÖLGE";
                        _lblETA.Text = "0 dk";
                        _lblOFK.Text = "TAHLİYE BAŞARILI (Kapalı)";
                        _lblOFK.ForeColor = Color.LimeGreen;
                        _lblPath.Text = "-";

                        _lblBPM.Text = $"{h.HeartRate} BPM (Normal)";
                        _lblBPM.ForeColor = Color.LimeGreen;
                    }
                    continue;
                }

                if (!_isGlobalEmergency && !h.IsFallen && !h.IsTrapped)
                {
                    mapHelmets.Add(new MapHelmet { Id = h.Id, Name = h.Name, NodeId = h.NodeId, TargetNodeId = "", Progress = 0, Route = null, IsFallen = false, IsTrapped = false });
                    if (selectedLeftPanelHelmet != null && selectedLeftPanelHelmet.Id == h.Id)
                    {
                        _lblDistance.Text = h.NodeId.Contains("IoT") ? h.NodeId : _graph.Nodes[h.NodeId].Name;
                        _lblTargetExit.Text = "BEKLEMEDE (Çalışıyor)";
                        _lblTargetExit.ForeColor = Color.LightGray;
                        _lblSafe.Text = "GÜVENLİ";
                        _lblSafe.ForeColor = Color.LimeGreen;
                        _lblNextDirection.Text = "SABİT (KOMUT YOK)";
                        _lblNextDirection.ForeColor = Color.White;
                        _lblNextNode.Text = "MEVCUT KONUM";
                        _lblETA.Text = "-";
                        _lblOFK.Text = "PASİF (Kemerde Hazır 30:00)";
                        _lblOFK.ForeColor = Color.White;
                        _lblPath.Text = "GÜZERGAH GEREKMİYOR";
                        _lblBPM.Text = $"{h.HeartRate} BPM (Normal)";
                        _lblBPM.ForeColor = Color.White;
                    }
                    continue;
                }

                var result = FindBestRouteWithConstraints(h);
                h.Route = result;

                if (h.IsFallen)
                {
                    h.IsTrapped = false; h.TargetNodeId = ""; h.EdgeProgress = 0; h.IsEvacuating = false;
                    var rescueResult = CalculateOptimalRoute(h.NodeId, new List<NodeType> { NodeType.MainExit, NodeType.AlternativeExit }, true);
                    h.RescueRoute = rescueResult.Success ? rescueResult : null;
                }
                else if (!result.Success)
                {
                    h.IsTrapped = true; h.TargetNodeId = ""; h.EdgeProgress = 0; h.IsEvacuating = false;
                    var rescueResult = CalculateOptimalRoute(h.NodeId, new List<NodeType> { NodeType.MainExit, NodeType.AlternativeExit }, true);
                    h.RescueRoute = rescueResult.Success ? rescueResult : null;
                }
                else
                {
                    h.IsTrapped = false; h.RescueRoute = null;
                }

                if (!string.IsNullOrEmpty(h.TargetNodeId) && result.Success && result.Route.Count > 1)
                {
                    if (result.Route[1].Id != h.TargetNodeId)
                    {
                        h.TargetNodeId = ""; h.EdgeProgress = 0;
                    }
                }

                mapHelmets.Add(new MapHelmet { Id = h.Id, Name = h.Name, NodeId = h.NodeId, TargetNodeId = h.TargetNodeId, Progress = (float)h.EdgeProgress, Route = result, IsFallen = h.IsFallen, IsTrapped = h.IsTrapped, RescueRoute = h.RescueRoute });

                if (selectedLeftPanelHelmet != null && selectedLeftPanelHelmet.Id == h.Id)
                {
                    _lblDistance.Text = h.NodeId.Contains("IoT") ? h.NodeId : _graph.Nodes[h.NodeId].Name;
                    string ofkFormat = $"{h.OfkSecondsRemaining / 60:D2}:{h.OfkSecondsRemaining % 60:D2}";

                    if (h.HeartRate > 120)
                    {
                        _lblBPM.Text = $"{h.HeartRate} BPM (Panik/Stres Yüksek ⚠️)";
                        _lblBPM.ForeColor = Color.Red;
                    }
                    else if (h.HeartRate > 100)
                    {
                        _lblBPM.Text = $"{h.HeartRate} BPM (Efor Harcıyor)";
                        _lblBPM.ForeColor = Color.Orange;
                    }
                    else
                    {
                        _lblBPM.Text = $"{h.HeartRate} BPM (Normal)";
                        _lblBPM.ForeColor = Color.LimeGreen;
                    }

                    if (h.IsFallen)
                    {
                        _lblTargetExit.Text = "KURTARMA EKİBİ YOLDA";
                        _lblTargetExit.ForeColor = Color.Red;
                        _lblETA.Text = "BİLİNMİYOR";
                        _lblSafe.Text = "YARALI (ACİL DURUM)";
                        _lblSafe.ForeColor = Color.Red;
                        _lblNextDirection.Text = "HAREKETSİZ KAL";
                        _lblNextDirection.ForeColor = Color.White;
                        _lblNextNode.Text = "YARDIM BEKLENİYOR";
                        _lblOFK.Text = "BİLİNÇ KAPALI";
                        _lblOFK.ForeColor = Color.Red;
                        if (h.RescueRoute != null) _lblPath.Text = string.Join(" > ", h.RescueRoute.Route.AsEnumerable().Reverse().Select(n => n.Name.Contains("[") ? n.Name.Split('[')[0].Trim() : n.Name.Split('(')[1].Replace(")", "").Trim()));
                        else _lblPath.Text = "ÖZEL EKİP (MOR) GÜZERGAHI";

                        if (_isRealHardwareMode && h.Id == "B-01") SendGuidanceToHardware("VIB:FALLEN");
                    }
                    else if (h.IsTrapped)
                    {
                        _lblTargetExit.Text = "TAHLİSİYE (KURTARMA) EKİBİ";
                        _lblTargetExit.ForeColor = Color.DarkOrange;
                        _lblETA.Text = "BEKLENİYOR";
                        _lblSafe.Text = "MAHSUR KALDI (TRAPPED)";
                        _lblSafe.ForeColor = Color.DarkOrange;
                        _lblNextDirection.Text = "GÜVENLİ YERDE BEKLE";
                        _lblNextDirection.ForeColor = Color.DarkOrange;
                        _lblNextNode.Text = "HAREKETSİZ";
                        _lblOFK.Text = $"AZALIYOR (Kalan: {ofkFormat})";
                        _lblOFK.ForeColor = h.OfkSecondsRemaining < 300 ? Color.Red : Color.Orange;
                        if (h.RescueRoute != null) _lblPath.Text = string.Join(" > ", h.RescueRoute.Route.AsEnumerable().Reverse().Select(n => n.Name.Contains("[") ? n.Name.Split('[')[0].Trim() : n.Name.Split('(')[1].Replace(")", "").Trim()));
                        else _lblPath.Text = "KURTARMA ROTASI HESAPLANIYOR";

                        if (_isRealHardwareMode && h.Id == "B-01") SendGuidanceToHardware("VIB:TRAPPED");
                    }
                    else if (result.Success)
                    {
                        _lblPath.Text = string.Join(" > ", result.Route.Select(n => n.Name.Contains("[") ? n.Name.Split('[')[0].Trim() : n.Name.Split('(')[1].Replace(")", "").Trim()));
                        _lblTargetExit.Text = h.IsForcedToShelter ? "SIĞINMA ODASI (KAYNAK YETERSİZ)" : result.Destination.Name.ToUpper();
                        _lblTargetExit.ForeColor = h.IsForcedToShelter ? Color.DarkOrange : Color.LimeGreen;

                        if (!h.IsEvacuating)
                        {
                            double etaMins = Math.Round(result.Distance / 50.0, 1);
                            _lblETA.Text = $"{Math.Round(result.Distance, 0)}m | ETA: {etaMins} dk";
                        }

                        _lblSafe.Text = h.IsForcedToShelter ? "RİSKLİ YÖNLENDİRME (PİL/OFK)" : "GÜVENLİ (TAHLİYE OLUYOR)";
                        _lblSafe.ForeColor = h.IsForcedToShelter ? Color.DarkOrange : Color.LimeGreen;

                        _lblOFK.Text = $"AKTİF KULLANIM ({ofkFormat})";
                        _lblOFK.ForeColor = h.OfkSecondsRemaining < 300 ? Color.Red : Color.LimeGreen;

                        if (result.Route.Count > 1)
                        {
                            MineNode current = _graph.Nodes[h.NodeId];
                            MineNode next = result.Route[1];

                            // YANLIŞ YOL TESPİTİ İÇİN HEDEFE KİLİTLENME
                            h.ExpectedNextNodeId = next.Id;

                            string direction = DetermineDirection(current, next);

                            _lblNextDirection.Text = direction;
                            _lblNextDirection.ForeColor = Color.Cyan;
                            _lblNextNode.Text = next.Name.Contains("[") ? next.Name.Split('[')[0].Trim() : next.Name;

                            if (_isRealHardwareMode && h.Id == "B-01")
                            {
                                if (direction == "SAĞA DÖN") SendGuidanceToHardware("VIB:RIGHT");
                                else if (direction == "SOLA DÖN") SendGuidanceToHardware("VIB:LEFT");
                                else SendGuidanceToHardware("VIB:FORWARD");
                            }

                        }
                        else
                        {
                            _lblNextDirection.Text = "TAHLİYE EDİLDİ";
                            _lblNextDirection.ForeColor = Color.LimeGreen;
                            _lblNextNode.Text = "-";
                        }
                    }
                }
            }
            _mapControl.ActiveHelmets = mapHelmets;
        }

        private bool ShowManualDecisionPopup(string sensorName, string anomalyName)
        {
            Form popup = new Form();
            popup.Size = new Size(500, 260);
            popup.BackColor = Color.FromArgb(20, 5, 5);
            popup.FormBorderStyle = FormBorderStyle.None;
            popup.StartPosition = FormStartPosition.CenterScreen;
            popup.TopMost = true;

            Panel border = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle };
            popup.Controls.Add(border);

            Label title = new Label { Text = "⚠️ DİKKAT: TEKİL SENSÖR UYARISI", Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = Color.Orange, Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleCenter, Height = 50 };
            border.Controls.Add(title);

            Label msg = new Label
            {
                Text = $"{sensorName} modülünde sadece '{anomalyName}' tespit edildi.\n\n" +
                       "Bu durum diğer çevresel sensörlerle (çapraz füzyon) doğrulanmadı.\n" +
                       "Yalancı alarm (False-Positive) veya lokal bir arıza olabilir.\n\n" +
                       "Lütfen operatör inisiyatifi kullanarak manuel karar verin:",
                Font = new Font("Segoe UI", 11),
                ForeColor = Color.White,
                Location = new Point(20, 55),
                AutoSize = true
            };
            border.Controls.Add(msg);

            bool decision = false;
            Button btnEvacuate = new Button { Text = "🚨 TAHLİYE ET", BackColor = Color.DarkRed, ForeColor = Color.White, Font = new Font("Segoe UI", 12, FontStyle.Bold), FlatStyle = FlatStyle.Flat, Location = new Point(35, 180), Size = new Size(190, 50) };
            btnEvacuate.Click += (s, e) => { decision = true; popup.Close(); };
            border.Controls.Add(btnEvacuate);

            Button btnWait = new Button { Text = "⏱️ BEKLE (İZLE)", BackColor = Color.DarkGoldenrod, ForeColor = Color.White, Font = new Font("Segoe UI", 12, FontStyle.Bold), FlatStyle = FlatStyle.Flat, Location = new Point(275, 180), Size = new Size(190, 50) };
            btnWait.Click += (s, e) => { decision = false; popup.Close(); };
            border.Controls.Add(btnWait);

            popup.ShowDialog();
            return decision;
        }

        private void UpdateHelmetTable()
        {
            _dgvHelmets.Rows.Clear();
            foreach (var h in _helmetsList)
            {
                string bpmStr = $"{h.HeartRate} BPM";
                if (h.HeartRate >= 120) bpmStr += " ⚠️";

                string motion = h.IsEvacuated ? "Güvende" : (h.IsFallen ? "Hareketsiz" : (h.IsTrapped ? "Mahsur" : (h.IsEvacuating ? "Yürüyor" : "Çalışıyor")));
                string status = h.IsEvacuated ? "BİTTİ" : (h.IsFallen ? "YARALI (KRİTİK)" : (h.IsTrapped ? "MAHSUR" : (h.IsForcedToShelter ? "SIĞINMA" : "Normal")));
                string ofkFormat = (_isGlobalEmergency || h.IsTrapped) ? $"{h.OfkSecondsRemaining / 60:D2}:{h.OfkSecondsRemaining % 60:D2}" : "30:00";

                int rIdx = _dgvHelmets.Rows.Add(h.Id, h.Name, h.IsEvacuated ? h.EvacuationPoint : h.NodeId, bpmStr, motion, status, h.Battery.ToString("F1") + "%", ofkFormat);

                if (h.IsFallen)
                {
                    _dgvHelmets.Rows[rIdx].DefaultCellStyle.BackColor = Color.DarkRed;
                    _dgvHelmets.Rows[rIdx].DefaultCellStyle.ForeColor = Color.White;
                }
                else if (h.IsTrapped || h.IsForcedToShelter)
                {
                    _dgvHelmets.Rows[rIdx].DefaultCellStyle.BackColor = Color.DarkOrange;
                    _dgvHelmets.Rows[rIdx].DefaultCellStyle.ForeColor = Color.Black;
                }
                else if (h.IsEvacuated)
                {
                    _dgvHelmets.Rows[rIdx].DefaultCellStyle.BackColor = Color.FromArgb(0, 100, 50);
                    _dgvHelmets.Rows[rIdx].DefaultCellStyle.ForeColor = Color.White;
                }
            }
        }

        private void Log(string message)
        {
            if (_rtbLog.TextLength > 10000) _rtbLog.Clear();
            _rtbLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
            _rtbLog.ScrollToCaret();
        }
    }
}