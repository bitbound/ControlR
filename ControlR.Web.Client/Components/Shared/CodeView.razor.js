const DARK_THEME_NAME = 'controlr-dark';
const LIGHT_THEME_NAME = 'controlr-light';

let _currentTheme = DARK_THEME_NAME;
let _themesDefined = false;

/**
 * Initializes a Monaco Editor instance in the given container element.
 * @param {HTMLElement} container - The container element for the editor
 * @param {string} content - Initial content
 * @param {string} language - Language identifier (csharp, powershell, log)
 * @param {boolean} isEditable - Whether the editor should be editable
 * @param {number} themeMode - 0 = Auto, 1 = Light, 2 = Dark
 */
export function initMonacoEditor(container, content, language, isEditable, themeMode) {
  if (!container) {
    console.warn('initMonacoEditor called with null container reference.');
    return;
  }

  container._monacoEditor = null;

  require.onLoadError = function (err) {
    console.error('Monaco loader error:', err);
  };

  require(['vs/editor/editor.main'], function () {
    if (!container) return;

    if (container._monacoEditor) {
      container._monacoEditor.dispose();
      container._monacoEditor = null;
    }

    container.innerHTML = '';

    registerLogLanguage();
    defineControlRThemes();

    _currentTheme =
      themeMode === 1 ? LIGHT_THEME_NAME :
      themeMode === 2 ? DARK_THEME_NAME :
      detectThemeMode();

    monaco.editor.setTheme(_currentTheme);

    const editor = monaco.editor.create(container, {
      value: content,
      language: language,
      readOnly: !isEditable,
      minimap: { enabled: true },
      scrollBeyondLastLine: false,
      automaticLayout: true,
      fontSize: 13,
      fontFamily: 'Consolas, "Courier New", monospace',
      wordWrap: 'on',
      lineNumbers: 'on',
      renderLineHighlight: 'none',
      hideCursorInOverviewRuler: true,
      overviewRulerLanes: 0,
      scrollbar: {
        vertical: 'auto',
        horizontal: 'auto',
        useShadows: false,
        verticalScrollbarSize: 10,
        horizontalScrollbarSize: 10
      },
      padding: { top: 8, bottom: 8 },
      lineDecorationsWidth: 0,
      lineNumbersMinChars: 3
    });

    container._monacoEditor = editor;

    editor.layout();

    if (content && content.length > 0) {
      scrollMonacoToBottom(container);
    }
  }, function (err) {
    console.error('Failed to load Monaco editor:', err);
  });
}

/**
 * Updates the content of a Monaco Editor instance.
 * @param {HTMLElement} container - The container element for the editor
 * @param {string} newContent - New content to set
 */
export function updateMonacoContent(container, newContent) {
  if (!container || !container._monacoEditor) {
    return;
  }

  const model = container._monacoEditor.getModel();
  if (model) {
    model.setValue(newContent);
  }
}

/**
 * Updates the Monaco editor theme based on a dark-mode flag computed by C#.
 * C# handles Auto → system preference, Light → false, Dark → true, so JS
 * just needs the resolved boolean value.
 * @param {number} themeMode - 0 = Auto, 1 = Light, 2 = Dark
 */
export function updateMonacoTheme(themeMode) {
  if (typeof monaco === 'undefined') return;

  defineControlRThemes();
  _currentTheme =
      themeMode === 1 ? LIGHT_THEME_NAME :
      themeMode === 2 ? DARK_THEME_NAME :
      detectThemeMode();
  monaco.editor.setTheme(_currentTheme);
}

/**
 * Scrolls the Monaco editor to the bottom of its content.
 * @param {HTMLElement} container - The container element for the editor
 */
export function scrollMonacoToBottom(container) {
  if (!container || !container._monacoEditor) {
    return;
  }

  const editor = container._monacoEditor;
  const model = editor.getModel();
  if (!model) return;

  const maxLine = model.getLineCount();
  if (maxLine > 0) {
    editor.setScrollTop(editor.getScrollHeight() - container.clientHeight);
  }
}

/**
 * Disposes a Monaco Editor instance to free resources.
 * @param {HTMLElement} container - The container element for the editor
 */
export function disposeMonacoEditor(container) {
  if (!container || !container._monacoEditor) {
    return;
  }

  container._monacoEditor.dispose();
  container._monacoEditor = null;
}

