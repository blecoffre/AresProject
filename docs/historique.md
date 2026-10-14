# Historique — les entrées soldées du backlog

> **Archive. Rien ici n'est à faire.** Ce document garde le raisonnement et les pièges des lots
> soldés, que `git log` ne raconte pas. Ce qui reste à faire est dans `backlog.md`.
>
> Les chiffres cités datent de leur lot. Beaucoup ont été rendus caducs par les refontes de
> septembre (plafond dynamique, cumul de campagne, arbre aplati) : `game-design.md` fait foi.
>
> **Règle de tenue.** Une entrée du backlog qui est soldée descend ici le jour même, en tête de
> la section du mois, avec le commit qui la règle.

---

## Correctifs du backlog — 2026-10-14

Huit entrées soldées dans un même lot, avant la passe de test de la refonte :

- **Libellé du Hardware** : « Génère X Datas par cycle » devient « Fournit X TFlops »
  (`UI_GENERATES_TFLOPS`, qui remplace `UI_GENERATES_DATAS`, devenue sans usage).
- **Pluriel** du bouton de sortie : « (Cycles CPU : +{0}) », juste au singulier comme au pluriel.
- **Séparateur décimal** : tranché par Bertrand, **la culture de la langue du jeu**, jamais celle de
  Windows. `ILocalizationService.Culture` (« fr » → fr-FR, mise à jour avant l'émission de
  `CurrentLanguage`) ; `GetText` formate avec elle ; `CurrencyFormatter` la reçoit en paramètre ;
  les trois `ToString("0.#")` des presenters sont passés dans les clés (`{0:0.#}`). Effet visible
  en français : les montants passent de « 1.5K » à « 1,5K », comme les durées. L'en-tête repeint
  désormais ses montants au changement de langue.
- **Rechargement de scène suivi** : l'écran de fin jetait la tâche (`_ = …`) ; une exception y
  disparaissait. Tâche suivie et loguée, double clic ignoré. L'écran de fin, le menu principal et
  le menu de pause passent `Application.exitCancellationToken` au lieu de `CancellationToken.None`
  (jamais leur propre jeton : ils vivent dans la scène que la transition décharge).
- **`HeaderPresenter`** : ses quatre abonnements du constructeur passent dans `Start()`.
- **`Currency`** : `Add` ignore tout montant non strictement positif ou non fini ; `TryRemove`
  refuse NaN et l'infini ; `Reset` ramène un NaN à zéro.
- **`LogObjectPool`** : tampon circulaire en O(1), sans liste ni file.
- **Les 9 warnings** : trois `CS0649` de chaque DTO JSON (`#pragma` local, la réflexion de
  `JsonUtility` remplit ces champs) et trois `CS0618` (`FindObjectsByType` /
  `FindFirstObjectByType`, tri par InstanceID pour garder l'ordre d'avant). `dotnet build` : 0
  warning.

Les entrées du backlog, telles qu'elles étaient :

- 📖 **Faux libellé sur l'onglet Hardware.** `GeneratorPresenter.BuildStatsText` affiche « Génère
  X Datas par cycle » (`UI_GENERATES_DATAS`) pour un Hardware, qui fournit des TFlops. L'onglet
  Proxy, lui, annonce son répit en secondes depuis le 2026-10-13.
- 📖 **Pluriel non géré** : `UI_EXFIL_READY` dit « +1 **Cycles** CPU ». Reformuler la clé suffit ;
  une vraie pluralisation demanderait un mécanisme dans `ILocalizationService`.
- 📖 **Séparateur décimal dépendant de la machine**, trois endroits : `ClickerPresenter`
  (`SecondsGained.ToString("0.#")`), et deux dans `GeneratorPresenter.BuildStatsText`
  (`GetCurrentCycleDuration().ToString("0.##")`, et les secondes de répit d'un Proxy en `"0.#"`). Ils utilisent `CurrentCulture` — « 1,5 s » ici,
  « 1.5 s » ailleurs — alors que `CurrencyFormatter` force `InvariantCulture`. Trancher une
  politique unique, et la trancher avant les traductions.
- 📖 **`GameOverPresenter.cs:183`** : `_ = _sceneLoader.LoadGameSceneAsync(CancellationToken.None)`.
  Il manque le `.Forget()` — une exception y disparaîtrait sans log, et le joueur resterait bloqué
  sur l'écran de fin — et `CancellationToken.None` rompt la chaîne d'annulation.
  `MainMenuPresenter.cs:95` attend proprement mais passe aussi `None`.
- 📖 **`HeaderPresenter` s'abonne dans son constructeur** (quatre abonnements) en plus de `Start()`,
  contre la règle de `architecture.md` § R3.
- 📖 **`Currency.Add()`** est public et sans garde contre les valeurs négatives ; les gardes sont
  dans `UserCurrencies`, mais `Currency` est exposé.
- 📖 **`LogObjectPool`** recycle par `_activeItems.RemoveAt(0)` et teste par un `Contains`
  linéaire. Un tampon circulaire le rendrait O(1).
- 📖 **3 warnings `CS0649`** dans `JsonLocalizationService` (champs remplis par `JsonUtility`) :
  bénins, mais ils brouillent la règle « zéro warning nouveau ». À faire taire localement.
- 📖 **3 warnings `CS0618` dormants** en code éditeur : `LocalizationAnalyzerWindow.cs:120` et
  `LocalizationAutoInjectorEditor.cs:38,46` (`FindObjectOfType` / `FindObjectsOfType`, dépréciés
  en Unity 6). Ils ne remontent qu'à la recompilation de ces fichiers.

---

## Refonte « Time Attack », lot 5 — le nettoyage (2026-10-14)

Retirés : les treize migrations de sauvegarde v0 → v13, leurs constantes et le champ legacy
`NormalizedThreat` (Bertrand, seul joueur, supprime sa sauvegarde à chaque changement de format) ;
`minCycleDuration` et `durationReductionPerLevel` (code, générateur, 60 entrées JSON) ; le `costMult`
inerte des nœuds de prestige ; `ExfiltrationUnlockDatas` ; l'attribut `FormerlySerializedAs` de
`UpgradeConfigSO` ; la détection de l'ancien format de Trace dans le générateur d'upgrades ;
`UpgradeModel.GetCurrentCost` et `LevelUp`, `LogObjectPool.ReturnToPool`,
`PrestigeRequirement.RawRequiredLevel`, `GeneratorView.SetVisible` et son appel redondant (le
prefab est déjà actif) ; le bouton `GameOverPanel/PrestigeButton` et son entrée dans la liste
`autoInjectGameObjects` ; les couleurs du Ghost Cache restées dans les quatre palettes ; les clés
`MONEY`, `TRACE` et `UI_RUN_END_PRESTIGE_BUTTON` ; 25 sprites et un preset jamais référencés,
présents depuis le commit initial ; le dossier vide `UI/Uppgrades`. Conservées : les deux classes
Steam Cloud, en attente du lot Steam (Auto-Cloud ou API).

Entrées du backlog soldées par ce lot : les deux textes de développeur qui mentaient
(`BankRunContribution` et `ExfiltrationUnlockDatas`) ; les clés orphelines ; le bouton mort de
l'écran de fin ; le code mort de `LogObjectPool` et d'`UpgradePanelPresenter` ; le dossier fantôme ;
les commentaires « 116 / 134 nœuds » de l'interface de l'arbre ; la migration v10 → v11 jamais
éprouvée (`a-tester.md` 0.1), devenue sans objet.

L'entrée du backlog sur le bouton mort, telle qu'elle était :

- 📖 **Un bouton mort traîne dans l'écran de fin : `GameOverPanel/PrestigeButton`.** C'était le
  « [ DÉPENSER — NOYAU IA ] » qui ouvrait l'arbre depuis l'écran de fin, quand l'arbre vivait
  encore à part. Depuis que l'arbre est DANS cet écran, il ne sert plus : son handler a été retiré
  et l'objet désactivé le 2026-08-29, sans être supprimé. Relu dans la scène le 2026-10-13 : il est
  toujours là, inactif, avec un `LocalizedText` qui porte la clé `UI_RUN_END_PRESTIGE_BUTTON`.
  À supprimer dans Unity (pas dans le YAML), puis retirer la clé de `fr.json`. ⚠️ Ne pas le
  confondre avec le bouton « Terre Brûlée » du panneau de la console (`ConsoleLogs`), qui
  s'appelle lui aussi `PrestigeButton` — nom hérité — et sert bien.

L'historique des migrations, tel que `persistence.md` le décrivait :

  **v1 → v2 (lot 2b-1)** : `ComputerPower` et `TotalComputerPower` retirés. Les TFlops sont devenues une capacité dérivée du parc Hardware, entièrement recalculable depuis `Upgrades` — les sauvegarder créait une seconde source de vérité qui pouvait diverger. Aucun report n'est nécessaire : `JsonUtility` ignore simplement les champs en trop d'un ancien fichier, la migration se contente de tamponner la version.
  **v2 → v3 (prestige sur la run)** : ajout de `RunMoney`, l'argent gagné depuis le début de la run, qui pilote désormais le gain de CPU Cycles. La migration le force à `0` au lieu de recopier le cumul à vie — sinon une run déjà entamée rapporterait un gain immérité au premier Game Over.
  **v3 → v4 (Ghost Cache)** : ajout de `GhostCacheSeconds`, la charge accumulée en secondes d'excédent de dissipation. *Le champ a quitté le format en v11 avec le Ghost Cache ; la migration v3 → v4 ne fait plus que tamponner la version.* État de RUN malgré sa sauvegarde : le wipe l'efface, mais fermer le jeu ne la perd pas — elle représente du temps de jeu investi. Un Overdrive **en cours** n'est délibérément pas sauvegardé ; sinon fermer la fenêtre reviendrait à mettre l'effet en pause, dans un jeu qui n'a aucune progression hors-ligne. La migration part de zéro : la mécanique n'existait pas avant.
  **v4 → v5 (Data Wiper)** : le Bouton d'Urgence passe du coût en argent au prérequis en TFlops et gagne `EmergencyBlockRemainingSeconds` et `EmergencyCooldownRemainingSeconds`. Les deux sont sauvegardés alors qu'un Overdrive en cours ne l'est pas, et l'asymétrie est délibérée : sauvegarder l'Overdrive aurait permis de mettre en PAUSE un bonus en fermant la fenêtre, ne pas sauvegarder le contrecoup permettrait d'ÉCHAPPER à une pénalité par le même geste. `EmergencyUsesInRun` existait déjà et n'est pas touché par la migration.
  **v5 → v6 (durée de run)** : ajout de `RunElapsedSeconds`. Cumulée frame par frame dans `GameSessionManager.Tick()` et non déduite d'un `Time.time` de départ — celui-ci repart de zéro à chaque lancement, et une run reprise le lendemain afficherait la durée de la seule session en cours. Sauvegardée pour la même raison ; le temps passé fenêtre close n'est jamais crédité, le compteur n'avance que dans `Tick()`. La migration part de zéro : les anciennes sauvegardes n'ont pas mesuré leur temps de jeu.
  **v6 → v7 (jauge absolue)** : `NormalizedThreat` (une fraction) devient `CurrentTrace`, en points **absolus** — le plafond de jauge cesse d'être fixe à 100 (il suit le parc Hardware depuis le 2026-08-30, la puissance du joueur depuis le 2026-09-15), donc une fraction ne disait plus quelle quantité de trace restaurer. L'ancien champ est **conservé dans la classe** : `JsonUtility` ne lit un champ que s'il existe encore, le retirer rendrait toutes les sauvegardes antérieures illisibles. La conversion multiplie la fraction par le plafond **historique** de 100, jamais par le plafond courant — sinon un joueur équipé se verrait rendre une trace bien plus lourde que celle qu'il avait. *(Entrée rétablie le 2026-10-13 ; détail du lot dans `historique.md`.)*
  **v7 → v8 (achat multiple)** : ajout de `BuyQuantityMode`, le mode d'achat sélectionné (0 = x1, 1 = x10, 2 = x100, 3 = MAX). Premier champ du fichier qui n'est **ni de la méta-progression ni de la run** : c'est une préférence d'interface, et le wipe n'y touche pas — perdre son mode d'achat parce qu'on s'est fait repérer n'aurait aucun sens. Stocké en `int` et non en `BuyQuantity` : les valeurs de l'enum sont désormais un format de fichier, les réordonner redéfinirait silencieusement la préférence de tous les joueurs installés. `EnsureValid()` fait retomber sur x1 tout ce qui sort du domaine. L'état vit dans `BuyQuantitySelector`, service du scope **racine** : le gateway le restaure avant que la GameScene n'existe, et il survit à son rechargement.
  **v8 → v9 (cumul de campagne)** : `CampaignDatasBanked` et `CpuCyclesAwarded` pilotent désormais les points de prestige, à la place de l'argent de la seule run. La migration remet **les deux** à zéro : le joueur garde ses Cycles et ses nœuds, et son prochain palier sera simplement le premier. N'en remettre qu'un à zéro lui bloquerait la progression jusqu'à ce qu'il rattrape son ancien total. *(Décision décrite dans `game-design.md` — cette entrée manquait ici.)*
  **v9 → v10 (victoire)** : ajout de `HasWon`. De la **méta-progression** : la victoire est un fait acquis de la campagne, insensible au wipe. Elle n'est sauvegardée que pour ne pas rejouer sa mise en scène à chaque ouverture du jeu — la partie continue normalement après. La migration part à **faux** même sur un parc qui dépasse déjà le seuil : ces sauvegardes n'ont jamais pu déclencher la victoire, et le joueur la recevra au premier recalcul des totaux. `VictorySystem.Restore` ne prend d'ailleurs jamais la valeur faux, pour qu'un fichier abîmé ne puisse pas effacer une victoire acquise.
  **v10 → v11 (refonte « Time Attack », 2026-10-13)** : la jauge passe à 10 000 UT fixes et l'arbre change de nœuds, de prix et d'effets. La migration **efface la run en cours** — sa Trace se comptait sur un plafond de plusieurs millions, la ramener sur 10 000 UT mettrait le joueur au bord de la saisie — et **rend tous les CPU Cycles dépensés dans l'arbre**, qu'elle remet à zéro (racine exceptée). En v10, chaque rang coûtait exactement un point : le remboursement se calcule sur le seul fichier, sans catalogue. Garder un Blindage acheté 1 point qui en coûte désormais 10 serait un cadeau ; perdre les points des nœuds retirés, une punition. Cumul de campagne, points accordés et victoire ne bougent pas. `GhostCacheSeconds` quitte le format : `JsonUtility` ignore le champ resté dans un ancien fichier.
  **v11 → v12 (Data Wiper et écran de fin, 2026-10-14)** : `EmergencyBlockRemainingSeconds` et `EmergencyCooldownRemainingSeconds` quittent le format — le Data Wiper n'a plus ni contrecoup temporaire ni délai, son sacrifice découle du seul `EmergencyUsesInRun`. Ce compteur repart à **zéro** : il valait un nombre de purges à délai, il fixe désormais un sacrifice permanent de 30 % par usage, et le reprendre infligerait d'un coup 60 % à qui avait purgé deux fois. Nouveau bloc `PendingRunEnd` (struct à plat, copie du `RunSummary`) : le bilan de la run terminée tant que le joueur n'a pas relancé. Restauré, il désarme la partie (`GameSessionManager.RestorePendingRunEnd`), et le `GameOverPresenter` rouvre l'écran de fin en `PostStart` — après le `Start` du panneau de prestige, dont il a besoin. Une raison inconnue efface le bloc (`EnsureValid`).
  **v12 → v13 (brouillard, 2026-10-14)** : nouveau bloc `Readout` (dernier relevé en UT, âge du relevé, sauts connus), restauré tel quel par `TraceReadout.Restore` après la jauge. Jusqu'ici le chargement recalait le relevé sur la vérité, ce qui levait le brouillard à chaque rechargement. Une sauvegarde antérieure reçoit un relevé posé sur sa jauge — ce que faisait le chargement — : le brouillard se reforme au relevé suivant. Un relevé au-dessus de la vérité (fichier édité) est recalé.

---

## Refonte « Time Attack », lot 4 — l'arbre à 140 nœuds retiré du GDD (2026-10-14)

Remplacé par l'arbre à 21 nœuds et le Noyau Compilé (voir `game-design.md`, « L'arbre de
compilation »). Retirés du code : les 120 nœuds ciblés, leur générateur
(`PrestigeSpecificNodesGenerator`, menu « Générer les nœuds ciblés »), leur fichier
`04_SpecificUpgrades.json`, la struct `SpecificUpgradeBonuses` et ses quatre types de bonus ; les
types sans nœud `StartingMoney`, `StartingComputerPower`, `GlobalAutomationReduction` ; le
`_maxTargetedReduction` du `BalancingConfig` ; dix-neuf clés de localisation. Aucune migration de
sauvegarde : Bertrand, seul joueur, repart d'une sauvegarde neuve. Un nœud d'une ancienne
sauvegarde absent du catalogue est ignoré et ne compte pas au Noyau.

Entrées du backlog soldées par ce lot : l'Interception au maximum qui écrasait le choix des Proxies
(rabotée à ×19,5, valeurs à régler en jouant) ; les 15 nœuds « Opti Prod » des Proxies sans effet ;
`P_EXTRACT_2` et `_3` qui exigeaient encore leur parent au niveau 3 ; les 120 nœuds de remplissage ;
la question du pooling de l'arbre ; le piège « `04_SpecificUpgrades.json` est une sortie » ; les clés
orphelines `PRESTIGE_P_AUTO_GLOBAL_*` et `PRESTIGE_P_POWER_START_*`. Et, trouvé en route :
l'inspecteur n'avait aucun libellé d'effet pour la racine et loguait une erreur en la sélectionnant.

Les sections du GDD, telles qu'elles étaient :

### Magnitude des bonus de prestige

**Magnitude des bonus de prestige (tranché le 2026-08-26)** : `StartingMoney` et
`StartingComputerPower` se calculent avec `BonusPerLevel × niveau`, comme les autres types de
bonus — indexer l'effet d'un nœud sur son prix couple deux réglages qui doivent bouger séparément.

### Les branches du compte à rebours (lot 1, 2026-10-13)

Chaque branche part de `P_ROOT` et se parcourt en chaîne : un nœud n'ouvre le suivant qu'**à son
niveau maximum**. Chaque rang coûte 1 point, sauf le Blindage.

| Branche | Effet | Nœuds | Rangs | Coût | Apport |
|---|---|---|---|---|---|
| **Blindage** | Vitesse de l'A.M.I. ÷ `(1 + Σ)` | `P_HARDEN_1` à `_4` | 1 chacun | 1, 3, 6, 10 | +0,5 · +1,0 · +2,5 · +6,0 |
| **Interception** | Wipe des Proxies × `(1 + Σ)` | `P_INTERCEPT_1` à `_4` | 5 chacun | 1 / rang | +0,2 · +1,0 · +4,0 · +15,0 par rang |
| **Overclock** | Avance de l'Overclock `+ Σ` secondes | `P_CLICK`, `P_OVERCLOCK_POWER_1` à `_3` | 5 chacun | 1 / rang | +0,1 · +0,3 · +1,0 · +3,0 s par rang |

Au maximum : Blindage Σ 10 (jauge ×11 en durée, 20 points), Interception Σ 101 (Wipe ×102,
20 points), Overclock Σ 22 s (avance 22,5 s, 20 points).

Ces sommes sont exposées par le `PrestigeManager` (`TotalHardenBonus`, `TotalInterceptBonus`,
`TotalOverclockBonus`, en lecture seule) et lues par le `ThreatManager` et l'`OverclockSystem`.

⚖️ **Interception au maximum (×102), calculé :** en fin de partie Blindage max (jauge de ~330 s),
`PRX_01` niveau 1 retire 78 % de la jauge et **tout Proxy à partir de `PRX_02` la vide
entièrement**. Le dosage fin d'un Proxy de milieu de liste, décrit par le GD, n'existe donc
qu'avec une Interception partielle.

**Tranché par le GD le 2026-10-14 :** les valeurs de base des Proxies sont bonnes, c'est
l'inflation de l'arbre qui écrase le choix. Les apports de `P_INTERCEPT_3` et `P_INTERCEPT_4`
(+4 et +15 par rang) seront **rabotés drastiquement au lot 4**, valeurs fixées en jouant, pour que
le multiplicateur final oblige encore à racheter des Proxies chers pour survivre.

