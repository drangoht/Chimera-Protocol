using Godot;

/// <summary>
/// Essaim Orbital — fusion de l'Essaim de Drones + Servo-Moteurs.
/// Réutilise toute la mécanique de <see cref="DroneSwarm"/> mais avec un essaim
/// dense et fulgurant : 6 drones, orbite très rapide, rayon élargi, dégâts soutenus.
/// Le boost est posé APRÈS base._Ready() (qui réinitialise les stats par défaut) ;
/// _Process resynchronise alors le nombre de drones et leurs dégâts à la frame suivante.
/// </summary>
public partial class OrbitalSwarm : DroneSwarm
{
    public override void _Ready()
    {
        base._Ready();          // pose les valeurs de base + instancie 2 drones
        DroneCount     = 6;     // _Process détecte l'écart et reconstruit l'essaim
        OrbitSpeedDeg  = 280f;  // tourbillon défensif rapide
        OrbitRadius    = 95f;   // couverture plus large
        Damage         = 24f;
        DamageInterval = 0.22f; // dégâts de contact quasi continus
    }
}
