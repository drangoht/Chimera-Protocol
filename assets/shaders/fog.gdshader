shader_type canvas_item;
render_mode unshaded;

// Brume atmosphérique : bruit fbm animé, teinté par biome.
// Parallaxe : échantillonne à FRAGCOORD + cam_offset * parallax (parallax < 1 => la brume
// se déplace plus lentement que le sol => impression de profondeur).
uniform vec4  fog_color : source_color = vec4(0.5, 0.6, 0.8, 1.0);
uniform float strength  : hint_range(0.0, 0.5) = 0.12;
uniform float scale     = 0.0016;
uniform vec2  cam_offset = vec2(0.0);
uniform float parallax  : hint_range(0.0, 1.0) = 0.35;
uniform float speed     = 6.0;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }

float noise(vec2 p) {
    vec2 i = floor(p), f = fract(p);
    float a = hash(i), b = hash(i + vec2(1.0, 0.0));
    float c = hash(i + vec2(0.0, 1.0)), d = hash(i + vec2(1.0, 1.0));
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

float fbm(vec2 p) {
    float v = 0.0, a = 0.5;
    for (int i = 0; i < 4; i++) { v += a * noise(p); p *= 2.0; a *= 0.5; }
    return v;
}

void fragment() {
    vec2 w = FRAGCOORD.xy + cam_offset * parallax;
    vec2 p = w * scale + vec2(TIME * 0.010 * speed, TIME * 0.006 * speed);
    float n = fbm(p);
    n = smoothstep(0.40, 0.95, n);
    COLOR = vec4(fog_color.rgb, n * strength);
}
