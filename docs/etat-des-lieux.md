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
> Dernière mise à jour : **2026-10-14** — lot 1 joué, lots 2 et 3 livrés, branche
> `chantier/refonte-time-attack`.

---

## En une phrase

Le gameplay est en **refonte « Time Attack »** depuis le 2026-10-13 : la Trace dynamique laisse la
place à un compte à rebours fixe, les Proxies deviennent des consommables de survie, l'arbre passe
à une vingtaine de nœuds. Le lot 1 (jauge de l'A.M.I., Overclock, Proxies) est livré et **joué le
2026-10-14** — la mécanique tient, mais l'ancien brouillard rend la jauge illisible ; les lots 2 à 5
restent à faire. Le menu, les options, la pause, le son et la
sauvegarde ne bougent pas.

---

## Où en est chaque domaine

| Domaine | État | Vu fonctionner en jeu ? |
|---|---|---|
| Boucle de base — 60 générateurs (25 Scripts, 20 Hardware, 15 Proxies), cycles, achat multiple | Livré | Oui |
| **Jauge de l'A.M.I.** — 10 000 UT fixes, vitesse sur les TFlops brutes, Blindage | **Refonte lot 1, livré le 2026-10-13** | Oui (2026-10-14) ; lisibilité à revoir avec le brouillard du lot 3 |
| **Proxies** — Wipe en secondes de survie à l'achat, Interception | **Refonte lot 1** | Oui (2026-10-14) ; valeurs de base du GD du 2026-10-13 |
| **Overclock** — avance + pénalité de 5 s de survie, rien sans cycle, 0,1 s entre deux appuis | **Refonte lot 1** | Oui (2026-10-14) |
| **Brouillard** — relevé en UT (800 → 200), borne sur la vitesse de l'A.M.I., relevé sauvegardé | **Refonte lot 3, livré le 2026-10-14** | **Non** |
| Paliers d'extraction, leur affichage, verrou du 90 % | Livré le 2026-09-16 ; branche Extraction refaite au lot 4 (×8,5 au max) | Oui (2026-10-14) ; nouvelles valeurs **non** |
| Points de prestige — cumul de campagne, modèle des boîtes, saisie à 30 % | Livré le 2026-10-02, inchangé par la refonte | Oui (2026-10-14) |
| **Arbre de prestige** — 21 nœuds en éventail, Noyau Compilé, `P_CORE_OVERRIDE` infini | **Refonte lot 4, livré le 2026-10-14** | Interface oui (août) ; **nouvel arbre non** |
| Zéro-Day Exploit / Ghost Cache | **Retirés** le 2026-10-13 | — |
| **Data Wiper** — −2 500 UT, sacrifice de 30 % des TFlops nettes par usage, trois usages | **Refonte lot 2, livré le 2026-10-14** | **Non** |
| Victoire — 1e12 TFlops nettes | Livrée le 2026-09-16, **seuil provisoire à 5e11** | Séquence oui ; écran jamais vu (objet éteint dans la scène, corrigé le 2026-10-14, à rejouer) |
| Sauvegarde — `SaveData` **v13**, écriture atomique, autosave, fermeture, écran de fin rouvert, relevé conservé | Livrée ; v11 le 2026-10-13, v12 et v13 le 2026-10-14 | v10 oui ; **migrations v11 à v13 non** |
| Menu principal, options en trois onglets, pause, langue à chaud | Livré le 2026-10-02 | Oui — touches et build vus le 2026-10-13 |
| Daltonisme — quatre palettes | Livré le 2026-10-02 | Oui (outil de contrôle + Play Mode) |
| Audio — musiques, clics, événements | Livré le 2026-10-12 | Oui (GD) |
| Traductions EN / ES | Reportées à la fin du contenu | — |
| Steam et Steam Deck | **Rien** | — |
| Tests automatisés, outil de mesure | **Aucun** — le harnais d'équilibrage a été supprimé avec l'ancien modèle | — |

---

## Feuille de route

Chaque lot se termine par son protocole de test (`a-tester.md`), sur la branche
`chantier/refonte-time-attack`. Bertrand fusionne dans `main`.

| # | Lot | Contenu | Statut |
|---|---|---|---|
| R1 | **Jauge, Overclock, Proxies** | Jauge de l'A.M.I. à 10 000 UT, TFlops brutes / nettes, Proxies en Wipe, Overclock pénalisé, branches Blindage / Interception / Overclock, retrait du Ghost Cache, du Zéro-Day, de `P_STEALTH`, du réveil et de la barre d'espace, `SaveData` v11, suppression du harnais | ✅ livré le 2026-10-13, joué le 2026-10-14 ; restent la migration v11 et la lisibilité (`a-tester.md` § 0) |
| R2 | **Data Wiper** | Sans délai, −2 500 UT, sacrifice de +30 % des TFlops nettes par usage jusqu'à 90 % (trois usages), permanent pour la run ; la vitesse de l'A.M.I. lit les TFlops brutes, donc ne ralentit pas. **Et la sauvegarde de l'écran de fin de run**, dans le même changement de format (v12) | ✅ livré le 2026-10-14, **à jouer** (`a-tester.md` § 0 bis) |
| R3 | **Brouillard** | Relevé tous les 800 UT remplis (200 en fin de partie, via les TFlops nettes) ; aiguille + borne pessimiste `relevé + V_AMI × âge × pessimisme` ; paliers et gain du bouton sur l'aiguille ; état du relevé sauvegardé (contre la triche du rechargement) | ✅ livré le 2026-10-14, **à jouer** (`a-tester.md` § 0 ter) |
| R4 | **Arbre** | Retrait des 120 nœuds ciblés et de leur générateur ; Noyau Compilé (coût × 0,99^points, plancher 0,05 ; rendement + 1 % par point, Datas et TFlops nettes) affiché dans les panneaux ; `P_CORE_OVERRIDE` infini ; `P_REFAC` 10 rangs, `P_DATAM` à 2 points, Extraction (+0,5 par rang, trois paliers) ; `P_INTERCEPT_3` et `_4` rabotés à +1 et +1,5 ; chaînes « parent au max » ; éventail. **Pas de migration** : Bertrand repart d'une sauvegarde neuve | ✅ livré le 2026-10-14, **à jouer** (`a-tester.md` § 0 quater) |
| R5 | **Nettoyage** — *suivant* | Code mort restant (champs obsolètes des générateurs, `ExfiltrationUnlockDatas`, clés orphelines, panneau de fin mort), commentaires périmés, doc, dossier partagé du GD | à faire |

**Bertrand joue l'ensemble des lots d'un bloc**, une fois le lot 5 livré (décidé le 2026-10-14) :
`a-tester.md` § 0 à § 0 quater forme le protocole complet.

Après la refonte, la feuille de route d'avant reprend : **session de jeu « chiffres »** (une
campagne complète, victoire remise à 1e12), **dette technique groupée** (`backlog.md` § 4 à 7),
**Steam et Steam Deck**, **traductions**.

