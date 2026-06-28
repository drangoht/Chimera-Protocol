shader_type canvas_item;

uniform float strength : hint_range(0.0, 10.0) = 4.0; // anime 0->4->0 par code
// repeat_disable = clamp-to-edge : sans lui, l'echantillonnage SCREEN_UV +/- offset
// hors [0,1] aux bords "wrap" sur le bord oppose -> liseré clignotant sur les bords.
uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, repeat_disable, filter_linear_mipmap;

void fragment() {
    vec2 offset = vec2(strength / 1280.0, 0.0); // en UV normalise

    // Clamp explicite par securite (en plus du repeat_disable).
    vec2 uv_r = clamp(SCREEN_UV + offset, vec2(0.0), vec2(1.0));
    vec2 uv_b = clamp(SCREEN_UV - offset, vec2(0.0), vec2(1.0));

    float r = texture(SCREEN_TEXTURE, uv_r).r;
    float g = texture(SCREEN_TEXTURE, SCREEN_UV).g;
    float b = texture(SCREEN_TEXTURE, uv_b).b;

    COLOR = vec4(r, g, b, 1.0);
}
