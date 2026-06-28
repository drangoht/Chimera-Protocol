shader_type canvas_item;

uniform float progress : hint_range(0.0, 1.0) = 0.0; // anime par code 0->1 en 0.4s
uniform float ring_width : hint_range(0.01, 0.15) = 0.06;
uniform vec4 ring_color = vec4(0.267, 1.0, 0.933, 1.0); // #44FFEE

void fragment() {
    vec2 centered = UV - vec2(0.5);
    float dist = length(centered) * 2.0; // 0 au centre, 1 au bord

    float outer = progress;
    float inner = max(0.0, progress - ring_width);

    float ring = step(inner, dist) * step(dist, outer);
    float fade = 1.0 - progress; // s'estompe en s'expandant

    COLOR = ring_color;
    COLOR.a = ring * fade * 0.85;
}
