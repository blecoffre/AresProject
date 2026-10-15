# Persistance — livrée et validée le 2026-08-25

> À jour au 2026-10-14 : format **v13**. **Plus aucune migration pendant le développement**
> (lot 5 de la refonte « Time Attack ») : une sauvegarde d'un autre format ouvre une partie neuve.

## Règle d'or

**Tout champ sauvegardé s'ajoute en DEUX endroits symétriques dans `GameStateGateway` : `Restore()` et `Capture()`.**

C'est précisément pour ça que les deux sens vivent dans la même classe, lisibles côte à côte. L'oubli d'un seul côté est le bug qui avait fait disparaître toute la méta-progression : `SaveData.PrestigeUpgrades` existait, `PrestigeManager.InitializeFromSave` existait, et personne ne les reliait.

## Chaîne complète

```
GameBootstrapper (IAsyncStartable)
  → ISaveService.FetchSaveAsync           LocalJsonSaveService
  → SaveData.Migrate + EnsureValid
  → GameStateGateway.Restore(save)        → HasRestored = true

SaveScheduler (IAsyncStartable + IDisposable, EntryPoint du Root)
  → boucle UniTask 5 min                  → WriteAsync("autosave")
  → GameSessionManager.OnSessionEnded     → WriteAsync("fin de run")
  → PrestigeManager.OnPrestigePurchased   → WriteAsync("achat de prestige")
  → Application.quitting                  → ISyncSaveService.SaveBlocking
```

`Application.quitting` **doit** passer par la voie synchrone : la boucle de jeu s'arrête là, rien n'est repris après un `await`. Ce chemin ne vise que le disque local — une écriture réseau n'a aucune garantie d'aboutir pendant l'arrêt du processus.

## Fichiers

- **`Models/SaveData.cs`** — **classe**, pas struct : elle porte deux `List<>`, et une copie de struct partagerait les références en donnant l'illusion d'une copie indépendante. `CurrentVersion = 13`, `EnsureValid()` (répare une désérialisation), `Migrate()`.
  **Pas de migration tant que le jeu n'a pas de joueurs externes** (Bertrand, 2026-10-14) : le seul
  joueur supprime sa sauvegarde à chaque changement de format. `Migrate()` rend la sauvegarde telle
  quelle si sa version est la courante, et sinon une partie neuve, avec un avertissement
  `[SAVE]` en console. Les treize migrations v0 → v13 écrites pendant le développement ont été
  retirées au lot 5 ; leur historique est archivé dans `historique.md`. **Après le premier build
  public, la règle s'inverse** : chaque incrément de version devra porter sa migration, sous peine
  de casser les sauvegardes des joueurs.
  `HasWon` (méta-progression) : la victoire n'est sauvegardée que pour ne pas rejouer sa mise en
  scène à chaque ouverture ; `VictorySystem.Restore` ne prend jamais la valeur faux, pour qu'un
  fichier abîmé ne puisse pas effacer une victoire acquise.
  ⚠️ **L'ordre de restauration est porteur de sens ici.** `_victory.Restore(data.HasWon)` doit précéder `_upgradeManager.InitializeFromSave` : celui-ci recalcule les totaux et pousse une valeur dans `NetTFlops`, que le `VictorySystem` observe. Poser le drapeau après rejouerait la victoire à chaque chargement.
  `EmergencyUsesInRun` : le sacrifice de TFlops du Data Wiper en découle (30 % par usage) ; le
  sauvegarder empêche qu'une fermeture rende la puissance engloutie. ⚠️ **Restauré lui aussi AVANT
  `InitializeFromSave`** (depuis le 2026-10-15) : le `VictorySystem` écoute déjà pendant le
  chargement, et un parc restauré sans son sacrifice vaudrait un instant sa pleine puissance — de
  quoi déclencher une victoire à tort. Le sacrifice est une fraction tenue par l'`UpgradeManager`,
  il s'applique aux générateurs recréés ensuite.
  `PendingRunEnd` (struct à plat, copie du `RunSummary`) : le bilan de la run terminée tant que le
  joueur n'a pas relancé. Restauré, il désarme la partie (`GameSessionManager.RestorePendingRunEnd`),
  et le `GameOverPresenter` rouvre l'écran de fin en `PostStart` — après le `Start` du panneau de
  prestige, dont il a besoin. Une raison inconnue efface le bloc (`EnsureValid`).
  `Readout` (dernier relevé en UT, âge du relevé, sauts connus) : restauré tel quel par
  `TraceReadout.Restore` après la jauge, pour que le brouillard ne se lève pas au rechargement. Un
  relevé au-dessus de la vérité (fichier édité) est recalé.

- **`Services/Persistence/GameStateGateway.cs`** — `Restore` / `Capture`. Tampon `SaveData` et deux `Dictionary` réutilisés : un autosave n'alloue rien hors la chaîne JSON.
- **`Services/Persistence/SaveScheduler.cs`** — les trois déclencheurs, plus le retour au menu (`ISaveRequester`). Une demande qui arrive pendant une écriture en vol n'est **jamais jetée** : elle arme **un** tour de rattrapage (`_pendingReason`), qui capture l'état au moment où il s'exécute. Un seul emplacement d'attente, et c'est délibéré : `Capture()` rend un tampon partagé qui continue de muter, une file empilerait N références au même état (corrigé le 2026-08-30, cf. `historique.md`).
- **`Services/Persistence/ISyncSaveService.cs`** — voie synchrone, fermeture uniquement.
- **`Services/Save/LocalJsonSaveService.cs`** — écriture atomique (`.tmp` puis `File.Replace` avec `.bak`), relecture du `.bak` si le fichier principal est corrompu. Deux temporaires distincts (`savegame.tmp` / `savegame.quit.tmp`) car un autosave peut être en vol au moment de la fermeture.

