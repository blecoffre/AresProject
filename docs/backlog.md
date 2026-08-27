# Backlog — audit du 2026-08-27

Audit complet refait à cette date. Le précédent (2026-08-25) contenait au moins une entrée fausse
propagée sans vérification, alors qu'elle décrivait un **comportement** et non du code.

**Méthode retenue, à conserver pour les prochains audits.** Chaque entrée porte sa nature :

- 🔬 **vérifié en Play Mode** — le comportement a été observé, pas déduit
- 📖 **lecture de code** — constat statique, vrai par construction (appelant absent, champ non lu…)
- ⚖️ **estimation** — plausible mais non mesuré ; ne pas planifier un lot dessus sans mesurer d'abord

Un « X n'a aucun appelant » est un constat de lecture. « La fonctionnalité est cassée » est un
constat de comportement, et demande le Play Mode. Ne pas confondre les deux : c'est exactement
l'erreur qui avait produit l'entrée fausse sur la révélation progressive.

État de la base : 59 fichiers runtime, 6 fichiers d'éditeur, ~6 600 lignes dans `Assets/Core`.
Compilation sans erreur ni warning.

---

## À faire par Bertrand dans Unity

- [ ] 🔬 **Faute de frappe `COMPTUER_POWER`** sur `Canvas/Header/Currencies/ComputerPower/CurrencyName`.
      La clé définie est `COMPUTER_POWER`. Ce `LocalizedText` n'est en plus **pas dans la liste
      « Auto Inject Game Objects »** du `GameSceneLifetimeScope` — contrairement à ses trois voisins
      `MONEY`, `TRACE` et `CYCLES` — donc il affiche en permanence le texte du prefab, `CURRENCY:`.
      Deux corrections : la clé, et l'ajout à la liste.
- [ ] 🔬 **`LocalizedText` parasite sur le bouton d'exfiltration.**
      `Canvas/ConsoleLogs/PrestigeButton/Text (TMP)` porte un `LocalizedText` de clé `OVERCLOCK`,
      vestige de la duplication du bouton Overclock. Il est inoffensif aujourd'hui parce qu'il n'est
      pas injecté, mais s'il l'était il écraserait le libellé écrit par `ExfiltrationPresenter`.
      À supprimer du GameObject.

## Majeurs restants

- 🔬 **L'Overclock « réveil » n'est pas implémenté.** Vérifié : SCR_01 acheté niveau 1, donc possédé
      et non automatisé ; un `TriggerManualOverclock()` suivi d'un `Tick()` laisse `IsRunning` à faux.
      Or `game-design.md` tranche depuis le 2026-08-26 qu'un clic doit **démarrer tous les Scripts
      possédés à l'arrêt ET avancer ceux qui tournent**. Aujourd'hui il ne fait que la seconde
      moitié. Environ dix lignes dans `OverclockSystem` et `ScriptCycleRunner`.
- 📖 **Le Bouton d'Urgence n'a aucune UI.** `EmergencyProtocolSystem.TryTriggerEmergency()` n'a
      toujours aucun appelant. Le système est vivant — coût de départ 500, compteur d'usages
      persisté — mais la seconde moitié du Risk/Reward du GDD reste injouable, et
      `IsEmergencyUnlocked` est le dernier bonus de prestige sans consommateur.
- 📖 **Le nœud de prestige d'automatisation n'existe pas dans les données.** Prévu ciblé
      (`TargetUpgradeId`), achetable par rangs, abaissant `AutomationLevel`. Le hook est prêt côté
      code : `UpgradeModel.SetAutomationThresholdReduction()`, qui n'a aucun appelant.
- 📖 **Le `SaveScheduler` abandonne silencieusement une écriture** si une autre est en vol
      (`_isWriting`). Le commentaire la justifie par « la prochaine capturera un état plus récent »,
      ce qui ne tient que s'il y en a une prochaine. Constaté pendant un test : trois achats de
      prestige rapprochés suivis d'un Game Over ont fait perdre l'écriture de fin de run. Sans
      conséquence depuis que le wipe précède toute capture, mais un drapeau « écriture en attente »
      serait plus sûr qu'un abandon.

## Données et contenu

