using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DiceOrbit.Core;
using DiceOrbit.Core.Run;
using DiceOrbit.Data;
using DiceOrbit.Data.Modifiers;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 전투 클리어 보상 화면 — "크림 종이" 톤(정보 패널/말풍선과 통일).
    ///
    /// 3개 화면(모두 코드 생성):
    ///  ① 보상 획득!   — 골드바 + 가로 한 줄 유동 타일(골드/강화/포션/유물/주사위). 각 칸에 이미지, 호버 시 설명. [계속]
    ///  ② 캐릭터 선택   — 강화할 캐릭터 초상 타일 가로 배치. [취소]
    ///  ③ 모디파이어 선택 — 카드 3장(설명이 이름 위). [취소]
    ///
    /// 타일 클릭 = 수령(골드/포션/유물은 즉시 제거, 강화/주사위는 하위 창을 연다).
    /// 레이아웃은 씬 배선에 의존하지 않고 자식 캔버스를 비운 뒤 전량 재구성한다(이중 UI 방지).
    /// </summary>
    public class RewardUI : MonoBehaviour
    {
        [Header("Tuning")]
        [SerializeField] private int goldPerReward = 50;
        [SerializeField] private int modifierChoiceCount = 3;
        [Range(0f, 1f)]
        [SerializeField] private float potionDropChance = 0.2f;   // 전투 보상 저확률 포션 드랍 (스펙 §6)

        // ── 팔레트: 크림 종이 (목업 기준) ──────────────────────
        private static readonly Color Paper  = new Color(0.9804f, 0.9529f, 0.8784f);   // #FAF3E0 패널 바탕
        private static readonly Color Border = new Color(0.3608f, 0.3020f, 0.2627f);   // #5C4D43 패널 테두리
        private static readonly Color Tile   = new Color(0.9529f, 0.8980f, 0.8078f);   // #F3E5CE 타일/카드
        private static readonly Color Bar     = new Color(0.9098f, 0.8314f, 0.7333f);  // #E8D4BB 라벨바/버튼
        private static readonly Color Ink    = new Color(0.2941f, 0.2588f, 0.3608f);   // #4B425C 잉크 텍스트
        private static readonly Color Backdrop = new Color(0f, 0f, 0f, 0.55f);         // 화면 딤

        private const int PanelRadius = 25;
        private const int TileRadius  = 22;
        private const int PillRadius  = 26;
        private const int BorderPx    = 7;

        // ── 런타임 상태 ────────────────────────────────────────
        private Character _pickedCharacter;
        private DieDefinitionSO _pendingNewDie;      // 주사위 보상: 교체할 새 주사위
        private GameObject _upgradeTile;             // 강화 타일 (강화 완료 시 제거)
        private GameObject _diceTile;                // 주사위 타일 (교체 완료 시 제거)
        private int _pendingGold;                    // 아직 안 받은 골드 (골드바 표시용)

        // ── 코드 생성 캐시 ─────────────────────────────────────
        private bool _built;
        private TMP_FontAsset _font;
        private RectTransform _mainPanel;
        private RectTransform _upgradePanel;
        private RectTransform _rewardRow;            // ① 보상 타일이 가로로 쌓이는 곳
        private RectTransform _choiceRow;            // ②③ 선택 카드가 가로로 쌓이는 곳
        private TextMeshProUGUI _goldBarText;
        private TextMeshProUGUI _upgradeHeader;

        // ═══════════════════════════════════════════════════════
        // 공개 API
        // ═══════════════════════════════════════════════════════

        public void Show()
        {
            gameObject.SetActive(true);
            EnsureBuilt();

            BattleInfoPanelUI.SetVisible(false);   // 보상/모집 화면 동안 정보 패널 숨김

            _pickedCharacter = null;
            _upgradeTile = null;
            _diceTile = null;
            ShowUpgradePanel(false);
            BuildRewardTiles();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            BattleInfoPanelUI.SetVisible(true);    // 전투 복귀 시 정보 패널 복원
        }

        // ═══════════════════════════════════════════════════════
        // ① 보상 획득! 화면
        // ═══════════════════════════════════════════════════════

        private void BuildRewardTiles()
        {
            ClearChildren(_rewardRow);

            // ① 골드 (+유물 보너스, 예: 황금 주사위)
            _pendingGold = goldPerReward + (ArtifactManager.Instance?.BattleGoldBonus ?? 0);
            RefreshGoldBar();
            AddRewardTile($"골드 +{_pendingGold}", null,
                $"골드 {_pendingGold} 획득", tile =>
                {
                    GoldManager.EnsureInstance().AddGold(_pendingGold);
                    _pendingGold = 0;
                    RefreshGoldBar();
                    Destroy(tile);
                });

            // ② 캐릭터 강화 (모디파이어 3택1)
            _upgradeTile = AddRewardTile("캐릭터 강화", null,
                "캐릭터 1명에게 모디파이어 1종을 장착합니다", tile => BeginUpgradeFlow());

            // ③ 유물 (엘리트 클리어 시 — 스펙 §2)
            var runNode = RunManager.Instance?.CurrentNode;
            if (runNode != null && runNode.Type == MapNodeType.Elite)
            {
                var artifact = ArtifactManager.EnsureInstance().GetShopOfferings(1).FirstOrDefault();
                if (artifact != null)
                {
                    AddRewardTile(artifact.artifactName, artifact.artifactIcon,
                        artifact.artifactTooltip, tile =>
                        {
                            ArtifactManager.Instance.Grant(artifact);
                            Destroy(tile);
                        });
                }
            }

            // ④ 포션 (저확률 드랍)
            if (Random.value < potionDropChance)
            {
                var potion = PotionManager.EnsureInstance().GetShopOfferings(1).FirstOrDefault();
                if (potion != null)
                {
                    AddRewardTile(potion.PotionName, potion.Icon,
                        potion.Description, tile =>
                        {
                            if (PotionManager.Instance.TryAdd(potion))
                                Destroy(tile);
                            else
                                Debug.Log("[Reward] 포션 슬롯이 가득 — HUD에서 버린 뒤 수령하세요.");
                        });
                }
            }

            // ⑤ 주사위 획득 (특수 주사위 풀 → 현재 덱과 교체)
            var newDie = DiceDeckManager.EnsureInstance()?.DrawRandomSpecial();
            if (newDie != null)
            {
                string faces = newDie.Faces != null ? $"눈금 [{string.Join(" ", newDie.Faces)}]" : "";
                _diceTile = AddRewardTile(newDie.Name, newDie.Icon,
                    $"{newDie.Name}\n{faces}\n덱의 주사위 1개와 교체합니다", tile => BeginDiceReplaceFlow(newDie));
            }
        }

        /// <summary>보상 타일 1칸 = [이름바 + 이미지 타일]. 호버 시 desc, 클릭 시 onClaim(자기 자신).</summary>
        private GameObject AddRewardTile(string label, Sprite image, string desc, System.Action<GameObject> onClaim)
        {
            var root = MakeCard(_rewardRow, "RewardTile", new Vector2(196f, 262f));

            var nameBar = MakeRoundImage(root, "NameBar", Bar, PillRadius);
            var nameLE = nameBar.gameObject.AddComponent<LayoutElement>();
            nameLE.preferredWidth = 172f; nameLE.preferredHeight = 50f;
            MakeText(nameBar, label, 23f, FontStyles.Bold, Ink, TextAlignmentOptions.Center)
                .margin = new Vector4(8f, 2f, 8f, 2f);

            var imgTile = MakeRoundImage(root, "ImageTile", Tile, TileRadius);
            var tileLE = imgTile.gameObject.AddComponent<LayoutElement>();
            tileLE.preferredWidth = 184f; tileLE.preferredHeight = 184f;
            FillIcon(imgTile, image, label);

            WireCard(root, desc, () => onClaim(root.gameObject));
            return root.gameObject;
        }

        // ═══════════════════════════════════════════════════════
        // ② 캐릭터 강화 흐름 (강화 타일 클릭 → 캐릭터 선택)
        // ═══════════════════════════════════════════════════════

        private void BeginUpgradeFlow()
        {
            _pickedCharacter = null;
            if (_upgradeHeader != null) _upgradeHeader.text = "강화할 캐릭터 선택";
            ClearChildren(_choiceRow);

            var party = PartyManager.Instance?.Party;
            if (party != null)
            {
                foreach (var ch in party)
                {
                    if (ch == null || !ch.IsAlive) continue;
                    var captured = ch;
                    string name = ch.Stats != null ? ch.Stats.CharacterName : ch.name;
                    var preset = ch.SourcePreset;
                    Sprite portrait = preset != null ? (preset.CharacterWindowSprite != null ? preset.CharacterWindowSprite : preset.Portrait) : null;
                    AddCharacterChoice(name, portrait, () => OnCharacterPicked(captured));
                }
            }
            ShowUpgradePanel(true);
        }

        private void OnCharacterPicked(Character ch)
        {
            _pickedCharacter = ch;
            string name = ch.Stats != null ? ch.Stats.CharacterName : ch.name;
            if (_upgradeHeader != null) _upgradeHeader.text = $"{name} 강화";
            ClearChildren(_choiceRow);

            // 이 캐릭터에게 적용 가능한 모디파이어만 제시
            var choices = ModifierRegistry.GetRandomChoicesFor(ch, modifierChoiceCount);
            foreach (var mod in choices)
            {
                var captured = mod;
                AddModifierChoice(mod.ModifierName, mod.Description, mod.Icon, () => OnModifierPicked(captured));
            }
        }

        private void OnModifierPicked(CharacterModifier mod)
        {
            if (_pickedCharacter != null && mod != null)
            {
                _pickedCharacter.Stats?.Modifiers?.Add(mod);
                Debug.Log($"[Reward] {_pickedCharacter.Stats?.CharacterName} ← '{mod.ModifierName}' 장착");
            }

            // 강화 타일 수령 완료 → 제거
            if (_upgradeTile != null) { Destroy(_upgradeTile); _upgradeTile = null; }
            ShowUpgradePanel(false);
        }

        /// <summary>캐릭터 선택 카드 = [이름바 + 초상 타일].</summary>
        private void AddCharacterChoice(string name, Sprite portrait, System.Action onClick)
        {
            var root = MakeCard(_choiceRow, "CharacterChoice", new Vector2(230f, 300f));

            var nameBar = MakeRoundImage(root, "NameBar", Bar, PillRadius);
            var nameLE = nameBar.gameObject.AddComponent<LayoutElement>();
            nameLE.preferredWidth = 200f; nameLE.preferredHeight = 58f;
            MakeText(nameBar, name, 28f, FontStyles.Bold, Ink, TextAlignmentOptions.Center)
                .margin = new Vector4(8f, 2f, 8f, 2f);

            var imgTile = MakeRoundImage(root, "Portrait", Tile, TileRadius);
            var tileLE = imgTile.gameObject.AddComponent<LayoutElement>();
            tileLE.preferredWidth = 220f; tileLE.preferredHeight = 220f;
            FillIcon(imgTile, portrait, name);

            WireCard(root, null, onClick);
        }

        /// <summary>모디파이어 카드 = 크림 타일 안에 [설명(위) + 이름(아래)].</summary>
        private void AddModifierChoice(string name, string desc, Sprite icon, System.Action onClick)
        {
            var root = MakeCard(_choiceRow, "ModifierChoice", new Vector2(240f, 240f));

            var card = MakeRoundImage(root, "Card", Tile, TileRadius);
            var cardLE = card.gameObject.AddComponent<LayoutElement>();
            cardLE.preferredWidth = 230f; cardLE.preferredHeight = 230f;

            // 카드 내부 세로 스택: 설명(플렉시블, 위) → 이름(아래)
            var vlg = card.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(14, 14, 16, 16);
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            var descText = MakeText(card, desc, 18f, FontStyles.Normal, Ink, TextAlignmentOptions.Top);
            var descLE = descText.gameObject.AddComponent<LayoutElement>();
            descLE.flexibleHeight = 1f;

            var nameText = MakeText(card, name, 24f, FontStyles.Bold, Ink, TextAlignmentOptions.Bottom);
            var nameLE = nameText.gameObject.AddComponent<LayoutElement>();
            nameLE.preferredHeight = 32f;

            WireCard(root, null, onClick);
        }

        // ═══════════════════════════════════════════════════════
        // ③ 주사위 교체 흐름 (주사위 타일 클릭 → 덱 슬롯 선택 → 교체)
        // ═══════════════════════════════════════════════════════

        private void BeginDiceReplaceFlow(DieDefinitionSO newDie)
        {
            _pendingNewDie = newDie;
            if (_upgradeHeader != null) _upgradeHeader.text = $"[{newDie.Name}]로 교체할 주사위 선택";
            ClearChildren(_choiceRow);

            var deck = DiceDeckManager.Instance?.Deck;
            if (deck != null)
            {
                for (int i = 0; i < deck.Count; i++)
                {
                    int idx = i;
                    var inst = deck[i];
                    string faces = inst.Faces != null ? string.Join(" ", inst.Faces) : "";
                    string name = inst.BaseDie != null ? inst.BaseDie.Name : "주사위";
                    AddModifierChoice(name, $"[{faces}]", inst.BaseDie != null ? inst.BaseDie.Icon : null,
                        () => OnDiceSlotPicked(idx));
                }
            }
            ShowUpgradePanel(true);
        }

        private void OnDiceSlotPicked(int index)
        {
            if (_pendingNewDie != null)
            {
                DiceDeckManager.Instance?.Replace(index, _pendingNewDie);
                Debug.Log($"[Reward] 덱 {index}번을 '{_pendingNewDie.Name}'로 교체");
            }
            _pendingNewDie = null;

            if (_diceTile != null) { Destroy(_diceTile); _diceTile = null; }
            ShowUpgradePanel(false);
        }

        private void OnNextClicked()
        {
            GameFlowManager.Instance?.OnRewardComplete();
        }

        private void ShowUpgradePanel(bool visible)
        {
            if (_upgradePanel != null) _upgradePanel.gameObject.SetActive(visible);
            if (_mainPanel != null) _mainPanel.gameObject.SetActive(!visible);   // 하위 창은 전체 화면 교체
        }

        private void RefreshGoldBar()
        {
            if (_goldBarText != null)
                _goldBarText.text = $"보유 골드 : {GoldManager.Instance?.Gold ?? 0} + {_pendingGold}";
        }

        // ═══════════════════════════════════════════════════════
        // 코드 생성 (크림 톤 전량 재구성)
        // ═══════════════════════════════════════════════════════

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            _font = BorrowFont();
            var canvas = EnsureCanvas();
            ClearChildren((RectTransform)canvas.transform);

            // 화면 딤 배경
            var backdrop = MakeChild((RectTransform)canvas.transform, "Backdrop");
            Stretch(backdrop);
            var bimg = backdrop.gameObject.AddComponent<Image>();
            bimg.color = Backdrop; bimg.raycastTarget = true;

            // ── ① 보상 획득! 패널 ─────────────────────────────
            // 폭은 보상 최대 5개(골드/강화/유물/포션/주사위)가 한 줄에 들어가도록 넉넉히.
            _mainPanel = MakeFramedPanel((RectTransform)canvas.transform, "MainPanel", new Vector2(1160f, 640f));

            var title = MakeText(_mainPanel, "보상 획득!", 52f, FontStyles.Bold, Ink, TextAlignmentOptions.Center);
            AnchorCenter(title.rectTransform, new Vector2(760f, 84f), new Vector2(0f, 260f));

            var goldBar = MakeRoundImage(_mainPanel, "GoldBar", Bar, PillRadius);
            AnchorCenter(goldBar, new Vector2(440f, 60f), new Vector2(0f, 178f));
            _goldBarText = MakeText(goldBar, "보유 골드 : 0 + 0", 30f, FontStyles.Bold, Ink, TextAlignmentOptions.Center);
            Stretch(_goldBarText.rectTransform);

            _rewardRow = MakeChild(_mainPanel, "RewardRow");
            AnchorCenter(_rewardRow, new Vector2(1100f, 300f), new Vector2(0f, -20f));
            var rowHlg = _rewardRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowHlg.spacing = 22f;
            rowHlg.childAlignment = TextAnchor.MiddleCenter;
            rowHlg.childControlWidth = true; rowHlg.childControlHeight = true;
            rowHlg.childForceExpandWidth = false; rowHlg.childForceExpandHeight = false;

            MakePillButton(_mainPanel, "계속", new Vector2(200f, 66f), new Vector2(0f, -265f), OnNextClicked);

            // ── ②③ 강화 패널 ─────────────────────────────────
            _upgradePanel = MakeFramedPanel((RectTransform)canvas.transform, "UpgradePanel", new Vector2(1150f, 600f));

            _upgradeHeader = MakeText(_upgradePanel, "강화", 30f, FontStyles.Bold, Ink, TextAlignmentOptions.Center);
            AnchorCenter(_upgradeHeader.rectTransform, new Vector2(700f, 56f), new Vector2(0f, 230f));

            _choiceRow = MakeChild(_upgradePanel, "ChoiceRow");
            AnchorCenter(_choiceRow, new Vector2(1060f, 320f), new Vector2(0f, -6f));
            var choiceHlg = _choiceRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            choiceHlg.spacing = 40f;
            choiceHlg.childAlignment = TextAnchor.MiddleCenter;
            choiceHlg.childControlWidth = true; choiceHlg.childControlHeight = true;
            choiceHlg.childForceExpandWidth = false; choiceHlg.childForceExpandHeight = false;

            MakePillButton(_upgradePanel, "취소", new Vector2(190f, 64f), new Vector2(0f, -250f),
                () => ShowUpgradePanel(false));

            _upgradePanel.gameObject.SetActive(false);
        }

        private Canvas EnsureCanvas()
        {
            var canvas = GetComponentInChildren<Canvas>(true);
            if (canvas != null) return canvas;

            var go = new GameObject("RewardCanvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        // ── UI 조립 헬퍼 ───────────────────────────────────────

        /// <summary>7px 갈색 테두리 + 크림 바탕 + 드롭 섀도의 액자형 패널. 내부(콘텐츠) RectTransform 반환.</summary>
        private RectTransform MakeFramedPanel(RectTransform parent, string name, Vector2 size)
        {
            var frame = MakeChild(parent, name);
            AnchorCenter(frame, size, Vector2.zero);
            var frameImg = frame.gameObject.AddComponent<Image>();
            frameImg.sprite = UiRoundedSprite.Get(PanelRadius);
            frameImg.type = Image.Type.Sliced;
            frameImg.color = Border;
            frameImg.raycastTarget = true;   // 패널 뒤 클릭 차단

            var shadow = frame.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.25f);
            shadow.effectDistance = new Vector2(0f, -4f);

            var inner = MakeChild(frame, "Paper");
            Stretch(inner);
            inner.offsetMin = new Vector2(BorderPx, BorderPx);
            inner.offsetMax = new Vector2(-BorderPx, -BorderPx);
            var innerImg = inner.gameObject.AddComponent<Image>();
            innerImg.sprite = UiRoundedSprite.Get(PanelRadius - 3);
            innerImg.type = Image.Type.Sliced;
            innerImg.color = Paper;
            innerImg.raycastTarget = false;

            return inner;
        }

        /// <summary>버튼-호버 겸용 카드 루트 (투명 레이캐스트 캐처 + 세로 스택).</summary>
        private RectTransform MakeCard(RectTransform parent, string name, Vector2 size)
        {
            var root = MakeChild(parent, name);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = size.x; le.preferredHeight = size.y;

            var catcher = root.gameObject.AddComponent<Image>();   // 전체 영역 클릭/호버 감지
            catcher.color = new Color(1f, 1f, 1f, 0f);
            catcher.raycastTarget = true;

            var vlg = root.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false; vlg.childForceExpandHeight = false;
            return root;
        }

        /// <summary>카드에 Button(클릭) + 호버 툴팁을 붙인다. desc가 있으면 호버 시 표시.</summary>
        private void WireCard(RectTransform root, string desc, System.Action onClick)
        {
            var btn = root.gameObject.AddComponent<Button>();
            btn.targetGraphic = root.GetComponent<Image>();
            btn.transition = Selectable.Transition.ColorTint;
            btn.colors = HoverTint();
            btn.onClick.AddListener(() => onClick());

            if (!string.IsNullOrEmpty(desc))
            {
                var hover = root.gameObject.AddComponent<RewardHoverInfo>();
                hover.Description = desc;
            }
        }

        private void MakePillButton(RectTransform parent, string label, Vector2 size, Vector2 pos, System.Action onClick)
        {
            var pill = MakeRoundImage(parent, label + "Button", Bar, PillRadius);
            AnchorCenter(pill, size, pos);
            var btn = pill.gameObject.AddComponent<Button>();
            btn.targetGraphic = pill.GetComponent<Image>();
            btn.colors = HoverTint();
            btn.onClick.AddListener(() => onClick());
            var txt = MakeText(pill, label, 34f, FontStyles.Bold, Ink, TextAlignmentOptions.Center);
            Stretch(txt.rectTransform);
        }

        /// <summary>둥근 사각 Image 하나 (색 채움).</summary>
        private RectTransform MakeRoundImage(RectTransform parent, string name, Color color, int radius)
        {
            var rt = MakeChild(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UiRoundedSprite.Get(radius);
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        /// <summary>타일 안에 아이콘 스프라이트를 채운다. 없으면 이름 텍스트로 대체.</summary>
        private void FillIcon(RectTransform tile, Sprite icon, string labelFallback)
        {
            if (icon != null)
            {
                var iconRT = MakeChild(tile, "Icon");
                Stretch(iconRT);
                iconRT.offsetMin = new Vector2(18f, 18f);
                iconRT.offsetMax = new Vector2(-18f, -18f);
                var img = iconRT.gameObject.AddComponent<Image>();
                img.sprite = icon; img.preserveAspect = true; img.raycastTarget = false;
                return;
            }

            // 스프라이트가 없으면 큰 텍스트(글리프/이름)로 대체
            var txt = MakeText(tile, string.IsNullOrEmpty(labelFallback) ? "?" : labelFallback,
                40f, FontStyles.Bold, new Color(Ink.r, Ink.g, Ink.b, 0.75f), TextAlignmentOptions.Center);
            Stretch(txt.rectTransform);
            txt.margin = new Vector4(10f, 10f, 10f, 10f);
            txt.enableAutoSizing = true;
            txt.fontSizeMin = 18f; txt.fontSizeMax = 46f;
        }

        private TextMeshProUGUI MakeText(RectTransform parent, string text, float size, FontStyles style,
            Color color, TextAlignmentOptions align)
        {
            var rt = MakeChild(parent, "Text");
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = true;
            if (_font != null) tmp.font = _font;
            return tmp;
        }

        private ColorBlock HoverTint()
        {
            var cb = ColorBlock.defaultColorBlock;
            cb.normalColor      = Color.white;
            cb.highlightedColor = new Color(1f, 1f, 1f, 1f);
            cb.pressedColor     = new Color(0.88f, 0.88f, 0.88f, 1f);
            cb.selectedColor    = Color.white;
            cb.colorMultiplier  = 1f;
            cb.fadeDuration     = 0.08f;
            return cb;
        }

        private TMP_FontAsset BorrowFont()
        {
            var anyText = FindAnyObjectByType<TextMeshProUGUI>(FindObjectsInactive.Include);
            return anyText != null ? anyText.font : null;
        }

        private static RectTransform MakeChild(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void ClearChildren(RectTransform root)
        {
            if (root == null) return;
            for (int i = root.childCount - 1; i >= 0; i--)
                Destroy(root.GetChild(i).gameObject);
        }

        private static void AnchorCenter(RectTransform rt, Vector2 size, Vector2 anchoredPos)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }

    /// <summary>보상 타일/카드 위에 커서를 올리면 HoverTooltipUI로 설명을 띄우는 경량 프록시.</summary>
    public class RewardHoverInfo : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public string Description;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (string.IsNullOrEmpty(Description)) return;
            HoverTooltipUI.EnsureInstance();
            HoverTooltipUI.Instance?.ShowPinned(Description);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            HoverTooltipUI.Instance?.HidePinned();
        }
    }
}
