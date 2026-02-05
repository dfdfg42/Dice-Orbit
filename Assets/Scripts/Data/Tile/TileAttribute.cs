using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.Data.Tile
{

    public class TileAttribute : MonoBehaviour
    {
        /// <summary>
        /// 타일을 지나갈 때 실행할 액션들 (직렬화되어 인스펙터에서 설정 가능).
        /// </summary>
        [Header("Actions")]
        [SerializeField] private List<IOnTraverse> traverses = new();

        /// <summary>
        /// 타일에 도착했을 때 실행할 액션들 (직렬화되어 인스펙터에서 설정 가능).
        /// </summary>
        [SerializeField] private List<IOnArrive> arrives = new();

        /// <summary>
        /// 타일에 도착했을 때 등록된 모든 `IOnArrive` 액션을 호출합니다.
        /// </summary>
        public void OnArrive(Core.Character character) { 
            foreach (var arrive in arrives)
            {
                arrive.OnArrive(character);
            }
        }

        /// <summary>
        /// 타일을 통과할 때 등록된 모든 `IOnTraverse` 액션을 호출합니다.
        /// </summary>
        public void OnTraverse(Core.Character character)
        {
            foreach (var traverse in traverses)
            {
                traverse.OnTraverse(character);
            }
        }

        /// <summary>
        /// 런타임에 `IOnTraverse` 액션을 추가합니다.
        /// </summary>
        public void AddTraverse(IOnTraverse traverse)
        {
            traverses.Add(traverse);
        }

        public void RemoveTraverse(IOnTraverse traverse)
        {
            traverses.Remove(traverse);
        }

        /// <summary>
        /// 런타임에 `IOnArrive` 액션을 추가합니다.
        /// </summary>
        public void AddArrive(IOnArrive arrive)
        {
            arrives.Add(arrive);
        }

        public void RemoveArrive(IOnArrive arrive)
        {
            arrives.Remove(arrive);
        }

        public bool IsEmpty => traverses.Count == 0 && arrives.Count == 0;
    }
}

