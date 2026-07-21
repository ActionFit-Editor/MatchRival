# ActionFit Match Rival

일정 기반 MatchRival 이벤트를 위한 프로젝트 중립 엔진입니다. 이벤트 구간, 스테이지 및 Easy/Hard 전환, 라이벌 제한 시간 계산, 스키마 버전이 있는 상태, 카탈로그 고정과 멱등 라운드/상자 보상 트랜잭션을 패키지가 소유합니다.

## 설치

공개 Git UPM 패키지를 `Packages/manifest.json`에 추가합니다.

```json
{
  "dependencies": {
    "com.actionfit.match-rival": "https://github.com/ActionFit-Editor/MatchRival.git#0.2.3"
  }
}
```

## 연동

다음 구현을 전달해 `MatchRivalEngine`을 구성합니다.

- `IContentStateStore`와 선택형 `IFlushableContentStateStore`
- 원자적 `IContentRewardService`
- 카탈로그, 시계, 난수, 일정, 해금, 진행 곡선, 상대 및 분석 어댑터

엔진은 Cat Merge의 `Main`, `GameEvents`, `DataStore`, Addressables, 분석 SDK, 팝업 큐 및 프로젝트 UI와 독립적입니다. canonical CSV는 이 패키지의 `Data/CSV/`가 소유하며, 사용하는 프로젝트는 `Assets/_Data/_MatchRival/`에 생성된 테이블을 `MatchRivalCatalog`으로 변환합니다. UI와 원본 visual baseline은 별도 UI 패키지가 소유합니다.

## Canonical CSV 독립 구성

`Data/CSV/`의 다섯 파일에서 읽은 `TextAsset.text`를 `MatchRivalCatalogCsvData`에 전달하고 `MatchRivalCatalogFactory.Create`를 호출하면 CSV Importer, 생성 Row/Table 코드와 프로젝트 Table SO 없이 `Catalog`과 `SchedulePolicy`를 얻습니다. `segment`를 생략하면 기본 밸런스, `MatchRivalCatalogFactory.RewardSegment`를 전달하면 Reward 밸런스를 구성합니다. 팩터리는 파일을 자동 탐색하거나 로드하지 않으며 비어 있거나 잘못된 CSV는 예외로 즉시 차단합니다.

```csharp
MatchRivalStandaloneCatalog standalone = MatchRivalCatalogFactory.Create(
    new MatchRivalCatalogCsvData(
        beanOrders.text,
        difficulties.text,
        eventSettings.text,
        boxRewards.text,
        roundRewards.text),
    MatchRivalCatalogFactory.RewardSegment);
```

CSV Importer 생성 결과는 계속 `Assets/_Data/_MatchRival/`에 둘 수 있으며, 패키지 팩터리와 기본·Reward `_Data` SO의 동등성은 프로젝트 EditMode 테스트로 검증합니다.

재사용 가능한 UI Foundation 프레젠테이션이 필요하면 선택형 공개 패키지 `com.actionfit.match-rival.ui`를 설치합니다. UI 패키지가 이 엔진에 의존하며 역방향 의존성은 만들지 않습니다. UI 패키지는 원본 production prefab/image baseline을 포함하고, Cat Merge의 Addressable 등록과 팝업 호환 wrapper는 프로젝트가 소유합니다.

시간은 `ActionFit.Time.IClock`과 calendar를 함께 주입합니다. server mode는 synchronized UTC + `TimeZoneInfo.Utc`, device mode는 device-backed UTC + `TimeZoneInfo.Local`을 사용하며, 시작된 deadline은 mode/zone 변경으로 다시 계산하지 않습니다.

새 이벤트의 활성 요일, 예상 남은 시간과 종료 자정은 비활성 저장 상태의 legacy basis와 무관하게 주입된 신규 calendar로 판단합니다. 진행 중인 legacy deadline 비교만 기존 숫자 축을 보존하며, 시작이 거절되면 basis나 저장 상태를 변경하지 않습니다.

`0.2.1`부터 기존 schema 1 저장은 숫자 tick을 변환하지 않고 `LegacyCalendarTicks`인 schema 2로 자동 승격됩니다. 승격된 스냅샷은 보상 복구나 다른 상태 전이보다 먼저 저장 및 flush되며, 알 수 없는 과거/미래 schema는 계속 덮어쓰지 않고 차단됩니다.

## 영속 보상

결과 및 상자 수령은 `GrantOnce`를 호출하기 전에 정확한 보상 스냅샷과 이벤트 종료 tick 및 스테이지가 포함된 트랜잭션 ID를 영속화합니다. 복원할 때 이미 시작된 트랜잭션을 정상 매치 또는 시간 초과 처리보다 먼저 복구합니다.

## 검증

EditMode 어셈블리 `com.actionfit.match-rival.Editor.Tests`를 실행합니다. publish, 저장소 생성, Git 태그와 카탈로그 등록은 Custom Package Manager에서 수동으로 실행합니다.
