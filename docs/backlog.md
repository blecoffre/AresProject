# Backlog — audit du 2026-08-27

Audit complet refait à cette date. Le précédent (2026-08-25) contenait au moins une entrée fausse
propagée sans vérification, alors qu'elle décrivait un **comportement** et non du code.

**Méthode retenue, à conserver pour les prochains audits.** Chaque entrée porte sa nature :

- 🔬 **vérifié en Play Mode** — le comportement a été observé, pas déduit
- 📖 **lecture de code** — constat statique, vrai par construction (appelant absent, champ non lu…)
- ⚖️ **estimation** — plausible mais non mesuré ; ne pas planifier un lot dessus sans mesurer d'abord

Un « X n'a aucun appelant » est un constat de lecture. « La fonctionnalité est cassée » est un
constat de comportement, et demande le Play Mode. Ne pas confondre les deux : c'est exactement
l'erreur qui avait produit l'entrée fausse sur la révélation progressive.

**Repassé entrée par entrée le 2026-08-30.** Chaque point ci-dessous a été revérifié à cette
date ; ceux qui avaient été réglés entre-temps sont descendus dans « Corrigé à ce jour ».

État de la base : 72 fichiers runtime, 6 fichiers d'éditeur, ~9 530 lignes dans `Assets/Core`.
Compilation sans erreur ; 3 warnings, tous en code éditeur (voir plus bas).

---

## Majeurs restants
- ⚠️ 🔬 **Les 30 s du Zéro-Day Exploit ne sont jamais atteintes — assumé, à observer en jeu.**
      Bertrand a tranché le 2026-08-27 : on garde le ×10 et on regarde ce que ça donne à la
      manette avant de décider si c'est la défense qui est trop faible ou le multiplicateur trop
      gros. Le chiffre à avoir en tête : la dissipation valant zéro pendant l'Exploit, la survie
      depuis une jauge vide vaut `100 / (génération × 10)`. Tenir 30 s exige ≤ **0,33 Trace/s**,
      alors qu'un `SCR_01` de niveau 1 en produit 0,5. Mesuré : 6,32 Trace/s → saisie à **1,58 s**.
      Le nœud `P_EXPLOIT_TRACE_REDUC` à fond ne monte le seuil qu'à 0,44 Trace/s.
      ⚠️ En attendant, **le libellé du bouton annonce 30 s** — il ment au joueur. Si le ×10 reste,
      c'est la durée affichée qu'il faudra revoir, pas seulement l'équilibrage.
- **Accessibilité — daltonisme.** À terme, une option permettra d'adapter la palette au type de
      daltonisme du joueur. Beaucoup de daltoniens insistent sur ce point dans ce genre de jeu, où
      la consultation de l'arbre de prestige leur est souvent difficile.
      ⚠️ **Le lot du 2026-08-29 a aggravé la dette sur ce point** : les cinq états d'un nœud sont
      distingués par la COULEUR SEULE — gris, rouge, ambre, vert, vert inversé. Rouge et vert sont
      précisément la paire que la deutéranopie confond, et c'est ici la différence entre « trop
      cher » et « déjà entamé ». Le texte de la ligne d'état (« VERROUILLÉ », un prix, un niveau,
      « MAX ») porte heureusement déjà l'information en toutes lettres : le nœud reste lisible
      sans la couleur. Il manque un second canal sur la pastille elle-même — trame de fond,
      épaisseur de cadre ou glyphe — le jour où le sujet sera traité.

## Données et contenu

- 🔬 **45 clés de localisation manquantes** (recompté le 2026-08-30) : les **`UPG_*_DESC`**,
      aucun générateur n'a de description. Les 45 noms sont là, et les 14 paires `PRESTIGE_*` des
      nœuds nommés ont été écrites le 2026-08-29 — l'arbre étant devenu visible, elles bloquaient.
      Les 105 nœuds spécifiques passent par des gabarits, ils n'ont pas de clé propre.
- 🔬 **Libellés du header à revoir** : `MONEY` = « Money » et `COMPUTER_POWER` = « Computer Power »
      sont clairement des placeholders. `TRACE`, `CYCLES` et `OVERCLOCK` passent en français tels
      quels et relèvent du choix de ton, pas de la traduction.
- 📖 **5 clés sans aucune référence** dans `fr.json` (recompté le 2026-08-30 en croisant le code,
      les scènes ET les prefabs ; les clés dérivées d'un id sont exclues du calcul, elles ne sont
      jamais littérales) : `UPGRADE_NAME_BOTNET` et `UPGRADE_NAME_BREACH_CORE`, vestiges d'avant la
      migration des noms ; `CONSOLE_LOGS` ; `COMPUTER_POWER`, dont le libellé du header est
      désormais composé dans la vue au lieu de passer par la clé ; et `UI_ACQUIRED`, devenue
      inutile le 2026-08-30 — un nœud à achat unique passe directement de « prix » à « MAX ».
- ✅ **180 paliers, 45/45 générateurs couverts** (2026-08-27). Le GD a complété les 15 Proxies :
      quatre `TraceMultiplier` chacun aux niveaux 10 / 25 / 50 / 100, cumul ×300 uniforme.
      Catalogue régénéré, zéro avertissement du générateur.
- 📖 **L'équilibrage des 105 nœuds spécifiques est du remplissage.** Leur nombre et leur pertinence
      sont corrects depuis le lot 3a, mais les valeurs restent auto-générées : `maxLevel: 5`,
      `costMult: 1.4` et `bonus: 0.1` identiques pour tous, `baseCost = 50 × order`.

## Cycle de vie

- 📖 `GameOverPresenter.HandleRestart` : `_ = _sceneLoader.LoadGameSceneAsync(CancellationToken.None)`
      — il manque le `.Forget()`, et `CancellationToken.None` rompt la chaîne d'annulation.
      Toujours vrai au 2026-08-30.
- 📖 `UpgradePanelView._resolver` : `IObjectResolver` injecté et jamais lu, alors qu'il réglerait
      justement le Service Locator d'`AutoInjectOnInstantiate`. Celui de `PrestigePanelView` a été
      retiré le 2026-08-29, et le `_cts` inutilisé du `GameOverPresenter` a disparu avec la
      réécriture du bilan.

## Performance

- ⚖️ **Repeinte de l'arbre : 119 nœuds × 3 passes par achat.** Les abonnements par nœud ont
      disparu le 2026-08-29 — le panneau écoute une fois et rediffuse — mais chaque passe refait un
      `Math.Pow` et alloue une chaîne par nœud, et un achat déclenche trois signaux dans la même
      frame. Ordre de grandeur déduit : ~350 chaînes par achat. **Jamais mesuré.** Un achat de
      prestige est rare et l'écran est modal, donc c'est probablement sans importance ; à profiler
      avant d'en faire un lot. Le remède, si besoin, est celui de `GeneratorPresenter` : cache et
      `DistinctUntilChanged`.
- 📖 **`ILocalizationService.GetText<T0>`** promet « Zero Boxing » mais fait un
      `string.Format(string, object)`, qui boxe chaque type valeur. → `ZString.Format<T0>`,
      déjà disponible avec UniTask.
- 📖 **`HeaderView`** : `$": {formattedValue}"` par-dessus un `CurrencyFormatter.Format` qui alloue
      déjà. `UpdateTraceDisplay` montre la bonne pratique avec `SetText("{0:F1}%", …)`.
- 📖 **`LogObjectPool`** : `_activeItems[0]` + `RemoveAt(0)` et un `Contains` linéaire. Un tampon
      circulaire rendrait le recyclage O(1).
- ⚖️ Le rendu de l'arbre de prestige à 119 nœuds demandera peut-être du pooling ou de la
      virtualisation. Jamais mesuré non plus — l'arbre s'affiche aujourd'hui sans ralentissement
      perceptible en éditeur.

## Hygiène
- 🔬 **`GameOverPanel/PrestigeButton` est mort.** L'arbre vit maintenant DANS l'écran de fin :
      le bouton « DÉPENSER — NOYAU IA » qui servait à l'ouvrir n'a plus d'objet. Son handler a été
      retiré du `GameOverPresenter` et le GameObject désactivé le 2026-08-29 ; il reste supprimable
      à la main, avec sa clé `UI_RUN_END_PRESTIGE_BUTTON`.

- 🔬 **Le brouillard de guerre a disparu des nœuds verrouillés.** L'ancien `_unknownPanel`
      masquait le nom d'un nœud dont la branche était fermée ; il n'existe plus dans le prefab
      remanié, et le nouveau code affiche le nom en gris avec la mention « VERROUILLÉ ».
      **C'est un choix, pas un oubli** : Bertrand veut consulter l'arbre pour planifier, et on ne
      planifie pas vers des cases vides. À rediscuter si le GD tient au mystère.

- 🔬 **Le trait des nœuds verrouillés n'est pas pointillé.** La maquette veut un cadre en
      tirets ; le sprite du cadre est plein, et un pointillé demande un sprite dédié. Le gris
      `#335533` porte seul l'état pour l'instant.

