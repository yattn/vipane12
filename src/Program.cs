using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

static class Program
{
    internal const int CopyDataMessage = 0x004A;
    internal const int ActivationData = 0x56503133;
    internal const int ActivationAccepted = 1;
    internal const int ActivationNotReady = 2;
    internal const int ActivationForegroundDenied = 3;
    private const int WindowNotRegistered = unchecked((int)0x8002802B);
    private static string windowKey;
    private delegate bool WindowCallback(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool SetProp(IntPtr window, string name, IntPtr value);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetProp(IntPtr window, string name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr RemoveProp(IntPtr window, string name);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")] private static extern bool AllowSetForegroundWindow(uint processId);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SendMessageTimeout(IntPtr window, int message, IntPtr wparam, ref CopyData data, uint flags, uint timeout, out IntPtr result);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentProcessId();
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsWindowEnabled(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] private static extern IntPtr GetLastActivePopup(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);

    // Public virtual-desktop manager, declared in v-table order.
    [ComImport]
    [Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IVirtualDesktopManager
    {
        [return: MarshalAs(UnmanagedType.Bool)]
        bool IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow);
        Guid GetWindowDesktopId(IntPtr topLevelWindow);
        void MoveWindowToDesktop(IntPtr topLevelWindow, [In] ref Guid desktopId);
    }

    private static readonly Guid VirtualDesktopManagerClass = new Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A");

    [StructLayout(LayoutKind.Sequential)]
    internal struct CopyData
    {
        public IntPtr Tag;
        public int Size;
        public IntPtr Data;
    }

    [STAThread]
    static void Main()
    {
        try
        {
            using (WindowsIdentity user = WindowsIdentity.GetCurrent())
                RunInstance(@"Local\ViPane12.71D7204C-68B8-48AF-9C93-1670C1F94789." + user.User.Value);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not start or activate ViPane12.\n" + ex.Message, "ViPane12 - Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.ExitCode = 1;
        }
    }

    internal static void RunInstance(string key)
    {
        using (Mutex mutex = new Mutex(false, key))
        {
            Stopwatch wait = Stopwatch.StartNew();
            Guid desktop = Guid.Empty;
            while (!AcquireInstance(mutex))
            {
                if (wait.ElapsedMilliseconds >= 10000)
                    throw new TimeoutException("The existing instance is still starting or busy. Try again after it is ready.");
                IntPtr window = FindInstanceWindow(key);
                if (window != IntPtr.Zero)
                {
                    if (desktop == Guid.Empty) desktop = GetCurrentDesktopId();
                    uint processId;
                    if (GetWindowThreadProcessId(window, out processId) == 0) continue;
                    // Delegate only to the identified instance, never to arbitrary processes.
                    AllowSetForegroundWindow(processId);
                    int result;
                    try { result = SendActivationRequest(window, desktop); }
                    catch (Win32Exception)
                    {
                        if (!IsWindow(window)) continue;
                        throw;
                    }
                    if (result == ActivationAccepted) return;
                    if (result == ActivationForegroundDenied)
                        throw new InvalidOperationException("Windows did not allow the existing window to come to the foreground.");
                    if (result < 0) throw new COMException("Could not move the existing window to this desktop (0x" + result.ToString("X8") + ").", result);
                    if (result != ActivationNotReady)
                        throw new InvalidOperationException("The running copy cannot handle desktop transfer. Close it and restart the updated ViPane12.exe.");
                }
                Thread.Sleep(50);
            }
            try
            {
                windowKey = key;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (MainForm main = new MainForm()) Application.Run(main);
            }
            finally
            {
                windowKey = null;
                mutex.ReleaseMutex();
            }
        }
    }

    private static bool AcquireInstance(Mutex mutex)
    {
        try { return mutex.WaitOne(0); }
        catch (AbandonedMutexException) { return true; }
    }

    internal static IVirtualDesktopManager CreateDesktopManager()
    {
        return (IVirtualDesktopManager)Activator.CreateInstance(Type.GetTypeFromCLSID(VirtualDesktopManagerClass));
    }

    internal static Guid GetCurrentDesktopId()
    {
        IVirtualDesktopManager manager = CreateDesktopManager();
        try
        {
            using (DesktopAnchor anchor = new DesktopAnchor())
            {
                anchor.Show();
                Stopwatch wait = Stopwatch.StartNew();
                while (wait.ElapsedMilliseconds < 2000)
                {
                    // Only this launcher's invisible window exists here, not a file-operation UI.
                    Application.DoEvents();
                    try
                    {
                        Guid desktop = manager.GetWindowDesktopId(anchor.Handle);
                        if (desktop != Guid.Empty)
                        {
                            if (!manager.IsWindowOnCurrentVirtualDesktop(anchor.Handle))
                                throw new InvalidOperationException("The current desktop changed during launch. Try launching again.");
                            return desktop;
                        }
                    }
                    catch (COMException ex) { if (ex.ErrorCode != WindowNotRegistered) throw; }
                    Thread.Sleep(20);
                }
                throw new TimeoutException("Windows did not identify the launcher's current virtual desktop.");
            }
        }
        finally { Marshal.ReleaseComObject(manager); }
    }

    internal static int SendActivationRequest(IntPtr window, Guid desktop)
    {
        if (desktop == Guid.Empty) throw new ArgumentException("A non-empty desktop identifier is required.");
        CopyData data = new CopyData();
        data.Tag = new IntPtr(ActivationData);
        data.Size = 16;
        data.Data = Marshal.AllocHGlobal(data.Size);
        try
        {
            Marshal.StructureToPtr(desktop, data.Data, false);
            IntPtr result;
            // WM_COPYDATA marshals the GUID across processes; a private message would not.
            if (SendMessageTimeout(window, CopyDataMessage, IntPtr.Zero, ref data, 0x22, 5000, out result) == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "The existing instance did not respond to desktop transfer.");
            return unchecked((int)result.ToInt64());
        }
        finally { Marshal.FreeHGlobal(data.Data); }
    }

    internal static bool MoveWindowToDesktop(IntPtr window, Guid desktop)
    {
        if (desktop == Guid.Empty) throw new ArgumentException("A non-empty desktop identifier is required.");
        uint processId;
        GetWindowThreadProcessId(window, out processId);
        if (processId != GetCurrentProcessId()) throw new UnauthorizedAccessException("Only the window's own process may move it.");
        IVirtualDesktopManager manager = CreateDesktopManager();
        try
        {
            Guid current;
            try { current = manager.GetWindowDesktopId(window); }
            catch (COMException ex) { if (ex.ErrorCode == WindowNotRegistered) return false; throw; }
            if (current == Guid.Empty) return false;
            List<IntPtr> windows = new List<IntPtr>();
            windows.Add(window);
            EnumWindows(delegate(IntPtr candidate, IntPtr parameter)
            {
                uint owner;
                GetWindowThreadProcessId(candidate, out owner);
                if (candidate != window && owner == processId && GetAncestor(candidate, 3) == window && IsWindowVisible(candidate))
                    windows.Add(candidate);
                return true;
            }, IntPtr.Zero);
            foreach (IntPtr target in windows)
            {
                Guid before = manager.GetWindowDesktopId(target);
                // Owned tool windows without their own desktop identity follow their owner.
                if (target != window && before == Guid.Empty) continue;
                if (before != desktop) manager.MoveWindowToDesktop(target, ref desktop);
                if (manager.GetWindowDesktopId(target) != desktop)
                    throw new InvalidOperationException("Windows did not move the window to the requested desktop.");
            }
            return true;
        }
        finally { Marshal.ReleaseComObject(manager); }
    }

    internal static IntPtr FindInstanceWindow(string key)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows(delegate(IntPtr window, IntPtr parameter)
        {
            if (GetProp(window, key) != new IntPtr(1)) return true;
            found = window;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    internal static void RegisterWindow(IntPtr window)
    {
        if (windowKey != null && !SetProp(window, windowKey, new IntPtr(1))) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    internal static void UnregisterWindow(IntPtr window)
    {
        if (windowKey != null) RemoveProp(window, windowKey);
    }

    internal static int ActivateWindow(IntPtr window)
    {
        if (IsIconic(window)) ShowWindow(window, 9); // SW_RESTORE also preserves a maximized placement.
        while (true)
        {
            IntPtr popup = GetLastActivePopup(window);
            if (popup == window || !IsWindowVisible(popup)) break;
            window = popup;
        }
        if (!IsWindowVisible(window) || !IsWindowEnabled(window)) return ActivationNotReady;
        return SetForegroundWindow(window) ? ActivationAccepted : ActivationForegroundDenied;
    }

    private sealed class DesktopAnchor : Form
    {
        public DesktopAnchor()
        {
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;
            Opacity = 0;
            StartPosition = FormStartPosition.Manual;
            Location = Screen.PrimaryScreen.WorkingArea.Location;
            Size = new Size(1, 1);
        }

        protected override bool ShowWithoutActivation { get { return true; } }
    }
}
