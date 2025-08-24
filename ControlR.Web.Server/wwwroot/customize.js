window.addEventListener("DOMContentLoaded", (event) => {
  const observer = new MutationObserver((mutations) => {
    mutations.forEach((mutation) => {
      mutation.addedNodes.forEach((node) => {
        if(node.nodeType !== Node.ELEMENT_NODE || node.id !== "components-reconnect-modal") 
        {
          return;
        }
        
        if (!node.shadowRoot){
          console.warn("No shadow root found on the reconnect modal.");
          return;
        }

        const customStyle = document.createElement("style");
        customStyle.textContent = `
          /* Custom styles for the reconnect modal */
          .components-reconnect-dialog {
            background-color: rgba(0, 0, 0, 0.9) !important;
            color: white !important;
          }
        `;
        node.shadowRoot.appendChild(customStyle);
      });
    });
  });

  observer.observe(document.body, {
    childList: true,
    subtree: true
  });
});