- 🔬 **L'écran méta recouvre la jauge de Trace.** Consulter l'arbre pendant une run masque la
      console ET la Trace, qui continue pourtant de monter. Cohérent avec « pas de planque
      gratuite », mais le joueur pilote à l'aveugle : un rappel de Trace dans le header de l'écran
      serait honnête. À arbitrer par le GD.

- 🔬 **Le fond de l'écran méta est à 98 % d'opacité**, pas à 100 %. `GameOverPanel/Background`
      couvre bien tout le canvas (1920×1080, ancres 0-1) et se dessine bien par-dessus la console
      et le panneau d'interactions : ce n'est PAS un problème de couverture ni d'ordre de rendu,
      seulement les 2 % restants. Sur des textes saturés et lumineux, ces 2 % suffisent à laisser
      lire la console et les boutons du jeu derrière l'arbre. Une valeur d'alpha à changer, si
      cette transparence n'est pas voulue.

- 📖 **`UI_HDR_Glow` échantillonne enfin sa texture — corrigé par Bertrand, reste à en profiter.**
      Vérifié le 2026-08-30 : le graphe contient désormais un `SampleTexture2DNode`, un `SplitNode`
      et un `MultiplyNode`, avec une propriété `_MainTex`. Il ne se réduit donc plus à
      `VertexColor × _Color`, qui remplissait tout le rectangle d'un aplat et détruisait les
      sprites de contour. **Constat de lecture du graphe, pas de rendu** : la preuve serait de
      remettre le halo de contour sur un `UiHoverGlow` — retiré le 2026-08-28 à cause de ce défaut
      — et de regarder. C'est le vrai reste à faire de cette entrée.

- 🔬 **Trois jauges ont disparu pendant le rework UI.** `GhostCacheView._fill`,
      `EmergencyView._fill` et `ExfiltrationView._progressFill` valent **NULL** dans la scène.
      Les vues se gardent (`if (_fill == null) return`), donc rien ne plante — mais l'information
      est perdue : plus de barre de charge du Ghost Cache, plus de décompte visuel du Data Wiper,
      plus de progression vers le premier CPU Cycle. Les libellés portent encore le pourcentage,
      c'est donc dégradant et non bloquant. Constaté le 2026-08-28.
- ✅ **Polices unifiées** (2026-08-30). Les 8 derniers textes en LiberationSans, tous dans
      `GameOverPanel`, sont passés en ShareTechMono. **Trois autres portaient déjà la bonne police
      avec le MAUVAIS matériau** — le narratif, le bilan et le libellé de `StartNewRunButton` —
      exactement le mésappariement contre lequel cette entrée mettait en garde. Contrôle final :
      32 / 32 textes de scène conformes police ET matériau, 9 / 9 dans les prefabs. Matériau **nu**
      partout, sans halo : le halo reste une décision d'emphase, à poser à la main. À noter,
      `↻` (U+21BB) manque aussi à l'atlas de ShareTechMono — le libellé des cycles reste en ASCII.

- 🔬 **Les couleurs de la console ne suivent pas la maquette.** `ConsoleView.FormatMessage`
      écrit `#00FF00` (vert pur) là où la maquette veut `#4AF626`, et surtout **`#FF00FF` magenta**
      pour les lignes narratives, là où la maquette veut de l'orange. Les balises `[OK]`,
      `[SYSTEM_WARN]` et `[INFO]` restent par ailleurs en dur — déjà noté plus bas.

- 🔬 **Le nom du bouton de relance est « Button ».** `GameOverPanel/StartNewRunButton/Text (TMP)`
      porte encore le libellé du prefab Unity : aucun `LocalizedText`, aucune clé, rien dans le
      `GameOverPresenter` qui l'écrive. C'est le bouton que le joueur doit presser à CHAQUE fin de
      run. Constaté le 2026-08-30. Deux lignes : une clé, un `LocalizedText`, puis relancer
      l'auto-enregistrement VContainer.

- 🔬 **L'inspecteur de nœud se chevauche : auto-sizing et `ContentSizeFitter` s'annulent.**
      `Name_Desc_Effects` et `Prerequisite` ont un `ContentSizeFitter` en *PreferredSize* mais un
      `VerticalLayoutGroup` à `childControlHeight = false` : le groupe ne mesure donc pas ses
      enfants, il additionne leurs hauteurs figées à 50 px, alors que la description en demande
      500. Le texte déborde et se dessine par-dessus le suivant.
      ⚠️ **Basculer `childControlHeight` à true ne suffit PAS — essayé le 2026-08-30, et c'est
      pire.** Les six textes ont `enableAutoSizing` activé : le fitter demande au texte sa hauteur
      préférée pendant que le texte grossit pour remplir la hauteur qu'on lui donne, et la boucle
      part en fonte géante. Les deux mécanismes sont incompatibles par construction. Il faut
      **désactiver l'auto-sizing** sur ces six textes et leur fixer une taille, avant de rendre la
      hauteur au layout group. Réglages laissés exactement dans l'état où Bertrand les a mis.

- 📖 **Aucun `.asmdef`**, aucun test. Tout `Assets/Core` recompile avec le reste, et rien n'empêche
      une dépendance de `Models` vers `UI`. Proposition inchangée : `Ares.Core.Domain`,
      `Ares.Core.Presentation`, `Ares.Core.Editor` — prérequis aux tests EditMode.
      `UpgradeModel`, `PrestigeManager`, `ThreatManager`, `CurrencyFormatter` et `UserCurrencies`
      ne touchent presque pas Unity : c'est le meilleur retour sur investissement du projet,
      surtout pour des formules qu'on modifie sans arrêt.
- 📖 **Code mort restant : `LogObjectPool.ReturnToPool()`**, sans appelant. Et le
      `viewInstance.SetVisible(true)` d'`UpgradePanelPresenter` porte sur un prefab dont la racine
      est déjà active, donc sans effet. `GeneratorPresenter.SetVisibility()` et
      `UpgradeManager.GetAllActiveUpgrades()` ont disparu depuis — vérifié le 2026-08-30.
      ⚠️ **Ne pas confondre avec la révélation progressive, qui FONCTIONNE** — 🔬 vérifié :
      45 upgrades au catalogue mais 3 `GeneratorView` instanciés au démarrage, et le suivant
      apparaît à chaque premier achat. Elle passe par l'instanciation à la demande
      (`GetInitiallyVisibleUpgrades` + `OnUpgradeRevealed`), pas par l'activation.
- 🔬 **`AutoInjectOnInstantiate` ne s'exécute pas sur un objet inactif.** Les générateurs
      instanciés dans les onglets masqués ne sont pas injectés et affichent le texte brut du
      prefab (« Button » au lieu de « Acheter »). Vérifié : ça **se rattrape à l'ouverture de
      l'onglet**, donc invisible en pratique — mais c'est la même famille de piège que celui qui
      avait cassé l'arbre de prestige. Le composant fait par ailleurs un `LifetimeScope.Find` dans
      `Awake()`, soit un Service Locator déguisé ; `_resolver.Instantiate(prefab, parent)` injecte
      l'arbre entier et rendrait ce composant inutile.
- 📖 **`SteamCloudSaveService` et `SaveServiceComposite` compilent mais ne sont pas enregistrés**
      (TODO commenté dans `RootLifetimeScope`). `SteamCloudSaveService` appelle
      `SteamManager.Initialized` en statique → à encapsuler derrière un `ISteamRuntime` injecté.
- 📖 **Français en dur** : reste `ConsoleView.FormatMessage`, avec ses tags `[OK]`,
      `[SYSTEM_WARN]`, `[INFO]` et leurs couleurs codées en dur.
- 📖 **3 warnings `CS0618`** dormants : `LocalizationAnalyzerWindow.cs:120` et
      `LocalizationAutoInjectorEditor.cs:38,46` utilisent `FindObjectOfType` / `FindObjectsOfType`,
      dépréciés en Unity 6. Ils ne remontent plus faute de recompilation de ces fichiers, mais le
      code est inchangé. Code éditeur uniquement.
- 📖 **`Currency.Add()`** est public et sans garde sur les valeurs négatives — les gardes sont dans
      `UserCurrencies`, mais `Currency` est exposé.
- 📖 **`HeaderPresenter`** s'abonne dans son constructeur ET dans `Start()`. Même écart dans
      `PrestigeItemPresenter`, qui s'abonne entièrement dans son constructeur.
- 📖 **Namespaces ≠ dossiers** et dossier fantôme vide `Assets/Core/UI/Uppgrades` toujours présent.
- 📖 **Séparateur décimal dépendant de la machine — trois endroits**, pas un seul (recompté le
      2026-08-30) : `GeneratorPresenter.BuildStatsText` (`GetCurrentCycleDuration().ToString("0.##")`)
      et **`ClickerPresenter` deux fois** (`result.SecondsGained.ToString("0.#")`). Tous utilisent
      `CurrentCulture` — « 1,5 s » ici, « 1.5 s » ailleurs — alors que `CurrencyFormatter` force
      `InvariantCulture`. Trancher une politique unique.
