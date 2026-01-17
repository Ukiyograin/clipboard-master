using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI.Core;
using ClipboardMaster.UI.Models;
using ClipboardMaster.UI.Services;
using ClipboardMaster.UI.ViewModels;
using ClipboardMaster.UI.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Microsoft.UI.Xaml.Media.Animation;
using CommunityToolkit.WinUI.UI.Controls;

namespace ClipboardMaster.UI.Views
{
    public sealed partial class MainWindow : Window
    {
        private MainViewModel _viewModel;
        private ClipboardService _clipboardService;
        private HotkeyService _hotkeyService;
        private SettingsService _settingsService;
        private bool _isClosing;
        private DispatcherTimer _refreshTimer;
        private DispatcherTimer _statsUpdateTimer;
        private Storyboard _showDetailAnimation;
        private Storyboard _hideDetailAnimation;
        
        public MainViewModel ViewModel => _viewModel;
        
        public MainWindow(
            MainViewModel viewModel,
            ClipboardService clipboardService,
            HotkeyService hotkeyService,
            SettingsService settingsService)
        {
            this.InitializeComponent();
            
            _viewModel = viewModel;
            _clipboardService = clipboardService;
            _hotkeyService = hotkeyService;
            _settingsService = settingsService;
            
            // 设置窗口属性
            SetWindowProperties();
            
            // 初始化事件
            InitializeEvents();
            
            // 初始化定时器
            InitializeTimers();
            
            // 初始化动画
            InitializeAnimations();
            
            // 加载数据
            LoadDataAsync();
        }
        
        private void SetWindowProperties()
        {
            // 设置窗口大小和位置
            this.AppWindow.Resize(new Windows.Graphics.SizeInt32(900, 650));
            this.AppWindow.MoveInZOrderAtTop();
            
            // 设置标题栏
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(AppTitleBar);
            
            // 设置窗口图标
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
            if (File.Exists(iconPath))
            {
                this.AppWindow.SetIcon(iconPath);
            }
            
            // 设置最小化到托盘
            this.Closed += OnWindowClosed;
        }
        
        private void InitializeEvents()
        {
            // 订阅剪贴板事件
            _clipboardService.ItemAdded += OnClipboardItemAdded;
            _clipboardService.ItemUpdated += OnClipboardItemUpdated;
            _clipboardService.ItemRemoved += OnClipboardItemRemoved;
            
            // 订阅热键事件
            _hotkeyService.HotkeyPressed += OnHotkeyPressed;
            
            // 窗口事件
            this.Activated += OnWindowActivated;
            this.VisibilityChanged += OnVisibilityChanged;
            
            // 键盘事件
            this.KeyDown += OnWindowKeyDown;
            
            // 鼠标事件
            this.PointerPressed += OnWindowPointerPressed;
        }
        
        private void InitializeTimers()
        {
            // 刷新定时器
            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(5);
            _refreshTimer.Tick += OnRefreshTimerTick;
            _refreshTimer.Start();
            
            // 统计信息更新定时器
            _statsUpdateTimer = new DispatcherTimer();
            _statsUpdateTimer.Interval = TimeSpan.FromSeconds(10);
            _statsUpdateTimer.Tick += OnStatsUpdateTimerTick;
            _statsUpdateTimer.Start();
        }
        
        private void InitializeAnimations()
        {
            // 显示详情面板动画
            _showDetailAnimation = new Storyboard();
            var showAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(showAnimation, DetailPanel);
            Storyboard.SetTargetProperty(showAnimation, "Opacity");
            _showDetailAnimation.Children.Add(showAnimation);
            
            // 隐藏详情面板动画
            _hideDetailAnimation = new Storyboard();
            var hideAnimation = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(hideAnimation, DetailPanel);
            Storyboard.SetTargetProperty(hideAnimation, "Opacity");
            _hideDetailAnimation.Children.Add(hideAnimation);
            _hideDetailAnimation.Completed += (s, e) => DetailPanel.Visibility = Visibility.Collapsed;
        }
        
        private async void LoadDataAsync()
        {
            try
            {
                ShowLoading(true);
                await _viewModel.LoadItemsAsync();
                await UpdateStatisticsAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载数据失败: {ex.Message}");
            }
            finally
            {
                ShowLoading(false);
            }
        }
        