- 🔬 **59 clés de localisation manquantes**, toutes dérivées des données : **52 descriptions**
      `UPG_<id>_DESC` et **7 paires** `PRESTIGE_<id>_NAME` / `_DESC` pour les nœuds réels.
      Vérifié à l'écran : 18 clés distinctes s'affichent entre crochets en jeu.
      Aucune clé de code ne manque, et aucune clé définie n'est orpheline côté données.
- 🔬 **6 libellés encore en anglais** dans `fr.json` : `MONEY` → « Money », `COMPUTER_POWER`,
      `TRACE`, `CYCLES`, `CONSOLE_LOGS`, `OVERCLOCK`. Ce sont des valeurs de remplissage.
- 📖 **2 clés mortes** dans `fr.json` : `UPGRADE_NAME_BOTNET` et `UPGRADE_NAME_BREACH_CORE`,
      vestiges d'avant la migration des noms. `CONSOLE_LOGS` n'est référencée ni par le code ni par
      un `LocalizedText` de la scène.
- 🔬 **Le système de paliers ne pilote toujours aucune donnée.** Vérifié : sur 45 upgrades,
      **0 possède `milestones`, 0 possède `automationLevel`**. Les générateurs tournent donc en
      rendement strictement linéaire avec un seuil d'automatisation à 10 par défaut, et la mécanique
      décrite dans `cycles.md` reste non testable. C'est le point où l'écriture de contenu débloque
      le plus de gameplay d'un coup.
- 📖 **L'équilibrage des 105 nœuds spécifiques est du remplissage.** Leur nombre et leur pertinence
      sont corrects depuis le lot 3a, mais les valeurs restent auto-générées : `maxLevel: 5`,
      `costMult: 1.4` et `bonus: 0.1` identiques pour tous, `baseCost = 50 × order`.

## Cycle de vie

- 📖 `GameOverPresenter.HandleRestart` : `_ = _sceneLoader.LoadGameSceneAsync(CancellationToken.None)`
      — il manque le `.Forget()`, et `CancellationToken.None` rompt la chaîne d'annulation.
- 📖 `GameOverPresenter._cts` : créé, disposé, jamais utilisé.
- 📖 `PrestigePanelView._resolver` et `UpgradePanelView._resolver` : `IObjectResolver` injectés et
      jamais lus, alors qu'ils régleraient justement le Service Locator d'`AutoInjectOnInstantiate`.

## Performance

- ⚖️ **`PrestigeItemPresenter`** s'abonne à `CpuCycles` pour chacun des 112 nœuds, et `RefreshView()`
      refait un `Math.Pow` et alloue plusieurs chaînes par nœud. L'ordre de grandeur — quelques
      centaines d'allocations par variation de CPU Cycles — **n'a jamais été mesuré**, seulement
      déduit du code. À profiler avant d'en faire un lot. Le remède, si c'est confirmé, est celui
      déjà appliqué à `GeneratorPresenter` : cache et `DistinctUntilChanged`.
- 📖 **`ILocalizationService.GetText<T0>`** promet « Zero Boxing » mais fait un
      `string.Format(string, object)`, qui boxe chaque type valeur. → `ZString.Format<T0>`,
      déjà disponible avec UniTask.
- 📖 **`HeaderView`** : `$": {formattedValue}"` par-dessus un `CurrencyFormatter.Format` qui alloue
      déjà. `UpdateTraceDisplay` montre la bonne pratique avec `SetText("{0:F1}%", …)`.
- 📖 **`LogObjectPool`** : `_activeItems[0]` + `RemoveAt(0)` et un `Contains` linéaire. Un tampon
      circulaire rendrait le recyclage O(1).
- ⚖️ Le rendu de l'arbre de prestige à 112 nœuds demandera peut-être du pooling ou de la
      virtualisation. Jamais mesuré non plus — l'arbre s'affiche aujourd'hui sans ralentissement
      perceptible en éditeur.

## Hygiène

- 📖 **Aucun `.asmdef`**, aucun test. Tout `Assets/Core` recompile avec le reste, et rien n'empêche
      une dépendance de `Models` vers `UI`. Proposition inchangée : `Ares.Core.Domain`,
      `Ares.Core.Presentation`, `Ares.Core.Editor` — prérequis aux tests EditMode.
      `UpgradeModel`, `PrestigeManager`, `ThreatManager`, `CurrencyFormatter` et `UserCurrencies`
      ne touchent presque pas Unity : c'est le meilleur retour sur investissement du projet,
      surtout pour des formules qu'on modifie sans arrêt.