- 📖 **`HeaderView` compose du texte affichable en dur** — la règle absolue du projet y échappe
      sur cinq lignes : `$": {valeur}"`, `$": {valeur} TFlops"`, `$"+{rendement}/s"` et
      `SetText(": {0:F1}%", …)`. L'unité « TFlops », le préfixe « : », le « /s » et le « % » sont
      des libellés, et ils devraient venir de clés. C'est aussi ce qui a rendu `COMPUTER_POWER`
      morte. Trouvé le 2026-08-30.

- 🔬 **Le nom des nœuds de prestige déborde de la pastille.** « Surcadencement » est tronqué en
      « Surcadencemen » sur un nœud de 500 px. Vu en capture le 2026-08-30. Auto-sizing ou marge,
      à trancher en même temps que le positionnement des nœuds que le GD doit revoir.

- 📖 **Faux libellé sur les onglets Hardware et Proxy** : `BuildStatsText()` affiche
      « Génère X **Datas** » pour tous les types, alors qu'un Hardware fournit des TFlops et qu'un
      Proxy dissipe de la Trace.
- 📖 **Pluriel non géré** : `UI_EXFIL_READY` dit « +1 **Cycles** CPU ». Une vraie pluralisation
      demanderait un mécanisme dans `ILocalizationService` ; reformuler la clé suffirait pour
      l'instant.
- ✅ **Remote Git en place** (2026-08-27) : `origin` pointe sur GitHub, `main` poussé.
      Reste à évaluer Git LFS le jour où le projet portera de vrais assets binaires.

## Pièges à retenir
- **Le projet est sur le NOUVEL Input System.** `UnityEngine.Input` y lève une
      `InvalidOperationException` à chaque appel — une par frame si c'est dans un `Tick`.
      Utiliser `Keyboard.current`, et tester sa nullité : elle vaut null sans clavier connecté.


Trois bugs de la même famille ont déjà coûté du temps. Le motif :

- **Unity n'appelle pas `Awake()` sur un objet inactif dans la hiérarchie.** Tout composant dont une
  méthode est appelée pendant la construction d'un panneau masqué doit résoudre ses références en
  **lazy**, jamais dans `Awake`. C'est ce qui avait cassé l'arbre de prestige, et c'est ce qui rend
  les onglets masqués non injectés.
- **Lancer le Play Mode depuis la GameScene est cassé par construction** : sans `RootScene`,
  `SceneLoader` ne tourne pas, donc pas d'`EnqueueParent`, donc le scope de scène n'a pas de parent
  et aucun service n'est résoluble. Le message d'erreur ne le dit pas. Une garde explicite dans
  `GameSceneLifetimeScope` éviterait le diagnostic à chaque fois.
- **`04_SpecificUpgrades.json` est une SORTIE, pas une source.** `PrestigeSpecificNodesGenerator`
  (`Tools/Core/Générer JSON (Grille - Arête de poisson Corrigée)`) le réécrit **intégralement**
  depuis le catalogue d'upgrades. Y ajouter des nœuds à la main ne survit pas à la prochaine
  exécution : c'est arrivé le 2026-08-30, où 15 nœuds d'automatisation écrits directement dans
  le fichier ont disparu, puis où le générateur d'arbre a supprimé les 15 `.asset` devenus
  orphelins — sans erreur ni avertissement, seulement un compte d'orphelins dans son message de
  succès que personne ne lit. **Un nouveau type de nœud ciblé s'ajoute dans le générateur, jamais
  dans le JSON.** Et avant de conclure qu'un nœud « ne s'affiche pas », compter les entrées du
  fichier : c'est la vérification la moins chère.

- **Le player loop de l'éditeur est figé tant que la fenêtre Unity n'a pas le focus**
  (`Time.frameCount` ne bouge pas), et `runInBackground` n'y change rien. Pour vérifier un
  `ITickable` sans focus, appeler `Tick()` à la main.

## Corrigé à ce jour

**Le SaveScheduler n'abandonne plus d'écriture** (2026-08-30, vérifié en Play Mode via MCP) — une demande arrivant pendant une écriture en vol était **jetée**, au motif que « la prochaine capturera un état plus récent ». Ce qui ne tient que s'il y en a une prochaine : trois achats de prestige rapprochés suivis d'un Game Over avaient fait perdre l'écriture de fin de run. La demande arme désormais un tour de **rattrapage** — front descendant — si bien qu'une rafale de N demandes coûte au plus une écriture de plus et que le dernier état atteint toujours le disque.

**Un seul emplacement d'attente, et c'est délibéré.** Une file serait FAUSSE ici : `GameStateGateway.Capture()` retourne un tampon partagé qui continue de muter, donc empiler des demandes empilerait N références au même objet. Le code ne s'en sort déjà que parce que `SaveAsync` sérialise avant son premier `await`. Un `SemaphoreSlim` aurait le même défaut, en écrivant plusieurs fois le même état final. Écarté aussi : router les déclencheurs critiques vers `SaveBlocking`, qui commite sur le même fichier qu'une écriture asynchrone potentiellement en vol — la voie async, sérialisée plus tôt donc plus ANCIENNE, pouvait commiter en dernier et écraser le plus récent.

Le `catch` est placé DANS la boucle : un échec d'écriture ne doit pas emporter la demande qui attend son tour. Et le rattrapage porte son propre message de console — sans lui, deux écritures consécutives produisent deux lignes identiques que Unity replie en une seule, rendant le mécanisme invisible à qui le débogue. C'est d'ailleurs ce qui a faussé la première mesure.

Vérifié : trois achats dans la **même frame** produisent exactement deux lignes, dont une « rattrapage », et le fichier contient les trois nœuds. La preuve tient parce que `Capture()` est appelé dans la notification du premier achat : l'écriture nº1 ne pouvait contenir que le premier nœud. Rien à signaler côté atomicité, déjà traitée — `LocalJsonSaveService` écrit dans un temporaire puis bascule par `File.Replace`, avec `.bak` de secours.

**Nœud d'automatisation des Scripts** (2026-08-30, vérifié en Play Mode via MCP) — Bertrand avait posé l'amorce : la valeur d'enum, les clés `PrestigeAutomationName/Description`, les trois branches de `PrestigeLabels`, le cas dans `RecalculateBonuses` et la reconnaissance du type par le générateur. **Trois maillons manquaient.** (1) `SpecificUpgradeBonuses` n'avait pas de champ pour l'automatisation : le `case Automation` d'`AccumulateSpecific` cumulait dans le vide, sans branche. (2) La garde d'égalité de `SetSpecificBonuses` ne couvrait pas le nouveau champ — un achat ne touchant QUE l'automatisation serait sorti par le retour anticipé. (3) **Les 15 nœuds n'existaient pas dans les données**, ce qui explique qu'aucun ne s'affichait.

**Défaut trouvé à la compilation, et il était grave** : la nouvelle valeur avait été insérée **au milieu** de `PrestigeBonusType`, juste après `SpecificUpgradeTimeReduction`. L'index est sérialisé dans les `.asset` : toute la famille Zéro-Day s'est décalée d'un cran, et la console a signalé que `P_EXPLOIT_CHARGES` se croyait un nœud d'automatisation — donc que les charges de Ghost Cache, le rendement de l'Exploit et son malus de Trace désignaient tous autre chose. C'est exactement ce contre quoi le commentaire de l'enum met en garde. Valeur déplacée en fin d'enum : les `.asset` existants retrouvent leur sens sans migration, et les sauvegardes n'étaient pas concernées (elles indexent par id, pas par index).

**Convention assumée** : `bonus` vaut ici un NOMBRE DE NIVEAUX, pas une fraction — seule exception à la règle du « tout en fraction », documentée dans la struct. Un seuil d'automatisation est un numéro de niveau. Arrondi et non troncature à l'application, même piège que les charges du Ghost Cache. **Choix de données** : un nœud par Script (15, eux seuls relancent des cycles), en Y=4 sous le nœud TIME et **en prolongement de celui-ci** plutôt qu'en quatrième frère du nœud COST — automatiser est le terme de la branche d'un Script, le seul bonus qui change sa façon de se jouer plutôt que ses chiffres. Valeurs alignées sur les frères (maxLevel 5, costMult 1.4, baseCost 50 × n), donc du remplissage comme les 105 autres. Arbre regénéré : **134 nœuds, 0 orphelin, 0 avertissement**.

Vérifié : le seuil de `SCR_02` descend d'exactement un niveau par rang, 10 → 5 ; à 5 rangs, un `SCR_02` de niveau 7 est **automatisé** là où il en aurait fallu 10 ; `SCR_03`, sans nœud, reste à 10 — le ciblage tient ; l'achat direct du nœud sans ses parents est refusé ; le 6ᵉ rang est refusé ; `P_EXPLOIT_CHARGES` a retrouvé son vrai type ; les 15 pastilles s'affichent avec nom, lore, effet et prérequis résolus. **Clé manquante ajoutée** : `PRESTIGE_AUTOMATION_DESC`, que `ResolveDescription` réclamait déjà.

