using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class MainForm : Form
{
    private FilePane[] panes = new FilePane[12];
    private FilePane activePane;
    private TableLayoutPanel grid;
    private StatusStrip statusBar;
    private ToolStripStatusLabel stateLabel;
    private ToolStripStatusLabel selectionLabel;
    private ToolStripStatusLabel lastActionLabel;
    private string currentState;
    private string lastAction;
    private bool escapePending;
    private SelectionList selectionList;
    private bool activationPending;
    private bool activationResultReady;
    private int activationResult;
    private Guid activationResultDesktop;

    public MainForm()
    {
        this.Text = "ViPane12";
        this.Font = new Font("Meiryo UI", this.Font.Size, this.Font.Style);
        this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Width = 1280;
        this.Height = 800;

        grid = new TableLayoutPanel();
        grid.Dock = DockStyle.Fill;
        grid.ColumnCount = 4;
        grid.RowCount = 3;
        for (int c = 0; c < 4; c++)
        {
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        }
        for (int r = 0; r < 3; r++)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33333F));
        }
        this.Controls.Add(grid);

        statusBar = new StatusStrip();
        statusBar.SizingGrip = false;
        stateLabel = new ToolStripStatusLabel();
        selectionLabel = new ToolStripStatusLabel();
        lastActionLabel = new ToolStripStatusLabel();
        foreach (ToolStripStatusLabel label in new ToolStripStatusLabel[] { stateLabel, selectionLabel, lastActionLabel })
        {
            label.Spring = true;
            label.AutoSize = false;
            label.TextAlign = ContentAlignment.MiddleLeft;
            statusBar.Items.Add(label);
        }
        stateLabel.BorderSides = ToolStripStatusLabelBorderSides.Right;
        selectionLabel.BorderSides = ToolStripStatusLabelBorderSides.Right;
        this.Controls.Add(statusBar);
        UpdateStatus();

        string[] paths = Workspace.Load(this);
        for (int i = 0; i < 12; i++)
        {
            FilePane p = new FilePane();
            p.Dock = DockStyle.Fill;
            p.Margin = new Padding(2);
            p.PaneActivated += new EventHandler(Pane_PaneActivated);
            p.MoveRequested += new Action<int, int>(Pane_MoveRequested);
            p.FilesChanged += Pane_FilesChanged;
            p.StatusChanged += delegate(object sender, EventArgs e) { if (sender == activePane) UpdateStatus(); };
            panes[i] = p;
            grid.Controls.Add(p, i % 4, i / 4);
        }
        for (int i = 0; i < 12; i++)
        {
            if (!panes[i].SetPath(paths[i], false) && paths[i] != Workspace.GetDefaultDirectory())
                panes[i].SetPath(Workspace.GetDefaultDirectory(), false);
        }
        SetActivePane(panes[0]);

        this.Shown += new EventHandler(MainForm_Shown);
        this.FormClosing += new FormClosingEventHandler(MainForm_FormClosing);
    }

    private void Pane_PaneActivated(object sender, EventArgs e)
    {
        SetActivePane((FilePane)sender);
    }

    private void Pane_MoveRequested(int dx, int dy)
    {
        MoveActivePane(dx, dy);
    }

    private void Pane_FilesChanged(string[] paths)
    {
        foreach (FilePane pane in panes)
        {
            if (pane.CurrentPath == null) continue;
            foreach (string path in paths)
            {
                if (path == null) continue;
                if (string.Equals(pane.CurrentPath, Path.GetDirectoryName(path), StringComparison.OrdinalIgnoreCase)
                    || string.Equals(pane.CurrentPath, path, StringComparison.OrdinalIgnoreCase)
                    || pane.CurrentPath.StartsWith(path.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
                {
                    pane.RefreshPane(false);
                    break;
                }
            }
        }
    }

    private void MainForm_Shown(object sender, EventArgs e)
    {
        if (activePane != null)
        {
            activePane.FocusList();
        }
    }

    private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
    {
        string[] paths = new string[12];
        for (int i = 0; i < 12; i++)
        {
            paths[i] = panes[i].CurrentPath;
        }
        Workspace.Save(paths, this);
    }

    private void SetActivePane(FilePane pane)
    {
        activePane = pane;
        for (int i = 0; i < panes.Length; i++)
        {
            if (panes[i] != null) panes[i].SetActive(panes[i] == pane);
        }
        UpdateStatus();
    }

    private void MoveActivePane(int dx, int dy)
    {
        int index = -1;
        for (int i = 0; i < panes.Length; i++)
        {
            if (panes[i] == activePane)
            {
                index = i;
                break;
            }
        }
        if (index < 0)
        {
            return;
        }
        int col = index % 4 + dx;
        int row = index / 4 + dy;
        if (col < 0 || col > 3 || row < 0 || row > 2)
        {
            return;
        }
        FilePane next = panes[row * 4 + col];
        SetActivePane(next);
        next.FocusList();
    }

    internal void SetCurrentState(string state)
    {
        currentState = state;
        UpdateStatus();
        // Paint before synchronous work, without pumping input or allowing reentrant operations.
        statusBar.Refresh();
    }

    internal void CompleteAction(string action)
    {
        if (action != null) lastAction = action;
        if (currentState == null || !currentState.StartsWith("Error:", StringComparison.Ordinal)) currentState = null;
        UpdateStatus();
    }

    internal void SetSelectionList(SelectionList list)
    {
        selectionList = list;
        UpdateStatus();
        statusBar.Refresh();
    }

    private void UpdateStatus()
    {
        string state = currentState;
        if (state == null || !state.StartsWith("Error:", StringComparison.Ordinal))
        {
            if (selectionList != null) state = selectionList.Text == "Commands" ? "Select command" : "Select directory";
            else if (state == null) state = escapePending ? "Esc again: clear all" : activePane == null ? "Ready" : activePane.StatusState;
        }
        stateLabel.Text = state.Replace('\r', ' ').Replace('\n', ' ');
        selectionLabel.Text = selectionList != null
            ? selectionList.ItemCount + (selectionList.Text == "Commands" ? " commands" : " candidates")
            : activePane == null ? "Selected: 0" : activePane.SelectionStatus;
        lastActionLabel.Text = "Last: " + (lastAction ?? "-");
    }

    internal bool HandleKey(Keys key)
    {
        if (key != Keys.Escape)
        {
            escapePending = false;
            if (currentState != null && currentState.StartsWith("Error:", StringComparison.Ordinal)) currentState = null;
            UpdateStatus();
            return false;
        }
        if (escapePending)
        {
            ClearTransientState();
            return true;
        }
        escapePending = true;
        if (currentState != null && currentState.StartsWith("Error:", StringComparison.Ordinal)) currentState = null;
        UpdateStatus();
        return false;
    }

    private void ClearTransientState()
    {
        escapePending = false;
        currentState = null;
        foreach (FilePane pane in panes) pane.ClearTransientState();
        AppClipboard.SetCopy(new string[0]);
        SelectionList dialog = selectionList;
        selectionList = null;
        if (dialog != null)
        {
            dialog.DialogResult = DialogResult.Cancel;
            dialog.Close();
        }
        if (activePane != null) activePane.FocusList();
        UpdateStatus();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // Tab and other dialog keys may never reach a control's KeyDown handler.
        if (keyData != Keys.Escape) HandleKey(keyData);
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Program.RegisterWindow(Handle);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == Program.CopyDataMessage)
        {
            message.Result = IntPtr.Zero;
            if (message.LParam == IntPtr.Zero) return;
            Program.CopyData data = (Program.CopyData)Marshal.PtrToStructure(message.LParam, typeof(Program.CopyData));
            if (data.Tag != new IntPtr(Program.ActivationData) || data.Size != 16 || data.Data == IntPtr.Zero) return;
            Guid desktop = (Guid)Marshal.PtrToStructure(data.Data, typeof(Guid));
            if (desktop == Guid.Empty) return;
            if (activationResultReady)
            {
                if (activationResultDesktop == desktop)
                {
                    activationResultReady = false;
                    message.Result = new IntPtr(activationResult);
                    return;
                }
                activationResultReady = false;
            }
            if (activationPending)
            {
                message.Result = new IntPtr(Program.ActivationNotReady);
                return;
            }
            activationPending = true;
            try
            {
                // Defer COM: calling it inside a synchronous WM_COPYDATA handler returns RPC_E_CANTCALLOUT_ININPUTSYNCCALL.
                BeginInvoke(new MethodInvoker(delegate
                {
                    int result;
                    try
                    {
                        if (!Visible) result = Program.ActivationNotReady;
                        else if (!Program.MoveWindowToDesktop(Handle, desktop)) result = Program.ActivationNotReady;
                        else result = Program.ActivateWindow(Handle);
                    }
                    catch (Exception ex) { result = Marshal.GetHRForException(ex); }
                    activationResult = result;
                    activationResultDesktop = desktop;
                    activationPending = false;
                    activationResultReady = true;
                }));
                message.Result = new IntPtr(Program.ActivationNotReady);
            }
            catch (Exception ex)
            {
                activationPending = false;
                message.Result = new IntPtr(Marshal.GetHRForException(ex));
            }
            return;
        }
        if (message.Msg == 0x0082) Program.UnregisterWindow(message.HWnd); // WM_NCDESTROY
        base.WndProc(ref message);
    }
}
