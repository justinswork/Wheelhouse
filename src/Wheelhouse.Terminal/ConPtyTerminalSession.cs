using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using static Wheelhouse.Terminal.ConPtyNativeMethods;

namespace Wheelhouse.Terminal;

internal sealed class ConPtyTerminalSession : ITerminalSession
{
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cts = new();
    private IntPtr _hPC;
    private IntPtr _hProcess;
    private FileStream? _inputStream;
    private FileStream? _outputStream;
    private bool _disposed;

    public Guid Id { get; } = Guid.NewGuid();
    public ShellProfile Shell { get; }
    public string WorkingDirectory { get; }

    public bool IsAlive
    {
        get
        {
            if (_hProcess == IntPtr.Zero || _disposed) return false;
            GetExitCodeProcess(_hProcess, out var code);
            return code == STILL_ACTIVE;
        }
    }

    public event EventHandler<string>? OutputReceived;
    public event EventHandler? SessionExited;

    internal ConPtyTerminalSession(ShellProfile shell, string workingDirectory, ILogger logger)
    {
        Shell = shell;
        WorkingDirectory = workingDirectory;
        _logger = logger;
    }

    internal void Start(short cols = 120, short rows = 30)
    {
        var size = new COORD { X = cols, Y = rows };

        CreatePipe(out var outputRead, out var outputWrite, IntPtr.Zero, 0);
        CreatePipe(out var inputRead,  out var inputWrite,  IntPtr.Zero, 0);

        int hr = CreatePseudoConsole(size, inputRead, outputWrite, 0, out _hPC);
        CloseHandle(outputWrite);
        CloseHandle(inputRead);

        if (hr != S_OK)
        {
            CloseHandle(outputRead);
            CloseHandle(inputWrite);
            throw new InvalidOperationException($"CreatePseudoConsole failed: 0x{hr:X8}");
        }

        try
        {
            IntPtr attrListSize = IntPtr.Zero;
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attrListSize);
            var attrList = Marshal.AllocHGlobal(attrListSize);
            try
            {
                if (!InitializeProcThreadAttributeList(attrList, 1, 0, ref attrListSize))
                    throw new InvalidOperationException($"InitializeProcThreadAttributeList failed: {Marshal.GetLastWin32Error()}");

                var hpc = _hPC;
                if (!UpdateProcThreadAttribute(attrList, 0,
                        (IntPtr)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                        ref hpc, (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
                    throw new InvalidOperationException($"UpdateProcThreadAttribute failed: {Marshal.GetLastWin32Error()}");

                var si = new STARTUPINFOEX();
                si.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>();
                si.lpAttributeList = attrList;

                var commandLine = string.IsNullOrEmpty(Shell.Arguments)
                    ? $"\"{Shell.ExecutablePath}\""
                    : $"\"{Shell.ExecutablePath}\" {Shell.Arguments}";

                if (!CreateProcess(null, commandLine, IntPtr.Zero, IntPtr.Zero, false,
                        EXTENDED_STARTUPINFO_PRESENT | CREATE_NO_WINDOW, IntPtr.Zero, WorkingDirectory,
                        ref si, out var pi))
                    throw new InvalidOperationException($"CreateProcess failed: {Marshal.GetLastWin32Error()}");

                _hProcess = pi.hProcess;
                CloseHandle(pi.hThread);
            }
            finally
            {
                DeleteProcThreadAttributeList(attrList);
                Marshal.FreeHGlobal(attrList);
            }

            // Anonymous pipe handles are not opened for overlapped I/O — isAsync must be false.
            // Reads/writes happen on background Task.Run threads, so synchronous I/O is fine.
            _inputStream  = new FileStream(new SafeFileHandle(inputWrite,  ownsHandle: true), FileAccess.Write, 4096, isAsync: false);
            _outputStream = new FileStream(new SafeFileHandle(outputRead, ownsHandle: true),  FileAccess.Read,  4096, isAsync: false);
        }
        catch
        {
            // Clean up anything that was allocated before the failure so no process or
            // pseudo-console is left running in the background with no owner.
            if (_hPC != IntPtr.Zero) { ClosePseudoConsole(_hPC); _hPC = IntPtr.Zero; }
            if (_hProcess != IntPtr.Zero) { CloseHandle(_hProcess); _hProcess = IntPtr.Zero; }
            CloseHandle(outputRead);
            CloseHandle(inputWrite);
            throw;
        }

        Task.Run(() => ReadOutputLoop(_cts.Token));
        Task.Run(() => MonitorExitAsync(_cts.Token));
    }

    private void ReadOutputLoop(CancellationToken ct)
    {
        var buffer = new byte[4096];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                int read = _outputStream!.Read(buffer, 0, buffer.Length);
                if (read == 0) break;
                OutputReceived?.Invoke(this, Encoding.UTF8.GetString(buffer, 0, read));
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _logger.LogDebug(ex, "Terminal output loop ended for {Id}", Id); }
    }

    private async Task MonitorExitAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && IsAlive)
                await Task.Delay(500, ct).ConfigureAwait(false);
            if (!ct.IsCancellationRequested)
                SessionExited?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException) { }
    }

    public Task WriteInputAsync(string input, CancellationToken ct = default)
    {
        if (_inputStream is null || _disposed) return Task.CompletedTask;
        return Task.Run(() =>
        {
            try
            {
                var bytes = Encoding.UTF8.GetBytes(input);
                _inputStream.Write(bytes);
                _inputStream.Flush();
            }
            catch (Exception ex) { _logger.LogDebug(ex, "Write to terminal {Id} failed", Id); }
        }, ct);
    }

    public Task ResizeAsync(int columns, int rows, CancellationToken ct = default)
    {
        if (_hPC != IntPtr.Zero)
            ResizePseudoConsole(_hPC, new COORD { X = (short)columns, Y = (short)rows });
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _cts.CancelAsync();
        _cts.Dispose();
        // Close the pseudoconsole first — this signals EOF to the output pipe,
        // which unblocks the synchronous Read() in ReadOutputLoop.
        if (_hPC != IntPtr.Zero) { ClosePseudoConsole(_hPC); _hPC = IntPtr.Zero; }
        _inputStream?.Dispose();
        _outputStream?.Dispose();
        if (_hProcess != IntPtr.Zero) { CloseHandle(_hProcess); _hProcess = IntPtr.Zero; }
    }
}
