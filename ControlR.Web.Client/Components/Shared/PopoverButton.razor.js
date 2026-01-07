let onDisposed = () => { };

export function initialize(dotNetHelper, elementId) {
  const clickHandler = (event) => {
    const popoverWrapper = document.getElementById(elementId);
    if (!popoverWrapper) {
      return;
    }

    // Check if click is inside the wrapper (button)
    if (popoverWrapper.contains(event.target)) {
      return;
    }

    // MudBlazor renders popovers as siblings to the wrapper, so check all popovers
    const allPopovers = document.querySelectorAll('.mud-popover');
    for (const popover of allPopovers) {
      if (popover.contains(event.target)) {
        return;
      }
    }

    // Click was outside both button and popover, so close it
    dotNetHelper.invokeMethodAsync('ClosePopover');
  };

  document.addEventListener('click', clickHandler, true);
  console.debug('PopoverButton initialized with elementId:', elementId);
  onDisposed = () => {
    document.removeEventListener('click', clickHandler, true);
    console.debug('PopoverButton disposed with elementId:', elementId);
  }
}

export function dispose() {
  onDisposed();
}