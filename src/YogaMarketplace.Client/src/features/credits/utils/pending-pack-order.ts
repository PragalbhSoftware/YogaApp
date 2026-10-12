const storageKey = "yoga.pendingPackOrder";
const maxAgeMs = 24 * 60 * 60 * 1000;

type PendingPackOrder = { orderId: string; userId: string; savedAt: number };

/**
 * A paid pack order the server hasn't confirmed yet, kept in this tab so a refresh resumes confirming instead of
 * inviting a second payment. Scoped to the signed-in user and dropped after a day.
 */
export function readPendingPackOrder(userId: string | undefined, now = Date.now()): string | null {
  if (!userId) return null;
  try {
    const raw = sessionStorage.getItem(storageKey);
    if (!raw) return null;
    const saved = JSON.parse(raw) as Partial<PendingPackOrder>;
    const fresh = typeof saved.savedAt === "number" && now - saved.savedAt < maxAgeMs;
    if (typeof saved.orderId === "string" && saved.userId === userId && fresh) return saved.orderId;
  } catch {
    // Unreadable storage behaves like no pending order.
  }
  clearPendingPackOrder();
  return null;
}

export function savePendingPackOrder(orderId: string, userId: string, now = Date.now()) {
  try {
    sessionStorage.setItem(storageKey, JSON.stringify({ orderId, userId, savedAt: now } satisfies PendingPackOrder));
  } catch {
    // Without storage a refresh simply loses the confirming banner; the purchase itself is unaffected.
  }
}

export function clearPendingPackOrder() {
  try {
    sessionStorage.removeItem(storageKey);
  } catch {
    // Nothing to clear.
  }
}
