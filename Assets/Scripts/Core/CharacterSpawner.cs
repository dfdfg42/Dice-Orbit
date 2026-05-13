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
        [SerializeField] private Vector2 colliderSizeMultiplier = new Vector2(1.6f, 1.6f);
        [SerializeField] private float colliderDepth = 0.2f;

        public Character Spawn(CharacterPreset preset)
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

            var startTile = orbitManager.GetTile(0);
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
            uiObj.transform.localPosition = new Vector3(0, 1.2f, 0);
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
            collider.center = new Vector3(0f, localSize.y * 0.5f, 0f);
        }
    }
}
