using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Globalization;

namespace SilenceCutterGUI
{
    public class Form1 : Form
    {
        private TextBox txtVideoPath;
        private Button btnBrowse;
        private TextBox txtThreshold;
        private TextBox txtDuration;
        private TextBox txtPadding;
        private Button btnProcess;
        private DataGridView gridCortes;
        private Label lblStatus;
        private ProgressBar progressBar;
        private TextBox txtResultPath;
        
        private string ffmpegPath = "";
        private string ffprobePath = "";
        private string lastDirectory = "";

        public Form1(string[] args)
        {
            InitializeComponent();
            ParseArgs(args);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ExtractEmbeddedFFmpeg();
        }

        private void ExtractEmbeddedFFmpeg()
        {
            try
            {
                lblStatus.Text = "ESTADO: Descomprimiendo motor FFmpeg interno (puede tardar unos segundos)...";
                this.Refresh(); // Force UI update

                string tempDir = Path.Combine(Path.GetTempPath(), "SilenceCutterCores");
                Directory.CreateDirectory(tempDir);

                ffmpegPath = Path.Combine(tempDir, "ffmpeg.exe");
                ffprobePath = Path.Combine(tempDir, "ffprobe.exe");

                ExtractResource("SilenceCutterGUI.ffmpeg.exe", ffmpegPath);
                ExtractResource("SilenceCutterGUI.ffprobe.exe", ffprobePath);

                lblStatus.Text = "Súper-Motor FFmpeg Extraído. Aplicación 100% Autocontenida Lista.";
                lblStatus.ForeColor = Color.LightGreen;
            }
            catch (Exception ex)
            {
                btnProcess.Enabled = false;
                gridCortes.Rows.Add(1, "ERROR FATAL", "DESCOMPRESIÓN", ex.Message, "BLOQUEADO");
                lblStatus.Text = "ESTADO CRÍTICO: Fallo al extraer dependencias incrustadas.";
                lblStatus.ForeColor = Color.Red;
            }
        }

        private void ExtractResource(string resourceName, string outPath)
        {
            // Solo extraemos si no existe o vamos a reescribir, usamos check tonto de nombre
            if (!File.Exists(outPath))
            {
                using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                {
                    if (stream == null) throw new Exception("Recurso " + resourceName + " no fue inyectado en el EXE original.");
                    using (var fStream = new FileStream(outPath, FileMode.Create))
                    {
                        stream.CopyTo(fStream);
                    }
                }
            }
        }

        private void InitializeComponent()
        {
            this.Text = "Silence Cutter GUI + CLI (Engine Davinci EDL)";
            this.Size = new Size(800, 650);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.White;

            Font mainFont = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            
            Label lblTitle = new Label() { Text = "Dashboard de Cortes de Audio", Font = new Font("Segoe UI", 14F, FontStyle.Bold), ForeColor = Color.LightSkyBlue, Top = 10, Left = 20, Width = 400 };

            Label l1 = new Label() { Text = "Video Archivo (.mp4, .mov):", Top = 50, Left = 20, Width = 190, Font = mainFont };
            txtVideoPath = new TextBox() { Top = 50, Left = 210, Width = 450, Font = mainFont, BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White };
            btnBrowse = new Button() { Text = "Buscar", Top = 48, Left = 670, Width = 80, Font = mainFont, BackColor = Color.DarkSlateBlue, FlatStyle = FlatStyle.Flat };
            btnBrowse.Click += BtnBrowse_Click;

            Label l2 = new Label() { Text = "Silencio (dB):", Top = 90, Left = 20, Width = 100, Font = mainFont };
            txtThreshold = new TextBox() { Text = "-35", Top = 90, Left = 120, Width = 60, Font = mainFont, BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White };

            Label l3 = new Label() { Text = "Mín. Duración (s):", Top = 90, Left = 200, Width = 120, Font = mainFont };
            txtDuration = new TextBox() { Text = "0.8", Top = 90, Left = 320, Width = 60, Font = mainFont, BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White };

            Label l4 = new Label() { Text = "Offset/Padding (s):", Top = 90, Left = 400, Width = 130, Font = mainFont };
            txtPadding = new TextBox() { Text = "0.15", Top = 90, Left = 530, Width = 60, Font = mainFont, BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White };

            btnProcess = new Button() { Text = "⚡ Generar EDL", Top = 85, Left = 610, Width = 140, Height = 35, Font = new Font("Segoe UI", 9F, FontStyle.Bold), BackColor = Color.OrangeRed, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnProcess.Click += async (s, e) => await ProcessVideo();

            gridCortes = new DataGridView() { Top = 140, Left = 20, Width = 740, Height = 330 };
            gridCortes.BackgroundColor = Color.FromArgb(40, 40, 40);
            gridCortes.ForeColor = Color.Black;
            gridCortes.Columns.Add("Id", "#");
            gridCortes.Columns.Add("Tipo", "Tipo");
            gridCortes.Columns.Add("Start", "Inicio (s)");
            gridCortes.Columns.Add("End", "Fin (s)");
            gridCortes.Columns.Add("Dur", "Duración (s)");
            gridCortes.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            gridCortes.AllowUserToAddRows = false;
            gridCortes.RowHeadersVisible = false;

            progressBar = new ProgressBar() { Top = 480, Left = 20, Width = 740, Height = 10, Style = ProgressBarStyle.Continuous };
            
            Label lRes = new Label() { Text = "Salida del Timeline (EDL):", Top = 505, Left = 20, Width = 170, Font = mainFont, ForeColor = Color.Yellow };
            txtResultPath = new TextBox() { Top = 505, Left = 190, Width = 570, Font = new Font("Segoe UI", 9F, FontStyle.Bold), BackColor = Color.FromArgb(20, 20, 20), ForeColor = Color.LimeGreen, ReadOnly = true };

            lblStatus = new Label() { Text = "Estado: Listo para comenzar.", Top = 545, Left = 20, Width = 740, Font = mainFont, ForeColor = Color.LightGray };

            this.Controls.Add(lblTitle);
            this.Controls.Add(l1); this.Controls.Add(txtVideoPath); this.Controls.Add(btnBrowse);
            this.Controls.Add(l2); this.Controls.Add(txtThreshold);
            this.Controls.Add(l3); this.Controls.Add(txtDuration);
            this.Controls.Add(l4); this.Controls.Add(txtPadding);
            this.Controls.Add(btnProcess);
            this.Controls.Add(gridCortes);
            this.Controls.Add(progressBar);
            this.Controls.Add(lRes); this.Controls.Add(txtResultPath);
            this.Controls.Add(lblStatus);
        }

        private void ParseArgs(string[] args)
        {
            bool autoRun = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--video" && i + 1 < args.Length) txtVideoPath.Text = args[++i];
                if (args[i] == "--offset" && i + 1 < args.Length) txtPadding.Text = args[++i];
                if (args[i] == "--threshold" && i + 1 < args.Length) txtThreshold.Text = args[++i];
                if (args[i] == "--autorun") autoRun = true;
            }

            if (autoRun && !string.IsNullOrWhiteSpace(txtVideoPath.Text))
            {
                this.Shown += async (s, e) => await ProcessVideo();
            }
        }

