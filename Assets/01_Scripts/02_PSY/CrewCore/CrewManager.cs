using JHS.Fishing;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Crew
{
    public class CrewManager : MonoBehaviour, ICrewBonus
    {
        // 선원 목록 관리
        // 1. 보유 선원 조회
        // 2. 장착 선원 조회
        // 3. 선원 배치/해제
        public static CrewManager Instance { get; private set;}
        [Header("데이터 베이스")]
        [SerializeField] private CrewDatabase _crewDatabase;
        [SerializeField] private ItemDatabase _itemDatabase;
        [SerializeField] private GachaDictionaryConvertor _gachaDictionaryConvertor;

        [Header("보유한 선원 정보")]
        [SerializeField] private List<CrewInstanceData> _ownedCrews = new();

        [Header("배치한 선원 정보")]
        [SerializeField] private List<CrewInstanceData> _equippedCrews = new();

        [Header("선원 패시브 효과 계수")]
        [SerializeField] private float _bossFishSpecializationPerLevel = 0.1f;
        [SerializeField] private float _fishRarityIncreasePerLevel = 0.05f;
        [SerializeField] private float _autoFishingSpeedPerLevel = 0.05f;
        [SerializeField] private float _offlineRewardEfficiencyPerLevel = 0.1f;
        [SerializeField] private float _multiplayerContributionPerLevel = 0.03f;

        [Header("선원 슬롯 관리")]
        [SerializeField] private List<CrewSlot> _crewSlots = new()
        {
            new CrewSlot { SlotIndex = 0, IsUnlocked = true },
            new CrewSlot { SlotIndex = 1, IsUnlocked = true },
            new CrewSlot { SlotIndex = 2, IsUnlocked = true },
            new CrewSlot { SlotIndex = 3, IsUnlocked = false },
        };

        [Header("선원 승급")]
        [SerializeField] private int _RtoSR = 50;
        [SerializeField] private int _SRtoSSR = 100;

        [System.Serializable]
        public class CrewSlot
        {
            public int SlotIndex;
            public bool IsUnlocked;
            public CrewInstanceData EquippedCrew;
        }

        public List<CrewSlot> CrewSlots => _crewSlots;
        public CrewDatabase CrewDataBase => _crewDatabase;
        public ItemDatabase ItemDataBase => _itemDatabase;
        public GachaDictionaryConvertor Convertor => _gachaDictionaryConvertor;
        public int RtoSR => _RtoSR;
        public int SRtoSSR => _SRtoSSR;

        public event Action OnCrewLoaded;

        public int GetDuplicateFragmentReward(CrewGrade grade)
        {
            return grade switch
            {
                CrewGrade.R => 70,
                CrewGrade.SR => 90,
                CrewGrade.SSR => 120,
                _ => 0
            };
        }

        public int GetRequiredFragmentCount(CrewGrade grade)
        {
            return grade switch
            {
                CrewGrade.R => RtoSR,
                CrewGrade.SR => SRtoSSR,
                _ => 0
            };
        }

        private Dictionary<string, FoodCatalogData> _foodCatalog;
        [System.Serializable]
        private class FoodCatalogData
        {
            public string displayNameKo;
            public int crewExp;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            DontDestroyOnLoad(gameObject);   // 필요한 경우
        }
        public float AutoPassiveBonus
        {
            get
            {
                int level = CrewPassiveCalculator.GetTotalPassiveLevel(_equippedCrews,
                        CrewPassiveType.MultiplayerContributionIncrease);

                return 1f + (level * _multiplayerContributionPerLevel);
            }
        }

        public float AutoSpeedMultiplier
        {
            get
            {
                int level = CrewPassiveCalculator.GetTotalPassiveLevel(_equippedCrews,
                        CrewPassiveType.AutoFishingSpeedIncrease);

                return 1f + (level * _autoFishingSpeedPerLevel);
            }
        }

        public float OfflineRewardBonus
        {
            get
            {
                int level = CrewPassiveCalculator.GetTotalPassiveLevel(_equippedCrews,
                        CrewPassiveType.OfflineRewardEfficiency);
                return 1f + (level * _offlineRewardEfficiencyPerLevel);    
            }
        }

        public float FishRarityBonus
        {
            get
            {
                int level = CrewPassiveCalculator.GetTotalPassiveLevel(_equippedCrews,
                        CrewPassiveType.FishRarityIncrease);
                return 1f + (level * _fishRarityIncreasePerLevel);
            }
        }

        public float BossFishBonus
        {
            get
            {
                int level = CrewPassiveCalculator.GetTotalPassiveLevel(_equippedCrews,
                        CrewPassiveType.BossFishSpecialization);
                return 1f + (level * _bossFishSpecializationPerLevel);
            }
        }

        public List<CrewInstanceData> GetOwnedCrews()
        {
            return _ownedCrews;
        }

        public List<CrewInstanceData> GetEquippedCrews()
        {
            return _crewSlots
                .Where(slot => slot.IsUnlocked)
                .Select(slot => slot.EquippedCrew)
                .Where(crew => crew != null)
                .ToList();
        }

        #region 패시브 보너스
        public float GetFishRarityBonus()
        {
            return CrewPassiveCalculator.GetFishRarityBonus(_equippedCrews);
        }

        public float GetOfflineRewardBonus()
        {
            return CrewPassiveCalculator.GetOfflineRewardBonus(_equippedCrews);
        }

        public float GetBossFishBonus()
        {
            return CrewPassiveCalculator.GetBossFishBonus(_equippedCrews);
        }

        public float GetMultiplayerContributionBonus()
        {
            return CrewPassiveCalculator.GetMultiplayerContributionBonus(_equippedCrews);
        }
        public float GetAutoFishingSpeedBonus()
        {
            return CrewPassiveCalculator.GetAutoFishingSpeedBonus(_equippedCrews);
        }

        #endregion

        #region 선원 슬롯 기능
        // 슬롯 해금
        public void UnlockSlot(Action<bool> onSuccess, Action<string> onFail)
        {
            PlayFabGateway.Instance.Crew.UnlockCrewSlot(
                onSuccess: result => {
                    _crewSlots[3].IsUnlocked = true;
                    SyncCrew(uploadCrew: false, uploadSlot: true, onComplete: () =>
                    {
                        Debug.Log("Crew Sync Complete");
                    });

                    onSuccess?.Invoke(result.allUnlocked);
                },
                onFail: error =>
                {
                    Debug.LogError($"슬롯 잠금 해제 실패.{error}");
                    onFail?.Invoke(error);
                });
        }

        //슬롯 잠금(디버그 기능)
        public void SlotLock()
        {
            PlayerInfo player = PlayFabDataStore.Instance.GetPlayerInfo();

            _crewSlots[3].IsUnlocked = false;

            player.crewSlot.crewSlots["3"].isUnlocked = false;

            Debug.Log("[Debug] 선원 슬롯 잠금");
            SyncCrew();
            return;
        }

        // 슬롯: 선원 배치
        public string EquipCrew(CrewInstanceData crew)
        {
            // 이미 배치된 선원인지 확인
            foreach (var slot in _crewSlots)
            {
                if (slot.EquippedCrew == crew)
                    return "이미 배치 된 선원입니다.";
            }

            foreach (var slot in _crewSlots)
            {
                if (!slot.IsUnlocked)
                    continue;

                if (slot.EquippedCrew != null)
                    continue;

                slot.EquippedCrew = crew;

                PlayerInfo player = PlayFabDataStore.Instance.GetPlayerInfo();

                string slotKey = slot.SlotIndex.ToString();
                player.crewSlot.crewSlots[slotKey].equippedCrewId = crew.CrewId;

                SyncCrew(uploadCrew: true, uploadSlot: true, onComplete: () =>
                {
                    Debug.Log("Crew Sync Complete");
                });

                _equippedCrews = GetEquippedCrews();

                return "선원이 배치되었습니다.";
            }

            return "배치할 수 있는 슬롯이 없습니다.";
        }


        // 슬롯: 선원 해제
        public void UnequipCrew(int slotIndex)
        {
            // 현재 장착된 선원 수 확인
            int equippedCount = _crewSlots.Count(slot => slot.EquippedCrew != null);

            // 마지막 선원은 해제 불가
            if (equippedCount <= 1)
            {
                Debug.Log("최소 1명의 선원은 배치되어 있어야 합니다.");
                return;
            }

            _crewSlots[slotIndex].EquippedCrew = null;

            PlayerInfo player = PlayFabDataStore.Instance.GetPlayerInfo();

            player.crewSlot.crewSlots[slotIndex.ToString()].equippedCrewId = null;

            SyncCrew(uploadCrew: true, uploadSlot: true, onComplete: () =>
            {
                Debug.Log("Crew Sync Complete");
            });

            _equippedCrews = GetEquippedCrews();
        }
        #endregion

        #region 선원 등급 업그레이드
        public int GetFragmentCount(string crewId)
        {
            string fragmentId = $"fragment_{crewId}";
            PlayerInfo player = PlayFabDataStore.Instance.GetPlayerInfo();

            if (player.inventory.inventoryItems.TryGetValue(fragmentId, out var item))
            {
                return item.itemCount;
            }

            return 0;
        }
        public void UpgradeCrew(CrewInstanceData crew, Action<CrewInstanceData> onSuccess, Action<string> onFail = null)
        {
            PlayFabGateway.Instance.Crew.Promote(crew.CrewId,
                onSuccess: result =>
                {
                    // 승급 완료 후 서버에서 최신 데이터 다시 받아오기
                    PlayFabGateway.Instance.RefreshAllPlayerData(
                        onSuccess: () =>
                        {
                            Debug.Log(
                                PlayFabDataStore.Instance
                                    .GetPlayerInfo()
                                    .crew
                                    .crews[crew.CrewId]
                                    .grade);

                            // 최신 PlayerData를 기반으로 CrewManager 갱신
                            LoadCrewData();

                            CrewInstanceData upgradedCrew = FindCrew(crew.CrewId);

                            onSuccess?.Invoke(upgradedCrew);
                        },

                        onError: error =>
                        {
                            onFail?.Invoke("플레이어 데이터 새로고침 실패");
                        });
                },

                onError: error =>
                {
                    Debug.LogError($"선원 승급 실패 : {error.GenerateErrorReport()}");

                    onFail?.Invoke(error.ErrorMessage);
                });
        }
        #endregion

        #region 선원 뽑기 결과 저장
        public void AddCrew(CrewInstanceData newCrew)
        {
            Debug.Log($"[AddCrew] BEFORE {_ownedCrews.Count}");

            _ownedCrews.Add(newCrew);

            Debug.Log($"[AddCrew] AFTER {_ownedCrews.Count}");

            foreach (var c in _ownedCrews)
                Debug.Log(c.CrewId);

            SyncCrew(onComplete: () =>
            {
                Debug.Log("Crew Sync Complete");
            });
        }

        public bool HasCrew(string crewId)
        {
            return _ownedCrews.Exists(crew => crew.CrewId == crewId);
        }
        public void SaveCrewData()
        {
            PlayerInfo player = PlayFabDataStore.Instance.GetPlayerInfo();

            Dictionary<string, CrewInfo> newCrews = new();

            foreach (var crew in _ownedCrews)
            {
                CrewInfo info = new();

                info.grade = crew.Grade.ToString();
                info.duplicateCount = crew.DuplicateCount;
                info.equipped = _crewSlots.Any(s => s.EquippedCrew == crew);

                foreach (var passive in crew.Passives)
                {
                    PassiveInfo target = passive.Type switch
                    {
                        CrewPassiveType.BossFishSpecialization =>
                            info.passives.BossFishSpecialization,

                        CrewPassiveType.FishRarityIncrease =>
                            info.passives.FishRarityIncrease,

                        CrewPassiveType.AutoFishingSpeedIncrease =>
                            info.passives.AutoFishingSpeedIncrease,

                        CrewPassiveType.OfflineRewardEfficiency =>
                            info.passives.OfflineRewardEfficiency,

                        CrewPassiveType.MultiplayerContributionIncrease =>
                            info.passives.MultiplayerContributionIncrease,

                        _ => null
                    };

                    if (target == null)
                        continue;

                    target.level = passive.Level;
                    target.levelProgress = Mathf.RoundToInt(passive.LevelProgress);
                }
                newCrews.Add(crew.CrewId, info);
            }
            player.crew.crews = newCrews;
            PlayFabDataStore.Instance.SaveLocal();
        }
        #endregion

        public void LoadCrewData()
        {
            Debug.Log("1. LoadCrewData");
            PlayerInfo player = PlayFabDataStore.Instance.GetPlayerInfo();

            _ownedCrews.Clear();

            foreach (var pair in player.crew.crews)
            {

                _ownedCrews.Add(CrewFactory.CreateFromJson(pair.Key, pair.Value));
            }

            LoadCrewSlots();

            Debug.Log($"OwnedCrew Count : {_ownedCrews.Count}");
            Debug.Log($"CrewSlot Count : {_crewSlots.Count}");
            Debug.Log("2. Invoke");
            OnCrewLoaded?.Invoke();
        }

        private void LoadCrewSlots()
        {
            PlayerInfo player = PlayFabDataStore.Instance.GetPlayerInfo();

            foreach (var pair in player.crewSlot.crewSlots)
            {
                int slotIndex = int.Parse(pair.Key);

                if (slotIndex < 0 || slotIndex >= _crewSlots.Count)
                    continue;

                _crewSlots[slotIndex].IsUnlocked = pair.Value.isUnlocked;

                if (string.IsNullOrEmpty(pair.Value.equippedCrewId))
                {
                    _crewSlots[slotIndex].EquippedCrew = null;
                    continue;
                }

                _crewSlots[slotIndex].EquippedCrew = _ownedCrews.Find(c => c.CrewId == pair.Value.equippedCrewId);
            }

            _equippedCrews = GetEquippedCrews();
        }

        private void SyncCrew(bool uploadCrew = true, bool uploadSlot = true, Action onComplete = null)
        {
            SaveCrewData();

            if (uploadCrew)
            {
                PlayFabGateway.Instance.Crew.SetCrewDatas(
                    PlayFabDataStore.Instance.GetPlayerInfo().crew.crews,
                    onSuccess: result =>
                    {
                        if (uploadSlot)
                        {
                            PlayFabGateway.Instance.Crew.UploadCrewSlots(
                                PlayFabDataStore.Instance.GetPlayerInfo().crewSlot.crewSlots,
                                onSuccess: _ => onComplete?.Invoke());
                        }
                        else
                        {
                            onComplete?.Invoke();
                        }
                    });
            }
            else
            {
                onComplete?.Invoke();
            }
        }

        public CrewInstanceData FindCrew(string crewId)
        {
            foreach (var crew in _ownedCrews)
            {
                if (crew.CrewId == crewId)
                {
                    return crew;
                }
            }

            return null;
        }

        #region 패시브
        // 패시브 강화
        public void IncreasePassiveProgress(string crewId, Action<CrewInstanceData> onSuccess = null, Action<string> onFail = null)
        {
            CrewInstanceData crew = _ownedCrews.Find(c => c.CrewId == crewId);

            if (crew == null)
            {
                onFail?.Invoke("선원을 찾을 수 없습니다.");
                return;
            }

            int maxLevel = CrewFactory.GetMaxPassiveLevel(crew.Grade);

            List<CrewPassiveData> activePassives = new();

            foreach (var p in crew.Passives)
            {
                if (p.Level >= 1)
                    activePassives.Add(p);
            }

            if (activePassives.Count == 0)
            {
                onFail?.Invoke("활성화된 패시브가 없습니다.");
                return;
            }

            int totalLevel = 0;
            foreach (var p in crew.Passives)
                totalLevel += p.Level;

            if (totalLevel >= maxLevel)
            {
                onFail?.Invoke("패시브 레벨이 최대치에 도달했습니다.");
                return;
            }

            string foodItemId = FindFoodInventory();

            if (string.IsNullOrEmpty(foodItemId))
            {
                onFail?.Invoke("요리가 없습니다.");
                return;
            }

            // 사용할 패시브 미리 선택
            CrewPassiveData passive =
                activePassives[UnityEngine.Random.Range(0, activePassives.Count)];

            int crewExp = GetCrewExp(foodItemId);

            // 먼저 음식 제거 요청
            PlayFabGateway.Instance.Inventory.Remove(foodItemId, 1);

            PlayFabGateway.Instance.Inventory.Flush(
                "FoodInventory",
                onSuccess: result =>
                {
                    // Flush 성공 후 패시브 강화 적용
                    passive.LevelProgress += crewExp;

                    while (true)
                    {
                        totalLevel = 0;
                        foreach (var p in crew.Passives)
                            totalLevel += p.Level;

                        if (totalLevel >= maxLevel)
                            break;

                        int requiredExp = GetRequiredCrewExp(crew.Grade, passive.Level);

                        if (passive.LevelProgress < requiredExp)
                            break;

                        passive.LevelProgress -= requiredExp;
                        passive.Level++;
                    }

                    SyncCrew(uploadCrew: true, uploadSlot: false, onComplete: () =>
                    {
                        Debug.Log("Crew Sync Complete");
                    });

                    Debug.Log($"After Feed : Level={passive.Level}, Progress={passive.LevelProgress}");

                    onSuccess?.Invoke(crew);
                },
                onFailure: error =>
                {
                    Debug.LogError($"요리 소모 실패 : {error}");

                    onFail?.Invoke("요리 소모에 실패했습니다.");
                });
        }

        // crewExp 요구량 계산
        public int GetRequiredCrewExp(CrewGrade grade, int currentLevel)
        {
            // Lv1 -> Lv2 = 50
            double exp = 20;

            // 현재 레벨까지 1.2배씩 증가
            for (int i = 1; i < currentLevel; i++)
            {
                exp *= 1.2;
            }

            // 등급 보정
            switch (grade)
            {
                case CrewGrade.R:
                    break;

                case CrewGrade.SR:
                    exp *= 1.5;
                    break;

                case CrewGrade.SSR:
                    exp *= 2.0;
                    break;
            }

            return Mathf.RoundToInt((float)exp);
        }

        // 선택한 패시브 한 개를 변경
        public bool RerollSinglePassive(string crewId, int passiveIndex)
        {
            CrewInstanceData crew = FindCrew(crewId);

            if (crew == null)
            {
                Debug.Log("선원을 찾을 수 없음");
                return false;
            }

            if (passiveIndex < 0 || passiveIndex >= crew.Passives.Count)
            {
                Debug.Log("잘못된 패시브 인덱스");
                return false;
            }

            // 활성화된 패시브
            List<CrewPassiveData> activePassives = new();

            foreach (var passive in crew.Passives)
            {
                if (passive.Level > 0)
                    activePassives.Add(passive);
            }

            if (passiveIndex < 0 || passiveIndex >= activePassives.Count)
            {
                Debug.Log("잘못된 패시브 인덱스");
                return false;
            }

            CrewPassiveData currentPassive = activePassives[passiveIndex];

            // 비활성화된 패시브
            List<CrewPassiveData> inactivePassives = new();

            foreach (var passive in crew.Passives)
            {
                if (passive.Level == 0)
                    inactivePassives.Add(passive);
            }

            // 랜덤 선택
            CrewPassiveData newPassive = inactivePassives[UnityEngine.Random.Range(0, inactivePassives.Count)];

            // 레벨 교환
            currentPassive.Level = 0;
            currentPassive.LevelProgress = 0;

            newPassive.Level = 1;
            newPassive.LevelProgress = 0;

            SyncCrew(uploadCrew: true, uploadSlot: false, onComplete: () =>
            {
                Debug.Log("Crew Sync Complete");
            });

            Debug.Log($"패시브 변경 완료 : {currentPassive.Type} -> {newPassive.Type}");

            return true;
        }
        #endregion

        #region 음식 정보 조회
        private void LoadFoodCatalog()
        {
            if (_foodCatalog != null)
                return;

            TextAsset json = Resources.Load<TextAsset>("05_CSH/RuntimeCatalog/FoodList");

            if (json == null)
            {
                Debug.LogError("FoodList.json을 찾을 수 없습니다.");
                return;
            }

            _foodCatalog = JsonConvert.DeserializeObject<Dictionary<string, FoodCatalogData>>(json.text);
        }

        private int GetCrewExp(string itemId)
        {
            LoadFoodCatalog();

            if (_foodCatalog != null &&
                _foodCatalog.TryGetValue(itemId, out var food))
            {
                return food.crewExp;
            }

            Debug.LogWarning($"FoodList에 '{itemId}'가 없습니다.");
            return 0;
        }

        private string FindFoodInventory()
        {
            LoadFoodCatalog();

            PlayerInfo player = PlayFabDataStore.Instance.GetPlayerInfo();

            string resultItemId = null;
            int minCrewExp = int.MaxValue;

            foreach (var pair in player.inventory.inventoryItems)
            {
                string itemId = pair.Key;
                var inventoryItem = pair.Value;

                // 수량이 없으면 제외
                if (inventoryItem.itemCount <= 0)
                    continue;

                // FoodList에 없는 아이템이면 제외
                if (!_foodCatalog.TryGetValue(itemId, out var food))
                    continue;

                // crewExp가 가장 작은 음식 선택
                if (food.crewExp < minCrewExp)
                {
                    minCrewExp = food.crewExp;
                    resultItemId = itemId;
                }
            }

            return resultItemId;
        }
        #endregion
    }
}