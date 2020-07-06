using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace YesChefTiffWatcher
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        System.Windows.Forms.NotifyIcon notifyIcon;
        System.Windows.Forms.MenuItem syncMenuItem;
        System.Windows.Forms.MenuItem showMenuItem;
        System.Windows.Forms.MenuItem stopMenuItem;
        System.Windows.Forms.MenuItem exitMenuItem;
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

                if (!Directory.Exists(strWatcherPath))
                {
                    TextBoxWatcher.BorderBrush = new SolidColorBrush(Colors.Red);
                }
            }

            strSyncerPath = Properties.Settings.Default.SyncerPath;
            if (!string.IsNullOrEmpty(strSyncerPath))
            {
                TextBoxSyncer.Text = strSyncerPath;

                if (!Directory.Exists(strSyncerPath))
                {
                    TextBoxSyncer.BorderBrush = new SolidColorBrush(Colors.Red);
                }
            }

            readyForWatch = CheckReadyToWatch();
            CheckBoxAutoRun.IsChecked = GetAutoRunState();
        }

        private void CommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        // Minimize
        private void CommandBinding_Executed_Minimize(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.MinimizeWindow(this);
        }

        // Close
        private void CommandBinding_Executed_Close(object sender, ExecutedRoutedEventArgs e)
        {
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            SystemCommands.CloseWindow(this);
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
            notifyIcon = new System.Windows.Forms.NotifyIcon { Text = Properties.Resources.AppName };

            syncMenuItem = new System.Windows.Forms.MenuItem("同步");
            syncMenuItem.Enabled = false;
            syncMenuItem.Click += Icon_SyncClick;
            showMenuItem = new System.Windows.Forms.MenuItem("打开");
            showMenuItem.Click += Icon_ShowClick;
            stopMenuItem = new System.Windows.Forms.MenuItem("暂停监控");
            stopMenuItem.Click += Icon_StopClick;
            exitMenuItem = new System.Windows.Forms.MenuItem("退出");
            exitMenuItem.Click += Icon_ExitClick;
            notifyIcon.ContextMenu = new System.Windows.Forms.ContextMenu(new[] { showMenuItem, syncMenuItem, stopMenuItem, exitMenuItem });
            notifyIcon.Icon = new System.Drawing.Icon("../../Resources/Icon.ico");
            notifyIcon.Visible = true;
            notifyIcon.DoubleClick += Icon_ShowClick;
        }

        private void RefreshSystemTray()
        {
            int count = syncingList.Count + removingList.Count;
            if (count > 0)
            {
                syncMenuItem.Text = $"同步 [{count}]";
                syncMenuItem.Enabled = true;
            }
            else
            {
                syncMenuItem.Text = "同步";
                syncMenuItem.Enabled = false;
            }
            notifyIcon.ContextMenu = new System.Windows.Forms.ContextMenu(new[] { showMenuItem, syncMenuItem, stopMenuItem, exitMenuItem });
            BtnSync.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;

            TextBlockSync.Text = $"待同步文件：[{syncingList.Count}]";
            TextBlockRemove.Text = $"待删除文件：[{removingList.Count}]";
        }

        private void InitializeWatcher()
        {
            watcher = new FileSystemWatcher { Filter = "*.tif" };
            watcher.Changed += OnWatcherFileChanged;
            watcher.Created += OnWatcherFileCreated;
            watcher.Deleted += OnWatcherFileDeleted;
            watcher.Renamed += OnWatcherFileRenamed;
            watcher.NotifyFilter = NotifyFilters.Attributes | NotifyFilters.CreationTime | NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.Security;
            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = false;
        }

        private void Icon_ShowClick(object sender, EventArgs e)
        {
            if (!Application.Current.MainWindow.IsVisible)
            {
                Application.Current.MainWindow.Show();
            }

            Application.Current.MainWindow.Show();
            Application.Current.MainWindow.Activate();
            Application.Current.MainWindow.Topmost = true;
            Application.Current.MainWindow.Topmost = false;
            Application.Current.MainWindow.Focus();
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
            notifyIcon.Dispose();
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
            else if (sender is Border border)
            {
                border.Background = new SolidColorBrush(Color.FromRgb(63, 63, 65));
            }
        }

        private void Button_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Button button)
            {
                button.Foreground = new SolidColorBrush(Color.FromRgb(241, 241, 241));
            }
            else if (sender is Border border)
            {
                border.Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));
            }
        }

        private void BtnPickWatcher_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog { IsFolderPicker = true };
            CommonFileDialogResult result = dialog.ShowDialog();
            if (result == CommonFileDialogResult.Ok)
            {
                string path = dialog.FileName;
                if (!string.IsNullOrEmpty(strSyncerPath) && strSyncerPath == path)
                {
                    MessageBox.Show("不能与同步文件夹相同。", "提示", MessageBoxButton.OK);
                    return;
                }

                if (Directory.Exists(path))
                {
                    strWatcherPath = path;
                    TextBoxWatcher.Text = strWatcherPath;
                    TextBoxWatcher.BorderBrush = new SolidColorBrush(Color.FromRgb(63, 63, 67));
                    Properties.Settings.Default.WatcherPath = strWatcherPath;
                    Properties.Settings.Default.Save();
                }
            }
        }

        private void BtnPickSyncer_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog { IsFolderPicker = true };
            CommonFileDialogResult result = dialog.ShowDialog();
            if (result == CommonFileDialogResult.Ok)
            {
                string path = dialog.FileName;
                if (!string.IsNullOrEmpty(strWatcherPath) && strWatcherPath == path)
                {
                    MessageBox.Show("不能与同步文件夹相同。", "提示", MessageBoxButton.OK);
                    return;
                }

                if (Directory.Exists(path))
                {
                    strSyncerPath = path;
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
            //else if (WindowState == WindowState.Normal)
            //{
            //    Application.Current.MainWindow.Topmost = true;
            //    Application.Current.MainWindow.Topmost = false;
            //    Application.Current.MainWindow.Focus();
            //}
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

            RefreshSystemTray();
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
                newPath = Path.ChangeExtension(newPath, ".png");
                if (removingList.Contains(newPath))
                {
                    removingList.Remove(newPath);
                }
            }

            if (!syncingList.Contains(e.FullPath))
            {
                syncingList.Add(e.FullPath);
            }

            RefreshSystemTray();
        }

        private void OnWatcherFileDeleted(object source, FileSystemEventArgs e)
        {
            if (!watchingState)
                return;

            string path = e.FullPath;
            string newPath = path.Replace(strWatcherPath, strSyncerPath);
            if (newPath == path)
                return;

            newPath = Path.ChangeExtension(newPath, ".png");
            if (File.Exists(newPath))
            {
                if (syncingList.Contains(path))
                {
                    syncingList.Remove(path);
                }

                if (!removingList.Contains(newPath))
                {
                    removingList.Add(newPath);
                }
            }

            RefreshSystemTray();
        }

        private void OnWatcherFileRenamed(object source, FileSystemEventArgs e)
        {
            if (!watchingState)
                return;

            string path = e.FullPath;

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


        private void SyncFile(string path)
        {
            string ext = Path.GetExtension(path);
            if (ext != ".tif")
                return;

            string newPath = path.Replace(strWatcherPath, strSyncerPath);
            if (newPath == path)
                return;

            string dir = Path.GetDirectoryName(newPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            newPath = Path.ChangeExtension(newPath, ".png");

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

        private void RemoveFile(string path)
        {
            FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (watchingState)
            {
                StopWatcher();
                ShowTrayMessage(Properties.Resources.AppName, "监控已暂停");
                BtnStart.Content = "开始监控";
                WindowTitle.Content = $"{Properties.Resources.AppName}";
                WindowIcon.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/Icon_StopRunning.ico", UriKind.RelativeOrAbsolute));
            }
            else
            {
                readyForWatch = CheckReadyToWatch();
                if (readyForWatch)
                {
                    StartWatcher();
                    MinimizeWindow();
                    ShowTrayMessage(Properties.Resources.AppName, $"正在监控：{strWatcherPath}");
                    BtnStart.Content = "暂停监控";
                    WindowTitle.Content = $"{Properties.Resources.AppName}  -  正在监控...";
                    WindowIcon.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/Icon_Running.ico", UriKind.RelativeOrAbsolute));
                }
                else
                {
                    MessageBox.Show("请检查路径。", "提示", MessageBoxButton.OK);
                }
            }
        }


        private void StartSync()
        {
            watcher.EnableRaisingEvents = false;
            foreach (string path in syncingList)
            {
                SyncFile(path);
            }
            syncingList.Clear();

            foreach (string path in removingList)
            {

            }
            removingList.Clear();


            if (watchingState)
                watcher.EnableRaisingEvents = true;

            BtnSync.Visibility = Visibility.Collapsed;
            TextBlockSync.Text = "待同步文件：[0]";
            TextBlockRemove.Text = "待删除文件：[0]";
        }

        private void MoveWindow(object sender, MouseButtonEventArgs e)
        {
            App.Current.MainWindow.DragMove();
        }

        private void AutoRun_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBoxAutoRun.IsChecked == true)
            {
                CheckBoxAutoRun.IsChecked = SetAutoRun(true);
            }
            else
            {
                CheckBoxAutoRun.IsChecked = SetAutoRun(false);
            }
        }

        private bool SetAutoRun(bool autoRun)
        {
            string startupPath = AppDomain.CurrentDomain.BaseDirectory + "YesChefTiffWatcher.exe";
            RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true) ??
                              Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");

            try
            {
                string[] keyNames = key.GetValueNames();
                foreach (string keyName in keyNames)
                {
                    if (keyName == "YesChefTiffWatcher")
                    {
                        if (!autoRun)
                        {
                            key.DeleteValue("YesChefTiffWatcher");
                            key.Close();
                            return false;
                        }
                        key.Close();
                        return true;
                    }
                }

                if (autoRun)
                {
                    key.SetValue("YesChefTiffWatcher", startupPath);
                    key.Close();
                    return true;
                }
            }
            catch
            {
                key.Close();
                return false;
            }

            key.Close();
            return false;
        }

        private bool GetAutoRunState()
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
            if (key == null)
                return false;

            try
            {
                string[] keyNames = key.GetValueNames();
                foreach (string keyName in keyNames)
                {
                    if (keyName == "YesChefTiffWatcher")
                    {
                        key.Close();
                        return true;
                    }
                }
            }
            catch
            {
                key.Close();
                return false;
            }

            key.Close();
            return false;
        }

        private void TextBlockSync_Click(object sender, MouseButtonEventArgs e)
        {
            StringBuilder str = new StringBuilder();
            foreach (string path in syncingList)
            {
                str.AppendLine(path);
            }

            MessageBox.Show(removingList.Count == 0 ? "没有待同步文件。" : str.ToString(), "待同步文件列表");
        }

        private void TextBlockRemove_Click(object sender, MouseButtonEventArgs e)
        {
            StringBuilder str = new StringBuilder();
            foreach (string path in removingList)
            {
                str.AppendLine(path);
            }

            MessageBox.Show(removingList.Count == 0 ? "没有待删除文件。" : str.ToString(), "待删除文件列表");
        }

        private void TextBlockClear_Click(object sender, MouseButtonEventArgs e)
        {
            syncingList.Clear();
            removingList.Clear();
            TextBlockSync.Text = "待同步文件：[0]";
            TextBlockRemove.Text = "待删除文件：[0]";
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            WindowShadowEffect.BlurRadius = 10;
            //BtnClose.SetValue(StyleProperty, Application.Current.Resources["CaptionInactiveButtonStyle"]);
            //BtnMinimize.SetValue(StyleProperty, Application.Current.Resources["CaptionInactiveButtonStyle"]);
        }

        private void MainWindow_OnActivated(object sender, EventArgs e)
        {
            WindowShadowEffect.BlurRadius = 20;
            //Style style1 = Application.Current.Resources["CaptionCloseButtonStyle"] as Style;
            //BtnClose.Style = style1;

            //Style style2 = Application.Current.Resources["CaptionButtonStyle"] as Style;
            //BtnMinimize.Style = style2;
        }
    }
}
