using UnityEngine;

/// <summary>
/// Gerbe de particules ponctuelle — le portage des <c>GpuParticles2D</c> en mode « one-shot » de
/// Godot : impacts, morts, souffle de flammes, éclats de givre.
///
/// <para>Un seul système, reconfiguré à chaque émission, plutôt qu'un système par effet. Les
/// modules d'un <c>ParticleSystem</c> Unity se règlent par structures temporaires (<c>var main =
/// ps.main</c>), ce qui rend la reconfiguration très bon marché — bien moins chère que créer puis
/// détruire un système par impact, à plusieurs dizaines de morts par seconde en nuée.</para>
/// </summary>
public sealed class VfxBurst : MonoBehaviour
{
    private ParticleSystem? _ps;
    private float _left;

    /// <summary>Construit le système. Appelé une seule fois, à la création de l'objet.</summary>
    internal void Build()
    {
        _ps = gameObject.AddComponent<ParticleSystem>();

        var main = _ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.gravityModifier = 0f;

        // Espace MONDE : sans cela les particules suivraient l'objet recyclé, qui est repositionné à
        // l'émission suivante — la gerbe précédente se téléporterait avec lui.
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 200;

        var emission = _ps.emission;
        emission.enabled = false;   // rien d'automatique : on émet nous-mêmes, en une fois

        var shape = _ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;

        // Émission depuis le BORD d'un cercle minuscule : c'est le bord qui donne aux particules
        // leur direction radiale. Avec un rayon nul, elles partiraient toutes dans le même sens.
        shape.radius = 0.01f;
        shape.radiusThickness = 0f;
        shape.arcMode = ParticleSystemShapeMultiModeValue.Random;

        var col = _ps.colorOverLifetime;
        col.enabled = true;

        var renderer = GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = VfxPrimitives.AdditiveSpark;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;

        _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    /// <summary>
    /// Émet une gerbe. <paramref name="dirDeg"/> et <paramref name="spreadDeg"/> décrivent le
    /// secteur d'émission ; un secteur de 360° donne une explosion radiale.
    /// </summary>
    internal void Emit(Vector2 position, Color from, Color to, int count,
                       float speedMin, float speedMax, float sizePx, float life,
                       float dirDeg, float spreadDeg, int order)
    {
        if (_ps == null) return;

        gameObject.SetActive(true);
        transform.position = position;

        // L'arc d'un cercle démarre sur +X et tourne dans le sens direct : on oriente donc l'objet
        // sur le bord d'attaque du secteur, et l'arc couvre le reste.
        transform.rotation = Quaternion.Euler(0f, 0f, dirDeg - spreadDeg * 0.5f);

        var main = _ps.main;
        main.startLifetime = life;
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sizePx * 0.6f, sizePx);
        main.startColor = Color.white;

        var shape = _ps.shape;
        shape.arc = Mathf.Clamp(spreadDeg, 1f, 360f);

        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
            new[] { new GradientAlphaKey(from.a, 0f), new GradientAlphaKey(0f, 1f) });

        var col = _ps.colorOverLifetime;
        col.color = new ParticleSystem.MinMaxGradient(grad);

        GetComponent<ParticleSystemRenderer>().sortingOrder = order;

        _ps.Emit(count);
        _left = Mathf.Max(_left, life + 0.05f);
    }

    private void Update()
    {
        _left -= Time.deltaTime;
        if (_left > 0f) return;

        gameObject.SetActive(false);
        Vfx.Recycle(this);
    }
}
