using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Reflection;
using System.IO;
using System.Diagnostics;

namespace SmartTaskbarHider
{
    public partial class MainWindow : Window
    {
        private TaskbarManager taskbarManager;
        private NotifyIcon notifyIcon;
        private KeyboardHook keyboardHook;
        private const string REGISTRY_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string APP_NAME = "任务栏隐藏工具";

        public MainWindow()
        {
            InitializeComponent();
            taskbarManager = new TaskbarManager();

            // 创建系统托盘图标
            CreateNotifyIcon();

            // 初始化键盘钩子
            InitializeKeyboardHook();

            // 程序启动时隐藏任务栏并扩展工作区域
            taskbarManager.HideTaskbarAndExpandWorkArea();

            // 隐藏主窗口
            this.WindowState = WindowState.Minimized;
            this.ShowInTaskbar = false;
            this.Visibility = Visibility.Hidden;
        }

        private void CreateNotifyIcon()
        {
            notifyIcon = new NotifyIcon();

            // 使用自定义图标
            notifyIcon.Icon = LoadIcon();

            // 设置托盘图标属性和菜单
            SetupNotifyIconProperties();
        }

        private System.Drawing.Icon LoadIcon()
        {
            try
            {
                // 方法1: 尝试从嵌入资源加载图标
                Assembly assembly = Assembly.GetExecutingAssembly();
                using (Stream stream = assembly.GetManifestResourceStream("SmartTaskbarHider.icon.ico"))
                {
                    if (stream != null)
                    {
                        return new System.Drawing.Icon(stream);
                    }
                }
            }
            catch { }

            try
            {
                // 方法2: 尝试从程序所在目录加载图标
                string exeDir = AppContext.BaseDirectory;
                string iconPath = Path.Combine(exeDir, "icon.ico");
                if (File.Exists(iconPath))
                {
                    return new System.Drawing.Icon(iconPath);
                }
            }
            catch { }

            try
            {
                // 方法3: 尝试从当前工作目录加载图标
                string iconPath = Path.Combine(Directory.GetCurrentDirectory(), "icon.ico");
                if (File.Exists(iconPath))
                {
                    return new System.Drawing.Icon(iconPath);
                }
            }
            catch { }

            // 如果所有方法都失败，使用系统默认图标
            return SystemIcons.Application;
        }

        private void SetupNotifyIconProperties()
        {
            notifyIcon.Text = "任务栏隐藏工具";
            notifyIcon.Visible = true;

            // 创建右键菜单
            ContextMenuStrip contextMenu = new ContextMenuStrip();

            // 快捷显示提示菜单项
            ToolStripMenuItem shortcutHintItem = new ToolStripMenuItem("快捷显示——ctrl+alt");
            shortcutHintItem.Click += (s, e) => { }; // 空的点击事件，保持可点击状态但不执行任何操作
            contextMenu.Items.Add(shortcutHintItem);

            // 分隔线
            contextMenu.Items.Add(new ToolStripSeparator());

            // 开机自启动菜单项
            ToolStripMenuItem autoStartItem = new ToolStripMenuItem();
            UpdateAutoStartMenuItem(autoStartItem);
            autoStartItem.Click += (s, e) => ToggleAutoStart(autoStartItem);
            contextMenu.Items.Add(autoStartItem);

            // 分隔线
            contextMenu.Items.Add(new ToolStripSeparator());

            // 退出菜单项
            ToolStripMenuItem exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += (s, e) => ExitApplication();
            contextMenu.Items.Add(exitItem);

            notifyIcon.ContextMenuStrip = contextMenu;
        }

