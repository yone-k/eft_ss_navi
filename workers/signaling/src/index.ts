import { RoomDurableObject } from "./room";

export interface Env {
  ROOMS: DurableObjectNamespace<RoomDurableObject>;
}

const roomPathPattern = /^\/rooms\/([0-9a-f]{64})$/;

export default {
  fetch(request: Request, env: Env): Promise<Response> | Response {
    const url = new URL(request.url);
    if (request.method === "GET" && url.pathname === "/health") {
      return Response.json({ status: "ok" });
    }

    if (request.method !== "GET" || !url.pathname.startsWith("/rooms/")) {
      return new Response(null, { status: 404 });
    }

    const match = roomPathPattern.exec(url.pathname);
    if (match === null) {
      return new Response(null, { status: 400 });
    }

    if (request.headers.get("Upgrade")?.toLowerCase() !== "websocket") {
      return new Response(null, { status: 426 });
    }

    const roomId = match[1];
    const id = env.ROOMS.idFromName(roomId);
    return env.ROOMS.get(id).fetch(request);
  },
} satisfies ExportedHandler<Env>;

export { RoomDurableObject };
