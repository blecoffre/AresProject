using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Core.Editor.Balance
{
    /// <summary>
    /// Traceur de courbes minimal pour la fenêtre d'équilibrage.
    ///
    /// <b>L'échelle logarithmique n'est pas une option cosmétique.</b> Les Datas d'une campagne
    /// couvrent une dizaine d'ordres de grandeur : en linéaire, tout ce qui précède la dernière
    /// run s'écrase sur l'axe et la courbe ne montre rien. C'est précisément en regardant une
    /// progression par ordres de grandeur qu'on repère une explosion — celle de la cinquième
    /// minute serait apparue immédiatement.
    /// </summary>
    public static class BalanceChart
    {
        private static readonly Color Frame = new Color(1f, 1f, 1f, 0.18f);
        private static readonly Color Grid = new Color(1f, 1f, 1f, 0.07f);
        private static readonly Color Background = new Color(0f, 0f, 0f, 0.22f);

        private static readonly GUIStyle LabelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(1f, 1f, 1f, 0.55f) }
        };

        /// <summary>Points réutilisés d'un tracé à l'autre : OnGUI passe des dizaines de fois par seconde.</summary>
        private static readonly List<Vector3> Path = new List<Vector3>(512);

        /// <summary>
        /// Trace une série. <paramref name="values"/> porte les ordonnées, <paramref name="times"/>
        /// les abscisses ; les deux doivent avoir la même longueur.
        /// </summary>
        public static void Draw(Rect rect, string title, IReadOnlyList<float> times,
                                IReadOnlyList<double> values, Color color, bool logarithmic,
                                string unitSuffix = "")
        {
            EditorGUI.DrawRect(rect, Background);
            Handles.BeginGUI();
            Handles.color = Frame;
            Handles.DrawAAPolyLine(1f,
                new Vector3(rect.xMin, rect.yMin), new Vector3(rect.xMin, rect.yMax),
                new Vector3(rect.xMax, rect.yMax));

            if (times == null || values == null || times.Count < 2)
            {
                Handles.EndGUI();
                GUI.Label(new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, 16f),
                          $"{title} — pas assez de points", LabelStyle);
                return;
            }

            int count = Math.Min(times.Count, values.Count);

            // Bornes. En log on travaille sur log10(1 + v) : la valeur 0 reste représentable,
            // ce qui évite un trou au démarrage de chaque run.
            double minY = double.MaxValue;
            double maxY = double.MinValue;
            for (int i = 0; i < count; i++)
            {
                double v = Transform(values[i], logarithmic);
                if (v < minY) minY = v;
                if (v > maxY) maxY = v;
            }

            float maxX = times[count - 1];
            if (maxX <= 0f) maxX = 1f;
            double span = maxY - minY;
            if (span <= 0d) span = 1d;

            // Trois lignes de grille horizontales : assez pour situer, pas assez pour encombrer.
            Handles.color = Grid;
            for (int g = 1; g <= 3; g++)
            {
                float y = Mathf.Lerp(rect.yMax, rect.yMin, g / 4f);
                Handles.DrawAAPolyLine(1f, new Vector3(rect.xMin, y), new Vector3(rect.xMax, y));
            }

            Path.Clear();
            for (int i = 0; i < count; i++)
            {
                float x = Mathf.Lerp(rect.xMin, rect.xMax, times[i] / maxX);
                double normalized = (Transform(values[i], logarithmic) - minY) / span;
                float y = Mathf.Lerp(rect.yMax, rect.yMin, (float)normalized);
                Path.Add(new Vector3(x, y));
            }

            Handles.color = color;
            Handles.DrawAAPolyLine(2f, Path.ToArray());
            Handles.EndGUI();

            GUI.Label(new Rect(rect.x + 6f, rect.y + 3f, rect.width - 12f, 16f),
                      $"{title}{(logarithmic ? "  (échelle log)" : string.Empty)}", LabelStyle);

            double realMax = values[0];
            for (int i = 1; i < count; i++)
            {
                if (values[i] > realMax) realMax = values[i];
            }

            GUI.Label(new Rect(rect.x + 6f, rect.yMax - 17f, rect.width - 12f, 16f),
                      $"max {Format(realMax)}{unitSuffix}   ·   {maxX / 60f:0.0} min", LabelStyle);
        }

        private static double Transform(double value, bool logarithmic)
        {
            return logarithmic ? Math.Log10(1d + Math.Max(0d, value)) : value;
        }

        private static string Format(double value)
        {
            if (value >= 1e6d) return value.ToString("0.00e+00");
            if (value >= 1000d) return value.ToString("N0");
            return value.ToString("0.##");
        }
    }
}