- 📖 **Code mort confirmé, sans appelant** : `GeneratorPresenter.SetVisibility()`,
      `UpgradeManager.GetAllActiveUpgrades()`, `LogObjectPool.ReturnToPool()`. Et le
      `viewInstance.SetVisible(true)` d'`UpgradePanelPresenter` porte sur un prefab dont la racine
      est déjà active, donc sans effet.
      ⚠️ **Ne pas confondre avec la révélation progressive, qui FONCTIONNE** — 🔬 vérifié :
      45 upgrades au catalogue mais 3 `GeneratorView` instanciés au démarrage, et le suivant
      apparaît à chaque premier achat. Elle passe par l'instanciation à la demande
      (`GetInitiallyVisibleUpgrades` + `OnUpgradeRevealed`), pas par l'activation.
- 🔬 **`AutoInjectOnInstantiate` ne s'exécute pas sur un objet inactif.** Les générateurs
      instanciés dans les onglets masqués ne sont pas injectés et affichent le texte brut du
      prefab (« Button » au lieu de « Acheter »). Vérifié : ça **se rattrape à l'ouverture de
      l'onglet**, donc invisible en pratique — mais c'est la même famille de piège que celui qui
      avait cassé l'arbre de prestige. Le composant fait par ailleurs un `LifetimeScope.Find` dans
      `Awake()`, soit un Service Locator déguisé ; `_resolver.Instantiate(prefab, parent)` injecte
      l'arbre entier et rendrait ce composant inutile.
- 📖 **`SteamCloudSaveService` et `SaveServiceComposite` compilent mais ne sont pas enregistrés**
      (TODO commenté dans `RootLifetimeScope`). `SteamCloudSaveService` appelle
      `SteamManager.Initialized` en statique → à encapsuler derrière un `ISteamRuntime` injecté.
- 📖 **Français en dur** : reste `ConsoleView.FormatMessage`, avec ses tags `[OK]`,
      `[SYSTEM_WARN]`, `[INFO]` et leurs couleurs codées en dur.
- 📖 **3 warnings `CS0618`** dormants : `LocalizationAnalyzerWindow.cs:120` et
      `LocalizationAutoInjectorEditor.cs:38,46` utilisent `FindObjectOfType` / `FindObjectsOfType`,
      dépréciés en Unity 6. Ils ne remontent plus faute de recompilation de ces fichiers, mais le
      code est inchangé. Code éditeur uniquement.
- 📖 **`Currency.Add()`** est public et sans garde sur les valeurs négatives — les gardes sont dans
      `UserCurrencies`, mais `Currency` est exposé.
- 📖 **`HeaderPresenter`** s'abonne dans son constructeur ET dans `Start()`. Même écart dans
      `PrestigeItemPresenter`, qui s'abonne entièrement dans son constructeur.
- 📖 **Namespaces ≠ dossiers** et dossier fantôme vide `Assets/Core/UI/Uppgrades` toujours présent.
- 📖 **Séparateur décimal dépendant de la machine** : `GetCurrentCycleDuration().ToString("0.##")`
      utilise `CurrentCulture` — « 1,5 s » ici, « 1.5 s » ailleurs — alors que `CurrencyFormatter`
      force `InvariantCulture`. Trancher une politique unique.
- 📖 **Faux libellé sur les onglets Hardware et Proxy** : `BuildStatsText()` affiche
      « Génère X **Datas** » pour tous les types, alors qu'un Hardware fournit des TFlops et qu'un
      Proxy dissipe de la Trace.
- 📖 **Pluriel non géré** : `UI_EXFIL_READY` dit « +1 **Cycles** CPU ». Une vraie pluralisation
      demanderait un mécanisme dans `ILocalizationService` ; reformuler la clé suffirait pour
      l'instant.
- 📖 **Équilibrage éparpillé en constantes** : `÷100` dans `SimulationTicker`, `500 / ×3 / −20 %`
      dans `EmergencyProtocolSystem`, `√(RunMoney/1000)` dans `UserCurrencies`, `0,05` et le
      plafond `0,95` dans `UpgradeModel`, `1,2` et `10` dans `GameSessionManager`, `1000` dans
      `ExfiltrationSystem`, `(600,300)` dans `PrestigePanelView`. → un `BalancingConfigSO`
      enregistré au Root. La liste s'allonge à chaque lot : c'est devenu le principal frein à
      l'équilibrage.
