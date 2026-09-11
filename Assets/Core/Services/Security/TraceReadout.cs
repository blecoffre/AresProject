using Core.Models.Economy;
using Core.Services.Platform;
using R3;
using System;
using UnityEngine;
using VContainer.Unity;

namespace Core.Services.Security
{
    /// <summary>
    /// Le capteur imparfait posé sur la jauge de Trace. <b>Il ne ment jamais, il retarde.</b>
    ///
    /// <b>Pourquoi il existe.</b> Mesuré : plafond de jauge divisé par 250, et toujours zéro
    /// saisie sur trente-cinq runs. Ce n'était pas un défaut de réglage mais une impossibilité —
    /// le joueur contrôle les quatre termes de l'équation (débit, plafond, dissipation, instant
    /// de sortie) et les observe tous en temps réel. Un compte à rebours déterministe,
    /// entièrement observable et piloté par celui qu'il menace ne peut pas être dangereux : c'est
    /// un budget, pas une menace.
    ///
    /// Baisser le plafond ne changeait rien, et l'arithmétique dit pourquoi : ça comprime toute
    /// la courbe uniformément, donc la part d'avertissement reste identique. <b>Une jauge
    /// linéaire ne peut, par construction, surprendre personne.</b>
    ///
    /// <b>Ce que fait ce capteur.</b> Il échantillonne la vérité à intervalle variable, et
    /// <b>l'intervalle s'allonge quand la jauge se remplit vite</b>. Trace tranquille : relevé
    /// quasi continu. Trace qui s'emballe : la télémétrie bégaie, et le joueur pilote au jugé —
    /// exactement au moment où le chiffre comptait. L'incertitude n'est pas un paramètre imposé
    /// au joueur, c'est la <b>conséquence de sa propre gourmandise</b>.
    ///
    /// <b>Aucun hasard.</b> La saisie tombe toujours à 100,000 % exactement. Le joueur ne perd
    /// jamais sur un tirage : il perd sur une estimation qu'il a mal faite, ce qui laisse l'échec
    /// entièrement à sa charge.
    ///
    /// <b>Frontière stricte :</b> ce service est en LECTURE SEULE sur le
    /// <see cref="ThreatManager"/>, qui reste la seule vérité. Aucune règle de jeu ne dépend du
    /// brouillard — seul l'affichage est incertain.
    /// </summary>
    public sealed class TraceReadout : ITickable, IDisposable
    {
        private readonly ThreatManager _threat;
        private readonly BalancingConfigSO _balancing;
        private readonly ITimeSource _time;

        private float _sampleAge;
        private float _previousTrueFraction;
        private float _lastKnownFillRate;

        private readonly ReactiveProperty<float> _lastKnownFraction = new(0f);
        private readonly ReactiveProperty<float> _estimatedMaxFraction = new(0f);

        /// <summary>
        /// Dernière valeur RELEVÉE de la jauge. C'est elle que l'aiguille affiche : une position
        /// connue, pas une position actuelle. Elle ne bouge qu'aux relevés, donc elle se fige
        /// visiblement quand la situation se dégrade.
        /// </summary>
        public ReadOnlyReactiveProperty<float> LastKnownFraction => _lastKnownFraction;

        /// <summary>
        /// Bord haut de la zone d'incertitude : où la Trace peut se trouver au pire, extrapolé
        /// depuis le dernier débit connu.
        ///
        /// <b>Ce n'est pas un chiffre décoratif</b> — c'est l'intervalle réel. Le joueur décide
        /// contre ce bord, pas contre un nombre, et c'est là que naît la sueur.
        /// </summary>
        public ReadOnlyReactiveProperty<float> EstimatedMaxFraction => _estimatedMaxFraction;

        /// <summary>Âge du dernier relevé, en secondes. La vue en fait « il y a 14 s ».</summary>
        public float SecondsSinceSample => _sampleAge;

        public TraceReadout(ThreatManager threat, BalancingConfigSO balancing, ITimeSource time)
        {
            _threat = threat;
            _balancing = balancing;
            _time = time;
        }

        public void Tick()
        {
            float deltaTime = _time.DeltaTime;
            if (deltaTime <= 0f) return;

            float trueFraction = _threat.NormalizedThreat.CurrentValue;

            // La Trace ne redescend JAMAIS d'elle-même : une baisse ne peut venir que d'un wipe
            // de fin de run ou du Data Wiper. Dans les deux cas le joueur voit l'événement, donc
            // son relevé doit se resynchroniser sur-le-champ — le laisser afficher une valeur
            // périmée PLUS HAUTE que la réalité serait un mensonge, pas un retard.
            if (trueFraction < _previousTrueFraction)
            {
                Resync(trueFraction);
                return;
            }

            float fillRate = (trueFraction - _previousTrueFraction) / deltaTime;
            _previousTrueFraction = trueFraction;

            _sampleAge += deltaTime;

            if (_sampleAge >= ResolveInterval(fillRate))
            {
                _lastKnownFraction.Value = trueFraction;
                _lastKnownFillRate = fillRate;
                _sampleAge = 0f;
            }

            // Le bord de la fourchette s'écarte tant qu'aucun relevé ne le ramène. Facteur de
            // pessimisme au-dessus de 1 : entre deux relevés le débit a pu croître, et une borne
            // haute qui se ferait dépasser par la réalité ne serait plus une borne.
            float projected = _lastKnownFraction.CurrentValue
                            + _lastKnownFillRate * _sampleAge * _balancing.TraceReadoutPessimism;

            _estimatedMaxFraction.Value = Mathf.Clamp01(projected);
        }

        /// <summary>
        /// Intervalle entre deux relevés, en secondes. Il s'allonge avec la vitesse de
        /// remplissage : c'est tout le mécanisme.
        /// </summary>
        private float ResolveInterval(float fillRate)
        {
            float reference = Mathf.Max(1e-6f, _balancing.TraceReadoutReferenceFillRate);
            float blend = Mathf.Clamp01(fillRate / reference);

            return Mathf.Lerp(_balancing.TraceReadoutMinInterval,
                              _balancing.TraceReadoutMaxInterval,
                              blend);
        }

        /// <summary>
        /// Recale le relevé sur la vérité, sans délai. Appelé quand la jauge redescend — wipe de
        /// fin de run, Data Wiper — et à la restauration d'une sauvegarde.
        /// </summary>
        public void Resync(float trueFraction)
        {
            _previousTrueFraction = trueFraction;
            _lastKnownFraction.Value = trueFraction;
            _estimatedMaxFraction.Value = trueFraction;
            _lastKnownFillRate = 0f;
            _sampleAge = 0f;
        }

        public void Dispose()
        {
            _lastKnownFraction.Dispose();
            _estimatedMaxFraction.Dispose();
        }
    }
}
