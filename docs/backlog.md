# Backlog — reste de l'audit du 2026-08-25

_Complété le 2026-08-25 par une seconde passe de vérification : les entrées annotées « Précision » ou « Confirmé » et les six constats nouveaux (localisation, paliers sans données, placeholders de prestige, ordre du reset de fin de run, assets orphelins, remote Git) en sont issus._

Rapport d'audit complet et priorisé : https://claude.ai/code/artifact/fa8276c0-1c93-4757-aabb-89716bad7dcf

## À faire par Bertrand dans Unity (bloque le lot 2a)

- [ ] **Prefab de générateur** : câbler un `Button` sur le nouveau champ `_runButton` de `GeneratorView`. Sans lui, un Script non automatisé est injouable — un warning le signale au lancement.
- [ ] **Clé de loc `UI_GENERATES_DATAS`** — ⚠️ **l'entrée d'origine était incomplète.** `GeneratorPresenter.BuildStatsText()` appelle la **même clé avec deux arités** : 2 arguments pour un Script (versement + durée), 1 seul pour un Hardware ou un Proxy. Ajouter `{1}` à la valeur ferait donc **lever une `FormatException`** sur les onglets Hardware et Proxy. Il faut **deux clés distinctes** (`UI_GENERATES_DATAS_CYCLE` pour les Scripts, `UI_GENERATES_DATAS` pour les autres) et le `if (_isScript)` de `BuildStatsText()` doit choisir laquelle. C'est un changement de code, pas seulement de données.
- [ ] **Supprimer `Assets/Core/Services/Economy/AutomationSystem.cs`** — plus aucune référence ni enregistrement. À supprimer depuis Unity pour que le `.meta` parte avec.
- [ ] Relancer `Tools/Core/Générer Catalogue Upgrades depuis JSON` (le générateur recopie enfin les durées de cycle). **Confirmé non fait au 2026-08-25 :** `SCR_01.asset` porte `_baseCycleDuration: 1` (JSON : `1.5`), `_minCycleDuration: 0.1` (JSON : `0.2`) et surtout `_displayName:` **vide**. C'est l'action au meilleur rapport effet/effort du moment — elle corrige les durées *et* les noms d'un coup.

## Bloquants restants

- **Les TFlops ne sont produites par personne.** `AddComputerPower` et `TotalTFlopsYieldPerSecond` n'ont aucun consommateur : l'onglet Hardware coûte de l'argent, génère de la Trace et ne rapporte rien. → lot 2b, formules dans `game-design.md`.
- ~~**Aucun nom d'objet du jeu ne s'affiche correctement.**~~ Réglé pour les upgrades (lot Localisation A). Reste l'affichage des nœuds de prestige spécifiques, traité au lot B.
- **Ancien constat, conservé pour mémoire :** `GeneratorPresenter` et `PrestigeItemPresenter` traitent le nom comme une **clé de localisation** (`_loc.GetText(Config.DisplayName)`), mais les données portent du texte français en dur : `scripts.json` a `"displayName": "Phishing familial"`, `04_SpecificUpgrades.json` a `"nameKey": "Test001 (Opti Coût)"`. Résultat à l'écran : `[]` aujourd'hui (asset vide), `[Phishing familial]` après régénération. `fr.json` ne contient que **19 clés** au total, dont 2 noms d'upgrades. → **Décision de design requise** : vraies clés dans les données + 214 entrées dans `fr.json`, ou texte direct et retrait du `GetText` sur les noms.
- **Clés de lore des 7 nœuds de prestige réels non définies** dans `fr.json` : `UI_PRESTIGE_{ROOT,REFAC,DATAM,CLICK,POWER,STEALTH,EMERG}_{NAME,DESC}`. Contenu à écrire par Bertrand (ton du GDD), pas à inventer. Les 4 clés purement fonctionnelles (`UI_MAX_LEVEL`, `UI_ACQUIRED`, `UI_PRESTIGE_LEVEL`, `UI_PRESTIGE_COST`) sont ajoutées.
- **`fr.json` contient encore de l'anglais** sur les libellés de base (`MONEY` → « Money », `COMPUTER_POWER` → « Computer Power », `CYCLES`, `CONSOLE_LOGS`, `OVERCLOCK`) : ce sont des valeurs de remplissage, pas des traductions.
- **La sauvegarde de fin de run capture un état à moitié réinitialisé.** `HandleGameOver()` remet à zéro les monnaies, l'urgence et la Trace, puis émet `OnSessionEnded` — sur lequel le `SaveScheduler` écrit immédiatement. Mais les **niveaux d'upgrades ne sont effacés que par `ResetSession()`**, qui n'arrive qu'au clic sur *Restart*. Si le joueur ferme le jeu sur l'écran de Game Over, il rouvre avec tous ses générateurs au niveau max, zéro argent et zéro Trace : une run gratuite. C'est un problème d'**ordre** entre le reset et l'écriture, distinct de l'entrée « `ResetSession()` laisse de l'état derrière » ci-dessous.

