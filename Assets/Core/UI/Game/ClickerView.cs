using UnityEngine;
using UnityEngine.UI;

namespace Core.UI.Game
{
    public class ClickerView : MonoBehaviour
    {
        [SerializeField] private Button _actionButton;

        private ClickerPresenter _presenter;

        // Injection du Presenter (peut se faire via VContainer ou initialisation de scène)
        public void Construct(ClickerPresenter presenter)
        {
            _presenter = presenter;
        }

        private void OnEnable()
        {
            if (_actionButton != null)
            {
                _actionButton.onClick.AddListener(HandleButtonClick);
            }
        }

        private void OnDisable()
        {
            if (_actionButton != null)
            {
                _actionButton.onClick.RemoveListener(HandleButtonClick);
            }
        }

        private void HandleButtonClick()
        {
            _presenter?.OnClickActionTriggered();
        }
    }
}