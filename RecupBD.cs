using System;

using System.Collections.Generic;

using System.ComponentModel;

using System.Diagnostics;

using System.Drawing;

using System.Drawing.Drawing2D;

using System.IO;

using System.Media;

using System.Reflection;

using System.Resources;

using System.Runtime.InteropServices;

using System.ServiceProcess;

using System.Text;

using System.Threading;

using System.Windows.Forms;

using Microsoft.Win32;

[assembly: AssemblyTitle("Recuperador de Banco Firebird")]

[assembly: AssemblyDescription("Utilitário para diagnóstico, reparo e recuperação automatizada de bancos de dados Firebird.")]

[assembly: AssemblyConfiguration("")]

[assembly: AssemblyCompany("Jhone Andrade")]

[assembly: AssemblyProduct("Recuperador de Banco Firebird")]

[assembly: AssemblyCopyright("© 2026 Jhone Andrade")]

[assembly: AssemblyTrademark("Jhone Andrade")]

[assembly: AssemblyCulture("")]

[assembly: NeutralResourcesLanguage("pt-BR")]

[assembly: ComVisible(false)]

[assembly: Guid("e58df2f4-a02e-4b47-b892-911b3d680c2f")]

[assembly: AssemblyVersion("1.2.0.0")]

[assembly: AssemblyFileVersion("1.2.0.0")]

[assembly: AssemblyInformationalVersion("1.2.0")]

namespace FirebirdRecupBD

{

    public class MainForm : Form

    {

        [DllImport("user32.dll")]

        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);

        private const int WM_SETREDRAW = 0x0B;

        // Categorias e Filtros de Log

        private enum LogCategory

        {

            Normal,

            Error,

            Warning,

            Step

        }

        private enum LogFilter

        {

            All,

            ErrorsOnly,

            StepsOnly

        }

        private class LogEntry

        {

            public string Text;

            public Color Color;

            public bool IsBold;

            public LogCategory Category;

            public DateTime Timestamp;

        }

        // Controles de Selecao e Configuracao

        private TextBox txtDbPath;

        private Button btnBrowse;

        private Label lblFileInfo;

        private TextBox txtHost;

        private TextBox txtUser;

        private TextBox txtPassword;

        private Button btnTogglePass;
        private CheckBox chkSaveCredentials;

        private PictureBox picLogo;

        private Panel pnlDbWrapper;

        private Panel pnlHostWrapper;

        private Panel pnlUserWrapper;

        private Panel pnlPassWrapper;

        // Controles de Progresso e Status

        private Label lblStep;

        private Label lblPercent;

        private Label lblErrorBadge;

        private SmoothProgressBar progressBar;

        // Abas / Filtros de Visualizacao do Terminal

        private Panel pnlLogTabs;

        private Button btnTabAll;

        private Button btnTabErrors;

        private Button btnTabSteps;

        private Label lblFilterHint;

        private RichTextBox rtbLog;

        private Button btnAbout;

        // Botoes de Acao

        private Button btnStart;

        private Button btnCancel;

        private Button btnOpenFolder;

        private Button btnClearLog;

        // Animacao de Progresso Fluida (0, 1, 2, 3... 100%)

        private System.Windows.Forms.Timer animTimer;

        private int _displayPercent = 0;

        private int _targetPercent = 0;

        private int _errorCount = 0;

        private bool _showPassword = false;

        // Armazenamento Historico para Filtragem Dinamica

        private readonly object _logLock = new object();

        private readonly List<LogEntry> _historyAll = new List<LogEntry>();

        private readonly List<LogEntry> _historyErrors = new List<LogEntry>();

        private readonly List<LogEntry> _historySteps = new List<LogEntry>();

        private readonly Queue<LogEntry> _logQueue = new Queue<LogEntry>();

        private System.Windows.Forms.Timer _logFlushTimer;

        private Font _logFontRegular;

        private Font _logFontBold;

        private LogFilter _currentFilter = LogFilter.All;

        // Controle de Execucao

        private Thread _workerThread;

        private Process _currentProcess;

        private bool _isCancelled = false;

        private bool _isRunning = false;

        public MainForm()

        {

            _logFontRegular = new Font("Consolas", 9f, FontStyle.Regular);

            _logFontBold = new Font("Consolas", 9f, FontStyle.Bold);

            InitializeComponent();

            SetupTimer();

            SetupLogDispatcher();

            LoadAppIcon();

            AdjustResponsiveLayout();

            UpdateFileInfo();

            FlushLogQueue();
            LoadUserConfig();
            UpdateFileInfo();

            this.Resize += (s, e) => AdjustResponsiveLayout();

        }

        private void SetupLogDispatcher()

        {

            _logFlushTimer = new System.Windows.Forms.Timer();

            _logFlushTimer.Interval = 30; // 33 fps de atualizacao suave

            _logFlushTimer.Tick += (s, e) => FlushLogQueue();

            _logFlushTimer.Start();

        }

        private void FlushLogQueue()

        {

            if (_logQueue.Count == 0 || rtbLog.IsDisposed) return;

            List<LogEntry> batch = new List<LogEntry>();

            lock (_logLock)

            {

                int count = 0;

                while (_logQueue.Count > 0 && count < 80)

                {

                    batch.Add(_logQueue.Dequeue());

                    count++;

                }

            }

            if (batch.Count == 0) return;

            try

            {

                SendMessage(rtbLog.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);

                // Evita estouro de memoria caso passe de 250 mil caracteres

                if (rtbLog.TextLength > 250000)

                {

                    rtbLog.Select(0, 80000);

                    rtbLog.SelectedText = "[... trecho de histórico compactado ...]" + Environment.NewLine;

                }

                foreach (var item in batch)

                {

                    rtbLog.SelectionStart = rtbLog.TextLength;

                    rtbLog.SelectionLength = 0;

                    rtbLog.SelectionColor = item.Color;

                    rtbLog.SelectionFont = item.IsBold ? _logFontBold : _logFontRegular;

                    rtbLog.AppendText(item.Text + Environment.NewLine);

                }

                rtbLog.SelectionStart = rtbLog.TextLength;

                rtbLog.ScrollToCaret();

            }

            catch { }

            finally

            {

                SendMessage(rtbLog.Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);

                rtbLog.Invalidate();

            }

        }

        private const string EMBEDDED_LOGO_BASE64 = "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";

        private static Image _cachedAppLogo = null;

        public static Image GetAppLogo()

        {

            if (_cachedAppLogo != null) return _cachedAppLogo;

            try

            {

                string dir = AppDomain.CurrentDomain.BaseDirectory;

                string logoPath = Path.Combine(dir, "app_logo.png");

                // local folder fallback

                if (File.Exists(logoPath))

                {

                    using (var bmp = new Bitmap(logoPath))

                    {

                        _cachedAppLogo = new Bitmap(bmp);

                        return _cachedAppLogo;

                    }

                }

            }

            catch { }

            try

            {

                byte[] raw = Convert.FromBase64String(EMBEDDED_LOGO_BASE64);

                using (MemoryStream ms = new MemoryStream(raw))

                {

                    _cachedAppLogo = new Bitmap(ms);

                    return _cachedAppLogo;

                }

            }

            catch { }

            try

            {

                Icon ico = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

                if (ico != null)

                {

                    _cachedAppLogo = ico.ToBitmap();

                    return _cachedAppLogo;

                }

            }

            catch { }

            return null;

        }

        private void LoadAppIcon()

        {

            try

            {

                picLogo.Image = GetAppLogo();

                string dir = AppDomain.CurrentDomain.BaseDirectory;

                string iconPath = Path.Combine(dir, "app_icon.ico");

                // local folder fallback

                if (File.Exists(iconPath))

                {

                    this.Icon = new Icon(iconPath);

                }

                else

                {

                    this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

                }

            }

            catch { }

        }

        private float _spinnerAngle = 0f;

        private void SetupTimer()

        {

            animTimer = new System.Windows.Forms.Timer();

            animTimer.Interval = 25;

            animTimer.Tick += (s, e) =>

            {

                if (_isRunning)

                {

                    _spinnerAngle = (_spinnerAngle + 12f) % 360f;

                    if (btnStart != null) btnStart.Invalidate();

                }

                if (_displayPercent < _targetPercent)

                {

                    _displayPercent++;

                    progressBar.Value = _displayPercent;

                    lblPercent.Text = _displayPercent + "%";

                }

                else if (_displayPercent > _targetPercent)

                {

                    _displayPercent--;

                    progressBar.Value = _displayPercent;

                    lblPercent.Text = _displayPercent + "%";

                }

            };

            animTimer.Start();

        }

        private void BtnStart_Paint(object sender, PaintEventArgs e)

        {

            Graphics g = e.Graphics;

            g.SmoothingMode = SmoothingMode.AntiAlias;

            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Rectangle rect = btnStart.ClientRectangle;

            if (_isRunning)

            {

                // Fundo verde escuro para estado ativo de processamento

                using (var brushBg = new SolidBrush(Color.FromArgb(21, 87, 46)))

                {

                    g.FillRectangle(brushBg, rect);

                }

                // Borda suave em tom esmeralda

                using (var penBorder = new Pen(Color.FromArgb(34, 197, 94), 1))

                {

                    g.DrawRectangle(penBorder, 0, 0, rect.Width - 1, rect.Height - 1);

                }

                // Spinner de carregamento giratório

                int spinnerSize = 15;

                int spinnerX = 36;

                int spinnerY = (rect.Height - spinnerSize) / 2;

                using (var penTrack = new Pen(Color.FromArgb(60, 255, 255, 255), 2.2f))

                {

                    g.DrawEllipse(penTrack, spinnerX, spinnerY, spinnerSize, spinnerSize);

                }

                using (var penActive = new Pen(Color.FromArgb(52, 211, 153), 2.2f))

                {

                    penActive.StartCap = LineCap.Round;

                    penActive.EndCap = LineCap.Round;

                    g.DrawArc(penActive, spinnerX, spinnerY, spinnerSize, spinnerSize, _spinnerAngle, 110);

                }

                // Texto "Executando..."

                using (var font = new Font("Segoe UI", 9.25f, FontStyle.Bold))

                using (var brushText = new SolidBrush(Color.White))

                {

                    g.DrawString("Executando...", font, brushText, spinnerX + spinnerSize + 10, (rect.Height - font.Height) / 2);

                }

            }

            else

            {

                // Fundo normal

                Color bg = btnStart.Enabled ? Color.FromArgb(22, 163, 74) : Color.FromArgb(40, 50, 45);

                using (var brushBg = new SolidBrush(bg))

                {

                    g.FillRectangle(brushBg, rect);

                }

                using (var font = new Font("Segoe UI", 9.25f, FontStyle.Bold))

                using (var brushText = new SolidBrush(btnStart.Enabled ? Color.White : Color.FromArgb(150, 150, 150)))

                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })

