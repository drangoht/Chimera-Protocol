using Godot;
using System.Collections.Generic;

/// <summary>
/// Indicateur HUD des power-ups temporaires actifs : une puce colorée par buff (nom + barre de
/// décompte qui se vide). Placée sous le cluster haut-gauche du HUD. Mise à jour par le Player.
/// </summary>
public partial class BuffBar : CanvasLayer
{
    private readonly Dictionary<PowerUpType, (Panel chip, ColorRect bar, Label label)> _chips = new();
    private VBoxContainer _root = null!;

    public override void _Ready()
    {
        Layer = 96;   // au-dessus du HUD (95)
        _root = new VBoxContainer { Name = "Buffs" };
        _root.AddThemeConstantOverride("separation", 4);
        _root.AnchorLeft = 0f; _root.AnchorTop = 0f;
        _root.OffsetLeft = 28; _root.OffsetTop = 96;
        AddChild(_root);

        foreach (var def in PowerUps.All)
            _chips[def.Type] = BuildChip(def);
    }

    private (Panel, ColorRect, Label) BuildChip(PowerUps.Def def)
    {
        var panel = new Panel { CustomMinimumSize = new Vector2(150, 26), Visible = false };
        var style = new StyleBoxFlat { BgColor = new Color(0.06f, 0.07f, 0.12f, 0.9f) };
        style.SetBorderWidthAll(2); style.BorderColor = def.Color; style.SetCornerRadiusAll(4);
        panel.AddThemeStyleboxOverride("panel", style);
        _root.AddChild(panel);

        // Barre de décompte (se vide)
        var bar = new ColorRect { Color = new Color(def.Color, 0.30f), MouseFilter = Control.MouseFilterEnum.Ignore };
        bar.AnchorLeft = 0; bar.AnchorTop = 0; bar.AnchorBottom = 1; bar.OffsetRight = 150;
        panel.AddChild(bar);

        var label = new Label { Text = Loc.T(def.NameKey), MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", def.Color);
        label.AnchorLeft = 0; label.OffsetLeft = 8; label.OffsetTop = 4;
        panel.AddChild(label);

        return (panel, bar, label);
    }

    /// <summary>Met à jour l'affichage depuis les temps restants par type de buff.</summary>
    public void UpdateBuffs(Dictionary<PowerUpType, float> remaining)
    {
        foreach (var def in PowerUps.All)
        {
            var (chip, bar, _) = _chips[def.Type];
            if (remaining.TryGetValue(def.Type, out float t) && t > 0f)
            {
                chip.Visible = true;
                float ratio = Mathf.Clamp(t / def.Duration, 0f, 1f);
                bar.OffsetRight = 150f * ratio;
            }
            else if (chip.Visible)
            {
                chip.Visible = false;
            }
        }
    }
}
