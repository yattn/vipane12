using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class FilePane : UserControl
{
    private enum PendingKey { None, Y, D }

    private TextBox pathInput;
    private ListBox fileList;
    private ContextMenuStrip contextMenu;
    private ToolStripMenuItem commandsMenu;
    private string currentPath;
    private List<string> itemPaths = new List<string>();
    private PendingKey pendingKey = PendingKey.None;
    private bool rangeSelecting = false;
    private int rangeAnchor = 0;
    private List<int> baseSelection = new List<int>();

    public event EventHandler PaneActivated;
    public event Action<string[]> FilesChanged;
    public event Action<int, int> MoveRequested;
    public event EventHandler StatusChanged;

    public string StatusState
    {
        get
        {
            if (pathInput.Focused) return "Path input";
            if (pendingKey == PendingKey.Y) return "Waiting: y";
            if (pendingKey == PendingKey.D) return "Waiting: d";
            return rangeSelecting ? "Range select" : "Ready";
        }
    }

    public string SelectionStatus
    {
        get
        {
            if (rangeSelecting) return "Range: " + fileList.SelectedIndices.Count;
            if (AppClipboard.HasData) return (AppClipboard.IsMove ? "Move pending: " : "Copy pending: ") + AppClipboard.Count;
            return "Selected: " + fileList.SelectedIndices.Count;
        }
    }

    public string CurrentPath
    {
        get { return currentPath; }
    }

    public string[] Selection
    {
        get
        {
            List<string> result = new List<string>();
            foreach (int index in fileList.SelectedIndices)
            {
                if (index >= 0 && index < itemPaths.Count)
                {
                    result.Add(itemPaths[index]);
                }
            }
            return result.ToArray();
        }
    }

    public string CursorPath
    {
        get
        {
            int c = CursorIndex();
            if (c >= 0 && c < itemPaths.Count)
            {
                return itemPaths[c];
            }
            return null;
        }
    }

    private const int LB_GETCARETINDEX = 0x019F;
    private const int LB_SETCARETINDEX = 0x019E;
    private const int WM_UPDATEUISTATE = 0x0128;

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private int CursorIndex()
    {
        if (fileList.Items.Count == 0)
        {
            return -1;
        }
        return (int)SendMessage(fileList.Handle, LB_GETCARETINDEX, IntPtr.Zero, IntPtr.Zero);
    }

    private void SetCaret(int index)
    {
        if (index < 0 || index >= fileList.Items.Count)
        {
            return;
        }
        SendMessage(fileList.Handle, LB_SETCARETINDEX, (IntPtr)index, IntPtr.Zero);
    }

    public FilePane()
    {
        this.Font = new Font("Meiryo UI", this.Font.Size, this.Font.Style);
        BuildUi();
    }

    private void BuildUi()
    {
        this.BorderStyle = BorderStyle.FixedSingle;

        pathInput = new TextBox();
        pathInput.Dock = DockStyle.Top;
        pathInput.KeyDown += new KeyEventHandler(PathInput_KeyDown);
        pathInput.Leave += new EventHandler(PathInput_Leave);
        pathInput.Enter += new EventHandler(Child_Enter);

        fileList = new ListBox();
        fileList.Dock = DockStyle.Fill;
        fileList.IntegralHeight = false;
        fileList.HorizontalScrollbar = true;
        fileList.SelectionMode = SelectionMode.MultiExtended;
        fileList.ImeMode = ImeMode.Disable;
        fileList.KeyDown += new KeyEventHandler(FileList_KeyDown);
        fileList.KeyPress += new KeyPressEventHandler(FileList_KeyPress);
        fileList.MouseDoubleClick += new MouseEventHandler(FileList_DoubleClick);
        fileList.MouseDown += new MouseEventHandler(FileList_MouseDown);
        fileList.Enter += new EventHandler(Child_Enter);
        fileList.Leave += delegate { pendingKey = PendingKey.None; EndRange(); };
        fileList.SelectedIndexChanged += delegate { OnStatusChanged(); };

        contextMenu = new ContextMenuStrip();
        ToolStripMenuItem openItem = new ToolStripMenuItem("Open");
        openItem.Click += new EventHandler(delegate(object s, EventArgs e) { OpenSelected(); });
        ToolStripMenuItem copyItem = new ToolStripMenuItem("Copy");
        copyItem.Click += new EventHandler(delegate(object s, EventArgs e) { CopySelection(); });
        ToolStripMenuItem cutItem = new ToolStripMenuItem("Cut");
        cutItem.Click += new EventHandler(delegate(object s, EventArgs e) { CutSelection(); });
        ToolStripMenuItem pasteItem = new ToolStripMenuItem("Paste");
        pasteItem.Click += new EventHandler(delegate(object s, EventArgs e) { Paste(); });
        ToolStripMenuItem deleteItem = new ToolStripMenuItem("Delete");
        deleteItem.Click += new EventHandler(delegate(object s, EventArgs e) { RecycleSelection(); });
        commandsMenu = new ToolStripMenuItem("Commands");
        contextMenu.Items.Add(openItem);
        contextMenu.Items.Add(copyItem);
        contextMenu.Items.Add(cutItem);
        contextMenu.Items.Add(pasteItem);
        contextMenu.Items.Add(deleteItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(commandsMenu);
        contextMenu.Opening += new System.ComponentModel.CancelEventHandler(ContextMenu_Opening);
        fileList.ContextMenuStrip = contextMenu;

        this.Controls.Add(fileList);
        this.Controls.Add(pathInput);
    }

    public bool SetPath(string path, bool recordAction = true)
    {
        if (recordAction) BeginAction(null);
        bool loaded = LoadPath(path, false);
        if (recordAction) FinishAction(loaded ? "Jumped to " + new DirectoryInfo(currentPath).Name : null);
        return loaded;
    }

    public void RefreshPane(bool recordAction = true)
    {
        if (recordAction) BeginAction(null);
        bool loaded = currentPath != null && LoadPath(currentPath, true);
        if (recordAction) FinishAction(loaded ? "Refreshed" : null);
    }

    private bool LoadPath(string path, bool refresh)
    {
        int caret = refresh ? CursorIndex() : 0;
        try
        {
            string target = NormalizePath(path);
            string[] dirs = Directory.GetDirectories(target);
            string[] files = Directory.GetFiles(target);
            Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            // Commit navigation only after both enumerations succeed.
            fileList.BeginUpdate();
            try
            {
                fileList.Items.Clear();
                itemPaths.Clear();
                foreach (string dir in dirs)
                {
                    fileList.Items.Add(Path.GetFileName(dir) + "\\");
                    itemPaths.Add(dir);
                }
                foreach (string file in files)
                {
                    fileList.Items.Add(Path.GetFileName(file));
                    itemPaths.Add(file);
                }
            }
            finally { fileList.EndUpdate(); }
            currentPath = target;
            pathInput.Text = currentPath;
            pendingKey = PendingKey.None;
            EndRange();
            if (fileList.Items.Count > 0)
            {
                caret = Math.Max(0, Math.Min(caret, fileList.Items.Count - 1));
                SetCaret(caret);
                EnsureVisible(caret);
            }
            OnStatusChanged();
            return true;
        }
        catch (Exception ex)
        {
            if (refresh)
            {
                fileList.Items.Clear();
                itemPaths.Clear();
                EndRange();
            }
            pathInput.Text = currentPath ?? "";
            FileOperations.ShowError(this, refresh ? "Refresh" : "Move", path, ex.Message);
            return false;
        }
    }

    public void FocusPathInput()
    {
        pathInput.Focus();
        pathInput.SelectAll();
    }

    public void FocusList()
    {
        fileList.Focus();
    }

    public void SetActive(bool active)
    {
        pathInput.BackColor = active ? Color.LightYellow : SystemColors.Window;
    }

    private void MoveSelection(int delta)
    {
        if (fileList.Items.Count == 0)
        {
            return;
        }
        int c = CursorIndex() + delta;
        if (c < 0)
        {
            c = 0;
        }
        if (c >= fileList.Items.Count)
        {
            c = fileList.Items.Count - 1;
        }
        SetCaret(c);
        EnsureVisible(c);
        if (rangeSelecting)
        {
            ApplyRange();
        }
    }

    private void ToggleCurrent()
    {
        int c = CursorIndex();
        if (c < 0)
        {
            return;
        }
        EndRange();
        fileList.SetSelected(c, !fileList.GetSelected(c));
        SetCaret(c);
    }

    private void ToggleRange()
    {
        if (fileList.Items.Count == 0)
        {
            return;
        }
        if (!rangeSelecting)
        {
            baseSelection.Clear();
            foreach (int i in fileList.SelectedIndices)
            {
                baseSelection.Add(i);
            }
            rangeAnchor = CursorIndex();
            if (rangeAnchor < 0)
            {
                rangeAnchor = 0;
            }
            rangeSelecting = true;
            ApplyRange();
        }
        else
        {
            EndRange();
        }
    }

    private void CancelRange()
    {
        if (!rangeSelecting)
        {
            return;
        }
        rangeSelecting = false;
        int c = CursorIndex();
        RestoreBase();
        SetCaret(c);
        baseSelection.Clear();
        OnStatusChanged();
    }

    private void EndRange()
    {
        rangeSelecting = false;
        baseSelection.Clear();
        OnStatusChanged();
    }

    private void ApplyRange()
    {
        int c = CursorIndex();
        if (c < 0)
        {
            return;
        }
        RestoreBase();
        int lo = Math.Min(rangeAnchor, c);
        int hi = Math.Max(rangeAnchor, c);
        if (lo < 0)
        {
            lo = 0;
        }
        if (hi >= fileList.Items.Count)
        {
            hi = fileList.Items.Count - 1;
        }
        for (int i = lo; i <= hi; i++)
        {
            fileList.SetSelected(i, true);
        }
        SetCaret(c);
    }

    private void RestoreBase()
    {
        fileList.ClearSelected();
        for (int i = 0; i < baseSelection.Count; i++)
        {
            int b = baseSelection[i];
            if (b >= 0 && b < fileList.Items.Count)
            {
                fileList.SetSelected(b, true);
            }
        }
    }

    private void EnsureVisible(int index)
    {
        int visible = Math.Max(1, fileList.ClientSize.Height / Math.Max(1, fileList.ItemHeight));
        if (index < fileList.TopIndex)
        {
            fileList.TopIndex = index;
        }
        else if (index >= fileList.TopIndex + visible)
        {
            fileList.TopIndex = index - visible + 1;
        }
    }

    private void GoParent()
    {
        if (currentPath == null)
        {
            return;
        }
        DirectoryInfo parent;
        try
        {
            parent = Directory.GetParent(currentPath);
        }
        catch (Exception ex)
        {
            FileOperations.ShowError(this, "Move", currentPath, ex.Message);
            return;
        }
        if (parent == null)
        {
            return;
        }
        string oldPath = currentPath;
        if (!SetPath(parent.FullName)) return;
        for (int i = 0; i < itemPaths.Count; i++)
        {
            if (string.Equals(itemPaths[i].TrimEnd(Path.DirectorySeparatorChar), oldPath.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            {
                SetCaret(i);
                EnsureVisible(i);
                break;
            }
        }
    }

    private void OpenSelected(bool directoriesOnly = false)
    {
        string p = CursorPath;
        if (p == null)
        {
            return;
        }
        BeginAction(null);
        if (Directory.Exists(p))
        {
            SetPath(p);
        }
        else if (File.Exists(p))
        {
            if (!directoriesOnly) FinishAction(FileOperations.Open(p, this) ? "Opened " + Path.GetFileName(p) : null);
        }
        else
        {
            FileOperations.ShowError(this, "Open", p, "File not found");
            FinishAction(null);
        }
    }

    private void CopySelection()
    {
        BeginAction(null);
        EndRange();
        AppClipboard.SetCopy(OperationPaths());
        FinishAction(AppClipboard.HasData ? "Copied selection" : null);
    }

    private void CutSelection()
    {
        BeginAction(null);
        EndRange();
        AppClipboard.SetMove(OperationPaths());
        FinishAction(AppClipboard.HasData ? "Move selection" : null);
    }

    private string[] OperationPaths()
    {
        string[] selected = Selection;
        if (selected.Length != 0) return selected;
        string cursor = CursorPath;
        return cursor == null ? new string[0] : new string[] { cursor };
    }

    private void Paste()
    {
        EndRange();
        if (!AppClipboard.HasData)
        {
            return;
        }
        string[] sources = AppClipboard.GetPaths();
        bool move = AppClipboard.IsMove;
        int completed = 0;
        BeginAction(move ? "Moving..." : "Copying...");
        try
        {
            if (move)
            {
                string[] remaining = FileOperations.MovePaths(sources, currentPath, this);
                AppClipboard.SetMove(remaining);
                completed = sources.Length - remaining.Length;
            }
            else
            {
                completed = FileOperations.CopyPaths(sources, currentPath, this);
            }
            List<string> affected = new List<string>(sources);
            affected.Add(currentPath);
            OnFilesChanged(affected.ToArray());
        }
        finally { FinishAction(completed > 0 ? (move ? "Moved " : "Copied ") + completed + " items" : null); }
    }

    private void RecycleSelection()
    {
        EndRange();
        string[] sel = OperationPaths();
        if (sel.Length == 0)
        {
            return;
        }
        int completed = 0;
        BeginAction("Deleting...");
        try
        {
            completed = FileOperations.DeleteToRecycle(sel, this);
            OnFilesChanged(sel);
        }
        finally { FinishAction(completed > 0 ? "Deleted " + completed + " items" : null); }
    }

    private void RecycleSelectionWithConfirm()
    {
        BeginAction(null);
        EndRange();
        string[] sel = OperationPaths();
        if (sel.Length == 0)
        {
            return;
        }
        string text = sel.Length == 1
            ? "Move 1 selected item to Recycle Bin?\n" + sel[0]
            : "Move " + sel.Length + " selected items to Recycle Bin?\n" + sel[0] + "\n...";
        DialogResult r = MessageBox.Show(this, text, "vipane12 - Recycle",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
        if (r != DialogResult.Yes)
        {
            return;
        }
        RecycleSelection();
    }

    private void PermanentDeleteSelection()
    {
        EndRange();
        string[] sel = OperationPaths();
        if (sel.Length == 0)
        {
            return;
        }
        int completed = 0;
        BeginAction("Deleting...");
        try
        {
            completed = FileOperations.DeletePermanent(sel, this);
            OnFilesChanged(sel);
        }
        finally { FinishAction(completed > 0 ? "Deleted " + completed + " items" : null); }
    }

    private static string NormalizePath(string path)
    {
        string full = Path.GetFullPath(path);
        string root = Path.GetPathRoot(full);
        if (full.Length > root.Length)
        {
            full = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        return full;
    }

    private void OnPaneActivated()
    {
        if (PaneActivated != null)
        {
            PaneActivated(this, EventArgs.Empty);
        }
        OnStatusChanged();
    }

    private void OnStatusChanged()
    {
        if (StatusChanged != null) StatusChanged(this, EventArgs.Empty);
    }

    private void BeginAction(string state)
    {
        MainForm main = FindForm() as MainForm;
        if (main != null) main.SetCurrentState(state);
    }

    private void FinishAction(string action)
    {
        MainForm main = FindForm() as MainForm;
        if (main != null) main.CompleteAction(action);
        OnStatusChanged();
    }

    public void ClearTransientState()
    {
        int caret = CursorIndex();
        pendingKey = PendingKey.None;
        EndRange();
        rangeAnchor = 0;
        fileList.ClearSelected();
        SetCaret(caret);
        pathInput.Text = currentPath ?? "";
        contextMenu.Close();
        OnStatusChanged();
    }

    private bool HandleGlobalKey(KeyEventArgs e)
    {
        MainForm main = FindForm() as MainForm;
        if (main == null || !main.HandleKey(e.KeyData)) return false;
        e.Handled = true;
        e.SuppressKeyPress = true;
        return true;
    }

    private void OnFilesChanged(string[] paths)
    {
        if (FilesChanged != null)
        {
            FilesChanged(paths);
        }
    }

    private void OnMoveRequested(int dx, int dy)
    {
        if (MoveRequested != null)
        {
            MoveRequested(dx, dy);
        }
    }

    private void Child_Enter(object sender, EventArgs e)
    {
        OnPaneActivated();
    }

    private void FileList_DoubleClick(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && fileList.IndexFromPoint(e.Location) >= 0) OpenSelected();
    }

    private void FileList_KeyPress(object sender, KeyPressEventArgs e)
    {
        e.Handled = true;
    }

    private void FileList_MouseDown(object sender, MouseEventArgs e)
    {
        pendingKey = PendingKey.None;
        EndRange();
        fileList.Focus();
        if (e.Button == MouseButtons.Right)
        {
            int index = fileList.IndexFromPoint(e.Location);
            if (index >= 0 && !fileList.GetSelected(index))
            {
                // SelectedIndex setter adds instead of replacing here, so clear first.
                fileList.ClearSelected();
                fileList.SetSelected(index, true);
            }
            SetCaret(index);
        }
    }

    private void ContextMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
    {
        pendingKey = PendingKey.None;
        while (commandsMenu.DropDownItems.Count > 0) commandsMenu.DropDownItems[0].Dispose();
        string[] cmds = CommandRunner.ListCommandFiles(this);
        if (cmds.Length == 0)
        {
            ToolStripMenuItem none = new ToolStripMenuItem("(No commands)");
            none.Enabled = false;
            commandsMenu.DropDownItems.Add(none);
            return;
        }
        for (int i = 0; i < cmds.Length; i++)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(CommandRunner.GetCommandName(cmds[i]).Replace("&", "&&"));
            item.Tag = cmds[i];
            item.Click += new EventHandler(CommandItem_Click);
            commandsMenu.DropDownItems.Add(item);
        }
    }

    private void CommandItem_Click(object sender, EventArgs e)
    {
        ToolStripMenuItem item = sender as ToolStripMenuItem;
        if (item == null || item.Tag == null)
        {
            return;
        }
        RunCommand((string)item.Tag);
    }

    private void RunCommand(string path)
    {
        string name = CommandRunner.GetCommandName(path);
        BeginAction("Running: " + name);
        string[] paths = OperationPaths();
        string sel = paths.Length == 0 ? "" : paths[0];
        FinishAction(CommandRunner.Run(path, sel, currentPath, this) ? name : null);
    }

    private void ShowCommands()
    {
        BeginAction(null);
        string[] files = CommandRunner.ListCommandFiles(this);
        if (files.Length == 0)
        {
            MessageBox.Show(this, "No commands", "vipane12",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        string[] names = new string[files.Length];
        for (int i = 0; i < files.Length; i++)
        {
            names[i] = CommandRunner.GetCommandName(files[i]);
        }
        string chosen = SelectionList.Select(this, "Commands", names, files);
        if (chosen != null)
        {
            RunCommand(chosen);
        }
        else FinishAction(null);
    }

    private void PathInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (HandleGlobalKey(e)) return;
        if (e.KeyCode == Keys.Enter)
        {
            EnterPath(pathInput.Text);
            FocusList();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyData == Keys.Escape)
        {
            pathInput.Text = currentPath != null ? currentPath : "";
            FocusList();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        OnStatusChanged();
    }

    private void EnterPath(string input)
    {
        BeginAction(null);
        try
        {
            if (string.IsNullOrWhiteSpace(input)) throw new ArgumentException("Enter a path or a Fuzzy Jump pattern.");
            if (Path.IsPathRooted(input) || input.IndexOf('\\') >= 0 || input == "." || input == "..")
            {
                SetPath(Path.IsPathRooted(input) ? input : Path.Combine(currentPath, input));
                return;
            }
            string dest;
            if (FuzzyJump.TryJump(this, currentPath, input, out dest,
                delegate(string token) { BeginAction("Fuzzy search: " + token); })) SetPath(dest);
            else pathInput.Text = currentPath ?? "";
        }
        catch (Exception ex)
        {
            pathInput.Text = currentPath ?? "";
            FileOperations.ShowError(this, "Move", input, ex.Message);
        }
        finally { FinishAction(null); }
    }

    private void PathInput_Leave(object sender, EventArgs e)
    {
        string want = currentPath != null ? currentPath : "";
        if (pathInput.Text != want)
        {
            pathInput.Text = want;
        }
    }

    private void FileList_KeyDown(object sender, KeyEventArgs e)
    {
        if (HandleGlobalKey(e)) return;
        // UIS_CLEAR | (UISF_HIDEFOCUS << 16): keyboard motion must remain visible after a mouse click.
        SendMessage(fileList.Handle, WM_UPDATEUISTATE, (IntPtr)0x10002, IntPtr.Zero);
        PendingKey previous = pendingKey;
        pendingKey = PendingKey.None;
        bool handled = true;
        switch (e.KeyData)
        {
            case Keys.Control | Keys.H: OnMoveRequested(-1, 0); break;
            case Keys.Control | Keys.J: OnMoveRequested(0, 1); break;
            case Keys.Control | Keys.K: OnMoveRequested(0, -1); break;
            case Keys.Control | Keys.L: OnMoveRequested(1, 0); break;
            case Keys.Control | Keys.C: CopySelection(); break;
            case Keys.Control | Keys.X: CutSelection(); break;
            case Keys.Control | Keys.V: Paste(); break;
            case Keys.F5: RefreshPane(); break;
            case Keys.Shift | Keys.Delete: PermanentDeleteSelection(); break;
            case Keys.Delete: RecycleSelection(); break;
            case Keys.Back:
            case Keys.H: GoParent(); break;
            case Keys.Enter: OpenSelected(); break;
            case Keys.L: OpenSelected(true); break;
            case Keys.Y:
                if (previous == PendingKey.Y) CopySelection();
                else pendingKey = PendingKey.Y;
                break;
            case Keys.D:
                if (previous == PendingKey.D) CutSelection();
                else pendingKey = PendingKey.D;
                break;
            case Keys.J: MoveSelection(1); break;
            case Keys.K: MoveSelection(-1); break;
            case Keys.E: FocusPathInput(); break;
            case Keys.P: Paste(); break;
            case Keys.X: RecycleSelectionWithConfirm(); break;
            case Keys.Space: ToggleCurrent(); break;
            case Keys.V: ToggleRange(); break;
            case Keys.C: ShowCommands(); break;
            case Keys.Escape:
                if (previous == PendingKey.None) CancelRange();
                break;
            case Keys.Up:
            case Keys.Down:
                if (rangeSelecting) MoveSelection(e.KeyCode == Keys.Down ? 1 : -1);
                else handled = false;
                break;
            default: handled = false; break;
        }
        if (handled)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down || e.KeyCode == Keys.Home
            || e.KeyCode == Keys.End || e.KeyCode == Keys.PageUp || e.KeyCode == Keys.PageDown)
        {
            EndRange();
        }
        OnStatusChanged();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && contextMenu != null) contextMenu.Dispose();
        base.Dispose(disposing);
    }
}
