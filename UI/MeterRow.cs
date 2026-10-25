using System;
using System.Collections.Generic;
using OWT.DamageMeter.Tracking;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using World.Devices;

namespace OWT.DamageMeter.UI
{
    public sealed class RowHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerClickHandler
    {
        public Action<bool> HoverChanged;
        public Action Clicked;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (HoverChanged != null)
            {
                HoverChanged(true);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (HoverChanged != null)
            {
                HoverChanged(false);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null
                && (eventData.button != PointerEventData.InputButton.Left || eventData.dragging))
            {
                return;
            }
            if (Clicked != null)
            {
                Clicked();
            }
        }
    }

    internal sealed class MeterRow
    {
        private const float NameX = 56f;
        private const float TotalWidth = 110f;
        private const float RightMargin = 10f;
        private const float SlotX = 56f;
        private const float SlotWidth = 42f;
        private const float ChipY = 35f;
        private const float ChipHeight = 15f;
        private const float SubX = 104f;
        private const float DerivedWidth = 90f;
        private const float BarX = 56f;
        private const float BarY = 55f;
        private const float DetailLineHeight = 16f;
        private const float DetailPad = 8f;
        private const int MaxDetailLines = 10;

        public readonly bool IsOther;
        public Device Device;
        public RectTransform Root;
        public Action<MeterRow> Clicked;

        public float Height
        {
            get { return _rowHeight; }
        }

        public bool Expanded
        {
            get { return _expanded; }
        }

        private Image _bg;
        private Image _accent;
        private Image _iconBg;
        private Image _icon;
        private TextMeshProUGUI _iconGlyph;
        private TextMeshProUGUI _name;
        private TextMeshProUGUI _slot;
        private TextMeshProUGUI _total;
        private TextMeshProUGUI _sub;
        private TextMeshProUGUI _derived;
        private RectTransform _barRoot;
        private Image _barBg;
        private Image _barFill;
        private TextMeshProUGUI _expandGlyph;

        private RectTransform _detailRoot;
        private Image _detailDivider;
        private readonly List<TextMeshProUGUI> _detailLabels = new List<TextMeshProUGUI>();
        private readonly List<TextMeshProUGUI> _detailValues = new List<TextMeshProUGUI>();
        private int _detailCount;
        private bool _expanded;
        private float _rowHeight = Theme.RowHeight;
        private float _contentAlpha = 1f;

        private Color _accentColor = Theme.Accent;
        private Color _nameColor = Theme.Text;
        private string _iconKey;
        private bool _iconResolved;
        private bool _hover;
        private bool _firstLayout = true;
        private float _hoverAmount;
        private float _targetY;
        private float _currentY;
        private float _contentWidth = Theme.ContentWidth;
        private float _barWidth = Theme.ContentWidth - 66f;

        private double _displayTotal;
        private double _displayDps;
        private double _displayShare;
        private double _displayDerived;
        private float _displayFill;

        private string _lastTotalText;
        private string _lastSubText;
        private string _lastDerivedText;
        private string _lastNameText;
        private string _lastSlotText;

