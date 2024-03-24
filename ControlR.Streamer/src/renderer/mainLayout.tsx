import { Component, CSSProperties, ReactNode } from "react";
import { iconImage512x512Base64 } from "./images";
import Home from "./pages/home";
import { BiHome, BiWrench, BiQuestionMark } from "react-icons/bi";
import { IconType } from "react-icons";
import { SessionIndicator } from "./components/sessionIndicator";

export class MainLayout extends Component<any, MainLayoutState> {
    constructor() {
        super(undefined);
        this.state = {
            activePage: appPages[0],
            initialized: false
        };
    }

    componentDidMount(): void {
        window.mainApi.getSessionId().then(sessionId => {
            this.setState({
                sessionId: sessionId ?? "",
                initialized: true
            })
        })
    }

    render(): ReactNode {
        if (!this.state.initialized) {
            return null;
        }

        if (this.state.sessionId) {
            return (
                <SessionIndicator />
            )
        }
        return this.renderFull();
    }

    renderFull(): ReactNode {
        return (
            <div style={layoutFrameCss}>
                <div style={sidebarCss}>
                    <div className="px-2 py-1 text-light" style={brandingCss}>
                        <div className="d-flex">
                            <img src={iconImage512x512Base64} style={{ width: "42px", height: "42px" }} />
                            <strong style={{ fontSize: "18px", marginTop: "10px", marginLeft: "5px" }}>
                                ControlR
                            </strong>
                        </div>
                        <div>
                            <span style={{ fontSize: "12px", marginLeft: "5px" }}>
                                Zero-trust remote control
                            </span>
                        </div>
                    </div>
                    <div className="px-2">
                        {
                            this.renderNavTabs()
                        }
                    </div>
                </div>
                <div style={{ padding: "1rem" }}>
                    {
                        this.renderActivePage()
                    }
                </div>
            </div>
        )
    }

    renderActivePage() {
        switch (this.state.activePage.title) {
            case "Home":
                return (<Home />);
            case "Settings":
                return (<div></div>);
            case "About":
                return (<div></div>);
            default:
                return undefined;
        }
    }
    renderIcon(icon: IconType) {
        icon({})
    }
    renderNavTabs() {
        return (
            appPages.map(x => {
                const isActive = x == this.state.activePage;
                let className = "btn p-3 my-2 d-block w-100";

                if (isActive) {
                    className += " btn-light text-dark";
                }
                else {
                    className += " btn-secondary";
                }

                return (
                    <button key={`nav-page-${x.title}`} className={className} onClick={(ev) => this.setState({ activePage: x })}>
                        <div className="d-flex align-middle">
                            {x.icon}
                            <span className="ms-2">
                                {x.title}
                            </span>
                        </div>
                    </button>
                )
            })
        )
    }
}


interface AppPage {
    title: string,
    icon: JSX.Element
}

const appPages = [
    {
        title: "Home",
        icon: <BiHome size={22} />
    },
    {
        title: "Settings",
        icon: <BiWrench size={22} />
    },
    {
        title: "About",
        icon: <BiQuestionMark size={22} />
    }
] as AppPage[];


const layoutFrameCss = {
    display: "grid",
    gridTemplateColumns: "auto 1fr",
    columnGap: "10px",
    userSelect: "none",
    height: "100%"
} as CSSProperties;

const brandingCss = {
    backgroundImage: "linear-gradient(145deg, rgb(30, 30, 30), #8bc34a 500%)",
} as CSSProperties;

const sidebarCss = {
    height: "100%",
    backgroundColor: "rgb(25,25,25)",
} as CSSProperties;

interface MainLayoutState {
    activePage: AppPage;
    sessionId?: string;
    initialized: boolean;
}