- 📖 **Aucun remote Git.** Le dépôt n'existe que sur `H:\`. Accessoirement : pas de Git LFS
      (une trentaine de binaires aujourd'hui, donc sans urgence — mais la mise en place se fait
      *avant* que l'art arrive), et `.gitattributes` sans `merge=unityyamlmerge` sur `*.unity` et
      `*.prefab`, indispensable dès qu'on travaillera sur des branches.

## Pièges à retenir

Trois bugs de la même famille ont déjà coûté du temps. Le motif :

- **Unity n'appelle pas `Awake()` sur un objet inactif dans la hiérarchie.** Tout composant dont une
  méthode est appelée pendant la construction d'un panneau masqué doit résoudre ses références en
  **lazy**, jamais dans `Awake`. C'est ce qui avait cassé l'arbre de prestige, et c'est ce qui rend
  les onglets masqués non injectés.
- **Lancer le Play Mode depuis la GameScene est cassé par construction** : sans `RootScene`,
  `SceneLoader` ne tourne pas, donc pas d'`EnqueueParent`, donc le scope de scène n'a pas de parent
  et aucun service n'est résoluble. Le message d'erreur ne le dit pas. Une garde explicite dans
  `GameSceneLifetimeScope` éviterait le diagnostic à chaque fois.
- **Le player loop de l'éditeur est figé tant que la fenêtre Unity n'a pas le focus**
  (`Time.frameCount` ne bouge pas), et `runInBackground` n'y change rien. Pour vérifier un
  `ITickable` sans focus, appeler `Tick()` à la main.

## Corrigé à ce jour

**Protocole Terre Brûlée — bouton câblé et testé de bout en bout** (2026-08-26) — l'affichage et l'interactivité sont désormais deux notions distinctes : `IsUnlocked` porte la condition de jeu et pilote le libellé et la jauge, `IsClickable` y ajoute la triche d'éditeur et ne pilote que le bouton. Sans cette séparation, l'éditeur affichait en permanence « [PRÊT] Gain : +0 Cycles CPU » — un libellé absurde — et la jauge de progression, masquée une fois débloquée, n'aurait jamais été observable pendant le développement. Vérifié en Play Mode sur le bouton réel : les trois états du GDD (`0 %`, `45 %`, `[PRÊT] +1 → Prochain à 4K`, `[PRÊT] +2 → Prochain à 9K`), le blocage des clics et le vidage du libellé pendant la séquence, les six lignes de purge rendues dans la console du jeu dans l'ordre, l'écran de fin qui n'apparaît qu'après, 2 cycles crédités, la run wipée et la sauvegarde écrite.

**Lot « 4b — Protocole Terre Brûlée, l'UI »** (2026-08-26, vérifié en Play Mode via MCP) — `ExfiltrationView` autonome (pas un champ de plus sur le Header, pour pouvoir déplacer le bouton sans toucher au code) et `ExfiltrationPresenter` qui porte les trois états et la séquence de purge. **La fin de run est découpée en deux temps** : `TryResolveVoluntaryExit` fige le résultat — gain crédité, run effacée, partie désarmée — puis `AnnounceRunEnded` déclenche l'écran de fin une fois la console déroulée. Jouer la séquence avant de figer aurait laissé la Trace monter pendant ~2 s : un joueur exfiltrant à 98 % pouvait se faire saisir au milieu de sa propre sortie et perdre ses +20 %. **Défaut trouvé au test et corrigé** : `HandleGameOver` pouvait s'exécuter sur une run déjà résolue, le `ThreatManager` émettant son lockdown sans savoir que la partie était finie — une saisie forcée pendant la séquence écrasait l'Effacement Propre et affichait l'écran par-dessus. Garde `if (!IsGameActive) return` ajoutée. Rafraîchissement du libellé filtré sur l'ENTIER de pourcent, sinon une chaîne serait allouée à chaque versement de cycle. La vue est enregistrée sous condition : sans elle, un warning explicite au lieu d'un boot cassé. Vérifié : temps 1 fige 18 cycles et laisse l'écran caché ; une saisie forcée pendant la séquence ne produit ni écran ni détection ; temps 2 affiche l'écran ; le gain est conservé.

**Lot « 4a — Protocole Terre Brûlée, la mécanique »** (2026-08-26, vérifié en Play Mode via MCP) — la sortie volontaire de run. Jusqu'ici seule la Trace à 100 % menait à l'écran de prestige : boucler la méta-progression imposait d'attendre de se faire prendre. `GameSessionManager.EndRun(multiplicateur)` factorise les deux fins ; `HandleGameOver` passe 1,0 et enregistre une détection, `TryEndRunVoluntarily` passe 1,2 et n'en enregistre pas — le joueur n'a jamais été pris. `ExfiltrationSystem` (scope racine) porte l'état réactif : `PendingCycles` avec `DistinctUntilChanged` — indispensable, l'argent bouge plusieurs fois par seconde quand l'entier ne change que quelques fois par run —, `IsUnlocked`, `ProgressToFirstCycle` et `GetNextCycleThreshold()`. Condition de déblocage unique : ≥ 1 CPU Cycle, soit 1 000 Datas de run. Contournement `#if UNITY_EDITOR || DEVELOPMENT_BUILD` pour enchaîner des runs de test. Vérifié : jauge 0 → 25 → 70 → 100 % puis 1 cycle pile à 1 000 Datas ; seuils suivants à 4 000 et 9 000 ; 250 000 Datas rapportent **18 cycles en sortie volontaire contre 15 en saisie** ; la run est wipée pareillement dans les deux cas ; `TryExfiltrate` sur une run terminée retourne false.

**Prestige calculé sur la run** (2026-08-26, vérifié en Play Mode via MCP) — décision de game design : le gain de CPU Cycles se base sur l'argent gagné pendant LA RUN, plus sur le cumul à vie. `UserCurrencies` gagne `RunMoneyGenerated`, remis à zéro par `WipeRun()` — après le calcul du gain, l'ordre compte. `SaveData` passe en **v3** avec le champ `RunMoney` dans le bloc de run, et la migration v2→v3 le force à zéro plutôt que de recopier le cumul, ce qui offrirait un gain immérité sur une run déjà entamée. Le compteur à vie reste, comme statistique. Vérifié : run à 250 000 $ → 15 cycles, compteur de run remis à 0 au Game Over, cumul à vie conservé ; run suivante à 4 000 $ → **2 cycles**, contre 15 sur l'ancien calcul.

**Lot « 3b — branchement des bonus de prestige »** (2026-08-26, vérifié en Play Mode via MCP) — les six bonus orphelins trouvent leurs consommateurs. `PrestigeManager` agrège les trois `SpecificUpgrade*` dans une table `Dictionary<string, SpecificUpgradeBonuses>` indexée par cible — les scalaires globaux ne pouvaient pas porter un bonus ne valant que pour `SCR_01` — et expose `OnBonusesRecalculated`, émis après CHAQUE recalcul. Ce signal est indispensable : `OnPrestigePurchased` n'est pas émis par `InitializeFromSave`, et le gateway restaure les générateurs AVANT le prestige, donc s'y abonner aurait fait perdre toute la méta-progression ciblée au chargement. Les bonus sont poussés dans les `UpgradeModel` (motif de `SetTFlops`), appliqués aux valeurs de BASE, avant les paliers, les TFlops et le plancher ; composition **additive**, bornée à 95 % pour qu'un générateur ne devienne jamais gratuit. Le PROD d'un Proxy boost sa DISSIPATION et surtout pas sa trace générée. `GlobalComputeMultiplier` s'applique aux TFlops, `CostMultiplierReduction` est enfin transmise par `TryPurchaseUpgrade`, `StartingMoney` s'AJOUTE à un plancher de 10 au lieu de le remplacer — sans quoi un joueur sans ce nœud repartait à zéro, incapable d'acheter son premier Script. **Ordre du reset de fin de run corrigé** : `WipeRun()` efface la run entière AVANT `OnSessionEnded`, sur lequel le `SaveScheduler` écrit. Vérifié : coût 10,7 → 9,63, rendement 1 → 1,1, durée 1,5 → 1,35, dissipation 1,5 → 1,65 (toutes exactes au rang 1) ; TFlops 2 → 2,3 avec ×1,15 ; coût 9,63 → 9,54 avec une réduction de multiplicateur de 0,01 ; Game Over provoqué → SCR_01 niveau 0, argent 10, jauge 0, et la capture du gateway rend 0 upgrade pour 7 nœuds de prestige conservés.

**Lot « 3a — données du prestige »** (2026-08-26, vérifié en Play Mode via MCP) — `PrestigeSpecificNodesGenerator` limite sa recherche à `Assets/GameData/Upgrades` : un `FindAssets` sur tout le projet ramassait `Data/Addressables/Upgrades/NewUpgradeConfig.asset`, un orphelin d'id « 000 », d'où trois nœuds ciblant une upgrade inexistante. Le triplet COST/PROD/TIME devient **conditionné par le type** : seuls les Scripts ont un cycle, donc générer un nœud de réduction de temps pour un Hardware ou un Proxy créait un piège à débutant — achetable, payé en CPU Cycles, sans le moindre effet. Le PROD des Proxies prend un sens : efficacité de **dissipation** (branchement au lot 3b). Résultat : 105 nœuds spécifiques au lieu de 138, tous utiles — 45 Script (triplet), 30 Hardware et 30 Proxy (COST/PROD). Vérifié : 112 entrées JSON = 112 dans le catalogue, 112 nœuds instanciés en scène, zéro libellé `[UPG_...]`.

**Lot « 2b-2 — la Trace »** (2026-08-26, vérifié en Play Mode via MCP) — la Trace change de propriétaire, parce qu'elle ne peut plus être une somme figée : sa part Script dépend de quels cycles tournent à la frame courante. `UpgradeManager` garde les parts **statiques** (`HardwareTracePerSecond`, `ProxyDissipationPerSecond`), recalculées à l'achat ; `ScriptCycleRunner` expose la part **dynamique** (`ActiveScriptTracePerSecond`), accumulée dans le parcours de slots qu'il fait déjà — un simple float, aucune allocation ; `SimulationTicker` combine et applique la réduction de prestige, qui trouve enfin son consommateur. **Bug de signe des Proxies corrigé** : `totalTrace +=` s'appliquait aux trois types, donc acheter un Proxy augmentait la Trace. Second rôle des TFlops branché : `dissipation = Σ(base × niveau) × (1 + log10(1 + TFlops))`. `GetCurrentTracePerSecond` renommée `GetTraceMagnitudePerSecond`, le sens du champ dépendant du type. Vérifié : Hardware 0,08/s continu dès l'achat ; dissipation d'1 Proxy à 2 TFlops = 2,215682 contre 2,2157 théorique, linéaire à 5 Proxies ; débit négatif = jauge figée ; Script possédé mais **à l'arrêt** ne génère rien (10 → 10), et sa trace n'apparaît qu'au lancement du cycle (10 → 12).

**Lot « 2b-1 — les TFlops »** (2026-08-26, vérifié en Play Mode via MCP) — les TFlops deviennent une **capacité dérivée** et non plus une monnaie : `UserCurrencies.ComputerPower` disparaît au profit de `UpgradeManager.TotalTFlops` (parc Hardware possédé + `StartingComputerPower`). `RecalculateTotals()` procède en **deux passes**, et l'ordre n'est pas négociable : le débit théorique d'un Script se calcule depuis sa durée de cycle, laquelle dépend des TFlops — tout sommer d'un coup utiliserait les durées de l'achat précédent. La capacité est poussée dans les modèles via `UpgradeModel.SetTFlops()`, ce qui invalide leur cache de durée : sans cette poussée, acheter un Hardware n'aurait jamais raccourci un cycle déjà en cache. `UpgradeManager` devient `IStartable` pour s'abonner à `StartingComputerPower`. **`SaveData` passe en v2** (`ComputerPower` et `TotalComputerPower` retirés, `case 1:` de migration ajouté). Vérifié : compression conforme à la formule (2 TFlops → 1,3636 s ; 20 TFlops → 0,75 s, soit l'exemple du GDD), plancher `MinCycleDuration` tenu à 10⁹ TFlops (0,2 s au lieu de 0), acheter un Script ne touche pas aux TFlops, acheter 10 HW_01 les porte à 20 et raccourcit SCR_01 en direct ; migration v1→v2 confirmée sur un vrai fichier de sauvegarde.

