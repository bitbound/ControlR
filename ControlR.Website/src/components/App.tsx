import "./App.tsx.css";
import { Routes, Route } from "react-router";
import Layout from "./Layout";
import Home from "./Home";
import Privacy from "./Privacy";

function App() {
  return (
    <Routes>
      <Route path="/" element={<Layout />}>
        <Route index element={<Home />} />
        <Route path="privacy" element={<Privacy />} />
      </Route>
    </Routes>
  );
}

export default App;
