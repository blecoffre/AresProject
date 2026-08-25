# Backlog — reste de l'audit du 2026-08-25

Rapport d'audit complet et priorisé : https://claude.ai/code/artifact/fa8276c0-1c93-4757-aabb-89716bad7dcf

## À faire par Bertrand dans Unity (bloque le lot 2a)

- [ ] **Prefab de générateur** : câbler un `Button` sur le nouveau champ `_runButton` de `GeneratorView`. Sans lui, un Script non automatisé est injouable — un warning le signale au lancement.
- [ ] **Clé de loc `UI_GENERATES_DATAS`** : reçoit désormais 2 arguments pour les Scripts (`{0}` versement, `{1}` durée en secondes).
- [ ] **Supprimer `Assets/Core/Services/Economy/AutomationSystem.cs`** — plus aucune référence ni enregistrement. À supprimer depuis Unity pour que le `.meta` parte avec.
- [ ] Relancer `Tools/Core/Générer Catalogue Upgrades depuis JSON` (le générateur recopie enfin les durées de cycle).

## Bloquants restants

- **Les TFlops ne sont produites par personne.** `AddComputerPower` et `TotalTFlopsYieldPerSecond` n'ont aucun consommateur : l'onglet Hardware coûte de l'argent, génère de la Trace et ne rapporte rien. → lot 2b, formules dans `game-design.md`.
- **Chaque nœud de prestige est instancié deux fois.** `PrestigePanelPresenter.Start()` contient deux boucles `foreach (_catalog.GetAllUpgrades())` qui appellent chacune `SpawnNode()`. Le premier lot n'est jamais positionné et double les abonnements.
- **Les liens de l'arbre sont tracés dans deux repères différents.** `line.DrawLine(parentPos, config.UiPosition)` : `parentPos` est en pixels (× `_gridCellSize`), `config.UiPosition` en coordonnées de grille. Utiliser `nodePositions[config.Id]`.

## Majeurs restants

- **Six bonus de prestige sur sept n'ont aucun effet.** `GlobalComputeMultiplier`, `TraceReductionMultiplier`, `CostMultiplierReduction`, `StartingMoney`, `StartingComputerPower`, `IsEmergencyUnlocked` : calculés, aucun consommateur. Seul `ClickPowerMultiplier` atteint le gameplay. `TryPurchaseUpgrade` appelle `GetCurrentCost()` sans passer la réduction.
- **Les trois bonus `SpecificUpgrade*` ne sont pas traités** par le `switch` de `RecalculateBonuses()`, alors que les nœuds existent dans les données (`P_UPG_*_COST` / `_PROD` / `_TIME`) et sont achetables.
- **Le nœud de prestige d'automatisation n'existe pas encore** dans les données. Prévu ciblé (`TargetUpgradeId`), achetable par rangs, abaissant `AutomationLevel`. Hook prêt : `UpgradeModel.SetAutomationThresholdReduction()`.
- **Le Bouton d'Urgence n'a aucune UI.** `EmergencyProtocolSystem.TryTriggerEmergency()` n'a aucun appelant — la mécanique de Risk/Reward du GDD n'est pas jouable. (Sa portée `Scoped` erronée est corrigée.)
- **La révélation progressive est neutralisée.** `SpawnAndBindGenerator` fait `SetVisible(true)` sans condition et `GeneratorPresenter.SetVisibility()` n'a aucun appelant.
- **`ResetSession()` laisse de l'état derrière** : ne remet à zéro ni `TotalMoneyGenerated` (qui pilote `CalculatePendingCpuCycles`), ni le compteur d'urgence côté modèle. Et `HandleGameOver` remet la jauge à 0 juste après avoir notifié l'UI, donc elle retombe visuellement pendant l'écran de fin.

## Cycle de vie restant

- `GameOverPresenter.HandleRestart` : `_ = _sceneLoader.LoadGameSceneAsync(CancellationToken.None)` — manque `.Forget()`, et `CancellationToken.None` rompt la chaîne d'annulation.
- `GameOverPresenter._cts` : créé, disposé, jamais utilisé.
- `PrestigePanelView._resolver` et `UpgradePanelView._resolver` : `IObjectResolver` injectés et jamais lus — alors qu'ils régleraient justement le Service Locator d'`AutoInjectOnInstantiate`.

