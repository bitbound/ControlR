import { useEffect, useState } from "react";
import { BiSolidBolt } from "react-icons/bi";

export default function ConnectionStatusIcon() {
  const [state, setState] = useState({
    textClass: "text-danger",
    statusMessage: "Connecting...",
  });

  return (
    <div className="p-2">
      <BiSolidBolt className={state.textClass}></BiSolidBolt>
      <small>{state.statusMessage}</small>
    </div>
  );
}