**Lot « Localisation B — affichage » (2026-08-25, vérifié en Play Mode via MCP)** — `PrestigeItemPresenter` compose nom et description des 162 nœuds ciblés à partir des 6 gabarits et de la clé de l'upgrade cible, sans dépendance nouvelle : la clé se dérive de `TargetUpgradeId`. La composition a lieu **une seule fois dans le constructeur**, pas dans `RefreshView()`. Un nœud « spécifique » sans `targetUpgradeId` déclenche une erreur explicite. `GeneratorView` gagne un `_descriptionText` facultatif, alimenté depuis `DisplayDescriptionKey`. **Bug d'arité corrigé** : les Scripts utilisent désormais `UI_GENERATES_DATAS_CYCLE` (2 arguments), les Hardware/Proxy gardent `UI_GENERATES_DATAS` (1 argument) — ajouter `{1}` à la clé commune aurait levé une `FormatException` sur deux onglets sur trois. Vérifié : 3 générateurs affichent « Phishing familial », « Overclocking du CPU familial », « Navigation Privée » ; 162 nœuds composés (« Effacement Temporel (Opti Temps) ») ; 7 nœuds réels en `[PRESTIGE_*_NAME]`, ce qui est le comportement attendu tant que leur lore n'est pas écrit ; zéro exception.

