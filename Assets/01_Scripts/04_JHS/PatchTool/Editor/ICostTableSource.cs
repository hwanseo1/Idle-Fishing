namespace JHS.PatchTool
{
    // 선호가 준 소스(파일 등)를 Title Data에 올릴 JSON 문자열로 변환하는 규격.
    // 포맷 의존부를 여기 한 곳에 격리 → JSON/SO 등 포맷이 바뀌어도 PatchService는 무수정(DIP/OCP).
    public interface ICostTableSource
    {
        // 소스를 읽어 JSON 문자열로 반환. 실패 시 예외(호출측 PatchService가 흡수).
        string LoadJson();
    }
}
