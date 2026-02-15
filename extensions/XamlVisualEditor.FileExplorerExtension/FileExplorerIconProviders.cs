using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.FileExplorerExtension;

public enum FileExplorerIconProviderKind
{
    Native,
    Theme
}

public interface IFileExplorerIconProvider
{
    object? GetIcon(string path, bool isDirectory);
}

public sealed class ExtensionSystemIconService : ISystemIconService
{
    private readonly ThemeFileExplorerIconProvider _themeProvider;
    private readonly Dictionary<NativeIconCacheKey, object> _nativeCache = new();
    private readonly object _sync = new();

    public ExtensionSystemIconService()
    {
        _themeProvider = new ThemeFileExplorerIconProvider(ResolveTheme());
    }

    public object? GetIcon(string? path, bool isDirectory, object? fallbackIcon = null, int iconSize = 16)
    {
        object? nativeIcon = TryGetNativeIcon(path, isDirectory, iconSize);
        if (nativeIcon is not null)
        {
            return nativeIcon;
        }

        if (fallbackIcon is not null)
        {
            return fallbackIcon;
        }

        return _themeProvider.GetIcon(path ?? string.Empty, isDirectory);
    }

    public object? GetFileIcon(string? path, object? fallbackIcon = null, int iconSize = 16)
    {
        return GetIcon(path, isDirectory: false, fallbackIcon, iconSize);
    }

    public object? GetFolderIcon(string? path, object? fallbackIcon = null, int iconSize = 16)
    {
        return GetIcon(path, isDirectory: true, fallbackIcon, iconSize);
    }

    private object? TryGetNativeIcon(string? path, bool isDirectory, int iconSize)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        NativeIconCacheKey cacheKey = new(iconSize, ResolveCacheKey(path, isDirectory));
        lock (_sync)
        {
            if (_nativeCache.TryGetValue(cacheKey, out object? cached))
            {
                return cached;
            }
        }

        object? icon = null;
        try
        {
            if (OperatingSystem.IsWindows())
            {
                icon = WindowsNativeIconLoader.TryLoad(path, isDirectory, iconSize);
            }
            else if (OperatingSystem.IsMacOS())
            {
                icon = MacNativeIconLoader.TryLoad(path, iconSize);
            }
            else if (OperatingSystem.IsLinux())
            {
                icon = LinuxNativeIconLoader.TryLoad(path, iconSize);
            }
        }
        catch
        {
            icon = null;
        }

        if (icon is not null)
        {
            lock (_sync)
            {
                _nativeCache[cacheKey] = icon;
            }
        }

        return icon;
    }

    private static string ResolveCacheKey(string path, bool isDirectory)
    {
        if (isDirectory)
        {
            return "dir";
        }

        string extension = Path.GetExtension(path);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return "file";
        }

        return extension.ToLowerInvariant();
    }

    private static FileExplorerIconTheme ResolveTheme()
    {
        if (OperatingSystem.IsMacOS())
        {
            return FileExplorerIconTheme.Mac;
        }

        if (OperatingSystem.IsWindows())
        {
            return FileExplorerIconTheme.Windows;
        }

        return FileExplorerIconTheme.Linux;
    }

    private readonly record struct NativeIconCacheKey(int IconSize, string Token);
}

public static class FileExplorerIconProviderFactory
{
    public static IFileExplorerIconProvider Create(
        FileExplorerIconProviderKind kind,
        int iconSize,
        ISystemIconService? systemIcons = null)
    {
        return kind switch
        {
            FileExplorerIconProviderKind.Native => CreateNativeProvider(iconSize, systemIcons),
            _ => CreateThemeProvider()
        };
    }

    private static IFileExplorerIconProvider CreateNativeProvider(int iconSize, ISystemIconService? systemIcons)
    {
        IFileExplorerIconProvider fallback = CreateThemeProvider();
        return new NativeFileExplorerIconProvider(iconSize, fallback, systemIcons);
    }