**Retirés de l'arbre au lot 1 :** `P_STEALTH` (réduction passive de Trace, qui n'a plus d'objet),
toute la branche Zéro-Day (`P_EXPLOIT_CHARGES`, `_MULT`, `_TRACE_REDUC`), `P_OVERCLOCK_AWAKE`,
`P_OVERCLOCK_SPACEBAR`. `P_EMERG`, `P_HARDEN_1`, `P_INTERCEPT_1` et `P_EXTRACT_1` se branchent
désormais sur la racine. **Le Ghost Cache et le Zéro-Day Exploit disparaissent du jeu** — leur
refonte en « Baroud d'Honneur », tranchée le 2026-09-16, est abandonnée avec eux.

### ⚠️ `04_SpecificUpgrades.json` est une SORTIE, pas une entrée

**Piège constaté le 2026-09-10, après lui avoir coûté deux sessions d'équilibrage.**

Contrairement aux cinq autres fichiers de `PrestigeData`, celui-ci est **écrit par un
générateur** — `PrestigeSpecificNodesGenerator`, menu `Tools/Core/Générer JSON (Grille — Arête de
poisson Corrigée)`. Y régler un prix à la main ne survit pas au prochain passage de ce menu.

Ce que ça a produit : un calage avait ramené les 120 nœuds spécifiques à `baseCost = 1`. Le
générateur relancé les a remis à `50 × Order`, et **l'arbre est passé de 3 477 à 527 552 CPU
Cycles** — dont 525 389 pour ce seul fichier, soit 99,6 % du total. La campagne plafonnait à
17 % de l'arbre en seize heures et personne ne voyait pourquoi : les JSON avaient l'air corrects,
puisqu'ils l'étaient au moment où on les regardait.

**Le prix de ces 120 nœuds se règle dans `PrestigeSpecificNodesGenerator.CreateGridItem`**, et
nulle part ailleurs. Il est désormais **plat** (`baseCost = 1.0`) : la progression par palier est
portée par la **chaîne de prérequis**, et par elle seule.

⚠️ **`costMult` est INERTE — vérifié le 2026-09-16.** Cette phrase invoquait aussi `costMult`, ce
qui contredisait la règle « tout coûte 1 point » énoncée plus bas. Les données tranchent : les
**147 nœuds ont tous `maxLevel = 1`**, et un multiplicateur de coût ne s'applique qu'à partir du
deuxième rang. Compté sur le catalogue généré : **zéro** nœud où `costMult` peut agir, et un arbre
complet à **147 CPU Cycles** pour 147 nœuds. Le champ survit dans les données sans être lu, comme
`minCycleDuration` et `durationReductionPerLevel`.

*Au 2026-10-13 : `costMult` reste inerte — `PrestigeConfigSO.GetCostAtLevel` rend `baseCost`
quel que soit le rang — mais l'arbre n'est plus entièrement à un rang. Voir « Tout coûte 1
point ».*

⚠️ La même prudence vaut pour tout fichier de `GameData/Editor/` : vérifier qu'il n'a pas de
générateur avant d'y régler quoi que ce soit à la main.

### Tout coûte 1 point, et l'arbre a fondu

Chaque niveau de chaque nœud coûte **1 CPU Cycle**. La courbe `BaseCost × CostMultiplier^niveau`
est supprimée — elle était recopiée dans quatre fichiers indépendants, et l'arbre s'était déjà
retrouvé 152 fois trop cher pour une erreur sur un seul terme.

Elle créait surtout un piège invisible : **mesuré, un joueur qui achète naturellement le nœud le
moins cher d'abord jouait cent fois moins bien qu'un joueur qui les évitait**, parce que 145 nœuds
affichés au même prix n'avaient pas du tout la même valeur.

L'arbre passe de **783 à 145 niveaux** : chaque nœud devient un choix unique portant d'un coup ce
que ses cinq rangs apportaient. Même puissance totale, mais un point achète cinq fois plus.

⚠️ **Les 120 nœuds ciblés sont une SORTIE de générateur.** `maxLevel` et le bonus se règlent dans
`PrestigeSpecificNodesGenerator.cs`, jamais dans le JSON.

⚠️ **Plus entièrement vrai depuis le 2026-10-02 (`a772e67`).** Chaque rang coûte toujours 1 point,
mais quinze nœuds ont repris des rangs : `P_REFAC` et `P_DATAM` 20, `P_STEALTH` 10, `P_CLICK`,
`P_OVERCLOCK_POWER_1` et `_3`, `P_EXPLOIT_MULT`, les quatre `P_INTERCEPT` et les trois
`P_EXTRACT` 5, `P_EXPLOIT_TRACE_REDUC` 3. L'arbre comptait **146 nœuds pour 239 rangs**. ⚠️ Un
nœud passé à N rangs multiplie sa puissance par N si son bonus n'est pas divisé : c'est arrivé à
`P_STEALTH`, qui saturait au 4ᵉ rang.

*Depuis le lot 1 de la refonte « Time Attack » : 140 nœuds ; le Blindage coûte 1, 3, 6 et 10
points, `P_DATAM` passera à 2 points par rang au lot 4 (voir « Les branches du compte à
rebours »).*

---

## Refonte « Time Attack », lot 3 — l'ancien brouillard retiré du GDD (2026-10-14)

Remplacé par le relevé en UT et la borne calculée sur la vitesse de l'A.M.I. (voir
`game-design.md`, « Le brouillard »). Retirés du code : l'intervalle en secondes et sa vitesse de
référence (`_traceReadoutMinInterval`, `_traceReadoutMaxInterval`, `_traceReadoutReferenceFillRate`),
le débit mesuré du relevé, le recalage sur la vérité au chargement.

Entrées du backlog soldées par ce lot : la jauge illisible (relevé toutes les 60 s) et la barre
rouge qui disparaissait après un achat de Proxy ; le rechargement qui révélait la vraie jauge. Et,
trouvé en route : le libellé « relevé il y a N s » lisait l'âge avant sa remise à zéro, affichait
la durée du relevé précédent, puis ne bougeait plus.

La section du GDD, telle qu'elle était :

### Le relevé de Trace — le danger vient de l'INCERTITUDE (tranché le 2026-09-10)

**Le constat qui a forcé ce modèle.** Plafond de jauge divisé par 250, et toujours **zéro saisie
sur trente-cinq runs**. Ce n'était pas un défaut de réglage mais une impossibilité : le joueur
contrôle les quatre termes de l'équation — débit, plafond, dissipation, instant de sortie — et les
observe tous en temps réel. Un compte à rebours déterministe, entièrement observable et piloté par
celui qu'il menace ne peut pas être dangereux. C'est un budget, pas une menace. Et baisser le
plafond n'y change rien : ça comprime toute la courbe uniformément, donc la part d'avertissement
reste identique. **Une jauge linéaire ne peut, par construction, surprendre personne.**

**La réponse : un capteur imparfait.** `TraceReadout` échantillonne la vérité à intervalle
variable, et **l'intervalle s'allonge quand la jauge se remplit vite**. Trace tranquille, relevé
quasi continu ; Trace qui s'emballe, la télémétrie bégaie et le joueur pilote au jugé — exactement
au moment où le chiffre comptait. L'incertitude n'est pas imposée au joueur, c'est la **conséquence
de sa propre gourmandise**.

L'interface affiche donc deux choses : la dernière position **relevée** (l'aiguille, qui se fige
visiblement quand ça se dégrade) et le **bord haut de la fourchette**, extrapolé depuis le dernier
débit connu. Le joueur décide contre un intervalle, jamais contre un nombre.

**Aucun hasard.** La saisie tombe toujours à 100,000 % exactement. On ne perd jamais sur un tirage,
on perd sur une estimation qu'on a mal faite — l'échec reste entièrement à la charge du joueur.

**Frontière stricte.** `TraceReadout` est en LECTURE SEULE sur le `ThreatManager`, qui reste la
seule vérité. **Aucune règle de jeu ne dépend du brouillard** : seul l'affichage est incertain.

#### Les quatre réglages, et pourquoi ils sont si extrêmes

| Réglage | Valeur | Pourquoi |
|---|---|---|
| `TraceReadoutMaxInterval` | **60 s** | Épaisseur du brouillard **au départ**, avant rachat. Mesuré le 2026-09-16 : à 25 s le capteur rachetait tout, et la posture agressive ne mourait plus jamais ; à 60 s elle meurt 2 fois sur 11 runs et boucle quand même l'arbre. |
| `TraceReadoutSensorPower` | **1** | Ce que la capacité de calcul achète en visibilité — voir la section suivante. À 0, la mécanique est éteinte et le brouillard redevient fonction de la seule vitesse. |
| `TraceReadoutReferenceFillRate` | **0,001/s** | À 0,005, la cécité n'arrivait qu'à sept fois le débit réel d'une run — donc jamais. |
| `TraceReadoutPessimism` | **3** | À 1,5, la « borne haute » passait **sous** la vérité (le prudent croyait sortir à 90 %, sortait à 93,8 %). La projection est linéaire alors que la production accélère. C'est la valeur **à zéro TFlop** : le capteur la ramène vers 1, jamais en dessous. |

⚠️ **Ce document a annoncé 120 s pendant que l'asset portait 25.** La dérive n'a été vue que le
2026-09-16. `BalancingConfig.asset` fait foi sur les valeurs ; ce document fait foi sur les
intentions. Quand les deux divergent, c'est un bug de l'un ou de l'autre, jamais une nuance.

⚠️ **Le pessimisme est un réglage de game design, pas un détail d'affichage.** C'est lui qui écarte
le joueur prudent du bord. Trop bas, prudence et gourmandise se rejoignent à 94 % de jauge et il
n'y a plus rien à arbitrer — les deux postures jouent le même jeu.

Le brouillard seul ne suffit pourtant pas : il crée le danger, pas la tentation. Il ne devient une
mécanique qu'associé aux **paliers d'extraction**, décrits plus bas — l'un rend le bord dangereux,
l'autre le rend désirable.

⚠️ **Limite connue, mesurée le 2026-09-10.** Indexer l'incertitude sur la vitesse de remplissage
faisait punir le MILIEU de partie et jamais le début : une run lente est une run lisible, donc les
premières runs — les plus lentes — étaient les plus sûres, et les saisies tombaient aux runs 6 à 8.

La **traque passive** a corrigé la moitié du problème par effet de bord : la jauge monte désormais
vite dès la première minute, donc le relevé bégaie aussi dès la première minute. Il reste que
l'épaisseur du brouillard n'est toujours pas un CHOIX du joueur — et c'est ce qui manque pour que
les saisies tombent quand il faut.

#### Le CAPTEUR s'achète — livré le 2026-09-16

L'incertitude ne vient plus de la seule vitesse : **la capacité de calcul la divise**. Run 1, le
joueur est aveugle parce qu'il n'a ni ping ni renifleur de paquets ; il achète ensuite sa
visibilité avec le Hardware.

```
Diviseur = 1 + TraceReadoutSensorPower × log10(1 + TFlops / TraceReadoutSensorReferenceTFlops)
```

⚠️ **L'échelle a été ajoutée le 2026-09-16, après une session de jeu.** Sans elle, le logarithme
concentrait tout le rachat sur les tout premiers TFlops : quatre Hardware de niveau 5 — quelques
minutes — ramenaient le brouillard de 60 s à 16,8 s. Mesuré sur cette run, l'écart entre la vérité
et le relevé valait **0,6 à 3,2 points de jauge**, soit six pixels : le joueur a rapporté n'avoir
« rien vu » du brouillard, et il avait raison. Il l'avait acheté sans s'en apercevoir.

Avec l'échelle à **1000 TFlops**, la même run garde 52,7 s de brouillard et des écarts de **1,8 à
10,9 points**, la fourchette courant jusqu'à trente points devant l'aiguille.

Il agit sur les **deux** versants du brouillard à la fois : il raccourcit l'intervalle entre deux
relevés, et ramène le pessimisme vers 1.

| TFlops | Intervalle |
|---|---|
| 0 *(run 1)* | 60,0 s |
| 377 *(4 Hardware niveau 5)* | 52,7 s |
| 1e4 | 29,4 s |
| 1e6 | 15,0 s |
| 1e9 | 8,6 s |
| 1e12 *(pic de campagne)* | 6,0 s |

**Le logarithme n'est pas décoratif.** Une proportionnalité directe rendrait le capteur parfait dès
le premier Hardware sérieux ; le log étale le rachat sur les douze ordres de grandeur que traverse
une campagne, et le premier achat se ressent quand même — `HW_01` au niveau 3 divise déjà le
brouillard par deux.

**Le pessimisme est interpolé DE 1 VERS le réglage, jamais en dessous.** Une borne haute que la
vérité franchirait ne serait plus une borne : c'est la seule garantie que le joueur possède, et
elle tient quelle que soit la puissance installée.

**Le plancher reste le plancher.** L'intervalle est borné par `TraceReadoutMinInterval` : sans ce
garde-fou, un parc de fin de partie le ramènerait sous la durée d'une frame, sans rien gagner de
lisible — une aiguille relevée deux fois par seconde est déjà continue pour l'œil.

⚠️ **Le sacrifice du Data Wiper aveugle aussi le capteur, et c'est voulu.** Les TFlops nettes sont la
capacité EFFECTIVE : l'`UpgradeManager` leur a déjà retiré la part sacrifiée. Purger sa Trace
fait donc perdre de la vue jusqu'à la fin de la run — le sacrifice s'étend à tout ce que les
TFlops nettes alimentent, comme il le fait déjà sur la compression des cycles.

**Mesuré le 2026-09-16, posture agressive (palier 90 %), campagnes tronquées :**

| | Sans capteur | Capteur, brouillard 25 s | **Capteur, brouillard 60 s** |
|---|---|---|---|
| Saisies | 15 sur 18 runs | 0 | **2 sur 11** |
| Arbre de prestige | 2,7 % | complet | **complet** |
| Contenu atteint | SCR_16 / 25 | SCR_25 / 25 | **SCR_25 / 25** |
| Pic de TFlops | 3,27e7 | 6,44e11 | **6,44e11** |

C'est la **spirale du chantier 1 qui se referme** : le téméraire meurt parfois et boucle quand même
sa campagne. À 25 s il ne mourait plus du tout — le capteur rachetait l'intégralité du brouillard,
et le pari disparaissait avec lui.

Le prudent, lui, ne bouge pas : palier 50 %, 169 runs / 10,07 h contre 170 / 10,16 h. **Le capteur
ne compte qu'au bord**, ce qui est sa raison d'être.

⚠️ **Reste à faire.** La posture 90 % sort en réalité à ~68 % de jauge : elle meurt en visant un
palier qu'elle n'atteint presque jamais, et n'encaisse donc que le bonus +15 %. À arbitrer avec la
seconde piste du chantier 1 (verrouiller le dernier palier). La **branche de prestige dédiée aux
capteurs** n'existe pas non plus : la visibilité ne s'achète aujourd'hui qu'en Hardware.

*Au 2026-10-13 : le dernier palier est verrouillé depuis le 2026-09-16 (voir « Le palier 90 % est
VERROUILLÉ »), mais le constat des ~68 % n'a pas été remesuré depuis. La branche capteurs n'existe
toujours pas.*

---

## Refonte « Time Attack », lot 2 — l'ancien Data Wiper retiré du GDD (2026-10-14)

Remplacé par le sacrifice permanent de TFlops (voir `game-design.md`, « Le Data Wiper »). Retirés du
code : le palier de TFlops requis, la réduction en pourcentage de la Trace
(`ThreatManager.ReduceTraceByFraction`), le contrecoup de 60 s et le délai de 5 min, avec leurs deux
champs de sauvegarde (`SaveData` v12) et les libellés « puissance insuffisante » et « recompilation ».

