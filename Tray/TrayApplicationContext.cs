using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CCCDReaderService.Tray;

public class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly WebApplication _webApp;
    private readonly string _dashboardUrl;
    private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "CCCDReaderService";

    public TrayApplicationContext(WebApplication webApp, string dashboardUrl)
    {
        _webApp = webApp;
        _dashboardUrl = dashboardUrl;

        _notifyIcon = new NotifyIcon
        {
            Icon = CreateAppIcon(),
            Text = $"CCCD Reader Service ({dashboardUrl})",
            Visible = true,
            ContextMenuStrip = CreateContextMenu()
        };

        _notifyIcon.DoubleClick += (s, e) => OpenDashboard();
    }

    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();

        var openDashboardItem = new ToolStripMenuItem("🌐 Mở Web Dashboard", null, (s, e) => OpenDashboard());
        openDashboardItem.Font = new Font(openDashboardItem.Font, FontStyle.Bold);
        menu.Items.Add(openDashboardItem);

        var autoStartItem = new ToolStripMenuItem("🔄 Khởi động cùng Windows")
        {
            Checked = IsAutoStartEnabled()
        };
        autoStartItem.Click += (s, e) =>
        {
            var newState = !autoStartItem.Checked;
            SetAutoStart(newState);
            autoStartItem.Checked = IsAutoStartEnabled();
        };
        menu.Items.Add(autoStartItem);

        menu.Items.Add(new ToolStripSeparator());

        var statusInfoItem = new ToolStripMenuItem($"ℹ️ Trạng thái: Đang hoạt động ({_dashboardUrl})")
        {
            Enabled = false
        };
        menu.Items.Add(statusInfoItem);

        menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("❌ Thoát", null, async (s, e) => await ExitApplicationAsync());
        menu.Items.Add(exitItem);

        return menu;
    }

    private void OpenDashboard()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _dashboardUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể mở trình duyệt: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
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
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
            if (key == null) return;

            if (enable)
            {
                var exePath = Environment.ProcessPath ?? Application.ExecutablePath;
                key.SetValue(AppName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi cập nhật cấu hình tự khởi động: {ex.Message}", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task ExitApplicationAsync()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();

        try
        {
            await _webApp.StopAsync(TimeSpan.FromSeconds(3));
        }
        catch
        {
            // Ignore error when shutting down
        }

        ExitThread();
        Application.Exit();
    }

    private static Icon CreateAppIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var bgBrush = new SolidBrush(Color.FromArgb(14, 116, 144));
            g.FillEllipse(bgBrush, 1, 1, 30, 30);

            using var cardBrush = new SolidBrush(Color.White);
            g.FillRectangle(cardBrush, 6, 9, 20, 14);

            using var chipBrush = new SolidBrush(Color.FromArgb(245, 158, 11));
            g.FillRectangle(chipBrush, 9, 12, 5, 4);

            using var linePen = new Pen(Color.FromArgb(203, 213, 225), 1.5f);
            g.DrawLine(linePen, 16, 12, 23, 12);
            g.DrawLine(linePen, 16, 15, 23, 15);
            g.DrawLine(linePen, 9, 19, 23, 19);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
