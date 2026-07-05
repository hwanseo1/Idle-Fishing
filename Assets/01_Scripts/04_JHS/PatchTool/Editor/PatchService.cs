using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace JHS.PatchTool
{
    // 패치 오케스트레이터: 소스 로드 → JSON 검증(푸시 전) → Publisher로 Title Data에 쓴다.
    // 소스 포맷(ICostTableSource)·전송 수단(ITitleDataPublisher)을 주입받음 = 둘 다 모름(DIP).
    // publisher가 Mock이면 자동으로 dry-run이 된다.
    public sealed class PatchService
    {
        private readonly ITitleDataPublisher _publisher;
        private readonly Func<string, string> _schemaValidator;   // null이면 구문 검증만. 스키마별 검증기 주입(OCP).

        public PatchService(ITitleDataPublisher publisher, Func<string, string> schemaValidator = null)
        {
            _publisher = publisher;
            _schemaValidator = schemaValidator;
        }

        // 소스를 읽어 검증(구문 + 선택적 스키마) 후 키에 푸시.
        public async Task<PublishResult> RunAsync(string key, ICostTableSource source)
        {
            if (string.IsNullOrEmpty(key)) return PublishResult.Fail("키가 비어 있음");
            if (source == null) return PublishResult.Fail("소스가 null");

            string json;
            try { json = source.LoadJson(); }
            catch (Exception e) { return PublishResult.Fail($"소스 로드 실패: {e.Message}"); }

            // 푸시 전 검증(자기검토 §12-3·4) — 깨진/스키마 안 맞는 JSON을 라이브에 올리지 않는다.
            string err = ValidateJson(json);                              // 1) 구문
            if (err != null) return PublishResult.Fail($"JSON 구문 오류: {err}");
            if (_schemaValidator != null)                                 // 2) 스키마(주입 시)
            {
                string serr = _schemaValidator(json);
                if (serr != null) return PublishResult.Fail($"스키마 검증 실패: {serr}");
            }

            Debug.Log($"[PatchTool] '{key}' 검증 통과 ({json.Length}자) → 푸시 시도");
            return await _publisher.PublishAsync(key, json);
        }

        // 유효한 JSON이면 null, 아니면 사유 문자열.
        public static string ValidateJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return "빈 문자열";
            try { JToken.Parse(json); return null; }
            catch (Exception e) { return e.Message; }
        }
    }
}
