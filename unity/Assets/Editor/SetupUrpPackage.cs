using System.Threading;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

/// <summary>
/// Installe le paquet URP. Volontairement SÉPARÉ de sa configuration (<c>SetupUrp</c>) : un script
/// qui référence les types URP ne peut pas compiler tant que le paquet n'est pas là — donc il ne
/// peut pas être celui qui l'installe.
/// </summary>
public static class SetupUrpPackage
{
    private const string PackageId = "com.unity.render-pipelines.universal";

    /// <summary>
    /// Ajoute URP <b>sans épingler de version</b> : le gestionnaire choisit celle qui correspond à
    /// cet éditeur. Un numéro en dur casserait à la première montée de version d'Unity.
    /// </summary>
    public static void Add()
    {
        Debug.Log($"[URP] installation de {PackageId}…");
        AddRequest request = Client.Add(PackageId);

        while (!request.IsCompleted) Thread.Sleep(100);

        if (request.Status != StatusCode.Success)
        {
            Debug.LogError($"[URP] echec : {request.Error?.message}");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log($"[URP] installe : {request.Result.name} {request.Result.version}");
    }
}
