shader_type canvas_item;
render_mode unshaded;

// Fond parallax "sous l'arene" : reechantillonne une tuile transparente en boucle
// (mod sur FRAGCOORD) et la teinte par l'accent du biome. Independant de la taille
// du polygone porteur : le motif se repete a l'infini tant que cam_offset suit la camera.
// Parallax bas (< couches poussiere/brume) => lecture "loin derriere" les murs.
uniform sampler2D tex : filter_nearest, repeat_enable;
uniform vec2  tile_size = vec2(64.0, 64.0);
uniform vec2  cam_offset = vec2(0.0);
uniform float parallax   : hint_range(0.0, 1.0) = 0.22;
uniform vec4  tint       : source_color = vec4(0.6, 0.7, 0.9, 1.0);
uniform float alpha_mult : hint_range(0.0, 2.0) = 1.0;

void fragment() {
    vec2 w  = FRAGCOORD.xy + cam_offset * parallax;
    vec2 uv = mod(w, tile_size) / tile_size;
    vec4 c  = texture(tex, uv);
    COLOR = vec4(tint.rgb, c.a * tint.a * alpha_mult);
}