**Prix arrondis à la hausse, polices unifiées** (2026-08-30, vérifié en Play Mode via MCP) — les deux points que Bertrand avait explicitement autorisés dans ce backlog. **`FormatCost`** : `Format` tronque vers le bas, ce qui est correct pour un solde et faux pour un prix. `SCR_01` au niveau 1 coûte 10,7 et s'affichait « Coût: 10 » ; un joueur avec exactement 10 Datas lisait le prix, voyait le bouton rester gris, et concluait que le jeu était cassé. Le nouveau formateur plafonne à la précision **réellement affichée**, pas à l'unité : « 1,234K » masque déjà 999 unités, et arrondir vers le bas cette décimale-là reproduirait le défaut un cran plus haut. Un arrondi à six décimales précède le plafonnement : les coûts sortent d'un `Math.Pow`, où un prix valant exactement 100 se stocke en 100,000000000000014, et plafonner tel quel afficherait 101 — un mensonge dans l'autre sens. Appliqué aux cinq endroits qui affichent un prix ou un seuil à atteindre ; les soldes et les gains gardent `Format`. Vérifié en jeu sur le cas d'origine : la vue affiche bien « Coût: 11 » pour un coût réel de 10,7. Contrepartie assumée : le garde-bruit avale une différence réelle de 0,5 à l'échelle du milliard, où elle n'a plus de sens.

**Polices** : 8 textes en LiberationSans converties, plus **3 matériaux mésapparis** que personne n'avait vus — police ShareTechMono, matériau LiberationSans, sur le narratif, le bilan et le bouton de relance. 32/32 textes de scène et 9/9 des prefabs sont désormais conformes sur les deux plans. **Effet de bord signalé** : ShareTechMono étant plus large, le débordement de l'inspecteur devient franchement visible — il préexistait (hauteurs figées à 50 px pour un contenu de 500) mais LiberationSans le masquait à moitié. Tentative de correction par `childControlHeight`, **annulée** : elle met l'auto-sizing et le `ContentSizeFitter` en boucle. Les réglages de layout sont rendus à l'état exact où Bertrand les avait laissés ; l'entrée détaillée est en Hygiène.

**Lot « Arbre de prestige : fenêtre d'achat, cascade et cinq états »** (2026-08-29, vérifié en Play Mode via MCP) — trois défauts signalés par Bertrand, plus un quatrième trouvé à l'inspection. **(1) L'achat était possible pendant une run.** `TryPurchasePrestige` n'avait aucune garde de session : les CPU Cycles se gagnent en TERMINANT une run, pouvoir les dépenser au milieu d'une autre transformait l'arbre en boutique d'appoint qu'on rouvre dès qu'on est en difficulté. Nouvelle `ArePurchasesAllowed` dans le `PrestigeManager`, pilotée par le `GameSessionManager` — c'est la session qui déclare son état, l'inverse fermerait le cycle de dépendances puisqu'elle dépend déjà du prestige. **(2) Les nœuds débloqués ne se rafraîchissaient pas.** `PrestigeItemPresenter` n'écoutait QUE `CpuCycles.Amount` : un achat n'ouvrait visuellement ses enfants que par effet de bord, parce qu'il coûtait de l'argent. L'écran écoute désormais `OnBonusesRecalculated`, émis après chaque recalcul — achat comme chargement de sauvegarde. **(3) Les cinq codes d'état de la maquette** sont implémentés : maxé en couleurs inversées, en cours en vert, débloquable en ambre pulsant, trop cher en rouge, verrouillé en gris. **(4) Défaut trouvé à l'inspection : `_interactionButton` était NULL sur `NodePrefab`** — le renommage `_buyButton` → `_interactionButton` avait perdu la référence, une NRE par nœud dans `Awake` et plus aucun clic. Le prefab est recablé. Idem pour `_maxedColor`, qui avait SURVÉCU au changement de script avec sa valeur (0,0,0,0) : un nœud maxé se peignait en noir opaque, texte noir compris.

Trois décisions d'architecture au passage. **Le clic sélectionne, il n'achète plus** : le `PrestigeNodeDetails` que Bertrand avait construit est branché (`PrestigeDetailsView` + presenter), et c'est son bouton qui dépense — sur un arbre de 119 nœuds, un clic qui engage immédiatement une monnaie gagnée en une run entière est un piège. Le bouton porte aussi le REFUS (« fonds insuffisants », « branche verrouillée », « hors run uniquement ») : un bouton grisé sans explication laisse chercher. **`GameOverPanel` devient l'écran méta unique**, conséquence de la fusion des deux panneaux dans la scène : l'arbre étant devenu son enfant, il ne pouvait plus s'afficher seul, son parent étant éteint. `PrestigePanelView` possède désormais la racine et deux groupes — « fin de run seulement » et « consultation seulement » — et `GameOverView` n'écrit plus que ses textes ; deux composants qui se disputent les mêmes `SetActive` finissent toujours par se marcher dessus. Échap est inerte sur l'écran de fin, il n'y a rien derrière à quoi revenir. **Tous les abonnements de l'écran vivent dans le panneau** : faire écouter les trois mêmes sources à chacun des 119 nœuds coûtait 357 abonnements pour trois signaux. Le panneau écoute une fois et rediffuse par boucle indexée ; les nœuds et l'inspecteur n'ont plus aucun `IDisposable` réactif. Corollaire assumé : un achat déclenche les trois signaux dans la même frame, donc trois passes de repeinte — les coalescer par numéro de frame serait un piège, le débit de la monnaie précédant l'incrément du niveau, la première passe lit encore l'ancien niveau. La pulsation ambre se coupe pendant une run (c'est un appel à l'action, pas un code couleur) et chaque `PrestigeItemView` se **désactive** quand il n'a pas à pulser — sur 119 nœuds, laisser 119 `Update()` tourner pour que trois clignotent serait payer le pire des deux mondes.

29 clés de localisation ajoutées, dont 14 gabarits d'effet, un par `PrestigeBonusType` — littéraux dans un `switch` et non clé construite depuis le nom de l'enum, pour rester visibles du `LocalizationAnalyzerWindow`. **Défaut trouvé au test** : `↻` (U+21BB) n'existe pas dans l'atlas de la police, remplacé par un carré — le libellé des cycles est repassé en ASCII. Vérifié : achat refusé pendant une run **sans rien dépenser** (25 cycles avant et après) ; les cinq états observés simultanément dans l'arbre ; achat de `P_STEALTH` à la fin d'une run → 25 → 5 cycles, le nœud passe en vert « Niv. 1 / 5 » et **`P_EMERG` bascule dans la même frame** de « VERROUILLÉ » gris à « 100 CPU » rouge ; l'inspecteur se repeint en place (coût 20 → 32, bouton « COMPILER » → « FONDS INSUFFISANTS ») ; le voile de sélection suit le nœud consulté ; Échap inerte en mode fin de run ; zéro erreur console.

**Durée de run au bilan** (2026-08-29, vérifié en Play Mode via MCP) — `GameSessionManager` devient `ITickable` et cumule les secondes de jeu. **Cumulées et non déduites d'un horodatage** : `Time.time` repart de zéro à chaque lancement, une run reprise le lendemain aurait affiché la durée de la seule session en cours. Le chronomètre s'arrête pendant l'écran de fin — contempler son bilan n'est pas du jeu — et repart à zéro au wipe. `SaveData` v6 (`RunElapsedSeconds`) : le temps passé fenêtre close n'est jamais crédité, le compteur n'avance que dans `Tick()`. Deux gabarits d'affichage, les unités dans les clés : « 14 min 07 s » au-delà de la minute, « 42 s » en deçà — un format unique aurait rendu « 0 min 42 s » sur la statistique la plus regardée de l'écran. Vérifié : les deux formats, la remise à zéro au wipe, l'aller-retour JSON, les migrations v2/v4/v5 → v6 qui préservent `EmergencyUsesInRun`, et la restauration qui rend bien 423,5 s au chronomètre.

**Bilan de fin de run** (2026-08-29, vérifié en Play Mode via MCP) — `OnSessionEnded` ne transportait qu'un `double`, le gain : l'écran de fin ne pouvait ni distinguer une Saisie Fédérale d'un Effacement Propre, ni rien raconter d'autre. Nouveau `RunSummary` — struct readonly — portant la raison, les cycles gagnés, ceux d'avant bonus, les Datas de la run, la Trace à la sortie et les usages du Data Wiper. **Capturé avant le wipe**, obligatoirement : `ResolveRunEnd` efface la run trois lignes plus bas, lu après il ne contiendrait que des zéros. Les deux fins ont leur narration et leur COULEUR de titre — rouge pour une saisie, vert pour un effacement propre — pour que le joueur sache en un coup d'œil s'il a bien joué. La ligne « Trace à la sortie » n'apparaît que sur une sortie volontaire : après une saisie elle vaut 100 % par définition. Le bonus Clean Exit n'est annoncé que s'il a réellement rapporté quelque chose. Un bouton « DÉPENSER » ouvre l'arbre depuis l'écran de fin, qui recouvre le bouton du header. Vérifié : 45 000 Datas → 6 cycles de base, 7 avec le bonus (+1 affiché), Trace 87 %, Data Wiper ×2 ; puis saisie à 12 000 Datas → 3 cycles, titre rouge, ligne Trace absente, détection enregistrée ; le bouton ouvre bien l'arbre.

