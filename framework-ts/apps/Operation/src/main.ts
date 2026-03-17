import './style.css';
import { createPushSendTab } from './tabs/push-send';
import { createObfuscateTab } from './tabs/obfuscate';
import { createDeobfuscateTab } from './tabs/deobfuscate';
import { createSaveDataTab } from './tabs/savedata';

interface TabDef {
  id: string;
  label: string;
  pipeline: string;
  create: (container: HTMLElement) => void;
}

const tabs: TabDef[] = [
  {
    id: 'push-send',
    label: 'Push Send',
    pipeline: 'Pipeline: PUSH_REMOTE.json \u2192 Firebase Function \u2192 FCM topic send',
    create: createPushSendTab,
  },
  {
    id: 'obfuscate',
    label: 'Obfuscate',
    pipeline: 'Pipeline: byte-substitution obfuscation',
    create: createObfuscateTab,
  },
  {
    id: 'deobfuscate',
    label: 'Deobfuscate',
    pipeline: 'Pipeline: byte-substitution deobfuscation',
    create: createDeobfuscateTab,
  },
  {
    id: 'savedata',
    label: 'Save Data',
    pipeline: 'Pipeline: JSON \u2192 ComplexUtil.Encrypt_Base64 \u2192 version prefix \u2192 .dvn',
    create: createSaveDataTab,
  },
];

function init() {
  const app = document.getElementById('app')!;

  // Title
  const title = document.createElement('h1');
  title.className = 'app-title';
  title.textContent = 'Operation';
  app.appendChild(title);

  // Tab bar
  const tabBar = document.createElement('div');
  tabBar.className = 'tab-bar';
  app.appendChild(tabBar);

  // Pipeline
  const pipeline = document.createElement('div');
  pipeline.className = 'pipeline';
  app.appendChild(pipeline);

  // Content
  const content = document.createElement('div');
  content.className = 'tab-content';
  app.appendChild(content);

  // Create tab buttons
  const tabBtns: HTMLButtonElement[] = [];
  for (const tab of tabs) {
    const btn = document.createElement('button');
    btn.className = 'tab-btn';
    btn.textContent = tab.label;
    btn.dataset.tab = tab.id;
    tabBar.appendChild(btn);
    tabBtns.push(btn);
  }

  function switchTab(tabId: string) {
    const tab = tabs.find(t => t.id === tabId);
    if (!tab) return;

    // Update buttons
    for (const btn of tabBtns) {
      btn.classList.toggle('active', btn.dataset.tab === tabId);
    }

    // Update pipeline
    pipeline.textContent = tab.pipeline;

    // Update content
    content.innerHTML = '';
    tab.create(content);
  }

  // Tab click handler
  tabBar.addEventListener('click', (e) => {
    const btn = (e.target as HTMLElement).closest('.tab-btn') as HTMLButtonElement | null;
    if (btn?.dataset.tab) {
      switchTab(btn.dataset.tab);
    }
  });

  // Default tab
  switchTab(tabs[0].id);
}

init();
