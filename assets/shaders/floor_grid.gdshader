shader_type canvas_item;

uniform float grid_size : hint_range(16.0, 64.0) = 32.0;
uniform float line_width : hint_range(0.5, 2.0) = 1.0;
uniform vec4 line_color = vec4(0.0, 0.627, 0.733, 1.0); // #00A0BB
uniform float opacity : hint_range(0.0, 0.15) = 0.06;
uniform float scroll_speed : hint_range(0.0, 5.0) = 0.5;

void fragment() {
    vec4 base = texture(TEXTURE, UV) * COLOR;

    // Coordonnees monde via FRAGCOORD pour eviter les artefacts UV sur Polygon2D sans texture
    vec2 world_px = FRAGCOORD.xy;
    vec2 scrolled = world_px + vec2(TIME * scroll_speed);

    // Grille orthogonale
    vec2 grid = mod(scrolled, grid_size);
    float line_h = step(grid_size - line_width, grid.x);
    float line_v = step(grid_size - line_width, grid.y);
    float grid_mask = max(line_h, line_v);

    // Grille diagonale secondaire (45 degres), opacite moitie
    vec2 diag = mod(scrolled * 0.707 + scrolled.yx * 0.707, grid_size);
    float diag_mask = max(
        step(grid_size - line_width * 0.8, diag.x),
        step(grid_size - line_width * 0.8, diag.y)
    ) * 0.4;

    float total_mask = clamp(grid_mask + diag_mask, 0.0, 1.0);

    vec4 grid_contrib = line_color * total_mask * opacity;
    COLOR = base + grid_contrib;
}
