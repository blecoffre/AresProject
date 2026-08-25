using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Core.UI.Prestige
{
    [RequireComponent(typeof(ScrollRect))]
    public class GraphZoomController : MonoBehaviour, IScrollHandler
    {
        [SerializeField] private RectTransform _content;
        [SerializeField] private float _zoomSpeed = 0.1f;
        [SerializeField] private float _minZoom = 0.3f;
        [SerializeField] private float _maxZoom = 2f;

        public void OnScroll(PointerEventData eventData)
        {
            float scrollDelta = eventData.scrollDelta.y;
            if (Mathf.Abs(scrollDelta) < 0.01f) return;

            // Récupération de la position de la souris relative au Content
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _content, eventData.position, eventData.pressEventCamera, out Vector2 pointerLocalPos);

            float currentScale = _content.localScale.x;
            float zoomAmount = scrollDelta * _zoomSpeed;
            float newScale = Mathf.Clamp(currentScale + zoomAmount, _minZoom, _maxZoom);

            // Application du zoom
            _content.localScale = Vector3.one * newScale;

            // Ajustement de la position pour zoomer vers le curseur
            Vector2 pivotShift = pointerLocalPos * (newScale - currentScale);
            _content.anchoredPosition -= pivotShift;
        }
    }
}