# Décisions de game design — tranchées, à appliquer telles quelles

Validées par Bertrand le 2026-08-25, refondues avec le GD le 2026-10-13 (refonte « Time Attack »).
Ne pas réinventer ces règles ; si l'une pose problème, le signaler avant d'écrire du code qui la
contourne.

> **Refonte « Time Attack » livrée le 2026-10-14, jouée et fusionnée le 2026-10-15.** Décidée avec le GD le
> 2026-10-13, sur la base de deux documents hors dépôt (`GDD_Refonte_Ares.pdf`, `GDDV2.pdf`) et de
> ses arbitrages écrits, tous consignés ici. Le document a été réécrit **lot par lot** ; ce qu'il
> remplace est dans `historique.md`, sections « Refonte Time Attack ».
>
> | Lot | Domaine | Ce document |
> |---|---|---|
> | 1 | Jauge de l'A.M.I., Proxies, Overclock, branches Blindage / Interception / Overclock, retrait du Ghost Cache | **à jour** |
> | 2 | Data Wiper ; écran de fin de run sauvegardé | **à jour** |
> | 3 | Brouillard : relevé en UT, borne sur la vitesse de l'A.M.I., relevé sauvegardé | **à jour** |
> | 4 | Arbre à 21 nœuds, Noyau Compilé, `P_CORE_OVERRIDE`, Extraction, Économie | **à jour** |
> | 5 | Nettoyage : code, données et réglages morts, migrations de sauvegarde | **à jour** (rien à décider) |

## Les quatre ressources

| Ressource | Rôle | Source |
|---|---|---|
| **Argent (`$`)** | Monnaie d'achat unique : Scripts, Hardware **et** Proxies | Cycles des Scripts |
| **TFlops** | Capacité de calcul — n'achète rien. Les **nettes** accélèrent l'économie, les **brutes** accélèrent la menace | Hardware possédé |
| **Trace (A.M.I.)** | Compte à rebours de la run : 10 000 UT, saisie au bout | Le temps, indexé sur les TFlops brutes |
| **CPU Cycles** | Monnaie de méta-progression, survit au wipe | Fin de run |

Principe directeur : **aucune ressource ne doit flotter**. Si un joueur peut ignorer une ressource, l'économie est cassée.

## Les TFlops sont une CAPACITÉ dérivée, pas un stock

La valeur ne bouge qu'à l'achat d'un Hardware (ou d'un nœud, ou d'un sacrifice du Data Wiper).
Elle ne s'accumule pas dans le temps et ne se dépense pas.

**Deux lectures depuis le 2026-10-13 :**

```
TFlops BRUTES = Σ rendement des Hardware possédés
TFlops NETTES = BRUTES × P_DATAM × Noyau × (1 − sacrifice du Data Wiper)
```

| | Brutes | Nettes |
|---|---|---|
| Ce qu'elles mesurent | La puissance de la **menace** | La puissance de l'**économie** |
| Ce qui les lit | La vitesse de l'A.M.I., seule | Compression et rendement des Scripts, capteur du brouillard, victoire, affichage |

**Pourquoi séparer.** Indexer la vitesse de l'A.M.I. sur les TFlops nettes faisait de chaque nœud
d'économie (`P_DATAM`, le Noyau) un accélérateur de mort. Le prestige doit accélérer les revenus
**sans accélérer la traque**. Et le sacrifice du Data Wiper ne doit jamais ralentir l'A.M.I. : sans
cette règle, il deviendrait un soin surpuissant (« Le sacrifice devient un vrai choix punitif » —
le GD, 2026-10-13).

*Le Noyau Compilé (lot 4) multiplie les nettes par son rendement. Le sacrifice du Data Wiper
(lot 2) en est retranché, jusqu'à la fin de la run.*

**Conséquence technique (conservée depuis août) :** `SaveData` ne stocke aucune TFlop, elles se
recalculent depuis les niveaux d'Upgrades.

## LES TROIS PILIERS — refonte « Time Attack » (2026-10-13)

**C'est la règle qui prime sur toutes les autres.** Elle remplace la « Trinité » du 2026-09-10
(archivée) : la Trace dynamique — générée par chaque Script, la chaleur du Hardware, un plafond
qui suivait la puissance, une réduction saturante des Proxies, le Ghost Cache — avait fini par
réclamer une formule par pilier et une correction par session d'équilibrage. Le GD l'a jugée
trop complexe pour ce qu'elle apportait.

| Pilier | Rôle, en une phrase | Ce qu'il touche |
|---|---|---|
| **Scripts** | **L'attaque.** Ils rapportent. | L'argent. **Plus aucune Trace.** |
| **Hardware** | **Le moteur.** Il accélère tout. | Les TFlops — donc les Scripts **et** l'A.M.I. |
| **Proxies** | **Les consommables de survie.** | Chaque achat retire des UT à la jauge |

La décision du joueur reste énonçable : *je stagne* → Scripts. *Je veux aller plus vite* →
Hardware, en sachant que l'A.M.I. accélère aussi. *Le temps me manque* → Proxies.

## La jauge de l'A.M.I. — le compte à rebours

La Trace n'est plus générée par les actions du joueur : c'est un compte à rebours inéluctable,
indexé sur la puissance installée.

| | Valeur |
|---|---|
| Plafond | **10 000 UT**, fixe — il ne varie jamais en cours de partie |
| Plancher | 0 UT |
| Saisie Fédérale | au plafond, instantanée |

```
V_AMI (UT/s) = 33 × (1 + 0,05 × TFlops BRUTES) ^ 0,10  ÷  (1 + Σ Blindage)
```

Tous ces nombres vivent dans `BalancingConfig.asset` (`_amiTraceCap`, `_amiBaseSpeed`,
`_amiTFlopsFactor`, `_amiSpeedExponent`). Le `ThreatManager` est **le seul à modifier la jauge** :
le temps (poussé à chaque frame par le `SimulationTicker`), la pénalité d'Overclock, le Wipe des
Proxies et le Data Wiper passent tous par lui. Il tient la vitesse en cache et ne la recalcule
qu'au changement de ses entrées (TFlops brutes, Blindage, ou réglage retouché dans l'inspecteur).

**L'exposant 0,10 (tranché le 2026-10-13).** Il est **distinct** de celui de la compression des
Scripts (0,25), et plus bas à dessein : le Hardware fait **gagner la course**, les Scripts
accélérant plus vite que la menace. À 0,25 — la première version — la run de victoire durait
moins d'une seconde sans Blindage, 10 s avec : injouable.

**Durée réelle d'une run** — `10 000 × (1 + Σ Blindage) ÷ (33 × (1 + 0,05 × Brutes)^0,10)`,
⚖️ calculée, Proxies et Overclock non compris :

