//using UnityEngine;
//using DiceOrbit.Visuals;
//#if UNITY_EDITOR
//using UnityEditor;
//#endif

//namespace DiceOrbit.Core
//{
//    /// <summary>
//    /// Scene 자동 설정 유틸리티 (Editor only)
//    /// Scene에서 우클릭 > Dice Orbit > Setup Scene으로 실행
//    /// </summary>
//    public class SceneSetup : MonoBehaviour
//    {
//#if UNITY_EDITOR
//        [MenuItem("Dice Orbit/Setup Scene")]
//        public static void SetupDiceOrbitScene()
//        {
//            Debug.Log("Setting up Dice Orbit Scene...");
            
//            // 1. GameManager 생성
//            GameObject gmObj = GameObject.Find("GameManager");
//            if (gmObj == null)
//            {
//                gmObj = new GameObject("GameManager");
//                gmObj.AddComponent<GameManager>();
//                Debug.Log("✓ GameManager created");
//            }
            
//            // 2. OrbitSystem 생성
//            GameObject orbitObj = GameObject.Find("OrbitSystem");
//            if (orbitObj == null)
//            {
//                orbitObj = new GameObject("OrbitSystem");
//                OrbitManager orbitMgr = orbitObj.AddComponent<OrbitManager>();
                
//                // 머티리얼 로드 시도
//                Material normalMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/NormalTile.mat");
//                Material levelUpMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/LevelUpTile.mat");
//                Material specialMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SpecialTile.mat");
                
//                // Reflection으로 private 필드 설정
//                var normalField = typeof(OrbitManager).GetField("normalMaterial", 
//                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
//                var levelUpField = typeof(OrbitManager).GetField("levelUpMaterial", 
//                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
//                var specialField = typeof(OrbitManager).GetField("specialMaterial", 
//                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
//                if (normalField != null && normalMat != null) normalField.SetValue(orbitMgr, normalMat);
//                if (levelUpField != null && levelUpMat != null) levelUpField.SetValue(orbitMgr, levelUpMat);
//                if (specialField != null && specialMat != null) specialField.SetValue(orbitMgr, specialMat);
                
//                Debug.Log("✓ OrbitSystem created");
                
//                // 2-1. OrbitVisualizer 생성 (OrbitSystem의 자식)
//                GameObject vizObj = new GameObject("Visualizer");
//                vizObj.transform.SetParent(orbitObj.transform);
//                vizObj.AddComponent<OrbitVisualizer>();
//                Debug.Log("✓ OrbitVisualizer created");
//            }
            
//            // 3. CenterZone 생성
//            GameObject centerObj = GameObject.Find("CenterZone");
//            if (centerObj == null)
//            {
//                centerObj = new GameObject("CenterZone");
//                centerObj.AddComponent<CenterZone>();
//                Debug.Log("✓ CenterZone created");
//            }
            
//            // 4. Camera 설정
//            Camera mainCam = Camera.main;
//            if (mainCam != null)
//            {
//                mainCam.transform.position = new Vector3(0, 20, -10);
//                mainCam.transform.rotation = Quaternion.Euler(60, 0, 0);
//                Debug.Log("✓ Camera positioned");
//            }
            
//            // 5. Directional Light 확인
//            Light dirLight = Object.FindAnyObjectByType<Light>();
//            if (dirLight == null)
//            {
//                GameObject lightObj = new GameObject("Directional Light");
//                dirLight = lightObj.AddComponent<Light>();
//                dirLight.type = LightType.Directional;
//                dirLight.transform.rotation = Quaternion.Euler(50, -30, 0);
//                Debug.Log("✓ Directional Light created");
//            }
            
//            Debug.Log("===== Scene Setup Complete! =====");
//            Debug.Log("Press Play to see the orbit in action!");
//        }
        
//        [MenuItem("Dice Orbit/Clear Scene")]
//        public static void ClearScene()
//        {
//            // 관련 오브젝트들 삭제
//            string[] objectNames = { "GameManager", "OrbitSystem", "CenterZone" };
            
//            foreach (string objName in objectNames)
//            {
//                GameObject obj = GameObject.Find(objName);
//                if (obj != null)
//                {
//                    DestroyImmediate(obj);
//                    Debug.Log($"✗ {objName} removed");
//                }
//            }
            
//            Debug.Log("Scene cleared!");
//        }
//#endif
//    }
//}
