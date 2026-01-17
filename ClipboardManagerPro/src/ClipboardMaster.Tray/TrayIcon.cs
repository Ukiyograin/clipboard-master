using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.UI.Xaml;
using Application = System.Windows.Forms.Application;

namespace ClipboardMaster.Tray
{
    public class TrayIcon : IDisposable
    {
        private NotifyIcon _notifyIcon;
        private Window _mainWindow;
        private bool _disposed;
        
        public TrayIcon(Window mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            InitializeTrayIcon();
        }
        
        private void InitializeTrayIcon()
        {
            // 创建系统托盘图标
            _notifyIcon = new NotifyIcon
            {
                Text = "Clipboard Master",
                Visible = true,
                ContextMenuStrip = CreateContextMenu()
            };
            
            // 设置图标
            SetIcon();
            
            // 注册事件
            _notifyIcon.DoubleClick += OnTrayIconDoubleClick;
            _notifyIcon.MouseClick += OnTrayIconMouseClick;
            _notifyIcon.BalloonTipClicked += OnBalloonTipClicked;
            
            // 显示启动提示
            ShowStartupNotification();
        }
        
        private void SetIcon()
        {
            try
            {
                // 尝试从资源加载图标
                var iconPath = System.IO.Path.Combine(
                    AppContext.BaseDirectory,
                    "Assets",
                    "icon.ico"
                );
                
                if (System.IO.File.Exists(iconPath))
                {
                    using var iconStream = new System.IO.FileStream(iconPath, System.IO.FileMode.Open);
                    _notifyIcon.Icon = new Icon(iconStream);
                }
                else
                {
                    // 创建默认图标
                    using var bitmap = new Bitmap(16, 16);
                    using var graphics = Graphics.FromImage(bitmap);
                    graphics.Clear(Color.FromArgb(0, 120, 215)); // Windows 蓝色
                    graphics.DrawString("CM", new Font("Arial", 8), Brushes.White, 2, 2);
                    
                    using var stream = new System.IO.MemoryStream();
                    bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                    stream.Seek(0, System.IO.SeekOrigin.Begin);
                    _notifyIcon.Icon = new Icon(stream);
                }
            }
            catch
            {
                // 如果加载失败，使用系统默认图标
            }
        }
        
        private ContextMenuStrip CreateContextMenu()
        {
            var menu = new ContextMenuStrip();
            
            // 显示/隐藏
            var showItem = new ToolStripMenuItem("显示主窗口", null, OnShowClicked);
            showItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.V;
            menu.Items.Add(showItem);
            
            menu.Items.Add(new ToolStripSeparator());
            
            // 快速操作
            var captureItem = new ToolStripMenuItem("捕获当前剪贴板", null, OnCaptureClicked);
            captureItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.C;
            menu.Items.Add(captureItem);
            
            var pinItem = new ToolStripMenuItem("固定当前项目", null, OnPinClicked);
            pinItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.P;
            menu.Items.Add(pinItem);
            
            menu.Items.Add(new ToolStripSeparator());
            
            // 设置
            var settingsItem = new ToolStripMenuItem("设置", null, OnSettingsClicked);
            menu.Items.Add(settingsItem);
            
            var statsItem = new ToolStripMenuItem("统计信息", null, OnStatisticsClicked);
            menu.Items.Add(statsItem);
            
            menu.Items.Add(new ToolStripSeparator());
            
            // 工具
            var cleanupItem = new ToolStripMenuItem("清理旧项目", null, OnCleanupClicked);
            menu.Items.Add(cleanupItem);
            
            var exportItem = new ToolStripMenuItem("导出数据", null, OnExportClicked);
            menu.Items.Add(exportItem);
            
            menu.Items.Add(new ToolStripSeparator());
            
            // 帮助
            var helpItem = new ToolStripMenuItem("帮助", null, OnHelpClicked);
            menu.Items.Add(helpItem);
            
            var aboutItem = new ToolStripMenuItem("关于", null, OnAboutClicked);
            menu.Items.Add(aboutItem);
            
            menu.Items.Add(new ToolStripSeparator());
            
            // 退出
            var exitItem = new ToolStripMenuItem("退出", null, OnExitClicked);
            exitItem.ForeColor = Color.DarkRed;
            menu.Items.Add(exitItem);
            
            return menu;
        }
        
