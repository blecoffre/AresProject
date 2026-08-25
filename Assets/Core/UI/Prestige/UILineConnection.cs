using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Core.UI.Prestige
{
    [RequireComponent(typeof(RectTransform))]
    public class UILineConnection : MonoBehaviour
    {
        [SerializeField] private Image _lineImage;

        [Header("États Visuels")]
        [SerializeField] private Color _lockedColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        [SerializeField] private Color _unlockedColor = Color.cyan;
        [SerializeField] private Color _completedColor = Color.green;

        private RectTransform _rectTransform;
        private CompositeDisposable _disposables = new();

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }

        public void DrawLine(Vector2 startPos, Vector2 endPos, float thickness = 4f)
        {
            Vector2 direction = endPos - startPos;
            _rectTransform.sizeDelta = new Vector2(direction.magnitude, thickness);
            _rectTransform.anchoredPosition = startPos + (direction / 2f);
            _rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        public void BindState(Observable<int> parentLevel, Observable<int> childLevel, int childMaxLevel)
        {
            _disposables.Clear(); // Nettoyage en cas de réutilisation (Pooling)

            Observable.CombineLatest(parentLevel, childLevel, (parent, child) =>
            {
                if (parent == 0) return _lockedColor;           // Le parent n'est pas acheté : bloqué
                if (child >= childMaxLevel) return _completedColor; // L'enfant est au max : complété
                return _unlockedColor;                          // En cours de progression
            })
            .Subscribe(color => _lineImage.color = color)
            .AddTo(_disposables);
        }

        private void OnDestroy()
        {
            _disposables.Dispose();
        }
    }
}