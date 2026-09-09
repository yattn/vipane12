using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

static class Workspace
{
    public const int PaneCount = 12;

    public static string GetBaseDirectory()
    {
        string exeDir = AppDomain.CurrentDomain.BaseDirectory;
        if (File.Exists(Path.Combine(exeDir, "portable.flag")))
        {
            return exeDir;
        }
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "vipane12");
    }

    public static string GetWorkspacePath()
    {
        return Path.Combine(GetBaseDirectory(), "workspace.txt");
    }

    public static string GetCommandsDirectory()
    {
        return Path.Combine(GetBaseDirectory(), "commands");
    }

    public static string GetDefaultDirectory()
    {
        return Path.GetPathRoot(Environment.SystemDirectory);
    }

    public static string[] Load(IWin32Window owner = null)
    {
        string[] paths = new string[PaneCount];
        for (int i = 0; i < PaneCount; i++)
        {
            paths[i] = GetDefaultDirectory();
        }
        string file = GetWorkspacePath();
        try
        {
            using (StreamReader reader = new StreamReader(file, new UTF8Encoding(false, true)))
            {
                for (int i = 0; i < PaneCount; i++)
                {
                    string line = reader.ReadLine();
                    if (line == null) break;
                    if (!string.IsNullOrWhiteSpace(line) && Directory.Exists(line)) paths[i] = line;
                }
            }
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
        catch (Exception ex)
        {
            FileOperations.ShowError(owner, "Load workspace", file, ex.Message);
        }
        return paths;
    }

    public static void Save(string[] paths, IWin32Window owner = null)
    {
        try
        {
            string file = GetWorkspacePath();
            string dir = Path.GetDirectoryName(file);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            string[] lines = new string[PaneCount];
            for (int i = 0; i < PaneCount; i++)
            {
                if (paths != null && i < paths.Length && paths[i] != null)
                {
                    lines[i] = paths[i];
                }
                else
                {
                    lines[i] = "";
                }
            }
            File.WriteAllLines(file, lines, new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            FileOperations.ShowError(owner, "Save workspace", GetWorkspacePath(), ex.Message);
        }
    }
}
