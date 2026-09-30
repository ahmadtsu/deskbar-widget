using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Forms = System.Windows.Forms;

namespace DeskbarWidget.App;

/// <summary>
/// System tray icon (WinForms NotifyIcon, no extra packages). Art comes from
/// the bundled eyes.png: near-black background is keyed to transparent so it
/// blends into light and dark trays, then downscaled to 32px.
/// Right-click menu, v1: Exit only.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint hIcon);

    private readonly Forms.NotifyIcon _icon;
    private Icon? _ownedIcon;
    private bool _disposed;

    public TrayIcon(Action onExit)
    {
        _ownedIcon = LoadIcon();

        var exit = new Forms.ToolStripMenuItem("Exit");
        exit.Click += (_, _) => onExit();

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(exit);

        _icon = new Forms.NotifyIcon
        {
            Text = "DeskbarWidget",
            Icon = _ownedIcon,
            ContextMenuStrip = menu,
            Visible = false,
        };
    }

    public void Show() => _icon.Visible = true;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _icon.Visible = false;
        _icon.Dispose();
        _ownedIcon?.Dispose();
        _ownedIcon = null;
    }

    private static Icon LoadIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/eyes.png", UriKind.Absolute);
            var stream = System.Windows.Application.GetResourceStream(uri)?.Stream;
            if (stream is null)
            {
                return SystemIcons.Application;
            }

            using (stream)
            using (var source = new Bitmap(stream))
            using (var cleaned = KeyOutBlack(source))
            using (var small = new Bitmap(cleaned, new System.Drawing.Size(32, 32)))
            {
                var h = small.GetHicon();
                try
                {
                    using var fromHandle = Icon.FromHandle(h);
                    return (Icon)fromHandle.Clone();
                }
                finally
                {
                    DestroyIcon(h);
                }
            }
        }
        catch
        {
            return SystemIcons.Application;
        }
    }

    /// <summary>
    /// eyes.png sits on a pure-black backdrop; punch it to transparent so the
    /// icon works on light and dark taskbars. Only touches near-black pixels
    /// to leave the artwork's dark strokes alone.
    /// </summary>
    private static Bitmap KeyOutBlack(Bitmap source)
    {
        var output = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using (var src = new PinnedBitmap(source))
        using (var dst = new PinnedBitmap(output, write: true))
        {
            for (var i = 0; i < src.Pixels.Length; i += 4)
            {
                var b = src.Pixels[i];
                var g = src.Pixels[i + 1];
                var r = src.Pixels[i + 2];
                var a = src.Pixels[i + 3];
                dst.Pixels[i] = b;
                dst.Pixels[i + 1] = g;
                dst.Pixels[i + 2] = r;
                dst.Pixels[i + 3] = (r + g + b <= 30) ? (byte)0 : a;
            }
        }

        return output;
    }

    private sealed class PinnedBitmap : IDisposable
    {
        public readonly byte[] Pixels;
        private readonly BitmapData _data;
        private readonly Bitmap _bitmap;

        public PinnedBitmap(Bitmap bitmap, bool write = false)
        {
            _bitmap = bitmap;
            var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            _data = bitmap.LockBits(
                rect,
                write ? ImageLockMode.WriteOnly : ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);
            Pixels = new byte[Math.Abs(_data.Stride) * bitmap.Height];
            if (!write)
            {
                Marshal.Copy(_data.Scan0, Pixels, 0, Pixels.Length);
            }
        }

        public void Dispose()
        {
            Marshal.Copy(Pixels, 0, _data.Scan0, Pixels.Length);
            _bitmap.UnlockBits(_data);
        }
    }
}
