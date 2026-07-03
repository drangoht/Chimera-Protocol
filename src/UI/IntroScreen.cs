using Godot;
using System;

/// <summary>
/// Cinématique d'introduction jouée au lancement (scène de boot), puis bascule sur le menu.
///
/// Ce n'est plus un simple diaporama d'images fixes : chaque « plan » est une petite scène 2D
/// animée (sprites `AnimatedSprite2D` réutilisant les `SpriteFrames` du jeu, particules
/// `CpuParticles2D`, mouvements/zoom via `Tween`), synchronisée avec une ligne de narration
/// (`INTRO_BEAT_*`) et la musique dédiée `music_intro` (CC0, SRG774). Un « stage » `Node2D`
/// contient le monde ; il est vidé/reconstruit entre les plans et fondu au noir pour un
/// enchaînement propre. Le tout est skippable (touche / clic / manette / Échap → menu).
///
/// UI construite entièrement en code (la scène = root Control + script).
/// </summary>
public partial class IntroScreen : Control
{
    // Chemins d'assets réutilisés (tolérants à l'absence : le sprite reste vide si introuvable).
    private const string PlayerFrames    = "res://assets/sprites/player/player_frames.tres";
    private const string DroneFrames     = "res://assets/sprites/enemies/drone/drone_frames.tres";
    private const string ColossusFrames  = "res://assets/sprites/enemies/colossus/colossus_frames.tres";
    private const string SwarmFrames     = "res://assets/sprites/enemies/rustswarm/rustswarm_frames.tres";
    private const string NoyauIcon       = "res://assets/sprites/ui/ui_icon_noyau.png";
    private const string NoyauParticle   = "res://assets/sprites/vfx/vfx_particle_noyau.png";
    private const string FusionAura      = "res://assets/sprites/vfx/vfx_aura_fusionblade.png";

    private static readonly Color Cyan   = new(0.267f, 1f, 0.933f);
    private static readonly Color Violet = new(0.667f, 0.267f, 1f);
    private static readonly Color Rust   = new(0.85f, 0.42f, 0.18f);
    private static readonly Color Gold   = new(1f, 0.80f, 0.27f);
    private static readonly Color Soft   = new(0.85f, 0.88f, 0.95f);

    // Résolution logique de référence (project.godot : 1280×720).
    private static readonly Vector2 Center = new(640, 360);

    private Node2D   _stage    = null!;   // « monde » 2D — vidé/reconstruit à chaque plan
    private Control  _subtitle = null!;   // conteneur du texte de narration (fondu commun)
    private Label    _line     = null!;
    private ColorRect _fade    = null!;   // overlay noir (ouverture / fermeture)
    private ColorRect _flash   = null!;   // overlay blanc (flash du reveal de titre)
    private Control  _titleBox = null!;   // titre + tagline (reveal final)

    private Tween? _seq;
    private Tween? _zoom;              // tween de zoom du plan courant (tué à chaque changement)
    private bool   _leaving = false;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        // Racine en MouseFilter=Stop par défaut → elle capterait le clic comme événement GUI
        // et `_UnhandledInput` ne le verrait jamais. On laisse le clic retomber pour skipper.
        MouseFilter = MouseFilterEnum.Ignore;

        // Fond quasi-noir bleuté
        var bg = new ColorRect { Color = new Color(0.012f, 0.016f, 0.04f) };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(bg);

        // Stage 2D (fondu au noir via Modulate.a entre les plans)
        _stage = new Node2D { Modulate = new Color(1, 1, 1, 0) };
        AddChild(_stage);

