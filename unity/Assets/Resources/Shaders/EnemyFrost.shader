// Silhouette « gelée » d'un ennemi — portage de `assets/shaders/enemy_frost.gdshader`.
//
// Il MÉLANGE la couleur du pixel VERS un bleu glacé selon `_Frost` (0 = normal, 1 = givré).
// C'est la seule opération qui convienne, et le portage l'a appris deux fois :
//   · une teinte sur `SpriteRenderer.color` MULTIPLIE, donc ne peut qu'assombrir — un ennemi rouge
//     gelé restait rouge en plus sombre, ce qui se lit « il est dans l'ombre » ;
//   · un calque ADDITIF par-dessus ajoute du bleu à du rouge et donne du ROSE délavé, pas du givre.
// Remplacer la couleur est donc nécessaire, et cela demande un shader.
//
// La luminance de la texture est conservée dans le mélange : c'est elle qui porte le relief
// pseudo-3D (ombres et éclats) de tous les sprites du jeu. La perdre aplatirait l'ennemi en
// silhouette bleue unie.
//
// La couleur du sommet — clignotement de dégât, teinte d'élite — est appliquée APRÈS le mélange,
// exactement comme le fragment par défaut de Godot : sans cela, un ennemi gelé et frappé perdrait
// son flash blanc, seul retour qui dit « le coup a porté ».
//
// Il vit dans Resources/ et se charge par Resources.Load : un shader seulement atteint par
// Shader.Find peut être RETIRÉ du build par le nettoyage de shaders, et l'effet disparaîtrait
// uniquement dans le jeu exporté — jamais dans l'éditeur, donc jamais pendant les tests.
Shader "Chimera/EnemyFrost"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color      ("Teinte", Color) = (1,1,1,1)
        _Frost      ("Givre", Range(0, 1)) = 0
        _FrostColor ("Couleur de givre", Color) = (0.42, 0.66, 1.15, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue"            = "Transparent"
            "RenderType"       = "Transparent"
            "IgnoreProjector"  = "True"
            "PreviewType"      = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Lighting Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color       : COLOR;
                float2 uv          : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _FrostColor;
                float  _Frost;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color       = IN.color * _Color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                float lum = dot(tex.rgb, half3(0.299, 0.587, 0.114));
                half3 frosted = _FrostColor.rgb * (0.35 + 0.75 * lum);

                return half4(lerp(tex.rgb, frosted, _Frost), tex.a) * IN.color;
            }
            ENDHLSL
        }
    }

    // Repli : si le mélange ne compile pas sur une machine, l'ennemi reste VISIBLE, simplement non
    // givré. Un état non signalé vaut infiniment mieux qu'un ennemi invisible.
    Fallback "Sprites/Default"
}
