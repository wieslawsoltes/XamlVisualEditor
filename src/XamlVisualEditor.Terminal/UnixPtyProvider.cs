using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace XamlVisualEditor.Terminal;

public sealed class UnixPtyProvider : IPtyProvider
{
    public IPtyProcess StartProcess(TerminalSessionOptions options)
    {
        string shell = options.Command ?? Environment.GetEnvironmentVariable("SHELL") ?? "/bin/zsh";
        string[] args = BuildArgs(shell, options.Arguments);

        IntPtr winSizePtr = CreateWinSize(options.Columns, options.Rows);
        IntPtr ttyPathPtr = Marshal.AllocHGlobal(UnixNative.PtyNameBufferSize);
        ZeroMemory(ttyPathPtr, UnixNative.PtyNameBufferSize);

        int pid = UnixNative.forkpty(out int masterFd, ttyPathPtr, IntPtr.Zero, winSizePtr);
        string? ttyPath = Marshal.PtrToStringAnsi(ttyPathPtr);
        Marshal.FreeHGlobal(ttyPathPtr);

        if (winSizePtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(winSizePtr);
        }

        if (pid == 0)
        {
            if (!string.IsNullOrWhiteSpace(options.WorkingDirectory))
            {
                UnixNative.chdir(options.WorkingDirectory);
            }

            ApplyEnvironment(options.Environment);
            ApplyDefaultEnvironment(options.Environment);

            Exec(shell, args);
            UnixNative._exit(1);
        }

        if (pid < 0)
        {
            throw new InvalidOperationException("Failed to create PTY process.");
        }

        return new UnixPtyProcess(pid, masterFd, options.Columns, options.Rows, ttyPath);
    }

    private static void ApplyEnvironment(IReadOnlyDictionary<string, string> environment)
    {
        foreach (KeyValuePair<string, string> kvp in environment)
        {
            UnixNative.setenv(kvp.Key, kvp.Value, 1);
        }
    }

    private static void ApplyDefaultEnvironment(IReadOnlyDictionary<string, string> environment)
    {
        if (!environment.ContainsKey("TERM"))
        {
            UnixNative.setenv("TERM", "xterm-256color", 1);
        }

        if (!environment.ContainsKey("COLORTERM"))
        {
            UnixNative.setenv("COLORTERM", "truecolor", 1);
        }
    }

    private static IntPtr CreateWinSize(int columns, int rows)
    {
        UnixNative.WinSize winsize = new()
        {
            ws_col = (ushort)Math.Clamp(columns, 1, ushort.MaxValue),
            ws_row = (ushort)Math.Clamp(rows, 1, ushort.MaxValue),
            ws_xpixel = 0,
            ws_ypixel = 0
        };

        IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf<UnixNative.WinSize>());
        Marshal.StructureToPtr(winsize, ptr, false);
        return ptr;
    }

    private static string[] BuildArgs(string shell, System.Collections.Generic.IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            return new[] { shell };
        }

        string[] result = new string[args.Count + 1];
        result[0] = shell;
        for (int i = 0; i < args.Count; i++)
        {
            result[i + 1] = args[i];
        }

        return result;
    }

    private static void Exec(string file, string[] args)
    {
        IntPtr argv = BuildArgv(args);
        UnixNative.execvp(file, argv);
    }

    private static IntPtr BuildArgv(string[] args)
    {
        int size = (args.Length + 1) * IntPtr.Size;
        IntPtr argv = Marshal.AllocHGlobal(size);
        for (int i = 0; i < args.Length; i++)
        {
            IntPtr strPtr = Marshal.StringToHGlobalAnsi(args[i]);
            Marshal.WriteIntPtr(argv, i * IntPtr.Size, strPtr);
        }
        Marshal.WriteIntPtr(argv, args.Length * IntPtr.Size, IntPtr.Zero);
        return argv;
    }

    private static void ZeroMemory(IntPtr ptr, int length)
    {
        for (int i = 0; i < length; i++)
        {
            Marshal.WriteByte(ptr, i, 0);
        }
    }
}

public sealed class UnixPtyProcess : IPtyProcess
{
    private readonly SafeFileHandle _handle;
    private readonly FileStream _stream;
    private readonly string? _ttyPath;
    private int _columns;
    private int _rows;

    public Stream Input => _stream;
    public Stream Output => _stream;
    public int Pid { get; }

