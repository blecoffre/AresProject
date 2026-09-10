using System;
using UnityEngine;

namespace Core.Models.Economy
{
    /// <summary>
    /// La condition d'ouverture d'un nœud de prestige : quel nœud parent, et à quel niveau.
    ///
    /// Jusqu'au 2026-09-09, le prérequis n'était qu'une référence vers le parent et la règle
    /// était figée dans <see cref="Core.Services.Economy.PrestigeManager.IsUnlocked"/> :
    /// <c>niveau &gt; 0</c>. Acheter le premier rang d'un nœud à dix rangs ouvrait donc toute la
    /// suite de la branche — une ligne comme les trois paliers d'Overclock se déverrouillait
    /// entièrement pour le prix d'un seul cran.
    ///
    /// <b>Struct et non classe</b> : c'est une donnée de configuration lue dans des chemins
    /// chauds (le panneau de prestige repeint chaque nœud à chaque signal). Un type valeur évite
    /// une allocation par nœud et une indirection par lecture.
    /// </summary>
    [Serializable]
    public struct PrestigeRequirement
    {
        /// <summary>
        /// Valeur sentinelle de <see cref="RawRequiredLevel"/> : « le niveau MAXIMUM du parent,
        /// quel qu'il soit ».
        ///
        /// Auto-maintenue, et c'est tout l'intérêt : passer un nœud de cinq à dix rangs déplace
        /// automatiquement l'exigence de ses enfants. Un entier écrit en dur, lui, aurait
        /// silencieusement cessé de vouloir dire « au max ».
        /// </summary>
        public const int RequireParentMaxLevel = 0;

        [Tooltip("Le nœud à posséder avant celui-ci. Laisser vide pour une racine de branche.")]
        [SerializeField] private PrestigeConfigSO _node;

        [Tooltip("Niveau à atteindre sur le parent. 1 = il suffit de le posséder. " +
                 "0 = son niveau MAXIMUM, quel qu'il devienne.")]
        [SerializeField] private int _requiredLevel;

        public PrestigeConfigSO Node => _node;

        /// <summary>Un nœud sans parent est une racine : il est ouvert d'emblée.</summary>
        public bool HasNode => _node != null;

        /// <summary>
        /// La valeur brute telle qu'elle est sérialisée, sentinelle comprise. Réservée à
        /// l'outillage d'éditeur, qui doit pouvoir distinguer « 0 » de sa résolution.
        /// </summary>
        public int RawRequiredLevel => _requiredLevel;

        /// <summary>
        /// Le niveau réellement exigé sur le parent, sentinelle résolue.
        ///
        /// Le résultat est <b>borné par le MaxLevel du parent</b> : une exigence supérieure
        /// rendrait la branche définitivement inaccessible, ce qui est toujours une erreur de
        /// saisie plutôt qu'une intention. Le générateur avertit dans ce cas — ici on préfère
        /// une branche jouable à un blocage silencieux.
        /// </summary>
        public int ResolveRequiredLevel()
        {
            if (_node == null) return 0;

            return _requiredLevel <= RequireParentMaxLevel
                ? _node.MaxLevel
                : Math.Min(_requiredLevel, _node.MaxLevel);
        }
    }
}