| TFlops brutes | Sans Blindage | Blindage max (÷ 11) |
|---|---|---|
| 0 | 5 min 03 | 55 min |
| 1e3 | 3 min 24 | 37 min |
| 1e6 | 1 min 43 | 18 min 50 |
| 1e9 | 52 s | 9 min 26 |
| ~2,4e11 *(victoire : 1e12 nettes)* | 30 s | **5 min 27** — la cible de pacing du GD |

### ⚠️ La traque ne démarre QUE lorsque le joueur s'est manifesté (2026-09-21, conservé)

Deux manifestations l'arment, et l'une suffit : avoir volé sa **première Data**, ou posséder du
**Hardware**. Avant, la jauge ne bouge pas. Relevé en jeu le 2026-09-21 sur l'ancien modèle : le
joueur regardait sa jauge monter avant tout achat — *« je suis puni sans avoir commencé à
jouer »*. L'A.M.I. cherche un intrus, pas une machine éteinte.

Aucune des deux conditions ne redescend pendant une run, donc la traque ne clignote pas. Aucune
faille : ne rien faire ne rapporte rien. Le GD l'a reconduite le 2026-10-13.

## Les Proxies — des secondes de survie (2026-10-13)

Plus de réduction passive, plus d'asymptote : un Proxy est un **consommable**. **À l'instant de
l'achat**, il retire des UT à la jauge.

```
Wipe (UT) = wipeSeconds × paliers du niveau acheté × quantité × V_AMI × (1 + Σ Interception)
```

- **Exprimé en secondes de survie**, converties en UT à la vitesse courante de l'A.M.I. Une échelle
  fixe en UT était une erreur face à une vitesse dynamique : dérisoire en fin de partie, ou vingt
  jauges vidées d'un coup au début. Un `PRX_01` rend toujours ~2,5 s de répit, avant bonus.
- **Chaque niveau compte avec les paliers de SON rang** (×1,32 au niveau 10, ×9,8 cumulés au
  niveau 100). Un achat ×10, ×100 ou MAX retire autant de Wipes qu'il achète de niveaux.
- **La jauge ne descend pas sous zéro, l'excédent est PERDU.** C'est voulu : il faut acheter au bon
  moment, pas spammer jauge vide. La console annonce les UT réellement retirées, zéro compris.
- Le champ JSON s'appelle `wipeSeconds`, et les paliers des Proxies `WipeMultiplier`.

**Valeurs de base, tranchées par le GD le 2026-10-13** — une progression géométrique qui
ralentit de ×1,4 à ×1,25 d'un Proxy au suivant :

| `PRX_01` | `_02` | `_03` | `_04` | `_05` | `_06` | `_07` | `_08` | `_09` | `_10` | `_11` | `_12` | `_13` | `_14` | `_15` |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 2,5 s | 3,5 | 5 | 7 | 10 | 14 | 19 | 25 | 33 | 43 | 55 | 70 | 90 | 115 | 145 |

Elles remplacent les anciennes magnitudes de dissipation (2,54 à 196 700), qui, lues en secondes,
faisaient de chaque achat de fin de liste une remise à zéro de plusieurs jauges. ⚖️ Sans
Interception, un niveau 1 retire au plus 48 % d'une jauge de départ (`PRX_15`), et 44 % d'une
jauge de victoire Blindage max.

**Une échelle commune** : `_proxyWipeScale` (×1) multiplie le Wipe de tous les Proxies, avant
l'Interception. Levier d'équilibrage demandé par le GD le 2026-10-15 ; il change aussi ce
qu'annonce la carte de chaque Proxy.

**Le gaspillage est assumé** (GD) : en fin de partie, les derniers Proxies deviennent des purges
totales et aveugles, dont l'excédent part dans le vide ; le joueur qui veut économiser dose un lot
×10 ou ×100 d'un Proxy de milieu de liste pour ramener la jauge juste sous le seuil critique.

## L'Overclock — un pari, plus un clic gratuit (2026-10-13)

