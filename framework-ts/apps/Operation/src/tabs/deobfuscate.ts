import { deobfuscate } from '../codec/obfuscation-codec';

export function createDeobfuscateTab(container: HTMLElement) {
  const section = document.createElement('div');
  section.className = 'io-section';

  section.innerHTML = `
    <label>Input</label>
    <textarea id="deobf-input" placeholder="Enter obfuscated Base64 string..."></textarea>
    <div class="btn-row">
      <button class="btn" id="deobf-btn">Deobfuscate</button>
    </div>
    <label>Output</label>
    <textarea id="deobf-output" readonly placeholder="Plain text output..."></textarea>
    <div class="status-msg" id="deobf-status"></div>
  `;

  container.appendChild(section);

  const input = section.querySelector<HTMLTextAreaElement>('#deobf-input')!;
  const output = section.querySelector<HTMLTextAreaElement>('#deobf-output')!;
  const btn = section.querySelector<HTMLButtonElement>('#deobf-btn')!;
  const status = section.querySelector<HTMLElement>('#deobf-status')!;

  btn.addEventListener('click', () => {
    status.textContent = '';
    status.className = 'status-msg';

    const text = input.value;
    if (!text) {
      status.textContent = 'Input is empty.';
      status.classList.add('error');
      return;
    }

    try {
      output.value = deobfuscate(text);
      status.textContent = 'Done.';
      status.classList.add('success');
    } catch (e) {
      status.textContent = `Error: ${e instanceof Error ? e.message : String(e)}`;
      status.classList.add('error');
    }
  });
}