    public UnixPtyProcess(int pid, int masterFd, int columns, int rows, string? ttyPath)
    {
        Pid = pid;
        _columns = columns;
        _rows = rows;
        _ttyPath = string.IsNullOrWhiteSpace(ttyPath) ? null : ttyPath;
        _handle = new SafeFileHandle(new IntPtr(masterFd), ownsHandle: true);
        _stream = new FileStream(_handle, FileAccess.ReadWrite, 4096, isAsync: false);
    }

    public void Resize(int columns, int rows, int pixelWidth = 0, int pixelHeight = 0)
    {
        int clampedColumns = Math.Clamp(columns, 1, ushort.MaxValue);
        int clampedRows = Math.Clamp(rows, 1, ushort.MaxValue);
        if (_columns == clampedColumns && _rows == clampedRows)
        {
            return;
        }

        _columns = clampedColumns;
        _rows = clampedRows;

        UnixNative.WinSize winsize = new()
        {
            ws_col = (ushort)clampedColumns,
            ws_row = (ushort)clampedRows,
            ws_xpixel = (ushort)Math.Clamp(pixelWidth, 0, ushort.MaxValue),
            ws_ypixel = (ushort)Math.Clamp(pixelHeight, 0, ushort.MaxValue)
        };

        if (UnixNative.IsLinux)
        {
            UnixNative.ioctl(_handle, UnixNative.TIOCSWINSZ, ref winsize);
            return;
        }

        if (_ttyPath is not null)
        {
            // BSD/macOS: use stty on the slave PTY to avoid ioctl varargs ABI issues.
            UnixNative.TryResizeWithStty(_ttyPath, clampedRows, clampedColumns);
        }
    }

    public void Dispose()
    {
        _stream.Dispose();
        _handle.Dispose();
    }
}

internal static class UnixNative
{
    public const uint TIOCSWINSZ = 0x5414;
    public const int PtyNameBufferSize = 128;
    public static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    public static int forkpty(out int master, IntPtr name, IntPtr termp, IntPtr winp)
    {
        if (!IsLinux)
        {
            return forkpty_libutil(out master, name, termp, winp);
        }

        try
        {
            return forkpty_linux_libc(out master, name, termp, winp);
        }
        catch (DllNotFoundException)
        {
            return forkpty_linux_libutil(out master, name, termp, winp);
        }
        catch (EntryPointNotFoundException)
        {
            return forkpty_linux_libutil(out master, name, termp, winp);
        }
    }

    [DllImport("libutil.so.1", EntryPoint = "forkpty")]
    private static extern int forkpty_linux_libutil(out int master, IntPtr name, IntPtr termp, IntPtr winp);

    [DllImport("libc", EntryPoint = "forkpty")]
    private static extern int forkpty_linux_libc(out int master, IntPtr name, IntPtr termp, IntPtr winp);

    [DllImport("libutil", EntryPoint = "forkpty")]
    private static extern int forkpty_libutil(out int master, IntPtr name, IntPtr termp, IntPtr winp);

    [DllImport("libc")]
    public static extern int execvp(string file, IntPtr argv);

    [DllImport("libc")]
    public static extern int chdir(string path);

    [DllImport("libc")]
    public static extern int setenv(string name, string value, int overwrite);

    [DllImport("libc")]
    public static extern void _exit(int status);

    [DllImport("libc", SetLastError = true)]
    public static extern int ioctl(SafeFileHandle fd, uint request, ref WinSize data);

    public static void TryResizeWithStty(string ttyPath, int rows, int columns)
    {
        string sttyPath = "/bin/stty";
        if (!File.Exists(sttyPath))
        {
            return;
        }

        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = sttyPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                || RuntimeInformation.IsOSPlatform(OSPlatform.Create("FREEBSD"))
                || RuntimeInformation.IsOSPlatform(OSPlatform.Create("NETBSD"))
                || RuntimeInformation.IsOSPlatform(OSPlatform.Create("OPENBSD")))
            {
                startInfo.ArgumentList.Add("-f");
            }
            else
            {
                startInfo.ArgumentList.Add("-F");
            }

            startInfo.ArgumentList.Add(ttyPath);
            startInfo.ArgumentList.Add("rows");
            startInfo.ArgumentList.Add(rows.ToString());
            startInfo.ArgumentList.Add("cols");
            startInfo.ArgumentList.Add(columns.ToString());

            Process? process = Process.Start(startInfo);
            if (process is null)
            {
                return;
            }

            using (process)
            {
                process.WaitForExit(250);
            }
        }
        catch
        {
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WinSize
    {
        public ushort ws_row;
        public ushort ws_col;
        public ushort ws_xpixel;
        public ushort ws_ypixel;
    }
}
