// Rais de lumière (god-rays) — portage de `assets/shaders/light_shafts.gdshader`.
//
// Bandes diagonales douces, ADDITIVES, teintées par l'accent du biome, dérivant lentement et
// respirant sur une pulsation longue. Leur parallaxe est plus faible que celle de la brume (0,15
// contre 0,35) : les rais se lisent comme venant de très loin derrière, la brume comme flottant
// juste au-dessus du sol. C'est cet ÉCART entre les deux couches qui donne l'épaisseur — deux
// couches au même facteur ne feraient qu'un seul voile plus dense.
//
// Blend additif et non alpha : de la lumière s'ajoute, elle ne recouvre pas. En mélange normal, une
// bande sur un sol sombre l'ÉCLAIRCIRAIT vers sa propre couleur en aplat, ce qui se lit comme de la
// peinture et non comme un faisceau.
Shader "Chimera/AtmosphereShafts"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _ShaftColor ("Couleur des rais", Color) = (1, 0.4, 0.9, 1)
        _Strength   ("Force", Range(0, 0.6)) = 0.18
        _Angle      ("Inclinaison (rad)", Float) = 0.6
        _Freq       ("Densité des bandes", Float) = 0.004
        _Speed      ("Dérive", Float) = 12.0
        _Sharpness  ("Finesse", Float) = 6.0
        _Parallax   ("Parallaxe", Range(0, 1)) = 0.15
        _CamOffset  ("Décalage caméra", Vector) = (0, 0, 0, 0)
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
        Blend SrcAlpha One

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

            fixed4 _ShaftColor;
            float  _Strength;
            float  _Angle;
            float  _Freq;
            float  _Speed;
            float  _Sharpness;
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

            fixed4 frag(v2f i) : SV_Target
            {
                float2 fragCoord = i.screen.xy / max(i.screen.w, 0.0001) * _ScreenParams.xy;
                float2 w = fragCoord + _CamOffset.xy * _Parallax;

                float d = w.x * cos(_Angle) + w.y * sin(_Angle);
                float beams = sin(d * _Freq + _Time.y * 0.05 * _Speed);
                beams = pow(max(beams, 0.0), _Sharpness);

                float pulse = 0.7 + 0.3 * sin(_Time.y * 0.4);

                return fixed4(_ShaftColor.rgb, beams * _Strength * pulse * i.color.a);
            }
            ENDCG
        }
    }

    Fallback Off
}
