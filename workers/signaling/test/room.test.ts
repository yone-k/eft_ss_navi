import { SELF } from "cloudflare:test";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

let roomId = "";
let roomSequence = 0;
const hostToken = "A".repeat(43);
const participantId = (value: number) => value.toString(16).padStart(32, "0");
const payload = "YWJj";

type ServerMessage =
  | { type: "host"; accepted: true }
  | { type: "offer"; participantId: string; payload: string }
  | { type: "answer"; participantId: string; payload: string }
  | { type: "reject"; reason: string }
  | { type: "error"; reason: string };

async function openSocket(): Promise<WebSocket> {
  const response = await SELF.fetch(`https://worker.test/rooms/${roomId}`, {
    headers: { Upgrade: "websocket" },
  });
  expect(response.status).toBe(101);
  const socket = response.webSocket;
  expect(socket).not.toBeNull();
  socket!.accept();
  return socket!;
}

function nextMessage(socket: WebSocket): Promise<ServerMessage> {
  return new Promise((resolve) => {
    socket.addEventListener(
      "message",
      (event) => resolve(JSON.parse(event.data as string) as ServerMessage),
      { once: true },
    );
  });
}

function nextClose(socket: WebSocket): Promise<CloseEvent> {
  return new Promise((resolve) => socket.addEventListener("close", resolve, { once: true }));
}

async function registerHost(token = hostToken): Promise<WebSocket> {
  const socket = await openSocket();
  const response = nextMessage(socket);
  socket.send(JSON.stringify({ type: "host", token }));
  await expect(response).resolves.toEqual({ type: "host", accepted: true });
  return socket;
}

async function registerParticipant(id: string): Promise<WebSocket> {
  const socket = await openSocket();
  socket.send(JSON.stringify({ type: "join", participantId: id }));
  return socket;
}

async function confirmParticipant(host: WebSocket, socket: WebSocket, id: string): Promise<void> {
  const forwarded = nextMessage(host);
  socket.send(JSON.stringify({ type: "offer", participantId: id, payload }));
  await forwarded;
}

afterEach(() => {
  vi.restoreAllMocks();
});

beforeEach(() => {
  roomSequence += 1;
  roomId = roomSequence.toString(16).padStart(64, "0");
});