| | Règle |
|---|---|
| Effet | Avance tous les cycles **en cours** de `0,5 s + Σ branche Overclock` (jusqu'à +22 s) |
| Coût | `V_AMI × 5 s` d'UT ajoutées à la jauge : **toujours cinq secondes de survie** |
| Sans cycle en cours | Ne fait **rien** et ne coûte **rien** (la console le dit) |
| Cadence | 0,1 s minimum entre deux appuis pris en compte ; pas de répétition au maintien |
| Gestes | Barre d'espace **et** bouton à l'écran, mêmes règles |

**La pénalité est indexée sur la vitesse, pas fixe.** La première version (+500 UT fixes) coûtait
15 s de survie au départ et une fraction de seconde en fin de partie. En secondes, la prise de
risque reste proportionnelle de la première à la dernière minute. Le Blindage ne la réduit qu'en
UT, jamais en secondes, puisqu'il ralentit la vitesse elle-même.

**Sans arbre, l'appui est perdant d'un facteur dix — et c'est voulu.** 0,5 s d'avance contre 5 s
de survie. L'Overclock devient rentable quand l'avance dépasse 5 s, soit ~13 points dans la
branche Overclock (`P_CLICK` et `P_OVERCLOCK_POWER_1` au max, 3 rangs de `_2`). Le GD :
*« un acte de désespoir en début de partie — gratter les Datas manquantes avant la saisie —, une
vraie mécanique une fois investi dans l'arbre. L'APM ne doit pas être la stratégie dominante sans
investissement. »*

**L'avance passe avant la pénalité.** Un appui qui déclenche la saisie verse quand même les cycles
qu'il vient d'achever : le joueur a payé, il touche, puis il tombe.

**Disparus avec la refonte :** le « réveil » des Scripts à l'arrêt (`P_OVERCLOCK_AWAKE`) et le
verrou de la barre d'espace (`P_OVERCLOCK_SPACEBAR`). L'Espace est libre dès le départ — la
pénalité suffit à décourager le spam.

## L'arbre de compilation — 21 nœuds (refonte, lots 1 et 4)

Le GD a remplacé les 140 nœuds de l'arbre — dont 120 nœuds ciblés, un rabot de coût et un de
rendement par générateur — par **20 nœuds majeurs**, le **Noyau Compilé** et un puits à points
infini. Chaque branche part de `P_ROOT` et se parcourt en chaîne : un nœud n'ouvre le suivant qu'**à
son niveau maximum** (`requiredLevel: 0`, voir « Prérequis de prestige »).

| Branche | Effet | Nœuds | Rangs | Coût | Apport |
|---|---|---|---|---|---|
| **Blindage** | Vitesse de l'A.M.I. ÷ `(1 + Σ)` | `P_HARDEN_1` à `_4` | 1 chacun | 1, 3, 6, 10 | +0,5 · +1,0 · +2,5 · +6,0 |
| **Interception** | Wipe des Proxies × `(1 + Σ)` | `P_INTERCEPT_1` à `_4` | 5 chacun | 1 / rang | +0,2 · +1,0 · +1,0 · +1,5 par rang |
| **Overclock** | Avance de l'Overclock `+ Σ` secondes | `P_CLICK`, `P_OVERCLOCK_POWER_1` à `_3` | 5 chacun | 1 / rang | +0,1 · +0,3 · +1,0 · +3,0 s par rang |
| **Économie** | Multiplicateur de coût de base `− Σ` | `P_REFAC` | 10 | 1 / rang | −0,01 par rang |
| | TFlops nettes × `(1 + Σ)` | `P_DATAM`, après `P_REFAC` | 10 | **2** / rang | +0,1 par rang |
| **Extraction** | Bonus des TROIS paliers × `(1 + Σ)` | `P_EXTRACT_1` à `_3` | 5 chacun | 1 / rang | +0,5 par rang |
| | Le palier à 90 % verse son bonus | `P_EXTRACT_HIGHRISK`, après `P_EXTRACT_3` | 1 | 1 | — |
| **Data Wiper** | Débloque le bouton | `P_EMERG` | 1 | 1 | — |
| **Noyau** | Puits à points | `P_CORE_OVERRIDE` | **sans plafond** | 1 / rang | aucun effet propre |

Au maximum : Blindage Σ 10 (jauge ×11 en durée, 20 points), Interception Σ 18,5 (Wipe ×19,5,
20 points), Overclock Σ 22 s (avance 22,5 s, 20 points), `P_REFAC` ×1,15 → ×1,05 sur la courbe de
prix (10 points), `P_DATAM` ×2 (20 points), Extraction ×8,5 (15 points), plus `P_EMERG` et
`P_EXTRACT_HIGHRISK`. **L'arbre utile coûte 107 points** ; une campagne en produirait ~220
(estimation, voir « Les points de prestige »).

Ces sommes sont exposées par le `PrestigeManager`, toutes en lecture seule (`TotalHardenBonus`,
`TotalInterceptBonus`, `TotalOverclockBonus`, `GlobalComputeMultiplier`, `CostMultiplierReduction`,
`CleanExitBonusMultiplier`, `TotalPointsSpent`, `TotalCostMultiplier`, `TotalYieldBonus`).

### Le Noyau Compilé — l'investissement paie, quel qu'il soit (lot 4, 2026-10-14)

Il remplace les 120 nœuds ciblés, et ne lit qu'une grandeur : les **points dépensés** dans l'arbre,
`P_CORE_OVERRIDE` compris.

```
Coût de TOUS les générateurs (Scripts, Hardware, Proxies)  × max(0,05 ; 0,99 ^ points)
Datas des Scripts ET TFlops nettes                          × (1 + 0,01 × points)
```

| Points dépensés | Coûts | Rendement |
|---|---|---|
| 10 | −10 % | ×1,10 |
| 50 | −39 % | ×1,50 |
| 107 — l'arbre utile complet | −66 % | ×2,07 |
| 220 — ~une campagne | −89 % | ×3,20 |
| 299 et au-delà | −95 %, le plancher | ×3,99, puis sans plafond |

**Le Noyau ne touche jamais la menace.** Il multiplie les TFlops nettes, pas les brutes : la vitesse
de l'A.M.I. n'en bouge pas, le Wipe des Proxies non plus. Il entre deux fois dans les Datas d'un
Script — directement, et par les TFlops nettes qui majorent leur rendement `(1 + TFlops)^0,15`. C'est
la formule du GD.

**Deux réductions de coût, à ne pas confondre.** Le Noyau abaisse le **prix d'entrée** (le coût de
base) ; `P_REFAC` abaisse la **hausse à chaque niveau** (le multiplicateur, jamais sous 1,01).

**Affichage.** Une ligne « Noyau : coûts −66 %, rendement ×2,07 » en tête du panneau d'upgrades et
de l'écran de l'arbre. **Masquée tant qu'aucun point n'est dépensé** (Bertrand, 2026-10-14) :
« −0 % / ×1,00 » parlerait d'un système que le joueur ne connaît pas encore.

Réglages : `_coreCostBase` (0,99), `_coreCostFloor` (0,05) et `_coreYieldPerPoint` (0,01), dans le
`BalancingConfig`.

### `P_CORE_OVERRIDE` — le puits à points

Une campagne produit bien plus de points que l'arbre n'en consomme (le GD : « plus de 220 générés
pour 107 utiles »). Ce nœud les absorbe : coût 1, prérequis `P_ROOT`, **aucun plafond**. Chaque rang
n'ajoute qu'un point dépensé au Noyau. L'arbre l'affiche « Niv. X », sans maximum.

Côté données, `"maxLevel": 0` veut dire « sans plafond » ; le générateur liste ces nœuds à chaque
passage, pour qu'un `maxLevel` oublié ne rende pas un nœud infini en silence (le champ vaut 1 s'il
est absent).

### Disposition — un éventail vers le bas

La racine en haut, sept colonnes en dessous : Interception, Blindage, Data Wiper, Noyau, Économie,
Extraction, Overclock. Chaque chaîne se lit de haut en bas. *Provisoire : Bertrand veut voir en jeu
si ça ne fait pas trop « arbre de talents de RPG » (2026-10-14).*

### L'Interception rabotée — validée par le GD (2026-10-15)

À ×102 (+4 et +15 par rang pour `P_INTERCEPT_3` et `_4`), en fin de partie Blindage max (jauge de
~330 s), `PRX_01` niveau 1 retirait 78 % de la jauge et **tout Proxy à partir de `PRX_02` la
vidait** : le dosage fin d'un Proxy de milieu de liste, décrit par le GD, n'existait plus. Le GD a
tranché le 2026-10-14 : les valeurs de base des Proxies restent, c'est l'arbre qu'on rabote, pour
que le multiplicateur final oblige encore à racheter des Proxies chers.

**Valeurs de départ (lot 4) : +1,0 et +1,5 par rang**, soit ×19,5 au maximum. Calcul, mêmes
conditions : `PRX_01` retire ~15 % de la jauge, `PRX_05` ~59 %, et il faut `PRX_07` pour la vider.
Les apports par rang restent croissants (0,2 · 1 · 1 · 1,5). **Validées le 2026-10-15** par le
critère du GD : `PRX_05` niveau 1 doit vider 50 à 60 % d'une jauge de fin de partie (« Les cibles
de la campagne »).

**Retirés de l'arbre au lot 1 :** `P_STEALTH` (réduction passive de Trace, qui n'a plus d'objet),
toute la branche Zéro-Day (`P_EXPLOIT_CHARGES`, `_MULT`, `_TRACE_REDUC`), `P_OVERCLOCK_AWAKE`,
`P_OVERCLOCK_SPACEBAR`. **Le Ghost Cache et le Zéro-Day Exploit disparaissent du jeu** — leur
refonte en « Baroud d'Honneur », tranchée le 2026-09-16, est abandonnée avec eux.

