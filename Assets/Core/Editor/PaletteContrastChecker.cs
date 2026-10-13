using Core.UI.Theme;
using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Core.Editor
{
    /// <summary>
    /// Vérifie que chaque palette reste lisible pour le mode de vision qu'elle sert.
    ///
    /// Pour chaque groupe de couleurs AFFICHÉES ENSEMBLE (états d'un nœud, paliers de la jauge…),
    /// simule la vision du mode (matrices de Machado, Oliveira et Fernandes, 2009, sévérité
    /// maximale) puis mesure l'écart perçu entre chaque paire (CIEDE2000). Chaque couleur est
    /// aussi comparée au vert terminal du décor, sauf celles qui SONT ce vert à dessein.
    ///
    /// Seuil : ΔE 20. En dessous, deux états se confondent pour le joueur concerné. La palette
    /// Standard est mesurée en vision normale : elle documente l'existant, elle n'est pas exigée.
    ///
    /// Second contrôle, valable pour TOUTES les palettes, Standard comprise : le contraste WCAG
    /// du texte des nœuds et de l'inspecteur sur leur fond (4,5:1 minimum). Ce n'est pas une
    /// question de daltonisme mais de lisibilité pour tous — écran en plein soleil, basse vision.
    /// L'état « au max » est exclu : son texte est écrit en négatif sur un fond plein.
    /// </summary>
    public static class PaletteContrastChecker
    {
        private const float Threshold = 20f;
        private const float MinTextContrast = 4.5f;

        // Fond des nœuds (PrestigeItemView._nodeBackgroundColor) et dose de teinte des états
        // « trop cher » et « en cours » (_subtleTintAmount). À garder alignés sur le prefab.
        private static readonly Color NodeBackground = new Color(0.012f, 0.027f, 0.012f, 1f);
        private const float SubtleTint = 0.12f;
        private static readonly Color TerminalGreen = new Color(0.29f, 0.96f, 0.15f, 1f);
        private static readonly Color Background = new Color(0.02f, 0.02f, 0.02f, 1f);

        private enum Vision { Normal, Protan, Deutan, Tritan }

        // Machado 2009, sévérité 1.0, appliquées en RGB linéaire.
        private static readonly float[,] Protan =
        {
            { 0.152286f, 1.052583f, -0.204868f }, { 0.114503f, 0.786281f, 0.099216f }, { -0.003882f, -0.048116f, 1.051998f },
        };
        private static readonly float[,] Deutan =
        {
            { 0.367322f, 0.860646f, -0.227968f }, { 0.280085f, 0.672501f, 0.047413f }, { -0.011820f, 0.042940f, 0.968881f },
        };
        private static readonly float[,] Tritan =
        {
            { 1.255528f, -0.076749f, -0.178779f }, { -0.078411f, 0.930809f, 0.147602f }, { 0.004733f, 0.691367f, 0.303900f },
        };

        [MenuItem("Tools/Core/Vérifier les palettes (daltonisme)")]
        public static void Run()
        {
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(UiPaletteCatalogSO));
            if (guids.Length == 0)
            {
                Debug.LogError("[PALETTE] Aucun UiPaletteCatalogSO dans le projet.");
                return;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<UiPaletteCatalogSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
            int failures = 0;
            var report = new StringBuilder("[PALETTE] Écart minimal par groupe (CIEDE2000, seuil " + Threshold + ")\n");

            failures += Check(report, "Standard", catalog.Standard, Vision.Normal);
            failures += Check(report, "Protanopie", catalog.Protanopia, Vision.Protan);
            failures += Check(report, "Deutéranopie", catalog.Deuteranopia, Vision.Deutan);
            failures += Check(report, "Tritanopie", catalog.Tritanopia, Vision.Tritan);

            if (failures == 0) Debug.Log(report.Append("Toutes les palettes passent.").ToString());
            else Debug.LogWarning(report.Append(failures + " groupe(s) sous le seuil.").ToString());
        }

        private static int Check(StringBuilder report, string name, UiPaletteSO p, Vision vision)
        {
            if (p == null)
            {
                report.Append("  ").Append(name).Append(" : palette absente du catalogue\n");
                return 1;
            }

            report.Append("  ").Append(name).Append(" (").Append(p.name).Append(", vision ").Append(vision).Append(")\n");

            int failures = 0;
            failures += Group(report, vision, "nœuds", p.NodeLocked, p.NodeTooExpensive, p.NodeAffordable, p.NodeInProgress, p.NodeMaxed);
            failures += Group(report, vision, "inspecteur", p.InspectorLocked, p.InspectorTooExpensive, p.InspectorAffordable, p.InspectorInProgress, p.InspectorMaxed);
            failures += Group(report, vision, "liens", p.LinkLocked, p.LinkUnlocked, p.LinkCompleted);
            failures += Group(report, vision, "paliers", p.TierReached, p.TierUncertain, p.TierUpcoming, p.TierLocked);
            failures += Group(report, vision, "fin de run", p.RunEndSeized, p.RunEndCleanExit);
            failures += Group(report, vision, "urgence", p.EmergencyUnavailable, p.EmergencyReady, p.EmergencyBlocked);
            failures += Group(report, vision, "console", p.ConsoleSuccess, p.ConsoleNarrative, p.ConsoleStandard);

            // Contraste du texte : exigé dans toutes les palettes, Standard comprise.
            int textFailures = TextContrast(report, "texte nœuds", p.NodeLocked, p.NodeTooExpensive, p.NodeAffordable, p.NodeInProgress)
                             + TextContrast(report, "texte inspecteur", p.InspectorLocked, p.InspectorTooExpensive, p.InspectorAffordable, p.InspectorInProgress);

            // La palette Standard décrit l'existant : elle est mesurée, pas exigée.
            return (vision == Vision.Normal ? 0 : failures) + textFailures;
        }

        /// <summary>
        /// Ordre attendu : verrouillé, trop cher, abordable, en cours — les deux du milieu-bas
        /// portent la teinte légère sur leur fond, ce qui réduit leur contraste.
        /// </summary>
        private static int TextContrast(StringBuilder report, string label, Color locked, Color tooExpensive, Color affordable, Color inProgress)
        {
            float worst = Mathf.Min(
                Mathf.Min(WcagContrast(locked, NodeBackground), WcagContrast(affordable, NodeBackground)),
                Mathf.Min(WcagContrast(tooExpensive, Color.Lerp(NodeBackground, tooExpensive, SubtleTint)),
                          WcagContrast(inProgress, Color.Lerp(NodeBackground, inProgress, SubtleTint))));

            bool ok = worst >= MinTextContrast;
            report.Append("    ").Append(ok ? "ok   " : "TROP ").Append(label.PadRight(16))
                  .Append(" contraste WCAG minimal ").Append(worst.ToString("0.0")).Append(":1").Append('\n');
            return ok ? 0 : 1;
        }

        private static float WcagContrast(Color a, Color b)
        {
            float la = RelativeLuminance(a), lb = RelativeLuminance(b);
            return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
        }

        private static float RelativeLuminance(Color c) =>
            0.2126f * Mathf.GammaToLinearSpace(c.r) + 0.7152f * Mathf.GammaToLinearSpace(c.g) + 0.0722f * Mathf.GammaToLinearSpace(c.b);

        private static int Group(StringBuilder report, Vision vision, string label, params Color[] colors)
        {
            float worst = float.MaxValue;

            for (int i = 0; i < colors.Length; i++)
            {
                for (int j = i + 1; j < colors.Length; j++)
                {
                    // Deux champs volontairement identiques (« en cours » et « au max » partagent
                    // le vert) : l'état est porté ailleurs, par le fond plein du nœud.
                    if (colors[i] == colors[j]) continue;
                    worst = Mathf.Min(worst, DeltaE(colors[i], colors[j], vision));
                }
            }

            float worstVsGreen = float.MaxValue;
            for (int i = 0; i < colors.Length; i++)
            {
                // Une couleur qui EST le vert terminal (à ΔE < 15 en vision normale) l'est à dessein.
                if (DeltaE(colors[i], TerminalGreen, Vision.Normal) < 15f) continue;
                worstVsGreen = Mathf.Min(worstVsGreen, DeltaE(colors[i], TerminalGreen, vision));
            }

            float min = Mathf.Min(worst, worstVsGreen);
            bool ok = min >= Threshold;

            report.Append("    ").Append(ok ? "ok   " : "TROP ").Append(label.PadRight(12))
                  .Append(" entre eux ").Append(Format(worst))
                  .Append(" | contre le vert du décor ").Append(Format(worstVsGreen)).Append('\n');

            return ok ? 0 : 1;
        }

        private static string Format(float value) => value == float.MaxValue ? "  —" : value.ToString("0").PadLeft(3);

        // ------------------------------------------------------------------ couleur

        private static float DeltaE(Color a, Color b, Vision vision) =>
            Ciede2000(ToLab(Simulate(Composite(a), vision)), ToLab(Simulate(Composite(b), vision)));

        /// <summary>Une couleur translucide est vue posée sur le fond du terminal.</summary>
        private static Color Composite(Color c) => Color.Lerp(Background, new Color(c.r, c.g, c.b, 1f), c.a);

        private static Vector3 Simulate(Color c, Vision vision)
        {
            var lin = new Vector3(Mathf.GammaToLinearSpace(c.r), Mathf.GammaToLinearSpace(c.g), Mathf.GammaToLinearSpace(c.b));
            float[,] m = vision switch
            {
                Vision.Protan => Protan,
                Vision.Deutan => Deutan,
                Vision.Tritan => Tritan,
                _ => null,
            };
            if (m == null) return lin;

            return new Vector3(
                Mathf.Clamp01(m[0, 0] * lin.x + m[0, 1] * lin.y + m[0, 2] * lin.z),
                Mathf.Clamp01(m[1, 0] * lin.x + m[1, 1] * lin.y + m[1, 2] * lin.z),
                Mathf.Clamp01(m[2, 0] * lin.x + m[2, 1] * lin.y + m[2, 2] * lin.z));
        }

        private static Vector3 ToLab(Vector3 lin)
        {
            float x = (0.4124f * lin.x + 0.3576f * lin.y + 0.1805f * lin.z) / 0.9505f;
            float y = 0.2126f * lin.x + 0.7152f * lin.y + 0.0722f * lin.z;
            float z = (0.0193f * lin.x + 0.1192f * lin.y + 0.9505f * lin.z) / 1.089f;
            float fx = F(x), fy = F(y), fz = F(z);
            return new Vector3(116f * fy - 16f, 500f * (fx - fy), 200f * (fy - fz));

            static float F(float t) => t > 0.008856f ? Mathf.Pow(t, 1f / 3f) : 7.787f * t + 16f / 116f;
        }

        private static float Ciede2000(Vector3 lab1, Vector3 lab2)
        {
            double l1 = lab1.x, a1 = lab1.y, b1 = lab1.z, l2 = lab2.x, a2 = lab2.y, b2 = lab2.z;
            double c1 = Math.Sqrt(a1 * a1 + b1 * b1), c2 = Math.Sqrt(a2 * a2 + b2 * b2), cb = (c1 + c2) / 2;
            double g = 0.5 * (1 - Math.Sqrt(Math.Pow(cb, 7) / (Math.Pow(cb, 7) + Math.Pow(25, 7))));
            double a1p = (1 + g) * a1, a2p = (1 + g) * a2;
            double c1p = Math.Sqrt(a1p * a1p + b1 * b1), c2p = Math.Sqrt(a2p * a2p + b2 * b2);
            double h1p = Deg(Math.Atan2(b1, a1p)), h2p = Deg(Math.Atan2(b2, a2p));

            double dLp = l2 - l1, dCp = c2p - c1p, dhp;
            if (c1p * c2p == 0) dhp = 0;
            else if (Math.Abs(h2p - h1p) <= 180) dhp = h2p - h1p;
            else dhp = h2p > h1p ? h2p - h1p - 360 : h2p - h1p + 360;
            double dHp = 2 * Math.Sqrt(c1p * c2p) * Math.Sin(Rad(dhp / 2));

            double lbp = (l1 + l2) / 2, cbp = (c1p + c2p) / 2, hbp;
            if (c1p * c2p == 0) hbp = h1p + h2p;
            else if (Math.Abs(h1p - h2p) <= 180) hbp = (h1p + h2p) / 2;
            else hbp = h1p + h2p < 360 ? (h1p + h2p + 360) / 2 : (h1p + h2p - 360) / 2;

            double t = 1 - 0.17 * Math.Cos(Rad(hbp - 30)) + 0.24 * Math.Cos(Rad(2 * hbp))
                       + 0.32 * Math.Cos(Rad(3 * hbp + 6)) - 0.20 * Math.Cos(Rad(4 * hbp - 63));
            double dTheta = 30 * Math.Exp(-Math.Pow((hbp - 275) / 25, 2));
            double rc = 2 * Math.Sqrt(Math.Pow(cbp, 7) / (Math.Pow(cbp, 7) + Math.Pow(25, 7)));
            double sl = 1 + 0.015 * Math.Pow(lbp - 50, 2) / Math.Sqrt(20 + Math.Pow(lbp - 50, 2));
            double sc = 1 + 0.045 * cbp, sh = 1 + 0.015 * cbp * t, rt = -Math.Sin(Rad(2 * dTheta)) * rc;

            return (float)Math.Sqrt(Math.Pow(dLp / sl, 2) + Math.Pow(dCp / sc, 2) + Math.Pow(dHp / sh, 2)
                                    + rt * (dCp / sc) * (dHp / sh));

            static double Deg(double r) { double d = r * 180 / Math.PI; return d < 0 ? d + 360 : d; }
            static double Rad(double d) => d * Math.PI / 180;
        }
    }
}
