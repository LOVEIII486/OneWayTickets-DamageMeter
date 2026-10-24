using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OWT.DamageMeter.UI
{
    public sealed class HudButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public Image Background;
        public TextMeshProUGUI Label;
        public Action Clicked;

        private bool _hover;
        private Color _labelColor = Theme.Muted;
        private Color _baseBackground = Color.clear;

        public void Init(Image background, TextMeshProUGUI label, Action clicked)
        {
            Background = background;
            Label = label;
            Clicked = clicked;
            _labelColor = label != null ? label.color : Theme.Muted;
            _baseBackground = background != null ? background.color : Color.clear;
            ApplyHover(false);
        }

        public void SetLabel(string value)
        {
            if (Label != null && !string.Equals(Label.text, value))
            {
                Label.text = value;
            }
        }

        public void SetLabelColor(Color color)
        {
            if (_labelColor != color)
            {
                _labelColor = color;
                if (Label != null && !_hover)
                {
                    Label.color = color;
                }
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            ApplyHover(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ApplyHover(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }
            if (Clicked != null)
            {
                Clicked();
            }
        }

        private void OnDisable()
        {
            _hover = false;
            ApplyHover(false);
        }

        private void ApplyHover(bool hover)
        {
            _hover = hover;
            if (Background != null)
            {
                if (_baseBackground.a < 0.01f)
                {
                    Background.color = hover ? Theme.ButtonHover : Color.clear;
                }
                else
                {
                    Background.color = hover
                        ? new Color(1f, 0.96f, 0.88f, 1f)
                        : _baseBackground;
                }
            }
            if (Label != null)
            {
                Label.color = hover ? Theme.Text : _labelColor;
            }
        }
    }
}