        private void InitializeKeyboardHook()
        {
            try
            {
                keyboardHook = new KeyboardHook();
                keyboardHook.ShiftEnterPressed += OnShiftEnterPressed;
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"键盘钩子初始化失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnShiftEnterPressed()
        {
            // 切换任务栏显示状态
            taskbarManager.ToggleTaskbarVisibility();
        }

        private void ExitApplication()
        {
            // 程序退出时恢复任务栏和工作区域
            taskbarManager.RestoreTaskbarAndWorkArea();
            keyboardHook?.Dispose();
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            System.Windows.Application.Current.Shutdown();
        }

        protected override void OnClosed(EventArgs e)
        {
            // 确保程序关闭时恢复任务栏
            taskbarManager?.RestoreTaskbarAndWorkArea();
            keyboardHook?.Dispose();
            notifyIcon?.Dispose();
            base.OnClosed(e);
        }

        private bool IsAutoStartEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY, false))
                {
                    return key?.GetValue(APP_NAME) != null;
                }
            }
            catch
            {
                return false;
            }
        }

        private void SetAutoStart(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY, true))
                {
                    if (enable)
                    {
                        // 获取当前执行文件的路径
                        string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                        key?.SetValue(APP_NAME, $"\"{exePath}\"");
                    }
                    else
                    {
                        key?.DeleteValue(APP_NAME, false);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"设置开机自启动失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateAutoStartMenuItem(ToolStripMenuItem menuItem)
        {
            bool isEnabled = IsAutoStartEnabled();
            menuItem.Text = "开机自启动";
            menuItem.Checked = isEnabled; // 使用系统的勾选标记
        }

        private void ToggleAutoStart(ToolStripMenuItem menuItem)
        {
            bool currentState = IsAutoStartEnabled();
            SetAutoStart(!currentState);
            UpdateAutoStartMenuItem(menuItem);
        }
    }

    public class TaskbarManager
    {
        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public System.Drawing.Point ptMinPosition;
            public System.Drawing.Point ptMaxPosition;
            public RECT rcNormalPosition;
        }

        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SPI_SETWORKAREA = 0x002F;
        private const uint SPI_GETWORKAREA = 0x0030;
        private const int SW_RESTORE = 9;
        private const int SW_MAXIMIZE = 3;
        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        private RECT originalWorkArea;
        private bool isTaskbarHidden = false;
        private bool isWorkAreaExpanded = false;
        private bool isTaskbarHiddenInDesktop = false; // 桌面模式下任务栏是否被隐藏

        public TaskbarManager()
        {
            // 保存原始工作区域
            SaveOriginalWorkArea();
        }

        private void SaveOriginalWorkArea()
        {
            IntPtr rectPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(RECT)));
            SystemParametersInfo(SPI_GETWORKAREA, 0, rectPtr, 0);
            originalWorkArea = (RECT)Marshal.PtrToStructure(rectPtr, typeof(RECT));
            Marshal.FreeHGlobal(rectPtr);
        }

        public void HideTaskbarAndExpandWorkArea()
        {
            if (isTaskbarHidden) return;

            try
            {
                IntPtr taskbarHandle = FindWindow("Shell_TrayWnd", null);
                if (taskbarHandle != IntPtr.Zero)
                {
                    // 将任务栏置于最底层（只在程序启动时设置一次）
                    SetWindowPos(taskbarHandle, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

                    // 扩展工作区域到全屏
                    ExpandWorkArea();

                    isTaskbarHidden = true;
                    isWorkAreaExpanded = true;
                }
            }
            catch
            {
                // 如果出错，不做任何操作
            }
        }

        private void ExpandWorkArea()
        {
            try
            {
                // 获取屏幕尺寸
                int screenWidth = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width;
                int screenHeight = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height;

                // 扩展工作区域到全屏
                RECT fullScreenRect = new RECT
                {
                    Left = 0,
                    Top = 0,
                    Right = screenWidth,
                    Bottom = screenHeight
                };

                IntPtr rectPtr = Marshal.AllocHGlobal(Marshal.SizeOf(fullScreenRect));
                Marshal.StructureToPtr(fullScreenRect, rectPtr, false);
                SystemParametersInfo(SPI_SETWORKAREA, 0, rectPtr, 0);
                Marshal.FreeHGlobal(rectPtr);

                // 刷新桌面以确保更改生效
                SystemParametersInfo(0x0014, 0, IntPtr.Zero, 0); // SPI_SETDESKWALLPAPER

                // 刷新所有最大化窗口以适应新的工作区域
                RefreshMaximizedWindows();

                isWorkAreaExpanded = true;
            }
            catch
            {
                // 如果出错，不做任何操作
            }
        }

        public void RestoreTaskbarAndWorkArea()
        {
            try
            {
                IntPtr taskbarHandle = FindWindow("Shell_TrayWnd", null);
                if (taskbarHandle != IntPtr.Zero)
                {
                    // 如果任务栏在桌面模式下被隐藏，先显示它
                    if (isTaskbarHiddenInDesktop)
                    {
                        ShowWindow(taskbarHandle, SW_SHOW);
                        isTaskbarHiddenInDesktop = false;
                    }

                    // 如果任务栏层级被修改，恢复到正常层级
                    if (isTaskbarHidden)
                    {
                        // 恢复任务栏到正常层级 - 先设为topmost再设为notopmost
                        SetWindowPos(taskbarHandle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                        System.Threading.Thread.Sleep(10); // 短暂延迟
                        SetWindowPos(taskbarHandle, new IntPtr(-2), 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                        isTaskbarHidden = false;
                    }

                    // 恢复原始工作区域
                    if (isWorkAreaExpanded)
                    {
                        RestoreWorkArea();
                    }
                }
            }
            catch
            {
                // 如果出错，尝试基本恢复
                try
                {
                    RestoreWorkArea();
                }
                catch { }
            }
        }

        private void RestoreWorkArea()
        {
            try
            {
                // 恢复原始工作区域
                IntPtr rectPtr = Marshal.AllocHGlobal(Marshal.SizeOf(originalWorkArea));
                Marshal.StructureToPtr(originalWorkArea, rectPtr, false);
                SystemParametersInfo(SPI_SETWORKAREA, 0, rectPtr, 0);
                Marshal.FreeHGlobal(rectPtr);

                // 刷新桌面以确保更改生效
                SystemParametersInfo(0x0014, 0, IntPtr.Zero, 0); // SPI_SETDESKWALLPAPER

                // 刷新所有最大化窗口以适应新的工作区域
                RefreshMaximizedWindows();

                isWorkAreaExpanded = false;
            }
            catch
            {
                // 如果出错，不做任何操作
            }
        }

        public void ToggleTaskbarVisibility()
        {
            // 检查是否有最大化窗口
            if (HasMaximizedWindow())
            {
                // 有最大化窗口时，只切换工作区域，不改变任务栏层级
                if (isWorkAreaExpanded)
                {
                    // 恢复工作区域到正常大小
                    RestoreWorkArea();
                }
                else
                {
                    // 扩展工作区域到全屏
                    ExpandWorkArea();
                }
            }
            else
            {
                // 桌面状态下，直接隐藏/显示任务栏
                ToggleTaskbarInDesktop();
            }
        }

        private bool HasMaximizedWindow()
        {
            bool hasMaximized = false;
            EnumWindows((hWnd, lParam) =>
            {
                if (IsWindowVisible(hWnd) && IsZoomed(hWnd))
                {
                    // 排除任务栏本身
                    IntPtr taskbarHandle = FindWindow("Shell_TrayWnd", null);
                    if (hWnd != taskbarHandle)
                    {
                        hasMaximized = true;
                        return false; // 停止枚举
                    }
                }
                return true; // 继续枚举
            }, IntPtr.Zero);

            return hasMaximized;
        }

        private void ToggleTaskbarInDesktop()
        {
            try
            {
                IntPtr taskbarHandle = FindWindow("Shell_TrayWnd", null);
                if (taskbarHandle != IntPtr.Zero)
                {
                    if (isTaskbarHiddenInDesktop)
                    {
                        // 显示任务栏
                        ShowWindow(taskbarHandle, SW_SHOW);
                        isTaskbarHiddenInDesktop = false;
                    }
                    else
                    {
                        // 隐藏任务栏
                        ShowWindow(taskbarHandle, SW_HIDE);
                        isTaskbarHiddenInDesktop = true;
                    }
                }
            }
            catch
            {
                // 如果出错，不做任何操作
            }
        }

        private void RefreshMaximizedWindows()
        {
            try
            {
                EnumWindows(RefreshWindowCallback, IntPtr.Zero);
            }
            catch
            {
                // 如果出错，不做任何操作
            }
        }

        private static bool RefreshWindowCallback(IntPtr hWnd, IntPtr lParam)
        {
            try
            {
                // 检查窗口是否可见且最大化
                if (IsWindowVisible(hWnd) && IsZoomed(hWnd))
                {
                    // 先恢复窗口，再最大化，这样会强制窗口重新计算大小
                    ShowWindow(hWnd, SW_RESTORE);
                    System.Threading.Thread.Sleep(10); // 短暂延迟
                    ShowWindow(hWnd, SW_MAXIMIZE);
                }
            }
            catch
            {
                // 忽略错误，继续处理下一个窗口
            }

            return true; // 继续枚举
        }
    }

    public class KeyboardHook : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int VK_LSHIFT = 0xA0;
        private const int VK_RSHIFT = 0xA1;
        private const int VK_SHIFT = 0x10;
        private const int VK_LCONTROL = 0xA2;
        private const int VK_LALT = 0xA4;
        private const int VK_RETURN = 0x0D;
        private const int VK_1 = 0x31;

        private LowLevelKeyboardProc _proc = HookCallback;
        private IntPtr _hookID = IntPtr.Zero;
        private static bool _leftControlPressed = false;
        private static bool _leftAltPressed = false;
        private static bool _otherKeyPressed = false;
        private static bool _rightShiftPressed = false;
        private static KeyboardHook _instance;

        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        public event Action ShiftEnterPressed;

        public KeyboardHook()
        {
            _instance = this;
            _hookID = SetHook(_proc);

            // 检查钩子是否成功安装
            if (_hookID == IntPtr.Zero)
            {
                throw new Exception("无法安装键盘钩子");
            }
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);

                if (wParam == (IntPtr)WM_KEYDOWN)
                {
                    if (vkCode == VK_LCONTROL)
                    {
                        _leftControlPressed = true;
                    }
                    else if (vkCode == VK_LALT)
                    {
                        _leftAltPressed = true;
                    }
                    else if (_leftControlPressed && _leftAltPressed && !_otherKeyPressed)
                    {
                        // Ctrl+Alt 组合键被按下，触发事件
                        _instance?.ShiftEnterPressed?.Invoke();
                        _otherKeyPressed = true; // 防止重复触发
                    }
                    else if (vkCode != VK_LCONTROL && vkCode != VK_LALT)
                    {
                        _otherKeyPressed = true;
                    }
                }
                else if (wParam == (IntPtr)WM_KEYUP)
                {
                    if (vkCode == VK_LCONTROL)
                    {
                        _leftControlPressed = false;
                        _otherKeyPressed = false;
                    }
                    else if (vkCode == VK_LALT)
                    {
                        _leftAltPressed = false;
                        _otherKeyPressed = false;
                    }
                }
            }

            return CallNextHookEx(_instance._hookID, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook,
            LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}
