import { DurableObject } from "cloudflare:workers";
import type { Env } from "./index";
import { isJoinAllowed, parseClientMessage, type ClientMessage } from "./protocol";

type SocketAttachment =
  | { role: "unregistered" }
  | { role: "host"; token: string }
  | { role: "participant"; participantId: string };

type RejectReason = "HostNotFound" | "HostExists" | "Capacity" | "RateLimited";
type ErrorReason = "InvalidMessage" | "PayloadTooLarge" | "Unauthorized";

const JOIN_TIMESTAMPS_KEY = "join-timestamps";
const MAX_GUESTS = 4;

export class RoomDurableObject extends DurableObject<Env> {
  fetch(request: Request): Response {
    if (request.headers.get("Upgrade")?.toLowerCase() !== "websocket") {
      return new Response(null, { status: 426 });
    }

    const pair = new WebSocketPair();
    const [client, server] = Object.values(pair);
    server.serializeAttachment({ role: "unregistered" } satisfies SocketAttachment);
    this.ctx.acceptWebSocket(server);
    return new Response(null, { status: 101, webSocket: client });
  }

  async webSocketMessage(socket: WebSocket, frame: string | ArrayBuffer): Promise<void> {
    const parsed = parseClientMessage(frame);
    if (!parsed.ok) {
      this.sendErrorAndClose(socket, parsed.reason);
      return;
    }

    const attachment = this.getAttachment(socket);
    if (attachment.role === "unregistered") {
      await this.handleRegistration(socket, parsed.message);
      return;
    }

    if (attachment.role === "host" && parsed.message.type === "answer") {
      this.handleAnswer(socket, attachment, parsed.message);
      return;
    }

    if (attachment.role === "participant" && parsed.message.type === "offer") {
      this.handleOffer(socket, attachment, parsed.message);
      return;
    }

    this.sendErrorAndClose(socket, "InvalidMessage");
  }

  async webSocketClose(socket: WebSocket): Promise<void> {
    await this.releaseSocket(socket);
  }

  async webSocketError(socket: WebSocket): Promise<void> {
    await this.releaseSocket(socket);
  }

  private async handleRegistration(socket: WebSocket, message: ClientMessage): Promise<void> {
    if (message.type === "host") {
      this.registerHost(socket, message.token);
      return;
    }

    if (message.type === "join") {
      await this.registerParticipant(socket, message.participantId);
      return;
    }

    this.sendErrorAndClose(socket, "InvalidMessage");
  }

  private registerHost(socket: WebSocket, token: string): void {
    if (this.findHost() !== undefined) {
      this.sendRejectAndClose(socket, "HostExists");
      return;
    }

    socket.serializeAttachment({ role: "host", token } satisfies SocketAttachment);
    this.send(socket, { type: "host", accepted: true });
  }

  private async registerParticipant(socket: WebSocket, participantId: string): Promise<void> {
    const rate = await this.ctx.storage.transaction(async (storage) => {
      const stored = await storage.get<number[]>(JOIN_TIMESTAMPS_KEY) ?? [];
      const result = isJoinAllowed(stored, Date.now());
      await storage.put(JOIN_TIMESTAMPS_KEY, result.timestamps);
      return result;
    });
    if (!rate.allowed) {
      this.sendRejectAndClose(socket, "RateLimited");
      return;
    }

    if (this.findHost() === undefined) {
      this.sendRejectAndClose(socket, "HostNotFound");
      return;
    }

    const participants = this.findParticipants();
    if (participants.some((entry) => entry.attachment.participantId === participantId)) {
      this.sendErrorAndClose(socket, "InvalidMessage");
      return;
    }

    if (participants.length >= MAX_GUESTS) {
      this.sendRejectAndClose(socket, "Capacity");
      return;
    }

    socket.serializeAttachment({ role: "participant", participantId } satisfies SocketAttachment);
  }

  private handleOffer(
    socket: WebSocket,
    attachment: Extract<SocketAttachment, { role: "participant" }>,
    message: Extract<ClientMessage, { type: "offer" }>,
  ): void {
    if (message.participantId !== attachment.participantId) {
      this.sendErrorAndClose(socket, "InvalidMessage");
      return;
    }

    const host = this.findHost();
    if (host === undefined) {
      this.sendRejectAndClose(socket, "HostNotFound");
      return;
    }

    this.send(host.socket, {
      type: "offer",
      participantId: attachment.participantId,
      payload: message.payload,
    });
  }

  private handleAnswer(
    socket: WebSocket,
    attachment: Extract<SocketAttachment, { role: "host" }>,
    message: Extract<ClientMessage, { type: "answer" }>,
  ): void {
    if (message.token !== attachment.token) {
      this.sendErrorAndClose(socket, "Unauthorized");
      return;
    }

    const participant = this.findParticipants().find(
      (entry) => entry.attachment.participantId === message.participantId,
    );
    if (participant === undefined) {
      return;
    }

    this.send(participant.socket, {
      type: "answer",
      participantId: message.participantId,
      payload: message.payload,
    });
    participant.socket.close(1000, "answer forwarded");
  }

  private async releaseSocket(socket: WebSocket): Promise<void> {
    const attachment = this.getAttachment(socket);
    if (attachment.role !== "host") {
      return;
    }

    for (const participant of this.findParticipants()) {
      participant.socket.close(1001, "host disconnected");
    }

    await this.ctx.storage.deleteAll();
  }

  private findHost():
    | { socket: WebSocket; attachment: Extract<SocketAttachment, { role: "host" }> }
    | undefined {
    for (const socket of this.ctx.getWebSockets()) {
      const attachment = this.getAttachment(socket);
      if (attachment.role === "host") {
        return { socket, attachment };
      }
    }

    return undefined;
  }

  private findParticipants(): Array<{
    socket: WebSocket;
    attachment: Extract<SocketAttachment, { role: "participant" }>;
  }> {
    const participants: Array<{
      socket: WebSocket;
      attachment: Extract<SocketAttachment, { role: "participant" }>;
    }> = [];
    for (const socket of this.ctx.getWebSockets()) {
      const attachment = this.getAttachment(socket);
      if (attachment.role === "participant") {
        participants.push({ socket, attachment });
      }
    }

    return participants;
  }

  private getAttachment(socket: WebSocket): SocketAttachment {
    return socket.deserializeAttachment() as SocketAttachment;
  }

  private sendRejectAndClose(socket: WebSocket, reason: RejectReason): void {
    this.send(socket, { type: "reject", reason });
    socket.close(1000, reason);
  }

  private sendErrorAndClose(socket: WebSocket, reason: ErrorReason): void {
    this.send(socket, { type: "error", reason });
    socket.close(1000, reason);
  }

  private send(socket: WebSocket, message: object): void {
    socket.send(JSON.stringify(message));
  }
}