        private void BtnBrowse_Click(object? sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Video Files|*.mp4;*.mov;*.mkv|Audio Files|*.wav;*.mp3|All Files|*.*";
                
                if (!string.IsNullOrEmpty(lastDirectory) && Directory.Exists(lastDirectory))
                    ofd.InitialDirectory = lastDirectory;
                else if (!string.IsNullOrWhiteSpace(txtVideoPath.Text) && File.Exists(txtVideoPath.Text))
                    ofd.InitialDirectory = Path.GetDirectoryName(txtVideoPath.Text) ?? "";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    txtVideoPath.Text = ofd.FileName;
                    lastDirectory = Path.GetDirectoryName(ofd.FileName) ?? "";
                }
            }
        }

        private async Task ProcessVideo()
        {
            if (string.IsNullOrWhiteSpace(txtVideoPath.Text) || !File.Exists(txtVideoPath.Text))
            {
                MessageBox.Show("Ruta de archivo de video no válida.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            btnProcess.Enabled = false;
            gridCortes.Rows.Clear();
            txtResultPath.Text = "";
            lblStatus.Text = "Estado: Analizando audio con FFmpeg...";
            progressBar.Style = ProgressBarStyle.Marquee;

            try
            {
                string video = txtVideoPath.Text;
                string thresh = txtThreshold.Text.EndsWith("dB") ? txtThreshold.Text : txtThreshold.Text + "dB";
                string dur = txtDuration.Text;
                
                double pad = 0.15;
                double.TryParse(txtPadding.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out pad);

                var (silences, totalDur) = await Task.Run(() => AnalyzeSilences(video, thresh, dur));
                
                if (totalDur == 0.0) totalDur = 43200.0;

                if (silences.Count == 0)
                {
                    lblStatus.Text = "Estado: No se encontraron silencios.";
                }
                else
                {
                    // Nombre basado en el video, extensión .edl (el formato que YA FUNCIONA)
                    string videoName = Path.GetFileNameWithoutExtension(video);
                    string videoDir = Path.GetDirectoryName(video) ?? "";
                    string edlPath = Path.Combine(videoDir, videoName + "_CORTES_FINAL.edl");
                    
                    var keeps = GenerateEDL(silences, edlPath, totalDur, pad);
                    
                    txtResultPath.Text = edlPath;
                    lblStatus.Text = $"¡Listo! {keeps} clips generados. Archivo: {Path.GetFileName(edlPath)}";
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Error: " + ex.Message;
            }
            finally
            {
                progressBar.Style = ProgressBarStyle.Continuous;
                btnProcess.Enabled = true;
            }
        }

        private double GetDuration(string videoPath)
        {
            try {
                var probeInfo = new ProcessStartInfo(string.IsNullOrEmpty(ffprobePath) ? "ffprobe" : ffprobePath, $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"")
                { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                
                using (var p = Process.Start(probeInfo)) {
                    if (p != null) {
                        string res = p.StandardOutput.ReadToEnd().Trim();
                        if (double.TryParse(res, NumberStyles.Any, CultureInfo.InvariantCulture, out double d)) return d;
                    }
                }
            } catch {}
            return 0.0;
        }

        private (List<(double Start, double End)>, double) AnalyzeSilences(string videoPath, string threshold, string duration)
        {
            double totalDur = GetDuration(videoPath);
            
            var ffInfo = new ProcessStartInfo(string.IsNullOrEmpty(ffmpegPath) ? "ffmpeg" : ffmpegPath, $"-i \"{videoPath}\" -af silencedetect=noise={threshold}:d={duration} -f null -")
            { RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };

            var silencias = new List<(double, double)>();
            using (var p = Process.Start(ffInfo))
            {
                if (p != null)
                {
                    string output = p.StandardError.ReadToEnd();
                    var starts = Regex.Matches(output, @"silence_start: ([\d\.]+)");
                    var ends = Regex.Matches(output, @"silence_end: ([\d\.]+)");
                    
                    for (int i = 0; i < starts.Count && i < ends.Count; i++)
                    {
                        double s = double.Parse(starts[i].Groups[1].Value, CultureInfo.InvariantCulture);
                        double e = double.Parse(ends[i].Groups[1].Value, CultureInfo.InvariantCulture);
                        silencias.Add((s, e));
                    }
                }
            }
            return (silencias, totalDur);
        }

        // ============================================================
        // FORMATO EDL IDÉNTICO AL SCRIPT PYTHON QUE YA FUNCIONÓ
        // ============================================================
        private string FrameToTimecode(double seconds, int fps = 30)
        {
            int h = (int)(seconds / 3600);
            int m = (int)((seconds % 3600) / 60);
            int s = (int)(seconds % 60);
            int f = (int)((seconds - (int)seconds) * fps);
            return $"{h:D2}:{m:D2}:{s:D2}:{f:D2}";
        }

        private int GenerateEDL(List<(double Start, double End)> silences, string edlPath, double totalDur, double pad)
        {
            // Calcular segmentos a MANTENER (donde se habla) — EXACTO al Python original
            var keeps = new List<(double Start, double End)>();
            double currentTime = 0.0;

            foreach (var (sStart, sEnd) in silences)
            {
                // pad: extendemos un poco el corte para no cortar sílabas
                double adjustedSilenceStart = sStart + pad;  // mantener un poquito más antes del silencio
                double adjustedSilenceEnd = sEnd - pad;       // retomar un poquito antes de que acabe el silencio

                if (adjustedSilenceStart > currentTime)
                {
                    keeps.Add((currentTime, adjustedSilenceStart));
                }
                currentTime = adjustedSilenceEnd;
            }

            if (currentTime < totalDur)
                keeps.Add((currentTime, totalDur));

            // Actualizar grid en el GUI
            this.Invoke(new Action(() => {
                gridCortes.Rows.Clear();
                int idx = 1;
                foreach (var k in keeps)
                {
                    double d = k.End - k.Start;
                    if (d < 0.1) continue;
                    gridCortes.Rows.Add(idx++, "Voz", k.Start.ToString("0.00"), k.End.ToString("0.00"), d.ToString("0.00"));
                }
            }));

            // Generar EDL — FORMATO IDÉNTICO al auto_recortes.py que YA FUNCIONÓ en DaVinci
            int fps = 30;
            double timelineOffset = 3600.0; // DaVinci empieza en 01:00:00:00
            double timelineCurrent = 0.0;

            using (var sw = new StreamWriter(edlPath, false, System.Text.Encoding.UTF8))
            {
                sw.WriteLine("TITLE: Cortes de Voz - Resolve");
                sw.WriteLine("FCM: NON-DROP FRAME");
                sw.WriteLine();

                int evNum = 0;
                foreach (var (kStart, kEnd) in keeps)
                {
                    double dur = kEnd - kStart;
                    if (dur < 0.1) continue;

                    evNum++;
                    string srcStart = FrameToTimecode(kStart, fps);
                    string srcEnd = FrameToTimecode(kEnd, fps);
                    string tlStart = FrameToTimecode(timelineOffset + timelineCurrent, fps);
                    string tlEnd = FrameToTimecode(timelineOffset + timelineCurrent + dur, fps);

                    // Video
                    sw.WriteLine($"{evNum:D3}  AX       V     C        {srcStart} {srcEnd} {tlStart} {tlEnd}");
                    // Audio
                    sw.WriteLine($"{evNum:D3}  AX       A     C        {srcStart} {srcEnd} {tlStart} {tlEnd}");
                    sw.WriteLine();

                    timelineCurrent += dur;
                }
            }

            return keeps.Count;
        }
    }
}
