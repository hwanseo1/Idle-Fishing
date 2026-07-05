#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using RMS.Data;

namespace RMS.Editor
{
    /*
        멀티플레이 협동 낚시 매치 전용 StageData를 자동 생성하는 에디터 툴.

        목적:
        - Assets/03_Data/01_RMS/FishData/{Common,Rare,Epic,Legendary} 폴더를 스캔해
          30종 FishData를 모두 모아 fishEntries에 채운다.
        - 보스 없음(IsBossStage=false), NextStage 없음(매치는 타이머로 종료되므로
          스테이지 전환이 필요 없음), ClearContributionThreshold는 멀티 매치에서
          의미 없는 큰 값으로 둬서 FishSpawnManager의 자동 클리어 로직이 절대
          발동하지 않게 한다(클리어/스테이지 전환은 FishingGameManager의
          타이머가 담당).
        - 희귀도 가중치는 Common이 가장 흔하고 Legendary가 가장 희귀하도록
          100 / 40 / 12 / 3 (등비 감소)으로 설정한다.

        사용법:
        Unity 메뉴 → Tools/RMS/멀티플레이 스테이지 생성
    */
    public static class MultiplayStageDataGenerator
    {
        private const string FishDataRoot = "Assets/03_Data/01_RMS/FishData";
        private const string OutputFolder = "Assets/03_Data/01_RMS/StageData";
        private const string OutputAssetName = "StageData_MultiplayArena.asset";

        // 등급별 출현 가중치: Common이 가장 흔하고 Legendary가 가장 희귀.
        private static readonly (FishRarity rarity, float weight)[] RarityWeightTable = new[]
        {
            (FishRarity.Common, 100f),
            (FishRarity.Rare, 40f),
            (FishRarity.Epic, 12f),
            (FishRarity.Legendary, 3f),
        };

