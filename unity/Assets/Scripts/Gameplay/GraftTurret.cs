using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Une tourelle de la <b>Ruche de Tourelles</b> — un corps autonome ancré autour du porteur.
///
/// <para><b>Ce que le portage en avait fait.</b> Les quatre tourelles n'existaient pas : la greffe se
/// résumait à tirer quatre projectiles <i>depuis le joueur</i>, tous vers la même cible, à la même
/// image. Rien à l'écran ne disait qu'on portait quoi que ce soit — la greffe la plus visible du jeu
/// était devenue une augmentation de cadence anonyme, alors qu'elle coûte une jauge entière et
/// consomme deux emplacements avant fusion.</para>
///
/// <para><b>Elles ne sont pas en orbite : elles suivent.</b> Chacune garde un point d'ancrage autour
/// du porteur et s'y rend à vitesse finie. C'est ce retard qui les fait lire comme des alliées qu'on
/// entraîne derrière soi, là où une orbite parfaite les aurait rendues indiscernables des symbiotes
/// de la greffe voisine.</para>
/// </summary>
public sealed class GraftTurret : MonoBehaviour
{
    /// <summary>Angle d'ancroche autour du porteur, en radians.</summary>
    private float _anchorAngle;

    private Transform? _carrier;
    private Player? _player;
    private LineRenderer? _link;
    private SpriteRenderer? _sprite;
    private SpriteRenderer? _barrel;

    /// <summary>Échelle de rendu du châssis et du canon (sprites en PPU 1, côté 32).</summary>
    private const float TurretScale = 1.15f;

    private float _anchorRadius = 90f;
    private float _followSpeed = 120f;
    private float _cooldown = 1f;
    private float _cooldownFloor = 0.15f;
    private bool _followsCdr = true;
    private float _damage = 12f;
    private float _range = 380f;
    private float _projectileSpeed = 300f;
    private bool _piercing = true;
    private float _lifesteal;
    private float _contactDamage;
    private float _contactRehit = 0.6f;

    private float _timer;

    /// <summary>Cibles heurtées récemment, et quand — sinon le contact frappe à chaque image.</summary>
    private readonly Dictionary<EnemyBase, float> _contactCooldowns = new();

    /// <summary>Rayon de contact de la tourelle, en pixels.</summary>
    private const float ContactRadius = 16f;

    /// <summary>Tirs effectués — observable pour les vérifications.</summary>
    public int Shots { get; private set; }

    /// <summary>
    /// Installe la tourelle. Tous les paramètres viennent de <c>grafts.json</c>.
    /// </summary>
    /// <remarks>
    /// ⚠ Le portage lisait <c>fireIntervalSec</c> et <c>rangePx</c>, deux clés qui <b>n'existent pas</b>
    /// dans les données : la greffe tournait donc entièrement sur les valeurs par défaut du code, et
    /// tout ce que le fichier déclarait était ignoré en silence. Même famille que « une table parsée
    /// n'est pas une table branchée » — et invisible, puisque des valeurs plausibles sortaient quand
    /// même.
    /// </remarks>
    public void Configure(Player player, GraftTable.GraftDef def, int index, int count)
    {
        _player = player;
        _carrier = player.transform;

        _anchorAngle = index * Mathf.PI * 2f / Mathf.Max(1, count);

        _anchorRadius    = (float)def.Effect("turrets", "anchorRadiusPx", 90.0);
        _followSpeed     = (float)def.Effect("turrets", "followSpeedPx", 120.0);
        _cooldown        = (float)def.Effect("turrets", "cooldownSec", 1.0);
        _cooldownFloor   = (float)def.Effect("turrets", "cooldownFloorSec", 0.15);
        _followsCdr      = def.Effect("turrets", "affectedByCooldownReduction", 1) != 0;
        _damage          = (float)def.Effect("turrets", "damage", 12.0);
        _range           = (float)def.Effect("turrets", "targetRangePx", 380.0);
        _projectileSpeed = (float)def.Effect("turrets", "projectileSpeed", 300.0);
        _piercing        = def.Effect("turrets", "pierceCount", 1) >= 1;
        _lifesteal       = (float)def.Effect("turrets", "lifestealFraction", 0.04);
        _contactDamage   = (float)def.Effect("turrets", "contactDamage", 8.0);
        _contactRehit    = (float)def.Effect("turrets", "contactRehitIntervalSec", 0.6);

        if (def.Effect("turrets", "scalesWithDamageMultiplier", 1) != 0)
        {
            _damage        *= player.Stats.DamageMultiplier;
            _contactDamage *= player.Stats.DamageMultiplier;
        }

        _timer = EffectiveCooldown();

        transform.position = _carrier.position;

        BuildVisuals();
    }

