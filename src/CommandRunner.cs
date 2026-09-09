using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

public static class CommandRunner
{
    public static string[] ListCommandFiles(IWin32Window owner)
    {
        string dir = Workspace.GetCommandsDirectory();
        try
        {
            List<string> files = new List<string>();
            foreach (string path in Directory.GetFiles(dir))
                if (string.Equals(Path.GetExtension(path), ".cmd", StringComparison.OrdinalIgnoreCase)) files.Add(path);
            files.Sort(StringComparer.OrdinalIgnoreCase);
            return files.ToArray();
        }
        catch (DirectoryNotFoundException) { return new string[0]; }
        catch (Exception ex)
        {
            FileOperations.ShowError(owner, "List commands", dir, ex.Message);
            return new string[0];
        }
    }

    public static string GetCommandName(string cmdPath)
    {
        return Path.GetFileNameWithoutExtension(cmdPath);
    }

    public static bool Run(string cmdPath, string selectedPath, string currentDir, IWin32Window owner)
    {
        try
        {
            cmdPath = Path.GetFullPath(cmdPath);
            currentDir = Path.GetFullPath(currentDir);
            if (!File.Exists(cmdPath)) throw new FileNotFoundException("Command not found.", cmdPath);
            if (!Directory.Exists(currentDir)) throw new DirectoryNotFoundException("Working directory not found: " + currentDir);
            if (!string.IsNullOrEmpty(selectedPath)) selectedPath = Path.GetFullPath(selectedPath);
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            psi.UseShellExecute = false;
            psi.WorkingDirectory = currentDir;
            // Expand paths once, from child-only variables, so %, ! and & stay literal.
            psi.EnvironmentVariables["VIPANE12_COMMAND"] = cmdPath;
            psi.EnvironmentVariables["VIPANE12_SELECTED"] = selectedPath ?? "";
            psi.EnvironmentVariables["VIPANE12_DIRECTORY"] = currentDir;
            psi.Arguments = "/d /v:off /s /c \"\"%VIPANE12_COMMAND%\" \""
                + (string.IsNullOrEmpty(selectedPath) ? "" : "%VIPANE12_SELECTED%")
                + "\" \"%VIPANE12_DIRECTORY%\"\"";
            using (Process process = Process.Start(psi)) { }
            return true;
        }
        catch (Exception ex)
        {
            FileOperations.ShowError(owner, "Run command", cmdPath, ex.Message);
            return false;
        }
    }
}
