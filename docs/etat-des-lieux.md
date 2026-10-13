# État des lieux

> Document de **reprise**, à lire en premier. Il dit où en est le projet, dans quel ordre on
> avance, et ce qu'il faut savoir avant de toucher à l'équilibrage.
>
> | Pour | Lire |
> |---|---|
> | les défauts et manques, un par un | `backlog.md` |
> | ce qui est livré mais jamais vu fonctionner, avec ses protocoles | `a-tester.md` |
> | les décisions de design — elles font foi | `game-design.md` |
> | ce qui a déjà été réglé, et comment | `historique.md` |
>
> Dernière mise à jour : **2026-10-13** — révision complète, contre `main` (`7b95a5a`).

---

## En une phrase

Le jeu se joue de bout en bout, du menu à la victoire, avec ses options, sa pause, son son et ses
palettes daltoniennes. Ce qui manque n'est plus de la mécanique de base : c'est la **refonte de
l'arbre de prestige** et ce qu'elle attend, l'**épreuve en jeu** de toute la fin de partie, et
**tout le volet Steam**.

---

## Où en est chaque domaine

| Domaine | État | Vu fonctionner en jeu ? |
|---|---|---|
| Boucle de base — 60 générateurs (25 Scripts, 20 Hardware, 15 Proxies), cycles, Overclock, achat multiple | Livré | Oui |
| Trace — saturation des Proxies, traque passive, plafond dynamique | Livré | Oui, sessions du GD jusqu'au 2026-09-22 *(réglages modifiés depuis)* |
| Capteur de Trace — le brouillard se rachète en Hardware | Livré le 2026-09-16 | **Non** — mécanique de ressenti, la simulation ne peut pas la juger |
| Paliers d'extraction, leur affichage sur la jauge, verrou du 90 % | Livré le 2026-09-16 | **Non** |
| Points de prestige — cumul de campagne, modèle des boîtes, saisie à 30 % | Livré le 2026-10-02 | **Non** |
| Arbre de prestige — 146 nœuds, 239 rangs | Fonctionnel, **à refondre** (annoncé par le GD le 2026-09-25) | Interface oui (août) ; données actuelles non |
| Zéro-Day Exploit / Ghost Cache | **Ne se déclenche plus** ; refonte tranchée, pas commencée | — |
| Data Wiper | Livré | Oui (août), valeurs provisoires |
| Victoire — 1e12 TFlops | Livrée le 2026-09-16, **seuil provisoire à 5e11** | **Non** |
| Sauvegarde — `SaveData` v10, écriture atomique, autosave, fermeture | Livrée | Oui (MCP) ; fermeture par Alt+F4 : dans un build seulement |
| Menu principal, options en trois onglets, pause, langue à chaud | Livré le 2026-10-02 | Oui, par clics simulés ; touches et plein écran jamais à la main |
| Daltonisme — quatre palettes | Livré le 2026-10-02 | Oui (outil de contrôle + Play Mode) |
| Audio — musiques, clics, événements | Livré le 2026-10-12, jugé suffisant pour une démo | Oui (GD) |
| Traductions EN / ES | Reportées à la fin du contenu | — |
| Steam — AppID, cloud, Steam Input — et Steam Deck — manette, 1280×800 | **Rien** | — |
| Tests automatisés | **Aucun** | — |

---

## Feuille de route

Ordre proposé le 2026-10-13, **à valider par le GD**. Chaque lot se termine par son protocole de
test, et chacun se mène sur sa propre branche (`chantier/…`).

