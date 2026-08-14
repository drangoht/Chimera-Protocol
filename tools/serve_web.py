"""Sert le build web en local, SANS cache navigateur.

Pourquoi cet outil plutôt que `python -m http.server`
-----------------------------------------------------
Les fichiers de sortie WebGL d'Unity portent toujours le même nom d'un build à l'autre
(`web.wasm.unityweb`, `web.data.unityweb`). Le serveur intégré de Python n'envoie aucun
`Cache-Control` : le navigateur applique alors son heuristique de fraîcheur et sert ce qu'il a en
mémoire. Après un rebuild, il peut donc associer le `.data` d'un build au `.wasm` d'un autre.

Le symptôme n'est pas « version périmée ». C'est, au démarrage :

    Chargement impossible : RuntimeError: memory access out of bounds
      at wasm://wasm/0b2ac7ce:wasm-function[97296]:0x1712ca9
      ... trois cents lignes d'offsets, pas un seul nom de méthode ...

Une heure a été perdue à chercher ça dans le code du jeu (2026-08-14). Le build pose bien un
garde-cache (`BuildBench.StampWebCacheBuster`), mais il vit dans `index.html` — et si la page hôte
elle-même sort du cache, le garde-cache désigne encore les fichiers de l'ancien build. Un mécanisme
d'invalidation transporté par une ressource cachable s'auto-annule : il faut une racine non
cachable, et seule une vraie en-tête HTTP l'obtient. Les balises `http-equiv` du HTML ne suffisent
pas, Chrome les ignore pour le document principal.

Usage
-----
    py tools/serve_web.py                 # http://localhost:8080
    py tools/serve_web.py --port 9000
    py tools/serve_web.py --dir unity/Build/web

⚠ Si le jeu reste cassé alors que ce serveur tourne, le cache du navigateur est déjà pollué par une
session précédente : changer de port donne une origine neuve, donc un cache vierge.
"""

from __future__ import annotations

import argparse
import functools
import http.server
import pathlib


class NoCacheHandler(http.server.SimpleHTTPRequestHandler):
    """Sert les fichiers en interdisant tout cache, et en annonçant les types dont Unity a besoin."""

    def end_headers(self) -> None:
        # ⚠ `no-store` VISÉ, pas global. Le premier essai l'appliquait à tout : le `.data` de 28 Mo se
        # retéléchargeait à chaque lancement et le jeu mettait près d'une minute à démarrer. Or le
        # défaut ne concernait jamais que la page hôte et les quatre fichiers du moteur — ceux dont
        # le nom ne change pas d'un build à l'autre. Le reste (`StreamingAssets`) peut être caché
        # sans risque : son contenu est décrit par un manifeste écrit au build.
        if self._must_not_be_cached():
            # `no-store` et non `no-cache` : le second autorise le stockage et se contente d'exiger
            # une revalidation, que le navigateur peut sauter (retour arrière, restauration
            # d'onglet). Ici on veut qu'il ne garde rien.
            self.send_header("Cache-Control", "no-store, no-cache, must-revalidate, max-age=0")
            self.send_header("Pragma", "no-cache")
            self.send_header("Expires", "0")

        super().end_headers()

    def _must_not_be_cached(self) -> bool:
        path = self.path.split("?", 1)[0].rstrip("/")

        # La page hôte : c'est elle qui porte l'identifiant de build qui invalide tout le reste.
        # Cachée, elle continue de désigner les fichiers de l'ancien build — le garde-cache existe
        # alors, il est correct, et il ne s'applique jamais.
        if path in ("", "/index.html"):
            return True

        # Les quatre fichiers du moteur, dont le nom est identique d'un build à l'autre.
        return path.startswith("/Build/")

    def guess_type(self, path):
        # Unity produit des `.unityweb` que le serveur de Python ne connaît pas. Le type importe peu
        # ici (le repli JS de décompression est actif), mais un type explicite évite qu'un navigateur
        # tente de les interpréter.
        if str(path).endswith(".unityweb"):
            return "application/octet-stream"
        if str(path).endswith(".wasm"):
            return "application/wasm"
        return super().guess_type(path)

    def log_message(self, fmt: str, *args) -> None:
        # Le journal par requête noie la sortie : un build web fait des centaines de requêtes de
        # StreamingAssets. On ne garde que les erreurs.
        status = args[1] if len(args) > 1 else ""
        if str(status).startswith(("4", "5")):
            super().log_message(fmt, *args)


def main() -> int:
    parser = argparse.ArgumentParser(description="Sert le build web sans cache navigateur.")
    parser.add_argument("--port", type=int, default=8080)
    parser.add_argument("--dir", default="unity/Build/web")
    args = parser.parse_args()

    root = pathlib.Path(args.dir).resolve()
    if not (root / "index.html").exists():
        print(f"!! {root} ne contient pas index.html — le build web a-t-il ete fait ?")
        return 1

    handler = functools.partial(NoCacheHandler, directory=str(root))

    # `allow_reuse_address` : sans lui, relancer le serveur juste apres l'avoir arrete echoue pendant
    # la temporisation TIME_WAIT — ce qui pousse a changer de port et brouille le diagnostic.
    http.server.ThreadingHTTPServer.allow_reuse_address = True

    # ⚠⚠ MULTI-THREAD OBLIGATOIRE, et ce n'est pas une optimisation.
    # Le premier essai utilisait `socketserver.TCPServer`, qui traite UNE requete a la fois. Le jeu
    # precharge ses StreamingAssets en parallele (`StreamingText.PreloadAll`) et le navigateur garde
    # ses connexions ouvertes : les requetes se bloquent mutuellement, le prechargement n'aboutit
    # jamais, et le jeu reste sur sa barre de demarrage — qui semble meme reculer. Aucune erreur,
    # ni dans la console du navigateur, ni cote serveur. C'est exactement ce qui s'est passe, et ce
    # qui explique que `python -m http.server` (qui utilise ThreadingHTTPServer depuis 3.7)
    # fonctionnait la ou ce script echouait.
    with http.server.ThreadingHTTPServer(("", args.port), handler) as httpd:
        print(f"Chimera Protocol (web) : http://localhost:{args.port}/")
        print(f"  dossier  : {root}")
        print( "  cache    : desactive (Cache-Control: no-store)")
        print( "  tactile  : ajouter ?touch pour forcer les controles au doigt")
        print( "  Ctrl+C pour arreter.")
        try:
            httpd.serve_forever()
        except KeyboardInterrupt:
            print("\nArrete.")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
