import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import App from "./App.tsx";
import { setMarketTimeZone } from "./utils/clock";
import "./index.css";

async function loadMarketTimeZone() {
  try {
    const response = await fetch("/api/config");
    if (!response.ok) return;
    const body = (await response.json()) as { timeZoneId?: string };
    if (body.timeZoneId) setMarketTimeZone(body.timeZoneId);
  } catch {
    // The default zone stays in place when config is unreachable.
  }
}

void loadMarketTimeZone().finally(() => {
  createRoot(document.getElementById("root")!).render(
    <StrictMode>
      <App />
    </StrictMode>,
  );
});