⚠️ **Inconnue de planning : y a-t-il une échéance** (démo publique, Steam Next Fest, ouverture de
la page) ? Si oui, le lot Steam remonte — au moins le nom du jeu et la manette.

---

## Équilibrage

**Toutes les mesures de campagne d'avant le 2026-10-13 décrivent l'ancien modèle** (Trace
dynamique, saturation des Proxies, plafond suivant la puissance). Elles sont archivées dans
`historique.md` et ne disent plus rien du jeu. Le nouveau modèle n'a été que **calculé** :

| | Valeur |
|---|---|
| Contenu | 25 Scripts · 20 Hardware · 15 Proxies |
| Run sans Hardware ni Blindage | 5 min (33 UT/s) |
| Run de victoire, Blindage max | ⚖️ ~5 min 30 — la cible de pacing du GD |
| Overclock | rentable au-delà de ~13 points dans sa branche (avance > 5 s) |
| Arbre utile (lot 4) | **107 points** : Blindage 20, Interception 20, Overclock 20, Économie 30, Extraction 16, Data Wiper 1 |
| Noyau Compilé | coûts −66 % et rendement ×2,07 à 107 points ; plancher −95 % vers 299 points |
| Interception max | ×19,5 — valeurs de départ, à régler en jouant |
| Condition de victoire | **1e12 TFlops nettes** — l'asset porte **5e11**, à titre provisoire |
| Premier point de prestige | **2e6** de cumul, boîte suivante +12 % (modèle des boîtes) |
| Saisie | verse **30 %** de sa contribution au cumul |

Rien n'a été joué. La première chose à faire après la refonte est une campagne réelle.

