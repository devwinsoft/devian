/**
 * firebase.ts — Firebase 클라이언트 초기화 (Operation 웹앱)
 *
 * localhost 전용 도구이므로 firebaseConfig를 코드에 직접 포함한다.
 * Firebase Console > Project settings > General > Web apps 에서 확인 가능.
 *
 * 초기화는 lazy — 첫 호출 시점에 수행된다 (모듈 로드 시 크래시 방지).
 *
 * TODO: 실제 프로젝트의 firebaseConfig 값으로 교체 필요
 */

import {initializeApp, type FirebaseApp} from 'firebase/app';
import {getFunctions, httpsCallable, type Functions} from 'firebase/functions';
import {getAuth, signInAnonymously, type Auth} from 'firebase/auth';

const firebaseConfig = {
  apiKey: 'AIzaSyCrQ5YqSYVauRvAv0zedLu5jgLQfEu1mTE',
  authDomain: 'devian-framwork-example.firebaseapp.com',
  projectId: 'devian-framwork-example',
  storageBucket: 'devian-framwork-example.firebasestorage.app',
  messagingSenderId: '119226054667',
  appId: '1:119226054667:web:eb6b3e71c7f7a441ab6f0e',
  measurementId: 'G-D4GYWN4XHC',
};

let _app: FirebaseApp | null = null;
let _functions: Functions | null = null;
let _auth: Auth | null = null;

function ensureFirebase(): {app: FirebaseApp; functions: Functions; auth: Auth} {
  if (!_app) {
    _app = initializeApp(firebaseConfig);
    _functions = getFunctions(_app, 'asia-northeast3');
    _auth = getAuth(_app);

    // Emulator 사용 시 주석 해제
    // connectFunctionsEmulator(_functions, '127.0.0.1', 5001);
  }
  return {app: _app, functions: _functions!, auth: _auth!};
}

/** Anonymous Auth로 로그인 (Callable auth 필수) */
async function ensureAuth(): Promise<void> {
  const {auth} = ensureFirebase();
  if (!auth.currentUser) {
    await signInAnonymously(auth);
  }
}

/** sendPushNotification Callable 래퍼 */
export interface SendEntry {
  pushId: string;
  body: string;
}

export interface SendResult {
  pushId: string;
  success: boolean;
  error?: string;
}

export async function callSendPushNotification(
  entries: SendEntry[],
): Promise<SendResult[]> {
  await ensureAuth();
  const {functions} = ensureFirebase();
  const fn = httpsCallable<{entries: SendEntry[]}, {results: SendResult[]}>(
    functions,
    'sendPushNotification',
  );
  const response = await fn({entries});
  return response.data.results;
}