        public MeterRow(Transform parent, bool other, float contentWidth)
        {
            IsOther = other;
            Root = UiKit.Rect(parent, other ? "OtherRow" : "WeaponRow");
            Root.sizeDelta = new Vector2(contentWidth, Theme.RowHeight);
            Root.anchorMin = new Vector2(0f, 1f);
            Root.anchorMax = new Vector2(0f, 1f);
            Root.pivot = new Vector2(0f, 1f);

            _bg = UiKit.Image(Root, "Bg", null, Color.clear, true);
            _bg.type = UnityEngine.UI.Image.Type.Simple;
            UiKit.Place(_bg.rectTransform, 0f, 0f, contentWidth, Theme.RowHeight);
            RowHover hover = _bg.gameObject.AddComponent<RowHover>();
            hover.HoverChanged = SetHover;
            hover.Clicked = OnRowClicked;

            _accent = UiKit.RoundedImage(Root, "Accent", other ? Theme.OtherColor : Theme.Accent, 2f, false);
            UiKit.Place(_accent.rectTransform, 0f, 12f, 3f, Theme.RowHeight - 24f);

            _iconBg = UiKit.RoundedImage(Root, "IconBg", Theme.WithAlpha(Theme.Accent, 0.14f), 9f, false);
            UiKit.Place(_iconBg.rectTransform, 8f, 10f, 42f, 42f);

            _icon = UiKit.Image(Root, "Icon", null, Color.white, false);
            _icon.type = UnityEngine.UI.Image.Type.Simple;
            _icon.preserveAspect = true;
            UiKit.Place(_icon.rectTransform, 11f, 13f, 36f, 36f);

            _iconGlyph = UiKit.Text(Root, "IconGlyph", other ? "?" : "•", 24f,
                Theme.WithAlpha(other ? Theme.OtherColor : Theme.Accent, 0.95f),
                TextAlignmentOptions.Center);
            UiKit.Place(_iconGlyph.rectTransform, 8f, 10f, 42f, 42f);

            _name = UiKit.Text(Root, "Name", other ? Strings.Other : string.Empty, 15f, Theme.Text,
                TextAlignmentOptions.MidlineLeft);
            _name.fontStyle = FontStyles.Bold;
            _name.overflowMode = TextOverflowModes.Ellipsis;
            UiKit.Place(_name.rectTransform, NameX, 6f,
                Mathf.Max(60f, contentWidth - 196f), 22f);

            _total = UiKit.Text(Root, "Total", "0", 15f, Theme.Text, TextAlignmentOptions.MidlineRight);
            _total.fontStyle = FontStyles.Bold;
            UiKit.PlaceRight(_total.rectTransform, RightMargin, 5f, TotalWidth, 22f);

            _slot = UiKit.Text(Root, "Slot", string.Empty, 8.5f, Theme.Muted,
                TextAlignmentOptions.MidlineLeft);
            UiKit.Place(_slot.rectTransform, SlotX, ChipY, SlotWidth, ChipHeight);

            _sub = UiKit.Text(Root, "Sub", string.Empty, 9.5f, Theme.Muted, TextAlignmentOptions.MidlineLeft);
            _sub.overflowMode = TextOverflowModes.Ellipsis;
            UiKit.Place(_sub.rectTransform, SubX, ChipY, Mathf.Max(24f, contentWidth - 210f), ChipHeight);

            _derived = UiKit.Text(Root, "Derived", string.Empty, 9f, Theme.DerivedColor,
                TextAlignmentOptions.MidlineRight);
            UiKit.PlaceRight(_derived.rectTransform, RightMargin, ChipY, DerivedWidth, ChipHeight);

            _expandGlyph = UiKit.Text(Root, "Expand", "▸", 9f, Theme.Faint,
                TextAlignmentOptions.MidlineRight);
            UiKit.PlaceRight(_expandGlyph.rectTransform, RightMargin + TotalWidth + 4f, 5f, 14f, 22f);

            _detailRoot = UiKit.Rect(Root, "Details");
            UiKit.Place(_detailRoot, 0f, Theme.RowHeight, contentWidth, 0f);
            _detailDivider = UiKit.Image(_detailRoot, "DetailDivider", null,
                Theme.WithAlpha(Theme.Text, 0.055f), false);
            UiKit.Place(_detailDivider.rectTransform, 56f, 0f, contentWidth - 66f, 1f);
            for (int i = 0; i < MaxDetailLines; i++)
            {
                TextMeshProUGUI label = UiKit.Text(_detailRoot, "DetailLabel" + i, string.Empty,
                    9f, Theme.Muted, TextAlignmentOptions.MidlineLeft);
                label.overflowMode = TextOverflowModes.Ellipsis;
                UiKit.Place(label.rectTransform, 56f, DetailPad + i * DetailLineHeight,
                    Mathf.Max(40f, contentWidth - 198f), 14f);
                _detailLabels.Add(label);

                TextMeshProUGUI value = UiKit.Text(_detailRoot, "DetailValue" + i, string.Empty,
                    9f, Theme.Text, TextAlignmentOptions.MidlineRight);
                value.overflowMode = TextOverflowModes.Ellipsis;
                UiKit.PlaceRight(value.rectTransform, RightMargin,
                    DetailPad + i * DetailLineHeight, 116f, 14f);
                _detailValues.Add(value);
                label.gameObject.SetActive(false);
                value.gameObject.SetActive(false);
            }
            _detailRoot.gameObject.SetActive(false);

            _barRoot = UiKit.Rect(Root, "Bar");
            UiKit.Place(_barRoot, BarX, BarY, contentWidth - BarX - RightMargin, Theme.BarHeight);

            _barBg = UiKit.Image(_barRoot, "BarBg", null, Theme.BarBg, false);
            _barBg.type = UnityEngine.UI.Image.Type.Simple;
            UiKit.Place(_barBg.rectTransform, 0f, 0f, contentWidth - BarX - RightMargin, Theme.BarHeight);

            _barFill = UiKit.Image(_barRoot, "BarFill", null, Theme.Accent, false);
            _barFill.type = UnityEngine.UI.Image.Type.Simple;
            UiKit.Place(_barFill.rectTransform, 0f, 0f, 0f, Theme.BarHeight);

            SetWidth(contentWidth);

            if (other)
            {
                _iconGlyph.gameObject.SetActive(true);
                _icon.gameObject.SetActive(false);
                _slot.text = "-";
                _lastSlotText = "-";
                _expandGlyph.gameObject.SetActive(false);
            }
        }

