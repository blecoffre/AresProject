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

- **`Models/SaveData.cs`** — **classe**, pas struct : elle porte deux `List<>`, et une copie de struct partagerait les références en donnant l'illusion d'une copie indépendante. `CurrentVersion = 1`, `EnsureValid()` (répare une désérialisation), `Migrate()` (cas chaînés par `goto case`).
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
