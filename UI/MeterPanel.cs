using System;
using System.Collections.Generic;
using Commons;
using OWT.DamageMeter.Tracking;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using World.Devices;

namespace OWT.DamageMeter.UI
{
    public sealed class PanelDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public RectTransform Target;
        public Canvas Canvas;
        public Action<Vector2> DragEnded;

        public void OnBeginDrag(PointerEventData eventData)
        {
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (Target == null)
            {
                return;
            }
            // eventData.delta is the movement since the previous drag event, not since
            // the drag began. Accumulate it instead of adding it to a fixed start point.
            float scale = Canvas != null ? Mathf.Max(0.01f, Canvas.scaleFactor) : 1f;
            Target.anchoredPosition += eventData.delta / scale;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (Target != null && DragEnded != null)
            {
                DragEnded(Target.anchoredPosition);
            }
        }
    }

    public sealed class BallWidget : MonoBehaviour, IPointerClickHandler, IPointerDownHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        internal MeterPanel Owner;

        private Vector2 _pressPosition;
        private bool _dragged;

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressPosition = eventData != null ? eventData.position : Vector2.zero;
            _dragged = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }
            // A drag ends with a click event in UGUI. Only open the panel when the
            // pointer actually stayed on the ball.
            if (_dragged)
            {
                _dragged = false;
                return;
            }
            if (Owner != null)
            {
                Owner.OpenFromBall();
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragged = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData != null
                && (eventData.position - _pressPosition).sqrMagnitude > 25f)
            {
                _dragged = true;
            }
            if (Owner != null)
            {
                Owner.BallDrag(eventData);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (Owner != null)
            {
                Owner.BallDragEnd();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Owner != null)
            {
                Owner.BallHover(true);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (Owner != null)
            {
                Owner.BallHover(false);
            }
        }
    }

    public sealed class ResizeHandle : MonoBehaviour, IBeginDragHandler, IDragHandler,
        IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        internal MeterPanel Owner;
        public Image Grip;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Owner != null)
            {
                Owner.ResizeBegin(eventData.position);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (Owner != null)
            {
                Owner.ResizeDrag(eventData.position);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (Owner != null)
            {
                Owner.ResizeEnd();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Grip != null)
            {
                Grip.color = new Color(1f, 1f, 1f, 0.95f);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (Grip != null)
            {
                Grip.color = new Color(1f, 1f, 1f, 0.55f);
            }
        }
    }

    internal sealed class MeterPanel
    {
        private const float BallSize = 64f;
        private const float MinScale = 0.7f;
        private const float MaxScale = 1.8f;

        private GameObject _canvasGo;
        private Canvas _canvas;
        private RectTransform _canvasRect;
        private CanvasGroup _group;
        private RectTransform _panel;

        private Image _shadow;
        private Image _bg;
        private Image _border;
        private Image _topAccent;
        private Image _dotGlow;
        private Image _dot;

        private RectTransform _header;
        private RectTransform _headerHit;
        private RectTransform _headerDivider;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _subtitle;

        private RectTransform _rowsRoot;
        private TextMeshProUGUI _emptyLabel;

        private RectTransform _footerRoot;
        private Image _footerLine;
        private TextMeshProUGUI _footerLabel;

        private RectTransform _ballRoot;
        private RectTransform _ballVisual;
        private CanvasGroup _ballGroup;
        private Image _ballGlow;
        private Image _ballBg;
        private Image _ballHoverOverlay;
        private Image _ballRing;
        private TextMeshProUGUI _ballValue;
        private TextMeshProUGUI _ballLabel;
        private bool _ballShown;
        private float _ballAlpha;
        private bool _ballHover;

        private Image _resizeGrip;
        private TextMeshProUGUI _resizeLabel;

        private readonly Dictionary<Device, MeterRow> _rowsByDevice = new Dictionary<Device, MeterRow>();
        private readonly HashSet<Device> _alive = new HashSet<Device>();
        private readonly HashSet<Device> _expanded = new HashSet<Device>();
        private MeterRow _otherRow;

        private bool _built;
        private bool _shown = true;
        private float _age;
        private float _height = 120f;
        private float _targetHeight = 120f;
        private float _localWidth = Theme.PanelWidth;
        private float _contentWidth = Theme.ContentWidth;
        private float _scaleOverride = -1f;
        private float _widthFactorOverride = -1f;
        private bool _resizing;
        private float _resizeStartScale;
        private float _resizeStartWidthFactor;
        private float _resizeStartPointerX;
        private float _resizeStartPointerY;
        private float _resizeStartWidthPx;
        private float _resizeStartLocalHeight;
        private float _resizeStartRenderedHeightPx;
        private string _lastSubtitle;
        private string _lastBallValue;
        private int _lastScreenWidth;
        private int _lastScreenHeight;
        private bool _lastLetterboxActive;
        private Rect _lastViewRect;

        public Action BallClicked;
        public Action CollapseRequested;

        public bool Built
        {
            get { return _built; }
        }

        private float EffectiveScale
        {
            get
            {
                if (_scaleOverride > 0f)
                {
                    return _scaleOverride;
                }
                return DamageMeterPlugin.PanelScale != null ? DamageMeterPlugin.PanelScale.Value : 1f;
            }
        }

        private float EffectiveWidthFactor
        {
            get
            {
                if (_widthFactorOverride > 0f)
                {
                    return _widthFactorOverride;
                }
                return DamageMeterPlugin.PanelWidthScale != null ? DamageMeterPlugin.PanelWidthScale.Value : 1f;
            }
        }

        private static float ClampWidthFactor(float widthFactor, float uiScale)
        {
            widthFactor = Mathf.Clamp(widthFactor, 0.7f, 1.8f);
            float minLocalWidth = Theme.MinContentWidth + Theme.Padding * 2f;
            float minFactor = minLocalWidth * uiScale / Theme.PanelWidth;
            if (widthFactor < minFactor)
            {
                widthFactor = Mathf.Min(1.8f, minFactor);
            }
            return widthFactor;
        }

        public bool EnsureBuilt()
        {
            if (_built)
            {
                return true;
            }
            if (!FontProvider.Ready)
            {
                return false;
            }

            Build();
            return _built;
        }

        public void SetShown(bool shown)
        {
            _shown = shown;
        }

        private void Build()
        {
            UiKit.ResetIconCache();

            _canvasGo = new GameObject("OWT Damage Meter Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas = _canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32100;
            _canvas.pixelPerfect = false;

            CanvasScaler scaler = _canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            GraphicRaycaster raycaster = _canvasGo.GetComponent<GraphicRaycaster>();
            raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;

            _canvasRect = (RectTransform)_canvasGo.transform;

            _panel = UiKit.Rect(_canvasGo.transform, "Panel");
            _panel.anchorMin = new Vector2(1f, 1f);
            _panel.anchorMax = new Vector2(1f, 1f);
            _panel.pivot = new Vector2(1f, 1f);
            _panel.sizeDelta = new Vector2(Theme.PanelWidth, _height);
            _panel.anchoredPosition = new Vector2(
                DamageMeterPlugin.PanelX != null ? DamageMeterPlugin.PanelX.Value : DamageMeterPlugin.DefaultPanelX,
                DamageMeterPlugin.PanelY != null ? DamageMeterPlugin.PanelY.Value : DamageMeterPlugin.DefaultPanelY);
            _group = _panel.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            // Put the drag handler on the panel root. UGUI walks up the hierarchy from
            // the pressed graphic, so the header, rows and empty areas all drag the panel.
            PanelDrag panelDrag = _panel.gameObject.AddComponent<PanelDrag>();
            panelDrag.Target = _panel;
            panelDrag.Canvas = _canvas;
            panelDrag.DragEnded = OnDragEnded;

            _shadow = UiKit.ShadowImage(_panel, "Shadow");
            UiKit.Place(_shadow.rectTransform, -10f, -8f, Theme.PanelWidth + 20f, _height + 20f);

            _bg = UiKit.RoundedImage(_panel, "Background", Theme.PanelBg, 16f, true);
            UiKit.Place(_bg.rectTransform, 0f, 0f, Theme.PanelWidth, _height);

            _border = UiKit.BorderImage(_panel, "Border", Theme.Border, 16f, false);
            UiKit.Place(_border.rectTransform, 0f, 0f, Theme.PanelWidth, _height);

            _topAccent = UiKit.Image(_panel, "TopAccent", null, Theme.WithAlpha(Theme.Accent, 0.85f), false);
            UiKit.Place(_topAccent.rectTransform, Theme.Padding, 0f, Theme.PanelWidth - Theme.Padding * 2f, 2f);

            _header = UiKit.Rect(_panel, "Header");
            UiKit.Place(_header, 0f, 0f, Theme.PanelWidth, Theme.HeaderHeight);
            Image headerHit = UiKit.Image(_header, "HeaderHit", null, Color.clear, true);
            headerHit.type = UnityEngine.UI.Image.Type.Simple;
            UiKit.Place(headerHit.rectTransform, 0f, 0f, Theme.PanelWidth, Theme.HeaderHeight);
            _headerHit = headerHit.rectTransform;

            _dotGlow = UiKit.Image(_panel, "DotGlow", UiKit.Glow, Theme.WithAlpha(Theme.Accent, 0.20f), false);
            _dotGlow.type = UnityEngine.UI.Image.Type.Simple;
            UiKit.Place(_dotGlow.rectTransform, Theme.Padding - 5f, 11f, 18f, 18f);

            _dot = UiKit.RoundedImage(_panel, "Dot", Theme.Accent, 2f, false);
            RectTransform dotRect = _dot.rectTransform;
            dotRect.anchorMin = new Vector2(0f, 1f);
            dotRect.anchorMax = new Vector2(0f, 1f);
            dotRect.pivot = new Vector2(0.5f, 0.5f);
            dotRect.sizeDelta = new Vector2(7f, 7f);
            dotRect.anchoredPosition = new Vector2(Theme.Padding + 3.5f, -19.5f);
            dotRect.localEulerAngles = new Vector3(0f, 0f, 45f);

            _title = UiKit.Text(_panel, "Title", Strings.Title, 18f, Theme.Text, TextAlignmentOptions.MidlineLeft);
            _title.fontStyle = FontStyles.Bold;
            UiKit.Place(_title.rectTransform, Theme.Padding + 15f, 9f, 220f, 26f);

            CreateHeaderButton("CollapseButton", 10f, 46f, Strings.Collapse, CollapseToBall);
            CreateHeaderButton("ResetButton", 60f, 52f, Strings.Reset, ResetLayout);

            _subtitle = UiKit.Text(_panel, "Subtitle", string.Empty, 10f, Theme.Muted,
                TextAlignmentOptions.MidlineLeft);
            _subtitle.overflowMode = TextOverflowModes.Ellipsis;
            UiKit.Place(_subtitle.rectTransform, Theme.Padding, 36f, Theme.PanelWidth - Theme.Padding * 2f, 14f);

            Image divider = UiKit.Image(_panel, "HeaderDivider", null, Theme.WithAlpha(Theme.Text, 0.055f), false);
            UiKit.Place(divider.rectTransform, Theme.Padding, Theme.HeaderHeight - 2f,
                Theme.PanelWidth - Theme.Padding * 2f, 1f);
            _headerDivider = divider.rectTransform;

            _rowsRoot = UiKit.Rect(_panel, "Rows");
            UiKit.Place(_rowsRoot, Theme.Padding, Theme.RowsTop, Theme.ContentWidth, 0f);

            _emptyLabel = UiKit.Text(_panel, "Empty", Strings.Waiting, 10.5f, Theme.Faint,
                TextAlignmentOptions.Center);
            UiKit.Place(_emptyLabel.rectTransform, Theme.Padding, Theme.RowsTop + 4f, Theme.ContentWidth, 34f);

            _footerRoot = UiKit.Rect(_panel, "Footer");
            _footerLine = UiKit.Image(_footerRoot, "FooterLine", null, Theme.WithAlpha(Theme.Text, 0.045f), false);
            UiKit.Place(_footerLine.rectTransform, 0f, 0f, Theme.ContentWidth, 1f);
            _footerLabel = UiKit.Text(_footerRoot, "FooterText",
                Strings.FooterHint(DamageMeterPlugin.ToggleKeyName != null ? DamageMeterPlugin.ToggleKeyName.Value : "F10"),
                9f, Theme.Faint, TextAlignmentOptions.MidlineLeft);
            _footerLabel.overflowMode = TextOverflowModes.Ellipsis;
            UiKit.Place(_footerLabel.rectTransform, 0f, 5f, Theme.ContentWidth, 16f);
            UiKit.Place(_footerRoot, Theme.Padding, _height - Theme.FooterHeight, Theme.ContentWidth, Theme.FooterHeight);

            BuildResizeHandle();
            BuildBall();

            _built = true;
            ApplyLayout(0f, true);
            ClampPosition();
            ClampBallPosition();
            DamageMeterPlugin.Log.LogInfo("Damage meter HUD built.");
        }

        private HudButton CreateHeaderButton(string name, float right, float width, string label, Action action)
        {
            Image background = UiKit.RoundedImage(_panel, name, Color.clear, 6f, true);
            UiKit.PlaceRight(background.rectTransform, right, 11f, width, 23f);
            TextMeshProUGUI text = UiKit.Text(background.transform, "Label", label, 9.5f, Theme.Muted,
                TextAlignmentOptions.Center);
            UiKit.Place(text.rectTransform, 0f, 0f, width, 23f);
            HudButton button = background.gameObject.AddComponent<HudButton>();
            button.Init(background, text, action);
            return button;
        }

        private void BuildResizeHandle()
        {
            Sprite grip = UiKit.ResizeGrip;
            if (grip == null)
            {
                return;
            }

            _resizeGrip = UiKit.Image(_panel, "ResizeGrip", grip, new Color(1f, 1f, 1f, 0.55f), true);
            _resizeGrip.type = UnityEngine.UI.Image.Type.Simple;
            RectTransform rect = _resizeGrip.rectTransform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(22f, 22f);
            rect.anchoredPosition = new Vector2(-6f, 6f);

            ResizeHandle handle = _resizeGrip.gameObject.AddComponent<ResizeHandle>();
            handle.Owner = this;
            handle.Grip = _resizeGrip;

            _resizeLabel = UiKit.Text(_panel, "ResizeLabel", string.Empty, 8.5f, Theme.Muted,
                TextAlignmentOptions.MidlineRight);
            RectTransform labelRect = _resizeLabel.rectTransform;
            labelRect.anchorMin = new Vector2(1f, 0f);
            labelRect.anchorMax = new Vector2(1f, 0f);
            labelRect.pivot = new Vector2(1f, 0f);
            labelRect.sizeDelta = new Vector2(96f, 18f);
            labelRect.anchoredPosition = new Vector2(-34f, 6f);
            _resizeLabel.gameObject.SetActive(false);
        }

        private void BuildBall()
        {
            Sprite ballSprite = UiKit.Ball;
            Color ballColor = Color.white;
            if (ballSprite == null)
            {
                ballSprite = UiKit.Circle;
                ballColor = new Color(0.05f, 0.07f, 0.10f, 1f);
            }
            if (ballSprite == null)
            {
                return;
            }

            _ballRoot = UiKit.Rect(_canvasGo.transform, "Ball");
            _ballRoot.anchorMin = new Vector2(1f, 0f);
            _ballRoot.anchorMax = new Vector2(1f, 0f);
            _ballRoot.pivot = new Vector2(1f, 0f);
            _ballRoot.sizeDelta = new Vector2(BallSize, BallSize);
            _ballRoot.anchoredPosition = new Vector2(
                DamageMeterPlugin.BallX != null ? DamageMeterPlugin.BallX.Value : DamageMeterPlugin.DefaultBallX,
                DamageMeterPlugin.BallY != null ? DamageMeterPlugin.BallY.Value : DamageMeterPlugin.DefaultBallY);

            _ballGroup = _ballRoot.gameObject.AddComponent<CanvasGroup>();
            _ballGroup.alpha = 0f;
            _ballGroup.blocksRaycasts = false;
            _ballGroup.interactable = false;

            _ballVisual = UiKit.Rect(_ballRoot, "Visual");
            _ballVisual.anchorMin = new Vector2(0.5f, 0.5f);
            _ballVisual.anchorMax = new Vector2(0.5f, 0.5f);
            _ballVisual.pivot = new Vector2(0.5f, 0.5f);
            _ballVisual.anchoredPosition = Vector2.zero;
            _ballVisual.sizeDelta = new Vector2(BallSize, BallSize);

            _ballGlow = UiKit.Image(_ballVisual, "Glow", UiKit.Glow, Theme.WithAlpha(Theme.Accent, 0.10f), false);
            _ballGlow.type = UnityEngine.UI.Image.Type.Simple;
            UiKit.Place(_ballGlow.rectTransform, (BallSize - 76f) * 0.5f, (BallSize - 76f) * 0.5f, 76f, 76f);

            _ballBg = UiKit.Image(_ballVisual, "Bg", ballSprite, ballColor, true);
            _ballBg.type = UnityEngine.UI.Image.Type.Simple;
            UiKit.Place(_ballBg.rectTransform, 0f, 0f, BallSize, BallSize);

            Sprite hoverSprite = UiKit.Circle;
            if (hoverSprite != null)
            {
                _ballHoverOverlay = UiKit.Image(_ballVisual, "Hover", hoverSprite, Color.clear, false);
                _ballHoverOverlay.type = UnityEngine.UI.Image.Type.Simple;
                UiKit.Place(_ballHoverOverlay.rectTransform, 4f, 4f, BallSize - 8f, BallSize - 8f);
            }

            Sprite ringSprite = UiKit.Ring;
            if (ringSprite != null)
            {
                _ballRing = UiKit.Image(_ballVisual, "Ring", ringSprite,
                    Theme.WithAlpha(Theme.Accent, 0.85f), false);
                _ballRing.type = UnityEngine.UI.Image.Type.Simple;
                UiKit.Place(_ballRing.rectTransform, 0f, 0f, BallSize, BallSize);
            }

            _ballValue = UiKit.Text(_ballVisual, "Value", "0.0", 14f, Theme.Text,
                TextAlignmentOptions.Center);
            _ballValue.fontStyle = FontStyles.Bold;
            _ballValue.enableAutoSizing = true;
            _ballValue.fontSizeMin = 9f;
            _ballValue.fontSizeMax = 15f;
            _ballValue.overflowMode = TextOverflowModes.Ellipsis;
            UiKit.Place(_ballValue.rectTransform, 4f, 15f, BallSize - 8f, 24f);

            _ballLabel = UiKit.Text(_ballVisual, "Label", Strings.BallLabel, 8f, Theme.Muted,
                TextAlignmentOptions.Center);
            _ballLabel.characterSpacing = 1.2f;
            UiKit.Place(_ballLabel.rectTransform, 0f, 39f, BallSize, 10f);

            BallWidget widget = _ballBg.gameObject.AddComponent<BallWidget>();
            widget.Owner = this;
        }

        public void UpdateUI(DamageTracker tracker, float deltaTime)
        {
            if (!_built || tracker == null)
            {
                return;
            }

            _age += deltaTime;
            UpdateViewportClamp();

            List<WeaponStat> stats = tracker.BuildVisibleRows();
            double weaponTotal = DamageTracker.SumDamage(stats);
            bool otherVisible = tracker.ShowOther;

            double maxTotal = 0.0;
            for (int i = 0; i < stats.Count; i++)
            {
                double value = stats[i].IsSupport ? stats[i].AssistDamage : stats[i].TotalDamage;
                if (value > maxTotal)
                {
                    maxTotal = value;
                }
            }
            if (otherVisible && tracker.OtherDamage > maxTotal)
            {
                maxTotal = tracker.OtherDamage;
            }

            bool compact = DamageMeterPlugin.UseCompactNumbers == null || DamageMeterPlugin.UseCompactNumbers.Value;
            SyncRows(stats);
            float y = 0f;
            for (int i = 0; i < stats.Count; i++)
            {
                MeterRow row;
                if (!_rowsByDevice.TryGetValue(stats[i].Device, out row))
                {
                    continue;
                }
                row.SetExpanded(_expanded.Contains(stats[i].Device));
                row.SetWidth(_contentWidth);
                row.SetWeapon(stats[i]);
                row.UpdateWeapon(stats[i], tracker, maxTotal, weaponTotal, compact, deltaTime);
                row.SetTargetY(y);
                row.UpdatePosition(deltaTime);
                y += row.Height + Theme.RowGap;
            }

            double allDamage = weaponTotal + tracker.OtherDamage;
            int rowCount = stats.Count;
            if (otherVisible)
            {
                if (_otherRow == null)
                {
                    _otherRow = new MeterRow(_rowsRoot, true, _contentWidth);
                }
                double otherShare = allDamage > 0.0 ? tracker.OtherDamage / allDamage : 0.0;
                _otherRow.SetWidth(_contentWidth);
                _otherRow.UpdateOther(tracker.OtherDamage, tracker.OtherDps, otherShare, maxTotal, compact, deltaTime);
                _otherRow.SetTargetY(y);
                _otherRow.UpdatePosition(deltaTime);
                y += _otherRow.Height + Theme.RowGap;
                rowCount++;
            }
            else if (_otherRow != null)
            {
                _otherRow.Destroy();
                _otherRow = null;
            }

            bool showEmpty = rowCount == 0;
            if (_emptyLabel.gameObject.activeSelf != showEmpty)
            {
                _emptyLabel.gameObject.SetActive(showEmpty);
            }
            if (rowCount == 0)
            {
                string emptyText = tracker.WeaponStatCount > 0 ? Strings.NoDamage : Strings.Waiting;
                if (!string.Equals(_emptyLabel.text, emptyText, StringComparison.Ordinal))
                {
                    _emptyLabel.text = emptyText;
                }
            }

            double avgDps = weaponTotal / Math.Max(1.0, tracker.SessionSeconds);
            UpdateBall(avgDps, compact);
            string subtitle = Strings.HeaderLine(
                Strings.Time(tracker.SessionSeconds),
                Strings.Number(weaponTotal, compact),
                Strings.Number1(avgDps, compact));
            if (!string.Equals(_lastSubtitle, subtitle, StringComparison.Ordinal))
            {
                _lastSubtitle = subtitle;
                _subtitle.text = subtitle;
            }

            float contentHeight = y > 0f ? Mathf.Max(0f, y - Theme.RowGap) : 34f;
            _targetHeight = Theme.RowsTop + contentHeight + 12f + Theme.FooterHeight + 4f;

            ApplyLayout(deltaTime, false);
            UpdateFade(deltaTime);
        }

        private void SyncRows(List<WeaponStat> stats)
        {
            _alive.Clear();
            for (int i = 0; i < stats.Count; i++)
            {
                Device device = stats[i].Device;
                if (device == null)
                {
                    continue;
                }
                _alive.Add(device);
                if (!_rowsByDevice.ContainsKey(device))
                {
                    MeterRow row = new MeterRow(_rowsRoot, false, _contentWidth);
                    row.Clicked = OnRowClicked;
                    _rowsByDevice.Add(device, row);
                }
            }

            List<Device> removing = null;
            foreach (KeyValuePair<Device, MeterRow> pair in _rowsByDevice)
            {
                if (!_alive.Contains(pair.Key))
                {
                    if (removing == null)
                    {
                        removing = new List<Device>();
                    }
                    removing.Add(pair.Key);
                }
            }

            if (removing != null)
            {
                for (int i = 0; i < removing.Count; i++)
                {
                    MeterRow row = _rowsByDevice[removing[i]];
                    row.Destroy();
                    _rowsByDevice.Remove(removing[i]);
                    _expanded.Remove(removing[i]);
                }
            }
        }

        private void OnRowClicked(MeterRow row)
        {
            if (row == null || row.Device == null)
            {
                return;
            }

            if (!_expanded.Add(row.Device))
            {
                _expanded.Remove(row.Device);
            }
        }

        private void CollapseToBall()
        {
            if (CollapseRequested != null)
            {
                CollapseRequested();
            }
        }

        private void ResetLayout()
        {
            _scaleOverride = -1f;
            _widthFactorOverride = -1f;
            _resizing = false;
            if (_resizeLabel != null)
            {
                _resizeLabel.gameObject.SetActive(false);
            }

            if (DamageMeterPlugin.PanelScale != null)
            {
                DamageMeterPlugin.PanelScale.Value = DamageMeterPlugin.DefaultPanelScale;
            }
            if (DamageMeterPlugin.PanelWidthScale != null)
            {
                DamageMeterPlugin.PanelWidthScale.Value = DamageMeterPlugin.DefaultPanelWidthScale;
            }
            if (_panel != null)
            {
                _panel.anchoredPosition = new Vector2(
                    DamageMeterPlugin.DefaultPanelX, DamageMeterPlugin.DefaultPanelY);
            }
            if (_ballRoot != null)
            {
                _ballRoot.anchoredPosition = new Vector2(
                    DamageMeterPlugin.DefaultBallX, DamageMeterPlugin.DefaultBallY);
            }

            ApplyLayout(0f, true);
            ClampPosition();
            ClampBallPosition();

            if (_panel != null)
            {
                if (DamageMeterPlugin.PanelX != null)
                {
                    DamageMeterPlugin.PanelX.Value = _panel.anchoredPosition.x;
                }
                if (DamageMeterPlugin.PanelY != null)
                {
                    DamageMeterPlugin.PanelY.Value = _panel.anchoredPosition.y;
                }
            }
            if (_ballRoot != null)
            {
                if (DamageMeterPlugin.BallX != null)
                {
                    DamageMeterPlugin.BallX.Value = _ballRoot.anchoredPosition.x;
                }
                if (DamageMeterPlugin.BallY != null)
                {
                    DamageMeterPlugin.BallY.Value = _ballRoot.anchoredPosition.y;
                }
            }
            SaveLayout();
        }

        private void ApplyLayout(float deltaTime, bool immediate)
        {
            float uiScale = EffectiveScale;
            float widthFactor = ClampWidthFactor(EffectiveWidthFactor, uiScale);
            _localWidth = Theme.PanelWidth * widthFactor / uiScale;
            _contentWidth = _localWidth - Theme.Padding * 2f;

            _panel.localScale = new Vector3(uiScale, uiScale, 1f);
            if (_ballRoot != null)
            {
                _ballRoot.localScale = new Vector3(uiScale, uiScale, 1f);
            }

            float smooth = immediate || deltaTime <= 0f ? 1f : 1f - Mathf.Exp(-16f * deltaTime);
            _height = Mathf.Lerp(_height, _targetHeight, smooth);
            _panel.sizeDelta = new Vector2(_localWidth, _height);
            _shadow.rectTransform.sizeDelta = new Vector2(_localWidth + 20f, _height + 20f);
            _bg.rectTransform.sizeDelta = new Vector2(_localWidth, _height);
            _border.rectTransform.sizeDelta = new Vector2(_localWidth, _height);

            if (_topAccent != null)
            {
                _topAccent.rectTransform.sizeDelta = new Vector2(_contentWidth, 2f);
            }
            if (_header != null)
            {
                _header.sizeDelta = new Vector2(_localWidth, Theme.HeaderHeight);
            }
            if (_headerHit != null)
            {
                _headerHit.sizeDelta = new Vector2(_localWidth, Theme.HeaderHeight);
            }
            if (_title != null)
            {
                float titleWidth = Mathf.Max(80f, _localWidth - (Theme.Padding + 15f) - 120f);
                UiKit.Place(_title.rectTransform, Theme.Padding + 15f, 9f, titleWidth, 26f);
            }
            if (_subtitle != null)
            {
                UiKit.Place(_subtitle.rectTransform, Theme.Padding, 36f, _contentWidth, 14f);
            }
            if (_headerDivider != null)
            {
                UiKit.Place(_headerDivider, Theme.Padding, Theme.HeaderHeight - 2f, _contentWidth, 1f);
            }
            if (_rowsRoot != null)
            {
                _rowsRoot.sizeDelta = new Vector2(_contentWidth,
                    Mathf.Max(0f, _height - Theme.RowsTop - Theme.FooterHeight));
                foreach (MeterRow row in _rowsByDevice.Values)
                {
                    row.SetWidth(_contentWidth);
                }
                if (_otherRow != null)
                {
                    _otherRow.SetWidth(_contentWidth);
                }
            }
            if (_emptyLabel != null)
            {
                UiKit.Place(_emptyLabel.rectTransform, Theme.Padding, Theme.RowsTop + 4f, _contentWidth, 34f);
            }
            if (_footerRoot != null)
            {
                UiKit.Place(_footerRoot, Theme.Padding, _height - Theme.FooterHeight, _contentWidth,
                    Theme.FooterHeight);
            }
            if (_footerLine != null)
            {
                _footerLine.rectTransform.sizeDelta = new Vector2(_contentWidth, 1f);
            }
            if (_footerLabel != null)
            {
                _footerLabel.rectTransform.sizeDelta = new Vector2(_contentWidth, 16f);
            }
        }

        private void UpdateFade(float deltaTime)
        {
            float opacity = DamageMeterPlugin.Opacity != null ? DamageMeterPlugin.Opacity.Value : 0.96f;
            float target = _shown ? opacity : 0f;
            float smooth = deltaTime > 0f ? 1f - Mathf.Exp(-14f * deltaTime) : 1f;
            _group.alpha = Mathf.Lerp(_group.alpha, target, smooth);
            bool interactive = _shown && _group.alpha > 0.05f;
            _group.blocksRaycasts = interactive;
            _group.interactable = interactive;

            if (_ballGroup != null)
            {
                float ballTarget = _ballShown ? opacity : 0f;
                _ballAlpha = Mathf.Lerp(_ballAlpha, ballTarget, smooth);
                _ballGroup.alpha = _ballAlpha;
                bool ballInteractive = _ballShown && _ballAlpha > 0.05f;
                _ballGroup.blocksRaycasts = ballInteractive;
                _ballGroup.interactable = ballInteractive;
            }
        }

        private void OnDragEnded(Vector2 position)
        {
            _panel.anchoredPosition = position;
            ClampPosition();
            if (DamageMeterPlugin.PanelX != null)
            {
                DamageMeterPlugin.PanelX.Value = _panel.anchoredPosition.x;
            }
            if (DamageMeterPlugin.PanelY != null)
            {
                DamageMeterPlugin.PanelY.Value = _panel.anchoredPosition.y;
            }
            SaveLayout();
        }

        private void UpdateViewportClamp()
        {
            int width = Screen.width;
            int height = Screen.height;
            bool active = Letterbox.IsActive;
            Rect view = Letterbox.PixelRect;
            if (width != _lastScreenWidth || height != _lastScreenHeight
                || active != _lastLetterboxActive || view != _lastViewRect)
            {
                _lastScreenWidth = width;
                _lastScreenHeight = height;
                _lastLetterboxActive = active;
                _lastViewRect = view;
                ClampPosition();
                ClampBallPosition();
            }
        }

        private Vector2 GetCanvasSize()
        {
            float canvasScale = _canvas != null ? Mathf.Max(0.01f, _canvas.scaleFactor) : 1f;
            Vector2 size = _canvasRect != null ? _canvasRect.rect.size : Vector2.zero;
            if (size.x <= 0f || size.y <= 0f)
            {
                size = new Vector2(Screen.width / canvasScale, Screen.height / canvasScale);
            }
            return size;
        }

        private void GetViewportMargins(out float left, out float right, out float bottom, out float top)
        {
            float canvasScale = _canvas != null ? Mathf.Max(0.01f, _canvas.scaleFactor) : 1f;
            left = 0f;
            right = 0f;
            bottom = 0f;
            top = 0f;
            Rect view = Letterbox.PixelRect;
            if (Letterbox.IsActive && view.width > 1f && view.height > 1f)
            {
                left = view.xMin / canvasScale;
                right = (Screen.width - view.xMax) / canvasScale;
                bottom = view.yMin / canvasScale;
                top = (Screen.height - view.yMax) / canvasScale;
            }
        }

        private void ClampPosition()
        {
            if (_panel == null || _canvasRect == null)
            {
                return;
            }

            float uiScale = EffectiveScale;
            float widthFactor = ClampWidthFactor(EffectiveWidthFactor, uiScale);
            float margin = 8f;
            Vector2 size = GetCanvasSize();
            float left;
            float right;
            float bottom;
            float top;
            GetViewportMargins(out left, out right, out bottom, out top);

            float panelWidth = Theme.PanelWidth * widthFactor;
            float panelHeight = _height * uiScale;
            float minX = -(size.x - left - margin) + panelWidth;
            float maxX = -(right + margin);
            float minY = -(size.y - bottom - margin) + panelHeight;
            float maxY = -(top + margin);

            Vector2 pos = _panel.anchoredPosition;
            pos.x = minX <= maxX ? Mathf.Clamp(pos.x, minX, maxX) : minX;
            pos.y = minY <= maxY ? Mathf.Clamp(pos.y, minY, maxY) : minY;
            _panel.anchoredPosition = pos;
        }

        private void ClampBallPosition()
        {
            if (_ballRoot == null || _canvasRect == null)
            {
                return;
            }

            float scale = EffectiveScale;
            float margin = 8f;
            Vector2 size = GetCanvasSize();
            float left;
            float right;
            float bottom;
            float top;
            GetViewportMargins(out left, out right, out bottom, out top);

            float ball = BallSize * scale;
            float minX = -(size.x - left - margin) + ball;
            float maxX = -(right + margin);
            float minY = bottom + margin;
            float maxY = size.y - top - margin - ball;

            Vector2 pos = _ballRoot.anchoredPosition;
            pos.x = minX <= maxX ? Mathf.Clamp(pos.x, minX, maxX) : minX;
            pos.y = minY <= maxY ? Mathf.Clamp(pos.y, minY, maxY) : maxY;
            _ballRoot.anchoredPosition = pos;
        }

        public void SetBallShown(bool shown)
        {
            _ballShown = shown;
        }

        public void OpenFromBall()
        {
            if (BallClicked != null)
            {
                BallClicked();
            }
        }

        public void BallDrag(PointerEventData eventData)
        {
            if (_ballRoot == null)
            {
                return;
            }
            float scale = _canvas != null ? Mathf.Max(0.01f, _canvas.scaleFactor) : 1f;
            _ballRoot.anchoredPosition += eventData.delta / scale;
            ClampBallPosition();
        }

        public void BallDragEnd()
        {
            if (_ballRoot == null)
            {
                return;
            }
            ClampBallPosition();
            if (DamageMeterPlugin.BallX != null)
            {
                DamageMeterPlugin.BallX.Value = _ballRoot.anchoredPosition.x;
            }
            if (DamageMeterPlugin.BallY != null)
            {
                DamageMeterPlugin.BallY.Value = _ballRoot.anchoredPosition.y;
            }
            SaveLayout();
        }

        public void BallHover(bool hover)
        {
            _ballHover = hover;
        }

        private void UpdateBall(double dps, bool compact)
        {
            if (_ballValue == null)
            {
                return;
            }

            string value = Strings.Number1(dps, compact);
            if (value.Length > 6)
            {
                value = Strings.Number(dps, true);
            }
            if (!string.Equals(_lastBallValue, value, StringComparison.Ordinal))
            {
                _lastBallValue = value;
                _ballValue.text = value;
            }

            if (_ballGlow != null)
            {
                _ballGlow.color = Theme.WithAlpha(Theme.Accent, _ballHover ? 0.18f : 0.10f);
            }
            if (_ballRing != null)
            {
                _ballRing.color = Theme.WithAlpha(Theme.Accent, _ballHover ? 1f : 0.85f);
            }
            if (_ballHoverOverlay != null)
            {
                _ballHoverOverlay.color = new Color(1f, 1f, 1f, _ballHover ? 0.07f : 0f);
            }
            if (_ballBg != null)
            {
                Color baseColor = UiKit.Ball != null
                    ? Color.white
                    : new Color(0.05f, 0.07f, 0.10f, 1f);
                _ballBg.color = _ballHover
                    ? Color.Lerp(baseColor, new Color(0.90f, 0.95f, 1f, 1f), 0.35f)
                    : baseColor;
            }
            if (_ballValue != null)
            {
                _ballValue.color = _ballHover ? Color.white : Theme.Text;
            }
            if (_ballLabel != null)
            {
                _ballLabel.color = _ballHover ? Theme.Text : Theme.Muted;
            }
        }

        public void ResizeBegin(Vector2 screenPosition)
        {
            if (_panel == null)
            {
                return;
            }
            float canvasScale = _canvas != null ? Mathf.Max(0.01f, _canvas.scaleFactor) : 1f;
            _resizeStartScale = Mathf.Clamp(EffectiveScale, MinScale, MaxScale);
            _resizeStartWidthFactor = ClampWidthFactor(EffectiveWidthFactor, _resizeStartScale);
            _resizeStartPointerX = screenPosition.x;
            _resizeStartPointerY = screenPosition.y;
            _resizeStartWidthPx = Theme.PanelWidth * _resizeStartWidthFactor * canvasScale;
            _resizeStartLocalHeight = Mathf.Max(1f, _height);
            _resizeStartRenderedHeightPx = _resizeStartLocalHeight * _resizeStartScale * canvasScale;
            _resizing = true;
            UpdateResizeLabel(_resizeStartWidthFactor, _resizeStartScale);
            if (_resizeLabel != null)
            {
                _resizeLabel.gameObject.SetActive(true);
            }
        }

        public void ResizeDrag(Vector2 screenPosition)
        {
            if (_panel == null)
            {
                return;
            }
            if (!_resizing)
            {
                ResizeBegin(screenPosition);
            }

            float canvasScale = _canvas != null ? Mathf.Max(0.01f, _canvas.scaleFactor) : 1f;

            // Horizontal drag changes width; vertical drag changes UI/row height.
            // Both are monotonic deltas from the drag start, so crossing the panel
            // center never reverses direction.
            float widthPx = _resizeStartWidthPx + (_resizeStartPointerX - screenPosition.x);
            float widthFactor = widthPx / (Theme.PanelWidth * canvasScale);

            float heightPx = _resizeStartRenderedHeightPx + (_resizeStartPointerY - screenPosition.y);
            float uiScale = heightPx / (_resizeStartLocalHeight * canvasScale);

            uiScale = Mathf.Clamp(uiScale, MinScale, MaxScale);
            widthFactor = ClampWidthFactor(widthFactor, uiScale);

            _widthFactorOverride = widthFactor;
            _scaleOverride = uiScale;
            UpdateResizeLabel(widthFactor, uiScale);
            ApplyLayout(0f, true);
        }

        public void ResizeEnd()
        {
            if (!_resizing)
            {
                return;
            }
            _resizing = false;
            float uiScale = Mathf.Clamp(EffectiveScale, MinScale, MaxScale);
            float widthFactor = ClampWidthFactor(EffectiveWidthFactor, uiScale);
            if (DamageMeterPlugin.PanelScale != null)
            {
                DamageMeterPlugin.PanelScale.Value = uiScale;
            }
            if (DamageMeterPlugin.PanelWidthScale != null)
            {
                DamageMeterPlugin.PanelWidthScale.Value = widthFactor;
            }
            _scaleOverride = -1f;
            _widthFactorOverride = -1f;
            if (_resizeLabel != null)
            {
                _resizeLabel.gameObject.SetActive(false);
            }
            ApplyLayout(0f, true);
            ClampPosition();
            ClampBallPosition();
            SaveLayout();
        }

        private void UpdateResizeLabel(float widthFactor, float uiScale)
        {
            if (_resizeLabel == null)
            {
                return;
            }
            _resizeLabel.text = Mathf.RoundToInt(widthFactor * 100f) + "%  ×  "
                + Mathf.RoundToInt(uiScale * 100f) + "%";
        }

        private static void SaveLayout()
        {
            if (DamageMeterPlugin.Instance != null)
            {
                DamageMeterPlugin.Instance.Config.Save();
            }
        }

        public void Destroy()
        {
            if (_canvasGo != null)
            {
                UnityEngine.Object.Destroy(_canvasGo);
                _canvasGo = null;
            }
            _rowsByDevice.Clear();
            _otherRow = null;
            _built = false;
            FontProvider.Reset();
            UiKit.ResetIconCache();
        }
    }
}
