/**
 * Gets the text content of a DOM element. 
 * @param {HTMLElement} element
 */
export function getElementText(element) {
  if (!element) {
    return null;
  }

  return element.textContent;
}