| # | Lot | Contenu | Dépend de | Statut |
|---|---|---|---|---|
| 0 | **Remise à plat du suivi** | Ce document, `backlog.md`, `a-tester.md`, `historique.md` ; corrections du GDD et des docs techniques | — | ✅ 2026-10-13 |
| 1 | **Session de jeu « ressenti et bugs »** | `a-tester.md` § 1 et 2 : fin de partie, capteur, paliers, victoire, touches | — | à faire — **le GD** |
| 2 | **Socle de la refonte de l'arbre** | Prérequis « X points dépensés » · suppression de l'automatisation au niveau 10 · verrou de la barre d'espace · Zéro-Day → Baroud d'Honneur (`SaveData` v11) · données `P_STEALTH` et `P_HARDEN` | 1, souhaitable | à faire |
| 3 | **Refonte de l'arbre, puis rééquilibrage** | Design du GD sur le socle · mesure au harnais · session de jeu « chiffres » · victoire remise à 1e12 | 2 | à faire |
| 4 | **Dette technique groupée** | `backlog.md` § 4 à 7 | — (à intercaler entre deux gros lots) | à faire |
| 5 | **Steam et Steam Deck** | Nom du jeu · AppID · cloud · navigation manette · Steam Input · lisibilité en 1280×800 · crédits et licences · déclaration IA | L'AppID pour une partie ; manette et 1280×800 peuvent démarrer avant | à faire |
| 6 | **Traductions EN / ES** | Une fois le texte stabilisé | 3 | à faire |

**Pourquoi la session de jeu avant le socle.** Les deux sessions précédentes ont chacune trouvé ce
que la simulation ne voyait pas (la run de 92 minutes sans danger, puis le rythme trop précipité).
Le lot 2 bâtit sur la fin de partie actuelle : autant savoir d'abord si elle tient debout.

**Pourquoi tout le socle en un lot.** Il regroupe tout ce qui change les mécaniques du prestige
**et** le format de sauvegarde. Le faire en une passe, c'est une seule migration (v10 → v11), et un
GD qui dessine l'arbre sur des mécaniques définitives plutôt que contre un mur.

**Option, avant le lot 2 :** un `.asmdef` et quelques tests EditMode sur les formules
(`UpgradeModel`, le modèle des boîtes, `CurrencyFormatter`). La refonte va remuer ces formules, et
le défaut corrigé dans `a772e67` — deux points et demi comptés pour trois — est exactement ce qu'un
test attrape.

⚠️ **Inconnue de planning : y a-t-il une échéance** (démo publique, Steam Next Fest, ouverture de
la page) ? Si oui, le lot 5 remonte — au moins le nom du jeu et la manette — et une démo pourrait
sortir avant la refonte de l'arbre.

---

## Équilibrage

### Ce qui est acquis

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

### Les chantiers ouverts

#### 1. Tout remesurer après le 2 octobre

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

#### 2. Le capteur — à reconfirmer

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

#### 3. Le Zéro-Day — à refondre

Zéro déclenchement sur 264 runs simulées. Il a marché, puis s'est éteint **trois fois pour trois
raisons différentes** : seuil de charge trop haut, capacité plus longue qu'une run, puis runs
raccourcies. C'est la **charge** qui est cassée, pas l'effet : l'Overdrive tient ses 30 s.

Le GD a tranché le 2026-09-16 : **Baroud d'Honneur** — un burst de 3 à 5 s, ×50 sur le rendement,
malus ×10 conservé, et N usages par run à la place de la charge de 90 s. Spécification dans le GDD,
§ Ghost Cache ; une question reste ouverte sur N (`backlog.md` § 9). Lot 2.

#### 4. La victoire — seuil provisoire, écran jamais vu

`VictorySystem` observe les TFlops et déclenche une fois, au franchissement ; la partie continue
après. `HasWon` est sauvegardé (v10) et restauré **avant** les totaux. L'écran `VictoryPanel` est
monté et câblé. Vérifié hors Play Mode : franchissement, non-redéclenchement, reprise d'une
sauvegarde gagnée, migration v9 → v10. **Le chemin complet n'a jamais été joué** (`a-tester.md`
1.12 à 1.14). Seuil à 5e11 pour tester ; à remonter à 1e12 au lot 3.

---

## Ce qu'il faut savoir avant de toucher à l'équilibrage

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

## Outillage de mesure

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

## Réglages en vigueur

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
