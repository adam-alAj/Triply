## Backend Security Checklist

**1. Credential Handling**
- [ ] Passwords hashed with Argon2id or bcrypt (never plaintext/reversible)
- [ ] JWT access tokens are short-lived
- [ ] Tokens set via `HttpOnly`, `Secure`, `SameSite` cookies — not `localStorage`

**2. Ownership Authorization (BOLA)**
- [ ] Every resource query checks `resource.user_id == current_user.id`
- [ ] No `user_id`/`owner_id` accepted from client input — always from session
- [ ] Cross-user access tested (or manually verified) for touched endpoints

**3. Gemini Key Protection**
- [ ] Gemini key only read from backend env vars
- [ ] No key in frontend code, logs, or committed files
- [ ] All Gemini calls proxied through backend — never called directly from client

**4. Rate Limiting**
- [ ] Auth endpoints (login/register/reset) are rate-limited
- [ ] AI-generation endpoints are rate-limited
- [ ] Limits enforced via Redis (shared across instances, not in-memory)
