declare module 'react' {
    namespace JSX {
        interface IntrinsicElements {
            "ms-store-badge": React.DetailedHTMLProps<MsStoreBadgeProps, HTMLElement>;
        }
    }
}

function MsStoreBadge(props: MsStoreBadgeProps) {
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