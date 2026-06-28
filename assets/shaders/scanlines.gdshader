shader_type canvas_item;

// Scanlines CRT subtiles (écran-espace) + très léger vignettage des bords.
uniform float strength  : hint_range(0.0, 0.25) = 0.06;
uniform float vignette  : hint_range(0.0, 0.6)  = 0.18;

void fragment() {
    // Lignes horizontales douces (période ~2 px via FRAGCOORD).
    float scan = strength * (0.5 + 0.5 * sin(FRAGCOORD.y * 3.14159));

    // Vignette discrète aux bords.
    vec2 d = UV - vec2(0.5);
    float vig = vignette * dot(d, d) * 1.4;

    COLOR = vec4(0.0, 0.0, 0.0, clamp(scan + vig, 0.0, 0.6));
}
