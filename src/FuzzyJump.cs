using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

public static class FuzzyJump
{
    public static bool TryJump(IWin32Window owner, string root, string pattern, out string dest, Action<string> searching = null)
    {
        dest = null;
        if (root == null || !Directory.Exists(root))
        {
            throw new DirectoryNotFoundException("Fuzzy Jump root not found: " + root);
        }
        root = Path.GetFullPath(root);
        if (root.TrimEnd('\\') == Path.GetPathRoot(root).TrimEnd('\\'))
        {
            throw new InvalidOperationException("Fuzzy Jump cannot search an entire drive. Open a working directory first.");
        }
        if (string.IsNullOrWhiteSpace(pattern)) throw new ArgumentException("Enter a Fuzzy Jump pattern.");
        string[] tokens = pattern.Split('/');
        foreach (string token in tokens)
        {
            if (string.IsNullOrWhiteSpace(token) || token == "." || token == ".."
                || token.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("Invalid Fuzzy Jump token: " + token);
        }
        string current = root;
        for (int t = 0; t < tokens.Length; t++)
        {
            if (searching != null) searching(tokens[t]);
            string[] cands = FindMatches(current, tokens[t]);
            if (cands.Length == 0)
            {
                throw new DirectoryNotFoundException("No directory matches '" + tokens[t] + "' below " + current);
            }
            if (cands.Length == 1)
            {
                current = cands[0];
                continue;
            }
            string picked = SelectionList.Select(owner, "Select directory", cands, cands, true);
            if (picked == null)
            {
                return false;
            }
            current = picked;
        }
        dest = current;
        return true;
    }

    private static string[] FindMatches(string root, string token)
    {
        token = token.Normalize(NormalizationForm.FormKC);
        string[] tokenElems = Elems(token);
        int thr = tokenElems.Length <= 3 ? 1 : 2;
        int best = 4;
        List<string> result = new List<string>();
        Stack<string> stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            string dir = stack.Pop();
            string[] children;
            try
            {
                // A junction can escape the root or lead back to an ancestor.
                if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0) continue;
                children = Directory.GetDirectories(dir);
            }
            catch (UnauthorizedAccessException) { continue; }
            catch (IOException) { continue; }
            if (dir != root)
            {
                string name = Path.GetFileName(dir).Normalize(NormalizationForm.FormKC);
                int level = MatchLevel(name, token, tokenElems, thr);
                if (level < best)
                {
                    best = level;
                    result.Clear();
                }
                if (level == best && level < 4) result.Add(dir);
            }
            foreach (string child in children) stack.Push(child);
        }
        string[] cands = result.ToArray();
        Array.Sort(cands, StringComparer.OrdinalIgnoreCase);
        return cands;
    }

    private static int MatchLevel(string name, string token, string[] tokenElems, int thr)
    {
        if (name == token)
        {
            return 0;
        }
        if (string.Equals(name, token, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }
        if (name.StartsWith(token, StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }
        string[] nameElems = Elems(name);
        if (Math.Abs(tokenElems.Length - nameElems.Length) <= thr && Distance(tokenElems, nameElems) <= thr)
        {
            return 3;
        }
        return 4;
    }

    private static string[] Elems(string s)
    {
        List<string> result = new List<string>();
        TextElementEnumerator e = StringInfo.GetTextElementEnumerator(s);
        while (e.MoveNext())
        {
            result.Add(e.GetTextElement());
        }
        return result.ToArray();
    }

    private static int Distance(string[] a, string[] b)
    {
        int n = a.Length;
        int m = b.Length;
        if (n == 0)
        {
            return m;
        }
        if (m == 0)
        {
            return n;
        }
        int[,] d = new int[n + 1, m + 1];
        for (int i = 0; i <= n; i++)
        {
            d[i, 0] = i;
        }
        for (int j = 0; j <= m; j++)
        {
            d[0, j] = j;
        }
        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = string.Equals(a[i - 1], b[j - 1], StringComparison.OrdinalIgnoreCase) ? 0 : 1;
                int v = d[i - 1, j] + 1;
                int x = d[i, j - 1] + 1;
                if (x < v)
                {
                    v = x;
                }
                int y = d[i - 1, j - 1] + cost;
                if (y < v)
                {
                    v = y;
                }
                if (i > 1 && j > 1
                    && string.Equals(a[i - 1], b[j - 2], StringComparison.OrdinalIgnoreCase)
                    && string.Equals(a[i - 2], b[j - 1], StringComparison.OrdinalIgnoreCase))
                {
                    int z = d[i - 2, j - 2] + 1;
                    if (z < v)
                    {
                        v = z;
                    }
                }
                d[i, j] = v;
            }
        }
        return d[n, m];
    }
}