**Retirés au lot 4 :** les 120 nœuds ciblés et leur générateur (`PrestigeSpecificNodesGenerator`),
les types de bonus que plus aucun nœud ne portait (argent et TFlops de départ, automatisation
globale) et le plafond des réductions ciblées (`_maxTargetedReduction`). Les valeurs d'avant le
lot 4 sont archivées dans `historique.md`.

## Les TFlops : vitesse et rendement des Scripts

Les TFlops **nettes** agissent sur les Scripts de deux façons :

```
DuréeRéelle = DuréeBase × paliers ÷ (1 + 0,05 × TFlopsNettes) ^ 0,25
Datas par cycle = BaseProductionYield × niveau × paliers × (1 + TFlopsNettes) ^ 0,15
```

La compression est asymptotique, jamais nulle ; elle a remplacé le mur `minCycleDuration`
(2026-08-31), qui saturait dès quelques dizaines de TFlops et tuait le pilier. Ce champ a quitté
les données au lot 5 de la refonte ; un plancher **absolu** unique (`AbsoluteMinCycleDuration`, 0,05 s) sert de garde-fou de boucle.

Le boost de rendement est **réservé aux Scripts** : chez un Hardware le rendement EST la capacité
en TFlops, le brancher là créerait une boucle divergente.

Les TFlops **brutes**, elles, n'alimentent que la vitesse de l'A.M.I. (ci-dessus). C'est tout le
pari du Hardware : il accélère la production plus vite que la menace (exposant 0,25 contre 0,10),
mais il accélère les deux.

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
on l'approfondit.

## Cycles de production — Scripts uniquement

Chaque Script a sa propre barre et verse son montant **à la fin** de son cycle. Le Hardware fixe la
capacité TFlops dès l'achat (effet immédiat, pas de barre). Les Proxies agissent à l'achat (pas de
barre).

Le niveau augmente **toujours** le versement (`BaseProductionYield × niveau`). Les bonus
supplémentaires viennent de **paliers explicites** listés par Script (voir `cycles.md`).

**Automatisation par seuil, propre à chaque Script.** Sous le seuil (niveau 10 sur les 25
Scripts), le joueur lance chaque cycle à la main ; au-dessus, il se relance seul.

**CONSERVÉE — tranché par le GD le 2026-10-13**, ce qui annule l'intention du 2026-09-25 de la
supprimer. *« Dans un modèle Time Attack où les cycles durent 0,05 s, forcer le relancement manuel
au-delà du niveau 10 est injouable. La tension viendra de la jauge, plus de l'APM. »* Aucun nœud ne
l'abaisse depuis le retrait de `P_AUTO_GLOBAL` (2026-10-02) puis des nœuds ciblés (2026-10-14).

**La BARRE ESPACE déclenche un Overclock (ajoutée le 2026-09-16)**, comme le bouton à l'écran — voir
« L'Overclock ». Ajoutée sur retour de jeu : *« le clic m'épuise »*.

⚠️ **Effet de bord traité : Espace est aussi la touche `UI/Submit`.** uGUI sélectionne un bouton
quand on le clique, et la liaison générique `*/{Submit}` couvre Entrée ET Espace. Sans correctif,
un appui sur Espace après un clic sur « Acheter » rachetait l'upgrade en plus de lancer l'Overclock.
La sélection d'interface est donc vidée à chaque frame, ce qui ne casse rien aujourd'hui — le projet
n'a aucune navigation clavier ou manette. **À revoir quand la navigation manette arrivera** (cible
Steam Deck) : il faudra alors lier `UI/Submit` explicitement à Entrée et au bouton Sud.

Les cycles sont **gelés** en fin de run et remis à zéro au redémarrage. La progression partielle n'est pas sauvegardée.

## La VICTOIRE — atteindre 1e12 TFlops (tranché le 2026-09-15)

Le lore est le hack d'une clé USB surpuissante : la fin se mesure en **capacité de calcul**.

Le critère s'est d'abord jugé sur « le dernier Hardware est débloqué », ce qui tombait au premier
exemplaire acheté — au moment où le joueur DÉCOUVRE la machine, pas au moment où il en a fait
quelque chose.

**Seuil : 1e12 TFlops.** C'est environ quatre fois le pic d'une campagne mesurée, et grosso modo le
dernier Hardware poussé au niveau 100. Débloquer la machine finale ne suffit pas : il faut revenir
la faire grandir, donc dépenser ses derniers points de prestige et refaire une run ou deux.

**Câblée dans le jeu le 2026-09-16.** `VictorySystem` (scope racine) observe les TFlops nettes (`NetTFlops`) et
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

## Le brouillard — le danger vient de l'INCERTITUDE (refondu au lot 3, 2026-10-14)

**Pourquoi il existe** (tranché le 2026-09-10, toujours vrai). Un compte à rebours déterministe,
entièrement observable et piloté par celui qu'il menace ne peut pas être dangereux : c'est un
budget, pas une menace. La jauge de l'A.M.I. est justement devenue un compte à rebours
déterministe. Le brouillard reste donc — mais **il ne vit que dans l'affichage** : aucune règle de
jeu ne le lit, le `ThreatManager` reste la seule vérité. La saisie tombe toujours à 10 000 UT
exactement ; on ne perd jamais sur un tirage, on perd sur une estimation mal faite.

### Deux lectures sur la jauge

| | Ce que c'est | Qui la lit |
|---|---|---|
| **L'aiguille** | Le dernier **relevé** de la jauge. Elle ne bouge qu'à chaque relevé | Les paliers d'extraction, le gain annoncé par le bouton de sortie, les alertes sonores |
| **La borne pessimiste** (barre rouge) | Où la jauge peut être, au pire. Avance à chaque frame | Le danger ; l'état « incertain » d'un palier qu'elle enjambe |

```
borne = relevé + V_AMI × âge du relevé × pessimisme + sauts connus
```

- **Calculée avec la vitesse de l'A.M.I.**, qui est connue, et non plus avec un débit mesuré : elle
  ne disparaît plus après un achat de Proxy (session du 2026-10-14), elle repart aussitôt du
  relevé recalé.
- **Jamais sous la vérité** : la vitesse ne baisse pas pendant une run, et le pessimisme ne
  descend pas sous 1. Les **sauts connus** sont les pénalités d'Overclock — le joueur les connaît,
  la console les annonce ; sans eux, trois appuis rapides feraient passer la borne sous la vérité.
  Vérifié sur une réplique du calcul (Overclock en rafale, Hardware acheté en cours de route) : la
  borne rejoint la vérité au relevé, jamais en dessous.
