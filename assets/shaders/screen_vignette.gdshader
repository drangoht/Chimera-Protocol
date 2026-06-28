shader_type canvas_item;

uniform float inner_radius : hint_range(0.2, 0.8) = 0.45;
uniform float feather : hint_range(0.1, 0.5) = 0.30;
uniform float max_opacity : hint_range(0.0, 1.0) = 0.72;
uniform vec4 vignette_color = vec4(0.0, 0.0, 0.02, 1.0); // #000005
uniform vec2 center = vec2(0.5, 0.5); // mis a jour par VignetteFollow.cs

void fragment() {
    // Distance normalisee depuis la position joueur a l'ecran
    vec2 centered = UV - center;
    // Corriger l'aspect ratio (viewport 1280x720 = 16:9)
    centered.x *= 1.777; // 16/9
    float dist = length(centered) * 1.414; // normalise vers 1.0 au coin

    float vignette = smoothstep(inner_radius, inner_radius + feather, dist);
    vignette *= max_opacity;

    COLOR = vignette_color;
    COLOR.a = vignette;
}