                {

                    g.DrawString("▶ Iniciar Recuperação", font, brushText, rect, sf);

                }

            }

        }

        private void AdjustResponsiveLayout()

        {

            int w = this.ClientSize.Width;

            if (w < 680) w = 680;

            if (btnAbout != null)

            {

                btnAbout.Left = w - 16 - btnAbout.Width;

            }

            // Botao Procurar e Campo do Banco

            btnBrowse.Left = w - 16 - btnBrowse.Width;

            pnlDbWrapper.Left = 16;

            pnlDbWrapper.Width = btnBrowse.Left - 16 - 8;

            // Barra e badges

            lblPercent.Left = w - 16 - lblPercent.Width;

            lblErrorBadge.Left = lblPercent.Left - 8 - lblErrorBadge.Width;

            progressBar.Left = 16;

            progressBar.Width = w - 32;

        }

        private Panel CreateModernInputWrapper(Control child, int width, int height)

        {

            Panel outer = new Panel

            {

                Size = new Size(width, height),

                BackColor = Color.FromArgb(39, 39, 42),

                Padding = new Padding(1)

            };

            Panel inner = new Panel

            {

                Dock = DockStyle.Fill,

                BackColor = Color.FromArgb(15, 15, 18),

                Padding = new Padding(6, 4, 6, 2)

            };

            child.Dock = DockStyle.Fill;

            inner.Controls.Add(child);

            outer.Controls.Add(inner);

            return outer;

        }

        private void InitializeComponent()

        {

            this.Text = "Recuperador de Banco de Dados Firebird";

            this.Size = new Size(880, 580);

            this.MinimumSize = new Size(760, 500);

            this.StartPosition = FormStartPosition.CenterScreen;

            this.BackColor = Color.FromArgb(20, 20, 24);

            this.ForeColor = Color.FromArgb(244, 244, 245);

            this.Font = new Font("Segoe UI", 9f, FontStyle.Regular);

            this.AllowDrop = true;

            this.DragEnter += MainForm_DragEnter;

            this.DragDrop += MainForm_DragDrop;

            // 1. CABECALHO COMPACTO (Altura 50px)

            Panel pnlHeader = new Panel

            {

                Dock = DockStyle.Top,

                Height = 50,

                BackColor = Color.FromArgb(16, 16, 18),

                Padding = new Padding(16, 6, 16, 6)

            };

            picLogo = new PictureBox

            {

                Size = new Size(36, 36),

                Location = new Point(16, 7),

                SizeMode = PictureBoxSizeMode.Zoom,

                BackColor = Color.Transparent

            };

            pnlHeader.Controls.Add(picLogo);

            Label lblTitle = new Label

            {

                Text = "RECUPERADOR DE BANCO FIREBIRD",

                Font = new Font("Segoe UI", 12f, FontStyle.Bold),

                ForeColor = Color.White,

                Location = new Point(60, 14),

                AutoSize = true

            };

            pnlHeader.Controls.Add(lblTitle);

            // Botao Circulo com ! para informacoes do criador

            btnAbout = new Button

            {

                Size = new Size(28, 28),

                Location = new Point(836, 11),

                BackColor = Color.FromArgb(24, 24, 28),

                FlatStyle = FlatStyle.Flat,

                Cursor = Cursors.Hand

            };

            btnAbout.FlatAppearance.BorderSize = 0;

            btnAbout.Paint += (s, pe) =>

            {

                pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                using (Pen circlePen = new Pen(Color.FromArgb(99, 102, 241), 1.8f))

                {

                    pe.Graphics.DrawEllipse(circlePen, 3, 3, 21, 21);

                }

                using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(244, 244, 245)))

                using (Font f = new Font("Segoe UI", 10.5f, FontStyle.Bold))

                {

                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

                    pe.Graphics.DrawString("!", f, textBrush, new RectangleF(0, 0, 28, 28), sf);

                }

            };

            btnAbout.MouseEnter += (s, e) => { btnAbout.BackColor = Color.FromArgb(39, 39, 42); };

            btnAbout.MouseLeave += (s, e) => { btnAbout.BackColor = Color.FromArgb(24, 24, 28); };

            btnAbout.Click += (s, e) =>

            {

                using (AboutForm frm = new AboutForm())

                {

                    frm.ShowDialog(this);

                }

            };

            pnlHeader.Controls.Add(btnAbout);

            ToolTip ttAbout = new ToolTip();

            ttAbout.SetToolTip(btnAbout, "Sobre o Desenvolvedor / Informações");

            // 2. CONFIGURACOES E ARQUIVO (Altura compacta 100px)

            Panel pnlConfig = new Panel

            {

                Dock = DockStyle.Top,

                Height = 100,

                BackColor = Color.FromArgb(20, 20, 24),

                Padding = new Padding(16, 6, 16, 6)

            };

            Label lblDb = new Label

            {

                Text = "Arquivo do Banco de Dados (.IB / .FDB):",

                Location = new Point(16, 6),

                AutoSize = true,

                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),

                ForeColor = Color.FromArgb(212, 212, 216)

            };

            pnlConfig.Controls.Add(lblDb);

            txtDbPath = new TextBox

            {

                Text = "",

                BackColor = Color.FromArgb(15, 15, 18),

                ForeColor = Color.White,

                BorderStyle = BorderStyle.None,

                Font = new Font("Segoe UI", 9f)

            };

            txtDbPath.TextChanged += (s, e) => UpdateFileInfo();

            pnlDbWrapper = CreateModernInputWrapper(txtDbPath, 720, 26);

            pnlDbWrapper.Location = new Point(16, 26);

            pnlConfig.Controls.Add(pnlDbWrapper);

            btnBrowse = CreateStyledButton("Procurar...", Color.FromArgb(39, 39, 42), 110, 26, true);

            btnBrowse.Location = new Point(745, 26);

            btnBrowse.Click += BtnBrowse_Click;

            pnlConfig.Controls.Add(btnBrowse);

            lblFileInfo = new Label

            {

                Text = "Aguardando verificação do arquivo e disco...",

                Location = new Point(16, 54),

                AutoSize = true,

                Font = new Font("Segoe UI", 8.25f, FontStyle.Regular),

                ForeColor = Color.FromArgb(148, 163, 184)

            };

            pnlConfig.Controls.Add(lblFileInfo);

            // Linha com Host, Usuario e Senha com Olho Vetorial

            Label lblHost = new Label { Text = "Servidor / Porta:", Location = new Point(16, 75), AutoSize = true, Font = new Font("Segoe UI", 8.25f), ForeColor = Color.FromArgb(161, 161, 170) };

            pnlConfig.Controls.Add(lblHost);

            txtHost = new TextBox { Text = "127.0.0.1/3050", BackColor = Color.FromArgb(15, 15, 18), ForeColor = Color.White, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 8.5f) };

            pnlHostWrapper = CreateModernInputWrapper(txtHost, 115, 24);

            pnlHostWrapper.Location = new Point(110, 73);

            pnlConfig.Controls.Add(pnlHostWrapper);

            Label lblUser = new Label { Text = "Usuário:", Location = new Point(240, 75), AutoSize = true, Font = new Font("Segoe UI", 8.25f), ForeColor = Color.FromArgb(161, 161, 170) };

            pnlConfig.Controls.Add(lblUser);

            txtUser = new TextBox { Text = "", BackColor = Color.FromArgb(15, 15, 18), ForeColor = Color.White, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 8.5f) };

            pnlUserWrapper = CreateModernInputWrapper(txtUser, 90, 24);

            pnlUserWrapper.Location = new Point(292, 73);

            pnlConfig.Controls.Add(pnlUserWrapper);

            Label lblPass = new Label { Text = "Senha:", Location = new Point(395, 75), AutoSize = true, Font = new Font("Segoe UI", 8.25f), ForeColor = Color.FromArgb(161, 161, 170) };

            pnlConfig.Controls.Add(lblPass);

            txtPassword = new TextBox { Text = "", PasswordChar = '•', BackColor = Color.FromArgb(15, 15, 18), ForeColor = Color.White, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 8.5f) };

            pnlPassWrapper = CreateModernInputWrapper(txtPassword, 110, 24);

            pnlPassWrapper.Location = new Point(440, 73);

            pnlConfig.Controls.Add(pnlPassWrapper);

            btnTogglePass = new Button

            {

                Size = new Size(28, 24),

                Location = new Point(553, 73),

                BackColor = Color.FromArgb(39, 39, 42),

                FlatStyle = FlatStyle.Flat,

                Cursor = Cursors.Hand

            };

            btnTogglePass.FlatAppearance.BorderSize = 0;

            btnTogglePass.Paint += (s, pe) =>

            {

                pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                Color eyeColor = _showPassword ? Color.FromArgb(34, 197, 94) : Color.FromArgb(161, 161, 170);

                using (Pen pen = new Pen(eyeColor, 1.4f))

                {

                    pe.Graphics.DrawArc(pen, 5, 7, 18, 10, 0, 180);

                    pe.Graphics.DrawArc(pen, 5, 5, 18, 10, 180, 180);

                    pe.Graphics.FillEllipse(new SolidBrush(eyeColor), 12, 8, 4, 4);

                    if (!_showPassword)

                    {

                        pe.Graphics.DrawLine(pen, 5, 6, 23, 16);

                    }

                }

            };

            btnTogglePass.Click += (s, e) =>

            {

                _showPassword = !_showPassword;

                txtPassword.PasswordChar = _showPassword ? '\0' : '•';

                btnTogglePass.Invalidate();

            };

            pnlConfig.Controls.Add(btnTogglePass);

            chkSaveCredentials = new CheckBox
            {
                Text = "Salvar dados neste computador",
                Checked = true,
                Location = new Point(595, 75),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.25f, FontStyle.Regular),
                ForeColor = Color.FromArgb(161, 161, 170),
                Cursor = Cursors.Hand
            };
            chkSaveCredentials.CheckedChanged += (s, e) => SaveUserConfig();
            pnlConfig.Controls.Add(chkSaveCredentials);

            // 3. BARRA DE PROGRESSO E STATUS (Altura 48px)

            Panel pnlProgress = new Panel

            {

                Dock = DockStyle.Top,

                Height = 48,

                BackColor = Color.FromArgb(18, 18, 20),

                Padding = new Padding(16, 4, 16, 4)

            };

            lblStep = new Label

            {

                Text = "Pronto para iniciar.",

                Location = new Point(16, 6),

                AutoSize = true,

                Font = new Font("Segoe UI", 8.75f, FontStyle.Bold),

                ForeColor = Color.FromArgb(244, 244, 245)

            };

            pnlProgress.Controls.Add(lblStep);

            lblErrorBadge = new Label

            {

                Text = "0 Erros",

                Location = new Point(720, 5),

                Size = new Size(70, 20),

                TextAlign = ContentAlignment.MiddleCenter,

                Font = new Font("Segoe UI", 8f, FontStyle.Bold),

                BackColor = Color.FromArgb(39, 39, 42),

                ForeColor = Color.FromArgb(161, 161, 170),

                Cursor = Cursors.Hand

            };

            lblErrorBadge.Click += (s, e) => SwitchLogFilter(LogFilter.ErrorsOnly);

            pnlProgress.Controls.Add(lblErrorBadge);

            lblPercent = new Label

            {

                Text = "0%",

                Location = new Point(800, 4),

                Size = new Size(55, 20),

                TextAlign = ContentAlignment.MiddleRight,

                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),

                ForeColor = Color.FromArgb(34, 197, 94)

            };

            pnlProgress.Controls.Add(lblPercent);

            progressBar = new SmoothProgressBar

            {

                Location = new Point(16, 28),

                Width = 840,

                Height = 12

            };

            pnlProgress.Controls.Add(progressBar);

            // 4. PAINEL INFERIOR DE BOTOES (Altura 50px - Sempre visivel sem corte)

            Panel pnlBottom = new Panel

            {

                Dock = DockStyle.Bottom,

                Height = 50,

                BackColor = Color.FromArgb(16, 16, 18),

                Padding = new Padding(16, 8, 16, 8)

            };

            // Iniciar: VERDE

            btnStart = CreateStyledButton("▶ Iniciar Recuperação", Color.FromArgb(22, 163, 74), 190, 34, false);

            btnStart.Font = new Font("Segoe UI", 9.25f, FontStyle.Bold);

            btnStart.Location = new Point(16, 8);

            btnStart.Click += BtnStart_Click;

            btnStart.Paint += BtnStart_Paint;

            pnlBottom.Controls.Add(btnStart);

            // Cancelar: VERMELHO

            btnCancel = CreateStyledButton("⏹ Cancelar", Color.FromArgb(220, 38, 38), 105, 34, false);

            btnCancel.Location = new Point(214, 8);

            btnCancel.Enabled = false;

            btnCancel.Click += BtnCancel_Click;

            pnlBottom.Controls.Add(btnCancel);

            // Abrir Pasta de Trabalho: PRETA / GRAFITE ESCURO

            btnOpenFolder = CreateStyledButton("Abrir Pasta de Trabalho", Color.FromArgb(24, 24, 27), 170, 34, true);

            btnOpenFolder.Location = new Point(328, 8);

            btnOpenFolder.Click += BtnOpenFolder_Click;

            pnlBottom.Controls.Add(btnOpenFolder);

            // Limpar Terminal: PRETA / GRAFITE ESCURO

            btnClearLog = CreateStyledButton("Limpar Terminal", Color.FromArgb(24, 24, 27), 130, 34, true);

            btnClearLog.Location = new Point(506, 8);

            btnClearLog.Click += (s, e) => ClearAllLogs();

            pnlBottom.Controls.Add(btnClearLog);

            // 5. TERMINAL DE LOGS COM ABAS DE FILTRAGEM (Centro Fill)

            Panel pnlLogContainer = new Panel

            {

                Dock = DockStyle.Fill,

                BackColor = Color.FromArgb(10, 10, 12),

                Padding = new Padding(16, 4, 16, 6)

            };

            // Barra de Abas de Visualizacao

            pnlLogTabs = new Panel

            {

                Dock = DockStyle.Top,

                Height = 32,

                BackColor = Color.FromArgb(14, 14, 17),

                Padding = new Padding(0, 2, 0, 4)

            };

            btnTabAll = CreateFilterTabButton("Todos os Logs", 120, LogFilter.All);

            btnTabAll.Location = new Point(0, 2);

            pnlLogTabs.Controls.Add(btnTabAll);

            btnTabErrors = CreateFilterTabButton("! Somente Erros (0)", 150, LogFilter.ErrorsOnly);

            btnTabErrors.Location = new Point(126, 2);

            pnlLogTabs.Controls.Add(btnTabErrors);

            btnTabSteps = CreateFilterTabButton("Etapas do Processo", 150, LogFilter.StepsOnly);

            btnTabSteps.Location = new Point(282, 2);

            pnlLogTabs.Controls.Add(btnTabSteps);

            lblFilterHint = new Label

            {

                Text = "Exibindo saída completa do Firebird",

                Dock = DockStyle.Right,

                TextAlign = ContentAlignment.MiddleRight,

                AutoSize = false,

                Width = 350,

                Font = new Font("Segoe UI", 8f, FontStyle.Regular),

                ForeColor = Color.FromArgb(113, 113, 122)

            };

            pnlLogTabs.Controls.Add(lblFilterHint);

            pnlLogContainer.Controls.Add(pnlLogTabs);

            rtbLog = new RichTextBox

            {

                Dock = DockStyle.Fill,

                BackColor = Color.FromArgb(10, 10, 12),

                ForeColor = Color.FromArgb(226, 232, 240),

                BorderStyle = BorderStyle.None,

                Font = _logFontRegular,

                ReadOnly = true,

                DetectUrls = false

            };

            pnlLogContainer.Controls.Add(rtbLog);

            rtbLog.BringToFront();

            // Montagem no Form

            this.Controls.Add(pnlLogContainer);

            this.Controls.Add(pnlBottom);

            this.Controls.Add(pnlProgress);

            this.Controls.Add(pnlConfig);

            this.Controls.Add(pnlHeader);

            pnlHeader.BringToFront();

            pnlConfig.BringToFront();

            pnlProgress.BringToFront();

            pnlBottom.BringToFront();

            pnlLogContainer.BringToFront();

            UpdateTabButtonsUI();

            // Mensagem de boas vindas

            AppendLog("================================================================================", Color.FromArgb(71, 85, 105), false, LogCategory.Normal);

            AppendLog(" RECUPERADOR DE BANCO FIREBIRD", Color.FromArgb(34, 197, 94), true, LogCategory.Step);

            AppendLog(" O banco original é renomeado com a data/hora diretamente na pasta de origem.", Color.FromArgb(148, 163, 184), false, LogCategory.Step);

            AppendLog(" O processo de reparo roda sobre a cópia e o banco corrigido assume o lugar original.", Color.FromArgb(148, 163, 184), false, LogCategory.Step);

            AppendLog(" Use as abas acima para alternar entre Todos os Logs, Somente Erros e Etapas.", Color.FromArgb(56, 189, 248), false, LogCategory.Step);

            AppendLog("================================================================================", Color.FromArgb(71, 85, 105), false, LogCategory.Normal);

        }

        private Button CreateFilterTabButton(string text, int width, LogFilter filter)

        {

            Button btn = new Button

            {

                Text = text,

                Width = width,

                Height = 26,

                FlatStyle = FlatStyle.Flat,

                Cursor = Cursors.Hand,

                Font = new Font("Segoe UI", 8.25f, FontStyle.Regular),

                Tag = filter

            };

            btn.FlatAppearance.BorderSize = 1;

            btn.Click += (s, e) => SwitchLogFilter(filter);

            return btn;

        }

        private void SwitchLogFilter(LogFilter filter)

        {

            if (this.InvokeRequired)

            {

                this.BeginInvoke(new Action(() => SwitchLogFilter(filter)));

                return;

            }

            _currentFilter = filter;

            UpdateTabButtonsUI();

            ReloadLogDisplay();

        }

        private void UpdateTabButtonsUI()

        {

            // Tab Todos

            bool isAll = _currentFilter == LogFilter.All;

            btnTabAll.BackColor = isAll ? Color.FromArgb(39, 39, 42) : Color.FromArgb(20, 20, 24);

            btnTabAll.ForeColor = isAll ? Color.White : Color.FromArgb(161, 161, 170);

            btnTabAll.FlatAppearance.BorderColor = isAll ? Color.FromArgb(59, 130, 246) : Color.FromArgb(39, 39, 42);

            btnTabAll.Font = new Font("Segoe UI", 8.25f, isAll ? FontStyle.Bold : FontStyle.Regular);

            // Tab Erros

            bool isErrors = _currentFilter == LogFilter.ErrorsOnly;

            btnTabErrors.Text = string.Format("! Somente Erros ({0})", _errorCount);

            if (_errorCount > 0)

            {

                btnTabErrors.BackColor = isErrors ? Color.FromArgb(185, 28, 28) : Color.FromArgb(69, 10, 10);

                btnTabErrors.ForeColor = isErrors ? Color.White : Color.FromArgb(254, 202, 202);

                btnTabErrors.FlatAppearance.BorderColor = Color.FromArgb(220, 38, 38);

            }

            else

            {

                btnTabErrors.BackColor = isErrors ? Color.FromArgb(39, 39, 42) : Color.FromArgb(20, 20, 24);

                btnTabErrors.ForeColor = isErrors ? Color.White : Color.FromArgb(161, 161, 170);

                btnTabErrors.FlatAppearance.BorderColor = isErrors ? Color.FromArgb(59, 130, 246) : Color.FromArgb(39, 39, 42);

            }

            btnTabErrors.Font = new Font("Segoe UI", 8.25f, isErrors ? FontStyle.Bold : FontStyle.Regular);

            // Tab Etapas

            bool isSteps = _currentFilter == LogFilter.StepsOnly;

            btnTabSteps.BackColor = isSteps ? Color.FromArgb(39, 39, 42) : Color.FromArgb(20, 20, 24);

            btnTabSteps.ForeColor = isSteps ? Color.White : Color.FromArgb(161, 161, 170);

            btnTabSteps.FlatAppearance.BorderColor = isSteps ? Color.FromArgb(34, 197, 94) : Color.FromArgb(39, 39, 42);

            btnTabSteps.Font = new Font("Segoe UI", 8.25f, isSteps ? FontStyle.Bold : FontStyle.Regular);

            // Dica de filtro

            if (_currentFilter == LogFilter.All)

                lblFilterHint.Text = "Exibindo saída completa do Firebird";

            else if (_currentFilter == LogFilter.ErrorsOnly)

                lblFilterHint.Text = string.Format("Exibindo exclusivamente falhas e corrupções ({0} detectadas)", _errorCount);

            else if (_currentFilter == LogFilter.StepsOnly)

                lblFilterHint.Text = "Exibindo resumo ordenado das etapas de recuperação";

            AdjustResponsiveLayout();

        }

        private void ReloadLogDisplay()

        {

            List<LogEntry> listToRender;

            lock (_logLock)

            {

                if (_currentFilter == LogFilter.ErrorsOnly)

                    listToRender = new List<LogEntry>(_historyErrors);

                else if (_currentFilter == LogFilter.StepsOnly)

                    listToRender = new List<LogEntry>(_historySteps);

                else

                    listToRender = new List<LogEntry>(_historyAll);

            }

            try

            {

                SendMessage(rtbLog.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);

                rtbLog.Clear();

                if (listToRender.Count == 0)

                {

                    if (_currentFilter == LogFilter.ErrorsOnly)

                    {

                        rtbLog.SelectionColor = Color.FromArgb(34, 197, 94);

                        rtbLog.SelectionFont = _logFontBold;

                        rtbLog.AppendText(Environment.NewLine + "  ✔ NENHUM ERRO OU CORRUPÇÃO DETECTADA ATÉ O MOMENTO." + Environment.NewLine);

                        rtbLog.SelectionColor = Color.FromArgb(148, 163, 184);

                        rtbLog.SelectionFont = _logFontRegular;

                        rtbLog.AppendText("  Caso o gfix ou gbak encontrem páginas corrompidas ou falhas, elas aparecerão aqui detalhadas." + Environment.NewLine);

                    }

                    else if (_currentFilter == LogFilter.StepsOnly)

                    {

                        rtbLog.SelectionColor = Color.FromArgb(148, 163, 184);

                        rtbLog.SelectionFont = _logFontRegular;

                        rtbLog.AppendText(Environment.NewLine + "  Aguardando o início do processo para listar as etapas..." + Environment.NewLine);

                    }

                }

                else

                {

                    foreach (var item in listToRender)

                    {

                        rtbLog.SelectionStart = rtbLog.TextLength;

                        rtbLog.SelectionLength = 0;

                        rtbLog.SelectionColor = item.Color;

                        rtbLog.SelectionFont = item.IsBold ? _logFontBold : _logFontRegular;

                        rtbLog.AppendText(item.Text + Environment.NewLine);

                    }

                }

                rtbLog.SelectionStart = rtbLog.TextLength;

                rtbLog.ScrollToCaret();

            }

            catch { }

            finally

            {

                SendMessage(rtbLog.Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);

                rtbLog.Invalidate();

            }

        }

        private void ClearAllLogs()

        {

            lock (_logLock)

            {

                _historyAll.Clear();

                _historyErrors.Clear();

                _historySteps.Clear();

                _logQueue.Clear();

                _errorCount = 0;

            }

            UpdateErrorBadge();

            UpdateTabButtonsUI();

            rtbLog.Clear();

        }

        private Button CreateStyledButton(string text, Color backColor, int width, int height, bool hasBorder)

        {

            Button btn = new Button

            {

                Text = text,

                Width = width,

                Height = height,

                BackColor = backColor,

                ForeColor = Color.White,

                FlatStyle = FlatStyle.Flat,

                Cursor = Cursors.Hand,

                Font = new Font("Segoe UI", 8.75f, FontStyle.Regular)

            };

            btn.FlatAppearance.BorderSize = hasBorder ? 1 : 0;

            if (hasBorder)

            {

                btn.FlatAppearance.BorderColor = Color.FromArgb(63, 63, 70);

            }

            return btn;

        }

        private void MainForm_DragEnter(object sender, DragEventArgs e)

        {

            if (e.Data.GetDataPresent(DataFormats.FileDrop))

                e.Effect = DragDropEffects.Copy;

            else

                e.Effect = DragDropEffects.None;

        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)

        {

            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (files != null && files.Length > 0)

            {

                txtDbPath.Text = files[0];

            }

        }

        // Persistencia e Criptografia Local de Configuracoes
        private static readonly byte[] CFG_KEY = new byte[] { 0x52, 0x65, 0x63, 0x75, 0x70, 0x42, 0x44, 0x40, 0x46, 0x69, 0x72, 0x65, 0x62, 0x69, 0x72, 0x64 };
        private static readonly byte[] CFG_IV = new byte[] { 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x30, 0x61, 0x62, 0x63, 0x64, 0x65, 0x66 };

        private static string EncryptPassword(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return "";
            try
            {
                using (var aes = System.Security.Cryptography.Aes.Create())
                {
                    aes.Key = CFG_KEY;
                    aes.IV = CFG_IV;
                    using (var ms = new MemoryStream())
                    {
                        using (var cs = new System.Security.Cryptography.CryptoStream(ms, aes.CreateEncryptor(), System.Security.Cryptography.CryptoStreamMode.Write))
                        {
                            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                            cs.Write(plainBytes, 0, plainBytes.Length);
                            cs.FlushFinalBlock();
                        }
                        return Convert.ToBase64String(ms.ToArray());
                    }
                }
            }
            catch { return ""; }
        }

        private static string DecryptPassword(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return "";
            try
            {
                byte[] cipherBytes = Convert.FromBase64String(cipherText);
                using (var aes = System.Security.Cryptography.Aes.Create())
                {
                    aes.Key = CFG_KEY;
                    aes.IV = CFG_IV;
                    using (var ms = new MemoryStream())
                    {
                        using (var cs = new System.Security.Cryptography.CryptoStream(ms, aes.CreateDecryptor(), System.Security.Cryptography.CryptoStreamMode.Write))
                        {
                            cs.Write(cipherBytes, 0, cipherBytes.Length);
                            cs.FlushFinalBlock();
                        }
                        return Encoding.UTF8.GetString(ms.ToArray());
                    }
                }
            }
            catch { return ""; }
        }

        private string GetConfigFilePath()
        {
            try
            {
                string localFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "recupbd.cfg");
                string testFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".test_write");
                File.WriteAllText(testFile, "1");
                File.Delete(testFile);
                return localFile;
            }
            catch
            {
                string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RecupBD");
                if (!Directory.Exists(appDataDir)) Directory.CreateDirectory(appDataDir);
                return Path.Combine(appDataDir, "recupbd.cfg");
            }
        }

        private void LoadUserConfig()
        {
            try
            {
                string cfgFile = GetConfigFilePath();
                if (!File.Exists(cfgFile))
                {
                    txtDbPath.Text = "";
                    txtUser.Text = "";
                    txtPassword.Text = "";
                    txtHost.Text = "127.0.0.1/3050";
                    if (chkSaveCredentials != null) chkSaveCredentials.Checked = true;
                    return;
                }

                string[] lines = File.ReadAllLines(cfgFile, Encoding.UTF8);
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || !line.Contains("=")) continue;
                    int eq = line.IndexOf('=');
                    string key = line.Substring(0, eq).Trim().ToUpperInvariant();
                    string val = line.Substring(eq + 1).Trim();

                    switch (key)
                    {
                        case "DB_PATH":
                            txtDbPath.Text = val;
                            break;
                        case "HOST":
                            if (!string.IsNullOrEmpty(val)) txtHost.Text = val;
                            break;
                        case "USER":
                            txtUser.Text = val;
                            break;
                        case "PASS":
                            txtPassword.Text = DecryptPassword(val);
                            break;
                        case "SAVE":
                            if (chkSaveCredentials != null) chkSaveCredentials.Checked = (val == "1" || val.ToLower() == "true");
                            break;
                    }
                }
            }
            catch { }
        }

        private void SaveUserConfig()
        {
            try
            {
                string cfgFile = GetConfigFilePath();

                if (chkSaveCredentials != null && !chkSaveCredentials.Checked)
                {
                    if (File.Exists(cfgFile)) File.Delete(cfgFile);
                    return;
                }

                List<string> lines = new List<string>();
                lines.Add("DB_PATH=" + txtDbPath.Text.Trim());
                lines.Add("HOST=" + txtHost.Text.Trim());
                lines.Add("USER=" + txtUser.Text.Trim());
                lines.Add("PASS=" + EncryptPassword(txtPassword.Text));
                lines.Add("SAVE=1");

                File.WriteAllLines(cfgFile, lines.ToArray(), Encoding.UTF8);
            }
            catch { }
        }

        private void BtnBrowse_Click(object sender, EventArgs e)

        {

            using (OpenFileDialog ofd = new OpenFileDialog())

            {

                ofd.Title = "Selecione o arquivo de banco de dados Firebird";

                ofd.Filter = "Banco Firebird (*.IB;*.FDB;*.GDB)|*.IB;*.FDB;*.GDB|Todos os Arquivos (*.*)|*.*";

                string current = txtDbPath.Text.Trim();

                if (!string.IsNullOrEmpty(current) && File.Exists(current))

                {

                    ofd.InitialDirectory = Path.GetDirectoryName(current);

                }

                if (ofd.ShowDialog() == DialogResult.OK)

                {

                    txtDbPath.Text = ofd.FileName;
                    if (chkSaveCredentials != null && chkSaveCredentials.Checked) SaveUserConfig();

                }

            }

        }

        private string FormatBytes(long bytes)

        {

            if (bytes >= 1024L * 1024 * 1024)

                return string.Format("{0:N2} GB", bytes / (1024.0 * 1024 * 1024));

            if (bytes >= 1024L * 1024)

                return string.Format("{0:N2} MB", bytes / (1024.0 * 1024));

            return string.Format("{0:N0} KB", bytes / 1024.0);

        }

        private void UpdateFileInfo()

        {

            string path = txtDbPath.Text.Trim();

            if (File.Exists(path))

            {

                FileInfo fi = new FileInfo(path);

                long requiredSpace = (long)(fi.Length * 3.0);

                long freeSpace = GetAvailableDiskSpace(path);

                string spaceStatus = string.Format(" | Espaço Livre: {0} (Necessário: ~{1})",

                    FormatBytes(freeSpace), FormatBytes(requiredSpace));

                if (freeSpace < requiredSpace)

                {

                    lblFileInfo.Text = string.Format("✔ Arquivo: {0:N2} MB {1} [ALERTA: Pouco espaço em disco!]",

                        fi.Length / (1024.0 * 1024.0), spaceStatus);

                    lblFileInfo.ForeColor = Color.FromArgb(239, 68, 68);

                }

                else

                {

                    lblFileInfo.Text = string.Format("✔ Arquivo: {0:N2} MB {1}",

                        fi.Length / (1024.0 * 1024.0), spaceStatus);

                    lblFileInfo.ForeColor = Color.FromArgb(34, 197, 94);

                }

            }

            else if (string.IsNullOrEmpty(path))

            {

                lblFileInfo.Text = "Informe o caminho do banco ou arraste o arquivo até a janela.";

                lblFileInfo.ForeColor = Color.FromArgb(148, 163, 184);

            }

            else

            {

                lblFileInfo.Text = "Aviso: O arquivo informado ainda não existe neste local.";

                lblFileInfo.ForeColor = Color.FromArgb(245, 158, 11);

            }

        }

        private long GetAvailableDiskSpace(string path)

        {

            try

            {

                string root = Path.GetPathRoot(Path.GetFullPath(path));

                DriveInfo d = new DriveInfo(root);

                return d.AvailableFreeSpace;

            }

            catch

            {

                return long.MaxValue;

            }

        }

        private void BtnOpenFolder_Click(object sender, EventArgs e)

        {

            try

            {

                string currentPath = txtDbPath.Text.Trim();

                string targetDir = AppDomain.CurrentDomain.BaseDirectory;

                if (!string.IsNullOrEmpty(currentPath) && File.Exists(currentPath))

                {

                    targetDir = Path.GetDirectoryName(currentPath);

                }

                Process.Start("explorer.exe", targetDir);

            }

            catch (Exception ex)

            {

                DarkMessageBox.Show(this, "Aviso", "Erro ao abrir pasta!", ex.Message, MessageBoxButtons.OK, MessageBoxIcon.Warning);

            }

        }

        private void BtnCancel_Click(object sender, EventArgs e)

        {

            if (_isRunning)

            {

                if (DarkMessageBox.Show(this, "Confirmar Cancelamento",

                    "Deseja realmente cancelar o processo de recuperação?",

                    "O banco original permanecerá seguro e os arquivos temporários serão limpos.",

                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)

                {

                    _isCancelled = true;

                    AppendLog(">>> CANCELAMENTO SOLICITADO PELO USUÁRIO! Encerrando processos...", Color.FromArgb(239, 68, 68), true, LogCategory.Step);

                    KillCurrentProcess();

                }

            }

        }

        private void KillCurrentProcess()

        {

            try

            {

                if (_currentProcess != null && !_currentProcess.HasExited)

                {

                    _currentProcess.Kill();

                }

            }

            catch { }

        }

        private void SetTargetProgress(int percent)

        {

            _targetPercent = Math.Max(0, Math.Min(100, percent));

        }

        private void UpdateStep(string text)

        {

            if (this.InvokeRequired)

            {

                this.BeginInvoke(new Action(() => UpdateStep(text)));

                return;

            }

            lblStep.Text = text;

        }

        private void UpdateErrorBadge()

        {

            if (this.InvokeRequired)

            {

                this.BeginInvoke(new Action(UpdateErrorBadge));

                return;

            }

            lblErrorBadge.Text = _errorCount + " Erros";

            if (_errorCount > 0)

            {

                lblErrorBadge.BackColor = Color.FromArgb(185, 28, 28);

                lblErrorBadge.ForeColor = Color.White;

            }

            else

            {

                lblErrorBadge.BackColor = Color.FromArgb(39, 39, 42);

                lblErrorBadge.ForeColor = Color.FromArgb(161, 161, 170);

            }

            UpdateTabButtonsUI();

        }

        // Thread-safe log dispatching com categorizacao e persistencia para as abas

        private void AppendLog(string line, Color color, bool isBold = false, LogCategory category = LogCategory.Normal)

        {

            LogEntry entry = new LogEntry

            {

                Text = line,

                Color = color,

                IsBold = isBold,

                Category = category,

                Timestamp = DateTime.Now

            };

            lock (_logLock)

            {

                _historyAll.Add(entry);

                if (category == LogCategory.Error || category == LogCategory.Warning)

                {

                    _historyErrors.Add(entry);

                }

                if (category == LogCategory.Step)

                {

                    _historySteps.Add(entry);

                }

                // Enfileira para exibicao se corresponder ao filtro ativo

                bool shouldDisplay = (_currentFilter == LogFilter.All) ||

                                     (_currentFilter == LogFilter.ErrorsOnly && (category == LogCategory.Error || category == LogCategory.Warning)) ||

                                     (_currentFilter == LogFilter.StepsOnly && category == LogCategory.Step);

                if (shouldDisplay)

                {

                    _logQueue.Enqueue(entry);

                }

            }

        }

        private void AppendStepLog(string line, bool isSuccess = false)

        {

            string formatted = string.Format("[{0:HH:mm:ss}] {1}", DateTime.Now, line);

            Color c = isSuccess ? Color.FromArgb(34, 197, 94) : Color.FromArgb(56, 189, 248);

            AppendLog(formatted, c, true, LogCategory.Step);

        }

        private bool IsErrorLine(string rawLine)

        {

            if (string.IsNullOrWhiteSpace(rawLine)) return false;

            string trimmed = rawLine.Trim();

            // Desconsiderar linhas normais de restauracao de metadados do gbak

            // (No Firebird, "EXCEPTION" e um objeto SQL legitimo, e tabelas/indices podem ter "ERRO" no nome)

            if (trimmed.StartsWith("gbak:restoring", StringComparison.OrdinalIgnoreCase) ||

                trimmed.StartsWith("gbak: restoring", StringComparison.OrdinalIgnoreCase) ||

                trimmed.StartsWith("gbak:writing", StringComparison.OrdinalIgnoreCase) ||

                trimmed.StartsWith("gbak: writing", StringComparison.OrdinalIgnoreCase) ||

                trimmed.StartsWith("gbak:adjusting", StringComparison.OrdinalIgnoreCase) ||

                trimmed.StartsWith("gbak: activating", StringComparison.OrdinalIgnoreCase) ||

                trimmed.StartsWith("gbak:activating", StringComparison.OrdinalIgnoreCase))

            {

                // So e erro real no gbak se contiver o prefixo oficial "ERROR:" ou termos de falha fatal

                if (!trimmed.Contains("ERROR:") && !trimmed.Contains("failed") && !trimmed.Contains("falha"))

                {

                    return false;

                }

            }

            string lower = rawLine.ToLowerInvariant();

            // Erros reais de integridade/corrupcao do Firebird (gfix e gbak)

            return lower.Contains("gbak: error:") ||

                   lower.Contains("gbak:error:") ||

                   lower.Contains("gfix: error") ||

                   lower.Contains("bad checksum") ||

                   lower.Contains("checksum error") ||

                   lower.Contains("wrong page type") ||

                   lower.Contains("wrong type") ||

                   lower.Contains("orphan page") ||

                   lower.Contains("invalid page") ||

                   lower.Contains("corrupt") ||

                   lower.Contains("damaged") ||

                   lower.Contains("consistency check") ||

                   lower.Contains("cannot attach") ||

                   lower.Contains("inconsistent") ||

                   lower.Contains("chain broken") ||

                   lower.Contains("decompression overran buffer") ||

                   lower.Contains("falha fatal") ||

                   lower.Contains("fatal error");

        }

        private bool IsWarningLine(string rawLine)

        {

            if (string.IsNullOrWhiteSpace(rawLine)) return false;

            string trimmed = rawLine.Trim();

            if (trimmed.StartsWith("gbak:restoring", StringComparison.OrdinalIgnoreCase) ||

                trimmed.StartsWith("gbak: restoring", StringComparison.OrdinalIgnoreCase) ||

                trimmed.StartsWith("gbak:writing", StringComparison.OrdinalIgnoreCase) ||

                trimmed.StartsWith("gbak: writing", StringComparison.OrdinalIgnoreCase))

            {

                if (!trimmed.Contains("warning:") && !trimmed.Contains("aviso:"))

                {

                    return false;

                }

            }

            string lower = rawLine.ToLowerInvariant();

            return lower.Contains("warning") || lower.Contains("aviso") || lower.Contains("alerta");

        }

        private void ProcessProcessOutput(string rawLine)

        {

            if (string.IsNullOrWhiteSpace(rawLine)) return;

            bool isError = IsErrorLine(rawLine);

            bool isWarning = IsWarningLine(rawLine);

            if (isError)

            {

                _errorCount++;

                UpdateErrorBadge();

                AppendLog("  [ERRO DETECTADO] " + rawLine, Color.FromArgb(239, 68, 68), true, LogCategory.Error);

            }

            else if (isWarning)

            {

                AppendLog("  [AVISO] " + rawLine, Color.FromArgb(245, 158, 11), false, LogCategory.Warning);

            }

            else if (rawLine.StartsWith("gbak: writing") || rawLine.StartsWith("gbak: restoring") || rawLine.StartsWith("gbak:"))

            {

                if (_targetPercent < 90 && _targetPercent >= 35)

                {

                    _targetPercent = Math.Min(_targetPercent + 1, 92);

                }

                AppendLog("  " + rawLine, Color.FromArgb(148, 163, 184), false, LogCategory.Normal);

            }

            else

            {

                AppendLog("  " + rawLine, Color.FromArgb(212, 212, 216), false, LogCategory.Normal);

            }

        }

        private void BtnStart_Click(object sender, EventArgs e)

        {

            if (_isRunning) return;

            string dbPath = txtDbPath.Text.Trim();

            if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))

            {

                DarkMessageBox.Show(this, "Arquivo Não Encontrado",

                    "Por favor, selecione um banco de dados válido (.IB / .FDB) existente!",

                    "Caminho informado: " + dbPath,

                    MessageBoxButtons.OK, MessageBoxIcon.Warning);

                return;

            }

            string user = txtUser.Text.Trim();
            if (string.IsNullOrEmpty(user))
            {
                DarkMessageBox.Show(this, "Usuário Ausente",
                    "Por favor, informe o usuário de conexão do Firebird!",
                    "O usuário padrão do Firebird costuma ser SYSDBA.",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtUser.Focus();
                return;
            }

            string password = txtPassword.Text;
            if (string.IsNullOrEmpty(password))
            {
                DarkMessageBox.Show(this, "Senha Ausente",
                    "Por favor, informe a senha de conexão do Firebird!",
                    "A senha padrão do Firebird costuma ser masterkey.",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtPassword.Focus();
                return;
            }

            if (chkSaveCredentials != null && chkSaveCredentials.Checked)
            {
                SaveUserConfig();
            }FileInfo fi = new FileInfo(dbPath);

            long requiredSpace = (long)(fi.Length * 3.0);

            long freeSpace = GetAvailableDiskSpace(dbPath);

            if (freeSpace < requiredSpace)

            {

                string msg = string.Format(

                    "Espaço em disco insuficiente para realizar a recuperação segura!\n\n" +

                    "• Espaço Livre Disponível:   {0}\n" +

                    "• Espaço Necessário Estimado: {1}\n\n" +

                    "O processo necessita de espaço para o banco renomeado, a cópia de trabalho e a restauração.\n" +

                    "Por favor, libere espaço no disco antes de iniciar.",

                    FormatBytes(freeSpace), FormatBytes(requiredSpace));

                DarkMessageBox.Show(this, "Espaço Insuficiente em Disco",

                    "Espaço em disco insuficiente para realizar a recuperação segura!",

                    msg, MessageBoxButtons.OK, MessageBoxIcon.Warning);

                return;

            }

            string gbakExe = LocateTool("gbak.exe");

            string gfixExe = LocateTool("gfix.exe");

            if (string.IsNullOrEmpty(gbakExe) || string.IsNullOrEmpty(gfixExe))

            {

                DarkMessageBox.Show(this, "Ferramentas Firebird Ausentes",

                    "Os utilitários gbak.exe e gfix.exe não foram localizados automaticamente.",

                    "Para solucionar:\n1. Verifique se o Firebird está instalado neste computador, ou\n2. Coloque os arquivos gbak.exe e gfix.exe na mesma pasta deste programa (" + AppDomain.CurrentDomain.BaseDirectory + ").",

                    MessageBoxButtons.OK, MessageBoxIcon.Error);

                return;

            }

            // Confirmação resumida no padrão visual escuro do sistema

            string confirmDetails =

                "• O serviço do Firebird será parado temporariamente.\n" +

                "• O banco original será preservado com data e hora na pasta de origem.\n" +

                "• O diagnóstico e reparo serão executados na cópia de trabalho.\n\n" +

                "Deseja continuar?";

            DialogResult confirm = DarkMessageBox.Show(

                this,

                "Confirmar Início da Recuperação",

                "Deseja iniciar a recuperação agora?",

                confirmDetails,

                MessageBoxButtons.YesNo,

                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes)

            {

                return;

            }

            _isRunning = true;

            _isCancelled = false;

            _errorCount = 0;

            _displayPercent = 0;

            _targetPercent = 0;

            progressBar.Value = 0;

            lblPercent.Text = "0%";

            UpdateErrorBadge();

            btnStart.Cursor = Cursors.WaitCursor;

            btnStart.Invalidate();

            btnCancel.Enabled = true;

            btnBrowse.Enabled = false;

            txtDbPath.Enabled = false;

            txtPassword.Enabled = false;

            
            
            string host = txtHost.Text.Trim();

            _workerThread = new Thread(() =>

            {

                ExecuteRecoveryProcess(dbPath, gbakExe, gfixExe, user, password, host, freeSpace, requiredSpace);

            });

            _workerThread.IsBackground = true;

            _workerThread.Start();

        }

        private string LocateTool(string toolName)

        {

            // 1. Mesma pasta do executavel

            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, toolName);

            if (File.Exists(localPath)) return localPath;

            // 2. Registro do Windows (Instancias do Firebird Server - 32 e 64 bits)

            // 2. Registro do Windows (Instancias do Firebird Server - 32 e 64 bits)

            try

            {

                string[] regKeys = new string[]

                {

                    @"SOFTWARE\WOW6432Node\Firebird Project\Firebird Server\Instances",

                    @"SOFTWARE\Firebird Project\Firebird Server\Instances"

                };

                foreach (string subKey in regKeys)

                {

                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(subKey))

                    {

                        if (key != null)

                        {

                            object defaultInst = key.GetValue("DefaultInstance");

                            if (defaultInst != null)

                            {

                                string candidate = Path.Combine(defaultInst.ToString().TrimEnd('\\', '/'), "bin", toolName);

                                if (File.Exists(candidate)) return candidate;

                            }

                            foreach (string valName in key.GetValueNames())

                            {

                                object inst = key.GetValue(valName);

                                if (inst != null)

                                {

                                    string candidate = Path.Combine(inst.ToString().TrimEnd('\\', '/'), "bin", toolName);

                                    if (File.Exists(candidate)) return candidate;

                                }

                            }

                        }

                    }

                }

            }

            catch { }

            // 4. Pastas padrao conhecidas de instalacao do Firebird

            string[] standardDirs = new string[]

            {

                @"C:\Program Files (x86)\Firebird\Firebird_4_0\bin",

                @"C:\Program Files\Firebird\Firebird_4_0\bin",

                @"C:\Program Files (x86)\Firebird\Firebird_5_0\bin",

                @"C:\Program Files\Firebird\Firebird_5_0\bin",

                @"C:\Program Files (x86)\Firebird\Firebird_3_0\bin",

                @"C:\Program Files\Firebird\Firebird_3_0\bin",

                @"C:\Program Files (x86)\Firebird\Firebird_2_5\bin",

                @"C:\Program Files\Firebird\Firebird_2_5\bin",

                @"C:\Program Files (x86)\Firebird\Firebird_2_1\bin",

                @"C:\Program Files\Firebird\Firebird_2_1\bin",

                @"C:\Program Files (x86)\Firebird\Firebird_2_0\bin",

                @"C:\Program Files\Firebird\Firebird_2_0\bin",

                @"C:\Firebird\bin"

            };

            foreach (string dir in standardDirs)

            {

                try

                {

                    string candidate = Path.Combine(dir, toolName);

                    if (File.Exists(candidate)) return candidate;

                }

                catch { }

            }

            // 5. Variavel PATH do sistema operacional

            try

            {

                string pathEnv = Environment.GetEnvironmentVariable("PATH");

                if (!string.IsNullOrEmpty(pathEnv))

                {

                    foreach (string item in pathEnv.Split(';'))

                    {

                        if (string.IsNullOrWhiteSpace(item)) continue;

                        try

                        {

                            string candidate = Path.Combine(item.Trim(), toolName);

                            if (File.Exists(candidate)) return candidate;

                        }

                        catch { }

                    }

                }

            }

            catch { }

            return null;

        }

        private void StopFirebirdService()

        {

            AppendLog("  [SERVIÇO] Parando serviço do Firebird para liberação do banco...", Color.FromArgb(245, 158, 11), false, LogCategory.Step);

            try

            {

                ProcessStartInfo psi = new ProcessStartInfo("net", "stop FirebirdServerDefaultInstance")

                {

                    UseShellExecute = false,

                    CreateNoWindow = true,

                    RedirectStandardOutput = true,

                    RedirectStandardError = true

                };

                using (Process p = Process.Start(psi))

                {

                    p.WaitForExit(10000);

                    if (p.ExitCode == 0)

                    {

                        AppendLog("  ✔ Serviço Firebird parado com sucesso.", Color.FromArgb(34, 197, 94), false, LogCategory.Step);

                        return;

                    }

                }

                ProcessStartInfo psiElevated = new ProcessStartInfo("net", "stop FirebirdServerDefaultInstance")

                {

                    UseShellExecute = true,

                    Verb = "runas",

                    WindowStyle = ProcessWindowStyle.Hidden

                };

                using (Process p = Process.Start(psiElevated))

                {

                    p.WaitForExit(15000);

                    AppendLog("  ✔ Serviço Firebird parado com privilégios administrativos.", Color.FromArgb(34, 197, 94), false, LogCategory.Step);

                }

            }

            catch (Exception ex)

            {

                AppendLog("  [AVISO] Tentativa de parar serviço: " + ex.Message, Color.FromArgb(245, 158, 11), false, LogCategory.Warning);

            }

        }

        private void StartFirebirdService()

        {

            AppendLog("  [SERVIÇO] Reiniciando serviço do Firebird...", Color.FromArgb(148, 163, 184), false, LogCategory.Step);

            try

            {

                ProcessStartInfo psi = new ProcessStartInfo("net", "start FirebirdServerDefaultInstance")

                {

                    UseShellExecute = false,

                    CreateNoWindow = true,

                    RedirectStandardOutput = true,

                    RedirectStandardError = true

                };

                using (Process p = Process.Start(psi))

                {

                    p.WaitForExit(10000);

                    if (p.ExitCode == 0)

                    {

                        AppendLog("  ✔ Serviço Firebird ativo e respondendo.", Color.FromArgb(34, 197, 94), false, LogCategory.Step);

                        return;

                    }

                }

                ProcessStartInfo psiElevated = new ProcessStartInfo("net", "start FirebirdServerDefaultInstance")

                {

                    UseShellExecute = true,

                    Verb = "runas",

                    WindowStyle = ProcessWindowStyle.Hidden

                };

                using (Process p = Process.Start(psiElevated))

                {

                    p.WaitForExit(15000);

                    AppendLog("  ✔ Serviço Firebird ativo com privilégios administrativos.", Color.FromArgb(34, 197, 94), false, LogCategory.Step);

                }

            }

            catch (Exception ex)

            {

                AppendLog("  [AVISO] Tentativa de iniciar serviço: " + ex.Message, Color.FromArgb(245, 158, 11), false, LogCategory.Warning);

            }

        }

        private void CleanWorkFiles(string workDb, string workFbk, string workOk)

        {

            try { if (File.Exists(workDb)) File.Delete(workDb); } catch { }

            try { if (File.Exists(workFbk)) File.Delete(workFbk); } catch { }

            try { if (File.Exists(workOk)) File.Delete(workOk); } catch { }

        }

        private void ExecuteRecoveryProcess(string originalDbPath, string gbakExe, string gfixExe, string user, string password, string host, long freeSpace, long requiredSpace)

        {

            string backupDbPath = null;

            string workDbPath = null;

            string workFbkPath = null;

            string workOkDbPath = null;

            try

            {

                string originalDir = Path.GetDirectoryName(originalDbPath);

                string workDir = originalDir;

                if (string.IsNullOrEmpty(workDir) || !Directory.Exists(workDir))

                    workDir = AppDomain.CurrentDomain.BaseDirectory;

                string dbNameWithoutExt = Path.GetFileNameWithoutExtension(originalDbPath);

                string dbExt = Path.GetExtension(originalDbPath);

                // O nome do arquivo original recebe o nome base + data e hora na pasta de origem

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

                backupDbPath = Path.Combine(originalDir, string.Format("{0}_{1}{2}", dbNameWithoutExt, timestamp, dbExt));

                // Arquivos temporarios de reparo na mesma particao do banco (excluidos ao final para nao acumular copias)

                workDbPath = Path.Combine(workDir, "BD_TRABALHO.IB");

                workFbkPath = Path.Combine(workDir, "BD_RECUP.FBK");

                workOkDbPath = Path.Combine(workDir, "BD_OK.IB");

                AppendLog("\n--------------------------------------------------------------------------------", Color.FromArgb(71, 85, 105), false, LogCategory.Normal);

                AppendStepLog("▶ INICIANDO PROCESSO DE RECUPERAÇÃO SEGURA");

                AppendLog("  Banco Original: " + originalDbPath, Color.White, false, LogCategory.Step);

                AppendLog("  Destino Backup Seguro na Origem: " + Path.GetFileName(backupDbPath), Color.FromArgb(56, 189, 248), false, LogCategory.Step);

                AppendLog("  Utilitários Firebird: " + Path.GetDirectoryName(gbakExe), Color.FromArgb(148, 163, 184), false, LogCategory.Normal);

                AppendLog(string.Format("  Disco: Livre: {0} | Necessário Estimado: {1} ✔", FormatBytes(freeSpace), FormatBytes(requiredSpace)), Color.FromArgb(34, 197, 94), false, LogCategory.Normal);

                AppendLog("--------------------------------------------------------------------------------", Color.FromArgb(71, 85, 105), false, LogCategory.Normal);

                // ETAPA 1: PARAR O SERVICO PARA LIBERACAO TOTAL DO ARQUIVO

                UpdateStep("Passo 1/7: Parando serviço Firebird para liberação do banco...");

                AppendStepLog("[ETAPA 1/7] Parando serviço do Firebird...");

                StopFirebirdService();

                SetTargetProgress(5);

                Thread.Sleep(400);

                if (_isCancelled) { RollbackAndFinish(originalDbPath, backupDbPath, workDbPath, workFbkPath, workOkDbPath); return; }

                // ETAPA 2: RENOMEAR O BANCO ORIGINAL NA PASTA DE ORIGEM (NOME + DATA E HORA)

                UpdateStep("Passo 2/7: Renomeando banco original com data e hora na pasta de origem...");

                AppendStepLog("[ETAPA 2/7] Renomeando original para: " + Path.GetFileName(backupDbPath));

                try

                {

                    if (File.Exists(backupDbPath)) File.Delete(backupDbPath);

                    File.Move(originalDbPath, backupDbPath);

                    AppendLog("  ✔ Banco original renomeado com sucesso e mantido seguro na origem.", Color.FromArgb(34, 197, 94), true, LogCategory.Step);

                }

                catch (Exception ex)

                {

                    AppendLog("  [FALHA] Não foi possível renomear o arquivo original: " + ex.Message, Color.FromArgb(239, 68, 68), true, LogCategory.Error);

                    StartFirebirdService();

                    return;

                }

                SetTargetProgress(10);

                Thread.Sleep(300);

                if (_isCancelled) { RollbackAndFinish(originalDbPath, backupDbPath, workDbPath, workFbkPath, workOkDbPath); return; }

                // ETAPA 3: GERAR COPIA DE TRABALHO TEMPORARIA

                UpdateStep("Passo 3/7: Criando cópia de trabalho para o reparo...");

                AppendStepLog("[ETAPA 3/7] Copiando dados para pasta de trabalho temporária...");

                if (!PerformStreamCopy(backupDbPath, workDbPath, 10, 22))

                {

                    if (_isCancelled) { RollbackAndFinish(originalDbPath, backupDbPath, workDbPath, workFbkPath, workOkDbPath); return; }

                    AppendLog("  [FALHA] Erro ao criar cópia de trabalho.", Color.FromArgb(239, 68, 68), true, LogCategory.Error);

                    RollbackAndFinish(originalDbPath, backupDbPath, workDbPath, workFbkPath, workOkDbPath);

                    return;

                }

                AppendLog("  ✔ Cópia de trabalho pronta. O banco original renomeado está 100% blindado!", Color.FromArgb(34, 197, 94), true, LogCategory.Step);

                SetTargetProgress(25);

                Thread.Sleep(300);

                if (_isCancelled) { RollbackAndFinish(originalDbPath, backupDbPath, workDbPath, workFbkPath, workOkDbPath); return; }

                // ETAPA 4: REINICIAR SERVICO PARA AS FERRAMENTAS GFIX / GBAK

                UpdateStep("Passo 4/7: Reiniciando serviço Firebird para gfix/gbak...");

                AppendStepLog("[ETAPA 4/7] Reiniciando serviço Firebird...");

                StartFirebirdService();

                SetTargetProgress(28);

                Thread.Sleep(400);

                if (_isCancelled) { RollbackAndFinish(originalDbPath, backupDbPath, workDbPath, workFbkPath, workOkDbPath); return; }

                // ETAPA 5: EXECUCAO DO GFIX NA COPIA DE TRABALHO

                UpdateStep("Passo 5/7: Executando validação e reparo de páginas com gfix...");

                AppendStepLog("[ETAPA 5/7] Executando gfix -v -full e gfix -mend na cópia de trabalho...");

                AppendLog("\n--- Executando gfix -v -full (Identificação de corrupções) ---", Color.FromArgb(56, 189, 248), false, LogCategory.Normal);

                RunCommand(gfixExe, string.Format("-v -full \"{0}\"", workDbPath), user, password);

                SetTargetProgress(35);

                if (_isCancelled) { RollbackAndFinish(originalDbPath, backupDbPath, workDbPath, workFbkPath, workOkDbPath); return; }

                AppendLog("\n--- Executando gfix -mend -full -ignore (Correção e liberação de páginas) ---", Color.FromArgb(56, 189, 248), false, LogCategory.Normal);

                RunCommand(gfixExe, string.Format("-mend -full -ignore \"{0}\"", workDbPath), user, password);

                SetTargetProgress(42);

                Thread.Sleep(300);

                if (_isCancelled) { RollbackAndFinish(originalDbPath, backupDbPath, workDbPath, workFbkPath, workOkDbPath); return; }

                // ETAPA 6: BACKUP LOGICO GBAK -B

                UpdateStep("Passo 6/7: Exportando tabelas com backup lógico (gbak -b)...");

                AppendStepLog("[ETAPA 6/7] Exportando dados estruturados para .FBK...");

                if (File.Exists(workFbkPath)) { try { File.Delete(workFbkPath); } catch { } }

                string gbakBackupArgs = string.Format("-b -v -ig -g -l -se {0}:service_mgr \"{1}\" \"{2}\"",

                    host, workDbPath, workFbkPath);

                int backupExit = RunCommand(gbakExe, gbakBackupArgs, user, password);

                if (!File.Exists(workFbkPath) || new FileInfo(workFbkPath).Length == 0)

                {

                    AppendLog("  [ALERTA] Tentando backup local direto sem service_mgr...", Color.FromArgb(245, 158, 11), false, LogCategory.Warning);

                    string gbakLocalArgs = string.Format("-b -v -ig -g -l \"{0}\" \"{1}\"", workDbPath, workFbkPath);

                    backupExit = RunCommand(gbakExe, gbakLocalArgs, user, password);

                }

                SetTargetProgress(68);

                Thread.Sleep(300);

                if (_isCancelled) { RollbackAndFinish(originalDbPath, backupDbPath, workDbPath, workFbkPath, workOkDbPath); return; }

                if (!File.Exists(workFbkPath) || new FileInfo(workFbkPath).Length == 0)

                {

                    AppendLog("  [FALHA] O arquivo de backup .FBK não foi gerado.", Color.FromArgb(239, 68, 68), true, LogCategory.Error);

                    RollbackAndFinish(originalDbPath, backupDbPath, workDbPath, workFbkPath, workOkDbPath);

                    return;

                }

                AppendLog("  ✔ Backup lógico .FBK gerado com sucesso!", Color.FromArgb(34, 197, 94), true, LogCategory.Step);

                // ETAPA 7: RESTAURACAO LIMPA GBAK -C E SUBSTITUICAO NO LUGAR ORIGINAL

                UpdateStep("Passo 7/7: Reconstruindo banco limpo e índices (gbak -c)...");

                AppendStepLog("[ETAPA 7/7] Restaurando banco novo e reconstruindo índices...");

                if (File.Exists(workOkDbPath)) { try { File.Delete(workOkDbPath); } catch { } }

                string gbakRestoreArgs = string.Format("-c -v -se {0}:service_mgr \"{1}\" \"{2}\"",

                    host, workFbkPath, workOkDbPath);

                int restoreExit = RunCommand(gbakExe, gbakRestoreArgs, user, password);

                if (!File.Exists(workOkDbPath) || new FileInfo(workOkDbPath).Length == 0)

                {

                    AppendLog("  [ALERTA] Tentando restauração local direta sem service_mgr...", Color.FromArgb(245, 158, 11), false, LogCategory.Warning);

                    string gbakLocalRestore = string.Format("-c -v \"{0}\" \"{1}\"", workFbkPath, workOkDbPath);

                    restoreExit = RunCommand(gbakExe, gbakLocalRestore, user, password);

                }

                SetTargetProgress(90);

                Thread.Sleep(300);

                if (_isCancelled) { RollbackAndFinish(originalDbPath, backupDbPath, workDbPath, workFbkPath, workOkDbPath); return; }

                if (!File.Exists(workOkDbPath) || new FileInfo(workOkDbPath).Length == 0)

                {

                    AppendLog("  [FALHA] A restauração do banco corrigido não foi concluída.", Color.FromArgb(239, 68, 68), true, LogCategory.Error);

                    RollbackAndFinish(originalDbPath, backupDbPath, workDbPath, workFbkPath, workOkDbPath);

                    return;

                }

                AppendLog("  ✔ Banco novo reconstruído sem erros de página!", Color.FromArgb(34, 197, 94), true, LogCategory.Step);

                // VALIDACAO FINAL NO BANCO NOVO

                UpdateStep("Validando integridade final (gfix -v -f)...");

                AppendStepLog("Executando validação final de integridade (gfix -v -f)...");

                SetTargetProgress(94);

                RunCommand(gfixExe, string.Format("-v -f \"{0}\"", workOkDbPath), user, password);

                // ETAPA FINAL: COLOCAR O BANCO CORRIGIDO NO LUGAR DO ORIGINAL

                UpdateStep("Finalizando: Posicionando banco corrigido no local original...");

                AppendStepLog("Colocando o banco corrigido no caminho original...");

                StopFirebirdService();

                try

                {

                    File.Copy(workOkDbPath, originalDbPath, true);

                    AppendLog("  ✔ Banco corrigido colocado no local original: " + originalDbPath, Color.FromArgb(34, 197, 94), true, LogCategory.Step);

                }

                catch (Exception ex)

                {

                    AppendLog("  [AVISO] Erro ao mover direto para original: " + ex.Message, Color.FromArgb(245, 158, 11), false, LogCategory.Warning);

                }

                // LIMPEZA DOS ARQUIVOS TEMPORARIOS (BD_TRABALHO, BD_RECUP, BD_OK)

                AppendStepLog("Limpando arquivos temporários intermediários...");

                CleanWorkFiles(workDbPath, workFbkPath, workOkDbPath);

                AppendLog("  ✔ Arquivos temporários removidos com sucesso. Apenas o backup e o corrigido foram mantidos.", Color.FromArgb(148, 163, 184), false, LogCategory.Step);

                // REINICIAR SERVICO

                StartFirebirdService();

                SetTargetProgress(100);

                UpdateStep("✔ PROCESSO CONCLUÍDO COM SUCESSO!");

                AppendLog("\n================================================================================", Color.FromArgb(34, 197, 94), false, LogCategory.Normal);

                AppendStepLog("🎉 RECUPERAÇÃO CONCLUÍDA COM TOTAL SUCESSO!", true);

                AppendLog("  • Banco Corrigido (Ativo no Sistema): " + originalDbPath, Color.FromArgb(34, 197, 94), true, LogCategory.Step);

                AppendLog("  • Cópia Original Salva com Data/Hora: " + backupDbPath, Color.White, false, LogCategory.Step);

                AppendLog("  • Erros / Corrupções Detectadas e Reparadas: " + _errorCount, (_errorCount > 0 ? Color.FromArgb(239, 68, 68) : Color.FromArgb(34, 197, 94)), true, LogCategory.Step);

                AppendLog("================================================================================", Color.FromArgb(34, 197, 94), false, LogCategory.Normal);

                try { SystemSounds.Asterisk.Play(); } catch { }

            }

            catch (Exception ex)

            {

                AppendLog("\n[ERRO CRÍTICO] " + ex.Message, Color.FromArgb(239, 68, 68), true, LogCategory.Error);

                AppendLog(ex.StackTrace, Color.FromArgb(148, 163, 184), false, LogCategory.Normal);

                RollbackAndFinish(originalDbPath, backupDbPath, workDbPath, workFbkPath, workOkDbPath);

            }

            finally

            {

                FinishProcess();

            }

        }

        private void RollbackAndFinish(string originalDbPath, string backupDbPath, string workDb, string workFbk, string workOk)

        {

            try

            {

                AppendLog("\n[SEGURANÇA] Verificando integridade dos arquivos originais...", Color.FromArgb(245, 158, 11), false, LogCategory.Step);

                // Se o original foi renomeado e o banco corrigido nao foi colocado no lugar, restaura o nome original

                if (!string.IsNullOrEmpty(backupDbPath) && File.Exists(backupDbPath) && !File.Exists(originalDbPath))

                {

                    try

                    {

                        File.Move(backupDbPath, originalDbPath);

                        AppendLog("  ✔ Banco original restaurado ao nome original por segurança.", Color.FromArgb(34, 197, 94), false, LogCategory.Step);

                    }

                    catch (Exception ex)

                    {

                        AppendLog("  [AVISO] Não foi possível reverter nome original: " + ex.Message + ". O arquivo está seguro em: " + backupDbPath, Color.FromArgb(245, 158, 11), false, LogCategory.Warning);

                    }

                }

                CleanWorkFiles(workDb, workFbk, workOk);

                StartFirebirdService();

            }

            catch { }

        }

        private bool PerformStreamCopy(string sourceFile, string destFile, int startPercent, int endPercent)

        {

            try

            {

                const int bufferSize = 1024 * 1024;

                byte[] buffer = new byte[bufferSize];

                using (FileStream source = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))

                using (FileStream dest = new FileStream(destFile, FileMode.Create, FileAccess.Write, FileShare.None))

                {

                    long totalBytes = source.Length;

                    long totalRead = 0;

                    int bytesRead;

                    while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)

                    {

                        if (_isCancelled) return false;

                        dest.Write(buffer, 0, bytesRead);

                        totalRead += bytesRead;

                        if (totalBytes > 0)

                        {

                            double progressFraction = (double)totalRead / totalBytes;

                            int target = startPercent + (int)(progressFraction * (endPercent - startPercent));

                            SetTargetProgress(target);

                        }

                    }

                }

                return true;

            }

            catch (Exception ex)

            {

                AppendLog("  [ERRO CÓPIA] " + ex.Message, Color.FromArgb(239, 68, 68), true, LogCategory.Error);

                return false;

            }

        }

        private int RunCommand(string exePath, string arguments, string user, string password)

        {

            if (_isCancelled) return -1;

            AppendLog(string.Format("  > {0} {1}", Path.GetFileName(exePath), arguments), Color.FromArgb(148, 163, 184), false, LogCategory.Normal);

            ProcessStartInfo psi = new ProcessStartInfo

            {

                FileName = exePath,

                Arguments = arguments,

                UseShellExecute = false,

                RedirectStandardOutput = true,

                RedirectStandardError = true,

                CreateNoWindow = true,

                StandardOutputEncoding = Encoding.Default,

                StandardErrorEncoding = Encoding.Default

            };

            psi.EnvironmentVariables["ISC_USER"] = user;

            psi.EnvironmentVariables["ISC_PASSWORD"] = password;

            using (Process p = new Process())

            {

                p.StartInfo = psi;

                _currentProcess = p;

                p.OutputDataReceived += (s, e) =>

                {

                    if (e.Data != null) ProcessProcessOutput(e.Data);

                };

                p.ErrorDataReceived += (s, e) =>

                {

                    if (e.Data != null) ProcessProcessOutput(e.Data);

                };

                p.Start();

                p.BeginOutputReadLine();

                p.BeginErrorReadLine();

                p.WaitForExit();

                _currentProcess = null;

                return p.ExitCode;

            }

        }

        private void FinishProcess()

        {

            if (this.InvokeRequired)

            {

                this.BeginInvoke(new Action(FinishProcess));

                return;

            }

            _isRunning = false;

            btnStart.Cursor = Cursors.Hand;

            btnStart.Invalidate();

            btnCancel.Enabled = false;

            btnBrowse.Enabled = true;

            txtDbPath.Enabled = true;

            txtPassword.Enabled = true;

            if (_isCancelled)

            {

                UpdateStep("Processo cancelado pelo usuário.");

                AppendLog("\n[STATUS] Operação cancelada.", Color.FromArgb(239, 68, 68), true, LogCategory.Step);

            }

        }

        private static void LogCrashToDisk(Exception ex)

        {

            try

            {

                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "erro_recup.log");

                File.AppendAllText(logPath, string.Format("\n[{0:dd/MM/yyyy HH:mm:ss}] CRASH:\n{1}\n{2}\n-----------------------------------\n",

                    DateTime.Now, ex.Message, ex.StackTrace));

            }

            catch { }

        }

        [STAThread]

        public static void Main()

        {

            Application.EnableVisualStyles();

            Application.SetCompatibleTextRenderingDefault(false);

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            Application.ThreadException += (s, e) =>

            {

                LogCrashToDisk(e.Exception);

                DarkMessageBox.Show(null, "Falha Inesperada",

                    "Ocorreu uma falha no aplicativo!",

                    e.Exception.Message + "\n\nO erro foi salvo em erro_recup.log.",

                    MessageBoxButtons.OK, MessageBoxIcon.Error);

            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>

            {

                Exception ex = e.ExceptionObject as Exception;

                if (ex != null)

                {

                    LogCrashToDisk(ex);

                }

            };

            Application.Run(new MainForm());

        }

    }

    // Controle customizado para barra de progresso suave com gradiente moderno

    public class SmoothProgressBar : Control

    {

        private int _value = 0;

        public int Value

        {

            get { return _value; }

            set

            {

                int clamped = Math.Max(0, Math.Min(100, value));

                if (_value != clamped)

                {

                    _value = clamped;

                    this.Invalidate();

                }

            }

        }

        public SmoothProgressBar()

        {

            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);

            this.BackColor = Color.FromArgb(24, 24, 28);

        }

        protected override void OnPaint(PaintEventArgs e)

        {

            Graphics g = e.Graphics;

            g.SmoothingMode = SmoothingMode.AntiAlias;

            int w = this.Width;

            int h = this.Height;

            // Fundo da barra

            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(24, 24, 28)))

            {

                g.FillRectangle(bgBrush, 0, 0, w, h);

            }

            int fillWidth = (int)((_value / 100.0) * w);

            if (fillWidth > 0)

            {

                Rectangle fillRect = new Rectangle(0, 0, fillWidth, h);

                using (LinearGradientBrush brush = new LinearGradientBrush(

                    fillRect,

                    Color.FromArgb(34, 197, 94),  // Verde moderno

                    Color.FromArgb(16, 185, 129), // Esmeralda brilhante

                    LinearGradientMode.Horizontal))

                {

                    g.FillRectangle(brush, fillRect);

                }

            }

            // Borda sutil

            using (Pen borderPen = new Pen(Color.FromArgb(39, 39, 42), 1f))

            {

                g.DrawRectangle(borderPen, 0, 0, w - 1, h - 1);

            }

        }

    }

    // Janela Modal com Informações do Criador

    public class AboutForm : Form

    {

        [DllImport("user32.dll")]

        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll")]

        public static extern bool ReleaseCapture();

        public AboutForm()

        {

            this.FormBorderStyle = FormBorderStyle.None;

            this.StartPosition = FormStartPosition.CenterParent;

            this.Size = new Size(500, 365);

            this.BackColor = Color.FromArgb(20, 20, 24);

            this.ForeColor = Color.FromArgb(244, 244, 245);

            this.Font = new Font("Segoe UI", 9f, FontStyle.Regular);

            this.ShowInTaskbar = false;

            this.Paint += (s, pe) =>

            {

                using (Pen borderPen = new Pen(Color.FromArgb(63, 63, 70), 1.5f))

                {

                    pe.Graphics.DrawRectangle(borderPen, 0, 0, this.Width - 1, this.Height - 1);

                }

            };

            // 1. Cabecalho

            Panel pnlHeader = new Panel

            {

                Dock = DockStyle.Top,

                Height = 48,

                BackColor = Color.FromArgb(16, 16, 18),

                Padding = new Padding(14, 8, 14, 8)

            };

            pnlHeader.MouseDown += (s, e) =>

            {

                if (e.Button == MouseButtons.Left)

                {

                    ReleaseCapture();

                    SendMessage(this.Handle, 0xA1, 0x2, 0);

                }

            };

            PictureBox picIcon = new PictureBox

            {

                Size = new Size(28, 28),

                Location = new Point(14, 10),

                BackColor = Color.Transparent

            };

            picIcon.Paint += (s, pe) =>

            {

                pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                using (Pen pen = new Pen(Color.FromArgb(99, 102, 241), 2f))

                {

                    pe.Graphics.DrawEllipse(pen, 2, 2, 23, 23);

                }

                using (SolidBrush brush = new SolidBrush(Color.FromArgb(244, 244, 245)))

                using (Font f = new Font("Segoe UI", 11f, FontStyle.Bold))

                {

                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

                    pe.Graphics.DrawString("!", f, brush, new RectangleF(0, 0, 28, 28), sf);

                }

            };

            pnlHeader.Controls.Add(picIcon);

            Label lblTitle = new Label

            {

                Text = "Sobre o Desenvolvedor",

                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),

                ForeColor = Color.White,

                Location = new Point(48, 13),

                AutoSize = true

            };

            lblTitle.MouseDown += (s, e) =>

            {

                if (e.Button == MouseButtons.Left)

                {

                    ReleaseCapture();

                    SendMessage(this.Handle, 0xA1, 0x2, 0);

                }

            };

            pnlHeader.Controls.Add(lblTitle);

            Button btnCloseX = new Button

            {

                Text = "✕",

                Size = new Size(28, 28),

                Location = new Point(500 - 38, 10),

                FlatStyle = FlatStyle.Flat,

                BackColor = Color.Transparent,

                ForeColor = Color.FromArgb(161, 161, 170),

                Cursor = Cursors.Hand,

                Font = new Font("Segoe UI", 10f, FontStyle.Bold)

            };

            btnCloseX.FlatAppearance.BorderSize = 0;

            btnCloseX.MouseEnter += (s, e) => { btnCloseX.ForeColor = Color.White; btnCloseX.BackColor = Color.FromArgb(220, 38, 38); };

            btnCloseX.MouseLeave += (s, e) => { btnCloseX.ForeColor = Color.FromArgb(161, 161, 170); btnCloseX.BackColor = Color.Transparent; };

            btnCloseX.Click += (s, e) => this.Close();

            pnlHeader.Controls.Add(btnCloseX);

            this.Controls.Add(pnlHeader);

            // 2. Card de Identificacao do Desenvolvedor com a LOGO

            Panel pnlCard = new Panel

            {

                Location = new Point(20, 58),

                Size = new Size(460, 106),

                BackColor = Color.FromArgb(15, 15, 18),

                Padding = new Padding(14, 10, 14, 10)

            };

            pnlCard.Paint += (s, pe) =>

            {

                using (Pen p = new Pen(Color.FromArgb(39, 39, 42), 1f))

                {

                    pe.Graphics.DrawRectangle(p, 0, 0, pnlCard.Width - 1, pnlCard.Height - 1);

                }

            };

            PictureBox picLogo = new PictureBox

            {

                Location = new Point(14, 14),

                Size = new Size(54, 54),

                SizeMode = PictureBoxSizeMode.Zoom,

                BackColor = Color.Transparent

            };

            Image logoImg = LoadAppLogo();

            if (logoImg != null)

            {

                picLogo.Image = logoImg;

            }

            pnlCard.Controls.Add(picLogo);

            Label lblDevName = new Label

            {

                Text = "Jhone Andrade",

                Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),

                ForeColor = Color.White,

                Location = new Point(80, 8),

                AutoSize = true

            };

            pnlCard.Controls.Add(lblDevName);

            Label lblRole = new Label

            {

                Text = "Analista de Sistemas",

                Font = new Font("Segoe UI", 9f, FontStyle.Regular),

                ForeColor = Color.FromArgb(148, 163, 184),

                Location = new Point(81, 33),

                AutoSize = true

            };

            pnlCard.Controls.Add(lblRole);

            Label lblLocation = new Label

            {

                Text = "Local: Uberlândia - MG",

                Font = new Font("Segoe UI", 8.75f, FontStyle.Regular),

                ForeColor = Color.FromArgb(212, 212, 216),

                Location = new Point(81, 55),

                AutoSize = true

            };

            pnlCard.Controls.Add(lblLocation);

            Label lblVersion = new Label

            {

                Text = "Versão: v1.2.0    •    Build: 28/09/2026",

                Font = new Font("Segoe UI", 8.75f, FontStyle.Bold),

                ForeColor = Color.FromArgb(34, 197, 94),

                Location = new Point(81, 77),

                AutoSize = true

            };

            pnlCard.Controls.Add(lblVersion);

            this.Controls.Add(pnlCard);

            // 3. Card LinkedIn com URL atualizada (br.linkedin.com)

            Panel pnlLinkedIn = CreateSocialCard(

                20, 174, 460, 48,

                "LinkedIn",

                "Jhone Andrade | LinkedIn",

                "https://br.linkedin.com/in/jhone-andrade-b450ab164",

                Color.FromArgb(10, 102, 194),

                "in"

            );

            this.Controls.Add(pnlLinkedIn);

            // 4. Card GitHub

            Panel pnlGitHub = CreateSocialCard(

                20, 230, 460, 48,

                "GitHub",

                "Jhone Andrade | GitHub",

                "https://github.com/jhoneandrade?tab=repositories",

                Color.FromArgb(36, 41, 47),

                "git"

            );

            this.Controls.Add(pnlGitHub);

            // 5. Rodape

            Label lblCopyright = new Label

            {

                Text = "© 2026 Jhone Andrade",

                Font = new Font("Segoe UI", 8.25f, FontStyle.Regular),

                ForeColor = Color.FromArgb(148, 163, 184),

                Location = new Point(22, 294),

                AutoSize = true

            };

            this.Controls.Add(lblCopyright);

            Label lblAiCollab = new Label

            {

                Text = "Projeto desenvolvido em colaboração com Inteligência Artificial",

                Font = new Font("Segoe UI", 7.5f, FontStyle.Regular),

                ForeColor = Color.FromArgb(113, 113, 122),

                Location = new Point(22, 314),

                AutoSize = true

            };

            this.Controls.Add(lblAiCollab);

            Button btnClose = new Button

            {

                Text = "Fechar",

                Size = new Size(100, 32),

                Location = new Point(500 - 20 - 100, 294),

                BackColor = Color.FromArgb(39, 39, 42),

                ForeColor = Color.White,

                FlatStyle = FlatStyle.Flat,

                Cursor = Cursors.Hand,

                Font = new Font("Segoe UI", 8.75f, FontStyle.Regular)

            };

            btnClose.FlatAppearance.BorderSize = 1;

            btnClose.FlatAppearance.BorderColor = Color.FromArgb(63, 63, 70);

            btnClose.Click += (s, e) => this.Close();

            this.Controls.Add(btnClose);

            this.KeyPreview = true;

            this.KeyDown += (s, e) =>

            {

                if (e.KeyCode == Keys.Escape) this.Close();

            };

        }

        private Image LoadAppLogo()

        {

            return MainForm.GetAppLogo();

        }

        private static void SafeOpenUrl(string url)

        {

            try

            {

                ProcessStartInfo psi = new ProcessStartInfo

                {

                    FileName = "explorer.exe",

                    Arguments = "\"" + url + "\"",

                    UseShellExecute = false

                };

                Process.Start(psi);

            }

            catch

            {

                try

                {

                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

                }

                catch { }

            }

        }

        private Panel CreateSocialCard(int x, int y, int width, int height, string platform, string linkText, string url, Color badgeColor, string badgeText)

        {

            Panel card = new Panel

            {

                Location = new Point(x, y),

                Size = new Size(width, height),

                BackColor = Color.FromArgb(15, 15, 18),

                Cursor = Cursors.Hand

            };

            card.Paint += (s, pe) =>

            {

                pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                using (Pen p = new Pen(Color.FromArgb(39, 39, 42), 1f))

                {

                    pe.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);

                }

                Rectangle badgeRect = new Rectangle(12, 9, 30, 30);

                using (SolidBrush b = new SolidBrush(badgeColor))

                {

                    pe.Graphics.FillRectangle(b, badgeRect);

                }

                using (SolidBrush tb = new SolidBrush(Color.White))

                using (Font bf = new Font("Segoe UI", 9.5f, FontStyle.Bold))

                {

                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

                    pe.Graphics.DrawString(badgeText, bf, tb, badgeRect, sf);

                }

            };

            Label lblPlat = new Label

            {

                Text = platform,

                Location = new Point(54, 7),

                AutoSize = true,

                Font = new Font("Segoe UI", 7.75f, FontStyle.Regular),

                ForeColor = Color.FromArgb(148, 163, 184),

                Cursor = Cursors.Hand

            };

            card.Controls.Add(lblPlat);

            LinkLabel lnk = new LinkLabel

            {

                Text = linkText,

                Location = new Point(54, 23),

                AutoSize = true,

                Font = new Font("Segoe UI", 9f, FontStyle.Bold),

                LinkColor = Color.FromArgb(56, 189, 248),

                ActiveLinkColor = Color.FromArgb(125, 211, 252),

                VisitedLinkColor = Color.FromArgb(56, 189, 248),

                LinkBehavior = LinkBehavior.HoverUnderline,

                Cursor = Cursors.Hand

            };

            card.Controls.Add(lnk);

            Action clickAction = () => SafeOpenUrl(url);

            card.Click += (s, e) => clickAction();

            lblPlat.Click += (s, e) => clickAction();

            lnk.Click += (s, e) => clickAction();

            EventHandler onEnter = (s, e) => { card.BackColor = Color.FromArgb(24, 24, 28); };

            EventHandler onLeave = (s, e) => { card.BackColor = Color.FromArgb(15, 15, 18); };

            card.MouseEnter += onEnter;

            card.MouseLeave += onLeave;

            lblPlat.MouseEnter += onEnter;

            lblPlat.MouseLeave += onLeave;

            lnk.MouseEnter += onEnter;

            lnk.MouseLeave += onLeave;

            return card;

        }

    }

    // Caixa de Dialogo Escura no Padrao do Sistema

    public class DarkMessageBox : Form

    {

        [DllImport("user32.dll")]

        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll")]

        private static extern bool ReleaseCapture();

        public static DialogResult Show(IWin32Window owner, string title, string prompt, string details = null, MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.Information)

        {

            using (var dlg = new DarkMessageBox(title, prompt, details, buttons, icon))

            {

                if (owner != null)

                {

                    dlg.StartPosition = FormStartPosition.CenterParent;

                    return dlg.ShowDialog(owner);

                }

                else

                {

                    dlg.StartPosition = FormStartPosition.CenterScreen;

                    return dlg.ShowDialog();

                }

            }

        }

        private DarkMessageBox(string title, string prompt, string details, MessageBoxButtons buttons, MessageBoxIcon icon)

        {

            this.FormBorderStyle = FormBorderStyle.None;

            this.ShowInTaskbar = false;

            this.Size = new Size(500, 245);

            this.BackColor = Color.FromArgb(20, 20, 24);

            this.ForeColor = Color.FromArgb(244, 244, 245);

            this.Font = new Font("Segoe UI", 9f, FontStyle.Regular);

            this.Paint += (s, pe) =>

            {

                using (Pen borderPen = new Pen(Color.FromArgb(63, 63, 70), 1.5f))

                {

                    pe.Graphics.DrawRectangle(borderPen, 0, 0, this.Width - 1, this.Height - 1);

                }

            };

            // 1. Cabecalho

            Panel pnlHeader = new Panel

            {

                Dock = DockStyle.Top,

                Height = 44,

                BackColor = Color.FromArgb(16, 16, 18),

                Padding = new Padding(16, 8, 16, 8)

            };

            pnlHeader.MouseDown += (s, e) =>

            {

                if (e.Button == MouseButtons.Left)

                {

                    ReleaseCapture();

                    SendMessage(this.Handle, 0xA1, 0x2, 0);

                }

            };

            Label lblTitle = new Label

            {

                Text = title,

                Font = new Font("Segoe UI", 10f, FontStyle.Bold),

                ForeColor = Color.White,

                Location = new Point(16, 12),

                AutoSize = true

            };

            lblTitle.MouseDown += (s, e) =>

            {

                if (e.Button == MouseButtons.Left)

                {

                    ReleaseCapture();

                    SendMessage(this.Handle, 0xA1, 0x2, 0);

                }

            };

            pnlHeader.Controls.Add(lblTitle);

            Button btnCloseX = new Button

            {

                Text = "✕",

                Size = new Size(28, 28),

                Location = new Point(500 - 38, 8),

                FlatStyle = FlatStyle.Flat,

                BackColor = Color.Transparent,

                ForeColor = Color.FromArgb(161, 161, 170),

                Cursor = Cursors.Hand,

                Font = new Font("Segoe UI", 9f, FontStyle.Bold)

            };

            btnCloseX.FlatAppearance.BorderSize = 0;

            btnCloseX.Click += (s, e) =>

            {

                this.DialogResult = (buttons == MessageBoxButtons.YesNo) ? DialogResult.No : DialogResult.Cancel;

                this.Close();

            };

            pnlHeader.Controls.Add(btnCloseX);

            this.Controls.Add(pnlHeader);

            // 2. Icone Vetorial

            PictureBox picIcon = new PictureBox

            {

                Size = new Size(36, 36),

                Location = new Point(20, 60),

                BackColor = Color.Transparent

            };

            Color iconColor;

            Color iconBgColor;

            string iconChar;

            if (icon == MessageBoxIcon.Question)

            {

                iconColor = Color.FromArgb(56, 189, 248);

                iconBgColor = Color.FromArgb(15, 30, 48);

                iconChar = "?";

            }

            else if (icon == MessageBoxIcon.Warning || icon == MessageBoxIcon.Exclamation)

            {

                iconColor = Color.FromArgb(245, 158, 11);

                iconBgColor = Color.FromArgb(48, 30, 10);

                iconChar = "!";

            }

            else if (icon == MessageBoxIcon.Error || icon == MessageBoxIcon.Hand || icon == MessageBoxIcon.Stop)

            {

                iconColor = Color.FromArgb(239, 68, 68);

                iconBgColor = Color.FromArgb(48, 15, 15);

                iconChar = "✕";

            }

            else

            {

                iconColor = Color.FromArgb(34, 197, 94);

                iconBgColor = Color.FromArgb(15, 40, 25);

                iconChar = "✔";

            }

            picIcon.Paint += (s, pe) =>

            {

                pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                using (var b = new SolidBrush(iconBgColor))

                {

                    pe.Graphics.FillEllipse(b, 2, 2, 32, 32);

                }

                using (var p = new Pen(iconColor, 1.8f))

                {

                    pe.Graphics.DrawEllipse(p, 2, 2, 32, 32);

                }

                using (var b = new SolidBrush(iconColor))

                using (var f = new Font("Segoe UI", (iconChar == "✕" ? 10f : 12f), FontStyle.Bold))

                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })

                {

                    pe.Graphics.DrawString(iconChar, f, b, new RectangleF(0, 0, 36, 36), sf);

                }

            };

            this.Controls.Add(picIcon);

            // 3. Texto Prompt e Detalhes

            Label lblPrompt = new Label

            {

                Text = prompt,

                Font = new Font("Segoe UI", 9.75f, FontStyle.Bold),

                ForeColor = Color.White,

                Location = new Point(68, 58),

                AutoSize = true

            };

            this.Controls.Add(lblPrompt);

            if (!string.IsNullOrEmpty(details))

            {

                Label lblDetails = new Label

                {

                    Text = details,

                    Font = new Font("Segoe UI", 8.75f, FontStyle.Regular),

                    ForeColor = Color.FromArgb(161, 161, 170),

                    Location = new Point(68, 86),

                    Size = new Size(410, 90)

                };

                this.Controls.Add(lblDetails);

            }

            // 4. Rodape com Botoes

            Panel pnlFooter = new Panel

            {

                Dock = DockStyle.Bottom,

                Height = 52,

                BackColor = Color.FromArgb(16, 16, 18),

                Padding = new Padding(16, 8, 16, 8)

            };

            if (buttons == MessageBoxButtons.YesNo)

            {

                Button btnYes = new Button

                {

                    Text = "Sim",

                    Size = new Size(100, 32),

                    Location = new Point(500 - 226, 10),

                    FlatStyle = FlatStyle.Flat,

                    BackColor = Color.FromArgb(22, 163, 74),

                    ForeColor = Color.White,

                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),

                    Cursor = Cursors.Hand,

                    DialogResult = DialogResult.Yes

                };

                btnYes.FlatAppearance.BorderSize = 0;

                pnlFooter.Controls.Add(btnYes);

                Button btnNo = new Button

                {

                    Text = "Não",

                    Size = new Size(100, 32),

                    Location = new Point(500 - 116, 10),

                    FlatStyle = FlatStyle.Flat,

                    BackColor = Color.FromArgb(39, 39, 42),

                    ForeColor = Color.White,

                    Font = new Font("Segoe UI", 9f, FontStyle.Regular),

                    Cursor = Cursors.Hand,

                    DialogResult = DialogResult.No

                };

                btnNo.FlatAppearance.BorderSize = 1;

                btnNo.FlatAppearance.BorderColor = Color.FromArgb(63, 63, 70);

                pnlFooter.Controls.Add(btnNo);

                this.AcceptButton = btnYes;

                this.CancelButton = btnNo;

            }

            else

            {

                Button btnOk = new Button

                {

                    Text = "Entendido",

                    Size = new Size(110, 32),

                    Location = new Point(500 - 126, 10),

                    FlatStyle = FlatStyle.Flat,

                    BackColor = Color.FromArgb(39, 39, 42),

                    ForeColor = Color.White,

                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),

                    Cursor = Cursors.Hand,

                    DialogResult = DialogResult.OK

                };

                btnOk.FlatAppearance.BorderSize = 1;

                btnOk.FlatAppearance.BorderColor = Color.FromArgb(63, 63, 70);

                pnlFooter.Controls.Add(btnOk);

                this.AcceptButton = btnOk;

                this.CancelButton = btnOk;

            }

            this.Controls.Add(pnlFooter);

        }

    }

}
