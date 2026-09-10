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

Les TFlops agissent à deux endroits :

⚠️ **Les TFlops agissent désormais à TROIS endroits, et les formules ci-dessous ont changé le
2026-08-31.** Voir « Le Hardware n'est plus un pilier mort » plus bas, qui fait foi.

**1. Compression du temps sur les Scripts**
```
TempsReel = TempsBase / (1 + TFlops × 0.05) ^ TFlopsCompressionExponent
```

Décroissance asymptotique : ne tombe jamais à zéro. L'exposant a été ajouté le 31/08 — sans lui,
la compression butait sur `minCycleDuration` dès quelques dizaines de TFlops.

**2. Efficacité des Proxies**
```
DissipationTrace = ProxyBase × (1 + log10(1 + TFlops))
```

Le `log10` donne un gros boost au début puis aplatit la courbe — le joueur ne doit **jamais**
devenir indétectable. Inchangé, mais ce n'est plus une valeur soustraite : elle alimente la
puissance `D` de la formule de saturation.

**3. Rendement des Scripts** *(ajouté le 2026-08-31)*
```
RendementScript × = (1 + TFlops) ^ TFlopsYieldExponent
```

Réservé aux Scripts. Chez un Hardware le rendement EST la capacité en TFlops : le brancher là
créerait une boucle divergente.

## Cycles de production — Scripts uniquement

Chaque Script a sa propre barre et verse son montant **à la fin** de son cycle. Le Hardware fixe la capacité TFlops dès l'achat (effet immédiat, pas de barre). Les Proxies dissipent en continu (pas de barre).

Le niveau augmente **toujours** le versement (`BaseProductionYield × niveau`). Les bonus supplémentaires viennent de **paliers explicites** listés par Script (voir `cycles.md`).

**Automatisation par seuil, propre à chaque Script.** Sous le seuil, le joueur lance chaque cycle à la main ; au-dessus, il se relance seul. Un nœud de prestige ciblé (`TargetUpgradeId`), achetable par rangs, abaissera ce seuil de quelques niveaux par rang. *(Note technique : s'assurer d'ajouter le champ `TargetUpgradeId` de type `string` dans le modèle de données `PrestigeItemData` du script de désérialisation JSON).*

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
sont jamais comptés comme réveillés : ils repartent seuls au Tick suivant.* Un même clic démarre d'un coup **tous** les Scripts possédés à l'arrêt, *et* avance de `0,5 s × ClickPowerMultiplier` ceux qui tournaient déjà. Aucun clic n'est donc perdu, et sa valeur reste constante à tous les stades de la partie. Ne pas implémenter de priorité exclusive (« tant qu'il reste un inactif, on ne fait que réveiller ») : en début de partie, chaque clic ne servirait qu'à relancer et jamais à accélérer.

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
D = Σ (base × niveau × paliers) × (1 + log10(1 + TFlops)) × (1 + TFlops × k) ^ CompressionExponent
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

**Synergie des Proxies (tranché le 2026-08-27)** : chaque niveau de Proxy possédé accélère TOUS les Scripts de 1 %, cumulé sur l'ensemble du parc — `1 + niveaux × 0,01`. Un achat de Proxy n'est donc jamais perdu, même quand la Trace est basse. Multiplicateur **dédié**, appliqué à la durée de cycle : le brancher sur les TFlops créerait une boucle, la dissipation dépendant elle-même des TFlops par son `log10`. ⚠️ Linéaire et sans plafond, contrairement au reste du jeu — c'est `minCycleDuration` qui bornera l'effet, donc un plafond subi plutôt que choisi.

**La règle d'or tient, confirmée le 2026-08-27.** Un excédent de dissipation ne fait PAS redescendre la jauge : il est capté par le Ghost Cache. Si la jauge se vidait, le joueur aurait toujours toute la marge devant lui et déclencher l'Overdrive ne coûterait rien. En la laissant où elle est, le Ghost Cache se remplit à la hauteur où le joueur s'est arrêté : à 30 % il a de la marge, à 80 % c'est un pari. C'est là que naît le choix « j'exfiltre ou je charge encore ».

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
| `SCR_01` niveau 10 (automatisation) | **90 s** | rapide ✅ |
| Datas générées à la 5ᵉ minute | **33 760** | quelques dizaines de milliers ✅ |
| Première run | **27 min**, jusqu'à `SCR_05` | ~30 min ✅ |
| Durée de run (min / médiane / max) | 19,6 / **31,0** / 40,9 min | — |
| Campagne complète (arbre entier) | **9,93 h** | ~10 h ✅ |
| Nombre de runs | 19 | — |
| Datas, run 1 → dernière | 1,4e8 → 9,3e10 (**×660**) | montée sensible ✅ |
| Arbitrage | lourde en début/milieu, **aucun Proxy en fin** | varie ✅ |

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

⚠️ **Conséquence mesurée du ×10 : les 30 secondes ne sont jamais atteintes.** La dissipation valant zéro, le temps de survie depuis une jauge vide vaut `100 / (génération × 10)`. Tenir les 30 s exige donc une génération ≤ **0,33 Trace/s**, alors qu'un seul `SCR_01` de niveau 1 en produit déjà 0,5. Vérifié en Play Mode avec `SCR_01` niveau 12 et `HW_01` niveau 4 (6,32 Trace/s) : jauge remplie à 0,632/s, saisie à **1,58 s**. L'Exploit est donc, en l'état, un bouton « encaisse et exfiltre » de une à deux secondes, pas une fenêtre de 30 s. À arbitrer — voir `backlog.md`.

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

Le contrecoup ET le délai sont **sauvegardés** (`SaveData` v5), contrairement à l'Overdrive du
Ghost Cache. L'asymétrie est volontaire : là-bas, sauvegarder aurait permis de mettre en PAUSE un
bonus ; ici, ne pas sauvegarder permettrait d'ÉCHAPPER à une pénalité en fermant la fenêtre.

## Exfiltration volontaire — « Protocole Terre Brûlée »

| | Déclencheur | Récompense |
|---|---|---|
| **Saisie Fédérale** | Trace atteint 100 % | CPU Cycles calculés normalement |
| **Effacement Propre** | Le joueur clique avant 100 % | CPU Cycles **+20 % « Clean Exit »** |

Le bonus pousse le joueur à flirter avec 95 % de Trace puis à sortir juste avant l'arrestation.

**Déblocage (précisé le 2026-08-26) :** condition UNIQUE — avoir de quoi gagner au moins 1 CPU Cycle, soit **1 000 Datas générées sur la run** (dépensées ou non), puisque `Cycles = floor(sqrt(RunMoney / 1000))`.

Pas de palier d'upgrade ni de seuil de TFlops : verrouiller derrière un achat précis casserait la liberté systémique, alors qu'un joueur qui joue mal doit pouvoir sortir s'il a farmé assez longtemps. Les TFlops sont un accélérateur, un moyen d'arriver à la fin — pas la condition de fin.

**Trois états du bouton**, la jauge portant l'explication à la place d'un texte d'aide :

1. `[VERROUILLÉ] Compilation du 1er Cycle CPU : 45 %` — barre remplie à `RunMoney / 1000`
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