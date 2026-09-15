# Triply — Authentication API Contract

**Backend base URL (local dev):** `https://localhost:8080`


All endpoints below are under `/api/auth`.

---

## 1. Register

**Endpoint:** `POST /api/auth/register`

### Request body

| Field       | Type   | Required | Notes                                                                                                                              |
| ----------- | ------ | -------- | ---------------------------------------------------------------------------------------------------------------------------------- |
| email       | string | Yes      | Must be a valid and unused email                                                                                                   |
| password    | string | Yes      | Minimum 8 characters, including at least one uppercase letter, one lowercase letter, one digit, and one non-alphanumeric character |
| displayName | string | No       | Optional, maximum 100 characters                                                                                                   |

### Example request

```json
{
  "email": "leen.test2026@example.com",
  "password": "Test1234!",
  "displayName": "Leen Test"
}
```

### Success response — `200 OK`

```json
{
  "token": "<JWT_TOKEN>",
  "expiresAtUtc": "2026-09-14T17:01:57.1299752Z",
  "userId": "<USER_ID>",
  "email": "leen.test2026@example.com"
}
```

Register already returns a valid JWT, so no separate login call is required immediately after registration.

### Tested in Swagger

![Register](image.png)

### Error responses

**`400 Bad Request` — validation error or duplicate email**

Example:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Email": [
      "An account with this email already exists."
    ]
  },
  "traceId": "<TRACE_ID>"
}
```

Other validation errors, such as invalid email or weak password, use the same `400` response structure with an `errors` object.

Flutter should read the relevant field from `errors` when displaying validation messages.

### Duplicate email test

![Register already registered account](image-4.png)

---

## 2. Login

**Endpoint:** `POST /api/auth/login`

### Request body

| Field    | Type   | Required |
| -------- | ------ | -------- |
| email    | string | Yes      |
| password | string | Yes      |

### Example request

```json
{
  "email": "leen.test2026@example.com",
  "password": "Test1234!"
}
```

### Success response — `200 OK`

```json
{
  "token": "<JWT_TOKEN>",
  "expiresAtUtc": "2026-09-14T17:02:57.7489158Z",
  "userId": "<USER_ID>",
  "email": "leen.test2026@example.com"
}
```

The response has the same structure as Register.

### Tested in Swagger

![Login](image-1.png)

### Error responses

**`401 Unauthorized` — invalid email or password**

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Invalid email or password.",
  "status": 401,
  "traceId": "<TRACE_ID>"
}
```

The same generic message is returned for both an incorrect password and an email that does not exist.

Flutter should show one generic authentication error instead of trying to distinguish between the two cases.

### Wrong password test

![Wrong password](image-2.png)

### Invalid or non-existing email test

![Invalid email](image-5.png)

---

### `429 Too Many Requests` — rate limit exceeded

The login endpoint allows up to **5 failed attempts per minute**. After the limit is exceeded, the API returns `429 Too Many Requests`.

Flutter should handle `429` separately and show a message such as:

> Too many attempts. Please try again in a moment.

### Rate limit test

![Multiple login rate limit](image-3.png)

---

## 3. JWT Usage

Send the returned token with every authenticated request:

```http
Authorization: Bearer <JWT_TOKEN>
```

* **Token lifetime:** 60 minutes
* **Issuer:** `Triply`
* **Audience:** `TriplyClients`
* `expiresAtUtc` is returned by both Register and Login.

Flutter should use the returned `expiresAtUtc` value rather than hardcoding the expiration time.

---

## 4. Summary

| Endpoint | Status | Meaning                             |
| -------- | -----: | ----------------------------------- |
| Register |    200 | Account created and JWT returned    |
| Register |    400 | Validation error or duplicate email |
| Login    |    200 | Login successful and JWT returned   |
| Login    |    401 | Invalid email or password           |
| Login    |    429 | Too many login attempts             |

### Tested cases

* Successful registration
* Successful login
* Wrong password → `401`
* Invalid/non-existing email → `401`
* Multiple login attempts → `429`
* Registering an already registered account → `400`
* Invalid registration data → `400`