### Ce qu'il faut savoir avant de toucher à l'équilibrage

⚠️ **Le réglage en vigueur n'a jamais été joué.** Chaque session du GD a trouvé un problème que
le calcul ne voyait pas. Il n'existe plus d'outil de mesure : toute grandeur exprimée en Datas
absolues se pose sur une partie **jouée**.

⚠️ **Un changement de réglage se consigne dans le GDD le jour même.** `BalancingConfig.asset` fait
foi sur les valeurs, le GDD sur les intentions ; quand les deux divergent, c'est un bug de l'un ou
de l'autre.

⚠️ **Les fichiers générés sont des SORTIES.** Tous les `.asset` de `GameData/` (sauf le
`BalancingConfig`) sont écrasés par leurs générateurs. Les sources sont les JSON de
`GameData/Editor/**`.

⚠️ **Les valeurs de l'enum `PrestigeBonusType` s'appendent en FIN.** L'index est sérialisé dans les
`.asset`. En retirer une n'est permis qu'en relançant les trois générateurs dans la foulée.

⚠️ **Un nœud à plusieurs rangs multiplie son bonus par son niveau.** Passer un nœud de 1 à N rangs
sans diviser son bonus multiplie sa puissance par N — c'est ce qui avait saturé `P_STEALTH`.

---

## Outillage

Le harnais d'équilibrage Unity (`Tools/Core/Équilibrage/`) et le simulateur Python
(`tools/balance/`) ont été **supprimés** le 2026-10-13 : ils simulaient l'ancien modèle de Trace
de bout en bout. Un petit harnais sera refait sur le nouveau modèle si le GD a besoin de mesures ;
la `SimulationClock` (`ITimeSource`) reste le point d'entrée prévu pour dérouler une campagne hors
Play Mode.

**Les générateurs de données**, sous `Tools/Core/`, à relancer après toute modification de
`GameData/Editor/` — chacun pour ses propres JSON, ils ne dépendent plus l'un de l'autre :

1. **Générer Catalogue Upgrades depuis JSON** — `UpgradeData/*.json`
2. **Générer l'Arbre depuis JSON (Dossier)** — `PrestigeData/*.json`. Lire ses messages : il liste
   les nœuds sans plafond, les prérequis au-delà du premier rang, et les `.asset` orphelins qu'il
   supprime

---

## Réglages en vigueur

Relevés dans `BalancingConfig.asset` le 2026-10-13, après le lot 1.

```
_amiTraceCap                  10000    plafond FIXE de la jauge, en UT
_amiBaseSpeed                 33       UT/s à zéro TFlop, sans Blindage
_amiTFlopsFactor              0.05     poids des TFlops BRUTES dans la vitesse
_amiSpeedExponent             0.1      exposant de la menace (Scripts : 0,25)
_tflopsTimeCompression        0.05     compression des cycles par les TFlops NETTES
_tflopsCompressionExponent    0.25
_tflopsYieldExponent          0.15     boost de rendement des Scripts
_overclockWarpSeconds         0.5      avance de base d'un Overclock
_overclockPenaltySeconds      5        coût d'un Overclock, en secondes de survie
_overclockMinIntervalSeconds  0.1      délai minimal entre deux appuis
_victoryTFlops                5e+11    ⚠️ PROVISOIRE — la cible est 1e12
_traceReadoutMaxIntervalUT    800      UT entre deux relevés AU DÉPART (~24 s)
_traceReadoutMinIntervalUT    200      plancher, atteint vers 1e9 TFlops nettes
_traceReadoutPessimism        3        avance de la borne sur la vitesse, au départ
_traceReadoutSensorPower      0.5      ce que les TFlops nettes achètent en visibilité
_prestigeFirstThresholdDatas  2e+06    première boîte
_prestigeThresholdGrowth      1.12     +12 % par boîte suivante
_seizedContributionRatio      0.3      part de la contribution versée par une saisie
_emergencyTraceRemoval        2500     Data Wiper : UT retirées par usage
_emergencySacrificeStep       0.3      part des TFlops NETTES sacrifiée par usage, jusqu'au wipe
_emergencyMaxSacrifice        0.9      plafond strict : trois usages
paliers d'extraction          50 % → +15 % · 75 % → +30 % · 90 % → +50 % (sous P_EXTRACT_HIGHRISK)
```