        private void ShowStartupNotification()
        {
            _notifyIcon.ShowBalloonTip(
                2000,
                "Clipboard Master",
                "剪贴板管理器已在后台运行\nCtrl+Shift+V 显示窗口",
                ToolTipIcon.Info
            );
        }
        
        public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
        {
            _notifyIcon.ShowBalloonTip(3000, title, message, icon);
        }
        
        public void UpdateIcon(bool hasNewItems)
        {
            // 如果有新项目，可以改变图标颜色或添加提示
            if (hasNewItems)
            {
                // 这里可以更新图标为有通知的版本
            }
        }
        
        #region 事件处理
        
        private void OnTrayIconDoubleClick(object sender, EventArgs e)
        {
            ShowMainWindow();
        }
        
        private void OnTrayIconMouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                // 右键点击时更新菜单状态
                UpdateMenuItems();
            }
        }
        
        private void OnBalloonTipClicked(object sender, EventArgs e)
        {
            ShowMainWindow();
        }
        
        private void OnShowClicked(object sender, EventArgs e)
        {
            ShowMainWindow();
        }
        
        private void OnCaptureClicked(object sender, EventArgs e)
        {
            // 触发捕获当前剪贴板
            // 需要通过事件或服务调用
        }
        
        private void OnPinClicked(object sender, EventArgs e)
        {
            // 触发固定当前项目
            // 需要通过事件或服务调用
        }
        
        private void OnSettingsClicked(object sender, EventArgs e)
        {
            // 打开设置窗口
            _ = _mainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                // 创建并显示设置窗口
            });
        }
        
        private void OnStatisticsClicked(object sender, EventArgs e)
        {
            // 打开统计窗口
            _ = _mainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                // 创建并显示统计窗口
            });
        }
        
        private async void OnCleanupClicked(object sender, EventArgs e)
        {
            // 执行清理
            await _mainWindow.DispatcherQueue.TryEnqueue(async () =>
            {
                // 调用清理服务
            });
        }
        
        private async void OnExportClicked(object sender, EventArgs e)
        {
            // 导出数据
            await _mainWindow.DispatcherQueue.TryEnqueue(async () =>
            {
                // 调用导出服务
            });
        }
        
        private void OnHelpClicked(object sender, EventArgs e)
        {
            // 打开帮助文档
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/Ukiyograin/clipboard-master/",
                UseShellExecute = true
            });
        }
        
        private void OnAboutClicked(object sender, EventArgs e)
        {
            // 显示关于对话框
            _ = _mainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                // 创建并显示关于对话框
            });
        }
        
        private async void OnExitClicked(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "确定要退出 Clipboard Master 吗？",
                "确认退出",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );
            
            if (result == DialogResult.Yes)
            {
                await _mainWindow.DispatcherQueue.TryEnqueue(async () =>
                {
                    await (App.CurrentApp?.ExitApplicationAsync() ?? Task.CompletedTask);
                });
            }
        }
        
        #endregion
        
        #region 辅助方法
        
        private void ShowMainWindow()
        {
            _ = _mainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                if (_mainWindow.Visible)
                {
                    _mainWindow.Activate();
                    _mainWindow.BringToFront();
                }
                else
                {
                    _mainWindow.Show();
                    _mainWindow.Activate();
                }
            });
        }
        
        private void UpdateMenuItems()
        {
            // 更新菜单项状态
            // 例如：根据当前状态启用/禁用某些项目
        }
        
        public void Show()
        {
            _notifyIcon.Visible = true;
        }
        
        public void Hide()
        {
            _notifyIcon.Visible = false;
        }
        
        #endregion
        
        #region IDisposable 实现
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _notifyIcon?.Dispose();
                }
                
                _disposed = true;
            }
        }
        
        ~TrayIcon()
        {
            Dispose(false);
        }
        
        #endregion
    }
}