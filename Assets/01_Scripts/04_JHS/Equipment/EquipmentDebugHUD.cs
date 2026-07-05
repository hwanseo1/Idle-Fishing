using System;
using UnityEngine;

namespace JHS.Equipment
{
    // 장비 등급/강화 + 배 액티브 스킬 상태 확인용 디버그. (실제 UI 붙으면 제거)
    public class EquipmentDebugHUD : MonoBehaviour
    {
        [SerializeField] private EquipmentManager _manager;
        [SerializeField] private BoatActiveSkillController _boat;  // 선택

        private void OnEnable()
        {
            if (_manager == null) _manager = GetComponent<EquipmentManager>();
            if (_boat == null) _boat = GetComponent<BoatActiveSkillController>();
        }

        private void OnGUI()
        {
            var mgr = _manager != null ? _manager : EquipmentManager.Instance;
            if (mgr == null) return;

            const int w = 320;
            GUILayout.BeginArea(new Rect(Screen.width - w - 10, 10, w, 480), GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"골드: {mgr.Gold:N0}", GUILayout.Width(170));
#if UNITY_EDITOR
            if (GUILayout.Button("+1만")) mgr.DebugAddGold(10_000);
            if (GUILayout.Button("+10만")) mgr.DebugAddGold(100_000);
#endif
            GUILayout.EndHorizontal();

            foreach (EquipmentType type in Enum.GetValues(typeof(EquipmentType)))
            {
                int lv = mgr.GetLevel(type);
                int max = mgr.GetMaxLevel(type);

                GUILayout.BeginHorizontal();
                GUILayout.Label($"{type}  Lv {lv}/{Mathf.Max(max, 0)}", GUILayout.Width(150));
                if (mgr.IsMaxLevel(type)) GUILayout.Label("MAX");
                else if (GUILayout.Button($"강화 ({mgr.GetUpgradeCost(type):N0}{MaterialLabel(mgr, type)})"))
                    Debug.Log($"[Equipment] {type} 강화 {(mgr.TryUpgrade(type) ? "성공" : "실패")}");
                GUILayout.EndHorizontal();
            }

            // 주요 스탯
            GUILayout.Space(4);
            GUILayout.Label($"다중포획 확률 {mgr.GetStat(EquipmentStat.MultiCatchChance):0.##} / 마릿수 {mgr.GetStat(EquipmentStat.MultiCatchCount, 1):0.#}");
            GUILayout.Label($"입질감소 {mgr.GetStat(EquipmentStat.BiteIntervalReduce):0.##} / 보스피해 {mgr.GetStat(EquipmentStat.BossDamage):0.#}");

            // 배 액티브 스킬 상태
            if (_boat != null)
            {
                GUILayout.Space(4);
                GUILayout.Label("배 액티브 스킬 (cd / 발동중)");
                foreach (var s in _boat.GetStates())
                    GUILayout.Label($"  {s.skill}  cd {s.cd:0.0}  {(s.active > 0 ? $"발동 {s.active:0.0}s" : "")}");
            }

            // 배 스킬 강화 + 장착 (스킬마다 개별, 장착은 1척만)
            GUILayout.Space(4);
            GUILayout.Label($"배 스킬 강화 / 장착   (현재 장착: {(mgr.HasEquippedBoat ? mgr.EquippedBoat.ToString() : "없음")})");
            foreach (var s in mgr.GetBoatSkills())
            {
                if (s == null) continue;
                BoatSkill skill = s.skill;
                int lv = mgr.GetBoatSkillLevel(skill);
                int max = mgr.GetBoatSkillMaxLevel(skill);
                bool equipped = mgr.HasEquippedBoat && mgr.EquippedBoat == skill;

                GUILayout.BeginHorizontal();
                GUILayout.Label($"{(equipped ? "▶" : "  ")}{skill} Lv{lv}/{Mathf.Max(max, 0)} v{mgr.GetBoatSkillValue(skill):0.##}", GUILayout.Width(190));
                if (mgr.IsBoatSkillMax(skill)) GUILayout.Label("MAX", GUILayout.Width(54));
                else if (GUILayout.Button($"강화({mgr.GetBoatSkillUpgradeCost(skill):N0}{BoatMaterialLabel(mgr, skill)})"))
                    Debug.Log($"[Boat] {skill} 강화 {(mgr.TryUpgradeBoatSkill(skill) ? "성공" : "실패")}");

                if (equipped) GUILayout.Label("장착중");
                else if (GUILayout.Button("장착"))
                    Debug.Log($"[Boat] {skill} 장착 {(mgr.TryEquipBoat(skill) ? "성공" : "실패")}");
                GUILayout.EndHorizontal();
            }

            GUILayout.EndArea();
        }

        // 강화 버튼에 붙일 재료 요구치 문자열 (재료 없으면 빈 문자열)
        private static string MaterialLabel(EquipmentManager mgr, EquipmentType type)
            => FormatMaterials(mgr.GetUpgradeMaterials(type));

        private static string BoatMaterialLabel(EquipmentManager mgr, BoatSkill skill)
            => FormatMaterials(mgr.GetBoatSkillUpgradeMaterials(skill));

        private static string FormatMaterials(System.Collections.Generic.IReadOnlyList<MaterialCost> mats)
        {
            if (mats == null || mats.Count == 0) return "";
            var sb = new System.Text.StringBuilder();
            foreach (var m in mats)
                if (m.count > 0) sb.Append($" +{m.materialId}x{m.count}");
            return sb.ToString();
        }
    }
}
