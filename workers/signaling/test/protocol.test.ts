import { describe, expect, it } from "vitest";
import { isJoinAllowed, parseClientMessage } from "../src/protocol";

describe("signaling protocol", () => {
  it("should require a 32-byte Base64URL host token", () => {
    // Given: A host token that is not the encoded length of 32 bytes.
    const message = JSON.stringify({ type: "host", token: "too-short" });

    // When: The host registration is parsed.
    const result = parseClientMessage(message);

    // Then: The malformed ownership credential is rejected.
    expect(result).toEqual({ ok: false, reason: "InvalidMessage" });
  });

  it("should reject messages larger than 64 KiB", () => {
    // Given: A text frame one byte over the protocol limit.
    const oversized = "x".repeat(65_537);

    // When: The frame is parsed.
    const result = parseClientMessage(oversized);

    // Then: It is classified without attempting JSON handling.
    expect(result).toEqual({ ok: false, reason: "PayloadTooLarge" });
  });

  it("should allow only twenty joins inside a rolling minute", () => {
    // Given: Twenty joins occurred within the preceding sixty seconds.
    const now = 100_000;
    const recent = Array.from({ length: 20 }, (_, index) => now - 59_999 + index);

    // When: Another join is checked.
    const result = isJoinAllowed(recent, now);

    // Then: The twenty-first request is rate limited.
    expect(result.allowed).toBe(false);
  });

  it("should discard join timestamps at least sixty seconds old", () => {
    // Given: Every recorded join is outside the rolling window.
    const now = 100_000;
    const expired = Array.from({ length: 20 }, (_, index) => now - 60_000 - index);

    // When: A new join is checked.
    const result = isJoinAllowed(expired, now);

    // Then: The expired history no longer consumes capacity.
    expect(result).toEqual({ allowed: true, timestamps: [now] });
  });
});
