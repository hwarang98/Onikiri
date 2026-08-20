/**
 * ONIKIRI 크로스 저장 보안 규칙 검증 (58단계).
 *
 * 운영 프로젝트(onikiri-9cc18)에 배포하지 않는다. Firestore Emulator에
 * `firestore.rules`를 그대로 올려 두고, **클라이언트가 조작됐다고 가정한 채**
 * 서버가 무엇을 거부하는지만 잰다.
 *
 * 여기서 재는 것은 규칙이 막는 것뿐이다. payload 안의 경제가 진짜인지는
 * 규칙이 답할 수 없다(문자열 한 덩어리라 서버가 열지 않는다) - 그것은
 * Cloud Functions와 Blaze의 일이고, 유료 재화 원장은 또 다른 층이다.
 */
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import assert from 'node:assert/strict';

import {
  initializeTestEnvironment,
  assertSucceeds,
  assertFails,
} from '@firebase/rules-unit-testing';

import {
  doc, getDoc, setDoc, deleteDoc, serverTimestamp, writeBatch, Timestamp,
} from 'firebase/firestore';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const RULES = readFileSync(path.join(HERE, '..', '..', 'firestore.rules'), 'utf8');

const ME = 'player-a';
const OTHER = 'player-b';

// id는 32자리 소문자 16진 (CloudSaveIds), 지문은 64자리 (SHA-256)
const SESSION_A = '1'.repeat(32);
const SESSION_B = '2'.repeat(32);
const DEVICE_A = 'a'.repeat(32);
const MUTATION_1 = 'b'.repeat(32);
const MUTATION_2 = 'c'.repeat(32);
const HASH_PAYLOAD = 'd'.repeat(64);
const HASH_STATE = 'e'.repeat(64);

let testEnv;

before(async () => {
  testEnv = await initializeTestEnvironment({
    projectId: 'demo-onikiri',
    firestore: { host: '127.0.0.1', port: 8089, rules: RULES },
  });
});

after(async () => {
  await testEnv.cleanup();
});

beforeEach(async () => {
  await testEnv.clearFirestore();
});

const me = () => testEnv.authenticatedContext(ME).firestore();
const stranger = () => testEnv.authenticatedContext(OTHER).firestore();
const anonymous = () => testEnv.unauthenticatedContext().firestore();

function summary(over = {}) {
  return {
    maxStageReached: 171,
    characterLevel: 48,
    evolutionTier: 3,
    gems: 320,
    gachaTotalPulls: 213,
    skillGachaTotalPulls: 88,
    ...over,
  };
}

function envelope(over = {}) {
  return {
    formatVersion: 1,
    saveVersion: 21,
    revision: 1,
    baseRevision: 0,
    payload: '{"version":21,"stage":82}',
    payloadSha256: HASH_PAYLOAD,
    stateSha256: HASH_STATE,
    lastMutationId: MUTATION_1,
    sessionId: SESSION_A,
    deviceId: DEVICE_A,
    updatedAt: serverTimestamp(),
    summary: summary(),
    ...over,
  };
}

/** 규칙을 끄고 상태를 만든다 - 만료된 heartbeat처럼 규칙이 못 쓰게 하는 상태용 */
async function seedSession({ sessionId = SESSION_A, released = false, ageSeconds = 0 } = {}) {
  await testEnv.withSecurityRulesDisabled(async (ctx) => {
    await setDoc(doc(ctx.firestore(), 'playerSaveSessions', ME), {
      sessionId,
      deviceId: DEVICE_A,
      released,
      heartbeatAt: Timestamp.fromMillis(Date.now() - ageSeconds * 1000),
    });
  });
}

async function seedCanonical(over = {}) {
  await testEnv.withSecurityRulesDisabled(async (ctx) => {
    await setDoc(doc(ctx.firestore(), 'playerSaves', ME),
      envelope({ updatedAt: Timestamp.fromMillis(Date.now() - 60_000), ...over }));
  });
}

async function readRaw(collection, uid = ME) {
  let data = null;
  await testEnv.withSecurityRulesDisabled(async (ctx) => {
    const snapshot = await getDoc(doc(ctx.firestore(), collection, uid));
    data = snapshot.exists() ? snapshot.data() : null;
  });
  return data;
}

/** 정본 교체 한 벌: 백업 복사 + 새 정본. 실제 클라이언트는 transaction으로 같은 것을 한다 */
function replacement(db, previous, over = {}) {
  const batch = writeBatch(db);
  batch.set(doc(db, 'playerSaveBackups', ME), previous);
  batch.set(doc(db, 'playerSaves', ME), envelope({
    revision: previous.revision + 1,
    baseRevision: previous.revision,
    lastMutationId: MUTATION_2,
    ...over,
  }));
  return batch;
}

