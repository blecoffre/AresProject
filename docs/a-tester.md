# À tester — livré, mais jamais vu fonctionner

> Ce qui a été construit et n'a **pas encore été observé** dans les conditions où le joueur le
> rencontrera. Pour chaque ligne : quoi faire, et ce qu'on doit voir.
>
> **Une ligne quitte ce document** quand on l'a vue fonctionner (elle part dans `historique.md`,
> datée, avec la façon dont elle a été vérifiée) ou échouer (elle devient une entrée de
> `backlog.md`).
>
> Mis à jour : **2026-10-14**.

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

Livré le 2026-10-13. **Joué par Bertrand le 2026-10-14** : les lignes vues fonctionner sont
parties dans `historique.md` ; restent celles-ci. Chiffres
attendus à 0 TFlop et sans Blindage : vitesse de l'A.M.I. 33 UT/s, jauge pleine en ~5 min. Tout ce
qui s'affiche sur la jauge passe encore par le brouillard (relevé jusqu'à 60 s au départ) : pour
les montées, **la console fait foi** ; les baisses, elles, se voient tout de suite.

| # | Quoi | Comment | Ce qu'on doit voir |
|---|---|---|---|
| 0.1 | **Migration v10 → v11** — *non éprouvée le 14/10 : la sauvegarde avait été supprimée* | Copier une sauvegarde v10, lancer depuis RootScene, « Continuer ». | Run neuve : générateurs au niveau 0, 10 $, jauge à 0 %. Le compteur de Cycles de l'en-tête a grossi des points qui étaient dépensés. Arbre (P, ou « [ NOYAU IA — ARBRE ] ») vide hors la racine. Aucune erreur en console. |
| 0.3 | **Vitesse de l'A.M.I.** — *illisible le 14/10 : relevé toutes les 60 s ; à refaire après le lot 3 (brouillard)* | Sans Hardware ni Proxy, laisser tourner. | ~20 % par minute, saisie vers 5 min. La jauge monte au même rythme quoi que produisent les Scripts. |
| 0.4 | **Overclock à vide** — *console OK le 14/10, jauge illisible ; à refaire après le lot 3* | Avant de lancer le moindre cycle (ou entre deux cycles manuels), appuyer sur Espace, puis sur le bouton « Overclock ». | Console : « Overclock sans effet : aucun cycle en cours. » La jauge ne bouge pas. |
| 0.15 | **La rangée d'actions** — *trou constaté le 14/10, corrigé le jour même* | Regarder la rangée d'actions de l'écran de jeu, sous la console. | Deux lignes pleine largeur : « Overclock », puis le Data Wiper. Plus de bouton Ghost Cache, aucun vide à droite. |


## 0 bis. Refonte « Time Attack », lot 2 — Data Wiper et écran de fin sauvegardé

Livré le 2026-10-14, compilé (Unity, zéro erreur), **jamais joué**. Pour débloquer le Data Wiper, il
faut le nœud **« Protocole d'Urgence »** (1 point), relié à la racine de l'arbre. Sans point en
réserve : jeu fermé, monter `CpuCycles` dans `savegame.json` (par exemple à 5), puis relancer.
Les achats de l'arbre ne se font qu'entre deux runs, sur l'écran de fin. Pour finir une run vite,
en éditeur, le bouton « [PRÊT] WIPE SYSTÈME » de la console est toujours cliquable.

La sauvegarde passe en v12 au premier lancement : la run en cours est conservée, seul le compteur
du Data Wiper repart à zéro.

