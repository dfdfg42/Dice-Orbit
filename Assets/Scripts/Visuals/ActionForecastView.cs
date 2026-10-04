using System.Collections.Generic;
using System.Text;
using DiceOrbit.Core;
using DiceOrbit.Core.Forecast;
using DiceOrbit.Data;
using DiceOrbit.Data.Tile;
using DiceOrbit.UI;
using DiceOrbit.UI.Skin;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceOrbit.Visuals
{
    /// <summary>
    /// 행동 예고 표시 (2026-10-04) — 캐릭터와 주사위를 고른 순간, 그 주사위로 벌어질 일을 한꺼번에 보여 준다.
    ///   · 이동 경로 + 도착 타일 (MovePathPreview)
    ///   · 도착 타일 → 대상 몬스터 조준 포물선 (DashedArcLine)
    ///   · 대상 몬스터 몸 위에 예상 피해 숫자 (피해 팝업과 같은 글꼴)
    ///   · [이동] 버튼 위 예고 카드 — 공격 종류·대상별 결과·콤보 변화·경로/턴 종료 타일 효과·도착지 위험
    ///
    /// 수치는 전부 ActionForecaster.Build에서 온다 — 여기에는 계산이 없다.
    /// 표시 중에는 refreshInterval마다 다시 계산해, 다른 캐릭터의 행동으로 전황이 바뀌면 따라간다.
    /// 플레이어 턴이 아니거나 예고를 만들 수 없으면 전부 숨긴다.
    /// </summary>
    public class ActionForecastView : MonoBehaviour
    {
        public static ActionForecastView Instance { get; private set; }

        [SerializeField] private float refreshInterval = 0.25f;

        [Header("조준선")]
        [Tooltip("이동 경로(체브론)와 같은 노랑 — \"저기로 가서 → 이걸 친다\"가 한 줄로 이어진다. 구역 플레이트 색(초록·연어·보라)과도 안 겹친다")]
        [SerializeField] private Color aimColor = new Color(1f, 0.92f, 0.35f, 0.95f);
        [SerializeField] private float aimWidth = 0.36f;
        [Tooltip("도착 타일 위 조준선 시작 높이 (캐릭터 가슴께)")]
        [SerializeField] private float aimStartHeight = 0.8f;

        [Header("예상 피해 숫자")]
        [SerializeField] private float tagFontSize = 14f;
        [SerializeField] private Color tagDamageColor = new Color(1f, 0.36f, 0.3f);
        [SerializeField] private Color tagLethalColor = new Color(1f, 0.88f, 0.25f);
        [SerializeField] private Color tagBlockedColor = new Color(0.62f, 0.76f, 0.95f);

        [Header("예고 카드")]
        [SerializeField] private float titleFontSize = 26f;
        [SerializeField] private float bodyFontSize = 21f;
        [SerializeField] private Color goodColor = new Color(0.16f, 0.5f, 0.27f);
        [Tooltip("[이동] 버튼 윗변에서 카드 아랫변까지의 간격(화면 픽셀)")]
        [SerializeField] private float cardGap = 14f;
        [SerializeField] private int cardSortingOrder = 30;

        private Character _character;
        private int _diceValue;
        private float _nextRefresh;

        private ActionForecast _forecast;
        /// <summary>지금 표시 중인 예고 (없으면 null).</summary>
        public ActionForecast Current => _forecast;

        // 경로 — 같은 경로면 다시 만들지 않는다 (MovePathPreview.Show가 리프트 연출을 처음부터 다시 돌린다)
        private TileData _shownPathStart, _shownPathEnd;
        private int _shownPathCount = -1;

        private readonly List<LineRenderer> _arcs = new List<LineRenderer>();
        private readonly List<LineRenderer> _arrows = new List<LineRenderer>();
        private readonly List<TextMeshPro> _tags = new List<TextMeshPro>();

        private RectTransform _card;
        private TextMeshProUGUI _cardText;
        private readonly List<ForecastLine> _lines = new List<ForecastLine>();
        private readonly StringBuilder _builder = new StringBuilder();
        private readonly Vector3[] _corners = new Vector3[4];
        private Camera _camera;

        // ── 수명 ─────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_card != null) Destroy(_card.root.gameObject);
        }

        public static ActionForecastView EnsureInstance()
        {
            if (Instance != null) return Instance;
            return new GameObject("[ActionForecastView]").AddComponent<ActionForecastView>();
        }

        // ── 공개 API ──────────────────────────────────────────

        /// <summary>이 캐릭터가 이 주사위 눈으로 움직였을 때의 예고를 띄운다. 같은 요청을 다시 넘겨도 된다.</summary>
        public void Show(Character character, int diceValue)
        {
            bool same = character == _character && diceValue == _diceValue;
            _character = character;
            _diceValue = diceValue;
            if (!same) Rebuild();
        }

        public void Hide()
        {
            _character = null;
            _forecast = null;
            ClearVisuals();
        }

        // ── 갱신 ─────────────────────────────────────────────

        private void Update()
        {
            if (_character == null)
            {
                if (_forecast != null) Hide();   // 조회하던 캐릭터가 파괴됐다
                return;
            }
            if (Time.unscaledTime >= _nextRefresh) Rebuild();
        }

        private void LateUpdate()
        {
            if (_forecast == null) return;
            PlaceCard();
            FaceTagsToCamera();
        }

        private void Rebuild()
        {
            _nextRefresh = Time.unscaledTime + refreshInterval;

            var combat = CombatManager.Instance;
            bool playerTurn = combat != null && combat.PlayerTurnActive;
            _forecast = playerTurn ? ActionForecaster.Build(_character, _diceValue) : null;

            if (_forecast == null)
            {
                ClearVisuals();
                return;
            }

            SyncPath(_forecast);
            SyncAims(_forecast);
            SyncTags(_forecast);
            SyncCard(_forecast);
        }

        private void ClearVisuals()
        {
            if (_shownPathCount >= 0)
            {
                MovePathPreview.Instance?.Hide();
                _shownPathCount = -1;
                _shownPathStart = _shownPathEnd = null;
            }
            for (int i = 0; i < _arcs.Count; i++) DashedArcLine.SetVisible(_arcs[i], _arrows[i], false);
            foreach (var tag in _tags) if (tag != null) tag.gameObject.SetActive(false);
            if (_card != null) _card.gameObject.SetActive(false);
        }

        // ── 경로 ─────────────────────────────────────────────

        private void SyncPath(ActionForecast forecast)
        {
            var path = forecast.Path;
            var start = path.Count > 0 ? path[0] : null;
            var end = path.Count > 0 ? path[path.Count - 1] : null;
            if (path.Count == _shownPathCount && start == _shownPathStart && end == _shownPathEnd) return;

            _shownPathCount = path.Count;
            _shownPathStart = start;
            _shownPathEnd = end;

            MovePathPreview.EnsureInstance();
            if (path.Count > 0) MovePathPreview.Instance.Show(path);
            else MovePathPreview.Instance.Hide();
        }

        // ── 조준선 ───────────────────────────────────────────

        private void SyncAims(ActionForecast forecast)
        {
            Vector3 from = forecast.Destination.Position + Vector3.up * aimStartHeight;

            int count = forecast.Targets.Count;
            while (_arcs.Count < count)
            {
                _arcs.Add(DashedArcLine.CreateArc(transform, aimWidth));
                _arrows.Add(DashedArcLine.CreateArrow(transform, aimWidth));
            }

            for (int i = 0; i < _arcs.Count; i++)
            {
                bool used = i < count && forecast.Targets[i].Target != null;
                DashedArcLine.SetVisible(_arcs[i], _arrows[i], used);
                if (!used) continue;
                DashedArcLine.SetArcWithArrow(_arcs[i], _arrows[i], from, BodyCenter(forecast.Targets[i].Target), aimColor);
            }
        }

        // ── 예상 피해 숫자 ───────────────────────────────────

        private void SyncTags(ActionForecast forecast)
        {
            int count = forecast.Targets.Count;
            while (_tags.Count < count) _tags.Add(CreateTag());

            for (int i = 0; i < _tags.Count; i++)
            {
                var tag = _tags[i];
                bool used = i < count && forecast.Targets[i].Target != null;
                if (tag.gameObject.activeSelf != used) tag.gameObject.SetActive(used);
                if (!used) continue;

                var result = forecast.Targets[i];
                tag.text = ActionForecastText.TargetTag(result);
                tag.color = result.Lethal ? tagLethalColor : result.HpLoss > 0 ? tagDamageColor : tagBlockedColor;
                tag.transform.position = BodyCenter(result.Target);
            }
        }

        private TextMeshPro CreateTag()
        {
            var go = new GameObject("_ForecastDamage");
            go.transform.SetParent(transform, false);
            var text = go.AddComponent<TextMeshPro>();
            FloatingLabelPopup.ApplyPopupStyle(text);   // 실제 피해 팝업과 같은 글꼴·외곽선
            text.fontSize = tagFontSize;
            text.sortingOrder = 190;                     // 유닛 위, 실제 피해 팝업(200) 아래
            text.textWrappingMode = TextWrappingModes.NoWrap;
            go.SetActive(false);
            return text;
        }

        private void FaceTagsToCamera()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;
            foreach (var tag in _tags)
                if (tag != null && tag.gameObject.activeSelf) tag.transform.rotation = _camera.transform.rotation;
        }

        /// <summary>유닛의 보이는 스프라이트 한가운데 (없으면 발 위 1).</summary>
        private static Vector3 BodyCenter(Unit unit)
        {
            var sprite = unit.SpriteRenderer;
            return sprite != null && sprite.sprite != null ? sprite.bounds.center : unit.transform.position + Vector3.up;
        }

        // ── 카드 ─────────────────────────────────────────────

        private void SyncCard(ActionForecast forecast)
        {
            if (_card == null) BuildCard();

            ActionForecastText.BuildCardLines(forecast, _lines);
            var skin = UiSkin.Current;

            _builder.Clear();
            for (int i = 0; i < _lines.Count; i++)
            {
                var line = _lines[i];
                if (i > 0) _builder.Append('\n');

                Color color = line.Tone == ForecastTone.Bad ? skin.Danger : line.Tone == ForecastTone.Good ? goodColor : skin.Ink;
                _builder.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(color)).Append('>');
                if (line.Title) _builder.Append("<size=").Append(titleFontSize.ToString("0")).Append("><b>").Append(line.Text).Append("</b></size>");
                else _builder.Append(line.Text);
                _builder.Append("</color>");
            }

            string text = _builder.ToString();
            if (_cardText.text != text)
            {
                _cardText.text = text;
                LayoutRebuilder.ForceRebuildLayoutImmediate(_card);
            }
            if (!_card.gameObject.activeSelf) _card.gameObject.SetActive(true);
            PlaceCard();
        }

        /// <summary>
        /// 카드를 [이동] 버튼 바로 위에 둔다 (버튼이 패널과 함께 미끄러져 들어오므로 매 프레임 따라간다).
        /// 오른쪽 변을 버튼 오른쪽 변에 맞춰 왼쪽으로 자란다 — 오른쪽에는 정보 패널이 있다.
        /// </summary>
        private void PlaceCard()
        {
            if (_card == null || !_card.gameObject.activeSelf) return;

            var anchor = CharacterActionUI.Instance != null ? CharacterActionUI.Instance.MoveButtonRect : null;
            if (anchor == null) return;

            anchor.GetWorldCorners(_corners);   // 0 좌하, 1 좌상, 2 우상, 3 우하 (오버레이 캔버스 = 화면 픽셀)
            float width = _card.rect.width * _card.lossyScale.x;
            float right = Mathf.Clamp(_corners[2].x, width + 8f, Screen.width - 8f);
            _card.position = new Vector3(right, _corners[1].y + cardGap, 0f);
        }

        private void BuildCard()
        {
            var root = new GameObject("_ActionForecastCanvas");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = cardSortingOrder;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            var cardGo = new GameObject("Card", typeof(RectTransform));
            cardGo.transform.SetParent(root.transform, false);
            _card = (RectTransform)cardGo.transform;
            _card.pivot = new Vector2(1f, 0f);     // 오른쪽 아래 — 버튼 위에서 왼쪽·위로 자란다

            var group = cardGo.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;          // 카드가 타일·몬스터 호버를 가로막지 않는다
            group.interactable = false;

            // 배경 Image는 레이아웃 무시 자식에 (editor_owned_ui_pattern 체크리스트 7)
            var bgGo = new GameObject("Bg", typeof(RectTransform));
            bgGo.transform.SetParent(cardGo.transform, false);
            var bgRect = (RectTransform)bgGo.transform;
            bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero; bgRect.offsetMax = Vector2.zero;
            var bg = bgGo.AddComponent<Image>();
            UiSkin.Current.Apply(bg, SkinPart.TooltipFrame);
            bg.raycastTarget = false;
            bgGo.AddComponent<LayoutElement>().ignoreLayout = true;

            var layout = cardGo.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(22, 22, 16, 18);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;

            var fitter = cardGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var textGo = new GameObject("Lines", typeof(RectTransform));
            textGo.transform.SetParent(cardGo.transform, false);
            _cardText = textGo.AddComponent<TextMeshProUGUI>();
            _cardText.alignment = TextAlignmentOptions.TopLeft;
            _cardText.fontSize = bodyFontSize;
            _cardText.lineSpacing = 6f;
            _cardText.richText = true;
            _cardText.raycastTarget = false;
            _cardText.textWrappingMode = TextWrappingModes.NoWrap;
            // 글꼴은 지정하지 않는다 — TMP 기본 글꼴(Pretendard)이 본문용이다. 씬의 아무 텍스트에서 빌리면 실행마다 글꼴이 달라진다.

            cardGo.SetActive(false);
        }
    }
}