// ---------------------------------------------------------------- 소유권

describe('소유권', () => {
  it('소유자는 자기 세이브를 읽고 쓴다', async () => {
    await seedSession();
    await assertSucceeds(setDoc(doc(me(), 'playerSaves', ME), envelope()));
    await assertSucceeds(getDoc(doc(me(), 'playerSaves', ME)));
  });

  it('남의 세이브는 읽지도 쓰지도 못한다', async () => {
    await seedSession();
    await seedCanonical();

    await assertFails(getDoc(doc(stranger(), 'playerSaves', ME)));
    await assertFails(setDoc(doc(stranger(), 'playerSaves', ME), envelope({ revision: 2, baseRevision: 1 })));
    await assertFails(getDoc(doc(stranger(), 'playerSaveBackups', ME)));
    await assertFails(getDoc(doc(stranger(), 'playerSaveSessions', ME)));
  });

  it('인증 없이는 아무것도 못 한다', async () => {
    await seedSession();
    await seedCanonical();

    await assertFails(getDoc(doc(anonymous(), 'playerSaves', ME)));
    await assertFails(setDoc(doc(anonymous(), 'playerSaves', ME), envelope()));
  });

  it('세이브는 지울 수 없다', async () => {
    await seedSession();
    await seedCanonical();

    await assertFails(deleteDoc(doc(me(), 'playerSaves', ME)));
    await assertFails(deleteDoc(doc(me(), 'playerSaveBackups', ME)));
    await assertFails(deleteDoc(doc(me(), 'playerSaveSessions', ME)));
  });
});

// ---------------------------------------------------------------- 사슬

describe('revision 사슬', () => {
  beforeEach(async () => { await seedSession(); });

  it('첫 정본은 revision 1이다', async () => {
    await assertSucceeds(setDoc(doc(me(), 'playerSaves', ME), envelope({ revision: 1, baseRevision: 0 })));
  });

  it('첫 정본이 revision 2이면 거부한다', async () => {
    await assertFails(setDoc(doc(me(), 'playerSaves', ME), envelope({ revision: 2, baseRevision: 1 })));
  });

  it('첫 정본의 baseRevision은 0이어야 한다', async () => {
    await assertFails(setDoc(doc(me(), 'playerSaves', ME), envelope({ revision: 1, baseRevision: 1 })));
  });

  it('오래된 baseRevision으로는 못 덮는다', async () => {
    await seedCanonical({ revision: 5, baseRevision: 4 });
    const previous = await readRaw('playerSaves');

    // 서버는 5인데 3에서 출발한 기기
    await assertFails(replacement(me(), previous, { revision: 6, baseRevision: 3 }).commit());
  });

  it('revision을 건너뛸 수 없다', async () => {
    await seedCanonical({ revision: 5, baseRevision: 4 });
    const previous = await readRaw('playerSaves');

    await assertFails(replacement(me(), previous, { revision: 7, baseRevision: 5 }).commit());
  });

  it('같은 revision을 다시 쓸 수 없다 (재시도는 AlreadyApplied로 끝나야 한다)', async () => {
    await seedCanonical({ revision: 5, baseRevision: 4 });
    const previous = await readRaw('playerSaves');

    await assertFails(replacement(me(), previous, { revision: 5, baseRevision: 4 }).commit());
    assert.equal((await readRaw('playerSaves')).revision, 5, '거부된 재시도는 revision을 안 올린다');
  });

  /**
   * 응답 유실 복구가 서버에서 읽는 값. 클라(CloudSaveStore)는 이 값이 자기
   * pendingMutationId와 같으면 **다시 올리지 않고** AlreadyApplied로 끝낸다.
   */
  it('커밋된 정본은 그 쓰기의 mutation id를 그대로 들고 있다', async () => {
    await seedCanonical({ revision: 5, baseRevision: 4 });
    const previous = await readRaw('playerSaves');

    await assertSucceeds(replacement(me(), previous).commit());

    const server = await readRaw('playerSaves');
    assert.equal(server.revision, 6);
    assert.equal(server.lastMutationId, MUTATION_2, '서버가 마지막 쓰기의 id를 보여준다');

    // 같은 mutation을 한 번 더 올리려 하면 사슬이 막는다 - 클라는 그 전에 멈춘다
    await assertFails(replacement(me(), previous, { revision: 6, baseRevision: 5 }).commit());
    assert.equal((await readRaw('playerSaves')).revision, 6);
  });

  it('정확히 하나 올리면 통과한다', async () => {
    await seedCanonical({ revision: 5, baseRevision: 4 });
    const previous = await readRaw('playerSaves');

    await assertSucceeds(replacement(me(), previous).commit());
    assert.equal((await readRaw('playerSaves')).revision, 6);
  });
});

