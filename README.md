# ActionFit Match Rival

일정 기반 MatchRival 이벤트를 위한 프로젝트 중립 엔진입니다. 이벤트 구간, 스테이지 및 Easy/Hard 전환, 라이벌 제한 시간 계산, 스키마 버전이 있는 상태, 카탈로그 고정과 멱등 라운드/상자 보상 트랜잭션을 패키지가 소유합니다.

## 설치

공개 Git UPM 패키지를 `Packages/manifest.json`에 추가합니다.

```json
{
  "dependencies": {
    "com.actionfit.match-rival": "https://github.com/ActionFit-Editor/MatchRival.git#0.1.6"
  }
}
```

## 연동

다음 구현을 전달해 `MatchRivalEngine`을 구성합니다.

- `IContentStateStore`와 선택형 `IFlushableContentStateStore`
- 원자적 `IContentRewardService`
- 카탈로그, 시계, 난수, 일정, 해금, 진행 곡선, 상대 및 분석 어댑터

엔진은 Cat Merge의 `Main`, `GameEvents`, `DataStore`, Addressables, 분석 SDK, 팝업 큐 및 프로젝트 UI와 독립적입니다. 사용하는 프로젝트는 자체 테이블을 `MatchRivalCatalog`으로 변환하고 UI와 에셋은 이 패키지 밖에서 소유합니다.

재사용 가능한 UI Foundation 프레젠테이션이 필요하면 선택형 공개 패키지 `com.actionfit.match-rival.ui`를 설치합니다. UI 패키지가 이 엔진에 의존하며 역방향 의존성은 만들지 않습니다. Cat Merge 운영 테마 에셋과 팝업 호환 wrapper는 계속 프로젝트가 소유합니다.

## 영속 보상

결과 및 상자 수령은 `GrantOnce`를 호출하기 전에 정확한 보상 스냅샷과 이벤트 종료 tick 및 스테이지가 포함된 트랜잭션 ID를 영속화합니다. 복원할 때 이미 시작된 트랜잭션을 정상 매치 또는 시간 초과 처리보다 먼저 복구합니다.

## 검증

EditMode 어셈블리 `com.actionfit.match-rival.Editor.Tests`를 실행합니다. publish, 저장소 생성, Git 태그와 카탈로그 등록은 Custom Package Manager에서 수동으로 실행합니다.
