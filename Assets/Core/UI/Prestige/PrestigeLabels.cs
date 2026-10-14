using Core.Models.Economy;
using Core.Services.Economy;
using Core.Services.Localization;
using UnityEngine;

namespace Core.UI.Prestige
{
    /// <summary>
    /// Compose les libellés d'un nœud de prestige : son nom, son lore, et la phrase qui décrit
    /// son effet.
    ///
    /// Statique et partagée parce que DEUX presenters en ont besoin — celui du nœud dans l'arbre
    /// et celui de l'inspecteur. Dupliquer la table des gabarits garantissait qu'elles
    /// divergeraient au premier ajout de type de bonus.
    ///
    /// Les clés sont des littéraux dans un switch, jamais une chaîne construite depuis le nom de
    /// l'enum : c'est ce qui les rend greppables, et surtout visibles par le
    /// LocalizationAnalyzerWindow qui, lui, ne sait pas exécuter du code.
    /// </summary>
    public static class PrestigeLabels
    {
        /// <summary>
        /// Le nom du nœud, par sa clé dérivée de l'id. Depuis le retrait des 120 nœuds ciblés
        /// (2026-10-14), plus aucun libellé n'est composé à partir d'un gabarit.
        /// </summary>
        public static string ResolveName(PrestigeConfigSO config, ILocalizationService loc)
        {
            return loc.GetText(config.DisplayNameKey);
        }

        public static string ResolveDescription(PrestigeConfigSO config, ILocalizationService loc)
        {
            return loc.GetText(config.DisplayDescriptionKey);
        }

        /// <summary>
        /// La phrase d'effet, apport PAR NIVEAU. Deux familles de mise en forme cohabitent et
        /// c'est irréductible : un multiplicateur se lit en pourcentage, un déblocage n'a aucun
        /// nombre à afficher. Le gabarit et le formatage sont choisis ensemble, ici, plutôt que
        /// laissés au fichier de traduction.
        /// </summary>
        public static string ResolveEffect(PrestigeConfigSO config, ILocalizationService loc)
        {
            float perLevel = config.BonusPerLevel;

            switch (config.BonusType)
            {
                // ---- Familles en pourcentage ------------------------------------------------
                case PrestigeBonusType.GlobalComputeMultiplier:
                    return loc.GetText("UI_PRESTIGE_EFFECT_COMPUTE", AsPercent(perLevel));

                // Un retrait au MULTIPLICATEUR de coût : 0,01 fait passer ×1,15 à ×1,14, soit un
                // point de hausse en moins à chaque niveau acheté.
                case PrestigeBonusType.CostMultiplierReduction:
                    return loc.GetText("UI_PRESTIGE_EFFECT_COSTINFLATION", AsPercent(perLevel));

                // Les branches du compte à rebours. Le Blindage divise la vitesse de l'A.M.I. par
                // (1 + Σ) : un apport de 0,5 fait donc tenir la jauge 50 % plus longtemps, et la
                // lecture en pourcentage reste juste en cumulé (Σ = 10, ×11, +1 000 %).
                case PrestigeBonusType.HardenBonus:
                    return loc.GetText("UI_PRESTIGE_EFFECT_HARDEN", AsPercent(perLevel));

                case PrestigeBonusType.InterceptBonus:
                    return loc.GetText("UI_PRESTIGE_EFFECT_INTERCEPT", AsPercent(perLevel));

                case PrestigeBonusType.CleanExitBonusMultiplier:
                    return loc.GetText("UI_PRESTIGE_EFFECT_CLEAN_EXIT", AsPercent(perLevel));

                // Des SECONDES d'avance, ajoutées à celles de base : pas un pourcentage.
                case PrestigeBonusType.OverclockBonus:
                    return loc.GetText("UI_PRESTIGE_EFFECT_OVERCLOCK", perLevel);

                // ---- Sans nombre -------------------------------------------------------------
                case PrestigeBonusType.UnlockEmergencyButton:
                    return loc.GetText("UI_PRESTIGE_EFFECT_UNLOCK_EMERGENCY");

                case PrestigeBonusType.UnlockHighRiskExtraction:
                    return loc.GetText("UI_PRESTIGE_EFFECT_UNLOCK_HIGHRISK");

                // Le puits à points n'a d'effet que par le Noyau, dont la ligne en tête de
                // l'écran donne déjà les chiffres.
                case PrestigeBonusType.CoreOverride:
                    return loc.GetText("UI_PRESTIGE_EFFECT_CORE_OVERRIDE");

                case PrestigeBonusType.Root:
                    return loc.GetText("UI_PRESTIGE_EFFECT_ROOT");

                default:
                    // Un type de bonus ajouté à l'enum sans gabarit ici afficherait du vide dans
                    // l'inspecteur, sans rien casser : on le dit au développeur, pas au joueur.
                    Debug.LogError(
                        "[PRESTIGE] Aucun gabarit d'effet pour le type de bonus '" + config.BonusType +
                        "' (nœud '" + config.Id + "'). L'inspecteur restera muet sur son effet.");

                    return string.Empty;
            }
        }

        /// <summary>
        /// La ligne du Noyau Compilé, « Noyau : coûts −66 %, rendement ×2,07 », affichée en tête du
        /// panneau d'upgrades et de l'arbre. Chaîne VIDE tant qu'aucun point n'est dépensé : la
        /// ligne est alors masquée, « −0 % / ×1,00 » parlerait d'un système que le joueur ne
        /// connaît pas encore (Bertrand, 2026-10-14).
        ///
        /// Partagée ici pour que les deux panneaux ne puissent pas l'écrire différemment.
        /// </summary>
        public static string ResolveCoreSummary(PrestigeManager prestige, ILocalizationService loc)
        {
            if (prestige.TotalPointsSpent.CurrentValue <= 0) return string.Empty;

            float costReduction = AsPercent(1f - prestige.TotalCostMultiplier.CurrentValue);
            return loc.GetText("UI_CORE_SUMMARY", costReduction, prestige.TotalYieldBonus.CurrentValue);
        }

        /// <summary>
        /// 0,15 devient 15. Le symbole « % » vit dans le fichier de traduction, pas ici — et le
        /// gabarit garde deux décimales facultatives : un bonus de 7,5 % ne doit pas s'annoncer 8 %.
        /// </summary>
        private static float AsPercent(float fraction)
        {
            return fraction * 100f;
        }
    }
}