        private async Task UpdateStatisticsAsync()
        {
            try
            {
                var stats = await _clipboardService.GetStatisticsAsync();
                await DispatcherQueue.TryEnqueue(() =>
                {
                    StatsText.Text = $"项目总数: {stats.TotalItems}\n"
                                   + $"收藏: {stats.FavoriteItems}\n"
                                   + $"固定: {stats.PinnedItems}\n"
                                   + $"数据库: {(stats.DatabaseSizeBytes / 1024.0 / 1024.0):F2} MB";
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"更新统计信息失败: {ex.Message}");
            }
        }
        
        #region 事件处理
        
        private async void OnClipboardItemAdded(object sender, ClipboardItemEventArgs e)
        {
            await DispatcherQueue.TryEnqueue(async () =>
            {
                await _viewModel.AddItemAsync(e.Item);
                
                // 播放提示音
                if (_settingsService.Settings.PlaySound)
                {
                    PlayCaptureSound();
                }
                
                // 显示通知
                if (_settingsService.Settings.ShowNotification)
                {
                    ShowNotification("剪贴板已捕获", e.Item.PreviewText);
                }
            });
        }
        
        private async void OnClipboardItemUpdated(object sender, ClipboardItemEventArgs e)
        {
            await DispatcherQueue.TryEnqueue(async () =>
            {
                await _viewModel.UpdateItemAsync(e.Item);
            });
        }
        
        private async void OnClipboardItemRemoved(object sender, ClipboardItemEventArgs e)
        {
            await DispatcherQueue.TryEnqueue(async () =>
            {
                await _viewModel.RemoveItemAsync(e.ItemId);
            });
        }
        
        private void OnHotkeyPressed(object sender, HotkeyEventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                switch (e.HotkeyId)
                {
                    case "ShowWindow":
                        ToggleWindowVisibility();
                        break;
                        
                    case "PinItem":
                        PinCurrentItem();
                        break;
                        
                    case "Search":
                        FocusSearchBox();
                        break;
                        
                    case "NextItem":
                        SelectNextItem();
                        break;
                        
                    case "PrevItem":
                        SelectPreviousItem();
                        break;
                }
            });
        }
        
