# Décisions de game design — tranchées, à appliquer telles quelles

Validées par Bertrand le 2026-08-25. Ne pas réinventer ces règles ; si l'une pose problème, le signaler avant d'écrire du code qui la contourne.

## Les quatre ressources

| Ressource | Rôle | Source |
|---|---|---|
| **Argent (`$`)** | Monnaie d'achat unique : Scripts, Proxies **et Hardware** | Cycles des Scripts |
| **TFlops** | Capacité de calcul — n'achète rien, **modifie** les deux autres piliers | Hardware possédé |
| **Trace (A.M.I.)** | Jauge de Game Over | Générée par les upgrades, dissipée par les Proxies |
| **CPU Cycles** | Monnaie de méta-progression, survit au wipe | Fin de run |

Principe directeur : **aucune ressource ne doit flotter**. Si un joueur peut ignorer une ressource, l'économie est cassée.

## Les TFlops sont une CAPACITÉ dérivée, pas un stock

`TFlops = Σ rendement des Hardware possédés (+ StartingComputerPower du prestige)`

**Magnitude des bonus de prestige (tranché le 2026-08-26)** : `StartingMoney` et `StartingComputerPower` se calculent avec `BonusPerLevel × niveau`, comme les cinq autres types de bonus — et **non** avec `BaseCost × niveau` comme le fait le code actuel. Indexer l'effet d'un nœud sur son prix couple deux réglages qui doivent bouger séparément pendant l'équilibrage.

La valeur ne bouge qu'à l'achat d'un Hardware. Elle ne s'accumule pas dans le temps et ne se dépense pas.

**Conséquence technique :** `UserCurrencies.ComputerPower` ne doit plus être un `Currency` alimenté par `Add()`, mais une `ReadOnlyReactiveProperty<double>` calculée depuis `UpgradeManager`. Le champ `ComputerPower` de `SaveData` devient redondant (recalculable depuis les niveaux d'upgrades) — conservé en v1, à retirer au lot 2b.

## LA TRINITÉ — un rôle par pilier, tranché le 2026-09-10

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

## Les TFlops : deux effets, tous deux offensifs

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

## Accélération des Scripts — une ladder uniforme

**Refonte du 2026-09-10.** L'ancienne table alternait au petit bonheur et produisait **deux
artefacts couplés** :

- Les Scripts **impairs** avaient un palier de durée, les **pairs** aucun.
- Chaque palier de durée *remplaçant* un palier de rendement dans les quatre emplacements, les
  Scripts pairs se retrouvaient **70 % plus puissants en rendement** (produit 9,80 contre 5,77).

L'équilibre entre paliers était donc en partie un accident de remplissage de tableau.

Ladder unique, appliquée aux quinze :

| Niveau | 10 | 25 | 50 | 100 | 150 | 250 |
|---|---|---|---|---|---|---|
| Effet | durée ×0,8 | rendt ×1,5 | durée ×0,8 | rendt ×1,5 | durée ×0,8 | rendt ×1,5 |

Cumul : **rendement ×3,375, durée ×0,512** — soit ×6,6 de débit au niveau 250, paliers seuls.

**Le premier cran est au niveau 10, et ce n'est pas un détail :** mesuré, la pyramide d'achats
descend vite (`SCR_06` autour du niveau 33, `SCR_07` autour de 13). Un palier posé au niveau 25
serait invisible pour la moitié du roster.

**Uniforme en RELATIF, à dessein.** Les durées de base s'étalent de 1,5 s à 3 600 s — un facteur
2 400. Rendre un Script à long cycle jouable dans l'absolu est le travail du **Hardware**, via la
compression. Le palier n'a qu'une mission : que chaque Script donne le sentiment d'accélérer quand
on l'approfondit. Et comme la trace se compte par cycle, un Script qui accélère génère mécaniquement
plus de Trace par seconde — « plus d'argent ET plus de menace », ce qui referme sa définition.

## Cycles de production — Scripts uniquement

Chaque Script a sa propre barre et verse son montant **à la fin** de son cycle. Le Hardware fixe la capacité TFlops dès l'achat (effet immédiat, pas de barre). Les Proxies dissipent en continu (pas de barre).

Le niveau augmente **toujours** le versement (`BaseProductionYield × niveau`). Les bonus supplémentaires viennent de **paliers explicites** listés par Script (voir `cycles.md`).

**Automatisation par seuil, propre à chaque Script.** Sous le seuil, le joueur lance chaque cycle à la main ; au-dessus, il se relance seul. Un nœud de prestige ciblé (`TargetUpgradeId`), achetable par rangs, abaissera ce seuil de quelques niveaux par rang. *(Note technique : s'assurer d'ajouter le champ `TargetUpgradeId` de type `string` dans le modèle de données `PrestigeItemData` du script de désérialisation JSON).*

**La BARRE ESPACE déclenche un Overclock (ajouté le 2026-09-16).** Même action que le clic, même
message de console, même valeur — seul le geste change. Ajoutée sur retour de jeu : *« le clic
m'épuise »*.

**Une pression = un Overclock. Pas de répétition au maintien**, et ce n'est pas un oubli : le modèle
d'équilibrage traite le clic comme « une MAIN, pas un auto-clicker », avec une fatigue qui allonge
les pauses au fil de la run. Une touche que l'on garderait enfoncée retirerait cette fatigue et
changerait l'ÉCONOMIE, pas seulement le confort — toutes les mesures de campagne seraient à refaire.

⚠️ **Effet de bord traité : Espace est aussi la touche `UI/Submit`.** uGUI sélectionne un bouton
quand on le clique, et la liaison générique `*/{Submit}` couvre Entrée ET Espace. Sans correctif,
un appui sur Espace après un clic sur « Acheter » rachetait l'upgrade en plus de lancer l'Overclock.
La sélection d'interface est donc vidée à chaque frame, ce qui ne casse rien aujourd'hui — le projet
n'a aucune navigation clavier ou manette. **À revoir quand la navigation manette arrivera** (cible
Steam Deck) : il faudra alors lier `UI/Submit` explicitement à Entrée et au bouton Sud.