- **Les paliers et le gain se lisent sur l'aiguille**, jamais sur la vérité ni sur la borne : le
  joueur ne touche jamais moins que ce qu'on lui annonce (choix du GD, 2026-10-13). Le calcul du
  gain vit en un seul endroit (`GameSessionManager.ResolveCleanExitMultiplierFor`) : la fin de
  run lui passe la vérité, l'affichage lui passe le relevé.

### Un relevé tous les N UT remplis, pas toutes les N secondes

L'ancien intervalle en secondes (60 s au départ) ne tenait plus face à une vitesse qui varie d'un
ordre de grandeur : au départ la jauge restait figée une minute (« la jauge n'avance pas, aucune
idée de la progression de l'A.M.I. », session du 2026-10-14), en fin de partie une run durait moins
qu'un intervalle. En UT, le relevé suit le remplissage de lui-même — Overclock compris.

| | Départ | Avec les TFlops nettes |
|---|---|---|
| Intervalle | **800 UT** (~24 s à 33 UT/s) | descend vers **200 UT** |
| Pessimisme | **×3** (la borne devance la vérité d'au plus 24 % juste avant un relevé) | descend vers ×1, jamais en dessous |

**Le capteur s'achète** (livré le 2026-09-16, conservé) :
`diviseur = 1 + 0,5 × log10(1 + TFlops nettes ÷ 1000)`. Il divise l'intervalle et ramène le
pessimisme vers 1. ⚖️ Calculé : 695 UT à 1e3 TFlops, 320 UT à 1e6, plancher de 200 UT vers 1e9 —
en fin de partie, comme le veut le GD. La puissance du capteur est passée de 1 à **0,5** le
2026-10-14 pour cette raison : à 1, le plancher tombait dès 1e6.

**Les TFlops NETTES**, à dessein : le sacrifice du Data Wiper aveugle aussi le capteur, et la
méta-progression aide à voir clair.

### Le relevé est sauvegardé (`SaveData` v13)

Recaler le relevé sur la vérité au chargement — le comportement d'avant — révélait la position
exacte de la jauge : quitter puis relancer suffisait à lever le brouillard (session du 2026-10-14).
Le dernier relevé, son âge et les sauts connus sont désormais sauvegardés et restaurés tels quels.

### Ce qui n'a pas changé

- La pause, le menu et l'écran de fin figent le brouillard avec la simulation.
- Le libellé dit l'âge du relevé (« relevé il y a N s ») — il vieillit désormais d'une seconde à
  l'autre ; jusqu'au lot 3, il affichait l'âge du relevé précédent puis se figeait.
- Les marqueurs de palier, leurs quatre états (franchi, incertain, à venir, verrouillé) et leurs
  alertes sonores (voir « Comment le joueur les lit », plus bas).

*L'ancienne section — débit mesuré, intervalle en secondes, mesures de septembre — est archivée
dans `historique.md`.*

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
« parent au maximum » — et les têtes de branche, qui ne déclarent rien, deviendraient
silencieusement bien plus dures à ouvrir. Le générateur imprime pour cette raison un
**récapitulatif des seules exigences non triviales** : si ce filet cédait, la liste passerait des
dix chaînons aux vingt nœuds de l'arbre, ce qui saute aux yeux. Le champ `maxLevel` a reçu le même
filet au lot 4 (voir « `P_CORE_OVERRIDE` »).

Une exigence supérieure au `maxLevel` du parent est **ramenée à ce maximum**, avec avertissement :
une branche fermée pour toujours est toujours une faute de saisie, jamais une intention.

**Un seul prérequis par nœud** — l'arbre reste un arbre. Rien n'empêche de passer à une liste plus
tard, mais ce serait un graphe, avec N liens à tracer par nœud et une ligne de détail qui doit
énumérer plusieurs manques.

**Côté UI**, le lien reste en pointillé tant que le niveau exigé n'est pas atteint — un trait plein
vers un nœud verrouillé serait un mensonge visuel — et le panneau de détail chiffre l'exigence
(`> PRÉREQUIS MANQUANT : Surcharge — niveau 3/5`). Les exigences au premier rang gardent l'ancien
libellé, sans chiffres : afficher « niveau 1/1 » sur les têtes de branche n'apprendrait rien.

### Où la règle est appliquée

Depuis la refonte « Time Attack » (2026-10-13), la règle du GD est uniforme : **un nœud doit être à
son niveau MAXIMUM pour ouvrir le suivant**. Elle s'écrit `requiredLevel: 0` partout.

| Chaîne | Exigence | Depuis |
|---|---|---|
| Overclock — `P_OVERCLOCK_POWER_1` à `_3` | le max du parent (5) | lot 1 |
| Interception — `P_INTERCEPT_2` à `_4` | le max du parent (5) | lot 1 |
| Blindage — `P_HARDEN_2` à `_4` | le max du parent (1, nœuds à un rang) | lot 1 |
| Économie — `P_DATAM` | le max de `P_REFAC` (10) | lot 4 |
| Extraction — `P_EXTRACT_2`, `_3`, `P_EXTRACT_HIGHRISK` | le max du parent (5) | lot 4 |
| Têtes de branche et `P_CORE_OVERRIDE` | `P_ROOT`, acquise d'office | — |

Un nœud **sans plafond** ne peut pas servir de parent « au maximum » : son maximum n'est jamais
atteint. Le générateur le refuse en erreur.

Le prérequis « avoir dépensé X points », envisagé le 2026-09-25 pour les nœuds tardifs, est
**abandonné** (GD, 2026-10-13). Le palier 3 du Blindage (tranché le 2026-09-09) n'a plus d'objet
depuis que ses nœuds n'ont qu'un rang ; son raisonnement est archivé.

## Principes de rythme

Deux règles survivent aux calages de septembre (archivés dans `historique.md`), parce qu'elles ne
dépendent d'aucune formule de Trace :

> **Règle 1 : on ne ralentit JAMAIS le début pour allonger une run.** Le frein se place plus loin.
> Le premier calage avait divisé tous les rendements par 10 : `SCR_01` atteignait son
> automatisation en 1 256 s au lieu de 69 s.

> **Règle 2 : la montée en puissance doit venir des bonus de prestige, pas de l'intérieur d'une
> run.** Sans quoi le joueur brûle l'économie du jeu dans sa première run, et le prestige ne sert
> plus à rien.

Et une règle de méthode, apprise à chaque session : **une grandeur exprimée en Datas absolues se
pose sur une partie JOUÉE**, jamais sur une sortie de simulateur. Le harnais d'équilibrage, qui
simulait l'ancien modèle de Trace, a été supprimé le 2026-10-13 ; il est refait sur le nouveau
modèle pour la session « chiffres », et la règle tient : il est calé sur des runs jouées (« Les
cibles de la campagne », La méthode).

## Les cibles de la campagne — session « chiffres » (GD, 2026-10-15)

Fixées par le GD le 2026-10-15. C'est contre elles que la campagne sera mesurée, à **±20 %** près
sur les temps et les revenus.

| Cible | Valeur |
|---|---|
| Durée de la campagne | **10 à 12 h de jeu actif**, de la première connexion à la victoire (1e12 TFlops nettes) |
| Nombre de runs | **80 à 100** |
| Durée d'une run | en **cloche asymétrique** : ~5 min au début, 8 à 11 min en milieu de campagne, ~5 min 30 en fin de partie |
| Premier point de prestige | à la fin de la **run 2** |
| Arbre utile (107 points) | complété vers **85 à 90 %** de la campagne |
| Interception | en fin de partie (jauge de ~330 s, Blindage max), `PRX_05` niveau 1 vide **50 à 60 %** de la jauge |

**Une campagne dense, pour le Steam Deck** : 10 à 12 heures sans grind infini, au rythme haché
d'une console portable.

**La cloche des durées.** Au début, des runs nerveuses de ~5 minutes pour accrocher le joueur. Au
milieu, le Blindage lui permet de survivre : il creuse l'économie et enchaîne des runs de 8 à 11
minutes pour s'offrir les nœuds les plus chers. En fin de partie, les TFlops brutes accélèrent
l'A.M.I. : la run retombe à ~5 min 30, un sprint sous haute tension.

**La run 1 est un tutoriel déguisé** : le joueur découvre l'A.M.I., panique, comprend l'Overclock
et sort à 50 %. La run 2 introduit l'optimisation et lui fait atteindre son premier point, donc la
méta-progression. Le seuil de 2 M de Datas peut baisser pour tenir ce rendez-vous.

**L'arbre se finit tard.** Le finir à mi-campagne est un défaut de pacing : `P_CORE_OVERRIDE`
(« Recompilation Forcée ») ne doit servir de puits qu'au dernier sprint vers la victoire. Si la
campagne produit ~220 points, comme l'estimait l'ancien modèle, sa génération doit être divisée
par deux — par le seuil du premier point ou par la croissance des boîtes (voir « Les points de
prestige »).

**L'Interception est validée.** Le critère tient avec les valeurs du lot 4 : `PRX_05` efface 10 s,
×19,5 au maximum, soit 195 s, **59,6 %** d'une jauge de ~327 s ; `PRX_07` (19 s) la vide entière, ce
qui oblige à racheter des Proxies chers pour une purge totale. Les +1,0 et +1,5 par rang ne sont
plus provisoires.

### Le joueur de référence

Un **joueur actif moyen** : il utilise l'Overclock pour se sortir d'un mauvais pas, ou pour forcer
un palier juste avant la sortie, et sort majoritairement à **75 %**. Un équilibrage qui exigerait
un jeu optimal — Overclocks au cadre près, sorties systématiques à 90 % — serait brutal et
frustrant pour la majorité.

### Les leviers autorisés

Pour entrer dans les cibles, ceux-là et pas d'autres :

- le seuil du premier point, `_prestigeFirstThresholdDatas` ;
- la croissance des boîtes, `_prestigeThresholdGrowth` (1,12) — **déverrouillée** par le GD ;
- les coûts et rendements de base des trois premiers Scripts et du premier Hardware, **pour
  lisser la run 1 seulement** ;
- les constantes globales de `BalancingConfig` : `_amiSpeedExponent`, et `_proxyWipeScale`, créé
  pour l'occasion (échelle commune du Wipe des Proxies, ×1 par défaut).

### La méthode

Un **harnais de simulation hors Play Mode**, qui déroule une campagne de cent runs en quelques
secondes (choix du GD). Il fait tourner les vrais systèmes du jeu, pas une réplique, et il est
**calé sur des runs jouées** : un bot ne mesure que lui-même. Le journal de fin de run
(`journal-runs.csv`, à côté de la sauvegarde, en éditeur et en build de développement) consigne
les runs d'un humain ; le bot est corrigé jusqu'à jouer comme lui. Choix de Bertrand pour le bot :
le meilleur gain par Data à l'achat des générateurs, le Blindage d'abord dans l'arbre.

## Le Data Wiper — un sacrifice, pas une recharge (lot 2, 2026-10-14)

Le bouton d'urgence, débloqué par le nœud `P_EMERG` (« Protocole d'Urgence »). Effacer ses traces,
c'est envoyer un ver dans les serveurs fédéraux — et la puissance qu'il mobilise ne revient pas.

| | Règle |
|---|---|
| Effet | **−2 500 UT** sur la jauge (un quart), instantanément. Sous zéro, l'excédent est perdu |
| Coût | **+30 % de TFlops nettes sacrifiées**, jusqu'à la fin de la run |
| Plafond | **90 %**, strict : trois usages par run, puis le bouton est épuisé |
| Délai | **Aucun** : utilisable à tout moment, jauge vide comprise |
| Prérequis | Le nœud seul. Plus de palier de TFlops à posséder |
| Wipe de fin de run | Remet le sacrifice et les usages à zéro |

```
TFlops NETTES = BRUTES × P_DATAM × Noyau × (1 − sacrifice)
```

**Le sacrifice frappe les TFlops NETTES, jamais les brutes** (règle du GD, 2026-10-13). Les Scripts
ralentissent et rapportent moins — compression et rendement lisent les nettes —, le capteur du
brouillard s'aveugle, la victoire s'éloigne. La vitesse de l'A.M.I., qui lit les brutes, **ne
baisse pas**. Si elle baissait, le bouton deviendrait un soin surpuissant : le sacrifice ralentirait
la menace autant que la production, et la course n'en serait pas affectée. Le GD : *« le sacrifice
devient un vrai choix punitif. »*

⚖️ **Ce que coûte un usage, calculé :** sur un parc de fin de partie, 30 % de TFlops nettes en moins
ralentissent les cycles de ~8 % et leur rendement de ~5 % ; 90 % les ralentissent de ~44 % et ôtent
~29 % de rendement. Le premier usage achète un quart de jauge pour peu ; le troisième se paie cher.
Sans Hardware, le sacrifice ne coûte rien sur le moment — mais il frappera aussi tout Hardware
acheté ensuite pendant la run.

**Règle gardée, et dite au joueur** (Bertrand, 2026-10-15). À la passe de test, les trois usages
semblaient gratuits en début de run. La règle ne change pas, mais le jeu prévient : la ligne de
console du Data Wiper précise « Hardware acheté ensuite compris », et la description de
« Protocole d'Urgence » le dit en toutes lettres.

Le compteur d'usages est **sauvegardé** (`SaveData.EmergencyUsesInRun`), et le sacrifice en découle :
fermer le jeu ne rend pas la puissance engloutie. *Le modèle précédent — palier de TFlops, −25 % de
la Trace courante, contrecoup de 60 s, délai de 5 min — est archivé dans `historique.md`.*

## Les points de prestige — un CUMUL de campagne (tranché le 2026-09-13)

Les points ne se calculent plus sur l'argent de la seule run. Un compteur de **Datas exfiltrées
depuis le début de la campagne** ne retombe jamais à zéro, et le Nième point demande
`PremierPalier × Croissance^(N−1)` de ce cumul.

**Pourquoi.** L'ancienne conversion rendait 28 points dès la première run, et elle était pilotée
par la même valeur que le déblocage de l'exfiltration : raréfier les points rendait mécaniquement
la sortie inatteignable. Les deux ont d'abord été découplés en deux réglages,
`PrestigeFirstThresholdDatas` et `ExfiltrationUnlockDatas` ; le second a ensuite été abandonné
(voir « Exfiltration volontaire ») et retiré le 2026-10-14.

**Aucune run n'est perdue.** Une run qui n'atteint pas le palier suivant compte quand même : ce
qu'elle a rapporté reste au cumul. C'est un gain de ressenti considérable pour un coût nul.

⚠️ **Une SAISIE n'alimente pas le compteur, et contribution et points sont résolus au MÊME
instant, à l'exfiltration.** *(Première moitié remplacée le 2026-10-02 : une saisie verse
désormais 30 % — voir « Le modèle des BOÎTES » ci-dessous. La seconde moitié tient toujours.)* Ce n'est pas un détail d'implémentation, c'est ce qui ferme un
exploit : si les points tombaient pendant la run alors que la saisie annule la contribution, il
deviendrait rentable de se faire prendre exprès — on encaisserait le palier sans jamais faire
monter le seuil, et la run suivante le réencaisserait. Le multiplicateur du palier d'extraction
porte sur la **contribution**, jamais sur les points, pour la même raison.

