# Architecture — Assets/Core

Stack : **VContainer** (DI), **R3** (réactif), **UniTask** (async), MVP strict, pipeline data-driven JSON → ScriptableObject → Catalogue.

## Hiérarchie des scopes

**`RootLifetimeScope`** (`Boot/`) — tout le métier, en `Singleton`. Survit au rechargement de la GameScene.

- Catalogues SO (`UpgradeCatalogSO`, `PrestigeCatalogSO`) injectés par l'inspecteur
- `UserCurrencies`, `UpgradeManager`, `PrestigeManager`, `ThreatManager`, `EmergencyProtocolSystem`, `GameSessionManager`
- Persistance : `LocalJsonSaveService` (`ISaveService` + `ISyncSaveService`), `GameStateGateway`
- `JsonLocalizationService`, `SceneLoader`, `ApplicationQuitter`
- `SimulationClock` (`ITimeSource` + `ISimulationClock`) — voir « Pause » plus bas
- `SettingsStore`, `SettingsService` (`ISettingsService`) — réglages du joueur, voir `persistence.md`
- EntryPoints : `GameBootstrapper` (IAsyncStartable), `SaveScheduler` (IAsyncStartable), `ScriptCycleRunner` (IStartable + ITickable)

**`GameSceneLifetimeScope`** (fichier dans `Services/`, namespace `Core.Boot`) — l'UI, en `Scoped`.

- Views (`RegisterComponent`) + Presenters
- `OverclockSystem`
- EntryPoints : `GameSceneBootstrapper`, `SimulationTicker`

> **Piège :** un service enregistré en `Lifetime.Scoped` **dans le Root** est ré-instancié dans chaque scope enfant. Le scope de scène manipulerait alors une seconde instance, désynchronisée. Tout ce qui est métier va en `Singleton`.

**`MainMenuLifetimeScope`** (`Boot/`) — l'écran titre : `MainMenuView` + `MainMenuPresenter`, l'écran de choix de langue (`LanguagePickerView` + `LanguagePickerPresenter`), le panneau d'options (`OptionsPanelView` + `OptionsPanelPresenter`) et `UiScalePresenter`.

Le panneau d'options est **un prefab** (`Data/Prefabs/UI/OptionsPanel.prefab`) posé dans chaque scène qui en a besoin, avec son presenter enregistré dans le scope de cette scène. Les boutons dérivent de `TerminalButton.prefab`, les lignes de réglage de `OptionSelector.prefab`.

`SceneLoader` charge la MainMenuScene ou la GameScene (jamais les deux) en additif avec `LifetimeScope.EnqueueParent`, ce qui rattache automatiquement le scope de scène au Root.

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
       └─ SceneLoader.LoadMainMenuSceneAsync(ct)
              └─ MainMenuLifetimeScope → « Continuer »
                     └─ SceneLoader.LoadGameSceneAsync(None)
                            └─ GameSceneLifetimeScope → Views + Presenters
