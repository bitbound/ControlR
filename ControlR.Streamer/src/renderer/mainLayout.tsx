import { CSSProperties, useEffect, useState } from "react";
import appicon from "../../assets/appicon.png";
import Home from "./pages/home";
import { BiWrench, BiHelpCircle, BiInfoCircle } from "react-icons/bi";
import SessionIndicator from "./components/sessionIndicator";
import Settings from "./pages/settings";
import About from "./pages/about";
import ConnectionStatusIcon from "./components/connectionStatusIcon";

export default function MainLayout() {
  const [state, setState] = useState({
    sessionId: "",
    initialized: false,
  });

  useEffect(() => {
    (async () => {
      const sessionId = await window.mainApi.getViewerConnectionId();
      setState({
        sessionId: sessionId,
        initialized: true,
      });
    })();
  });

  if (!state.initialized) {
    return <></>;
  }

  if (state.sessionId) {
    return <SessionIndicator />;
  }

  return <FullLayout />;
}

const FullLayout = () => {
  const [state, setState] = useState<MainLayoutState>({
    activePage: appPages[0],
  });

  function renderActivePage() {
    switch (state.activePage.title) {
      case "Get Help":
        return <Home />;
      case "Settings":
        return <Settings />;
      case "About":
        return <About />;
      default:
        return undefined;
    }
  }

  function renderNavTabs() {
    return appPages.map((x) => {
      const isActive = x == state.activePage;
      let className = "btn p-3 my-2 d-block w-100";

      if (isActive) {
        className += " btn-info";
      } else {
        className += " btn-dark";
      }

      return (
        <button
          key={`nav-page-${x.title}`}
          className={className}
          onClick={() => setState({ activePage: x })}
        >
          <div className="d-flex align-middle">
            {x.icon}
            <span className="ms-2">{x.title}</span>
          </div>
        </button>
      );
    });
  }

  return (
    <div style={layoutFrameCss}>
      <div style={sidebarCss}>
        <div className="px-2 py-1 text-light" style={brandingCss}>
          <div className="d-flex">
            <img src={appicon} style={{ width: "42px", height: "42px" }} />
            <strong
              style={{
                fontSize: "18px",
                marginTop: "10px",
                marginLeft: "5px",
              }}
            >
              ControlR
            </strong>
          </div>
          <div>
            <span style={{ fontSize: "12px", marginLeft: "5px" }}>
              Zero-trust remote control
            </span>
          </div>
        </div>
        <div className="px-2">{renderNavTabs()}</div>
        <div>
          <ConnectionStatusIcon />
        </div>
      </div>
      <div style={{ padding: "1rem" }}>{renderActivePage()}</div>
    </div>
  );
};

interface AppPage {
  title: "Get Help" | "Settings" | "About";
  icon: JSX.Element;
}

const appPages = [
  {
    title: "Get Help",
    icon: <BiHelpCircle size={22} />,
  },
  {
    title: "Settings",
    icon: <BiWrench size={22} />,
  },
  {
    title: "About",
    icon: <BiInfoCircle size={22} />,
  },
] as AppPage[];

const layoutFrameCss = {
  display: "grid",
  gridTemplateColumns: "auto 1fr",
  columnGap: "10px",
  userSelect: "none",
  height: "100%",
} as CSSProperties;

const brandingCss = {
  backgroundColor: "rgb(20, 20, 20)",
} as CSSProperties;

const sidebarCss = {
  display: "grid",
  gridTemplateRows: "auto 1fr auto",
  height: "100%",
  backgroundColor: "rgb(25,25,25)",
} as CSSProperties;

interface MainLayoutState {
  activePage: AppPage;
}
