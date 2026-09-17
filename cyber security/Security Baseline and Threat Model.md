## 1. Credential Handling

**Risk:** Password/session compromise leads to account takeover.

**Attack vectors:**
- Weak/plaintext password hashing → cracked in a DB breach
- JWTs stored in `localStorage` → stolen via XSS
- Long-lived tokens → wide attack window
- Missing `SameSite` → CSRF

**Controls:**
- Hash passwords with **Argon2id** (or bcrypt, cost ≥ 12) — no plaintext, no fast hashes
- Short-lived JWT access tokens (~15 min); refresh tokens rotated on use
- Tokens delivered via cookies with **`HttpOnly` + `Secure` + `SameSite`**
- Enforce HTTPS everywhere

---

## 2. Ownership Authorization (BOLA)

**Risk:** A logged-in user accesses or edits *another user's* data (OWASP API #1 risk).

**Attack vectors:**
- Changing a resource ID in the URL/body (IDOR)
- Relying only on frontend checks
- Accepting `user_id` from client input on create/update

**Controls:**
- Every resource endpoint checks **`resource.user_id == current_user.id`** server-side
- Ownership check centralized (middleware/guard), not duplicated per-route
- Never trust `user_id`/`owner_id` from request body — always derive from session
- Fail closed (403/404) on any doubt
- Prefer UUIDs over sequential IDs

---

## 3. Gemini Key Leakage

**Risk:** Leaked API key → financial loss, quota abuse, service disruption.

**Attack vectors:**
- Key bundled into frontend JS/mobile app
- Key hardcoded or committed to git
- Key printed in logs/error messages

**Controls:**
- Gemini key lives **only** in backend environment variables
- Backend acts as a **proxy** — frontend never calls Gemini directly
- `.env` in `.gitignore`; secret-scanning (e.g., `gitleaks`) runs in CI
- Redact secrets from logs
- Rotate key immediately if leaked

---

## 4. Rate-Limit Abuse

**Risk:** Brute force on auth, or cost-abuse on AI endpoints.

**Attack vectors:**
- Automated login attempts (credential stuffing)
- Rapid-fire requests to Gemini-backed endpoints (real $ cost)
- Unthrottled retries causing outage

**Controls:**
- **Redis-based** rate limiting (shared across instances)
- Strict limits on auth endpoints (login/register/reset) with backoff/lockout
- Separate, stricter limits on AI-generation endpoints
- Return `429 Too Many Requests` with `Retry-After`
- Limit per-user **and** per-IP