### Le modèle des BOÎTES, et la saisie qui verse 30 % (2026-10-02)

*Consigné le 2026-10-13 : ces trois changements, livrés dans `a772e67`, n'avaient été écrits nulle
part dans ce document.*

**1. Chaque point est une boîte à remplir entièrement.** Le Nième point coûte toujours
`PremierPalier × Croissance^(N−1)`, mais détenir N points exige la **SOMME** des N boîtes :

```
Cumul(N) = PremierPalier × (Croissance^N − 1) / (Croissance − 1)
```

Jusqu'ici le cumul n'était comparé qu'au prix de la DERNIÈRE boîte : chaque point ne coûtait en
réalité que l'écart avec le précédent, soit 12 % du palier, et 2,5 M rendaient trois points au lieu
de deux. Le malentendu était consigné à l'identique dans le code, ce document et le harnais. Les
boîtes remplies restent acquises, et le reliquat travaille pour la suivante.

**2. Le premier palier passe de 5e6 à 2e6**, pour compenser une première boîte devenue pleine.

**3. Une saisie verse 30 % de sa contribution** (`SeizedContributionRatio`), au lieu de rien. Le
constat, sur une partie réelle du 2026-09-22 : **294 M de Datas générées sur deux morts, zéro
banquée, arbre vide.** Rien dans le jeu ne capitalisait une run perdue ; le joueur restait à
distance constante de son premier point, quelle que soit sa performance — « le seuil monte » et
« je n'avance jamais » sont la même sensation vue du fauteuil.

