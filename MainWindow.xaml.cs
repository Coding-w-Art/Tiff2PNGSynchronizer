using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.VisualBasic.FileIO;

namespace YesChefTiffWatcher
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        System.Windows.Forms.NotifyIcon notifyIcon;
        System.Windows.Forms.MenuItem syncMenuItem;
        FileSystemWatcher watcher;

        string strWatcherPath;
        string strSyncerPath;
        bool watchingState = false;
        bool readyForWatch = false;
        bool syncingState = false;


        List<string> syncingList = new List<string>();
        List<string> removingList = new List<string>();

        public MainWindow()
        {
            InitializeComponent();
            InitializeSystemTray();
            InitializeWatcher();

            FileSystemWatcher watcher = new FileSystemWatcher();

            strWatcherPath = Properties.Settings.Default.WatcherPath;
            if (!string.IsNullOrEmpty(strWatcherPath))
            {
                TextBoxWatcher.Text = strWatcherPath;
            }

            if (!Directory.Exists(strWatcherPath))
            {
                TextBoxWatcher.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 0, 0));
            }

            strSyncerPath = Properties.Settings.Default.SyncerPath;
            if (!string.IsNullOrEmpty(strSyncerPath))
            {
                TextBoxSyncer.Text = strSyncerPath;
            }

            if (!Directory.Exists(strSyncerPath))
            {
                TextBoxSyncer.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 0, 0));
            }

            readyForWatch = CheckReadyToWatch();
        }

        private void StartWatcher()
        {
            watcher.Path = strWatcherPath;
            watchingState = true;
            watcher.EnableRaisingEvents = true;
            notifyIcon.Icon = new System.Drawing.Icon("../../Resources/Icon_Running.ico");
        }

        private void StopWatcher()
        {
            watchingState = false;
            watcher.EnableRaisingEvents = false;
            notifyIcon.Icon = new System.Drawing.Icon("../../Resources/Icon_StopRunning.ico");
        }

        private bool CheckReadyToWatch()
        {
            if (string.IsNullOrEmpty(strWatcherPath) || string.IsNullOrEmpty(strSyncerPath))
                return false;

            if (strWatcherPath == strSyncerPath)
                return false;

            if (!Directory.Exists(strWatcherPath))
                return false;

            if (!Directory.Exists(strSyncerPath))
                return false;

            return true;
        }

        private void InitializeSystemTray()
        {
            notifyIcon = new System.Windows.Forms.NotifyIcon();
            notifyIcon.Text = "Yes! Chef 资源同步工具";

            syncMenuItem = new System.Windows.Forms.MenuItem("同步");
            syncMenuItem.Enabled = false;
            syncMenuItem.Click += Icon_SyncClick;
            System.Windows.Forms.MenuItem show = new System.Windows.Forms.MenuItem("打开");
            show.Click += Icon_ShowClick;
            System.Windows.Forms.MenuItem stop = new System.Windows.Forms.MenuItem("暂停监控");
            stop.Click += Icon_StopClick;
            System.Windows.Forms.MenuItem exit = new System.Windows.Forms.MenuItem("退出");
            exit.Click += Icon_ExitClick;
            notifyIcon.ContextMenu = new System.Windows.Forms.ContextMenu(new System.Windows.Forms.MenuItem[] { show, syncMenuItem, stop, exit });
            notifyIcon.Icon = new System.Drawing.Icon("../../Resources/Icon.ico");
            notifyIcon.Visible = true;
            notifyIcon.DoubleClick += Icon_ShowClick;
        }

        private void RefreshSystemTray()
        {
            int count = syncingList.Count + removingList.Count;
            if (count > 0)
            {
                syncMenuItem.Text = "同步 【】";
                syncMenuItem.Enabled = true;
            }
            else
            {
                syncMenuItem.Text = "同步";
                syncMenuItem.Enabled = false;
            }
        }

        private void InitializeWatcher()
        {
            watcher = new FileSystemWatcher();
            watcher.Filter = "*.tif";
            watcher.Changed += new FileSystemEventHandler(OnWatcherFileChanged);
            watcher.Created += new FileSystemEventHandler(OnWatcherFileCreated);
            watcher.Deleted += new FileSystemEventHandler(OnWatcherFileDeleted);
            watcher.Renamed += new RenamedEventHandler(OnWatcherFileRenamed);
            watcher.NotifyFilter = NotifyFilters.Attributes | NotifyFilters.CreationTime | NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size;
            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = false;
        }

        private void Icon_ShowClick(object sender, EventArgs e)
        {
            if (!App.Current.MainWindow.IsVisible)
            {
                App.Current.MainWindow.Show();
            }

            App.Current.MainWindow.Show();
            App.Current.MainWindow.Activate();
            App.Current.MainWindow.Topmost = true;
            App.Current.MainWindow.Topmost = false;
            App.Current.MainWindow.Focus();
        }

        private void Icon_StopClick(object sender, EventArgs e)
        {
            watchingState = false;
            watcher.EnableRaisingEvents = false;
            StopWatcher();
        }

        private void Icon_ExitClick(object sender, EventArgs e)
        {
            notifyIcon.Visible = false;
            Application.Current.Shutdown();
        }

        private void Icon_SyncClick(object sender, EventArgs e)
        {
            Icon_ShowClick(sender, e);
        }


        private void Button_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Button button)
            {
                button.Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 0));
            }
        }

        private void Button_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Button button)
            {
                button.Foreground = new SolidColorBrush(Color.FromRgb(241, 241, 241));
            }
        }

        private void BtnPickWatcher_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog openFileDialog = new System.Windows.Forms.FolderBrowserDialog();
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (!string.IsNullOrEmpty(strSyncerPath) && strSyncerPath == openFileDialog.SelectedPath)
                {
                    MessageBox.Show("不能与同步文件夹相同。", "提示", MessageBoxButton.OK);
                    return;
                }

                if (Directory.Exists(openFileDialog.SelectedPath))
                {
                    strWatcherPath = openFileDialog.SelectedPath;
                    TextBoxWatcher.Text = strWatcherPath;
                    TextBoxWatcher.BorderBrush = new SolidColorBrush(Color.FromRgb(63, 63, 67));
                    Properties.Settings.Default.WatcherPath = strWatcherPath;
                    Properties.Settings.Default.Save();
                }
            }
        }

        private void BtnPickSyncer_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog openFileDialog = new System.Windows.Forms.FolderBrowserDialog();
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (!string.IsNullOrEmpty(strWatcherPath) && strWatcherPath == openFileDialog.SelectedPath)
                {
                    MessageBox.Show("不能与监控文件夹相同。", "提示", MessageBoxButton.OK);
                    return;
                }

                if (Directory.Exists(openFileDialog.SelectedPath))
                {
                    strSyncerPath = openFileDialog.SelectedPath;
                    TextBoxSyncer.Text = strSyncerPath;
                    TextBoxSyncer.BorderBrush = new SolidColorBrush(Color.FromRgb(63, 63, 67));
                    Properties.Settings.Default.SyncerPath = strSyncerPath;
                    Properties.Settings.Default.Save();
                }
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            Properties.Settings.Default.Save();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                MinimizeWindow();
            }
        }

        private void MinimizeWindow()
        {
            WindowState = WindowState.Minimized;
            App.Current.MainWindow.Visibility = Visibility.Hidden;
        }

        private void OnWatcherFileChanged(object source, FileSystemEventArgs e)
        {
            if (!watchingState)
                return;

            string path = e.FullPath;
            //if (File.Exists(path))
            //{
            //    syncingList.Add(path);
            //}

            //ShowTrayMessage("修改文件", path);
            //SyncTiffFile(e.FullPath);

            if (!syncingList.Contains(e.FullPath))
            {
                syncingList.Add(e.FullPath);
            }
        }

        private void OnWatcherFileCreated(object source, FileSystemEventArgs e)
        {
            if (!watchingState)
                return;

            string path = e.FullPath;
            //if (File.Exists(path))
            //{
            //    syncingList.Add(path);
            //}

            //ShowTrayMessage("创建文件", path);
            //SyncTiffFile(e.FullPath);


            string newPath = path.Replace(strWatcherPath, strSyncerPath);
            if (newPath != path)
            {
                newPath = System.IO.Path.ChangeExtension(newPath, ".png");
                if (removingList.Contains(newPath))
                {
                    removingList.Remove(newPath);
                }
            }

            if (!syncingList.Contains(e.FullPath))
            {
                syncingList.Add(e.FullPath);
            }
        }

        private void OnWatcherFileDeleted(object source, FileSystemEventArgs e)
        {
            if (!watchingState)
                return;

            string path = e.FullPath;
            string newPath = path.Replace(strWatcherPath, strSyncerPath);
            if (newPath == path)
                return;

            newPath = System.IO.Path.ChangeExtension(newPath, ".png");
            if (File.Exists(newPath))
            {
                //ShowTrayMessage("删除文件", newPath);
                //FileSystem.DeleteFile(newPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                if (syncingList.Contains(path))
                {
                    syncingList.Remove(path);
                }

                if (!removingList.Contains(newPath))
                {
                    removingList.Add(newPath);
                }
            }
        }

        private void OnWatcherFileRenamed(object source, FileSystemEventArgs e)
        {
            if (!watchingState)
                return;

            string path = e.FullPath;
            //ShowTrayMessage("重命名文件", path);
            //SyncTiffFile(e.FullPath);

            if (!syncingList.Contains(e.FullPath))
            {
                syncingList.Add(e.FullPath);
            }
        }

        private void ShowTrayMessage(string title, string text)
        {
            notifyIcon.BalloonTipTitle = title;
            notifyIcon.BalloonTipText = text;
            notifyIcon.ShowBalloonTip(1000);
        }


        private void SyncTiffFile(string path)
        {
            string ext = System.IO.Path.GetExtension(path);
            if (ext != ".tif" && ext != ".tiff")
                return;

            string newPath = path.Replace(strWatcherPath, strSyncerPath);
            if (newPath == path)
                return;

            string dir = System.IO.Path.GetDirectoryName(newPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            newPath = System.IO.Path.ChangeExtension(newPath, ".png");

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                if (stream.CanRead && stream.Length > 0)
                {
                    System.Drawing.Image image = System.Drawing.Image.FromStream(stream);
                    image.Save(newPath, System.Drawing.Imaging.ImageFormat.Png);
                    //var eps = new System.Drawing.Imaging.EncoderParameters(1);
                    //var ep = new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.ColorDepth, 24L);
                    //eps.Param[0] = ep;
                    //ep.Dispose();
                    //eps.Dispose();
                    image.Dispose();
                }
                stream.Close();
                stream.Dispose();
            }
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (watchingState)
            {
                StopWatcher();
                ShowTrayMessage("Yes! Chef 资源同步工具", "监控已暂停");
                window.Title = "Yes! Chef 资源同步工具  -  监控已暂停";
                BtnStart.Content = "开始监控";
                window.Icon = new BitmapImage(new Uri("pack://application:,,,/Resources/Icon_NotRunning.ico", UriKind.RelativeOrAbsolute));
            }
            else
            {
                readyForWatch = CheckReadyToWatch();
                if (readyForWatch)
                {
                    StartWatcher();
                    MinimizeWindow();
                    ShowTrayMessage("Yes! Chef 资源同步工具", $"正在监控：{strWatcherPath}");
                    BtnStart.Content = "暂停监控";
                    window.Icon = new BitmapImage(new Uri("pack://application:,,,/Resources/Icon_Running.ico", UriKind.RelativeOrAbsolute));
                    window.Title = "Yes! Chef 资源同步工具  -  正在监控...";
                }
                else
                {
                    MessageBox.Show("请检查路径。", "提示", MessageBoxButton.OK);
                }
            }
        }



        private void StartSync()
        {

        }
    }
}
