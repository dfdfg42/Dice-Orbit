using DiceOrbit.Systems.Artifact;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

namespace DiceOrbit.Core
{
    public class ArtifactManager : MonoBehaviour
    {
        public static ArtifactManager Instance { get; private set; }
        private List<RuntimeArtifact> artifacts = new List<RuntimeArtifact>();

        [Header("Default Artifacts (Debug)")]
        [SerializeField] private ArtifactData debugPowerfullPunchData;

        /// <summary>
        /// 외부에서 아티팩트 목록을 읽기 전용으로 참조할 수 있도록 제공합니다.
        /// </summary>
        public IReadOnlyList<RuntimeArtifact> Artifacts => artifacts.AsReadOnly();

        private void Awake()
        {
            // 싱글톤 패턴
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Debug.LogWarning("Multiple ArtifactManagers detected! Destroying duplicate.");
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            ResetArtifacts();
        }

        /// <summary>
        /// 새로운 아티팩트를 추가합니다.
        /// </summary>
        public void AddArtifact(RuntimeArtifact artifact)
        {
            if (artifact == null) return;

            artifacts.Add(artifact);
            Debug.Log($"[ArtifactManager] Added artifact: {artifact.GetType().Name}");
            RefreshUI();
        }

        /// <summary>
        /// 특정 타입의 아티팩트를 찾아 모두 제거합니다.
        /// </summary>
        /// <typeparam name="T">제거할 아티팩트 클래스 타입</typeparam>
        /// <returns>제거 성공 여부</returns>
        public bool RemoveArtifact<T>() where T : RuntimeArtifact
        {
            // 리스트에서 타입이 T이거나 T를 상속받은 요소를 모두 제거합니다.
            int removedCount = artifacts.RemoveAll(a => a is T);

            if (removedCount > 0)
            {
                Debug.Log($"[ArtifactManager] Removed {removedCount} artifact(s) of type: {typeof(T).Name}");
                RefreshUI();
                return true;
            }

            Debug.LogWarning($"[ArtifactManager] Artifact of type {typeof(T).Name} not found.");
            return false;
        }

        /// <summary>
        /// 아티팩트 객체 인스턴스를 직접 전달하여 제거합니다.
        /// </summary>
        public bool RemoveArtifact(RuntimeArtifact artifact)
        {
            if (artifacts.Remove(artifact))
            {
                Debug.Log($"[ArtifactManager] Removed artifact instance: {artifact.GetType().Name}");
                RefreshUI();
                return true;
            }

            Debug.LogWarning($"[ArtifactManager] Artifact instance {artifact.GetType().Name} not found.");
            return false;
        }

        private void ResetArtifacts()
        {
            artifacts.Clear();

            if (debugPowerfullPunchData != null)
            {
                artifacts.Add(new PowerfullPunch(debugPowerfullPunchData));
            }
            else
            {
                Debug.LogWarning("[ArtifactManager] Debug PowerfullPunch Data is missing! Please assign it in the Inspector.");
            }

            Debug.Log("[ArtifactManager] Artifacts reset to default.");
            RefreshUI();
        }

        private void RefreshUI()
        {
            DiceOrbit.UI.ArtifactPanelUI.Instance?.RefreshArtifactList();
        }
    }
}