        public void SetWidth(float contentWidth)
        {
            _contentWidth = Mathf.Max(Theme.MinContentWidth, contentWidth);
            ApplyHeight();
            _name.rectTransform.sizeDelta = new Vector2(
                Mathf.Max(60f, _contentWidth - NameX - TotalWidth - RightMargin - 24f), 22f);
            _sub.rectTransform.sizeDelta = new Vector2(
                Mathf.Max(24f, _contentWidth - SubX - DerivedWidth - RightMargin - 6f), ChipHeight);
            _barWidth = Mathf.Max(40f, _contentWidth - BarX - RightMargin);
            _barRoot.sizeDelta = new Vector2(_barWidth, Theme.BarHeight);
            _barBg.rectTransform.sizeDelta = new Vector2(_barWidth, Theme.BarHeight);
            if (_barFill.rectTransform.sizeDelta.x > _barWidth)
            {
                _barFill.rectTransform.sizeDelta = new Vector2(_barWidth, Theme.BarHeight);
            }

            float detailWidth = Mathf.Max(40f, _contentWidth - 66f);
            _detailDivider.rectTransform.sizeDelta = new Vector2(detailWidth, 1f);
            for (int i = 0; i < _detailLabels.Count; i++)
            {
                _detailLabels[i].rectTransform.sizeDelta = new Vector2(
                    Mathf.Max(40f, _contentWidth - 198f), 14f);
                _detailValues[i].rectTransform.sizeDelta = new Vector2(116f, 14f);
            }
        }

        public void SetWeapon(WeaponStat stat)
        {
            Device = stat.Device;
            _accentColor = Theme.QualityColor(stat.Rank);

            string name = stat.MetaResolved
                ? (string.IsNullOrEmpty(stat.Name) ? Strings.UnknownDevice : stat.Name)
                : Strings.UnknownDevice;
            if (!string.Equals(_lastNameText, name, StringComparison.Ordinal))
            {
                _lastNameText = name;
                _name.text = name;
            }

            if (stat.MetaResolved)
            {
                TrySetIcon(stat.IconKey);
            }

            string slotText = Strings.Slot(stat.SlotKey);
            if (stat.Destroyed)
            {
                slotText = Strings.Slot(stat.SlotKey) + (Strings.Chinese ? "·损" : "·BAD");
            }
            else if (!stat.Mounted)
            {
                slotText = Strings.Chinese ? "已卸下" : "REMOVED";
            }
            if (!string.Equals(_lastSlotText, slotText, StringComparison.Ordinal))
            {
                _lastSlotText = slotText;
                _slot.text = slotText;
            }

            if (!IsOther)
            {
                _nameColor = Theme.QualityColor(stat.Rank);
                _name.color = _nameColor;
            }
        }

