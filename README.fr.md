# Couch Co-op pour Core Keeper

**La coop locale en écran partagé pour Core Keeper** : deux joueurs, deux manettes, un seul écran (ta télé). Chaque joueur a son propre personnage, son inventaire et sa caméra, comme deux joueurs en ligne.

Le dépôt contient aussi **NotProton Fixes**, un mod séparé et facultatif qui rend Core Keeper jouable sur **macOS via NotProton** (Steam + CrossOver).

🇬🇧 [English version](README.md)

---

## Fonctionnalités

**Couch Co-op** (toutes plateformes)
- **Choix des manettes** façon FIFA : pousse ta manette à gauche (J1) ou à droite (J2). Chaque fenêtre n'écoute que sa manette.
- **Écran partagé en un bouton** : lance le serveur dédié officiel en arrière-plan avec un de tes mondes, puis ouvre une 2e fenêtre du jeu pour J2.
- **Disposition propre** : fenêtres sans bordure, J2 en haut et J1 en bas (ou côte à côte). Le rendu est élargi pour remplir chaque moitié, donc **sans bandes noires**.
- **« Join With IP » pré-rempli** dans les deux fenêtres : il n'y a plus qu'à appuyer sur *Join*.
- **J1 et J2 admins** du serveur local.
- **Sauvegardes de J2 séparées** : personnages et réglages du jeu. La musique de J2 est coupée par défaut, mais les bruitages restent.
- **Hub en jeu dessiné avec les polices et cadres du jeu**, utilisable à la manette (télé, Steam Deck), en français et en anglais.
- **Échelle réglable** : un seul réglage (Auto, x1 à x4) pour l'interface et le zoom du jeu dans chaque moitié, et pour les menus du mod. C'est toujours un facteur entier, donc tout reste net.

**NotProton Fixes** (macOS + NotProton uniquement, sans effet ailleurs)
- Ignore la synchro Steam Cloud du jeu, qui fait planter le premier chargement sous NotProton.
- Répare les sauvegardes : sous le Wine de NotProton, `File.Exists` ment et `File.Delete`/`Replace` échouent, donc rien n'était écrit.
- Garde le jeu actif quand macOS lui retire le focus.
- Fonctionne aussi dans le serveur dédié.

## Prérequis
- Core Keeper 1.3 (Steam).
- Pour l'écran partagé : **Core Keeper Dedicated Server**, gratuit (Steam › Bibliothèque › Outils), installé dans la même bibliothèque Steam que le jeu.
- Deux manettes.

## Installation

### Windows *(non testé : tes retours sont bienvenus)*
1. Télécharge `CouchCoop-<version>.zip` et décompresse-le.
2. Copie le dossier `CouchCoop` dans `…\steamapps\common\Core Keeper\CoreKeeper_Data\StreamingAssets\Mods\`, ou lance `scripts\install-windows.ps1`.

### macOS (NotProton)
Première fois sur Mac ? Lis le guide complet (en anglais) : [docs/MACOS.md](docs/MACOS.md).

1. Télécharge `CoreKeeper-CouchCoop-macOS-<version>.zip` et décompresse-le.
2. Jeu fermé, double-clique sur `INSTALL-macOS.command`.

Le script installe les deux mods dans le jeu, NotProton Fixes dans le serveur dédié, et le contournement du chargeur de mods pour NotProton.

### Linux / Steam Deck (Proton) *(non testé)*
Comme sous Windows : copie `CouchCoop` dans `CoreKeeper_Data/StreamingAssets/Mods/`.

> Le jeu peut afficher un avertissement indiquant que des mods sont actifs. C'est normal.

## Jouer en écran partagé

| | Manette | Clavier |
|---|---|---|
| Ouvrir le hub | **Select + Start** maintenus 1 s | **F8** |
| Changer d'onglet | **LB / RB** | Q / E |
| Lancer l'écran partagé | hub › *Écran partagé* › *Lancer* | **F9** |
| Moitié d'écran ⇄ plein écran | hub › *Écran partagé* | **F10** |
| Copier les infos de connexion | hub › *Écran partagé* | **F6** |
| Afficher/masquer la carte de connexion | — | **F7** |

1. Au menu principal, ouvre le hub. Dans l'onglet *Manettes*, place une manette sur **J1** (à gauche) et l'autre sur **J2** (à droite), puis appuie sur **A**.
2. Lance l'écran partagé. Le serveur local démarre ; la première fois, il peut mettre jusqu'à une minute le temps de charger ton monde. **La fenêtre J2 s'ouvre dès que le serveur est prêt.**
3. Dans **chaque** fenêtre, va dans **Rejoindre une partie**. *Join With IP* est déjà rempli : appuie sur **Join**.
4. Jouez ! Quand J1 quitte le jeu, le serveur s'arrête proprement et sauvegarde le monde.

### À propos du monde
- La première fois, le monde choisi dans *Options › Monde copié dans le serveur* (`0` par défaut) est **copié** dans le serveur local.
- Ensuite, le monde coop vit dans `…/Pugstorm/Core Keeper/CouchCoop/Server/` et reste séparé de ta sauvegarde solo.

## Réglages
Modifiables dans l'onglet *Options* du hub. Ils sont stockés via l'API officielle de configuration des mods, dans `…/Pugstorm/Core Keeper/Steam/<id>/mods/<Mod>/`. Le détail de chaque réglage est dans le [README anglais](README.md#settings).

## Fonctionnement
- **Pourquoi un serveur dédié ?** Le client Steam ne sait héberger que via le relais Steam, et les deux fenêtres utilisent le même compte Steam. Le serveur dédié officiel accepte les connexions directes par IP (`-port`) : les deux fenêtres s'y connectent sur `127.0.0.1`.
- **Deux instances, une licence.** J2 est une 2e instance du jeu lancée par le mod. C'est la même approche que les autres outils d'écran partagé. Ce n'est pas officiellement approuvé par Pugstorm.
- **Permissions du mod.** Les deux mods sont des DLL précompilées qui exigent `accessesExtraAssemblies` et `skipSafetyChecks`. Ils utilisent Harmony (fourni avec le jeu), appellent Win32 pour placer les fenêtres et lancent des processus : le serveur dédié et la fenêtre J2. Tout le code source est dans ce dépôt.

## Dépannage
- **Logs** :
  - J1 : `…/Pugstorm/Core Keeper/Player.log`
  - J2 : `…/Core Keeper/J2/Player.log`
  - Serveur : `…/CouchCoop/Server/CoreKeeperServerLog.txt`
  - Les lignes des mods commencent par `[CouchCoop]` / `[NotProtonFixes]`.
- **Fenêtre J2 bloquée sur un écran noir** : ferme-la, puis utilise *Relancer la fenêtre J2* dans le hub (ou Maj+F9).
- **macOS : « mod load failed »** : relance l'installateur. Il réapplique le contournement NotProton.
- **Problèmes d'affichage** (bandes noires, texte flou) : appuie sur **F11** dans la fenêtre concernée, puis partage `Player.log`. Il liste chaque caméra et sa taille de rendu.

## Compiler
```bash
scripts/build.sh            # SDK .NET requis (macOS : brew install dotnet) et jeu installé
scripts/install-macos.sh    # macOS : compile + installe
scripts/package.sh          # archives de publication dans dist/
```

## Licence
MIT, voir [LICENSE](LICENSE). Core Keeper est © Pugstorm / Fireshine Games. Ce projet n'est pas affilié à eux.