Entrées du backlog soldées par ce lot : les valeurs provisoires du Data Wiper (palier de 50 TFlops,
escalade ×3, délai de 5 min plus long qu'une run) ; le tooltip de `_emergencyTraceReduction` qui
disait « en ABSOLU » ; le libellé « Trace −25 pts » du bouton, qui retirait en fait 25 % de la Trace
courante ; l'écran de fin de run non sauvegardé (session du 2026-10-14), désormais rouvert au
chargement.

La section du GDD, telle qu'elle était :

### Bouton d'Urgence — le « Data Wiper »

Refondu le 2026-08-27. L'ancienne version achetait l'effacement avec de l'argent, comme un
pot-de-vin. La nouvelle ne coûte **rien** : elle **exige** de la puissance de calcul. Effacer ses
traces, c'est envoyer un ver dans les serveurs fédéraux — il faut de la force brute, pas des
billets.

| Paramètre | Valeur |
|---|---|
| **Prérequis** | Posséder un palier de TFlops. Rien n'est dépensé |
| **Escalade du palier** | ×3 par usage sur la run ⚠️ *valeurs provisoires : 50 TFlops de base, ×3 — non chiffrées par le GD* |
| **Effet** | ~~−20 points ABSOLUS de jauge~~ → depuis le 2026-08-30 : **−25 % de la Trace accumulée** (proportionnel, `EmergencyTraceReduction` = 0,25). À 60 % de jauge on tombe à 45 %. Un retrait absolu ne pesait plus rien face à un plafond de plusieurs millions |
| **Contrecoup** | **30 % des TFlops immobilisées pendant 60 s**, +10 points par usage (30, 40, 50…), plafonné à 90 % |
| **Délai** | **5 minutes** entre deux activations |
| **Déblocage** | Nœud de prestige `P_EMERG` |

**Le contrecoup est le cœur de la mécanique.** Les TFlops alimentent aussi la dissipation des
Proxies : purger la Trace **affaiblit la défense juste après**, et la jauge remonte plus vite.
Mesuré : 52 TFlops → 36,4, dissipation de 163,5 → 154,4, cycle de `SCR_01` de 0,347 s → 0,443 s.
La tranche s'alourdissant de dix points par usage, le bouton devient de plus en plus dangereux
à mesure que la run avance — bénéfique en principe, risqué en fin de run.

Le plafond à 90 % n'est pas dans la spécification du GD : sans lui, le septième usage d'une run
immobiliserait 100 % du parc, donc plus aucune dissipation ni compression de cycle. Le bouton
deviendrait un suicide pur, ce qui n'est pas un choix mais un piège.

**Mesuré le 2026-09-15 — la fenêtre d'usage fait toute la valeur du bouton.** Déclenché à 85 % de
jauge, dos au mur, il est franchement NUISIBLE : la posture agressive tombait à 18 runs et mourait
83 % du temps. Déclenché à 60 %, la même posture tient 95 runs et ne meurt plus que 16 % du temps,
son arbre passant de 64 à 92 %. Le GDD le disait déjà — « bénéfique en principe, risqué en fin de
run » — mais l'écart est bien plus violent que la formule ne le laissait entendre.

⚠️ **Le bénéfice est un POURCENTAGE de jauge, le contrecoup un coût FIXE en secondes.** Sur une run
de 1200 s, 20 points valent 240 secondes gagnées contre 60 secondes de production diminuée. Sur une
run de 200 s, ils n'en valent plus que 40. Ces constantes ont été calibrées pour des runs de vingt
à trente minutes : si la durée des runs rebaisse, le bouton redeviendra un piège — et le délai de
cinq minutes entre deux usages dépasse déjà la durée d'une run de campagne.

Le contrecoup ET le délai sont **sauvegardés** (`SaveData` v5), contrairement à l'Overdrive du
Ghost Cache. L'asymétrie est volontaire : là-bas, sauvegarder aurait permis de mettre en PAUSE un
bonus ; ici, ne pas sauvegarder permettrait d'ÉCHAPPER à une pénalité en fermant la fenêtre.

---

## Session de test du lot 1 de la refonte — 2026-10-14

Jouée par Bertrand en éditeur, sur `chantier/refonte-time-attack`, sauvegarde supprimée au
préalable (la migration v11 n'a donc pas été éprouvée).

**Vu fonctionner** — lignes retirées de `a-tester.md`, telles qu'elles étaient :

| # | Quoi | Comment | Ce qu'on doit voir |
|---|---|---|---|
| 0.2 | **Départ de la traque** | Run neuve. Attendre 30 s sans rien faire, puis acheter `SCR_01` et lancer son cycle. | Jauge à 0,0 % tant qu'aucune Data n'est volée. Elle démarre à la fin du premier cycle (ou au premier Hardware acheté). |
| 0.5 | **Overclock en cycle** | Un cycle de `SCR_01` en cours, appuyer une fois sur Espace. | La barre du Script bondit. Console : « Overclock : +0,5 s sur les cycles en cours. Jauge +165 UT. » (un peu plus avec du Hardware). Même chose au bouton. |
| 0.6 | **Overclock maintenu** | Tenir Espace enfoncé 3 s, cycle en cours. | **Une seule** ligne d'Overclock dans la console. |
| 0.7 | **Saisie par Overclock** | Cycles automatisés en cours (`SCR_01` niveau 10), marteler Espace. | Chaque appui retenu coûte 165 UT : la saisie tombe en une soixantaine d'appuis. Écran de fin de saisie habituel. |
| 0.8 | **Proxy : la jauge descend** | Jauge non nulle, acheter un `PRX_01`. | Carte du Proxy : « Efface 2,5 s de traque par achat ». Console : « > Brouillage : jauge −83 UT. » (un peu plus avec du Hardware). L'aiguille de la jauge recule aussitôt. |
| 0.9 | **Proxy acheté à vide** | Run neuve, avant toute Data volée, acheter un `PRX_01`. | Console : « > Brouillage : jauge −0 UT. » — l'excédent est perdu, c'est voulu. |
| 0.10 | **Proxy en lot** | Mode d'achat ×10 sur `PRX_01` (niveau 0 → 10), jauge bien remplie. | Wipe d'environ 10,3 fois celui du niveau 1 (neuf niveaux nus, le dixième au palier ×1,32), soit ~850 UT. |
| 0.11 | **L'arbre du lot 1** | Écran de fin de run (exfiltrer : le bouton « [PRÊT] WIPE SYSTÈME » reste cliquable en éditeur). Parcourir l'arbre. | Plus de Furtivité, de Zéro-Day, d'Injecteur Automatique ni d'Accélérateur Manuel. Data Wiper, Blindage, Interception et Extraction reliés à la racine. Blindage à 1, 3, 6 et 10 points. Lignes d'effet remplies : « > Temps avant saisie : +50 % (l'A.M.I. ralentit) », « > Brouillage de chaque Proxy acheté : +20 % par niveau », « > Avance de l'Overclock : +0,1 s par niveau ». Aucune clé entre crochets. |
| 0.12 | **Chaînes au maximum** | Acheter un rang de `P_INTERCEPT_1`. | `P_INTERCEPT_2` reste verrouillé jusqu'à `P_INTERCEPT_1` 5/5. Idem `P_HARDEN_2` derrière `P_HARDEN_1`, et les paliers d'Overclock. |
| 0.13 | **Blindage** | Acheter `P_HARDEN_1`, « [ NOUVELLE INTRUSION ] », Overclock en cycle. | Console : « Jauge +110 UT » au lieu de 165 (vitesse divisée par 1,5). La jauge met ~7 min 30 à se remplir. |
| 0.14 | **Interception et Overclock** | Acheter un rang de `P_INTERCEPT_1` et un de `P_CLICK`, nouvelle intrusion. | Carte de `PRX_01` : « Efface 3 s » ; Wipe ~99 UT. Overclock : « +0,6 s ». |
| 0.16 | **Pause** | En pleine run, Échap 30 s, puis refermer. | Pendant la pause, le compteur « relevé il y a N s » ne bouge pas : la jauge est figée. |
| 1.2 | **Capteur : le brouillard se voit** | Première run, sans Hardware. Laisser la jauge monter. | L'aiguille se fige entre deux relevés (« relevé il y a N s ») et la fourchette court devant elle. En début de partie, le relevé doit **bégayer franchement** (jusqu'à 60 s). |
| 1.3 | **Capteur : le Hardware rachète la vue** | Acheter du Hardware au fil de la run. | Les relevés se rapprochent, la fourchette se resserre. Question de ressenti : le joueur s'en aperçoit-il ? (À la session du 2026-09-16, il n'avait « rien vu » : c'est ce qui a fait ajouter l'échelle de 1000 TFlops.) |
| 1.4 | **Les trois paliers sur la jauge** | Monter vers 50 %, 75 %, 90 %. | Trois traits, chacun dans son état : vert (franchi), **ambre pulsant** (la fourchette l'enjambe), terne (à venir), gris (verrouillé). Questions de ressenti : la pulsation de l'incertain se lit-elle du coin de l'œil ? |
| 1.5 | **Le gain du bouton d'exfiltration saute** | Pendant la même montée, regarder le bouton « [PRÊT] WIPE SYSTÈME ». | Le gain annoncé change d'un cran à chaque palier franchi **sur le relevé** (pas sur la vraie jauge). Ressenti : le saut se remarque-t-il ? |
| 1.6 | **Verrou du palier 90 %** | Sans le nœud `P_EXTRACT_HIGHRISK`, pousser au-delà de 90 % et exfiltrer. | Le trait des 90 % est gris, marqué [VERROUILLÉ]. Au bilan, le bonus vaut celui des 75 % (+30 %), jamais zéro. Puis acheter le nœud et refaire : +50 %. |
| 1.7 | **L'aperçu du bouton tient sa promesse** | Noter le gain annoncé par le bouton juste avant de cliquer. | Le bilan crédite **exactement** ce nombre de Cycles. (Défaut corrigé dans `a772e67` : le bouton annonçait 6,5 là où la fin de run en versait 6.) |
| 1.8 | **Modèle des boîtes** | Enchaîner deux ou trois exfiltrations, noter les Cycles crédités et le « Prochain à N Datas ». | Le seuil suivant grandit de 12 % par point. Un point exige la **somme** des boîtes, pas le prix de la dernière. Repère calculé (non mesuré) pour une première run, bonus de palier non compris : 3 M de Datas → 1 point, 7,9 M → 3, 15,7 M → 5. |
| 1.9 | **La saisie verse 30 %** | Se laisser saisir à 100 % après une run correcte. | Le bilan montre la ligne « perdu à la saisie … −N » et crédite quand même des Cycles si le cumul franchit une boîte. Une saisie ne doit jamais rapporter autant qu'une exfiltration au même moment. |
| 1.10 | **Alertes sonores des paliers** | Pendant 1.4, écouter. | Le son d'un palier part quand le trait s'allume **à l'écran** (sur le relevé), jamais avant. Pas d'alerte au chargement d'une sauvegarde. Un palier verrouillé ne sonne pas. |
| 1.11 | **Le Data Wiper aveugle le capteur** | Avec `P_EMERG`, déclencher le Data Wiper en milieu de run. | La Trace baisse de 25 % de sa valeur courante (⚠️ le bouton annonce « −25 pts », c'est un libellé faux connu). Pendant les 60 s de contrecoup, le brouillard s'épaissit. |
| 2.6 | **Mode daltonien à chaud** | Choisir **Deutéranopie** dans Options > Accessibilité, refermer les options **et la pause**, puis ouvrir l'arbre (P) — ou attendre l'écran de fin de run. ⚠️ Les options ne s'ouvrent que depuis la pause, et Échap referme l'arbre avant d'ouvrir la pause : on ne peut **pas** regarder l'arbre en changeant de mode. Le premier protocole le demandait, et c'est pour ça que le test du 13/10 n'a rien montré. | Sur l'écran de jeu principal, presque rien ne change : le vert terminal reste le « positif » dans tous les modes (option B). Dans l'arbre : nœuds **abordables** blancs au lieu d'orange, nœuds **trop chers** violets au lieu de rouges, liens **ouverts** lilas au lieu de cyan. Sur la jauge, le palier **incertain** blanc au lieu d'ambre. Le Data Wiper, une fois débloqué, change aussi. Revenir en Standard : tout reprend ses couleurs d'origine. |

Remarques de Bertrand sur ces lignes, reportées au backlog :

- 0.8 : la barre rouge du brouillard disparaît après un achat de Proxy (lot 3).
- 0.10 : quitter et relancer révèle la vraie jauge — une triche (lot 3).
- 0.11 : quitter sur l'écran de fin relance une run ; la fenêtre d'achat du prestige est perdue
  (lot 2).
- 1.11 : le brouillard se met à jour plus tard après le Data Wiper — attendu, c'est le contrecoup.

**Corrigé le jour même :**

- 0.15 — le retrait du Ghost Cache laissait un trou à droite du Data Wiper : la rangée d'actions
  n'a pas de layout automatique. Le Data Wiper prend désormais toute la largeur, sous l'Overclock
  (choix de Bertrand).
- 1.12 — la séquence console de victoire se déroulait, mais aucun écran ne suivait : l'objet
  `VictoryPanel`, qui porte la `VictoryView`, était **éteint dans la scène depuis sa création**
  (`bf490c6`, 2026-09-22). Le panneau enfant s'allumait sous un parent inactif. Réactivé ; la vue
  éteint elle-même son panneau à l'`Awake`, comme prévu. 1.12 à 1.14 sont à rejouer.

**Méthode :** les protocoles nommeront désormais les nœuds par leur nom affiché en jeu
(« Filtrage DNS »), pas par leur id (`P_INTERCEPT_1`).

---

## Refonte « Time Attack », lot 1 — le modèle de Trace retiré du GDD (2026-10-13)

Le GD a remplacé la Trace dynamique par un compte à rebours fixe (voir `game-design.md`, « La jauge
de l'A.M.I. »). Les sections ci-dessous sont celles que ce lot a retirées du GDD, **telles
qu'elles étaient** au commit `d8465d0` : elles expliquent pourquoi l'ancien modèle était fait
ainsi — génération de Trace par Script, chaleur du Hardware, saturation des Proxies, plafond
dynamique, Ghost Cache, calages de septembre, harnais. **Rien n'y fait plus foi.**

Retirés du code dans le même lot : `GhostCacheSystem` et sa vue, le harnais d'équilibrage
(`Editor/Balance`, ~2 700 lignes) et le simulateur Python (`tools/balance/`), les nœuds
`P_STEALTH`, `P_EXPLOIT_*`, `P_OVERCLOCK_AWAKE`, `P_OVERCLOCK_SPACEBAR`, le son du Zéro-Day.

### Entrées du backlog soldées par le lot 1

Chacune tombe avec la mécanique qu'elle visait, par décision du GD du 2026-10-13 :

- **`P_OVERCLOCK_SPACEBAR`** → nœud retiré, la barre d'espace est libre dès le départ.
- **Le Zéro-Day qui ne se déclenche plus** → Ghost Cache, Zéro-Day et Baroud d'Honneur abandonnés.
- **`P_STEALTH` qui sature** → nœud retiré, la réduction passive de Trace n'existe plus.
- **`P_HARDEN_2` à `_4` au niveau 3** → règle « parent au maximum » partout (`requiredLevel: 0`).
- **Les deux mécaniques attendues par l'arbre** → le prérequis « X points dépensés » est abandonné,
  l'automatisation au niveau 10 est conservée.
- **Les `wipeSeconds` des Proxies à refaire** (ouverte et soldée le 2026-10-13) → le GD a fourni
  15 valeurs de 2,5 à 145 s ; les anciennes magnitudes de dissipation, lues en secondes, faisaient
  valoir à `PRX_07`–`PRX_15` plus qu'une jauge entière dès le niveau 1.

Leur texte au moment de la clôture :

- 📖 **`P_OVERCLOCK_SPACEBAR` (« Accélérateur manuel ») n'a aucun effet.** Il coûte 1 point, mais
  `PrestigeManager.IsSpacebarUnlocked` n'a **aucun lecteur** : la barre d'espace marche pour tout
  le monde, nœud acheté ou non. Laissé débranché exprès dans `a772e67` — brancher le verrou
  retirerait le clic aux sauvegardes existantes. **À trancher par le GD** : brancher le verrou
  (et offrir le nœud aux sauvegardes antérieures par migration ?) ou retirer le nœud. À traiter
  avec la refonte de l'arbre, qui fait de l'espace un nœud tardif (cf. GDD).
- 📖 **Le Zéro-Day Exploit ne se déclenche plus — mais ses nœuds s'achètent.** Zéro déclenchement
  sur 264 runs simulées (2026-09-16). C'est la **charge** qui est hors d'atteinte (90 s de posture
  tenue au-dessus de 59,5 % de réduction), pas l'effet. Les trois nœuds `P_EXPLOIT_CHARGES`,
  `P_EXPLOIT_MULT` (5 rangs) et `P_EXPLOIT_TRACE_REDUC` (3 rangs) coûtent 9 points. La refonte en
  **Baroud d'Honneur** est tranchée depuis le 2026-09-16 et pas commencée (spécification dans le
  GDD, § Ghost Cache). Elle fait sortir `GhostCacheSeconds` de la sauvegarde → `SaveData` v11.
- 📖 **`P_STEALTH` sature au 4ᵉ de ses 10 rangs.** 0,25 par rang, et
  `TraceReductionMultiplier = max(0,1 ; 1 − Σ)` touche son plancher dès 4 rangs. Les rangs 5 à 10
  coûtent 6 points pour rien, et le 4ᵉ retire d'un coup 90 % de la Trace brute. Probable erreur de
  `a772e67` : le nœud est passé de 1 à 10 rangs sans que son bonus — qui portait jusque-là les
  cinq anciens rangs d'un coup — soit divisé. ⚖️ Effet sur l'équilibrage non mesuré.
- 📖 **`P_HARDEN_2` à `_4` exigent leur parent au niveau 3, qui n'a qu'un rang.** Le générateur
  ramène l'exigence au maximum et avertit à chaque passage : sans conséquence en jeu, mais trois
  avertissements permanents noient les vrais. Vestige de l'aplatissement du 2026-09-12 : retirer
  les `requiredLevel` des trois JSON.
- 📖 **La refonte de l'arbre attend deux mécaniques qui n'existent pas.**
  1. Un prérequis **« X points dépensés »** : `PrestigeRequirement` ne connaît qu'un couple (nœud
     parent, niveau). Le GD en a besoin au moins deux fois (barre d'espace, réveil).
  2. **La fin de l'automatisation au niveau 10.** Le seuil existe toujours
     (`UpgradeConfigSO._automationLevel`, 10 par défaut, déclaré sur les 25 Scripts) mais plus aucun
     nœud ne l'abaisse depuis le retrait de `P_AUTO_GLOBAL` : `GlobalAutomationReduction` et
     `SpecificUpgradeBonuses.AutomationThresholdReduction` sont du code mort côté données.

### LA TRINITÉ — un rôle par pilier, tranché le 2026-09-10

**C'est la règle qui prime sur toutes les autres.** Trois sessions d'équilibrage se sont brisées
dessus : chaque correction en cassait deux, parce qu'aucun pilier n'avait de frontière. Le
Hardware faisait **cinq choses** — vitesse, rendement, plafond, efficacité des Proxies, sa propre
chaleur — et les Proxies faisaient de l'offense.

| Pilier | Rôle, en une phrase | Ce qu'il touche |
|---|---|---|
| **Scripts** | **L'attaque.** Ils rapportent, et c'est eux qu'on repère. | Argent, génération de Trace |
| **Proxies** | **La furtivité active.** Dissipation pure, rien d'autre. | Dissipation |
| **Hardware** | **L'infrastructure.** Il encaisse le choc et suralimente le moteur. | Plafond de jauge, compression des cycles, rendement des Scripts |

La décision du joueur devient énonçable : *je stagne financièrement* → Scripts. *Le mur arrive trop
vite* → Hardware. *La pression ne redescend pas* → Proxies.

**Ce qui a été coupé le 2026-09-10 :**

- **Le Hardware n'améliore plus les Proxies.** Le facteur `(1 + log10(1 + TFlops))` sur la
  dissipation a disparu. C'était le vrai coupable : le Hardware tenait à lui seul les DEUX moitiés
  de la défense, il levait le plafond *et* rendait les Proxies meilleurs. Aucun réglage ne pouvait
  départager les deux piliers tant que ce facteur existait.
- **Les Proxies n'accélèrent plus les cycles.** La synergie de +1 % par niveau existait pour qu'un
  achat de Proxy ne soit « jamais perdu ». Elle donnait de l'offense au pilier défensif. Le confort
  est assumé perdu : **c'est le coût d'opportunité des Proxies qui rend la répartition
  intéressante.**

⚠️ **Ce qui RESTE et n'est pas un bonus :** la compression s'applique aussi à la dissipation. Ce
n'est pas « le Hardware aide les Proxies », c'est une **conversion d'unité**. La trace d'un Script
se compte par CYCLE : comprimé ×33, il la verse trente-trois fois plus souvent. Une dissipation
exprimée par seconde serait diluée d'autant — mesuré, 1 500 niveaux de Proxies ne tenaient plus que
24 % de réduction. En l'appliquant des deux côtés, la compression devient **neutre** : le Hardware
ne rend les Proxies ni meilleurs ni pires. C'est exactement la neutralité qu'exigent des rôles
tranchés.

### Les TFlops : deux effets, tous deux offensifs

Ils ne touchent plus à la défense. Ce sont **la puissance de calcul du joueur, et rien d'autre** —
un supercalculateur détourné doit calculer, pas servir de bouclier passif.

**1. Compression du temps sur les Scripts**
```
DuréeRéelle = DuréeBase / (1 + TFlops × k) ^ CompressionExponent
```
Décroissance asymptotique, jamais zéro. L'exposant a remplacé le mur `minCycleDuration`, qui
saturait dès quelques dizaines de TFlops et tuait le pilier.

**2. Rendement des Scripts**
```
RendementScript × = (1 + TFlops) ^ YieldExponent
```
Réservé aux Scripts : chez un Hardware le rendement EST la capacité en TFlops, l'y brancher
créerait une boucle divergente.

Le **plafond de jauge**, lui, ne passe PAS par les TFlops : il vient directement du champ
`traceCapIncrease` de chaque Hardware, en `puissance^TraceYieldExponent`.

### Ladder des Scripts — la phrase sur la Trace par cycle

**Uniforme en RELATIF, à dessein.** Les durées de base s'étalent de 1,5 s à 3 600 s — un facteur
2 400. Rendre un Script à long cycle jouable dans l'absolu est le travail du **Hardware**, via la
compression. Le palier n'a qu'une mission : que chaque Script donne le sentiment d'accélérer quand
on l'approfondit. Et comme la trace se compte par cycle, un Script qui accélère génère mécaniquement
plus de Trace par seconde — « plus d'argent ET plus de menace », ce qui referme sa définition.

### L'Overclock « réveil » et `P_OVERCLOCK_AWAKE`

**Action de l'Overclock (Clic manuel) :** Le clic global (Overclock) agit comme un **réveil**. S'il y a des Scripts inactifs (car sous le seuil d'automatisation), le clic de l'Overclock les démarre en priorité. Si les cycles sont déjà en cours, il avance le temps de tous les cycles de `0,5 s × ClickPowerMultiplier`.

**Revirement du 2026-08-27 — le « réveil » devient un nœud de prestige avancé.** Un clic ne
démarre PAS les Scripts à l'arrêt par défaut : il se contente d'avancer ceux qui tournent. Galérer
au lancement manuel fait partie de l'expérience, et pendant une bonne partie du jeu. Le réveil
automatique se débloque par un nœud de prestige tardif, pour fluidifier les runs de haut niveau
où relancer quinze Scripts à la main devient une corvée plutôt qu'un choix.

Le nœud est `P_OVERCLOCK_AWAKE` — « Injecteur Automatique », un seul rang, **5 000 CPU Cycles**,
branche Automatisation derrière `P_POWER_START`.

*État au 2026-10-13 : depuis l'aplatissement de l'arbre, il coûte **1 point**, derrière
`P_OVERCLOCK_POWER_3` au maximum ; `P_POWER_START` n'existe plus. La refonte annoncée en fait un
nœud tardif conditionné par « avoir dépensé X points » (voir « Refonte de l'arbre »).*

*Le paragraphe ci-dessous décrivait le comportement de base envisagé le 26/08. Il décrit
désormais l'effet du nœud, une fois celui-ci acheté :*

**Un clic fait alors les DEUX, jamais l'un ou l'autre.**

*L'ordre compte à l'implémentation : on avance D'ABORD les cycles en cours, on réveille ENSUITE.
Réveiller en premier offrirait au passage une demi-seconde d'avance aux Scripts qui viennent tout
juste de démarrer, alors que le GDD dit « ceux qui tournaient déjà ». Les Scripts automatisés ne
sont jamais comptés comme réveillés : ils repartent seuls au Tick suivant.* Un même clic démarre d'un coup **tous** les Scripts possédés à l'arrêt, *et* avance de `0,5 s × ClickPowerMultiplier` ceux qui tournaient déjà. Aucun clic n'est donc perdu, et sa valeur reste constante à tous les stades de la partie. Ne pas implémenter de priorité exclusive (« tant qu'il reste un inactif, on ne fait que réveiller »).

⚠️ **Justification corrigée le 2026-09-16.** Cette règle s'appuyait sur le début de partie — « chaque
clic ne servirait qu'à relancer et jamais à accélérer » — ce qui n'a plus de sens depuis que le
réveil est passé derrière `P_OVERCLOCK_AWAKE` : sans le nœud, un clic ne réveille rien. **La règle,
elle, reste nécessaire**, mais elle protège désormais l'autre bout de la partie — le joueur QUI A
ACHETÉ le nœud et se retrouve avec des Scripts à l'arrêt. Une priorité exclusive ferait cesser ses
clics d'accélérer au moment précis où il en a quinze à gérer, c'est-à-dire là où le nœud était censé
l'aider.

Implémenté conformément dans `OverclockSystem.TriggerManualOverclock` : `AdvanceAllActive` d'abord,
`StartAllIdle` ensuite et seulement si le nœud est possédé.

### Trace — les Proxies agissent sur le débit, jamais sur la jauge

**Refonte du 2026-08-31 — la soustraction plate est morte, remplacée par une saturation.**

```
TraceBrute  = (Σ TraceScriptsActifs + Σ TraceHardwarePossédés) × (1 − ReductionPrestige)
Reduction   = MaxTraceReduction × D / (D + HalfPointRatio × TraceBrute)
DebitTrace  = TraceBrute × (1 − Reduction)
```

où `D` est la puissance de dissipation cumulée des Proxies :

```
D = Σ (base × niveau × paliers) × MultiplicateurInterception × (1 + TFlops × k) ^ CompressionExponent
```

**Le facteur de compression y a été ajouté le 2026-08-31, et il n'est pas un bonus de plus.**
Sans lui la défense décrochait mécaniquement : un Script comprimé ×33 verse sa trace de cycle
trente-trois fois plus souvent, donc son Trace/s est multiplié d'autant, alors que la dissipation
— exprimée par seconde — ne gagnait que le `log10`. Mesuré en simulation de fin de partie :
1 500 niveaux de Proxies achetés ne tenaient plus que **24 %** de réduction. Avec le facteur,
**80 %**.

Un Proxy filtre du **trafic**, pas des secondes, et il tourne sur le matériel du joueur : si les
Scripts vont trente fois plus vite, sa charge suit. La compression devient ainsi **neutre** sur le
rapport dissipation/génération, et tout le danger du Hardware passe par où il doit — sa propre
chaleur et le boost de rendement qu'il donne aux Scripts, tous deux comptés en `puissance^0,6`.

**Pourquoi.** L'ancienne formule `max(0, Brut − Dissipation)` opposait une valeur **non bornée**
à une génération **bornée** — la trace étant devenue forfaitaire le 30/08, elle plafonnait à
28 192/s pour le jeu ENTIER. Mesuré : un unique `PRX_01` monté au niveau 100, soit 2,7 M$ dans
une économie qui atteint 1e13, annulait toute la trace du jeu définitivement. Le joueur ne
mourait plus que s'il le décidait, et il n'existait aucun arbitrage.

**Trois propriétés de la nouvelle forme :**

1. **L'asymptote n'est jamais atteinte.** Une fraction de la trace passe toujours, donc la jauge
   monte toujours. L'invulnérabilité n'est plus un réglage à ne pas rater : elle est
   arithmétiquement hors d'atteinte.
2. **Le coût de chaque tranche explose.** Passer de 42,5 % à 76,5 % de réduction demande 9× plus
   de puissance ; atteindre 84,9 % en demande 849×. C'est ce qui rend **tout plafond de niveau
   inutile** — le rendement décroissant *est* le plafond, et il est naturel plutôt qu'arbitraire.
   Vérifié : tout au niveau 1000, Proxies compris, donne 18,7 % de réduction seulement.
3. **Seul le RATIO `D/Brut` compte, jamais la magnitude.** La formule se comporte à l'identique à
   1e0 et à 1e13. Conséquence voulue et centrale : **faire grossir son économie dilue les Proxies
   déjà achetés**. Défense figée à 100 niveaux de `PRX_01`, en poussant la production :
   84,4 % → 62,8 % → 3,6 %. Le joueur doit réinvestir en permanence, ou assumer le risque.

Il n'y a **pas** de plafond par Proxy : les quinze versent dans un `D` unique et c'est le *pool*
qui sature. Des budgets séparés feraient une check-list à remplir, pas un arbitrage.

La réduction de prestige s'applique **avant** la réduction des Proxies.

#### La TRAQUE PASSIVE — le temps est un coût (tranché le 2026-09-10)

L'A.M.I. te cherche activement. Rester connecté coûte, **indépendamment de ce que tu produis** :
un débit constant s'ajoute au brut, réglé par `PassiveTraceFillMinutes` (25 min à l'origine,
**6 min depuis le 2026-09-12** — voir « La cible des 30 minutes est abandonnée »).

#### ⚠️ Elle ne démarre QUE lorsque le joueur s'est manifesté (corrigé le 2026-09-21)

La traque s'appliquait **dès la première frame de la run**, avant tout achat. Relevé en jeu : le
joueur regardait sa jauge monter sans avoir encore rien fait — *« je suis puni sans avoir commencé
à jouer »*. Absurde côté règles, et contraire au lore : l'A.M.I. cherche un intrus, pas une machine
éteinte.

Deux manifestations l'arment, et l'une suffit :

| Condition | Pourquoi |
|---|---|
| Avoir volé sa **première Data** | Le piratage a eu lieu, il y a quelque chose à repérer |
| Posséder du **Hardware** | Il chauffe en permanence dès l'achat — il trahit sans rien rapporter |

**Aucune des deux ne redescend pendant une run**, ce qui est le point : un simple test sur le débit
des Scripts aurait allumé et éteint la traque entre deux cycles manuels, et la jauge aurait
clignoté en début de partie.

**Aucune faille ouverte** : ne rien faire ne rapporte rien, et le seul moyen de retarder la traque
est de ne pas jouer. Un Script acheté mais jamais lancé n'émet rien, donc n'attire rien — c'est
cohérent, pas un trou.

Mesuré : 180 s d'inaction totale laissent la jauge à **0,000 %** ; elle démarre à la seconde où un
cycle s'achève ou un Hardware est posé.

**Le constat qui l'a rendue nécessaire.** La Trace ne mesurait QUE la production du joueur. Elle
était donc une horloge *proportionnelle* — jouer faiblement achetait du temps illimité. Mesuré sur
la première run, en n'achetant que de l'acquisition (ni Hardware ni Proxy) :

| | Sans traque passive | Avec |
|---|---|---|
| Durée de la run | **92,7 min** | **22,1 min** |
| Jauge à 5 min | 0,2 % | 20,6 % |
| Jauge à 20 min | 6,3 % | 86,9 % |

Le joueur rampait pendant une heure et demie sans que rien ne vienne jamais le chercher. Il n'y
avait **aucune pression absolue** dans le jeu : le seul chronomètre était celui que le joueur
remontait lui-même. Confirmé en jeu réel, pas seulement en simulation.

**Quatre conséquences, toutes voulues :**

- **Le temps redevient un coût.** Traîner n'est plus gratuit.
- **Les Proxies ont un rôle dès la première minute.** La traque entre dans le brut, donc elle
  passe par la dissipation. Avant, ils n'avaient rien à dissiper avant la vingtième minute — d'où
  l'impression, juste, qu'ils ne servaient à rien au début.
- **Elle s'efface toute seule.** 0,067 fraction/s écrase une production naissante et devient
  négligeable face à une économie mûre. La traque domine le début puis disparaît, sans second
  réglage.
- **Le Blindage garde son sens.** Le débit dérive du plafond **DE BASE**, jamais du plafond
  courant : relever le plafond dilue donc la traque et achète du temps. L'indexer sur le plafond
  courant la rendrait inesquivable, et viderait toute la branche Blindage de son intérêt.

⚠️ **Ne jamais indexer la traque sur le plafond courant.** C'est le piège évident, et il annule à
la fois le Hardware et le Blindage.

**Rythme obtenu** (campagnes complètes, arbre mené à son terme) :

| Palier visé | Runs | Durée de campagne |
|---|---|---|
| 50 % | 58 | 13,29 h |
| 75 % | 32 | **10,30 h** |
| 90 % | 25 | 8,58 h |

La cible de 10 heures est désormais atteinte par un **style de jeu**, pas par une constante à
caler : le prudent fait une campagne longue, le téméraire une campagne courte.

#### Le PLAFOND suit la puissance, et la Furtivité décide de la durée (tranché le 2026-09-15)

Le plafond de jauge dérive du même agrégat que la Trace, et avec le même exposant. La durée d'une
run devient donc **invariante à la puissance**.

**Le défaut corrigé.** La Trace est alimentée par TOUTE la production, mais le plafond ne venait
que de `traceCapIncrease`, une statistique secondaire portée par les objets Hardware. Acheter des
Scripts faisait monter le numérateur sans le dénominateur. Mesuré : la durée de run s'effondrait de
vingt minutes à trois, puis se figeait — chaque gain de puissance était immédiatement reconverti en
run plus courte, jusqu'à un point fixe. Les 10 derniers pour-cent de jauge finissaient par passer en
deux secondes, rendant le dernier palier d'extraction arithmétiquement injouable.

| | Plafond statique | Plafond dynamique |
|---|---|---|
| Durée des runs, sur 202 runs | 20 min → 3 min, puis figée | **3,4 → 3,4 → 3,4 → 3,8** |
| Marge après le palier 90 % | 2 s | 10 s à 1,2 min |
| Saisies, posture agressive | 15 | **1** |

**La Furtivité devient le seul levier de durée**, et c'est visible dans les chiffres :

| Dissipation tenue | Durée de run |
|---|---|
| 23,0 % | 3,4 min |
| 51,5 % | 5,6 min |
| 61,1 % | 7,0 min |

C'est la relation `1/(1−R)`, bornée à ×6,67 par l'asymptote. **La Furtivité ne pouvait PAS suppléer
au plafond statique** : son asymptote laisse passer 15 % du brut quoi qu'il arrive, alors que le
brut croît sans borne. Mais une fois le plafond rendu dynamique, elle hérite du rôle entier.

⚠️ **Le facteur se calcule en RAPPORT à un rendement de référence**, jamais en valeur absolue.
Sans cette normalisation il multiplie un plafond de base à sept chiffres : mesuré, cent cinquante
minutes de run sur les quatre postures, le joueur littéralement immortel. L'exposant était bon, la
constante fausse de six ordres de grandeur.

**Ce que le Hardware perd, et ce qu'il garde.** Il perd `traceCapIncrease`, une statistique
secondaire. Il garde l'intégralité de son rôle : les TFlops n'alimentent QUE les Scripts, en
majorant leur rendement et en raccourcissant leurs cycles. Vérifié dans le code avant de trancher.
Les rôles en ressortent plus nets qu'avant — Scripts : ce que tu gagnes. Hardware : la vitesse et
la force avec lesquelles tu le gagnes. Furtivité : combien de temps tu tiens.

Le réglage reste **basculable** (`DynamicTraceCap`), pour pouvoir mesurer les deux sans toucher au
code.

#### La trace suit le rendement, à un exposant strictement entre 0 et 1

**Tranché le 2026-08-31, annule et remplace le « coût de surface » du 30/08.**

```
TraceParCycle = BaseTrace × BaseCycleDuration × (Puissance × BoostTFlops) ^ TraceYieldExponent
TracePerSeconde = TraceParCycle / DuréeCycleCourante
```

`Puissance` = niveau × paliers de rendement. Le Hardware suit la même règle, mais en continu.
Le Proxy garde un couplage **linéaire** : sa magnitude est une dissipation, l'amortir rendrait un
Proxy de niveau 50 à peine meilleur qu'un de niveau 1.

C'est la synthèse de deux échecs successifs. À l'exposant **1** (avant le 30/08), monter un
générateur accélérait les gains ET la mort dans la même proportion : une run rapportait 133 Datas
quoi que fasse le joueur. À l'exposant **0** (le coût de surface du 30/08), la génération devenait
bornée pour toujours et la défense gagnait mécaniquement. À **0,6**, doubler son rendement ne
multiplie la trace que par 1,52 : progresser paie, sans jamais supprimer le danger.

Le facteur `BaseCycleDuration` convertit la donnée historique — exprimée par seconde — en quantité
par cycle. Au niveau 1, sans palier ni TFlops, les deux écritures coïncident exactement : **aucune
des 45 valeurs de trace des JSON n'a eu besoin d'être réécrite.**

#### Le Hardware n'est plus un pilier mort

**Tranché le 2026-08-31.** `minCycleDuration` bornait la compression, et les seuils de saturation
étaient dérisoires : `SCR_15` à **4 TFlops**, `SCR_14` et `SCR_03` à **10**, médiane à 55 — alors
que `HW_01` niveau 10 en fournit déjà 40. Passé ce mur, le Hardware n'apportait plus rien
économiquement. Constaté en simulation : après 60 min, une IA d'achat au meilleur ROI avait
`SCR_01=252` mais `HW_01=13, HW_02=2`.

Deux changements :

```
DuréeCycle = DuréeBase / (1 + TFlops × k) ^ TFlopsCompressionExponent     (plus de mur par générateur)
RendementScript × = (1 + TFlops) ^ TFlopsYieldExponent                    (nouveau)
```

`minCycleDuration` **n'est plus lu** — comme `durationReductionPerLevel`, le champ survit dans les
données sans être utilisé. Un plancher **absolu** unique, dans `BalancingConfig`, sert de garde-fou
de boucle au `ScriptCycleRunner`.

Le boost de rendement est **réservé aux Scripts** : chez un Hardware le rendement EST la capacité
en TFlops, donc le brancher là créerait une boucle divergente.

Les deux effets ne jouent pas le même rôle face au risque. La **compression** accélère l'argent et
la trace dans la même proportion, et depuis le correctif ci-dessus elle accélère aussi la
dissipation : elle est donc neutre sur le risque, et n'est plus qu'un accélérateur d'horloge. Le
**boost de rendement**, lui, passe par l'exposant 0,6 : il rapporte plus qu'il ne coûte en trace.
C'est le versant récompense du pilier.

#### Le plafond de jauge suit la puissance, plus le seul niveau

**Tranché le 2026-08-31 — annule l'asymétrie posée le 30/08.**

```
PlafondHardware = traceCapIncrease × puissance ^ TraceYieldExponent
```

Il valait `base × niveau`, et le GDD assumait cette asymétrie — « approfondir une machine améliore
son refroidissement sans augmenter son encombrement » — tant que la trace générée était
forfaitaire. Elle suit désormais `puissance^0,6` : un plafond resté linéaire décroche aussitôt.

Mesuré, et c'est sans appel : **avec l'arbre de prestige entièrement acheté, le brut était
multiplié par 98 quand le plafond ne gagnait que 8 %** — une run de fin de partie tombait de
1 766 s à **82 s**. Plus le joueur progressait, plus ses runs se raccourcissaient, jusqu'à rendre
la méta-progression impossible.

Les deux grandeurs partagent donc l'exposant, ce qui rend la contribution du Hardware à la survie
constante en proportion, donc **calable** : le rapport entre `traceCapIncrease` et
`traceGeneratedPerSecond` d'un palier dit à lui seul combien de secondes de survie cette machine
achète contre sa propre chaleur. Il vaut ≈ 60 sur les quinze paliers actuels.

**Génération de la Trace :** Un Script génère de la Trace **uniquement pendant qu'un cycle tourne**. Cela renforce le concept de risk/reward (l'A.M.I. ne repère le piratage que lorsqu'il est actif), particulièrement en début de partie quand le lancement est strictement manuel.

**Le Hardware, lui, chauffe en permanence** (tranché le 2026-08-26) : sa `traceGeneratedPerSecond` s'applique en continu dès l'achat, puisqu'il n'a pas de cycle. C'est ce qui maintient un risque sur l'onglet le plus rentable — sans quoi le Hardware deviendrait un achat sans contrepartie.

**Attention au nom du champ.** `traceGeneratedPerSecond` porte une **génération** pour les Scripts et les Hardware, mais une **dissipation** pour les Proxies (dont le `baseProductionYield` vaut 0). Le nom ment pour ce troisième type : c'est la valeur qui alimente `ProxyBase` dans la formule de dissipation ci-dessus, et elle doit être **soustraite**, jamais ajoutée.

**Synergie des Proxies — RETIRÉE le 2026-09-10.** Chaque niveau de Proxy accélérait tous les
Scripts de 1 %, pour qu'un achat de Proxy ne soit « jamais perdu » même quand la Trace était basse.
Elle donnait de l'offense au pilier défensif et brouillait la lecture des rôles. Voir « LA
TRINITÉ » en tête de document, qui fait foi.

**La règle d'or tient, confirmée le 2026-08-27.** Un excédent de dissipation ne fait PAS redescendre la jauge : il est capté par le Ghost Cache. Si la jauge se vidait, le joueur aurait toujours toute la marge devant lui et déclencher l'Overdrive ne coûterait rien. En la laissant où elle est, le Ghost Cache se remplit à la hauteur où le joueur s'est arrêté : à 30 % il a de la marge, à 80 % c'est un pari. C'est là que naît le choix « j'exfiltre ou je charge encore ».

#### Où la règle est appliquée

| Chaîne | Exigence | Raison |
|---|---|---|
| Overclock — `P_OVERCLOCK_POWER_2`, `_3`, `P_OVERCLOCK_AWAKE` | `requiredLevel: 0` (le max, soit 5) | Cinq rangs par palier : au premier rang, toute la ligne s'ouvrait d'un coup |
| Blindage — `P_HARDEN_2`, `_3`, `_4` | `requiredLevel: 3` | Voir ci-dessous |

**Pourquoi 3 et non le maximum sur le Blindage (tranché le 2026-09-09).** `P_HARDEN_4` verse
**cent fois** le bonus de `P_HARDEN_1` par rang (1,0 contre 0,01). Le chemin le plus court jusqu'à
lui, avec un coût de rang valant `1,5^(k-1)` :

| Règle | Cycles pour ouvrir `P_HARDEN_4` | Plafond obtenu au passage |
|---|---|---|
| Niveau 1 (avant) | **4,0** | ×2,26 |
| **Niveau 3 (retenu)** | **15,25** | ×2,78 |
| Niveau max | 340 | ×4,39 |

Exiger le maximum coûterait une quinzaine de runs rien que pour ouvrir la fin de la branche —
punitif, et cela **forcerait le joueur à maximiser contre sa volonté**. Le palier 3 multiplie le
ticket d'entrée par 3,8 tout en donnant 23 % de plafond en plus au passage, puisqu'il impose
d'acheter les rangs intermédiaires : il retarde sans punir. Rapporté au rythme calé (~22 cycles
en run 1), la chaîne passe de 18 % à 69 % du budget de la première run.

⚠️ **Chiffrage arithmétique, toujours pas simulé.** Le simulateur a été réécrit le 2026-09-09 dans
`tools/balance/` (versionné cette fois, et il lit `BalancingConfig.asset` directement). Son
**modèle de run est vérifié fidèle** — il reproduit au chiffre près les mesures du tableau
ci-dessus. Sa **boucle de campagne ne l'est pas** : là où ce document consigne 19 runs / 9,93 h /
arbre complet, la réécriture donne 11 runs / 16 h / 18 %, quelle que soit la règle de sortie de
run testée. L'agent réécrit est environ deux fois plus faible en fin de partie.

L'effet des paliers de prérequis sur la durée totale reste donc **non mesuré**. Ce qu'on sait :
le palier coûte ~15 cycles sur un arbre de 3 477, soit **0,4 %** — l'ordre de grandeur rend un
dérapage des 10 h très improbable, mais ce n'est pas une confirmation.

L'entrée de branche (`P_HARDEN_1` derrière `P_STEALTH`) reste au niveau 1 : ouvrir une branche et
la parcourir sont deux décisions différentes.

### Branche Blindage — la colonne vertébrale de la méta

**Ajoutée le 2026-08-31.** Quatre nœuds (`P_HARDEN_1` à `_4`, dix rangs chacun, derrière
`P_STEALTH`) portant un nouveau `bonusType` : **`TraceCapacityMultiplier`**, qui multiplie le
plafond de la jauge — base comprise, donc utile dès la première run.

**Pourquoi elle existe.** Sans elle, la campagne était un tapis roulant : **125 runs identiques de
quatre minutes**, mesurées en simulation. Aucun des 134 nœuds ne déplaçait la contrainte qui met
fin à une run — la jauge se remplissait au même point quels que soient les bonus achetés. Le
plafond est le SEUL levier qui fasse grandir une run, et l'écart est brutal :

| Levier, arbre de prestige complet | Durée de run | CPU Cycles |
|---|---|---|
| référence | 236 s | 6 |
| plafond ×10 | 563 s | 70 |
| plafond ×1000 | 2 016 s | **80 050** |
| asymptote de défense 0,85 → 0,98 | 238 s | 6 *(nul)* |

La boucle qui manquait : survivre plus longtemps → construire plus haut → gagner plus → racheter
du plafond. C'est elle qui rend une campagne progressive.

⚠️ **`TraceCapacityMultiplier` est appendé en FIN d'enum**, comme tous les types depuis l'incident
du 30/08 : l'index est sérialisé dans les `.asset` générés, une insertion au milieu redéfinirait
silencieusement des nœuds existants.

### Rythme — calé le 2026-08-31, corrigé deux fois le 2026-09-01

**Deux échecs successifs, et les deux leçons sont structurantes.**

**Échec 1 — ralentir le début.** Pour atteindre « 30 minutes avant le premier prestige », le
premier calage avait divisé **tous** les rendements par 10 et alourdi **toutes** les courbes de
coût. Mesuré en jeu : `SCR_01` atteignait son automatisation en **1 256 s au lieu de 69 s**.

> **Règle 1 : on ne ralentit JAMAIS le début pour allonger une run.** Le frein se place plus loin.

**Échec 2 — l'explosion de la cinquième minute.** Avec les rendements restaurés, la production
passait de 7 000 Datas à la 2ᵉ minute à **1,7 milliard** à la 5ᵉ. Le joueur brûlait toute
l'économie du jeu dans sa première run, et le prestige ne servait plus à rien.

> **Règle 2 : la montée en puissance doit venir des bonus de prestige, pas de l'intérieur d'une
> run.** Cible fixée par le GD : quelques **dizaines de milliers** de Datas à la 5ᵉ minute.

#### Ce qui freine, et ce qui ne freine pas

Le coupable de l'explosion n'était ni les rendements ni le prix des nœuds de prestige (ceux-ci
coûtaient **0,01 CPU Cycle** — vérifié) mais deux choses :

| Levier | Effet sur les 10 premiers niveaux | Effet au niveau 190 |
|---|---|---|
| `costMultiplier` **1,07 → 1,15** | 138 $ → 203 $ *(négligeable)* | 5,5e7 $ → **2,3e13 $** |
| Paliers **`facteur ^ 0,4`** | aucun *(le 1ᵉʳ palier est au niveau 10)* | empilement ×300 → **×4** |

C'est le levier chirurgical cherché : **le début est intact, la profondeur devient chère.** Les
`baseProductionYield` restent ceux d'origine et ne doivent plus jamais servir de variable de
rythme.

#### Mesures obtenues, sur les fichiers du projet

| Mesure | Valeur | Cible |
|---|---|---|
| `SCR_01` niveau 10 (automatisation) | **75 s** | rapide ✅ |
| Datas générées à la 5ᵉ minute | **40 066** | quelques dizaines de milliers ✅ |
| Première run | **9,7 min**, jusqu'à `SCR_04` | ~10 min ✅ |
| Campagne complète (arbre entier) | **10,69 h** au palier 75 % | ~10 h ✅ |
| Nombre de runs | 39 à 45 selon le palier visé | — |

### ⚠️ La cible « 30 minutes avant le premier prestige » est ABANDONNÉE (2026-09-12)

Elle était une erreur, et c'est une partie jouée qui l'a tranchée, pas un calcul.

**Le verdict manette en main :** « je trouve la run longue et ennuyante ; passé les cinq premières
minutes cela devient long, les gains ne sont pas énormes donc on achète très rarement quoi que ce
soit, le spam d'Overclock devient la seule solution, mais cela épuise vite ».

**Ce que la mesure a confirmé.** Sur une run de 25 minutes, relevé minute par minute :

| Minute | 5 | 10 | 15 | 20 | 24 |
|---|---|---|---|---|---|
| Achats dans la minute | 29 | 14 | 10 | 6 | 78 |
| Croissance des Datas | ×3,97 | ×1,46 | ×1,26 | ×1,15 | ×1,11 |

**La phase exponentielle meurt vers la minute 12.** Après, la run est linéaire : +10 à 15 % par
minute, activité d'achat divisée par trois. Un incrémental linéaire est un incrémental ennuyeux.
Les vingt dernières minutes n'ajoutaient rien qu'une attente.

**Le réglage.** `PassiveTraceFillMinutes` passe de 25 à **6**, ce qui donne une première run de
**9,7 minutes**. La courbe d'achats des premières minutes est rigoureusement identique — on n'a
retiré aucun moment intéressant, on a coupé la partie plate. La run se termine désormais au PIC
d'activité (61 achats à la minute 7) et en pleine exponentielle (×1,69 sur la dernière minute).

⚠️ **Ce réglage n'est pas un cadran direct sur la durée de run.** Le Hardware relève le plafond et
dilue la traque : à 12 minutes de remplissage, la run durait encore 19,9 minutes. Toujours mesurer
plutôt que déduire.

**Conséquence heureuse et non conçue :** les runs S'ALLONGENT au fil de la campagne — 9,7 min au
départ, ~14,5 min en moyenne — parce que le Blindage relève le plafond. La méta-progression achète
littéralement du temps de jeu par run.

**Conséquence à assumer :** viser le palier 50 % ne boucle plus l'arbre (141 runs, 87 % au bout de
16 h). Jouer très prudemment est devenu perdant, pas seulement lent.

