# Persistance — livrée et validée le 2026-08-25

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

- **`Models/SaveData.cs`** — **classe**, pas struct : elle porte deux `List<>`, et une copie de struct partagerait les références en donnant l'illusion d'une copie indépendante. `CurrentVersion = 6`, `EnsureValid()` (répare une désérialisation), `Migrate()` (cas chaînés par `goto case`).
  **v1 → v2 (lot 2b-1)** : `ComputerPower` et `TotalComputerPower` retirés. Les TFlops sont devenues une capacité dérivée du parc Hardware, entièrement recalculable depuis `Upgrades` — les sauvegarder créait une seconde source de vérité qui pouvait diverger. Aucun report n'est nécessaire : `JsonUtility` ignore simplement les champs en trop d'un ancien fichier, la migration se contente de tamponner la version.
  **v2 → v3 (prestige sur la run)** : ajout de `RunMoney`, l'argent gagné depuis le début de la run, qui pilote désormais le gain de CPU Cycles. La migration le force à `0` au lieu de recopier le cumul à vie — sinon une run déjà entamée rapporterait un gain immérité au premier Game Over.
  **v3 → v4 (Ghost Cache)** : ajout de `GhostCacheSeconds`, la charge accumulée en secondes d'excédent de dissipation. État de RUN malgré sa sauvegarde : le wipe l'efface, mais fermer le jeu ne la perd pas — elle représente du temps de jeu investi. Un Overdrive **en cours** n'est délibérément pas sauvegardé ; sinon fermer la fenêtre reviendrait à mettre l'effet en pause, dans un jeu qui n'a aucune progression hors-ligne. La migration part de zéro : la mécanique n'existait pas avant.
  **v4 → v5 (Data Wiper)** : le Bouton d'Urgence passe du coût en argent au prérequis en TFlops et gagne `EmergencyBlockRemainingSeconds` et `EmergencyCooldownRemainingSeconds`. Les deux sont sauvegardés alors qu'un Overdrive en cours ne l'est pas, et l'asymétrie est délibérée : sauvegarder l'Overdrive aurait permis de mettre en PAUSE un bonus en fermant la fenêtre, ne pas sauvegarder le contrecoup permettrait d'ÉCHAPPER à une pénalité par le même geste. `EmergencyUsesInRun` existait déjà et n'est pas touché par la migration.
  **v5 → v6 (durée de run)** : ajout de `RunElapsedSeconds`. Cumulée frame par frame dans `GameSessionManager.Tick()` et non déduite d'un `Time.time` de départ — celui-ci repart de zéro à chaque lancement, et une run reprise le lendemain afficherait la durée de la seule session en cours. Sauvegardée pour la même raison ; le temps passé fenêtre close n'est jamais crédité, le compteur n'avance que dans `Tick()`. La migration part de zéro : les anciennes sauvegardes n'ont pas mesuré leur temps de jeu.
  **v7 → v8 (achat multiple)** : ajout de `BuyQuantityMode`, le mode d'achat sélectionné (0 = x1, 1 = x10, 2 = x100, 3 = MAX). Premier champ du fichier qui n'est **ni de la méta-progression ni de la run** : c'est une préférence d'interface, et le wipe n'y touche pas — perdre son mode d'achat parce qu'on s'est fait repérer n'aurait aucun sens. Stocké en `int` et non en `BuyQuantity` : les valeurs de l'enum sont désormais un format de fichier, les réordonner redéfinirait silencieusement la préférence de tous les joueurs installés. `EnsureValid()` fait retomber sur x1 tout ce qui sort du domaine. L'état vit dans `BuyQuantitySelector`, service du scope **racine** : le gateway le restaure avant que la GameScene n'existe, et il survit à son rechargement.

- **`Services/Persistence/GameStateGateway.cs`** — `Restore` / `Capture`. Tampon `SaveData` et deux `Dictionary` réutilisés : un autosave n'alloue rien hors la chaîne JSON.
- **`Services/Persistence/SaveScheduler.cs`** — les trois déclencheurs. `_isWriting` évite d'empiler les écritures.
- **`Services/Persistence/ISyncSaveService.cs`** — voie synchrone, fermeture uniquement.
- **`Services/Save/LocalJsonSaveService.cs`** — écriture atomique (`.tmp` puis `File.Replace` avec `.bak`), relecture du `.bak` si le fichier principal est corrompu. Deux temporaires distincts (`savegame.tmp` / `savegame.quit.tmp`) car un autosave peut être en vol au moment de la fermeture.

Emplacement du fichier : `%userprofile%\AppData\LocalLow\<Company>\<Product>\savegame.json`

## Points de conception à ne pas défaire

- **`MaxRestorableThreat = 0.99f`** — on ne restaure jamais une jauge pleine, sinon la partie reprend en Game Over avant que l'UI ne soit abonnée. `ThreatManager.RestoreThreat()` ne déclenche **jamais** le lockdown : c'est le rôle exclusif d'`AddThreat`.
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

- `ISteamRuntime` injecté, pour remplacer l'appel statique `SteamManager.Initialized`.
- `SaveServiceComposite` inversé : local prioritaire, cloud départagé par `SavedAtUnixSeconds`.
- Déplacer `ISaveService` de `Core.Models` vers `Core.Services.Persistence`.
- Unifier le nom de fichier (l'ancien `core_exe_save_v1.json` du chemin Steam est aujourd'hui ignoré).

`SteamCloudSaveService` et `SaveServiceComposite` sont **désenregistrés** (TODO commenté dans `RootLifetimeScope`) mais compilent toujours.
