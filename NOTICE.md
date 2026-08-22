# NOTICE — statut juridique du dépôt

> Ce dépôt est **public**. Ce fichier dit ce qui s'y trouve, à qui cela appartient, et **ce qu'il ne
> faut pas faire**. Il existe parce que la réponse à « peut-on committer les musiques ? » ne dépend
> pas du commit, mais de la licence sous laquelle le dépôt les offre.

## Le dépôt n'a pas de licence open source, et c'est délibéré

Il n'y a **pas de fichier `LICENSE`**. En droit d'auteur, un dépôt public sans licence est **« tous
droits réservés »** : le publier sur GitHub n'accorde que ce que les conditions de GitHub prévoient
(consulter le code, le forker sur la plateforme). Personne ne reçoit de droit d'usage, de
modification ou de redistribution.

C'est exactement ce qu'il faut ici, parce que **le dépôt contient des fichiers dont l'auteur du jeu
ne détient pas les droits** (voir le tableau). Tant qu'aucune licence n'est déclarée, rien n'est
sur-licencié.

## ⚠ NE PAS ajouter de licence permissive en l'état

Ajouter un `LICENSE` MIT, Apache ou BSD « pour faire propre » accorderait à des tiers un droit
d'usage **commercial** sur l'ensemble du dépôt — donc sur les 14 pistes musicales, qui sont
réservées à un usage **non commercial** et dont la propriété reste à Suno. Ce serait le seul faux
pas réellement dangereux de ce dossier, et il se commet en trois secondes avec les meilleures
intentions.

Si le code doit être ouvert un jour, la forme correcte est : une licence **portant explicitement sur
le code seul**, plus une clause d'exclusion nommant les répertoires d'assets ci-dessous. Jamais une
licence posée à la racine sans réserve.

## Ce que contient le dépôt

| Contenu | Origine | Licence / statut | Emplacement |
|---|---|---|---|
| Code, design, textes, sprites, VFX | Projet | Propriété de l'auteur — tous droits réservés | `unity/Assets/Scripts`, `unity/Assets/Art`, `docs/`, `tools/` |
| **14 pistes musicales** | **Suno, plan gratuit** (2026-07-27) | ⚠ **Usage NON COMMERCIAL.** Les droits commerciaux dépendent du plan actif **au moment de la génération** ; le plan gratuit ne les accorde pas, et Suno conserve la propriété des morceaux. | `unity/Assets/Resources/Audio/music/` (sauf les 3 stingers) |
| 3 stingers (`death`, `victory`, `levelup`) | Synthétisés par le dépôt (`tools/generate_music_v3.py`) | Propriété du projet, aucune contrainte | `unity/Assets/Resources/Audio/music/music_stinger_*.ogg` |
| 25 SFX | Packs Kenney.nl | **CC0 1.0** — domaine public, usage commercial libre, attribution facultative | `unity/Assets/Resources/Audio/sfx/` |
| Sources Kenney brutes (411 `.ogg`) | Packs Kenney.nl | **CC0 1.0** | `tools/kenney_downloads/extracted/` |
| Share Tech Mono | Carrois Type Design (Ralph du Carrois) | **SIL OFL 1.1** | `unity/Assets/Resources/Fonts/ShareTechMono.ttf` |
| VT323 | The VT323 Project Authors (Peter Hull) | **SIL OFL 1.1** | `unity/Assets/Resources/Fonts/VT323.ttf` |

Détail et historique : `docs/AUDIO_CREDITS.md` · `docs/FONTS_CREDITS.md`.

## Pourquoi la distribution actuelle est cohérente

Chimera Protocol est publié **gratuitement** sur itch.io : aucun prix, aucun paiement requis, aucune
publicité. C'est ce qui rend l'usage des pistes Suno conforme à la réserve « non commercial », et
c'est la base sur laquelle la 1.17.0 a été publiée.

## Ce qui déclencherait une action

Trois situations, et une seule réponse valable pour chacune :

1. **Le jeu devient payant** (prix, dons contre le jeu, clé Steam payante, bundle payant) →
   regénérer les 14 pistes sous un plan Suno **payant**, ou basculer sur la bande-son de secours.
   ⚠ **Un abonnement pris aujourd'hui ne couvre pas rétroactivement** les pistes générées en juillet
   2026 : c'est le plan actif **au moment de la génération** qui décide. Il faut les **regénérer**.
2. **Monétisation vidéo** (trailer YouTube monétisé, partenariat) → même réserve, même réponse.
3. **Ouverture du code** → licence portant sur le code seul + exclusion nommée des assets ci-dessus.

**Sortie de secours, disponible immédiatement** : `python tools/generate_music_v3.py` regénère une
bande-son complète synthétisée par le dépôt, propriété du projet, sans aucune contrainte de licence.
Elle a déjà servi de bande-son au jeu et reste regénérable à l'identique.

---

*Ce document n'est pas un avis juridique. Sur un point engageant, lire les conditions d'utilisation
de Suno en vigueur.*