L'exploit que fermait la règle « une saisie ne verse rien » reste fermé : contribution et points
sont toujours résolus au **même instant**, à la fin de run, donc se faire prendre exprès n'encaisse
jamais un palier sans faire monter le seuil suivant. Et la saisie reste nettement moins rentable
qu'une sortie, qui verse 100 % **et** le bonus de palier. ⚠️ Le ratio doit rester nettement sous 1.

⚠️ **Rien de tout cela n'a été mesuré en campagne.** Estimation arithmétique, à cumul égal : les
boîtes retirent ~19 points, le seuil à 2e6 en rend ~8 — soit ~220 points par campagne au lieu des
~232 mesurés le 2026-09-16, pour un arbre passé de 147 à **239** rangs dans le même commit. Pour la
mesure, voir la session « chiffres » de la feuille de route, dans `etat-des-lieux.md`.

### Ce que doit rapporter une première run (tranché le 2026-09-16)

> ⚠️ **Remplacé le 2026-10-15** par la cible du GD : le premier point tombe à la fin de la
> **run 2**, la run 1 servant de tutoriel (« Les cibles de la campagne »).

> ⚠️ **Le tableau ci-dessous décrit l'ancien calcul** (seuil 5e6, dernière boîte seule). Avec les
> boîtes et le seuil à 2e6, le calcul donne pour les mêmes runs, bonus de palier non compris :
> **5** points pour 15,7 M, **3** pour 7,9 M, **2** pour 5,5 M, **1** pour 3 M. Calculé, pas
> mesuré.

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

**Déverrouillé par le GD le 2026-10-15** (session « chiffres ») : l'arbre utile doit se finir vers
85 à 90 % de la campagne, ce qui demande ~120 points et non ~220. L'avertissement reste valable —
trop forte, la croissance rend l'arbre infinissable — : toute retouche se mesure au harnais.

### Un coût plat par rang

Chaque rang d'un nœud coûte son `baseCost`, **quel que soit le rang** : 1 point presque partout,
**1, 3, 6 et 10** pour les quatre nœuds du Blindage, **2** par rang pour `P_DATAM` (lot 4). La
courbe `BaseCost × CostMultiplier^niveau` a été supprimée le 2026-09-12 — elle était recopiée dans
quatre fichiers indépendants, et l'arbre s'était déjà retrouvé 152 fois trop cher pour une erreur
sur un seul terme. `costMult`, resté inerte dans les données, en a été retiré au lot 5.

Les points dépensés — ce que lit le Noyau Compilé — se comptent sur la même règle
(`PrestigeConfigSO.GetTotalCostOfLevels`) : une courbe réintroduite devrait l'être aux deux endroits.

⚠️ **Un nœud passé à N rangs multiplie sa puissance par N** si son bonus par rang n'est pas divisé
d'autant. C'est arrivé à `P_STEALTH` le 2026-10-02, qui saturait au 4ᵉ rang.

*L'historique de l'aplatissement (783 puis 145 niveaux, 239 rangs le 2026-10-02, 140 nœuds au lot 1)
est archivé dans `historique.md`.*

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

**La branche Extraction multiplie ces trois bonus** (lot 4) : `bonus du palier × (1 + Σ)`, à +0,5
par rang sur quinze rangs, soit **×8,5** au maximum — +127,5 %, +255 % et +425 %. Les trois nœuds
agissent sur les trois paliers (GD, 2026-10-13). Au lot 1, la branche valait ×31 (+0,5, +1,5 et +4
par rang). Le multiplicateur porte sur la **contribution** au cumul, jamais sur les points.

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