        private void TrySetIcon(string key)
        {
            if (_iconResolved && string.Equals(_iconKey, key, StringComparison.Ordinal))
            {
                return;
            }

            _iconKey = key;
            Sprite sprite = UiKit.LoadDeviceIcon(key);
            if (sprite != null)
            {
                _icon.sprite = sprite;
                _icon.gameObject.SetActive(true);
                _iconGlyph.gameObject.SetActive(false);
                _iconResolved = true;
            }
            else if (!string.IsNullOrEmpty(key))
            {
                // Addressables may not be ready on the first frame. Retry next refresh.
                _iconResolved = false;
            }
            else
            {
                _iconResolved = true;
                _icon.gameObject.SetActive(false);
                _iconGlyph.gameObject.SetActive(true);
            }
        }

        public void UpdateWeapon(WeaponStat stat, DamageTracker tracker, double maxTotal,
            double weaponTotal, bool compact, float dt)
        {
            Color color = stat.Destroyed ? Theme.Danger : Theme.QualityColor(stat.Rank);
            if (!stat.Mounted && !stat.Destroyed)
            {
                color = Theme.WithAlpha(color, 0.55f);
            }
            SetAccent(color);

            bool support = stat.IsSupport;
            double shown = support ? stat.AssistDamage : stat.TotalDamage;
            double dps = support ? stat.AssistDps : stat.Dps;
            double share = weaponTotal > 0.0 ? shown / weaponTotal : 0.0;
            double fill = maxTotal > 0.0 ? shown / maxTotal : 0.0;
            string chip = support ? Strings.Support : null;
            Color chipColor = support ? Theme.Muted : Theme.DerivedColor;
            UpdateVisuals(shown, dps, share, fill, support ? 0.0 : stat.DerivedDamage,
                compact, dt, chip, chipColor);
            SetDetails(stat, tracker, compact);

            float dim = stat.HasContribution ? 1f : 0.55f;
            SetContentAlpha(dim);
        }

        public void UpdateOther(double damage, double dps, double share, double maxTotal, bool compact, float dt)
        {
            SetAccent(Theme.OtherColor);
            double fill = maxTotal > 0.0 ? damage / maxTotal : 0.0;
            UpdateVisuals(damage, dps, share, fill, 0.0, compact, dt);
            SetContentAlpha(damage > 0.0 ? 1f : 0.55f);
        }

        private void UpdateVisuals(double total, double dps, double share, double fill,
            double derived, bool compact, float dt, string chipOverride = null,
            Color? chipColor = null)
        {
            float smooth = dt > 0f ? 1f - Mathf.Exp(-12f * dt) : 1f;
            _displayTotal = Lerp(_displayTotal, total, smooth);
            _displayDps = Lerp(_displayDps, dps, smooth);
            _displayShare = Lerp(_displayShare, share, smooth);
            _displayDerived = Lerp(_displayDerived, derived, smooth);
            _displayFill = Mathf.Lerp(_displayFill, Mathf.Clamp01((float)fill), smooth);

            SetText(_total, ref _lastTotalText, Strings.Number(_displayTotal, compact));
            string sub = Strings.Number1(_displayDps, compact) + "/s  ·  " + Strings.Percent(_displayShare);
            SetText(_sub, ref _lastSubText, sub);

            string derivedText = chipOverride;
            if (derivedText == null)
            {
                derivedText = derived > 0.5
                    ? Strings.DerivedShort + " " + Strings.Number(_displayDerived, compact)
                    : string.Empty;
            }
            SetText(_derived, ref _lastDerivedText, derivedText);
            Color chipBase = chipColor.HasValue ? chipColor.Value : Theme.DerivedColor;
            Color chip = chipBase;
            chip.a = _contentAlpha;
            _derived.color = chip;

            float width = _barWidth * _displayFill;
            bool show = width > 0.5f;
            if (_barFill.gameObject.activeSelf != show)
            {
                _barFill.gameObject.SetActive(show);
            }
            if (show)
            {
                _barFill.rectTransform.sizeDelta = new Vector2(width, Theme.BarHeight);
                _barFill.color = Theme.WithAlpha(_accentColor, 0.95f);
            }

            UpdateHoverVisual(dt);
        }

