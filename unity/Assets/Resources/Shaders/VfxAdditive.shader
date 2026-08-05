// Mélange ADDITIF pour les effets — l'équivalent Unity du PointLight2D en BlendMode.Add de Godot.
//
// Pourquoi un shader et pas un simple sprite : tout le vocabulaire visuel du jeu repose sur des
// lueurs qui S'ADDITIONNENT (un faisceau qui croise un flash d'impact doit blanchir, pas se
// substituer). En mélange alpha ordinaire, deux effets superposés donnent le plus proche, et la
// scène perd exactement ce qui la rendait « énergétique » sous Godot.
//
// Il vit dans Resources/ et se charge par Resources.Load : un shader seulement atteint par
// Shader.Find peut être RETIRÉ du build par le nettoyage de shaders, et l'effet disparaîtrait
// uniquement dans le jeu exporté — jamais dans l'éditeur, donc jamais pendant les tests.
Shader "Chimera/VfxAdditive"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Teinte", Color) = (1,1,1,1)
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

        // src.rgb * src.a + dst : le noir est neutre, donc la texture n'a pas besoin d'être découpée.
        Blend SrcAlpha One
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
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);

                // La couleur vient du sommet : c'est le canal commun au SpriteRenderer (color), au
                // LineRenderer (dégradé) et au ParticleSystem (startColor). Un seul shader suffit
                // donc aux trois familles d'effets.
                OUT.color = IN.color * _Color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color;
            }
            ENDHLSL
        }
    }

    // Repli : si l'additif ne compile pas sur une machine, les effets restent VISIBLES en mélange
    // alpha plutôt que roses. Un effet moins beau vaut mieux qu'un effet manquant.
    Fallback "Sprites/Default"
}
