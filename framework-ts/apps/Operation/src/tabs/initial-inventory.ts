import { db } from '../firebase';
import { doc, getDoc, setDoc } from 'firebase/firestore';

const DOC_PATH = 'config/initialInventory';
const ID_CATALOG_DOC_PATH = 'config/rewardIdCatalog';

const REWARD_TYPES = [
  'CARD',
  'CURRENCY',
  'EQUIP',
  'HERO',
  'RENTAL',
  'PASS',
] as const;

type RewardType = typeof REWARD_TYPES[number];

interface RewardRow {
  type: RewardType;
  id: string;
  amount: number;
}

interface RewardIdCatalog {
  currencyIds: string[];
  equipIds: string[];
  cardIds: string[];
  heroIds: string[];
}

interface RewardTypeMeta {
  amountGuide: string;
  interpretation: string;
}

const REWARD_TYPE_META: Record<RewardType, RewardTypeMeta> = {
  CURRENCY: {
    amountGuide: 'Amount는 잔고 증가 수량이다.',
    interpretation: 'Currency balance accumulates.',
  },
  EQUIP: {
    amountGuide: 'Amount만큼 장비 인스턴스를 생성한다.',
    interpretation: 'Create equipment instances by amount.',
  },
  CARD: {
    amountGuide: 'Amount만큼 카드 보유량을 누적한다.',
    interpretation: 'Card amount accumulates.',
  },
  HERO: {
    amountGuide: 'Amount만큼 영웅 수량(UNIT_AMOUNT)을 누적한다.',
    interpretation: 'Hero UNIT_AMOUNT accumulates.',
  },
  RENTAL: {
    amountGuide: '양수 amount면 RENTAL 활성화로 해석한다.',
    interpretation: 'Positive amount enables rental.',
  },
  PASS: {
    amountGuide: '양수 amount면 PASS 소유로 해석한다.',
    interpretation: 'Positive amount grants pass ownership.',
  },
};

function isRewardType(value: string): value is RewardType {
  return (REWARD_TYPES as readonly string[]).includes(value);
}

function getRewardTypeMeta(type: RewardType): RewardTypeMeta {
  return REWARD_TYPE_META[type];
}

function normalizeAmountByType(type: RewardType, amount: number): number {
  void type;
  return amount;
}

function normalizeRewardType(rawType: string): string {
  // Legacy compatibility: previously saved value
  if (rawType === 'SEASON_PASS') return 'PASS';
  return rawType;
}

function normalizeReward(raw: unknown): RewardRow | null {
  if (raw == null || typeof raw !== 'object') return null;

  const row = raw as Record<string, unknown>;
  const type = normalizeRewardType(String(row.type ?? '').trim().toUpperCase());
  const id = String(row.id ?? '').trim();
  const amount = Number(row.amount);

  if (!isRewardType(type)) return null;
  if (!id) return null;
  if (!Number.isInteger(amount) || amount <= 0) return null;

  return { type, id, amount: normalizeAmountByType(type, amount) };
}

function normalizeIdList(raw: unknown): string[] {
  if (!Array.isArray(raw)) return [];

  const dedup = new Set<string>();
  for (let i = 0; i < raw.length; i++) {
    const id = String(raw[i] ?? '').trim();
    if (id) dedup.add(id);
  }

  return Array.from(dedup).sort((a, b) => a.localeCompare(b));
}

function validateRewardRow(row: RewardRow, rowIndex: number): string | null {
  if (!isRewardType(row.type))
    return `Row ${rowIndex + 1}: invalid type.`;

  if (!row.id?.trim())
    return `Row ${rowIndex + 1}: id is empty.`;

  if (!Number.isInteger(row.amount) || row.amount <= 0)
    return `Row ${rowIndex + 1}: amount must be a positive integer.`;

  return null;
}

