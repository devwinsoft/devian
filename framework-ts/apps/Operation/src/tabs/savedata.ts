import { EditorView, basicSetup } from 'codemirror';
import { json } from '@codemirror/lang-json';
import { oneDark } from '@codemirror/theme-one-dark';
import { encodeDvn, decodeDvn } from '../codec/dvn-codec';
import { obfuscate, deobfuscate } from '../codec/obfuscation-codec';

function createEditor(parent: HTMLElement): EditorView {
  return new EditorView({
    doc: '',
    extensions: [
      basicSetup,
      json(),
      oneDark,
      EditorView.theme({
        '&': { fontSize: '13px' },
        '.cm-scroller': { minHeight: '200px', maxHeight: '400px', overflow: 'auto' },
      }),
    ],
    parent,
  });
}

function getFileExt(filename: string): 'json' | 'dvn' | null {
  if (filename.endsWith('.json')) return 'json';
  if (filename.endsWith('.dvn')) return 'dvn';
  return null;
}

export function createSaveDataTab(container: HTMLElement) {
  const section = document.createElement('div');
  section.className = 'io-section';

  // File upload area
  const fileUpload = document.createElement('div');
  fileUpload.className = 'file-upload';
  fileUpload.innerHTML = `
    <div>Drop .json or .dvn file here or click to select</div>
    <div class="file-name" id="sd-filename"></div>
    <input type="file" id="sd-file" accept=".json,.dvn" />
  `;
  section.appendChild(fileUpload);

  // Import button row
  const importRow = document.createElement('div');
  importRow.className = 'btn-row';
  importRow.innerHTML = `<button class="btn" id="sd-import-btn">Import</button>`;
  section.appendChild(importRow);

  // Wrapper JSON editor
  const wrapperLabel = document.createElement('label');
  wrapperLabel.className = 'editor-label';
  wrapperLabel.textContent = 'Wrapper JSON (payload excluded)';
  section.appendChild(wrapperLabel);

  const wrapperContainer = document.createElement('div');
  wrapperContainer.className = 'editor-container';
  section.appendChild(wrapperContainer);

  // Payload JSON editor
  const payloadLabel = document.createElement('label');
  payloadLabel.className = 'editor-label';
  payloadLabel.textContent = 'Payload JSON (game state)';
  section.appendChild(payloadLabel);

  const payloadContainer = document.createElement('div');
  payloadContainer.className = 'editor-container';
  section.appendChild(payloadContainer);

  // Export button row
  const exportRow = document.createElement('div');
  exportRow.className = 'btn-row';
  exportRow.innerHTML = `<button class="btn" id="sd-export-btn">Export &amp; Download</button>`;
  section.appendChild(exportRow);

  // Status
  const status = document.createElement('div');
  status.className = 'status-msg';
  status.id = 'sd-status';
  section.appendChild(status);

  container.appendChild(section);

  // CodeMirror editors
  const wrapperEditor = createEditor(wrapperContainer);
  const payloadEditor = createEditor(payloadContainer);

  // File state
  let fileContent: string | null = null;
  let fileExt: 'json' | 'dvn' | null = null;

  const fileInput = section.querySelector<HTMLInputElement>('#sd-file')!;
  const fileNameEl = section.querySelector<HTMLElement>('#sd-filename')!;
  const importBtn = section.querySelector<HTMLButtonElement>('#sd-import-btn')!;
  const exportBtn = section.querySelector<HTMLButtonElement>('#sd-export-btn')!;

  // File upload - click
  fileUpload.addEventListener('click', () => fileInput.click());

  // File upload - drag & drop
  fileUpload.addEventListener('dragover', (e) => {
    e.preventDefault();
    fileUpload.classList.add('dragover');
  });
  fileUpload.addEventListener('dragleave', () => {
    fileUpload.classList.remove('dragover');
  });
  fileUpload.addEventListener('drop', (e) => {
    e.preventDefault();
    fileUpload.classList.remove('dragover');
    const file = e.dataTransfer?.files[0];
    if (file) loadFile(file);
  });

  // File input change
  fileInput.addEventListener('change', () => {
    const file = fileInput.files?.[0];
    if (file) loadFile(file);
  });

  function loadFile(file: File) {
    const ext = getFileExt(file.name);
    if (!ext) {
      setStatus('Unsupported file type. Use .json or .dvn', 'error');
      return;
    }

    const reader = new FileReader();
    reader.onload = () => {
      fileContent = reader.result as string;
      fileExt = ext;
      fileNameEl.textContent = file.name;
      exportBtn.textContent = `Export & Download .${ext}`;
      setStatus(`File loaded (.${ext}). Click [Import] to decode.`, 'success');
    };
    reader.onerror = () => {
      setStatus('Failed to read file.', 'error');
    };
    reader.readAsText(file);
  }

  // Import: file → 2-level decode
  importBtn.addEventListener('click', () => {
    if (!fileContent || !fileExt) {
      setStatus('No file loaded.', 'error');
      return;
    }

    try {
      // Level 1: get wrapper JSON string
      const jsonStr = fileExt === 'dvn'
        ? decodeDvn(fileContent)
        : fileContent;

      const wrapper = JSON.parse(jsonStr);

      // Extract payload
      const rawPayload = wrapper.payload;
      if (typeof rawPayload !== 'string') {
        throw new Error('payload field not found or not a string');
      }

      // Level 2: deobfuscate payload
      const payloadJson = deobfuscate(rawPayload);

      // Wrapper without payload → Editor 1
      const { payload: _, ...wrapperWithoutPayload } = wrapper;
      const wrapperFormatted = JSON.stringify(wrapperWithoutPayload, null, 2);
      wrapperEditor.dispatch({
        changes: { from: 0, to: wrapperEditor.state.doc.length, insert: wrapperFormatted },
      });

      // Decoded payload → Editor 2
      const payloadFormatted = JSON.stringify(JSON.parse(payloadJson), null, 2);
      payloadEditor.dispatch({
        changes: { from: 0, to: payloadEditor.state.doc.length, insert: payloadFormatted },
      });

      setStatus('Import complete.', 'success');
    } catch (e) {
      setStatus(`Decode error: ${e instanceof Error ? e.message : String(e)}`, 'error');
    }
  });

  // Export: 2-level encode → download
  exportBtn.addEventListener('click', () => {
    if (!fileExt) {
      setStatus('Import a file first to determine export format.', 'error');
      return;
    }

    const wrapperStr = wrapperEditor.state.doc.toString().trim();
    const payloadStr = payloadEditor.state.doc.toString().trim();

    if (!wrapperStr || !payloadStr) {
      setStatus('Both editors must have content.', 'error');
      return;
    }

    let wrapperObj: Record<string, unknown>;
    try {
      wrapperObj = JSON.parse(wrapperStr);
    } catch {
      setStatus('Invalid wrapper JSON.', 'error');
      return;
    }

    try {
      JSON.parse(payloadStr);
    } catch {
      setStatus('Invalid payload JSON.', 'error');
      return;
    }

    try {
      // Level 2: obfuscate payload
      wrapperObj.payload = obfuscate(payloadStr);

      // Level 1: format-specific encoding
      const fullJson = JSON.stringify(wrapperObj);
      const output = fileExt === 'dvn' ? encodeDvn(fullJson) : fullJson;

      downloadFile(output, fileExt);
      setStatus('Export complete. File downloaded.', 'success');
    } catch (e) {
      setStatus(`Encode error: ${e instanceof Error ? e.message : String(e)}`, 'error');
    }
  });

  function downloadFile(content: string, ext: string) {
    const timestamp = new Date().toISOString().replace(/[:.]/g, '-').slice(0, 19);
    const filename = `savedata_${timestamp}.${ext}`;
    const blob = new Blob([content], { type: 'application/octet-stream' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
  }

  function setStatus(msg: string, type: string) {
    status.textContent = msg;
    status.className = 'status-msg';
    if (type) status.classList.add(type);
  }
}