**Action de l'Overclock (Clic manuel) :** Le clic global (Overclock) agit comme un **réveil**. S'il y a des Scripts inactifs (car sous le seuil d'automatisation), le clic de l'Overclock les démarre en priorité. Si les cycles sont déjà en cours, il avance le temps de tous les cycles de `0,5 s × ClickPowerMultiplier`.

**Revirement du 2026-08-27 — le « réveil » devient un nœud de prestige avancé.** Un clic ne
démarre PAS les Scripts à l'arrêt par défaut : il se contente d'avancer ceux qui tournent. Galérer
au lancement manuel fait partie de l'expérience, et pendant une bonne partie du jeu. Le réveil
automatique se débloque par un nœud de prestige tardif, pour fluidifier les runs de haut niveau
où relancer quinze Scripts à la main devient une corvée plutôt qu'un choix.

Le nœud est `P_OVERCLOCK_AWAKE` — « Injecteur Automatique », un seul rang, **5 000 CPU Cycles**,
branche Automatisation derrière `P_POWER_START`.

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

Les cycles sont **gelés** en fin de run et remis à zéro au redémarrage. La progression partielle n'est pas sauvegardée.

## Trace — les Proxies agissent sur le débit, jamais sur la jauge

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

### La TRAQUE PASSIVE — le temps est un coût (tranché le 2026-09-10)

L'A.M.I. te cherche activement. Rester connecté coûte, **indépendamment de ce que tu produis** :
un débit constant s'ajoute au brut, réglé par `PassiveTraceFillMinutes` (25 min).

### ⚠️ Elle ne démarre QUE lorsque le joueur s'est manifesté (corrigé le 2026-09-21)

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

### Le PLAFOND suit la puissance, et la Furtivité décide de la durée (tranché le 2026-09-15)

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

## La VICTOIRE — atteindre 1e12 TFlops (tranché le 2026-09-15)

Le lore est le hack d'une clé USB surpuissante : la fin se mesure en **capacité de calcul**.

Le critère s'est d'abord jugé sur « le dernier Hardware est débloqué », ce qui tombait au premier
exemplaire acheté — au moment où le joueur DÉCOUVRE la machine, pas au moment où il en a fait
quelque chose.

**Seuil : 1e12 TFlops.** C'est environ quatre fois le pic d'une campagne mesurée, et grosso modo le
dernier Hardware poussé au niveau 100. Débloquer la machine finale ne suffit pas : il faut revenir
la faire grandir, donc dépenser ses derniers points de prestige et refaire une run ou deux.

**Câblée dans le jeu le 2026-09-16.** `VictorySystem` (scope racine) observe `TotalTFlops` et
déclenche une fois, au franchissement. `VictoryPresenter` déroule la séquence dans la console.

**La victoire n'arrête PAS la partie.** Elle se raconte, puis le joueur reprend où il en était.
C'est un incrémental : il lui reste presque toujours de l'arbre à finir, et figer la partie au
meilleur moment de sa courbe lui retirerait ce qu'il vient de gagner. Le drapeau `HasWon` est de la
méta-progression (`SaveData` v10) — il survit au wipe, et n'existe que pour ne pas rejouer la
séquence à chaque ouverture.

⚠️ **Seuil abaissé à 5e11 à titre PROVISOIRE** (GD, 2026-09-16), pour pouvoir tester la fin de
campagne sans grinder : le pic mesuré est de 6,44e11, la cible de 1e12 obligeait à repasser deux
fois. À remonter une fois la fin de partie éprouvée. Le réglage vit dans `BalancingConfig.asset`.

**L'écran existe depuis le 2026-09-16** : `VictoryPanel`, dernier enfant du Canvas de la GameScene.
Vue autonome qui possède sa propre racine — elle ne partage rien avec l'écran de bilan, que
`PrestigePanelView` possède et bascule. Emprunter celui-ci ferait croire au joueur que sa run s'est
arrêtée, et ferait se disputer deux composants pour le même `SetActive`.

Le fond est opaque et capte les clics : l'overlay est modal, mais **la simulation continue derrière**
— les cycles tournent, la Trace monte. Gagner ne met pas la partie en pause, et ne la protège pas
non plus.

### La trace suit le rendement, à un exposant strictement entre 0 et 1

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

### Le Hardware n'est plus un pilier mort

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

### Le plafond de jauge suit la puissance, plus le seul niveau

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

## Le relevé de Trace — le danger vient de l'INCERTITUDE (tranché le 2026-09-10)

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

### Les quatre réglages, et pourquoi ils sont si extrêmes

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

### Le CAPTEUR s'achète — livré le 2026-09-16

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

⚠️ **Le contrecoup du Data Wiper aveugle aussi le capteur, et c'est voulu.** `TotalTFlops` est la
capacité EFFECTIVE : l'`UpgradeManager` lui a déjà retiré la tranche immobilisée. Purger sa Trace
fait donc perdre la vue pendant les 60 s de contrecoup — le contrecoup s'étend à tout ce que les
TFlops alimentent, comme il le fait déjà sur la compression des cycles et la dissipation.

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

## ⚠️ `04_SpecificUpgrades.json` est une SORTIE, pas une entrée

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

⚠️ La même prudence vaut pour tout fichier de `GameData/Editor/` : vérifier qu'il n'a pas de
générateur avant d'y régler quoi que ce soit à la main.

## Prérequis de prestige — un nœud ET un niveau

**Tranché le 2026-09-09.** Le prérequis n'était qu'une référence vers le nœud parent, et la règle
d'ouverture était figée à `niveau > 0`. Conséquence : **acheter le premier rang d'un nœud à dix
rangs ouvrait toute la suite de la branche.** La ligne d'Overclock — trois paliers de cinq rangs
puis l'Injecteur Automatique — se déverrouillait entièrement pour le prix d'un seul cran.

Le prérequis est désormais une struct, `PrestigeRequirement` : **quel parent, et à quel niveau.**

```json
"prerequisiteId": "P_OVERCLOCK_POWER_1",
"requiredLevel": 0
```

| `requiredLevel` | Sens |
|---|---|
| **absent ou 1** | Il suffit de posséder le parent — le comportement historique |
| **n > 1** | Ce rang précis du parent |
| **0** | Le **niveau MAXIMUM** du parent, quel qu'il devienne |

La sentinelle `0` est **auto-maintenue**, et c'est pour ça qu'elle existe : passer un nœud de cinq
à dix rangs déplace automatiquement l'exigence de ses enfants. Un entier écrit en dur aurait
silencieusement cessé de vouloir dire « au max ».

⚠️ **Piège de sérialisation à ne jamais retirer.** `PrestigeItemData.requiredLevel` est initialisé
à `1` dans le générateur. JsonUtility construit l'objet avant de le remplir : un champ absent du
fichier conserve donc cette valeur. Sans cet initialiseur, un champ omis vaudrait `0` — donc
« parent au maximum » — et les cent trente nœuds qui ne déclarent rien deviendraient tous
silencieusement bien plus durs à ouvrir. Le générateur imprime pour cette raison un
**récapitulatif des seules exigences non triviales** : si ce filet cédait, la liste passerait de
trois lignes à plus de cent trente, ce qui saute aux yeux.

Une exigence supérieure au `maxLevel` du parent est **ramenée à ce maximum**, avec avertissement :
une branche fermée pour toujours est toujours une faute de saisie, jamais une intention.

**Un seul prérequis par nœud** — l'arbre reste un arbre. Rien n'empêche de passer à une liste plus
tard, mais ce serait un graphe, avec N liens à tracer par nœud et une ligne de détail qui doit
énumérer plusieurs manques.

**Côté UI**, le lien reste en pointillé tant que le niveau exigé n'est pas atteint — un trait plein
vers un nœud verrouillé serait un mensonge visuel — et le panneau de détail chiffre l'exigence
(`> PRÉREQUIS MANQUANT : Surcharge — niveau 3/5`). Les exigences au premier rang gardent l'ancien
libellé, sans chiffres : afficher « niveau 1/1 » sur cent trente nœuds n'apprendrait rien.

### Où la règle est appliquée

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

## Branche Blindage — la colonne vertébrale de la méta

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

## Rythme — calé le 2026-08-31, corrigé deux fois le 2026-09-01

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

### Ce qui freine, et ce qui ne freine pas

Le coupable de l'explosion n'était ni les rendements ni le prix des nœuds de prestige (ceux-ci
coûtaient **0,01 CPU Cycle** — vérifié) mais deux choses :

| Levier | Effet sur les 10 premiers niveaux | Effet au niveau 190 |
|---|---|---|
| `costMultiplier` **1,07 → 1,15** | 138 $ → 203 $ *(négligeable)* | 5,5e7 $ → **2,3e13 $** |
| Paliers **`facteur ^ 0,4`** | aucun *(le 1ᵉʳ palier est au niveau 10)* | empilement ×300 → **×4** |

C'est le levier chirurgical cherché : **le début est intact, la profondeur devient chère.** Les
`baseProductionYield` restent ceux d'origine et ne doivent plus jamais servir de variable de
rythme.

### Mesures obtenues, sur les fichiers du projet

| Mesure | Valeur | Cible |
|---|---|---|
| `SCR_01` niveau 10 (automatisation) | **75 s** | rapide ✅ |
| Datas générées à la 5ᵉ minute | **40 066** | quelques dizaines de milliers ✅ |
| Première run | **9,7 min**, jusqu'à `SCR_04` | ~10 min ✅ |
| Campagne complète (arbre entier) | **10,69 h** au palier 75 % | ~10 h ✅ |
| Nombre de runs | 39 à 45 selon le palier visé | — |

## ⚠️ La cible « 30 minutes avant le premier prestige » est ABANDONNÉE (2026-09-12)

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

### ⚠️ Le simulateur ne clique jamais l'Overclock

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

### La branche Blindage est le moteur de la campagne

Sans elle, les Datas ne progressaient que de ×13 sur toute la campagne — 90 runs identiques. Avec
elle calée à `+0,01 / +0,05 / +0,2 / +1,0` par rang (plafond ×13,6 au maximum), elles font **×660**
et les runs s'allongent de 19 à 41 minutes.

⚠️ **Piège vécu :** un facteur d'échelle appliqué deux fois avait ramené ses bonus à 0,00016 par
rang. La branche devenait inerte et la campagne redevenait un tapis roulant — sans que rien ne le
signale. Toute modification du Blindage doit être suivie d'une mesure de la croissance des Datas
sur une campagne complète.

### `DissipationHalfPointRatio` (κ) décide si la défense vaut le coup

Ramené à **0,3**. À cette valeur, les Proxies sont inutiles en début de partie — le joueur est trop
petit pour être repéré — deviennent payants en milieu, puis redeviennent un mauvais achat en fin de
campagne, où le budget doit repartir vers la production.

⚠️ **κ est à revérifier après CHAQUE modification des magnitudes de trace ou de plafond.** Il ne
porte pas une valeur absolue mais un rapport, et tout changement d'échelle le déplace. Il a dû être
repris trois fois pendant ce calage (0,3 → 10 → 0,3).

## Ghost Cache et Zéro-Day Exploit

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

### Les trois nœuds de prestige de l'Exploit

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

## Bouton d'Urgence — le « Data Wiper »

Refondu le 2026-08-27. L'ancienne version achetait l'effacement avec de l'argent, comme un
pot-de-vin. La nouvelle ne coûte **rien** : elle **exige** de la puissance de calcul. Effacer ses
traces, c'est envoyer un ver dans les serveurs fédéraux — il faut de la force brute, pas des
billets.

| Paramètre | Valeur |
|---|---|
| **Prérequis** | Posséder un palier de TFlops. Rien n'est dépensé |
| **Escalade du palier** | ×3 par usage sur la run ⚠️ *valeurs provisoires : 50 TFlops de base, ×3 — non chiffrées par le GD* |
| **Effet** | −**20 points ABSOLUS** de jauge : à 63 % on tombe à 43 % |
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

## Les points de prestige — un CUMUL de campagne (tranché le 2026-09-13)

Les points ne se calculent plus sur l'argent de la seule run. Un compteur de **Datas exfiltrées
depuis le début de la campagne** ne retombe jamais à zéro, et le Nième point demande
`PremierPalier × Croissance^(N−1)` de ce cumul.

**Pourquoi.** L'ancienne conversion rendait 28 points dès la première run, et elle était pilotée
par la même valeur que le déblocage de l'exfiltration : raréfier les points rendait mécaniquement
la sortie inatteignable. Les deux sont désormais deux réglages distincts,
`PrestigeFirstThresholdDatas` et `ExfiltrationUnlockDatas`.

**Aucune run n'est perdue.** Une run qui n'atteint pas le palier suivant compte quand même : ce
qu'elle a rapporté reste au cumul. C'est un gain de ressenti considérable pour un coût nul.

⚠️ **Une SAISIE n'alimente pas le compteur, et contribution et points sont résolus au MÊME
instant, à l'exfiltration.** Ce n'est pas un détail d'implémentation, c'est ce qui ferme un
exploit : si les points tombaient pendant la run alors que la saisie annule la contribution, il
deviendrait rentable de se faire prendre exprès — on encaisserait le palier sans jamais faire
monter le seuil, et la run suivante le réencaisserait. Le multiplicateur du palier d'extraction
porte sur la **contribution**, jamais sur les points, pour la même raison.

### Ce que doit rapporter une première run (tranché le 2026-09-16)

**Cible en vigueur, et elle annule le « 28 CPU Cycles » cité plus haut**, qui datait d'une formule
abandonnée le lendemain de sa mesure.

```
PrestigeFirstThresholdDatas = 5e6
```

| Première run | Points |
|---|---|
| Bonne (15,7 M de Datas — mesurée en jeu) | **11** |
| Moyenne (7,9 M) | **5** |
| Faible (5,5 M) | **1** |
| Très faible (3 M) | 0 |

**Calibré sur une partie JOUÉE, pas sur le harnais.** Le seuil avait été posé deux fois sur des
mesures du simulateur — 1e8, puis 4e7 — et les deux étaient hors d'atteinte : une run réelle de
7 min 38, poussée jusqu'à 98 % de Trace, ne rapportait **rien du tout**. Le simulateur annonce
4,85e7 Datas pour une première run là où un joueur en fait 15,7 M ; il surestime d'un facteur 3.

⚠️ **Règle de méthode :** ce seuil, et toute grandeur exprimée en Datas absolues, se pose sur une
partie jouée. Jamais sur une sortie du harnais.

**Pourquoi si bas.** Une cible plus haute — donnant les 2 ou 3 points d'abord envisagés — faisait
retomber à zéro toute run moins bonne que celle qui avait servi de référence, c'est-à-dire
exactement le mur qu'on venait de retirer. Et le coût de la générosité est quasi nul : sur toute la
campagne le total passe de 224 à **232 points**, pour un arbre qui en coûte **147**. Il y avait
déjà 50 % de surplus.

⚠️ **La courbe est raide juste au-dessus du seuil.** `PrestigeThresholdGrowth` valant 1,12, chaque
point suivant ne demande que +12 % de cumul : une run deux fois meilleure rend deux fois plus de
points, pas deux de plus. C'est assumé — une bonne run doit se sentir.

⚠️ **`PrestigeThresholdGrowth` est VERROUILLÉ par la longueur de campagne.** C'est le levier qui
paraît naturel pour élargir la fourchette des premiers points, et il ne faut pas y toucher : à 1,12
la campagne produit ~232 points pour un arbre qui en coûte 147 ; la passer à 2,0 la ferait tomber à
39, et l'arbre deviendrait infinissable. **Le seuil est le seul levier libre.**

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

**Couplage à connaître :** ces deux chantiers ne peuvent pas se calibrer séparément. Avec un arbre
de 1450 niveaux, 4 points en achètent 4 — la production ne bouge pas, le cumul monte linéairement
pendant que les seuils montent en exponentielle, et la progression se bloque DÉFINITIVEMENT.

## ⚠️ Le simulateur CLIQUE, depuis le 2026-09-13

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

## Exfiltration volontaire — « Protocole Terre Brûlée »

| | Déclencheur | Récompense |
|---|---|---|
| **Saisie Fédérale** | Trace atteint 100 % | CPU Cycles calculés normalement |
| **Effacement Propre** | Le joueur clique avant 100 % | Bonus du **palier d'extraction** atteint |

### Paliers d'extraction — la décision est DISCRÈTE (tranché le 2026-09-10)

| Palier | Jauge requise | Bonus de CPU Cycles |
|---|---|---|
| Extraction propre | 50 % | +15 % |
| Extraction profonde | 75 % | +30 % |
| Extraction critique | 90 % | +50 % |

Sous 50 %, aucun bonus — mais les Cycles de base restent acquis. **Se faire saisir ne coûte
jamais la run, seulement ce qu'on est allé chercher.**

### Le palier 90 % est VERROUILLÉ au départ (tranché le 2026-09-16)

Il est débloqué par le nœud de prestige **`P_EXTRACT_HIGHRISK` — « Extraction Haut Risque »**, en
bout de la branche Extraction, derrière `P_EXTRACT_3`.

**Pourquoi.** Proposer le palier le plus dangereux dès la première run, quand le joueur n'a encore
aucun capteur et pilote à l'aveugle, c'est lui tendre un piège plutôt qu'un pari. Mesuré sans
capteur, la posture qui le visait mourait **15 fois sur 18** et plafonnait à 2,7 % de l'arbre. Le
réserver à qui s'est équipé fait coïncider l'invitation et les moyens de la tenir.

**La branche qui l'ouvre est celle qui achète de l'appât**, et c'est cohérent : investir dans
l'Extraction culmine en obtenant le droit d'aller chercher le plus gros palier.

⚠️ **Un palier verrouillé est IGNORÉ, pas bloquant.** Le joueur retombe sur le meilleur palier
libre qu'il a franchi : pousser à 92 % sans le nœud rapporte ×1,30 — ce que rapportent 75 % — et
jamais zéro. Vérifié par mesure sur le vrai code.

Le mécanisme est générique : le champ `requiresUnlock` de `CleanExitTier` marque n'importe quel
palier comme conditionnel. Seul celui à 90 % l'est aujourd'hui.

### Comment le joueur les lit (construit le 2026-09-16)

Les paliers n'étaient affichés **nulle part** : ni les seuils, ni les bonus. Les « zones marquées
sur la jauge » promises plus haut n'avaient jamais été construites, et la zone d'incertitude
elle-même n'était pas câblée dans la scène — le brouillard existait sans se voir.

Le dispositif tient en deux moitiés, sur **le même instrument que le danger** : séparer les deux
obligerait le joueur à quitter des yeux ce qui le menace pour lire ce qu'il y gagne.

**1. Trois traits sur la jauge de Trace**, à leur seuil, chacun dans un des quatre états :

| État | Quand | Lecture |
|---|---|---|
| **Franchi** | le relevé confirmé l'a dépassé | vert — le bonus est acquis |
| **Incertain** | la fourchette l'enjambe | ambre, pulsant — *de quel côté suis-je ?* |
| **À venir** | relevé et fourchette sont en deçà | terne |
| **Verrouillé** | palier sous condition | gris |

L'état **incertain** est le cœur du dispositif : quand la bande d'incertitude enjambe un seuil, le
joueur ignore s'il l'a déjà franchi. C'est le « je tente ou pas » rendu visible, **sans ajouter la
moindre règle** — le brouillard existant suffit à le produire.

**2. Le gain annoncé par le bouton d'exfiltration porte le bonus du palier.** Il saute d'un cran à
chaque marqueur traversé : 40 → 46 → 52 → 60 Cycles aux seuils 50/75/90. Sans cela, franchir un
palier ne changeait rien à l'écran et l'appât restait théorique.

⚠️ **Ce gain se résout sur le dernier relevé CONNU, jamais sur la vérité.** Le calculer sur la
jauge réelle ferait de ce chiffre une fenêtre sur la position exacte du joueur, et viderait le
brouillard de son sens. La règle des paliers vit en un seul endroit
(`GameSessionManager.ResolveCleanExitMultiplierFor`) : la fin de run lui passe la vérité,
l'affichage lui passe le relevé.

⚠️ **Pas de libellé sur les marqueurs.** Essayé, mesuré, abandonné : la jauge fait 200 px et trois
libellés s'y chevauchent en bouillie. La position dit le seuil, la couleur dit l'état, et le bouton
dit ce que ça vaut.

La branche de prestige **Extraction** (`07_Extraction.json`) multiplie la valeur de chaque palier.
C'est la seule branche qui récompense la *manière* de jouer plutôt que la puissance brute : plus le
joueur y investit, plus il a de raisons d'aller chercher le palier suivant, et plus une saisie lui
coûte cher. Elle achète de l'appât, pas de la sécurité.

**Deux échecs avant d'arriver là, et il faut les connaître pour ne pas y revenir.**

*Le forfait.* Le bonus a longtemps valu +20 % quelle que soit la Trace atteinte. Les Datas
s'accumulent **proportionnellement à la jauge** — mesuré : 50 % de jauge = 31 % des Datas, 90 % =
90 %, la Trace étant un compteur de production. Le dernier pour-cent de jauge valait donc
exactement ce que valait le premier : pousser de 90 à 100 % rapportait ~10 % de Datas, soit ~5 % de
Cycles après la racine carrée, alors qu'une seule saisie en coûtait 17. **Un joueur qui gagnait
tous ses paris perdait quand même.** Mesuré : le téméraire mourait 9 fois sur 32 pour 1,3 % de gain.
Le danger ne s'achetait rien — un impôt, pas un arbitrage.

*La courbe continue.* La correction suivante fut `1 + 1,5 × jauge⁵`. Excès inverse : le bonus
passait de +25 % à +89 % entre 70 % et 90 % de jauge, donc chaque seconde de retard devenait
monnayable et la sortie se jouait au chronomètre au lieu de se décider.

**Un palier ne bouge pas entre deux seuils.** Traîner ne rapporte rien, ce qui supprime
l'optimisation fine ; la seule question est franche — « je tente le palier suivant, oui ou non ? ».
Et c'est **affichable** : des zones marquées sur la jauge, ce qu'une courbe ne permettait pas.

**Mesure de validation** (campagnes complètes, arbre de prestige mené à son terme) :

| Palier visé | Runs | Durée | Saisies | Sortie réelle |
|---|---|---|---|---|
| 50 % | 49 | 12,58 h | 0 | 53,2 % |
| 75 % | 27 | 9,60 h | 0 | 83,7 % |
| 90 % | 22 | 8,44 h | 0 | 95,5 % |
| 90 %, mais en lisant le bord de la fourchette | 23 | 8,75 h | 0 | 93,0 % |

Viser haut **divise la campagne par deux**. C'est l'appât, et il est énorme.

⚠️ **Le risque, lui, n'est pas encore là : zéro saisie sur les quatre postures.** Un joueur qui sort
dès que l'affichage annonce le palier franchi garde 10 % de marge, et le relevé actuel ne se trompe
pas d'assez pour la manger. La récompense est en place, le danger reste à installer — c'est l'objet
du lot « capteurs » : rendre le brouillard épais AU DÉBUT de la partie, quand le joueur n'a encore
rien acheté pour voir clair. Voir la section sur le relevé de Trace.

**Déblocage (précisé le 2026-08-26, rétabli le 2026-09-16) :** condition UNIQUE — **la run rapporte
au moins 1 CPU Cycle**.

⚠️ **Ne JAMAIS retranscrire cette condition en un seuil de Datas.** Elle l'a été deux fois, et les
deux fois la dérivation a survécu à la formule qui la produisait :

- « 1 000 Datas sur la run » valait pour `Cycles = floor(sqrt(RunMoney / 1000))`, abandonnée le
  2026-09-13 au profit du cumul de campagne.
- `ExfiltrationUnlockDatas`, introduit ensuite pour découpler déblocage et gain, s'est retrouvé
  à **4e5** quand le premier point en demandait **5e6** — douze fois plus. Constaté en jeu le
  2026-09-16 : une run de 7 min 38 sortait en rapportant **zéro point**, sur un bouton qui
  s'annonçait pourtant « Compilation du 1er Cycle CPU ».

Le code lit désormais la condition elle-même — `PendingCycles >= 1` — et le réglage
`ExfiltrationUnlockDatas` n'est plus lu.

**Le découplage d'origine reste respecté.** Il existait parce que points et déblocage partageaient
la même valeur : raréfier les uns rendait l'autre inatteignable. Ce n'est plus le cas depuis que les
points se décrochent sur un CUMUL DE CAMPAGNE — la condition porte sur ce que rapporte **cette run**,
cumul déjà acquis déduit. Une run tardive n'ouvre donc pas le bouton à la première seconde : il lui
faut franchir son propre palier.

Pas de palier d'upgrade ni de seuil de TFlops : verrouiller derrière un achat précis casserait la liberté systémique, alors qu'un joueur qui joue mal doit pouvoir sortir s'il a farmé assez longtemps. Les TFlops sont un accélérateur, un moyen d'arriver à la fin — pas la condition de fin.

**Trois états du bouton**, la jauge portant l'explication à la place d'un texte d'aide :

1. `[VERROUILLÉ] Compilation du 1er Cycle CPU : 45 %` — barre remplie à `RunMoney / Datas exigées`, ce dénominateur variant d'une run à l'autre puisqu'il déduit le cumul déjà acquis
2. `[PRÊT] WIPE SYSTÈME (Gain : +1 Cycle CPU)` — bordures rouges, cliquable
3. `[PRÊT] WIPE SYSTÈME (Gain : +2 Cycles CPU) → Prochain à 9 000 Datas` — le seuil suivant vaut `(n+1)² × 1 000`, ce qui donne au joueur une raison de repousser sa sortie

*Note technique : en éditeur et en build de développement, le bouton est toujours cliquable même à zéro cycle, pour enchaîner des runs de test sans farmer.* Placé bien visible, en rouge, dans le Header.

- Grisé, au survol : `[ERREUR] Données insuffisantes pour compiler un nouveau noyau.`
- Débloqué : rouge clignotant, au survol `[PRÊT] Formatage recommandé. Gain estimé : +X Cycles CPU`

**Séquence :** geler l'UI ~2 s et dérouler dans la console —
```
> Lancement de PURGE.exe...
> Effacement des disques en cours... 100%
> Brouillage de l'adresse MAC... 100%
> La clé USB est en sécurité.
> Compilation des erreurs... Noyau IA amélioré.
> Redémarrage du système...
```

## Tous les réglages vivent dans `BalancingConfig.asset`

Depuis le 2026-08-27, l'équilibrage ne demande plus de toucher au C#. Un ScriptableObject unique,
`Assets/GameData/Balancing/BalancingConfig.asset`, porte **toutes** les valeurs : diviseur de
Trace, compression par TFlop, synergie des Proxies, argent de départ, bonus Clean Exit, Datas par
CPU Cycle, secondes d'Overclock, les quatre réglages du Ghost Cache et les huit du Data Wiper.

Il est modifiable **en Play Mode** : les systèmes lisent les propriétés à l'usage plutôt que de
les recopier au démarrage, donc un changement dans l'inspecteur prend effet immédiatement. C'est
le seul asset du projet qu'on édite à la main sans qu'un générateur ne l'écrase.

Chaque champ porte un tooltip qui dit ce qu'il fait et dans quel sens il pousse. Les `[Range]` ne
sont pas décoratifs : ils empêchent de saisir une valeur qui casserait une formule.

## Lore

L'objectif ultime du joueur est de récupérer **une photo de chat sur une clé USB verrouillée**. Chaque wipe = déménagement dans un nouveau sous-sol avec un noyau IA amélioré, compilé à partir des tentatives ratées. Ton absurde assumé.

## Échelle de contenu

≈ 15 améliorations par onglet (45 générateurs) et **≈ 170 nœuds de prestige**. Cette dernière valeur rend le sujet performance et le rendu de l'arbre non négociables : pooling ou virtualisation seront nécessaires.

## Sauvegarde

- Autosave toutes les **5 minutes**, plus fin de run et achat de prestige.
- Save d'urgence à la fermeture (croix, Alt+F4). Ne couvre ni un kill process ni un crash.
- **Aucune progression hors-ligne, aucun rattrapage au retour.**
- **`runInBackground` est à `true`** (tranché le 2026-08-26). « Hors-ligne » veut dire *jeu fermé*, pas *fenêtre en arrière-plan* : le joueur peut laisser tourner Ares pendant qu'il fait autre chose, et l'A.M.I. continue de le traquer. Le mettre à `false` offrirait une planque parfaite et gratuite — il suffirait d'alt-tab pour geler la Trace.
  *Note pour les tests : ce réglage ne s'applique qu'au build. Dans l'éditeur, le player loop reste figé tant que la fenêtre Unity n'a pas le focus (`Time.frameCount` ne bouge pas). Pour vérifier un `ITickable` sans focus, appeler `Tick()` à la main plutôt que d'attendre des frames.*
- Fichier en JSON lisible et éditable à la main — choix assumé jusqu'à la sortie Steam.

## Steam

Pas de page Steam ni d'AppId pour l'instant. La sauvegarde **locale fait autorité** ; le cloud viendra plus tard et servira de miroir, départagé par horodatage.