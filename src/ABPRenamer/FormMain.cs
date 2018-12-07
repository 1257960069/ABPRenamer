using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ABPRenamer
{

    public partial class FormMain : Form
    {
        public FormMain()
        {
            InitializeComponent();
        }
        private void BtnStart_Click(object sender, EventArgs e)
        {
            if (btnStart.Text == "执行")
            {
                StartMethod();
            }
            else
            {
                StopMethod();
            }
        }
        private void StartMethod()
        {
            Arguments arguments = new Arguments
            {
                OldCompanyName = txtOldCompanyName.Text.Trim(),
                OldProjectName = txtOldProjectName.Text.Trim(),

                NewCompanyName = txtNewCompanyName.Text.Trim()
            };
            arguments.NewPeojectName = txtNewProjectName.Text.Trim();
            if (string.IsNullOrEmpty(arguments.NewPeojectName))
            {
                MessageBox.Show("请选择项目路径!", "提示", MessageBoxButtons.OK, MessageBoxIcon.Question);
                txtNewProjectName.Focus();
                return;
            }

            arguments.RootDir = txtRootDir.Text.Trim();
            if (string.IsNullOrWhiteSpace(arguments.RootDir))
            {
                if (DialogResult.Yes == MessageBox.Show("请选择项目路径!", "提示", MessageBoxButtons.OK, MessageBoxIcon.Question))
                {
                    BtnSelect_Click(null, null);
                }
                return;
            }
            if (!Directory.Exists(arguments.RootDir))
            {
                MessageBox.Show("请选择正确的项目路径!");
                return;
            }

            //显示进度条
            progressBar1.Visible = true;

            backgroundWorker1.RunWorkerAsync(arguments);
        }
        private void StopMethod()
        {
            if (backgroundWorker1.IsBusy)
            {
                MessageBox.Show("正在取消中");
                backgroundWorker1.CancelAsync();
            }
        }
        private void Log(string value)
        {
            if (Console.InvokeRequired)
            {
                Action<string> act = (text) =>
                {
                    Console.AppendText(text);
                };
                Console.Invoke(act, value);
            }
            else
            {
                Console.AppendText(value);
            }

        }

        #region worker的事件回调

        /// <summary>
        /// worker开始执行的回调方法
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BackgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker work = (BackgroundWorker)sender;
            Arguments arguments = e.Argument as Arguments;

            //备份 RootDir;递归的时候,RootDir进行了修改
            string backupRootDir = arguments.RootDir;


            Stopwatch sp = new Stopwatch();

            long spdir;

            sp.Start();

            RenameAllDir(work, e, arguments);
            sp.Stop();
            spdir = sp.ElapsedMilliseconds;

            Log($"================= 目录重命名完成 =================耗时{spdir}(s)\r\n");

            sp.Reset();
            sp.Start();

            //还原RootDir
            arguments.RootDir = backupRootDir;

            RenameAllFileNameAndContent(work, e, arguments);
            sp.Stop();
            Log($"================= 文件名和内容重命名完成 =================耗时{sp.ElapsedMilliseconds}(s)\r\n");

            Log($"================= 全部完成 =================目录耗时:{ spdir }s 文件耗时:{ sp.ElapsedMilliseconds}s\r\n");

        }
        /// <summary>
        /// worker传回报告的回调方法
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BackgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            //e.UserState 送回报告传过来的自定义参数   

            Log(e.UserState.ToString());

            //异步任务的百分比
            progressBar1.PerformStep();
        }
        /// <summary>
        /// worker执行完成的回调方法
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BackgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            //恢复开始按钮的状态
            btnStart.Text = "执行";

            if (e.Cancelled)
            {
                MessageBox.Show("任务被终止");
            }
            else if (e.Error != null)
            {
                MessageBox.Show("内部发生错误", e.Error.Message);
                throw e.Error;
            }
            else//你的业务逻辑
            {
                if (DialogResult.Yes == MessageBox.Show("处理完成,是否关闭本系统？", "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Information))
                {
                    BtnClose_Click(null, new MyEventArgs());
                }
            }

        }

        public class MyEventArgs : EventArgs
        {
            //此属性未使用,用的是类型判断
            public bool IsCompleted { get; set; } = true;
        }

        #endregion       

        #region 递归重命名所有目录

        /// <summary>
        /// 递归重命名所有目录
        /// </summary>
        private void RenameAllDir(BackgroundWorker worker, DoWorkEventArgs e, Arguments arguments)
        {
            string[] allDir = Directory.GetDirectories(arguments.RootDir);

            int i = 0;
            foreach (string currDir in allDir)
            {

                // 检查是否取消操作
                if (worker.CancellationPending)
                {
                    e.Cancel = true;
                    break;
                }
                else// 开始处理内容...
                {
                    arguments.RootDir = currDir;
                    RenameAllDir(worker, e, arguments);

                    DirectoryInfo dinfo = new DirectoryInfo(currDir);
                    if (dinfo.Name.Contains(arguments.OldCompanyName) || dinfo.Name.Contains(arguments.OldProjectName))
                    {
                        string newName = dinfo.Name;

                        if (!string.IsNullOrEmpty(arguments.OldCompanyName))
                        {
                            newName = newName.Replace(arguments.OldCompanyName, arguments.NewCompanyName);
                        }
                        newName = newName.Replace(arguments.OldProjectName, arguments.NewPeojectName);

                        string newPath = Path.Combine(dinfo.Parent.FullName, newName);

                        if (dinfo.FullName != newPath)
                        {
                            //发送报告  ,这里仅仅发送 进度的数值, 可以第二个参数继续发送相关信息
                            worker.ReportProgress((i), $"{dinfo.FullName}\r\n=>\r\n{newPath}\r\n\r\n");
                            dinfo.MoveTo(newPath);
                        }

                    }

                } //处理内容结 束

            }
        }

        #endregion

        #region 递归重命名所有文件名和文件内容

        /// <summary>
        /// 递归重命名所有文件名和文件内容
        /// </summary>
        private void RenameAllFileNameAndContent(BackgroundWorker worker, DoWorkEventArgs e, Arguments arguments)
        {
            //获取当前目录所有指定文件扩展名的文件
            List<FileInfo> files = new DirectoryInfo(arguments.RootDir).GetFiles().Where(m => arguments.filter.Contains(m.Extension)).ToList();

            int i = 0;
            //重命名当前目录文件和文件内容
            foreach (FileInfo item in files)
            {

                // 检查是否取消操作
                if (worker.CancellationPending)
                {
                    e.Cancel = true;
                    break;
                }
                else// 开始处理内容...
                {
                    string text = File.ReadAllText(item.FullName, Encoding.UTF8);
                    if (!string.IsNullOrEmpty(arguments.OldCompanyName))
                    {
                        text = text.Replace(arguments.OldCompanyName, arguments.NewCompanyName);
                    }

                    text = text.Replace(arguments.OldProjectName, arguments.NewPeojectName);

                    if (item.Name.Contains(arguments.OldCompanyName) || item.Name.Contains(arguments.OldProjectName))
                    {
                        string newName = item.Name;

                        if (!string.IsNullOrEmpty(arguments.OldCompanyName))
                        {
                            newName = newName.Replace(arguments.OldCompanyName, arguments.NewCompanyName);

                        }
                        newName = newName.Replace(arguments.OldProjectName, arguments.NewPeojectName);
                        string newFullName = Path.Combine(item.DirectoryName, newName);

                        if (newFullName != item.FullName)
                        {
                            //记录文件名的变化
                            worker.ReportProgress(i, $"\r\n{item.FullName}\r\n=>\r\n{newFullName}\r\n\r\n");
                            File.Delete(item.FullName);
                        }
                        File.WriteAllText(newFullName, text, Encoding.UTF8);
                    }
                    else
                    {
                        File.WriteAllText(item.FullName, text, Encoding.UTF8);

                    }
                    worker.ReportProgress(i, $"{item.Name}=>完成\r\n");


                } //处理内容结束

            }
            //重命名当前目录文件和文件内容

            //获取子目录
            string[] dirs = Directory.GetDirectories(arguments.RootDir);
            foreach (string dir in dirs)
            {

                // 检查是否取消操作
                if (worker.CancellationPending)
                {
                    e.Cancel = true;
                    break;
                }
                else// 开始处理内容...
                {

                    arguments.RootDir = dir;
                    RenameAllFileNameAndContent(worker, e, arguments);
                } //处理内容结束               
            }
            //获取子目录
        }

        #endregion

        #region 选择文件路径
        /// <summary>
        /// 选择文件路径
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnSelect_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog
            {
                Description = "请选择ABP项目所在文件夹(aspnet-zero-core-6.2.0)"
            };
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                if (string.IsNullOrEmpty(dialog.SelectedPath))
                {
                    MessageBox.Show(this, "文件夹路径不能为空", "提示");
                    return;
                }
                txtRootDir.Text = dialog.SelectedPath;
            }
        }

        #endregion

        #region 退出并保存设置
        private void BtnClose_Click(object sender, EventArgs e)
        {

            if (!string.IsNullOrWhiteSpace(txtFilter.Text))
            {
                Settings1.Default.setFilter = txtFilter.Text.Trim();
            }
            if (!string.IsNullOrWhiteSpace(txtOldCompanyName.Text))
            {
                Settings1.Default.setOldCompanyName = txtOldCompanyName.Text.Trim();
            }
            if (!string.IsNullOrWhiteSpace(txtOldProjectName.Text))
            {
                Settings1.Default.setOldProjectName = txtOldProjectName.Text.Trim();
            }
            if (!string.IsNullOrWhiteSpace(txtRootDir.Text))
            {
                Settings1.Default.setRootDir = txtRootDir.Text.Trim();
            }
            Settings1.Default.setNewCompanyName = txtNewCompanyName.Text.Trim();
            if (!string.IsNullOrWhiteSpace(txtNewProjectName.Text))
            {
                Settings1.Default.setNewProjectName = txtNewProjectName.Text.Trim();
            }

            if (e is MyEventArgs)
            {
                Settings1.Default.setOldCompanyName = txtNewCompanyName.Text.Trim();
                Settings1.Default.setOldProjectName = txtNewProjectName.Text.Trim();
            }

            Settings1.Default.Save();
            Environment.Exit(0);
        }
        #endregion

        #region 启动加载设置
        private void FormMain_Load(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(Settings1.Default.setFilter))
            {
                txtFilter.Text = Settings1.Default.setFilter.Trim(); ;
            }
            if (!string.IsNullOrWhiteSpace(Settings1.Default.setOldCompanyName))
            {
                txtOldCompanyName.Text = Settings1.Default.setOldCompanyName.Trim();
            }
            if (!string.IsNullOrWhiteSpace(Settings1.Default.setOldProjectName))
            {
                txtOldProjectName.Text = Settings1.Default.setOldProjectName.Trim();
            }
            if (!string.IsNullOrWhiteSpace(Settings1.Default.setRootDir))
            {
                txtRootDir.Text = Settings1.Default.setRootDir.Trim();
            }
            if (!string.IsNullOrWhiteSpace(Settings1.Default.setNewCompanyName))
            {
                txtNewCompanyName.Text = Settings1.Default.setNewCompanyName.Trim();
            }
            if (!string.IsNullOrWhiteSpace(Settings1.Default.setNewProjectName))
            {
                txtNewProjectName.Text = Settings1.Default.setNewProjectName.Trim();
            }
        }
        #endregion

        #region 恢复默认值
        private void BtnReset_Click(object sender, EventArgs e)
        {
            txtFilter.Text = ".cs,.cshtml,.js,.ts,.csproj,.sln,.xml,.config";
        }
        private void Label1_Click(object sender, EventArgs e)
        {
            txtOldCompanyName.Text = "MyCompanyName";
        }

        private void Label2_Click(object sender, EventArgs e)
        {
            txtOldProjectName.Text = "AbpZeroTemplate";
        }

        private void Label5_Click(object sender, EventArgs e)
        {
            txtNewCompanyName.Text = "";
        }

        private void Label4_Click(object sender, EventArgs e)
        {
            txtNewProjectName.Text = "";
        }

        private void Label3_Click(object sender, EventArgs e)
        {
            txtRootDir.Text = "";
        }
        #endregion
    }
    public class Arguments
    {
        public readonly string filter = ".cs,.cshtml,.js,.ts,.csproj,.sln,.xml,.config";
        private string _oldCompanyName = "MyCompanyName";
        public string OldCompanyName
        {
            get => string.IsNullOrWhiteSpace(NewCompanyName) ? _oldCompanyName + "." : _oldCompanyName;
            set => _oldCompanyName = value;

        }
        public string OldProjectName { get; set; } = "AbpZeroTemplate";
        public string NewCompanyName { get; set; }
        public string NewPeojectName { get; set; }
        public string RootDir { get; set; }
    }
}
