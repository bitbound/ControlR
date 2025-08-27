let isResizing = false;
let startX = 0;
let startWidth = 0;
let container = null;
let leftPanel = null;
let rightPanel = null;

export function initializeGridSplitter(containerRef, splitterRef, leftPanelRef, rightPanelRef) {
    container = containerRef;
    leftPanel = leftPanelRef;
    rightPanel = rightPanelRef;
    
    splitterRef.addEventListener('mousedown', startResize);
    splitterRef.addEventListener('touchstart', startResize);
    
    // Prevent text selection during resize
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
    
    // Add classes for visual feedback
    document.body.classList.add('is-resizing');
    container.classList.add('is-resizing');
    
    e.preventDefault();
}

function doResize(e) {
    if (!isResizing) return;
    
    const clientX = e.clientX || e.touches[0].clientX;
    const deltaX = clientX - startX;
    const newWidth = startWidth + deltaX;
    
    // Set minimum and maximum widths
    const minWidth = 200;
    const maxWidth = container.offsetWidth - 300; // Leave at least 300px for right panel
    
    if (newWidth >= minWidth && newWidth <= maxWidth) {
        // Update the grid template columns
        const rightWidth = container.offsetWidth - newWidth - 5; // 5px for splitter
        container.querySelector('.file-system-grid').style.gridTemplateColumns = `${newWidth}px 5px 1fr`;
    }
    
    e.preventDefault();
}

function stopResize() {
    isResizing = false;
    
    document.removeEventListener('mousemove', doResize);
    document.removeEventListener('mouseup', stopResize);
    document.removeEventListener('touchmove', doResize);
    document.removeEventListener('touchend', stopResize);
    
    // Remove classes
    document.body.classList.remove('is-resizing');
    container.classList.remove('is-resizing');
}

function preventSelection(e) {
    if (isResizing) {
        e.preventDefault();
        return false;
    }
}

// Clean up event listeners when component is disposed
export function dispose() {
    document.removeEventListener('selectstart', preventSelection);
    if (container) {
        container.removeEventListener('mousemove', doResize);
        container.removeEventListener('mouseup', stopResize);
        container.removeEventListener('touchmove', doResize);
        container.removeEventListener('touchend', stopResize);
    }
}

export function triggerFileInput(fileInputElement) {
    if (fileInputElement) {
        fileInputElement.click();
    }
}

export function downloadFile(url, filename) {
    const link = document.createElement('a');
    link.href = url;
    link.download = filename || '';
    link.style.display = 'none';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
}
