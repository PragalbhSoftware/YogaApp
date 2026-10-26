import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import App from "./App.tsx";
import { loadMarketTimeZone } from "./utils/clock";
import "./index.css";

void loadMarketTimeZone(AbortSignal.timeout(3000)).finally(() => {
  createRoot(document.getElementById("root")!).render(
    <StrictMode>
      <App />
    </StrictMode>,
  );
});
