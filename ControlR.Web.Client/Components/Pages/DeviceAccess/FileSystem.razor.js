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

export function downloadArchive(url, archiveFileName, targetPaths) {
  if (!Array.isArray(targetPaths) || targetPaths.length === 0) {
    throw new Error('At least one target path is required.');
  }

  const form = document.createElement('form');
  form.method = 'POST';
  form.action = url;
  form.style.display = 'none';

  appendHiddenField(form, 'archiveFileName', archiveFileName || 'controlr-download.zip');

  for (const targetPath of targetPaths) {
    appendHiddenField(form, 'targetPaths', targetPath);
  }

  document.body.appendChild(form);
  form.submit();
  document.body.removeChild(form);
}

function appendHiddenField(form, name, value) {
  const input = document.createElement('input');
  input.type = 'hidden';
  input.name = name;
  input.value = value;
  form.appendChild(input);
}
