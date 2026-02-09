using System.Drawing;
using Microsoft.Win32;

namespace Greg.KeySub;

/// <summary>
/// Application context that runs the app in the system tray without a main window.
/// </summary>
public class TrayApplicationContext : ApplicationContext
{
    private const string AppName = "Greg.KeySub";
    private const string StartupRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    
    private readonly NotifyIcon _trayIcon;
    private readonly GlobalKeyboardHook _keyboardHook;
    private bool _isEnabled = true;

    public TrayApplicationContext()
    {
        // Create context menu
        var contextMenu = new ContextMenuStrip();
        
        var enabledItem = new ToolStripMenuItem("Enabled", null, OnToggleEnabled)
        {
            Checked = true,
            CheckOnClick = true
        };
        contextMenu.Items.Add(enabledItem);
        
        var autoStartItem = new ToolStripMenuItem("Start with Windows", null, OnToggleAutoStart)
        {
            Checked = IsAutoStartEnabled(),
            CheckOnClick = true
        };
        contextMenu.Items.Add(autoStartItem);
        
        contextMenu.Items.Add(new ToolStripSeparator());
        
        contextMenu.Items.Add("Diagnostics", null, OnDiagnostics);
        contextMenu.Items.Add("About", null, OnAbout);
        contextMenu.Items.Add("Exit", null, OnExit);

        // Create tray icon
        _trayIcon = new NotifyIcon
        {
            Icon = CreateIcon(),
            Text = "KeySub - § → ` (Enabled)",
            ContextMenuStrip = contextMenu,
            Visible = true
        };

        _trayIcon.DoubleClick += OnToggleEnabled;

        // Initialize and start keyboard hook
        _keyboardHook = new GlobalKeyboardHook();
        _keyboardHook.KeyIntercepted += OnKeyIntercepted;

        try
        {
            _keyboardHook.Install();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to install keyboard hook: {ex.Message}\n\nThe application may need to run as Administrator.",
                "KeySub Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void OnKeyIntercepted(object? sender, KeyInterceptedEventArgs e)
    {
        if (!_isEnabled)
        {
            e.Handled = false;
            return;
        }

        // Replace § with `
        e.ReplacementChar = '`';
        e.Handled = true;
    }

    private void OnToggleEnabled(object? sender, EventArgs e)
    {
        _isEnabled = !_isEnabled;
        
        // Update menu item if triggered from double-click
        if (sender is NotifyIcon)
        {
            var menu = _trayIcon.ContextMenuStrip;
            if (menu?.Items[0] is ToolStripMenuItem enabledItem)
            {
                enabledItem.Checked = _isEnabled;
            }
        }
        else if (sender is ToolStripMenuItem menuItem)
        {
            _isEnabled = menuItem.Checked;
        }

        _trayIcon.Text = $"KeySub - § → ` ({(_isEnabled ? "Enabled" : "Disabled")})";
        ShowBalloon("KeySub", _isEnabled ? "Key substitution enabled" : "Key substitution disabled");
    }

    private void OnToggleAutoStart(object? sender, EventArgs e)
    {
        if (sender is ToolStripMenuItem menuItem)
        {
            SetAutoStart(menuItem.Checked);
        }
    }

    private static bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, false);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }

    private static void SetAutoStart(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true);
            if (key == null) return;

            if (enable)
            {
                string exePath = Environment.ProcessPath ?? Application.ExecutablePath;
                key.SetValue(AppName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to update auto-start setting: {ex.Message}",
                "KeySub Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void OnDiagnostics(object? sender, EventArgs e)
    {
        MessageBox.Show(
            $"Keyboard Layout Detection\n\n{_keyboardHook.DiagnosticInfo}\n\n" +
            "If § is not detected, please report the issue.",
            "KeySub Diagnostics",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void OnAbout(object? sender, EventArgs e)
    {
        MessageBox.Show(
            "Greg.KeySub\n\n" +
            "A utility that replaces § with ` globally.\n\n" +
            "• Double-click the tray icon to toggle\n" +
            "• Right-click for menu options\n\n" +
            "© 2026",
            "About KeySub",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void OnExit(object? sender, EventArgs e)
    {
        _trayIcon.Visible = false;
        _keyboardHook.Dispose();
        Application.Exit();
    }

    private void ShowBalloon(string title, string text)
    {
        _trayIcon.BalloonTipTitle = title;
        _trayIcon.BalloonTipText = text;
        _trayIcon.BalloonTipIcon = ToolTipIcon.Info;
        _trayIcon.ShowBalloonTip(2000);
    }

    private static Icon CreateIcon()
    {
        // Try to load logo.ico from application directory
        var iconPath = Path.Combine(AppContext.BaseDirectory, "logo.ico");
        if (File.Exists(iconPath))
        {
            return new Icon(iconPath);
        }

        // Create a simple icon programmatically as fallback
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        
        graphics.Clear(Color.FromArgb(64, 64, 64));
        
        using var font = new Font("Segoe UI", 16, FontStyle.Bold);
        using var brush = new SolidBrush(Color.White);
        
        var text = "§";
        var size = graphics.MeasureString(text, font);
        var x = (32 - size.Width) / 2;
        var y = (32 - size.Height) / 2;
        
        graphics.DrawString(text, font, brush, x, y);

        return Icon.FromHandle(bitmap.GetHicon());
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _trayIcon.Dispose();
            _keyboardHook.Dispose();
        }
        base.Dispose(disposing);
    }
}
