using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.Remoting.Messaging;
using System.Windows.Forms;

namespace PowerCopyWinform
{
    public partial class FormMain : Form
    {
        public FormMain()
        {
            InitializeComponent();
        }

        private void executeButton_Click(object sender, EventArgs e)
        {
            var src = srcTextBox.Text;
            src = src.Trim();
            var dst = dstTextBox.Text;
            dst = dst.Trim();

            ClearLog();
            startButton.Enabled = false;
            actionRadioButtonPanel.Enabled = false;
            cancelButton.Enabled = true;
            timer1.Start();

            RoboAction? roboAction = null;
            if (copyRadioButton.Checked)
            {
                roboAction = RoboAction.Copy;
                statusLabel.Text = "复制中...";
            }
            else if (moveRadioButton.Checked)
            {
                roboAction = RoboAction.Move;
                statusLabel.Text = "移动中...";
            }

            if (roboAction == null)
            {
                throw new InvalidOperationException(nameof(roboAction));
            }

            backgroundWorker1.RunWorkerAsync(new RoboActionParams()
            {
                SourcePath = src,
                TargetPath = dst,
                RoboAction = roboAction.Value,
            });
        }

        private void ClearLog()
        {
            if (logRichTextBox.InvokeRequired)
            {
                logRichTextBox.Invoke(new MethodInvoker(ClearLog));
            }
            else
            {
                logRichTextBox.Text = "";
                logRichTextBox.ScrollToCaret();
            }
        }

        private void AppendLog(string txt)
        {
            if (string.IsNullOrEmpty(txt))
            {
                return;
            }

            if (logRichTextBox.InvokeRequired)
            {
                logRichTextBox.Invoke(new MethodInvoker(() => AppendLog(txt)));
            }
            else
            {
                logRichTextBox.AppendText(txt);
                if (!txt.EndsWith("\n"))
                {
                    logRichTextBox.AppendText("\r\n");
                }

                logRichTextBox.ScrollToCaret();
            }
        }

        private void srcBrowseButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.CheckFileExists = true;
                dialog.Title = "选择文件";
                dialog.Filter = "所有文件 (*.*)|*.*";
                dialog.InitialDirectory = string.IsNullOrEmpty(srcTextBox.Text)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.MyComputer)
                    : Path.GetDirectoryName(srcTextBox.Text.Trim());
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    srcTextBox.Text = dialog.FileName;
                }
            }
        }

        private void dstBrowseButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "选择文件";
                dialog.Filter = "所有文件 (*.*)|*.*";
                dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer);
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    dstTextBox.Text = dialog.FileName;
                }
            }
        }

        private static string TrimSlash(string p)
        {
            return p.TrimEnd('\\');
        }

        private void BackgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            var actionParams = (RoboActionParams)e.Argument;

            // https://learn.microsoft.com/zh-cn/windows-server/administration/windows-commands/robocopy
            // MT: multithread
            // E:  Copies subdirectories and includes empty directories
            // Z:  Copies files in restartable mode
            // IS: Includes the same files. Same files are identical in name, size, times, and all attributes.
            var flags = " /MT /E /Z";

            switch (actionParams.RoboAction)
            {
                case RoboAction.Copy:
                    break;
                case RoboAction.Move:
                    flags += " /MOVE";
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            string arguments;
            if (Directory.Exists(actionParams.SourcePath))
            {
                var src = actionParams.SourcePath;
                var dst = actionParams.TargetPath;
                if (dst.EndsWith("\\"))
                {
                    dst += Path.GetFileName(src);
                }

                dst = TrimSlash(dst);
                arguments = $"\"{src}\" \"{dst}\"" + flags;
            }
            else if (File.Exists(actionParams.SourcePath))
            {
                var src = TrimSlash(Path.GetDirectoryName(actionParams.SourcePath));
                var dst = TrimSlash(actionParams.TargetPath);
                var fileName = Path.GetFileName(actionParams.SourcePath);
                arguments = $"\"{src}\" \"{dst}\" \"{fileName}\" /J" + flags;
            }
            else
            {
                AppendLog("Source not exists");
                e.Result = -1;
                return;
            }

            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "robocopy.exe",
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

                while (!process.HasExited)
                {
                    if (backgroundWorker1.CancellationPending)
                    {
                        process.Kill();
                        e.Cancel = true;
                        return;
                    }

                    System.Threading.Thread.Sleep(100);
                }

                e.Result = process.ExitCode;
            }
        }

        private void ProcessOnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data))
            {
                return;
            }

            if (backgroundWorker1.IsBusy)
            {
                backgroundWorker1.ReportProgress(0, e.Data);
            }
        }

        private void BecomeReady()
        {
            statusLabel.Text = "就绪";
            startButton.Enabled = true;
            actionRadioButtonPanel.Enabled = true;
            progressBar.Value = progressBar.Maximum;
            toolStripStatusLabelProgress.Text = "100%";
            timer1.Stop();
            elapsed = DateTime.MinValue;
            toolStripStatusLabelElapsed.Text = "";
            cancelButton.Enabled = false;
        }

        private void BackgroundWorker1_OnRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            BecomeReady();
        }

        private int FindPercent(string text)
        {
            var i = text.IndexOf('%');
            if (i < 0)
            {
                return -1;
            }

            var s = text.Substring(0, i).Trim();
            if (!int.TryParse(s, out var p))
            {
                return -1;
            }

            return p;
        }

        private void BackgroundWorker1_OnProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            var message = (string)e.UserState;
            var p = FindPercent(message);
            if (p >= 0 && p <= 100)
            {
                progressBar.Value = p;
                toolStripStatusLabelProgress.Text = $"{p}%";
            }
            else
            {
                AppendLog(message);
            }
        }

        private void srcFolderBrowseButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    srcTextBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void dstFolderBrowseButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    dstTextBox.Text = dialog.SelectedPath;
                }
            }
        }

        public void Cancel()
        {
            if (backgroundWorker1.IsBusy)
            {
                backgroundWorker1.CancelAsync();
            }

            AppendLog("用户取消了操作");
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            Cancel();
        }

        private DateTime elapsed;

        private void timer1_Tick(object sender, EventArgs e)
        {
            elapsed += TimeSpan.FromMilliseconds(timer1.Interval);
            toolStripStatusLabelElapsed.Text = elapsed.ToString("HH:mm:ss");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            var path = dstTextBox.Text.Trim();
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            var baseDir = path;
            if (File.Exists(path))
            {
                baseDir = Path.GetDirectoryName(path);
            }
            if (Directory.Exists(baseDir))
            {
                Process.Start("explorer.exe", baseDir);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var path = srcTextBox.Text.Trim();
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            var baseDir = path;
            if (File.Exists(path))
            {
                baseDir = Path.GetDirectoryName(path);
            }
            if (Directory.Exists(baseDir))
            {
                Process.Start("explorer.exe", baseDir);
            }
        }
    }
}