**Lot « Localisation A — socle » (2026-08-25)** — la convention de nommage devient exécutable via `LocalizationKeys` (`Core.Services.Localization`), point unique dont dépendent les deux générateurs. `UpgradeConfigSO._displayName` devient `_displayNameKey` (avec `FormerlySerializedAs`) et gagne `_displayDescriptionKey`. Les trois générateurs d'éditeur **dérivent** désormais les clés de l'id au lieu de les lire depuis le JSON — une désynchronisation donnée/traduction devient impossible. Les nœuds de prestige « spécifiques » reçoivent des clés vides : leur libellé est composé à l'affichage (lot B) à partir de 6 gabarits, au lieu de 324 entrées de traduction. Migration unique des 45 libellés français vers `fr.json`, et purge de tout texte affichable des JSON de `GameData` (45 champs `displayName`, 169 paires `nameKey`/`descKey`). Vérifié : compilation sans erreur, générateurs relancés (45 upgrades / 169 nœuds), assets porteurs des clés dérivées, zéro français résiduel dans les `.asset`, 74 clés chargées au boot.

**Lot « panneau de prestige » (2026-08-25, vérifié en Play Mode via MCP)** — ⚠️ **piège trouvé au test, à retenir : Unity n'appelle jamais `Awake()` sur un objet inactif dans la hiérarchie.** L'arbre est construit alors que `Canvas/PrestigeTree` est encore désactivé, donc `UILineConnection._rectTransform` et `PrestigeItemView._rectTransform`, résolus dans `Awake()`, restaient `null`. Conséquences : `DrawLine` levait une `NullReferenceException` au premier lien — ce qui interrompait `Start()` et donc tout l'arbre — et `SetNodePosition` était un no-op silencieux laissant les 169 nœuds empilés à l'origine. Les deux passent en **résolution paresseuse** (propriété `Rect`), jamais dans `Awake`. Tout composant dont une méthode est appelée pendant la construction d'un panneau masqué doit suivre ce modèle. Résultat vérifié : 169 nœuds, 168 liens, positions et rotations correctes, zéro exception. — double instanciation des nœuds supprimée (169 au lieu de 338) · repère des liens unifié en pixels via `PrestigePanelView.GridToPixels` · placement déplacé dans la vue (`SpawnNode(Vector2)`), un nœud ne peut plus rester à la position du prefab · `_gridCellSize` sorti en `[SerializeField]` · repli `GetComponent<RectTransform>()` dans `PrestigeItemView.Awake` · lambda `onClick` remplacée par une méthode nommée · `"MAX"` et `$"Niv. {n} / {max}"` passés en clés de loc · erreur explicite sur prérequis absent du catalogue · `LOG_MANUAL_OVERCLOCK` annonçait des `%` alors que le code passe des secondes.

Persistance complète (autosave, fermeture, événements, écriture atomique, versionnement, prestige enfin persisté) · moteur de cycles par Script · paliers et automatisation · `EmergencyProtocolSystem` en Singleton · `UpgradeManager` / `PrestigeManager` / `ThreatManager` passés `IDisposable` · `UpgradeModel` disposés à la reconstruction · désabonnement lambda de `UpgradePanelPresenter` · `CancellationToken` sur la localisation · LINQ retiré du runtime · cache de coût et `DistinctUntilChanged` sur les générateurs · log d'achat · valeur d'overclock réelle · encodage UTF-8 de `GameSceneLifetimeScope` · recopie des durées de cycle dans le générateur d'éditeur.
