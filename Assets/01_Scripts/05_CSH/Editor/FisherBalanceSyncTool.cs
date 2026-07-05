using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using JHS.Fishing;
using OfflineReward;
using RMS.Data;
using RMS.Fishing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fisher.PlayerSystems.Editor
{
    /// <summary>
    /// Core balance CSV snapshots are the local export layer from the balance workbook.
    /// Generated CSV is the Unity-facing contract layer. This tool only applies whitelisted numeric fields.
    /// </summary>
    public static class FisherBalanceSyncTool
    {
        private const string CoreFolder = "Assets/03_Data/05_CSH/BalanceCore";
        private const string GeneratedFolder = "Assets/03_Data/05_CSH/Generated";
        private const string SceneFolder = "Assets/00_Scenes";
        private const string MainScenePath = "Assets/00_Scenes/00_MainScene.unity";
        private const string PrefabFolder = "Assets/02_Prefabs";
        private const string StageDataFolder = "Assets/03_Data/01_RMS/StageData";

        private const string BalanceParamsCsv = "balance_params.csv";
        private const string GachaRateTiersCsv = "gacha_rate_tiers.csv";
        private const string GachaDuplicateRewardsCsv = "gacha_duplicate_rewards.csv";
        private const string StageFishWeightsCsv = "rms_stage_fish_weights.csv";
        private const string Project3Root = "D:/Users/admin/Documents/project3";
        private const string Project3ValidatorScript = "tools/fisher_balance_generated_csv_validator.py";
        private const string Project3ValidatorBase =
            "docs/evidence/exports/fisher-balance-source-refresh-2026-06-29";
        private const int ValidatorTimeoutMs = 120000;

        private static readonly string[] BalanceCsvFiles =
        {
            BalanceParamsCsv,
            GachaRateTiersCsv,
            GachaDuplicateRewardsCsv,
            StageFishWeightsCsv
        };

        private static readonly string[] RequiredParamKeys =
        {
            "fishing_manual_base_reward",
            "fishing_auto_target_contribution",
            "fishing_auto_penalty",
            "manual_multi_catch_common_chance",
            "manual_multi_catch_rare_chance",
            "manual_multi_catch_epic_chance",
            "manual_multi_catch_legendary_chance",
            "manual_multi_catch_count_min",
            "manual_multi_catch_count_max",
            "manual_boss_multi_catch_chance",
            "manual_boss_multi_catch_multiplier",
            "manual_minigame_fill_speed",
            "manual_minigame_drain_speed",
            "manual_minigame_fail_speed_base",
            "manual_minigame_fail_speed_difficulty_scale",
            "manual_minigame_fail_start_delay",
            "manual_minigame_base_barrier_clicks",
            "offline_reward_base_cap_hours",
            "offline_reward_fish_per_hour"
        };

        [MenuItem("FISHER/밸런스/1. 코어 엑셀 -> Generated CSV 동기화", false, 10)]
        public static void SyncCoreToGeneratedMenu()
        {
            try
            {
                BalanceSyncReport report = SyncCoreToGenerated();
                Debug.Log(report.BuildMessage());
                EditorUtility.DisplayDialog("Fisher 밸런스 CSV 동기화", report.BuildMessage(), "확인");
            }
            catch (Exception exception)
            {
                Debug.LogError("[Fisher Balance Sync]\n" + exception);
                EditorUtility.DisplayDialog("Fisher 밸런스 CSV 동기화 실패", exception.Message, "확인");
            }
        }

        [MenuItem("FISHER/밸런스/2. Generated CSV 검증만", false, 11)]
        public static void ValidateGeneratedMenu()
        {
            try
            {
                Project3ValidatorReport report = RunProject3GeneratedCsvValidator();
                string message = report.BuildMessage();
                if (report.Success)
                {
                    Debug.Log(message);
                }
                else
                {
                    Debug.LogError(message);
                }

                ShowProject3ValidatorDialog(report);
            }
            catch (Exception exception)
            {
                Debug.LogError("[Fisher Balance Validate]\n" + exception);
                EditorUtility.DisplayDialog("Fisher 밸런스 CSV 검증 실패", exception.Message, "확인");
            }
        }

        [MenuItem("FISHER/밸런스/3. Generated CSV -> Unity 밸런스 적용", false, 12)]
        public static void ApplyGeneratedMenu()
        {
            try
            {
                BalanceSyncReport validation = ValidateGenerated();
                if (!validation.Success)
                {
                    Debug.LogWarning(validation.BuildMessage());
                    EditorUtility.DisplayDialog("Fisher 밸런스 적용 중단", validation.BuildMessage(), "확인");
                    return;
                }

                string preview =
                    "Generated CSV 검증 통과.\n\n" +
                    "적용 대상:\n" +
                    "- 00_MainScene.unity 안의 AutoFishingController 수치\n" +
                    "- 00_MainScene.unity 안의 OfflineRewardCaculator 수치\n" +
                    "- 00_MainScene.unity 안의 GachaSystem 확률 수치\n" +
                    "- ManualFishingMinigame 수치\n" +
                    "- RewardManager 중복 조각 수치\n" +
                    "- StageData 기존 fish spawnWeight\n\n" +
                    "01_RMS, 02_PSY, 03_YWJ, 04_JHS 씬은 열거나 저장하지 않습니다.\n" +
                    "열린 다른 씬의 저장도 요청하지 않습니다.\n\n" +
                    "참조 추가/삭제, PlayFab, 결제, 서버 계약은 건드리지 않습니다.";

                if (!EditorUtility.DisplayDialog("Fisher 밸런스 적용", preview, "적용", "취소"))
                {
                    return;
                }

                if (!ConfirmMainSceneSaveIfDirty())
                {
                    Debug.LogWarning("[Fisher Balance Apply] MainScene 저장 확인이 취소되어 적용을 중단했습니다.");
                    return;
                }

                BalanceSyncReport report = ApplyGenerated();
                Debug.Log(report.BuildMessage());
                EditorUtility.DisplayDialog("Fisher 밸런스 적용", report.BuildMessage(), "확인");
            }
            catch (Exception exception)
            {
                Debug.LogError("[Fisher Balance Apply]\n" + exception);
                EditorUtility.DisplayDialog("Fisher 밸런스 적용 실패", exception.Message, "확인");
            }
        }

        private static BalanceSyncReport SyncCoreToGenerated()
        {
            BalanceSyncReport report = new BalanceSyncReport("Core CSV -> Generated CSV");
            EnsureFolder(GeneratedFolder);

            for (int i = 0; i < BalanceCsvFiles.Length; i++)
            {
                string fileName = BalanceCsvFiles[i];
                string sourcePath = CombineAssetPath(CoreFolder, fileName);
                string targetPath = CombineAssetPath(GeneratedFolder, fileName);
                if (!File.Exists(ToFullPath(sourcePath)))
                {
                    report.Errors.Add("Core CSV missing: " + sourcePath);
                    continue;
                }

                File.Copy(ToFullPath(sourcePath), ToFullPath(targetPath), true);
                report.Changed.Add(fileName + " copied");
            }

            AssetDatabase.Refresh();
            if (report.Errors.Count == 0)
            {
                BalanceSyncReport validation = ValidateGenerated();
                report.Warnings.AddRange(validation.Warnings);
                report.Errors.AddRange(validation.Errors);
            }

            return report;
        }

        private static BalanceSyncReport ValidateGenerated()
        {
            BalanceSyncReport report = new BalanceSyncReport("Generated CSV Validation");
            BalanceData data = LoadGeneratedData(report);
            if (report.Errors.Count > 0)
            {
                return report;
            }

            ValidateRequiredParams(data, report);
            ValidateDerivedAutoPenalty(data, report);
            ValidateGachaRates(data, report);
            ValidateGachaDuplicateRewards(data, report);
            ValidateStageFishWeights(data, report);
            ValidateSceneGachaGhosts(report);

            return report;
        }

        private static Project3ValidatorReport RunProject3GeneratedCsvValidator()
        {
            string project3Root = NormalizeFullPath(Project3Root);
            string validatorPath = Path.Combine(project3Root, Project3ValidatorScript.Replace("/", Path.DirectorySeparatorChar.ToString()));
            string validatorBase = Path.Combine(project3Root, Project3ValidatorBase.Replace("/", Path.DirectorySeparatorChar.ToString()));
            string generatedPath = NormalizeFullPath(ToFullPath(GeneratedFolder));
            string balanceCorePath = NormalizeFullPath(ToFullPath(CoreFolder));

            if (!File.Exists(validatorPath))
            {
                throw new FileNotFoundException("Project3 Generated CSV validator를 찾지 못했습니다.", validatorPath);
            }

            string arguments =
                QuoteArg(validatorPath) +
                " --base " + QuoteArg(validatorBase) +
                " --generated-dir " + QuoteArg(generatedPath) +
                " --balancecore-dir " + QuoteArg(balanceCorePath);

            string configuredPython = Environment.GetEnvironmentVariable("PROJECT3_FISHER_PYTHON");
            List<string> pythonCandidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(configuredPython))
            {
                pythonCandidates.Add(configuredPython);
            }

            pythonCandidates.Add("python");
            pythonCandidates.Add("py");

            Exception lastException = null;
            for (int i = 0; i < pythonCandidates.Count; i++)
            {
                string executable = pythonCandidates[i];
                string candidateArguments = string.Equals(executable, "py", StringComparison.OrdinalIgnoreCase)
                    ? "-3 " + arguments
                    : arguments;

                try
                {
                    return RunProject3ValidatorProcess(
                        executable,
                        candidateArguments,
                        project3Root,
                        validatorBase);
                }
                catch (System.ComponentModel.Win32Exception exception)
                {
                    lastException = exception;
                }
            }

            throw new InvalidOperationException(
                "Python 실행 파일을 찾지 못했습니다. PROJECT3_FISHER_PYTHON 환경 변수 또는 python PATH를 확인하세요.",
                lastException);
        }

        private static Project3ValidatorReport RunProject3ValidatorProcess(
            string executable,
            string arguments,
            string workingDirectory,
            string validatorBase)
        {
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            StringBuilder stdout = new StringBuilder();
            StringBuilder stderr = new StringBuilder();
            using (System.Diagnostics.Process process = new System.Diagnostics.Process())
            {
                process.StartInfo = startInfo;
                process.OutputDataReceived += (_, args) =>
                {
                    if (args.Data != null)
                    {
                        stdout.AppendLine(args.Data);
                    }
                };
                process.ErrorDataReceived += (_, args) =>
                {
                    if (args.Data != null)
                    {
                        stderr.AppendLine(args.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit(ValidatorTimeoutMs))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch (Exception)
                    {
                    }

                    throw new TimeoutException("Generated CSV validator timed out after " + ValidatorTimeoutMs + "ms.");
                }

                process.WaitForExit();
                return new Project3ValidatorReport(
                    executable,
                    arguments,
                    process.ExitCode,
                    stdout.ToString(),
                    stderr.ToString(),
                    validatorBase);
            }
        }

        private static void ShowProject3ValidatorDialog(Project3ValidatorReport report)
        {
            int choice = EditorUtility.DisplayDialogComplex(
                "Fisher Generated CSV Validator",
                report.BuildDialogMessage(),
                "결과 MD 열기",
                "확인",
                "결과 폴더 열기");

            if (choice == 0)
            {
                OpenPath(report.ResultsMarkdownPath);
            }
            else if (choice == 2)
            {
                OpenPath(report.ResultsDirectory);
            }
        }

        private static BalanceSyncReport ApplyGenerated()
        {
            BalanceSyncReport report = new BalanceSyncReport("Generated CSV -> Unity");
            BalanceData data = LoadGeneratedData(report);
            if (report.Errors.Count > 0)
            {
                return report;
            }

            ValidateRequiredParams(data, report);
            ValidateDerivedAutoPenalty(data, report);
            ValidateGachaRates(data, report);
            ValidateGachaDuplicateRewards(data, report);
            ValidateStageFishWeights(data, report);
            if (report.Errors.Count > 0)
            {
                return report;
            }

            PatchSourceDefaults(data, report);
            ApplyScenes(data, report);
            ApplyManualMinigamePrefabs(data, report);
            ApplyStageFishWeights(data, report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return report;
        }

        private static BalanceData LoadGeneratedData(BalanceSyncReport report)
        {
            BalanceData data = new BalanceData();
            data.Params = LoadTable(GeneratedFolder, BalanceParamsCsv, report);
            data.GachaRates = LoadTable(GeneratedFolder, GachaRateTiersCsv, report);
            data.GachaDuplicateRewards = LoadTable(GeneratedFolder, GachaDuplicateRewardsCsv, report);
            data.StageFishWeights = LoadTable(GeneratedFolder, StageFishWeightsCsv, report);
            return data;
        }

        private static CsvTable LoadTable(string folder, string fileName, BalanceSyncReport report)
        {
            string path = CombineAssetPath(folder, fileName);
            string fullPath = ToFullPath(path);
            if (!File.Exists(fullPath))
            {
                report.Errors.Add("CSV missing: " + path);
                return CsvTable.Empty(fileName);
            }

            CsvTable table = CsvTable.Parse(fileName, File.ReadAllText(fullPath, Encoding.UTF8));
            if (table.Headers.Count == 0)
            {
                report.Errors.Add(fileName + ": header is empty");
            }

            report.Checked.Add(fileName + " rows=" + table.Rows.Count);
            return table;
        }

        private static void ValidateRequiredParams(BalanceData data, BalanceSyncReport report)
        {
            data.ParamRows.Clear();
            for (int i = 0; i < data.Params.Rows.Count; i++)
            {
                CsvRow row = data.Params.Rows[i];
                string key = row.Get("key");
                if (string.IsNullOrEmpty(key))
                {
                    report.Errors.Add(BalanceParamsCsv + ": empty key at row " + (i + 2));
                    continue;
                }

                if (data.ParamRows.ContainsKey(key))
                {
                    report.Errors.Add(BalanceParamsCsv + ": duplicate key " + key);
                    continue;
                }

                data.ParamRows.Add(key, row);
            }

            for (int i = 0; i < RequiredParamKeys.Length; i++)
            {
                if (!data.ParamRows.ContainsKey(RequiredParamKeys[i]))
                {
                    report.Errors.Add(BalanceParamsCsv + ": required key missing: " + RequiredParamKeys[i]);
                }
            }

            RequireFloat(data, "fishing_manual_base_reward", 0.01f, 100f, report);
            RequireFloat(data, "fishing_auto_target_contribution", 0f, 100f, report);
            RequireFloat(data, "fishing_auto_penalty", 0f, 1f, report);
            RequireFloat(data, "manual_multi_catch_common_chance", 0f, 1f, report);
            RequireFloat(data, "manual_multi_catch_rare_chance", 0f, 1f, report);
            RequireFloat(data, "manual_multi_catch_epic_chance", 0f, 1f, report);
            RequireFloat(data, "manual_multi_catch_legendary_chance", 0f, 1f, report);
            RequireInt(data, "manual_multi_catch_count_min", 1, 100, report);
            RequireInt(data, "manual_multi_catch_count_max", 1, 100, report);
            RequireFloat(data, "manual_boss_multi_catch_chance", 0f, 1f, report);
            RequireInt(data, "manual_boss_multi_catch_multiplier", 1, 100, report);
            RequireFloat(data, "manual_minigame_fill_speed", 0.01f, 100f, report);
            RequireFloat(data, "manual_minigame_drain_speed", 0f, 100f, report);
            RequireFloat(data, "manual_minigame_fail_speed_base", 0f, 100f, report);
            RequireFloat(data, "manual_minigame_fail_speed_difficulty_scale", 0f, 100f, report);
            RequireFloat(data, "manual_minigame_fail_start_delay", 0f, 100f, report);
            RequireInt(data, "manual_minigame_base_barrier_clicks", 0, 100, report);
            RequireInt(data, "offline_reward_base_cap_hours", 0, 240, report);
            RequireInt(data, "offline_reward_fish_per_hour", 0, 100000, report);

            if (TryGetInt(data, "manual_multi_catch_count_min", out int minCount) &&
                TryGetInt(data, "manual_multi_catch_count_max", out int maxCount) &&
                minCount > maxCount)
            {
                report.Errors.Add("manual_multi_catch_count_min cannot exceed max");
            }
        }

        private static void ValidateDerivedAutoPenalty(BalanceData data, BalanceSyncReport report)
        {
            if (!TryGetFloat(data, "fishing_manual_base_reward", out float manualBase) ||
                !TryGetFloat(data, "fishing_auto_target_contribution", out float targetAuto) ||
                !TryGetFloat(data, "fishing_auto_penalty", out float autoPenalty))
            {
                return;
            }

            float expected = targetAuto / manualBase;
            if (Math.Abs(expected - autoPenalty) > 0.00001f)
            {
                report.Errors.Add(
                    "fishing_auto_penalty mismatch. expected " +
                    FormatFloat(expected) +
                    " from target/manual, csv " +
                    FormatFloat(autoPenalty));
            }
        }

        private static void ValidateGachaRates(BalanceData data, BalanceSyncReport report)
        {
            RequireHeaders(data.GachaRates, GachaRateTiersCsv, report, "bannerId", "rollLayer", "rewardType", "grade", "ratePct", "isEnabled");
            Dictionary<string, float> sums = new Dictionary<string, float>(StringComparer.Ordinal);
            for (int i = 0; i < data.GachaRates.Rows.Count; i++)
            {
                CsvRow row = data.GachaRates.Rows[i];
                if (!ParseBool(row.Get("isEnabled"), true))
                {
                    continue;
                }

                string group = row.Get("bannerId") + "/" + row.Get("rollLayer");
                if (!TryParseFloat(row.Get("ratePct"), out float rate))
                {
                    report.Errors.Add(GachaRateTiersCsv + ": invalid ratePct at row " + (i + 2));
                    continue;
                }

                if (rate < 0f || rate > 100f)
                {
                    report.Errors.Add(GachaRateTiersCsv + ": ratePct out of range at row " + (i + 2));
                }

                if (!sums.ContainsKey(group))
                {
                    sums[group] = 0f;
                }

                sums[group] += rate;
            }

            foreach (KeyValuePair<string, float> pair in sums)
            {
                if (Math.Abs(pair.Value - 100f) > 0.0001f)
                {
                    report.Errors.Add(GachaRateTiersCsv + ": " + pair.Key + " rate sum is " + FormatFloat(pair.Value) + ", expected 100");
                }
            }

            RequireGachaRate(data, "premium_crew", "crew_grade", "R", report);
            RequireGachaRate(data, "premium_crew", "crew_grade", "SR", report);
            RequireGachaRate(data, "premium_crew", "crew_grade", "SSR", report);
            RequireGachaRewardRate(data, "basic_material", "reward_type", "Materials", report);
            RequireGachaRewardRate(data, "basic_material", "reward_type", "CrewFragment", report);
            RequireGachaRewardRate(data, "basic_material", "reward_type", "Crew", report);
        }

        private static void ValidateGachaDuplicateRewards(BalanceData data, BalanceSyncReport report)
        {
            RequireHeaders(data.GachaDuplicateRewards, GachaDuplicateRewardsCsv, report, "grade", "duplicateFragmentAmount", "fragmentIdPattern", "isEnabled");
            RequireDuplicateGrade(data, "R", report);
            RequireDuplicateGrade(data, "SR", report);
            RequireDuplicateGrade(data, "SSR", report);
        }

        private static void ValidateStageFishWeights(BalanceData data, BalanceSyncReport report)
        {
            RequireHeaders(data.StageFishWeights, StageFishWeightsCsv, report, "stageId", "fishId", "spawnWeight", "isEnabled", "applyMode");
            Dictionary<string, StageData> stages = LoadStageDataById(report);
            for (int i = 0; i < data.StageFishWeights.Rows.Count; i++)
            {
                CsvRow row = data.StageFishWeights.Rows[i];
                if (!ParseBool(row.Get("isEnabled"), true))
                {
                    continue;
                }

                string applyMode = row.Get("applyMode");
                if (!string.Equals(applyMode, "applyExisting", StringComparison.Ordinal) &&
                    !string.Equals(applyMode, "validateOnly", StringComparison.Ordinal))
                {
                    report.Errors.Add(StageFishWeightsCsv + ": unsupported applyMode at row " + (i + 2) + ": " + applyMode);
                }

                if (!TryParseFloat(row.Get("spawnWeight"), out float weight) || weight < 0f)
                {
                    report.Errors.Add(StageFishWeightsCsv + ": invalid spawnWeight at row " + (i + 2));
                    continue;
                }

                string stageId = row.Get("stageId");
                string fishId = row.Get("fishId");
                if (!stages.TryGetValue(stageId, out StageData stage))
                {
                    report.Errors.Add(StageFishWeightsCsv + ": stage missing at row " + (i + 2) + ": " + stageId);
                    continue;
                }

                if (!StageContainsFish(stage, fishId))
                {
                    report.Errors.Add(StageFishWeightsCsv + ": " + stageId + " has no existing fish entry " + fishId);
                }
            }
        }

        private static void ValidateSceneGachaGhosts(BalanceSyncReport report)
        {
            string[] sceneGuids = AssetDatabase.FindAssets("t:SceneAsset", new[] { SceneFolder });
            for (int i = 0; i < sceneGuids.Length; i++)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                string fullPath = ToFullPath(scenePath);
                if (!File.Exists(fullPath))
                {
                    continue;
                }

                string text = File.ReadAllText(fullPath, Encoding.UTF8);
                if (text.IndexOf("Crew.CrewGachaSystem", StringComparison.Ordinal) >= 0)
                {
                    report.Warnings.Add(scenePath + ": legacy/missing Crew.CrewGachaSystem component reference remains. Not applied.");
                }
            }
        }

        private static void PatchSourceDefaults(BalanceData data, BalanceSyncReport report)
        {
            string autoPath = "Assets/01_Scripts/04_JHS/AutoFishing/AutoFishingController.cs";
            PatchSourceFloat(autoPath, "_baseReward", GetFloat(data, "fishing_manual_base_reward"), report);
            PatchSourceFloat(autoPath, "_autoPenalty", GetFloat(data, "fishing_auto_penalty"), report);
            PatchSourceFloat(autoPath, "_commonMultiCatchChance", GetFloat(data, "manual_multi_catch_common_chance"), report);
            PatchSourceFloat(autoPath, "_rareMultiCatchChance", GetFloat(data, "manual_multi_catch_rare_chance"), report);
            PatchSourceFloat(autoPath, "_epicMultiCatchChance", GetFloat(data, "manual_multi_catch_epic_chance"), report);
            PatchSourceFloat(autoPath, "_legendaryMultiCatchChance", GetFloat(data, "manual_multi_catch_legendary_chance"), report);
            PatchSourceInt(autoPath, "_multiCatchCountMin", GetInt(data, "manual_multi_catch_count_min"), report);
            PatchSourceInt(autoPath, "_multiCatchCountMax", GetInt(data, "manual_multi_catch_count_max"), report);
            PatchSourceFloat(autoPath, "_bossMultiCatchChance", GetFloat(data, "manual_boss_multi_catch_chance"), report);
            PatchSourceInt(autoPath, "_bossMultiCatchMultiplier", GetInt(data, "manual_boss_multi_catch_multiplier"), report);

            string minigamePath = "Assets/01_Scripts/01_RMS/Fishing/ManualFishingMinigame.cs";
            PatchSourceFloat(minigamePath, "_fillSpeed", GetFloat(data, "manual_minigame_fill_speed"), report);
            PatchSourceFloat(minigamePath, "_drainSpeed", GetFloat(data, "manual_minigame_drain_speed"), report);
            PatchSourceFloat(minigamePath, "_failSpeedBase", GetFloat(data, "manual_minigame_fail_speed_base"), report);
            PatchSourceFloat(minigamePath, "_failSpeedDifficultyScale", GetFloat(data, "manual_minigame_fail_speed_difficulty_scale"), report);
            PatchSourceFloat(minigamePath, "_failStartDelay", GetFloat(data, "manual_minigame_fail_start_delay"), report);
            PatchSourceInt(minigamePath, "_baseBarrierClicks", GetInt(data, "manual_minigame_base_barrier_clicks"), report);

            string offlinePath = "Assets/01_Scripts/03_YWJ/OfflineReward/OfflineRewardCaculator.cs";
            PatchSourceInt(offlinePath, "_baseMaxOfflineHours", GetInt(data, "offline_reward_base_cap_hours"), report);
            PatchSourceInt(offlinePath, "_maxOfflineHours", GetInt(data, "offline_reward_base_cap_hours"), report);
            PatchSourceInt(offlinePath, "_offlineRewardFishPerHour", GetInt(data, "offline_reward_fish_per_hour"), report);

            string gachaPath = "Assets/01_Scripts/02_PSY/Gacha/GachaSystem.cs";
            PatchSourceFloat(gachaPath, "_rRate", GetGachaGradeRate(data, "R"), report);
            PatchSourceFloat(gachaPath, "_srRate", GetGachaGradeRate(data, "SR"), report);
            PatchSourceFloat(gachaPath, "_ssrRate", GetGachaGradeRate(data, "SSR"), report);
            PatchSourceFloat(gachaPath, "_materialRate", GetGachaRewardRate(data, "Materials"), report);
            PatchSourceFloat(gachaPath, "_crewFregmentsRate", GetGachaRewardRate(data, "CrewFragment"), report);
            PatchSourceFloat(gachaPath, "_crewRate", GetGachaRewardRate(data, "Crew"), report);

            string rewardPath = "Assets/01_Scripts/02_PSY/Gacha/RewardManager.cs";
            PatchSourceSwitchInt(rewardPath, "R", GetDuplicateAmount(data, "R"), report);
            PatchSourceSwitchInt(rewardPath, "SR", GetDuplicateAmount(data, "SR"), report);
            PatchSourceSwitchInt(rewardPath, "SSR", GetDuplicateAmount(data, "SSR"), report);
        }

        private static void ApplyScenes(BalanceData data, BalanceSyncReport report)
        {
            string scenePath = MainScenePath;
            if (!File.Exists(ToFullPath(scenePath)))
            {
                report.Errors.Add("Main scene missing: " + scenePath);
                return;
            }

            bool openedForApply = false;
            Scene scene = FindOpenScene(scenePath);
            if (!scene.IsValid())
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                openedForApply = true;
            }

            int changes = 0;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                AutoFishingController[] autoControllers = roots[r].GetComponentsInChildren<AutoFishingController>(true);
                for (int j = 0; j < autoControllers.Length; j++)
                {
                    if (ApplyAutoFishing(autoControllers[j], data))
                    {
                        changes++;
                    }
                }

                OfflineRewardCaculator[] offlineCalculators = roots[r].GetComponentsInChildren<OfflineRewardCaculator>(true);
                for (int j = 0; j < offlineCalculators.Length; j++)
                {
                    if (ApplyOfflineReward(offlineCalculators[j], data))
                    {
                        changes++;
                    }
                }

                global::GachaSystem[] gachaSystems = roots[r].GetComponentsInChildren<global::GachaSystem>(true);
                for (int j = 0; j < gachaSystems.Length; j++)
                {
                    if (ApplyGachaSystem(gachaSystems[j], data))
                    {
                        changes++;
                    }
                }
            }

            if (changes > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                report.Changed.Add(scenePath + " changed components=" + changes);
            }
            else
            {
                report.Checked.Add(scenePath + " no numeric scene changes");
            }

            report.Checked.Add("Scene apply allowlist: only " + MainScenePath);

            if (openedForApply)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static bool ApplyAutoFishing(AutoFishingController controller, BalanceData data)
        {
            SerializedObject so = new SerializedObject(controller);
            bool changed = false;
            changed |= SetFloat(so, "_baseReward", GetFloat(data, "fishing_manual_base_reward"));
            changed |= SetFloat(so, "_autoPenalty", GetFloat(data, "fishing_auto_penalty"));
            changed |= SetFloat(so, "_commonMultiCatchChance", GetFloat(data, "manual_multi_catch_common_chance"));
            changed |= SetFloat(so, "_rareMultiCatchChance", GetFloat(data, "manual_multi_catch_rare_chance"));
            changed |= SetFloat(so, "_epicMultiCatchChance", GetFloat(data, "manual_multi_catch_epic_chance"));
            changed |= SetFloat(so, "_legendaryMultiCatchChance", GetFloat(data, "manual_multi_catch_legendary_chance"));
            changed |= SetInt(so, "_multiCatchCountMin", GetInt(data, "manual_multi_catch_count_min"));
            changed |= SetInt(so, "_multiCatchCountMax", GetInt(data, "manual_multi_catch_count_max"));
            changed |= SetFloat(so, "_bossMultiCatchChance", GetFloat(data, "manual_boss_multi_catch_chance"));
            changed |= SetInt(so, "_bossMultiCatchMultiplier", GetInt(data, "manual_boss_multi_catch_multiplier"));
            if (changed)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(controller);
            }

            return changed;
        }

        private static bool ApplyOfflineReward(OfflineRewardCaculator calculator, BalanceData data)
        {
            SerializedObject so = new SerializedObject(calculator);
            bool changed = false;
            changed |= SetInt(so, "_baseMaxOfflineHours", GetInt(data, "offline_reward_base_cap_hours"));
            changed |= SetInt(so, "_maxOfflineHours", GetInt(data, "offline_reward_base_cap_hours"));
            changed |= SetInt(so, "_offlineRewardFishPerHour", GetInt(data, "offline_reward_fish_per_hour"));
            if (changed)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(calculator);
            }

            return changed;
        }

        private static bool ApplyGachaSystem(global::GachaSystem gacha, BalanceData data)
        {
            SerializedObject so = new SerializedObject(gacha);
            bool changed = false;
            changed |= SetFloat(so, "_rRate", GetGachaGradeRate(data, "R"));
            changed |= SetFloat(so, "_srRate", GetGachaGradeRate(data, "SR"));
            changed |= SetFloat(so, "_ssrRate", GetGachaGradeRate(data, "SSR"));
            changed |= SetFloat(so, "_materialRate", GetGachaRewardRate(data, "Materials"));
            changed |= SetFloat(so, "_crewFregmentsRate", GetGachaRewardRate(data, "CrewFragment"));
            changed |= SetFloat(so, "_crewRate", GetGachaRewardRate(data, "Crew"));
            if (changed)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(gacha);
            }

            return changed;
        }

        private static void ApplyManualMinigamePrefabs(BalanceData data, BalanceSyncReport report)
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder });
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    continue;
                }

                ManualFishingMinigame[] minigames = prefab.GetComponentsInChildren<ManualFishingMinigame>(true);
                int changes = 0;
                for (int j = 0; j < minigames.Length; j++)
                {
                    SerializedObject so = new SerializedObject(minigames[j]);
                    bool changed = false;
                    changed |= SetFloat(so, "_fillSpeed", GetFloat(data, "manual_minigame_fill_speed"));
                    changed |= SetFloat(so, "_drainSpeed", GetFloat(data, "manual_minigame_drain_speed"));
                    changed |= SetFloat(so, "_failSpeedBase", GetFloat(data, "manual_minigame_fail_speed_base"));
                    changed |= SetFloat(so, "_failSpeedDifficultyScale", GetFloat(data, "manual_minigame_fail_speed_difficulty_scale"));
                    changed |= SetFloat(so, "_failStartDelay", GetFloat(data, "manual_minigame_fail_start_delay"));
                    changed |= SetInt(so, "_baseBarrierClicks", GetInt(data, "manual_minigame_base_barrier_clicks"));
                    if (changed)
                    {
                        so.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(minigames[j]);
                        changes++;
                    }
                }

                if (changes > 0)
                {
                    EditorUtility.SetDirty(prefab);
                    report.Changed.Add(prefabPath + " minigames=" + changes);
                }
            }
        }

        private static void ApplyStageFishWeights(BalanceData data, BalanceSyncReport report)
        {
            Dictionary<string, StageData> stages = LoadStageDataById(report);
            int changed = 0;
            for (int i = 0; i < data.StageFishWeights.Rows.Count; i++)
            {
                CsvRow row = data.StageFishWeights.Rows[i];
                if (!ParseBool(row.Get("isEnabled"), true) ||
                    !string.Equals(row.Get("applyMode"), "applyExisting", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!stages.TryGetValue(row.Get("stageId"), out StageData stage))
                {
                    continue;
                }

                SerializedObject so = new SerializedObject(stage);
                SerializedProperty entries = so.FindProperty("_fishEntries");
                if (entries == null || !entries.isArray)
                {
                    continue;
                }

                bool stageChanged = false;
                for (int j = 0; j < entries.arraySize; j++)
                {
                    SerializedProperty entry = entries.GetArrayElementAtIndex(j);
                    SerializedProperty fishProperty = entry.FindPropertyRelative("fishData");
                    FishData fish = fishProperty != null ? fishProperty.objectReferenceValue as FishData : null;
                    if (fish == null || !string.Equals(fish.FishId, row.Get("fishId"), StringComparison.Ordinal))
                    {
                        continue;
                    }

                    SerializedProperty weightProperty = entry.FindPropertyRelative("spawnWeight");
                    float nextWeight = ParseFloat(row.Get("spawnWeight"));
                    if (weightProperty != null && !NearlyEqual(weightProperty.floatValue, nextWeight))
                    {
                        weightProperty.floatValue = nextWeight;
                        stageChanged = true;
                    }
                }

                if (stageChanged)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(stage);
                    changed++;
                }
            }

            if (changed > 0)
            {
                report.Changed.Add("StageData fish weights changed stages=" + changed);
            }
        }

        private static Dictionary<string, StageData> LoadStageDataById(BalanceSyncReport report)
        {
            Dictionary<string, StageData> stages = new Dictionary<string, StageData>(StringComparer.Ordinal);
            string[] guids = AssetDatabase.FindAssets("t:StageData", new[] { StageDataFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                StageData stage = AssetDatabase.LoadAssetAtPath<StageData>(path);
                if (stage == null || string.IsNullOrEmpty(stage.StageId))
                {
                    continue;
                }

                if (!stages.ContainsKey(stage.StageId))
                {
                    stages.Add(stage.StageId, stage);
                }
                else
                {
                    report.Errors.Add("Duplicate StageData stageId: " + stage.StageId);
                }
            }

            return stages;
        }

        private static bool StageContainsFish(StageData stage, string fishId)
        {
            if (stage.FishEntries == null)
            {
                return false;
            }

            for (int i = 0; i < stage.FishEntries.Length; i++)
            {
                FishData fish = stage.FishEntries[i].fishData;
                if (fish != null && string.Equals(fish.FishId, fishId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static Scene FindOpenScene(string scenePath)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && string.Equals(scene.path, scenePath, StringComparison.OrdinalIgnoreCase))
                {
                    return scene;
                }
            }

            return default;
        }

        private static bool ConfirmMainSceneSaveIfDirty()
        {
            Scene mainScene = FindOpenScene(MainScenePath);
            if (!mainScene.IsValid() || !mainScene.isDirty)
            {
                return true;
            }

            string message =
                "00_MainScene.unity에 저장되지 않은 변경이 있습니다.\n\n" +
                "밸런스 적용은 MainScene만 대상으로 하며, 다른 열린 씬은 저장하지 않습니다.";
            if (!EditorUtility.DisplayDialog("Fisher 밸런스 적용", message, "MainScene 저장 후 적용", "취소"))
            {
                return false;
            }

            return EditorSceneManager.SaveScene(mainScene);
        }

        private static void PatchSourceFloat(string assetPath, string fieldName, float value, BalanceSyncReport report)
        {
            string pattern = @"(private\s+float\s+" + Regex.Escape(fieldName) + @"\s*=\s*)[-+]?\d+(?:\.\d+)?f";
            PatchSource(assetPath, pattern, "${1}" + FormatFloat(value) + "f", fieldName, report);
        }

        private static void PatchSourceInt(string assetPath, string fieldName, int value, BalanceSyncReport report)
        {
            string pattern = @"(private\s+int\s+" + Regex.Escape(fieldName) + @"\s*=\s*)[-+]?\d+";
            PatchSource(assetPath, pattern, "${1}" + value.ToString(CultureInfo.InvariantCulture), fieldName, report);
        }

        private static void PatchSourceSwitchInt(string assetPath, string grade, int value, BalanceSyncReport report)
        {
            string pattern = @"(CrewGrade\." + Regex.Escape(grade) + @"\s*=>\s*)[-+]?\d+";
            PatchSource(assetPath, pattern, "${1}" + value.ToString(CultureInfo.InvariantCulture), "CrewGrade." + grade, report);
        }

        private static void PatchSource(string assetPath, string pattern, string replacement, string label, BalanceSyncReport report)
        {
            string fullPath = ToFullPath(assetPath);
            if (!File.Exists(fullPath))
            {
                report.Errors.Add("Source missing: " + assetPath);
                return;
            }

            string before = File.ReadAllText(fullPath, Encoding.UTF8);
            Regex regex = new Regex(pattern);
            string after = regex.Replace(before, replacement, 1);
            if (string.Equals(before, after, StringComparison.Ordinal))
            {
                if (!regex.IsMatch(before))
                {
                    report.Warnings.Add(assetPath + ": source pattern not found for " + label);
                }

                return;
            }

            File.WriteAllText(fullPath, after, Encoding.UTF8);
            report.Changed.Add(assetPath + " source default " + label);
            AssetDatabase.ImportAsset(assetPath);
        }

        private static bool SetFloat(SerializedObject so, string propertyName, float value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property == null)
            {
                return false;
            }

            if (NearlyEqual(property.floatValue, value))
            {
                return false;
            }

            property.floatValue = value;
            return true;
        }

        private static bool SetInt(SerializedObject so, string propertyName, int value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property == null)
            {
                return false;
            }

            if (property.intValue == value)
            {
                return false;
            }

            property.intValue = value;
            return true;
        }

        private static void RequireHeaders(CsvTable table, string fileName, BalanceSyncReport report, params string[] headers)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                if (!table.HeaderSet.Contains(headers[i]))
                {
                    report.Errors.Add(fileName + ": missing header " + headers[i]);
                }
            }
        }

        private static void RequireFloat(BalanceData data, string key, float min, float max, BalanceSyncReport report)
        {
            if (!TryGetFloat(data, key, out float value))
            {
                report.Errors.Add(BalanceParamsCsv + ": invalid float key " + key);
                return;
            }

            if (value < min || value > max)
            {
                report.Errors.Add(BalanceParamsCsv + ": " + key + " out of range " + FormatFloat(value));
            }
        }

        private static void RequireInt(BalanceData data, string key, int min, int max, BalanceSyncReport report)
        {
            if (!TryGetInt(data, key, out int value))
            {
                report.Errors.Add(BalanceParamsCsv + ": invalid int key " + key);
                return;
            }

            if (value < min || value > max)
            {
                report.Errors.Add(BalanceParamsCsv + ": " + key + " out of range " + value);
            }
        }

        private static void RequireGachaRate(BalanceData data, string bannerId, string rollLayer, string grade, BalanceSyncReport report)
        {
            if (!TryFindGachaGradeRate(data, bannerId, rollLayer, grade, out _))
            {
                report.Errors.Add(GachaRateTiersCsv + ": missing grade rate " + bannerId + "/" + grade);
            }
        }

        private static void RequireGachaRewardRate(BalanceData data, string bannerId, string rollLayer, string rewardType, BalanceSyncReport report)
        {
            if (!TryFindGachaRewardRate(data, bannerId, rollLayer, rewardType, out _))
            {
                report.Errors.Add(GachaRateTiersCsv + ": missing reward type rate " + bannerId + "/" + rewardType);
            }
        }

        private static void RequireDuplicateGrade(BalanceData data, string grade, BalanceSyncReport report)
        {
            if (!TryFindDuplicateAmount(data, grade, out int amount))
            {
                report.Errors.Add(GachaDuplicateRewardsCsv + ": missing duplicate grade " + grade);
                return;
            }

            if (amount < 0 || amount > 100000)
            {
                report.Errors.Add(GachaDuplicateRewardsCsv + ": duplicate amount out of range for " + grade);
            }
        }

        private static float GetFloat(BalanceData data, string key)
        {
            TryGetFloat(data, key, out float value);
            return value;
        }

        private static int GetInt(BalanceData data, string key)
        {
            TryGetInt(data, key, out int value);
            return value;
        }

        private static bool TryGetFloat(BalanceData data, string key, out float value)
        {
            value = 0f;
            return data.ParamRows.TryGetValue(key, out CsvRow row) &&
                   TryParseFloat(row.Get("value"), out value);
        }

        private static bool TryGetInt(BalanceData data, string key, out int value)
        {
            value = 0;
            return data.ParamRows.TryGetValue(key, out CsvRow row) &&
                   int.TryParse(row.Get("value"), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static float GetGachaGradeRate(BalanceData data, string grade)
        {
            TryFindGachaGradeRate(data, "premium_crew", "crew_grade", grade, out float value);
            return value;
        }

        private static float GetGachaRewardRate(BalanceData data, string rewardType)
        {
            TryFindGachaRewardRate(data, "basic_material", "reward_type", rewardType, out float value);
            return value;
        }

        private static int GetDuplicateAmount(BalanceData data, string grade)
        {
            TryFindDuplicateAmount(data, grade, out int value);
            return value;
        }

        private static bool TryFindGachaGradeRate(BalanceData data, string bannerId, string rollLayer, string grade, out float value)
        {
            value = 0f;
            for (int i = 0; i < data.GachaRates.Rows.Count; i++)
            {
                CsvRow row = data.GachaRates.Rows[i];
                if (ParseBool(row.Get("isEnabled"), true) &&
                    string.Equals(row.Get("bannerId"), bannerId, StringComparison.Ordinal) &&
                    string.Equals(row.Get("rollLayer"), rollLayer, StringComparison.Ordinal) &&
                    string.Equals(row.Get("grade"), grade, StringComparison.Ordinal))
                {
                    return TryParseFloat(row.Get("ratePct"), out value);
                }
            }

            return false;
        }

        private static bool TryFindGachaRewardRate(BalanceData data, string bannerId, string rollLayer, string rewardType, out float value)
        {
            value = 0f;
            for (int i = 0; i < data.GachaRates.Rows.Count; i++)
            {
                CsvRow row = data.GachaRates.Rows[i];
                if (ParseBool(row.Get("isEnabled"), true) &&
                    string.Equals(row.Get("bannerId"), bannerId, StringComparison.Ordinal) &&
                    string.Equals(row.Get("rollLayer"), rollLayer, StringComparison.Ordinal) &&
                    string.Equals(row.Get("rewardType"), rewardType, StringComparison.Ordinal))
                {
                    return TryParseFloat(row.Get("ratePct"), out value);
                }
            }

            return false;
        }

        private static bool TryFindDuplicateAmount(BalanceData data, string grade, out int amount)
        {
            amount = 0;
            for (int i = 0; i < data.GachaDuplicateRewards.Rows.Count; i++)
            {
                CsvRow row = data.GachaDuplicateRewards.Rows[i];
                if (ParseBool(row.Get("isEnabled"), true) &&
                    string.Equals(row.Get("grade"), grade, StringComparison.Ordinal))
                {
                    return int.TryParse(row.Get("duplicateFragmentAmount"), NumberStyles.Integer, CultureInfo.InvariantCulture, out amount);
                }
            }

            return false;
        }

        private static bool TryParseFloat(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static float ParseFloat(string text)
        {
            TryParseFloat(text, out float value);
            return value;
        }

        private static bool ParseBool(string text, bool defaultValue)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return defaultValue;
            }

            return string.Equals(text, "TRUE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(text, "1", StringComparison.Ordinal);
        }

        private static bool NearlyEqual(float left, float right)
        {
            return Math.Abs(left - right) < 0.00001f;
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static string NormalizeFullPath(string path)
        {
            return Path.GetFullPath(path);
        }

        private static string QuoteArg(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static void OpenPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                (!File.Exists(path) && !Directory.Exists(path)))
            {
                Debug.LogWarning("[Fisher Balance Validate] 열 수 없는 경로입니다: " + path);
                return;
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }

        private static string LimitText(string text, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "(none)";
            }

            string trimmed = text.Trim();
            if (trimmed.Length <= maxLength)
            {
                return trimmed;
            }

            return trimmed.Substring(0, maxLength) + "\n... (truncated)";
        }

        private static void EnsureFolder(string assetFolder)
        {
            string fullPath = ToFullPath(assetFolder);
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
            }
        }

        private static string CombineAssetPath(string folder, string fileName)
        {
            return folder.TrimEnd('/') + "/" + fileName;
        }

        private static string ToFullPath(string assetPath)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), assetPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
        }

        private sealed class BalanceData
        {
            public CsvTable Params;
            public CsvTable GachaRates;
            public CsvTable GachaDuplicateRewards;
            public CsvTable StageFishWeights;
            public readonly Dictionary<string, CsvRow> ParamRows = new Dictionary<string, CsvRow>(StringComparer.Ordinal);
        }

        private sealed class BalanceSyncReport
        {
            private readonly string title;
            public readonly List<string> Checked = new List<string>();
            public readonly List<string> Changed = new List<string>();
            public readonly List<string> Warnings = new List<string>();
            public readonly List<string> Errors = new List<string>();

            public BalanceSyncReport(string title)
            {
                this.title = title;
            }

            public bool Success => Errors.Count == 0;

            public string BuildMessage()
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine("[Fisher Balance Sync] " + title);
                builder.AppendLine("Result: " + (Success ? "OK" : "FAILED"));
                builder.AppendLine("Checked: " + Checked.Count);
                for (int i = 0; i < Checked.Count; i++)
                {
                    builder.AppendLine("- " + Checked[i]);
                }

                builder.AppendLine("Changed: " + Changed.Count);
                for (int i = 0; i < Changed.Count; i++)
                {
                    builder.AppendLine("- " + Changed[i]);
                }

                builder.AppendLine("Warnings: " + Warnings.Count);
                for (int i = 0; i < Warnings.Count; i++)
                {
                    builder.AppendLine("- " + Warnings[i]);
                }

                builder.AppendLine("Errors: " + Errors.Count);
                for (int i = 0; i < Errors.Count; i++)
                {
                    builder.AppendLine("- " + Errors[i]);
                }

                return builder.ToString();
            }
        }

        private sealed class Project3ValidatorReport
        {
            private const int ConsoleOutputLimit = 6000;
            private const int DialogOutputLimit = 1400;

            public readonly string Executable;
            public readonly string Arguments;
            public readonly int ExitCode;
            public readonly string Stdout;
            public readonly string Stderr;
            public readonly string ResultsDirectory;

            public Project3ValidatorReport(
                string executable,
                string arguments,
                int exitCode,
                string stdout,
                string stderr,
                string resultsDirectory)
            {
                Executable = executable;
                Arguments = arguments;
                ExitCode = exitCode;
                Stdout = stdout;
                Stderr = stderr;
                ResultsDirectory = resultsDirectory;
            }

            public bool Success => ExitCode == 0;
            public string ResultsCsvPath => Path.Combine(ResultsDirectory, "generated_csv_validator_results.csv");
            public string ResultsMarkdownPath => Path.Combine(ResultsDirectory, "generated_csv_validator_results.md");

            public string BuildMessage()
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine("[Fisher Balance Sync] Project3 Generated CSV Validator");
                builder.AppendLine("Result: " + (Success ? "OK" : "FAILED"));
                builder.AppendLine("ExitCode: " + ExitCode);
                builder.AppendLine("Command: " + Executable + " " + Arguments);
                builder.AppendLine("Changed: 0 Unity files. Validator only writes Project3 report files.");
                builder.AppendLine("Results CSV: " + ResultsCsvPath);
                builder.AppendLine("Results MD: " + ResultsMarkdownPath);
                builder.AppendLine("stdout:");
                builder.AppendLine(LimitText(Stdout, ConsoleOutputLimit));
                if (!string.IsNullOrWhiteSpace(Stderr))
                {
                    builder.AppendLine("stderr:");
                    builder.AppendLine(LimitText(Stderr, ConsoleOutputLimit));
                }

                return builder.ToString();
            }

            public string BuildDialogMessage()
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine("Result: " + (Success ? "OK" : "FAILED"));
                builder.AppendLine("ExitCode: " + ExitCode);
                builder.AppendLine();
                builder.AppendLine(LimitText(Stdout, DialogOutputLimit));
                if (!string.IsNullOrWhiteSpace(Stderr))
                {
                    builder.AppendLine();
                    builder.AppendLine("stderr:");
                    builder.AppendLine(LimitText(Stderr, DialogOutputLimit));
                }

                builder.AppendLine();
                builder.AppendLine("Results:");
                builder.AppendLine(ResultsMarkdownPath);
                builder.AppendLine(ResultsCsvPath);
                return builder.ToString();
            }
        }

        private sealed class CsvTable
        {
            public readonly List<string> Headers = new List<string>();
            public readonly HashSet<string> HeaderSet = new HashSet<string>(StringComparer.Ordinal);
            public readonly List<CsvRow> Rows = new List<CsvRow>();

            public static CsvTable Empty(string fileName)
            {
                return new CsvTable();
            }

            public static CsvTable Parse(string fileName, string text)
            {
                CsvTable table = new CsvTable();
                string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
                int index = 0;
                while (index < lines.Length && string.IsNullOrWhiteSpace(lines[index]))
                {
                    index++;
                }

                if (index >= lines.Length)
                {
                    return table;
                }

                table.Headers.AddRange(ParseLine(lines[index]));
                for (int i = 0; i < table.Headers.Count; i++)
                {
                    table.HeaderSet.Add(table.Headers[i]);
                }

                for (int i = index + 1; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i]))
                    {
                        continue;
                    }

                    List<string> values = ParseLine(lines[i]);
                    table.Rows.Add(new CsvRow(table.Headers, values));
                }

                return table;
            }

            private static List<string> ParseLine(string line)
            {
                List<string> values = new List<string>();
                StringBuilder current = new StringBuilder();
                bool inQuotes = false;
                for (int i = 0; i < line.Length; i++)
                {
                    char c = line[i];
                    if (c == '"')
                    {
                        if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                        {
                            current.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = !inQuotes;
                        }
                    }
                    else if (c == ',' && !inQuotes)
                    {
                        values.Add(current.ToString());
                        current.Length = 0;
                    }
                    else
                    {
                        current.Append(c);
                    }
                }

                values.Add(current.ToString());
                return values;
            }
        }

        private sealed class CsvRow
        {
            private readonly Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);

            public CsvRow(List<string> headers, List<string> rowValues)
            {
                for (int i = 0; i < headers.Count; i++)
                {
                    string value = i < rowValues.Count ? rowValues[i] : string.Empty;
                    values[headers[i]] = value.Trim();
                }
            }

            public string Get(string key)
            {
                return values.TryGetValue(key, out string value) ? value : string.Empty;
            }
        }
    }
}
