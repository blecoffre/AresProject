# Persistance — livrée et validée le 2026-08-25

> À jour au 2026-10-13 : format **v11** (refonte « Time Attack », lot 1). Prochaines évolutions
> prévues : le Data Wiper sans délai (lot 2) remplace son contrecoup et son délai par un ratio de
> sacrifice, et l'arbre à 20 nœuds (lot 4) demandera un nouveau remboursement — voir
> `etat-des-lieux.md`, feuille de route.

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

- **`Models/SaveData.cs`** — **classe**, pas struct : elle porte deux `List<>`, et une copie de struct partagerait les références en donnant l'illusion d'une copie indépendante. `CurrentVersion = 11`, `EnsureValid()` (répare une désérialisation), `Migrate()` (cas chaînés par `goto case`).
  **v1 → v2 (lot 2b-1)** : `ComputerPower` et `TotalComputerPower` retirés. Les TFlops sont devenues une capacité dérivée du parc Hardware, entièrement recalculable depuis `Upgrades` — les sauvegarder créait une seconde source de vérité qui pouvait diverger. Aucun report n'est nécessaire : `JsonUtility` ignore simplement les champs en trop d'un ancien fichier, la migration se contente de tamponner la version.
  **v2 → v3 (prestige sur la run)** : ajout de `RunMoney`, l'argent gagné depuis le début de la run, qui pilote désormais le gain de CPU Cycles. La migration le force à `0` au lieu de recopier le cumul à vie — sinon une run déjà entamée rapporterait un gain immérité au premier Game Over.
  **v3 → v4 (Ghost Cache)** : ajout de `GhostCacheSeconds`, la charge accumulée en secondes d'excédent de dissipation. *Le champ a quitté le format en v11 avec le Ghost Cache ; la migration v3 → v4 ne fait plus que tamponner la version.* État de RUN malgré sa sauvegarde : le wipe l'efface, mais fermer le jeu ne la perd pas — elle représente du temps de jeu investi. Un Overdrive **en cours** n'est délibérément pas sauvegardé ; sinon fermer la fenêtre reviendrait à mettre l'effet en pause, dans un jeu qui n'a aucune progression hors-ligne. La migration part de zéro : la mécanique n'existait pas avant.
  **v4 → v5 (Data Wiper)** : le Bouton d'Urgence passe du coût en argent au prérequis en TFlops et gagne `EmergencyBlockRemainingSeconds` et `EmergencyCooldownRemainingSeconds`. Les deux sont sauvegardés alors qu'un Overdrive en cours ne l'est pas, et l'asymétrie est délibérée : sauvegarder l'Overdrive aurait permis de mettre en PAUSE un bonus en fermant la fenêtre, ne pas sauvegarder le contrecoup permettrait d'ÉCHAPPER à une pénalité par le même geste. `EmergencyUsesInRun` existait déjà et n'est pas touché par la migration.
  **v5 → v6 (durée de run)** : ajout de `RunElapsedSeconds`. Cumulée frame par frame dans `GameSessionManager.Tick()` et non déduite d'un `Time.time` de départ — celui-ci repart de zéro à chaque lancement, et une run reprise le lendemain afficherait la durée de la seule session en cours. Sauvegardée pour la même raison ; le temps passé fenêtre close n'est jamais crédité, le compteur n'avance que dans `Tick()`. La migration part de zéro : les anciennes sauvegardes n'ont pas mesuré leur temps de jeu.
  **v6 → v7 (jauge absolue)** : `NormalizedThreat` (une fraction) devient `CurrentTrace`, en points **absolus** — le plafond de jauge cesse d'être fixe à 100 (il suit le parc Hardware depuis le 2026-08-30, la puissance du joueur depuis le 2026-09-15), donc une fraction ne disait plus quelle quantité de trace restaurer. L'ancien champ est **conservé dans la classe** : `JsonUtility` ne lit un champ que s'il existe encore, le retirer rendrait toutes les sauvegardes antérieures illisibles. La conversion multiplie la fraction par le plafond **historique** de 100, jamais par le plafond courant — sinon un joueur équipé se verrait rendre une trace bien plus lourde que celle qu'il avait. *(Entrée rétablie le 2026-10-13 ; détail du lot dans `historique.md`.)*
  **v7 → v8 (achat multiple)** : ajout de `BuyQuantityMode`, le mode d'achat sélectionné (0 = x1, 1 = x10, 2 = x100, 3 = MAX). Premier champ du fichier qui n'est **ni de la méta-progression ni de la run** : c'est une préférence d'interface, et le wipe n'y touche pas — perdre son mode d'achat parce qu'on s'est fait repérer n'aurait aucun sens. Stocké en `int` et non en `BuyQuantity` : les valeurs de l'enum sont désormais un format de fichier, les réordonner redéfinirait silencieusement la préférence de tous les joueurs installés. `EnsureValid()` fait retomber sur x1 tout ce qui sort du domaine. L'état vit dans `BuyQuantitySelector`, service du scope **racine** : le gateway le restaure avant que la GameScene n'existe, et il survit à son rechargement.
  **v8 → v9 (cumul de campagne)** : `CampaignDatasBanked` et `CpuCyclesAwarded` pilotent désormais les points de prestige, à la place de l'argent de la seule run. La migration remet **les deux** à zéro : le joueur garde ses Cycles et ses nœuds, et son prochain palier sera simplement le premier. N'en remettre qu'un à zéro lui bloquerait la progression jusqu'à ce qu'il rattrape son ancien total. *(Décision décrite dans `game-design.md` — cette entrée manquait ici.)*
  **v9 → v10 (victoire)** : ajout de `HasWon`. De la **méta-progression** : la victoire est un fait acquis de la campagne, insensible au wipe. Elle n'est sauvegardée que pour ne pas rejouer sa mise en scène à chaque ouverture du jeu — la partie continue normalement après. La migration part à **faux** même sur un parc qui dépasse déjà le seuil : ces sauvegardes n'ont jamais pu déclencher la victoire, et le joueur la recevra au premier recalcul des totaux. `VictorySystem.Restore` ne prend d'ailleurs jamais la valeur faux, pour qu'un fichier abîmé ne puisse pas effacer une victoire acquise.
  ⚠️ **L'ordre de restauration est porteur de sens ici.** `_victory.Restore(data.HasWon)` doit précéder `_upgradeManager.InitializeFromSave` : celui-ci recalcule les totaux et pousse une valeur dans `NetTFlops`, que le `VictorySystem` observe. Poser le drapeau après rejouerait la victoire à chaque chargement.
  **v10 → v11 (refonte « Time Attack », 2026-10-13)** : la jauge passe à 10 000 UT fixes et l'arbre change de nœuds, de prix et d'effets. La migration **efface la run en cours** — sa Trace se comptait sur un plafond de plusieurs millions, la ramener sur 10 000 UT mettrait le joueur au bord de la saisie — et **rend tous les CPU Cycles dépensés dans l'arbre**, qu'elle remet à zéro (racine exceptée). En v10, chaque rang coûtait exactement un point : le remboursement se calcule sur le seul fichier, sans catalogue. Garder un Blindage acheté 1 point qui en coûte désormais 10 serait un cadeau ; perdre les points des nœuds retirés, une punition. Cumul de campagne, points accordés et victoire ne bougent pas. `GhostCacheSeconds` quitte le format : `JsonUtility` ignore le champ resté dans un ancien fichier.

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

- **`MaxRestorableThreat = 0.99f`** — on ne restaure jamais une jauge pleine (99 % de 10 000 UT), sinon la partie reprend en Game Over avant que l'UI ne soit abonnée. `ThreatManager.RestoreThreat()` ne déclenche **jamais** le lockdown : c'est le rôle exclusif d'`AddThreat`.
- **Verrou `HasRestored`** — sans lui, fermer le jeu pendant le chargement remplace la sauvegarde du joueur par une partie neuve.
- **Sérialisation synchrone en entrée de `SaveAsync`** — le gateway rend un tampon réutilisé ; le lire après un `await` mélangerait deux états.
- Les niveaux à 0 ne sont pas écrits dans le fichier.
- La Trace et le compteur du Bouton d'Urgence **font partie de l'état sauvegardé**. Sans eux, fermer et rouvrir le jeu vide la jauge gratuitement et rend le Bouton d'Urgence inutile.

## Ajouter un champ sauvegardé — procédure

1. Ajouter le champ dans `SaveData`, dans le bon bloc (méta-progression ou run en cours).
2. Incrémenter `CurrentVersion` et ajouter le `case` correspondant dans `Migrate()`.
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