function registerLogLanguage() {
  if (typeof monaco === 'undefined') return;
  if (!monaco.languages.getLanguages().some(l => l.id === 'log')) {
    monaco.languages.register({ id: 'log' });
  }

  monaco.languages.setMonarchTokensProvider('log', {
    ignoreCase: true,
    tokenizer: {
      root: [
        [/^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}(?: [+-]\d{2}:\d{2})?/, 'log.date'],
        [/\[(?:trace|trc|debug|dbg|info|inf|notice|ntc)\]/, 'log.info'],
        [/\[(?:warn|wrn|warning)\]/, 'log.warning'],
        [/\[(?:err|error|fatal|fail|critical|crt)\]/, 'log.error'],
        [/^\s*--- End of inner exception stack trace ---$/, 'log.stack'],
        [/^\s*--->.*$/, 'log.stack'],
        [/^\s+at\s+.*$/, 'log.stack'],
        [/\b(?:[A-Za-z_][\w`+]*\.)+[A-Za-z_][\w`+]*(?:Exception|Error)\b(?::)?/, 'log.exception'],
        [/\{/, { token: 'delimiter.bracket', next: '@metadata' }],
        [/\b[A-Za-z][A-Za-z0-9_.-]*(?=:\s)/, 'log.meta.key'],
        [/"(?:[^"\\]|\\.)*"/, 'string'],
        [/[A-Za-z][A-Za-z0-9_.-]*:/, 'log.meta.key'],
        [/[-A-Za-z0-9_/.]+:\d+/, 'log.stack'],
        [/\b0x[0-9a-f]+\b/, 'number.hex'],
        [/\b\d+\b/, 'number']
      ],
      metadata: [
        [/\}/, { token: 'delimiter.bracket', next: '@pop' }],
        [/\b[A-Za-z][A-Za-z0-9_.-]*(?=:\s)/, 'log.meta.key'],
        [/"(?:[^"\\]|\\.)*"/, 'string'],
        [/\b(?:true|false|null)\b/, 'keyword'],
        [/\b\d+\b/, 'number'],
        [/,/, 'delimiter'],
        [/\s+/, 'white']
      ]
    }
  });
}

function defineControlRThemes() {
  if (typeof monaco === 'undefined') return;
  if (_themesDefined) return;
  _themesDefined = true;

  monaco.editor.defineTheme(DARK_THEME_NAME, {
    base: 'vs-dark',
    inherit: true,
    rules: [
      { token: 'log.info', foreground: '#4FC1FF' },
      { token: 'log.warning', foreground: '#DCDCAA' },
      { token: 'log.error', foreground: '#F44747' },
      { token: 'log.date', foreground: '#B5CEA8' },
      { token: 'log.exception', foreground: '#F48771' },
      { token: 'log.stack', foreground: '#F48771' },
      { token: 'log.meta', foreground: '#CCCCCC' },
      { token: 'log.meta.key', foreground: '#C586C0' }
    ],
    colors: {
      'editor.background': '#121212',
      'editor.foreground': '#dedede',
      'editorLineNumber.foreground': '#636363',
      'editorLineNumber.activeForeground': '#dedede',
      'editorCursor.foreground': '#dedede',
      'editor.selectionBackground': '#2a2a2a',
      'editor.inactiveSelectionBackground': '#1a1a1a',
      'editorWhitespace.foreground': '#2a2a2a',
      'editor.lineHighlightBackground': '#141414',
      'editorOverviewRuler.border': '#1a1a1a',
      'scrollbarSlider.background': '#2a2a2a',
      'scrollbarSlider.hoverBackground': '#3c3c3c',
      'scrollbarSlider.activeBackground': '#4f4f4f'
    }
  });

  monaco.editor.defineTheme(LIGHT_THEME_NAME, {
    base: 'vs',
    inherit: true,
    rules: [
      { token: 'log.info', foreground: '#007ACC' },
      { token: 'log.warning', foreground: '#795E26' },
      { token: 'log.error', foreground: '#CD3131' },
      { token: 'log.date', foreground: '#098658' },
      { token: 'log.exception', foreground: '#C72E0F' },
      { token: 'log.stack', foreground: '#C72E0F' },
      { token: 'log.meta', foreground: '#666666' },
      { token: 'log.meta.key', foreground: '#AF00DB' }
    ],
    colors: {
      'editor.background': '#FAFAFA',
      'editor.foreground': '#212121',
      'editorLineNumber.foreground': '#999999',
      'editorLineNumber.activeForeground': '#212121',
      'editorCursor.foreground': '#212121',
      'editor.selectionBackground': '#dcdcdc',
      'editor.inactiveSelectionBackground': '#e8e8e8',
      'editorWhitespace.foreground': '#dcdcdc',
      'editor.lineHighlightBackground': '#f5f5f5',
      'editorOverviewRuler.border': '#e8e8e8',
      'scrollbarSlider.background': '#dcdcdc',
      'scrollbarSlider.hoverBackground': '#cccccc',
      'scrollbarSlider.activeBackground': '#b3b3b3'
    }
  });
}

function detectThemeMode() {
  if (typeof window === 'undefined') return DARK_THEME_NAME;
  return window.matchMedia('(prefers-color-scheme: dark)').matches ? DARK_THEME_NAME : LIGHT_THEME_NAME;
}