Le code lit désormais la condition elle-même — `PendingCycles >= 1`. Le réglage
`ExfiltrationUnlockDatas`, qui n'était plus lu, a été retiré du `BalancingConfig` le 2026-10-14.

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
`Assets/GameData/Balancing/BalancingConfig.asset`, porte **toutes** les valeurs : la jauge de l'A.M.I.
(plafond, vitesse de base, facteur et exposant des TFlops), la compression et le rendement des
Scripts, l'Overclock (avance, pénalité en secondes, délai entre deux appuis), l'échelle du Wipe
des Proxies, le brouillard, la victoire, les points de prestige, les paliers d'extraction, le Noyau
Compilé et le Data Wiper.

Il est modifiable **en Play Mode** : les systèmes lisent les propriétés à l'usage plutôt que de
les recopier au démarrage, donc un changement dans l'inspecteur prend effet immédiatement. Deux
exceptions : le `ThreatManager` garde la vitesse de l'A.M.I. en cache, le `PrestigeManager` les
multiplicateurs du Noyau. L'asset émet un signal à chaque retouche (`Edited`) pour qu'ils les
recalculent. C'est le seul asset du projet qu'on édite à la
main sans qu'un générateur ne l'écrase.

Chaque champ porte un tooltip qui dit ce qu'il fait et dans quel sens il pousse. Les `[Range]` ne
sont pas décoratifs : ils empêchent de saisir une valeur qui casserait une formule.

## Lore

L'objectif ultime du joueur est de récupérer **une photo de chat sur une clé USB verrouillée**. Chaque wipe = déménagement dans un nouveau sous-sol avec un noyau IA amélioré, compilé à partir des tentatives ratées. Ton absurde assumé.

## Échelle de contenu

≈ 15 améliorations par onglet (45 générateurs) et **≈ 170 nœuds de prestige**. Cette dernière valeur rend le sujet performance et le rendu de l'arbre non négociables : pooling ou virtualisation seront nécessaires.

*Au 2026-10-14 : **60 générateurs** (25 Scripts, 20 Hardware, 15 Proxies) et **21 nœuds** de
prestige (les 20 nœuds majeurs et `P_CORE_OVERRIDE`) depuis le lot 4 de la refonte « Time Attack ».
La question du pooling et de la virtualisation de l'arbre a disparu avec les 120 nœuds ciblés.*

## Sauvegarde

- Autosave toutes les **5 minutes**, plus fin de run et achat de prestige.
- Save d'urgence à la fermeture (croix, Alt+F4). Ne couvre ni un kill process ni un crash.
- **Aucune progression hors-ligne, aucun rattrapage au retour.**
- **`runInBackground` est à `true`** (tranché le 2026-08-26). « Hors-ligne » veut dire *jeu fermé*, pas *fenêtre en arrière-plan* : le joueur peut laisser tourner Ares pendant qu'il fait autre chose, et l'A.M.I. continue de le traquer. Le mettre à `false` offrirait une planque parfaite et gratuite — il suffirait d'alt-tab pour geler la Trace.
  *Note pour les tests : ce réglage ne s'applique qu'au build. Dans l'éditeur, le player loop reste figé tant que la fenêtre Unity n'a pas le focus (`Time.frameCount` ne bouge pas). Pour vérifier un `ITickable` sans focus, appeler `Tick()` à la main plutôt que d'attendre des frames.*
- Fichier en JSON lisible et éditable à la main — choix assumé jusqu'à la sortie Steam.
- **Aucune migration de format pendant le développement** (Bertrand, 2026-10-14) : une sauvegarde
  d'une autre version ouvre une partie neuve. La règle s'inverse au premier build public.
- **Une partie fermée sur l'écran de fin le rouvre** (lot 2, `SaveData` v12) : bilan, arbre et
  fenêtre d'achat du prestige compris. Avant, elle relançait une run neuve, et le joueur perdait sa
  seule occasion de dépenser ses points avant la run suivante (session du 2026-10-14).
- **Le menu principal et le menu de pause figent la simulation** (tranché le 2026-10-01). C'est une planque assumée, la seule : on ne punit pas le joueur qui règle ses options. Elle reste coûteuse en pratique, puisque rien ne progresse pendant qu'il est dessus — ce n'est pas une planque *productive*, contrairement à ce qu'aurait été l'alt-tab. L'arbre de prestige, lui, **ne** met **pas** en pause.

## Steam

Pas de page Steam ni d'AppId pour l'instant. La sauvegarde **locale fait autorité** ; le cloud viendra plus tard et servira de miroir, départagé par horodatage.

### Cible de sortie (tranchée le 2026-08-30)

**Windows 64 bits uniquement, avec l'objectif du badge Steam Deck Verified.** Pas de build Linux
natif — le Deck fait tourner la version Windows sous Proton —, pas de macOS : la notarisation Apple
a été jugée non rentable pour une première sortie.

Déjà appliqué dans le projet :

- API graphique épinglée sur **Direct3D 11 seul** (Auto désactivé) : le chemin le plus mature sous
  Proton/DXVK.
- Lancement en fenêtré redimensionnable 1280×720, instance unique forcée.
- IL2CPP, stripping minimal, validé par une vraie build avec aller-retour de sauvegarde.
- **Cadence bridée** (`FrameRateGovernor`) : 60 FPS par défaut, réglable ; **10 FPS hors focus**,
  sans jamais mettre la simulation en pause. Un incrémental qui redessine une interface quasi
  statique à 144 Hz fait tourner les ventilateurs et divise l'autonomie du Deck.

### Ce qui manque pour le Deck

- **La navigation à la manette.** Le jeu est souris et clavier, et la sélection d'interface est
  vidée à chaque frame (cf. « La BARRE ESPACE »). Inquiétude du GD (2026-10-02) : l'Overclock,
  martelé en début de partie, serait pénible à aller chercher au pad.
- **Une configuration Steam Input par défaut**, déclarée côté Steamworks. Sans elle, le Deck est
  injouable manette en main — motif de refus du badge.
- **La lisibilité en 1280×800.** Interface pensée en 1920×1080 : le facteur d'échelle tombe à
  ≈ 0,72 sur Deck, et un texte de 20 px à ~14 px sur un écran de 7 pouces.

### Avant le premier build public

- **Le nom du jeu.** `productName` vaut aujourd'hui `HackTheKey` ; le dossier de sauvegarde en
  dérive. Le changer après une démo ferait perdre leurs sauvegardes aux joueurs.
- **La déclaration de contenu créé avec une IA** (sons synthétisés, traductions prévues par IA).
- **Les crédits** : licence OFL des polices, et les attributions que demanderaient de futurs sons.

Détail et suivi : `backlog.md` § 1.