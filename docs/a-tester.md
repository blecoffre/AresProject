# À tester — livré, mais jamais vu fonctionner

> Ce qui a été construit et n'a **pas encore été observé** dans les conditions où le joueur le
> rencontrera. Pour chaque ligne : quoi faire, et ce qu'on doit voir.
>
> **Une ligne quitte ce document** quand on l'a vue fonctionner (elle part dans `historique.md`,
> datée, avec la façon dont elle a été vérifiée) ou échouer (elle devient une entrée de
> `backlog.md`).
>
> Mis à jour : **2026-10-15**.

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
  avant de la retoucher. ⚠️ **Plus aucune migration** (lot 5) : la branche
  `chantier/refonte-time-attack` remplace toute sauvegarde d'un autre format par une partie neuve.
  Copier le fichier AVANT le premier lancement, pour pouvoir revenir sur `main`.
- **La refonte a été jouée d'un bloc le 2026-10-15** (`historique.md`). Le § 0 ne porte plus que
  les retours de cette passe.

---

## 0. Retours de la passe de test de la refonte (2026-10-15)

Compilés (Unity et `dotnet build`, zéro erreur, zéro warning), **jamais joués**. Les gains de la
ligne de stats ont été calculés dans l'éditeur ; la console et la rangée d'actions, mesurées dans
l'éditeur, jamais regardées à l'écran.

