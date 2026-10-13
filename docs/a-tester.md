# À tester — livré, mais jamais vu fonctionner

> Ce qui a été construit et n'a **pas encore été observé** dans les conditions où le joueur le
> rencontrera. Pour chaque ligne : quoi faire, et ce qu'on doit voir.
>
> **Une ligne quitte ce document** quand on l'a vue fonctionner (elle part dans `historique.md`,
> datée, avec la façon dont elle a été vérifiée) ou échouer (elle devient une entrée de
> `backlog.md`).
>
> Mis à jour : **2026-10-13**.

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
  avant de la retoucher, et avant tout test de migration. ⚠️ **Le premier lancement de la branche
  `chantier/refonte-time-attack` migre la sauvegarde en v11** : run effacée, arbre remis à zéro,
  points rendus. Copier le fichier AVANT, pour pouvoir revenir sur `main`.

---

## 0. Refonte « Time Attack », lot 1 — jauge de l'A.M.I., Overclock, Proxies

Livré le 2026-10-13, compilé (Unity, zéro erreur) et données régénérées, **jamais joué**. Chiffres
attendus à 0 TFlop et sans Blindage : vitesse de l'A.M.I. 33 UT/s, jauge pleine en ~5 min. Tout ce
qui s'affiche sur la jauge passe encore par le brouillard (relevé jusqu'à 60 s au départ) : pour
les montées, **la console fait foi** ; les baisses, elles, se voient tout de suite.

| # | Quoi | Comment | Ce qu'on doit voir |
|---|---|---|---|
| 0.1 | **Migration v10 → v11** | Copier la sauvegarde, lancer depuis RootScene, « Continuer ». | Run neuve : générateurs au niveau 0, 10 $, jauge à 0 %. Le compteur de Cycles de l'en-tête a grossi des points qui étaient dépensés. Arbre (P, ou « [ NOYAU IA — ARBRE ] ») vide hors la racine. Aucune erreur en console. |
| 0.2 | **Départ de la traque** | Run neuve. Attendre 30 s sans rien faire, puis acheter `SCR_01` et lancer son cycle. | Jauge à 0,0 % tant qu'aucune Data n'est volée. Elle démarre à la fin du premier cycle (ou au premier Hardware acheté). |
| 0.3 | **Vitesse de l'A.M.I.** | Sans Hardware ni Proxy, laisser tourner. | ~20 % par minute, saisie vers 5 min. La jauge monte au même rythme quoi que produisent les Scripts. |
| 0.4 | **Overclock à vide** | Avant de lancer le moindre cycle (ou entre deux cycles manuels), appuyer sur Espace, puis sur le bouton « Overclock ». | Console : « Overclock sans effet : aucun cycle en cours. » La jauge ne bouge pas. |
| 0.5 | **Overclock en cycle** | Un cycle de `SCR_01` en cours, appuyer une fois sur Espace. | La barre du Script bondit. Console : « Overclock : +0,5 s sur les cycles en cours. Jauge +165 UT. » (un peu plus avec du Hardware). Même chose au bouton. |
| 0.6 | **Overclock maintenu** | Tenir Espace enfoncé 3 s, cycle en cours. | **Une seule** ligne d'Overclock dans la console. |
| 0.7 | **Saisie par Overclock** | Cycles automatisés en cours (`SCR_01` niveau 10), marteler Espace. | Chaque appui retenu coûte 165 UT : la saisie tombe en une soixantaine d'appuis. Écran de fin de saisie habituel. |
| 0.8 | **Proxy : la jauge descend** | Jauge non nulle, acheter un `PRX_01`. | Carte du Proxy : « Efface 2,5 s de traque par achat ». Console : « > Brouillage : jauge −84 UT. » L'aiguille de la jauge recule aussitôt. |
| 0.9 | **Proxy acheté à vide** | Run neuve, avant toute Data volée, acheter un `PRX_01`. | Console : « > Brouillage : jauge −0 UT. » — l'excédent est perdu, c'est voulu. |
| 0.10 | **Proxy en lot** | Mode d'achat ×10 sur `PRX_01` (niveau 0 → 10), jauge bien remplie. | Wipe d'environ 10,3 fois celui du niveau 1 (neuf niveaux nus, le dixième au palier ×1,32), soit ~865 UT. |
| 0.11 | **L'arbre du lot 1** | Écran de fin de run (exfiltrer : le bouton « [PRÊT] WIPE SYSTÈME » reste cliquable en éditeur). Parcourir l'arbre. | Plus de Furtivité, de Zéro-Day, d'Injecteur Automatique ni d'Accélérateur Manuel. Data Wiper, Blindage, Interception et Extraction reliés à la racine. Blindage à 1, 3, 6 et 10 points. Lignes d'effet remplies : « > Temps avant saisie : +50 % (l'A.M.I. ralentit) », « > Brouillage de chaque Proxy acheté : +20 % par niveau », « > Avance de l'Overclock : +0,1 s par niveau ». Aucune clé entre crochets. |
| 0.12 | **Chaînes au maximum** | Acheter un rang de `P_INTERCEPT_1`. | `P_INTERCEPT_2` reste verrouillé jusqu'à `P_INTERCEPT_1` 5/5. Idem `P_HARDEN_2` derrière `P_HARDEN_1`, et les paliers d'Overclock. |
| 0.13 | **Blindage** | Acheter `P_HARDEN_1`, « [ NOUVELLE INTRUSION ] », Overclock en cycle. | Console : « Jauge +110 UT » au lieu de 165 (vitesse divisée par 1,5). La jauge met ~7 min 30 à se remplir. |
| 0.14 | **Interception et Overclock** | Acheter un rang de `P_INTERCEPT_1` et un de `P_CLICK`, nouvelle intrusion. | Carte de `PRX_01` : « Efface 3 s » ; Wipe ~101 UT. Overclock : « +0,6 s ». |
| 0.15 | **Le bouton du Ghost Cache a disparu** | Regarder la rangée d'actions de l'écran de jeu. | Plus de bouton Ghost Cache, et pas de trou dans la rangée. |
| 0.16 | **Pause** | En pleine run, Échap 30 s, puis refermer. | Pendant la pause, le compteur « relevé il y a N s » ne bouge pas : la jauge est figée. |


