import { SELF } from "cloudflare:test";
import { describe, expect, it } from "vitest";

const roomId = "a".repeat(64);

describe("signaling worker routes", () => {
  it("should return health status when health endpoint is requested", async () => {
    // Given: The deployed worker entry point.
    // When: Health is requested.
    const response = await SELF.fetch("https://worker.test/health");

    // Then: A stable JSON success response is returned.
    expect(response.status).toBe(200);
    expect(await response.json()).toEqual({ status: "ok" });
    expect(response.headers.get("content-type")).toContain("application/json");
  });

  it("should reject invalid room id before durable object routing", async () => {
    // Given: A room path whose id is not 64 lowercase hexadecimal characters.
    // When: A WebSocket upgrade is requested.
    const response = await SELF.fetch("https://worker.test/rooms/INVALID", {
      headers: { Upgrade: "websocket" },
    });

    // Then: Input validation rejects it.
    expect(response.status).toBe(400);
  });

  it("should require WebSocket upgrade for a valid room", async () => {
    // Given: A syntactically valid room URL.
    // When: It is fetched without an Upgrade header.
    const response = await SELF.fetch(`https://worker.test/rooms/${roomId}`);

    // Then: The HTTP client is told to upgrade.
    expect(response.status).toBe(426);
  });

  it("should return not found for every unspecified route", async () => {
    // Given: A route outside the two public endpoints.
    // When: It is fetched.
    const response = await SELF.fetch("https://worker.test/other");

    // Then: No fallback endpoint is exposed.
    expect(response.status).toBe(404);
  });
});