| # | Quoi | Comment | Ce qu'on doit voir |
|---|---|---|---|
| 8.1 | **La console défile comme un terminal** — *15/10 : les nouvelles lignes restaient cachées sous le bord* | Partie neuve. Regarder les premières lignes, puis acheter et overclocker jusqu'à remplir la console, et continuer. | Les premières lignes partent du **haut**. Une fois la console pleine, chaque nouvelle ligne apparaît **aussitôt en bas, entière**, et la plus ancienne sort par le haut. La console descend maintenant jusqu'au bas de l'écran. |
| 8.2 | **La rangée d'actions** — *15/10 : demandée par Bertrand* | Regarder le bas de la colonne de droite. | « Overclock » sur toute la largeur ; dessous, côte à côte et de même hauteur : le Data Wiper à gauche, la sortie à droite (avec sa fine barre rouge). Les listes de générateurs s'arrêtent au-dessus d'« Overclock », sans chevauchement. |
| 8.3 | **Les libellés courts** | Faire passer chaque bouton par ses états : Data Wiper verrouillé (sans « Protocole d'Urgence »), prêt, épuisé (trois usages) ; sortie verrouillée, puis prête. | Chaque libellé tient dans son bouton, sur deux ou trois lignes : « [VERROUILLÉ] DATA WIPER / Requiert le nœud / Protocole d'Urgence », « [PRÊT] DATA WIPER · reste 3 / Jauge −2500 UT / TFlops sacrifiées : 30 % », « [ÉPUISÉ] DATA WIPER / 90 % des TFlops sacrifiées / jusqu'à la fin de la run », « [VERROUILLÉ] WIPE SYSTÈME / 1er Cycle CPU : N % », « [PRÊT] WIPE SYSTÈME / Cycles CPU : +N / Prochain à N Datas ». Rien ne déborde. |
| 8.4 | **Gain à l'achat** — *15/10 : « Fournit 0 TFlops » au niveau 0* | Onglet Hardware, « Vieux PC Recyclé » au niveau 0 en ×1 ; l'acheter ; passer en ×10. Onglet Acquisition, « Campagne de Phishing Basique » au niveau 0. | « Fournit 0 TFlops (+2) », puis « Fournit 2 TFlops (+2) » ; en ×10, le gain suit le lot affiché (« +27 » au niveau 1 : un palier est franchi dans le lot). « Génère 0 Datas (+1) toutes les 1,5 s ». |
| 8.5 | **Proxy en lot** | Onglet Furtivité, « VPN Public Décentralisé » au niveau 0, sans Interception : passer de ×1 à ×10. | « Efface 2,5 s de traque par achat », puis « 25,8 s » en ×10 — le répit que l'achat du lot efface vraiment. Avant, la ligne restait à 2,5 s. |
| 8.6 | **Un Script suit le Hardware** | Noter la ligne de « Campagne de Phishing Basique » (montant et durée), puis acheter un « Vieux PC Recyclé » sans toucher au Script. | La ligne du Script change aussitôt : durée plus courte, montant égal ou plus haut. Avant, elle restait figée jusqu'au prochain achat du Script. |
| 8.7 | **Le Data Wiper prévient** | Utiliser le Data Wiper ; puis, sur l'écran de fin, cliquer « Protocole d'Urgence » dans l'arbre. | Console : « … 30 % des TFlops englouties jusqu'à la fin de la run — Hardware acheté ensuite compris. » La description du nœud le dit aussi. |
| 8.8 | **La pénalité ne bouge pas** (reprise du 2.4) | Un Overclock juste avant le Data Wiper, un juste après, **sans rien acheter entre les deux**. | Les deux lignes de console annoncent la même « Jauge +N UT ». Un Hardware acheté entre les deux la fait monter, et c'est normal : l'A.M.I. accélère avec les TFlops brutes. |

## 1. Fin de partie et économie — d'avant la refonte, jamais jouées

Tout ce bloc vient de `bf490c6` (2026-09-22), dont le message dit : « Rien de tout cela n'a été
éprouvé en Play Mode », et de `a772e67` (2026-10-02). C'est une session **« ressenti et bugs »** :
on regarde si ça marche et si ça se lit, pas si les chiffres sont bons. ⚠️ La refonte « Time
Attack » a réécrit le brouillard (lot 3) ; il ne reste ici que la victoire, à rejouer.

| # | Quoi | Comment | Ce qu'on doit voir |
|---|---|---|---|
| 1.12 | **Victoire : séquence et écran** — *14/10 : séquence OK, écran absent (objet `VictoryPanel` éteint dans la scène), corrigé le jour même* | Abaisser `_victoryTFlops` sous les TFlops courants (cf. « Avant de commencer »). | La console déroule la séquence (« Attaque du chiffrement… C'est un chat. »), puis l'écran « ACCÈS TOTAL » apparaît, textes résolus (aucune clé entre crochets), fond opaque qui capte les clics. La simulation **continue derrière** : la Trace monte. |
| 1.13 | **Victoire : fermeture** | Cliquer « Reprendre ». | L'écran se ferme, la partie continue où elle en était. |
| 1.14 | **Victoire : jamais deux fois** | Remettre `_victoryTFlops`, sauvegarder (retour au menu), relancer le jeu, revenir en jeu. | Aucune séquence ni écran de victoire au rechargement. (Vérifié hors Play Mode sur le code ; jamais sur le chemin complet.) |

## 2. Contrôles et réglages

Les touches, la pause et l'audio ont été éprouvés à la main le 2026-10-13 (`historique.md`).
Restent :

| # | Quoi | Comment | Ce qu'on doit voir |
|---|---|---|---|
| 2.8 | **Taille d'interface au-dessus de 100 %** — *14/10 : options et inspecteur de nœud se chevauchent au-delà de 120 % (`backlog.md` § 4) ; l'inspecteur est refait le 15/10, à revoir* | Options > Affichage > Taille d'interface à 110 %, puis 150 %. Parcourir le menu, les options, la pause, l'écran de jeu, l'arbre, l'écran de fin. | Rien ne se chevauche, rien ne sort de l'écran. ⚠️ Le panneau d'options se chevauche par construction dès 110 % (`backlog.md` § 4) ; les autres écrans n'ont jamais été regardés au-delà de 100 %. |

## 3. Uniquement dans un build

Tout ce bloc a été validé en build le 2026-10-13 (`historique.md`). Une ligne y reviendra si un
lot touche au démarrage, à la sauvegarde ou à l'affichage.

## 4. À confirmer à l'œil

Passé en revue le 2026-10-13 : les constats confirmés sont au backlog (§ 4), les autres sont
soldés dans `historique.md`. Rien en attente.

## 5. Plus tard — à tester quand le lot concerné sera livré

| Après | Quoi |
|---|---|
| Le § 0 (la refonte elle-même est jouée depuis le 2026-10-15) | Session de jeu « chiffres » sur une campagne : durée des runs, points par run, part de l'arbre à la victoire, valeurs de l'Interception. Victoire remise à 1e12. |
| Steam | Lisibilité sur un écran 1280×800. Navigation complète à la manette, sans souris. Overlay Steam. Synchronisation cloud entre deux machines. |
| Traductions | **Langue à chaud** (ex-2.5) : basculer de langue en pleine partie, parcourir tous les écrans ; aucun texte ne doit rester dans l'ancienne langue, hors lignes déjà écrites dans la console. **Premier lancement** : l'écran de choix propose toutes les langues, celle du système présélectionnée. D'ici là, la technique du `zz.json` (chaque valeur de `fr.json` préfixée « ZZ· ») permet de traquer les textes en dur ; supprimer le fichier et sa `.meta` après. |
