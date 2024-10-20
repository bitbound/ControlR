/** @type {HTMLButtonElement} */
const toggleButton = document.getElementById("nav-drawer-toggle-button");

toggleButton.addEventListener("click", () => {
  const currentLocation = new URL(location.href);
  const drawerOpen = currentLocation.searchParams.get("drawerOpen");

  if (!drawerOpen) {
    currentLocation.searchParams.set("drawerOpen", "true");
  }
  else {
    if (drawerOpen === "true") {
      currentLocation.searchParams.set("drawerOpen", "false");
    }
    else {
      currentLocation.searchParams.set("drawerOpen", "true");
    }
  }
  location.assign(currentLocation.href);
});