## Majeurs restants

- **Six bonus de prestige sur sept n'ont aucun effet.** `GlobalComputeMultiplier`, `TraceReductionMultiplier`, `CostMultiplierReduction`, `StartingMoney`, `StartingComputerPower`, `IsEmergencyUnlocked` : calculés, aucun consommateur. Seul `ClickPowerMultiplier` atteint le gameplay. `TryPurchaseUpgrade` appelle `GetCurrentCost()` sans passer la réduction.
- **Les trois bonus `SpecificUpgrade*` ne sont pas traités** par le `switch` de `RecalculateBonuses()`, alors que les nœuds existent dans les données (`P_UPG_*_COST` / `_PROD` / `_TIME`) et sont achetables.
- **Le nœud de prestige d'automatisation n'existe pas encore** dans les données. Prévu ciblé (`TargetUpgradeId`), achetable par rangs, abaissant `AutomationLevel`. Hook prêt : `UpgradeModel.SetAutomationThresholdReduction()`.
- **Le Bouton d'Urgence n'a aucune UI.** `EmergencyProtocolSystem.TryTriggerEmergency()` n'a aucun appelant — la mécanique de Risk/Reward du GDD n'est pas jouable. (Sa portée `Scoped` erronée est corrigée.)
- **La révélation progressive est neutralisée.** `SpawnAndBindGenerator` fait `SetVisible(true)` sans condition et `GeneratorPresenter.SetVisibility()` n'a aucun appelant.
- **`ResetSession()` laisse de l'état derrière** : ne remet à zéro ni `TotalMoneyGenerated` (qui pilote `CalculatePendingCpuCycles`), ni le compteur d'urgence côté modèle. Et `HandleGameOver` remet la jauge à 0 juste après avoir notifié l'UI, donc elle retombe visuellement pendant l'écran de fin.
- **Le système de paliers ne pilote aucune donnée.** `UpgradeMilestone`, le cache de `UpgradeModel` et le tri à la génération sont livrés et propres, mais **aucun des trois JSON ne contient un seul `milestones` ni un seul `automationLevel`**. Les 45 générateurs tournent en rendement linéaire avec un seuil d'automatisation à 10 par défaut : la mécanique décrite dans `cycles.md` n'est pas testable en l'état. → écrire les paliers des 15 Scripts en priorité.
- **162 des 169 nœuds de prestige sont des placeholders de test.** `04_SpecificUpgrades.json` est une génération automatique : noms `Test001`, 6 nœuds à `baseCost: 0.0` (donc gratuits), un `targetUpgradeId: "000"` qui ne correspond à rien, et **24 nœuds ciblant 8 upgrades supprimés** (`SCR_BOTNET`, `SCR_KEYLOGGER`, `SCR_SPIDER`, `HW_MAINFRAME`, `HW_PI_ZERO`, `HW_RIG_GPU`, `PRX_VPN`, `PRX_DARKWEB`). Le compte de ≈ 170 nœuds du GDD est atteint, mais le contenu est du remplissage.

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
- **Français en dur** : reste `ConsoleView.FormatMessage` (tags `[OK]` / `[INFO]` et couleurs). ~~`PrestigeItemPresenter`~~ et ~~`PrestigeItemView` (`"MAX"`)~~ sont faits.
- **`Currency.Add()`** est public et sans garde sur les valeurs négatives — les gardes sont dans `UserCurrencies`, mais `Currency` est exposé.
- **`HeaderPresenter`** s'abonne dans son constructeur ET dans `Start()`. Même écart dans **`PrestigeItemPresenter`**, qui s'abonne entièrement dans son constructeur — il est construit par `new` et non par le conteneur, donc rien ne casse aujourd'hui, mais c'est la même règle d'`architecture.md`.
- **8 `.asset` orphelins** dans `Assets/GameData/Upgrades/` : 53 fichiers pour 45 entrées JSON (`SCR_BOTNET`, `SCR_KEYLOGGER`, `SCR_SPIDER`, `HW_MAINFRAME`, `HW_PI_ZERO`, `HW_RIG_GPU`, `PRX_VPN`, `PRX_DARKWEB`). Ils sont **hors catalogue**, donc inoffensifs au runtime — mais `UpgradeCatalogGenerator` ne supprime jamais les assets dont l'id a disparu du JSON, donc ça s'accumulera à chaque renommage.
- **Faux libellé sur les onglets Hardware et Proxy** : `GeneratorPresenter.BuildStatsText()` affiche « Génère X **Datas** » pour tous les types, alors qu'un Hardware fournit des TFlops et qu'un Proxy ne produit rien. À traiter avec le lot 2b (TFlops), dont dépend le vocabulaire.
- **Séparateur décimal dépendant de la machine** : `GetCurrentCycleDuration().ToString("0.##")` utilise `CurrentCulture` — « 1,5 s » en français, « 1.5 s » ailleurs — alors que `CurrencyFormatter` force `InvariantCulture`. Trancher une politique unique et l'appliquer partout.
- **3 warnings `CS0618`** à la compilation : `LocalizationAnalyzerWindow.cs:120` et `LocalizationAutoInjectorEditor.cs:38,46` utilisent `FindObjectOfType` / `FindObjectsOfType`, dépréciés en Unity 6 au profit de `FindFirstObjectByType` / `FindObjectsByType(FindObjectSortMode.None)`. Code éditeur uniquement, aucun impact runtime.
- **Aucun remote Git.** Le dépôt n'existe que sur `H:\` — un commit local ne protège pas d'une panne disque. Accessoirement : pas de Git LFS (34 binaires / ≈ 5 Mo aujourd'hui, donc sans urgence, mais la mise en place se fait *avant* que l'art arrive), et `.gitattributes` sans `merge=unityyamlmerge` sur `*.unity` / `*.prefab` — indispensable dès qu'on travaillera sur des branches.
- **Équilibrage éparpillé en constantes** : `÷100` dans `SimulationTicker`, `500 / ×3 / −20 %` dans `EmergencyProtocolSystem`, `√(total/1000)` dans `UserCurrencies`, `(600,300)` dans `PrestigePanelPresenter`. → un `BalancingConfigSO` enregistré au Root.