function getTypeIdOptions(type: RewardType, catalog: RewardIdCatalog): string[] {
  switch (type) {
    case 'CURRENCY':
      return catalog.currencyIds;
    case 'EQUIP':
      return catalog.equipIds;
    case 'CARD':
      return catalog.cardIds;
    case 'HERO':
      return catalog.heroIds;
    case 'RENTAL':
    case 'PASS':
    default:
      return [];
  }
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
            <th>Interpretation</th>
            <th class="reward-action-col">Action</th>
          </tr>
        </thead>
        <tbody id="ii-reward-list"></tbody>
      </table>
    </div>

    <label>Add Reward (RewardData)</label>
    <div class="reward-input-row">
      <select id="ii-type"></select>
      <select id="ii-id"></select>
      <input type="text" id="ii-id-text" placeholder="id" style="display:none;" />
      <input type="number" id="ii-amount" min="1" step="1" placeholder="amount" />
      <button class="btn reward-action-btn" id="ii-add" title="Add reward">+</button>
    </div>
    <div class="reward-interpret" id="ii-interpret"></div>
    <div class="reward-guide" id="ii-guide"></div>

    <div class="btn-row">
      <button class="btn btn-secondary" id="ii-import-catalog">Import Reward IDs</button>
      <button class="btn" id="ii-save">Save</button>
    </div>

    <div class="status-msg" id="ii-status"></div>
  `;

  container.appendChild(section);

  const rewardList = section.querySelector<HTMLTableSectionElement>('#ii-reward-list')!;
  const typeSelect = section.querySelector<HTMLSelectElement>('#ii-type')!;
  const idSelect = section.querySelector<HTMLSelectElement>('#ii-id')!;
  const idTextInput = section.querySelector<HTMLInputElement>('#ii-id-text')!;
  const amountInput = section.querySelector<HTMLInputElement>('#ii-amount')!;
  const addBtn = section.querySelector<HTMLButtonElement>('#ii-add')!;
  const importCatalogBtn = section.querySelector<HTMLButtonElement>('#ii-import-catalog')!;
  const saveBtn = section.querySelector<HTMLButtonElement>('#ii-save')!;
  const interpretation = section.querySelector<HTMLElement>('#ii-interpret')!;
  const guide = section.querySelector<HTMLElement>('#ii-guide')!;
  const status = section.querySelector<HTMLElement>('#ii-status')!;

  let rewards: RewardRow[] = [];
  let idCatalog: RewardIdCatalog = { currencyIds: [], equipIds: [], cardIds: [], heroIds: [] };
  let catalogState = '';
  let useManualIdInput = false;

  function setStatus(msg: string, type: 'success' | 'error' | '') {
    status.textContent = msg;
    status.className = 'status-msg';
    if (type) status.classList.add(type);
  }

  function tailLog(log: string, maxLines = 6): string {
    const lines = log
      .split(/\r?\n/)
      .map((line) => line.trim())
      .filter(Boolean)
      .map((line) => (line.length > 160 ? `${line.slice(0, 157)}...` : line));

    if (lines.length <= 0) return '';
    const joined = (
      lines.length <= maxLines
        ? lines.join(' | ')
        : `... ${lines.slice(-maxLines).join(' | ')}`
    );
    return joined.length > 600 ? `${joined.slice(0, 597)}...` : joined;
  }

  function renderRewardList() {
    if (rewards.length === 0) {
      rewardList.innerHTML = `
        <tr>
          <td colspan="5" class="reward-empty">No rewards configured.</td>
        </tr>
      `;
      return;
    }

    rewardList.innerHTML = rewards.map((row, index) => `
      <tr>
        <td>${row.type}</td>
        <td>${row.id}</td>
        <td>${row.amount}</td>
        <td>${getRewardTypeMeta(row.type).interpretation}</td>
        <td class="reward-action-col">
          <button class="btn btn-danger reward-action-btn" data-index="${index}" title="Delete reward">-</button>
        </td>
      </tr>
    `).join('');
  }

  function renderIdOptions(options: string[], disabledLabel: string) {
    if (options.length <= 0) {
      idSelect.innerHTML = `<option value="">${disabledLabel}</option>`;
      return;
    }

    idSelect.innerHTML = options
      .map((id) => `<option value="${id}">${id}</option>`)
      .join('');
  }

  function setManualIdInput(enabled: boolean, placeholder = 'id') {
    useManualIdInput = enabled;
    idSelect.style.display = enabled ? 'none' : '';
    idSelect.disabled = enabled;
    idTextInput.style.display = enabled ? '' : 'none';
    idTextInput.disabled = !enabled;
    idTextInput.placeholder = placeholder;
  }

  function getSelectedId(): string {
    return useManualIdInput ? idTextInput.value.trim() : idSelect.value.trim();
  }

  function syncInputForType() {
    const selected = typeSelect.value.trim().toUpperCase();
    const type: RewardType = isRewardType(selected) ? selected : 'CURRENCY';
    const meta = getRewardTypeMeta(type);
    const options = getTypeIdOptions(type, idCatalog);
    const requiresCatalog = type === 'CURRENCY' || type === 'EQUIP' || type === 'CARD' || type === 'HERO';

    interpretation.textContent = `Interpretation: ${meta.interpretation}`;

    amountInput.disabled = false;
    if (!amountInput.value.trim()) amountInput.value = '1';

    if (options.length <= 0) {
      if (requiresCatalog) {
        setManualIdInput(false);
        renderIdOptions([], 'No IDs imported');
        idSelect.disabled = true;
        addBtn.disabled = true;
        guide.textContent = `ID source: /config/rewardIdCatalog. Run reward-id import first. ${catalogState}`.trim();
      } else {
        setManualIdInput(true, `${type} ID`);
        addBtn.disabled = false;
        guide.textContent = `ID source: manual input (${type}). ${meta.amountGuide}`;
      }
      return;
    }

    setManualIdInput(false);
    renderIdOptions(options, 'No selectable ID');
    idSelect.disabled = false;
    addBtn.disabled = false;

    if (type === 'CURRENCY') {
      guide.textContent = 'ID source: /config/rewardIdCatalog (from ENUM_TYPES.json:CURRENCY_TYPE).';
      return;
    }

    guide.textContent = `ID source: /config/rewardIdCatalog (${type}). ${meta.amountGuide}`;
  }

  function addRewardFromInput() {
    const type = typeSelect.value.trim().toUpperCase();
    if (!isRewardType(type)) {
      setStatus('Invalid reward type.', 'error');
      return;
    }

    const id = getSelectedId();
    const rawAmount = Number(amountInput.value.trim());
    const amount = normalizeAmountByType(type, rawAmount);
    const row: RewardRow = { type, id, amount };

    const error = validateRewardRow(row, rewards.length);
    if (error) {
      setStatus(error, 'error');
      return;
    }

    rewards.push(row);
    renderRewardList();

    if (!amountInput.disabled) amountInput.value = '';
    syncInputForType();
    setStatus('Reward added. Click Save to persist.', '');
  }

  function validateAllRewards(): string | null {
    rewards = rewards.map((row) => ({
      ...row,
      amount: normalizeAmountByType(row.type, row.amount),
    }));

    for (let i = 0; i < rewards.length; i++) {
      const error = validateRewardRow(rewards[i], i);
      if (error) return error;
    }

    return null;
  }

  async function loadRewardIdCatalog() {
    try {
      const snap = await getDoc(doc(db, ID_CATALOG_DOC_PATH));
      if (!snap.exists()) {
        idCatalog = { currencyIds: [], equipIds: [], cardIds: [], heroIds: [] };
        catalogState = 'Catalog document not found.';
        return;
      }

      const data = snap.data();
      idCatalog = {
        currencyIds: normalizeIdList(data.currencyIds),
        equipIds: normalizeIdList(data.equipIds),
        cardIds: normalizeIdList(data.cardIds),
        heroIds: normalizeIdList(data.heroIds),
      };

      catalogState = `Catalog loaded: CUR=${idCatalog.currencyIds.length}, E=${idCatalog.equipIds.length}, C=${idCatalog.cardIds.length}, H=${idCatalog.heroIds.length}.`;
    } catch (e) {
      idCatalog = { currencyIds: [], equipIds: [], cardIds: [], heroIds: [] };
      catalogState = `Catalog load failed: ${e instanceof Error ? e.message : String(e)}`;
    }
  }

  async function load() {
    setStatus('Loading...', '');

    await loadRewardIdCatalog();

    try {
      const snap = await getDoc(doc(db, DOC_PATH));
      if (!snap.exists()) {
        rewards = [];
        renderRewardList();
        syncInputForType();
        setStatus(`No config found. Document does not exist yet. ${catalogState}`.trim(), '');
        return;
      }

      const rawRewards = snap.data().rewards;
      if (rawRewards == null) {
        rewards = [];
        renderRewardList();
        syncInputForType();
        setStatus(`Loaded. rewards is empty. ${catalogState}`.trim(), 'success');
        return;
      }

      if (!Array.isArray(rawRewards)) {
        rewards = [];
        renderRewardList();
        syncInputForType();
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
      syncInputForType();

      const messages: string[] = ['Loaded from Firestore.'];
      let type: 'success' | 'error' = 'success';
      if (invalidCount > 0) {
        messages.push(`${invalidCount} invalid row(s) skipped.`);
        type = 'error';
      }
      if (catalogState) {
        messages.push(catalogState);
      }

      setStatus(messages.join(' '), type);
    } catch (e) {
      setStatus(`Load error: ${e instanceof Error ? e.message : String(e)}`, 'error');
    }
  }

  async function importRewardIdCatalog() {
    setStatus('Importing reward ID catalog...', '');
    importCatalogBtn.disabled = true;

    try {
      const response = await fetch('/__operation/import-reward-id-catalog', {
        method: 'POST',
      });

      const rawBody = await response.text();
      let payload: {
        ok?: boolean;
        error?: string;
        stdout?: string;
        stderr?: string;
      } | null = null;

      try {
        payload = rawBody ? JSON.parse(rawBody) : null;
      } catch {
        payload = null;
      }

      if (!response.ok || payload?.ok !== true) {
        const errorLog = payload?.stderr || payload?.error || rawBody || response.statusText;
        setStatus(`Import error: ${tailLog(errorLog) || 'Unknown error.'}`, 'error');
        return;
      }

      await loadRewardIdCatalog();
      syncInputForType();

      const out = tailLog(payload.stdout ?? '');
      if (out) {
        setStatus(`Imported catalog. ${out}`, 'success');
      } else {
        setStatus('Imported catalog successfully.', 'success');
      }
    } catch (e) {
      setStatus(`Import error: ${e instanceof Error ? e.message : String(e)}`, 'error');
    } finally {
      importCatalogBtn.disabled = false;
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
    .map((type) => `<option value="${type}">${type}</option>`)
    .join('');
  typeSelect.value = 'CURRENCY';

  typeSelect.addEventListener('change', syncInputForType);

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
  importCatalogBtn.addEventListener('click', importRewardIdCatalog);
  saveBtn.addEventListener('click', save);

  renderRewardList();
  syncInputForType();
  load();
}