        [MenuItem("Tools/RMS/멀티플레이 스테이지 생성")]
        public static void GenerateMultiplayStage()
        {
            // 1. 30종 FishData 전체 스캔 (4개 희귀도 폴더)
            List<FishData> allFish = new List<FishData>();
            string[] subFolders = { "Common", "Rare", "Epic", "Legendary" };

            foreach (string sub in subFolders)
            {
                string folderPath = $"{FishDataRoot}/{sub}";
                if (!AssetDatabase.IsValidFolder(folderPath))
                {
                    Debug.LogWarning($"[MultiplayStageDataGenerator] 폴더를 찾을 수 없습니다: {folderPath}");
                    continue;
                }

                string[] guids = AssetDatabase.FindAssets("t:FishData", new[] { folderPath });
                foreach (string guid in guids)
                {
                    string fishAssetPath = AssetDatabase.GUIDToAssetPath(guid);
                    FishData fish = AssetDatabase.LoadAssetAtPath<FishData>(fishAssetPath);
                    if (fish != null) allFish.Add(fish);
                }
            }

            if (allFish.Count == 0)
            {
                Debug.LogError("[MultiplayStageDataGenerator] FishData를 하나도 찾지 못했습니다. " +
                    $"경로를 확인하세요: {FishDataRoot}");
                return;
            }

            Debug.Log($"[MultiplayStageDataGenerator] FishData {allFish.Count}종 발견. " +
                $"(기대값: 30종 — 다르면 폴더 구성을 확인하세요)");

            // 2. fishEntries 배열 생성 (같은 등급 내에서는 spawnWeight 동일하게 1)
            FishEntry[] fishEntries = new FishEntry[allFish.Count];
            for (int i = 0; i < allFish.Count; i++)
            {
                fishEntries[i] = new FishEntry
                {
                    fishData = allFish[i],
                    spawnWeight = 1f
                };
            }

            // 3. rarityWeights 배열 생성
            RarityWeightEntry[] rarityWeights = new RarityWeightEntry[RarityWeightTable.Length];
            for (int i = 0; i < RarityWeightTable.Length; i++)
            {
                rarityWeights[i] = new RarityWeightEntry
                {
                    rarity = RarityWeightTable[i].rarity,
                    weight = RarityWeightTable[i].weight
                };
            }

            // 4. 출력 폴더 보장
            if (!AssetDatabase.IsValidFolder(OutputFolder))
            {
                string parent = Path.GetDirectoryName(OutputFolder).Replace("\\", "/");
                string newFolderName = Path.GetFileName(OutputFolder);
                if (!AssetDatabase.IsValidFolder(parent))
                {
                    Debug.LogError($"[MultiplayStageDataGenerator] 상위 폴더가 없습니다: {parent}. " +
                        "먼저 03_Data/01_RMS 폴더 구조를 확인하세요.");
                    return;
                }
                AssetDatabase.CreateFolder(parent, newFolderName);
            }

            string assetPath = $"{OutputFolder}/{OutputAssetName}";

            // 5. 기존 에셋이 있으면 갱신, 없으면 새로 생성
            StageData stage = AssetDatabase.LoadAssetAtPath<StageData>(assetPath);
            bool isNew = stage == null;
            if (isNew)
            {
                stage = ScriptableObject.CreateInstance<StageData>();
            }

            // SerializedObject로 private 필드에 안전하게 접근 (StageData에 별도 setter가 없으므로)
            SerializedObject so = new SerializedObject(stage);
            so.FindProperty("_stageId").stringValue = "multiplay_arena";
            so.FindProperty("_displayName").stringValue = "협동 낚시 매치 (멀티플레이)";
            so.FindProperty("_isBossStage").boolValue = false;
            so.FindProperty("_bossData").objectReferenceValue = null;
            so.FindProperty("_nextStage").objectReferenceValue = null;

            // 클리어 조건은 멀티 매치에서 의미 없음 → 매치 시간 내 절대 도달 못 할 큰 값으로 고정.
            // (FishSpawnManager.CheckStageCleared가 의도치 않게 발동해 MoveToNextStage를
            //  호출하는 사고를 막기 위한 안전장치. 실제 클리어/종료는 FishingGameManager가 담당)
            so.FindProperty("_clearContributionThreshold").floatValue = 999999f;

            ApplyArray(so.FindProperty("_fishEntries"), fishEntries.Length, (prop, i) =>
            {
                prop.FindPropertyRelative("fishData").objectReferenceValue = fishEntries[i].fishData;
                prop.FindPropertyRelative("spawnWeight").floatValue = fishEntries[i].spawnWeight;
            });

            ApplyArray(so.FindProperty("_rarityWeights"), rarityWeights.Length, (prop, i) =>
            {
                prop.FindPropertyRelative("rarity").enumValueIndex = (int)rarityWeights[i].rarity;
                prop.FindPropertyRelative("weight").floatValue = rarityWeights[i].weight;
            });

            so.ApplyModifiedPropertiesWithoutUndo();

            if (isNew)
            {
                AssetDatabase.CreateAsset(stage, assetPath);
            }
            else
            {
                EditorUtility.SetDirty(stage);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=#00FF00>[MultiplayStageDataGenerator] 완료: {assetPath} " +
                $"(물고기 {fishEntries.Length}종, 희귀도 가중치 {rarityWeights.Length}개)</color>");

            EditorGUIUtility.PingObject(stage);
            Selection.activeObject = stage;
        }

        // SerializedProperty 배열 크기를 설정하고 각 원소를 채우는 헬퍼.
        private static void ApplyArray(SerializedProperty arrayProp, int count, System.Action<SerializedProperty, int> fillElement)
        {
            arrayProp.arraySize = count;
            for (int i = 0; i < count; i++)
            {
                SerializedProperty element = arrayProp.GetArrayElementAtIndex(i);
                fillElement(element, i);
            }
        }
    }
}
#endif