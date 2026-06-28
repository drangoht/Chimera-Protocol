using Godot;
using System.Collections.Generic;

/// <summary>
/// Essaim de Drones — N drones en orbite autour du joueur.
/// Chaque drone est une Area2D ; la collision avec un ennemi déclenche les dégâts.
/// </summary>
public partial class DroneSwarm : WeaponBase
{
    public int   DroneCount     { get; set; } = 2;
    public float OrbitSpeedDeg  { get; set; } = 120f;
    public float DamageInterval { get; set; } = 0.5f;
    public float OrbitRadius    { get; set; } = 70f;

    private float _orbitAngle = 0f;
    private readonly List<DroneEntity> _drones = new();

    private static PackedScene? _droneScene;

    public override void _Ready()
    {
        Damage        = 12f;
        Cooldown      = 999f; // Pas de cooldown global — les drones gèrent leurs propres timers
        DroneCount    = 2;
        OrbitSpeedDeg = 120f;
        DamageInterval = 0.5f;
        OrbitRadius   = 70f;

        _droneScene ??= GD.Load<PackedScene>("res://scenes/weapons/DroneEntity.tscn");
        RebuildDrones();
        base._Ready();
    }

    public override void _Process(double delta)
    {
        _orbitAngle += OrbitSpeedDeg * (float)delta * Mathf.Pi / 180f;

        var player = GameManager.Instance.PlayerInstance;
        if (player != null)
        {
            // Synchronise le nombre de drones si DroneCount a changé
            if (_drones.Count != DroneCount)
                RebuildDrones();

            for (int i = 0; i < _drones.Count; i++)
            {
                if (!IsInstanceValid(_drones[i])) continue;
                float angle = _orbitAngle + i * (2f * Mathf.Pi / _drones.Count);
                _drones[i].GlobalPosition = player.GlobalPosition + Vector2.Right.Rotated(angle) * OrbitRadius;
                _drones[i].Damage         = Damage;
                _drones[i].DamageInterval = DamageInterval;
            }
        }

        // Appel base pour le timer WeaponBase (cooldown fictif 999s, Attack() est vide)
        base._Process(delta);
    }

    // WeaponBase._Process appelle Attack() ; on ne l'utilise pas ici (cooldown fictif très grand)
    protected override void Attack() { }

    private void RebuildDrones()
    {
        // Nettoie les anciens drones
        foreach (var d in _drones)
            if (IsInstanceValid(d)) d.QueueFree();
        _drones.Clear();

        if (_droneScene == null) return;

        for (int i = 0; i < DroneCount; i++)
        {
            var drone = _droneScene.Instantiate<DroneEntity>();
            AddChild(drone);
            drone.Damage        = Damage;
            drone.DamageInterval = DamageInterval;
            _drones.Add(drone);
        }
    }

    public override void _ExitTree()
    {
        foreach (var d in _drones)
            if (IsInstanceValid(d)) d.QueueFree();
    }
}
