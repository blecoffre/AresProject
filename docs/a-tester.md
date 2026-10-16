# À tester — livré, mais jamais vu fonctionner

> Ce qui a été construit et n'a **pas encore été observé** dans les conditions où le joueur le
> rencontrera. Pour chaque ligne : quoi faire, et ce qu'on doit voir.
>
> **Une ligne quitte ce document** quand on l'a vue fonctionner (elle part dans `historique.md`,
> datée, avec la façon dont elle a été vérifiée) ou échouer (elle devient une entrée de
> `backlog.md`).
>
> Mis à jour : **2026-10-16**.

---

## Avant de commencer

- **Lancer le Play Mode depuis `Assets/Data/Scenes/RootScene.unity`**, jamais depuis la GameScene
  (cf. `backlog.md`, Pièges).
- **Garder Unity au premier plan** : sans focus, le player loop de l'éditeur se fige, et rien ne
  bouge.
- **En éditeur, le bouton d'exfiltration reste cliquable même sans point.** C'est voulu, pour
  enchaîner des runs de test. Le libellé et la jauge affichés, eux, sont les vrais. Le verrou
  lui-même ne se vérifie que dans un build de production (§ 3).
- **`BalancingConfig.asset` se règle en Play Mode**, et l'effet est immédiat. Pour atteindre la
  victoire sans grinder : abaisser `_victoryTFlops` sous les TFlops du moment. **Remettre ensuite
  la valeur d'origine** (5e11, provisoire).
- **La sauvegarde est du JSON lisible**, dans
  `%userprofile%\AppData\LocalLow\DeathtreakCorp\HackTheKey\savegame.json`. En faire une copie
  avant de la retoucher. ⚠️ **Plus aucune migration** (lot 5 de la refonte) : une sauvegarde d'un
  autre format est remplacée par une partie neuve. Copier le fichier avant de changer de branche.
- **La refonte est entièrement jouée** (2026-10-15, `historique.md`) : plus rien en attente de ce
  côté. Prochaine étape, la session « chiffres » (§ 5).

---

## 0. Session « chiffres », lot C1 — les trois runs de calibrage (2026-10-15)

