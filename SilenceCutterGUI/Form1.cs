using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Globalization;
using System.Text;

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
        private TextBox txtTranscriptionPath;
        private TextBox txtTranscriptionJson;
        private Button btnTranscribe;
        private Button btnDownloadJson;
        private Button btnAiSpotlights;
        private bool hasGpu = false;
        private string currentTranscriptionJson = "";
        
        
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
            this.Size = new Size(800, 850);
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

            btnProcess = new Button() { Text = "⚡ Generar EDL (Silencios)", Top = 130, Left = 20, Width = 240, Height = 40, Font = new Font("Segoe UI", 9F, FontStyle.Bold), BackColor = Color.OrangeRed, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnProcess.Click += async (s, e) => await ProcessVideo();

            btnTranscribe = new Button() { Text = "🎙️ Transcribir (GPU)", Top = 130, Left = 270, Width = 240, Height = 40, Font = new Font("Segoe UI", 9F, FontStyle.Bold), BackColor = Color.DarkCyan, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Enabled = false };
            btnTranscribe.Click += async (s, e) => await TranscribeVideo();

            btnAiSpotlights = new Button() { Text = "✨ Generar EDL Spotlights (AI)", Top = 130, Left = 520, Width = 240, Height = 40, Font = new Font("Segoe UI", 9F, FontStyle.Bold), BackColor = Color.MediumPurple, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Enabled = false };
            btnAiSpotlights.Click += async (s, e) => await DetectSpotlights();

            gridCortes = new DataGridView() { Top = 180, Left = 20, Width = 740, Height = 290 };
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

            // Sección de Transcripción (fila propia)
            Label lTransFile = new Label() { Text = "Transcripción JSON:", Top = 545, Left = 20, Width = 150, Font = mainFont, ForeColor = Color.LightBlue };
            txtTranscriptionPath = new TextBox() { Top = 545, Left = 170, Width = 540, Font = new Font("Segoe UI", 9F), BackColor = Color.FromArgb(20, 20, 20), ForeColor = Color.Cyan, ReadOnly = true };
            
            Button btnBrowseJson = new Button() { Text = "📂", Top = 543, Left = 720, Width = 40, Height = 25, BackColor = Color.DarkSlateBlue, FlatStyle = FlatStyle.Flat };
            btnBrowseJson.Click += (s, e) => BrowseTranscriptionFile();

            Label lTrans = new Label() { Text = "Contenido:", Top = 585, Left = 20, Width = 100, Font = mainFont, ForeColor = Color.Cyan };
            txtTranscriptionJson = new TextBox() { Top = 610, Left = 20, Width = 740, Height = 140, Multiline = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 9F), BackColor = Color.Black, ForeColor = Color.FromArgb(0, 255, 0), ReadOnly = false };

            btnDownloadJson = new Button() { Text = "💾 Guardar Cambios Texto", Top = 582, Left = 570, Width = 190, Height = 28, Font = new Font("Segoe UI", 9F, FontStyle.Bold), BackColor = Color.MediumSeaGreen, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnDownloadJson.Click += (s, e) => SaveJsonFile();

            lblStatus = new Label() { Text = "Estado: Verificando GPU...", Top = 760, Left = 20, Width = 740, Font = mainFont, ForeColor = Color.LightGray };
            
            txtVideoPath.TextChanged += (s, e) => CheckExistingTranscription();

            this.Controls.Add(lblTitle);
            this.Controls.Add(l1); this.Controls.Add(txtVideoPath); this.Controls.Add(btnBrowse);
            this.Controls.Add(l2); this.Controls.Add(txtThreshold);
            this.Controls.Add(l3); this.Controls.Add(txtDuration);
            this.Controls.Add(l4); this.Controls.Add(txtPadding);
            this.Controls.Add(btnProcess);
            this.Controls.Add(btnTranscribe);
            this.Controls.Add(btnAiSpotlights);
            this.Controls.Add(gridCortes);
            this.Controls.Add(progressBar);
            this.Controls.Add(lRes); this.Controls.Add(txtResultPath);
            this.Controls.Add(lTransFile); this.Controls.Add(txtTranscriptionPath); this.Controls.Add(btnBrowseJson);
            this.Controls.Add(lTrans); this.Controls.Add(txtTranscriptionJson);
            this.Controls.Add(btnDownloadJson);
            this.Controls.Add(lblStatus);

            CheckGpu();
        }

        private void CheckGpu()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("nvidia-smi", "--query-gpu=name --format=csv,noheader")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd().Trim();
                    if (!string.IsNullOrEmpty(output))
                    {
                        hasGpu = true;
                        btnTranscribe.Enabled = true;
                        btnTranscribe.Text = "🚀 Transcribir (GPU)";
                        lblStatus.Text = "Estado: GPU Detectada: " + output;
                    }
                }
            }
            catch
            {
                btnTranscribe.Enabled = false;
                btnTranscribe.Text = "❌ No GPU";
                lblStatus.Text = "Estado: No se detectó GPU NVIDIA (Whisper local desactivado).";
            }
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

        private void CheckExistingTranscription()
        {
            try
            {
                string video = txtVideoPath.Text;
                if (string.IsNullOrWhiteSpace(video) || !File.Exists(video)) return;

                string videoDir = Path.GetDirectoryName(video) ?? "";
                string videoName = Path.GetFileNameWithoutExtension(video);
                string jsonPath = Path.Combine(videoDir, videoName + "_transcript.json");

                if (File.Exists(jsonPath))
                {
                    txtTranscriptionPath.Text = jsonPath;
                    txtTranscriptionJson.Text = File.ReadAllText(jsonPath);
                    btnAiSpotlights.Enabled = true;
                    lblStatus.Text = "Estado: Transcripción previa detectada.";
                }
                else
                {
                    txtTranscriptionPath.Text = "";
                    txtTranscriptionJson.Text = "";
                    btnAiSpotlights.Enabled = false;
                }
            }
            catch { }
        }

        private void BrowseTranscriptionFile()
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Transcription Files|*.json|All Files|*.*";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    txtTranscriptionPath.Text = ofd.FileName;
                    txtTranscriptionJson.Text = File.ReadAllText(ofd.FileName);
                    btnAiSpotlights.Enabled = true;
                    lblStatus.Text = "Estado: Transcripción manual seleccionada.";
                }
            }
        }

        private double GetStartTimecode(string videoPath)
        {
            try
            {
                string cmd = $"-v error -show_entries stream_tags=timecode -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"";
                var psi = new ProcessStartInfo
                {
                    FileName = ffprobePath,
                    Arguments = cmd,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                {
                    string output = process.StandardOutput.ReadToEnd().Trim();
                    if (string.IsNullOrEmpty(output)) return 0;
                    
                    // Solo tomar la primera línea si hay varias
                    string firstLine = output.Split('\n')[0].Trim();
                    string[] parts = firstLine.Split(':');
                    if (parts.Length == 4)
                    {
                        double h = double.Parse(parts[0]);
                        double m = double.Parse(parts[1]);
                        double s = double.Parse(parts[2]);
                        double f = double.Parse(parts[3]);
                        return h * 3600 + m * 60 + s + (f / 30.0); // Asumiendo 30fps para el TC inicial
                    }
                }
            }
            catch { }
            return 0;
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
            double timelineOffset = 3600.0;
            double sourceOffset = GetStartTimecode(txtVideoPath.Text);
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
                    string srcStart = FrameToTimecode(sourceOffset + kStart, fps);
                    string srcEnd = FrameToTimecode(sourceOffset + kEnd, fps);
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

        private async Task TranscribeVideo()
        {
            if (string.IsNullOrWhiteSpace(txtVideoPath.Text) || !File.Exists(txtVideoPath.Text))
            {
                MessageBox.Show("Ruta de archivo de video no válida.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            btnTranscribe.Enabled = false;
            txtTranscriptionJson.Text = "--- LOGS DE PROCESO ---\r\n";
            lblStatus.Text = "Estado: Iniciando motor Whisper...";
            progressBar.Style = ProgressBarStyle.Marquee;

            try
            {
                string video = txtVideoPath.Text;
                string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "transcribir_whisper.py");
                
                if (!File.Exists(scriptPath)) 
                    scriptPath = Path.Combine(Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.Parent.Parent.FullName, "transcribir_whisper.py");

                if (!File.Exists(scriptPath))
                    scriptPath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "transcribir_whisper.py");

                string pythonPath = "python";
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string[] possiblePaths = new string[] {
                    Path.Combine(userProfile, "Miniconda3", "python.exe"),
                    Path.Combine(userProfile, "miniconda3", "python.exe"),
                    "C:\\Users\\marco\\Miniconda3\\python.exe",
                    "python"
                };

                foreach (var path in possiblePaths) {
                    if (path == "python" || File.Exists(path)) {
                        pythonPath = path;
                        if (path != "python") break;
                    }
                }

                StringBuilder fullOutput = new StringBuilder();
                
                await Task.Run(() => {
                    ProcessStartInfo psi = new ProcessStartInfo(pythonPath, $"\"{scriptPath}\" \"{video}\"")
                    {
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = System.Text.Encoding.UTF8
                    };

                    using (Process p = new Process())
                    {
                        p.StartInfo = psi;
                        p.OutputDataReceived += (s, ev) => {
                            if (!string.IsNullOrEmpty(ev.Data)) {
                                fullOutput.AppendLine(ev.Data);
                                this.Invoke(new Action(() => {
                                    txtTranscriptionJson.AppendText(ev.Data + "\r\n");
                                    if (ev.Data.Contains("s]")) lblStatus.Text = "Progreso: " + ev.Data;
                                }));
                            }
                        };
                        p.ErrorDataReceived += (s, ev) => {
                            if (!string.IsNullOrEmpty(ev.Data)) {
                                fullOutput.AppendLine(ev.Data);
                                this.Invoke(new Action(() => txtTranscriptionJson.AppendText("ERR: " + ev.Data + "\r\n")));
                            }
                        };

                        p.Start();
                        p.BeginOutputReadLine();
                        p.BeginErrorReadLine();
                        p.WaitForExit();
                    }
                });

                string finalStr = fullOutput.ToString();
                if (finalStr.Contains("---JSON_START---"))
                {
                    string content = finalStr.Split(new[] { "---JSON_START---" }, StringSplitOptions.None)[1]
                                          .Split(new[] { "---JSON_END---" }, StringSplitOptions.None)[0].Trim();
                    
                    txtTranscriptionJson.Text = content;
                    SaveTranscriptionToDisk(content);
                    btnAiSpotlights.Enabled = true;
                    lblStatus.Text = "¡Transcripción completada y guardada en disco!";
                }
                else
                {
                    lblStatus.Text = "Error en la transcripción. Revisa los logs.";
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Error: " + ex.Message;
                txtTranscriptionJson.AppendText("\nEXCEPTION: " + ex.Message);
            }
            finally
            {
                progressBar.Style = ProgressBarStyle.Continuous;
                btnTranscribe.Enabled = true;
            }
        }

        private void SaveTranscriptionToDisk(string content)
        {
            try
            {
                string video = txtVideoPath.Text;
                string videoDir = Path.GetDirectoryName(video) ?? "";
                string videoName = Path.GetFileNameWithoutExtension(video);
                string jsonPath = Path.Combine(videoDir, videoName + "_transcript.json");

                File.WriteAllText(jsonPath, content);
                txtTranscriptionPath.Text = jsonPath;
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Error al guardar transcripción: " + ex.Message;
            }
        }

        private void SaveJsonFile()
        {
            try
            {
                string video = txtVideoPath.Text;
                string videoDir = Path.GetDirectoryName(video) ?? "";
                string videoName = Path.GetFileNameWithoutExtension(video);
                string jsonPath = Path.Combine(videoDir, videoName + "_transcript.json");

                File.WriteAllText(jsonPath, txtTranscriptionJson.Text);
                txtTranscriptionPath.Text = jsonPath;
                MessageBox.Show($"Archivo guardado en:\n{jsonPath}", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                btnAiSpotlights.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al guardar: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task DetectSpotlights()
        {
            string transcriptContent = txtTranscriptionJson.Text;
            if (string.IsNullOrWhiteSpace(transcriptContent)) return;

            btnAiSpotlights.Enabled = false;
            lblStatus.Text = "Estado: AI identificando Spotlights en LM Studio (localhost:1234)...";
            progressBar.Style = ProgressBarStyle.Marquee;

            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(20); // Ampliado para modelos grandes (35B+)
                    
                    var prompt = "ACTÚA COMO UN EDITOR EXPERTO EN VIDEOS CORTOS (REELS/SHORTS). " +
                                 "Tu objetivo es encontrar los 5 momentos más impactantes de este podcast. " +
                                 "REGLAS DE ORO:\n" +
                                 "1. DURACIÓN: Cada fragmento debe ser de entre 4 y 7 segundos. JAMÁS cortes a mitad de una palabra.\n" +
                                 "2. FINAL NATURAL: El fragmento debe terminar al final de una frase o en una pausa natural (punto, coma, silencio). Debe sentirse como un 'hook' que termina arriba.\n" +
                                 "3. CONTENIDO: Busca ganchos de intriga total (clickbait). No busques lógica ni explicaciones. " +
                                 "Busca el momento donde el espectador diga '¿Qué?' o '¡No puede ser!'.\n" +
                                 "4. FORMATO: Responde solo con un JSON array de objetos: [{\"start\": 0.0, \"end\": 0.0, \"title\": \"TITULO\"}].\n" +
                                 "Contenido:\n" + transcriptContent;

                    var requestBody = new
                    {
                        model = "local-model",
                        messages = new[] {
                            new { role = "system", content = "Eres un director de marketing experto en viralización de cortos para redes sociales. Tu especialidad es encontrar fragmentos que detengan el scroll del usuario mediante el impacto y la intriga absoluta." },
                            new { role = "user", content = prompt }
                        },
                        temperature = 0.4
                    };

                    string jsonRequest = System.Text.Json.JsonSerializer.Serialize(requestBody);
                    var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync("http://localhost:1234/v1/chat/completions", content);
                    if (!response.IsSuccessStatusCode) throw new Exception("LM Studio no respondió correctamente. Asegúrate de que esté abierto en el puerto 1234.");

                    var responseStr = await response.Content.ReadAsStringAsync();
                    var doc = System.Text.Json.JsonDocument.Parse(responseStr);
                    string aiResponse = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";

                    // Limpiar el JSON si el modelo añadió texto extra
                    int startIdx = aiResponse.IndexOf("[");
                    int endIdx = aiResponse.LastIndexOf("]");
                    if (startIdx == -1 || endIdx == -1) throw new Exception("La AI no devolvió un formato JSON válido.");
                    string cleanJson = aiResponse.Substring(startIdx, endIdx - startIdx + 1);

                    var highlights = System.Text.Json.JsonSerializer.Deserialize<List<HighlightSegment>>(cleanJson);
                    
                    if (highlights != null && highlights.Count > 0)
                    {
                        string video = txtVideoPath.Text;
                        string videoDir = Path.GetDirectoryName(video) ?? "";
                        string videoName = Path.GetFileNameWithoutExtension(video);
                        string cutsEdlPath = Path.Combine(videoDir, videoName + "_SPOTLIGHTS_CUTS.edl");
                        string markersEdlPath = Path.Combine(videoDir, videoName + "_SPOTLIGHTS_MARKERS.edl");

                        GenerateSpotlightEDL(highlights, cutsEdlPath);
                        GenerateSpotlightMarkerEDL(highlights, markersEdlPath);
                        
                        txtResultPath.Text = markersEdlPath;
                        lblStatus.Text = $"¡Listo! Se han generado dos EDLs (Cortes y Marcadores).";
                        MessageBox.Show($"Se han identificado {highlights.Count} momentos.\n\nArchivos generados:\n1. {Path.GetFileName(cutsEdlPath)} (Cortes)\n2. {Path.GetFileName(markersEdlPath)} (Marcadores)\n\nEn Resolve usa 'Import -> Markers from EDL' para los marcadores.", "Spotlights AI", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al contactar con LM Studio: " + ex.Message, "Error AI", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Error en Spotlights AI.";
            }
            finally
            {
                progressBar.Style = ProgressBarStyle.Continuous;
                btnAiSpotlights.Enabled = true;
            }
        }

        private void GenerateSpotlightEDL(List<HighlightSegment> highlights, string edlPath)
        {
            int fps = 30;
            double timelineOffset = 3600.0;
            double sourceOffset = GetStartTimecode(txtVideoPath.Text);
            double timelineCurrent = 0.0;

            using (var sw = new StreamWriter(edlPath, false, Encoding.UTF8))
            {
                sw.WriteLine("TITLE: Spotlights de Podcast - AI");
                sw.WriteLine("FCM: NON-DROP FRAME");
                sw.WriteLine();

                int evNum = 0;
                string videoFileName = Path.GetFileName(txtVideoPath.Text);
                
                foreach (var h in highlights)
                {
                    double dur = h.end - h.start;
                    if (dur < 0.1) continue;

                    evNum++;
                    string srcStart = FrameToTimecode(sourceOffset + h.start, fps);
                    string srcEnd = FrameToTimecode(sourceOffset + h.end, fps);
                    string tlStart = FrameToTimecode(timelineOffset + timelineCurrent, fps);
                    string tlEnd = FrameToTimecode(timelineOffset + timelineCurrent + dur, fps);

                    // Línea de video - Siguiendo el template timline_1.edl
                    sw.WriteLine($"{evNum:D3}  AX       V     C        {srcStart} {srcEnd} {tlStart} {tlEnd}  ");
                    sw.WriteLine($"* FROM CLIP NAME: {videoFileName}");
                    
                    // Línea de audio
                    evNum++;
                    sw.WriteLine($"{evNum:D3}  AX       A     C        {srcStart} {srcEnd} {tlStart} {tlEnd}  ");
                    sw.WriteLine($"* FROM CLIP NAME: {videoFileName}");
                    sw.WriteLine();

                    timelineCurrent += dur;
                }
            }
        }

        private void GenerateSpotlightMarkerEDL(List<HighlightSegment> highlights, string edlPath)
        {
            int fps = 30;
            double sourceOffset = GetStartTimecode(txtVideoPath.Text);
            string videoFileName = Path.GetFileName(txtVideoPath.Text);

            using (var sw = new StreamWriter(edlPath, false, Encoding.UTF8))
            {
                sw.WriteLine("TITLE: Marcadores de Spotlight - Resolve");
                sw.WriteLine("FCM: NON-DROP FRAME");
                sw.WriteLine();

                int evNum = 0;
                // Colores específicos detectados en el template del usuario
                string[] colors = { "ResolveColorLemon", "ResolveColorPurple", "ResolveColorYellow", "ResolveColorPink", "ResolveColorGreen", "ResolveColorCyan" };
                
                foreach (var h in highlights)
                {
                    string color = colors[evNum % colors.Length];
                    evNum++;

                    double startT = sourceOffset + h.start;
                    // Los marcadores de punto en Resolve usan entrada/salida igual o con 1 frame de diferencia
                    string tc = FrameToTimecode(startT, fps);
                    string tcPlus = FrameToTimecode(startT + (1.0/fps), fps);
                    
                    int durationFrames = (int)((h.end - h.start) * fps);
                    
                    sw.WriteLine($"{evNum:D3}  001      V     C        {tc} {tcPlus} {tc} {tcPlus}  ");
                    sw.WriteLine($"* DE CLIP {videoFileName}");
                    // Estructura exacta solicitada: ** SPOTLIGHT : [Tit] |C:[Col] |M:[Mark] |D:[Dur]
                    sw.WriteLine($"** SPOTLIGHT : Identificado por AI local |C:{color} |M:{h.title.ToUpper()} |D:{durationFrames}");
                    sw.WriteLine();
                }
            }
        }

        public class HighlightSegment { public double start { get; set; } public double end { get; set; } public string title { get; set; } = ""; }
    }
}
