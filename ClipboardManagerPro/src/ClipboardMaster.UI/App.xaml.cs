using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using ClipboardMaster.UI.Services;
using ClipboardMaster.UI.ViewModels;
using ClipboardMaster.UI.Views;
using ClipboardMaster.Tray;

namespace ClipboardMaster.UI
{
    public partial class App : Application
    {
        private Window? _mainWindow;
        private IHost? _host;
        private TrayIcon? _trayIcon;
        private ClipboardService? _clipboardService;
        private HotkeyService? _hotkeyService;
        
        public static App? CurrentApp { get; private set; }
        public IServiceProvider? Services => _host?.Services;
        
        public App()
        {
            this.InitializeComponent();
            CurrentApp = this;
            
            // 配置日志
            ConfigureLogging();
            
            // 设置异常处理
            this.UnhandledException += OnUnhandledException;
        }
        
        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            try
            {
                // 创建主机
                _host = CreateHostBuilder().Build();
                
                // 初始化服务
                await InitializeServicesAsync();
                
                // 创建主窗口
                _mainWindow = _host.Services.GetRequiredService<MainWindow>();
                _mainWindow.Activate();
                
                // 创建托盘图标
                CreateTrayIcon();
                
                Log.Information("应用程序启动完成");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "应用程序启动失败");
                ShowErrorMessage("应用程序启动失败", ex.Message);
            }
        }
        
        private IHostBuilder CreateHostBuilder()
        {
            return Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // 注册服务
                    services.AddSingleton<ClipboardService>();
                    services.AddSingleton<HotkeyService>();
                    services.AddSingleton<DatabaseService>();
                    services.AddSingleton<SettingsService>();
                    services.AddSingleton<ThemeService>();
                    
                    // 注册视图模型
                    services.AddSingleton<MainViewModel>();
                    services.AddSingleton<SettingsViewModel>();
                    services.AddSingleton<StatisticsViewModel>();
                    
                    // 注册窗口
                    services.AddTransient<MainWindow>();
                    services.AddTransient<SettingsWindow>();
                    services.AddTransient<StatisticsWindow>();
                    
                    // 注册视图
                    services.AddTransient<ClipboardItemsView>();
                    services.AddTransient<PinnedItemsView>();
                    services.AddTransient<SearchView>();
                });
        }
        
        private async Task InitializeServicesAsync()
        {
            if (_host?.Services == null) return;
            
            // 初始化核心服务
            _clipboardService = _host.Services.GetRequiredService<ClipboardService>();
            _hotkeyService = _host.Services.GetRequiredService<HotkeyService>();
            
            await _clipboardService.InitializeAsync();
            await _hotkeyService.InitializeAsync();
            
            // 加载设置
            var settingsService = _host.Services.GetRequiredService<SettingsService>();
            await settingsService.LoadSettingsAsync();
            
            // 应用主题
            var themeService = _host.Services.GetRequiredService<ThemeService>();
            await themeService.InitializeAsync();
        }
        
        private void CreateTrayIcon()
        {
            if (_mainWindow == null) return;
            
            _trayIcon = new TrayIcon(_mainWindow);
            _trayIcon.Show();
        }
        
        private void ConfigureLogging()
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ClipboardMaster",
                "logs",
                "clipboard-master-.log"
            );
            
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    logPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
                )
                .WriteTo.Debug()
                .CreateLogger();
        }
        
        private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Log.Error(e.Exception, "未处理的异常");
            e.Handled = true;
            
            ShowErrorMessage("应用程序错误", e.Exception.Message);
        }
        
        private void ShowErrorMessage(string title, string message)
        {
            // 在主线程显示错误消息
            _ = _mainWindow?.DispatcherQueue.TryEnqueue(() =>
            {
                var dialog = new ContentDialog
                {
                    Title = title,
                    Content = message,
                    PrimaryButtonText = "确定",
                    XamlRoot = _mainWindow?.Content.XamlRoot
                };
                dialog.ShowAsync();
            });
        }
        
        public void ShowMainWindow()
        {
            _mainWindow?.DispatcherQueue.TryEnqueue(() =>
            {
                if (_mainWindow == null) return;
                
                // 如果窗口最小化则恢复
                var presenter = _mainWindow.AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
                if (presenter != null && presenter.State == Microsoft.UI.Windowing.OverlappedPresenterState.Minimized)
                {
                    presenter.Restore();
                }
                
                // 激活窗口
                _mainWindow.Activate();
                _mainWindow.BringToFront();
            });
        }
        
        public async Task ExitApplicationAsync()
        {
            try
            {
                Log.Information("正在关闭应用程序...");
                
                // 停止服务
                if (_hotkeyService != null)
                    await _hotkeyService.StopAsync();
                
                if (_clipboardService != null)
                    await _clipboardService.StopAsync();
                
                // 清理托盘图标
                _trayIcon?.Dispose();
                
                // 保存设置
                var settingsService = Services?.GetService<SettingsService>();
                if (settingsService != null)
                    await settingsService.SaveSettingsAsync();
                
                Log.Information("应用程序关闭完成");
                
                // 关闭日志
                await Log.CloseAndFlushAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "关闭应用程序时发生错误");
            }
            finally
            {
                Environment.Exit(0);
            }
        }
    }
}