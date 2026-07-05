using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fisher.Data
{
    /// <summary>
    /// foodId 기준 요리 결과물 성장 데이터를 Inspector에서 한 번에 넘겨주기 위한 목록 SO입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "FoodDatabase", menuName = "Scriptable Objects/FoodDatabase")]
    public sealed class FoodDatabase : ScriptableObject
    {
        [SerializeField] private FoodData[] _foods = Array.Empty<FoodData>();

        public FoodData[] Foods => _foods ?? Array.Empty<FoodData>();

        public bool TryGetFood(string foodId, out FoodData food)
        {
            food = null;
            if (string.IsNullOrWhiteSpace(foodId))
            {
                return false;
            }

            FoodData[] foods = Foods;
            for (int i = 0; i < foods.Length; i++)
            {
                FoodData current = foods[i];
                if (current != null && current.FoodId == foodId)
                {
                    food = current;
                    return true;
                }
            }

            return false;
        }

        public int GetCrewExpOrDefault(string foodId, int defaultValue = 0)
        {
            return TryGetFood(foodId, out FoodData food) ? food.CrewExp : defaultValue;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            FoodData[] foods = Foods;
            for (int i = 0; i < foods.Length; i++)
            {
                FoodData food = foods[i];
                if (food == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(food.FoodId))
                {
                    Debug.LogWarning($"[FoodDatabase] {name}: foods[{i}]의 foodId가 비어 있습니다.", this);
                    continue;
                }

                if (!seen.Add(food.FoodId))
                {
                    Debug.LogWarning($"[FoodDatabase] {name}: 중복 foodId가 있습니다. foodId={food.FoodId}", this);
                }
            }
        }
#endif
    }
}