#### ⚠️ Le simulateur ne clique jamais l'Overclock

Mesuré le 2026-09-12 : première run réelle **28 CPU Cycles**, même run simulée **3**. Le harnais ne
modélise aucun clic, alors qu'un joueur spamme l'Overclock — qui avance tous les Scripts en cours.
L'écart est d'environ deux ordres de grandeur sur les Datas.

⚠️ **Le « 28 » est PÉRIMÉ** — il précède d'un jour le passage des points au cumul de campagne, et
a donc été produit par une formule abandonnée. Il ne constitue **aucune cible**. La cible en vigueur
est fixée plus bas, section « Ce que doit rapporter une première run ». Ce qui survit de ce
paragraphe, c'est l'angle mort du harnais, pas le chiffre.

**Toute mesure du harnais est donc une BORNE BASSE**, jamais une prédiction. C'est aussi ce qui
expliquait un écart relevé plus tôt : une jauge à 27 % au bout de vingt minutes en partie réelle,
contre 6 % en simulation.

La première run monte jusqu'à `SCR_05` en pyramide (`84 / 60 / 42 / 23 / 9`) : le joueur achète
sans arrêt, il n'y a ni plateau mort ni empilement dégénéré sur un seul générateur.

#### La branche Blindage est le moteur de la campagne

