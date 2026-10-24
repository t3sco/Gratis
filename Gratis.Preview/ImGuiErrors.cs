using System.Runtime.InteropServices;
using System.Security.Cryptography;
using ImGuiNET;

namespace Gratis.Preview;

internal static class ImGuiErrors
{
    private const string NativeHash = "60EC992BCEBD354D59F235ED97CDC3B0EEA62C2DEF53C4A71F22937E500FA80B";
    private const int ErrorCallbackOffset = 0x2890;
    private const int NativeSetErrorModeRva = 0xE7410;
    private static readonly ErrorCallback Callback = Record;
    private static readonly List<string> messages = [];
    private static bool configured;
    public static int Count => messages.Count;
    public static nint CreateContext()
    {
        ConfigureNative();
        var context = ImGui.CreateContext();
        var io = ImGui.GetIO();
        io.ConfigErrorRecovery = true;
        io.ConfigErrorRecoveryEnableAssert = false;
        io.ConfigErrorRecoveryEnableDebugLog = true;
        io.ConfigErrorRecoveryEnableTooltip = false;
        if (Marshal.ReadIntPtr(context + ErrorCallbackOffset) != 0) {
            ImGui.DestroyContext(context);
            throw new InvalidOperationException("Unexpected native ImGui error callback; refusing to overwrite it.");
        }
        Marshal.WriteIntPtr(context + ErrorCallbackOffset, Marshal.GetFunctionPointerForDelegate(Callback));
        return context;
    }
    public static void DestroyContext(nint context) => ImGui.DestroyContext(context);
    public static void ThrowIfNewErrors(int previous)
    {
        if (Count > previous) throw new InvalidOperationException($"{Count - previous} ImGui error(s): " + string.Join(" | ", messages.Skip(previous)));
    }
    private static void Record(nint context, nint userData, nint message)
    {
        try {
            var text = Marshal.PtrToStringUTF8(message) ?? "Unknown ImGui error";
            messages.Add(text);
            Console.Error.WriteLine("[imgui-error] " + text);
        }
        catch { Environment.FailFast("Unable to record native ImGui error."); }
    }
    private static void ConfigureNative()
    {
        if (configured) return;
        if (!OperatingSystem.IsWindows() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
            throw new PlatformNotSupportedException("Native ImGui diagnostics require the pinned Windows x64 runtime.");
        var path = Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native", "cimgui.dll");
        using var file = File.OpenRead(path);
        if (Convert.ToHexString(SHA256.HashData(file)) != NativeHash)
            throw new InvalidOperationException("Unknown cimgui image; validate the error-callback ABI bridge before running the harness.");
        var module = NativeLibrary.Load(path);
        SetErrorMode(0x0001 | 0x0002 | 0x8000);
        var setMode = Marshal.GetDelegateForFunctionPointer<SetNativeErrorMode>(module + NativeSetErrorModeRva);
        setMode(1);
        configured = true;
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void ErrorCallback(nint context, nint userData, nint message);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int SetNativeErrorMode(int mode);
    [DllImport("kernel32.dll")] private static extern uint SetErrorMode(uint mode);
}
