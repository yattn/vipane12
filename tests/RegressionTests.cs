using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;

static class RegressionTests
{
    private static string root;
    private static int checks;
    private static int failures;
    private static int dialogs;
    private static string dialogText;
    private static string overwriteDefault;
    private static int messageDefault;
    private delegate bool WindowCallback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumThreadWindows(uint id, WindowCallback callback, IntPtr parameter);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern uint GetShortPathName(string path, StringBuilder result, uint capacity);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder text, int capacity);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int capacity);
    [DllImport("user32.dll")] private static extern IntPtr GetDlgItem(IntPtr window, int id);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, int message, IntPtr wparam, IntPtr lparam);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wparam, IntPtr lparam);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsZoomed(IntPtr window);
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern IntPtr GetLastActivePopup(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SendMessageTimeout(IntPtr window, int message, IntPtr wparam, IntPtr lparam, uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wparam, ref Program.CopyData data);

    private const int TestMoveOnly = 0x8101;
    private const int TestOpenDialog = 0x8102;
    private const int TestState = 0x8103;
    private const int TestMoveStatus = 0x8104;

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--instance-host") return RunInstanceHost(args);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(root);
        // This executable is built in its own disposable directory by test.bat.
        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "portable.flag"), "");
        try
        {
            Run("File operations", TestFiles);
            Run("Selection and keys", TestSelection);
            Run("Navigation versus execution", TestOpenKeys);
            Run("Fuzzy Jump", TestFuzzy);
            Run("Junction boundaries", TestJunctions);
            Run("Commands and Unicode", TestCommands);
            Run("Workspace and panes", TestWorkspace);
            Run("Single instance", TestSingleInstance);
            Run("Status bar and Escape", TestStatusAndEscape);
        }
        finally
        {
            // A launched command can briefly retain its working-directory handle after UI completion.
            for (int attempt = 0; ; attempt++)
            {
                try { Directory.Delete(root, true); break; }
                catch (IOException)
                {
                    if (attempt == 99) throw;
                    Thread.Sleep(20);
                }
            }
        }
        Console.WriteLine("Checks: " + checks + ", failures: " + failures);
        return failures == 0 ? 0 : 1;
    }

    private static void Run(string name, Action test)
    {
        try { test(); }
        catch (Exception ex) { failures++; Console.WriteLine("FAIL " + name + ": " + ex); }
    }

    private static void Check(bool condition, string name)
    {
        checks++;
        if (!condition) failures++;
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
    }

    private static string Dir(string relative)
    {
        string path = Path.Combine(root, relative);
        Directory.CreateDirectory(path);
        return path;
    }

    private static string Put(string directory, string name, string contents)
    {
        string path = Path.Combine(directory, name);
        File.WriteAllText(path, contents, new UTF8Encoding(false));
        return path;
    }

    private static object Call(object target, string name, params object[] args)
    {
        try { return target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args); }
        catch (TargetInvocationException ex) { throw ex.InnerException; }
    }

    private static T Field<T>(object target, string name)
    {
        return (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }

    private static void Key(FilePane pane, params Keys[] keys)
    {
        foreach (Keys key in keys) Call(pane, "FileList_KeyDown", pane, new KeyEventArgs(key));
    }

    private static void NativeKey(Control control, Keys key)
    {
        IntPtr handle = control.Handle;
        PostMessage(handle, 0x100, (IntPtr)key, (IntPtr)1);
        PostMessage(handle, 0x101, (IntPtr)key, (IntPtr)1);
        Application.DoEvents();
    }

    private static void Dialogs(Action action, string overwrite = "Skip All", int answer = 1, Action<SelectionList> choose = null)
    {
        dialogs = 0;
        dialogText = "";
        overwriteDefault = "";
        messageDefault = 0;
        using (System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer())
        {
            timer.Interval = 20;
            timer.Tick += delegate
            {
                foreach (Form form in Application.OpenForms)
                {
                    SelectionList selection = form as SelectionList;
                    if (selection != null && form.Visible)
                    {
                        dialogs++;
                        if (choose != null) choose(selection);
                        else Call(selection, "List_KeyDown", selection, new KeyEventArgs(Keys.Escape));
                        return;
                    }
                    if (form.Text == "vipane12 - Confirm" && form.Visible)
                    {
                        dialogs++;
                        overwriteDefault = form.ActiveControl == null ? "" : form.ActiveControl.Text;
                        foreach (Control control in form.Controls)
                        {
                            Button button = control as Button;
                            if (button != null && button.Text == overwrite) { button.PerformClick(); return; }
                        }
                    }
                }
                // Only inspect dialogs belonging to this test thread, never the user's app.
                EnumThreadWindows(GetCurrentThreadId(), delegate(IntPtr window, IntPtr unused)
                {
                    StringBuilder text = new StringBuilder(4096);
                    GetClassName(window, text, text.Capacity);
                    if (text.ToString() != "#32770") return true;
                    dialogs++;
                    GetWindowText(GetDlgItem(window, 0xffff), text, text.Capacity);
                    dialogText = text.ToString();
                    messageDefault = (int)SendMessage(window, 0x400, IntPtr.Zero, IntPtr.Zero) & 0xffff;
                    IntPtr button = GetDlgItem(window, answer);
                    // An OK-only MessageBox can expose its sole button as IDCANCEL.
                    if (button == IntPtr.Zero && answer == 1) button = GetDlgItem(window, 2);
                    PostMessage(button, 0xf5, IntPtr.Zero, IntPtr.Zero);
                    return false;
                }, IntPtr.Zero);
            };
            timer.Start();
            action();
            timer.Stop();
        }
    }

    private static void TestFiles()
    {
        string src = Dir("copy-source");
        string dst = Dir("copy-destination");
        string name = "\u65e5\u672c\u8a9e & [x] {brace}+plus=eq %PATH% !.txt";
        string file = Put(src, name, "original");
        Dialogs(delegate { FileOperations.CopyPaths(new string[] { file }, dst, null); });
        Check(dialogs == 0 && File.ReadAllText(Path.Combine(dst, name)) == "original", "copy Unicode, spaces and symbols literally");

        string folder = Dir("self\\{brace}+plus=eq");
        Dialogs(delegate { FileOperations.CopyPaths(new string[] { folder }, folder, null); });
        Check(dialogs == 1 && Directory.GetFileSystemEntries(folder).Length == 0, "self copy rejected without recursive directories");
        string child = Directory.CreateDirectory(Path.Combine(folder, "child")).FullName;
        Dialogs(delegate { FileOperations.MovePaths(new string[] { folder }, child, null); });
        Check(dialogs == 1 && Directory.GetFileSystemEntries(child).Length == 0, "move into descendant rejected before mutation");
        string aliasSource = Dir("short name alias source");
        StringBuilder shortName = new StringBuilder(260);
        if (GetShortPathName(aliasSource, shortName, (uint)shortName.Capacity) == 0) throw new Exception("Could not query short path.");
        if (string.Equals(aliasSource, shortName.ToString(), StringComparison.OrdinalIgnoreCase))
            Console.WriteLine("SKIP short-name alias: no distinct 8.3 name on this volume");
        else
        {
            Dialogs(delegate { FileOperations.CopyPaths(new string[] { aliasSource }, shortName.ToString(), null); });
            Check(dialogs == 1 && Directory.GetFileSystemEntries(aliasSource).Length == 0, "short-name alias cannot bypass self-copy guard");
        }
        string ancestor = Dir("ancestor\\Box");
        string nested = Directory.CreateDirectory(Path.Combine(ancestor, "Box")).FullName;
        Put(nested, "keep.txt", "keep");
        Dialogs(delegate { FileOperations.CopyPaths(new string[] { nested }, Path.GetDirectoryName(ancestor), null); }, "Replace All");
        Check(dialogs == 1 && File.Exists(Path.Combine(nested, "keep.txt")), "ancestor replacement cannot delete its own source");

        using (FileStream locked = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Put(dst, name, "old destination");
            Dialogs(delegate { FileOperations.CopyPaths(new string[] { file }, dst, null); }, "Replace All");
            Check(dialogs == 2 && File.ReadAllText(Path.Combine(dst, name)) == "old destination", "unreadable source does not pre-delete destination");
        }
        Check(overwriteDefault == "Skip All", "overwrite dialog focuses safe default");

        string merge = Dir("copy-source\\merge");
        Put(merge, "replace.txt", "new");
        string existing = Directory.CreateDirectory(Path.Combine(dst, "merge")).FullName;
        Put(existing, "replace.txt", "old");
        Put(existing, "destination-only.txt", "keep");
        Dialogs(delegate { FileOperations.CopyPaths(new string[] { merge }, dst, null); }, "Replace All");
        Check(dialogs == 1 && File.ReadAllText(Path.Combine(existing, "replace.txt")) == "new"
            && File.ReadAllText(Path.Combine(existing, "destination-only.txt")) == "keep", "directory replace merges and preserves destination-only files");

        string mismatch = Put(Dir("type-source"), "merge", "file");
        Dialogs(delegate { FileOperations.CopyPaths(new string[] { mismatch }, dst, null); }, "Replace All");
        Check(dialogs == 1 && File.Exists(Path.Combine(existing, "destination-only.txt")), "file/directory conflict leaves both sides intact");

        string second = Put(src, "second.txt", "second");
        string free = Put(src, "free.txt", "free");
        Put(dst, "second.txt", "old second");
        Dialogs(delegate { FileOperations.CopyPaths(new string[] { file, second, free }, dst, null); });
        Check(dialogs == 1 && File.ReadAllText(Path.Combine(dst, "second.txt")) == "old second"
            && File.Exists(Path.Combine(dst, "free.txt")), "Skip All applies to every collision but still copies new items");
        string later = Put(src, "later.txt", "later");
        Dialogs(delegate { FileOperations.CopyPaths(new string[] { second, later }, dst, null); }, "Cancel");
        Check(dialogs == 1 && !File.Exists(Path.Combine(dst, "later.txt")), "Cancel stops remaining transfers");
        string[] remaining = null;
        Dialogs(delegate { remaining = FileOperations.MovePaths(new string[] { second, later }, dst, null); });
        Check(remaining.Length == 1 && remaining[0] == second && File.Exists(second)
            && !File.Exists(later) && File.Exists(Path.Combine(dst, "later.txt")), "move returns only skipped or failed sources");

        string move = Put(src, "move.txt", "move");
        using (FilePane pane = new FilePane())
        {
            pane.SetPath(dst);
            AppClipboard.SetMove(new string[] { move });
            Call(pane, "Paste");
            Check(!AppClipboard.HasData && !File.Exists(move), "successful move clears pending clipboard paths");
            Put(src, "move.txt", "new unrelated file");
            Call(pane, "Paste");
            Check(File.Exists(move), "second paste cannot move a new file at the old source path");
        }

        string longDir = Dir(new string('z', 230 - root.Length - 1));
        string longName = Put(src, new string('n', 100) + ".txt", "data");
        Dialogs(delegate { FileOperations.CopyPaths(new string[] { longName }, longDir, null); });
        Check(dialogs == 1 && Directory.GetFileSystemEntries(longDir).Length == 0, "genuinely long destination rejected before writing");

        string permanent = Put(src, "permanent.txt", "delete only this fixture");
        Dialogs(delegate { FileOperations.DeletePermanent(new string[] { permanent }, null); });
        Check(dialogs == 1 && !File.Exists(permanent), "confirmed permanent deletion removes fixture");
        string readOnly = Put(src, "readonly.txt", "keep attributes");
        File.SetAttributes(readOnly, FileAttributes.ReadOnly | FileAttributes.Hidden);
        try
        {
            Dialogs(delegate { FileOperations.DeletePermanent(new string[] { readOnly }, null); });
            Check(dialogs == 2 && File.Exists(readOnly) && (File.GetAttributes(readOnly) & FileAttributes.Hidden) != 0,
                "failed delete does not silently clear read-only or hidden attributes");
        }
        finally { File.SetAttributes(readOnly, FileAttributes.Normal); }
    }

    private static void TestSelection()
    {
        string path = Dir("selection");
        for (int i = 0; i < 6; i++) Put(path, i + ".txt", i.ToString());
        using (Form form = new Form())
        using (FilePane pane = new FilePane())
        {
            pane.Dock = DockStyle.Fill;
            form.Controls.Add(pane);
            pane.SetPath(path);
            form.Show();
            pane.FocusList();
            Application.DoEvents();
            ListBox list = Field<ListBox>(pane, "fileList");
            PostMessage(list.Handle, 0x100, (IntPtr)Keys.J, (IntPtr)1);
            Application.DoEvents();
            Check(pane.CursorPath == Path.Combine(path, "1.txt"), "file pane receives native j key message");
            Key(pane, Keys.K);
            TextBox input = Field<TextBox>(pane, "pathInput");
            PostMessage(list.Handle, 0x100, (IntPtr)Keys.E, (IntPtr)1);
            Application.DoEvents();
            input.Text = "";
            PostMessage(input.Handle, 0x102, (IntPtr)'j', (IntPtr)1);
            Application.DoEvents();
            Check(input.Focused && input.Text == "j", "path TextBox accepts ordinary j text, not navigation");
            PostMessage(input.Handle, 0x100, (IntPtr)Keys.Escape, (IntPtr)1);
            Application.DoEvents();
            Check(list.Focused && input.Text == path, "native Esc restores path and list focus");
            Key(pane, Keys.Y, Keys.Y);
            Check(AppClipboard.GetPaths()[0] == Path.Combine(path, "0.txt"), "yy copies cursor when no marks exist");
            Key(pane, Keys.Space, Keys.Space, Keys.J, Keys.D, Keys.D);
            Check(AppClipboard.IsMove && AppClipboard.GetPaths()[0] == Path.Combine(path, "1.txt"), "dd works after Space deselects");
            Key(pane, Keys.Space, Keys.J, Keys.J, Keys.Space);
            Check(pane.Selection.Length == 2, "Space makes non-contiguous selections");
            Key(pane, Keys.V, Keys.J, Keys.K, Keys.K);
            Check(pane.CursorPath == Path.Combine(path, "2.txt") && pane.Selection.Length == 3, "range shrinks in both directions without caret drift");
            Key(pane, Keys.Escape);
            Check(pane.Selection.Length == 2 && pane.CursorPath == Path.Combine(path, "2.txt"), "Esc restores base marks without moving cursor");
            Key(pane, Keys.Control | Keys.C);
            Check(AppClipboard.GetPaths().Length == 2, "explicit marks take priority over cursor fallback");
            list.ClearSelected();
            Key(pane, Keys.V, Keys.J, Keys.Escape, Keys.Y, Keys.Y);
            Check(AppClipboard.GetPaths().Length == 1 && AppClipboard.GetPaths()[0] == pane.CursorPath, "yy works after cancelling v with empty base");

            Dialogs(delegate { Key(pane, Keys.X); }, answer: 7);
            Check(dialogs == 1 && messageDefault == 7 && File.Exists(pane.CursorPath), "unmarked x confirms cursor deletion and defaults to No");
            Dialogs(delegate { Key(pane, Keys.Shift | Keys.Delete); }, answer: 2);
            Check(dialogs == 1 && messageDefault == 2 && File.Exists(pane.CursorPath), "permanent delete defaults to Cancel");

            AppClipboard.SetCopy(new string[] { "sentinel" });
            Key(pane, Keys.Y, Keys.Control | Keys.A, Keys.Y);
            Check(AppClipboard.GetPaths()[0] == "sentinel", "unrelated modified key cancels yy sequence");
            pane.FocusPathInput();
            pane.FocusList();
            Key(pane, Keys.Y);
            Check(AppClipboard.GetPaths()[0] == "sentinel", "focus change cancels pending sequence");
            Key(pane, Keys.Shift | Keys.X);
            Check(AppClipboard.GetPaths()[0] == "sentinel", "Shift+letter does not execute plain Vim binding");

            Key(pane, Keys.Escape);
            list.ClearSelected();
            list.SetSelected(1, true);
            File.Delete(Path.Combine(path, "0.txt"));
            pane.RefreshPane();
            Check(pane.Selection.Length == 0, "refresh does not transfer marks to a different file by row index");
            SendMessage(list.Handle, 0x128, (IntPtr)0x10001, IntPtr.Zero);
            Key(pane, Keys.J);
            Check(((int)SendMessage(list.Handle, 0x129, IntPtr.Zero, IntPtr.Zero) & 1) == 0, "keyboard reveals focus rectangle after mouse hides it");
            Key(pane, Keys.V, Keys.J);
            Rectangle r = list.GetItemRectangle(0);
            Call(pane, "FileList_MouseDown", list, new MouseEventArgs(MouseButtons.Right, 1, r.X + 2, r.Y + 2, 0));
            Key(pane, Keys.Escape);
            Check(pane.CursorPath == Path.Combine(path, "1.txt") && pane.Selection.Length == 1, "mouse selection ends range and updates right-click cursor");

            string old = pane.CurrentPath;
            Dialogs(delegate { pane.SetPath(Path.Combine(path, "absent")); });
            Check(dialogs == 1 && pane.CurrentPath == old && list.Items.Count == 5, "failed navigation preserves current path and list");
            string denied = Dir("access-denied");
            DirectorySecurity blocked = Directory.GetAccessControl(denied);
            FileSystemAccessRule rule = new FileSystemAccessRule(WindowsIdentity.GetCurrent().User, FileSystemRights.ListDirectory, AccessControlType.Deny);
            blocked.AddAccessRule(rule);
            try
            {
                Directory.SetAccessControl(denied, blocked);
                Dialogs(delegate { pane.SetPath(denied); });
                Check(dialogs == 1 && pane.CurrentPath == old && list.Items.Count == 5, "access-denied navigation is atomic");
            }
            finally
            {
                DirectorySecurity restore = Directory.GetAccessControl(denied);
                restore.RemoveAccessRuleSpecific(rule);
                Directory.SetAccessControl(denied, restore);
            }

            string paneSrc = Directory.CreateDirectory(Path.Combine(path, "src")).FullName;
            Call(pane, "EnterPath", "src");
            Check(pane.CurrentPath == paneSrc, "bare Fuzzy token resolves below pane, not process cwd");
            Call(pane, "EnterPath", "..\\src");
            Check(pane.CurrentPath == paneSrc, "explicit relative path resolves against pane");
            Key(pane, Keys.Y, Keys.Y);
            Check(!AppClipboard.HasData, "copy in empty folder clears stale clipboard");
            Directory.Delete(paneSrc);
            Dialogs(delegate { pane.RefreshPane(); });
            Check(dialogs == 1 && list.Items.Count == 0 && pane.CursorPath == null, "missing current folder does not leave actionable stale rows");
            form.Close();
        }
    }

    private static void TestFuzzy()
    {
        string path = Dir("fuzzy");
        string goal = Dir("fuzzy\\Project A\\Design\\10foobar\\inner\\20bar");
        string first = Path.GetDirectoryName(Path.GetDirectoryName(goal));
        Dir("fuzzy\\Project A\\Design\\10foobar_old");
        string dest;
        Check(FuzzyJump.TryJump(null, path, "10foobar", out dest) && dest == first, "exact match wins over prefix");
        Check(FuzzyJump.TryJump(null, path, "10FOOBAR", out dest) && dest == first, "case-insensitive exact match");
        dest = null;
        bool ok = false;
        Dialogs(delegate { ok = FuzzyJump.TryJump(null, path, "10fo/20ba", out dest); },
            choose: delegate(SelectionList selection) { Call(selection, "List_KeyDown", selection, new KeyEventArgs(Keys.Enter)); });
        Check(ok && dialogs == 1 && dest == goal, "hierarchical resolution selects one token at a time");
        Check(FuzzyJump.TryJump(null, path, "10FOOBRA", out dest) && dest == first, "case-insensitive transposition distance");
        string wide = Dir("fuzzy\\\uff11\uff10_\u8a2d\u8a08\u66f8");
        Check(FuzzyJump.TryJump(null, path, "10_\u8a2d", out dest) && dest == wide, "normalized full-width prefix keeps original path");
        string kana = Dir("fuzzy\\\uff84\uff9e\uff97\uff72\uff8a\uff9e");
        Check(FuzzyJump.TryJump(null, path, "\u30c9\u30e9", out dest) && dest == kana, "half-width kana normalizes for comparison only");
        string combining = Dir("fuzzy\\e\u0301cole");
        Check(FuzzyJump.TryJump(null, path, "\u00e9col", out dest) && dest == combining, "combining characters normalize without renaming");
        string astral = Dir("fuzzy\\\uD842\uDFB7\u7530");
        Check(FuzzyJump.TryJump(null, path, "\uD842\uDFB7\u5c71", out dest) && dest == astral, "supplementary character counts as one text element");

        bool rejected = false;
        dest = null;
        try { FuzzyJump.TryJump(null, path, "10foobar/zzzz-no-match", out dest); }
        catch (DirectoryNotFoundException) { rejected = true; }
        Check(rejected && dest == null, "later-token failure never returns a partial destination");
        rejected = false;
        try { FuzzyJump.TryJump(null, path, "10foobar/", out dest); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "empty token rejected before traversal");
        rejected = false;
        try { FuzzyJump.TryJump(null, Path.GetPathRoot(path), "anything", out dest); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Fuzzy Jump refuses whole-drive scans");
        using (Form form = new Form())
        using (FilePane pane = new FilePane())
        {
            form.Controls.Add(pane);
            pane.SetPath(path);
            form.Show();
            pane.FocusPathInput();
            Dialogs(delegate { Call(pane, "EnterPath", "10fo"); });
            Check(dialogs == 1 && pane.CurrentPath == path, "candidate cancellation preserves path without a second error dialog");
            Dialogs(delegate { Call(pane, "EnterPath", "10foobar/zzzz-no-match"); });
            Check(dialogs == 1 && pane.CurrentPath == path, "failed hierarchical input leaves pane path unchanged");
            form.Close();
        }
        using (SelectionList selection = new SelectionList("Test selection", new string[] { "A", "B", "C" }, new string[] { "a", "b", "c" }))
        {
            selection.Show();
            ListBox list = Field<ListBox>(selection, "list");
            PostMessage(list.Handle, 0x100, (IntPtr)Keys.J, (IntPtr)1);
            Application.DoEvents();
            Check(list.SelectedIndex == 1, "SelectionList receives native j key message");
            Call(selection, "List_KeyDown", list, new KeyEventArgs(Keys.K));
            Check(list.SelectedIndex == 0, "SelectionList k moves up");
            Call(selection, "List_KeyDown", list, new KeyEventArgs(Keys.L));
            Check(selection.Result == null && selection.Visible, "command selection does not accept l");
            NativeKey(list, Keys.Enter);
            Check(selection.Result == "a", "SelectionList Enter returns selected value");
        }
    }

    private static void TestJunctions()
    {
        string path = Dir("junction-root");
        string outside = Dir("junction-outside");
        Directory.CreateDirectory(Path.Combine(outside, "outside-only"));
        string link = Path.Combine(path, "link");
        ProcessStartInfo start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"), "/d /c mklink /J \"" + link + "\" \"" + outside + "\"");
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        using (Process process = Process.Start(start)) { process.WaitForExit(); if (process.ExitCode != 0) throw new Exception("Could not create test junction."); }
        try
        {
            string dest;
            bool rejected = false;
            try { FuzzyJump.TryJump(null, path, "outside-only", out dest); }
            catch (DirectoryNotFoundException) { rejected = true; }
            Check(rejected, "Fuzzy Jump never follows junction outside root");
            string destination = Dir("junction-copy");
            Dialogs(delegate { FileOperations.CopyPaths(new string[] { path }, destination, null); });
            Check(dialogs == 1 && Directory.GetFileSystemEntries(destination).Length == 0, "copy rejects source junction before creating destination");
            string file = Put(Dir("junction-source"), "file.txt", "data");
            Dialogs(delegate { FileOperations.CopyPaths(new string[] { file }, link, null); });
            Check(dialogs == 1 && !File.Exists(Path.Combine(outside, "file.txt")), "copy rejects destination junction");
        }
        finally { Directory.Delete(link); }
    }

    private static void TestCommands()
    {
        string working = Dir("command %PATH% ! & \u65e5\u672c\u8a9e");
        string selected = Put(working, "a %PATH% ! ^ & \uD842\uDFB7 e\u0301.txt", "test");
        string command = Put(root, "command %PATH% ! &.cmd", "@echo off\r\nsetlocal DisableDelayedExpansion\r\nset \"RESULT_ONE=%~1\"\r\nset \"RESULT_TWO=%~2\"\r\ncmd.exe /d /u /c set RESULT_ > \"%~dp0result.txt\"\r\n> \"%~dp0done.txt\" echo done\r\n");
        Dialogs(delegate { CommandRunner.Run(command, selected, working, null); });
        string result = Path.Combine(root, "result.txt");
        Stopwatch watch = Stopwatch.StartNew();
        while (!File.Exists(Path.Combine(root, "done.txt")) && watch.ElapsedMilliseconds < 5000) Thread.Sleep(20);
        string text = File.ReadAllText(result, Encoding.Unicode);
        Check(dialogs == 0 && text.Contains("RESULT_ONE=" + selected) && text.Contains("RESULT_TWO=" + working), "command receives literal %, !, &, Unicode and combining characters");
        File.Delete(Path.Combine(root, "done.txt"));
        Dialogs(delegate { CommandRunner.Run(command, null, Path.GetPathRoot(root), null); });
        watch.Restart();
        while (!File.Exists(Path.Combine(root, "done.txt")) && watch.ElapsedMilliseconds < 5000) Thread.Sleep(20);
        text = File.ReadAllText(result, Encoding.Unicode);
        Check(!text.Contains("RESULT_ONE=") && text.Contains("RESULT_TWO=" + Path.GetPathRoot(root)), "command handles empty selection and trailing root separator");
        string commands = Workspace.GetCommandsDirectory();
        Directory.CreateDirectory(commands);
        Put(commands, "B.cmd", "@echo off");
        Put(commands, "A.cmd", File.ReadAllText(command));
        Put(commands, "C.cmdx", "@echo off");
        string[] list = CommandRunner.ListCommandFiles(null);
        Check(list.Length == 2 && Path.GetFileName(list[0]) == "A.cmd", "command enumeration excludes cmdx and sorts names");
        Put(working, "z-cursor.txt", "not selected");
        using (Form form = new Form())
        using (FilePane pane = new FilePane())
        {
            form.Controls.Add(pane);
            pane.SetPath(working);
            form.Show();
            pane.FocusList();
            Field<ListBox>(pane, "fileList").SetSelected(0, true);
            Call(pane, "SetCaret", 1);
            Dialogs(delegate { Key(pane, Keys.C); }, choose: delegate(SelectionList selection)
            {
                Call(selection, "List_KeyDown", selection, new KeyEventArgs(Keys.J));
                Call(selection, "List_KeyDown", selection, new KeyEventArgs(Keys.K));
                Call(selection, "List_KeyDown", selection, new KeyEventArgs(Keys.L));
                Check(selection.Visible && selection.Result == null, "l in command picker does not launch a command");
                NativeKey(Field<ListBox>(selection, "list"), Keys.Enter);
            });
            watch.Restart();
            while (!File.Exists(Path.Combine(commands, "done.txt")) && watch.ElapsedMilliseconds < 5000) Thread.Sleep(20);
            text = File.ReadAllText(Path.Combine(commands, "result.txt"), Encoding.Unicode);
            Check(dialogs == 1 && text.Contains("RESULT_ONE=" + selected), "c selects a command with j/k/Enter and passes explicit selection, not unrelated cursor");
            form.Close();
        }
    }

    private static void TestWorkspace()
    {
        string unicode = Dir("workspace-\u4ed5\u4e8b-\uD842\uDFB7-e\u0301");
        string other = Dir("workspace-other");
        string[] paths = new string[12];
        for (int i = 0; i < paths.Length; i++) paths[i] = unicode;
        paths[2] = other;
        Workspace.Save(paths);
        string[] restored = Workspace.Load();
        Check(restored.Length == 12 && restored[0] == unicode && restored[2] == other, "workspace preserves original Unicode paths");
        File.WriteAllLines(Workspace.GetWorkspacePath(), new string[] { unicode, "", Path.Combine(root, "missing") }, new UTF8Encoding(false));
        restored = Workspace.Load();
        Check(restored.Length == 12 && restored[0] == unicode && restored[1] == Workspace.GetDefaultDirectory()
            && restored[11] == Workspace.GetDefaultDirectory(), "short workspace and missing paths use per-pane defaults");
        using (FileStream locked = new FileStream(Workspace.GetWorkspacePath(), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Dialogs(delegate { Workspace.Load(); });
            Check(dialogs == 1, "workspace read errors are reported instead of silently ignored");
        }
        Workspace.Save(paths);
        Put(unicode, "a.txt", "a");
        Put(other, "b.txt", "b");
        using (MainForm main = new MainForm())
        {
            main.Show();
            Application.DoEvents();
            FilePane[] panes = Field<FilePane[]>(main, "panes");
            panes[0].FocusList();
            Key(panes[0], Keys.Control | Keys.J);
            Check(Field<FilePane>(main, "activePane") == panes[4], "Ctrl+j moves to adjacent pane");
            Key(panes[4], Keys.Control | Keys.K);
            Check(Field<FilePane>(main, "activePane") == panes[0], "Ctrl+k moves back");
            Key(panes[0], Keys.Control | Keys.H);
            Check(Field<FilePane>(main, "activePane") == panes[0], "pane edge does not wrap");
            Field<ListBox>(panes[2], "fileList").SetSelected(0, true);
            Call(main, "Pane_FilesChanged", (object)new string[] { Path.Combine(unicode, "a.txt") });
            Check(panes[2].Selection.Length == 1, "unrelated pane retains selection after file operation");
            main.Close();
        }
    }

    private static void TestSingleInstance()
    {
        string key = @"Local\ViPane12.test." + Guid.NewGuid().ToString("N");
        string firstDir = Dir("instance one");
        string secondDir = Dir("instance two");
        string firstExe = Path.Combine(firstDir, "ViPane12.exe");
        string secondExe = Path.Combine(secondDir, "Another copy.exe");
        File.Copy(Assembly.GetExecutingAssembly().Location, firstExe);
        File.Copy(Assembly.GetExecutingAssembly().Location, secondExe);
        foreach (string folder in new string[] { firstDir, secondDir })
        {
            Put(folder, "portable.flag", "");
            string work = Directory.CreateDirectory(Path.Combine(folder, "work")).FullName;
            Put(work, "sample.txt", "fixture");
            string[] saved = new string[12];
            for (int i = 0; i < saved.Length; i++) saved[i] = work;
            File.WriteAllLines(Path.Combine(folder, "workspace.txt"), saved, new UTF8Encoding(false));
        }
        string firstWorkspace = File.ReadAllText(Path.Combine(firstDir, "workspace.txt"));
        string secondWorkspace = File.ReadAllText(Path.Combine(secondDir, "workspace.txt"));
        Guid current = Program.GetCurrentDesktopId();
        Guid other = Guid.Empty;
        Program.IVirtualDesktopManager manager = Program.CreateDesktopManager();
        List<Process> children = new List<Process>();
        try
        {
            // Read existing desktop IDs, but never create desktops or move another application's windows.
            EnumWindows(delegate(IntPtr window, IntPtr unused)
            {
                try
                {
                    Guid id = manager.GetWindowDesktopId(window);
                    if (id != Guid.Empty && id != current) other = id;
                }
                catch (COMException) { }
                return true;
            }, IntPtr.Zero);
            Process first = StartInstanceHost(firstExe, key, other);
            children.Add(first);
            WaitFor(delegate { return File.Exists(Path.Combine(firstDir, "ready.txt")) || first.HasExited; });
            if (first.HasExited) throw new Exception(first.StandardError.ReadToEnd());
            string firstReady = File.ReadAllText(Path.Combine(firstDir, "ready.txt"));
            Check(firstReady == "ready", "primary test process can move its own window before handoff");
            IntPtr primary = Program.FindInstanceWindow(key);
            WaitFor(delegate { return IsWindowVisible(primary) && manager.GetWindowDesktopId(primary) != Guid.Empty; });
            Check(primary != IntPtr.Zero, "registered primary is a separate process");
            if (other != Guid.Empty)
            {
                WaitFor(delegate { return manager.GetWindowDesktopId(primary) == other; });
                Check(manager.GetWindowDesktopId(primary) == other, "primary starts on the non-current test desktop");
            }
            Check(SendProbe(primary, TestState) == 1, "primary starts with marked selection, clipboard and Last Action");
            bool rejected = false;
            try { Program.MoveWindowToDesktop(primary, current); }
            catch (UnauthorizedAccessException) { rejected = true; }
            Check(rejected, "a secondary process cannot directly move the primary window");

            using (Form reference = new Form())
            {
                reference.Show();
                Application.DoEvents();
                if (other != Guid.Empty)
                {
                    SetForegroundWindow(reference.Handle);
                    Check(SendProbe(primary, TestMoveOnly) == 1, "owning process accepts an asynchronous desktop move request");
                    WaitFor(delegate { return SendProbe(primary, TestMoveStatus) != -1; });
                    Check(SendProbe(primary, TestMoveStatus) == 1 && manager.GetWindowDesktopId(primary) == other,
                        "owning process places its window on a genuinely different desktop");
                    Check(Program.GetCurrentDesktopId() == current, "test setup does not switch the user's current desktop");
                }
                else Console.WriteLine("SKIP cross-desktop setup: no second desktop with a window is available");

                Process second = StartInstanceHost(secondExe, key, Guid.Empty);
                children.Add(second);
                WaitFor(delegate { return second.HasExited; });
                Check(second.ExitCode == 0, "launching a differently named copy exits after owner-side desktop transfer: " + second.StandardError.ReadToEnd());
                Check(!first.HasExited && Program.FindInstanceWindow(key) == primary, "relaunch reuses the same primary HWND and process");
                Check(manager.GetWindowDesktopId(primary) == current && Program.GetCurrentDesktopId() == current,
                    "primary is brought to the current desktop without switching the user's desktop");
                Check(SendProbe(primary, TestState) == 1 && File.ReadAllText(Path.Combine(firstDir, "workspace.txt")) == firstWorkspace
                    && File.ReadAllText(Path.Combine(secondDir, "workspace.txt")) == secondWorkspace && !File.Exists(Path.Combine(secondDir, "ready.txt")),
                    "relaunch preserves primary UI state and does not load or save the second workspace");

                SendProbe(primary, TestOpenDialog);
                WaitFor(delegate { return GetLastActivePopup(primary) != primary && IsWindowVisible(GetLastActivePopup(primary)); });
                IntPtr popup = GetLastActivePopup(primary);
                if (other != Guid.Empty)
                {
                    SetForegroundWindow(reference.Handle);
                    Check(SendProbe(primary, TestMoveOnly) == 1, "owner-side transfer accepts a modal-window move request");
                    WaitFor(delegate { return SendProbe(primary, TestMoveStatus) != -1; });
                    Check(SendProbe(primary, TestMoveStatus) == 1, "owner-side transfer includes an open modal dialog");
                }
                Process withDialog = StartInstanceHost(secondExe, key, Guid.Empty);
                children.Add(withDialog);
                WaitFor(delegate { return withDialog.HasExited; });
                Check(withDialog.ExitCode == 0 && manager.IsWindowOnCurrentVirtualDesktop(popup)
                    && manager.GetWindowDesktopId(primary) == current, "relaunch brings the primary and its modal dialog to this desktop");
                Check(GetForegroundWindow() == popup, "activation targets the modal dialog rather than the disabled main window");
                PostMessage(popup, 0x10, IntPtr.Zero, IntPtr.Zero);
                WaitFor(delegate { return GetLastActivePopup(primary) == primary || !IsWindowVisible(popup); });

                ShowWindow(primary, 3);
                ShowWindow(primary, 6);
                Check(IsIconic(primary), "primary is minimized from a maximized placement");
                Guid invalidDesktop = Guid.NewGuid();
                int failed = Program.ActivationNotReady;
                WaitFor(delegate
                {
                    failed = Program.SendActivationRequest(primary, invalidDesktop);
                    return failed != Program.ActivationNotReady;
                });
                Check(failed < 0 && IsIconic(primary) && manager.GetWindowDesktopId(primary) == current,
                    "failed desktop transfer reports an error without restoring or activating the old desktop");

                Program.CopyData malformed = new Program.CopyData();
                malformed.Tag = new IntPtr(Program.ActivationData);
                malformed.Size = 0;
                malformed.Data = IntPtr.Zero;
                Check(SendMessage(primary, Program.CopyDataMessage, IntPtr.Zero, ref malformed) == IntPtr.Zero && IsIconic(primary),
                    "malformed activation payload is rejected without changing window state");
                Process restore = StartInstanceHost(secondExe, key, Guid.Empty);
                children.Add(restore);
                WaitFor(delegate { return restore.HasExited; });
                Check(restore.ExitCode == 0 && !IsIconic(primary) && IsZoomed(primary), "relaunch restores a maximized window without losing its placement");

                using (Mutex keepMutexAlive = new Mutex(false, key))
                {
                    first.Kill();
                    first.WaitForExit();
                    Process restarted = StartInstanceHost(secondExe, key, Guid.Empty);
                    children.Add(restarted);
                    WaitFor(delegate { return File.Exists(Path.Combine(secondDir, "ready.txt")) || restarted.HasExited; });
                    Check(!restarted.HasExited && Program.FindInstanceWindow(key) != IntPtr.Zero,
                        "new process acquires an abandoned mutex and opens its own workspace");
                    PostMessage(Program.FindInstanceWindow(key), 0x10, IntPtr.Zero, IntPtr.Zero);
                    WaitFor(delegate { return restarted.HasExited; });
                    Check(restarted.ExitCode == 0, "restarted primary shuts down cleanly");
                }
                reference.Close();
            }
            Check(Program.FindInstanceWindow(key) == IntPtr.Zero, "closing primary removes the instance window marker");
        }
        finally
        {
            foreach (Process child in children)
            {
                if (!child.HasExited)
                {
                    child.CloseMainWindow();
                    if (!child.WaitForExit(3000)) { child.Kill(); child.WaitForExit(); }
                }
                child.Dispose();
            }
            Marshal.ReleaseComObject(manager);
        }
    }

    private static void WaitFor(Func<bool> condition)
    {
        Stopwatch wait = Stopwatch.StartNew();
        while (!condition())
        {
            if (wait.ElapsedMilliseconds >= 10000) throw new TimeoutException("Test process did not reach the expected state.");
            Application.DoEvents();
            Thread.Sleep(20);
        }
    }

    private static Process StartInstanceHost(string executable, string key, Guid other)
    {
        ProcessStartInfo start = new ProcessStartInfo(executable, "--instance-host " + key + " " + other.ToString());
        start.WorkingDirectory = Path.GetDirectoryName(executable);
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.RedirectStandardError = true;
        return Process.Start(start);
    }

    private static int SendProbe(IntPtr window, int message)
    {
        IntPtr result;
        if (SendMessageTimeout(window, message, IntPtr.Zero, IntPtr.Zero, 0x22, 5000, out result) == IntPtr.Zero)
            throw new TimeoutException("Test window did not respond.");
        return unchecked((int)result.ToInt64());
    }

    private static int RunInstanceHost(string[] args)
    {
        InstanceTestWindow bridge = null;
        EventHandler ready = null;
        ready = delegate
        {
            foreach (Form form in Application.OpenForms)
            {
                MainForm main = form as MainForm;
                if (main == null || !main.Visible) continue;
                Application.Idle -= ready;
                FilePane pane = Field<FilePane[]>(main, "panes")[0];
                Field<ListBox>(pane, "fileList").SetSelected(0, true);
                Call(pane, "CopySelection");
                bridge = new InstanceTestWindow(main, new Guid(args[2]));
                Guid initialDesktop = new Guid(args[2]);
                if (initialDesktop == Guid.Empty)
                {
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ready.txt"), "ready");
                }
                else
                {
                    main.BeginInvoke(new MethodInvoker(delegate
                    {
                        try
                        {
                            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ready.txt"),
                                Program.MoveWindowToDesktop(main.Handle, initialDesktop) ? "ready" : "move-failed");
                        }
                        catch (Exception ex) { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ready.txt"), "error:" + ex.GetType().Name); }
                    }));
                }
                break;
            }
        };
        Application.Idle += ready;
        try { Program.RunInstance(args[1]); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally
        {
            Application.Idle -= ready;
            if (bridge != null) bridge.ReleaseHandle();
        }
    }

    private sealed class InstanceTestWindow : NativeWindow
    {
        private readonly MainForm main;
        private readonly Guid other;
        private readonly string originalPath;
        private int moveResult = -1;

        public InstanceTestWindow(MainForm main, Guid other)
        {
            this.main = main;
            this.other = other;
            originalPath = Field<FilePane[]>(main, "panes")[0].CurrentPath;
            AssignHandle(main.Handle);
        }

        protected override void WndProc(ref Message message)
        {
            try
            {
                if (message.Msg == TestMoveOnly)
                {
                    moveResult = -1;
                    main.BeginInvoke(new MethodInvoker(delegate
                    {
                        try { moveResult = Program.MoveWindowToDesktop(Handle, other) ? 1 : 2; }
                        catch { moveResult = 0; }
                    }));
                    message.Result = new IntPtr(1);
                    return;
                }
                if (message.Msg == TestMoveStatus)
                {
                    message.Result = new IntPtr(moveResult);
                    return;
                }
                if (message.Msg == TestOpenDialog)
                {
                    main.BeginInvoke(new MethodInvoker(delegate { SelectionList.Select(main, "Commands", new string[] { "Fixture" }, new string[] { "fixture" }); }));
                    message.Result = new IntPtr(1);
                    return;
                }
                if (message.Msg == TestState)
                {
                    FilePane pane = Field<FilePane[]>(main, "panes")[0];
                    bool preserved = pane.Selection.Length == 1 && AppClipboard.Count == 1 && pane.CurrentPath == originalPath
                        && Field<ToolStripStatusLabel>(main, "lastActionLabel").Text == "Last: Copied selection";
                    message.Result = preserved ? new IntPtr(1) : IntPtr.Zero;
                    return;
                }
                base.WndProc(ref message);
            }
            catch (Exception ex) { message.Result = new IntPtr(Marshal.GetHRForException(ex)); }
        }
    }

    private static void TestStatusAndEscape()
    {
        string source = Dir("status\\source");
        string destination = Dir("status\\destination");
        string other = Dir("status\\other");
        string partial = Dir("status\\partial");
        string fuzzy = Dir("status\\fuzzy");
        string alpha = Directory.CreateDirectory(Path.Combine(fuzzy, "10alpha")).FullName;
        Directory.CreateDirectory(Path.Combine(fuzzy, "10alpine"));
        foreach (string name in new string[] { "a.txt", "b.txt", "c.txt", "d.txt" }) Put(source, name, name);
        Put(other, "keep.txt", "keep");
        string[] paths = new string[12];
        for (int i = 0; i < paths.Length; i++) paths[i] = other;
        paths[0] = source;
        paths[1] = destination;
        paths[3] = partial;
        paths[4] = fuzzy;
        Workspace.Save(paths);
        AppClipboard.SetCopy(new string[0]);
        using (MainForm main = new MainForm())
        {
            main.Show();
            Application.DoEvents();
            FilePane[] panes = Field<FilePane[]>(main, "panes");
            FilePane pane = panes[0];
            ListBox list = Field<ListBox>(pane, "fileList");
            TextBox input = Field<TextBox>(pane, "pathInput");
            ToolStripStatusLabel state = Field<ToolStripStatusLabel>(main, "stateLabel");
            ToolStripStatusLabel selection = Field<ToolStripStatusLabel>(main, "selectionLabel");
            ToolStripStatusLabel last = Field<ToolStripStatusLabel>(main, "lastActionLabel");
            StatusStrip bar = Field<StatusStrip>(main, "statusBar");
            List<string> observed = new List<string>();
            state.TextChanged += delegate { observed.Add(state.Text); };
            pane.FocusList();
            Check(state.Text == "Ready" && selection.Text == "Selected: 0" && last.Text == "Last: -", "status starts Ready without inventing a completed action");
            Check(bar.Items.Count == 3 && bar.Dock == DockStyle.Bottom && Field<TableLayoutPanel>(main, "grid").Bottom <= bar.Top,
                "three status regions sit below all twelve panes");
            int barHeight = bar.Height;
            main.ClientSize = new Size(600, 400);
            main.SetCurrentState("Running: " + new string('x', 250));
            Check(bar.Height == barHeight && state.Bounds.Y == last.Bounds.Y && state.Bounds.Right <= selection.Bounds.Left
                && selection.Bounds.Right <= last.Bounds.Left && last.Bounds.Right <= bar.ClientSize.Width,
                "status stays one row without overlapping at narrow width");
            main.CompleteAction(null);
            main.ClientSize = new Size(1260, 760);

            NativeKey(list, Keys.Y);
            Check(state.Text == "Waiting: y", "first native y is visible as pending input");
            NativeKey(list, Keys.Escape);
            Check(state.Text == "Esc again: clear all", "single Esc cancels pending y and arms clear-all");
            NativeKey(list, Keys.J);
            Check(state.Text == "Ready" && !Field<bool>(main, "escapePending"), "non-Escape key cancels Escape sequence and still navigates");
            Key(pane, Keys.D);
            Check(state.Text == "Waiting: d", "first d is visible as pending input");
            Key(pane, Keys.D);
            Check(state.Text == "Ready" && selection.Text == "Move pending: 1" && last.Text == "Last: Move selection", "dd reports pending count and completed clipboard registration");
            Key(pane, Keys.Escape, Keys.Escape);
            Check(state.Text == "Ready" && selection.Text == "Selected: 0" && !AppClipboard.HasData, "Esc Esc discards Move pending without moving files");
            Check(last.Text == "Last: Move selection" && File.Exists(Path.Combine(source, "b.txt")), "clear-all preserves Last Action and existing files");

            Key(pane, Keys.V, Keys.J, Keys.Y, Keys.Escape);
            Check(Field<bool>(pane, "rangeSelecting") && pane.Selection.Length == 2 && state.Text == "Esc again: clear all",
                "first Esc cancels pending y without also cancelling the underlying range");
            Key(pane, Keys.Escape);
            Call(pane, "SetCaret", 1);

            Key(pane, Keys.Space, Keys.J, Keys.J, Keys.Space);
            Check(selection.Text == "Selected: 2", "Space selection count is read from actual marks");
            Key(pane, Keys.Control | Keys.C);
            Check(selection.Text == "Copy pending: 2" && last.Text == "Last: Copied selection", "Copy pending is visible with its actual count");
            Field<ListBox>(panes[2], "fileList").SetSelected(0, true);
            Key(pane, Keys.V, Keys.K);
            Check(state.Text == "Range select" && selection.Text == "Range: 3", "active range count takes priority over clipboard count");
            string savedWorkspace = File.ReadAllText(Workspace.GetWorkspacePath());
            string priorAction = last.Text;
            Key(pane, Keys.Escape);
            Check(pane.Selection.Length == 2 && AppClipboard.Count == 2 && Field<ListBox>(panes[2], "fileList").SelectedIndices.Count == 1,
                "first Esc restores only range base, keeping clipboard and other panes");
            Thread.Sleep(100);
            Key(pane, Keys.Escape);
            bool clear = true;
            for (int i = 0; i < panes.Length; i++)
            {
                clear &= panes[i].Selection.Length == 0 && !Field<bool>(panes[i], "rangeSelecting")
                    && Field<List<int>>(panes[i], "baseSelection").Count == 0 && Field<int>(panes[i], "rangeAnchor") == 0
                    && panes[i].CurrentPath == paths[i];
            }
            Check(clear && !AppClipboard.HasData && !Field<bool>(main, "escapePending") && state.Text == "Ready", "second Esc clears transient state in all twelve panes without a timer");
            Check(last.Text == priorAction && File.ReadAllText(Workspace.GetWorkspacePath()) == savedWorkspace,
                "Esc Esc preserves Last Action, current directories and persisted workspace");

            Key(pane, Keys.Space, Keys.E);
            Check(state.Text == "Path input" && selection.Text == "Selected: 1", "path focus is reflected without duplicating selection state");
            input.Text = "discard this input";
            NativeKey(input, Keys.Escape);
            Check(list.Focused && input.Text == source && state.Text == "Esc again: clear all" && pane.Selection.Length == 1,
                "first path Escape discards edit and carries Escape sequence into file list");
            NativeKey(list, Keys.Escape);
            Check(pane.Selection.Length == 0 && state.Text == "Ready" && last.Text == priorAction, "second Escape after path cancellation clears marks but preserves Last Action");
            Key(pane, Keys.Space, Keys.Escape);
            NativeKey(list, Keys.Tab);
            Check(!Field<bool>(main, "escapePending"), "Tab cancels Escape sequence even when KeyDown is bypassed");
            pane.FocusList();
            Key(pane, Keys.Escape);
            Check(pane.Selection.Length == 1, "Escape after Tab is a first Escape, not clear-all");
            Key(pane, Keys.Escape);

            list.SetSelected(0, true);
            list.SetSelected(1, true);
            Key(pane, Keys.Y, Keys.Y);
            panes[1].FocusList();
            Check(selection.Text == "Copy pending: 2", "clipboard status follows active pane changes");
            Key(panes[1], Keys.P);
            Check(observed.Contains("Copying...") && state.Text == "Ready" && last.Text == "Last: Copied 2 items",
                "copy reports actual completed items, not subsequent automatic refreshes");
            Check(File.Exists(Path.Combine(destination, "a.txt")) && File.Exists(Path.Combine(destination, "b.txt")), "copy status corresponds to completed filesystem work");
            priorAction = last.Text;
            Dialogs(delegate { Key(panes[1], Keys.P); });
            Check(last.Text == priorAction && state.Text == "Ready", "Skip All does not claim another successful copy");
            Dialogs(delegate { Key(panes[1], Keys.P); }, "Cancel");
            Check(last.Text == priorAction && state.Text == "Ready", "Cancel preserves Last Action and exits busy state");

            pane.FocusList();
            list.SetSelected(0, true);
            list.SetSelected(1, true);
            Key(pane, Keys.Y, Keys.Y);
            File.Delete(Path.Combine(source, "b.txt"));
            panes[3].FocusList();
            Dialogs(delegate { Key(panes[3], Keys.P); });
            Check(dialogs == 1 && state.Text == "Error: Copy failed" && last.Text == "Last: Copied 1 items",
                "partial failure retains Error priority and counts only fully completed targets");
            Check(!state.Text.Contains(root), "status error is short; full details remain in the error dialog");
            Key(panes[3], Keys.Escape, Keys.Escape);
            Check(state.Text == "Ready" && File.Exists(Path.Combine(partial, "a.txt")) && last.Text == "Last: Copied 1 items",
                "Esc Esc clears error and pending work without undoing the completed copy");

            pane.FocusList();
            Call(pane, "SetCaret", 1);
            Key(pane, Keys.D, Keys.D);
            panes[1].FocusList();
            Key(panes[1], Keys.P);
            Check(observed.Contains("Moving...") && last.Text == "Last: Moved 1 items" && selection.Text == "Selected: 0", "successful move reports completion and clears Move pending");
            Call(panes[1], "SetCaret", 2);
            priorAction = last.Text;
            Dialogs(delegate { Key(panes[1], Keys.Shift | Keys.Delete); }, answer: 2);
            Check(last.Text == priorAction && state.Text == "Ready", "cancelled permanent deletion never becomes Last: Deleted");
            Dialogs(delegate { Key(panes[1], Keys.Shift | Keys.Delete); });
            Check(observed.Contains("Deleting...") && last.Text == "Last: Deleted 1 items" && !File.Exists(Path.Combine(destination, "c.txt")),
                "deletion reports a completed target only after confirmation and success");
            AppClipboard.SetCopy(new string[] { Path.Combine(source, "missing.txt") });
            priorAction = last.Text;
            Dialogs(delegate { Key(panes[1], Keys.P); });
            Check(state.Text == "Error: Copy failed" && last.Text == priorAction, "fully failed operation does not change Last Action");
            Key(panes[1], Keys.Escape, Keys.Escape);
            Key(panes[1], Keys.F5);
            Check(last.Text == "Last: Refreshed", "explicit F5 records the completed refresh");

            FilePane jumpPane = panes[4];
            jumpPane.FocusList();
            Key(jumpPane, Keys.E);
            TextBox jumpInput = Field<TextBox>(jumpPane, "pathInput");
            jumpInput.Text = "10a";
            Dialogs(delegate { NativeKey(jumpInput, Keys.Enter); }, choose: delegate(SelectionList choice)
            {
                Check(state.Text == "Select directory" && selection.Text == "2 candidates" && last.Text == "Last: Refreshed",
                    "directory picker shows its own live candidate count without replacing Last Action");
                NativeKey(Field<ListBox>(choice, "list"), Keys.Escape);
            });
            Check(observed.Contains("Fuzzy search: 10a") && state.Text == "Esc again: clear all" && jumpPane.CurrentPath == fuzzy,
                "native picker Escape preserves path and carries sequence back to file list");
            NativeKey(Field<ListBox>(jumpPane, "fileList"), Keys.Escape);
            Check(state.Text == "Ready" && last.Text == "Last: Refreshed", "second Escape after picker cancellation clears all without changing Last Action");
            Key(jumpPane, Keys.E);
            jumpInput.Text = "10alpha";
            NativeKey(jumpInput, Keys.Enter);
            Check(observed.Contains("Fuzzy search: 10alpha") && last.Text == "Last: Jumped to 10alpha" && jumpPane.CurrentPath == alpha,
                "Fuzzy Jump reports token while searching and only the leaf name after success");
            priorAction = last.Text;
            Key(jumpPane, Keys.E);
            jumpInput.Text = "does-not-exist";
            Dialogs(delegate { NativeKey(jumpInput, Keys.Enter); });
            Check(state.Text == "Error: Move failed" && last.Text == priorAction && jumpPane.CurrentPath == alpha,
                "failed Fuzzy Jump preserves Last Action and current directory");
            Key(jumpPane, Keys.Escape, Keys.Escape);

            string commands = Workspace.GetCommandsDirectory();
            string command = Put(commands, "Status command.cmd", "@echo off\r\nexit /b 0\r\n");
            int commandCount = CommandRunner.ListCommandFiles(main).Length;
            Dialogs(delegate { Key(jumpPane, Keys.C); }, choose: delegate(SelectionList choice)
            {
                Check(state.Text == "Select command" && selection.Text == commandCount + " commands", "command picker shows current state and live command count");
                NativeKey(Field<ListBox>(choice, "list"), Keys.Escape);
            });
            Check(state.Text == "Esc again: clear all" && last.Text == priorAction, "command picker cancellation does not invent a completed command");
            NativeKey(Field<ListBox>(jumpPane, "fileList"), Keys.Escape);
            Check(state.Text == "Ready", "Esc sequence continues across command picker boundary");
            Dialogs(delegate { Key(jumpPane, Keys.C); }, choose: delegate(SelectionList choice)
            {
                int index = Array.IndexOf(Field<string[]>(choice, "values"), command);
                Field<ListBox>(choice, "list").SelectedIndex = index;
                NativeKey(Field<ListBox>(choice, "list"), Keys.Enter);
            });
            Check(observed.Contains("Running: Status command") && last.Text == "Last: Status command" && state.Text == "Ready",
                "external command responsibility ends at successful process launch");
            Key(jumpPane, Keys.Escape, Keys.Escape);
            Check(last.Text == "Last: Status command", "clear-all never undoes or forgets a launched command");
            Dialogs(delegate { Call(jumpPane, "RunCommand", Path.Combine(commands, "absent.cmd")); });
            Check(dialogs == 1 && state.Text == "Error: Run command failed" && last.Text == "Last: Status command",
                "failed command launch preserves Last Action and displays Error");
            main.Close();
        }
    }

    private static void TestOpenKeys()
    {
        string directory = Dir("open-keys");
        string child = Directory.CreateDirectory(Path.Combine(directory, "child")).FullName;
        string marker = Path.Combine(directory, "executed.txt");
        Put(directory, "run.cmd", "@echo off\r\n> \"%~dp0executed.txt\" echo executed\r\n");
        using (Form form = new Form())
        using (FilePane pane = new FilePane())
        {
            form.Controls.Add(pane);
            pane.SetPath(directory);
            form.Show();
            pane.FocusList();
            ListBox list = Field<ListBox>(pane, "fileList");
            NativeKey(list, Keys.L);
            Check(pane.CurrentPath == child, "l still enters a directory");
            Key(pane, Keys.H);
            NativeKey(list, Keys.Enter);
            Check(pane.CurrentPath == child, "Enter still enters a directory");
            Key(pane, Keys.H);
            Call(pane, "SetCaret", 1);
            NativeKey(list, Keys.L);
            Thread.Sleep(300);
            Check(pane.CurrentPath == directory && !File.Exists(marker), "l on a file does not execute or navigate");
            NativeKey(list, Keys.Enter);
            Stopwatch wait = Stopwatch.StartNew();
            while (!File.Exists(marker) && wait.ElapsedMilliseconds < 5000) { Application.DoEvents(); Thread.Sleep(20); }
            Check(File.Exists(marker), "Enter executes a file through its Windows association");
            File.Delete(marker);
            Rectangle item = list.GetItemRectangle(1);
            Call(pane, "FileList_DoubleClick", list, new MouseEventArgs(MouseButtons.Left, 2, item.X + 2, item.Y + 2, 0));
            wait.Restart();
            while (!File.Exists(marker) && wait.ElapsedMilliseconds < 5000) { Application.DoEvents(); Thread.Sleep(20); }
            Check(File.Exists(marker), "mouse double-click execution remains available");
            form.Close();
        }
        string destination = null;
        Dialogs(delegate { FuzzyJump.TryJump(null, root, "open-key", out destination); },
            choose: delegate(SelectionList choice) { NativeKey(Field<ListBox>(choice, "list"), Keys.L); });
        Check(destination == directory, "Fuzzy directory navigation remains available");
        using (SelectionList choice = new SelectionList("Select directory", new string[] { "child" }, new string[] { child }, true))
        {
            choice.Show();
            NativeKey(Field<ListBox>(choice, "list"), Keys.L);
            Check(choice.Result == child, "l accepts directory candidates but not commands");
        }
    }
}
