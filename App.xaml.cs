using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;

namespace YesChefTiffWatcher
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        void App_DispatcherUnhandledException(object sender,
            System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"{e.Exception.Message}{Environment.NewLine}{e.Exception.StackTrace}", "Error");
            e.Handled = true;
        }


        [DllImport("user32")]
        private static extern int SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        protected override void OnStartup(StartupEventArgs e)
        {
            IntPtr parenthWnd = FindWindow(null, Assembly.GetExecutingAssembly().GetName().Name);
            if (parenthWnd != IntPtr.Zero)
            {
                //选中当前的句柄窗口
                SetForegroundWindow(parenthWnd);
                Application.Current.Shutdown();
                return;
            }
            base.OnStartup(e);
        }
    }
}
