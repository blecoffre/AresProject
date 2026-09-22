using Core.Models.Economy;
using Core.Services.Economy;
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
    /// <b>Le capteur s'ACHÈTE.</b> L'incertitude ne vient plus de la seule vitesse de
    /// remplissage : la capacité de calcul la divise, sur les DEUX versants à la fois — fréquence
    /// des relevés et pessimisme du bord haut. Run 1, le joueur n'a ni ping ni renifleur de
    /// paquets, donc il pilote à l'aveugle ; il paie ensuite sa visibilité en Hardware.
    ///
    /// C'était la correction que réclamait le modèle précédent : indexer l'incertitude sur la
    /// vitesse punissait le MILIEU de partie et jamais le début, puisqu'une run lente est une run
    /// lisible — les premières runs, les plus lentes, étaient donc les plus sûres. Bénéfice
    /// secondaire : un troisième rôle propre au Hardware, qui n'empiète toujours pas sur les
    /// Proxies.
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
        private readonly UpgradeManager _upgrades;

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

        public TraceReadout(ThreatManager threat,
                            BalancingConfigSO balancing,
                            ITimeSource time,
                            UpgradeManager upgrades)
        {
            _threat = threat;
            _balancing = balancing;
            _time = time;
            _upgrades = upgrades;
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

            // Un SEUL calcul par frame, réutilisé par les deux versants du brouillard. Le
            // recalculer dans chaque méthode paierait deux logarithmes là où un suffit, dans une
            // boucle qui tourne à chaque frame de la partie.
            float precision = ResolvePrecisionDivisor();

            if (_sampleAge >= ResolveInterval(fillRate, precision))
            {
                _lastKnownFraction.Value = trueFraction;
                _lastKnownFillRate = fillRate;
                _sampleAge = 0f;
            }

            // Le bord de la fourchette s'écarte tant qu'aucun relevé ne le ramène. Facteur de
            // pessimisme au-dessus de 1 : entre deux relevés le débit a pu croître, et une borne
            // haute qui se ferait dépasser par la réalité ne serait plus une borne.
            //
            // Le capteur le RAMÈNE vers 1 sans jamais passer dessous — l'interpolation part de 1
            // et va vers le réglage, donc la borne reste une borne quelle que soit la puissance
            // installée. Un pessimisme sous 1 rendrait le bord haut franchissable par la vérité,
            // ce qui retirerait au joueur la seule garantie qu'il ait.
            float pessimism = Mathf.Lerp(1f, _balancing.TraceReadoutPessimism, 1f / precision);

            float projected = _lastKnownFraction.CurrentValue
                            + _lastKnownFillRate * _sampleAge * pessimism;

            _estimatedMaxFraction.Value = Mathf.Clamp01(projected);
        }

        /// <summary>
        /// Intervalle entre deux relevés, en secondes. Il s'allonge avec la vitesse de
        /// remplissage, et se raccourcit avec la puissance de calcul installée.
        /// </summary>
        private float ResolveInterval(float fillRate, float precisionDivisor)
        {
            float reference = Mathf.Max(1e-6f, _balancing.TraceReadoutReferenceFillRate);
            float blend = Mathf.Clamp01(fillRate / reference);

            float baseInterval = Mathf.Lerp(_balancing.TraceReadoutMinInterval,
                                            _balancing.TraceReadoutMaxInterval,
                                            blend);

            // Le plancher reste le plancher. Sans ce garde-fou un parc de fin de partie
            // ramènerait l'intervalle sous la durée d'une frame, et TraceReadoutMinInterval
            // cesserait de vouloir dire ce que son nom annonce — on ne gagnerait rien de
            // lisible en échange, une aiguille relevée deux fois par seconde étant déjà
            // continue pour l'œil.
            return Mathf.Max(_balancing.TraceReadoutMinInterval, baseInterval / precisionDivisor);
        }

        /// <summary>
        /// Ce que la capacité de calcul achète en visibilité :
        /// <c>1 + puissance × log10(1 + TFlops / échelle)</c>. Vaut 1 à zéro TFlop — aucune aide,
        /// le brouillard nominal — et ne descend jamais dessous.
        ///
        /// <b>L'échelle n'est pas cosmétique.</b> Sans elle, le logarithme concentrait tout le
        /// rachat sur les tout premiers TFlops : mesuré le 2026-09-16, quatre Hardware de niveau 5
        /// — quelques minutes de jeu — ramenaient le brouillard de 60 s à 16,8 s, et l'écart entre
        /// la vérité et le relevé tombait à trois points de jauge, soit six pixels. Le joueur
        /// achetait sa clarté avant même de s'être rendu compte qu'il y avait du brouillard, ce
        /// qui est l'exact inverse de l'intention.
        /// </summary>
        private float ResolvePrecisionDivisor()
        {
            // TotalTFlops est la capacité EFFECTIVE : l'UpgradeManager lui a déjà appliqué la
            // tranche immobilisée par le Data Wiper. Purger la Trace aveugle donc le capteur
            // pendant tout le contrecoup. Ce n'est pas un effet de bord subi mais le contrecoup
            // qui s'étend à ce que les TFlops alimentent, comme il le fait déjà sur la
            // compression des cycles et sur la dissipation des Proxies — le joueur qui efface
            // ses traces perd la vue au moment précis où il vient de prendre un risque.
            float tflops = Mathf.Max(0f, (float)_upgrades.TotalTFlops.CurrentValue);
            float reference = Mathf.Max(1f, _balancing.TraceReadoutSensorReferenceTFlops);

            return 1f + _balancing.TraceReadoutSensorPower * Mathf.Log10(1f + tflops / reference);
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
