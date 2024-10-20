/** @type {HTMLButtonElement} */
const toggleButton = document.getElementById("nav-drawer-toggle-button");
const navDrawer = document.getElementById("nav-drawer");
const drawerParent = navDrawer.parentElement;
const desktopQuery = window.matchMedia("(min-width: 960px)");

desktopQuery.addEventListener("change", ev => {
  const isOpen = navDrawer.classList.contains("mud-drawer--open");
  const hasBreakpoint = navDrawer.classList.contains("mud-drawer-md");

  if (ev.matches) {
    navDrawer.classList.add("mud-drawer-md");
    navDrawer.classList.remove("mud-drawer--closed");
    navDrawer.classList.add("mud-drawer--open");
    drawerParent.classList.remove("mud-drawer-closed-responsive-md-left");
    drawerParent.classList.add("mud-drawer-open-responsive-md-left");
  }
});

toggleButton.addEventListener("click", () => {
  const isDesktopWidth = desktopQuery.matches;
  const hasBreakpoint = navDrawer.classList.contains("mud-drawer-md");

  if (!isDesktopWidth && hasBreakpoint) {
      navDrawer.classList.remove("mud-drawer-md");
    return;
  }

  if (isDesktopWidth && !hasBreakpoint) {
    navDrawer.classList.add("mud-drawer-md");
  }

  navDrawer.classList.toggle("mud-drawer--open");
  navDrawer.classList.toggle("mud-drawer--closed");
  drawerParent.classList.toggle("mud-drawer-closed-responsive-md-left");
  drawerParent.classList.toggle("mud-drawer-open-responsive-md-left");
});