## 1. Fin de partie et économie — d'avant la refonte, jamais jouées

Tout ce bloc vient de `bf490c6` (2026-09-22), dont le message dit : « Rien de tout cela n'a été
éprouvé en Play Mode », et de `a772e67` (2026-10-02). C'est une session **« ressenti et bugs »** :
on regarde si ça marche et si ça se lit, pas si les chiffres sont bons. ⚠️ La refonte « Time
Attack » réécrit le brouillard (1.2 à 1.5, 1.10 : lot 3) et le Data Wiper (1.11 : lot 2) : ne pas
s'attarder sur leur ressenti avant ces lots. L'ex-1.1 (départ de la traque) est devenue 0.2.

| # | Quoi | Comment | Ce qu'on doit voir |
|---|---|---|---|
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
| 1.12 | **Victoire : séquence et écran** | Abaisser `_victoryTFlops` sous les TFlops courants (cf. « Avant de commencer »). | La console déroule la séquence (« Attaque du chiffrement… C'est un chat. »), puis l'écran « ACCÈS TOTAL » apparaît, textes résolus (aucune clé entre crochets), fond opaque qui capte les clics. La simulation **continue derrière** : la Trace monte. |
| 1.13 | **Victoire : fermeture** | Cliquer « Reprendre ». | L'écran se ferme, la partie continue où elle en était. |
| 1.14 | **Victoire : jamais deux fois** | Remettre `_victoryTFlops`, sauvegarder (retour au menu), relancer le jeu, revenir en jeu. | Aucune séquence ni écran de victoire au rechargement. (Vérifié hors Play Mode sur le code ; jamais sur le chemin complet.) |

## 2. Contrôles et réglages

Les touches, la pause et l'audio ont été éprouvés à la main le 2026-10-13 (`historique.md`).
Restent :

| # | Quoi | Comment | Ce qu'on doit voir |
|---|---|---|---|
| 2.6 | **Mode daltonien à chaud** | Choisir **Deutéranopie** dans Options > Accessibilité, refermer les options **et la pause**, puis ouvrir l'arbre (P) — ou attendre l'écran de fin de run. ⚠️ Les options ne s'ouvrent que depuis la pause, et Échap referme l'arbre avant d'ouvrir la pause : on ne peut **pas** regarder l'arbre en changeant de mode. Le premier protocole le demandait, et c'est pour ça que le test du 13/10 n'a rien montré. | Sur l'écran de jeu principal, presque rien ne change : le vert terminal reste le « positif » dans tous les modes (option B). Dans l'arbre : nœuds **abordables** blancs au lieu d'orange, nœuds **trop chers** violets au lieu de rouges, liens **ouverts** lilas au lieu de cyan. Sur la jauge, le palier **incertain** blanc au lieu d'ambre. Le Data Wiper, une fois débloqué, change aussi. Revenir en Standard : tout reprend ses couleurs d'origine. |
| 2.8 | **Taille d'interface au-dessus de 100 %** | Options > Affichage > Taille d'interface à 110 %, puis 150 %. Parcourir le menu, les options, la pause, l'écran de jeu, l'arbre, l'écran de fin. | Rien ne se chevauche, rien ne sort de l'écran. ⚠️ Le panneau d'options se chevauche par construction dès 110 % (`backlog.md` § 4) ; les autres écrans n'ont jamais été regardés au-delà de 100 %. |

## 3. Uniquement dans un build

Tout ce bloc a été validé en build le 2026-10-13 (`historique.md`). Une ligne y reviendra si un
lot touche au démarrage, à la sauvegarde ou à l'affichage.

## 4. À confirmer à l'œil

Passé en revue le 2026-10-13 : les constats confirmés sont au backlog (§ 4), les autres sont
soldés dans `historique.md`. Rien en attente.

## 5. Plus tard — à tester quand le lot concerné sera livré

| Après | Quoi |
|---|---|
| Refonte, lot 2 (Data Wiper) | Trois usages par run, −2 500 UT chacun, sacrifice de 30 / 60 / 90 % des TFlops nettes jusqu'à la fin de la run ; la vitesse de l'A.M.I. ne baisse pas. |
| Refonte, lot 3 (brouillard) | Relevé tous les 800 UT au départ, 200 en fin de partie ; aiguille et borne pessimiste ; paliers et gain du bouton lus sur l'aiguille. |
| Refonte, lot 4 (arbre) | Arbre à 21 nœuds, Noyau Compilé affiché dans les panneaux, `P_CORE_OVERRIDE` infini, nouvelle migration avec remboursement. Puis session de jeu « chiffres » sur une campagne : durée des runs, points par run, part de l'arbre à la victoire. Victoire remise à 1e12. |
| Steam | Lisibilité sur un écran 1280×800. Navigation complète à la manette, sans souris. Overlay Steam. Synchronisation cloud entre deux machines. |
| Traductions | **Langue à chaud** (ex-2.5) : basculer de langue en pleine partie, parcourir tous les écrans ; aucun texte ne doit rester dans l'ancienne langue, hors lignes déjà écrites dans la console. **Premier lancement** : l'écran de choix propose toutes les langues, celle du système présélectionnée. D'ici là, la technique du `zz.json` (chaque valeur de `fr.json` préfixée « ZZ· ») permet de traquer les textes en dur ; supprimer le fichier et sa `.meta` après. |