// ---------------------------------------------------------------- 원자성

describe('정본과 백업의 원자성', () => {
  beforeEach(async () => {
    await seedSession();
    await seedCanonical({ revision: 5, baseRevision: 4 });
  });

  it('정본 교체와 백업 복사가 한 번에 성공한다', async () => {
    const previous = await readRaw('playerSaves');

    await assertSucceeds(replacement(me(), previous).commit());

    const backup = await readRaw('playerSaveBackups');
    assert.equal(backup.revision, 5, '백업은 교체된 그 정본이다');
    assert.equal(backup.lastMutationId, previous.lastMutationId);
    assert.equal((await readRaw('playerSaves')).revision, 6);
  });

  it('정본만 갱신하면 거부한다 (백업 없이 덮어쓰기 금지)', async () => {
    const db = me();
    await assertFails(setDoc(doc(db, 'playerSaves', ME),
      envelope({ revision: 6, baseRevision: 5, lastMutationId: MUTATION_2 })));
  });

  it('백업만 조작하면 거부한다', async () => {
    const db = me();
    const previous = await readRaw('playerSaves');

    // 정본은 그대로 둔 채 백업만 쓰기
    await assertFails(setDoc(doc(db, 'playerSaveBackups', ME), previous));

    // 값을 지어내서 백업이라 부르는 것도 마찬가지
    await assertFails(setDoc(doc(db, 'playerSaveBackups', ME),
      envelope({ revision: 4, baseRevision: 3 })));
  });

  it('백업이 교체되는 정본과 다르면 거부한다', async () => {
    const previous = await readRaw('playerSaves');
    const db = me();

    const batch = writeBatch(db);
    batch.set(doc(db, 'playerSaveBackups', ME), { ...previous, payloadSha256: 'f'.repeat(64) });
    batch.set(doc(db, 'playerSaves', ME), envelope({ revision: 6, baseRevision: 5 }));

    await assertFails(batch.commit());
  });

  it('거부된 교체는 정본도 백업도 남기지 않는다', async () => {
    const db = me();
    const batch = writeBatch(db);
    batch.set(doc(db, 'playerSaveBackups', ME), envelope({ revision: 5, baseRevision: 4 }));
    batch.set(doc(db, 'playerSaves', ME), envelope({ revision: 7, baseRevision: 6 }));

    await assertFails(batch.commit());

    assert.equal((await readRaw('playerSaves')).revision, 5, '정본 무변');
    assert.equal(await readRaw('playerSaveBackups'), null, '백업 무변(없음)');
  });
});

// ---------------------------------------------------------------- 형식

describe('봉투 형식', () => {
  beforeEach(async () => { await seedSession(); });

  const rejects = (over, label) => it(label, async () => {
    await assertFails(setDoc(doc(me(), 'playerSaves', ME), envelope(over)));
  });

  rejects({ updatedAt: Timestamp.fromMillis(Date.now()) }, '클라이언트 시각은 거부한다');
  rejects({ saveVersion: 22 }, '미래 saveVersion은 거부한다');
  rejects({ saveVersion: 0 }, 'saveVersion 0은 거부한다');
  rejects({ formatVersion: 2 }, '모르는 봉투 형식은 거부한다');
  rejects({ payload: 'x'.repeat(204801) }, '200KB 초과 payload는 거부한다');
  rejects({ payload: '' }, '빈 payload는 거부한다');
  rejects({ payloadSha256: 'not-a-hash' }, '지문 형식이 아니면 거부한다');
  rejects({ stateSha256: 'D'.repeat(64) }, '대문자 지문은 거부한다');
  rejects({ lastMutationId: 'mutation-1' }, '쓰기 id 형식이 아니면 거부한다');
  rejects({ deviceId: 'device-1' }, '기기 id 형식이 아니면 거부한다');
  rejects({ summary: summary({ evolutionTier: 7 }) }, '요약 범위 밖은 거부한다');
  rejects({ summary: summary({ gems: -1 }) }, '음수 보석은 거부한다');
  rejects({ summary: { maxStageReached: 1 } }, '요약이 모자라면 거부한다');

  it('요약에 임의 필드를 붙일 수 없다', async () => {
    await assertFails(setDoc(doc(me(), 'playerSaves', ME),
      envelope({ summary: summary({ secretStash: 999 }) })));
  });

  it('봉투에 임의 필드를 붙일 수 없다', async () => {
    await assertFails(setDoc(doc(me(), 'playerSaves', ME),
      envelope({ freeStorage: 'anything' })));
  });

  it('필드가 빠지면 거부한다', async () => {
    const partial = envelope();
    delete partial.stateSha256;
    await assertFails(setDoc(doc(me(), 'playerSaves', ME), partial));
  });
});