**L'arbre de prestige est enfin accessible** (2026-08-29, vérifié en Play Mode via MCP) — les 119 nœuds et leurs 118 liens étaient construits au démarrage depuis toujours, mais `Canvas/PrestigeTree` était **inactif** et aucune ligne de code ne l'activait : `PrestigePanelView` n'avait ni `Show` ni `Hide`. Le panneau s'ouvre désormais par un bouton du header, se ferme par un bouton dans le panneau ou par **Échap**. **Consultable pendant une run** — décision de Bertrand : le joueur doit pouvoir planifier ses achats et revoir ce qu'il possède après une absence. **Le jeu ne se met PAS en pause** : la Trace continue de monter pendant la consultation, sans quoi le panneau serait exactement la planque gratuite que `runInBackground = true` existe pour interdire. L'arbre se construit une fois au démarrage, l'ouverture n'est qu'un `SetActive`. **Défaut trouvé au test** : le raccourci utilisait `Input.GetKeyDown`, alors que le projet est passé au nouvel Input System — une `InvalidOperationException` par frame ; basculé sur `Keyboard.current`. Deux fausses alertes écartées au passage : le libellé de fermeture se résout bien, ma lecture tombait dans la frame de l'activation ; et les « New Text » des nœuds sont dans le panneau masqué par le brouillard de guerre, invisibles au joueur. Vérifié : 595 textes dans l'arbre, **une seule** clé non résolue au moment de l'ouverture et résolue la frame suivante ; `timeScale` reste à 1 ; zéro erreur sur 502 frames.

**`GlowMaterialAssigner` retiré** (2026-08-28) — sa prémisse était fausse : le halo est une décision d'EMPHASE, pas une conséquence de la couleur. Deux textes de la même teinte méritent légitimement des traitements différents, ce que la passe manuelle de Bertrand a établi — les devises du header, par exemple, n'en ont pas besoin. L'outil aurait écrasé ce travail à la prochaine exécution : le garder était un piège. Sa passe initiale a servi à poser les matériaux en masse, c'était son seul usage légitime. Récupérable dans l'historique au commit `103cfd4` si un besoin de passe groupée revenait.

**Les halos ne se voyaient pas — deux causes distinctes** (2026-08-28, signalé par Bertrand) — les matériaux étaient bien assignés, mais réglés pour ne rien produire. **Texte** : `_GlowOuter` valait **0,05**, le halo ne débordait pratiquement pas du glyphe ; porté à 0,3. (Le mot-clé `GLOW_ON` était déjà actif — ma première lecture du YAML disait le contraire, à tort.) **Images** : le Bloom de la GameScene a un **seuil à 1**, or les trois `Mat_UIGlow_*` sortaient des couleurs ≤ 1, donc aucune ne franchissait le seuil ; leurs `_Color` sont passées en HDR à 2, ce qui fait enfin briller la jauge de Trace et les barres d'onglet. **Bouton Acheter** : le survol manquait purement et simplement, il n'avait pas été traité — ajouté sur le `BuyButton` de l'ActionPrefab, donc sur tous les clones. **Halo de contour retiré** des cinq survols : le shadergraph ne lisant pas la texture, il transformait le cadre en aplat plein. Le survol se limite désormais au fond à 10 %, qui est correct.

**Lot « Halos et états visuels »** (2026-08-28, vérifié en Play Mode via MCP) — première passe du rework UI, calquée sur la maquette `core_ui.html`. **`GlowMaterialAssigner`**, outil d'éditeur à deux entrées (rapport à blanc, puis application) : il apparie chaque texte au matériau de halo de SA couleur, règle dérivée de la teinte et non d'une liste de chemins. Possible parce que les trois matériaux ont un `_FaceColor` **blanc** — seul leur `_GlowColor` diffère, donc la couleur vient toujours du composant et un matériau mal apparié donnerait un texte rouge à halo vert. Il couvre les prefabs et les objets inactifs, saute les polices étrangères et les couleurs éteintes (les bordures à `#1A4D1A` ne brillent pas dans la maquette). Résultat : **14 textes de scène + 2 du prefab**, zéro mésappariement. **`TabButtonView`** : l'`ActiveBackground` et la `HighlightBar` existaient dans la scène mais **rien ne les allumait** — `ShowTab` ne basculait que les conteneurs. L'état actif suit maintenant la maquette : fond sombre, barre lumineuse, libellé vif à halo ; les onglets au repos passent en `#1A4D1A` sans halo. **`UiHoverGlow`** : ni Color Tint (qui ne sait que multiplier la couleur du targetGraphic, donc ne peut pas changer un matériau) ni Animation (un Animator et quatre clips par bouton, et une animation réécrit ses propriétés chaque frame, donc se battrait avec les presenters). Un composant qui ne touche QUE le matériau du contour et un fond à 10 % d'opacité — deux choses dont aucun presenter ne s'occupe. Le ColorTint est conservé pour le seul état grisé, son survol neutralisé. Un `Update` gardé par le survol éteint un bouton qui se grise SOUS le curseur, cas réel du Data Wiper qui part en recharge au clic. Vérifié : clic sur Hardware → l'onglet Scripts s'éteint et Hardware s'allume ; survol → halo et fond apparaissent sur Overclock (vert) et Terre Brûlée (rouge), **rien** sur Data Wiper et Ghost Cache qui sont grisés ; sortie → retour au repos ; grisage sous le curseur → extinction immédiate.

**Le panneau affichait les valeurs du niveau PRÉCÉDENT** (2026-08-27, constaté en jeu par Bertrand, corrigé et vérifié en Play Mode) — après le tout premier achat, `SCR_01` annonçait « Génère **0** Datas toutes les 1,5 s » alors que le modèle portait bien un rendement de 1. `UpgradeModel.LevelUp()` faisait `_currentLevel.Value++` PUIS `RecalculateCache()` : la notification R3 réveillait la vue immédiatement, et `BindLevel` lisait les caches avant leur mise à jour. Le rendement, le coût ET la durée étaient donc affichés avec un cran de retard — invisible au-delà du premier niveau, où l'écart devient un simple décalage, mais fatal au niveau 1 où la valeur précédente vaut zéro. Le niveau devient un champ `int` ordinaire, source de vérité de tous les calculs ; le `ReactiveProperty` ne sert plus qu'à notifier, et il est écrit EN DERNIER, une fois les caches cohérents — même principe que `RecalculateBonuses`. Vérifié : « Niv. 0 / Génère 0 Datas » avant achat, « Niv. 1 / Génère 1 Datas » après, et un cycle lancé à la main crédite bien 1 Data à chaque livraison.

**La profondeur de l'arbre de prestige est appliquée par le modèle** (2026-08-27, vérifié en Play Mode via MCP) — `TryPurchasePrestige` ne contrôlait que le niveau maximum et le coût : un appel direct achetait n'importe quel nœud sans ses parents, ce qui rendait toute la méta-progression facultative pour qui contournait le panneau. La règle vivait uniquement dans `PrestigeItemPresenter`, l'endroit le plus volatil du projet. Nouvelle méthode `PrestigeManager.IsUnlocked(config)`, appelée par l'achat **et** par la vue : celle-ci reflète désormais la règle au lieu de la redéfinir. Vérifié : le saut direct sur `P_OVERCLOCK_AWAKE` est refusé et **ne dépense rien** ; la chaêne complète s'achète normalement dans l'ordre, chaque maillon débloquant le suivant à l'achat ; `P_ROOT`, sans prérequis, reste accessible ; un nœud d'une autre branche (`P_EXPLOIT_MULT`) reste fermé ; le plafond de niveau tient toujours ; une config nulle rend `false` sans exception.

**Chaîne « Surtension » en amont de l'Injecteur** (2026-08-27, vérifié en Play Mode via MCP) — Bertrand a écrit `P_OVERCLOCK_POWER_1/2/3` et rétabli le prérequis de `P_OVERCLOCK_AWAKE`. Deux corrections de données : leur `bonusType` était `OverclockPowerMultiplier`, absent de l'enum — le générateur **rejette** un type inconnu, les trois nœuds n'auraient pas été créés et l'Injecteur se serait retrouvé sans parent, donc accessible d'emblée ; ils sont mappés sur `ClickPowerMultiplier`, qui EST déjà ce levier (bonus 0,5 / 1 / 2 par rang, une continuation de `P_CLICK`). Le `PrestigeTreeGenerator` **avertit désormais** quand un JSON porte encore un `nameKey`/`descKey`, comme le fait celui des upgrades depuis le 25/08 ; il a immédiatement signalé sept nœuds, dont les champs ont été retirés après vérification que la clé dérivée existait bien en base. Six clés de localisation ajoutées (Surtension I à III). Arbre régénéré : **119 nœuds, zéro avertissement**. Vérifié : la chaîne remonte à `P_ROOT`, et à fond le clic passe de 0,5 s à **11,75 s** (×23,5) — ⚠️ magnitude à surveiller à l'équilibrage, un clic avancerait alors une douzaine de cycles de `SCR_01` d'un coup.

