using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Construit les <see cref="SpriteFramesAsset"/> à partir des manifestes JSON produits par
/// <c>tools/unity/convert_spriteframes.py</c> (docs/UNITY_MIGRATION_PLAN.md §7.2).
///
/// <para><b>Pourquoi cette étape est côté Unity et non côté Python.</b> Un asset Unity référence
/// ses sprites par GUID, attribués et gérés par l'AssetDatabase. Les fabriquer depuis un script
/// externe reviendrait à deviner ces identifiants — silencieusement faux dès qu'un GUID change,
/// avec pour symptôme des références vides qu'aucune compilation ne signale. Python produit donc
/// une description neutre (des chemins), et c'est ici que les références sont résolues pour de bon.
/// </para>
///
/// <para>Usage : <c>Unity.exe -batchmode -quit -executeMethod BuildSpriteFrames.Run</c></para>
/// </summary>
public static class BuildSpriteFrames
{
    private const string ManifestDir = "Assets/Editor/spriteframes";
    // Sous Resources/ : c'est la SEULE façon de charger un asset par son nom à l'exécution.
    // Rangés ailleurs, les jeux d'animations existent dans le projet mais restent inaccessibles au
    // spawner — des ennemis parfaitement fonctionnels, et parfaitement invisibles.
    private const string OutputDir   = "Assets/Resources/SpriteFrames";

    [Serializable] private sealed class Manifest
    {
        public string id = "";
        public AnimEntry[] animations = Array.Empty<AnimEntry>();
    }

    [Serializable] private sealed class AnimEntry
    {
        public string name = "";
        public float speed = 8f;
        public bool loop = true;
        public string[] frames = Array.Empty<string>();
    }

    [MenuItem("Chimera/Construire les SpriteFrames")]
    public static void Run()
    {
        string manifestFull = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, ManifestDir);
        if (!Directory.Exists(manifestFull))
        {
            Debug.LogError($"[SPRITEFRAMES] manifestes introuvables dans {ManifestDir} — " +
                           "lancer d'abord tools/unity/convert_spriteframes.py");
            EditorApplication.Exit(1);
            return;
        }

        Directory.CreateDirectory(Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, OutputDir));

        string[] files = Directory.GetFiles(manifestFull, "*.json");
        int built = 0, animCount = 0, frameCount = 0;
        var missing = new List<string>();

        try
        {
            AssetDatabase.StartAssetEditing();

            foreach (string file in files)
            {
                var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(file));
                if (manifest == null || string.IsNullOrEmpty(manifest.id))
                {
                    missing.Add($"{Path.GetFileName(file)} : manifeste illisible");
                    continue;
                }

                string outPath = $"{OutputDir}/{manifest.id}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<SpriteFramesAsset>(outPath);
                bool isNew = asset == null;
                if (isNew) asset = ScriptableObject.CreateInstance<SpriteFramesAsset>();

                asset.Id = manifest.id;
                var animations = new List<SpriteFramesAsset.Animation>();

                foreach (var a in manifest.animations)
                {
                    var sprites = new List<Sprite>();
                    foreach (string p in a.frames)
                    {
                        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(p);
                        // Une référence manquante ne doit JAMAIS passer en silence : elle donnerait
                        // une animation à trous, visible seulement en jouant.
                        if (sprite == null) missing.Add($"{manifest.id}/{a.name} : {p}");
                        else sprites.Add(sprite);
                    }

                    animations.Add(new SpriteFramesAsset.Animation
                    {
                        Name = a.name, Speed = a.speed, Loop = a.loop, Frames = sprites.ToArray(),
                    });
                    animCount++;
                    frameCount += sprites.Count;
                }

                asset.Animations = animations.ToArray();

                if (isNew) AssetDatabase.CreateAsset(asset, outPath);
                else       EditorUtility.SetDirty(asset);

                built++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
        }

        Debug.Log($"[SPRITEFRAMES] {built} asset(s) construits — {animCount} animations, " +
                  $"{frameCount} images resolues.");

        if (missing.Count > 0)
        {
            Debug.LogError($"[SPRITEFRAMES] {missing.Count} sprite(s) introuvable(s) :\n  " +
                           string.Join("\n  ", missing.GetRange(0, Math.Min(20, missing.Count))));
            EditorApplication.Exit(1);
        }
    }
}
