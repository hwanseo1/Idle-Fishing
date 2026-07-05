using System.IO;

namespace JHS.PatchTool
{
    // JSON 파일을 그대로 읽어 문자열로 반환. 포맷=JSON 확정이라 변환 단계 없음(선호가 JSON 파일을 주는 경로).
    // 나중에 선호가 SO로 주면 ScriptableObjectCostSource : ICostTableSource 만 추가하면 됨(OCP).
    public sealed class JsonFileCostSource : ICostTableSource
    {
        private readonly string _path;

        public JsonFileCostSource(string path) => _path = path;

        public string LoadJson()
        {
            if (string.IsNullOrEmpty(_path) || !File.Exists(_path))
                throw new FileNotFoundException($"소스 JSON 파일을 찾을 수 없음: {_path}");
            return File.ReadAllText(_path);
        }
    }
}