## À rédiger par Bertrand — contenu, pas code

Ces clés sont **volontairement absentes** de `fr.json` : elles s'afficheront entre crochets et
seront signalées par le `LocalizationAnalyzerWindow` tant qu'elles ne sont pas écrites.

- [ ] **45 descriptions d'upgrades** — `UPG_<id>_DESC` (ex. `UPG_SCR_01_DESC`). Les 45 noms sont déjà migrés.
- [ ] **7 noms + 7 descriptions de nœuds de prestige** — `PRESTIGE_<id>_NAME` / `_DESC` pour
      `P_ROOT`, `P_REFAC`, `P_DATAM`, `P_CLICK`, `P_POWER_START`, `P_STEALTH`, `P_EMERG`.
      Aucun texte français n'a jamais existé pour ces nœuds : il n'y avait rien à migrer.
- [ ] **Valeurs de remplissage en anglais** dans `fr.json` : `MONEY` → « Money »,
      `COMPUTER_POWER` → « Computer Power », `CYCLES`, `CONSOLE_LOGS`, `OVERCLOCK`.

## Corrigé à ce jour

**Lot « Localisation B — affichage » (2026-08-25, vérifié en Play Mode via MCP)** — `PrestigeItemPresenter` compose nom et description des 162 nœuds ciblés à partir des 6 gabarits et de la clé de l'upgrade cible, sans dépendance nouvelle : la clé se dérive de `TargetUpgradeId`. La composition a lieu **une seule fois dans le constructeur**, pas dans `RefreshView()`. Un nœud « spécifique » sans `targetUpgradeId` déclenche une erreur explicite. `GeneratorView` gagne un `_descriptionText` facultatif, alimenté depuis `DisplayDescriptionKey`. **Bug d'arité corrigé** : les Scripts utilisent désormais `UI_GENERATES_DATAS_CYCLE` (2 arguments), les Hardware/Proxy gardent `UI_GENERATES_DATAS` (1 argument) — ajouter `{1}` à la clé commune aurait levé une `FormatException` sur deux onglets sur trois. Vérifié : 3 générateurs affichent « Phishing familial », « Overclocking du CPU familial », « Navigation Privée » ; 162 nœuds composés (« Effacement Temporel (Opti Temps) ») ; 7 nœuds réels en `[PRESTIGE_*_NAME]`, ce qui est le comportement attendu tant que leur lore n'est pas écrit ; zéro exception.

