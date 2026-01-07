let isResizing = false;
let startX = 0;
let startWidth = 0;
let container = null;
let leftPanel = null;
let rightPanel = null;
let minLeftWidth = 200;
let minRightWidth = 300;

export function initializeGridSplitter(containerRef, splitterRef, leftPanelRef, rightPanelRef, minLeft, minRight) {
  container = containerRef;
  leftPanel = leftPanelRef;
  rightPanel = rightPanelRef;
  minLeftWidth = minLeft || 200;
  minRightWidth = minRight || 300;

  splitterRef.addEventListener('mousedown', startResize);
  splitterRef.addEventListener('touchstart', startResize);

  document.addEventListener('selectstart', preventSelection);
}

function startResize(e) {
  isResizing = true;
  startX = e.clientX || e.touches[0].clientX;
  startWidth = leftPanel.offsetWidth;

  document.addEventListener('mousemove', doResize);
  document.addEventListener('mouseup', stopResize);
  document.addEventListener('touchmove', doResize);
  document.addEventListener('touchend', stopResize);

  document.body.classList.add('is-resizing');
  container.classList.add('is-resizing');

  e.preventDefault();
}

function doResize(e) {
  if (!isResizing) return;

  const clientX = e.clientX || e.touches[0].clientX;
  const deltaX = clientX - startX;
  const newWidth = startWidth + deltaX;

  const maxWidth = container.offsetWidth - minRightWidth;

  if (newWidth >= minLeftWidth && newWidth <= maxWidth) {
    const grid = container.querySelector('.grid-splitter-grid');
    const splitterWidth = parseInt(getComputedStyle(grid).gridTemplateColumns.split(' ')[1]);
    grid.style.gridTemplateColumns = `${newWidth}px ${splitterWidth}px 1fr`;
  }

  e.preventDefault();
}

function stopResize() {
  isResizing = false;

  document.removeEventListener('mousemove', doResize);
  document.removeEventListener('mouseup', stopResize);
  document.removeEventListener('touchmove', doResize);
  document.removeEventListener('touchend', stopResize);

  document.body.classList.remove('is-resizing');
  container.classList.remove('is-resizing');
}

function preventSelection(e) {
  if (isResizing) {
    e.preventDefault();
    return false;
  }
}

export function dispose() {
  document.removeEventListener('selectstart', preventSelection);
  if (container) {
    container.removeEventListener('mousemove', doResize);
    container.removeEventListener('mouseup', stopResize);
    container.removeEventListener('touchmove', doResize);
    container.removeEventListener('touchend', stopResize);
  }
}
