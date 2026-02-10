using System;
using System.Collections.Generic;
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
        int pid = UnixNative.forkpty(out int masterFd, IntPtr.Zero, IntPtr.Zero, winSizePtr);
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

        return new UnixPtyProcess(pid, masterFd, options.Columns, options.Rows);
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
        if (!environment.ContainsKey("TERM") && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TERM")))
        {
            UnixNative.setenv("TERM", "xterm-256color", 1);
        }

        if (!environment.ContainsKey("COLORTERM") && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COLORTERM")))
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
}

public sealed class UnixPtyProcess : IPtyProcess
{
    private readonly SafeFileHandle _handle;
    private readonly FileStream _stream;

    public Stream Input => _stream;
    public Stream Output => _stream;
    public int Pid { get; }

    public UnixPtyProcess(int pid, int masterFd, int columns, int rows)
    {
        Pid = pid;
        _handle = new SafeFileHandle(new IntPtr(masterFd), ownsHandle: true);
        _stream = new FileStream(_handle, FileAccess.ReadWrite, 4096, isAsync: false);
        Resize(columns, rows);
    }

    public void Resize(int columns, int rows, int pixelWidth = 0, int pixelHeight = 0)
    {
        UnixNative.WinSize winsize = new()
        {
            ws_col = (ushort)columns,
            ws_row = (ushort)rows,
            ws_xpixel = (ushort)Math.Clamp(pixelWidth, 0, ushort.MaxValue),
            ws_ypixel = (ushort)Math.Clamp(pixelHeight, 0, ushort.MaxValue)
        };

        UnixNative.ioctl(_handle, UnixNative.TIOCSWINSZ, ref winsize);
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

    [DllImport("libutil")]
    public static extern int forkpty(out int master, IntPtr name, IntPtr termp, IntPtr winp);

    [DllImport("libc")]
    public static extern int execvp(string file, IntPtr argv);

    [DllImport("libc")]
    public static extern int chdir(string path);

    [DllImport("libc")]
    public static extern int setenv(string name, string value, int overwrite);

    [DllImport("libc")]
    public static extern void _exit(int status);

    [DllImport("libc")]
    public static extern int ioctl(SafeFileHandle fd, uint request, ref WinSize data);

    [StructLayout(LayoutKind.Sequential)]
    public struct WinSize
    {
        public ushort ws_row;
        public ushort ws_col;
        public ushort ws_xpixel;
        public ushort ws_ypixel;
    }
}
