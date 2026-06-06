using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Toolkit.Uwp.Notifications;

namespace ReminderApp
{
    static class Program
    {
        static System.Windows.Forms.Timer _timer;
        
        static int WorkMinutes = 45;
        static int RestMinutes = 15;
        static int OvertimeMinutes = 10;
        
        static bool _isResting = false;
        static bool _isPaused = false;
        static DateTime _phaseEndTime;
        static TimeSpan _remainingTime;
        
        static NotifyIcon _trayIcon;
        static ContextMenuStrip _trayMenu;
        static ToolStripMenuItem _pauseMenuItem;
        
        static Icon _workIcon;
        static Icon _restIcon;

        static readonly object _stateLock = new object();

        [STAThread]
        static void Main(string[] args)
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            LoadConfig();

            _trayMenu = new ContextMenuStrip();
            _pauseMenuItem = new ToolStripMenuItem("Pause", null, OnPauseClicked);
            _trayMenu.Items.Add(_pauseMenuItem);
            _trayMenu.Items.Add("Skip", null, OnSkipClicked);
            _trayMenu.Items.Add("-");
            _trayMenu.Items.Add("Exit", null, OnExitClicked);

            _workIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            try
            {
                using var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("ReminderApp.rest.ico");
                if (stream != null) _restIcon = new Icon(stream);
                else _restIcon = _workIcon;
            }
            catch { _restIcon = _workIcon; }

            _trayIcon = new NotifyIcon()
            {
                Text = "Reminder",
                Icon = _workIcon,
                ContextMenuStrip = _trayMenu,
                Visible = true
            };

            ToastNotificationManagerCompat.OnActivated += toastArgs =>
            {
                var argsStr = toastArgs.Argument;
                if (argsStr.Contains("action=rest"))
                {
                    lock (_stateLock) { StartRestPhase(); }
                }
                else if (argsStr.Contains("action=ignore"))
                {
                    lock (_stateLock) { StartOvertimePhase(); }
                }
            };

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 1000;
            _timer.Tick += TimerTick;
            _timer.Start();

            lock (_stateLock) { StartWorkPhase(); }

            Application.Run();
        }

        static void TimerTick(object sender, EventArgs e)
        {
            lock (_stateLock)
            {
                UpdateTrayText();

                if (!_isPaused && DateTime.Now >= _phaseEndTime)
                {
                    if (_isResting)
                    {
                        StartWorkPhase();
                    }
                    else
                    {
                        ShowRestReminder();
                        StartOvertimePhase();
                    }
                }
            }
        }

        static void OnPauseClicked(object sender, EventArgs e)
        {
            lock (_stateLock)
            {
                if (_isPaused)
                {
                    _isPaused = false;
                    _pauseMenuItem.Text = "Pause";
                    _phaseEndTime = DateTime.Now + _remainingTime;
                }
                else
                {
                    _isPaused = true;
                    _pauseMenuItem.Text = "Resume";
                    _remainingTime = _phaseEndTime - DateTime.Now;
                    if (_remainingTime.TotalMilliseconds < 0) _remainingTime = TimeSpan.Zero;
                }
                UpdateTrayText();
            }
        }

        static void OnSkipClicked(object sender, EventArgs e)
        {
            lock (_stateLock)
            {
                _isPaused = false;
                _pauseMenuItem.Text = "Pause";
                
                if (_isResting)
                {
                    StartWorkPhase();
                }
                else
                {
                    StartRestPhase();
                }
            }
        }

        static void OnExitClicked(object sender, EventArgs e)
        {
            _trayIcon.Visible = false;
            Environment.Exit(0);
        }

        static void LoadConfig()
        {
            try
            {
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string configPath = Path.Combine(exeDir, "config.txt");
                if (File.Exists(configPath))
                {
                    var lines = File.ReadAllLines(configPath);
                    foreach (var line in lines)
                    {
                        var parts = line.Split('=');
                        if (parts.Length == 2)
                        {
                            var key = parts[0].Trim().ToLower();
                            var val = parts[1].Trim();
                            if (int.TryParse(val, out int mins))
                            {
                                if (key == "work") WorkMinutes = mins;
                                else if (key == "rest") RestMinutes = mins;
                                else if (key == "overtime") OvertimeMinutes = mins;
                            }
                        }
                    }
                }
                else
                {
                    File.WriteAllText(configPath, $"work={WorkMinutes}\nrest={RestMinutes}\novertime={OvertimeMinutes}");
                }
            }
            catch { }
        }

        static void StartWorkPhase()
        {
            _isResting = false;
            ClearToasts();
            SetTimer(WorkMinutes);
        }

        static void StartRestPhase()
        {
            _isResting = true;
            ClearToasts();
            SetTimer(RestMinutes);
        }

        static void StartOvertimePhase()
        {
            _isResting = false;
            SetTimer(OvertimeMinutes);
        }

        static void SetTimer(int minutes)
        {
            _phaseEndTime = DateTime.Now.AddMinutes(minutes);
            UpdateTrayText();
        }

        static void UpdateTrayText()
        {
            if (_trayIcon == null) return;
            
            string phaseName = _isResting ? "Rest" : "Work";
            string status = _isPaused ? "[Paused] " : "";
            
            TimeSpan t = _isPaused ? _remainingTime : (_phaseEndTime - DateTime.Now);
            if (t.TotalSeconds < 0) t = TimeSpan.Zero;
            
            string timeStr = $"{(int)t.TotalMinutes}m {t.Seconds}s left";
            
            string text = $"{status}{phaseName} - {timeStr}";
            if (text.Length > 63) text = text.Substring(0, 63);
            _trayIcon.Text = text;
            _trayIcon.Icon = _isResting ? _restIcon : _workIcon;
        }

        static void ShowRestReminder()
        {
            new ToastContentBuilder()
                .AddText("Time to Rest!")
                .AddText($"Take a {RestMinutes} min break.")
                .AddButton(new ToastButton()
                    .SetContent("Rest")
                    .AddArgument("action", "rest")
                    .SetBackgroundActivation())
                .AddButton(new ToastButton()
                    .SetContent("Ignore")
                    .AddArgument("action", "ignore")
                    .SetBackgroundActivation())
                .Show();
        }

        static void ClearToasts()
        {
            try
            {
                ToastNotificationManagerCompat.History.Clear();
            }
            catch { }
        }
    }
}
