using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 캐릭터 선택 감지 (UI와 독립적으로 작동)
    /// </summary>
    public class CharacterSelector : MonoBehaviour
    {
        private Camera mainCamera;
        
        private void Start()
        {
            mainCamera = Camera.main;
            Debug.Log("CharacterSelector started! Camera: " + (mainCamera != null ? mainCamera.name : "NULL"));
        }
        
        private void Update()
        {
            // 새로운 Input System 사용
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                Debug.Log($"[CharacterSelector] Mouse clicked at: {Mouse.current.position.ReadValue()}");
                
                // UI 클릭이 아닌지 확인
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    Debug.Log("[CharacterSelector] Click was over UI, ignoring");
                    return;
                }
                
                Debug.Log("[CharacterSelector] Click is NOT over UI, doing raycast");
                
                // 카메라에서 Raycast
                Vector2 mousePos = Mouse.current.position.ReadValue();
                Ray ray = mainCamera.ScreenPointToRay(mousePos);
                RaycastHit hit;
                
                Debug.Log($"[CharacterSelector] Ray origin: {ray.origin}, direction: {ray.direction}");
                
                // 모든 레이어에 대해 500f 거리까지 체크
                if (Physics.Raycast(ray, out hit, 500f, ~0))  // ~0 = 모든 레이어
                {
                    Debug.Log($"[CharacterSelector] Raycast hit: {hit.collider.gameObject.name} at {hit.point}");
                    
                    // Character 또는 TestCharacter 확인
                    var character = hit.collider.GetComponent<Character>();
                    //var testCharacter = hit.collider.GetComponent<TestCharacter>();
                    
                    if (character != null)
                    {
                        Debug.Log("[CharacterSelector] Character found! Selecting...");
                        character.OnSelected();
                    }
                    
                    else
                    {
                        var components = hit.collider.GetComponents<Component>();
                        Debug.Log($"[CharacterSelector] Hit object has no Character component. Component count: {components.Length}");
                    }
                }
                else
                {
                    Debug.Log("[CharacterSelector] Raycast hit nothing");
                }
            }
        }
    }
}