    private void BuildVisuals()
    {
        // ⚠ Deux pièces, et une seule tourne. Le châssis porte l'ombrage pseudo-3D du projet, qui
        // suppose une lumière FIXE venue du haut-gauche : le faire pivoter emporterait sa lumière
        // avec lui et trahirait l'illusion à chaque changement de cible. Le canon, lui, est lumineux
        // — une émission n'a pas de face éclairée, elle peut donc viser librement.
        var body = new GameObject("Chassis", typeof(SpriteRenderer));
        body.transform.SetParent(transform, false);

        _sprite = body.GetComponent<SpriteRenderer>();
        _sprite.sprite = TurretSprite.Body;

        // ⚠ Le sprite est en PPU 1 : son côté vaut 32 unités, donc l'échelle est un RAPPORT et non
        // une taille. Poser 18 ici donnerait une tourelle de 576 px — la confusion qui a déjà produit
        // les langues de feu de 288 px et les drones géants.
        body.transform.localScale = Vector3.one * TurretScale;

        // Au-dessus de la faune (10) et du joueur : quatre alliés qui passent SOUS la nuée qu'ils
        // combattent seraient invisibles au moment précis où l'on veut les voir.
        _sprite.sortingOrder = 22;

        var barrelGo = new GameObject("Canon", typeof(SpriteRenderer));
        barrelGo.transform.SetParent(transform, false);
        barrelGo.transform.localScale = Vector3.one * TurretScale;

        _barrel = barrelGo.GetComponent<SpriteRenderer>();
        _barrel.sprite = TurretSprite.Barrel;
        _barrel.sortingOrder = 23;   // devant le châssis qui le porte

        // Lien d'ancrage : c'est lui qui rattache visuellement quatre objets épars à leur porteur.
        // Sans lui, on lit « des choses volent près de moi », pas « je porte une ruche ».
        var linkGo = new GameObject("Lien", typeof(LineRenderer));
        linkGo.transform.SetParent(transform, false);

        _link = linkGo.GetComponent<LineRenderer>();
        _link.positionCount = 2;
        _link.useWorldSpace = true;
        _link.widthMultiplier = 2f;
        _link.numCapVertices = 0;
        _link.sharedMaterial = VfxPrimitives.AdditiveBeam;
        _link.sortingOrder = 9;   // derrière les corps, devant le sol
        _link.startColor = _link.endColor = new Color(0.27f, 1f, 0.93f, 0.22f);
        _link.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private float EffectiveCooldown()
    {
        if (_player == null || !_followsCdr) return _cooldown;

        float reduction = Mathf.Min(_player.Stats.CooldownReduction, StatCaps.MaxCooldownReduction);
        return Mathf.Max(_cooldownFloor, _cooldown * (1f - reduction));
    }

    private void Update()
    {
        if (_carrier == null || _player == null) return;

        float dt = Time.deltaTime;

        Vector2 anchor = (Vector2)_carrier.position
                       + new Vector2(Mathf.Cos(_anchorAngle), Mathf.Sin(_anchorAngle)) * _anchorRadius;

        // Vitesse FINIE : la tourelle traîne quand le porteur court, et le rattrape quand il
        // s'arrête. Un placement instantané en ferait un élément d'interface collé au joueur.
        transform.position = Vector2.MoveTowards(transform.position, anchor, _followSpeed * dt);

        if (_link != null)
        {
            _link.SetPosition(0, _carrier.position);
            _link.SetPosition(1, transform.position);
        }

        var target = NearestEnemy(transform.position, _range);

        // Le CANON pointe sa cible même quand il ne peut pas tirer : c'est ce qui rend la couverture
        // de la tourelle lisible avant le tir, donc anticipable. Le châssis, lui, ne bouge jamais —
        // son ombrage suppose une lumière fixe.
        if (target != null && _barrel != null)
        {
            Vector2 aim = (Vector2)target.transform.position - (Vector2)transform.position;
            if (aim.sqrMagnitude > 0.01f)
                _barrel.transform.rotation =
                    Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
        }

        ApplyContact(dt);

        _timer -= dt;
        if (_timer > 0f || target == null) return;

        _timer = EffectiveCooldown();
        Fire(target);
    }

    private void Fire(EnemyBase target)
    {
        var prefab = ProjectilePrefabs.Bullet;
        if (prefab == null) return;

        Vector2 origin = transform.position;
        Vector2 dir = ((Vector2)target.transform.position - origin).normalized;

        var go = Instantiate(prefab, origin, Quaternion.identity);
        go.SetActive(true);

        var bullet = go.GetComponent<Bullet>();
        if (bullet == null) { Destroy(go); return; }

        bullet.Piercing = _piercing;
        bullet.Power = 3;
        bullet.SetTint(new Color(0.27f, 1f, 0.93f));

        // ⚠ Launch attend une VITESSE, pas une direction : un vecteur unitaire donnerait un
        // projectile avançant d'un pixel par seconde — visible, et parfaitement inoffensif.
        bullet.Launch(dir * _projectileSpeed, _damage, _range);

        Vfx.Muzzle(origin, dir);
        Shots++;

        // Le vol de vie se prend sur le tir et non sur le coup au but : le projectile survit à sa
        // tourelle, et un rappel vers un objet détruit serait perdu — la greffe promet ce vol, il ne
        // peut pas dépendre de qui est encore là dans deux secondes.
        if (_lifesteal > 0f) _player?.HealFlat(_damage * _lifesteal);
    }

    /// <summary>
    /// Dégâts de contact : la tourelle blesse ce qui la traverse, à intervalle borné.
    /// </summary>
    /// <remarks>
    /// ⚠ Sans le registre des cibles récemment heurtées, le contact frapperait <b>à chaque image</b>
    /// tant qu'un corps la chevauche, ce qui en ferait de très loin l'effet le plus fort du jeu.
    /// C'est la même parade que la liste des cibles déjà traversées d'un projectile perforant.
    /// </remarks>
    private void ApplyContact(float dt)
    {
        if (_contactDamage <= 0f) return;

        Vector2 me = transform.position;
        float sqr = ContactRadius * ContactRadius;

        foreach (var enemy in EnemyBase.Active.ToArray())
        {
            if (enemy == null || enemy.IsDead) continue;
            if (((Vector2)enemy.transform.position - me).sqrMagnitude > sqr) continue;

            if (_contactCooldowns.TryGetValue(enemy, out float left) && left > 0f) continue;

            _contactCooldowns[enemy] = _contactRehit;
            enemy.TakeDamage(_contactDamage);
            if (_lifesteal > 0f) _player?.HealFlat(_contactDamage * _lifesteal);
        }

        // Décompte et purge : le registre suivrait sinon des ennemis morts pendant toute la run.
        if (_contactCooldowns.Count == 0) return;

        _expired.Clear();

        foreach (var key in _contactCooldowns.Keys) _expiredKeys.Add(key);

        foreach (var key in _expiredKeys)
        {
            float left = _contactCooldowns[key] - dt;
            if (left <= 0f || key == null || key.IsDead) _expired.Add(key);
            else _contactCooldowns[key] = left;
        }

        foreach (var key in _expired) _contactCooldowns.Remove(key);
        _expiredKeys.Clear();
    }

    private readonly List<EnemyBase> _expired = new();
    private readonly List<EnemyBase> _expiredKeys = new();

    private static EnemyBase? NearestEnemy(Vector2 from, float range)
    {
        EnemyBase? best = null;
        float bestSqr = range * range;

        foreach (var enemy in EnemyBase.Active)
        {
            if (enemy == null || enemy.IsDead) continue;

            float sqr = ((Vector2)enemy.transform.position - from).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = enemy; }
        }

        return best;
    }
}
