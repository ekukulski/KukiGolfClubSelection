using System;
using System.Threading.Tasks;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.PlatformConfiguration;

#if WINDOWS
using Microsoft.Maui.Storage;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using WinRT.Interop;
#endif

namespace KukiGolfClubSelection
{
    public partial class App : Application
    {
#if WINDOWS
        private const string WindowWidthKey = "WindowWidth";
        private const string WindowHeightKey = "WindowHeight";
        private const string WindowXKey = "WindowX";
        private const string WindowYKey = "WindowY";
        private const string WindowStateKey = "WindowState"; // "Maximized" or "Normal"
#endif

        public App()
        {
            InitializeComponent();
            MainPage = new NavigationPage(new HomePage());
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // MainPage is Page? in MAUI, so guard it to avoid CS8604
            var page = MainPage ?? new NavigationPage(new HomePage());
            var window = new Window(page);

#if WINDOWS
            window.HandlerChanged += (s, e) =>
            {
                var nativeWindow = window.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
                if (nativeWindow == null)
                    return;

                var hwnd = WindowNative.GetWindowHandle(nativeWindow);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = AppWindow.GetFromWindowId(windowId);
                var presenter = appWindow.Presenter as OverlappedPresenter;

                // Load saved settings
                int savedW = Preferences.Default.Get(WindowWidthKey, 0);
                int savedH = Preferences.Default.Get(WindowHeightKey, 0);
                int savedX = Preferences.Default.Get(WindowXKey, int.MinValue);
                int savedY = Preferences.Default.Get(WindowYKey, int.MinValue);
                string savedState = Preferences.Default.Get(WindowStateKey, "");

                bool hasSavedBounds =
                    savedW > 0 && savedH > 0 &&
                    savedX != int.MinValue && savedY != int.MinValue;

                bool isApplyingRestore = false;

                // Save whenever user moves/resizes (after things settle)
                appWindow.Changed += async (sender, args) =>
                {
                    if (isApplyingRestore)
                        return;

                    var op = appWindow.Presenter as OverlappedPresenter;
                    if (op == null)
                        return;

                    var state = (op.State == OverlappedPresenterState.Maximized) ? "Maximized" : "Normal";
                    Preferences.Default.Set(WindowStateKey, state);

                    // Only persist bounds when in normal state
                    if (state != "Normal")
                        return;

                    if (!args.DidSizeChange && !args.DidPositionChange)
                        return;

                    await Task.Delay(200);

                    if (isApplyingRestore)
                        return;

                    Preferences.Default.Set(WindowWidthKey, appWindow.Size.Width);
                    Preferences.Default.Set(WindowHeightKey, appWindow.Size.Height);
                    Preferences.Default.Set(WindowXKey, appWindow.Position.X);
                    Preferences.Default.Set(WindowYKey, appWindow.Position.Y);
                };

                if (!hasSavedBounds)
                {
                    // First run: maximize
                    presenter?.Maximize();
                    return;
                }

                // Restore AFTER activation to prevent Windows “drift”
                bool appliedOnce = false;
                nativeWindow.Activated += async (_, __) =>
                {
                    if (appliedOnce)
                        return;

                    appliedOnce = true;
                    await Task.Delay(150);

                    isApplyingRestore = true;
                    try
                    {
                        presenter?.Restore();

                        var rect = new RectInt32(savedX, savedY, savedW, savedH);
                        rect = ClampRectToVisibleDisplay(rect);

                        appWindow.MoveAndResize(rect);

                        if (savedState == "Maximized")
                            presenter?.Maximize();
                    }
                    finally
                    {
                        await Task.Delay(150);
                        isApplyingRestore = false;
                    }
                };
            };
#endif

            return window;
        }

#if WINDOWS
        private static RectInt32 ClampRectToVisibleDisplay(RectInt32 rect)
        {
            var center = new PointInt32(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            var displayArea = DisplayArea.GetFromPoint(center, DisplayAreaFallback.Primary);
            var work = displayArea.WorkArea; // excludes taskbar

            int minW = 500;
            int minH = 400;

            int w = Math.Max(rect.Width, minW);
            int h = Math.Max(rect.Height, minH);

            if (w > work.Width) w = work.Width;
            if (h > work.Height) h = work.Height;

            int maxX = (work.X + work.Width) - w;
            int maxY = (work.Y + work.Height) - h;

            int x = rect.X;
            int y = rect.Y;

            if (x < work.X) x = work.X;
            if (y < work.Y) y = work.Y;

            if (x > maxX) x = maxX;
            if (y > maxY) y = maxY;

            return new RectInt32(x, y, w, h);
        }
#endif
    }
}
