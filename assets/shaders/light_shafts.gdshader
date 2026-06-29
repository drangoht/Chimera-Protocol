shader_type canvas_item;
render_mode blend_add, unshaded;

// Rais de lumière / god-rays : bandes diagonales douces, additives, teintées par biome.
// Parallaxe par cam_offset (parallax faible => les rais semblent loin derrière).
uniform vec4  shaft_color : source_color = vec4(1.0, 0.4, 0.9, 1.0);
uniform float strength    : hint_range(0.0, 0.6) = 0.18;
uniform vec2  cam_offset  = vec2(0.0);
uniform float parallax    : hint_range(0.0, 1.0) = 0.15;
uniform float angle       = 0.6;     // inclinaison des rais (radians)
uniform float freq        = 0.004;   // densité des bandes
uniform float speed       = 12.0;    // dérive temporelle
uniform float sharpness   = 6.0;     // finesse des bandes

void fragment() {
    vec2 w = FRAGCOORD.xy + cam_offset * parallax;
    float d = w.x * cos(angle) + w.y * sin(angle);
    float beams = sin(d * freq + TIME * 0.05 * speed);
    beams = pow(max(beams, 0.0), sharpness);
    float pulse = 0.7 + 0.3 * sin(TIME * 0.4);
    COLOR = vec4(shaft_color.rgb, beams * strength * pulse);
}