        // Bloc sous-titre (narration), sous le centre
        _subtitle = new Control { Modulate = new Color(1, 1, 1, 0), MouseFilter = MouseFilterEnum.Ignore };
        _subtitle.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_subtitle);

        _line = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            AutowrapMode        = TextServer.AutowrapMode.WordSmart,
            MouseFilter         = MouseFilterEnum.Ignore,
        };
        _line.AnchorLeft = 0.10f; _line.AnchorRight = 0.90f;
        _line.AnchorTop  = 0.74f; _line.AnchorBottom = 0.92f;
        _line.OffsetLeft = _line.OffsetRight = _line.OffsetTop = _line.OffsetBottom = 0;
        _line.AddThemeFontSizeOverride("font_size", 22);
        _line.AddThemeColorOverride("font_color", Soft);
        _line.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f));
        _line.AddThemeConstantOverride("outline_size", 6);
        _subtitle.AddChild(_line);

        // Titre + tagline (masqués jusqu'au reveal final)
        _titleBox = new Control { Modulate = new Color(1, 1, 1, 0), MouseFilter = MouseFilterEnum.Ignore };
        _titleBox.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_titleBox);

        var title = new Label
        {
            Text                = Loc.T("INTRO_TITLE"),
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter         = MouseFilterEnum.Ignore,
        };
        title.AnchorLeft = 0.05f; title.AnchorRight = 0.95f;
        title.AnchorTop  = 0.36f; title.AnchorBottom = 0.52f;
        title.OffsetLeft = title.OffsetRight = title.OffsetTop = title.OffsetBottom = 0;
        title.AddThemeFontSizeOverride("font_size", 64);
        title.AddThemeColorOverride("font_color", Cyan);
        title.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        title.AddThemeConstantOverride("outline_size", 8);
        _titleBox.AddChild(title);

        var tagline = new Label
        {
            Text                = Loc.T("INTRO_TAGLINE"),
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter         = MouseFilterEnum.Ignore,
        };
        tagline.AnchorLeft = 0.05f; tagline.AnchorRight = 0.95f;
        tagline.AnchorTop  = 0.54f; tagline.AnchorBottom = 0.62f;
        tagline.OffsetLeft = tagline.OffsetRight = tagline.OffsetTop = tagline.OffsetBottom = 0;
        tagline.AddThemeFontSizeOverride("font_size", 22);
        tagline.AddThemeColorOverride("font_color", Gold);
        tagline.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f));
        tagline.AddThemeConstantOverride("outline_size", 5);
        _titleBox.AddChild(tagline);

        // Indice « passer »
        var hint = new Label
        {
            Text                = "— " + Loc.T("INTRO_SKIP") + " —",
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter         = MouseFilterEnum.Ignore,
            Modulate            = new Color(1, 1, 1, 0.5f),
        };
        hint.SetAnchorsPreset(LayoutPreset.BottomWide);
        hint.AnchorTop = 0.94f; hint.AnchorBottom = 0.99f;
        hint.OffsetLeft = hint.OffsetRight = hint.OffsetTop = hint.OffsetBottom = 0;
        hint.AddThemeFontSizeOverride("font_size", 13);
        hint.AddThemeColorOverride("font_color", Cyan);
        AddChild(hint);

        // Flash blanc (reveal de titre) — sous le fade de fermeture
        _flash = new ColorRect { Color = new Color(1, 1, 1, 0) };
        _flash.SetAnchorsPreset(LayoutPreset.FullRect);
        _flash.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_flash);

        // Overlay de fondu (noir → transparent à l'entrée, → noir à la sortie)
        _fade = new ColorRect { Color = new Color(0, 0, 0, 1) };
        _fade.SetAnchorsPreset(LayoutPreset.FullRect);
        _fade.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_fade);

        AudioSystem.Instance?.PlayMusic("music_intro", 1.5f);

        BuildSequence();
    }

    // -------------------------------------------------------------------------
    // Séquence maître : enchaîne les plans, chacun cadré sur une ligne de narration.
    // -------------------------------------------------------------------------

    private void BuildSequence()
    {
        _seq = CreateTween();
        _seq.TweenProperty(_fade, "color:a", 0f, 1.0);   // fondu d'ouverture

        AddShot("INTRO_BEAT_1", ShotConvergenceCore,  3.4);  // le Noyau d'Aether, avant la Convergence
        AddShot("INTRO_BEAT_2", ShotDroneCorruption,  3.4);  // la fusion : une machine se corrompt
        AddShot("INTRO_BEAT_3", ShotRustSwarm,        3.6);  // la Rouille Vivante déferle
        AddShot("INTRO_BEAT_4", ShotSanctuaryCore,    3.4);  // les Sanctuaires gardent les Noyaux
        AddShot("INTRO_BEAT_5", ShotArpenteurDescent, 3.8);  // « Ce sera toi » : l'Arpenteur avance

        _seq.TweenCallback(Callable.From(RevealTitle));      // flash + titre
        _seq.TweenInterval(2.6);
        _seq.TweenCallback(Callable.From(GoToMenu));
    }

    /// <summary>
    /// Ajoute un plan à la séquence : reconstruit le stage, fond entrant, tient la durée,
    /// fond sortant. Les mouvements internes du plan sont lancés par <paramref name="setup"/>.
    /// </summary>
    private void AddShot(string beatKey, Action setup, double holdSec)
    {
        _seq!.TweenCallback(Callable.From(() =>
        {
            _stage.Modulate = new Color(1, 1, 1, 0);   // reconstruit à l'état invisible
            _line.Text      = Loc.T(beatKey);
            RebuildStage(setup);
        }));
        _seq.TweenProperty(_stage,    "modulate:a", 1f, 0.7);
        _seq.Parallel().TweenProperty(_subtitle, "modulate:a", 1f, 0.7);
        _seq.Chain().TweenInterval(holdSec);
        _seq.TweenProperty(_stage,    "modulate:a", 0f, 0.5);
        _seq.Parallel().TweenProperty(_subtitle, "modulate:a", 0f, 0.4);
        _seq.Chain();
    }

    private void RebuildStage(Action setup)
    {
        foreach (var c in _stage.GetChildren())
            c.QueueFree();
        setup();
    }

    // -------------------------------------------------------------------------
    // Plans
    // -------------------------------------------------------------------------

    // Plan 1 — Le Noyau d'Aether pulse dans le noir, énergie qui monte. Zoom lent.
    private void ShotConvergenceCore()
    {
        var core = MakeSprite(NoyauIcon, Center, 5.5f, Violet);
        var t = core.CreateTween().SetLoops();
        t.TweenProperty(core, "scale", Vector2.One * 6.2f, 1.3).SetTrans(Tween.TransitionType.Sine);
        t.TweenProperty(core, "scale", Vector2.One * 5.5f, 1.3).SetTrans(Tween.TransitionType.Sine);

        AddParticles(NoyauParticle, Center + new Vector2(0, 40), Violet, amount: 42,
                     velocity: 55f, spread: 25f, direction: new Vector2(0, -1), scale: 2.6f);

        SlowZoom(1.0f, 1.12f, 6.5);
    }

    // Plan 2 — Un drone dérive, sa couleur vire à la rouille, glitch/flash : la fusion.
    private void ShotDroneCorruption()
    {
        var drone = MakeAnimated(DroneFrames, "move", new Vector2(360, 300), 4.5f, new Color(0.6f, 0.85f, 1f));
        var move = drone.CreateTween();
        move.TweenProperty(drone, "position", new Vector2(760, 340), 5.0).SetTrans(Tween.TransitionType.Sine);

        // Corruption progressive de la teinte : bleu machine → rouille
        var corrupt = drone.CreateTween();
        corrupt.TweenInterval(1.4);
        corrupt.TweenProperty(drone, "modulate", Rust, 1.6).SetTrans(Tween.TransitionType.Sine);
        corrupt.TweenCallback(Callable.From(() => AddParticles(
            NoyauParticle, drone.Position, Rust, amount: 30, velocity: 70f, spread: 180f,
            direction: Vector2.Right, scale: 2.0f)));

        AddParticles(NoyauParticle, new Vector2(560, 320), Violet, amount: 24, velocity: 40f,
                     spread: 180f, direction: Vector2.Up, scale: 1.8f);
        SlowZoom(1.05f, 1.0f, 6.5);
    }

    // Plan 3 — La Rouille déferle : nuée + colosse qui se dresse. Teinte rouille dominante.
    private void ShotRustSwarm()
    {
        var colossus = MakeAnimated(ColossusFrames, "move", new Vector2(880, 380), 3.4f, Rust);
        colossus.Modulate = new Color(Rust.R, Rust.G, Rust.B, 0f);
        var rise = colossus.CreateTween();
        rise.TweenProperty(colossus, "modulate:a", 1f, 1.4);
        rise.Parallel().TweenProperty(colossus, "position", new Vector2(880, 340), 2.5).SetTrans(Tween.TransitionType.Back);

        // Nuée qui traverse depuis la gauche
        var rng = new Random();
        for (int i = 0; i < 7; i++)
        {
            var start = new Vector2(-60 - i * 40, 250 + rng.Next(-90, 160));
            var swarm = MakeAnimated(SwarmFrames, "move", start, 2.6f, new Color(0.9f, 0.55f, 0.35f));
            var target = new Vector2(700 + rng.Next(-60, 120), Center.Y + rng.Next(-100, 100));
            var t = swarm.CreateTween();
            t.TweenProperty(swarm, "position", target, 3.2 + rng.NextDouble()).SetTrans(Tween.TransitionType.Sine);
        }

        AddParticles(NoyauParticle, new Vector2(300, 360), Rust, amount: 60, velocity: 120f,
                     spread: 40f, direction: Vector2.Right, scale: 1.6f);
        SlowZoom(1.12f, 1.0f, 7.0);
    }

    // Plan 4 — Un Noyau pur luit dans le calme retrouvé : l'espoir. Teinte cyan.
    private void ShotSanctuaryCore()
    {
        var core = MakeSprite(NoyauIcon, Center + new Vector2(0, -20), 5.0f, Cyan);
        var pulse = core.CreateTween().SetLoops();
        pulse.TweenProperty(core, "modulate", new Color(0.6f, 1f, 0.95f), 1.1).SetTrans(Tween.TransitionType.Sine);
        pulse.TweenProperty(core, "modulate", Cyan, 1.1).SetTrans(Tween.TransitionType.Sine);

        AddParticles(NoyauParticle, Center + new Vector2(0, 60), Cyan, amount: 50, velocity: 45f,
                     spread: 30f, direction: Vector2.Up, scale: 2.2f);
        SlowZoom(1.0f, 1.08f, 6.5);
    }

    // Plan 5 — « Ce sera toi » : l'Arpenteur avance vers le joueur, aura de fusion qui s'allume.
    private void ShotArpenteurDescent()
    {
        var aura = MakeSprite(FusionAura, Center + new Vector2(0, 30), 0.5f, new Color(Cyan.R, Cyan.G, Cyan.B, 0f));
        var auraT = aura.CreateTween();
        auraT.TweenInterval(1.6);
        auraT.TweenProperty(aura, "modulate:a", 0.9f, 1.2);
        auraT.Parallel().TweenProperty(aura, "scale", Vector2.One * 7f, 1.8).SetTrans(Tween.TransitionType.Sine);

        var player = MakeAnimated(PlayerFrames, "run_down", new Vector2(640, 220), 2.6f, Colors.White);
        var walk = player.CreateTween();
        walk.TweenProperty(player, "position", Center + new Vector2(0, 40), 3.4).SetTrans(Tween.TransitionType.Sine);
        walk.Parallel().TweenProperty(player, "scale", Vector2.One * 5.5f, 3.4).SetTrans(Tween.TransitionType.Sine);

        AddParticles(NoyauParticle, Center + new Vector2(0, 120), Cyan, amount: 40, velocity: 60f,
                     spread: 60f, direction: Vector2.Up, scale: 1.8f);
        SlowZoom(1.0f, 1.15f, 7.0);
    }

    // -------------------------------------------------------------------------
    // Reveal de titre
    // -------------------------------------------------------------------------

    private void RevealTitle()
    {
        if (_leaving) return;
        var t = CreateTween();
        t.TweenProperty(_flash, "color:a", 0.85f, 0.2);           // flash blanc
        t.TweenProperty(_flash, "color:a", 0f, 0.6);
        t.Parallel().TweenProperty(_titleBox, "modulate:a", 1f, 0.8);
        t.Parallel().TweenProperty(_subtitle, "modulate:a", 0f, 0.3);
    }

    // -------------------------------------------------------------------------
    // Fabriques de nœuds
    // -------------------------------------------------------------------------

    private Sprite2D MakeSprite(string texPath, Vector2 pos, float scale, Color modulate)
    {
        var s = new Sprite2D
        {
            Position      = pos,
            Scale         = Vector2.One * scale,
            Modulate      = modulate,
            TextureFilter = TextureFilterEnum.Nearest,
        };
        if (ResourceLoader.Exists(texPath))
            s.Texture = GD.Load<Texture2D>(texPath);
        _stage.AddChild(s);
        return s;
    }

    private AnimatedSprite2D MakeAnimated(string framesPath, string anim, Vector2 pos, float scale, Color modulate)
    {
        var a = new AnimatedSprite2D
        {
            Position      = pos,
            Scale         = Vector2.One * scale,
            Modulate      = modulate,
            TextureFilter = TextureFilterEnum.Nearest,
        };
        if (ResourceLoader.Exists(framesPath))
        {
            var frames = GD.Load<SpriteFrames>(framesPath);
            a.SpriteFrames = frames;
            string play = frames.HasAnimation(anim) ? anim
                        : frames.HasAnimation("idle") ? "idle"
                        : (frames.GetAnimationNames().Length > 0 ? frames.GetAnimationNames()[0] : anim);
            if (frames.HasAnimation(play)) a.Play(play);
        }
        _stage.AddChild(a);
        return a;
    }

    private void AddParticles(string texPath, Vector2 pos, Color color, int amount, float velocity,
                              float spread, Vector2 direction, float scale)
    {
        var p = new CpuParticles2D
        {
            Position          = pos,
            Amount            = amount,
            Lifetime          = 2.2,
            Direction         = direction,
            Spread            = spread,
            InitialVelocityMin = velocity * 0.6f,
            InitialVelocityMax = velocity,
            ScaleAmountMin    = scale * 0.6f,
            ScaleAmountMax    = scale,
            Color             = color,
            Gravity           = Vector2.Zero,
            TextureFilter     = TextureFilterEnum.Nearest,
            Emitting          = true,
        };
        if (ResourceLoader.Exists(texPath))
            p.Texture = GD.Load<Texture2D>(texPath);
        _stage.AddChild(p);
    }

    /// <summary>Zoom lent du stage autour du centre écran (effet caméra cinématique).</summary>
    private void SlowZoom(float from, float to, double dur)
    {
        _zoom?.Kill();
        _stage.Scale    = Vector2.One * from;
        _stage.Position = Center * (1f - from);
        _zoom = _stage.CreateTween().SetParallel();
        _zoom.TweenProperty(_stage, "scale",    Vector2.One * to,    dur).SetTrans(Tween.TransitionType.Sine);
        _zoom.TweenProperty(_stage, "position", Center * (1f - to),  dur).SetTrans(Tween.TransitionType.Sine);
    }

    // -------------------------------------------------------------------------
    // Skip / sortie
    // -------------------------------------------------------------------------

    public override void _UnhandledInput(InputEvent @event)
    {
        bool trigger = @event is InputEventKey { Pressed: true }
                    || @event is InputEventMouseButton { Pressed: true }
                    || @event is InputEventJoypadButton { Pressed: true };
        if (trigger)
        {
            GetViewport().SetInputAsHandled();
            GoToMenu();
        }
    }

    private void GoToMenu()
    {
        if (_leaving) return;
        _leaving = true;
        _seq?.Kill();

        var t = CreateTween();
        t.TweenProperty(_fade, "color:a", 1f, 0.4);
        t.TweenCallback(Callable.From(() =>
            GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn")));
    }
}
