using System;
using Onikiri.Cloud;
using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 충돌 선택 화면 (60단계). **자동 병합 버튼이 없다 - 그것이 이 화면의 전부다.**
     *
     * 두 브랜치를 나란히 세우고 사람이 하나를 고른다. 어느 쪽도 "더 좋다"고
     * 추천하지 않는다 - 높은 스테이지가 희귀 오의·보석까지 더 낫다는 보장이
     * 없고(설계 §7), 추천은 곧 자동 선택의 첫 줄이다.
     *
     * ## 선택 = 씬 재로드
     *
     * 어느 쪽을 골라도 **Main 씬을 다시 연다.** 전투 중 상태를 부분적으로
     * Restore하지 않는 것이 59단계 단일 Apply 계약이고, 재로드가 그것을 지키는
     * 방법이다 - 새 부팅이 `ApplyBoot` 1회 + 방치 보상 1회를 처음부터 다시 지난다.
     *
     * ## 세 갈래
     *
     *   클라우드   로컬 3벌 순환 백업 -> 클라우드 payload를 디스크에 -> sidecar가
     *              서버 revision 채택 -> 재로드 (부팅이 InSync로 들어간다)
     *   현재 기기  sidecar가 서버 head를 base로 채택 -> 재로드 -> Dirty ->
     *              다음 커밋이 rev+1로 올린다 (58단계 트랜잭션이 기존 정본을
     *              백업 문서로 옮긴다 - 서버 쪽 한 벌도 살아남는다)
     *   나중에     로컬 플레이만. 클라우드 쓰기 정지(Conflict 상태 유지 -
     *              CloudSaveSyncPolicy.MayWrite가 막는다). 설정 줄이
     *              "기록 선택 필요"로 남아 언제든 다시 연다
     */
    public sealed class CloudConflictPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text localSummaryLabel;
        [SerializeField] private TMP_Text cloudSummaryLabel;
        [SerializeField] private TMP_Text cloudStampLabel;

        [SerializeField] private Button useCloudButton;
        [SerializeField] private Button useLocalButton;
        [SerializeField] private Button laterButton;

        private CloudSaveEnvelope serverEnvelope;

        private void Awake()
        {
            if (useCloudButton != null) useCloudButton.onClick.AddListener(ChooseCloud);
            if (useLocalButton != null) useLocalButton.onClick.AddListener(ChooseLocal);
            if (laterButton != null) laterButton.onClick.AddListener(ChooseLater);
        }

        /**
         * @brief 화면을 연다. **서버 봉투가 없으면 열지 않는다** - 비교할 것이 없다.
         *
         * 안전한 시점에만 부른다(설정에서 사람이 눌렀거나, 부팅 직후). 전투
         * 도중 충돌이 발견되면 상태만 남고 이 화면은 안 뜬다 - hot swap 금지.
         */
        public bool Show(CloudSaveEnvelope server)
        {
            if (server == null || server.summary == null) return false;

            serverEnvelope = server;

            var local = SaveSystem.Load();
            if (titleLabel != null) titleLabel.text = "기록 선택 필요";

            if (localSummaryLabel != null) localSummaryLabel.text = SummaryLine(
                local.maxStageReached, local.characterLevel, local.evolutionTier, local.gems);

            var summary = server.summary;
            if (cloudSummaryLabel != null) cloudSummaryLabel.text = SummaryLine(
                summary.maxStageReached, summary.characterLevel, summary.evolutionTier, summary.gems);

            if (cloudStampLabel != null) cloudStampLabel.text = StampLine(server.updatedAtUtcTicks);

            gameObject.SetActive(true);
            return true;
        }

        /** "최고 82층 · Lv.48 · 검귀 · 보석 320" - 설계 §7의 넉 줄 요약 */
        private static string SummaryLine(int maxStage, int level, int tier, long gems)
        {
            return "최고 " + maxStage + "층 · Lv." + level + " · "
                   + EvolutionCatalog.NameOf(tier) + " · 보석 " + gems;
        }

        /** "서버 저장 8월 18일 06:12". 판정이 아니라 **사람의 기억을 돕는 줄**이다 */
        private static string StampLine(long utcTicks)
        {
            if (utcTicks <= 0L) return string.Empty;

            DateTime local = new DateTime(utcTicks, DateTimeKind.Utc).ToLocalTime();
            return "서버 저장 " + local.Month + "월 " + local.Day + "일 "
                   + local.ToString("HH:mm");
        }

        // ---------------------------------------------------------------- 세 갈래

        /** 클라우드 채택. 로컬은 3벌 순환 백업으로 남는다 */
        public void ChooseCloud()
        {
            if (serverEnvelope == null) return;

            var cloud = CloudSaveFingerprint.Deserialize(serverEnvelope.payload);
            if (cloud == null || !SaveData.Migrate(cloud))
            {
                Debug.LogWarning("[CloudSave] 클라우드 payload를 적용할 수 없습니다.");
                return;
            }

            // 덮이기 전의 로컬을 먼저 지킨다 (precloud.1/2/3 순환)
            CloudSaveCoordinator.KeepPreCloudBackup();

            SaveSystem.Save(cloud);
            AdoptServerHead();

            Debug.Log("[CloudSave] 충돌 해결: 클라우드 기록 사용 (rev "
                      + serverEnvelope.revision + ")");
            Reload();
        }

        /**
         * @brief 현재 기기 채택. 서버 head를 인정하고 **그 위에** 올린다.
         *
         * sidecar의 base를 서버 revision으로 옮기는 것이 요점이다 - 다음 커밋이
         * rev+1이 되어 58단계 트랜잭션을 통과하고, 그 트랜잭션이 기존 서버
         * 정본을 백업 문서로 옮긴다. 지우는 것이 아니라 **밀어내는** 것이다.
         */
        public void ChooseLocal()
        {
            if (serverEnvelope == null) return;

            CloudSaveCoordinator.KeepPreCloudBackup();
            AdoptServerHead();

            Debug.Log("[CloudSave] 충돌 해결: 현재 기기 기록 사용 (서버 rev "
                      + serverEnvelope.revision + " 위에 올린다)");
            Reload();
        }

        /** 나중에. 로컬 플레이는 계속, 클라우드 쓰기만 멈춘다 */
        public void ChooseLater()
        {
            Debug.Log("[CloudSave] 충돌 보류: 이 기기에서만 저장됩니다.");
            gameObject.SetActive(false);
        }

        /**
         * @brief sidecar가 서버의 지금 자리를 base로 받아들인다.
         *
         * 두 갈래가 같은 줄을 지나는 것이 의도다. 클라우드를 골랐으면 그 자리가
         * 곧 내 자리이고(InSync), 기기를 골랐으면 그 자리 **위에서** 다음 쓰기가
         * 출발한다(Dirty -> rev+1).
         */
        private void AdoptServerHead()
        {
            string uid = CloudScores.Uid;
            var sidecar = CloudSaveSidecar.Load();

            if (!CloudSavePolicy.SidecarAppliesTo(sidecar, uid))
                sidecar = CloudSaveLocalState.NewFor(uid, CloudSaveSidecar.NewDeviceId());

            if (sidecar == null) return;

            if (sidecar.MarkSynced(serverEnvelope.revision, serverEnvelope.payloadSha256,
                                   serverEnvelope.stateSha256, serverEnvelope.updatedAtUtcTicks))
                CloudSaveSidecar.Save(sidecar);
        }

        /** 새 부팅이 ApplyBoot 1회 + 방치 보상 1회를 처음부터 다시 지난다 */
        private static void Reload()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (SuppressReloadForTests) return;
#endif
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /** 테스트가 갈래의 파일·sidecar 효과만 재고 씬 재로드는 스스로 한다 */
        public static bool SuppressReloadForTests;
#endif
    }
}
