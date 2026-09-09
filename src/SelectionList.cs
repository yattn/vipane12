using System;
using System.Drawing;
using System.Windows.Forms;

public sealed class SelectionList : Form
{
    private ListBox list;
    private string[] values;
    private string result;
    private readonly bool allowNavigationKey;

    public string Result
    {
        get { return result; }
    }

    public int ItemCount
    {
        get { return list.Items.Count; }
    }

    public SelectionList(string title, string[] names, string[] values, bool allowNavigationKey = false)
    {
        this.allowNavigationKey = allowNavigationKey;
        this.Text = title;
        this.Font = new Font("Meiryo UI", this.Font.Size, this.Font.Style);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.StartPosition = FormStartPosition.CenterParent;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.ShowInTaskbar = false;
        this.Width = 480;
        this.Height = 320;

        list = new ListBox();
        list.Dock = DockStyle.Fill;
        list.IntegralHeight = false;
        list.HorizontalScrollbar = true;
        list.SelectionMode = SelectionMode.One;
        list.ImeMode = ImeMode.Disable;
        list.KeyDown += new KeyEventHandler(List_KeyDown);
        list.KeyPress += new KeyPressEventHandler(List_KeyPress);
        list.MouseDoubleClick += new MouseEventHandler(List_DoubleClick);
        this.Controls.Add(list);

        this.values = values;
        for (int i = 0; i < names.Length; i++)
        {
            list.Items.Add(names[i]);
        }
        if (list.Items.Count > 0)
        {
            list.ClearSelected();
            list.SetSelected(0, true);
        }
        this.Shown += new EventHandler(delegate(object s, EventArgs e) { list.Focus(); });
    }

    public static string Select(IWin32Window owner, string title, string[] names, string[] values, bool allowNavigationKey = false)
    {
        if (names == null || names.Length == 0)
        {
            return null;
        }
        using (SelectionList f = new SelectionList(title, names, values, allowNavigationKey))
        {
            Control control = owner as Control;
            MainForm main = control == null ? null : control.FindForm() as MainForm;
            if (main != null) main.SetSelectionList(f);
            try
            {
                return f.ShowDialog(main == null ? owner : main) == DialogResult.OK ? f.Result : null;
            }
            finally { if (main != null) main.SetSelectionList(null); }
        }
    }

    private void Accept()
    {
        int i = list.SelectedIndex;
        if (i >= 0 && i < values.Length)
        {
            result = values[i];
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }

    private void Cancel()
    {
        result = null;
        this.DialogResult = DialogResult.Cancel;
        this.Close();
    }

    private void List_DoubleClick(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && list.IndexFromPoint(e.Location) >= 0) Accept();
    }

    private void List_KeyPress(object sender, KeyPressEventArgs e)
    {
        e.Handled = true;
    }

    private void List_KeyDown(object sender, KeyEventArgs e)
    {
        MainForm main = Owner as MainForm;
        if (main != null && main.HandleKey(e.KeyData))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }
        if (e.Modifiers != Keys.None)
        {
            return;
        }
        if (e.KeyCode == Keys.J)
        {
            MoveCursor(1);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.K)
        {
            MoveCursor(-1);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Enter || (allowNavigationKey && e.KeyCode == Keys.L))
        {
            Accept();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            Cancel();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private void MoveCursor(int delta)
    {
        if (list.Items.Count == 0)
        {
            return;
        }
        int i = list.SelectedIndex + delta;
        if (i < 0)
        {
            i = 0;
        }
        if (i >= list.Items.Count)
        {
            i = list.Items.Count - 1;
        }
        list.ClearSelected();
        list.SetSelected(i, true);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        MainForm main = Owner as MainForm;
        if (main != null && keyData != Keys.Escape) main.HandleKey(keyData);
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