Compilé (Unity et `dotnet build`, zéro erreur, zéro warning), **jamais joué**. Ces runs servent à
caler le bot du harnais sur un joueur réel : **jouer naturellement**, comme le « joueur actif
moyen » du GD (Overclock pour se sortir d'un mauvais pas ou forcer un palier avant la sortie,
sortie vers 75 % — la run 1 peut sortir plus tôt, c'est le tutoriel). Chaque run d'une traite :
une fermeture en pleine run fausse ses compteurs.

**Mise en place.** Jeu fermé, dans `%userprofile%\AppData\LocalLow\DeathtreakCorp\HackTheKey\` :
supprimer `savegame.json` **et** `journal-runs.csv` s'il existe. Lancer depuis la RootScene.

| # | Quoi | Comment | Ce qu'on doit voir |
|---|---|---|---|
| 10.1 | **Le journal écrit** | Finir la run 1 (sortie ou saisie). | Console d'Unity : « [JOURNAL] Run 1 consignée dans … ». Le fichier `journal-runs.csv` apparaît, avec une ligne d'en-tête et une ligne de run. |
| 10.2 | **Trois runs** | Enchaîner les runs 2 et 3, en dépensant les points éventuels sur l'écran de fin. | Trois lignes de run, numérotées 1 à 3. Me transmettre le fichier, ou me dire qu'il est là : je le lis directement. |
| 10.3 | **L'échelle des Proxies** | En Play Mode, passer `_proxyWipeScale` de `BalancingConfig.asset` à 2, puis cliquer ×10 et ×1 (la carte ne se repeint qu'à un changement) et regarder une carte de Proxy. Remettre ensuite à 1. | La carte annonce le double de secondes (« Efface 5 s » pour le premier Proxy), et l'achat retire deux fois plus de jauge. Remis à 1, tout revient. |

## 0 bis. Session « chiffres », lot C2 — six nouveaux nœuds (2026-10-16)

Compilés (Unity et `dotnet build`, zéro erreur, zéro warning), arbre régénéré (27 nœuds), **jamais
joués**. La logique a été vérifiée dans l'éditeur, sur les vrais services montés dans un conteneur :
argent de départ 310, État d'Alerte ×1,5 à mi-jauge, 24,4 % de critiques sur 2 000 appuis (avance
×3, jauge intacte), Routine Pare-Feu (achat du Proxy visible le plus cher, un seul par passage,
réessai faute d'argent, Overclock à 99,9 % sans saisie).

⚠️ **Faire d'abord les trois runs du § 0**, sur la sauvegarde neuve, et garder une copie de
`journal-runs.csv`. Ensuite seulement, pour avoir des points : jeu fermé, dans `savegame.json`, passer
`CpuCycles` à 150 et ajouter à `PrestigeUpgrades` les chaînes préalables au maximum —
`{ "Id": "P_CLICK", "Level": 5 }`, de même `P_OVERCLOCK_POWER_1`, `P_OVERCLOCK_POWER_2`,
`P_EXTRACT_1`, `P_EXTRACT_2`, `P_EXTRACT_3`, `P_INTERCEPT_1` à `P_INTERCEPT_4`. Les nœuds s'achètent
sur l'écran de fin (« [PRÊT] WIPE SYSTÈME » reste cliquable en éditeur).

| # | Quoi | Comment | Ce qu'on doit voir |
|---|---|---|---|
| 11.1 | **L'arbre** | Ouvrir l'arbre, dézoomer. | 27 nœuds. Tout à gauche, une nouvelle colonne : « Capital d'Amorçage » puis « Financement Occulte ». Sous « Nuage de témoins » : « Routine Pare-Feu ». À droite de « Surtension III » : « Saisie Multiplexée » puis « Instabilité Zéro-Day ». En diagonale sous « Sortie chirurgicale » : « État d'Alerte ». Aucun lien ne traverse un nœud, aucune clé entre crochets. |
| 11.2 | **L'Espace verrouillé** | Run neuve sans « Saisie Multiplexée ». Un cycle en cours, appuyer deux fois sur Espace, puis cliquer « Overclock ». | Le premier appui écrit une seule ligne : « Barre d'Espace inactive : l'Overclock au clavier s'installe avec le nœud Saisie Multiplexée… ». Le second ne fait rien. Le bouton, lui, overclocke. |
| 11.3 | **Saisie Multiplexée** | L'acheter (5 points, « Surtension II » au max), nouvelle intrusion. | Espace overclocke comme le bouton. Marteler Espace et cliquer en même temps : la console n'aligne jamais plus de 10 lignes d'Overclock par seconde (délai partagé). |
| 11.4 | **Capital de Départ** | « Capital d'Amorçage » 5/5, nouvelle intrusion ; puis « Financement Occulte » 5/5, nouvelle intrusion. | L'en-tête démarre à 60 Datas, puis à 310. Acheter un Hardware d'entrée lance la traque aussitôt (c'est la règle). |
| 11.5 | **Coup critique** | « Instabilité Zéro-Day » 5/5. Des cycles en cours, une vingtaine d'Overclocks. | Environ un sur quatre : ligne verte « [CRITICAL BYPASS] Overclock ×3 : +22,5 s sur les cycles en cours. Jauge intacte. » (la mise en place donne +7,5 s à un appui normal), un son bref de trois bips montants, et aucune hausse de jauge. Les autres : la ligne habituelle avec « Jauge +N UT ». L'inspecteur du nœud : « +5 % par niveau (avance ×3, aucune pénalité) ». |
| 11.6 | **État d'Alerte** | « État d'Alerte » 10/10. Ne posséder que la « Campagne de Phishing Basique », cycle manuel. Lancer un cycle jauge presque vide, noter le gain ; recommencer jauge vers 80 %. | Jauge vide : le gain vaut le « Génère X Datas » de la carte. Vers 80 % : environ 1,8 fois plus. (La vraie jauge compte, pas l'aiguille : l'écart peut être de quelques points.) |
| 11.7 | **Routine Pare-Feu** | « Routine Pare-Feu » (15 points, « Nuage de témoins » au max). Posséder quelques Proxies, garder de l'argent, laisser monter la jauge au-delà de 95 %. | Console : « > Routine Pare-Feu : [Proxy] acheté d'office. Jauge −N UT. », le Proxy le plus cher qu'on puisse payer parmi ceux affichés ; la jauge redescend nettement. Un seul achat par passage. Overclocker à 96 % : la routine rachète un Proxy plutôt que de laisser saisir. |
| 11.8 | **Pare-Feu sans argent** | Même chose, solde vide au moment de dépasser 95 %. | Aucun achat ; dès qu'un cycle verse de quoi payer, la routine achète à la montée suivante. Sans argent jusqu'au bout : saisie normale. |

## 1. Fin de partie et économie

La victoire a été jouée le 2026-10-15 (`historique.md`). Rien en attente.

## 2. Contrôles et réglages

Les touches, la pause et l'audio ont été éprouvés à la main le 2026-10-13 (`historique.md`). La
taille d'interface au-dessus de 100 % attend que l'interface soit définitive (§ 5).

## 3. Uniquement dans un build

Tout ce bloc a été validé en build le 2026-10-13 (`historique.md`). Une ligne y reviendra si un
lot touche au démarrage, à la sauvegarde ou à l'affichage.

## 4. À confirmer à l'œil

Passé en revue le 2026-10-13 : les constats confirmés sont au backlog (§ 4), les autres sont
soldés dans `historique.md`. Rien en attente.

## 5. Plus tard — à tester quand le lot concerné sera livré

| Après | Quoi |
|---|---|
| Maintenant (refonte jouée le 2026-10-15) | Session de jeu « chiffres » sur une campagne : durée des runs, points par run, part de l'arbre à la victoire, valeurs de l'Interception. Victoire remise à 1e12. |
| L'interface définitive | **Taille d'interface au-dessus de 100 %** (ex-2.8) — *15/10 : « semble ok partout », vérification sommaire ; Bertrand la garde ouverte jusqu'à ce que toute l'interface soit définitive*. Options > Affichage > Taille d'interface à 110 %, puis 150 %. Parcourir **tous** les écrans : menu, chaque onglet des options (Audio surtout, le plus haut), pause, écran de jeu, arbre et inspecteur de nœud (« Extraction Haut Risque », la plus longue description), écran de fin, victoire. Rien ne doit se chevaucher ni sortir de l'écran (`backlog.md` § 4). |
| Steam | Lisibilité sur un écran 1280×800. Navigation complète à la manette, sans souris. Overlay Steam. Synchronisation cloud entre deux machines. |
| Traductions | **Langue à chaud** (ex-2.5) : basculer de langue en pleine partie, parcourir tous les écrans ; aucun texte ne doit rester dans l'ancienne langue, hors lignes déjà écrites dans la console. **Premier lancement** : l'écran de choix propose toutes les langues, celle du système présélectionnée. D'ici là, la technique du `zz.json` (chaque valeur de `fr.json` préfixée « ZZ· ») permet de traquer les textes en dur ; supprimer le fichier et sa `.meta` après. |