**Lot « Injecteur Automatique » — le réveil de l'Overclock** (2026-08-27, vérifié en Play Mode via MCP) — `OverclockWakesScripts` rejoint `PrestigeBonusType`, appendé en fin d'enum comme les trois précédents. `ScriptCycleRunner.StartAllIdle()` démarre les Scripts possédés à l'arrêt et retourne leur nombre ; il ignore les automatisés, qui repartent seuls au Tick suivant et dont le décompte donnerait un retour trompeur. `TriggerManualOverclock` retourne désormais un `OverclockResult` — struct readonly, un clic ne doit rien allouer — portant les secondes ET les réveils, et le presenter choisit entre deux messages de console selon qu'il y a eu réveil ou non. **L'ordre est avance-puis-réveille** : l'inverse offrirait une demi-seconde d'avance aux Scripts qui viennent de démarrer, alors que le GDD parle de « ceux qui tournaient déjà ». **Donnée corrigée** : le nœud écrit par le GD déclarait le prérequis `P_OVERCLOCK_POWER_3`, qui n'existe dans aucun fichier — le générateur aurait laissé le nœud sans parent, donc accessible d'emblée, ce qui annulait le « positionnement tardif » ; rattaché à `P_POWER_START`, le nœud existant le plus profond de la branche Automatisation. Arbre régénéré : 116 nœuds, aucun prérequis cassé. Vérifié : sans le nœud, un clic donne +0,5 s et **0 réveil**, les trois Scripts restent à l'arrêt ; avec, le même clic en réveille **3** ; un second clic en réveille 0, tout tournant déjà.

**Les quatre boutons sont câblés et vérifiés** (2026-08-27, Play Mode via MCP) — le Data Wiper est le `PanicButton` de `Canvas/ConsoleLogs`, aux côtés du Ghost Cache, de l'Overclock et du Protocole Terre Brûlée (`PrestigeButton`). Les trois vues autonomes ont leur `Button`, leur libellé et leur `Image` en Filled, et les neuf champs du `GameSceneLifetimeScope` sont renseignés. États initiaux corrects sur une partie neuve, zéro warning au démarrage : Ghost Cache et Data Wiper affichent leur état verrouillé en nommant le nœud de prestige manquant, l'exfiltration sa jauge à 0 %.

**Lot « BalancingConfigSO »** (2026-08-27, vérifié en Play Mode via MCP) — les vingt réglages d'équilibrage étaient des `const` dans huit fichiers : le GD ne pouvait rien régler sans recompiler, et une session d'équilibrage coûtait une recompilation par essai. Tout vit désormais dans `Assets/GameData/Balancing/BalancingConfig.asset`, injecté par `RegisterInstance` depuis le `RootLifetimeScope`. **Modifiable en Play Mode** : les systèmes lisent les propriétés à l'usage plutôt que de les recopier au démarrage. `UpgradeModel` reçoit la config par constructeur — c'est un POCO créé à la main, il ne passe pas par le conteneur. Les statiques que lisaient les presenters (`GhostCacheSystem.CapacitySeconds`, `EmergencyProtocolSystem.TraceReduction`…) deviennent des propriétés d'instance, ce qui garde le SO inconnu de l'UI. **Défaut trouvé au test et corrigé** : `RequiredTFlops` était une chaîne R3 dérivée du compteur d'usages, donc régler le palier dans l'inspecteur restait sans effet jusqu'au prochain déclenchement — exactement ce que le lot devait rendre possible ; c'est maintenant une propriété calculée, et la vue écoute le compteur d'usages. **Second défaut** : créer l'asset et l'assigner à la scène dans le même appel d'éditeur écrivait `fileID: 0` — la référence n'existait pas encore à la sérialisation ; il faut recharger l'asset depuis son chemin avant de l'assigner. Vérifié à chaud en Play Mode : compression 0,05 → 0,5 fait tomber le cycle de `SCR_01` de 1 s à 0,214 s ; le multiplicateur de Trace de l'Exploit passe de ×10 à ×3 ; le palier du Data Wiper de 50 à 5 TFlops rend `HasEnoughPower` vrai immédiatement ; le seuil d'exfiltration suit `MoneyPerCpuCycle`. Il ne reste dans le code que le drapeau de triche `BypassUnlockCondition`, qui est une configuration de build et non un réglage, et deux garde-fous anti-division (`MinSafeDuration`, plancher à 1,01 du multiplicateur de coût).

**Lot « Data Wiper » — refonte du Bouton d'Urgence** (2026-08-27, vérifié en Play Mode via MCP) — le bouton ne coûte plus d'argent : il **exige** un palier de TFlops, qui grimpe ×3 par usage. Rien n'est dépensé — les TFlops sont une capacité dérivée du parc Hardware, il n'y a rien à en soustraire, et c'est ce point qui avait bloqué la première formulation du GD. Le coût réel est un **contrecoup** : 30 % des TFlops immobilisées 60 s, +10 points par usage, plafonné à 90 %. Comme les TFlops alimentent la dissipation des Proxies, purger la Trace affaiblit la défense juste après. Délai de 5 min entre activations. Effet : −20 points ABSOLUS de jauge. `EmergencyProtocolSystem` devient `ITickable` et perd sa dépendance à `UserCurrencies`. `SaveData` v5 : le contrecoup et le délai sont sauvegardés, contrairement à l'Overdrive — là-bas sauvegarder aurait mis un bonus en pause, ici ne pas sauvegarder ferait échapper à une pénalité. ⚠️ **Palier de base (50 TFlops) et escalade (×3) sont PROVISOIRES** : le GD a chiffré la progression, pas le palier lui-même ; le ×3 reprend l'ancien coût en argent. Le plafond de 90 % est également une décision de code, absente de la spécification. Vérifié : verrouillé sans `P_EMERG` ; refus à 48/50 TFlops ; déclenchement à 52 → Trace 0,63 → 0,43, TFlops 52 → 36,4, dissipation 163,5 → 154,4, cycle `SCR_01` 0,347 → 0,443 s ; second appel refusé ; palier 50 → 150, tranche suivante 40 % ; à l'échéance les TFlops reviennent à 52 ; `Restore(2 usages, 30 s)` réapplique bien 40 % et non 50 % ; migration v2 → v5 préserve `EmergencyUsesInRun` ; wipe remet tout à zéro sauf le déblocage de prestige.

**Lot « Nœuds de prestige de l'Exploit »** (2026-08-27, vérifié en Play Mode via MCP) — les trois nœuds écrits par le GD dans `01_Economy.json` sont branchés : `P_EXPLOIT_CHARGES` (5 rangs, +1 charge), `P_EXPLOIT_MULT` (10 rangs, +10 % de rendement) et `P_EXPLOIT_TRACE_REDUC` (5 rangs, −0,5 de malus). Trois valeurs **appendues en fin** de `PrestigeBonusType` : l'index est sérialisé dans les `.asset`, une insertion au milieu aurait redéfini tous les nœuds existants. Le Ghost Cache passe en **multi-charges** — capacité = rangs × 300 s, une seule charge consommée par déclenchement — et l'Exploit devient **verrouillé tant que `P_EXPLOIT_CHARGES` vaut 0**, avec un quatrième état de bouton qui nomme le nœud manquant plutôt que de rester gris. Rien ne s'accumule pendant le verrouillage. Surcharge `GetText` à 4 arguments ajoutée. **Deux défauts trouvés au test et corrigés** : le presenter ne s'abonnait qu'à `ExploitMaxCharges`, si bien qu'acheter les dix rangs de rendement laissait le bouton annoncer ×50 — remplaçé par un abonnement à `OnBonusesRecalculated`, qui couvre les trois nœuds ; et la jauge était forcée à 1 en état armé, masquant l'avancement de la charge suivante — c'est la couleur qui dit « armé », la barre montre désormais la suite. **Un troisième défaut d'affichage** : `RoundToInt(7,5)` faisait annoncer « trace x8 » pour un malus réel de 7,5 ; les valeurs passent brutes et le gabarit les met en forme. Vérifié : verrouillé → `Accumulate(500)` laisse la charge à 0 ; 3 rangs → 900 s de capacité et bouton débloqué immédiatement ; 420 s → 1 charge et jauge à 0,40 ; rangs max → « rendement x100 / trace x7,5 » ; déclenchement → `SCR_01` de 24 à 2 400 et réserve de 420 à 120 s ; wipe → charge à 0 mais capacité toujours à 3.

