using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace LocalForge
{
    public partial class Form1 : Form
    {
        enum ServiceStatus
        {
            Starting,
            Running,
            Failed
        }

        private Process apacheProcess;
        private Process mysqlProcess;
        private Process mailpitProcess;

        Color GetStatusColor(ServiceStatus status)
        {
            return status switch
            {
                ServiceStatus.Running => Color.Green,
                ServiceStatus.Starting => Color.Gold,
                ServiceStatus.Failed => Color.Red,
                _ => Color.Gray
            };
        }
        private ServiceStatus StartServiceWithStatus(
            string name,
            string exePath,
            string arguments,
            string workingDir,
            int port = 0)
        {
            try
            {
                var process = StartService(name, exePath, arguments, workingDir);

                switch (name.ToLower())
                {
                    case "apache": apacheProcess = process; break;
                    case "mysql": mysqlProcess = process; break;
                    case "mailpit": mailpitProcess = process; break;
                }

                if (port > 0 && !WaitForPort(port, 5000))
                    return ServiceStatus.Failed;

                return ServiceStatus.Running;
            }
            catch
            {
                return ServiceStatus.Failed;
            }
        }
        void SetControl(Action action)
        {
            if (InvokeRequired)
                Invoke(action);
            else
                action();
        }

        void UpdateStatusLabel(ToolStripStatusLabel label, string text, ServiceStatus status)
        {
            if (label.GetCurrentParent().InvokeRequired)
            {
                label.GetCurrentParent().Invoke(new Action(() =>
                {
                    label.Text = text;
                    label.BackColor = GetStatusColor(status);
                }));
            }
            else
            {
                label.Text = text;
                label.BackColor = GetStatusColor(status);
            }
        }
        private static Process StartService(string name, string exePath, string arguments, string workingDir)
        {
            string logsDir = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(logsDir);

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var process = new Process { StartInfo = psi };
            process.Start();

            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    File.AppendAllText(Path.Combine(logsDir, $"{name}.log"), e.Data + Environment.NewLine);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    File.AppendAllText(Path.Combine(logsDir, $"{name}.log"), "[ERR] " + e.Data + Environment.NewLine);
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return process;
        }

        static string ResolveServiceRoot(string service)
        {
            string root = Path.Combine(AppContext.BaseDirectory, "bin", service);
            if (!Directory.Exists(root))
                throw new DirectoryNotFoundException(root);

            string activeFile = Path.Combine(root, ".active");
            if (File.Exists(activeFile))
            {
                string pinned = File.ReadAllText(activeFile).Trim();
                string pinnedPath = Path.Combine(root, pinned);
                if (!Directory.Exists(pinnedPath))
                    throw new DirectoryNotFoundException(pinnedPath);
                return pinnedPath;
            }

            return Directory.GetDirectories(root).OrderByDescending(d => d).First();
        }

        static string GetApacheExe() => Path.Combine(ResolveServiceRoot("apache"), "bin", "httpd.exe");
        static string GetMySqlExe() => Path.Combine(ResolveServiceRoot("mysql"), "bin", "mysqld.exe");
        static string GetMailpitExe() => Path.Combine(ResolveServiceRoot("mailpit"), "mailpit.exe");

        static bool WaitForPort(int port, int timeoutMs = 5000)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try
                {
                    using var client = new TcpClient("127.0.0.1", port);
                    return true;
                }
                catch { Thread.Sleep(200); }
            }
            return false;
        }

        void EnsureHost(string domain)
        {
            string hostsPath = @"C:\Windows\System32\drivers\etc\hosts";
            string entry = $"127.0.0.1 {domain}";

            try
            {
                string content = File.ReadAllText(hostsPath);
                if (!content.Contains(entry))
                {
                    File.AppendAllText(hostsPath, entry + Environment.NewLine);
                    Console.WriteLine($"Added hosts entry: {entry}");
                }
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show($"Please run as administrator to add hosts entry for {domain}", "Permission Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        void SyncHostsWithProjects()
        {
            string hostsPath = @"C:\Windows\System32\drivers\etc\hosts";
            string wwwRoot = Path.Combine(AppContext.BaseDirectory, "www");

            if (!Directory.Exists(wwwRoot))
                return;

            const string START = "# LocalForge START";
            const string END = "# LocalForge END";

            var domains = Directory.GetDirectories(wwwRoot)
                .Select(d => Path.GetFileName(d) + ".test")
                .OrderBy(d => d)
                .ToList();

            try
            {
                var lines = File.Exists(hostsPath)
                    ? File.ReadAllLines(hostsPath).ToList()
                    : new List<string>();

                int startIndex = lines.FindIndex(l => l.Trim() == START);
                int endIndex = lines.FindIndex(l => l.Trim() == END);

                if (startIndex != -1 && endIndex != -1 && endIndex > startIndex)
                {
                    lines.RemoveRange(startIndex, endIndex - startIndex + 1);
                }

                lines.Add(START);

                foreach (var domain in domains)
                {
                    lines.Add($"127.0.0.1 {domain}");
                }

                lines.Add(END);

                File.WriteAllLines(hostsPath, lines);
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show(
                    "Please run LocalForge as Administrator to manage hosts entries.",
                    "Administrator Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
            }
        }

        void FetchProjects()
        {
            SyncHostsWithProjects();
        }
        void GenerateApacheConfig()
        {
            string baseDir = AppContext.BaseDirectory.Replace("\\", "/").TrimEnd('/');
            string apacheRoot = ResolveServiceRoot("apache").Replace("\\", "/").TrimEnd('/');
            string phpRoot = ResolveServiceRoot("php").Replace("\\", "/").TrimEnd('/');
            string wwwRoot = Path.Combine(baseDir, "www").Replace("\\", "/").TrimEnd('/');

            string templatePath = Path.Combine(baseDir, "config", "apache", "httpd.conf.template");
            string outputPath = Path.Combine(baseDir, "config", "apache", "httpd.conf");

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            string template = File.ReadAllText(templatePath);

            string config = template
                .Replace("{APACHE_ROOT}", apacheRoot)
                .Replace("{PHP_ROOT}", phpRoot)
                .Replace("{WWW_ROOT}", wwwRoot)
                .Replace("{LOCALFORGE_ROOT}", baseDir);

            string tmp = outputPath + ".tmp";
            File.WriteAllText(tmp, config);
            File.Move(tmp, outputPath, true);
        }

        void GenerateMySQLConfig()
        {
            string baseDir = AppContext.BaseDirectory;
            string configDir = Path.Combine(baseDir, "config", "mysql");
            Directory.CreateDirectory(configDir);

            string myIniPath = Path.Combine(configDir, "my.ini");
            string mysqlRoot = ResolveServiceRoot("mysql").Replace("\\", "/").TrimEnd('/');
            string dataDir = Path.Combine(baseDir, "data").Replace("\\", "/").TrimEnd('/');
            Directory.CreateDirectory(dataDir);

            if (!File.Exists(myIniPath))
            {
                string myIniContent = $@"[mysqld]
basedir=""{mysqlRoot}""
datadir=""{dataDir}""
port=3306
sql_mode=STRICT_TRANS_TABLES
default_authentication_plugin=mysql_native_password
";

                File.WriteAllText(myIniPath, myIniContent);
            }

            if (!Directory.EnumerateFileSystemEntries(dataDir).Any())
            {
                string mysqldExe = GetMySqlExe();
                var psi = new ProcessStartInfo
                {
                    FileName = mysqldExe,
                    Arguments = $"--initialize-insecure --basedir=\"{mysqlRoot}\" --datadir=\"{dataDir}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                proc.WaitForExit();

                if (proc.ExitCode != 0)
                {
                    string output = proc.StandardOutput.ReadToEnd() + proc.StandardError.ReadToEnd();
                    throw new Exception("Failed to initialize MySQL data directory:\n" + output);
                }
            }
        }

        void EnsurePhpIni()
        {
            string phpRoot = ResolveServiceRoot("php");
            string phpIni = Path.Combine(phpRoot, "php.ini");
            string templateIni = Path.Combine(phpRoot, "php.ini-development");

            if (!File.Exists(templateIni))
                throw new FileNotFoundException("php.ini-development not found.", templateIni);

            File.Copy(templateIni, phpIni, true);

            var lines = File.ReadAllLines(phpIni).ToList();

            for (int i = 0; i < lines.Count; i++)
            {
                string trimmed = lines[i].Trim();

                if (trimmed.StartsWith(";extension_dir") && trimmed.Contains("\"ext\""))
                {
                    lines[i] = lines[i].TrimStart(';', ' ');
                    break;
                }
            }

            string[] extensions = {
                "mysqli",
                "pdo_mysql",
                "bz2",
                "curl",
                "fileinfo",
                "gd",
                "gettext",
                "imap",
                "mbstring",
                "openssl",
                "zip"
            };

            foreach (var ext in extensions)
            {
                var matches = new List<int>();

                for (int i = 0; i < lines.Count; i++)
                {
                    if (lines[i].Trim().StartsWith($";extension={ext}"))
                        matches.Add(i);
                }

                if (matches.Count == 1)
                {
                    lines[matches[0]] = lines[matches[0]].TrimStart(';', ' ');
                }
                else if (matches.Count > 1)
                {
                    lines[matches[1]] = lines[matches[1]].TrimStart(';', ' ');
                }
            }

            File.WriteAllLines(phpIni, lines);
        }


        void StartApache()
        {
            UpdateStatusLabel(toolStripStatusLabel1, "Apache: Starting...", ServiceStatus.Starting);
            EnsurePhpIni();
            GenerateApacheConfig();
            FetchProjects();

            var status = StartServiceWithStatus(
                "apache",
                GetApacheExe(),
                $"-f \"{Path.Combine(AppContext.BaseDirectory, "config", "apache", "httpd.conf")}\"",
                Path.GetDirectoryName(GetApacheExe())!,
                80
            );

            UpdateStatusLabel(toolStripStatusLabel1, $"Apache: {status}", status);
            if (status == ServiceStatus.Running)
            {
                SetControl(() =>
                {
                    linkLabel1.Visible = true;
                    checkBox1.Checked = true;
                });
            }
        }

        void StartMySQL()
        {
            GenerateMySQLConfig();

            string baseDir = AppContext.BaseDirectory;
            string myIniPath = Path.Combine(baseDir, "config", "mysql", "my.ini");
            string mysqlRoot = ResolveServiceRoot("mysql");
            string dataDir = Path.Combine(baseDir, "data");

            UpdateStatusLabel(toolStripStatusLabel2, "MySQL: Starting...", ServiceStatus.Starting);

            var status = StartServiceWithStatus(
                "mysql",
                GetMySqlExe(),
                $"--defaults-file=\"{myIniPath}\" " +
                $"--basedir=\"{mysqlRoot}\" " +
                $"--datadir=\"{dataDir}\" " +
                $"--console",
                dataDir,
                3306
            );

            UpdateStatusLabel(toolStripStatusLabel2, $"MySQL: {status}", status);

            if (status == ServiceStatus.Running)
            {
                SetControl(() =>
                {
                    linkLabel2.Visible = true;
                    checkBox2.Checked = true;
                });
            }
        }

        void StartMailpit()
        {
            UpdateStatusLabel(toolStripStatusLabel3, "Mailpit: Starting...", ServiceStatus.Starting);

            var status = StartServiceWithStatus(
                "mailpit",
                GetMailpitExe(),
                "-s 127.0.0.1:1025 -l 127.0.0.1:8025",
                ResolveServiceRoot("mailpit"),
                8025
            );

            UpdateStatusLabel(toolStripStatusLabel3, $"Mailpit: {status}", status);
            if (status == ServiceStatus.Running)
            {
                SetControl(() =>
                {
                    linkLabel3.Visible = true;
                    checkBox3.Checked = true;
                });
            }
        }

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            GenerateApacheConfig();
        }

        async void StopServices()
        {
            if (apacheProcess != null && !apacheProcess.HasExited)
                apacheProcess.Kill();


            if (mysqlProcess != null && !mysqlProcess.HasExited)
                mysqlProcess.Kill();

            if (mailpitProcess != null && !mailpitProcess.HasExited)
                mailpitProcess.Kill();
            var tasks = new List<Task>();
            tasks.Add(Task.Run(() =>
            {
                foreach (var proc in Process.GetProcessesByName("httpd"))
                {
                    try
                    {
                        proc.Kill();
                        proc.WaitForExit();
                    }
                    catch { }
                }
            }));
            tasks.Add(Task.Run(() =>
            {
                foreach (var proc in Process.GetProcessesByName("mysqld"))
                {
                    try
                    {
                        proc.Kill();
                        proc.WaitForExit();
                    }
                    catch { }
                }
            }));
            tasks.Add(Task.Run(() =>
            {
                foreach (var proc in Process.GetProcessesByName("mailpit"))
                {
                    try
                    {
                        proc.Kill();
                        proc.WaitForExit();
                    }
                    catch { }
                }
            }));
            await Task.WhenAll(tasks);

            SetControl(() =>
            {
                checkBox1.Checked = false;
                linkLabel1.Visible = false;
                UpdateStatusLabel(toolStripStatusLabel1, "Apache: Stopped", ServiceStatus.Failed);

                checkBox2.Checked = false;
                linkLabel2.Visible = false;
                UpdateStatusLabel(toolStripStatusLabel2, "MySQL: Stopped", ServiceStatus.Failed);

                checkBox3.Checked = false;
                linkLabel3.Visible = false;
                UpdateStatusLabel(toolStripStatusLabel3, "Mailpit: Stopped", ServiceStatus.Failed);
            });
        }

        private bool servicesRunning = false;

        private async void button1_Click(object sender, EventArgs e)
        {
            button1.Enabled = false;

            if (!servicesRunning)
            {
                button1.Text = "Starting...";
                var tasks = new List<Task>();
                tasks.Add(Task.Run(() => StartApache()));
                tasks.Add(Task.Run(() => StartMySQL()));
                tasks.Add(Task.Run(() => StartMailpit()));

                await Task.WhenAll(tasks);

                servicesRunning = true;
                checkBox1.Enabled = false;
                button1.Text = "Stop";
            }
            else
            {
                button1.Text = "Stopping...";
                StopServices();
                servicesRunning = false;
                checkBox1.Enabled = true;
                button1.Text = "Start";
            }

            button1.Enabled = true;
        }


        private async void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            button1.Enabled = false;
            await Task.Run(() =>
            {
                foreach (var proc in Process.GetProcessesByName("httpd"))
                {
                    try
                    {
                        proc.Kill();
                        proc.WaitForExit();
                    }
                    catch { }
                }
            });

            await Task.Run(() => StartApache());

            button1.Enabled = true;
        }

        private async void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            button1.Enabled = false;
            await Task.Run(() =>
            {
                foreach (var proc in Process.GetProcessesByName("mysqld"))
                {
                    try
                    {
                        proc.Kill();
                        proc.WaitForExit();
                    }
                    catch { }
                }
            });

            await Task.Run(() => StartMySQL());

            button1.Enabled = true;
        }

        private async void linkLabel3_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            button1.Enabled = false;
            await Task.Run(() =>
            {
                foreach (var proc in Process.GetProcessesByName("mailpit"))
                {
                    try
                    {
                        proc.Kill();
                        proc.WaitForExit();
                    }
                    catch { }
                }
            });

            await Task.Run(() => StartMailpit());

            button1.Enabled = true;
        }

        private async void ToggleService(CheckBox checkBox, LinkLabel linkLabel, ToolStripStatusLabel statusLabel,
    Func<Task> startService, Process serviceProcess, string serviceName)
        {
            button1.Enabled = false;

            if (checkBox.Checked)
            {
                SetControl(() => UpdateStatusLabel(statusLabel, $"{serviceName}: Starting...", ServiceStatus.Starting));

                await startService();

                SetControl(() =>
                {
                    linkLabel.Visible = true;
                    UpdateStatusLabel(statusLabel, $"{serviceName}: Running", ServiceStatus.Running);
                });
            }
            else
            {
                switch (serviceName)
                {
                    case "Apache":
                        await Task.Run(() =>
                    {
                        foreach (var proc in Process.GetProcessesByName("httpd"))
                        {
                            try
                            {
                                proc.Kill();
                                proc.WaitForExit();
                            }
                            catch { }
                        }
                    }); break;
                    case "MySQL":
                        await Task.Run(() =>
                        {
                            foreach (var proc in Process.GetProcessesByName("mysqld"))
                            {
                                try
                                {
                                    proc.Kill();
                                    proc.WaitForExit();
                                }
                                catch { }
                            }
                        }); break;
                    case "Mailpit":
                        await Task.Run(() =>
                        {
                            foreach (var proc in Process.GetProcessesByName("mailpit"))
                            {
                                try
                                {
                                    proc.Kill();
                                    proc.WaitForExit();
                                }
                                catch { }
                            }
                        }); break;

                }

                SetControl(() =>
                {
                    linkLabel.Visible = false;
                    UpdateStatusLabel(statusLabel, $"{serviceName}: Stopped", ServiceStatus.Failed);
                });
            }

            SetControl(() => button1.Enabled = true);
        }


        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            ToggleService(checkBox1, linkLabel1, toolStripStatusLabel1,
                async () => await Task.Run(() => StartApache()), apacheProcess, "Apache");
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            ToggleService(checkBox2, linkLabel2, toolStripStatusLabel2,
                async () => await Task.Run(() => StartMySQL()), mysqlProcess, "MySQL");
        }

        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            ToggleService(checkBox3, linkLabel3, toolStripStatusLabel3,
                async () => await Task.Run(() => StartMailpit()), mailpitProcess, "Mailpit");
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if(servicesRunning)
            {
                button1.Text = "Stopping...";
                StopServices();
                servicesRunning = false;
            }
        }
    }
}
