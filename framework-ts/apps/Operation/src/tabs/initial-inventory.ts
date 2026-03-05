import { db } from '../firebase';
import { doc, getDoc, setDoc } from 'firebase/firestore';

const DOC_PATH = 'config/initialInventory';

const REWARD_TYPES = [
  'CARD',
  'CURRENCY',
  'EQUIP',
  'HERO',
  'RENTAL',
  'SEASON_PASS',
] as const;

type RewardType = typeof REWARD_TYPES[number];

interface RewardRow {
  type: RewardType;
  id: string;
  amount: number;
}

function isRewardType(value: string): value is RewardType {
  return (REWARD_TYPES as readonly string[]).includes(value);
}

function normalizeReward(raw: unknown): RewardRow | null {
  if (raw == null || typeof raw !== 'object') return null;

  const row = raw as Record<string, unknown>;
  const type = String(row.type ?? '').trim().toUpperCase();
  const id = String(row.id ?? '').trim();
  const amount = Number(row.amount);

  if (!isRewardType(type)) return null;
  if (!id) return null;
  if (!Number.isInteger(amount) || amount <= 0) return null;

  return { type, id, amount };
}

export function createInitialInventoryTab(container: HTMLElement) {
  const section = document.createElement('div');
  section.className = 'io-section';

  section.innerHTML = `
    <label>Current Rewards</label>
    <div class="reward-table-wrap">
      <table class="reward-table">
        <thead>
          <tr>
            <th>Type</th>
            <th>ID</th>
            <th>Amount</th>
            <th class="reward-action-col">Action</th>
          </tr>
        </thead>
        <tbody id="ii-reward-list"></tbody>
      </table>
    </div>

    <label>Add Reward</label>
    <div class="reward-input-row">
      <select id="ii-type"></select>
      <input type="text" id="ii-id" placeholder="reward id (e.g. GOLD)" />
      <input type="number" id="ii-amount" min="1" step="1" placeholder="amount" />
      <button class="btn reward-action-btn" id="ii-add" title="Add reward">+</button>
    </div>

    <div class="btn-row">
      <button class="btn" id="ii-save">Save</button>
    </div>

    <div class="status-msg" id="ii-status"></div>
  `;

  container.appendChild(section);

  const rewardList = section.querySelector<HTMLTableSectionElement>('#ii-reward-list')!;
  const typeSelect = section.querySelector<HTMLSelectElement>('#ii-type')!;
  const idInput = section.querySelector<HTMLInputElement>('#ii-id')!;
  const amountInput = section.querySelector<HTMLInputElement>('#ii-amount')!;
  const addBtn = section.querySelector<HTMLButtonElement>('#ii-add')!;
  const saveBtn = section.querySelector<HTMLButtonElement>('#ii-save')!;
  const status = section.querySelector<HTMLElement>('#ii-status')!;

  let rewards: RewardRow[] = [];

  function setStatus(msg: string, type: 'success' | 'error' | '') {
    status.textContent = msg;
    status.className = 'status-msg';
    if (type) status.classList.add(type);
  }

  function renderRewardList() {
    if (rewards.length === 0) {
      rewardList.innerHTML = `
        <tr>
          <td colspan="4" class="reward-empty">No rewards configured.</td>
        </tr>
      `;
      return;
    }

    rewardList.innerHTML = rewards.map((row, index) => `
      <tr>
        <td>${row.type}</td>
        <td>${row.id}</td>
        <td>${row.amount}</td>
        <td class="reward-action-col">
          <button class="btn btn-danger reward-action-btn" data-index="${index}" title="Delete reward">-</button>
        </td>
      </tr>
    `).join('');
  }

  function addRewardFromInput() {
    const type = typeSelect.value.trim().toUpperCase();
    const id = idInput.value.trim();
    const amount = Number(amountInput.value.trim());

    if (!isRewardType(type)) {
      setStatus('Invalid reward type.', 'error');
      return;
    }

    if (!id) {
      setStatus('Reward id is required.', 'error');
      return;
    }

    if (!Number.isInteger(amount) || amount <= 0) {
      setStatus('Amount must be a positive integer.', 'error');
      return;
    }

    rewards.push({ type, id, amount });
    renderRewardList();

    idInput.value = '';
    amountInput.value = '';
    setStatus('Reward added. Click Save to persist.', '');
  }

  function validateAllRewards(): string | null {
    for (let i = 0; i < rewards.length; i++) {
      const row = rewards[i];
      if (!isRewardType(row.type)) return `Row ${i + 1}: invalid type.`;
      if (!row.id?.trim()) return `Row ${i + 1}: id is empty.`;
      if (!Number.isInteger(row.amount) || row.amount <= 0) {
        return `Row ${i + 1}: amount must be a positive integer.`;
      }
    }

    return null;
  }

  async function load() {
    setStatus('Loading...', '');

    try {
      const snap = await getDoc(doc(db, DOC_PATH));
      if (!snap.exists()) {
        rewards = [];
        renderRewardList();
        setStatus('No config found. Document does not exist yet.', '');
        return;
      }

      const rawRewards = snap.data().rewards;
      if (rawRewards == null) {
        rewards = [];
        renderRewardList();
        setStatus('Loaded. rewards is empty.', 'success');
        return;
      }

      if (!Array.isArray(rawRewards)) {
        rewards = [];
        renderRewardList();
        setStatus('Load error: rewards must be an array.', 'error');
        return;
      }

      const parsed: RewardRow[] = [];
      let invalidCount = 0;
      for (let i = 0; i < rawRewards.length; i++) {
        const reward = normalizeReward(rawRewards[i]);
        if (reward == null) {
          invalidCount++;
          continue;
        }
        parsed.push(reward);
      }

      rewards = parsed;
      renderRewardList();

      if (invalidCount > 0) {
        setStatus(`Loaded with warning: ${invalidCount} invalid row(s) skipped.`, 'error');
      } else {
        setStatus('Loaded from Firestore.', 'success');
      }
    } catch (e) {
      setStatus(`Load error: ${e instanceof Error ? e.message : String(e)}`, 'error');
    }
  }

  async function save() {
    const validationError = validateAllRewards();
    if (validationError) {
      setStatus(validationError, 'error');
      return;
    }

    setStatus('Saving...', '');
    try {
      await setDoc(doc(db, DOC_PATH), { rewards });
      setStatus('Saved successfully.', 'success');
    } catch (e) {
      setStatus(`Save error: ${e instanceof Error ? e.message : String(e)}`, 'error');
    }
  }

  typeSelect.innerHTML = REWARD_TYPES
    .map(type => `<option value="${type}">${type}</option>`)
    .join('');
  typeSelect.value = 'CURRENCY';

  rewardList.addEventListener('click', (event) => {
    const btn = (event.target as HTMLElement).closest('button[data-index]') as HTMLButtonElement | null;
    if (!btn?.dataset.index) return;

    const index = Number(btn.dataset.index);
    if (!Number.isInteger(index) || index < 0 || index >= rewards.length) return;

    rewards.splice(index, 1);
    renderRewardList();
    setStatus('Reward removed. Click Save to persist.', '');
  });

  addBtn.addEventListener('click', addRewardFromInput);
  saveBtn.addEventListener('click', save);

  renderRewardList();
  load();
}
