/// <summary>
/// Comparaison de numéros de version sémantiques « MAJEUR.MINEUR.CORRECTIF » (logique pure, testable).
/// Sert au contrôle de mise à jour du menu : la version distante (itch/GitHub) est-elle plus récente
/// que la version embarquée dans l'exe ? Tolère les composants manquants et les suffixes non numériques.
/// </summary>
public static class VersionCompare
{
    /// <summary>Vrai si <paramref name="remote"/> est strictement plus récente que <paramref name="local"/>.</summary>
    public static bool IsNewer(string remote, string local)
    {
        int[] r = Parse(remote);
        int[] l = Parse(local);
        int n = System.Math.Max(r.Length, l.Length);
        for (int i = 0; i < n; i++)
        {
            int rc = i < r.Length ? r[i] : 0;
            int lc = i < l.Length ? l[i] : 0;
            if (rc != lc) return rc > lc;
        }
        return false;
    }

    /// <summary>Découpe « 1.2.0 » (ou « v1.2.0-rc1 ») en composants entiers ; ignore ce qui n'est pas un nombre.</summary>
    private static int[] Parse(string version)
    {
        if (string.IsNullOrWhiteSpace(version)) return System.Array.Empty<int>();

        string[] parts = version.Trim().TrimStart('v', 'V').Split('.');
        var result = new System.Collections.Generic.List<int>(parts.Length);
        foreach (string part in parts)
        {
            var digits = new System.Text.StringBuilder();
            foreach (char c in part)
            {
                if (char.IsDigit(c)) digits.Append(c);
                else break; // « 0-rc1 » -> 0
            }
            result.Add(digits.Length > 0 ? int.Parse(digits.ToString()) : 0);
        }
        return result.ToArray();
    }
}
