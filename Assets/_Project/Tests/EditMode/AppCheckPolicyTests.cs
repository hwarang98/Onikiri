using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Onikiri.Cloud;
using Onikiri.Progression;
using UnityEngine;

namespace Onikiri.Tests
{
    /**
     * @brief App Check(62단계)의 **계약**을 소스와 정적 상태에서 잰다.
     *
     * ## 왜 대부분이 소스 검사인가
     *
     * 이 스텝이 막으려는 실패 셋은 전부 **런타임에 관측되지 않는다**:
     *
     *   순서      provider를 늦게 붙이면 토큰 없는 핸들이 서는데, enforcement를
     *             켜기 전까지는 아무 증상이 없다. 켜는 순간 전 요청이 401이고,
     *             그때는 이미 출시된 뒤다
     *
     *   비밀      debug token이 소스·씬·보고서에 실리면 그 값 하나로 누구나
     *             이 프로젝트의 App Check를 통과한다. 새어 나간 뒤에 아는 것은
     *             늦다 - 커밋되기 전에 걸려야 한다
     *
     *   컴파일    "릴리스에서는 안 돈다"는 플래그로 못 지킨다. 플래그는
     *             뒤집히고, 컴파일에서 지운 코드는 안 뒤집힌다 (56·58단계 판단)
     *
     * 에디터에서는 컴파일 변형을 만들 수 없으므로 소스를 직접 읽는다. 실제
     * 토큰 발급·Verified 기록은 Firebase 콘솔과 실기의 몫이고, 여기서 증명할
     * 수 있는 것이 아니다.
     */
    public class AppCheckPolicyTests
    {
        /** Firebase 핸들이 만들어지는 순간들. **bootstrap보다 뒤에 있어야 한다** */
        private static readonly string[] FirebaseHandles =
        {
            "FirebaseApp.CheckAndFixDependenciesAsync",
            "FirebaseApp.DefaultInstance",
            "FirebaseAuth.GetAuth",
            "FirebaseFirestore.GetInstance",
            "FirebaseFirestore.DefaultInstance"
        };

        private const string Bootstrap = "FirebaseAppCheckBootstrap.EnsureConfigured()";

        private const string BootstrapFile =
            "Assets/_Project/Scripts/Cloud/FirebaseAppCheckBootstrap.cs";
        private const string DiagnosticsFile =
            "Assets/_Project/Scripts/Cloud/AppCheckDiagnostics.cs";
        private const string ScoresFile = "Assets/_Project/Scripts/Cloud/CloudScores.cs";
        private const string RuntimeFile = "Assets/_Project/Scripts/Cloud/FirebaseRuntime.cs";
        private const string StoreFile = "Assets/_Project/Scripts/Cloud/CloudSaveStore.cs";

        [TearDown]
        public void TearDown()
        {
            FirebaseAppCheckBootstrap.ResetForTests();
            CloudSaveSync.ResetForTests();
        }

        // ---------------------------------------------------------------- 순서

        /**
         * ★★ **App Check가 Firebase보다 먼저다.** 이 스텝 전체가 이 한 줄이다.
         *
         * `SetAppCheckProviderFactory`는 아직 만들어지지 않은 FirebaseApp에만
         * 온전히 먹는다. 아래 다섯 중 하나라도 먼저 지나가면 그 핸들은 토큰을
         * 달지 않고, 그 상태는 enforcement를 켜기 전까지 **증상이 없다.**
         */
        [Test]
        public void TheBootstrapRunsBeforeEveryFirebaseHandle()
        {
            foreach (string file in new[] { ScoresFile, RuntimeFile })
            {
                string source = Read(file);

                int wired = source.IndexOf(Bootstrap, StringComparison.Ordinal);
                Assert.Greater(wired, -1, file + " 이 App Check bootstrap을 부르지 않는다");

                foreach (string handle in FirebaseHandles)
                {
                    int index = IndexOfCode(source, handle);
                    if (index < 0) continue;

                    Assert.Less(wired, index,
                        file + " 에서 " + handle + " 이 App Check bootstrap보다 앞에 있다");
                }
            }
        }

        /**
         * ★ Firebase 핸들을 만드는 파일이 **그 둘뿐이다.**
         *
         * 세 번째 초기화 경로가 생기면 그쪽은 bootstrap을 안 지나고, 위 검사는
         * 초록인 채로 계약이 깨진다. 그래서 파일 목록 자체를 못 박는다 - 늘어나면
         * 이 검사가 먼저 빨개지고, 사람이 새 경로에도 bootstrap을 넣게 된다.
         */
        [Test]
        public void OnlyTwoFilesEverCreateFirebaseHandles()
        {
            var offenders = new List<string>();

            foreach (string file in Directory.GetFiles(
                         Path.Combine(Root(), "Assets/_Project/Scripts"), "*.cs",
                         SearchOption.AllDirectories))
            {
                string relative = Relative(file);
                if (relative == ScoresFile || relative == RuntimeFile) continue;

                string source = File.ReadAllText(file);
                foreach (string handle in FirebaseHandles)
                    if (IndexOfCode(source, handle) >= 0) offenders.Add(relative + " -> " + handle);
            }

            CollectionAssert.IsEmpty(offenders,
                "Firebase 초기화 경로가 늘었다. 새 경로도 App Check bootstrap을 먼저 지나야 한다");
        }