## Performance restante

- **`PrestigeItemPresenter`** s'abonne à `CpuCycles` pour chaque nœud et `RefreshView()` refait un `Math.Pow` et alloue 3-4 chaînes **par nœud**. Avec ≈ 170 nœuds, c'est ≈ 680 allocations par variation de CPU Cycles. Appliquer le même traitement qu'à `GeneratorPresenter` (cache + `DistinctUntilChanged`).
- **`ILocalizationService.GetText<T0>`** promet « Zero Boxing » mais l'implémentation fait `string.Format(string, object)`, qui boxe chaque type valeur. → `ZString.Format<T0>` (écosystème Cysharp, déjà présent avec UniTask).
- **`HeaderView`** : `$": {formattedValue}"` par-dessus `CurrencyFormatter.Format` qui alloue déjà. `UpdateTraceDisplay` montre la bonne pratique avec `SetText("{0:F1}%", …)` — à généraliser.
- **`LogObjectPool`** : `_activeItems[0]` + `RemoveAt(0)` et un `Contains` linéaire. Un tampon circulaire rendrait le recyclage O(1).
- Le rendu de l'arbre de prestige à ≈ 170 nœuds demandera du pooling ou de la virtualisation.

## Hygiène restante

- **Aucun `.asmdef`.** Proposition : `Ares.Core.Domain` (Models + Services, sans référence UI), `Ares.Core.Presentation` (UI + Boot), `Ares.Core.Editor`. Prérequis aux tests EditMode.
- **Aucun test.** `UpgradeModel`, `PrestigeManager`, `ThreatManager`, `CurrencyFormatter`, `UserCurrencies` ne touchent presque pas Unity — c'est le meilleur retour sur investissement du projet, surtout pour des formules qu'on va modifier cent fois pendant l'équilibrage.
- **Namespaces ≠ dossiers** et dossier fantôme `UI/Uppgrades` (voir `architecture.md`).
- **`SteamCloudSaveService` appelle `SteamManager.Initialized`** en statique → encapsuler derrière un `ISteamRuntime` injecté.
- **`AutoInjectOnInstantiate`** fait un `LifetimeScope.Find` dans `Awake()` : Service Locator déguisé. `_resolver.Instantiate(prefab, parent)` injecte l'arbre entier et rend ce composant inutile.
- **Français en dur** : `PrestigeItemPresenter` (`$"Niv. {n} / {max}"`, `" CPU"`), `ConsoleView.FormatMessage` (tags `[OK]` / `[INFO]` et couleurs).
- **`Currency.Add()`** est public et sans garde sur les valeurs négatives — les gardes sont dans `UserCurrencies`, mais `Currency` est exposé.
- **`HeaderPresenter`** s'abonne dans son constructeur ET dans `Start()`.
- **Équilibrage éparpillé en constantes** : `÷100` dans `SimulationTicker`, `500 / ×3 / −20 %` dans `EmergencyProtocolSystem`, `√(total/1000)` dans `UserCurrencies`, `(600,300)` dans `PrestigePanelPresenter`. → un `BalancingConfigSO` enregistré au Root.

## Corrigé à ce jour

Persistance complète (autosave, fermeture, événements, écriture atomique, versionnement, prestige enfin persisté) · moteur de cycles par Script · paliers et automatisation · `EmergencyProtocolSystem` en Singleton · `UpgradeManager` / `PrestigeManager` / `ThreatManager` passés `IDisposable` · `UpgradeModel` disposés à la reconstruction · désabonnement lambda de `UpgradePanelPresenter` · `CancellationToken` sur la localisation · LINQ retiré du runtime · cache de coût et `DistinctUntilChanged` sur les générateurs · log d'achat · valeur d'overclock réelle · encodage UTF-8 de `GameSceneLifetimeScope` · recopie des durées de cycle dans le générateur d'éditeur.
