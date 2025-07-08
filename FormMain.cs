using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace PowerCopy32 {
    public partial class FormMain : Form {
        private DateTime _elapsed;
        private readonly string _roboCopyPath;
        private bool IsSystemRoboCopy => _roboCopyPath.ToLowerInvariant().Contains("\\system32\\");

        public FormMain() {
            InitializeComponent();
            const string roboCopyPath = "robocopy.exe";
            var ok = Win32.FindInPath(roboCopyPath, out _roboCopyPath);
            if (!ok) {
                _roboCopyPath = roboCopyPath;
            }
            AppendLog(IsSystemRoboCopy ? "Using system robocopy" : "Using internal robocopy");
        }

        public FormMain(string action, string sourcePath) : this() {
            switch (action) {
                case "cp":
                    copyRadioButton.Checked = true;
                    break;
                case "mv":
                    moveRadioButton.Checked = true;
                    break;
            }

            sourcePath = sourcePath.Trim();
            if (!string.IsNullOrEmpty(sourcePath)) {
                srcTextBox.Text = sourcePath;
            }
        }

        private void Cancel() {
            if (backgroundWorker1.IsBusy) {
                backgroundWorker1.CancelAsync();
            }

            AppendLog("用户取消了操作");
        }

        private void executeButton_Click(object sender, EventArgs e) {
            var src = srcTextBox.Text;
            src = src.Trim();
            var dst = dstTextBox.Text;
            dst = dst.Trim();

            if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(dst)) {
                ClearLog();
                AppendLog("请输入源路径或目的路径");
                return;
            }

            if (!File.Exists(src) && !Directory.Exists(src)) {
                ClearLog();
                AppendLog("源路径不存在");
                return;
            }

            ClearLog();
            startButton.Enabled = false;
            actionRadioButtonPanel.Enabled = false;
            cancelButton.Enabled = true;
            timer1.Start();

            RoboAction? roboAction = null;
            if (copyRadioButton.Checked) {
                roboAction = RoboAction.Copy;
                statusLabel.Text = "复制中...";
            } else if (moveRadioButton.Checked) {
                roboAction = RoboAction.Move;
                statusLabel.Text = "移动中...";
            }

            if (roboAction == null) {
                return;
            }

            backgroundWorker1.RunWorkerAsync(new RoboActionParams {
                SourcePath = src,
                TargetPath = dst,
                RoboAction = roboAction.Value
            });
        }

        private void ClearLog() {
            if (logRichTextBox.InvokeRequired) {
                logRichTextBox.Invoke(new MethodInvoker(ClearLog));
            } else {
                logRichTextBox.Text = "";
                logRichTextBox.ScrollToCaret();
            }
        }

        private void AppendLog(string txt) {
            if (string.IsNullOrEmpty(txt)) {
                return;
            }

            if (logRichTextBox.InvokeRequired) {
                logRichTextBox.Invoke(new MethodInvoker(() => AppendLog(txt)));
            } else {
                logRichTextBox.AppendText(txt);
                if (!txt.EndsWith("\n")) {
                    logRichTextBox.AppendText("\r\n");
                }

                logRichTextBox.ScrollToCaret();
            }
        }

        private void srcBrowseButton_Click(object sender, EventArgs e) {
            using (var dialog = new OpenFileDialog()) {
                dialog.CheckFileExists = true;
                dialog.Title = "选择文件";
                dialog.Filter = "所有文件 (*.*)|*.*";
                dialog.InitialDirectory = string.IsNullOrEmpty(srcTextBox.Text)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.MyComputer)
                    : Path.GetDirectoryName(srcTextBox.Text.Trim());
                if (dialog.ShowDialog() == DialogResult.OK) {
                    srcTextBox.Text = dialog.FileName;
                }
            }
        }

        private void dstBrowseButton_Click(object sender, EventArgs e) {
            using (var dialog = new OpenFileDialog()) {
                dialog.Title = "选择文件";
                dialog.Filter = "所有文件 (*.*)|*.*";
                dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer);
                if (dialog.ShowDialog() == DialogResult.OK) {
                    dstTextBox.Text = dialog.FileName;
                }
            }
        }

        private static string TrimSlash(string p) {
            return p.TrimEnd('\\');
        }

        private void BackgroundWorker1_DoWork(object sender, DoWorkEventArgs e) {
            var actionParams = (RoboActionParams)e.Argument;

            // https://learn.microsoft.com/zh-cn/windows-server/administration/windows-commands/robocopy
            // WinXP robocopy.exe don't support /J /MT
            // MT: multithread
            // E:  Copies subdirectories and includes empty directories
            // Z:  Copies files in restartable mode
            // IS: Includes the same files. Same files are identical in name, size, times, and all attributes.
            // J:  Copies using unbuffered I/O (recommended for large files)
            var flags = " /E /Z";
            if (IsSystemRoboCopy) {
                flags += " /MT";
            }

            switch (actionParams.RoboAction) {
                case RoboAction.Copy:
                    break;
                case RoboAction.Move:
                    flags += " /MOVE";
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            string arguments;
            if (Directory.Exists(actionParams.SourcePath)) {
                var src = actionParams.SourcePath;
                var dst = actionParams.TargetPath;
                if (dst.EndsWith("\\")) {
                    dst += Path.GetFileName(src);
                }
                dst = TrimSlash(dst);

                if (IsSystemRoboCopy) {
                    arguments = $"\"{src}\" \"{dst}\"" + flags;
                } else {
                    src = Win32.GetShortPathName(src);
                    dst = Win32.GetShortPathName(dst);
                    arguments = $"{src} {dst}" + flags;
                }

            } else if (File.Exists(actionParams.SourcePath)) {
                var src = TrimSlash(Path.GetDirectoryName(actionParams.SourcePath));
                var dst = TrimSlash(actionParams.TargetPath);
                var fileName = Path.GetFileName(actionParams.SourcePath);

                if (IsSystemRoboCopy) {
                    arguments = $"\"{src}\" \"{dst}\" \"{fileName}\" /J /LEV:1" + flags;
                } else {
                    src = Win32.GetShortPathName(src);
                    dst = Win32.GetShortPathName(dst);
                    arguments = $"{src} {dst} {fileName} /LEV:1" + flags;
                }
            } else {
                AppendLog("源路径不存在");
                e.Result = -1;
                return;
            }

            using (var process = new Process()) {
                process.StartInfo = new ProcessStartInfo {
                    FileName = _roboCopyPath,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                process.OutputDataReceived += ProcessOnOutputDataReceived;

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                while (!process.HasExited) {
                    if (backgroundWorker1.CancellationPending) {
                        process.Kill();
                        e.Cancel = true;
                        return;
                    }

                    Thread.Sleep(100);
                }

                e.Result = process.ExitCode;
            }
        }

        private void ProcessOnOutputDataReceived(object sender, DataReceivedEventArgs e) {
            if (string.IsNullOrEmpty(e.Data)) {
                return;
            }

            if (backgroundWorker1.IsBusy) {
                backgroundWorker1.ReportProgress(0, e.Data);
            }
        }

        private void BecomeReady() {
            statusLabel.Text = "就绪";
            startButton.Enabled = true;
            actionRadioButtonPanel.Enabled = true;
            progressBar.Value = progressBar.Maximum;
            toolStripStatusLabelProgress.Text = "100%";
            timer1.Stop();
            _elapsed = DateTime.MinValue;
            toolStripStatusLabelElapsed.Text = "";
            cancelButton.Enabled = false;
        }

        private void BackgroundWorker1_OnRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            BecomeReady();
        }

        private float FindPercent(string text) {
            var i = text.IndexOf('%');
            if (i < 0) {
                return -1;
            }

            var s = text.Substring(0, i).Trim();
            if (!float.TryParse(s, out var p)) {
                return -1;
            }

            return p;
        }

        private void BackgroundWorker1_OnProgressChanged(object sender, ProgressChangedEventArgs e) {
            var message = (string)e.UserState;
            var p = FindPercent(message);
            if (p >= 0 && p <= 100) {
                progressBar.Value = (int)Math.Round(p);
                toolStripStatusLabelProgress.Text = $"{p}%";
            } else {
                AppendLog(message);
            }
        }

        private void srcFolderBrowseButton_Click(object sender, EventArgs e) {
            using (var dialog = new FolderBrowserDialog()) {
                if (dialog.ShowDialog() == DialogResult.OK) {
                    srcTextBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void dstFolderBrowseButton_Click(object sender, EventArgs e) {
            using (var dialog = new FolderBrowserDialog()) {
                if (dialog.ShowDialog() == DialogResult.OK) {
                    dstTextBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void cancelButton_Click(object sender, EventArgs e) {
            Cancel();
        }

        private void timer1_Tick(object sender, EventArgs e) {
            _elapsed += TimeSpan.FromMilliseconds(timer1.Interval);
            toolStripStatusLabelElapsed.Text = _elapsed.ToString("HH:mm:ss");
        }

        private void button2_Click(object sender, EventArgs e) {
            var path = dstTextBox.Text.Trim();
            if (string.IsNullOrEmpty(path)) {
                return;
            }

            var baseDir = path;
            if (File.Exists(path)) {
                baseDir = Path.GetDirectoryName(path);
            }

            if (Directory.Exists(baseDir)) {
                Process.Start("explorer.exe", baseDir);
            }
        }

        private void button1_Click(object sender, EventArgs e) {
            var path = srcTextBox.Text.Trim();
            if (string.IsNullOrEmpty(path)) {
                return;
            }

            var baseDir = path;
            if (File.Exists(path)) {
                baseDir = Path.GetDirectoryName(path);
            }

            if (Directory.Exists(baseDir)) {
                Process.Start("explorer.exe", baseDir);
            }
        }
    }
}
