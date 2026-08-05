// Brume atmosphérique — portage de `assets/shaders/fog.gdshader`.
//
// Bruit fbm animé, teinté par biome, échantillonné à la position ÉCRAN décalée par la caméra :
// c'est ce décalage partiel (parallax < 1) qui fait que la brume se déplace plus lentement que le
// sol, donc qu'elle se lit comme une couche derrière lui plutôt que comme un voile collé à
// l'objectif.
//
// Pourquoi un shader et pas des sprites : une brume faite de sprites doux se trahit par ses bords —
// on compte les taches. Le bruit procédural n'a pas de bord, et il s'anime sans qu'aucun objet ne
// bouge, ce qui est exactement ce qu'on demande à une brume.
//
// Il vit dans Resources/ et se charge par Resources.Load : un shader seulement atteint par
// Shader.Find peut être RETIRÉ du build par le nettoyage de shaders, et l'effet disparaîtrait
// uniquement dans le jeu exporté — jamais dans l'éditeur, donc jamais pendant les tests.
Shader "Chimera/AtmosphereFog"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _FogColor  ("Couleur de brume", Color) = (0.5, 0.6, 0.8, 1)
        _Strength  ("Force", Range(0, 0.6)) = 0.12
        _Scale     ("Échelle du bruit", Float) = 0.0016
        _Speed     ("Vitesse", Float) = 6.0
        _Parallax  ("Parallaxe", Range(0, 1)) = 0.35
        _CamOffset ("Décalage caméra", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "RenderType"        = "Transparent"
            "IgnoreProjector"   = "True"
            "PreviewType"       = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos    : SV_POSITION;
                float4 color  : COLOR;
                float4 screen : TEXCOORD0;
            };

            fixed4 _FogColor;
            float  _Strength;
            float  _Scale;
            float  _Speed;
            float  _Parallax;
            float4 _CamOffset;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.screen = ComputeScreenPos(o.pos);
                return o;
            }

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));

                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;

                for (int i = 0; i < 4; i++)
                {
                    v += a * noise(p);
                    p *= 2.0;
                    a *= 0.5;
                }

                return v;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Position en pixels d'écran — l'équivalent de FRAGCOORD sous Godot.
                float2 fragCoord = i.screen.xy / max(i.screen.w, 0.0001) * _ScreenParams.xy;

                float2 w = fragCoord + _CamOffset.xy * _Parallax;
                float2 p = w * _Scale + float2(_Time.y * 0.010 * _Speed, _Time.y * 0.006 * _Speed);

                float n = fbm(p);
                n = smoothstep(0.40, 0.95, n);

                return fixed4(_FogColor.rgb, n * _Strength * i.color.a);
            }
            ENDCG
        }
    }

    Fallback Off
}
