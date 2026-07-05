using System.Threading.Tasks;
using UnityEngine;

namespace JHS.Content
{
    // 공통 종류 ID로 그 ID의 실제 데이터(정의 SO)를 불러오는 통로.
    // 소비자(차선호 UI 등)는 이 인터페이스에만 의존한다 — 뒤 구현이 인앱 레지스트리든 Addressables든 모름(DIP).
    //   · 지금: 인앱 동봉 레지스트리(Local).
    //   · 나중(P1): 같은 인터페이스로 Addressables Remote(CCD) 교체 → 소비자 코드 무변경.
    // 한 ID = 한 진짜 데이터(equip_rod = 낚싯대 데이터). 아이콘 등은 그 데이터 안에 있다.
    // <T>는 받을 데이터 타입(소비자가 아는 정의 SO 타입)일 뿐, ID를 에셋 타입으로 쪼개는 게 아니다.
    public interface IAssetProvider
    {
        // ID로 그 데이터를 비동기 로드. 못 찾으면 null. (예: LoadByIdAsync<EquipmentData>("equip_rod"))
        Task<T> LoadByIdAsync<T>(string id) where T : Object;

        // 로드한 에셋 핸들 반환(누수 방지). 레지스트리 구현은 no-op, Addressables 구현은 ref-count 감소.
        void Release(Object asset);
    }
}
