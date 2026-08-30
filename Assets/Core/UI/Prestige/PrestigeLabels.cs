using Core.Models.Economy;
using Core.Services.Localization;
using Core.Utils;
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
        /// Les nœuds ciblant une upgrade précise n'ont pas de nom en base : on compose
        /// « &lt;nom de l'upgrade&gt; (Opti Coût) » depuis un gabarit localisé. Les autres portent
        /// leur propre clé, dérivée de leur id par le générateur.
        /// </summary>
        public static string ResolveName(PrestigeConfigSO config, ILocalizationService loc)
        {
            switch (config.BonusType)
            {
                case PrestigeBonusType.SpecificUpgradeCostReduction:
                    return loc.GetText(LocalizationKeys.PrestigeSpecificCostName, TargetName(config, loc));

                case PrestigeBonusType.SpecificUpgradeYieldBoost:
                    return loc.GetText(LocalizationKeys.PrestigeSpecificYieldName, TargetName(config, loc));

                case PrestigeBonusType.SpecificUpgradeTimeReduction:
                    return loc.GetText(LocalizationKeys.PrestigeSpecificTimeName, TargetName(config, loc));

                case PrestigeBonusType.SpecificUpgradeAutomationTresholdReduction:
                    return loc.GetText(LocalizationKeys.PrestigeAutomationName, TargetName(config, loc));

                default:
                    return loc.GetText(config.DisplayNameKey);
            }
        }

        public static string ResolveDescription(PrestigeConfigSO config, ILocalizationService loc)
        {
            switch (config.BonusType)
            {
                case PrestigeBonusType.SpecificUpgradeCostReduction:
                    return loc.GetText(LocalizationKeys.PrestigeSpecificCostDescription, TargetName(config, loc));

                case PrestigeBonusType.SpecificUpgradeYieldBoost:
                    return loc.GetText(LocalizationKeys.PrestigeSpecificYieldDescription, TargetName(config, loc));

                case PrestigeBonusType.SpecificUpgradeTimeReduction:
                    return loc.GetText(LocalizationKeys.PrestigeSpecificTimeDescription, TargetName(config, loc));

                case PrestigeBonusType.SpecificUpgradeAutomationTresholdReduction:
                    return loc.GetText(LocalizationKeys.PrestigeAutomationDescription, TargetName(config, loc));

                default:
                    return loc.GetText(config.DisplayDescriptionKey);
            }
        }

        /// <summary>
        /// La phrase d'effet, apport PAR NIVEAU. Trois familles de mise en forme cohabitent et
        /// c'est irréductible : un multiplicateur se lit en pourcentage, des fonds de départ en
        /// monnaie, et un déblocage n'a aucun nombre à afficher. Le gabarit et le formatage sont
        /// choisis ensemble, ici, plutôt que laissés au fichier de traduction.
        /// </summary>
        public static string ResolveEffect(PrestigeConfigSO config, ILocalizationService loc)
        {
            float perLevel = config.BonusPerLevel;

            switch (config.BonusType)
            {
                // ---- Familles en pourcentage ------------------------------------------------
                case PrestigeBonusType.GlobalComputeMultiplier:
                    return loc.GetText("UI_PRESTIGE_EFFECT_COMPUTE", AsPercent(perLevel));

                case PrestigeBonusType.TraceReduction:
                    return loc.GetText("UI_PRESTIGE_EFFECT_TRACE", AsPercent(perLevel));

                case PrestigeBonusType.ClickPowerMultiplier:
                    return loc.GetText("UI_PRESTIGE_EFFECT_CLICK", AsPercent(perLevel));

                case PrestigeBonusType.CostMultiplierReduction:
                    return loc.GetText("UI_PRESTIGE_EFFECT_COSTINFLATION", AsPercent(perLevel));

                case PrestigeBonusType.ExploitYieldBoost:
                    return loc.GetText("UI_PRESTIGE_EFFECT_EXPLOIT_YIELD", AsPercent(perLevel));

                case PrestigeBonusType.SpecificUpgradeCostReduction:
                    return loc.GetText("UI_PRESTIGE_EFFECT_SPECIFIC_COST", AsPercent(perLevel), TargetName(config, loc));

                case PrestigeBonusType.SpecificUpgradeYieldBoost:
                    return loc.GetText("UI_PRESTIGE_EFFECT_SPECIFIC_YIELD", AsPercent(perLevel), TargetName(config, loc));

                case PrestigeBonusType.SpecificUpgradeTimeReduction:
                    return loc.GetText("UI_PRESTIGE_EFFECT_SPECIFIC_TIME", AsPercent(perLevel), TargetName(config, loc));

                case PrestigeBonusType.SpecificUpgradeAutomationTresholdReduction:
                    return loc.GetText("UI_PRESTIGE_EFFECT_AUTOMATION", perLevel, TargetName(config, loc));

                // ---- Familles en valeur absolue ---------------------------------------------
                case PrestigeBonusType.StartingMoney:
                    return loc.GetText("UI_PRESTIGE_EFFECT_START_MONEY", CurrencyFormatter.Format(perLevel));

                case PrestigeBonusType.StartingComputerPower:
                    return loc.GetText("UI_PRESTIGE_EFFECT_START_TFLOPS", CurrencyFormatter.Format(perLevel));

                case PrestigeBonusType.UnlockExploitCharges:
                    return loc.GetText("UI_PRESTIGE_EFFECT_EXPLOIT_CHARGES", Mathf.RoundToInt(perLevel));

                case PrestigeBonusType.ExploitTracePenaltyReduction:
                    return loc.GetText("UI_PRESTIGE_EFFECT_EXPLOIT_TRACE", perLevel);

                // ---- Déblocages : aucun nombre n'a de sens -----------------------------------
                case PrestigeBonusType.UnlockEmergencyButton:
                    return loc.GetText("UI_PRESTIGE_EFFECT_UNLOCK_EMERGENCY");

                case PrestigeBonusType.OverclockWakesScripts:
                    return loc.GetText("UI_PRESTIGE_EFFECT_OVERCLOCK_WAKE");

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
        /// 0,15 devient 15. Le symbole « % » vit dans le fichier de traduction, pas ici — et le
        /// gabarit garde deux décimales facultatives : un bonus de 7,5 % ne doit pas s'annoncer 8 %.
        /// </summary>
        private static float AsPercent(float fraction)
        {
            return fraction * 100f;
        }

        /// <summary>
        /// Résout le nom de l'upgrade ciblée par sa seule clé, sans passer par le catalogue :
        /// la clé se dérive de l'id, donc rien de plus n'a besoin d'être injecté pour ça.
        /// </summary>
        private static string TargetName(PrestigeConfigSO config, ILocalizationService loc)
        {
            if (string.IsNullOrEmpty(config.TargetUpgradeId))
            {
                // Nœud marqué « spécifique » mais sans cible : donnée incohérente, on le dit.
                Debug.LogError(
                    "[PRESTIGE] Le nœud '" + config.Id + "' a un bonus ciblé (" + config.BonusType +
                    ") mais aucun targetUpgradeId. Son libellé sera incomplet.");

                return string.Empty;
            }

            return loc.GetText(LocalizationKeys.UpgradeName(config.TargetUpgradeId));
        }
    }
}
