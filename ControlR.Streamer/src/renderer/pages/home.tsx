import { CSSProperties, useState } from "react";
import { FloatingLabel, Form } from "react-bootstrap"

interface HomeState {
  quickSessionId: string;
  password: string;
  partnerCode?: string;
}

export default function Home() {

  const [state, setState] = useState<HomeState>({
    quickSessionId: "Retrieving...",
    password: "Retrieving..."
  });


  return (
    <>
      <h3 className="text-info">
        Receive Support
      </h3>
      <p className="mt-3">
        Give the below session ID and password to your partner to let them remotely access your computer and provide support.
      </p>

      <div className="mt-3" style={sessionGridCss}>
        <div>
          <FloatingLabel label="Session ID">
            <Form.Control type="text" placeholder="Session ID" readOnly={true} value={state.quickSessionId} style={inputCss} />
          </FloatingLabel>

        </div>
        <div>
          <FloatingLabel label="Password">
            <Form.Control type="text" placeholder="Password" readOnly={true} value={state.password} style={inputCss} />
          </FloatingLabel>
        </div>
      </div>
    </>
  )
}


const sessionGridCss = {
  display: "grid",
  gridTemplateColumns: "1fr 1fr",
  columnGap: "15px"
} as CSSProperties;

const inputCss = {
  userSelect: "text"
} as CSSProperties;