# Architecture — Assets/Core

Stack : **VContainer** (DI), **R3** (réactif), **UniTask** (async), MVP strict, pipeline data-driven JSON → ScriptableObject → Catalogue.

## Hiérarchie des scopes

**`RootLifetimeScope`** (`Boot/`) — tout le métier, en `Singleton`. Survit au rechargement de la GameScene.

- Instances posées dans l'inspecteur : catalogues (`UpgradeCatalogSO`, `PrestigeCatalogSO`), `BalancingConfigSO`, `UiPaletteCatalogSO`, et pour l'audio le mixer, `AudioLibrarySO`, `UiSoundChannelSO`
- Économie : `UserCurrencies`, `BuyQuantitySelector`, `UpgradeManager`, `PrestigeManager`
- Trace et run : `ThreatManager` (la jauge de l'A.M.I. ; IStartable, seul à la modifier), `TraceReadout` (le relevé imparfait, en lecture seule sur la vérité), `EmergencyProtocolSystem` (Data Wiper), `GameSessionManager`, `ExfiltrationSystem`, `VictorySystem`
- Persistance : `LocalJsonSaveService` (`ISaveService` + `ISyncSaveService`), `GameStateGateway`
- `JsonLocalizationService`, `SceneLoader`, `ApplicationQuitter`
- `SimulationClock` (`ITimeSource` + `ISimulationClock`) — voir « Pause » plus bas
- `SettingsStore`, `SettingsService` (`ISettingsService`) — réglages du joueur, voir `persistence.md` ; `PaletteService` (`IPaletteService`) — voir « Palettes »
- Audio : `AudioHost`, `MusicPlayer` (ou `SilentMusicPlayer` sans bibliothèque) — voir « Audio »
- EntryPoints : `GameBootstrapper` (IAsyncStartable), `SaveScheduler` (IAsyncStartable), `ScriptCycleRunner` (IStartable + ITickable), `FrameRateGovernor`, `AudioVolumeApplier`, `UiSoundPlayer`, `GameEventSounds`

**`GameSceneLifetimeScope`** (fichier dans `Services/`, namespace `Core.Boot`) — l'UI, en `Scoped`.

- Views (`RegisterComponent`) + Presenters
- `OverclockSystem`
- EntryPoints : `GameSceneBootstrapper` (clavier, Échap, retenue de la simulation), `SimulationTicker`, `SceneMusic`

> **Piège :** un service enregistré en `Lifetime.Scoped` **dans le Root** est ré-instancié dans chaque scope enfant. Le scope de scène manipulerait alors une seconde instance, désynchronisée. Tout ce qui est métier va en `Singleton`.

**`MainMenuLifetimeScope`** (`Boot/`) — l'écran titre : `MainMenuView` + `MainMenuPresenter`, l'écran de choix de langue (`LanguagePickerView` + `LanguagePickerPresenter`), le panneau d'options (`OptionsPanelView` + `OptionsPanelPresenter`) et `UiScalePresenter`.

Le panneau d'options est **un prefab** (`Data/Prefabs/UI/OptionsPanel.prefab`) posé dans chaque scène qui en a besoin, avec son presenter enregistré dans le scope de cette scène. Les boutons dérivent de `TerminalButton.prefab`, les lignes de réglage de `OptionSelector.prefab`.

`SceneLoader` charge la MainMenuScene ou la GameScene (jamais les deux) en additif avec `LifetimeScope.EnqueueParent`, ce qui rattache automatiquement le scope de scène au Root.

## Séquence de démarrage

```
RootLifetimeScope.Configure
  → ScriptCycleRunner.Start()        (IStartable — emplacements encore vides)
  → GameBootstrapper.StartAsync()    (IAsyncStartable)
       ├─ JsonLocalizationService.LoadLanguageAsync(langue des réglages, sinon celle du système, ct)
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

## Audio

**Volumes**

- `Data/Audio/GameMixer.mixer` : groupes **Master → Music, SFX**, paramètres exposés **`MasterVolume`**, **`MusicVolume`** et **`SfxVolume`** (volume de chaque groupe).
- Le Master ne fait que couper ou laisser passer : « Couper tout le son », ou fenêtre inactive quand « Son en arrière-plan » est sur Non (`Application.focusChanged`, même parti pris que le `FrameRateGovernor` : focus supposé acquis au démarrage).
- `AudioVolumeApplier` (Root) est le **seul** à écrire ces volumes, depuis les 6 réglages audio de `settings.json` (volume 0-100 % par pas de 10, et une coupure indépendante par canal). Conversion logarithmique : 50 % = -6 dB, 0 % ou coupé = -80 dB.
- Toute `AudioSource` est routée vers `Music` ou `SFX` (`outputAudioMixerGroup`) et ne lit ni n'écrit JAMAIS les volumes du mixer. Renommer un paramètre exposé casse le lien en silence (un avertissement est loggé une fois).

**Contenu** — `Data/Audio/AudioLibrary.asset` (`AudioLibrarySO`, sur le `RootLifetimeScope`) : les deux groupes de sortie, la musique du menu, celle de la partie, la durée du fondu, la famille de clics par défaut. Un clip vide n'est pas une erreur : le son correspondant se tait. Les licences des sons sont tenues dans `docs/audio-licences.md`.

**Familles de sons** — `SoundSetSO` (`Data/Audio/SoundSets/`), pour les clics comme pour les événements : une liste de variantes, une variation de hauteur (±5 % par défaut) et un volume. Parti pris (2026-10-12) : **le clavier partout**, l'unité sonore d'un hacker qui tape ; les boutons se distinguent par la famille (touches ordinaires = `KeyPress`, par défaut : 36 frappes de `Button/Typing/` ; plus tard une famille « Validation » en Entrée/Espace pour les actions qui engagent), jamais par des sons d'une autre origine.

Familles branchées (2026-10-12) — une famille de BOUTON vide retombe sur `KeyPress` : un bouton préparé garde sa frappe en attendant son son. Le refus et les événements, eux, n'ont pas de repli : vides, ils se taisent (un refus qui sonnerait comme une frappe dirait « c'est fait »).

| Famille | Hauteur | Boutons | Où |
|---|---|---|---|
| `KeyPress` | ±5 % | tous les autres (défaut) | `AudioLibrary` |
| `Validation` | ±5 % | Continuer/Commencer, Recommencer (fin de run), confirmation de langue | MainMenuScene, GameScene, `LanguagePicker.prefab` |
| `PrestigePurchase` | fixe | achat d'un nœud de prestige (`BuyButton`) | GameScene |
| `Exfiltration` | fixe | « Terre Brûlée » (objet nommé `PrestigeButton`, nom hérité) | GameScene |
| `DataWiper` | fixe | Bouton d'Urgence (`PanicButton`) | GameScene |

Les familles « signature » ont une hauteur fixe : un son qu'on doit reconnaître ne varie pas. Chaque bouton se grise quand l'action est impossible, donc son son ne part que sur une vraie action. Les overrides vivent sur les instances de scène (et dans `LanguagePicker.prefab`) : un bouton recréé ailleurs devra être rebranché à la main.

**Réglages d'import** — `AudioImportRules` (éditeur) les impose par dossier, et écrase toute retouche manuelle :
- `Data/Audio/Button/` : DecompressOnLoad, ADPCM, mono (sons courts joués en rafale).
- `Data/Audio/Events/` : DecompressOnLoad, **PCM**, mono (sons synthétiques : l'ADPCM y ajouterait un souffle).
- `Data/Audio/Music/` : Streaming, Vorbis 0,7 (les musiques vont dans ce dossier).

**Sons générés** — `tools/audio/` (Python standard, sans dépendance) : `synth_sfx.py` (les 10 sons d'événements, un réglage par son en tête de fonction, graine fixe), `synth_music.py` (deux boucles bouche-trou sans couture : fréquences quantifiées sur la durée de boucle, notes repliées, bruit fondu sur lui-même), `extract_keys.py` / `clean_keys.py` (découpe et nettoyage de prises de frappe), `make_validation.py` (famille Validation). Les volumes se règlent sur le **niveau moyen (RMS)**, pas sur le pic : `KeyPress` ≈ -30 dB, refus -31, paliers -18 à -16, moments forts -17 à -15. Régénérer, puis recopier dans `Data/Audio/` : les noms de fichiers étant stables, les familles suivent.

**Lecteurs (Root)**

- `AudioHost` porte toutes les sources sous `[Audio]`, enfant du `RootLifetimeScope` — donc dans la RootScene, qui ne se décharge jamais — ainsi que **l'unique `AudioListener`**. Les caméras des scènes n'en ont plus : `SceneLoader` décharge avant de charger, une transition sans écouteur ferait sauter la musique. **Ne pas en remettre sur une caméra** (Unity signalerait deux écouteurs).
- `MusicPlayer` (`IMusicPlayer`) : deux sources en alternance, fondu enchaîné sur le volume des **sources** (le mixer reste à l'`AudioVolumeApplier`). Une piste déjà en cours n'est pas relancée : le rechargement de la GameScene en fin de run ne remet pas la musique au début. La pause ne touche pas à la musique (décision du 2026-10-02).
- `SceneMusic` : chaque scope de scène l'enregistre avec sa piste (`WithParameter(MusicTrack.MainMenu / Game)`). Le lecteur ne connaît ni les scènes ni le `SceneLoader`.
- `SoundSetVoices` : un petit groupe de sources en rotation (la hauteur se règle par source : la changer sous un son en cours le déformerait), qui tire une variante au hasard sans jamais rejouer la précédente. Chaque lecteur a le sien : une rafale de clics ne coupe pas une alerte. Aucune allocation à la lecture.
- `UiSoundPlayer` : 4 voix SFX, priorité 128. Clic (famille du bouton ou `KeyPress`) et refus (`Refusal`).
- `GameEventSounds` : 2 voix SFX, priorité 64 (devant les clics). Écoute en lecture seule `ThreatManager.OnCriticalLockdown` (`Seizure`), `VictorySystem.OnVictory` (`Victory`) et les paliers d'extraction (`TraceTier1..3`, un par palier de `CleanExitTiers`, le dernier resservant s'il en manque).
  - **Les paliers s'écoutent sur `TraceReadout.LastKnownFraction`, jamais sur la vraie Trace** : une alerte sur la vérité trahirait le brouillard du capteur. Elle sonne quand le palier s'allume à l'écran.
  - Pas d'alerte quand la simulation est retenue (`ITimeSource.DeltaTime` nul) : le recalage du relevé à la restauration d'une sauvegarde n'est pas un franchissement.
  - Plusieurs paliers d'un coup → seul le plus haut sonne ; à 100 % la saisie parle seule ; un palier verrouillé (Extraction Haut Risque non achetée) ne s'annonce pas ; un palier refranchi après une baisse (Data Wiper, nouvelle run) se signale à nouveau.
- Sans `AudioLibrary` ou `UiSoundChannel` sur le Root, le jeu démarre muet (`SilentMusicPlayer`) avec un avertissement.

**Clic des boutons** — `UiClickSound` (`UI/Theme`) sur chaque `Button`, branché sur `onClick` : le son suit l'action (souris, clavier, manette), jamais un bouton désactivé. Il passe par un **asset canal**, `Data/Audio/UiSoundChannel.asset` (`UiSoundChannelSO`), et non par l'injection : les prefabs de boutons sont hors de portée fiable de VContainer (cf. `autoInjectGameObjects`). Un champ `_soundSetOverride` permet de donner une autre famille à un bouton.

**Refus** — le même composant joue `Refusal` quand on clique un bouton grisé (`IsInteractable`, donc CanvasGroup parent compris). L'état est relevé à l'**appui** : un bouton qui se grise en réponse à son propre clic ne joue que son clic. Souris uniquement (la navigation clavier/manette saute les boutons grisés). Case `_refusalSound` pour l'exclure bouton par bouton.

- **Tout nouveau bouton** : relancer `Tools/Core/Audio/Équiper les boutons du son de clic` (idempotent : prefabs de `Assets/Data`, puis scènes du Build Settings ; un objet issu d'un prefab est traité dans son prefab). `Vérifier le son de clic des boutons` fait le même parcours sans rien modifier.
- Les toggles et sliders n'existent pas dans le projet : onglets et sélecteurs d'options sont des `Button`. Un futur `Toggle` ou `Slider` demandera d'étendre le composant.

## Palettes (daltonisme)

Les couleurs qui **portent un sens** (états des nœuds et des liens, paliers de la jauge, titre de fin de run, urgence, étiquettes de la console) viennent de `UiPaletteSO`, une par mode de vision (`GameData/Palettes/` : Standard, Protanopia, Deuteranopia, Tritanopia), réunies dans `UiPaletteCatalogSO` sur le `RootLifetimeScope`. `IPaletteService.Current` suit l'option `ColorVision` de `settings.json` ; les presenters s'y abonnent et repeignent à chaud, comme pour la langue.

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

*Réécrite le 2026-10-13 pour la refonte « Time Attack » (jauge fixe à 10 000 UT, plus aucune
Trace générée par les upgrades). Les formules et leurs raisons sont dans `game-design.md` ; ici,
qui appelle qui.*

```
ScriptCycleRunner (ITickable, Root)
   → par Script : progression du cycle → versement à échéance → Money (+ cumul de la run)

ThreatManager (IStartable, Root) — la vitesse de l'A.M.I., en cache
   → CombineLatest(TFlops BRUTES, Σ Blindage) → V_AMI = 33 × (1 + 0,05 × brutes)^0,10 ÷ (1 + Σ)
   → UpgradeManager.OnProxyWipe (secondes) → Wipe = secondes × V_AMI × (1 + Σ Interception)

SimulationTicker (ITickable, Scene) — si la run est active et la traque armée
                                      (première Data volée, ou Hardware possédé)
   → ThreatManager.Advance(Δt)                 jauge += V_AMI × Δt, saisie à 10 000 UT

EmergencyProtocolSystem (Root) — clic sur le Data Wiper, trois fois par run au plus
   → ThreatManager.Wipe(2 500 UT)
   → UpgradeManager.SetTFlopsSacrificedFraction(30 / 60 / 90 %)   TFlops NETTES seules

OverclockSystem (Scene) — appui sur Espace ou sur le bouton
   → ScriptCycleRunner.AdvanceAllActive(0,5 s + Σ Overclock) ; rien ne tourne → ni effet ni coût
   → ThreatManager.ApplyOverclockPenalty()     jauge += V_AMI × 5 s

TraceReadout (Root) — échantillonne la vérité à intervalle variable : ce que l'UI affiche

Fin de run, deux chemins vers GameSessionManager.ResolveRunEnd :
   ThreatManager à 100 %  → OnCriticalLockdown → HandleGameOver   (saisie : contribution × 30 %)
   bouton « Terre Brûlée » → ExfiltrationSystem → sortie propre    (contribution × bonus de palier)
        → UserCurrencies.BankRunContribution → points par le modèle des boîtes
        → wipe de la run, PUIS OnSessionEnded → GameOverPresenter + SaveScheduler
        → le bilan reste en attente (TryGetPendingRunEnd) jusqu'à « Nouvelle intrusion » :
          sauvegardé (v12), il rouvre l'écran de fin au chargement (GameOverPresenter.PostStart)
```

Le wipe a lieu **avant** la notification, et ce n'est pas un détail : la sauvegarde de fin de run
s'écrit sur `OnSessionEnded`. Si le wipe attendait le clic sur « Recommencer », fermer le jeu sur
l'écran de fin sauvegarderait les générateurs au niveau max avec zéro Trace — une run gratuite.

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

`SimulationTicker` et `ScriptCycleRunner.Tick()` sont les deux boucles de frame (le premier n'y fait plus qu'une multiplication et une addition depuis la refonte « Time Attack »). Elles sont écrites sans allocation, sans closure et sans boxing — tableaux de structs parcourus par index, lectures de `CurrentValue`, garde d'entrée sur `IsGameActive`. **C'est le standard à tenir pour tout nouveau code de frame.**

## Dette structurelle connue

*Le suivi de cette dette, avec le reste, vit dans `backlog.md` §§ 5 et 7.*

- **Aucun `.asmdef`** : tout `Assets/Core` recompile avec le reste du projet, et rien n'empêche une dépendance de `Models` vers `UI`.
- **Namespaces ≠ dossiers** : `Services/GameSceneLifetimeScope.cs` déclare `Core.Boot` ; `Models/Economy/*CatalogSO.cs` déclarent `Core.Economy.Data` ; `Services/LogObjectPool.cs` déclare `Core.Services.Console` ; `SteamManager` est dans le namespace global. Deux dossiers pour la sauvegarde (`Services/Persistence` et `Services/Save`).
- **Dossier fantôme** `UI/Uppgrades` (faute de frappe, vide) à côté de `UI/Upgrades`.
- **`SteamManager`** est un Singleton statique (boilerplate Steamworks.NET du domaine public — ne pas le modifier). L'appel statique `SteamManager.Initialized` depuis `SteamCloudSaveService` doit être encapsulé derrière un `ISteamRuntime` injecté.