| # | Quoi | Comment | Ce qu'on doit voir |
|---|---|---|---|
| 2.1 | **Verrouillé** | Sans le nœud « Protocole d'Urgence », regarder le bouton sous « Overclock ». | « [VERROUILLÉ] Data Wiper — requiert le nœud Protocole d'Urgence », grisé. |
| 2.2 | **Prêt** | Acheter « Protocole d'Urgence » sur l'écran de fin, « [ NOUVELLE INTRUSION ] ». | « [PRÊT] DATA WIPER — Jauge −2500 UT / TFlops sacrifiées : 30 % · usages restants : 3 ». Cliquable, sans aucune condition de TFlops. |
| 2.3 | **Premier usage** | Posséder du Hardware (noter les TFlops de l'en-tête), laisser la jauge dépasser 25 %, cliquer le Data Wiper. | Console : « > wiper.sh --inject : … Jauge −2500 UT, 30 % des TFlops englouties jusqu'à la fin de la run. » L'aiguille de la jauge recule aussitôt d'environ 25 points. Les TFlops de l'en-tête tombent à 70 %. Le bouton annonce maintenant 60 % et 2 usages restants. |
| 2.4 | **La menace ne ralentit pas** | Juste avant et juste après le premier usage, un Overclock pendant un cycle. | La console annonce la **même** pénalité en UT avant et après (« Jauge +N UT ») : le sacrifice n'a pas touché la vitesse de l'A.M.I. |
| 2.5 | **Épuisé** | Deux usages de plus. | Après le troisième : « [ÉPUISÉ] DATA WIPER — 90 % des TFlops sacrifiées jusqu'à la fin de la run », grisé, jauge pleine de la couleur du sacrifice. TFlops de l'en-tête à 10 % de leur valeur de départ ; les barres des Scripts sont nettement plus lentes. |
| 2.6 | **Usage à vide** | Run neuve, avant toute Data volée : cliquer le Data Wiper. | Console : « Jauge −0 UT » ; l'usage est consommé quand même (2 restants). |
| 2.7 | **Le sacrifice survit à la fermeture** | Deux usages, puis fermer le jeu (croix) et relancer, « Continuer ». | Le bouton annonce 1 usage restant ; les TFlops de l'en-tête sont toujours à 40 %. |
| 2.8 | **Le wipe le rend** | Finir la run, « [ NOUVELLE INTRUSION ] ». | Trois usages de nouveau ; plus aucune TFlop sacrifiée. Au bilan de la run précédente : « Data Wiper déployé … N fois ». |
| 2.9 | **Écran de fin rouvert après fermeture** | Finir une run (saisie ou sortie), rester sur l'écran de fin, **fermer le jeu** (croix ou Alt+F4), relancer, « Continuer ». | L'écran de fin revient, avec le même bilan et l'arbre. Un nœud s'achète. « [ NOUVELLE INTRUSION ] » lance bien une run neuve. |
| 2.10 | **Écran de fin rouvert depuis le menu** | Sur l'écran de fin : Échap, « Menu principal », puis « Continuer ». | Même chose qu'en 2.9 : écran de fin et arbre, pas de run neuve. |

## 1. Fin de partie et économie — d'avant la refonte, jamais jouées

Tout ce bloc vient de `bf490c6` (2026-09-22), dont le message dit : « Rien de tout cela n'a été
éprouvé en Play Mode », et de `a772e67` (2026-10-02). C'est une session **« ressenti et bugs »** :
on regarde si ça marche et si ça se lit, pas si les chiffres sont bons. ⚠️ La refonte « Time
Attack » réécrit le brouillard (lot 3) : ne pas s'attarder sur son ressenti avant. L'ex-1.1
(départ de la traque) est devenue 0.2 ; il ne reste ici que la victoire, à rejouer.

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
| 2.8 | **Taille d'interface au-dessus de 100 %** — *14/10 : options et inspecteur de nœud se chevauchent au-delà de 120 % (`backlog.md` § 4)* | Options > Affichage > Taille d'interface à 110 %, puis 150 %. Parcourir le menu, les options, la pause, l'écran de jeu, l'arbre, l'écran de fin. | Rien ne se chevauche, rien ne sort de l'écran. ⚠️ Le panneau d'options se chevauche par construction dès 110 % (`backlog.md` § 4) ; les autres écrans n'ont jamais été regardés au-delà de 100 %. |

## 3. Uniquement dans un build

Tout ce bloc a été validé en build le 2026-10-13 (`historique.md`). Une ligne y reviendra si un
lot touche au démarrage, à la sauvegarde ou à l'affichage.

## 4. À confirmer à l'œil

Passé en revue le 2026-10-13 : les constats confirmés sont au backlog (§ 4), les autres sont
soldés dans `historique.md`. Rien en attente.

## 5. Plus tard — à tester quand le lot concerné sera livré

| Après | Quoi |
|---|---|
| Refonte, lot 3 (brouillard) | Relevé tous les 800 UT au départ, 200 en fin de partie ; aiguille et borne pessimiste ; paliers et gain du bouton lus sur l'aiguille. |
| Refonte, lot 4 (arbre) | Arbre à 21 nœuds, Noyau Compilé affiché dans les panneaux, `P_CORE_OVERRIDE` infini, nouvelle migration avec remboursement. Puis session de jeu « chiffres » sur une campagne : durée des runs, points par run, part de l'arbre à la victoire. Victoire remise à 1e12. |
| Steam | Lisibilité sur un écran 1280×800. Navigation complète à la manette, sans souris. Overlay Steam. Synchronisation cloud entre deux machines. |
| Traductions | **Langue à chaud** (ex-2.5) : basculer de langue en pleine partie, parcourir tous les écrans ; aucun texte ne doit rester dans l'ancienne langue, hors lignes déjà écrites dans la console. **Premier lancement** : l'écran de choix propose toutes les langues, celle du système présélectionnée. D'ici là, la technique du `zz.json` (chaque valeur de `fr.json` préfixée « ZZ· ») permet de traquer les textes en dur ; supprimer le fichier et sa `.meta` après. |
