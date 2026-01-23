using System.Collections.Generic;
using UnityEngine;

namespace DiceOrbit.Data.Tile
{
    public class TileAttribute : MonoBehaviour
    {
        private List<IOnTraverse> traverses = new();
        private List<IOnArrive> arrives = new();

        public void OnArrive(Core.Character character) { 
            foreach (var arrive in arrives)
            {
                arrive.OnArrive(character);
            }
        }

        public void OnTraverse(Core.Character character)
        {
            foreach (var traverse in traverses)
            {
                traverse.OnTraverse(character);
            }
        }
    }
}