    private static IFileExplorerIconProvider CreateThemeProvider()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return new ThemeFileExplorerIconProvider(FileExplorerIconTheme.Mac);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new ThemeFileExplorerIconProvider(FileExplorerIconTheme.Windows);
        }

        return new ThemeFileExplorerIconProvider(FileExplorerIconTheme.Linux);
    }
}

public sealed class ThemeFileExplorerIconProvider : IFileExplorerIconProvider
{
    private readonly FileExplorerIconTheme _theme;

    public ThemeFileExplorerIconProvider(FileExplorerIconTheme theme)
    {
        _theme = theme;
    }

    public object? GetIcon(string path, bool isDirectory)
    {
        return _theme.GetIcon(path, isDirectory);
    }
}

public sealed class NativeFileExplorerIconProvider : IFileExplorerIconProvider
{
    private readonly int _iconSize;
    private readonly ISystemIconService _systemIcons;
    private readonly IFileExplorerIconProvider _fallback;

    public NativeFileExplorerIconProvider(
        int iconSize,
        IFileExplorerIconProvider fallback,
        ISystemIconService? systemIcons = null)
    {
        _iconSize = iconSize;
        _fallback = fallback;
        _systemIcons = systemIcons ?? new ExtensionSystemIconService();
    }

    public object? GetIcon(string path, bool isDirectory)
    {
        object? fallbackIcon = _fallback.GetIcon(path, isDirectory);
        return _systemIcons.GetIcon(path, isDirectory, fallbackIcon, _iconSize);
    }
}

public sealed class FileExplorerIconTheme
{
    private readonly string _folderIcon;
    private readonly string _defaultFileIcon;
    private readonly Dictionary<string, string> _fileIcons;

    private FileExplorerIconTheme(string folderIcon, string defaultFileIcon, Dictionary<string, string> fileIcons)
    {
        _folderIcon = folderIcon;
        _defaultFileIcon = defaultFileIcon;
        _fileIcons = fileIcons;
    }

    public static FileExplorerIconTheme Windows { get; } = CreateWindowsTheme();

    public static FileExplorerIconTheme Mac { get; } = CreateMacTheme();

    public static FileExplorerIconTheme Linux { get; } = CreateLinuxTheme();

    public object GetIcon(string path, bool isDirectory)
    {
        if (isDirectory)
        {
            return _folderIcon;
        }

        string extension = Path.GetExtension(path).ToLowerInvariant();
        if (_fileIcons.TryGetValue(extension, out string? icon))
        {
            return icon;
        }

        return _defaultFileIcon;
    }

    private static FileExplorerIconTheme CreateWindowsTheme()
    {
        return new FileExplorerIconTheme(
            folderIcon: "📁",
            defaultFileIcon: "📄",
            fileIcons: CreateDefaultFileIcons());
    }

    private static FileExplorerIconTheme CreateMacTheme()
    {
        return new FileExplorerIconTheme(
            folderIcon: "🗂",
            defaultFileIcon: "📄",
            fileIcons: CreateDefaultFileIcons());
    }

    private static FileExplorerIconTheme CreateLinuxTheme()
    {
        return new FileExplorerIconTheme(
            folderIcon: "📁",
            defaultFileIcon: "📄",
            fileIcons: CreateDefaultFileIcons());
    }

    private static Dictionary<string, string> CreateDefaultFileIcons()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".axaml"] = "📄",
            [".xaml"] = "📄",
            [".cs"] = "🧩",
            [".json"] = "🧾",
            [".xml"] = "🧾",
            [".md"] = "📝",
            [".yml"] = "⚙",
            [".yaml"] = "⚙",
            [".png"] = "🖼",
            [".jpg"] = "🖼",
            [".jpeg"] = "🖼",
            [".gif"] = "🖼",
            [".svg"] = "🖼"
        };
    }
}

