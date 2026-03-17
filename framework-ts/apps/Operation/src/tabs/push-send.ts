/**
 * push-send.ts — Push Send 탭
 *
 * PUSH_REMOTE.json (NDJSON)을 로드하여 토픽별/언어별 FCM 푸시 알림을 발송한다.
 */

import {callSendPushNotification, type SendEntry, type SendResult} from '../firebase';

interface PushRemoteEntry {
  PushId: string;
  Topic: string;
  Language: string;
  DefaultMsg: string;
  IsTest: boolean;
}

const LANGUAGES = ['Korean', 'English', 'Japanese'] as const;

let loadedEntries: PushRemoteEntry[] = [];

export function createPushSendTab(container: HTMLElement): void {
  const section = document.createElement('div');
  section.className = 'io-section';

  section.innerHTML = `
    <div class="push-load-row">
      <button class="btn" id="push-load-btn">Load Data</button>
      <span class="status-msg" id="push-load-status">No data loaded.</span>
    </div>

    <label>Topic</label>
    <select id="push-topic" disabled>
      <option value="">-- Load data first --</option>
    </select>

    <label>Korean</label>
    <textarea id="push-body-Korean" rows="2" placeholder="Korean body..."></textarea>

    <label>English</label>
    <textarea id="push-body-English" rows="2" placeholder="English body..."></textarea>

    <label>Japanese</label>
    <textarea id="push-body-Japanese" rows="2" placeholder="Japanese body..."></textarea>

    <div class="btn-row">
      <button class="btn" id="push-send-btn" disabled>Send</button>
    </div>

    <label>Result</label>
    <textarea id="push-result" readonly placeholder="Send results will appear here..."></textarea>
    <div class="status-msg" id="push-status"></div>
  `;

  container.appendChild(section);

  // DOM refs
  const loadBtn = section.querySelector<HTMLButtonElement>('#push-load-btn')!;
  const loadStatus = section.querySelector<HTMLElement>('#push-load-status')!;
  const topicSelect = section.querySelector<HTMLSelectElement>('#push-topic')!;
  const sendBtn = section.querySelector<HTMLButtonElement>('#push-send-btn')!;
  const resultArea = section.querySelector<HTMLTextAreaElement>('#push-result')!;
  const statusMsg = section.querySelector<HTMLElement>('#push-status')!;

  const bodyInputs: Record<string, HTMLTextAreaElement> = {};
  for (const lang of LANGUAGES) {
    bodyInputs[lang] = section.querySelector<HTMLTextAreaElement>(`#push-body-${lang}`)!;
  }

  // ── Load Data ──
  loadBtn.addEventListener('click', () => {
    const fileInput = document.createElement('input');
    fileInput.type = 'file';
    fileInput.accept = '.json';

    fileInput.addEventListener('change', () => {
      const file = fileInput.files?.[0];
      if (!file) return;

      const reader = new FileReader();
      reader.onload = () => {
        try {
          const text = reader.result as string;
          const lines = text.trim().split('\n').filter((l) => l.trim());
          loadedEntries = lines.map((line) => JSON.parse(line) as PushRemoteEntry);

          const topics = [...new Set(loadedEntries.map((e) => e.Topic))];

          topicSelect.innerHTML = '';
          for (const topic of topics) {
            const opt = document.createElement('option');
            opt.value = topic;
            opt.textContent = topic;
            topicSelect.appendChild(opt);
          }

          topicSelect.disabled = false;
          sendBtn.disabled = false;
          loadStatus.textContent = `Loaded ${loadedEntries.length} entries (${topics.length} topics)`;
          loadStatus.className = 'status-msg success';
          resultArea.value = '';
          statusMsg.textContent = '';
        } catch (err) {
          loadStatus.textContent = `Error: ${err instanceof Error ? err.message : String(err)}`;
          loadStatus.className = 'status-msg error';
        }
      };
      reader.readAsText(file);
    });

    fileInput.click();
  });

  // ── Send ──
  sendBtn.addEventListener('click', async () => {
    const selectedTopic = topicSelect.value;
    if (!selectedTopic) return;

    statusMsg.textContent = '';
    statusMsg.className = 'status-msg';
    resultArea.value = 'Sending...';
    sendBtn.disabled = true;

    try {
      const topicRows = loadedEntries.filter((e) => e.Topic === selectedTopic);

      const entries: SendEntry[] = [];
      const skipped: string[] = [];

      for (const lang of LANGUAGES) {
        const body = bodyInputs[lang].value.trim();
        if (!body) {
          skipped.push(`${lang}: (empty body, skipped)`);
          continue;
        }

        const row = topicRows.find((r) => r.Language === lang);
        if (!row) {
          skipped.push(`${lang}: no matching entry in PUSH_REMOTE`);
          continue;
        }

        entries.push({pushId: row.PushId, body});
      }

      if (entries.length === 0) {
        resultArea.value = 'No entries to send.\n' + skipped.join('\n');
        statusMsg.textContent = 'Nothing sent.';
        statusMsg.className = 'status-msg error';
        sendBtn.disabled = false;
        return;
      }

      const results = await callSendPushNotification(entries);

      const lines: string[] = [];
      for (const r of results) {
        lines.push(r.success ? `✓ ${r.pushId}: sent` : `✗ ${r.pushId}: ${r.error}`);
      }
      for (const s of skipped) {
        lines.push(`- ${s}`);
      }

      resultArea.value = lines.join('\n');

      const allSuccess = results.every((r) => r.success);
      statusMsg.textContent = allSuccess ? 'Done.' : 'Completed with errors.';
      statusMsg.className = allSuccess ? 'status-msg success' : 'status-msg error';
    } catch (err) {
      resultArea.value = `Error: ${err instanceof Error ? err.message : String(err)}`;
      statusMsg.textContent = 'Send failed.';
      statusMsg.className = 'status-msg error';
    } finally {
      sendBtn.disabled = false;
    }
  });
}