Sans elle, les Datas ne progressaient que de ×13 sur toute la campagne — 90 runs identiques. Avec
elle calée à `+0,01 / +0,05 / +0,2 / +1,0` par rang (plafond ×13,6 au maximum), elles font **×660**
et les runs s'allongent de 19 à 41 minutes.

⚠️ **Piège vécu :** un facteur d'échelle appliqué deux fois avait ramené ses bonus à 0,00016 par
rang. La branche devenait inerte et la campagne redevenait un tapis roulant — sans que rien ne le
signale. Toute modification du Blindage doit être suivie d'une mesure de la croissance des Datas
sur une campagne complète.

#### `DissipationHalfPointRatio` (κ) décide si la défense vaut le coup

Ramené à **0,3**. À cette valeur, les Proxies sont inutiles en début de partie — le joueur est trop
petit pour être repéré — deviennent payants en milieu, puis redeviennent un mauvais achat en fin de
campagne, où le budget doit repartir vers la production.

⚠️ **κ est à revérifier après CHAQUE modification des magnitudes de trace ou de plafond.** Il ne
porte pas une valeur absolue mais un rapport, et tout changement d'échelle le déplace. Il a dû être
repris trois fois pendant ce calage (0,3 → 10 → 0,3).

### Ghost Cache et Zéro-Day Exploit

> ⚠️ **État au 2026-10-13 : la mécanique décrite ci-dessous est en vigueur dans le code, mais ne se
> déclenche plus** (zéro fois sur 264 runs simulées). Elle est condamnée : sa refonte est tranchée,
> pas implémentée.
>
> **Le BAROUD D'HONNEUR (tranché par le GD le 2026-09-16)** remplace la charge :
>
> | | Aujourd'hui | Baroud d'Honneur |
> |---|---|---|
> | Accès | Charge de 90 s de posture défensive tenue (`GhostCacheCapacitySeconds`) | **N usages par run**, N donné par `P_EXPLOIT_CHARGES` |
> | Durée | 30 s | **3 à 5 s** |
> | Rendement | ×50 sur les Scripts | ×50, inchangé |
> | Malus de Trace | ×10, dissipation coupée | ×10 conservé |
> | Sauvegarde | `GhostCacheSeconds` (v4) | Le champ sort du format → `SaveData` v11 |
>
> Pourquoi : les conditions d'activation sont devenues inatteignables — la charge exige plus de
> posture tenue qu'une run de campagne n'en offre. La justification d'origine était double
> (« la durée n'est jamais tenue » aussi) ; seule la première tient, voir plus bas. Raccourcir à
> 3-5 s reste un choix de design, plus une réparation.
>
> **Question ouverte** : « N = rangs de `P_EXPLOIT_CHARGES` », mais ce nœud n'a plus qu'**un
> rang**, de bonus 5, depuis l'aplatissement de l'arbre. N vaut-il le bonus ?
>
> Valeurs du code à cette date, qui diffèrent des tableaux ci-dessous : capacité **90 s** (et non
> 300), `P_EXPLOIT_CHARGES` 1 rang de bonus 5, `P_EXPLOIT_MULT` 5 rangs, `P_EXPLOIT_TRACE_REDUC`
> 3 rangs de 2,5.

Valeurs tranchées par le GD le 2026-08-27. L'excédent de dissipation, jusque-là purement jeté, alimente une charge unique échangeable contre un pic de production.

| Paramètre | Valeur | Raison |
|---|---|---|
| **Capacité de la jauge** | **300 secondes d'excédent** | Exprimée en temps et non en magnitude, sinon un joueur de fin de partie la remplirait instantanément |
| **Durée de l'Exploit** | **30 secondes** | Assez long pour regarder la Trace monter et le regretter |
| **Multiplicateur** | **×50 sur le RENDEMENT** | Pas sur la vitesse : `minCycleDuration` bornerait l'effet et le gain deviendrait imprévisible d'un Script à l'autre |
| **Contrepartie 1** | **Dissipation → 0** | Tous les Proxies s'éteignent |
| **Contrepartie 2** | **×10 sur la génération brute de Trace** | Ajouté le 2026-08-27. La jauge se remplit dix fois plus vite en plus de n'être plus dissipée |

**La charge se remplit en TEMPS, pas en magnitude.** Une seconde de posture défensive tenue vaut une seconde de charge. Le prix à payer est un maintien de posture, pas un empilement de Proxies. ⚠️ Conséquence à surveiller : rien ne récompense une dissipation massive, seulement sa durée.

**Condition de charge revue le 2026-08-31.** Elle captait « l'excédent de dissipation ». Cette notion n'existe plus : la réduction étant asymptotique, la trace monte toujours et il n'y a plus rien à jeter. Le seuil porte désormais sur la **fraction de réduction tenue** (`GhostCacheReductionThreshold`, en part de l'asymptote). ⚠️ **Charger n'est plus gratuit** — la jauge continue de grimper pendant l'accumulation, là où l'excédent mettait le joueur à l'abri. Se constituer une réserve devient un pari sur la jauge, ce qui est le propos même de la mécanique.

**Le ×50 ne touche QUE les Scripts.** Chez un Hardware, le « rendement » EST sa contribution en TFlops : le laisser passer multiplierait par 50 la capacité de calcul, donc la compression des cycles *et* la dissipation des Proxies. Un buff économique deviendrait une invulnérabilité.

⚠️ **Le ×10 NE casse PAS l'Exploit — corrigé le 2026-09-16.** Ce paragraphe a longtemps annoncé
une saisie à **1,58 s**, donc un bouton inutilisable. C'était vrai, et ça ne l'est plus : le calcul
`100 / (génération × 10)` supposait un plafond de jauge de **100**, alors que `BaseTraceCap` vaut
**5e6** depuis la refonte du 31/08. La conclusion est restée dans le document deux semaines après
que sa prémisse a disparu.

**Mesuré sur le code actuel**, avec l'exemple d'origine (`SCR_01` niveau 12 + `HW_01` niveau 4) :
l'Overdrive **va au bout de ses 30 secondes**, et la partie est encore vivante à l'arrivée.

| | Valeur |
|---|---|
| Durée tenue | **30,0 s** sur 30 nominales |
| Jauge à la fin | **81,8 %**, partie active |
| Jauge consommée | ~81 points |

⚠️ **Le pari est donc réel, et il est dans le bon sens :** consommer 81 points de jauge veut dire
que déclencher l'Exploit **au-dessus de ~19 % de jauge tue avant la fin des 30 s**. Le joueur
encaisse le ×50 entier s'il le lance tôt, et joue sa run s'il le lance tard. C'est exactement
l'arbitrage que cette section cherchait à créer — il existait déjà, personne ne l'avait mesuré.

⚠️ **Leçon de méthode.** Une conclusion chiffrée survit à la constante qui l'a produite. Toute
mesure consignée ici doit nommer les réglages qui la sous-tendent, faute de quoi elle devient un
fait acquis que plus personne ne rejoue.

#### Les trois nœuds de prestige de l'Exploit

Ajoutés le 2026-08-27, branche Économie, tous trois derrière `P_EMERG`.

| Nœud | Rangs | Par rang | Au maximum |
|---|---|---|---|
| `P_EXPLOIT_CHARGES` | 5 | +1 charge stockable | 5 charges, soit 1 500 s de réserve |
| `P_EXPLOIT_MULT` | 10 | +10 % de rendement | ×100 au lieu de ×50 |
| `P_EXPLOIT_TRACE_REDUC` | 5 | −0,5 sur le malus de Trace | ×7,5 au lieu de ×10 |

**Le Zéro-Day Exploit est VERROUILLÉ au départ.** `P_EXPLOIT_CHARGES` vaut 0 tant qu'il n'est pas
acheté, donc la capacité du Ghost Cache est nulle et **rien ne s'accumule** — pas de réserve
fantôme qui attendrait un déblocage. Le bouton porte un quatrième état, verrouillé, qui nomme le
nœud à acheter plutôt que de rester gris et muet.

Le malus de Trace est borné à ×1 : le joueur peut l'alléger, jamais le supprimer ni le retourner
en bonus.

**Cas limites, tranchés par le GD :**

- **Mort pendant l'Exploit** → la charge est perdue. C'est la punition pour avoir mal calculé son pari.
- **Interruption manuelle** → impossible. Une fois lancé, l'Exploit va jusqu'au bout.
- **Wipe** → la charge est effacée, même pleine. C'est de l'état de run. La laisser survivre offrirait un Exploit gratuit au démarrage de chaque run, exactement quand la Trace est à zéro et où il ne coûterait donc rien.
- **Fermeture du jeu** → la charge est sauvegardée (`SaveData` v4). Elle représente du temps de jeu investi.
- **Débordement** → plafonne à 1 charge, pour forcer la consommation plutôt que la thésaurisation.

*Précision technique : un Exploit EN COURS n'est pas sauvegardé — la charge ayant déjà été consommée, fermer la fenêtre pendant les 30 s revient à les perdre. Sauvegarder un effet temporaire permettrait de le mettre en pause par fermeture du jeu, dans un jeu qui n'a justement aucune progression hors-ligne.*

La jauge elle-même n'est réduite que par le **Bouton d'Urgence** (voir plus bas) et par le wipe. Règle d'or : *« le FBI n'oublie jamais, sauf si tu formates tout. »* Il faut un mur qui pousse inévitablement au wipe — sinon le joueur trouve une planque parfaite (`ΔTrace = 0`) et le danger disparaît.

### Refonte de l'arbre — intentions du GD (2026-09-25)

Le GD a annoncé une **passe massive** sur l'arbre : 120 des 146 nœuds sont du remplissage généré
(deux par générateur, tous identiques), et l'ensemble a cessé d'être maîtrisable. Ce qui est tranché :

- **L'arbre pourra être complété, sans que ce soit garanti.** Finir l'histoire n'exige pas l'arbre
  complet ; selon le style de jeu, certains l'achèveront, d'autres non. ⚠️ Le cumul d'une campagne
  **n'a donc pas à dépasser** le prix de l'arbre entier : il doit atterrir dans le même voisinage.
  Un écart de quelques points en fin de campagne est normal, un écart de cinquante ne l'est pas.
- **La barre d'espace (Overclock au clavier) et le réveil deviennent des nœuds tardifs**,
  conditionnés par « avoir dépensé X points de prestige ».
- **L'automatisation des Scripts au niveau 10 disparaît** : elle rend le jeu trop simple trop vite.
- **Le Data Wiper reste payé en TFlops** : une raison de plus de monter le Hardware.

**Ce qui manque avant de pouvoir dessiner dessus** (lot 2 de la feuille de route) : un prérequis
« X points dépensés » — `PrestigeRequirement` ne connaît qu'un couple (nœud, niveau) — et le retrait
de l'automatisation. Les sources de l'arbre sont les JSON de `GameData/Editor/PrestigeData/`,
`04_SpecificUpgrades.json` excepté (sortie de générateur, voir plus haut).

**Couplage à connaître :** ces deux chantiers ne peuvent pas se calibrer séparément. Avec un arbre
de 1450 niveaux, 4 points en achètent 4 — la production ne bouge pas, le cumul monte linéairement
pendant que les seuils montent en exponentielle, et la progression se bloque DÉFINITIVEMENT.

### ⚠️ Le simulateur CLIQUE, depuis le 2026-09-13

Le harnais ne modélisait aucun clic d'Overclock. C'était son plus gros angle mort :

| Première run | Datas | Points |
|---|---|---|
| Partie réelle | 196 M | 28 |
| Simulée sans clic | 2,9 M | 0 |
| Simulée avec clic | **107 M** | **5** |

Un facteur **87**. Toute grandeur exprimée en Datas absolues — paliers de points, seuil
d'exfiltration — était calibrée sur un joueur qui n'existe pas.

⚠️ **CES TROIS LIGNES SONT PÉRIMÉES — ne pas s'en servir pour calibrer quoi que ce soit.** Elles
datent du 2026-09-12, soit la VEILLE du passage des points au cumul de campagne : la colonne
« Points » a été produite par une formule qui n'existe plus. Et l'économie a bougé d'un ordre de
grandeur depuis — mesuré en jeu le **2026-09-16**, une première run réelle ne fait plus 196 M de
Datas mais **15,7 M**.

Ce qui reste vrai de ce tableau : le simulateur sous-estimait le joueur d'un facteur 87 faute de
modéliser le clic. C'était le propos. **Les chiffres, eux, ne décrivent plus le jeu.**

**Conséquence rétroactive à connaître :** la mesure « 40 066 Datas à la 5ᵉ minute », consignée
plus haut comme conforme à la cible, a été prise sans clic. Avec clic, c'est **18 M**. La cible
« quelques dizaines de milliers » n'a jamais décrit une partie réelle.

**Le modèle est une MAIN, pas un auto-clicker.** Cadence de rafale, part du temps réellement passée
à cliquer, et fatigue qui allonge les pauses au fil de la run. Les rafales ne sont pas simulées une
à une : l'apport d'un clic étant linéaire et sans temps de recharge, une cadence moyenne donne le
même résultat pour un dixième du coût de calcul.

⚠️ **Le modèle est calé sur UNE SEULE run réelle.** Il reste 1,8× sous elle. C'est infiniment mieux
que zéro clic, mais ce n'est pas une distribution validée : toute conclusion tirée d'un écart
inférieur à 2× est du bruit.

### Tous les réglages vivent dans `BalancingConfig.asset`

Depuis le 2026-08-27, l'équilibrage ne demande plus de toucher au C#. Un ScriptableObject unique,
`Assets/GameData/Balancing/BalancingConfig.asset`, porte **toutes** les valeurs : diviseur de
Trace, compression par TFlop, synergie des Proxies, argent de départ, bonus Clean Exit, Datas par
CPU Cycle, secondes d'Overclock, les quatre réglages du Ghost Cache et les huit du Data Wiper.

Il est modifiable **en Play Mode** : les systèmes lisent les propriétés à l'usage plutôt que de
les recopier au démarrage, donc un changement dans l'inspecteur prend effet immédiatement. C'est
le seul asset du projet qu'on édite à la main sans qu'un générateur ne l'écrase.

Chaque champ porte un tooltip qui dit ce qu'il fait et dans quel sens il pousse. Les `[Range]` ne
sont pas décoratifs : ils empêchent de saisir une valeur qui casserait une formule.

### État des lieux d'avant la refonte — équilibrage, outillage, réglages

La partie chiffrée de `etat-des-lieux.md` au commit `d8465d0`, telle qu'elle était. Elle mesure
l'ancien modèle de Trace avec le harnais supprimé.

#### Équilibrage

##### Ce qui est acquis

