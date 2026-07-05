shader_type canvas_item;

// Rendu « gelé » d'un ennemi : recolore la texture vers un bleu glacé selon `frost` (0 = normal,
// 1 = givré). Contrairement à un multiply sur SelfModulate — qui ne peut qu'ASSOMBRIR, jamais ajouter
// du bleu absent d'un sprite chaud (orange) — ce shader LERPE la couleur du pixel vers le bleu cible,
// donc un ennemi orange lit bien « bleu » une fois gelé.
//
// CRITIQUE : sous le batching 2D de Godot, le modulate du nœud (HitFlash) et le self_modulate (teinte
// d'élite) sont bakés dans le COLOR ENTRANT du fragment (pas de ré-application automatique après un
// fragment custom). On reproduit donc le fragment par défaut « COLOR = texture * COLOR » en remplaçant
// juste la texture par sa version givrée → HitFlash et teinte d'élite restent composés par-dessus.
// À frost = 0, mix(tex, .., 0) = tex → strictement identique à l'absence de shader.
uniform float frost       : hint_range(0.0, 1.0) = 0.0;
uniform vec4  frost_color : source_color = vec4(0.42, 0.66, 1.15, 1.0);

void fragment() {
    vec4 tex = texture(TEXTURE, UV);
    // Luminance de la texture → conserve le relief pseudo-3D (ombres/highlights) en teintant.
    float lum = dot(tex.rgb, vec3(0.299, 0.587, 0.114));
    vec3 frosted = frost_color.rgb * (0.35 + 0.75 * lum);
    COLOR = vec4(mix(tex.rgb, frosted, frost), tex.a) * COLOR;
}
