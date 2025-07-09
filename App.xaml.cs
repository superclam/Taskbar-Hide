using System;
using System.Threading;
using System.Windows;

namespace SmartTaskbarHider
{
    public partial class App : Application
    {
        private static Mutex mutex = null;
        private const string MUTEX_NAME = "SmartTaskbarHider_SingleInstance_Mutex";

        protected override void OnStartup(StartupEventArgs e)
        {
            // 尝试创建互斥锁
            bool createdNew;
            mutex = new Mutex(true, MUTEX_NAME, out createdNew);

            if (!createdNew)
            {
                // 如果互斥锁已存在，说明程序已经在运行
                MessageBox.Show("任务栏隐藏工具已经在运行中！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);

                // 退出当前实例
                Current.Shutdown();
                return;
            }

            // 如果是第一个实例，正常启动
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // 释放互斥锁
            if (mutex != null)
            {
                mutex.ReleaseMutex();
                mutex.Dispose();
            }

            base.OnExit(e);
        }
    }
}