        public void SetExpanded(bool expanded)
        {
            if (_expanded == expanded)
            {
                return;
            }

            _expanded = expanded;
            _detailRoot.gameObject.SetActive(expanded);
            if (_expandGlyph != null)
            {
                _expandGlyph.text = expanded ? "▾" : "▸";
            }
            if (!expanded)
            {
                _detailCount = 0;
                ApplyHeight();
            }
        }

        private void SetDetails(WeaponStat stat, DamageTracker tracker, bool compact)
        {
            if (!_expanded)
            {
                return;
            }

            int count = 0;
            if (stat.IsSupport)
            {
                string description = tracker.GetDeviceDescription(stat.Device);
                if (!string.IsNullOrEmpty(description))
                {
                    AddDetail(ref count, Strings.EffectPrefix + description, 0.0, false, false, compact);
                }
                AddDetail(ref count, Strings.AssistDamage, stat.AssistDamage, false, true, compact);
                AddDetail(ref count, Strings.SupportNote, 0.0, false, false, compact);
                for (int i = 0; i < stat.Effects.Count; i++)
                {
                    EffectStat effect = stat.Effects[i];
                    AddDetail(ref count, Strings.EffectName(effect.Kind), effect.Damage, true, true, compact);
                }
                if (stat.Assisted.Count > 0)
                {
                    AddDetail(ref count, Strings.AffectedDevices, 0.0, false, false, compact);
                    for (int i = 0; i < stat.Assisted.Count; i++)
                    {
                        AssistStat assist = stat.Assisted[i];
                        AddDetail(ref count, tracker.GetDeviceName(assist.Weapon),
                            assist.Damage, true, true, compact);
                    }
                }
            }
            else
            {
                AddDetail(ref count, Strings.DirectDamage, stat.DirectDamage, false, true, compact);
                AddDetail(ref count, Strings.DerivedDamage, stat.DerivedDamage, false, true, compact);
                if (stat.Effects.Count == 0)
                {
                    if (stat.DerivedDamage > 0.5)
                    {
                        AddDetail(ref count, Strings.EffectName(DamageKind.OtherDerived),
                            stat.DerivedDamage, true, true, compact);
                    }
                    else
                    {
                        AddDetail(ref count, Strings.NoDerived, 0.0, true, false, compact);
                    }
                }
                else
                {
                    for (int i = 0; i < stat.Effects.Count; i++)
                    {
                        EffectStat effect = stat.Effects[i];
                        string label = Strings.EffectName(effect.Kind);
                        if (effect.Support != null)
                        {
                            label = tracker.GetDeviceName(effect.Support) + " · " + label;
                        }
                        AddDetail(ref count, label, effect.Damage, true, true, compact);
                    }
                }
            }

            for (int i = count; i < _detailLabels.Count; i++)
            {
                if (_detailLabels[i].gameObject.activeSelf)
                {
                    _detailLabels[i].gameObject.SetActive(false);
                    _detailValues[i].gameObject.SetActive(false);
                }
            }

            _detailCount = count;
            ApplyHeight();
        }

        private void AddDetail(ref int count, string label, double value, bool indent,
            bool showValue, bool compact)
        {
            if (count >= _detailLabels.Count)
            {
                return;
            }

            TextMeshProUGUI labelText = _detailLabels[count];
            TextMeshProUGUI valueText = _detailValues[count];
            labelText.text = (indent ? "· " : string.Empty) + label;
            labelText.rectTransform.anchoredPosition = new Vector2(
                indent ? 66f : 56f, -(DetailPad + count * DetailLineHeight));
            labelText.color = Theme.WithAlpha(Theme.Muted, _contentAlpha * 0.95f);
            valueText.text = showValue ? Strings.Number(value, compact) : string.Empty;
            valueText.color = Theme.WithAlpha(Theme.Text, _contentAlpha);
            if (!labelText.gameObject.activeSelf)
            {
                labelText.gameObject.SetActive(true);
                valueText.gameObject.SetActive(true);
            }
            count++;
        }

