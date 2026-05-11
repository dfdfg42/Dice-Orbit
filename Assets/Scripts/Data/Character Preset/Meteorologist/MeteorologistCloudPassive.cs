using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;
using DiceOrbit.Visuals;
using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.Data.CharacterPassives
{
    /// <summary>
    /// [구름] 패시브 — 웨이브 시작 시 무작위 타일 3개에 구름 속성을 부여합니다.
    /// 기상학자 캐릭터 선택 시 구름 타일을 Buff 색상으로 강조합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "MeteorologistCloudPassive", menuName = "Dice Orbit/Passive Templates/Meteorologist Cloud")]
    public class MeteorologistCloudPassive : CharacterPassive
    {
        [SerializeField] private int cloudTileCount = 3;

        private List<TileData> _cloudTiles = new List<TileData>();

        public override void Initialize(Unit ownerUnit)
        {
            base.Initialize(ownerUnit);
            _cloudTiles = new List<TileData>();

            var waveManager = WaveManager.Instance;
            if (waveManager != null)
            {
                waveManager.OnWaveStart += OnWaveStart;
                if (waveManager.IsWaveActive)
                {
                    PlaceCloudTiles();
                }
            }
        }

        private void OnWaveStart(int wave)
        {
            PlaceCloudTiles();
        }

        private void PlaceCloudTiles()
        {
            foreach (var tile in _cloudTiles)
                tile?.RemoveAttributeType(TileAttributeType.Cloud);
            _cloudTiles.Clear();

            var orbitManager = Object.FindFirstObjectByType<OrbitManager>();
            if (orbitManager == null) return;

            var allTiles = new List<TileData>(orbitManager.Tiles);
            allTiles.RemoveAll(t => t == null);

            int count = Mathf.Min(cloudTileCount, allTiles.Count);
            for (int i = 0; i < count; i++)
            {
                int swapIdx = Random.Range(i, allTiles.Count);
                (allTiles[i], allTiles[swapIdx]) = (allTiles[swapIdx], allTiles[i]);
            }

            for (int i = 0; i < count; i++)
            {
                allTiles[i].AddAttribute(new CloudTileAttribute());
                _cloudTiles.Add(allTiles[i]);
            }

            Debug.Log($"[구름 패시브] 구름 타일 {_cloudTiles.Count}개 배치 완료");
        }

        public override void OnOwnerSelected(Character c)
        {
            if (_cloudTiles.Count == 0) return;
            TileSkillPreviewManager.EnsureInstance();
            TileSkillPreviewManager.Instance?.ShowPreview(_cloudTiles, TilePreviewStyle.Buff);
        }

        public override void OnOwnerDeselected()
        {
            TileSkillPreviewManager.Instance?.HidePreview();
        }

        public override void OnReact(CombatTrigger trigger, CombatContext context) { }

        public override IPassive Clone()
        {
            var clone = (MeteorologistCloudPassive)Instantiate(this);
            clone._cloudTiles = new List<TileData>();
            return clone;
        }
    }
}
