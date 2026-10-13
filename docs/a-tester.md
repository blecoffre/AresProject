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
  avant de la retoucher, et avant tout test de migration.

---

## 1. Fin de partie et économie — jamais jouées *(lot 1 de la feuille de route)*

Tout ce bloc vient de `bf490c6` (2026-09-22), dont le message dit : « Rien de tout cela n'a été
éprouvé en Play Mode », et de `a772e67` (2026-10-02). C'est une session **« ressenti et bugs »** :
on regarde si ça marche et si ça se lit, pas si les chiffres sont bons — ceux-là bougeront avec la
refonte de l'arbre.

| # | Quoi | Comment | Ce qu'on doit voir |
|---|---|---|---|
| 1.1 | **Traque passive : départ** | Partie neuve (ou nouvelle run). Ne rien faire pendant une minute, puis lancer un premier cycle de `SCR_01`. | Jauge à 0,0 % tant que rien n'a été volé et qu'aucun Hardware n'est posé. Elle démarre à la fin du premier cycle, ou au premier Hardware. |
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
| 2.6 | **Mode daltonien à chaud** | Choisir **Deutéranopie** dans Options > Accessibilité, refermer les options **et la pause**, puis ouvrir l'arbre (P) — ou attendre l'écran de fin de run. ⚠️ Les options ne s'ouvrent que depuis la pause, et Échap referme l'arbre avant d'ouvrir la pause : on ne peut **pas** regarder l'arbre en changeant de mode. Le premier protocole le demandait, et c'est pour ça que le test du 13/10 n'a rien montré. | Sur l'écran de jeu principal, presque rien ne change : le vert terminal reste le « positif » dans tous les modes (option B). Dans l'arbre : nœuds **abordables** blancs au lieu d'orange, nœuds **trop chers** violets au lieu de rouges, liens **ouverts** lilas au lieu de cyan. Sur la jauge, le palier **incertain** blanc au lieu d'ambre. Ghost Cache et Data Wiper, une fois débloqués, changent aussi. Revenir en Standard : tout reprend ses couleurs d'origine. |
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
| Lot 2 (socle de la refonte) | Migration `SaveData` v10 → v11 sur une **copie** de sauvegarde réelle, charge du Ghost Cache en cours comprise. Verrou de la barre d'espace sur une sauvegarde existante. Prérequis « X points dépensés » : ouverture du nœud et libellé du prérequis manquant. Baroud d'Honneur : N usages, durée, malus. |
| Lot 3 (refonte de l'arbre) | Session de jeu « chiffres » sur une campagne : durée des runs, points par run, part de l'arbre achetée à la victoire. Victoire remise à 1e12. |
| Lot 5 (Steam) | Lisibilité sur un écran 1280×800. Navigation complète à la manette, sans souris. Overlay Steam. Synchronisation cloud entre deux machines. |
| Lot 6 (traductions) | **Langue à chaud** (ex-2.5) : basculer de langue en pleine partie, parcourir tous les écrans ; aucun texte ne doit rester dans l'ancienne langue, hors lignes déjà écrites dans la console. **Premier lancement** : l'écran de choix propose toutes les langues, celle du système présélectionnée. D'ici là, la technique du `zz.json` (chaque valeur de `fr.json` préfixée « ZZ· ») permet de traquer les textes en dur ; supprimer le fichier et sa `.meta` après. |
