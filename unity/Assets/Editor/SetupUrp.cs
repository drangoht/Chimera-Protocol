using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Installe et configure URP avec le <b>renderer 2D</b> (docs/UNITY_MIGRATION_PLAN.md — Lot 2).
///
/// <para><b>Pourquoi URP est obligatoire ici, et non un choix de confort.</b> Le jeu Godot utilise
/// <b>108 <c>PointLight2D</c></b> et 26 <c>ShaderMaterial</c>. Le pipeline intégré d'Unity n'a
/// <b>aucune</b> notion de lumière 2D : reproduire l'éclairage du jeu y demanderait de réécrire à la
/// main ce qu'URP fournit. La décision est donc dictée par le contenu, pas par une préférence.</para>
///
/// <para>L'installation du paquet vit dans <see cref="SetupUrpPackage"/> : elle doit avoir lieu
/// AVANT, puisque ce fichier-ci reference des types qui n'existent qu'une fois URP present.</para>
/// </summary>
public static class SetupUrp
{
    private const string AssetDir   = "Assets/Settings";
    private const string RendererPath = AssetDir + "/Renderer2D.asset";
    private const string PipelinePath = AssetDir + "/UrpPipeline2D.asset";

    /// <summary>
    /// Crée le pipeline et son renderer 2D, puis les rend actifs. Sans l'affectation dans les
    /// réglages Graphics ET Quality, Unity continue silencieusement d'utiliser le pipeline
    /// intégré — et les lumières 2D n'ont alors aucun effet, sans le moindre message.
    /// </summary>
    public static void ConfigurePipeline()
    {
        Directory.CreateDirectory(Path.Combine(
            Directory.GetParent(Application.dataPath)!.FullName, AssetDir));

        var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererPath);
        if (renderer == null)
        {
            renderer = ScriptableObject.CreateInstance<Renderer2DData>();
            AssetDatabase.CreateAsset(renderer, RendererPath);
            Debug.Log("[URP] renderer 2D cree : " + RendererPath);
        }

        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
        if (pipeline == null)
        {
            pipeline = UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(pipeline, PipelinePath);
            Debug.Log("[URP] pipeline cree : " + PipelinePath);
        }

        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline         = pipeline;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        bool ok = GraphicsSettings.defaultRenderPipeline != null;
        Debug.Log($"[URP] pipeline actif : {ok} ({GraphicsSettings.defaultRenderPipeline?.name})");
        if (!ok) EditorApplication.Exit(1);
    }
}
