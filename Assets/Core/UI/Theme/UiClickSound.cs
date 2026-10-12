using Core.Services.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Core.UI.Theme
{
    /// <summary>
    /// Le son d'un bouton : son clic quand il est actif, le refus quand il est grisé. À poser sur
    /// chaque <see cref="Button"/> du jeu — l'outil <c>Tools/Core/Audio/Équiper les boutons du son
    /// de clic</c> s'en charge pour les prefabs et les scènes, et signale tout bouton resté muet.
    ///
    /// <b>Le clic</b> est branché sur <c>onClick</c> et non sur le pointeur : le son part
    /// exactement quand l'action a lieu, au clic de souris comme à la validation clavier ou
    /// manette, et jamais sur un bouton désactivé (Unity ne déclenche pas son onClick).
    ///
    /// <b>Le refus</b> passe par le pointeur, seul signal qu'un bouton grisé reçoit encore.
    /// L'état est relevé à l'APPUI, pas au relâchement : un bouton qui se grise en réponse à son
    /// propre clic (« Acheter » devenu trop cher) a été cliqué actif, il ne doit sonner que son
    /// clic — pas un refus en prime. Clavier et manette ne refusent pas : la navigation saute
    /// déjà les boutons grisés.
    /// </summary>
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public sealed class UiClickSound : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
    {
        [Tooltip("Le canal des sons d'interface (Data/Audio/UiSoundChannel).")]
        [SerializeField] private UiSoundChannelSO _channel;

        [Tooltip("Laisser vide pour la famille de clics par défaut de l'AudioLibrary. Renseigner " +
                 "pour donner à ce bouton une autre famille (ex. Validation). Tant que cette " +
                 "famille est vide, le bouton garde la famille par défaut.")]
        [SerializeField] private SoundSetSO _soundSetOverride;

        [Tooltip("Joue le son de refus quand on clique ce bouton alors qu'il est grisé. À décocher " +
                 "pour un bouton dont l'état grisé ne signifie pas « pas encore ».")]
        [SerializeField] private bool _refusalSound = true;

        private Button _button;
        private bool _pressedWhileDisabled;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(HandleClick);

            if (_channel == null)
            {
                Debug.LogWarning($"[AUDIO] « {name} » a un UiClickSound sans canal : ce bouton est muet.", this);
            }
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(HandleClick);
        }

        // Méthode nommée : un retrait de lambda ne désabonne rien.
        private void HandleClick()
        {
            if (_channel != null) _channel.RaiseClick(_soundSetOverride);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // IsInteractable et non .interactable : un CanvasGroup parent peut griser toute une
            // ligne (réglage d'option sans effet) sans toucher au bouton lui-même.
            _pressedWhileDisabled = !_button.IsInteractable();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_pressedWhileDisabled) return;
            _pressedWhileDisabled = false;

            if (!_refusalSound || eventData.button != PointerEventData.InputButton.Left) return;
            if (_channel != null) _channel.RaiseRefusal();
        }
    }
}
