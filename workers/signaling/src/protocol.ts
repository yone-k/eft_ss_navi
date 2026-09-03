export const MAX_MESSAGE_BYTES = 65_536;
export const RATE_LIMIT_WINDOW_MS = 60_000;
export const MAX_JOINS_PER_WINDOW = 20;

export type ClientMessage =
  | { type: "host"; token: string }
  | { type: "join"; participantId: string }
  | { type: "offer"; participantId: string; payload: string }
  | { type: "answer"; token: string; participantId: string; payload: string };

export type ProtocolErrorReason = "InvalidMessage" | "PayloadTooLarge";

export type ParseResult =
  | { ok: true; message: ClientMessage }
  | { ok: false; reason: ProtocolErrorReason };

const participantIdPattern = /^[0-9a-f]{32}$/;
const hostTokenPattern = /^[A-Za-z0-9_-]{43}$/;
const base64Pattern = /^(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?$/;

export function parseClientMessage(frame: string | ArrayBuffer): ParseResult {
  if (typeof frame !== "string") {
    return { ok: false, reason: "InvalidMessage" };
  }

  if (new TextEncoder().encode(frame).byteLength > MAX_MESSAGE_BYTES) {
    return { ok: false, reason: "PayloadTooLarge" };
  }

  let value: unknown;
  try {
    value = JSON.parse(frame);
  } catch {
    return { ok: false, reason: "InvalidMessage" };
  }

  if (!isRecord(value) || typeof value.type !== "string") {
    return { ok: false, reason: "InvalidMessage" };
  }

  switch (value.type) {
    case "host":
      return isHostToken(value.token)
        ? { ok: true, message: { type: "host", token: value.token } }
        : { ok: false, reason: "InvalidMessage" };
    case "join":
      return isParticipantId(value.participantId)
        ? { ok: true, message: { type: "join", participantId: value.participantId } }
        : { ok: false, reason: "InvalidMessage" };
    case "offer":
      return isParticipantId(value.participantId) && isBase64(value.payload)
        ? {
            ok: true,
            message: { type: "offer", participantId: value.participantId, payload: value.payload },
          }
        : { ok: false, reason: "InvalidMessage" };
    case "answer":
      return isHostToken(value.token)
        && isParticipantId(value.participantId)
        && isBase64(value.payload)
        ? {
            ok: true,
            message: {
              type: "answer",
              token: value.token,
              participantId: value.participantId,
              payload: value.payload,
            },
          }
        : { ok: false, reason: "InvalidMessage" };
    default:
      return { ok: false, reason: "InvalidMessage" };
  }
}

export function isJoinAllowed(
  timestamps: readonly number[],
  now: number,
): { allowed: boolean; timestamps: number[] } {
  const recent = timestamps.filter((timestamp) => now - timestamp < RATE_LIMIT_WINDOW_MS);
  const allowed = recent.length < MAX_JOINS_PER_WINDOW;
  return {
    allowed,
    timestamps: [...recent, now].slice(-MAX_JOINS_PER_WINDOW),
  };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function isParticipantId(value: unknown): value is string {
  return typeof value === "string" && participantIdPattern.test(value);
}

function isHostToken(value: unknown): value is string {
  return typeof value === "string" && hostTokenPattern.test(value);
}

function isBase64(value: unknown): value is string {
  if (typeof value !== "string" || value.length === 0 || !base64Pattern.test(value)) {
    return false;
  }

  try {
    atob(value);
    return true;
  } catch {
    return false;
  }
}
