using Crew;
using UnityEngine;
using System.Collections.Generic;

public class MainStageCrewManager : MonoBehaviour
{
    [Header("Spawn Positions")]
    [SerializeField] private Transform[] _spawnPositions;

    [Header("Crew Database")]
    [SerializeField] private CrewDatabase _crewDatabase;

    private List<CrewInstanceData> _equippedCrewList = new();

    private readonly Dictionary<string, GameObject> _spawnedCrewObjects = new();


    private void Awake()
    {
        if (_crewDatabase == null)
            Debug.LogError("[MainStageCrewManager] CrewDatabase가 할당되지 않았습니다.");

        RuntimeStateEventBus.OnMainStageStateEntered += SpawnEquippedCrewOnShip;
    }

    private void LoadEquippedCrew()
    {
        _equippedCrewList = CrewManager.Instance.GetEquippedCrews();
    }

    public void SpawnEquippedCrewOnShip()
    {
        ClearSpawnedCrew();

        LoadEquippedCrew();

        if (_equippedCrewList == null || _equippedCrewList.Count == 0)
        {
            Debug.Log("[MainStageCrewManager] 장착된 선원이 없습니다.");
            return;
        }

        int spawnCount = Mathf.Min(_equippedCrewList.Count, _spawnPositions.Length);

        for (int i = 0; i < spawnCount; i++)
        {
            CrewInstanceData crewInstance = _equippedCrewList[i];

            if (crewInstance == null || string.IsNullOrEmpty(crewInstance.CrewId))
                continue;

            CrewData crewData = _crewDatabase.GetCrewData(crewInstance.CrewId);

            if (crewData == null)
            {
                Debug.LogWarning($"[MainStageCrewManager] CrewData를 찾을 수 없음: {crewInstance.CrewId}");
                continue;
            }

            if (crewData.CrewPrefab == null)
            {
                Debug.LogWarning($"[MainStageCrewManager] CrewPrefab이 없음: {crewInstance.CrewId}");
                continue;
            }

            if (_spawnPositions[i] == null)
            {
                Debug.LogWarning($"[MainStageCrewManager] SpawnPosition이 없음: 인덱스 {i}");
                continue;
            }

            Transform spawnPoint = _spawnPositions[i];

            GameObject crewObject = Instantiate(
                crewData.CrewPrefab,
                spawnPoint.position,
                spawnPoint.rotation,
                spawnPoint);

            _spawnedCrewObjects[crewInstance.CrewId] = crewObject;
        }

        Debug.Log($"[MainStageCrewManager] 선원 생성 완료: {_spawnedCrewObjects.Count}명");
    }

    public void ClearSpawnedCrew()
    {
        foreach (var kvp in _spawnedCrewObjects)
        {
            if (kvp.Value != null)
                Destroy(kvp.Value);
        }

        _spawnedCrewObjects.Clear();
    }

    public GameObject GetSpawnedCrewObject(string crewId)
    {
        if (_spawnedCrewObjects.TryGetValue(crewId, out GameObject obj))
            return obj;

        return null;
    }
}