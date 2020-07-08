using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
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
using Color = System.Windows.Media.Color;
using Image = System.Drawing.Image;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using SearchOption = System.IO.SearchOption;

namespace YesChefTiffWatcher
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        System.Windows.Forms.NotifyIcon notifyIcon;
        System.Windows.Forms.MenuItem showMenuItem;
        System.Windows.Forms.MenuItem syncMenuItem;
        System.Windows.Forms.MenuItem stopMenuItem;
        System.Windows.Forms.MenuItem resumeMenuItem;
        System.Windows.Forms.MenuItem exitMenuItem;
        FileSystemWatcher watcher;

        string strWatcherPath;
        string strSyncerPath;
        private bool realTimeSync;
        bool watchingState = false;
        bool readyForWatch = false;
        bool syncingState = false;
        private bool updateUI = true;
        private double progressBarWidth;


        List<string> syncingList = new List<string>();
        List<string> removingList = new List<string>();



        [StructLayout(LayoutKind.Sequential)]
        public struct SHFILEINFO
        {
            public IntPtr hIcon;                                        //文件的图标句柄
            public int iIcon;                                           //文件图标的系统索引号
            public uint dwAttributes;                                   //文件的属性值

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;                                //文件的显示名

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;                                   //文件的类型名
        }

        [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int SHGetFileInfo(string strFilePath, uint dwFileAttributes, ref SHFILEINFO lpFileInfo, uint cbFileInfoSize, uint uFlags);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr hIcon);
        //获取图标
        private const uint SHGFI_ICON = 0x100;
        //大图标 32 x 32
        private const uint SHGFI_LARGEICON = 0x0;

        //小图标 16 x 16
        private const uint SHGFI_SMALLICON = 0x1;

        //使用use passed dwFileAttribute
        private const uint SHGFI_USEFILEATTRIBUTES = 0x10;

        private const uint FILE_ATTRIBUTE_NORMAL = 0x80;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
        private const uint SHGFI_DISPLAYNAME = 0x200;
        private const uint SHGFI_SYSICONINDEX = 0x400;


        public MainWindow()
        {
            InitializeComponent();
            InitializeSystemTray();
            InitializeWatcher();

            //BitmapSource source1 = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, new Int32Rect(0, 0, 32, 32), BitmapSizeOptions.FromWidth(32));

            window.Title = Properties.Resources.AppName;
            WindowIcon.Source = new BitmapImage(new Uri("./Resources/Icon.ico", UriKind.RelativeOrAbsolute));

            progressBarWidth = ProgressBar.Width;
            realTimeSync = Properties.Settings.Default.RealTime;
            CheckBoxRealTime.IsChecked = realTimeSync;
            PanelList.Visibility = realTimeSync ? Visibility.Collapsed : Visibility.Visible;

            strWatcherPath = Properties.Settings.Default.WatcherPath;
            if (!string.IsNullOrEmpty(strWatcherPath))
            {
                TextBoxWatcher.Text = strWatcherPath;

                if (!Directory.Exists(strWatcherPath))
                {
                    TextBoxWatcher.BorderBrush = new SolidColorBrush(Colors.Red);
                }
                else
                {
                    ImageWatcher.Source = GetIcon(strWatcherPath);
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
                else
                {
                    ImageSyncer.Source = GetIcon(strWatcherPath);
                }
            }

            readyForWatch = CheckReadyToWatch();
            CheckBoxAutoRun.IsChecked = GetAutoRunState();
        }

        public static ImageSource GetIcon(string strFilePath)
        {
            uint uFlag = SHGFI_ICON | SHGFI_USEFILEATTRIBUTES | SHGFI_DISPLAYNAME | SHGFI_LARGEICON;
            uint uAttribute = FILE_ATTRIBUTE_NORMAL | FILE_ATTRIBUTE_DIRECTORY;

            SHFILEINFO fileInfo = new SHFILEINFO();
            if (0 != SHGetFileInfo(strFilePath, uAttribute, ref fileInfo, (uint)Marshal.SizeOf(typeof(SHFILEINFO)), uFlag))
            {
                if (fileInfo.hIcon != IntPtr.Zero)
                {
                    BitmapSource bmpSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(fileInfo.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    DestroyIcon(fileInfo.hIcon);
                    return bmpSource;
                }
            }

            return null;
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
            if (!watchingState)
            {
                ShowTrayMessage($"正在监控：{strWatcherPath}");
            }

            watcher.Path = strWatcherPath;
            watchingState = true;
            watcher.EnableRaisingEvents = true;
            notifyIcon.Icon = new Icon("./Resources/Icon_Running.ico");
            notifyIcon.ContextMenu = new System.Windows.Forms.ContextMenu(new[] { showMenuItem, syncMenuItem, stopMenuItem, exitMenuItem });

            if (updateUI)
            {
                CheckBoxRealTime.IsEnabled = false;
                BtnStart.Content = "暂停监控";
                string name = $"{Properties.Resources.AppName}  -  正在监控...";
                WindowTitle.Content = name;
                window.Title = name;
                WindowIcon.Source = new BitmapImage(new Uri("./Resources/Icon_Running.ico", UriKind.RelativeOrAbsolute));
            }
        }

        private void StopWatcher()
        {
            if (watchingState)
            {
                ShowTrayMessage("监控已暂停");
            }

            watchingState = false;
            watcher.EnableRaisingEvents = false;
            notifyIcon.Icon = new Icon("./Resources/Icon_StopRunning.ico");
            notifyIcon.ContextMenu = new System.Windows.Forms.ContextMenu(new[] { showMenuItem, syncMenuItem, resumeMenuItem, exitMenuItem });

            if (updateUI)
            {
                CheckBoxRealTime.IsEnabled = true;
                BtnStart.Content = "开始监控";
                WindowTitle.Content = Properties.Resources.AppName;
                window.Title = Properties.Resources.AppName;
                WindowIcon.Source = new BitmapImage(new Uri("./Resources/Icon.ico", UriKind.RelativeOrAbsolute));
            }
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
            syncMenuItem = new System.Windows.Forms.MenuItem("待同步 [0]");
            syncMenuItem.Enabled = false;
            syncMenuItem.Click += Icon_SyncClick;
            showMenuItem = new System.Windows.Forms.MenuItem("打开");
            showMenuItem.Click += Icon_ShowClick;
            stopMenuItem = new System.Windows.Forms.MenuItem("暂停监控");
            stopMenuItem.Click += Icon_StopClick;
            resumeMenuItem = new System.Windows.Forms.MenuItem("开始监控");
            resumeMenuItem.Click += Icon_ResumeClick;
            exitMenuItem = new System.Windows.Forms.MenuItem("退出");
            exitMenuItem.Click += Icon_ExitClick;
            notifyIcon.ContextMenu = new System.Windows.Forms.ContextMenu(new[] { /*showMenuItem,*/ syncMenuItem, resumeMenuItem, exitMenuItem });
            notifyIcon.Icon = new Icon("./Resources/Icon.ico");
            notifyIcon.Visible = true;
            notifyIcon.DoubleClick += Icon_ShowClick;
        }

        private void RefreshSystemTray()
        {
            int count = syncingList.Count + removingList.Count;
            if (count > 0)
            {
                syncMenuItem.Text = $"待同步 [{count}]";
            }
            else
            {
                syncMenuItem.Text = "待同步 [0]";
            }

            if (watchingState)
            {
                notifyIcon.ContextMenu = new System.Windows.Forms.ContextMenu(new[] { syncMenuItem, stopMenuItem, exitMenuItem });
            }
            else
            {
                notifyIcon.ContextMenu = new System.Windows.Forms.ContextMenu(new[] { syncMenuItem, resumeMenuItem, exitMenuItem });
            }

            if (updateUI)
            {
                BtnSync.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
                TextBlockSync.Text = syncingList.Count > 0 ? $"待同步文件：[{syncingList.Count}]" : "";
                TextBlockRemove.Text = removingList.Count > 0 ? $"待删除文件：[{removingList.Count}]" : "";
                TextBlockClear.Text = count > 0 ? "清除..." : "";
            }
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
            StopWatcher();
            window.ShowInTaskbar = true;
            WindowState = WindowState.Normal;
            window.Activate();
        }

        private void Icon_StopClick(object sender, EventArgs e)
        {
            StopWatcher();
        }

        private void Icon_ResumeClick(object sender, EventArgs e)
        {
            if (!Directory.Exists(strWatcherPath))
                return;
            StartWatcher();

            if (WindowState == WindowState.Normal)
                WindowState = WindowState.Minimized;
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
                    MessageBox.Show("不能与目标文件夹相同。", Properties.Resources.AppName, MessageBoxButton.OK);
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
                    MessageBox.Show("不能与原始文件夹相同。", Properties.Resources.AppName, MessageBoxButton.OK);
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
                window.ShowInTaskbar = false;
                updateUI = false;
            }
            else if (WindowState == WindowState.Normal)
            {
                updateUI = true;
                int count = syncingList.Count + removingList.Count;
                BtnSync.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
                TextBlockSync.Text = syncingList.Count > 0 ? $"待同步文件：[{syncingList.Count}]" : "";
                TextBlockRemove.Text = removingList.Count > 0 ? $"待删除文件：[{removingList.Count}]" : "";
                TextBlockClear.Text = count > 0 ? "清除..." : "";
                PanelList.Visibility = realTimeSync ? Visibility.Collapsed : Visibility.Visible;
                CheckBoxRealTime.IsEnabled = !watchingState;


                if (watchingState)
                {
                    BtnStart.Content = "暂停监控";
                    string name = $"{Properties.Resources.AppName}  -  正在监控...";
                    WindowTitle.Content = name;
                    window.Title = name;
                    WindowIcon.Source = new BitmapImage(new Uri("./Resources/Icon_Running.ico", UriKind.RelativeOrAbsolute));
                }
                else
                {
                    BtnStart.Content = "开始监控";
                    WindowTitle.Content = Properties.Resources.AppName;
                    window.Title = Properties.Resources.AppName;
                    WindowIcon.Source = new BitmapImage(new Uri("./Resources/Icon.ico", UriKind.RelativeOrAbsolute));
                }
            }
        }

        private void MinimizeWindow()
        {
            WindowState = WindowState.Minimized;
            window.ShowInTaskbar = false;
        }

        private void OnWatcherFileChanged(object source, FileSystemEventArgs e)
        {
            if (!watchingState)
                return;

            string path = e.FullPath;
            if (!syncingList.Contains(path))
            {
                syncingList.Add(path);
            }
            RefreshSystemTray();
        }

        private void OnWatcherFileCreated(object source, FileSystemEventArgs e)
        {
            if (!watchingState)
                return;

            string path = e.FullPath;
            string newPath = path.Replace(strWatcherPath, strSyncerPath);

            if (realTimeSync)
            {
                if (SyncFile(path, strSyncerPath))
                    ShowTrayMessage($"创建文件：{newPath}");
                return;
            }

            if (newPath != path)
            {
                newPath = Path.ChangeExtension(newPath, ".png");
                if (removingList.Contains(newPath))
                {
                    removingList.Remove(newPath);
                }
            }

            if (!syncingList.Contains(path))
            {
                syncingList.Add(path);
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

            if (realTimeSync)
            {
                if (File.Exists(newPath))
                {
                    FileSystem.DeleteFile(newPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                }
                if (File.Exists(newPath + ".meta"))
                {
                    FileSystem.DeleteFile(newPath + ".meta", UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                }
                ShowTrayMessage($"删除文件：{newPath}");
                return;
            }

            if (File.Exists(newPath) && !removingList.Contains(newPath))
            {
                removingList.Add(newPath);
            }

            string meta = newPath + ".meta";
            if (File.Exists(meta) && !removingList.Contains(meta))
            {
                removingList.Add(meta);
            }

            newPath = Path.ChangeExtension(newPath, ".png");
            if (File.Exists(newPath))
            {
                if (syncingList.Contains(newPath))
                {
                    syncingList.Remove(newPath);
                }

                if (!removingList.Contains(newPath))
                {
                    removingList.Add(newPath);
                }
            }

            string newMeta = newPath + ".meta";
            if (File.Exists(newMeta))
            {
                if (syncingList.Contains(newMeta))
                {
                    syncingList.Remove(newMeta);
                }

                if (!removingList.Contains(newMeta))
                {
                    removingList.Add(newMeta);
                }
            }

            RefreshSystemTray();
        }

        private void OnWatcherFileRenamed(object source, RenamedEventArgs e)
        {
            if (!watchingState)
                return;

            string newPath = Path.ChangeExtension(e.FullPath, ".png");
            newPath = newPath.Replace(strWatcherPath, strSyncerPath);
            string oldPath = Path.ChangeExtension(e.OldFullPath, ".png");
            oldPath = oldPath.Replace(strWatcherPath, strSyncerPath);
            if (File.Exists(oldPath))
            {
                if (File.Exists(newPath))
                {
                    FileSystem.DeleteFile(newPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                }
                File.Move(oldPath, newPath);
            }

            string newMeta = newPath + ".meta";
            string oldMeta = oldPath + ".meta";
            if (File.Exists(oldMeta))
            {
                if (File.Exists(newMeta))
                {
                    FileSystem.DeleteFile(newMeta, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                }
                File.Move(oldMeta, newMeta);
            }
        }

        private void ShowTrayMessage(string text)
        {
            notifyIcon.BalloonTipTitle = Properties.Resources.AppName;
            notifyIcon.BalloonTipText = text;
            notifyIcon.ShowBalloonTip(1000);
        }

        private bool SyncFile(string path, string destPath)
        {
            if (!Directory.Exists(destPath))
                return false;

            string ext = Path.GetExtension(path);
            if (ext != ".tif")
                return false;

            string newPath = path.Replace(strWatcherPath, strSyncerPath);
            bool deleteOriginal = false;
            if (newPath != path)
            {
                if (File.Exists(newPath))
                {
                    FileSystem.DeleteFile(newPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                }
            }
            else
            {
                deleteOriginal = true;
            }

            string dir = Path.GetDirectoryName(newPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string newMeta = newPath + ".meta";
            newPath = Path.ChangeExtension(newPath, ".png");
            if (File.Exists(newMeta) && !File.Exists(newPath + ".meta"))
            {
                File.Move(newMeta, newPath + ".meta");
            }

            //FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            //Image image = realTimeSync ? Image.FromStream(stream) : Image.FromFile(path);
            //stream.Close();
            //stream.Dispose();

            if (!File.Exists(path))
                return false;

            Bitmap bitmap = new Bitmap(path);

            EncoderParameters eps = new EncoderParameters(1);
            if (bitmap.PixelFormat == PixelFormat.Format32bppArgb)
            {
                eps.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.ColorDepth, 32L);
                bitmap.Save(newPath, GetEncoderInfo("image/png"), eps);
            }
            else if (bitmap.PixelFormat == PixelFormat.Format24bppRgb)
            {
                eps.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.ColorDepth, 24L); bitmap.Save(newPath, GetEncoderInfo("image/png"), eps);
            }
            else if (bitmap.PixelFormat == PixelFormat.Format64bppArgb)
            {
                Bitmap bmNew = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb);
                bmNew.SetResolution(bitmap.VerticalResolution, bitmap.VerticalResolution);
                Graphics g = Graphics.FromImage(bmNew);
                g.DrawImage(bitmap, 0, 0);
                g.Dispose();

                eps.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.ColorDepth, 32L);
                bmNew.Save(newPath, GetEncoderInfo("image/png"), eps);
                bmNew.Dispose();
            }
            else if (bitmap.PixelFormat == PixelFormat.Format48bppRgb)
            {
                Bitmap bmNew = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format24bppRgb);
                bmNew.SetResolution(bitmap.VerticalResolution, bitmap.VerticalResolution);
                Graphics g = Graphics.FromImage(bmNew);
                g.DrawImage(bitmap, 0, 0);
                g.Dispose();

                eps.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.ColorDepth, 24L);
                bmNew.Save(newPath, GetEncoderInfo("image/png"), eps);
                bmNew.Dispose();

            }
            bitmap.Dispose();

            if (deleteOriginal)
            {
                FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            }

            return true;
        }

        private static ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            ImageCodecInfo[] encoders = ImageCodecInfo.GetImageEncoders();
            foreach (ImageCodecInfo encoder in encoders)
            {
                if (encoder.MimeType == mimeType)
                    return encoder;
            }
            return null;
        }

        private void RemoveFile(string path)
        {
            string meta = path + ".meta";
            if (File.Exists(meta))
            {
                FileSystem.DeleteFile(meta, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            }

            if (File.Exists(path))
            {
                FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            }
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (watchingState)
            {
                StopWatcher();
            }
            else
            {
                readyForWatch = CheckReadyToWatch();
                if (readyForWatch)
                {
                    StartWatcher();
                    MinimizeWindow();
                }
                else
                {
                    MessageBox.Show("请检查路径。", Properties.Resources.AppName, MessageBoxButton.OK);
                }
            }
        }

        private void BtnSync_Click(object sender, RoutedEventArgs e)
        {
            watcher.EnableRaisingEvents = false;
            for (int i = 0; i < syncingList.Count; i++)
            {
                ShowProgressBar(i + 1, syncingList.Count + 1);
                string path = syncingList[i];
                SyncFile(path, strSyncerPath);
            }

            syncingList.Clear();

            ShowProgressBar(1, 1);
            foreach (string path in removingList)
            {
                RemoveFile(path);
            }
            removingList.Clear();

            HideProgressBar();

            if (watchingState)
                watcher.EnableRaisingEvents = true;

            BtnSync.Visibility = Visibility.Collapsed;
            TextBlockSync.Text = "";
            TextBlockRemove.Text = "";
            TextBlockClear.Text = "";
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

            MessageBox.Show(syncingList.Count == 0 ? "没有待同步文件。" : str.ToString(), "待同步文件列表");
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
            if (MessageBox.Show("是否清除待同步文件列表？", Properties.Resources.AppName, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                syncingList.Clear();
                removingList.Clear();
                TextBlockSync.Text = "";
                TextBlockRemove.Text = "";
            }
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            WindowShadowEffect.BlurRadius = 10;
        }

        private void MainWindow_OnActivated(object sender, EventArgs e)
        {
            WindowShadowEffect.BlurRadius = 20;
        }

        private void ManualSyncFile_Click(object sender, RoutedEventArgs e)
        {
            StopWatcher();
            CommonOpenFileDialog dialog = new CommonOpenFileDialog { IsFolderPicker = false, Multiselect = false, Title = $"{Properties.Resources.AppName}  -  选择需要同步的文件" };
            if (Directory.Exists(strWatcherPath))
            {
                dialog.InitialDirectory = strWatcherPath;
            }
            dialog.Filters.Add(new CommonFileDialogFilter("TIFF 文件", "*.tif"));
            CommonFileDialogResult result = dialog.ShowDialog();
            if (result != CommonFileDialogResult.Ok) return;

            string path = dialog.FileName;
            if (!File.Exists(path)) return;

            CommonOpenFileDialog dialog2 = new CommonOpenFileDialog { IsFolderPicker = true, Multiselect = false, Title = $"{Properties.Resources.AppName}  -  选择目标文件夹：" };
            if (Directory.Exists(strSyncerPath))
            {
                dialog2.InitialDirectory = strSyncerPath;
            }
            CommonFileDialogResult result2 = dialog2.ShowDialog();
            if (result2 != CommonFileDialogResult.Ok) return;
            string path2 = dialog2.FileName;
            if (!Directory.Exists(path2)) return;

            if (SyncFile(path, path2))
            {
                ShowTrayMessage("已同步选择的文件。");
            }
        }

        private void ManualSyncFolder_Click(object sender, RoutedEventArgs e)
        {
            StopWatcher();
            CommonOpenFileDialog dialog = new CommonOpenFileDialog { IsFolderPicker = true, Multiselect = false, Title = $"{Properties.Resources.AppName}  -  选择原始文件夹" };
            if (Directory.Exists(strWatcherPath))
            {
                dialog.InitialDirectory = strWatcherPath;
            }
            CommonFileDialogResult result = dialog.ShowDialog();
            if (result != CommonFileDialogResult.Ok) return;

            string dir = dialog.FileName;
            if (!Directory.Exists(dir)) return;
            string[] files = Directory.GetFiles(dir, "*.tif", SearchOption.AllDirectories);
            if (files.Length == 0)
            {
                MessageBox.Show("没有发现 *.tif 文件。", Properties.Resources.AppName, MessageBoxButton.OK);
                return;
            }

            CommonOpenFileDialog dialog2 = new CommonOpenFileDialog { IsFolderPicker = true, Multiselect = false, Title = $"{Properties.Resources.AppName}  -  选择目标文件夹：" };
            if (Directory.Exists(strSyncerPath))
            {
                dialog2.InitialDirectory = strSyncerPath;
            }
            CommonFileDialogResult result2 = dialog2.ShowDialog();
            if (result2 != CommonFileDialogResult.Ok) return;
            string path2 = dialog.FileName;
            if (!Directory.Exists(path2)) return;

            int count = 0;
            for (int i = 0; i < files.Length; i++)
            {
                ShowProgressBar(i + 1, files.Length);
                string p = files[i];
                if (SyncFile(p, path2))
                {
                    count++;
                }
            }

            HideProgressBar();
            ShowTrayMessage($"已同步 {count} 个文件。");
        }

        private void ShowProgressBar(int index, int count)
        {
            ProgressBar.Height = 4;
            double width = index * progressBarWidth / count;
            ProgressBar.Width = width;
        }

        private void HideProgressBar()
        {
            ProgressBar.Height = 0;
        }

        private void RealTime_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBoxRealTime.IsChecked == true)
            {
                realTimeSync = true;
                PanelList.Visibility = Visibility.Collapsed;
            }
            else
            {
                realTimeSync = false;
                PanelList.Visibility = Visibility.Visible;
            }

            Properties.Settings.Default.RealTime = realTimeSync;
            Properties.Settings.Default.Save();
        }

        private void Folder_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender == ImageWatcher)
            {
                if (Directory.Exists(strWatcherPath))
                {
                    Process.Start(strWatcherPath);
                }
            }
            else if (sender == ImageSyncer)
            {
                if (Directory.Exists(strSyncerPath))
                {
                    Process.Start(strSyncerPath);
                }
            }
        }
    }
}
