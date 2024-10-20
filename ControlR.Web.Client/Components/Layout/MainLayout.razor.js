/** @type {HTMLButtonElement} */
const toggleButton = document.getElementById("nav-drawer-toggle-button");

toggleButton.addEventListener("click", () => {
  const currentLocation = new URL(location.href);
  const drawerSetOpen = currentLocation.searchParams.get("drawerOpen");
  const drawerDefaultOpen = window.matchMedia("(min-width: 960px)").matches;

  if (!drawerSetOpen) {
    currentLocation.searchParams.set("drawerOpen", `${!drawerDefaultOpen}`);
  }
  else {
    currentLocation.searchParams.delete("drawerOpen");
  }

  location.assign(currentLocation.href);
});