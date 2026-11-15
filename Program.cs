using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace WindowsKeyBlocker;

internal static class Program
{
    private static readonly Config Config = new();

    private const string InstanceMutexName = @"Local\WindowsKeyBlocker.Instance";
    private const string StopEventName = @"Local\WindowsKeyBlocker.Stop";

    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const uint WmStop = 0x8001;
    private const uint PmNoRemove = 0x0000;
    private const uint VkLWin = 0x5B;
    private const uint VkRWin = 0x5C;

    private static readonly LowLevelKeyboardProc HookCallbackDelegate = HookCallback;
    private static IntPtr _hookHandle;
    private static bool _leftWinWasBlocked;
    private static bool _rightWinWasBlocked;

    private static int Main(string[] args)
    {
        try
        {
            ReadJson();

            using var instanceMutex = new Mutex(true, InstanceMutexName, out bool createdNew);
            if (!createdNew)
                return 0;

            using var stopEvent = new EventWaitHandle(false, EventResetMode.AutoReset, StopEventName);

            _hookHandle = SetWindowsHookEx(
                WhKeyboardLl,
                HookCallbackDelegate,
                GetModuleHandle(null),
                0);

            if (_hookHandle == IntPtr.Zero)
                return 1;

            var threadId = GetCurrentThreadId();

            // Create the thread's message queue before the stop listener can post to it.
            PeekMessage(out _, IntPtr.Zero, 0, 0, PmNoRemove);

            var stopListener = new Thread(() =>
            {
                try
                {
                    stopEvent.WaitOne();
                    PostThreadMessage(threadId, WmStop, UIntPtr.Zero, IntPtr.Zero);
                }
                catch (ObjectDisposedException)
                {
                    // The process is already shutting down.
                }
            })
            {
                IsBackground = true,
                Name = "GameWinKeyBlocker stop listener"
            };
            stopListener.Start();

            return RunMessageLoop();
        }
        catch
        {
            // This is a silent background process: startup failures do not open a window.
            return 1;
        }
        finally
        {
            if (_hookHandle != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }
        }
    }


    private static int RunMessageLoop()
    {
        while (true)
        {
            var result = GetMessage(out var message, IntPtr.Zero, 0, 0);

            if (result == 0)
                return 0;
            if (result == -1)
                return 2;
            if (message.MessageId == WmStop)
                return 0;

            TranslateMessage(ref message);
            DispatchMessage(ref message);
        }
    }

    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var message = wParam.ToInt32();
            var isDown = message is WmKeyDown or WmSysKeyDown;
            var isUp = message is WmKeyUp or WmSysKeyUp;

            if (isDown || isUp)
            {
                var key = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);

                if (key.VkCode is VkLWin or VkRWin)
                {
                    var isLeft = key.VkCode == VkLWin;
                    var wasBlocked = isLeft ? _leftWinWasBlocked : _rightWinWasBlocked;

                    if (isDown && (wasBlocked || IsTargetGameForeground()))
                    {
                        SetBlockedState(isLeft, true);
                        return new IntPtr(1);
                    }

                    if (isUp && wasBlocked)
                    {
                        SetBlockedState(isLeft, false);
                        return new IntPtr(1);
                    }
                }
            }
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private static void SetBlockedState(bool isLeft, bool blocked)
    {
        if (isLeft)
            _leftWinWasBlocked = blocked;
        else
            _rightWinWasBlocked = blocked;
    }

    private static bool IsTargetGameForeground()
    {
        var foregroundWindow = GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero)
            return false;

        GetWindowThreadProcessId(foregroundWindow, out var processId);
        if (processId == 0)
            return false;

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return Config.Executables.Contains(process.ProcessName);
        }
        catch
        {
            return false;
        }
    }

    private static void ReadJson()
    {
        try
        {
            var jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

            if (!File.Exists(jsonPath))
            {
                CreateDefaultSettings(jsonPath);
            }

            WaitJsonEdit(jsonPath);

            var json = File.ReadAllText(jsonPath);

            var config = JsonSerializer.Deserialize<Config>(json);

            if (config == null)
            {
                throw new Exception("ARQUIVO DE CONFIGURAÇÃO INVÁLIDO");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERRO NA FUNÇÃO \"ReadJson()\": {ex.Message}");
        }
    }

    private static void CreateDefaultSettings(string jsonPath)
    {
        File.Create(jsonPath).Close();

        var newConfig = new Config
        {
            Executables =
            [
                "game 1",
                "game 2"
            ]
        };

        var jsonWrite = JsonSerializer.Serialize(newConfig, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        File.WriteAllText(jsonPath, jsonWrite);
    }

    private static void WaitJsonEdit(string jsonPath)
    {
        while (true)
        {
            try
            {
                var json = File.ReadAllText(jsonPath);

                var config = JsonSerializer.Deserialize<Config>(json);

                if (config is not null && !config.Executables.Contains("game 1") || !config.Executables.Contains("game 2"))
                {
                    return;
                }
            }
            catch
            {
                // Ignores while user is editing/saving the JSON file.
            }

            Thread.Sleep(1500);
        }
    }


    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint VkCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr DwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public IntPtr Window;
        public uint MessageId;
        public UIntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public Point Point;
        public uint Private;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(
        int idHook,
        LowLevelKeyboardProc callback,
        IntPtr moduleHandle,
        uint threadId);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(
        IntPtr hookHandle,
        int nCode,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hookHandle);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessage(out NativeMessage message, IntPtr windowHandle, uint min, uint max, uint remove);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetMessage(out NativeMessage message, IntPtr windowHandle, uint min, uint max);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(ref NativeMessage message);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref NativeMessage message);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessage(uint threadId, uint message, UIntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string moduleName);
}
