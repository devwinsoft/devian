import { obfuscate } from '../codec/obfuscation-codec';

export function createObfuscateTab(container: HTMLElement) {
  const section = document.createElement('div');
  section.className = 'io-section';

  section.innerHTML = `
    <label>Input</label>
    <textarea id="obf-input" placeholder="Enter plain text (JSON string)..."></textarea>
    <div class="btn-row">
      <button class="btn" id="obf-btn">Obfuscate</button>
    </div>
    <label>Output</label>
    <textarea id="obf-output" readonly placeholder="Obfuscated Base64 output..."></textarea>
    <div class="status-msg" id="obf-status"></div>
  `;

  container.appendChild(section);

  const input = section.querySelector<HTMLTextAreaElement>('#obf-input')!;
  const output = section.querySelector<HTMLTextAreaElement>('#obf-output')!;
  const btn = section.querySelector<HTMLButtonElement>('#obf-btn')!;
  const status = section.querySelector<HTMLElement>('#obf-status')!;

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
      output.value = obfuscate(text);
      status.textContent = 'Done.';
      status.classList.add('success');
    } catch (e) {
      status.textContent = `Error: ${e instanceof Error ? e.message : String(e)}`;
      status.classList.add('error');
    }
  });
}