        // ---------------------------------------------------------------- 멱등

        /**
         * ★ 두 번째 호출부터는 **아무 일도 하지 않는다.**
         *
         * 두 부팅 경로가 각자 부르고, 계정 복구는 한 실행 안에서 초기화를 다시
         * 밟는다. factory를 두 번 갈아 끼우면 이미 발급된 토큰의 출처가 흐려진다.
         */
        [Test]
        public void TheBootstrapIsIdempotent()
        {
            FirebaseAppCheckBootstrap.ResetForTests();
            Assert.IsFalse(FirebaseAppCheckBootstrap.HasRun, "리셋 직후에는 안 지난 상태다");

            FirebaseAppCheckBootstrap.EnsureConfigured();

            bool ran = FirebaseAppCheckBootstrap.HasRun;
            AppCheckSetup setup = FirebaseAppCheckBootstrap.Setup;
            AppCheckProvider provider = FirebaseAppCheckBootstrap.Provider;
            string detail = FirebaseAppCheckBootstrap.Detail;

            Assert.IsTrue(ran);
            Assert.AreNotEqual(AppCheckSetup.NotAttempted, setup, "한 번은 판정이 나야 한다");

            for (int i = 0; i < 3; i++) FirebaseAppCheckBootstrap.EnsureConfigured();

            Assert.AreEqual(setup, FirebaseAppCheckBootstrap.Setup);
            Assert.AreEqual(provider, FirebaseAppCheckBootstrap.Provider);
            Assert.AreEqual(detail, FirebaseAppCheckBootstrap.Detail);
        }

        // ---------------------------------------------------------------- 플랫폼 표

        /** 에디터는 Debug provider다. 이 검사는 에디터에서 돌므로 직접 잰다 */
        [Test]
        public void TheEditorUsesTheDebugProvider()
        {
            Assert.AreEqual(AppCheckProvider.Debug, FirebaseAppCheckBootstrap.PlannedProvider);
        }

        /**
         * ★ 안드로이드 실기는 Play Integrity다.
         *
         * 에디터에서는 그 갈래가 컴파일되지 않으므로 소스로 잰다 - `#elif
         * UNITY_ANDROID` 블록이 Play Integrity factory를 붙이는가, 그리고
         * 그 블록 안에 Debug provider가 **없는가.**
         */
        [Test]
        public void AndroidUsesPlayIntegrity()
        {
            var branches = BranchesOf(BootstrapFile, "#elif UNITY_ANDROID");
            Assert.IsNotEmpty(branches, "안드로이드 갈래가 없다");

            bool wired = false;

            foreach (string branch in branches)
            {
                StringAssert.DoesNotContain("DebugAppCheckProviderFactory", branch,
                    "실기 갈래에 Debug provider가 들어가면 이 스텝은 아무것도 증명하지 못한다");

                if (branch.Contains("PlayIntegrityProviderFactory.Instance")
                    && branch.Contains("SetAppCheckProviderFactory")) wired = true;
            }

            Assert.IsTrue(wired, "안드로이드에서 Play Integrity factory를 붙이지 않는다");
        }

        /** 그 밖의 플랫폼은 **부팅을 깨지 않는다.** provider만 안 붙는다 */
        [Test]
        public void AnUnsupportedPlatformStillBoots()
        {
            var branches = BranchesOf(BootstrapFile, "#else");
            Assert.IsNotEmpty(branches);

            bool marked = false;

            foreach (string branch in branches)
            {
                StringAssert.DoesNotContain("throw", branch,
                    "미지원 플랫폼에서 Firebase 부팅을 깨면 안 된다");

                if (branch.Contains("AppCheckSetup.UnsupportedPlatform")) marked = true;
            }

            Assert.IsTrue(marked,
                "미지원 플랫폼이 그 사실을 판정에 남기지 않으면 나중에 enforcement에서 조용히 죽는다");
        }

        // ---------------------------------------------------------------- 비밀

