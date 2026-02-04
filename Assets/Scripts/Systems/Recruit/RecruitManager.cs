using UnityEngine;

namespace DiceOrbit.Systems.Recruit
{
    public class RecruitManager : MonoBehaviour
    {
        public static RecruitManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void GenerateOptions()
        {
            Debug.Log("[RecruitManager] Generating recruit options...");
            // Logic to generate random characters for recruitment
        }
    }
}
