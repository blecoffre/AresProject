using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.Editor
{
    /// <summary>
    /// Assigne à chaque texte le matériau de halo qui correspond à SA couleur.
    ///
    /// Pourquoi un outil plutôt qu'une sélection manuelle : la scène et les prefabs portent une
    /// soixantaine de TextMeshProUGUI, dont une partie n'existe qu'à l'exécution (lignes de
    /// console, lignes de générateur, nœuds de prestige). Les traiter à la main est long, et
    /// surtout le travail est à refaire à chaque élément ajouté.
    ///
    /// <b>La règle est dérivée de la couleur, pas d'une liste de chemins.</b> C'est possible
    /// parce que les trois matériaux ont un `_FaceColor` BLANC : seul leur `_GlowColor` diffère.
    /// La couleur du texte continue donc de venir du composant, et le matériau ne pilote que le
    /// halo — ce qui veut aussi dire qu'un matériau mal apparié donnerait un texte rouge cerné
    /// d'un halo vert. L'appariement par teinte est la seule façon de ne jamais se tromper.
    ///
    /// Un texte éteint ne reçoit RIEN : dans la maquette, les bordures et les libellés de coût
    /// (`#1A4D1A`) ne brillent pas. D'où le seuil de luminosité.
    ///
    /// Les Images ne sont volontairement pas touchées : dans la maquette, le halo d'un bouton
    /// est un état de SURVOL, pas son état de repos. Les deux seules images qui brillent en
    /// permanence — la jauge de Trace et les onglets actifs — sont déjà réglées à la main.
    /// </summary>
    public static class GlowMaterialAssigner
    {
        private const string FontFolder = "Assets/TextMesh Pro/Resources/Fonts & Materials/";
        private const string GreenPath = FontFolder + "ShareTechMono-Regular SDF Material_GreenGlow.mat";
        private const string OrangePath = FontFolder + "ShareTechMono-Regular SDF Material_OrangeGlow.mat";
        private const string RedPath = FontFolder + "ShareTechMono-Regular SDF Material_RedGlow.mat";

        private static readonly string[] PrefabPaths =
        {
            "Assets/Data/Prefabs/ActionPrefab.prefab",
            "Assets/Data/Prefabs/ConsoleLogPrefab.prefab"
        };

        /// <summary>
        /// Sous ce niveau de luminosité, un texte est considéré comme « éteint » et ne reçoit
        /// aucun halo. Cale sur la maquette : `#1A4D1A` vaut 0,30 et ne doit pas briller,
        /// `#4AF626` vaut 0,96 et doit briller.
        /// </summary>
        private const float MinValue = 0.5f;

        /// <summary>
        /// Sous ce niveau de saturation, la couleur n'a pas de teinte exploitable — blanc, gris,
        /// noir. Ces textes-là sont ceux dont la couleur est décidée à l'EXÉCUTION (lignes de
        /// console, libellés des boutons d'état) : leur matériau doit suivre le même chemin, pas
        /// être figé ici.
        /// </summary>
        private const float MinSaturation = 0.25f;

        [MenuItem("Tools/Core/Halos — Rapport (aucune modification)")]
        public static void Report() => Run(dryRun: true);

        [MenuItem("Tools/Core/Halos — Assigner les matériaux de texte")]
        public static void Apply() => Run(dryRun: false);

        private static void Run(bool dryRun)
        {
            Material green = Load(GreenPath);
            Material orange = Load(OrangePath);
            Material red = Load(RedPath);

            if (green == null || orange == null || red == null) return;

            var report = new StringBuilder();
            report.AppendLine(dryRun
                ? "<color=#FF9900>[Halos] RAPPORT</color> — rien n'a été modifié."
                : "<color=green>[Halos] APPLIQUÉ</color>");

            int changed = 0;
            int skipped = 0;

            // --- Scènes ouvertes -------------------------------------------------------------
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                report.AppendLine("— scène " + scene.name);
                bool touched = false;

                GameObject[] roots = scene.GetRootGameObjects();
                for (int r = 0; r < roots.Length; r++)
                {
                    // true : les objets INACTIFS comptent aussi. Le panneau de prestige et l'écran
                    // de fin sont désactivés au repos, et ce sont eux qu'on oublie à la main.
                    var texts = roots[r].GetComponentsInChildren<TextMeshProUGUI>(true);
                    for (int t = 0; t < texts.Length; t++)
                    {
                        if (Process(texts[t], green, orange, red, dryRun, report, ref skipped))
                        {
                            changed++;
                            touched = true;
                        }
                    }
                }

                if (touched && !dryRun) EditorSceneManager.MarkSceneDirty(scene);
            }

            // --- Prefabs ---------------------------------------------------------------------
            for (int p = 0; p < PrefabPaths.Length; p++)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(PrefabPaths[p]);
                if (root == null)
                {
                    report.AppendLine("  ⚠ prefab introuvable : " + PrefabPaths[p]);
                    continue;
                }

                report.AppendLine("— prefab " + System.IO.Path.GetFileName(PrefabPaths[p]));
                bool touched = false;

                var texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
                for (int t = 0; t < texts.Length; t++)
                {
                    if (Process(texts[t], green, orange, red, dryRun, report, ref skipped))
                    {
                        changed++;
                        touched = true;
                    }
                }

                // SaveAsPrefabAsset avant UnloadPrefabContents, sinon les modifications sont
                // perdues : le contenu chargé est une copie détachée de l'asset.
                if (touched && !dryRun) PrefabUtility.SaveAsPrefabAsset(root, PrefabPaths[p]);
                PrefabUtility.UnloadPrefabContents(root);
            }

            report.AppendLine();
            report.AppendLine(changed + " texte(s) à changer, " + skipped + " laissé(s) tels quels.");
            report.AppendLine("Les textes laissés tels quels sont soit déjà corrects, soit sans teinte "
                            + "exploitable — leur couleur est décidée à l'exécution, leur matériau doit "
                            + "l'être aussi.");

            if (!dryRun)
            {
                AssetDatabase.SaveAssets();
                report.AppendLine("⚠ Les scènes modifiées sont marquées « dirty » : à toi de les enregistrer.");
            }

            Debug.Log(report.ToString());
        }

        /// <summary>
        /// Traite un texte. Retourne true s'il a été modifié (ou le serait, en mode rapport).
        /// </summary>
        private static bool Process(
            TextMeshProUGUI text,
            Material green,
            Material orange,
            Material red,
            bool dryRun,
            StringBuilder report,
            ref int skipped)
        {
            // Un texte dont la police n'est pas celle du jeu n'a rien à faire ici : lui coller un
            // matériau ShareTechMono afficherait un atlas qui ne correspond pas à ses glyphes,
            // donc des caractères illisibles. Le cas existe — l'écran de fin est encore en
            // LiberationSans.
            if (text.font == null || !text.font.name.StartsWith("ShareTechMono"))
            {
                skipped++;
                report.AppendLine("    · " + Path(text) + " — police « "
                                + (text.font == null ? "aucune" : text.font.name) + " », ignoré");
                return false;
            }

            Material target = Resolve(text.color, green, orange, red);
            if (target == null)
            {
                skipped++;
                return false;
            }

            if (text.fontSharedMaterial == target)
            {
                skipped++;
                return false;
            }

            report.AppendLine("    → " + Path(text) + "  ["
                            + ColorUtility.ToHtmlStringRGB(text.color) + "]  "
                            + (text.fontSharedMaterial == null ? "aucun" : text.fontSharedMaterial.name)
                            + "  ⇒  " + target.name);

            if (dryRun) return true;

            Undo.RecordObject(text, "Assigner le matériau de halo");
            text.fontSharedMaterial = target;
            EditorUtility.SetDirty(text);
            return true;
        }

        /// <summary>
        /// Choisit le matériau d'après la TEINTE de la couleur. Retourne null quand la couleur
        /// est trop sombre ou trop désaturée pour qu'un halo ait un sens.
        /// </summary>
        private static Material Resolve(Color color, Material green, Material orange, Material red)
        {
            float h, s, v;
            Color.RGBToHSV(color, out h, out s, out v);

            if (v < MinValue || s < MinSaturation) return null;

            float degrees = h * 360f;

            // Rouge à cheval sur 0°, d'où les deux bornes.
            if (degrees >= 330f || degrees < 20f) return red;
            if (degrees >= 20f && degrees < 70f) return orange;
            if (degrees >= 70f && degrees < 170f) return green;

            // Bleus, violets : pas de matériau, et le signaler vaut mieux que de deviner.
            return null;
        }

        private static string Path(Component c)
        {
            var parts = new List<string>();
            Transform t = c.transform;
            while (t != null)
            {
                parts.Add(t.name);
                t = t.parent;
            }
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }

        private static Material Load(string path)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Debug.LogError("[Halos] Matériau introuvable : " + path
                             + ". L'outil ne peut rien faire tant que les trois matériaux ne sont "
                             + "pas à leur place attendue.");
            }
            return mat;
        }
    }
}