describe("room durable object", () => {
  it("should reject offer as the first socket message", async () => {
    // Given: A WebSocket without an assigned role.
    const socket = await openSocket();
    const response = nextMessage(socket);

    // When: It attempts to send an offer before join.
    socket.send(JSON.stringify({ type: "offer", participantId: participantId(1), payload }));

    // Then: Role validation rejects the message.
    await expect(response).resolves.toEqual({ type: "error", reason: "InvalidMessage" });
  });

  it("should reject join immediately when no host exists", async () => {
    // Given: A room without a registered host.
    const participant = await openSocket();
    const response = nextMessage(participant);
    const closed = nextClose(participant);

    // When: A participant joins.
    participant.send(JSON.stringify({ type: "join", participantId: participantId(1) }));

    // Then: HostNotFound is returned immediately.
    await expect(response).resolves.toEqual({ type: "reject", reason: "HostNotFound" });
    await expect(closed).resolves.toMatchObject({ code: 1000 });
  });

  it("should retain first host ownership when another host registers", async () => {
    // Given: The room already has an accepted host.
    const firstHost = await registerHost();
    const secondHost = await openSocket();
    const rejection = nextMessage(secondHost);

    // When: A second host attempts registration.
    secondHost.send(JSON.stringify({ type: "host", token: "B".repeat(43) }));

    // Then: It is rejected while the first host still receives offers.
    await expect(rejection).resolves.toEqual({ type: "reject", reason: "HostExists" });
    const offer = nextMessage(firstHost);
    const participant = await registerParticipant(participantId(2));
    participant.send(JSON.stringify({ type: "offer", participantId: participantId(2), payload }));
    await expect(offer).resolves.toEqual({
      type: "offer",
      participantId: participantId(2),
      payload,
    });
  });

  it("should reject malformed participant id", async () => {
    // Given: A room with a host and a new participant socket.
    await registerHost();
    const participant = await openSocket();
    const response = nextMessage(participant);
    const closed = nextClose(participant);

    // When: Join contains a non-canonical participant id.
    participant.send(JSON.stringify({ type: "join", participantId: "INVALID" }));

    // Then: The message is rejected as invalid.
    await expect(response).resolves.toEqual({ type: "error", reason: "InvalidMessage" });
    await expect(closed).resolves.toMatchObject({ code: 1000 });
  });

  it("should reject duplicate active participant id", async () => {
    // Given: A participant id is already connected to a hosted room.
    const host = await registerHost();
    const original = await registerParticipant(participantId(3));
    await confirmParticipant(host, original, participantId(3));
    const duplicate = await openSocket();
    const response = nextMessage(duplicate);

    // When: A second socket joins with the same id.
    duplicate.send(JSON.stringify({ type: "join", participantId: participantId(3) }));

    // Then: The duplicate identity is rejected.
    await expect(response).resolves.toEqual({ type: "error", reason: "InvalidMessage" });
  });

  it("should reject fifth concurrent guest connection", async () => {
    // Given: Four guest signaling sockets are active.
    const host = await registerHost();
    for (let index = 1; index <= 4; index += 1) {
      const id = participantId(index + 10);
      const participant = await registerParticipant(id);
      await confirmParticipant(host, participant, id);
    }
    const fifth = await openSocket();
    const response = nextMessage(fifth);

    // When: A fifth guest joins.
    fifth.send(JSON.stringify({ type: "join", participantId: participantId(20) }));

    // Then: Capacity is returned without replacing active guests.
    await expect(response).resolves.toEqual({ type: "reject", reason: "Capacity" });
  });

  it("should rate limit twenty-first syntactically valid join in sixty seconds", async () => {
    // Given: A hosted room has received twenty valid joins in the current window.
    const host = await registerHost();
    for (let index = 1; index <= 20; index += 1) {
      const socket = await openSocket();
      const response = index > 4 ? nextMessage(socket) : undefined;
      const id = participantId(index + 30);
      socket.send(JSON.stringify({ type: "join", participantId: id }));
      if (response) {
        await expect(response).resolves.toEqual({ type: "reject", reason: "Capacity" });
      } else {
        await confirmParticipant(host, socket, id);
      }
    }
    const limited = await openSocket();
    const response = nextMessage(limited);

    // When: The twenty-first valid join is sent.
    limited.send(JSON.stringify({ type: "join", participantId: participantId(99) }));

    // Then: RateLimited takes precedence over capacity.
    await expect(response).resolves.toEqual({ type: "reject", reason: "RateLimited" });
  });

  it("should reject text frame larger than 64 KiB", async () => {
    // Given: An unregistered room socket.
    const socket = await openSocket();
    const response = nextMessage(socket);

    // When: It sends a frame over 65,536 UTF-8 bytes.
    socket.send("x".repeat(65_537));

    // Then: The payload-specific error is returned.
    await expect(response).resolves.toEqual({ type: "error", reason: "PayloadTooLarge" });
  });

  it("should reject answer whose token does not own the room", async () => {
    // Given: A host and target participant are connected.
    const host = await registerHost();
    const participant = await registerParticipant(participantId(70));
    await confirmParticipant(host, participant, participantId(70));
    const response = nextMessage(host);

    // When: The host connection supplies a different ownership token.
    host.send(JSON.stringify({
      type: "answer",
      token: "B".repeat(43),
      participantId: participantId(70),
      payload,
    }));

    // Then: The answer is not forwarded.
    await expect(response).resolves.toEqual({ type: "error", reason: "Unauthorized" });
  });

  it("should route offer and answer then close participant normally", async () => {
    // Given: A host and participant are registered in the same room.
    const host = await registerHost();
    const participant = await registerParticipant(participantId(80));

    // When: Participant offers and host answers for that identity.
    const receivedOffer = nextMessage(host);
    participant.send(JSON.stringify({
      type: "offer",
      participantId: participantId(80),
      payload,
    }));
    await expect(receivedOffer).resolves.toEqual({
      type: "offer",
      participantId: participantId(80),
      payload,
    });
    const receivedAnswer = nextMessage(participant);
    const closed = nextClose(participant);
    host.send(JSON.stringify({
      type: "answer",
      token: hostToken,
      participantId: participantId(80),
      payload,
    }));

    // Then: Only the target gets the answer and closes with code 1000.
    await expect(receivedAnswer).resolves.toEqual({
      type: "answer",
      participantId: participantId(80),
      payload,
    });
    await expect(closed).resolves.toMatchObject({ code: 1000 });
  });

  it("should close every guest with code 1001 when host disconnects", async () => {
    // Given: Two participants are waiting under one host.
    const host = await registerHost();
    const first = await registerParticipant(participantId(90));
    const second = await registerParticipant(participantId(91));
    const firstOffer = nextMessage(host);
    first.send(JSON.stringify({ type: "offer", participantId: participantId(90), payload }));
    await firstOffer;
    const secondOffer = nextMessage(host);
    second.send(JSON.stringify({ type: "offer", participantId: participantId(91), payload }));
    await secondOffer;
    const firstClosed = nextClose(first);
    const secondClosed = nextClose(second);

    // When: The owning host disconnects.
    host.close(1000, "host ended");

    // Then: Every participant is discarded as a room shutdown.
    await expect(firstClosed).resolves.toMatchObject({ code: 1001 });
    await expect(secondClosed).resolves.toMatchObject({ code: 1001 });
  });

  it("should not write sensitive signaling data to application console", async () => {
    // Given: Every application console method is observed.
    const spies = ["log", "debug", "info", "warn", "error"].map((method) =>
      vi.spyOn(console, method as "log").mockImplementation(() => undefined),
    );
    const host = await registerHost();
    const participant = await registerParticipant(participantId(100));

    // When: An encrypted signaling payload is relayed.
    participant.send(JSON.stringify({
      type: "offer",
      participantId: participantId(100),
      payload: "c2Vuc2l0aXZlLXBheWxvYWQ=",
    }));
    await nextMessage(host);

    // Then: No room, identity, IP, token, or payload is logged.
    expect(spies.every((spy) => spy.mock.calls.length === 0)).toBe(true);
  });
});
