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
using DiceOrbit.UI.Skin;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 전투 클리어 보상 화면 — "크림 종이" 톤(정보 패널/말풍선과 통일).
    ///
    /// 2026-08-28 전환: 정적 프레임(캔버스/패널/타이틀/골드바/버튼)은 씬 하이라키에 배치하고
    /// 인스펙터로 배선한다 — 코드는 '동적 내용'(보상 타일·선택 카드)만 생성한다.
    /// 씬 배선이 비어 있으면 코드 생성 폴백 없이 명시적 에러를 낸다.
    ///
    /// 3개 화면:
    ///  ① 보상 획득!   — 골드바 + 가로 한 줄 유동 타일(골드/강화/포션/유물/주사위). [계속]
    ///  ② 캐릭터 선택   — 강화할 캐릭터 초상 타일 가로 배치. [취소]
    ///  ③ 모디파이어/주사위 선택 — 카드 가로 배치. [취소]
    ///
    /// 타일 클릭 = 수령(골드/포션/유물은 즉시 제거, 강화/주사위는 하위 창을 연다).
    /// </summary>
    public class RewardUI : MonoBehaviour
    {
        [Header("Tuning")]
        [SerializeField] private int goldPerReward = 50;
        [SerializeField] private int modifierChoiceCount = 3;
        [Range(0f, 1f)]
        [SerializeField] private float potionDropChance = 0.35f;   // 전투 보상 포션 드랍 (스펙 §6, 2026-08-28 20%→35% 상향)

        [Header("씬 배선 (정적 프레임 — 비어 있으면 에러, 코드 생성 폴백 없음)")]
        [SerializeField] private Canvas rewardCanvas;              // RewardCanvas (오버레이 500)
        [SerializeField] private RectTransform mainPanel;          // ① 보상 획득! 프레임 루트
        [SerializeField] private RectTransform upgradePanel;       // ②③ 선택 프레임 루트
        [SerializeField] private RectTransform rewardRow;          // ① 보상 타일이 가로로 쌓이는 곳
        [SerializeField] private RectTransform choiceRow;          // ②③ 선택 카드가 가로로 쌓이는 곳
        [SerializeField] private TextMeshProUGUI goldBarText;
        [SerializeField] private TextMeshProUGUI upgradeHeader;
        [SerializeField] private Button continueButton;            // [계속]
        [SerializeField] private Button cancelButton;              // [취소]

        private static UiSkin Skin => UiSkin.Current;   // 팔레트·스프라이트 단일 권위 (2026-09-25) — 동적 카드가 쓴다

        // ── 런타임 상태 ────────────────────────────────────────
        private Character _pickedCharacter;
        private DieDefinitionSO _pendingNewDie;      // 주사위 보상: 교체할 새 주사위
        private GameObject _upgradeTile;             // 강화 타일 (강화 완료 시 제거)
        private GameObject _diceTile;                // 주사위 타일 (교체 완료 시 제거)
        private int _pendingGold;                    // 아직 안 받은 골드 (골드바 표시용)

        private bool _wired;
        private TMP_FontAsset _font;

        // ═══════════════════════════════════════════════════════
        // 공개 API
        // ═══════════════════════════════════════════════════════

        public void Show()
        {
            gameObject.SetActive(true);
            if (!EnsureSceneRefs()) return;
            rewardCanvas.gameObject.SetActive(true);

            BattleInfoPanelUI.SetVisible(false);   // 보상/모집 화면 동안 정보 패널 숨김

            _pickedCharacter = null;
            _upgradeTile = null;
            _diceTile = null;
            ShowUpgradePanel(false);
            BuildRewardTiles();
        }

        public void Hide()
        {
            if (rewardCanvas != null) rewardCanvas.gameObject.SetActive(false);
            gameObject.SetActive(false);
            BattleInfoPanelUI.SetVisible(true);    // 전투 복귀 시 정보 패널 복원
        }

        /// <summary>씬 배선 검증 + 버튼 1회 연결. 폴백 생성은 없다 — 빠졌으면 배선 문제를 드러낸다.</summary>
        private bool EnsureSceneRefs()
        {
            if (rewardCanvas == null || mainPanel == null || upgradePanel == null
                || rewardRow == null || choiceRow == null
                || goldBarText == null || upgradeHeader == null
                || continueButton == null || cancelButton == null)
            {
                Debug.LogError("[RewardUI] 씬 배선이 비어 있다 — RewardCanvas 하이라키와 인스펙터 참조를 확인할 것 (코드 생성 폴백은 없다).");
                return false;
            }

            if (!_wired)
            {
                _wired = true;
                _font = BorrowFont();
                continueButton.onClick.AddListener(OnNextClicked);
                cancelButton.onClick.AddListener(() => ShowUpgradePanel(false));
            }
            return true;
        }

        // ═══════════════════════════════════════════════════════
        // ① 보상 획득! 화면
        // ═══════════════════════════════════════════════════════

        private void BuildRewardTiles()
        {
            ClearChildren(rewardRow);

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

            // ⑤ 주사위 획득 (특수 주사위 풀 → 현재 덱과 교체, 등급 가중 드로우)
            // 호버 시 텍스트 대신 다이스 패널과 같은 GUI(6면 아트+숫자+효과)가 뜬다.
            var newDie = DiceDeckManager.EnsureInstance()?.DrawRandomSpecial();
            if (newDie != null)
            {
                Sprite dieArt = newDie.Icon != null ? newDie.Icon
                    : DiceHoverTooltipUI.EnsureInstance()?.DieFaceSprite;
                _diceTile = AddRewardTile(newDie.Name, dieArt, null, tile => BeginDiceReplaceFlow(newDie));

                var hover = _diceTile.AddComponent<DiceCardHover>();
                hover.Faces = newDie.Faces;
                hover.Effect = newDie.Effect;
            }
        }

        /// <summary>보상 타일 1칸 = [이름바 + 이미지 타일]. 호버 시 desc, 클릭 시 onClaim(자기 자신).</summary>
        private GameObject AddRewardTile(string label, Sprite image, string desc, System.Action<GameObject> onClaim)
        {
            var root = MakeCard(rewardRow, "RewardTile", new Vector2(196f, 262f));

            var nameBar = MakeSkinImage(root, "NameBar", SkinPart.RewardNameTag);
            var nameLE = nameBar.gameObject.AddComponent<LayoutElement>();
            nameLE.preferredWidth = 172f; nameLE.preferredHeight = 50f;
            MakeText(nameBar, label, 23f, FontStyles.Bold, Skin.Ink, TextAlignmentOptions.Center)
                .margin = new Vector4(8f, 2f, 8f, 2f);

            var imgTile = MakeSkinImage(root, "ImageTile", SkinPart.RewardCard);
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
            if (upgradeHeader != null) upgradeHeader.text = "강화할 캐릭터 선택";
            ClearChildren(choiceRow);

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
            if (upgradeHeader != null) upgradeHeader.text = $"{name} 강화";
            ClearChildren(choiceRow);

            // 이 캐릭터에게 적용 가능한 모디파이어만 제시 (3중첩 도달 종류는 자동 제외)
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
            var root = MakeCard(choiceRow, "CharacterChoice", new Vector2(230f, 300f));

            var nameBar = MakeSkinImage(root, "NameBar", SkinPart.RewardNameTag);
            var nameLE = nameBar.gameObject.AddComponent<LayoutElement>();
            nameLE.preferredWidth = 200f; nameLE.preferredHeight = 58f;
            MakeText(nameBar, name, 28f, FontStyles.Bold, Skin.Ink, TextAlignmentOptions.Center)
                .margin = new Vector4(8f, 2f, 8f, 2f);

            var imgTile = MakeSkinImage(root, "Portrait", SkinPart.RewardCard);
            var tileLE = imgTile.gameObject.AddComponent<LayoutElement>();
            tileLE.preferredWidth = 220f; tileLE.preferredHeight = 220f;
            FillIcon(imgTile, portrait, name);

            WireCard(root, null, onClick);
        }

        /// <summary>모디파이어 카드 = 크림 타일 안에 [설명(위) + 이름(아래)].</summary>
        private void AddModifierChoice(string name, string desc, Sprite icon, System.Action onClick)
        {
            var root = MakeCard(choiceRow, "ModifierChoice", new Vector2(240f, 240f));

            var card = MakeSkinImage(root, "Card", SkinPart.RewardCard);
            var cardLE = card.gameObject.AddComponent<LayoutElement>();
            cardLE.preferredWidth = 230f; cardLE.preferredHeight = 230f;

            // 카드 내부 세로 스택: 설명(플렉시블, 위) → 이름(아래)
            var vlg = card.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(14, 14, 16, 16);
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            var descText = MakeText(card, desc, 18f, FontStyles.Normal, Skin.Ink, TextAlignmentOptions.Top);
            var descLE = descText.gameObject.AddComponent<LayoutElement>();
            descLE.flexibleHeight = 1f;

            var nameText = MakeText(card, name, 24f, FontStyles.Bold, Skin.Ink, TextAlignmentOptions.Bottom);
            var nameLE = nameText.gameObject.AddComponent<LayoutElement>();
            nameLE.preferredHeight = 32f;

            WireCard(root, null, onClick);
        }

        // ═══════════════════════════════════════════════════════
        // ③ 주사위 교체 흐름 (주사위 타일 클릭 → 덱 카드 선택 → 교체)
        // ═══════════════════════════════════════════════════════

        private void BeginDiceReplaceFlow(DieDefinitionSO newDie)
        {
            DiceHoverTooltipUI.Instance?.Hide();   // 보상 타일 호버 툴팁 정리

            _pendingNewDie = newDie;
            if (upgradeHeader != null) upgradeHeader.text = $"[{newDie.Name}]로 교체할 주사위 선택";
            ClearChildren(choiceRow);

            // 소지한 주사위들이 주사위 면 아트 카드로 늘어선다. 호버 = 다이스 패널과 같은 GUI, 클릭 = 교체.
            var deck = DiceDeckManager.Instance?.Deck;
            if (deck != null)
            {
                for (int i = 0; i < deck.Count; i++)
                {
                    int idx = i;
                    AddDieChoice(deck[i], () => OnDiceSlotPicked(idx));
                }
            }
            ShowUpgradePanel(true);
        }

        /// <summary>교체 후보 카드 = [이름바 + 주사위 아트]. 호버 시 6면 툴팁, 클릭 시 교체.</summary>
        private void AddDieChoice(DieInstance inst, System.Action onClick)
        {
            if (inst == null) return;
            string name = inst.BaseDie != null ? inst.BaseDie.Name : "주사위";

            var root = MakeCard(choiceRow, "DieChoice", new Vector2(180f, 240f));

            var nameBar = MakeSkinImage(root, "NameBar", SkinPart.RewardNameTag);
            var nameLE = nameBar.gameObject.AddComponent<LayoutElement>();
            nameLE.preferredWidth = 164f; nameLE.preferredHeight = 46f;
            MakeText(nameBar, name, 20f, FontStyles.Bold, Skin.Ink, TextAlignmentOptions.Center)
                .margin = new Vector4(6f, 2f, 6f, 2f);

            var imgTile = MakeSkinImage(root, "DieArt", SkinPart.RewardCard);
            var tileLE = imgTile.gameObject.AddComponent<LayoutElement>();
            tileLE.preferredWidth = 168f; tileLE.preferredHeight = 168f;
            Sprite art = inst.BaseDie != null && inst.BaseDie.Icon != null
                ? inst.BaseDie.Icon
                : DiceHoverTooltipUI.EnsureInstance()?.DieFaceSprite;
            FillIcon(imgTile, art, name);

            WireCard(root, null, () =>
            {
                DiceHoverTooltipUI.Instance?.Hide();
                onClick();
            });

            var hover = root.gameObject.AddComponent<DiceCardHover>();
            hover.Faces = inst.Faces;
            hover.Effect = inst.Effect;
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
            if (upgradePanel != null) upgradePanel.gameObject.SetActive(visible);
            if (mainPanel != null) mainPanel.gameObject.SetActive(!visible);   // 하위 창은 전체 화면 교체
        }

        private void RefreshGoldBar()
        {
            if (goldBarText != null)
                goldBarText.text = $"보유 골드 : {GoldManager.Instance?.Gold ?? 0} + {_pendingGold}";
        }

        // ═══════════════════════════════════════════════════════
        // 동적 카드 조립 헬퍼 (정적 프레임은 씬에 있다)
        // ═══════════════════════════════════════════════════════

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
            // 호버 중인 타일을 클릭해 수령하면 타일이 파괴돼 OnPointerExit가 안 뜬다 → 툴팁을 직접 닫는다.
            btn.onClick.AddListener(() => { HoverTooltipUI.Instance?.HidePinned(); onClick(); });

            if (!string.IsNullOrEmpty(desc))
            {
                var hover = root.gameObject.AddComponent<RewardHoverInfo>();
                hover.Description = desc;
            }
        }

        /// <summary>스킨 파트 Image 하나.</summary>
        private RectTransform MakeSkinImage(RectTransform parent, string name, SkinPart part)
        {
            var rt = MakeChild(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            Skin.Apply(img, part);
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
                40f, FontStyles.Bold, new Color(Skin.Ink.r, Skin.Ink.g, Skin.Ink.b, 0.75f), TextAlignmentOptions.Center);
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

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }

    /// <summary>
    /// 주사위 보상 타일/교체 카드 위에 커서를 올리면 다이스 패널과 같은 GUI
    /// (6면 아트+숫자 그리드+효과 카드)를 카드 상단에 띄우는 경량 프록시.
    /// </summary>
    public class DiceCardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public int[] Faces;
        public DieEffect Effect;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Faces == null || Faces.Length == 0) return;
            DiceHoverTooltipUI.EnsureInstance();
            DiceHoverTooltipUI.Instance?.ShowAt(Faces, Effect, GetTopCenter());
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            DiceHoverTooltipUI.Instance?.Hide();
        }

        private void OnDisable()
        {
            // 호버 중 카드가 파괴/비활성되면(수령·화면 전환) Exit가 안 오므로 직접 닫는다.
            DiceHoverTooltipUI.Instance?.Hide();
        }

        /// <summary>카드 상단 중앙 (오버레이 좌표) — 툴팁이 카드 위로 뜨게.</summary>
        private Vector2 GetTopCenter()
        {
            if (!(transform is RectTransform rt)) return transform.position;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            return (corners[1] + corners[2]) * 0.5f;
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