internal static class WindowsNativeIconLoader
{
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiUseFileAttributes = 0x000000010;
    private const uint ShgfiSmallIcon = 0x000000001;
    private const uint ShgfiLargeIcon = 0x000000000;
    private const uint FileAttributeDirectory = 0x00000010;
    private const uint FileAttributeNormal = 0x00000080;
    private const uint BiRgb = 0;
    private const uint DibRgbColors = 0;

    public static Bitmap? TryLoad(string path, bool isDirectory, int iconSize)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        SHFILEINFO info = new();
        uint flags = ShgfiIcon | ShgfiUseFileAttributes | (iconSize <= 16 ? ShgfiSmallIcon : ShgfiLargeIcon);
        uint attributes = isDirectory ? FileAttributeDirectory : FileAttributeNormal;
        IntPtr result = SHGetFileInfo(path, attributes, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), flags);
        if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return CreateBitmapFromHIcon(info.hIcon);
        }
        finally
        {
            _ = DestroyIcon(info.hIcon);
        }
    }

    private static Bitmap? CreateBitmapFromHIcon(IntPtr hIcon)
    {
        if (!GetIconInfo(hIcon, out ICONINFO iconInfo))
        {
            return null;
        }

        try
        {
            IntPtr hBitmap = iconInfo.hbmColor != IntPtr.Zero ? iconInfo.hbmColor : iconInfo.hbmMask;
            if (hBitmap == IntPtr.Zero)
            {
                return null;
            }

            if (GetObject(hBitmap, Marshal.SizeOf<BITMAP>(), out BITMAP bitmap) == 0)
            {
                return null;
            }

            int width = bitmap.bmWidth;
            int height = Math.Abs(bitmap.bmHeight);
            if (width <= 0 || height <= 0)
            {
                return null;
            }

            BITMAPINFO info = new()
            {
                bmiHeader = new BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth = width,
                    biHeight = -height,
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = BiRgb
                }
            };

            int stride = width * 4;
            byte[] pixels = new byte[stride * height];
            IntPtr hdc = GetDC(IntPtr.Zero);
            try
            {
                int scanned = GetDIBits(hdc, hBitmap, 0, (uint)height, pixels, ref info, DibRgbColors);
                if (scanned == 0)
                {
                    return null;
                }
            }
            finally
            {
                _ = ReleaseDC(IntPtr.Zero, hdc);
            }

            WriteableBitmap target = new(
                new PixelSize(width, height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul);

            using (ILockedFramebuffer buffer = target.Lock())
            {
                if (buffer.RowBytes == stride)
                {
                    Marshal.Copy(pixels, 0, buffer.Address, pixels.Length);
                }
                else
                {
                    for (int y = 0; y < height; y++)
                    {
                        IntPtr row = IntPtr.Add(buffer.Address, y * buffer.RowBytes);
                        Marshal.Copy(pixels, y * stride, row, stride);
                    }
                }
            }

            return target;
        }
        finally
        {
            if (iconInfo.hbmColor != IntPtr.Zero)
            {
                _ = DeleteObject(iconInfo.hbmColor);
            }

            if (iconInfo.hbmMask != IntPtr.Zero)
            {
                _ = DeleteObject(iconInfo.hbmMask);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        public bool fIcon;
        public uint xHotspot;
        public uint yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public IntPtr bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        public uint bmiColors;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref SHFILEINFO psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int GetObject(IntPtr hObject, int nCount, out BITMAP lpObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int GetDIBits(
        IntPtr hdc,
        IntPtr hbmp,
        uint uStartScan,
        uint cScanLines,
        byte[] lpvBits,
        ref BITMAPINFO lpbmi,
        uint uUsage);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
}

internal static class MacNativeIconLoader
{
    private const uint NspngFileType = 4;

    public static Bitmap? TryLoad(string path, int iconSize)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return null;
        }

        using AutoreleasePool pool = new();

        IntPtr workspaceClass = ObjC.GetClass("NSWorkspace");
        IntPtr workspace = ObjC.SendIntPtr(workspaceClass, "sharedWorkspace");
        if (workspace == IntPtr.Zero)
        {
            return null;
        }

        IntPtr nsPath = ObjC.CreateNSString(path);
        if (nsPath == IntPtr.Zero)
        {
            return null;
        }

        IntPtr image = ObjC.SendIntPtr(workspace, "iconForFile:", nsPath);
        if (image == IntPtr.Zero)
        {
            return null;
        }

        ObjC.SendVoid(image, "setSize:", new NSSize(iconSize, iconSize));

        IntPtr tiff = ObjC.SendIntPtr(image, "TIFFRepresentation");
        if (tiff == IntPtr.Zero)
        {
            return null;
        }

        IntPtr imageRepClass = ObjC.GetClass("NSBitmapImageRep");
        IntPtr imageRep = ObjC.SendIntPtr(imageRepClass, "imageRepWithData:", tiff);
        if (imageRep == IntPtr.Zero)
        {
            return null;
        }

        IntPtr pngData = ObjC.SendIntPtr(imageRep, "representationUsingType:properties:", (nuint)NspngFileType, IntPtr.Zero);
        if (pngData == IntPtr.Zero)
        {
            return null;
        }

        IntPtr bytes = ObjC.SendIntPtr(pngData, "bytes");
        nuint length = ObjC.SendUIntPtr(pngData, "length");
        if (bytes == IntPtr.Zero || length == 0)
        {
            return null;
        }

        byte[] buffer = new byte[(int)length];
        Marshal.Copy(bytes, buffer, 0, buffer.Length);
        return LoadBitmap(buffer);
    }

    private static Bitmap? LoadBitmap(byte[] data)
    {
        try
        {
            using MemoryStream stream = new(data, writable: false);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NSSize
    {
        public NSSize(double width, double height)
        {
            Width = width;
            Height = height;
        }

        public double Width { get; }
        public double Height { get; }
    }

    private sealed class AutoreleasePool : IDisposable
    {
        private readonly IntPtr _pool;

        public AutoreleasePool()
        {
            IntPtr poolClass = ObjC.GetClass("NSAutoreleasePool");
            IntPtr alloc = ObjC.SendIntPtr(poolClass, "alloc");
            _pool = ObjC.SendIntPtr(alloc, "init");
        }

        public void Dispose()
        {
            if (_pool != IntPtr.Zero)
            {
                ObjC.SendVoid(_pool, "drain");
            }
        }
    }

    private static class ObjC
    {
        private const string ObjCLib = "/usr/lib/libobjc.A.dylib";

        public static IntPtr GetClass(string name) => objc_getClass(name);

        public static IntPtr SendIntPtr(IntPtr receiver, string selectorName)
            => objc_msgSend(receiver, sel_registerName(selectorName));

        public static IntPtr SendIntPtr(IntPtr receiver, string selectorName, IntPtr arg1)
            => objc_msgSend_IntPtr(receiver, sel_registerName(selectorName), arg1);

        public static IntPtr SendIntPtr(IntPtr receiver, string selectorName, nuint arg1, IntPtr arg2)
            => objc_msgSend_UIntPtr_IntPtr(receiver, sel_registerName(selectorName), arg1, arg2);

        public static void SendVoid(IntPtr receiver, string selectorName, NSSize size)
            => objc_msgSend_NSSize(receiver, sel_registerName(selectorName), size);

        public static void SendVoid(IntPtr receiver, string selectorName)
            => objc_msgSend_void(receiver, sel_registerName(selectorName));

        public static nuint SendUIntPtr(IntPtr receiver, string selectorName)
            => objc_msgSend_UIntPtr(receiver, sel_registerName(selectorName));

        public static IntPtr CreateNSString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return IntPtr.Zero;
            }

            IntPtr nsStringClass = GetClass("NSString");
            IntPtr selector = sel_registerName("stringWithUTF8String:");
            IntPtr utf8 = Marshal.StringToCoTaskMemUTF8(value);
            try
            {
                return objc_msgSend_IntPtr(nsStringClass, selector, utf8);
            }
            finally
            {
                Marshal.FreeCoTaskMem(utf8);
            }
        }

        [DllImport(ObjCLib)]
        private static extern IntPtr objc_getClass(string name);

        [DllImport(ObjCLib)]
        private static extern IntPtr sel_registerName(string name);

        [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
        private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

        [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
        private static extern IntPtr objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1);

        [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
        private static extern IntPtr objc_msgSend_UIntPtr_IntPtr(IntPtr receiver, IntPtr selector, nuint arg1, IntPtr arg2);

        [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
        private static extern nuint objc_msgSend_UIntPtr(IntPtr receiver, IntPtr selector);

        [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
        private static extern void objc_msgSend_NSSize(IntPtr receiver, IntPtr selector, NSSize size);

        [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
        private static extern void objc_msgSend_void(IntPtr receiver, IntPtr selector);
    }
}

internal static class LinuxNativeIconLoader
{
    private const string GioLib = "libgio-2.0.so.0";
    private const string GObjectLib = "libgobject-2.0.so.0";
    private const string GLibLib = "libglib-2.0.so.0";

    public static Bitmap? TryLoad(string path, int iconSize)
    {
        if (!OperatingSystem.IsLinux())
        {
            return null;
        }

        IntPtr file = g_file_new_for_path(path);
        if (file == IntPtr.Zero)
        {
            return null;
        }

        IntPtr info = IntPtr.Zero;
        try
        {
            IntPtr error;
            info = g_file_query_info(file, "standard::icon", 0, IntPtr.Zero, out error);
            if (error != IntPtr.Zero)
            {
                g_error_free(error);
            }

            if (info == IntPtr.Zero)
            {
                return null;
            }

            IntPtr icon = g_file_info_get_icon(info);
            if (icon == IntPtr.Zero)
            {
                return null;
            }

            IntPtr iconStringPtr = g_icon_to_string(icon);
            if (iconStringPtr == IntPtr.Zero)
            {
                return null;
            }

            string? iconString = Marshal.PtrToStringUTF8(iconStringPtr);
            g_free(iconStringPtr);

            if (string.IsNullOrWhiteSpace(iconString))
            {
                return null;
            }

            string? iconPath = ResolveIconPath(iconString);
            if (string.IsNullOrWhiteSpace(iconPath) || !File.Exists(iconPath))
            {
                return null;
            }

            if (Path.GetExtension(iconPath).Equals(".svg", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return LoadBitmapFromFile(iconPath);
        }
        finally
        {
            if (info != IntPtr.Zero)
            {
                g_object_unref(info);
            }

            g_object_unref(file);
        }
    }

    private static string? ResolveIconPath(string iconString)
    {
        if (iconString.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(iconString, UriKind.Absolute, out Uri? uri))
            {
                return uri.LocalPath;
            }
        }

        if (iconString.StartsWith("/", StringComparison.Ordinal))
        {
            return iconString;
        }

        return null;
    }

    private static Bitmap? LoadBitmapFromFile(string path)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    [DllImport(GioLib)]
    private static extern IntPtr g_file_new_for_path(string path);

    [DllImport(GioLib)]
    private static extern IntPtr g_file_query_info(
        IntPtr file,
        string attributes,
        int flags,
        IntPtr cancellable,
        out IntPtr error);

    [DllImport(GioLib)]
    private static extern IntPtr g_file_info_get_icon(IntPtr info);

    [DllImport(GioLib)]
    private static extern IntPtr g_icon_to_string(IntPtr icon);

    [DllImport(GObjectLib)]
    private static extern void g_object_unref(IntPtr obj);

    [DllImport(GLibLib)]
    private static extern void g_error_free(IntPtr error);

    [DllImport(GLibLib)]
    private static extern void g_free(IntPtr ptr);
}
