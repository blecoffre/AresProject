# État des lieux — équilibrage

> Document de **reprise**. Il dit où on en est, ce qui reste à faire et par quoi commencer.
> Les décisions de design, elles, vivent dans `game-design.md` — qui fait foi.
>
> Dernière mise à jour : **2026-09-16**

---

## En une phrase

Le jeu se termine, tout son contenu est accessible, et les runs ne s'effondrent plus. Il reste
**une boucle de rétroaction instable**, une mécanique qui ne se déclenche jamais, et aucun écran de
victoire.

---

## Ce qui est acquis

| | Valeur |
|---|---|
| Contenu | 25 Scripts · 20 Hardware · 15 Proxies |
| Arbre de prestige | 146 nœuds, 1 point chacun |
| Condition de victoire | **1e12 TFlops** |
| Pic atteint en campagne | 6,4e11 — soit ~64 % de la cible |
| Première run | 10,8 min, palier 90 % à 8,9 min |
| Durée des runs en campagne | stable, ~3,5 à 6,3 min selon la posture |

Les quatre piliers ont chacun un rôle mesurable et distinct :

- **Scripts** — ce que tu gagnes
- **Hardware** — la vitesse et la force avec lesquelles tu le gagnes *(TFlops → Scripts uniquement)*
- **Furtivité** — combien de temps tu tiens. Seul levier de durée depuis le plafond dynamique :
  23 % de dissipation donnent des runs de 3,4 min, 61 % en donnent 7,0
- **Blindage** *(prestige)* — multiplie le plafond

---

## Les trois chantiers restants

### 1. La spirale de la posture agressive ⚠️ *le plus important*

Un joueur qui vise le dernier palier d'extraction meurt, donc ne banque rien, donc n'achète aucun
prestige, donc meurt encore. **Aucun réglage n'en sort** : en déplaçant une seule constante, la même
posture bascule de 1 saisie à 15, et de « contenu complet » à « SCR_16, arbre à 2,7 % ». La marge
avant la saisie, elle, reste identique dans les deux cas — ce n'est donc pas elle qui décide.

**Deux pistes, à trancher par le GD :**

- **Adoucir la saisie.** Elle ne verse aujourd'hui *rien* au cumul de campagne. Lui faire verser 20
  à 30 % de la contribution laisserait le joueur malchanceux progresser lentement au lieu de
  stagner. Une ligne dans `GameSessionManager.ResolveRunEnd` et un réglage. *(recommandé)*
- **Verrouiller le dernier palier.** Il est proposé dès la première run, donc il invite. S'il doit
  être réservé aux joueurs équipés, il devrait se débloquer plus tard.

### 2. Le Zéro-Day Exploit ne se déclenche plus

Zéro fois sur 264 runs. Il a marché, puis s'est éteint **trois fois pour trois raisons
différentes** : seuil de charge trop haut, capacité en secondes plus longue qu'une run, et
maintenant des runs raccourcies par l'extension des paliers.

La charge exige `GhostCacheCapacitySeconds` (90 s) de posture défensive tenue au-dessus de
`MaxTraceReduction × GhostCacheReductionThreshold` (59,5 %). Vérifier lequel des deux bloque avant
de toucher à quoi que ce soit.

### 3. Aucun écran de victoire

Le seuil de 1e12 TFlops n'est câblé **que dans le harnais de mesure**. Rien ne se passe dans le jeu
quand le joueur l'atteint. C'est une fonctionnalité à écrire, pas un réglage.

---

## Ce qu'il faut savoir avant de toucher à l'équilibrage

**Sept défauts ont été trouvés en deux jours, et pas un seul ne venait d'un réglage.** Tous venaient
d'une mécanique que le simulateur n'exerçait pas, ou d'une constante qui ne suivait pas le reste.
Avant de tourner un curseur, vérifier que la chose qu'on mesure est bien jouée.

⚠️ **Le simulateur est une borne basse, jamais une prédiction.** Il clique l'Overclock depuis le
2026-09-13, mais son modèle est calé sur **une seule run réelle** et reste 1,8× en dessous. Tout
écart inférieur à un facteur 2 est du bruit.

⚠️ **Rien n'a jamais été vérifié en Play Mode.** Toutes les mesures viennent de la simulation. Les
deux sessions de jeu du GD ont chacune trouvé un problème que les mesures ne voyaient pas — la run
de 92 minutes sans danger, et le rythme trop précipité.

⚠️ **Les fichiers générés sont des SORTIES.** `04_SpecificUpgrades.json` et tous les `.asset` de
`GameData/` sont écrasés par leurs générateurs. Régler une valeur dedans ne survit pas. Les sources
sont `GameData/Editor/**` et `PrestigeSpecificNodesGenerator.cs`.

⚠️ **Les valeurs de l'enum `PrestigeBonusType` s'appendent en FIN.** L'index est sérialisé dans les
`.asset` : une insertion au milieu redéfinit silencieusement des nœuds existants. Déjà arrivé.

---

## Outillage de mesure

Tout est sous `Tools/Core/Équilibrage/` :

| Entrée | Ce qu'elle répond |
|---|---|
| **Comparer les paliers d'extraction** | la mesure principale — 4 postures, durée, contenu, saisies, dissipation, mécaniques utilisées |
| **Première run — temps avant le Game Over** | le plafond dur d'une run, et la marge après chaque palier |
| **Première run — où se trouve le temps mort ?** | densité d'achats minute par minute |
| **Et si le joueur évitait une famille de nœuds ?** | ce que coûte l'évitement d'une famille |
| **Test rapide (une run)** | un relevé complet sur une seule run |

Et les générateurs, sous `Tools/Core/` : catalogue d'upgrades, nœuds ciblés, arbre depuis JSON.
**Les trois doivent être relancés dans cet ordre** après toute modification de `GameData/Editor/`.

Une campagne est coupée à **45 secondes de calcul** et s'annonce alors « MESURE TRONQUÉE ». Ce
n'est pas « jamais fini » : c'est une mesure incomplète, et les confondre mène à une fausse
conclusion.

Scripts Python versionnés sous `tools/balance/` : `recost.py` aligne la croissance des coûts sur
celle des rendements, `extend_ladder.py` étend une échelle en conservant ses deux extrémités.

---

## Réglages en vigueur

```
_dynamicTraceCap            1          le plafond suit la puissance
_dynamicCapReferenceYield   3e+05      baisser = runs plus longues
_victoryTFlops              1e+12      la clé USB
_passiveTraceFillMinutes    6          la traque de l'A.M.I.
_maxTraceReduction          0.85       asymptote de la Furtivité
_ghostCacheCapacitySeconds  90         charge du Zéro-Day
_prestigeFirstThresholdDatas 1e+08     premier point de prestige
_prestigeThresholdGrowth    1.12       +12 % de cumul par point suivant
paliers d'extraction        50 % → +15 % · 75 % → +30 % · 90 % → +50 %
```

Le plafond dynamique est **basculable** : décocher `_dynamicTraceCap` restaure l'ancien
comportement sans toucher au code, pour comparer en jouant.

---

## Par quoi je reprendrais

1. **Une session de jeu.** Les deux précédentes ont chacune trouvé ce que la simulation ne voyait
   pas. Le réglage actuel n'a jamais été joué.
2. **La spirale** (chantier 1), en adoucissant la saisie.
3. **Le Zéro-Day** (chantier 2), en identifiant d'abord lequel des deux verrous bloque.