| | Valeur |
|---|---|
| Contenu | 25 Scripts · 20 Hardware · 15 Proxies |
| Arbre de prestige | **146 nœuds, 239 rangs**, 1 point par rang *(131 nœuds à un rang, 15 à plusieurs depuis le 2026-10-02)* |
| Condition de victoire | **1e12 TFlops** — l'asset porte **5e11**, à titre provisoire |
| Premier point de prestige | **2e6** de cumul, boîte suivante +12 % (modèle des boîtes) |
| Saisie | verse **30 %** de sa contribution au cumul |
| Pic atteint en campagne | 6,4e11 TFlops *(mesuré le 2026-09-16)* |
| Première run | 10,8 min, palier 90 % à 8,9 min *(idem)* |
| Durée des runs en campagne | stable, ~3,5 à 6,3 min selon la posture *(idem)* |

⚠️ **Toutes les mesures de campagne de ce document datent du 2026-09-16 ou avant.** Elles
précèdent `a772e67` (2026-10-02), qui a changé à la fois le calcul des points, leur premier
seuil, la saisie et le nombre de rangs de l'arbre. Elles décrivent l'ancien réglage.

Les quatre piliers ont chacun un rôle mesurable et distinct :

- **Scripts** — ce que tu gagnes
- **Hardware** — la vitesse et la force avec lesquelles tu le gagnes *(TFlops → Scripts
  uniquement)*, et **ce que tu vois** : les TFlops divisent le brouillard du relevé de Trace.
  Troisième rôle propre, qui n'empiète pas sur les Proxies
- **Furtivité** — combien de temps tu tiens. Seul levier de durée depuis le plafond dynamique :
  23 % de dissipation donnent des runs de 3,4 min, 61 % en donnent 7,0
- **Blindage** *(prestige)* — multiplie le plafond

##### Les chantiers ouverts

###### 1. Tout remesurer après le 2 octobre

`a772e67` a changé quatre choses que **personne n'a mesurées depuis** :

- **Le modèle des boîtes.** N points exigent la SOMME de N boîtes, et non plus le prix de la
  dernière. L'ancien calcul rendait trois points pour deux et demi.
- **Le premier point** passe de 5e6 à **2e6**, pour compenser.
- **La saisie verse 30 %** au lieu de rien (`_seizedContributionRatio`), après une partie réelle
  du 2026-09-22 : 294 M de Datas sur deux morts, zéro point, arbre vide.
- **L'arbre passe de 147 à 239 rangs** : quinze nœuds ont repris des rangs (`P_REFAC` et
  `P_DATAM` à 20, `P_STEALTH` à 10…).

⚖️ **Estimation arithmétique, à cumul de campagne égal** : les boîtes retirent ~19 points, le
seuil à 2e6 en rend ~8, soit **~220 points** par campagne au lieu des ~232 mesurés le 16/09. Or
l'arbre est passé de 147 à 239 rangs. Le surplus de 50 % devient un **déficit d'environ 8 %** —
ce qui rejoint l'intention du GD (le cumul doit atterrir « dans le voisinage » du prix de l'arbre,
cf. GDD § Refonte de l'arbre) mais n'a **jamais été mesuré**. La saisie à 30 % pousse dans l'autre
sens, et `P_STEALTH` saturé (`backlog.md` § 2) fausse probablement tout.

Première chose à faire au lot 3, avant de régler quoi que ce soit : relancer « Comparer les
paliers d'extraction » sur le réglage actuel.

###### 2. Le capteur — à reconfirmer

Le lot « capteurs » a désamorcé la spirale de la posture agressive : le brouillard se rachète en
Hardware, donc le téméraire peut s'outiller au lieu de subir. Mesuré le 2026-09-16, posture 90 % :

| | Sans capteur | Capteur, brouillard 25 s | **Capteur, brouillard 60 s** |
|---|---|---|---|
| Saisies | 15 sur 18 runs | 0 | **2 sur 11** |
| Arbre | 2,7 % | complet | **complet** |
| Contenu | SCR_16 / 25 | SCR_25 / 25 | **SCR_25 / 25** |

Le téméraire meurt parfois et boucle quand même sa campagne. C'est `_traceReadoutMaxInterval` qui
dose l'ensemble. ⚠️ **Un seul passage par configuration, toutes campagnes TRONQUÉES** : un signal
net, pas une validation. Et le réglage a changé depuis (chantier 1).

**Ce qui reste, et qui n'est plus la même question :** la posture 90 % sortait en réalité à
**~68 %** de jauge. Le palier 90 % est verrouillé depuis (`P_EXTRACT_HIGHRISK`) : il faut remesurer
avant de savoir si la question se pose encore.

###### 3. Le Zéro-Day — à refondre

Zéro déclenchement sur 264 runs simulées. Il a marché, puis s'est éteint **trois fois pour trois
raisons différentes** : seuil de charge trop haut, capacité plus longue qu'une run, puis runs
raccourcies. C'est la **charge** qui est cassée, pas l'effet : l'Overdrive tient ses 30 s.

Le GD a tranché le 2026-09-16 : **Baroud d'Honneur** — un burst de 3 à 5 s, ×50 sur le rendement,
malus ×10 conservé, et N usages par run à la place de la charge de 90 s. Spécification dans le GDD,
§ Ghost Cache ; une question reste ouverte sur N (`backlog.md` § 9). Lot 2.

###### 4. La victoire — seuil provisoire, écran jamais vu

`VictorySystem` observe les TFlops et déclenche une fois, au franchissement ; la partie continue
après. `HasWon` est sauvegardé (v10) et restauré **avant** les totaux. L'écran `VictoryPanel` est
monté et câblé. Vérifié hors Play Mode : franchissement, non-redéclenchement, reprise d'une
sauvegarde gagnée, migration v9 → v10. **Le chemin complet n'a jamais été joué** (`a-tester.md`
1.12 à 1.14). Seuil à 5e11 pour tester ; à remonter à 1e12 au lot 3.

---

#### Ce qu'il faut savoir avant de toucher à l'équilibrage

**Sept défauts ont été trouvés en deux jours en septembre, et pas un seul ne venait d'un
réglage.** Tous venaient d'une mécanique que le simulateur n'exerçait pas, ou d'une constante qui
ne suivait pas le reste. Avant de tourner un curseur, vérifier que la chose qu'on mesure est bien
jouée.

⚠️ **Le simulateur est une borne basse, jamais une prédiction.** Il clique l'Overclock depuis le
2026-09-13, mais son modèle est calé sur **une seule run réelle** et reste 1,8× en dessous. Tout
écart inférieur à un facteur 2 est du bruit. Toute grandeur exprimée en Datas absolues (seuil du
premier point, notamment) se pose sur une partie **jouée**, jamais sur une sortie du harnais.

⚠️ **Le réglage en vigueur n'a jamais été joué.** Le dernier retour de jeu consigné date du
2026-09-22 ; `a772e67` a tout changé dix jours plus tard. Chaque session du GD a trouvé un problème
que les mesures ne voyaient pas.

⚠️ **Un changement de réglage se consigne dans le GDD le jour même.** Les trois changements
d'économie de `a772e67` ne l'avaient été nulle part : ni dans le message de commit, ni dans le
GDD, ni ici. Ils ont été retrouvés en comparant `BalancingConfig.asset` à sa documentation.
`BalancingConfig.asset` fait foi sur les valeurs, le GDD sur les intentions ; quand les deux
divergent, c'est un bug de l'un ou de l'autre.

⚠️ **Les fichiers générés sont des SORTIES.** `04_SpecificUpgrades.json` et tous les `.asset` de
`GameData/` sont écrasés par leurs générateurs. Régler une valeur dedans ne survit pas. Les sources
sont `GameData/Editor/**` (sauf `04_SpecificUpgrades.json`) et `PrestigeSpecificNodesGenerator.cs`.

⚠️ **Les valeurs de l'enum `PrestigeBonusType` s'appendent en FIN.** L'index est sérialisé dans les
`.asset` : une insertion au milieu redéfinit silencieusement des nœuds existants. Déjà arrivé.

⚠️ **Un nœud à plusieurs rangs multiplie son bonus par son niveau.** Passer un nœud de 1 à N rangs
sans diviser son bonus multiplie sa puissance par N — c'est ce qui sature `P_STEALTH`.

---

#### Outillage de mesure

Tout est sous `Tools/Core/Équilibrage/` :

| Entrée | Ce qu'elle répond |
|---|---|
| **Comparer les paliers d'extraction** | la mesure principale — 4 postures, durée, contenu, saisies, dissipation, mécaniques utilisées |
| **Campagne complète** / **+ export CSV** / **« joueur gourmand » + export CSV** | une campagne entière, et son détail run par run |
| **Première run — temps avant le Game Over** | le plafond dur d'une run, et la marge après chaque palier |
| **Première run — où se trouve le temps mort ?** | densité d'achats minute par minute |
| **Première run — acquisition seule, minute par minute** | la première run d'un joueur qui n'achète que de l'acquisition : la courbe à confronter à une partie réelle |
| **Et si le joueur évitait une famille de nœuds ?** | ce que coûte l'évitement d'une famille |
| **Test rapide (une run)** | un relevé complet sur une seule run |
| **Fenêtre de simulation** | le poste de pilotage : lancer une simulation, lire les courbes, exporter |

Une campagne est coupée à **45 secondes de calcul** et s'annonce alors « MESURE TRONQUÉE ». Ce
n'est pas « jamais fini » : c'est une mesure incomplète, et les confondre mène à une fausse
conclusion.

**Les générateurs de données**, sous `Tools/Core/`, à relancer **dans cet ordre** après toute
modification de `GameData/Editor/` :

1. **Générer Catalogue Upgrades depuis JSON**
2. **Générer les nœuds ciblés (04_SpecificUpgrades)** — le même générateur s'ouvre aussi en
   fenêtre par « Générer JSON (Grille - Arête de poisson Corrigée) »
3. **Générer l'Arbre depuis JSON (Dossier)** — lire son message : il compte les `.asset` orphelins
   qu'il supprime