```

L'ordre `IStartable` avant `IAsyncStartable` est **volontairement sûr dans les deux sens** : le runner reconstruit ses emplacements en `Start()` *et* sur l'événement, donc peu importe lequel arrive en premier.

## Localisation à chaud

La langue change en cours de partie, sans recharger de scène. `ILocalizationService.CurrentLanguage` (`ReadOnlyReactiveProperty<string>`) émet **après** le remplacement du dictionnaire ; chaque presenter qui écrit du texte s'y abonne et se réécrit.

**Règle pour tout nouveau texte affiché :**

- Un presenter qui appelle `GetText` s'abonne à `_loc.CurrentLanguage`, soit comme premier rendu (l'abonnement émet la valeur courante), soit avec `.Skip(1)` s'il a déjà rendu juste avant.
- Un texte composé à un instant donné (bilan de fin de run, écran de victoire) mémorise ses **données**, pas sa chaîne, pour pouvoir se recomposer.
- Un filtre « rien n'a bougé » (ex. `GeneratorPresenter.RefreshPurchaseState`) doit être invalidé au changement de langue.
- Exception assumée : les lignes déjà écrites dans la console restent dans leur langue d'origine (c'est un historique).
- `LocalizedText` suit la langue tout seul, mais reste soumis à la liste `autoInjectGameObjects` : préférer un texte écrit par le presenter.

Les langues proposées sont les fichiers présents dans `StreamingAssets/Localization`. Chaque fichier porte les clés `LANGUAGE_NAME_<CODE>` de **toutes** les langues, écrites dans leur propre langue (« Español »). La langue choisie est mémorisée dans `settings.json` ; vide, elle déclenche l'écran de choix du premier lancement (`LanguagePickerPresenter`, langue du système présélectionnée).

## Audio (terrain préparé, aucun son encore)

- `Data/Audio/GameMixer.mixer` : groupes **Master → Music, SFX**, paramètres exposés **`MasterVolume`**, **`MusicVolume`** et **`SfxVolume`** (volume de chaque groupe).
- Le Master ne fait que couper ou laisser passer : « Couper tout le son », ou fenêtre inactive quand « Son en arrière-plan » est sur Non (`Application.focusChanged`, même parti pris que le `FrameRateGovernor` : focus supposé acquis au démarrage).
- `AudioVolumeApplier` (Root) est le **seul** à écrire ces volumes, depuis les 6 réglages audio de `settings.json` (volume 0-100 % par pas de 10, et une coupure indépendante par canal). Conversion logarithmique : 50 % = -6 dB, 0 % ou coupé = -80 dB.
- **Pour le futur système audio** : router chaque `AudioSource` vers le groupe `Music` ou `SFX` du mixer (`outputAudioMixerGroup`), et ne JAMAIS lire ni écrire les volumes lui-même. Renommer un paramètre exposé casse le lien en silence (un avertissement est loggé une fois).

## Palettes (daltonisme)

Les couleurs qui **portent un sens** (états des nœuds et des liens, paliers de la jauge, titre de fin de run, Ghost Cache, urgence, étiquettes de la console) viennent de `UiPaletteSO`, une par mode de vision (`GameData/Palettes/` : Standard, Protanopia, Deuteranopia, Tritanopia), réunies dans `UiPaletteCatalogSO` sur le `RootLifetimeScope`. `IPaletteService.Current` suit l'option `ColorVision` de `settings.json` ; les presenters s'y abonnent et repeignent à chaud, comme pour la langue.

- **Un champ par usage**, pas des rôles génériques : la palette Standard reproduit exactement les teintes d'origine.
- **Option B (tranchée le 2026-10-01)** : le vert terminal reste le « positif » dans tous les modes ; seuls abordable, danger et spécial changent.
- **Le décor n'est pas dans la palette** (vert terminal, verts sombres, gris) : il se lit à la luminosité, que les daltonismes conservent.
- **Toute retouche d'une palette** se valide par `Tools/Core/Vérifier les palettes (daltonisme)` : simulation de Machado 2009 + CIEDE2000, seuil ΔE 20, groupe par groupe et contre le vert du décor.
- Le même outil exige un **contraste WCAG ≥ 4,5:1** pour le texte des nœuds et de l'inspecteur, dans **toutes** les palettes (lisibilité pour tous, pas seulement daltonisme). C'est ce contrôle qui a fait passer « verrouillé » de `#335533` (2,4:1) à `#5E8A5E` (5,1:1) le 2026-10-02 — et, par ricochet, « trop cher » au violet `#9B6BFF` en deutéranopie, où tous les rouges se confondaient avec ce vert éclairci.
- **Arbre de prestige — canaux redondants** : chaque état se lit sans la couleur (ligne de statut « VERROUILLÉ » / prix / « Niv. x/y » / « MAX », fond plein du max, pulsation de l'abordable, pointillés et épaisseur des liens). Seul le couple abordable / trop cher partage le même texte (un prix) : c'est la **pulsation** qui les départage. Ne jamais la supprimer sans ajouter un autre canal.

## Pause

Toute la simulation lit son pas de temps via `ITimeSource`, servi par `SimulationClock`. Celle-ci rend `Time.deltaTime`, ou `0` tant qu'une raison (`SimulationHold`, drapeaux cumulables) la retient :

- `OutOfGame` — posée à la construction, levée par `GameSceneBootstrapper.Start()`, reposée par son `Dispose()`. **La simulation ne tourne que lorsque la GameScene existe** : menu, démarrage et transitions sont figés.
- `PauseMenu` — posée par `PauseMenuPresenter.Open()`, levée par `Close()` **et par son `Dispose()`** : sans ce dernier, revenir au menu depuis la pause ferait démarrer la partie suivante figée.

**Échap en jeu** (`GameSceneBootstrapper.HandleEscape`) referme ce qui est ouvert, du plus haut au plus bas — options, puis pause, puis arbre de prestige — et n'ouvre la pause que si rien ne l'était. L'écran de fin de run ne se ferme pas : Échap y ouvre la pause. Pendant la pause, P et Espace sont ignorés.

Le retour au menu principal demande une sauvegarde via `ISaveRequester` (implémenté par `SaveScheduler`, même file que l'autosave).

`Time.timeScale` n'est **jamais** touché : il figerait aussi l'UI, et lierait la simulation au rendu.

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