**Lot « Localisation A — socle » (2026-08-25)** — la convention de nommage devient exécutable via `LocalizationKeys` (`Core.Services.Localization`), point unique dont dépendent les deux générateurs. `UpgradeConfigSO._displayName` devient `_displayNameKey` (avec `FormerlySerializedAs`) et gagne `_displayDescriptionKey`. Les trois générateurs d'éditeur **dérivent** désormais les clés de l'id au lieu de les lire depuis le JSON — une désynchronisation donnée/traduction devient impossible. Les nœuds de prestige « spécifiques » reçoivent des clés vides : leur libellé est composé à l'affichage (lot B) à partir de 6 gabarits, au lieu de 324 entrées de traduction. Migration unique des 45 libellés français vers `fr.json`, et purge de tout texte affichable des JSON de `GameData` (45 champs `displayName`, 169 paires `nameKey`/`descKey`). Vérifié : compilation sans erreur, générateurs relancés (45 upgrades / 169 nœuds), assets porteurs des clés dérivées, zéro français résiduel dans les `.asset`, 74 clés chargées au boot.

**Lot « panneau de prestige » (2026-08-25, vérifié en Play Mode via MCP)** — ⚠️ **piège trouvé au test, à retenir : Unity n'appelle jamais `Awake()` sur un objet inactif dans la hiérarchie.** L'arbre est construit alors que `Canvas/PrestigeTree` est encore désactivé, donc `UILineConnection._rectTransform` et `PrestigeItemView._rectTransform`, résolus dans `Awake()`, restaient `null`. Conséquences : `DrawLine` levait une `NullReferenceException` au premier lien — ce qui interrompait `Start()` et donc tout l'arbre — et `SetNodePosition` était un no-op silencieux laissant les 169 nœuds empilés à l'origine. Les deux passent en **résolution paresseuse** (propriété `Rect`), jamais dans `Awake`. Tout composant dont une méthode est appelée pendant la construction d'un panneau masqué doit suivre ce modèle. Résultat vérifié : 169 nœuds, 168 liens, positions et rotations correctes, zéro exception. — double instanciation des nœuds supprimée (169 au lieu de 338) · repère des liens unifié en pixels via `PrestigePanelView.GridToPixels` · placement déplacé dans la vue (`SpawnNode(Vector2)`), un nœud ne peut plus rester à la position du prefab · `_gridCellSize` sorti en `[SerializeField]` · repli `GetComponent<RectTransform>()` dans `PrestigeItemView.Awake` · lambda `onClick` remplacée par une méthode nommée · `"MAX"` et `$"Niv. {n} / {max}"` passés en clés de loc · erreur explicite sur prérequis absent du catalogue · `LOG_MANUAL_OVERCLOCK` annonçait des `%` alors que le code passe des secondes.

Persistance complète (autosave, fermeture, événements, écriture atomique, versionnement, prestige enfin persisté) · moteur de cycles par Script · paliers et automatisation · `EmergencyProtocolSystem` en Singleton · `UpgradeManager` / `PrestigeManager` / `ThreatManager` passés `IDisposable` · `UpgradeModel` disposés à la reconstruction · désabonnement lambda de `UpgradePanelPresenter` · `CancellationToken` sur la localisation · LINQ retiré du runtime · cache de coût et `DistinctUntilChanged` sur les générateurs · log d'achat · valeur d'overclock réelle · encodage UTF-8 de `GameSceneLifetimeScope` · recopie des durées de cycle dans le générateur d'éditeur.
