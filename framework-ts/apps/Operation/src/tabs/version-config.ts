import { db } from '../firebase';
import { doc, getDoc, setDoc } from 'firebase/firestore';

const DOC_PATH = 'config/appVersion';

/** #.#.# 포맷 검증 */
function isValidVersion(value: string): boolean {
  return /^\d+\.\d+\.\d+$/.test(value);
}

export function createVersionConfigTab(container: HTMLElement) {
  const section = document.createElement('div');
  section.className = 'io-section';

  section.innerHTML = `
    <label>Min Version (ForceUpdate threshold)</label>
    <input type="text" id="vc-min" placeholder="e.g. 1.2.0" />
    <label>Current Version (latest release)</label>
    <input type="text" id="vc-recommend" placeholder="e.g. 1.3.0" />
    <div class="btn-row">
      <button class="btn" id="vc-load" style="margin-right:8px;">Load</button>
      <button class="btn" id="vc-save">Save</button>
    </div>
    <div class="status-msg" id="vc-status"></div>
  `;

  container.appendChild(section);

  const minInput = section.querySelector<HTMLInputElement>('#vc-min')!;
  const recommendInput = section.querySelector<HTMLInputElement>('#vc-recommend')!;
  const loadBtn = section.querySelector<HTMLButtonElement>('#vc-load')!;
  const saveBtn = section.querySelector<HTMLButtonElement>('#vc-save')!;
  const status = section.querySelector<HTMLElement>('#vc-status')!;

  function setStatus(msg: string, type: 'success' | 'error' | '') {
    status.textContent = msg;
    status.className = 'status-msg';
    if (type) status.classList.add(type);
  }

  async function load() {
    setStatus('Loading...', '');
    try {
      const snap = await getDoc(doc(db, DOC_PATH));
      if (snap.exists()) {
        const data = snap.data();
        minInput.value = data.minVersion ?? '';
        recommendInput.value = data.currentVersion ?? '';
        setStatus('Loaded from Firestore.', 'success');
      } else {
        minInput.value = '';
        recommendInput.value = '';
        setStatus('No config found. Document does not exist yet.', '');
      }
    } catch (e) {
      setStatus(`Load error: ${e instanceof Error ? e.message : String(e)}`, 'error');
    }
  }

  async function save() {
    const minVersion = minInput.value.trim();
    const currentVersion = recommendInput.value.trim();

    if (minVersion && !isValidVersion(minVersion)) {
      setStatus('Invalid minVersion format. Expected #.#.# (e.g. 1.2.0)', 'error');
      return;
    }
    if (currentVersion && !isValidVersion(currentVersion)) {
      setStatus('Invalid currentVersion format. Expected #.#.# (e.g. 1.3.0)', 'error');
      return;
    }

    setStatus('Saving...', '');
    try {
      await setDoc(doc(db, DOC_PATH), {
        minVersion,
        currentVersion,
      });
      setStatus('Saved successfully.', 'success');
    } catch (e) {
      setStatus(`Save error: ${e instanceof Error ? e.message : String(e)}`, 'error');
    }
  }

  loadBtn.addEventListener('click', load);
  saveBtn.addEventListener('click', save);

  // Auto-load on tab open
  load();
}
