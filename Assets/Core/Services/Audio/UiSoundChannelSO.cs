using System;
using UnityEngine;

namespace Core.Services.Audio
{
    /// <summary>
    /// Le canal par lequel un bouton demande son clic, sans connaître le lecteur.
    ///
    /// <b>Pourquoi un asset et pas une injection :</b> les boutons vivent dans des prefabs
    /// instanciés partout, souvent hors de portée de VContainer. Les faire injecter passerait par
    /// <c>autoInjectGameObjects</c>, qui oublie en silence tout objet absent de sa liste. Une
    /// référence d'asset sérialisée dans le prefab, elle, ne peut pas manquer à l'exécution.
    /// Le lecteur (<see cref="UiSoundPlayer"/>) reçoit ce même asset par le conteneur et s'y
    /// abonne : aucun Singleton, aucun accès statique.
    /// </summary>
    [CreateAssetMenu(fileName = "UiSoundChannel", menuName = "Core/Audio/UI Sound Channel")]
    public sealed class UiSoundChannelSO : ScriptableObject
    {
        /// <summary>
        /// Un clic validé. La famille vaut null quand le bouton s'en remet à celle par défaut.
        /// </summary>
        public event Action<SoundSetSO> ClickRequested;

        /// <summary>Un clic sur un bouton grisé : l'action demandée est impossible pour l'instant.</summary>
        public event Action RefusalRequested;

        public void RaiseClick(SoundSetSO setOverride) => ClickRequested?.Invoke(setOverride);

        public void RaiseRefusal() => RefusalRequested?.Invoke();
    }
}
