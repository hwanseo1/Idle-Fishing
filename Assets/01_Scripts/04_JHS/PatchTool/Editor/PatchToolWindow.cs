using System;
using UnityEditor;
using UnityEngine;
using PlayFab;

namespace JHS.PatchTool
{
    // Tools 메뉴에서 여는 패치 도구 창. 소스 JSON을 Title Data 키에 푸시(or dry-run).
    // Editor 폴더 = 빌드 미포함. Secret Key는 EditorPrefs에만(소스/빌드 노출 X).
    public sealed class PatchToolWindow : EditorWindow
    {
        private string _titleId = "";
        private string _secretKey = "";
        private string _sourcePath = "";
        private int _targetIndex;                             // 선택한 패치 대상(기본 [0]=대상 미선택, 무해)
        private string _dataKey = "";                         // OnEnable에서 Target.DefaultKey로 설정
        private bool _dryRun = true;                          // 기본 안전: dry-run
        private string _log = "";
        private bool _busy;

        private PatchTarget Target => PatchTargets.All[_targetIndex];

        [MenuItem("Tools/JHS/패치 도구 (Title Data)")]
        public static void Open() => GetWindow<PatchToolWindow>("JHS 패치 도구");

        private void OnEnable()
        {
            _secretKey = PatchSecretStore.Load();
            if (string.IsNullOrEmpty(_titleId)) _titleId = PlayFabSettings.staticSettings.TitleId;
            _dataKey = Target.DefaultKey;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("PlayFab Title Data 패치", EditorStyles.boldLabel);

            _titleId = EditorGUILayout.TextField("Title ID", _titleId);

            EditorGUILayout.BeginHorizontal();
            _secretKey = EditorGUILayout.PasswordField("Secret Key", _secretKey);
            if (GUILayout.Button("저장", GUILayout.Width(45))) { PatchSecretStore.Save(_secretKey); Log("Secret Key 저장(EditorPrefs)"); }
            if (GUILayout.Button("삭제", GUILayout.Width(45))) { PatchSecretStore.Clear(); _secretKey = ""; Log("Secret Key 삭제"); }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("Secret Key는 이 PC EditorPrefs에만 저장(소스/빌드 미포함).", MessageType.None);

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            _sourcePath = EditorGUILayout.TextField("소스 JSON", _sourcePath);
            if (GUILayout.Button("찾기", GUILayout.Width(45)))
            {
                string p = EditorUtility.OpenFilePanel("소스 JSON 선택", Application.dataPath, "json");
                if (!string.IsNullOrEmpty(p)) _sourcePath = p;
            }
            EditorGUILayout.EndHorizontal();

            // 패치 대상 — 선택하면 키·검증기가 자동 세팅(고정 대상은 키 잠금, 임의 키 모드만 편집 허용)
            int newIndex = EditorGUILayout.Popup("패치 대상", _targetIndex, PatchTargets.DisplayNames());
            if (newIndex != _targetIndex) { _targetIndex = newIndex; _dataKey = Target.DefaultKey; }

            using (new EditorGUI.DisabledScope(!Target.EditableKey))
                _dataKey = EditorGUILayout.TextField("Title Data 키", Target.EditableKey ? _dataKey : Target.DefaultKey);

            EditorGUILayout.LabelField(" ", Target.SchemaValidator != null ? "검증: 구문 + 스키마" : "검증: 구문만");
            _dryRun = EditorGUILayout.Toggle("Dry-run(미리보기)", _dryRun);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_busy))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("검증만")) Validate();
                if (GUILayout.Button(_dryRun ? "Dry-run 실행" : "실제 푸시")) Push();
                EditorGUILayout.EndHorizontal();
                if (GUILayout.Button("현재 라이브값 읽기")) Read();   // Phase2: 푸시 전후 라이브 값 확인(비파괴)
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("로그", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(string.IsNullOrEmpty(_log) ? "(없음)" : _log, MessageType.None);
        }

        private void Validate()
        {
            if (!TryLoadJson(out string json)) return;
            string err = PatchService.ValidateJson(json);                       // 1) 구문
            if (err == null && Target.SchemaValidator != null)                  // 2) 스키마(대상에 있으면)
                err = Target.SchemaValidator(json);
            string mode = Target.SchemaValidator != null ? "구문+스키마" : "구문만";
            Log(err == null ? $"검증 통과 ({json.Length}자, {mode})" : $"검증 실패: {err}");
        }

        private bool TryLoadJson(out string json)
        {
            json = null;
            try { json = new JsonFileCostSource(_sourcePath).LoadJson(); return true; }
            catch (Exception e) { Log($"소스 로드 실패: {e.Message}"); return false; }
        }

        private async void Push()
        {
            if (string.IsNullOrEmpty(_dataKey)) { Log("키가 비어 있음"); return; }

            ITitleDataPublisher publisher;
            if (_dryRun)
            {
                publisher = new MockTitleDataPublisher();
            }
            else
            {
                if (!ConfirmRealPush()) return;
                if (!ApplyPlayFabSettings()) return;
                publisher = new PlayFabTitleDataPublisher();
            }

            _busy = true; Log("실행 중…"); Repaint();
            var service = new PatchService(publisher, Target.SchemaValidator);   // 선택 대상의 검증기 주입
            PublishResult result = await service.RunAsync(_dataKey, new JsonFileCostSource(_sourcePath));
            _busy = false;
            Log(result.Success ? $"성공: {result.Message}" : $"실패: {result.Message}");
            Repaint();
        }

        // Phase2: 현재 라이브 Title Data 값 읽기(비파괴). Admin API라 Secret Key 필요(푸시와 동일 주입).
        private async void Read()
        {
            if (string.IsNullOrEmpty(_dataKey)) { Log("키가 비어 있음 — 읽을 대상을 선택/입력하세요"); return; }
            if (!ApplyPlayFabSettings()) return;

            _busy = true; Log("읽는 중…"); Repaint();
            ReadResult result = await new PlayFabTitleDataReader().ReadAsync(_dataKey);
            _busy = false;
            if (!result.Success) Log($"읽기 실패: {result.Message}");
            else if (result.Value == null) Log($"키 '{_dataKey}' 없음(미설정)");
            else Log($"현재값['{_dataKey}'] ({result.Value.Length}자):\n{Preview(result.Value)}");
            Repaint();
        }

        // 긴 JSON은 앞부분만 잘라 표시(로그/창 오염 방지).
        private static string Preview(string s)
            => string.IsNullOrEmpty(s) ? "(빈 값)"
             : s.Length <= 300 ? s : s.Substring(0, 300) + $"… (총 {s.Length}자)";

        private bool ConfirmRealPush()
            => EditorUtility.DisplayDialog("실제 푸시 확인",
                $"Title Data 키 '{_dataKey}' 를 실제로 덮어씁니다.\n(이 타이틀의 모든 클라가 다음 부팅 때 읽음)\n계속할까요?",
                "푸시", "취소");

        // 실제 푸시 직전에만 Secret Key/TitleId를 PlayFab에 주입(메모리에만, 디스크 저장 X).
        private bool ApplyPlayFabSettings()
        {
            if (string.IsNullOrEmpty(_secretKey)) { Log("Secret Key 없음 — 입력 후 저장 필요"); return false; }
            if (!string.IsNullOrEmpty(_titleId)) PlayFabSettings.staticSettings.TitleId = _titleId;
            PlayFabSettings.staticSettings.DeveloperSecretKey = _secretKey;
            return true;
        }

        private void Log(string msg) { _log = msg; Debug.Log($"[PatchTool] {msg}"); }
    }
}