        /**
         * ★★ **debug token은 환경 변수에서만 온다.**
         *
         * 상수·씬·프리팹·ProjectSettings 어디에도 리터럴이 없어야 한다.
         * `SetDebugToken(` 에 문자열 리터럴이 붙는 순간 그 값은 git에 들어가고,
         * git에 들어간 비밀은 콘솔에서 앱을 갈아야 회수된다.
         */
        [Test]
        public void TheDebugTokenOnlyEverComesFromTheEnvironment()
        {
            string source = Read(BootstrapFile);

            StringAssert.Contains(
                "Environment.GetEnvironmentVariable(DebugTokenVariable)", source,
                "토큰을 읽는 곳은 환경 변수 하나여야 한다");

            StringAssert.DoesNotContain("SetDebugToken(\"", source,
                "토큰 리터럴이 소스에 있다");

            // **소스만** 훑는다. 보고서가 이 규칙을 설명하려고 같은 문자열을 적는
            // 것은 비밀 유출이 아니다 - 실제로 62단계 보고서가 여기 걸렸고, 그때
            // 약해질 뻔한 것은 검사가 아니라 문장이었다. 값 검사(아래 시험)는
            // 문서까지 그대로 훑는다
            foreach (string file in TrackedTextFiles())
            {
                if (Path.GetExtension(file).ToLowerInvariant() != ".cs") continue;

                string text = File.ReadAllText(file);
                Assert.IsFalse(text.Contains("SetDebugToken(\""),
                    Relative(file) + " 에 debug token 리터럴이 있다");
            }
        }

        /**
         * ★★ 지금 환경에 있는 **그 토큰 값**이 추적 파일 어디에도 없다.
         *
         * 위 검사는 모양을 보고, 이 검사는 값을 본다. 실수로 한 번 붙여 넣은
         * 뒤 형태를 바꿔 놓은 경우(주석·문서·씬의 문자열 필드)를 잡는 것은
         * 이쪽뿐이다. 토큰이 설정돼 있지 않은 기계에서는 검사할 값이 없으므로
         * 모양 검사만 남는다 - 그래서 두 검사가 갈라져 있다.
         */
        [Test]
        public void TheLiveDebugTokenIsNotCommittedAnywhere()
        {
            string token = Environment.GetEnvironmentVariable(
                FirebaseAppCheckBootstrap.DebugTokenVariable);

            if (string.IsNullOrEmpty(token))
            {
                Assert.Pass("환경 변수에 토큰이 없다 - 값 검사는 건너뛰고 모양 검사가 남는다");
                return;
            }

            token = token.Trim();
            Assert.Greater(token.Length, 8, "토큰이 너무 짧다 - 값 검사가 오탐을 낸다");

            foreach (string file in TrackedTextFiles())
                Assert.IsFalse(
                    File.ReadAllText(file).IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0,
                    Relative(file) + " 에 App Check debug token 원문이 실려 있다");
        }

        /** 진단기는 **토큰 원문을 읽지 않는다** - 읽으면 어딘가에 찍힌다 */
        [Test]
        public void TheDiagnosticsNeverTouchTheRawToken()
        {
            string source = Read(DiagnosticsFile);

            StringAssert.Contains("ExpireTime", source, "만료 시각은 찍어야 진단이 된다");
            StringAssert.DoesNotContain(".Token", source,
                "AppCheckToken.Token 을 읽는 순간 그 값은 로그·화면으로 샌다");
        }

        // ---------------------------------------------------------------- 컴파일 가드

        /**
         * ★★ Debug provider 경로가 **릴리스 컴파일에 존재하지 않는다.**
         *
         * 에디터에서는 컴파일 변형을 만들 수 없으므로 소스를 읽는다 - 각
         * 토큰이 `#if UNITY_EDITOR` 안쪽인지 가드를 세어 확인한다.
         */
        [Test]
        public void TheDebugProviderIsCompiledOutOfReleaseBuilds()
        {
            string source = Read(BootstrapFile);

            foreach (string seam in new[]
            {
                "DebugAppCheckProviderFactory", "SetDebugToken", "DebugTokenVariable",
                "ReadDebugToken", "EditorServerCallsAllowed", "EditorBlockReason"
            })
                AssertGuarded(source, BootstrapFile, seam);
        }

        /** 진단기는 **파일 전체가** 가드 안이다 */
        [Test]
        public void TheDiagnosticsAreCompiledOutOfReleaseBuilds()
        {
            string[] lines = Read(DiagnosticsFile).Split('\n');

            Assert.AreEqual("#if UNITY_EDITOR || DEVELOPMENT_BUILD", lines[0].Trim(),
                "진단기는 첫 줄부터 가드 안이어야 한다");

            string last = string.Empty;
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                last = lines[i].Trim();
                if (last.Length > 0) break;
            }

            Assert.AreEqual("#endif", last);
        }

        /**
         * ★★ **임의 실패 스위치가 출시 빌드에 없다.**
         *
         * 응답 유실 모의는 서버가 커밋한 뒤 로컬을 뒤로 남기는 seam이다.
         * production에 남으면 그것은 진단 도구가 아니라 사고의 원인이 된다.
         */
        [Test]
        public void TheResponseLossSeamIsCompiledOutOfReleaseBuilds()
        {
            AssertGuarded(Read(StoreFile), StoreFile, "DropNextCommitResponseForTests");
        }

        // ---------------------------------------------------------------- 게이트