Emplacement du fichier : `%userprofile%\AppData\LocalLow\<Company>\<Product>\savegame.json`

## Partie neuve ou existante

`SaveData.IsFresh` (`[NonSerialized]`, jamais écrit) vaut vrai pour une partie créée faute de sauvegarde. `GameStateGateway.HasSavedGame` en dérive : vrai si la partie a été restaurée d'un fichier, ou dès qu'une capture est écrite. Le menu principal s'en sert pour dire « Commencer » plutôt que « Continuer » au premier lancement. Ne pas en faire un champ sérialisé : une sauvegarde relue doit toujours valoir « existante ».

**Une partie neuve n'est écrite qu'une fois entamée.** `GameStateGateway.CanWrite` = restauré ET (partie sur disque OU `HasGameStarted`, posé par le `GameSceneBootstrapper` à l'entrée en jeu). L'autosave et la sauvegarde de fermeture le respectent : ouvrir puis fermer le jeu depuis le menu sans jouer ne crée aucun fichier, et le menu dit encore « Commencer » au lancement suivant. Il n'y a rien à perdre — la partie neuve est recréée à l'identique.

## Réglages du joueur — `settings.json` (hors sauvegarde)

Les options (langue, affichage, V-Sync, plafond de FPS, échelle d'interface, mode daltonien, volumes et coupures audio) vivent dans `settings.json`, **à côté** de `savegame.json` mais **jamais dedans** : elles décrivent une machine, pas une progression. Elles ne passent donc ni par `SaveData` ni par le `GameStateGateway`, et ne voyageront pas avec Steam Cloud.

- Modèle : `Core.Models.Settings.GameSettings` (champs publics `JsonUtility`, valeurs par défaut à l'initialisation → un champ ajouté ne casse pas un fichier existant).
- Lecture/écriture : `SettingsStore`, synchrone et atomique (temporaire puis remplacement).
- Source de vérité : `SettingsService` (Root), qui valide (`Sanitize`), publie en `ReactiveProperty` et écrit à **chaque** changement.
- Appliqués par : `SettingsService` (mode d'affichage, résolution), `FrameRateGovernor` (V-Sync, FPS), `UiScalePresenter` (échelle, un par scène), `PaletteService` (mode daltonien), `AudioVolumeApplier` (volumes, via le mixer), `ILocalizationService` (langue, chargée par le `GameBootstrapper` et les options).

## Points de conception à ne pas défaire

- **`MaxRestorableThreat = 0.99f`** — on ne restaure jamais une jauge pleine (99 % de 10 000 UT), sinon la partie reprend en Game Over avant que l'UI ne soit abonnée. `ThreatManager.RestoreTrace()` ne déclenche **jamais** la saisie : c'est le rôle exclusif de l'avance de la jauge (`Advance`) et de la pénalité d'Overclock.
- **Verrou `HasRestored`** — sans lui, fermer le jeu pendant le chargement remplace la sauvegarde du joueur par une partie neuve.
- **Sérialisation synchrone en entrée de `SaveAsync`** — le gateway rend un tampon réutilisé ; le lire après un `await` mélangerait deux états.
- Les niveaux à 0 ne sont pas écrits dans le fichier.
- La Trace et le compteur du Bouton d'Urgence **font partie de l'état sauvegardé**. Sans eux, fermer et rouvrir le jeu vide la jauge gratuitement et rend la puissance sacrifiée par le Data Wiper.
- **L'écran de fin fait partie de l'état sauvegardé** (v12). Sans lui, fermer le jeu sur cet écran relance une run, et la seule fenêtre d'achat du prestige se ferme.

## Ajouter un champ sauvegardé — procédure

1. Ajouter le champ dans `SaveData`, dans le bon bloc (méta-progression ou run en cours).
2. Incrémenter `CurrentVersion`. Pendant le développement, rien de plus : une sauvegarde plus
   ancienne ouvrira une partie neuve. Après le premier build public, écrire aussi sa migration
   dans `Migrate()` (un `switch` sur la version, cas chaînés par `goto case`).
3. Le lire dans `GameStateGateway.Restore()`.
4. L'écrire dans `GameStateGateway.Capture()`.
5. Le borner dans `EnsureValid()` si la valeur a un domaine.

## Reste à faire — étape 1b (Steam), reportée

Sans AppId Steam il n'y a rien à tester ; l'étape attend l'ouverture de la page Steam.

⚖️ **Alternative à évaluer d'abord : l'Auto-Cloud de Steamworks.** Il synchronise un dossier
déclaré sur la page Steamworks, sans une ligne de code. Réglé sur `savegame.json` **seul** —
`settings.json` décrit la machine et ne doit pas voyager —, il rendrait la liste ci-dessous et les
deux classes Steam inutiles. À trancher au lot Steam ; la liste ne vaut que si l'on garde la voie
par l'API.

- `ISteamRuntime` injecté, pour remplacer l'appel statique `SteamManager.Initialized`.
- `SaveServiceComposite` inversé : local prioritaire, cloud départagé par `SavedAtUnixSeconds`.
- Déplacer `ISaveService` de `Core.Models` vers `Core.Services.Persistence`.
- Unifier le nom de fichier (l'ancien `core_exe_save_v1.json` du chemin Steam est aujourd'hui ignoré).

`SteamCloudSaveService` et `SaveServiceComposite` sont **désenregistrés** (TODO commenté dans `RootLifetimeScope`) mais compilent toujours.
