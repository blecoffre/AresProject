# Architecture — Assets/Core

Stack : **VContainer** (DI), **R3** (réactif), **UniTask** (async), MVP strict, pipeline data-driven JSON → ScriptableObject → Catalogue.

## Hiérarchie des scopes

**`RootLifetimeScope`** (`Boot/`) — tout le métier, en `Singleton`. Survit au rechargement de la GameScene.

- Catalogues SO (`UpgradeCatalogSO`, `PrestigeCatalogSO`) injectés par l'inspecteur
- `UserCurrencies`, `UpgradeManager`, `PrestigeManager`, `ThreatManager`, `EmergencyProtocolSystem`, `GameSessionManager`
- Persistance : `LocalJsonSaveService` (`ISaveService` + `ISyncSaveService`), `GameStateGateway`
- `JsonLocalizationService`, `SceneLoader`
- EntryPoints : `GameBootstrapper` (IAsyncStartable), `SaveScheduler` (IAsyncStartable), `ScriptCycleRunner` (IStartable + ITickable)

**`GameSceneLifetimeScope`** (fichier dans `Services/`, namespace `Core.Boot`) — l'UI, en `Scoped`.

- Views (`RegisterComponent`) + Presenters
- `OverclockSystem`
- EntryPoints : `GameSceneBootstrapper`, `SimulationTicker`

> **Piège :** un service enregistré en `Lifetime.Scoped` **dans le Root** est ré-instancié dans chaque scope enfant. Le scope de scène manipulerait alors une seconde instance, désynchronisée. Tout ce qui est métier va en `Singleton`.

`SceneLoader` charge la GameScene en additif avec `LifetimeScope.EnqueueParent`, ce qui rattache automatiquement le scope de scène au Root.

## Séquence de démarrage

```
RootLifetimeScope.Configure
  → ScriptCycleRunner.Start()        (IStartable — emplacements encore vides)
  → GameBootstrapper.StartAsync()    (IAsyncStartable)
       ├─ JsonLocalizationService.LoadLanguageAsync("fr", ct)
       ├─ ISaveService.FetchSaveAsync(ct)
       ├─ GameStateGateway.Restore(save)
       │     └─ UpgradeManager.InitializeFromSave → OnUpgradesRebuilt
       │           └─ ScriptCycleRunner.RebuildSlots()
       └─ SceneLoader.LoadGameSceneAsync(ct)
              └─ GameSceneLifetimeScope → Views + Presenters
```

L'ordre `IStartable` avant `IAsyncStartable` est **volontairement sûr dans les deux sens** : le runner reconstruit ses emplacements en `Start()` *et* sur l'événement, donc peu importe lequel arrive en premier.

## Boucle de jeu

```
ScriptCycleRunner (ITickable, Root)
   → par Script : progression du cycle → versement à échéance → Money

SimulationTicker (ITickable, Scene)
   → UpgradeManager.TotalTracePerSecond ÷ 100 × deltaTime → ThreatManager.AddThreat

ThreatManager : jauge à 1 → OnCriticalLockdown
   → GameSessionManager.HandleGameOver
        → CPU Cycles = floor(sqrt(TotalMoneyGenerated / 1000))
        → OnSessionEnded → GameOverPresenter + SaveScheduler
```

## Règles R3

- Tout abonnement se termine par `.AddTo(_disposables)` ou `.AddTo(ref _disposables)`.
- `CompositeDisposable` pour une classe, `DisposableBag` (struct, passé par `ref`) quand on veut éviter l'allocation.
- Les abonnements se font dans `Start()`, **jamais dans le constructeur** : un constructeur appelé par le conteneur ne doit pas avoir d'effets de bord.
- Lire une valeur : `.CurrentValue`. Écrire : `.Value`.
- **Ne jamais se désabonner d'un `event` avec une lambda** : `-= x => ...` crée un nouveau delegate et ne retire rien. Toujours une méthode nommée.

## Règles UniTask

- Le `CancellationToken` traverse **toute** la chaîne, jusqu'à l'appel bloquant.
- Un fire-and-forget s'écrit `.Forget()`, jamais `_ = ...` : sans lui, une exception disparaît sans log.
- `CancellationToken.None` est un signal d'alarme : il rompt la chaîne d'annulation.

## Zéro-GC

`SimulationTicker` et `ScriptCycleRunner.Tick()` sont les deux boucles de frame. Elles sont écrites sans allocation, sans closure et sans boxing — tableaux de structs parcourus par index, lectures de `CurrentValue`, garde d'entrée sur `IsGameActive`. **C'est le standard à tenir pour tout nouveau code de frame.**

## Dette structurelle connue

- **Aucun `.asmdef`** : tout `Assets/Core` recompile avec le reste du projet, et rien n'empêche une dépendance de `Models` vers `UI`.
- **Namespaces ≠ dossiers** : `Services/GameSceneLifetimeScope.cs` déclare `Core.Boot` ; `Models/Economy/*CatalogSO.cs` déclarent `Core.Economy.Data` ; `Services/LogObjectPool.cs` déclare `Core.Services.Console` ; `SteamManager` est dans le namespace global. Deux dossiers pour la sauvegarde (`Services/Persistence` et `Services/Save`).
- **Dossier fantôme** `UI/Uppgrades` (faute de frappe, vide) à côté de `UI/Upgrades`.
- **`SteamManager`** est un Singleton statique (boilerplate Steamworks.NET du domaine public — ne pas le modifier). L'appel statique `SteamManager.Initialized` depuis `SteamCloudSaveService` doit être encapsulé derrière un `ISteamRuntime` injecté.