**Lot « Ghost Cache et Zéro-Day Exploit »** (2026-08-27, vérifié en Play Mode via MCP) — troisième volet de la proposition du GD, débloqué par ses quatre chiffres : capacité **300 s d'excédent**, durée **30 s**, **×50 sur le rendement**, **dissipation → 0**. L'excédent de dissipation, jusque-là jeté par `SimulationTicker`, alimente une charge unique. **La charge se remplit en TEMPS, pas en magnitude** — une seconde d'excédent vaut une seconde, qu'il soit de 1 ou de 100 000 — sans quoi un joueur de fin de partie remplirait la jauge instantanément. Le test d'accumulation est `debit < 0` et non `<= 0` : une partie sans aucun générateur donne un débit nul, et charger l'Exploit en ne faisant rien n'aurait aucun sens. **Le ×50 ne touche que les Scripts**, et cette restriction est le point délicat du lot : chez un Hardware, `GetCurrentYield()` EST sa contribution en TFlops — le laisser passer aurait multiplié par 50 la capacité de calcul, donc la compression des cycles ET la dissipation des Proxies, transformant un buff économique en invulnérabilité. Garde posée des deux côtés : le modèle refuse le multiplicateur hors Script, et le manager ne le pousse qu'aux Scripts. Le multiplicateur passe par les caches des modèles plutôt que par le versement, pour que le chiffre affiché dans le header et le montant crédité racontent la même chose. `SaveData` v4 (`GhostCacheSeconds`) : la charge survit à la fermeture du jeu mais pas au wipe. Un Overdrive **en cours** n'est pas sauvegardé. Vérifié en Play Mode : charge à 22 s avec la jauge de Trace toujours à 0 (l'excédent est capté sans que la règle d'or bouge) ; déclenchement → `SCR_01` passe de 5 à 250, durée inchangée, **HW_01 et les TFlops restés à 6** ; Trace qui monte à 0,0024/s pendant l'Exploit, soit exactement la trace Hardware sans aucune dissipation ; second déclenchement refusé ; à l'échéance le rendement revient à 5 et la jauge **reste à 0,072** au lieu de redescendre ; migration v2 → v4 en chaîne et aller-retour JSON corrects ; wipe → charge à 0.

**Lot « Proxies utiles »** (2026-08-27, vérifié en Play Mode via MCP) — les Proxies n'étaient atteints par aucun palier : rendement de base nul, pas de cycle. Deux ajouts, sur proposition du game designer. **`TraceMultiplier`**, troisième effet de palier, amplifie la magnitude de Trace — donc la dissipation pour un Proxy. Le sens dépend du type, comme le champ qu'il amplifie : le générateur avertit désormais pour tout palier inerte ou inversé (`DurationMultiplier` hors Script, `YieldMultiplier` sur rendement nul, `TraceMultiplier` > 1 hors Proxy). **Synergie** : chaque niveau de Proxy possédé accélère tous les Scripts de 1 %, via un multiplicateur dédié appliqué à la durée — pas branché sur les TFlops, ce qui aurait créé une boucle avec le `log10` de la dissipation. `GetTraceMagnitudePerSecond()` passe en cache : elle est appelée à chaque frame par `ScriptCycleRunner`, un parcours de paliers y serait payé soixante fois par seconde. Vérifié sur `PRX_01` : magnitude 13,5 au niveau 9 puis **30 au niveau 10** (×2), **225 au 25** (×3) et **2 250 au 50** (×5) ; 50 niveaux de Proxy donnent ×1,5 de vitesse et ramènent le cycle de `SCR_01` de 1,5 s à 1 s.

**Protocole Terre Brûlée — bouton câblé et testé de bout en bout** (2026-08-26) — l'affichage et l'interactivité sont désormais deux notions distinctes : `IsUnlocked` porte la condition de jeu et pilote le libellé et la jauge, `IsClickable` y ajoute la triche d'éditeur et ne pilote que le bouton. Sans cette séparation, l'éditeur affichait en permanence « [PRÊT] Gain : +0 Cycles CPU » — un libellé absurde — et la jauge de progression, masquée une fois débloquée, n'aurait jamais été observable pendant le développement. Vérifié en Play Mode sur le bouton réel : les trois états du GDD (`0 %`, `45 %`, `[PRÊT] +1 → Prochain à 4K`, `[PRÊT] +2 → Prochain à 9K`), le blocage des clics et le vidage du libellé pendant la séquence, les six lignes de purge rendues dans la console du jeu dans l'ordre, l'écran de fin qui n'apparaît qu'après, 2 cycles crédités, la run wipée et la sauvegarde écrite.

**Lot « 4b — Protocole Terre Brûlée, l'UI »** (2026-08-26, vérifié en Play Mode via MCP) — `ExfiltrationView` autonome (pas un champ de plus sur le Header, pour pouvoir déplacer le bouton sans toucher au code) et `ExfiltrationPresenter` qui porte les trois états et la séquence de purge. **La fin de run est découpée en deux temps** : `TryResolveVoluntaryExit` fige le résultat — gain crédité, run effacée, partie désarmée — puis `AnnounceRunEnded` déclenche l'écran de fin une fois la console déroulée. Jouer la séquence avant de figer aurait laissé la Trace monter pendant ~2 s : un joueur exfiltrant à 98 % pouvait se faire saisir au milieu de sa propre sortie et perdre ses +20 %. **Défaut trouvé au test et corrigé** : `HandleGameOver` pouvait s'exécuter sur une run déjà résolue, le `ThreatManager` émettant son lockdown sans savoir que la partie était finie — une saisie forcée pendant la séquence écrasait l'Effacement Propre et affichait l'écran par-dessus. Garde `if (!IsGameActive) return` ajoutée. Rafraîchissement du libellé filtré sur l'ENTIER de pourcent, sinon une chaîne serait allouée à chaque versement de cycle. La vue est enregistrée sous condition : sans elle, un warning explicite au lieu d'un boot cassé. Vérifié : temps 1 fige 18 cycles et laisse l'écran caché ; une saisie forcée pendant la séquence ne produit ni écran ni détection ; temps 2 affiche l'écran ; le gain est conservé.

**Lot « 4a — Protocole Terre Brûlée, la mécanique »** (2026-08-26, vérifié en Play Mode via MCP) — la sortie volontaire de run. Jusqu'ici seule la Trace à 100 % menait à l'écran de prestige : boucler la méta-progression imposait d'attendre de se faire prendre. `GameSessionManager.EndRun(multiplicateur)` factorise les deux fins ; `HandleGameOver` passe 1,0 et enregistre une détection, `TryEndRunVoluntarily` passe 1,2 et n'en enregistre pas — le joueur n'a jamais été pris. `ExfiltrationSystem` (scope racine) porte l'état réactif : `PendingCycles` avec `DistinctUntilChanged` — indispensable, l'argent bouge plusieurs fois par seconde quand l'entier ne change que quelques fois par run —, `IsUnlocked`, `ProgressToFirstCycle` et `GetNextCycleThreshold()`. Condition de déblocage unique : ≥ 1 CPU Cycle, soit 1 000 Datas de run. Contournement `#if UNITY_EDITOR || DEVELOPMENT_BUILD` pour enchaîner des runs de test. Vérifié : jauge 0 → 25 → 70 → 100 % puis 1 cycle pile à 1 000 Datas ; seuils suivants à 4 000 et 9 000 ; 250 000 Datas rapportent **18 cycles en sortie volontaire contre 15 en saisie** ; la run est wipée pareillement dans les deux cas ; `TryExfiltrate` sur une run terminée retourne false.

**Prestige calculé sur la run** (2026-08-26, vérifié en Play Mode via MCP) — décision de game design : le gain de CPU Cycles se base sur l'argent gagné pendant LA RUN, plus sur le cumul à vie. `UserCurrencies` gagne `RunMoneyGenerated`, remis à zéro par `WipeRun()` — après le calcul du gain, l'ordre compte. `SaveData` passe en **v3** avec le champ `RunMoney` dans le bloc de run, et la migration v2→v3 le force à zéro plutôt que de recopier le cumul, ce qui offrirait un gain immérité sur une run déjà entamée. Le compteur à vie reste, comme statistique. Vérifié : run à 250 000 $ → 15 cycles, compteur de run remis à 0 au Game Over, cumul à vie conservé ; run suivante à 4 000 $ → **2 cycles**, contre 15 sur l'ancien calcul.

**Lot « 3b — branchement des bonus de prestige »** (2026-08-26, vérifié en Play Mode via MCP) — les six bonus orphelins trouvent leurs consommateurs. `PrestigeManager` agrège les trois `SpecificUpgrade*` dans une table `Dictionary<string, SpecificUpgradeBonuses>` indexée par cible — les scalaires globaux ne pouvaient pas porter un bonus ne valant que pour `SCR_01` — et expose `OnBonusesRecalculated`, émis après CHAQUE recalcul. Ce signal est indispensable : `OnPrestigePurchased` n'est pas émis par `InitializeFromSave`, et le gateway restaure les générateurs AVANT le prestige, donc s'y abonner aurait fait perdre toute la méta-progression ciblée au chargement. Les bonus sont poussés dans les `UpgradeModel` (motif de `SetTFlops`), appliqués aux valeurs de BASE, avant les paliers, les TFlops et le plancher ; composition **additive**, bornée à 95 % pour qu'un générateur ne devienne jamais gratuit. Le PROD d'un Proxy boost sa DISSIPATION et surtout pas sa trace générée. `GlobalComputeMultiplier` s'applique aux TFlops, `CostMultiplierReduction` est enfin transmise par `TryPurchaseUpgrade`, `StartingMoney` s'AJOUTE à un plancher de 10 au lieu de le remplacer — sans quoi un joueur sans ce nœud repartait à zéro, incapable d'acheter son premier Script. **Ordre du reset de fin de run corrigé** : `WipeRun()` efface la run entière AVANT `OnSessionEnded`, sur lequel le `SaveScheduler` écrit. Vérifié : coût 10,7 → 9,63, rendement 1 → 1,1, durée 1,5 → 1,35, dissipation 1,5 → 1,65 (toutes exactes au rang 1) ; TFlops 2 → 2,3 avec ×1,15 ; coût 9,63 → 9,54 avec une réduction de multiplicateur de 0,01 ; Game Over provoqué → SCR_01 niveau 0, argent 10, jauge 0, et la capture du gateway rend 0 upgrade pour 7 nœuds de prestige conservés.

