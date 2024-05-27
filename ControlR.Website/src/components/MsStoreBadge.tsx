import { useEffect } from "react";

declare module 'react' {
    namespace JSX {
        interface IntrinsicElements {
            "ms-store-badge": React.DetailedHTMLProps<MsStoreBadgeProps, HTMLElement>;
        }
    }
}

function MsStoreBadge(props: MsStoreBadgeProps) {
    useEffect(() => {
        // CSS can't target shadow elements, so we have to
        // inject it after the component loads.
        const imgStyle = document.createElement('style');
        imgStyle.innerHTML = 'img { width: 200px !important; height: auto !important; }'
        const storeBadge = document.querySelector('ms-store-badge');

        if (!storeBadge?.shadowRoot) {
            console.warn("Store badge shadow element not found.");
            return;
        }

        storeBadge?.shadowRoot?.appendChild(imgStyle);
    }, []);
    
    return  <ms-store-badge
        productid={props.productid}
        window-mode={props["window-mode"]}
        theme={props.theme}
        language={props.language}
        animation={props.animation}>
    </ms-store-badge>
}

export default MsStoreBadge;

interface MsStoreBadgeProps extends React.HTMLAttributes<HTMLElement> {
    productid?: string;
    productname?: string;
    cid?: string;
    "window-mode": "direct" | "popup" | "full";
    theme: "dark" | "light",
    animation: "on" | "off",
    language: string;
}