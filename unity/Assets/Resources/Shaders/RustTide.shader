// La Marée de Rouille — nappe, front rongé, liseré, vagues et fumée, sur UN seul quad.
//
// Pourquoi un shader et pas des sprites : le premier rendu posait quatre rectangles pleins autour du
// rectangle sûr. Signalé en jouant le 2026-08-22, « la marée est un peu trop carrée, dans la vraie
// vie la rouille n'est pas nette comme ça » — et c'est structurel, pas une affaire de réglage. Une
// arête de sprite est droite par construction ; on peut la découper en segments, mais alors on
// compte les segments, exactement comme on compte les taches d'une brume faite de sprites doux
// (cf. docs/PITFALLS_UNITY.md, § brume). Un champ de distance évalué par pixel n'a ni segment ni
// tache, et son bord peut être aussi mangé qu'on veut sans qu'aucun objet ne bouge.
//
// ⚠ Le contour n'est PAS inventé ici. Il est calculé par la même formule que Shared/Rules/
// RustErosion.cs, qui décide des dégâts : voir la fonction Edge() plus bas, transcription littérale
// de RustErosion.EdgeAt. Un bord dessiné librement par-dessus une géométrie rectangulaire aurait été
// dix fois plus simple et aurait menti au joueur de 70 pixels sur la seule information que la marée
// donne. Toute retouche de la dentelure se fait DANS LES DEUX fichiers, sinon le liseré cesse d'être
// l'endroit où ça fait mal.
//
// ⚠ Il vit dans Resources/ et se charge par Resources.Load : un shader seulement atteint par
// Shader.Find peut être RETIRÉ du build par le nettoyage de shaders, et la marée deviendrait
// invisible dans le jeu exporté seulement — jamais dans l'éditeur, donc jamais pendant les tests.
Shader "Chimera/RustTide"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}

        _NappeColor ("Couleur de nappe", Color) = (0.58, 0.22, 0.09, 0.45)
        _RimColor   ("Couleur de liseré", Color) = (1.0, 0.48, 0.16, 1.0)
        _FrontColor ("Couleur de front",  Color) = (0.95, 0.42, 0.14, 0.26)
        _SmokeColor ("Couleur de fumée",  Color) = (0.72, 0.34, 0.16, 1.0)

        _SafeHalf   ("Demi-axes sûrs (px)", Vector) = (960, 608, 0, 0)
        _ArenaHalf  ("Demi-axes de l'arène (px)", Vector) = (960, 608, 0, 0)
        _TideTime   ("Horloge de la marée (s)", Float) = 0
        _WavePhase  ("Phase des vagues", Float) = 0
        _RimPulse   ("Pulsation du liseré", Range(0, 1)) = 1
        _Submersion ("Submersion (0-1)", Range(0, 1)) = 0
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
                float4 pos   : SV_POSITION;
                float4 color : COLOR;
                float2 world : TEXCOORD0;
            };

            fixed4 _NappeColor;
            fixed4 _RimColor;
            fixed4 _FrontColor;
            fixed4 _SmokeColor;
            float4 _SafeHalf;
            float4 _ArenaHalf;
            float  _TideTime;
            float  _WavePhase;
            float  _RimPulse;
            float  _Submersion;

            // ─── Constantes partagées avec RustErosion.cs ────────────────────────────────────────
            // Toute divergence se voit immédiatement : le liseré ne serait plus sur la limite des
            // dégâts. Elles sont écrites ici en clair plutôt que passées en uniformes pour que la
            // transcription soit relisible ligne à ligne face au C#.
            static const float TAU      = 6.28318530718;
            static const float AMP_PX   = 72.0;
            static const float MAX_SHARE = 0.5;
            static const float LAMBDA1 = 337.0, LAMBDA2 = 139.0, LAMBDA3 = 53.0;
            static const float SPEED1  = 0.035, SPEED2  = -0.061, SPEED3  = 0.092;
            static const float W1 = 0.50, W2 = 0.32, W3 = 0.18;

            // RustErosion.Bite01
            float Bite01(float side, float u, float t)
            {
                float phase = side * 0.377;

                float n = W1 * sin(TAU * (u / LAMBDA1 + t * SPEED1 + phase))
                        + W2 * sin(TAU * (u / LAMBDA2 + t * SPEED2 + phase * 2.0))
                        + W3 * sin(TAU * (u / LAMBDA3 + t * SPEED3 + phase * 3.0));

                return saturate(0.5 + 0.5 * n);
            }

            // RustErosion.EdgeAt — la morsure ne va QUE vers l'intérieur, ce qui est ce qui préserve
            // la garantie de fin de partie : à demi-axe nul, le bord érodé est nul lui aussi.
            //
            // ⚠ La troisième borne (arenaHalf - safeHalf) n'est pas un garde-fou : c'est ce qui fait
            // NAÎTRE la dentelure. La corrosion ne peut pas être plus profonde que ce qu'elle a déjà
            // mangé, sans quoi le bord de l'arène serait mordu de 72 px dès la première seconde
            // d'overtime — pendant la minute de grâce, où rien ne doit bouger.
            float Edge(float side, float u, float safeHalf, float arenaHalf, float t)
            {
                float safe  = max(safeHalf, 0.0);
                float eaten = max(arenaHalf - safe, 0.0);
                float amp   = min(AMP_PX, min(safe * MAX_SHARE, eaten));
                return max(0.0, safe - amp * Bite01(side, u, t));
            }

            // Distance signée au terrain sûr : négative dedans, positive dans la rouille, nulle sur
            // le liseré. Même décomposition par axe que RustTide.Depth — un coin enfonce sur deux
            // axes à la fois, et doit donc ronger plus fort qu'un milieu de bord.
            float SignedDepth(float2 p)
            {
                float ex = Edge(p.x >= 0.0 ? 0.0 : 1.0, p.y, _SafeHalf.x, _ArenaHalf.x, _TideTime);
                float ey = Edge(p.y >= 0.0 ? 2.0 : 3.0, p.x, _SafeHalf.y, _ArenaHalf.y, _TideTime);

                float dx = abs(p.x) - ex;
                float dy = abs(p.y) - ey;

                if (dx > 0.0 || dy > 0.0) return length(max(float2(dx, dy), 0.0));
                return max(dx, dy);
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

            // Trois octaves et pas quatre : ce quad couvre tout l'écran et le bruit y est échantillonné
            // deux fois (grain du bord, fumée). L'octave la plus fine du fbm de brume ne se lit pas
            // sur une nappe déjà texturée par sa dentelure — elle ne coûterait que du remplissage.
            float fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;

                for (int i = 0; i < 3; i++)
                {
                    v += a * noise(p);
                    p *= 2.0;
                    a *= 0.5;
                }

                return v;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.world = mul(unity_ObjectToWorld, v.vertex).xy;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.world;
                float sd = SignedDepth(p);

                // Sortie anticipée loin en terrain sûr. Ce n'est pas une micro-optimisation : le quad
                // couvre l'arène entière plus son débord, et sans elle chaque pixel du terrain sûr
                // paierait deux fbm pour rendre du vide.
                if (sd < -140.0 && _Submersion <= 0.0) return fixed4(0, 0, 0, 0);

                // ─── Grain du bord ───────────────────────────────────────────────────────────────
                // Le bord reste EXACTEMENT à sd = 0 — c'est la limite des dégâts, elle ne se négocie
                // pas. Ce que le bruit module, c'est la DISTANCE sur laquelle la nappe monte à pleine
                // opacité : de 6 px là où la rouille mord franchement à 52 là où elle s'étale. Un
                // fondu de largeur variable se lit comme un bord déchiqueté sans qu'un seul pixel de
                // teinte ne déborde sur le terrain encore sûr.
                float grain = fbm(p * 0.011 + float2(_TideTime * 0.013, -_TideTime * 0.009));
                float feather = 6.0 + 46.0 * grain;
                float nappe = smoothstep(0.0, feather, sd);

                // Texture de rouille dans la nappe : plaques et piqûres, immobiles dans le monde, qui
                // donnent à la surface une matière au lieu d'un aplat translucide.
                float plaque = fbm(p * 0.024 + 17.0);
                nappe *= 0.72 + 0.42 * plaque;

                // ─── Vagues ──────────────────────────────────────────────────────────────────────
                // Elles courent vers l'intérieur bien plus vite que le bord ne recule (~110 u/s contre
                // 1,6) : c'est ce découplage qui rend la progression perceptible, le bord avançant
                // d'un pixel toutes les 0,8 s. La phase est ACCUMULÉE côté C# et passée en uniforme —
                // jamais recalculée depuis une horloge divisée par une profondeur qui grandit
                // (cf. TideWaves).
                float band = frac(sd / 210.0 + _WavePhase);
                float wave = smoothstep(0.0, 0.35, band) * smoothstep(1.0, 0.65, band);
                nappe += 0.34 * wave * step(0.0, sd);

                // ─── Fumée ───────────────────────────────────────────────────────────────────────
                // Volutes qui montent de la nappe et lèchent le front. Elles dérivent vers l'intérieur
                // (donc dans le sens de l'avancée) et se déforment sur place.
                //
                // ⚠ Elles débordent volontairement de quelques dizaines de pixels au-dessus du terrain
                // sûr, seule chose du rendu qui le fasse. C'est le prix de « pas net » : une nappe qui
                // s'arrête pile sur sa limite EST une arête, quelle que soit la façon dont on la
                // dessine. Le débord est ténu (opacité plafonnée bien en dessous du liseré) et sans
                // couleur vive, de sorte qu'il ne peut pas se confondre avec le liseré, qui reste la
                // seule marque nette du rendu et le seul endroit où l'on commence à brûler.
                float2 drift = float2(_TideTime * 3.1, _TideTime * 2.3);
                float smokeN = fbm(p * 0.0075 - drift * 0.006 + fbm(p * 0.02 + drift * 0.01) * 0.9);
                float smokeBand = smoothstep(-90.0, 40.0, sd) * smoothstep(420.0, 120.0, sd);
                float smoke = smokeBand * smoothstep(0.42, 0.86, smokeN) * 0.34;

                // ─── Front et liseré ─────────────────────────────────────────────────────────────
                // Le halo de front donne au bord une épaisseur diffuse ; le liseré, lui, est la seule
                // arête assumée du rendu — « à partir d'ici ça fait mal » doit se lire d'un coup d'œil
                // au milieu d'une nuée. Il pulse (uniforme calculé en temps NON mis à l'échelle : un
                // repère de danger qui se fige pendant un ralenti se lit comme éteint).
                float front = exp(-sd * sd / (2.0 * 46.0 * 46.0)) * step(-12.0, sd);
                float rim   = exp(-sd * sd / (2.0 * 7.5 * 7.5));

                // ─── Composition ─────────────────────────────────────────────────────────────────
                float3 rgb = _NappeColor.rgb;
                float  a   = nappe * _NappeColor.a;

                rgb = lerp(rgb, _SmokeColor.rgb, saturate(smoke / max(a + smoke, 0.0001)));
                a += smoke;

                rgb = lerp(rgb, _FrontColor.rgb, saturate(front * 0.8));
                a += front * _FrontColor.a;

                rgb = lerp(rgb, _RimColor.rgb, saturate(rim * 1.2));
                a += rim * _RimPulse;

                // Submersion : passé la fermeture il n'y a plus de terrain sûr du tout, et la teinte
                // doit le dire partout — y compris au centre exact, le point que la géométrie seule
                // laisserait sain (cf. RustTide.FloorFractionPerSecond, qui existe pour ce trou-là).
                a = lerp(a, max(a, 0.55 + 0.2 * plaque), _Submersion);

                return fixed4(rgb, saturate(a) * i.color.a);
            }
            ENDCG
        }
    }

    Fallback Off
}