        private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState != WindowActivationState.Deactivated)
            {
                // 窗口激活时更新数据
                if (AutoRefreshToggle?.IsChecked == true)
                {
                    RefreshItemsAsync();
                }
            }
        }
        
        private void OnVisibilityChanged(object sender, WindowVisibilityChangedEventArgs args)
        {
            if (!args.Visible)
            {
                // 窗口隐藏时停止自动刷新
                _refreshTimer.Stop();
            }
            else
            {
                // 窗口显示时恢复自动刷新
                if (AutoRefreshToggle?.IsChecked == true)
                {
                    _refreshTimer.Start();
                }
            }
        }
        
        private void OnWindowKeyDown(object sender, KeyRoutedEventArgs e)
        {
            // 全局快捷键
            var modifiers = GetCurrentModifiers();
            
            if (modifiers == VirtualKeyModifiers.Control && e.Key == VirtualKey.F)
            {
                FocusSearchBox();
                e.Handled = true;
            }
            else if (modifiers == VirtualKeyModifiers.Control && e.Key == VirtualKey.W)
            {
                HideWindow();
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Escape)
            {
                if (DetailPanel.Visibility == Visibility.Visible)
                {
                    HideDetailPanel();
                }
                else
                {
                    HideWindow();
                }
                e.Handled = true;
            }
        }
        
        private void OnWindowPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // 点击窗口外部时隐藏详情面板
            var point = e.GetCurrentPoint(this);
            if (!DetailPanel.IsMouseOver && point.Position.X > DetailPanel.ActualWidth)
            {
                HideDetailPanel();
            }
        }
        
        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            if (!_isClosing)
            {
                args.Handled = true;
                HideWindow();
            }
        }
        
        #endregion
        
        #region UI 交互方法
        
        private void ToggleWindowVisibility()
        {
            if (this.Visible)
            {
                HideWindow();
            }
            else
            {
                ShowWindow();
            }
        }
        
        private void ShowWindow()
        {
            this.Show();
            this.Activate();
            this.BringToFront();
            
            // 恢复刷新定时器
            if (AutoRefreshToggle?.IsChecked == true)
            {
                _refreshTimer.Start();
            }
        }
        
        private void HideWindow()
        {
            this.Hide();
            _refreshTimer.Stop();
        }
        
        private void FocusSearchBox()
        {
            var searchBox = FindName("SearchBox") as AutoSuggestBox;
            searchBox?.Focus(FocusState.Programmatic);
        }
        
        private void SelectNextItem()
        {
            if (ItemsListView.Items.Count > 0)
            {
                var currentIndex = ItemsListView.SelectedIndex;
                var nextIndex = currentIndex < ItemsListView.Items.Count - 1 ? currentIndex + 1 : 0;
                ItemsListView.SelectedIndex = nextIndex;
                ItemsListView.ScrollIntoView(ItemsListView.SelectedItem);
            }
        }
        
        private void SelectPreviousItem()
        {
            if (ItemsListView.Items.Count > 0)
            {
                var currentIndex = ItemsListView.SelectedIndex;
                var prevIndex = currentIndex > 0 ? currentIndex - 1 : ItemsListView.Items.Count - 1;
                ItemsListView.SelectedIndex = prevIndex;
                ItemsListView.ScrollIntoView(ItemsListView.SelectedItem);
            }
        }
        
        private void PinCurrentItem()
        {
            if (_viewModel.SelectedItem != null)
            {
                _viewModel.SelectedItem.IsPinned = !_viewModel.SelectedItem.IsPinned;
                _clipboardService.UpdateItemAsync(_viewModel.SelectedItem.ToClipboardItem());
            }
        }
        
        private void ShowDetailPanel()
        {
            if (DetailPanel.Visibility != Visibility.Visible)
            {
                DetailPanel.Visibility = Visibility.Visible;
                _showDetailAnimation.Begin();
            }
        }
        
        private void HideDetailPanel()
        {
            if (DetailPanel.Visibility == Visibility.Visible)
            {
                _hideDetailAnimation.Begin();
            }
        }
        
        private void ShowLoading(bool show)
        {
            LoadingOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }
        
        private async void PlayCaptureSound()
        {
            try
            {
                var soundPath = Path.Combine(AppContext.BaseDirectory, "Assets", "sounds", "capture.wav");
                if (File.Exists(soundPath))
                {
                    // 使用MediaPlayer播放声音
                    var player = new MediaPlayer();
                    player.Source = MediaSource.CreateFromUri(new Uri(soundPath));
                    player.Volume = 0.3;
                    player.Play();
                    
                    // 播放完成后清理
                    player.MediaEnded += (s, e) => player.Dispose();
                }
            }
            catch
            {
                // 忽略播放错误
            }
        }
        
        private void ShowNotification(string title, string message)
        {
            // 创建简单的通知
            var notification = new InfoBar
            {
                Title = title,
                Message = message.Length > 100 ? message.Substring(0, 100) + "..." : message,
                Severity = InfoBarSeverity.Success,
                IsOpen = true,
                Margin = new Thickness(0, 0, 0, 10)
            };
            
            // 添加到通知区域
            var notificationArea = FindName("NotificationArea") as StackPanel;
            notificationArea?.Children.Insert(0, notification);
            
            // 3秒后自动关闭
            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(3);
            timer.Tick += (s, e) =>
            {
                notification.IsOpen = false;
                timer.Stop();
                notificationArea?.Children.Remove(notification);
            };
            timer.Start();
        }
        
        #endregion
        
        #region 按钮点击事件
        
        private async void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            await RefreshItemsAsync();
        }
        
        private async void OnCopySelectedClick(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedItem != null)
            {
                await _clipboardService.CopyToClipboardAsync(_viewModel.SelectedItem.ToClipboardItem());
            }
        }
        
        private async void OnDeleteSelectedClick(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedItem != null)
            {
                var result = await ShowConfirmDialog("确认删除", "确定要删除选中的项目吗？");
                if (result)
                {
                    await _clipboardService.DeleteItemAsync(_viewModel.SelectedItem.Id);
                }
            }
        }
        
        private async void OnFavoriteClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ClipboardItemViewModel item)
            {
                item.IsFavorite = !item.IsFavorite;
                await _clipboardService.UpdateItemAsync(item.ToClipboardItem());
            }
        }
        
        private async void OnPinClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ClipboardItemViewModel item)
            {
                item.IsPinned = !item.IsPinned;
                await _clipboardService.UpdateItemAsync(item.ToClipboardItem());
            }
        }
        
        private async void OnCopyClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ClipboardItemViewModel item)
            {
                await _clipboardService.CopyToClipboardAsync(item.ToClipboardItem());
            }
        }
        
        private void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow();
            settingsWindow.Activate();
        }
        
        private void OnStatisticsClick(object sender, RoutedEventArgs e)
        {
            var statsWindow = new StatisticsWindow();
            statsWindow.Activate();
        }
        
        private void OnMinimizeClick(object sender, RoutedEventArgs e)
        {
            this.Minimize();
        }
        
        private async void OnCloseClick(object sender, RoutedEventArgs e)
        {
            var result = await ShowConfirmDialog("退出程序", "确定要退出 Clipboard Master 吗？");
            if (result)
            {
                _isClosing = true;
                await App.CurrentApp?.ExitApplicationAsync();
            }
        }
        
        private async void OnCleanupClick(object sender, RoutedEventArgs e)
        {
            var result = await ShowConfirmDialog("清理确认", "确定要清理30天前的非收藏/固定项目吗？");
            if (result)
            {
                ShowLoading(true);
                try
                {
                    var count = await _clipboardService.CleanupOldItemsAsync(30);
                    await ShowMessageDialog("清理完成", $"已清理 {count} 个旧项目");
                    await RefreshItemsAsync();
                }
                catch (Exception ex)
                {
                    await ShowMessageDialog("清理失败", ex.Message);
                }
                finally
                {
                    ShowLoading(false);
                }
            }
        }
        
        private async void OnExportClick(object sender, RoutedEventArgs e)
        {
            var picker = new FileSavePicker();
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeChoices.Add("JSON 文件", new List<string> { ".json" });
            picker.FileTypeChoices.Add("CSV 文件", new List<string> { ".csv" });
            picker.SuggestedFileName = $"clipboard-export-{DateTime.Now:yyyyMMdd-HHmmss}";
            
            var hwnd = WinRT.Interop.WindowHelper.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            
            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                ShowLoading(true);
                try
                {
                    await _clipboardService.ExportItemsAsync(file.Path, "json");
                    await ShowMessageDialog("导出成功", $"数据已导出到: {file.Path}");
                }
                catch (Exception ex)
                {
                    await ShowMessageDialog("导出失败", ex.Message);
                }
                finally
                {
                    ShowLoading(false);
                }
            }
        }
        
        private async void OnImportClick(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add(".json");
            picker.FileTypeFilter.Add(".csv");
            
            var hwnd = WinRT.Interop.WindowHelper.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            
            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                var result = await ShowConfirmDialog("导入确认", "确定要导入数据吗？这会添加新的项目到数据库中。");
                if (result)
                {
                    ShowLoading(true);
                    try
                    {
                        var count = await _clipboardService.ImportItemsAsync(file.Path);
                        await ShowMessageDialog("导入成功", $"已导入 {count} 个项目");
                        await RefreshItemsAsync();
                    }
                    catch (Exception ex)
                    {
                        await ShowMessageDialog("导入失败", ex.Message);
                    }
                    finally
                    {
                        ShowLoading(false);
                    }
                }
            }
        }
        
        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ClipboardItemViewModel item)
            {
                _viewModel.SelectedItem = item;
                ShowDetailPanel();
            }
        }
        
        private void OnItemSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ItemsListView.SelectedItem is ClipboardItemViewModel item)
            {
                _viewModel.SelectedItem = item;
            }
        }
        
        private async void OnAddTagsClick(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedItem != null)
            {
                var dialog = new TagEditorDialog(_viewModel.SelectedItem.Tags);
                dialog.XamlRoot = this.Content.XamlRoot;
                
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary && dialog.Tags != null)
                {
                    _viewModel.SelectedItem.Tags = dialog.Tags;
                    await _clipboardService.UpdateItemAsync(_viewModel.SelectedItem.ToClipboardItem());
                }
            }
        }
        
        private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                var suggestions = _viewModel.GetSearchSuggestions(sender.Text);
                sender.ItemsSource = suggestions;
            }
        }
        
        private async void OnSearchSubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (!string.IsNullOrWhiteSpace(args.QueryText))
            {
                ShowLoading(true);
                try
                {
                    await _viewModel.SearchItemsAsync(args.QueryText);
                }
                finally
                {
                    ShowLoading(false);
                }
            }
        }
        
        #endregion
        
        #region 辅助方法
        
        private async Task RefreshItemsAsync()
        {
            ShowLoading(true);
            try
            {
                await _viewModel.RefreshItemsAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"刷新数据失败: {ex.Message}");
            }
            finally
            {
                ShowLoading(false);
            }
        }
        
        private VirtualKeyModifiers GetCurrentModifiers()
        {
            var modifiers = VirtualKeyModifiers.None;
            
            if (Windows.System.VirtualKeyModifiers.Control.HasFlag(Windows.System.VirtualKeyModifiers.Control))
                modifiers |= VirtualKeyModifiers.Control;
            if (Windows.System.VirtualKeyModifiers.Shift.HasFlag(Windows.System.VirtualKeyModifiers.Shift))
                modifiers |= VirtualKeyModifiers.Shift;
            if (Windows.System.VirtualKeyModifiers.Alt.HasFlag(Windows.System.VirtualKeyModifiers.Alt))
                modifiers |= VirtualKeyModifiers.Alt;
            if (Windows.System.VirtualKeyModifiers.Windows.HasFlag(Windows.System.VirtualKeyModifiers.Windows))
                modifiers |= VirtualKeyModifiers.Windows;
            
            return modifiers;
        }
        
        private async Task<bool> ShowConfirmDialog(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                PrimaryButtonText = "确定",
                SecondaryButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot
            };
            
            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }
        
        private async Task ShowMessageDialog(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                PrimaryButtonText = "确定",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot
            };
            
            await dialog.ShowAsync();
        }
        
        private void OnRefreshTimerTick(object sender, object e)
        {
            if (this.Visible && AutoRefreshToggle?.IsChecked == true)
            {
                _ = RefreshItemsAsync();
            }
        }
        
        private async void OnStatsUpdateTimerTick(object sender, object e)
        {
            await UpdateStatisticsAsync();
        }
        
        #endregion
        
        #region 导航按钮事件
        
        private async void OnNavAllChecked(object sender, RoutedEventArgs e)
        {
            await _viewModel.LoadAllItemsAsync();
        }
        
        private async void OnNavFavoritesChecked(object sender, RoutedEventArgs e)
        {
            await _viewModel.LoadFavoriteItemsAsync();
        }
        
        private async void OnNavPinnedChecked(object sender, RoutedEventArgs e)
        {
            await _viewModel.LoadPinnedItemsAsync();
        }
        
        private async void OnNavImagesChecked(object sender, RoutedEventArgs e)
        {
            await _viewModel.LoadImageItemsAsync();
        }
        
        private async void OnNavFilesChecked(object sender, RoutedEventArgs e)
        {
            await _viewModel.LoadFileItemsAsync();
        }
        
        private void OnAutoRefreshChecked(object sender, RoutedEventArgs e)
        {
            _refreshTimer.Start();
        }
        
        private void OnAutoRefreshUnchecked(object sender, RoutedEventArgs e)
        {
            _refreshTimer.Stop();
        }
        
        private void OnSortChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                _viewModel.SortBy = comboBox.SelectedIndex switch
                {
                    0 => SortBy.Time,
                    1 => SortBy.Name,
                    2 => SortBy.Type,
                    _ => SortBy.Time
                };
                _viewModel.SortItems();
            }
        }
        
        private void OnDetailPanelClosed(object sender, RoutedEventArgs e)
        {
            HideDetailPanel();
        }
        
        #endregion
        
        #region 窗口管理扩展
        
        public void BringToFront()
        {
            // 激活窗口
            var hwnd = WinRT.Interop.WindowHelper.GetWindowHandle(this);
            Windows.Win32.PInvoke.SetForegroundWindow(new Windows.Win32.Foundation.HWND(hwnd));
            
            // 闪烁任务栏按钮
            FlashWindow(hwnd, true);
        }
        
        public void Minimize()
        {
            this.AppWindow.MoveInZOrderAtBottom();
            this.Hide();
        }
        
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FlashWindow(IntPtr hwnd, bool invert);
        
        #endregion
    }
}