// ---------------------------------------------------------------- 세션

describe('작성 세션', () => {
  it('세션 문서가 없으면 세이브를 못 쓴다', async () => {
    await assertFails(setDoc(doc(me(), 'playerSaves', ME), envelope()));
  });

  it('다른 세션이 작성권을 들고 있으면 못 쓴다', async () => {
    await seedSession({ sessionId: SESSION_B });
    await assertFails(setDoc(doc(me(), 'playerSaves', ME), envelope({ sessionId: SESSION_A })));
  });

  it('놓아준 세션으로는 못 쓴다', async () => {
    await seedSession({ sessionId: SESSION_A, released: true });
    await assertFails(setDoc(doc(me(), 'playerSaves', ME), envelope({ sessionId: SESSION_A })));
  });

  it('세션은 처음 만들 수 있다', async () => {
    await assertSucceeds(setDoc(doc(me(), 'playerSaveSessions', ME), {
      sessionId: SESSION_A, deviceId: DEVICE_A, released: false, heartbeatAt: serverTimestamp(),
    }));
  });

  it('같은 세션은 heartbeat로 갱신한다', async () => {
    await seedSession({ sessionId: SESSION_A });
    await assertSucceeds(setDoc(doc(me(), 'playerSaveSessions', ME), {
      sessionId: SESSION_A, deviceId: DEVICE_A, released: false, heartbeatAt: serverTimestamp(),
    }));
  });

  it('살아 있는 다른 세션은 인수하지 못한다', async () => {
    await seedSession({ sessionId: SESSION_B, ageSeconds: 30 });
    await assertFails(setDoc(doc(me(), 'playerSaveSessions', ME), {
      sessionId: SESSION_A, deviceId: DEVICE_A, released: false, heartbeatAt: serverTimestamp(),
    }));
  });

  it('만료된 세션(180초)은 인수한다', async () => {
    await seedSession({ sessionId: SESSION_B, ageSeconds: 200 });
    await assertSucceeds(setDoc(doc(me(), 'playerSaveSessions', ME), {
      sessionId: SESSION_A, deviceId: DEVICE_A, released: false, heartbeatAt: serverTimestamp(),
    }));
  });

  it('놓아준 세션은 만료 전에도 인수한다', async () => {
    await seedSession({ sessionId: SESSION_B, released: true, ageSeconds: 5 });
    await assertSucceeds(setDoc(doc(me(), 'playerSaveSessions', ME), {
      sessionId: SESSION_A, deviceId: DEVICE_A, released: false, heartbeatAt: serverTimestamp(),
    }));
  });

  it('heartbeat를 미래로 적을 수 없다', async () => {
    await seedSession({ sessionId: SESSION_A });
    await assertFails(setDoc(doc(me(), 'playerSaveSessions', ME), {
      sessionId: SESSION_A,
      deviceId: DEVICE_A,
      released: false,
      heartbeatAt: Timestamp.fromMillis(Date.now() + 3_600_000),
    }));
  });

  it('세션에 임의 필드를 붙일 수 없다', async () => {
    await assertFails(setDoc(doc(me(), 'playerSaveSessions', ME), {
      sessionId: SESSION_A, deviceId: DEVICE_A, released: false,
      heartbeatAt: serverTimestamp(), owner: 'me',
    }));
  });
});

// ---------------------------------------------------------------- 랭킹 회귀

describe('랭킹(scores) 회귀 - 54단계 계약은 그대로다', () => {
  const score = (over = {}) => ({ uid: ME, name: '랑무사', maxStage: 171, updatedAt: serverTimestamp(), ...over });

  it('랭킹은 로그인 없이도 읽힌다', async () => {
    await testEnv.withSecurityRulesDisabled(async (ctx) => {
      await setDoc(doc(ctx.firestore(), 'scores', ME), { ...score(), updatedAt: Timestamp.now() });
    });
    await assertSucceeds(getDoc(doc(anonymous(), 'scores', ME)));
  });

  it('자기 기록은 쓸 수 있다', async () => {
    await assertSucceeds(setDoc(doc(me(), 'scores', ME), score()));
  });

  it('도달층은 내려갈 수 없다', async () => {
    await assertSucceeds(setDoc(doc(me(), 'scores', ME), score()));
    await assertFails(setDoc(doc(me(), 'scores', ME), score({ maxStage: 170 })));
    await assertSucceeds(setDoc(doc(me(), 'scores', ME), score({ maxStage: 172 })));
  });

  it('남의 기록은 못 쓴다', async () => {
    await assertFails(setDoc(doc(stranger(), 'scores', ME), score()));
  });
});
