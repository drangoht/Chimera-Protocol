// Écrit sur le disque du navigateur ce que le jeu croit avoir déjà enregistré.
//
// En WebGL, `Application.persistentDataPath` n'est pas un dossier : c'est un système de fichiers
// émulé qui vit dans la MÉMOIRE de l'onglet. `File.WriteAllText` y réussit, ne lève rien, et relire
// le fichier dans la foulée rend bien son contenu — tout paraît donc normal. Mais rien n'a atteint
// IndexedDB, et fermer l'onglet efface l'ensemble.
//
// C'est le pire mode d'échec possible pour ce jeu en particulier : la sauvegarde est le seul endroit
// dont la perte est irréversible pour le joueur — Échos accumulés, améliorations achetées, records,
// arsenal découvert. Et il ne se manifeste qu'à la SESSION SUIVANTE, jamais pendant les essais.
//
// `FS.syncfs(false, …)` pousse la mémoire vers IndexedDB. Le sens du booléen est contre-intuitif :
// `false` signifie « de la mémoire vers le stockage », c'est-à-dire enregistrer. `true` ferait
// l'inverse et écraserait la partie en cours par ce qui traîne sur le disque.
mergeInto(LibraryManager.library, {
  ChimeraSyncFilesystem: function () {
    FS.syncfs(false, function (err) {
      if (err) {
        console.error('[Chimera] enregistrement impossible : ' + err);
      }
    });
  },
});