**Lot « 3a — données du prestige »** (2026-08-26, vérifié en Play Mode via MCP) — `PrestigeSpecificNodesGenerator` limite sa recherche à `Assets/GameData/Upgrades` : un `FindAssets` sur tout le projet ramassait `Data/Addressables/Upgrades/NewUpgradeConfig.asset`, un orphelin d'id « 000 », d'où trois nœuds ciblant une upgrade inexistante. Le triplet COST/PROD/TIME devient **conditionné par le type** : seuls les Scripts ont un cycle, donc générer un nœud de réduction de temps pour un Hardware ou un Proxy créait un piège à débutant — achetable, payé en CPU Cycles, sans le moindre effet. Le PROD des Proxies prend un sens : efficacité de **dissipation** (branchement au lot 3b). Résultat : 105 nœuds spécifiques au lieu de 138, tous utiles — 45 Script (triplet), 30 Hardware et 30 Proxy (COST/PROD). Vérifié : 112 entrées JSON = 112 dans le catalogue, 112 nœuds instanciés en scène, zéro libellé `[UPG_...]`.

**Lot « 2b-2 — la Trace »** (2026-08-26, vérifié en Play Mode via MCP) — la Trace change de propriétaire, parce qu'elle ne peut plus être une somme figée : sa part Script dépend de quels cycles tournent à la frame courante. `UpgradeManager` garde les parts **statiques** (`HardwareTracePerSecond`, `ProxyDissipationPerSecond`), recalculées à l'achat ; `ScriptCycleRunner` expose la part **dynamique** (`ActiveScriptTracePerSecond`), accumulée dans le parcours de slots qu'il fait déjà — un simple float, aucune allocation ; `SimulationTicker` combine et applique la réduction de prestige, qui trouve enfin son consommateur. **Bug de signe des Proxies corrigé** : `totalTrace +=` s'appliquait aux trois types, donc acheter un Proxy augmentait la Trace. Second rôle des TFlops branché : `dissipation = Σ(base × niveau) × (1 + log10(1 + TFlops))`. `GetCurrentTracePerSecond` renommée `GetTraceMagnitudePerSecond`, le sens du champ dépendant du type. Vérifié : Hardware 0,08/s continu dès l'achat ; dissipation d'1 Proxy à 2 TFlops = 2,215682 contre 2,2157 théorique, linéaire à 5 Proxies ; débit négatif = jauge figée ; Script possédé mais **à l'arrêt** ne génère rien (10 → 10), et sa trace n'apparaît qu'au lancement du cycle (10 → 12).

**Lot « 2b-1 — les TFlops »** (2026-08-26, vérifié en Play Mode via MCP) — les TFlops deviennent une **capacité dérivée** et non plus une monnaie : `UserCurrencies.ComputerPower` disparaît au profit de `UpgradeManager.TotalTFlops` (parc Hardware possédé + `StartingComputerPower`). `RecalculateTotals()` procède en **deux passes**, et l'ordre n'est pas négociable : le débit théorique d'un Script se calcule depuis sa durée de cycle, laquelle dépend des TFlops — tout sommer d'un coup utiliserait les durées de l'achat précédent. La capacité est poussée dans les modèles via `UpgradeModel.SetTFlops()`, ce qui invalide leur cache de durée : sans cette poussée, acheter un Hardware n'aurait jamais raccourci un cycle déjà en cache. `UpgradeManager` devient `IStartable` pour s'abonner à `StartingComputerPower`. **`SaveData` passe en v2** (`ComputerPower` et `TotalComputerPower` retirés, `case 1:` de migration ajouté). Vérifié : compression conforme à la formule (2 TFlops → 1,3636 s ; 20 TFlops → 0,75 s, soit l'exemple du GDD), plancher `MinCycleDuration` tenu à 10⁹ TFlops (0,2 s au lieu de 0), acheter un Script ne touche pas aux TFlops, acheter 10 HW_01 les porte à 20 et raccourcit SCR_01 en direct ; migration v1→v2 confirmée sur un vrai fichier de sauvegarde.

**Lot « Localisation B — affichage » (2026-08-25, vérifié en Play Mode via MCP)** — `PrestigeItemPresenter` compose nom et description des 162 nœuds ciblés à partir des 6 gabarits et de la clé de l'upgrade cible, sans dépendance nouvelle : la clé se dérive de `TargetUpgradeId`. La composition a lieu **une seule fois dans le constructeur**, pas dans `RefreshView()`. Un nœud « spécifique » sans `targetUpgradeId` déclenche une erreur explicite. `GeneratorView` gagne un `_descriptionText` facultatif, alimenté depuis `DisplayDescriptionKey`. **Bug d'arité corrigé** : les Scripts utilisent désormais `UI_GENERATES_DATAS_CYCLE` (2 arguments), les Hardware/Proxy gardent `UI_GENERATES_DATAS` (1 argument) — ajouter `{1}` à la clé commune aurait levé une `FormatException` sur deux onglets sur trois. Vérifié : 3 générateurs affichent « Phishing familial », « Overclocking du CPU familial », « Navigation Privée » ; 162 nœuds composés (« Effacement Temporel (Opti Temps) ») ; 7 nœuds réels en `[PRESTIGE_*_NAME]`, ce qui est le comportement attendu tant que leur lore n'est pas écrit ; zéro exception.

**Lot « Localisation A — socle » (2026-08-25)** — la convention de nommage devient exécutable via `LocalizationKeys` (`Core.Services.Localization`), point unique dont dépendent les deux générateurs. `UpgradeConfigSO._displayName` devient `_displayNameKey` (avec `FormerlySerializedAs`) et gagne `_displayDescriptionKey`. Les trois générateurs d'éditeur **dérivent** désormais les clés de l'id au lieu de les lire depuis le JSON — une désynchronisation donnée/traduction devient impossible. Les nœuds de prestige « spécifiques » reçoivent des clés vides : leur libellé est composé à l'affichage (lot B) à partir de 6 gabarits, au lieu de 324 entrées de traduction. Migration unique des 45 libellés français vers `fr.json`, et purge de tout texte affichable des JSON de `GameData` (45 champs `displayName`, 169 paires `nameKey`/`descKey`). Vérifié : compilation sans erreur, générateurs relancés (45 upgrades / 169 nœuds), assets porteurs des clés dérivées, zéro français résiduel dans les `.asset`, 74 clés chargées au boot.

**Lot « panneau de prestige » (2026-08-25, vérifié en Play Mode via MCP)** — ⚠️ **piège trouvé au test, à retenir : Unity n'appelle jamais `Awake()` sur un objet inactif dans la hiérarchie.** L'arbre est construit alors que `Canvas/PrestigeTree` est encore désactivé, donc `UILineConnection._rectTransform` et `PrestigeItemView._rectTransform`, résolus dans `Awake()`, restaient `null`. Conséquences : `DrawLine` levait une `NullReferenceException` au premier lien — ce qui interrompait `Start()` et donc tout l'arbre — et `SetNodePosition` était un no-op silencieux laissant les 169 nœuds empilés à l'origine. Les deux passent en **résolution paresseuse** (propriété `Rect`), jamais dans `Awake`. Tout composant dont une méthode est appelée pendant la construction d'un panneau masqué doit suivre ce modèle. Résultat vérifié : 169 nœuds, 168 liens, positions et rotations correctes, zéro exception. — double instanciation des nœuds supprimée (169 au lieu de 338) · repère des liens unifié en pixels via `PrestigePanelView.GridToPixels` · placement déplacé dans la vue (`SpawnNode(Vector2)`), un nœud ne peut plus rester à la position du prefab · `_gridCellSize` sorti en `[SerializeField]` · repli `GetComponent<RectTransform>()` dans `PrestigeItemView.Awake` · lambda `onClick` remplacée par une méthode nommée · `"MAX"` et `$"Niv. {n} / {max}"` passés en clés de loc · erreur explicite sur prérequis absent du catalogue · `LOG_MANUAL_OVERCLOCK` annonçait des `%` alors que le code passe des secondes.

Persistance complète (autosave, fermeture, événements, écriture atomique, versionnement, prestige enfin persisté) · moteur de cycles par Script · paliers et automatisation · `EmergencyProtocolSystem` en Singleton · `UpgradeManager` / `PrestigeManager` / `ThreatManager` passés `IDisposable` · `UpgradeModel` disposés à la reconstruction · désabonnement lambda de `UpgradePanelPresenter` · `CancellationToken` sur la localisation · LINQ retiré du runtime · cache de coût et `DistinctUntilChanged` sur les générateurs · log d'achat · valeur d'overclock réelle · encodage UTF-8 de `GameSceneLifetimeScope` · recopie des durées de cycle dans le générateur d'éditeur.
