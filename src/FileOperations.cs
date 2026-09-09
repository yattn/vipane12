using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public static class AppClipboard
{
    private static string[] paths;
    private static bool isMove;

    public static bool HasData
    {
        get { return paths != null && paths.Length > 0; }
    }

    public static bool IsMove
    {
        get { return isMove; }
    }

    public static int Count
    {
        get { return paths == null ? 0 : paths.Length; }
    }

    public static void SetCopy(string[] sources)
    {
        paths = Clone(sources);
        isMove = false;
    }

    public static void SetMove(string[] sources)
    {
        paths = Clone(sources);
        isMove = true;
    }

    public static string[] GetPaths()
    {
        return Clone(paths);
    }

    private static string[] Clone(string[] sources)
    {
        if (sources == null)
        {
            return null;
        }
        string[] copy = new string[sources.Length];
        Array.Copy(sources, copy, sources.Length);
        return copy;
    }
}

public static class FileOperations
{
    private enum OverwriteAction { ReplaceAll, SkipAll, Cancel }

    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_WANTNUKEWARNING = 0x4000;
    private const ushort FOF_NO_CONNECTED_ELEMENTS = 0x2000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

    public static bool Open(string path, IWin32Window owner)
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = path;
            psi.UseShellExecute = true;
            using (Process process = Process.Start(psi)) { }
            return true;
        }
        catch (Exception ex)
        {
            ShowError(owner, "Open", path, ex.Message);
            return false;
        }
    }

    public static int CopyPaths(string[] sources, string destDir, IWin32Window owner)
    {
        string[] remaining = TransferPaths(sources, destDir, false, owner);
        return sources == null ? 0 : sources.Length - remaining.Length;
    }

    public static string[] MovePaths(string[] sources, string destDir, IWin32Window owner)
    {
        return TransferPaths(sources, destDir, true, owner);
    }

    private static string[] TransferPaths(string[] sources, string destDir, bool move, IWin32Window owner)
    {
        List<string> remaining = new List<string>();
        if (sources == null) return remaining.ToArray();
        OverwriteAction? action = null;
        for (int i = 0; i < sources.Length; i++)
        {
            string source = sources[i];
            string dest = destDir;
            if (action == OverwriteAction.Cancel)
            {
                remaining.Add(source);
                continue;
            }
            try
            {
                source = Path.GetFullPath(source);
                if (source.Length == Path.GetPathRoot(source).Length)
                    throw new IOException("Copying or moving a drive root is not supported.");
                source = source.TrimEnd('\\');
                destDir = Path.GetFullPath(destDir);
                if ((File.GetAttributes(destDir) & FileAttributes.Directory) == 0)
                    throw new IOException("Destination is not a directory.");
                EnsureNoReparsePoint(source);
                EnsureNoReparsePoint(destDir);
                dest = Path.Combine(destDir, Path.GetFileName(source));
                if (IsWithin(dest, source) || IsWithin(source, dest))
                    throw new IOException("Source and destination must not be the same path or contain one another.");

                // Check the whole source tree before creating or replacing anything.
                ValidateTransfer(source, dest);
                if (!TransferEntry(source, dest, move, owner, ref action)) remaining.Add(sources[i]);
            }
            catch (Exception ex)
            {
                remaining.Add(sources[i]);
                ShowError(owner, move ? "Move" : "Copy", source + "\nDestination: " + dest, ex.Message);
            }
        }
        return remaining.ToArray();
    }

    private static bool IsWithin(string path, string parent)
    {
        return string.Equals(path, parent, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(parent.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureNoReparsePoint(string path)
    {
        for (string p = path; p != null; p = Path.GetDirectoryName(p))
        {
            if ((File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Operations through links or junctions are not supported: " + p);
        }
    }

    private static FileAttributes? DestinationAttributes(string path)
    {
        try { return File.GetAttributes(path); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    private static void ValidateTransfer(string source, string dest)
    {
        FileAttributes attributes = File.GetAttributes(source);
        FileAttributes? target = DestinationAttributes(dest);
        if ((attributes & FileAttributes.ReparsePoint) != 0
            || (target.HasValue && (target.Value & FileAttributes.ReparsePoint) != 0))
            throw new IOException("Copying or moving links and junctions is not supported: " + source + " -> " + dest);
        bool directory = (attributes & FileAttributes.Directory) != 0;
        if (dest.Length >= (directory ? 248 : 260))
            throw new PathTooLongException("Destination exceeds the .NET Framework path limit: " + dest);
        if (target.HasValue && directory != ((target.Value & FileAttributes.Directory) != 0))
            throw new IOException("A file cannot replace a directory, or vice versa: " + dest);
        if (directory)
        {
            foreach (string child in Directory.GetFileSystemEntries(source))
                ValidateTransfer(child, Path.Combine(dest, Path.GetFileName(child)));
        }
    }

    private static bool TransferEntry(string source, string dest, bool move, IWin32Window owner, ref OverwriteAction? action)
    {
        FileAttributes attributes = File.GetAttributes(source);
        FileAttributes? target = DestinationAttributes(dest);
        if (target.HasValue)
        {
            if (!action.HasValue) action = AskOverwrite(owner, dest);
            if (action != OverwriteAction.ReplaceAll) return false;
        }
        bool directory = (attributes & FileAttributes.Directory) != 0;
        if (!directory)
        {
            if (move && !target.HasValue)
            {
                File.Move(source, dest);
            }
            else
            {
                // Never delete the old destination before the copy can read its source.
                File.Copy(source, dest, target.HasValue);
                if (move) File.Delete(source);
            }
            return true;
        }
        if (move && !target.HasValue && string.Equals(Path.GetPathRoot(source), Path.GetPathRoot(dest), StringComparison.OrdinalIgnoreCase))
        {
            Directory.Move(source, dest);
            return true;
        }

        // Replace All merges directories; destination-only items are not deleted.
        Directory.CreateDirectory(dest);
        bool complete = true;
        string[] children = Directory.GetFileSystemEntries(source);
        Array.Sort(children, StringComparer.OrdinalIgnoreCase);
        foreach (string child in children)
        {
            if (action == OverwriteAction.Cancel) return false;
            if (!TransferEntry(child, Path.Combine(dest, Path.GetFileName(child)), move, owner, ref action)) complete = false;
        }
        if (move && complete) Directory.Delete(source, false);
        return complete;
    }

    public static int DeleteToRecycle(string[] paths, IWin32Window owner)
    {
        if (paths == null || paths.Length == 0)
        {
            return 0;
        }
        List<string> existing = new List<string>();
        for (int i = 0; i < paths.Length; i++)
        {
            try
            {
                string path = Path.GetFullPath(paths[i]);
                EnsureNoReparsePoint(path);
                existing.Add(path);
            }
            catch (Exception ex)
            {
                ShowError(owner, "Delete", paths[i], ex.Message);
            }
        }
        if (existing.Count == 0)
        {
            return 0;
        }
        SHFILEOPSTRUCT fo = new SHFILEOPSTRUCT();
        fo.hwnd = owner == null ? IntPtr.Zero : owner.Handle;
        fo.wFunc = FO_DELETE;
        fo.pFrom = string.Join("\0", existing.ToArray()) + "\0\0";
        fo.pTo = null;
        // Do not silently fall back to permanent deletion or include HTML companion folders.
        fo.fFlags = (ushort)(FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_WANTNUKEWARNING | FOF_NO_CONNECTED_ELEMENTS);
        fo.fAnyOperationsAborted = false;
        fo.hNameMappings = IntPtr.Zero;
        fo.lpszProgressTitle = null;
        int rc = SHFileOperation(ref fo);
        if (rc != 0 && !fo.fAnyOperationsAborted)
        {
            ShowError(owner, "Delete", string.Join("\n", existing.ToArray()), "Shell delete failed (0x" + rc.ToString("X") + ").");
        }
        return rc == 0 && !fo.fAnyOperationsAborted ? existing.Count : 0;
    }

    public static int DeletePermanent(string[] paths, IWin32Window owner)
    {
        if (paths == null || paths.Length == 0)
        {
            return 0;
        }
        string text;
        if (paths.Length == 1)
        {
            text = "Delete permanently?\n" + paths[0];
        }
        else
        {
            text = "Delete " + paths.Length + " items permanently?\n" + paths[0] + "\n...";
        }
        DialogResult r = MessageBox.Show(
            owner,
            text,
            "vipane12 - Permanent delete",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (r != DialogResult.OK)
        {
            return 0;
        }
        int completed = 0;
        for (int i = 0; i < paths.Length; i++)
        {
            try
            {
                DeletePath(paths[i]);
                completed++;
            }
            catch (Exception ex)
            {
                ShowError(owner, "Delete", paths[i], ex.Message);
            }
        }
        return completed;
    }

    public static void ShowError(IWin32Window owner, string operation, string path, string message)
    {
        Control control = owner as Control;
        MainForm main = control == null ? null : control.FindForm() as MainForm;
        if (main != null) main.SetCurrentState("Error: " + operation + " failed");
        string text = "Operation: " + operation + "\nPath: " + path + "\n" + message;
        if (owner != null)
        {
            MessageBox.Show(owner, text, "vipane12 - Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        else
        {
            MessageBox.Show(text, "vipane12 - Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void DeletePath(string path)
    {
        path = Path.GetFullPath(path);
        EnsureNoReparsePoint(path);
        if ((File.GetAttributes(path) & FileAttributes.Directory) != 0)
        {
            Directory.Delete(path, true);
        }
        else
        {
            File.Delete(path);
        }
    }

    private static OverwriteAction AskOverwrite(IWin32Window owner, string destPath)
    {
        using (OverwriteDialog dlg = new OverwriteDialog(destPath))
        {
            dlg.ShowDialog(owner);
            return dlg.Result;
        }
    }

    private sealed class OverwriteDialog : Form
    {
        private OverwriteAction result = OverwriteAction.Cancel;

        public OverwriteAction Result
        {
            get { return result; }
        }

        public OverwriteDialog(string destPath)
        {
            this.Text = "vipane12 - Confirm";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.Width = 440;
            this.Height = 170;

            Label label = new Label();
            label.AutoSize = false;
            label.SetBounds(12, 12, 400, 60);
            label.Text = "Already exists:\n" + destPath + "\nReplace files / merge directories, skip, or cancel?";
            this.Controls.Add(label);

            Button replaceButton = new Button();
            replaceButton.Text = "Replace All";
            replaceButton.SetBounds(12, 80, 120, 30);
            replaceButton.Click += delegate(object s, EventArgs e)
            {
                result = OverwriteAction.ReplaceAll;
                this.DialogResult = DialogResult.Yes;
                this.Close();
            };
            this.Controls.Add(replaceButton);

            Button skipButton = new Button();
            skipButton.Text = "Skip All";
            skipButton.SetBounds(148, 80, 120, 30);
            skipButton.Click += delegate(object s, EventArgs e)
            {
                result = OverwriteAction.SkipAll;
                this.DialogResult = DialogResult.No;
                this.Close();
            };
            this.Controls.Add(skipButton);

            Button cancelButton = new Button();
            cancelButton.Text = "Cancel";
            cancelButton.SetBounds(284, 80, 120, 30);
            cancelButton.Click += delegate(object s, EventArgs e)
            {
                result = OverwriteAction.Cancel;
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };
            this.Controls.Add(cancelButton);

            this.AcceptButton = skipButton;
            this.CancelButton = cancelButton;
            this.ActiveControl = skipButton;
        }
    }
}