Scripts Python versionnés sous `tools/balance/` (réplique indépendante du modèle de run, qui
s'accorde avec le harnais Unity) : `recost.py` aligne la croissance des coûts sur celle des
rendements, `extend_ladder.py` étend une échelle en conservant ses deux extrémités.

---

#### Réglages en vigueur

Relevés dans `BalancingConfig.asset` le 2026-10-13.

```
_dynamicTraceCap              1        le plafond suit la puissance
_dynamicCapReferenceYield     3e+05    baisser = runs plus longues
_victoryTFlops                5e+11    ⚠️ PROVISOIRE — la cible est 1e12
_passiveTraceFillMinutes      6        la traque de l'A.M.I.
_maxTraceReduction            0.85     asymptote de la Furtivité
_dissipationHalfPointRatio    0.3      κ — à revérifier après tout changement d'échelle
_traceReadoutMaxInterval      60       brouillard AU DÉPART, avant rachat
_traceReadoutSensorPower      1        ce que les TFlops achètent en visibilité ; 0 = éteint
_traceReadoutSensorReferenceTFlops 1000  en dessous, le Hardware n'achète PAS de visibilité
_prestigeFirstThresholdDatas  2e+06    première boîte (5e6 jusqu'au 2026-10-02)
_prestigeThresholdGrowth      1.12     +12 % par boîte suivante
_seizedContributionRatio      0.3      part de la contribution versée par une saisie
_ghostCacheCapacitySeconds    90       charge du Zéro-Day (disparaît avec le Baroud d'Honneur)
_ghostCacheReductionThreshold 0.7      × l'asymptote = 59,5 % de réduction à tenir
_emergencyTraceReduction      0.25     Data Wiper : FRACTION de la Trace courante
paliers d'extraction          50 % → +15 % · 75 % → +30 % · 90 % → +50 % (sous P_EXTRACT_HIGHRISK)
```

Le plafond dynamique est **basculable** : décocher `_dynamicTraceCap` restaure l'ancien
comportement sans toucher au code, pour comparer en jouant.

---

## Session de test du GD — 2026-10-13

Première passe sur `a-tester.md`, jouée à la main par Bertrand.

**Vu fonctionner, en éditeur (Play Mode, touches pressées à la main)** — le chantier menu & options
n'avait été vérifié que par clics simulés via MCP :

- 2.1 — Échap referme options, puis pause, puis arbre, et n'ouvre la pause que si rien ne l'était ;
  sur l'écran de fin, il ouvre la pause.
- 2.2 — P et Espace sont ignorés pendant la pause.
- 2.3 — Espace donne un Overclock par appui, sans répétition au maintien, et ne rachète pas
  l'upgrade après un clic sur « Acheter ».
- 2.4 — La pause fige jauge et cycles ; l'arbre de prestige, non.
- 2.7 — Coupures audio indépendantes ; la musique ne redémarre ni en fin de run ni à la pause.

**Vu fonctionner, dans un build de production** :

- 3.1 — Premier lancement : écran de langue, « Commencer », aucune sauvegarde créée sans jouer.
- 3.2 — Exfiltration verrouillée tant que la run ne rapporte pas un point.
- 3.3 — Hors focus : la simulation continue, rendu à 10 FPS, son coupé si demandé.
- 3.4 — Sauvegarde de fermeture, par la croix comme par Alt+F4.
- 3.5 — Plein écran, fenêtré, résolutions, V-Sync, plafond de FPS, taille d'interface.
- 3.6 — Instance unique.

**Soldés sans correction** (constats d'août revus à l'œil) :

- **Nom tronqué sur la pastille d'un nœud** (« Surcadencemen », 2026-08-30) : non reproduit.
- **Fond de l'écran méta à 98 %** : la console se lit bien à travers l'arbre, et c'est **voulu**
  (décision de Bertrand). Ne pas le passer à 100 %.
- **Halo du shader `UI_HDR_Glow`** : au survol, il remplit le bouton d'un aplat lumineux au lieu
  d'épouser son contour. Le rendu plaît : **conservé tel quel** (décision de Bertrand). Ne pas
  « corriger » le shader vers un halo de contour.

**Écartés ou reportés** :

- 2.5, langue à chaud : invérifiable tant que seul `fr.json` existe. Reporté après le lot 6
  (`a-tester.md` § 5).
- 2.6, mode daltonien : « aucune différence à l'écran ». Le code et le câblage des palettes sont
  corrects (relus le 2026-10-13, vérifiés en Play Mode le 2026-10-02) ; c'est le **protocole qui
  était faux** — il demandait de regarder l'arbre en changeant de mode, ce qu'Échap rend
  impossible. Protocole réécrit, ligne maintenue dans `a-tester.md`.

**Trouvés pendant la session**, portés au backlog : chevauchement des boutons du panneau
d'options (§ 4) ; l'inspecteur de nœud qui se chevauche, reconfirmé (§ 4).

## Soldé à la révision du 2026-10-13

Entrées de l'audit du 2026-08-27 retrouvées réglées en relisant le code de `main` (`7b95a5a`).
Elles avaient été corrigées en route, sans que le backlog soit mis à jour.

- **Daltonisme — la palette permutable** (couche 3 de la stratégie d'août). Quatre `UiPaletteSO`
  (Standard, protanopie, deutéranopie, tritanopie), l'option « Mode daltonien », 9 vues repeintes
  à chaud et l'outil `Tools/Core/Vérifier les palettes (daltonisme)`. Le prérequis « aucun système
  d'options n'existe » est tombé avec lui. — `3bf5f67`, 2026-10-02. Détail dans `architecture.md`
  § Palettes.
- **Les 45 descriptions `UPG_*_DESC` manquantes.** Aucune clé dérivée d'un id ne manque plus
  (60 générateurs, 26 nœuds nommés, contrôlé le 2026-10-13). — `cb1bd31`.
- **Les 5 clés sans référence** d'août (`UPGRADE_NAME_BOTNET`, `UPGRADE_NAME_BREACH_CORE`,
  `CONSOLE_LOGS`, `COMPUTER_POWER`, `UI_ACQUIRED`) ont disparu de `fr.json`. D'autres orphelines
  sont apparues depuis : voir le backlog.
- **Le bouton de relance affichait « Button ».** Clé et texte posés. — `3bf5f67`.
- **Balises et couleurs de la console en dur** (`[OK]`, `[SYSTEM_WARN]`, `[INFO]`, `#00FF00`,
  magenta narratif). Le `ConsolePresenter` compose désormais les préfixes depuis la langue ET la
  palette. — `3bf5f67`.
- **`HeaderView` composait du texte affichable** (`$": {valeur} TFlops"`, `"+{rendement}/s"`…).
  « TFlops » et « /s » passent par des clés ; la vue ne fait plus que `SetText`. — `bf490c6`.
- **Deux des trois jauges disparues** : Ghost Cache et Data Wiper recâblés par un liseré `Gauge`
  de 4 px en bas du bouton. — `3bf5f67`, 2026-10-02. *(La troisième, celle de l'exfiltration, est
  toujours vide : elle reste au backlog.)*
- **« Les 30 s du Zéro-Day ne sont jamais atteintes »** — **entrée FAUSSE depuis le 2026-08-31.**
  Son calcul supposait un plafond de jauge de 100, qui vaut 5e6 depuis la refonte. Mesuré le
  2026-09-16 sur le code : l'Overdrive tient ses 30 s et laisse la partie à 81,8 % de jauge. Le
  vrai défaut est ailleurs — la CHARGE ne se remplit plus — et figure au backlog.
- **Adoucir la saisie** (piste ouverte dans `etat-des-lieux.md` du 2026-09-16) : une saisie
  verse désormais 30 % de sa contribution au cumul de campagne. — `a772e67`, 2026-10-02.
- **Verrouiller le palier 90 %** (même piste) : le palier attend le nœud `P_EXTRACT_HIGHRISK`.
  — `bf490c6`.
- **Le texte de la jauge du Header recomposé à chaque frame** (performance) : sans objet depuis
  que la vue ne compose plus rien.

---

## Août 2026 — lots corrigés et vérifiés

Section « Corrigé à ce jour » de l'audit du 2026-08-27, reprise telle quelle.

**Lot 2 d'équilibrage : plafond de Trace dynamique, jauge absolue, `SaveData` v7** (2026-08-30, vérifié en Play Mode via MCP) — la jauge valait 100 points fixes face à une économie étalée sur sept ordres de grandeur. **Aucun barème de trace ne pouvait marcher** : quel que soit l'exposant, la fenêtre de survie s'effondrait sous la seconde vers le dixième palier. Le plafond suit désormais le parc Hardware — lui le rempart, les Proxies le débit.

`ThreatManager` tient une trace ABSOLUE ; `NormalizedThreat` survit en valeur dérivée, ce qui laisse intacts l'affichage, le bilan de fin de run et tous les lecteurs. Relever le plafond **ne soigne pas** : la trace absolue ne bouge pas, seule la fraction affichée descend — l'analogie des points de vie maximum, vérifiée. Le plafond est POUSSÉ par le `SimulationTicker` avant toute sortie anticipée : une run stabilisée n'appelle jamais `AddThreat`, et le pourcentage affiché serait resté périmé après un achat de Hardware. Le `GameStateGateway` le pousse aussi AVANT de restaurer, sinon la trace serait écrêtée sur le plafond de base.

**Data Wiper proportionnel** : il retire 25 % de la trace ACCUMULÉE au lieu de 20 points absolus, qui ne seraient plus qu'un grain de sable devant un plafond de trois millions au dernier palier.

**`SaveData` v7** : `NormalizedThreat` devient `CurrentTrace`, en points absolus. L'ancien champ est **conservé dans la classe** — JsonUtility ne peut lire un champ que s'il existe encore, le retirer rendrait toutes les sauvegardes antérieures illisibles. La conversion multiplie la fraction par le plafond HISTORIQUE de 100, jamais par le plafond courant : sinon un joueur équipé se verrait rendre une trace bien plus lourde que celle qu'il avait.

**Trois colonnes dérivées**, α = 0,65, marge d'absorption 130 %. Une première dérivation ne couvrait que les Scripts : le tableau de rythme a montré la pression tomber de 14,5 à 1,5 min, parce que **la colonne de trace du Hardware n'avait jamais été refaite** et pèse jusqu'à 51 % du total au palier 10. Elle est désormais calée à 30 % de celle des Scripts — l'infrastructure fait moins de bruit que le crime — et les Proxies couvrent les deux. Résultat mesuré dans le catalogue : **11 min, 7 min, puis 5,0 min constantes** sur les dix premiers paliers, et « stable » après chaque Proxy. La cible du GD, au dixième de minute.

Vérifié aussi : migration v6→v7 (0,42 → 42) et chaîne complète depuis la v2 ; plafond 100 sans Hardware, 150 avec `HW_01` au niveau 5 ; trace forfaitaire du Hardware inchangée par le niveau alors que sa capacité le suit ; aller-retour de sauvegarde exact ; ouverture de partie à **23 % de jauge** pour le premier Proxy. **Décision prise seul, à valider** : `HW_01` recevait 0 de plafond (la dérivation l'absorbait dans le plafond de base), ce qui rendait le rempart invisible au palier où le joueur l'apprend — interpolé à 2,87 par niveau.

**Lot 1 d'équilibrage : la trace devient un coût de surface** (2026-08-30, vérifié en Play Mode via MCP) — la Trace était `base × niveau` pour les trois types. Rendement et trace croissant tous deux linéairement, monter un générateur accélérait les gains ET la mort dans la même proportion : **une run rapportait 133 Datas quoi que fasse le joueur**, le premier palier utile de `SCR_01` en coûtait 138, et `floor(√(133/1000))` donnait **zéro CPU Cycle**. Aucun cliquet, aucune marge de progression : le Game Over était un mur mathématique, pas un réglage trop dur.

**Règle par type**, arrêtée avec le GD. Scripts et Hardware : trace **forfaitaire**, payée à l'acquisition et figée ensuite — approfondir un outil devient silencieux, élargir l'arsenal est ce qui attire l'attention. Proxies : **× niveau, inchangé**, et c'était la condition à ne pas rater — `traceMagnitude` est le MÊME champ pour la génération et pour la dissipation, donc le figer aurait rendu un Proxy de niveau 50 aussi efficace qu'un de niveau 1, ses quatre paliers `TraceMultiplier` devenant son seul moteur. Une garde `level > 0` remplace la multiplication : sans elle, un générateur jamais acheté exposerait le joueur.

Deux ancrages de données fournis par le GD : `SCR_01` 0,5 → **0,15**, `PRX_01` 1,5 → **0,4** — le premier Proxy devient un soulagement et non une immunité. Catalogue régénéré : 45 améliorations, 180 paliers, 0 orphelin.

Vérifié : la trace de `SCR_01` vaut 0,150 aux niveaux 1, 2, 5, 10 et 20 ; celle de `HW_01` 0,080 partout ; la dissipation de `PRX_01` suit bien le niveau (0,4 → 0,8 → 2,0 → 8,0 avec son palier ×2). **L'invariant est rompu** : le budget d'une run passe de 988 Datas au niveau 1 à 106 667 au niveau 20. Et l'ouverture de partie atteint les 100 Datas du premier Proxy à **23 % de jauge**, contre 75 % avant.

⚠️ **Reste à faire, et c'est bloquant pour le palier 2** : seules deux valeurs sur trente ont été ancrées. `SCR_02` vaut toujours 2,00, soit **treize fois** `SCR_01`. Posséder les deux donne un débit net de 1,75 contre le premier Proxy, donc une jauge pleine en **57 secondes**. Avec la valeur de 0,8 évoquée par le GD, on remonte à 182 s. Les colonnes Scripts et Proxies doivent être dérivées ensemble avant que ce lot soit jouable au-delà du premier palier.

**Le nœud débloquable respire aussi pendant une run** (2026-08-30, vérifié en Play Mode via MCP) — deuxième étape du chantier accessibilité, décidée par Bertrand. La pulsation était coupée quand la fenêtre de compilation était fermée, au motif qu'un appel à l'action n'a pas de sens si l'achat est impossible. Le raisonnement ratait l'essentiel : c'est précisément pendant une run qu'on ouvre l'arbre pour PLANIFIER, et sur 134 nœuds on repère les actionnables du coin de l'œil, pas en lisant les libellés. Le canal préattentif s'éteignait au moment où le balayage sert le plus.

Deux régimes désormais, tous deux réglables à l'inspecteur : **appel à l'action** quand l'achat est possible (vitesse 3, creux à 0,35 — franc), **veille** pendant une run (vitesse 1,2, creux à 0,7 — respiration discrète plutôt que clignotement). Le paramètre de `Render` cesse de s'appeler `pulse` pour `canPurchaseNow` : il ne décide plus SI le nœud respire, seulement avec quelle insistance.

Le composant continue de se **désactiver** sur les nœuds qui n'ont pas à pulser : trois `Update()` sur 134, pas 134. Vérifié : pendant une run, 3 nœuds respirent en régime veille là où il y en avait **zéro** ; à la fin de la run les mêmes trois passent en régime urgent. Contrôle déterministe plutôt qu'à l'œil : l'alpha observé du cadre a été recalculé depuis `Time.unscaledTime` pour les deux régimes — correspondance **exacte à quatre décimales** avec le régime attendu, écart de 0,5 avec l'autre.

**Les liens de l'arbre ne dépendent plus de la couleur** (2026-08-30, vérifié en Play Mode via MCP) — première étape du chantier accessibilité, et la seule qui corrigeait un défaut réel. `UILineConnection` encodait ses trois états par la **teinte seule** : gris, cyan, vert. Une ligne n'a pas de texte, c'était donc le seul canal du jeu sans redondance — invisible pour un joueur deutéranope, et pour quiconque sur un mauvais écran. Chaque état porte désormais son MOTIF et son ÉPAISSEUR : verrouillé en pointillé fin, en cours en trait plein moyen, complété en trait plein épais. Les couleurs restent, elles ne sont simplement plus seules.

Sprite de tiret **généré par un utilitaire d'éditeur** (`Tools/Core/Générer le sprite de ligne pointillée`) plutôt que déposé à la main : le rythme du pointillé est un réglage visuel qu'on voudra ajuster, et deux constantes se relisent mieux qu'une image binaire. Toutes ses lignes de pixels sont identiques, si bien que la répétition verticale est invisible et que l'épaisseur peut varier sans jamais couper un tiret de travers. Le vide est du blanc TRANSPARENT et non du noir : un pixel transparent noir baverait en gris sur les bords au moindre filtrage.

**Défaut trouvé à la capture, pas au raisonnement** : avec 2 / 4 / 7 px, un lien verrouillé tombait à **0,7 px à l'écran** au zoom minimum de l'arbre — sous-pixel, donc la ligne s'effaçait et le pointillé avec elle. Les épaisseurs sont dimensionnées depuis le zoom RÉEL (`GraphZoomController` descend à 0,3) : 5 / 9 / 16 px, soit 1,5 / 2,7 / 4,8 px à l'écran dans le pire cas. Corrigé au passage : le sprite était en *Sliced* avec des bordures de 64 px pour un rectangle de 4 px de haut — faux depuis toujours, sans conséquence tant que l'image était uniformément blanche, mais cassant dès qu'on y met un motif.

`DrawLine` perd son paramètre d'épaisseur, qui appartient désormais à l'état, et la vue mémorise ses extrémités pour refaire sa géométrie quand l'état change. Vérifié : 133 liens, **3 combinaisons distinctes** motif + épaisseur, une par état, observées simultanément à l'écran au zoom minimum. Zéro erreur console.

**Le SaveScheduler n'abandonne plus d'écriture** (2026-08-30, vérifié en Play Mode via MCP) — une demande arrivant pendant une écriture en vol était **jetée**, au motif que « la prochaine capturera un état plus récent ». Ce qui ne tient que s'il y en a une prochaine : trois achats de prestige rapprochés suivis d'un Game Over avaient fait perdre l'écriture de fin de run. La demande arme désormais un tour de **rattrapage** — front descendant — si bien qu'une rafale de N demandes coûte au plus une écriture de plus et que le dernier état atteint toujours le disque.

**Un seul emplacement d'attente, et c'est délibéré.** Une file serait FAUSSE ici : `GameStateGateway.Capture()` retourne un tampon partagé qui continue de muter, donc empiler des demandes empilerait N références au même objet. Le code ne s'en sort déjà que parce que `SaveAsync` sérialise avant son premier `await`. Un `SemaphoreSlim` aurait le même défaut, en écrivant plusieurs fois le même état final. Écarté aussi : router les déclencheurs critiques vers `SaveBlocking`, qui commite sur le même fichier qu'une écriture asynchrone potentiellement en vol — la voie async, sérialisée plus tôt donc plus ANCIENNE, pouvait commiter en dernier et écraser le plus récent.

Le `catch` est placé DANS la boucle : un échec d'écriture ne doit pas emporter la demande qui attend son tour. Et le rattrapage porte son propre message de console — sans lui, deux écritures consécutives produisent deux lignes identiques que Unity replie en une seule, rendant le mécanisme invisible à qui le débogue. C'est d'ailleurs ce qui a faussé la première mesure.

Vérifié : trois achats dans la **même frame** produisent exactement deux lignes, dont une « rattrapage », et le fichier contient les trois nœuds. La preuve tient parce que `Capture()` est appelé dans la notification du premier achat : l'écriture nº1 ne pouvait contenir que le premier nœud. Rien à signaler côté atomicité, déjà traitée — `LocalJsonSaveService` écrit dans un temporaire puis bascule par `File.Replace`, avec `.bak` de secours.

**Nœud d'automatisation des Scripts** (2026-08-30, vérifié en Play Mode via MCP) — Bertrand avait posé l'amorce : la valeur d'enum, les clés `PrestigeAutomationName/Description`, les trois branches de `PrestigeLabels`, le cas dans `RecalculateBonuses` et la reconnaissance du type par le générateur. **Trois maillons manquaient.** (1) `SpecificUpgradeBonuses` n'avait pas de champ pour l'automatisation : le `case Automation` d'`AccumulateSpecific` cumulait dans le vide, sans branche. (2) La garde d'égalité de `SetSpecificBonuses` ne couvrait pas le nouveau champ — un achat ne touchant QUE l'automatisation serait sorti par le retour anticipé. (3) **Les 15 nœuds n'existaient pas dans les données**, ce qui explique qu'aucun ne s'affichait.

**Défaut trouvé à la compilation, et il était grave** : la nouvelle valeur avait été insérée **au milieu** de `PrestigeBonusType`, juste après `SpecificUpgradeTimeReduction`. L'index est sérialisé dans les `.asset` : toute la famille Zéro-Day s'est décalée d'un cran, et la console a signalé que `P_EXPLOIT_CHARGES` se croyait un nœud d'automatisation — donc que les charges de Ghost Cache, le rendement de l'Exploit et son malus de Trace désignaient tous autre chose. C'est exactement ce contre quoi le commentaire de l'enum met en garde. Valeur déplacée en fin d'enum : les `.asset` existants retrouvent leur sens sans migration, et les sauvegardes n'étaient pas concernées (elles indexent par id, pas par index).

**Convention assumée** : `bonus` vaut ici un NOMBRE DE NIVEAUX, pas une fraction — seule exception à la règle du « tout en fraction », documentée dans la struct. Un seuil d'automatisation est un numéro de niveau. Arrondi et non troncature à l'application, même piège que les charges du Ghost Cache. **Choix de données** : un nœud par Script (15, eux seuls relancent des cycles), en Y=4 sous le nœud TIME et **en prolongement de celui-ci** plutôt qu'en quatrième frère du nœud COST — automatiser est le terme de la branche d'un Script, le seul bonus qui change sa façon de se jouer plutôt que ses chiffres. Valeurs alignées sur les frères (maxLevel 5, costMult 1.4, baseCost 50 × n), donc du remplissage comme les 105 autres. Arbre regénéré : **134 nœuds, 0 orphelin, 0 avertissement**.

Vérifié : le seuil de `SCR_02` descend d'exactement un niveau par rang, 10 → 5 ; à 5 rangs, un `SCR_02` de niveau 7 est **automatisé** là où il en aurait fallu 10 ; `SCR_03`, sans nœud, reste à 10 — le ciblage tient ; l'achat direct du nœud sans ses parents est refusé ; le 6ᵉ rang est refusé ; `P_EXPLOIT_CHARGES` a retrouvé son vrai type ; les 15 pastilles s'affichent avec nom, lore, effet et prérequis résolus. **Clé manquante ajoutée** : `PRESTIGE_AUTOMATION_DESC`, que `ResolveDescription` réclamait déjà.

**Prix arrondis à la hausse, polices unifiées** (2026-08-30, vérifié en Play Mode via MCP) — les deux points que Bertrand avait explicitement autorisés dans ce backlog. **`FormatCost`** : `Format` tronque vers le bas, ce qui est correct pour un solde et faux pour un prix. `SCR_01` au niveau 1 coûte 10,7 et s'affichait « Coût: 10 » ; un joueur avec exactement 10 Datas lisait le prix, voyait le bouton rester gris, et concluait que le jeu était cassé. Le nouveau formateur plafonne à la précision **réellement affichée**, pas à l'unité : « 1,234K » masque déjà 999 unités, et arrondir vers le bas cette décimale-là reproduirait le défaut un cran plus haut. Un arrondi à six décimales précède le plafonnement : les coûts sortent d'un `Math.Pow`, où un prix valant exactement 100 se stocke en 100,000000000000014, et plafonner tel quel afficherait 101 — un mensonge dans l'autre sens. Appliqué aux cinq endroits qui affichent un prix ou un seuil à atteindre ; les soldes et les gains gardent `Format`. Vérifié en jeu sur le cas d'origine : la vue affiche bien « Coût: 11 » pour un coût réel de 10,7. Contrepartie assumée : le garde-bruit avale une différence réelle de 0,5 à l'échelle du milliard, où elle n'a plus de sens.

**Polices** : 8 textes en LiberationSans converties, plus **3 matériaux mésapparis** que personne n'avait vus — police ShareTechMono, matériau LiberationSans, sur le narratif, le bilan et le bouton de relance. 32/32 textes de scène et 9/9 des prefabs sont désormais conformes sur les deux plans. **Effet de bord signalé** : ShareTechMono étant plus large, le débordement de l'inspecteur devient franchement visible — il préexistait (hauteurs figées à 50 px pour un contenu de 500) mais LiberationSans le masquait à moitié. Tentative de correction par `childControlHeight`, **annulée** : elle met l'auto-sizing et le `ContentSizeFitter` en boucle. Les réglages de layout sont rendus à l'état exact où Bertrand les avait laissés ; l'entrée détaillée est en Hygiène.

**Lot « Arbre de prestige : fenêtre d'achat, cascade et cinq états »** (2026-08-29, vérifié en Play Mode via MCP) — trois défauts signalés par Bertrand, plus un quatrième trouvé à l'inspection. **(1) L'achat était possible pendant une run.** `TryPurchasePrestige` n'avait aucune garde de session : les CPU Cycles se gagnent en TERMINANT une run, pouvoir les dépenser au milieu d'une autre transformait l'arbre en boutique d'appoint qu'on rouvre dès qu'on est en difficulté. Nouvelle `ArePurchasesAllowed` dans le `PrestigeManager`, pilotée par le `GameSessionManager` — c'est la session qui déclare son état, l'inverse fermerait le cycle de dépendances puisqu'elle dépend déjà du prestige. **(2) Les nœuds débloqués ne se rafraîchissaient pas.** `PrestigeItemPresenter` n'écoutait QUE `CpuCycles.Amount` : un achat n'ouvrait visuellement ses enfants que par effet de bord, parce qu'il coûtait de l'argent. L'écran écoute désormais `OnBonusesRecalculated`, émis après chaque recalcul — achat comme chargement de sauvegarde. **(3) Les cinq codes d'état de la maquette** sont implémentés : maxé en couleurs inversées, en cours en vert, débloquable en ambre pulsant, trop cher en rouge, verrouillé en gris. **(4) Défaut trouvé à l'inspection : `_interactionButton` était NULL sur `NodePrefab`** — le renommage `_buyButton` → `_interactionButton` avait perdu la référence, une NRE par nœud dans `Awake` et plus aucun clic. Le prefab est recablé. Idem pour `_maxedColor`, qui avait SURVÉCU au changement de script avec sa valeur (0,0,0,0) : un nœud maxé se peignait en noir opaque, texte noir compris.

Trois décisions d'architecture au passage. **Le clic sélectionne, il n'achète plus** : le `PrestigeNodeDetails` que Bertrand avait construit est branché (`PrestigeDetailsView` + presenter), et c'est son bouton qui dépense — sur un arbre de 119 nœuds, un clic qui engage immédiatement une monnaie gagnée en une run entière est un piège. Le bouton porte aussi le REFUS (« fonds insuffisants », « branche verrouillée », « hors run uniquement ») : un bouton grisé sans explication laisse chercher. **`GameOverPanel` devient l'écran méta unique**, conséquence de la fusion des deux panneaux dans la scène : l'arbre étant devenu son enfant, il ne pouvait plus s'afficher seul, son parent étant éteint. `PrestigePanelView` possède désormais la racine et deux groupes — « fin de run seulement » et « consultation seulement » — et `GameOverView` n'écrit plus que ses textes ; deux composants qui se disputent les mêmes `SetActive` finissent toujours par se marcher dessus. Échap est inerte sur l'écran de fin, il n'y a rien derrière à quoi revenir. **Tous les abonnements de l'écran vivent dans le panneau** : faire écouter les trois mêmes sources à chacun des 119 nœuds coûtait 357 abonnements pour trois signaux. Le panneau écoute une fois et rediffuse par boucle indexée ; les nœuds et l'inspecteur n'ont plus aucun `IDisposable` réactif. Corollaire assumé : un achat déclenche les trois signaux dans la même frame, donc trois passes de repeinte — les coalescer par numéro de frame serait un piège, le débit de la monnaie précédant l'incrément du niveau, la première passe lit encore l'ancien niveau. La pulsation ambre se coupe pendant une run (c'est un appel à l'action, pas un code couleur) et chaque `PrestigeItemView` se **désactive** quand il n'a pas à pulser — sur 119 nœuds, laisser 119 `Update()` tourner pour que trois clignotent serait payer le pire des deux mondes.

29 clés de localisation ajoutées, dont 14 gabarits d'effet, un par `PrestigeBonusType` — littéraux dans un `switch` et non clé construite depuis le nom de l'enum, pour rester visibles du `LocalizationAnalyzerWindow`. **Défaut trouvé au test** : `↻` (U+21BB) n'existe pas dans l'atlas de la police, remplacé par un carré — le libellé des cycles est repassé en ASCII. Vérifié : achat refusé pendant une run **sans rien dépenser** (25 cycles avant et après) ; les cinq états observés simultanément dans l'arbre ; achat de `P_STEALTH` à la fin d'une run → 25 → 5 cycles, le nœud passe en vert « Niv. 1 / 5 » et **`P_EMERG` bascule dans la même frame** de « VERROUILLÉ » gris à « 100 CPU » rouge ; l'inspecteur se repeint en place (coût 20 → 32, bouton « COMPILER » → « FONDS INSUFFISANTS ») ; le voile de sélection suit le nœud consulté ; Échap inerte en mode fin de run ; zéro erreur console.

**Durée de run au bilan** (2026-08-29, vérifié en Play Mode via MCP) — `GameSessionManager` devient `ITickable` et cumule les secondes de jeu. **Cumulées et non déduites d'un horodatage** : `Time.time` repart de zéro à chaque lancement, une run reprise le lendemain aurait affiché la durée de la seule session en cours. Le chronomètre s'arrête pendant l'écran de fin — contempler son bilan n'est pas du jeu — et repart à zéro au wipe. `SaveData` v6 (`RunElapsedSeconds`) : le temps passé fenêtre close n'est jamais crédité, le compteur n'avance que dans `Tick()`. Deux gabarits d'affichage, les unités dans les clés : « 14 min 07 s » au-delà de la minute, « 42 s » en deçà — un format unique aurait rendu « 0 min 42 s » sur la statistique la plus regardée de l'écran. Vérifié : les deux formats, la remise à zéro au wipe, l'aller-retour JSON, les migrations v2/v4/v5 → v6 qui préservent `EmergencyUsesInRun`, et la restauration qui rend bien 423,5 s au chronomètre.

**Bilan de fin de run** (2026-08-29, vérifié en Play Mode via MCP) — `OnSessionEnded` ne transportait qu'un `double`, le gain : l'écran de fin ne pouvait ni distinguer une Saisie Fédérale d'un Effacement Propre, ni rien raconter d'autre. Nouveau `RunSummary` — struct readonly — portant la raison, les cycles gagnés, ceux d'avant bonus, les Datas de la run, la Trace à la sortie et les usages du Data Wiper. **Capturé avant le wipe**, obligatoirement : `ResolveRunEnd` efface la run trois lignes plus bas, lu après il ne contiendrait que des zéros. Les deux fins ont leur narration et leur COULEUR de titre — rouge pour une saisie, vert pour un effacement propre — pour que le joueur sache en un coup d'œil s'il a bien joué. La ligne « Trace à la sortie » n'apparaît que sur une sortie volontaire : après une saisie elle vaut 100 % par définition. Le bonus Clean Exit n'est annoncé que s'il a réellement rapporté quelque chose. Un bouton « DÉPENSER » ouvre l'arbre depuis l'écran de fin, qui recouvre le bouton du header. Vérifié : 45 000 Datas → 6 cycles de base, 7 avec le bonus (+1 affiché), Trace 87 %, Data Wiper ×2 ; puis saisie à 12 000 Datas → 3 cycles, titre rouge, ligne Trace absente, détection enregistrée ; le bouton ouvre bien l'arbre.

**L'arbre de prestige est enfin accessible** (2026-08-29, vérifié en Play Mode via MCP) — les 119 nœuds et leurs 118 liens étaient construits au démarrage depuis toujours, mais `Canvas/PrestigeTree` était **inactif** et aucune ligne de code ne l'activait : `PrestigePanelView` n'avait ni `Show` ni `Hide`. Le panneau s'ouvre désormais par un bouton du header, se ferme par un bouton dans le panneau ou par **Échap**. **Consultable pendant une run** — décision de Bertrand : le joueur doit pouvoir planifier ses achats et revoir ce qu'il possède après une absence. **Le jeu ne se met PAS en pause** : la Trace continue de monter pendant la consultation, sans quoi le panneau serait exactement la planque gratuite que `runInBackground = true` existe pour interdire. L'arbre se construit une fois au démarrage, l'ouverture n'est qu'un `SetActive`. **Défaut trouvé au test** : le raccourci utilisait `Input.GetKeyDown`, alors que le projet est passé au nouvel Input System — une `InvalidOperationException` par frame ; basculé sur `Keyboard.current`. Deux fausses alertes écartées au passage : le libellé de fermeture se résout bien, ma lecture tombait dans la frame de l'activation ; et les « New Text » des nœuds sont dans le panneau masqué par le brouillard de guerre, invisibles au joueur. Vérifié : 595 textes dans l'arbre, **une seule** clé non résolue au moment de l'ouverture et résolue la frame suivante ; `timeScale` reste à 1 ; zéro erreur sur 502 frames.

**`GlowMaterialAssigner` retiré** (2026-08-28) — sa prémisse était fausse : le halo est une décision d'EMPHASE, pas une conséquence de la couleur. Deux textes de la même teinte méritent légitimement des traitements différents, ce que la passe manuelle de Bertrand a établi — les devises du header, par exemple, n'en ont pas besoin. L'outil aurait écrasé ce travail à la prochaine exécution : le garder était un piège. Sa passe initiale a servi à poser les matériaux en masse, c'était son seul usage légitime. Récupérable dans l'historique au commit `103cfd4` si un besoin de passe groupée revenait.

**Les halos ne se voyaient pas — deux causes distinctes** (2026-08-28, signalé par Bertrand) — les matériaux étaient bien assignés, mais réglés pour ne rien produire. **Texte** : `_GlowOuter` valait **0,05**, le halo ne débordait pratiquement pas du glyphe ; porté à 0,3. (Le mot-clé `GLOW_ON` était déjà actif — ma première lecture du YAML disait le contraire, à tort.) **Images** : le Bloom de la GameScene a un **seuil à 1**, or les trois `Mat_UIGlow_*` sortaient des couleurs ≤ 1, donc aucune ne franchissait le seuil ; leurs `_Color` sont passées en HDR à 2, ce qui fait enfin briller la jauge de Trace et les barres d'onglet. **Bouton Acheter** : le survol manquait purement et simplement, il n'avait pas été traité — ajouté sur le `BuyButton` de l'ActionPrefab, donc sur tous les clones. **Halo de contour retiré** des cinq survols : le shadergraph ne lisant pas la texture, il transformait le cadre en aplat plein. Le survol se limite désormais au fond à 10 %, qui est correct.

**Lot « Halos et états visuels »** (2026-08-28, vérifié en Play Mode via MCP) — première passe du rework UI, calquée sur la maquette `core_ui.html`. **`GlowMaterialAssigner`**, outil d'éditeur à deux entrées (rapport à blanc, puis application) : il apparie chaque texte au matériau de halo de SA couleur, règle dérivée de la teinte et non d'une liste de chemins. Possible parce que les trois matériaux ont un `_FaceColor` **blanc** — seul leur `_GlowColor` diffère, donc la couleur vient toujours du composant et un matériau mal apparié donnerait un texte rouge à halo vert. Il couvre les prefabs et les objets inactifs, saute les polices étrangères et les couleurs éteintes (les bordures à `#1A4D1A` ne brillent pas dans la maquette). Résultat : **14 textes de scène + 2 du prefab**, zéro mésappariement. **`TabButtonView`** : l'`ActiveBackground` et la `HighlightBar` existaient dans la scène mais **rien ne les allumait** — `ShowTab` ne basculait que les conteneurs. L'état actif suit maintenant la maquette : fond sombre, barre lumineuse, libellé vif à halo ; les onglets au repos passent en `#1A4D1A` sans halo. **`UiHoverGlow`** : ni Color Tint (qui ne sait que multiplier la couleur du targetGraphic, donc ne peut pas changer un matériau) ni Animation (un Animator et quatre clips par bouton, et une animation réécrit ses propriétés chaque frame, donc se battrait avec les presenters). Un composant qui ne touche QUE le matériau du contour et un fond à 10 % d'opacité — deux choses dont aucun presenter ne s'occupe. Le ColorTint est conservé pour le seul état grisé, son survol neutralisé. Un `Update` gardé par le survol éteint un bouton qui se grise SOUS le curseur, cas réel du Data Wiper qui part en recharge au clic. Vérifié : clic sur Hardware → l'onglet Scripts s'éteint et Hardware s'allume ; survol → halo et fond apparaissent sur Overclock (vert) et Terre Brûlée (rouge), **rien** sur Data Wiper et Ghost Cache qui sont grisés ; sortie → retour au repos ; grisage sous le curseur → extinction immédiate.

**Le panneau affichait les valeurs du niveau PRÉCÉDENT** (2026-08-27, constaté en jeu par Bertrand, corrigé et vérifié en Play Mode) — après le tout premier achat, `SCR_01` annonçait « Génère **0** Datas toutes les 1,5 s » alors que le modèle portait bien un rendement de 1. `UpgradeModel.LevelUp()` faisait `_currentLevel.Value++` PUIS `RecalculateCache()` : la notification R3 réveillait la vue immédiatement, et `BindLevel` lisait les caches avant leur mise à jour. Le rendement, le coût ET la durée étaient donc affichés avec un cran de retard — invisible au-delà du premier niveau, où l'écart devient un simple décalage, mais fatal au niveau 1 où la valeur précédente vaut zéro. Le niveau devient un champ `int` ordinaire, source de vérité de tous les calculs ; le `ReactiveProperty` ne sert plus qu'à notifier, et il est écrit EN DERNIER, une fois les caches cohérents — même principe que `RecalculateBonuses`. Vérifié : « Niv. 0 / Génère 0 Datas » avant achat, « Niv. 1 / Génère 1 Datas » après, et un cycle lancé à la main crédite bien 1 Data à chaque livraison.

**La profondeur de l'arbre de prestige est appliquée par le modèle** (2026-08-27, vérifié en Play Mode via MCP) — `TryPurchasePrestige` ne contrôlait que le niveau maximum et le coût : un appel direct achetait n'importe quel nœud sans ses parents, ce qui rendait toute la méta-progression facultative pour qui contournait le panneau. La règle vivait uniquement dans `PrestigeItemPresenter`, l'endroit le plus volatil du projet. Nouvelle méthode `PrestigeManager.IsUnlocked(config)`, appelée par l'achat **et** par la vue : celle-ci reflète désormais la règle au lieu de la redéfinir. Vérifié : le saut direct sur `P_OVERCLOCK_AWAKE` est refusé et **ne dépense rien** ; la chaêne complète s'achète normalement dans l'ordre, chaque maillon débloquant le suivant à l'achat ; `P_ROOT`, sans prérequis, reste accessible ; un nœud d'une autre branche (`P_EXPLOIT_MULT`) reste fermé ; le plafond de niveau tient toujours ; une config nulle rend `false` sans exception.

**Chaîne « Surtension » en amont de l'Injecteur** (2026-08-27, vérifié en Play Mode via MCP) — Bertrand a écrit `P_OVERCLOCK_POWER_1/2/3` et rétabli le prérequis de `P_OVERCLOCK_AWAKE`. Deux corrections de données : leur `bonusType` était `OverclockPowerMultiplier`, absent de l'enum — le générateur **rejette** un type inconnu, les trois nœuds n'auraient pas été créés et l'Injecteur se serait retrouvé sans parent, donc accessible d'emblée ; ils sont mappés sur `ClickPowerMultiplier`, qui EST déjà ce levier (bonus 0,5 / 1 / 2 par rang, une continuation de `P_CLICK`). Le `PrestigeTreeGenerator` **avertit désormais** quand un JSON porte encore un `nameKey`/`descKey`, comme le fait celui des upgrades depuis le 25/08 ; il a immédiatement signalé sept nœuds, dont les champs ont été retirés après vérification que la clé dérivée existait bien en base. Six clés de localisation ajoutées (Surtension I à III). Arbre régénéré : **119 nœuds, zéro avertissement**. Vérifié : la chaîne remonte à `P_ROOT`, et à fond le clic passe de 0,5 s à **11,75 s** (×23,5) — ⚠️ magnitude à surveiller à l'équilibrage, un clic avancerait alors une douzaine de cycles de `SCR_01` d'un coup.

**Lot « Injecteur Automatique » — le réveil de l'Overclock** (2026-08-27, vérifié en Play Mode via MCP) — `OverclockWakesScripts` rejoint `PrestigeBonusType`, appendé en fin d'enum comme les trois précédents. `ScriptCycleRunner.StartAllIdle()` démarre les Scripts possédés à l'arrêt et retourne leur nombre ; il ignore les automatisés, qui repartent seuls au Tick suivant et dont le décompte donnerait un retour trompeur. `TriggerManualOverclock` retourne désormais un `OverclockResult` — struct readonly, un clic ne doit rien allouer — portant les secondes ET les réveils, et le presenter choisit entre deux messages de console selon qu'il y a eu réveil ou non. **L'ordre est avance-puis-réveille** : l'inverse offrirait une demi-seconde d'avance aux Scripts qui viennent de démarrer, alors que le GDD parle de « ceux qui tournaient déjà ». **Donnée corrigée** : le nœud écrit par le GD déclarait le prérequis `P_OVERCLOCK_POWER_3`, qui n'existe dans aucun fichier — le générateur aurait laissé le nœud sans parent, donc accessible d'emblée, ce qui annulait le « positionnement tardif » ; rattaché à `P_POWER_START`, le nœud existant le plus profond de la branche Automatisation. Arbre régénéré : 116 nœuds, aucun prérequis cassé. Vérifié : sans le nœud, un clic donne +0,5 s et **0 réveil**, les trois Scripts restent à l'arrêt ; avec, le même clic en réveille **3** ; un second clic en réveille 0, tout tournant déjà.

**Les quatre boutons sont câblés et vérifiés** (2026-08-27, Play Mode via MCP) — le Data Wiper est le `PanicButton` de `Canvas/ConsoleLogs`, aux côtés du Ghost Cache, de l'Overclock et du Protocole Terre Brûlée (`PrestigeButton`). Les trois vues autonomes ont leur `Button`, leur libellé et leur `Image` en Filled, et les neuf champs du `GameSceneLifetimeScope` sont renseignés. États initiaux corrects sur une partie neuve, zéro warning au démarrage : Ghost Cache et Data Wiper affichent leur état verrouillé en nommant le nœud de prestige manquant, l'exfiltration sa jauge à 0 %.

**Lot « BalancingConfigSO »** (2026-08-27, vérifié en Play Mode via MCP) — les vingt réglages d'équilibrage étaient des `const` dans huit fichiers : le GD ne pouvait rien régler sans recompiler, et une session d'équilibrage coûtait une recompilation par essai. Tout vit désormais dans `Assets/GameData/Balancing/BalancingConfig.asset`, injecté par `RegisterInstance` depuis le `RootLifetimeScope`. **Modifiable en Play Mode** : les systèmes lisent les propriétés à l'usage plutôt que de les recopier au démarrage. `UpgradeModel` reçoit la config par constructeur — c'est un POCO créé à la main, il ne passe pas par le conteneur. Les statiques que lisaient les presenters (`GhostCacheSystem.CapacitySeconds`, `EmergencyProtocolSystem.TraceReduction`…) deviennent des propriétés d'instance, ce qui garde le SO inconnu de l'UI. **Défaut trouvé au test et corrigé** : `RequiredTFlops` était une chaîne R3 dérivée du compteur d'usages, donc régler le palier dans l'inspecteur restait sans effet jusqu'au prochain déclenchement — exactement ce que le lot devait rendre possible ; c'est maintenant une propriété calculée, et la vue écoute le compteur d'usages. **Second défaut** : créer l'asset et l'assigner à la scène dans le même appel d'éditeur écrivait `fileID: 0` — la référence n'existait pas encore à la sérialisation ; il faut recharger l'asset depuis son chemin avant de l'assigner. Vérifié à chaud en Play Mode : compression 0,05 → 0,5 fait tomber le cycle de `SCR_01` de 1 s à 0,214 s ; le multiplicateur de Trace de l'Exploit passe de ×10 à ×3 ; le palier du Data Wiper de 50 à 5 TFlops rend `HasEnoughPower` vrai immédiatement ; le seuil d'exfiltration suit `MoneyPerCpuCycle`. Il ne reste dans le code que le drapeau de triche `BypassUnlockCondition`, qui est une configuration de build et non un réglage, et deux garde-fous anti-division (`MinSafeDuration`, plancher à 1,01 du multiplicateur de coût).

**Lot « Data Wiper » — refonte du Bouton d'Urgence** (2026-08-27, vérifié en Play Mode via MCP) — le bouton ne coûte plus d'argent : il **exige** un palier de TFlops, qui grimpe ×3 par usage. Rien n'est dépensé — les TFlops sont une capacité dérivée du parc Hardware, il n'y a rien à en soustraire, et c'est ce point qui avait bloqué la première formulation du GD. Le coût réel est un **contrecoup** : 30 % des TFlops immobilisées 60 s, +10 points par usage, plafonné à 90 %. Comme les TFlops alimentent la dissipation des Proxies, purger la Trace affaiblit la défense juste après. Délai de 5 min entre activations. Effet : −20 points ABSOLUS de jauge. `EmergencyProtocolSystem` devient `ITickable` et perd sa dépendance à `UserCurrencies`. `SaveData` v5 : le contrecoup et le délai sont sauvegardés, contrairement à l'Overdrive — là-bas sauvegarder aurait mis un bonus en pause, ici ne pas sauvegarder ferait échapper à une pénalité. ⚠️ **Palier de base (50 TFlops) et escalade (×3) sont PROVISOIRES** : le GD a chiffré la progression, pas le palier lui-même ; le ×3 reprend l'ancien coût en argent. Le plafond de 90 % est également une décision de code, absente de la spécification. Vérifié : verrouillé sans `P_EMERG` ; refus à 48/50 TFlops ; déclenchement à 52 → Trace 0,63 → 0,43, TFlops 52 → 36,4, dissipation 163,5 → 154,4, cycle `SCR_01` 0,347 → 0,443 s ; second appel refusé ; palier 50 → 150, tranche suivante 40 % ; à l'échéance les TFlops reviennent à 52 ; `Restore(2 usages, 30 s)` réapplique bien 40 % et non 50 % ; migration v2 → v5 préserve `EmergencyUsesInRun` ; wipe remet tout à zéro sauf le déblocage de prestige.

**Lot « Nœuds de prestige de l'Exploit »** (2026-08-27, vérifié en Play Mode via MCP) — les trois nœuds écrits par le GD dans `01_Economy.json` sont branchés : `P_EXPLOIT_CHARGES` (5 rangs, +1 charge), `P_EXPLOIT_MULT` (10 rangs, +10 % de rendement) et `P_EXPLOIT_TRACE_REDUC` (5 rangs, −0,5 de malus). Trois valeurs **appendues en fin** de `PrestigeBonusType` : l'index est sérialisé dans les `.asset`, une insertion au milieu aurait redéfini tous les nœuds existants. Le Ghost Cache passe en **multi-charges** — capacité = rangs × 300 s, une seule charge consommée par déclenchement — et l'Exploit devient **verrouillé tant que `P_EXPLOIT_CHARGES` vaut 0**, avec un quatrième état de bouton qui nomme le nœud manquant plutôt que de rester gris. Rien ne s'accumule pendant le verrouillage. Surcharge `GetText` à 4 arguments ajoutée. **Deux défauts trouvés au test et corrigés** : le presenter ne s'abonnait qu'à `ExploitMaxCharges`, si bien qu'acheter les dix rangs de rendement laissait le bouton annoncer ×50 — remplaçé par un abonnement à `OnBonusesRecalculated`, qui couvre les trois nœuds ; et la jauge était forcée à 1 en état armé, masquant l'avancement de la charge suivante — c'est la couleur qui dit « armé », la barre montre désormais la suite. **Un troisième défaut d'affichage** : `RoundToInt(7,5)` faisait annoncer « trace x8 » pour un malus réel de 7,5 ; les valeurs passent brutes et le gabarit les met en forme. Vérifié : verrouillé → `Accumulate(500)` laisse la charge à 0 ; 3 rangs → 900 s de capacité et bouton débloqué immédiatement ; 420 s → 1 charge et jauge à 0,40 ; rangs max → « rendement x100 / trace x7,5 » ; déclenchement → `SCR_01` de 24 à 2 400 et réserve de 420 à 120 s ; wipe → charge à 0 mais capacité toujours à 3.

**Lot « Ghost Cache et Zéro-Day Exploit »** (2026-08-27, vérifié en Play Mode via MCP) — troisième volet de la proposition du GD, débloqué par ses quatre chiffres : capacité **300 s d'excédent**, durée **30 s**, **×50 sur le rendement**, **dissipation → 0**. L'excédent de dissipation, jusque-là jeté par `SimulationTicker`, alimente une charge unique. **La charge se remplit en TEMPS, pas en magnitude** — une seconde d'excédent vaut une seconde, qu'il soit de 1 ou de 100 000 — sans quoi un joueur de fin de partie remplirait la jauge instantanément. Le test d'accumulation est `debit < 0` et non `<= 0` : une partie sans aucun générateur donne un débit nul, et charger l'Exploit en ne faisant rien n'aurait aucun sens. **Le ×50 ne touche que les Scripts**, et cette restriction est le point délicat du lot : chez un Hardware, `GetCurrentYield()` EST sa contribution en TFlops — le laisser passer aurait multiplié par 50 la capacité de calcul, donc la compression des cycles ET la dissipation des Proxies, transformant un buff économique en invulnérabilité. Garde posée des deux côtés : le modèle refuse le multiplicateur hors Script, et le manager ne le pousse qu'aux Scripts. Le multiplicateur passe par les caches des modèles plutôt que par le versement, pour que le chiffre affiché dans le header et le montant crédité racontent la même chose. `SaveData` v4 (`GhostCacheSeconds`) : la charge survit à la fermeture du jeu mais pas au wipe. Un Overdrive **en cours** n'est pas sauvegardé. Vérifié en Play Mode : charge à 22 s avec la jauge de Trace toujours à 0 (l'excédent est capté sans que la règle d'or bouge) ; déclenchement → `SCR_01` passe de 5 à 250, durée inchangée, **HW_01 et les TFlops restés à 6** ; Trace qui monte à 0,0024/s pendant l'Exploit, soit exactement la trace Hardware sans aucune dissipation ; second déclenchement refusé ; à l'échéance le rendement revient à 5 et la jauge **reste à 0,072** au lieu de redescendre ; migration v2 → v4 en chaîne et aller-retour JSON corrects ; wipe → charge à 0.

**Lot « Proxies utiles »** (2026-08-27, vérifié en Play Mode via MCP) — les Proxies n'étaient atteints par aucun palier : rendement de base nul, pas de cycle. Deux ajouts, sur proposition du game designer. **`TraceMultiplier`**, troisième effet de palier, amplifie la magnitude de Trace — donc la dissipation pour un Proxy. Le sens dépend du type, comme le champ qu'il amplifie : le générateur avertit désormais pour tout palier inerte ou inversé (`DurationMultiplier` hors Script, `YieldMultiplier` sur rendement nul, `TraceMultiplier` > 1 hors Proxy). **Synergie** : chaque niveau de Proxy possédé accélère tous les Scripts de 1 %, via un multiplicateur dédié appliqué à la durée — pas branché sur les TFlops, ce qui aurait créé une boucle avec le `log10` de la dissipation. `GetTraceMagnitudePerSecond()` passe en cache : elle est appelée à chaque frame par `ScriptCycleRunner`, un parcours de paliers y serait payé soixante fois par seconde. Vérifié sur `PRX_01` : magnitude 13,5 au niveau 9 puis **30 au niveau 10** (×2), **225 au 25** (×3) et **2 250 au 50** (×5) ; 50 niveaux de Proxy donnent ×1,5 de vitesse et ramènent le cycle de `SCR_01` de 1,5 s à 1 s.

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
