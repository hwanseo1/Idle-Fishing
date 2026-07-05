using System;

namespace JHS.PatchTool
{
    // 패치 대상 1개 = 표시명 + Title Data 키 + 스키마 검증기. 대상마다 키·검증기를 묶어둔다.
    // 검증기가 null이면 구문(JSON 유효성)만 검사. 새 대상 추가 = PatchTargets에 한 줄(OCP).
    public sealed class PatchTarget
    {
        public readonly string DisplayName;
        public readonly string DefaultKey;
        public readonly bool EditableKey;                  // true면 창에서 키 직접 수정 허용(임의 키 모드)
        public readonly Func<string, string> SchemaValidator;  // null = 구문만

        public PatchTarget(string displayName, string defaultKey, bool editableKey, Func<string, string> schemaValidator)
        {
            DisplayName = displayName;
            DefaultKey = defaultKey;
            EditableKey = editableKey;
            SchemaValidator = schemaValidator;
        }
    }
}
