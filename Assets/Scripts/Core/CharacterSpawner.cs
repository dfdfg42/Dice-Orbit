using UnityEngine;
using DiceOrbit.UI;
using DiceOrbit.Visuals;

namespace DiceOrbit.Core
{
    public class CharacterSpawner : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject characterUIPrefab;

        [Header("Fallback Animator")]
        [SerializeField] private RuntimeAnimatorController fallbackAnimatorController;

        [Header("Spawn Settings")]
        [SerializeField] private Vector3 characterScale = new Vector3(0.3f, 0.3f, 1f);
        [SerializeField] private Vector2 colliderSizeMultiplier = new Vector2(0.8f, 0.8f); // 스프라이트 본체보다 약간 작게(본체만 클릭)
        [SerializeField] private float colliderDepth = 0.2f;

        [Header("Character UI")]
        [Tooltip("캐릭터 머리 위 체력바/이름 UI의 로컬 오프셋")]
        [SerializeField] private Vector3 characterUIOffset = new Vector3(0f, 1.2f, 0f);

        /// <summary>
        /// 캐릭터를 스폰한다. slotIndex/slotCount로 전체 타일을 균등 분할해 배치한다.
        /// 예) 타일 20개, slotCount 4 → 간격 5칸 → slot 0/1/2/3 = 타일 0/5/10/15.
        /// </summary>
        public Character Spawn(CharacterPreset preset, int slotIndex = 0, int slotCount = 1)
        {
            if (preset == null)
            {
                Debug.LogError("[CharacterSpawner] Preset is null.");
                return null;
            }

            var orbitManager = Object.FindAnyObjectByType<OrbitManager>();
            if (orbitManager == null)
            {
                Debug.LogError("[CharacterSpawner] OrbitManager not found.");
                return null;
            }

            int tileIndex = 0;
            int total = orbitManager.TileCount;
            if (total > 0)
            {
                int slots = Mathf.Max(1, slotCount);
                int gap = Mathf.Max(1, total / slots);
                tileIndex = (slotIndex * gap) % total;
                if (tileIndex < 0) tileIndex += total;
            }

            var startTile = orbitManager.GetTile(tileIndex);
            if (startTile == null)
            {
                Debug.LogError("[CharacterSpawner] Start tile not found.");
                return null;
            }

            var characterObj = new GameObject($"Player_{preset.CharacterName}");
            characterObj.transform.localScale = characterScale;

            var spriteRenderer = characterObj.AddComponent<SpriteRenderer>();
            var animator = characterObj.AddComponent<Animator>();
            characterObj.AddComponent<CharacterSpriteVisual>();

            var controller = preset.AnimatorController != null ? preset.AnimatorController : fallbackAnimatorController;
            if (controller != null)
                animator.runtimeAnimatorController = controller;
            else
                Debug.LogWarning($"[CharacterSpawner] No animator controller for '{preset.CharacterName}'.");

            var character = characterObj.AddComponent<Character>();
            character.InitializeStats(preset.CreateStats());
            // Character.Start의 지연 초기화가 이 인덱스로 시작 타일을 잡도록 지정
            character.SetStartTileIndex(tileIndex);

            // Fit collider after InitializeStats so sprite bounds are valid
            var collider = characterObj.AddComponent<BoxCollider>();
            FitColliderToSprite(collider, spriteRenderer, characterObj.transform);

            characterObj.transform.position = startTile.Position + Character.TILE_OFFSET;

            PartyManager.Instance?.AddCharacter(character);
            CreateCharacterUI(characterObj, character);

            Debug.Log($"[CharacterSpawner] Spawned {preset.CharacterName}");
            return character;
        }

        private void CreateCharacterUI(GameObject characterObj, Character character)
        {
            if (characterUIPrefab == null) return;
            var uiObj = Instantiate(characterUIPrefab, characterObj.transform);
            uiObj.transform.localPosition = characterUIOffset;
            uiObj.GetComponent<CharacterUI>()?.SetCharacter(character);
        }

        private void FitColliderToSprite(BoxCollider collider, SpriteRenderer renderer, Transform target)
        {
            if (collider == null || renderer == null || renderer.sprite == null || target == null) return;

            var bounds = renderer.bounds;
            var localSize = new Vector3(
                bounds.size.x / target.localScale.x * colliderSizeMultiplier.x,
                bounds.size.y / target.localScale.y * colliderSizeMultiplier.y,
                colliderDepth
            );
            collider.size = localSize;
            // 스프라이트 실제 중심에 맞춤(베이스 고정 X) → 콜라이더가 본체만 덮어 머리 위 체력바 영역이 클릭되지 않음
            collider.center = target.InverseTransformPoint(bounds.center);
        }
    }
}
