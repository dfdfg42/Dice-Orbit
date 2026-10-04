using UnityEngine;
using DiceOrbit.Data.Tile;

namespace DiceOrbit.Core
{
    /// <summary>
    /// 전투 시작마다 궤도 타일에 홀짝 극성을 부여한다.
    ///   홀수 인덱스 → 공격 타일(딜↑·받피↑), 짝수 인덱스 → 방어 타일(딜↓·받피↓).
    ///   0번(레벨업) 타일은 제외.
    /// EventRunState와 동일하게 CombatManager.OnCombatStart 훅을 사용하며, 씬 배선 없이 자동 부트스트랩된다.
    /// TileData.AddAttribute가 아이콘 리프레시 + 설치 VFX를 자동 트리거하므로 별도 연출 코드는 없다.
    /// </summary>
    public class TilePolarityInstaller : MonoBehaviour
    {
        public static TilePolarityInstaller Instance { get; private set; }

        [Tooltip("공격/방어 보정 % (주는 피해·받는 피해 공통). 30 = ±30%")]
        [SerializeField] private int percent = 30;

        private CombatManager _hookedCombat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => EnsureInstance();

        public static void EnsureInstance()
        {
            if (Instance != null) return;
            var existing = FindAnyObjectByType<TilePolarityInstaller>(FindObjectsInactive.Include);
            Instance = existing != null
                ? existing
                : new GameObject("TilePolarityInstaller").AddComponent<TilePolarityInstaller>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_hookedCombat != null) _hookedCombat.OnCombatStart -= PlaceAll;
        }

        private void Start() => EnsureCombatHook();

        // 씬 리로드로 CombatManager가 교체돼도 재구독 (참조 비교라 비용 미미).
        private void Update()
        {
            if (_hookedCombat != CombatManager.Instance) EnsureCombatHook();
        }

        private void EnsureCombatHook()
        {
            var cm = CombatManager.Instance;
            if (cm == null || _hookedCombat == cm) return;
            if (_hookedCombat != null) _hookedCombat.OnCombatStart -= PlaceAll;
            cm.OnCombatStart += PlaceAll;
            _hookedCombat = cm;
        }

        /// <summary>0번 제외, 홀수→공격 / 짝수→방어. AddAttribute는 중복 방지(idempotent)라 매 전투 안전.</summary>
        private void PlaceAll()
        {
            var orbit = FindAnyObjectByType<OrbitManager>();
            if (orbit == null || orbit.Tiles == null) return;

            foreach (var tile in orbit.Tiles)
            {
                if (tile == null || tile.TileIndex == 0) continue;   // 0번(레벨업) 제외
                bool odd = (tile.TileIndex & 1) == 1;
                tile.AddAttribute(odd ? (TileAttribute)new AttackTile(percent) : new DefenseTile(percent));
            }
        }
    }
}
