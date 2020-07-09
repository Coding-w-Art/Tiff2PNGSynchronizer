using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Microsoft.WindowsAPICodePack.Taskbar;
using Color = System.Windows.Media.Color;
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
        private FileSystemWatcher watcher;
        private TaskbarManager taskbar;

        string strWatcherPath;
        string strSyncerPath;
        private bool realTimeSync;
        bool watchingState = false;
        bool readyForWatch = false;
        bool syncingState = false;
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

            taskbar = TaskbarManager.Instance;
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
                ShowTrayMessage($"正在监控美术文件夹：\n{strWatcherPath}");
            }

            watcher.Path = strWatcherPath;
            watchingState = true;
            watcher.EnableRaisingEvents = true;
            notifyIcon.Icon = new Icon("./Resources/Icon_Running.ico");
            notifyIcon.ContextMenu = new System.Windows.Forms.ContextMenu(new[] { showMenuItem, syncMenuItem, stopMenuItem, exitMenuItem });
            WindowBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 122, 204));
            WindowShadowEffect.Color = Color.FromRgb(0, 122, 204);
            GridTitle.Background = new SolidColorBrush(Color.FromRgb(0, 122, 204));

            if (window.WindowState == WindowState.Normal)
            {
                CheckBoxRealTime.IsEnabled = false;
                BtnStart.Content = "暂停监控";
                BtnStart.BorderBrush = new SolidColorBrush(Color.FromRgb(202, 81, 0));
                string name = $"{Properties.Resources.AppName}  -  正在监控...";
                WindowTitle.Content = name;
                window.Title = name;
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
            WindowBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(63, 63, 65));
            WindowShadowEffect.Color = Color.FromRgb(0, 0, 0);
            GridTitle.Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));

            if (window.WindowState == WindowState.Normal)
            {
                CheckBoxRealTime.IsEnabled = true;
                BtnStart.Content = "开始监控";
                BtnStart.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 122, 204));
                WindowTitle.Content = Properties.Resources.AppName;
                window.Title = Properties.Resources.AppName;
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
            window.Dispatcher.Invoke(() =>
            {
                int count = syncingList.Count + removingList.Count;
                if (count > 0)
                {
                    syncMenuItem.Text = $"待同步 [{count}]";
                    syncMenuItem.Enabled = true;
                }
                else
                {
                    syncMenuItem.Text = "待同步 [0]";
                    syncMenuItem.Enabled = false;
                }

                if (watchingState)
                {
                    notifyIcon.ContextMenu = new System.Windows.Forms.ContextMenu(new[] { syncMenuItem, stopMenuItem, exitMenuItem });
                }
                else
                {
                    notifyIcon.ContextMenu = new System.Windows.Forms.ContextMenu(new[] { syncMenuItem, resumeMenuItem, exitMenuItem });
                }

                if (window.WindowState == WindowState.Normal)
                {
                    BtnSync.Visibility = realTimeSync && count > 0 ? Visibility.Visible : Visibility.Collapsed;
                    TextBlockSync.Text = syncingList.Count > 0 ? $"待同步文件：[{syncingList.Count}]" : "";
                    TextBlockRemove.Text = removingList.Count > 0 ? $"待删除文件：[{removingList.Count}]" : "";
                    TextBlockClear.Text = count > 0 ? "清除..." : "";
                }
            });
        }

        private void InitializeWatcher()
        {
            watcher = new FileSystemWatcher { Filter = "*.tif" };
            watcher.Changed += OnWatcherFileChanged;
            watcher.Created += OnWatcherFileCreated;
            watcher.Deleted += OnWatcherFileDeleted;
            watcher.Renamed += OnWatcherFileRenamed;
            watcher.NotifyFilter = NotifyFilters.Attributes | NotifyFilters.CreationTime | NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size;
            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = false;
        }

        private void Icon_ShowClick(object sender, EventArgs e)
        {
            //StopWatcher();
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
            }
            else if (WindowState == WindowState.Normal)
            {
                int count = syncingList.Count + removingList.Count;
                BtnSync.Visibility = realTimeSync && count > 0 ? Visibility.Visible : Visibility.Collapsed;
                TextBlockSync.Text = syncingList.Count > 0 ? $"待同步文件：[{syncingList.Count}]" : "";
                TextBlockRemove.Text = removingList.Count > 0 ? $"待删除文件：[{removingList.Count}]" : "";
                TextBlockClear.Text = count > 0 ? "清除..." : "";
                PanelList.Visibility = realTimeSync ? Visibility.Collapsed : Visibility.Visible;
                CheckBoxRealTime.IsEnabled = !watchingState;

                if (watchingState)
                {
                    BtnStart.Content = "暂停监控";
                    BtnStart.BorderBrush = new SolidColorBrush(Color.FromRgb(202, 81, 0));
                    string name = $"{Properties.Resources.AppName}  -  正在监控...";
                    WindowTitle.Content = name;
                    window.Title = name;
                }
                else
                {
                    BtnStart.Content = "开始监控";
                    BtnStart.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 122, 204));
                    WindowTitle.Content = Properties.Resources.AppName;
                    window.Title = Properties.Resources.AppName;
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
            string newPath = path.Replace(strWatcherPath, strSyncerPath);
            newPath = Path.ChangeExtension(newPath, ".png");

            if (File.Exists(newPath))
            {
                if (SyncFile(path, strWatcherPath, strSyncerPath))
                    ShowTrayMessage($"修改文件：\n{newPath}");
                return;
            }
            
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
                if (SyncFile(path, strWatcherPath, strSyncerPath))
                    ShowTrayMessage($"创建文件：\n{newPath}");
                return;
            }

            if (newPath != path)
            {
                newPath = Path.ChangeExtension(newPath, ".png");
                if (File.Exists(newPath) && !removingList.Contains(newPath))
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
                    ShowTrayMessage($"删除文件：\n{newPath}");
                }
                if (File.Exists(newPath + ".meta"))
                {
                    FileSystem.DeleteFile(newPath + ".meta", UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                }
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

        private bool SyncFile(string filePath, string srcPath, string destPath)
        {
            if (!File.Exists(filePath))
                return false;

            if (!Directory.Exists(srcPath))
                return false;

            if (!Directory.Exists(destPath))
                return false;

            string ext = Path.GetExtension(filePath);
            if (ext != ".tif")
                return false;

            string newPath = filePath.Replace(srcPath, destPath);
            bool deleteOriginal = false;
            if (newPath != filePath)
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

            if (watchingState && realTimeSync)
            {
                int time = 0;
                while (time <= 30000)
                {
                    try
                    {
                        using (StreamReader stream = new StreamReader(filePath))
                        {
                            Bitmap bitmap = new Bitmap(stream.BaseStream);
                            ProcessImage(bitmap, newPath);
                            bitmap.Dispose();
                            stream.Close();
                            stream.Dispose();
                            break;
                        }
                    }
                    catch
                    {
                        Thread.Sleep(100);
                        time += 100;
                    }
                }

                if (time >= 3000)
                {
                    ShowTrayMessage($"同步文件超时：\n{filePath}");
                    return false;
                }
            }
            else
            {
                Bitmap bitmap = new Bitmap(filePath);
                ProcessImage(bitmap, newPath);
                bitmap.Dispose();
            }

            if (deleteOriginal)
            {
                if (File.Exists(filePath))
                {
                    FileSystem.DeleteFile(filePath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                }
            }

            return true;
        }

        private void ProcessImage(Bitmap bitmap, string savePath)
        {
            EncoderParameters eps = new EncoderParameters(1);
            if (bitmap.PixelFormat == PixelFormat.Format32bppArgb)
            {
                eps.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.ColorDepth, 32L);
                bitmap.Save(savePath, GetEncoderInfo("image/png"), eps);
            }
            else if (bitmap.PixelFormat == PixelFormat.Format24bppRgb)
            {
                eps.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.ColorDepth, 24L);
                bitmap.Save(savePath, GetEncoderInfo("image/png"), eps);
            }
            else if (bitmap.PixelFormat == PixelFormat.Format64bppArgb)
            {
                Bitmap bmNew = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb);
                bmNew.SetResolution(bitmap.VerticalResolution, bitmap.VerticalResolution);
                Graphics g = Graphics.FromImage(bmNew);
                g.DrawImage(bitmap, 0, 0);
                g.Dispose();

                eps.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.ColorDepth, 32L);
                bmNew.Save(savePath, GetEncoderInfo("image/png"), eps);
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
                bmNew.Save(savePath, GetEncoderInfo("image/png"), eps);
                bmNew.Dispose();
            }
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
            if (syncingState)
                return;

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
                    //MinimizeWindow();
                }
                else
                {
                    MessageBox.Show("请检查路径。", Properties.Resources.AppName, MessageBoxButton.OK);
                }
            }
        }

        private void BtnSync_Click(object sender, RoutedEventArgs e)
        {
            if (syncingState)
                return;

            syncingState = true;

            ShowProgressBar(0, 1);
            foreach (string path in removingList)
            {
                RemoveFile(path);
            }
            removingList.Clear();

            watcher.EnableRaisingEvents = false;
            for (int i = 0; i < syncingList.Count; i++)
            {
                ShowProgressBar(i + 1, syncingList.Count + 1);
                string path = syncingList[i];
                SyncFile(path, strWatcherPath, strSyncerPath);
            }
            syncingList.Clear();


            HideProgressBar();
            syncingState = false;

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
            if (syncingState)
                return;

            StopWatcher();
            CommonOpenFileDialog dialog = new CommonOpenFileDialog { IsFolderPicker = false, Multiselect = false, Title = "选择原始文件" };
            if (Directory.Exists(strWatcherPath))
            {
                dialog.InitialDirectory = strWatcherPath;
            }
            dialog.Filters.Add(new CommonFileDialogFilter("TIFF 文件", "*.tif"));
            CommonFileDialogResult result = dialog.ShowDialog();
            if (result != CommonFileDialogResult.Ok) return;

            string path = dialog.FileName;
            if (!File.Exists(path)) return;

            CommonOpenFileDialog dialog2 = new CommonOpenFileDialog { IsFolderPicker = true, Multiselect = false, Title = "选择目标文件夹" };
            if (Directory.Exists(strSyncerPath))
            {
                dialog2.InitialDirectory = strSyncerPath;
            }
            CommonFileDialogResult result2 = dialog2.ShowDialog();
            if (result2 != CommonFileDialogResult.Ok) return;
            string path2 = dialog2.FileName;
            if (!Directory.Exists(path2)) return;

            if (Path.GetDirectoryName(path) == path2)
            {
                if (MessageBox.Show("原始文件与同步文件夹相同：\n\n是否转换并删除原始文件？", Properties.Resources.AppName, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                    return;
            }

            syncingState = true;
            if (SyncFile(path, Path.GetDirectoryName(path), path2))
            {
                ShowTrayMessage("已同步选择的文件。");
            }

            syncingState = false;
        }

        private void ManualSyncFolder_Click(object sender, RoutedEventArgs e)
        {
            if (syncingState)
                return;

            StopWatcher();
            CommonOpenFileDialog dialog = new CommonOpenFileDialog { IsFolderPicker = true, Multiselect = false, Title = "选择原始文件夹" };
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
                MessageBox.Show("没有发现 *.tif 文件。", Properties.Resources.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            CommonOpenFileDialog dialog2 = new CommonOpenFileDialog { IsFolderPicker = true, Multiselect = false, Title = "选择目标文件夹" };
            if (Directory.Exists(strSyncerPath))
            {
                dialog2.InitialDirectory = strSyncerPath;
            }
            CommonFileDialogResult result2 = dialog2.ShowDialog();
            if (result2 != CommonFileDialogResult.Ok) return;
            string path2 = dialog.FileName;
            if (!Directory.Exists(path2)) return;

            if (dir == path2)
            {
                if (MessageBox.Show($"原始文件夹与同步文件夹相同：\n{path2}\n，是否转换并删除原始文件？", Properties.Resources.AppName, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                    return;
            }
            else
            {
                if (MessageBox.Show($"发现 {files.Length} 个文件，同步到文件夹：\n{path2}", Properties.Resources.AppName, MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK)
                    return;
            }

            syncingState = true;
            int count = 0;
            for (int i = 0; i < files.Length; i++)
            {
                ShowProgressBar(i + 1, files.Length);
                string p = files[i];
                if (SyncFile(p, dir, path2))
                {
                    count++;
                }
            }

            HideProgressBar();
            ShowTrayMessage($"已同步 {count} 个文件。");
            syncingState = false;
        }

        private void ShowProgressBar(int index, int count)
        {
            ProgressBar.Height = 4;
            double width = index * progressBarWidth / count;
            ProgressBar.Width = width;

            taskbar.SetProgressState(TaskbarProgressBarState.Normal, window);
            taskbar.SetProgressValue(index, count, window);
        }

        private void HideProgressBar()
        {
            ProgressBar.Height = 0;
            taskbar.SetProgressState(TaskbarProgressBarState.NoProgress, window);
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
