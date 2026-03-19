using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using DiceOrbit.Data;

namespace DiceOrbit.UI
{
    /// <summary>
    /// 주사위 UI 컨테이너
    /// </summary>
    public class DiceUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform diceContainer;
        [SerializeField] private GameObject diceElementPrefab;
        [SerializeField] private Button rollButton;
        [SerializeField] private DiceRollAnimator rollAnimator;
        
        [Header("Settings")]
        [SerializeField] private bool autoHideRollButton = true;
        [SerializeField] private bool useRollAnimation = true;
        
        // Runtime
        private List<DiceElement> diceElements = new List<DiceElement>();
        
        private void Start()
        {
            // Roll 버튼 이벤트 연결
            if (rollButton != null)
            {
                rollButton.onClick.AddListener(OnRollButtonClicked);
            }
            
            // DiceManager에 자신을 등록
            var diceManager = Core.DiceManager.Instance;
            if (diceManager != null)
            {
                diceManager.SetDiceUI(this);
            }
        }
        
        /// <summary>
        /// 주사위 표시
        /// </summary>
        public void DisplayDice(List<DiceData> diceList)
        {
            // 기존 주사위 UI 제거
            ClearDice();
            
            // 새로운 주사위 UI 생성
            foreach (var diceData in diceList)
            {
                CreateDiceElement(diceData);
            }
            
            // Roll 버튼 숨기기
            if (autoHideRollButton && rollButton != null)
            {
                rollButton.gameObject.SetActive(false);
            }

            // 애니메이션 실행
            if (useRollAnimation && rollAnimator != null && diceElements.Count > 0)
            {
                rollAnimator.PlayRollAnimation(new List<DiceElement>(diceElements), diceContainer);
            }
        }
        
        /// <summary>
        /// 주사위 UI 요소 생성
        /// </summary>
        private void CreateDiceElement(DiceData diceData)
        {
            if (diceElementPrefab == null || diceContainer == null)
            {
                Debug.LogError("DiceElementPrefab or DiceContainer is null!");
                return;
            }
            
            GameObject obj = Instantiate(diceElementPrefab, diceContainer);
            DiceElement element = obj.GetComponent<DiceElement>();
            
            if (element != null)
            {
                element.SetDiceData(diceData);
                diceElements.Add(element);
            }
        }
        
        /// <summary>
        /// 주사위 UI 제거
        /// </summary>
        public void ClearDice()
        {
            foreach (var element in diceElements)
            {
                if (element != null)
                {
                    Destroy(element.gameObject);
                }
            }
            
            diceElements.Clear();
            
            // Roll 버튼 다시 표시
            if (rollButton != null)
            {
                rollButton.gameObject.SetActive(true);
            }
        }
        
        /// <summary>
        /// Roll 버튼 클릭 핸들러
        /// </summary>
        private void OnRollButtonClicked()
        {
            // DiceManager가 직접 주사위 굴림 처리
            var diceManager = Core.DiceManager.Instance;
            if (diceManager != null)
            {
                diceManager.RollDice();
            }
        }
        
        /// <summary>
        /// 특정 주사위 사용됨 표시
        /// </summary>
        public void MarkDiceAsUsed(DiceData diceData)
        {
            var element = diceElements.Find(e => e.Data == diceData);
            if (element != null)
            {
                element.MarkAsUsed();
                
                // 사용된 주사위는 1초 후 제거 (애니메이션용)
                StartCoroutine(RemoveDiceAfterDelay(element, 0.5f));
            }
        }
        
        /// <summary>
        /// 주사위 제거 (지연)
        /// </summary>
        private System.Collections.IEnumerator RemoveDiceAfterDelay(DiceElement element, float delay)
        {
            yield return new UnityEngine.WaitForSeconds(delay);
            
            if (element != null)
            {
                diceElements.Remove(element);
                Destroy(element.gameObject);
                Debug.Log("Used dice removed from UI");
            }
        }
    }
}