        private void ApplyHeight()
        {
            _rowHeight = _expanded
                ? Theme.RowHeight + DetailPad + Mathf.Max(1, _detailCount) * DetailLineHeight + DetailPad
                : Theme.RowHeight;
            Root.sizeDelta = new Vector2(_contentWidth, _rowHeight);
            if (_bg != null)
            {
                _bg.rectTransform.sizeDelta = new Vector2(_contentWidth, _rowHeight);
            }
            if (_detailRoot != null)
            {
                _detailRoot.sizeDelta = new Vector2(_contentWidth,
                    Mathf.Max(0f, _rowHeight - Theme.RowHeight));
            }
        }

        private void OnRowClicked()
        {
            if (IsOther || Clicked == null)
            {
                return;
            }
            Clicked(this);
        }

        private void SetText(TextMeshProUGUI label, ref string cache, string value)
        {
            if (!string.Equals(cache, value, StringComparison.Ordinal))
            {
                cache = value;
                label.text = value;
            }
        }

        public void SetTargetY(float y)
        {
            _targetY = y;
            if (_firstLayout)
            {
                _firstLayout = false;
                _currentY = y;
                Root.anchoredPosition = new Vector2(0f, -y);
            }
        }

        public void UpdatePosition(float dt)
        {
            float smooth = dt > 0f ? 1f - Mathf.Exp(-18f * dt) : 1f;
            _currentY = Mathf.Lerp(_currentY, _targetY, smooth);
            Root.anchoredPosition = new Vector2(0f, -_currentY);
        }

        private void SetHover(bool hover)
        {
            _hover = hover;
        }

        private void UpdateHoverVisual(float dt)
        {
            float target = _hover ? 1f : 0f;
            float smooth = dt > 0f ? 1f - Mathf.Exp(-16f * dt) : 1f;
            _hoverAmount = Mathf.Lerp(_hoverAmount, target, smooth);
            Color baseColor = IsOther ? Theme.OtherColor : _accentColor;
            _bg.color = Theme.WithAlpha(Theme.Text, _hoverAmount * 0.045f);
            _accent.color = Theme.WithAlpha(baseColor, 0.35f + _hoverAmount * 0.65f);
        }

        private void SetAccent(Color color)
        {
            _accentColor = color;
            _accent.color = Theme.WithAlpha(color, 0.35f + _hoverAmount * 0.65f);
            _iconBg.color = Theme.WithAlpha(color, 0.14f);
            _iconGlyph.color = Theme.WithAlpha(color, 0.95f);
        }

        private void SetContentAlpha(float alpha)
        {
            _contentAlpha = alpha;
            Color name = _nameColor;
            name.a = alpha;
            _name.color = name;
            Color total = Theme.Text;
            total.a = alpha;
            _total.color = total;
            Color muted = Theme.Muted;
            muted.a = alpha * 0.95f;
            _slot.color = muted;
            _sub.color = muted;

            Color chip = _derived.color;
            chip.a = alpha;
            _derived.color = chip;

            if (_expandGlyph != null)
            {
                _expandGlyph.color = Theme.WithAlpha(Theme.Faint, alpha);
            }
            if (_detailDivider != null)
            {
                _detailDivider.color = Theme.WithAlpha(Theme.Text, alpha * 0.055f);
            }
            for (int i = 0; i < _detailLabels.Count; i++)
            {
                if (_detailLabels[i].gameObject.activeSelf)
                {
                    _detailLabels[i].color = Theme.WithAlpha(Theme.Muted, alpha * 0.95f);
                    _detailValues[i].color = Theme.WithAlpha(Theme.Text, alpha);
                }
            }
        }

        private static double Lerp(double from, double to, float t)
        {
            return from + (to - from) * t;
        }

        public void Destroy()
        {
            if (Root != null)
            {
                UnityEngine.Object.Destroy(Root.gameObject);
            }
        }
    }
}