        /**
         * ★ 토큰이 없으면 **에디터의 실서버 왕복이 막힌다.**
         *
         * 기존 스위치(EditorServerCheckAllowed · EditorNetworkAllowed)를 대신하지
         * 않고 그 위에 얹는다 - 사람이 켰더라도 토큰이 없으면 나가는 요청은
         * enforcement 뒤에 전부 거부되고, 그 거부는 규칙 문제와 구분되지 않는다.
         */
        [Test]
        public void AMissingTokenClosesTheEditorServerGate()
        {
            FirebaseAppCheckBootstrap.ResetForTests();

            bool hasToken = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(
                FirebaseAppCheckBootstrap.DebugTokenVariable));

            bool allowed = FirebaseAppCheckBootstrap.EditorServerCallsAllowed;

            Assert.AreEqual(hasToken, allowed,
                "토큰이 있을 때만 에디터가 실서버로 나간다");
            Assert.AreEqual(allowed, FirebaseAppCheckBootstrap.IsConfigured);

            if (!allowed)
            {
                Assert.AreEqual(AppCheckSetup.MissingDebugToken, FirebaseAppCheckBootstrap.Setup);
                StringAssert.Contains(FirebaseAppCheckBootstrap.DebugTokenVariable,
                    FirebaseAppCheckBootstrap.EditorBlockReason,
                    "왜 막혔는지 화면이 읽을 수 있어야 한다");
            }
        }

        /** 세 게이트가 모두 App Check 조건을 지난다 */
        [Test]
        public void EveryEditorServerGateChecksAppCheck()
        {
            foreach (string file in new[]
            {
                "Assets/_Project/Scripts/Cloud/CloudSaveCoordinator.cs",
                "Assets/_Project/Scripts/Cloud/CloudSaveSync.cs",
                "Assets/_Project/Scripts/Cloud/CloudSaveRecovery.cs"
            })
                StringAssert.Contains("FirebaseAppCheckBootstrap.EditorServerCallsAllowed",
                    Read(file), file + " 의 에디터 게이트가 App Check를 안 본다");
        }

        // ---------------------------------------------------------------- 무회귀

        /**
         * ★★ **App Check가 죽어도 로컬 저장은 산다.**
         *
         * 방치형에서 클라우드 하나로 저장이 막히면 몇 시간이 통째로 날아간다.
         * 막히는 것은 클라우드뿐이고, 그 사실이 상태 문장에 그대로 적혀야 한다.
         */
        [Test]
        public void ABlockedAppCheckNeverStopsTheLocalSave()
        {
            FirebaseAppCheckBootstrap.ResetForTests();

            using (new SaveSandbox())
            {
                var data = SaveData.NewGame();
                data.stage = 42;
                data.maxStageReached = 42;
                data.gems = 777L;

                SaveSystem.Save(data);

                SaveData loaded = SaveSystem.Load();
                Assert.IsNotNull(loaded, "App Check와 무관하게 저장·읽기는 된다");
                Assert.AreEqual(42, loaded.maxStageReached);
                Assert.AreEqual(777L, loaded.gems);
                Assert.AreEqual(SaveData.CurrentVersion, loaded.version, "세이브 v21 유지");
            }
        }

        /** App Check가 막으면 **클라우드 상태만** 움직인다 */
        [Test]
        public void ABlockedAppCheckOnlyMovesTheCloudState()
        {
            Assert.IsFalse(CloudSaveSyncPolicy.MayWrite(CloudSaveState.Blocked));
            Assert.IsFalse(CloudSaveSyncPolicy.MayWrite(CloudSaveState.Conflict));

            // 서버를 못 본 부팅은 실패가 아니라 LocalOnly다 - 로컬 한 벌로 들어간다
            var local = SaveData.NewGame();
            local.stage = 9;
            local.maxStageReached = 9;

            var choice = CloudSaveCoordinator.ChooseFrom(
                local, null, default(CloudSaveFetchResult), false);

            Assert.AreEqual(CloudSaveState.LocalOnly, choice.state);
            Assert.AreSame(local, choice.data, "막힌 것은 클라우드뿐이고 진행은 로컬 그대로다");
        }

        // ---------------------------------------------------------------- urgent 수렴

        /**
         * ★★ 같은 프레임의 urgent 이벤트 여럿이 **커밋 하나로 모인다.**
         *
         * 10연 한 번에 뽑기 이벤트 열 개와 보석 감소 하나가 온다. 그때마다
         * 시계가 새로 걸리면 유예가 계속 밀리거나 이벤트마다 커밋이 나가고,
         * 후자는 곧 revision 폭증이다 - 백업 한 벌이 그만큼씩 밀려난다.
         */
        [Test]
        public void ManyUrgentEventsConvergeOnOneCommit()
        {
            CloudSaveSync.ResetForTests();
            Assert.Less(CloudSaveSync.UrgentSinceForTests, 0f, "처음에는 걸린 적이 없다");

            CloudSaveSync.RequestUrgent("10연 - 첫 결과");
            float first = CloudSaveSync.UrgentSinceForTests;
            Assert.GreaterOrEqual(first, 0f);

            for (int i = 0; i < 10; i++) CloudSaveSync.RequestUrgent("10연 - 결과 " + i);
            CloudSaveSync.RequestUrgent("보석 소비");

            Assert.AreEqual(first, CloudSaveSync.UrgentSinceForTests,
                "urgent 시계는 처음 한 번만 걸린다 - 다시 걸리면 커밋이 이벤트마다 나간다");
        }

        /**
         * ★★ urgent은 **올리기 직전에 저장을 세운다.**
         *
         * 62단계 실기에서 2/2로 재현된 실패다: `GameSession.Save()`가 자동 저장
         * 30초·pause·종료에서만 도는데 urgent 유예는 2초라, 보석을 쓴 직후의
         * 커밋이 **지불 전 스냅샷**을 올렸다(rev 44 = 1022, rev 45 = 992).
         * urgent이 막으려던 바로 그 창이 열려 있었다.
         *
         * 재화가 복제되지는 않았다 - 전체 스냅샷이라 지불과 상품이 함께 빠진다.
         * 그래도 "잃으면 지불이 사라지는 순간"에 아무 일도 못 하는 장치였다.
         */
        [Test]
        public void AnUrgentCommitSavesBeforeItUploads()
        {
            string source = Read("Assets/_Project/Scripts/Cloud/CloudSaveSync.cs");

            int tick = source.IndexOf("public static void Tick()", StringComparison.Ordinal);
            Assert.Greater(tick, -1);

            int commit = source.IndexOf("CommitAsync(", tick, StringComparison.Ordinal);
            Assert.Greater(commit, tick, "Tick이 커밋을 부르지 않는다");

            string body = source.Substring(tick, commit - tick);

            // 주석이 아니라 **호출**을 센다. 이 파일의 주석은 두 이름을 다 말하고,
            // 실제로 그 주석에 한 번 걸렸다 - 잡을 것은 문장이 아니라 순서다
            int save = IndexOfCode(body, "SaveRequested");
            Assert.Greater(save, -1,
                "urgent이 자기 스냅샷을 세우지 않는다 - 지불 전 스냅샷이 올라간다");

            // ★★ 순서가 계약이다. 저장 요청이 이 가드보다 **뒤**로 가면,
            // 커밋 직후(pendingData=null·dirtySince=-1)의 지불은 자동 저장
            // 30초가 올 때까지 영영 안 올라간다 - 실기에서 물린 그대로다
            int guard = IndexOfCode(body, "pendingData == null");
            Assert.Greater(guard, -1, "Tick의 pendingData 가드가 사라졌다");
            Assert.Less(save, guard,
                "저장 요청이 pendingData 가드보다 뒤에 있다 - 커밋 직후의 urgent이 죽는다");

            // 유예 전에 부르면 10연이 디스크를 열 번 친다
            StringAssert.Contains("UrgentDelaySeconds", body,
                "유예가 차기 전에 저장을 부르면 안 된다");
        }

        /** GameSession이 그 창구에 자기 Save를 걸어 둔다 - 안 걸면 위 검사가 헛돈다 */
        [Test]
        public void TheSessionWiresItsSaveIntoTheUrgentPath()
        {
            StringAssert.Contains("CloudSaveSync.SaveRequested = Save",
                Read("Assets/_Project/Scripts/Subsystems/GameSession.cs"),
                "GameSession이 urgent 저장 창구를 배선하지 않는다");
        }

        // ---------------------------------------------------------------- 응답 유실

        /**
         * ★★ 응답이 유실된 커밋의 재시도는 **revision을 더 올리지 않는다.**
         *
         * 서버는 커밋했는데 응답 전에 앱이 죽으면 로컬은 "안 올라갔다"고 믿는다.
         * 그 상태에서 새 id로 재시도하면 revision이 하나 더 오르고 백업이 한 칸
         * 밀린다 - 밀려난 백업은 되찾을 방법이 없다.
         */
        [Test]
        public void AReplayedMutationDoesNotAdvanceTheRevision()
        {
            string mutation = CloudSaveIds.New();

            Assert.IsTrue(CloudSavePolicy.WasCommitAlreadyApplied(mutation, mutation));
            Assert.IsFalse(CloudSavePolicy.WasCommitAlreadyApplied(mutation, CloudSaveIds.New()));
            Assert.IsFalse(CloudSavePolicy.WasCommitAlreadyApplied(mutation, string.Empty),
                "서버에 id가 없으면 '이미 적용됨'이 아니다");

            // AlreadyApplied는 성공으로 센다. 그리고 revision은 **서버 값 그대로**다
            var replay = new CloudSaveCommitResult
            {
                status = CloudSaveStoreStatus.AlreadyApplied,
                revision = 7L
            };

            Assert.IsTrue(replay.IsSynced);
            Assert.AreEqual(7L, replay.revision, "재시도가 8로 올리면 백업이 한 칸 밀린다");
        }

        /**
         * ★ 실패한 커밋은 **pending을 남긴다.** 그 자국이 다음 실행의 열쇠다.
         *
         * 응답 유실 모의(seam)가 만드는 상태가 정확히 이것이다 - 서버는 rev N,
         * 로컬 sidecar는 pendingMutationId를 든 채 base rev N-1.
         */
        [Test]
        public void AFailedCommitKeepsThePendingMutation()
        {
            var sidecar = CloudSaveLocalState.NewFor(CloudSaveIds.New(), CloudSaveIds.New());
            Assert.IsNotNull(sidecar);
            Assert.IsFalse(sidecar.HasPending);

            string mutation = CloudSaveIds.New();
            Assert.IsTrue(sidecar.MarkPending(mutation, Hash()));

            Assert.IsTrue(sidecar.HasPending, "응답이 안 왔으면 pending은 그대로다");
            Assert.AreEqual(mutation, sidecar.pendingMutationId);
            Assert.AreEqual(0L, sidecar.baseRevision, "서버가 커밋했어도 로컬은 아직 모른다");

            // 다음 실행이 같은 id로 재시도해 AlreadyApplied를 받은 뒤에야 앞으로 민다
            Assert.IsTrue(sidecar.MarkSynced(1L, Hash(), Hash(), 0L));
            Assert.IsFalse(sidecar.HasPending);
            Assert.AreEqual(1L, sidecar.baseRevision);
        }

        // ---------------------------------------------------------------- 진단 차단

        /**
         * ★★ **토큰이 실패하면 진단이 Firestore를 한 번도 부르지 않는다** (62.1단계).
         *
         * 예전에는 토큰 요청이 실패해도 그 다음 줄에서 `Source.Server` 정본을 읽었다.
         * 나쁜 이유가 둘이다:
         *
         *   ① 미검증 요청을 **우리 손으로 만든다.** enforcement 뒤에는 거부되고,
         *      그 거부가 Metrics의 "확인되지 않음"에 쌓인다 - 진단 도구가 진단
         *      대상을 오염시킨다.
         *   ② enforcement가 꺼져 있는 동안에는 토큰 없이도 정본이 돌아온다.
         *      그 성공을 "App Check가 된다"로 적으면 거짓 보고가 된다.
         */
        [Test]
        public void AFailedTokenStopsTheDiagnosticsBeforeAnyServerCall()
        {
            FirebaseAppCheckBootstrap.ResetForTests();
            AppCheckDiagnostics.ResetForTests();

            if (FirebaseAppCheckBootstrap.EditorServerCallsAllowed)
            {
                Assert.Ignore("이 기계에는 debug token이 있어 실패 갈래를 만들 수 없다 "
                              + "- 토큰 없는 기계에서 재면 된다");
                return;
            }

            string report;
            using (new SaveSandbox())
                report = AppCheckDiagnostics.RunAsync().GetAwaiter().GetResult();

            Assert.AreEqual(0, AppCheckDiagnostics.ServerCallsForTests,
                "토큰이 없는데 Firestore로 나갔다");
            Assert.IsFalse(AppCheckDiagnostics.LastTokenOk);

            StringAssert.Contains(AppCheckDiagnostics.AbortedMarker, report,
                "왜 멈췄는지 화면에서 읽혀야 한다");
            StringAssert.DoesNotContain("정본 읽기", report,
                "정본을 읽지 않았는데 읽은 것처럼 적히면 안 된다");
        }

        /** ★ 중단 갈래가 `RunAsync` 안에 실제로 있다 - 순서까지 소스로 못 박는다 */
        [Test]
        public void TheDiagnosticsAbortBranchGuardsTheCanonicalRead()
        {
            string source = Read(DiagnosticsFile);

            int run = source.IndexOf("public static async Task<string> RunAsync()",
                                     StringComparison.Ordinal);
            Assert.Greater(run, -1);

            int guard = IndexOfCode(source.Substring(run), "if (!LastTokenOk)");
            int read = IndexOfCode(source.Substring(run), "await ReadCanonicalAsync()");

            Assert.Greater(guard, -1, "토큰 실패를 보는 갈래가 없다");
            Assert.Greater(read, -1, "정본 읽기가 사라졌다");
            Assert.Less(guard, read,
                "토큰 검사가 정본 읽기보다 뒤에 있다 - 그러면 미검증 요청이 나간다");
        }

        /**
         * ★★ **토큰이 확인되지 않으면 `ReadCanonicalAsync`가 직접 불려도 안 나간다.**
         *
         * 62.1.1이 닫은 우회 경로다. `RunAsync`의 조기 중단만으로는 부족했다 -
         * 테스트 패널의 `정본 읽기` 버튼이 이 메서드를 **직접** 부르고, 그 버튼의
         * 활성 조건이 `IsConfigured`뿐이었다. provider는 붙었는데 토큰 갱신이
         * 실패한 상태에서 누르면 미검증 요청이 그대로 나갔다.
         *
         * UI만 고치면 호출자가 하나 늘 때 같은 구멍이 다시 열린다. 그래서
         * 나가는 문 안쪽에서 잰다 - **호출 횟수 0**이 계약이다.
         */
        [Test]
        public void ADirectCanonicalReadWithoutATokenNeverReachesTheServer()
        {
            AppCheckDiagnostics.ResetForTests();
            Assert.IsFalse(AppCheckDiagnostics.LastTokenOk, "리셋 직후에는 토큰이 없다");

            string result;
            using (new SaveSandbox())
                result = AppCheckDiagnostics.ReadCanonicalAsync().GetAwaiter().GetResult();

            Assert.AreEqual(0, AppCheckDiagnostics.ServerCallsForTests,
                "토큰이 확인되지 않았는데 Firestore로 나갔다");
            Assert.AreEqual(AppCheckDiagnostics.TokenlessAbort, result,
                "왜 안 읽었는지 부르는 쪽이 알아야 한다");
        }

        /**
         * ★★ **토큰 강제 갱신이 실패하면 그 뒤의 직접 읽기도 0회다.**
         *
         * 갱신 실패가 옛 성공을 남겨 두면, 그 다음 읽기가 "확인된 토큰"이라고
         * 믿고 나간다. 그래서 `RefreshTokenAsync`는 **진입에서 먼저 내린다.**
         */
        [Test]
        public void AFailedRefreshDoesNotLeaveAStaleSuccess()
        {
            AppCheckDiagnostics.ResetForTests();
            FirebaseAppCheckBootstrap.ResetForTests();

            if (FirebaseAppCheckBootstrap.EditorServerCallsAllowed)
            {
                Assert.Ignore("이 기계에는 debug token이 있어 갱신 실패를 만들 수 없다");
                return;
            }

            using (new SaveSandbox())
            {
                AppCheckDiagnostics.RefreshTokenAsync().GetAwaiter().GetResult();

                Assert.IsFalse(AppCheckDiagnostics.LastTokenOk,
                    "갱신이 실패했는데 성공 상태가 남았다");

                string result = AppCheckDiagnostics.ReadCanonicalAsync().GetAwaiter().GetResult();

                Assert.AreEqual(0, AppCheckDiagnostics.ServerCallsForTests);
                Assert.AreEqual(AppCheckDiagnostics.TokenlessAbort, result);
            }
        }

        /**
         * ★ 토큰이 확인되면 **토큰 게이트를 지나간다.**
         *
         * 방어가 과해져 정상 경로까지 막으면 진단이 쓸모없어진다. 다만 에디터에는
         * 로그인이 없어 그 다음 관문(uid)에서 멈추므로, 여기서 잴 수 있는 것은
         * "토큰 때문에 막히지는 않았다"까지다 - 멈춘 이유가 **토큰이 아니라
         * 로그인**임을 결과 문자열로 가른다. 실제 Firestore 왕복은 여전히 0회다.
         */
        [Test]
        public void AVerifiedTokenPassesTheTokenGate()
        {
            AppCheckDiagnostics.ResetForTests();
            AppCheckDiagnostics.SetTokenOkForTests(true);

            string result;
            using (new SaveSandbox())
                result = AppCheckDiagnostics.ReadCanonicalAsync().GetAwaiter().GetResult();

            Assert.AreNotEqual(AppCheckDiagnostics.TokenlessAbort, result,
                "토큰이 확인됐는데 토큰 게이트가 막으면 진단이 쓸모없다");
            StringAssert.Contains("로그인 전", result,
                "멈춘 이유는 토큰이 아니라 로그인이어야 한다");
            Assert.AreEqual(0, AppCheckDiagnostics.ServerCallsForTests,
                "로그인 전에는 서버로 나가지 않는다");
        }

        /** ★ 에디터 `정본 읽기` 버튼의 활성 조건에 **토큰 성공**이 들어 있다 */
        [Test]
        public void TheEditorCanonicalButtonChecksTheToken()
        {
            string source = Read("Assets/_Project/Editor/OnikiriTestPanel.cs");

            int button = source.IndexOf("AppCheckDiagnostics.ReadCanonicalAsync()",
                                        StringComparison.Ordinal);
            Assert.Greater(button, -1, "App Check 절의 정본 읽기 버튼이 사라졌다");

            // 버튼 바로 앞의 DisabledScope가 토큰을 보는가
            string before = source.Substring(Math.Max(0, button - 700), Math.Min(700, button));

            StringAssert.Contains("AppCheckDiagnostics.LastTokenOk", before,
                "provider 설정만 보고 버튼을 열면 갱신 실패 상태에서 미검증 요청이 나간다");
        }

        /**
         * ★ 미검증 요청을 **일부러 보내는 창구가 없다.**
         *
         * 음성 검사(토큰 없이 보내서 거부되는지 보기)는 클라이언트에 두지 않는다 -
         * 출시 빌드에 남으면 그 자체가 미검증 트래픽 생성기다. 그 확인은 콘솔의
         * enforcement 단계에서 한다.
         */
        [Test]
        public void NoDeliberateUnverifiedRequestPathExists()
        {
            string source = Read(DiagnosticsFile);

            foreach (string forbidden in new[] { "ForceUnverified", "SkipAppCheck", "WithoutToken" })
                StringAssert.DoesNotContain(forbidden, source,
                    "미검증 요청을 만드는 창구는 두지 않는다");
        }

        // ---------------------------------------------------------------- 도구

        private static string Root()
        {
            return Path.GetDirectoryName(Application.dataPath);
        }

        private static string Read(string relative)
        {
            return File.ReadAllText(Path.Combine(Root(), relative));
        }

        private static string Relative(string absolute)
        {
            return absolute.Substring(Root().Length + 1).Replace('\\', '/');
        }

        /**
         * @brief 전처리 지시어 하나가 여는 **모든** 갈래의 본문.
         *
         * 같은 지시어가 파일에 여러 번 나온다(PlannedProvider와 Configure가 같은
         * 플랫폼 표를 쓴다). 첫 번째만 보면 엉뚱한 갈래를 검사하게 되므로 전부
         * 모아서 각각에 대해 잰다.
         */
        private static List<string> BranchesOf(string file, string directive)
        {
            string source = Read(file);
            var branches = new List<string>();

            int index = source.IndexOf(directive, StringComparison.Ordinal);

            while (index >= 0)
            {
                int elif = source.IndexOf("#elif", index + directive.Length, StringComparison.Ordinal);
                int other = source.IndexOf("#else", index + directive.Length, StringComparison.Ordinal);
                int end = source.IndexOf("#endif", index + directive.Length, StringComparison.Ordinal);

                int stop = end;
                if (elif >= 0 && elif < stop) stop = elif;
                if (other >= 0 && other < stop) stop = other;

                if (stop > index) branches.Add(source.Substring(index, stop - index));

                index = source.IndexOf(directive, index + directive.Length, StringComparison.Ordinal);
            }

            return branches;
        }

        private static string Hash()
        {
            return CloudSaveFingerprint.HashOf(Guid.NewGuid().ToString("N"));
        }

        /**
         * @brief 이 프로젝트가 git으로 나르는 텍스트 파일들. 비밀 검사가 훑는 범위다.
         *
         * Firebase SDK·TextMesh Pro 같은 벤더 폴더는 뺀다 - 우리가 쓰지 않고,
         * 넣으면 검사 한 번에 수천 파일을 읽는다.
         */
        private static IEnumerable<string> TrackedTextFiles()
        {
            string root = Root();

            foreach (string folder in new[]
            {
                "Assets/_Project", "Assets/Scenes", "ProjectSettings", "docs"
            })
            {
                string path = Path.Combine(root, folder);
                if (!Directory.Exists(path)) continue;

                foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                {
                    string extension = Path.GetExtension(file).ToLowerInvariant();

                    if (extension == ".cs" || extension == ".unity" || extension == ".prefab"
                        || extension == ".asset" || extension == ".md" || extension == ".json"
                        || extension == ".xml" || extension == ".txt" || extension == ".rules")
                        yield return file;
                }
            }
        }

        /**
         * @brief 이 토큰의 **모든** 등장이 컴파일 가드 안인가.
         *
         * 열린 `#if UNITY_EDITOR`의 수가 닫힌 `#endif`보다 많으면 그 위치는
         * 가드 안이다. CloudSaveSyncTests의 S4-0-1과 같은 셈이고, 같은 이유로
         * 정확하다 - 이 코드베이스에는 중첩 가드가 없다.
         */
        private static void AssertGuarded(string source, string file, string seam)
        {
            int index = source.IndexOf(seam, StringComparison.Ordinal);
            Assert.Greater(index, -1, file + " 에 " + seam + " 이 없다 - 검사가 헛돌고 있다");

            while (index >= 0)
            {
                string before = source.Substring(0, index);

                Assert.Greater(Count(before, "#if UNITY_EDITOR"), Count(before, "#endif"),
                    file + " 의 " + seam + " 이 컴파일 가드 밖에 있다 (offset " + index + ")");

                index = source.IndexOf(seam, index + 1, StringComparison.Ordinal);
            }
        }

        /** 주석이 아닌 줄에서만 찾는다 - 설계를 설명하는 문장은 호출이 아니다 */
        private static int IndexOfCode(string source, string token)
        {
            int offset = 0;

            foreach (string line in source.Split('\n'))
            {
                string trimmed = line.TrimStart();

                if (!trimmed.StartsWith("//") && !trimmed.StartsWith("*")
                    && !trimmed.StartsWith("/*"))
                {
                    int index = line.IndexOf(token, StringComparison.Ordinal);
                    if (index >= 0) return offset + index;
                }

                offset += line.Length + 1;
            }

            return -1;
        }

        private static int Count(string text, string token)
        {
            int count = 0, index = text.IndexOf(token, StringComparison.Ordinal);

            while (index >= 0)
            {
                count++;
                index = text.IndexOf(token, index + 1, StringComparison.Ordinal);
            }

            return count;
        }
    }
}
